using DtmToolbox.Device;
using DtmToolbox.Protocol;
using DtmToolbox.ViewModels;

namespace DtmToolbox.Settings;

/// <summary>What the application remembers between runs.</summary>
public sealed class AppSettings
{
    public string? PortName { get; set; }

    public int BaudRate { get; set; } = SerialDtmLink.DefaultBaudRate;

    public VendorProfile VendorProfile { get; set; } = VendorProfile.NordicNrf5x;

    public bool IsSweep { get; set; }

    public int Channel { get; set; } = 19;

    public int SweepFirstChannel { get; set; } = DtmChannel.Min;

    public int SweepLastChannel { get; set; } = DtmChannel.Max;

    public int DwellTimeMs { get; set; } = 30;

    public int TransmitPowerDbm { get; set; }

    public Phy Phy { get; set; } = Phy.Le1M;

    public Payload Payload { get; set; } = Payload.Prbs9;

    public int PayloadLength { get; set; } = 37;

    public int TimeoutSeconds { get; set; }

    public bool ShowLog { get; set; } = true;

    public bool AutoScrollLog { get; set; } = true;

    public bool ShowRawFrames { get; set; }
}
