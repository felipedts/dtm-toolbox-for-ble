using System.Linq;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

public enum TestStopReason
{
    /// <summary>The caller asked the run to stop.</summary>
    Stopped,

    /// <summary>The timeout of the plan elapsed.</summary>
    TimedOut,
}

/// <summary>Outcome of a finished test run.</summary>
public sealed class TestResult
{
    public TestResult(TestMode mode, TestStopReason stopReason, int[] packetsPerChannel, TransmitPowerReport? power)
    {
        Mode = mode;
        StopReason = stopReason;
        PacketsPerChannel = packetsPerChannel;
        Power = power;
    }

    public TestMode Mode { get; }

    public TestStopReason StopReason { get; }

    /// <summary>Packets received on each RF channel, index 0 to 39. All zero after a transmitter test.</summary>
    public int[] PacketsPerChannel { get; }

    public int TotalPackets => PacketsPerChannel.Sum();

    /// <summary>Transmit power the device applied. Null when the plan did not request one.</summary>
    public TransmitPowerReport? Power { get; }
}
