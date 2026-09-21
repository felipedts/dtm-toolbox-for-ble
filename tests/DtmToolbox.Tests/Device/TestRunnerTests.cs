using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DtmToolbox.Device;
using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Device;

public class TestRunnerTests
{
    private const int Reset = 0x0000;
    private const int TestEnd = 0xC000;
    private const int StandardModulation = 0x0300;
    private const int Phy1M = 0x0204;

    [Fact]
    public async Task SingleChannelTransmitter_ConfiguresStartsAndEndsOnStop()
    {
        var link = new FakeLink { TransmitPowerResponse = new DtmFrame(0x0008) };
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(link), StopAfterWaits(stop, 1));
        var plan = new TestPlan { FirstChannel = 17, LastChannel = 17, PayloadLength = 37, TransmitPowerDbm = 4 };

        TestResult result = await runner.RunAsync(plan, null, stop.Token);

        Assert.Equal(
            new[] { Reset, 0x0904, 0x0100, StandardModulation, Phy1M, 0x9194, TestEnd },
            link.CommandValues);
        Assert.Equal(TestStopReason.Stopped, result.StopReason);
        Assert.Equal(4, result.Power!.Value.LevelDbm);
        Assert.Equal(0, result.TotalPackets);
    }

    [Fact]
    public async Task SingleChannel_WaitsForTheStopInsteadOfADwellTime()
    {
        var waits = new List<int>();
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(new FakeLink()), (milliseconds, token) =>
        {
            waits.Add(milliseconds);
            stop.Cancel();
            return Task.CompletedTask;
        });

        await runner.RunAsync(new TestPlan { Mode = TestMode.Receiver, DwellTimeMs = 30 }, null, stop.Token);

        Assert.Equal(new[] { Timeout.Infinite }, waits);
    }

    [Fact]
    public async Task ReceiverSweep_WalksTheRangeInALoopAndAddsThePacketsOfEachChannel()
    {
        var link = new FakeLink();
        foreach (int count in new[] { 10, 20, 30, 1, 2 })
        {
            link.PacketCounts.Enqueue(count);
        }

        var progress = new RecordingProgress();
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(link), StopAfterWaits(stop, 5));
        var plan = new TestPlan { Mode = TestMode.Receiver, FirstChannel = 0, LastChannel = 2, DwellTimeMs = 50 };

        TestResult result = await runner.RunAsync(plan, progress, stop.Token);

        Assert.Equal(
            new[] { Reset, StandardModulation, Phy1M, 0x4000, TestEnd, 0x4100, TestEnd, 0x4200, TestEnd, 0x4000, TestEnd, 0x4100, TestEnd },
            link.CommandValues);
        Assert.Equal(11, result.PacketsPerChannel[0]);
        Assert.Equal(22, result.PacketsPerChannel[1]);
        Assert.Equal(30, result.PacketsPerChannel[2]);
        Assert.Equal(63, result.TotalPackets);
        Assert.Equal(new[] { 0, 1, 2, 0, 1 }, progress.Ended.Select(item => item.Channel));
        Assert.Equal(new[] { 10, 20, 30, 1, 2 }, progress.Ended.Select(item => item.Packets));
    }

    [Fact]
    public async Task Receiver_DoesNotSendTransmitterSettings()
    {
        var link = new FakeLink();
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(link), StopAfterWaits(stop, 1));

        await runner.RunAsync(new TestPlan { Mode = TestMode.Receiver, FirstChannel = 5, LastChannel = 5 }, null, stop.Token);

        Assert.Equal(new[] { Reset, StandardModulation, Phy1M, 0x4500, TestEnd }, link.CommandValues);
    }

    [Fact]
    public async Task ConstantCarrier_SendsTheVendorCommand()
    {
        var link = new FakeLink();
        using var stop = new CancellationTokenSource();
        var runner = new TestRunner(new DtmDevice(link), StopAfterWaits(stop, 1));
        var plan = new TestPlan { ConstantCarrier = true, FirstChannel = 17, LastChannel = 17, TransmitPowerDbm = null };

        await runner.RunAsync(plan, null, stop.Token);

        Assert.Contains(0x9103, link.CommandValues);
        Assert.Equal(TestEnd, link.CommandValues.Last());
    }

    [Fact]
    public async Task Timeout_EndsTheRunWithoutAStopRequest()
    {
        var link = new FakeLink();
        var runner = new TestRunner(new DtmDevice(link));

        TestResult result = await runner.RunAsync(new TestPlan { TimeoutMs = 50 }, null, CancellationToken.None);

        Assert.Equal(TestStopReason.TimedOut, result.StopReason);
        Assert.Equal(TestEnd, link.CommandValues.Last());
    }

    [Fact]
    public async Task RejectedStart_FailsTheRunWithoutATestEnd()
    {
        var link = new FakeLink { RejectedCommand = DtmCommand.ReceiverTest(7) };
        var runner = new TestRunner(new DtmDevice(link));
        var plan = new TestPlan { Mode = TestMode.Receiver, FirstChannel = 7, LastChannel = 7 };

        await Assert.ThrowsAsync<DtmCommandRejectedException>(() => runner.RunAsync(plan, null, CancellationToken.None));

        Assert.DoesNotContain(TestEnd, link.CommandValues);
    }

    [Fact]
    public async Task StopRequestedBeforeTheRun_StartsNoTest()
    {
        var link = new FakeLink();
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        TestResult result = await new TestRunner(new DtmDevice(link)).RunAsync(new TestPlan(), null, stop.Token);

        Assert.Equal(TestStopReason.Stopped, result.StopReason);
        Assert.DoesNotContain(TestEnd, link.CommandValues);
    }

    [Fact]
    public async Task InvalidPlan_IsRejectedBeforeAnyCommand()
    {
        var link = new FakeLink();
        var runner = new TestRunner(new DtmDevice(link));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(new TestPlan { FirstChannel = 10, LastChannel = 5 }, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(new TestPlan { ConstantCarrier = true, Phy = Phy.LeCodedS8 }, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(new TestPlan { PacketType = PacketType.Pattern11111111 }, null, CancellationToken.None));

        Assert.Empty(link.Commands);
    }

    // A delay that returns at once and cancels the run on its n-th call.
    private static Func<int, CancellationToken, Task> StopAfterWaits(CancellationTokenSource stop, int waits)
    {
        int calls = 0;
        return (milliseconds, token) =>
        {
            if (Interlocked.Increment(ref calls) >= waits)
            {
                stop.Cancel();
            }

            return Task.CompletedTask;
        };
    }

    // Progress<T> posts to the thread pool, which would reorder the notifications.
    private sealed class RecordingProgress : IProgress<TestProgress>
    {
        private readonly object _sync = new object();
        private readonly List<TestProgress> _items = new List<TestProgress>();

        public IReadOnlyList<TestProgress> Ended
        {
            get
            {
                lock (_sync)
                {
                    return _items.Where(item => item.Kind == TestProgressKind.ChannelEnded).ToList();
                }
            }
        }

        public void Report(TestProgress value)
        {
            lock (_sync)
            {
                _items.Add(value);
            }
        }
    }
}
