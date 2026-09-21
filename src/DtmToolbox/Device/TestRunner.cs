using System;
using System.Threading;
using System.Threading.Tasks;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>
/// Runs a <see cref="TestPlan"/> on a device: configures it, then keeps one test running on a
/// single channel or moves across a channel range, until it is stopped or the plan times out.
/// </summary>
/// <remarks>
/// A receiver test on a single channel runs as one uninterrupted test and reports its packet
/// count when it ends. Restarting it to refresh the count would miss the packets sent during
/// each restart, which corrupts a packet error rate measured against a fixed number of packets.
/// </remarks>
public sealed class TestRunner
{
    private readonly DtmDevice _device;
    private readonly Func<int, CancellationToken, Task> _delay;

    public TestRunner(DtmDevice device)
        : this(device, (milliseconds, token) => Task.Delay(milliseconds, token))
    {
    }

    /// <param name="device">Device to drive.</param>
    /// <param name="delay">Waits the given milliseconds, or until the token is cancelled. -1 waits for the token only.</param>
    public TestRunner(DtmDevice device, Func<int, CancellationToken, Task> delay)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    /// <summary>
    /// Runs the plan on a worker thread. Completes after <paramref name="stop"/> is cancelled or
    /// the plan times out, with the running test ended on the device.
    /// </summary>
    /// <exception cref="ArgumentException">The plan is not valid.</exception>
    /// <exception cref="DtmException">The device rejected a command, did not answer, or the link failed.</exception>
    public Task<TestResult> RunAsync(TestPlan plan, IProgress<TestProgress>? progress, CancellationToken stop)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        plan.Validate();
        return Task.Run(() => RunCoreAsync(plan, progress, stop));
    }

    private async Task<TestResult> RunCoreAsync(TestPlan plan, IProgress<TestProgress>? progress, CancellationToken stop)
    {
        TransmitPowerReport? power = Configure(plan, progress);
        int[] packets = new int[DtmChannel.Count];

        using var timeout = new CancellationTokenSource();
        using var ending = CancellationTokenSource.CreateLinkedTokenSource(stop, timeout.Token);
        if (plan.TimeoutMs > 0)
        {
            timeout.CancelAfter(plan.TimeoutMs);
        }

        int channel = plan.FirstChannel;
        while (!ending.IsCancellationRequested)
        {
            StartTest(plan, channel);
            progress?.Report(TestProgress.ChannelStarted(channel));

            int received;
            try
            {
                await WaitAsync(plan.IsSweep ? plan.DwellTimeMs : Timeout.Infinite, ending.Token).ConfigureAwait(false);
            }
            finally
            {
                // The device keeps transmitting or receiving until told to stop, so the test is
                // ended even when the wait fails.
                received = _device.EndTest();
            }

            packets[channel] += received;
            progress?.Report(TestProgress.ChannelEnded(channel, received));

            channel = channel < plan.LastChannel ? channel + 1 : plan.FirstChannel;
        }

        TestStopReason reason = stop.IsCancellationRequested ? TestStopReason.Stopped : TestStopReason.TimedOut;
        return new TestResult(plan.Mode, reason, packets, power);
    }

    private TransmitPowerReport? Configure(TestPlan plan, IProgress<TestProgress>? progress)
    {
        TransmitPowerReport? power = null;

        _device.Reset();
        if (plan.Mode == TestMode.Transmitter)
        {
            if (plan.TransmitPowerDbm is int dbm)
            {
                power = _device.SetTransmitPower(dbm);
                progress?.Report(TestProgress.PowerApplied(power.Value));
            }

            _device.SetPayloadLength(plan.PayloadLength);
        }

        _device.SetModulationIndex(plan.ModulationIndex);
        _device.SetPhy(plan.Phy);
        return power;
    }

    private void StartTest(TestPlan plan, int channel)
    {
        if (plan.Mode == TestMode.Receiver)
        {
            _device.StartReceiver(channel);
        }
        else if (plan.ConstantCarrier)
        {
            _device.StartConstantCarrier(channel);
        }
        else
        {
            _device.StartTransmitter(channel, plan.PayloadLength, plan.PacketType);
        }
    }

    private async Task WaitAsync(int milliseconds, CancellationToken token)
    {
        try
        {
            await _delay(milliseconds, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is how a wait ends early. The caller checks the token.
        }
    }
}
