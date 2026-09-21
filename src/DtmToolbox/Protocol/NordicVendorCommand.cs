using System;

namespace DtmToolbox.Protocol;

/// <summary>
/// Vendor commands of the Nordic Semiconductor nRF5x DTM firmware. They travel as a transmitter
/// test command with packet type 3 on the LE 1M or LE 2M PHY: the length field selects the
/// vendor command and the channel field carries its argument.
/// </summary>
public static class NordicVendorCommand
{
    /// <summary>Lowest level the 6-bit argument of the transmit power command can carry.</summary>
    public const int MinTransmitPowerDbm = -48;

    /// <summary>Highest level the 6-bit argument of the transmit power command can carry.</summary>
    public const int MaxTransmitPowerDbm = 15;

    private const int VendorPacketType = 3;
    private const int ConstantCarrierCommand = 0;
    private const int SetTransmitPowerCommand = 2;

    /// <summary>Unmodulated carrier on the given channel, until the test is ended.</summary>
    public static DtmFrame ConstantCarrier(int channel)
    {
        DtmChannel.Check(channel);
        return Build(ConstantCarrierCommand, channel);
    }

    /// <summary>
    /// Sets the transmit power on firmware that predates the specification command (setup 0x09).
    /// The level travels in the six bits of the channel field. The firmware reads it as negative
    /// when bit 5 or bit 4 is set, and accepts only the levels its radio has.
    /// </summary>
    public static DtmFrame SetTransmitPower(int dbm)
    {
        if (dbm < MinTransmitPowerDbm || dbm > MaxTransmitPowerDbm)
        {
            throw new ArgumentOutOfRangeException(nameof(dbm), dbm, "The vendor transmit power command carries levels from -48 to +15 dBm.");
        }

        return Build(SetTransmitPowerCommand, dbm & 0x3F);
    }

    private static DtmFrame Build(int vendorCommand, int argument) =>
        DtmCommand.Build(DtmCommandCode.TransmitterTest, argument, (byte)((vendorCommand << 2) | VendorPacketType));
}
