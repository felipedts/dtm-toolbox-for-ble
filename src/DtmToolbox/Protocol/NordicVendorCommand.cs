namespace DtmToolbox.Protocol;

/// <summary>
/// Vendor commands of the Nordic Semiconductor nRF5x DTM firmware. They travel as a transmitter
/// test command with packet type 3 on the LE 1M or LE 2M PHY: the length field selects the
/// vendor command and the channel field carries its argument.
/// </summary>
public static class NordicVendorCommand
{
    private const int VendorPacketType = 3;
    private const int ConstantCarrierCommand = 0;

    /// <summary>Unmodulated carrier on the given channel, until the test is ended.</summary>
    public static DtmFrame ConstantCarrier(int channel)
    {
        DtmChannel.Check(channel);
        return DtmCommand.Build(DtmCommandCode.TransmitterTest, channel, (byte)((ConstantCarrierCommand << 2) | VendorPacketType));
    }
}
