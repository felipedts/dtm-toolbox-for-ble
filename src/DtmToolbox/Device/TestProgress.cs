using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

public enum TestProgressKind
{
    /// <summary>The device answered the transmit power request.</summary>
    PowerApplied,

    /// <summary>A test started on <see cref="TestProgress.Channel"/>.</summary>
    ChannelStarted,

    /// <summary>The test on <see cref="TestProgress.Channel"/> ended.</summary>
    ChannelEnded,
}

/// <summary>Progress notification of a running test.</summary>
public sealed class TestProgress
{
    private TestProgress(TestProgressKind kind, int channel, int packets, TransmitPowerReport? power)
    {
        Kind = kind;
        Channel = channel;
        Packets = packets;
        Power = power;
    }

    public TestProgressKind Kind { get; }

    /// <summary>RF channel, 0 to 39. Not meaningful for <see cref="TestProgressKind.PowerApplied"/>.</summary>
    public int Channel { get; }

    /// <summary>Packets received on the channel, for <see cref="TestProgressKind.ChannelEnded"/> of a receiver test.</summary>
    public int Packets { get; }

    /// <summary>Set for <see cref="TestProgressKind.PowerApplied"/>.</summary>
    public TransmitPowerReport? Power { get; }

    public static TestProgress PowerApplied(TransmitPowerReport power) =>
        new TestProgress(TestProgressKind.PowerApplied, 0, 0, power);

    public static TestProgress ChannelStarted(int channel) =>
        new TestProgress(TestProgressKind.ChannelStarted, channel, 0, null);

    public static TestProgress ChannelEnded(int channel, int packets) =>
        new TestProgress(TestProgressKind.ChannelEnded, channel, packets, null);
}
