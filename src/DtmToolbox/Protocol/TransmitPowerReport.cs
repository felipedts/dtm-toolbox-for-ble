namespace DtmToolbox.Protocol;

/// <summary>
/// The transmit power a device applied. A device that does not support the requested level
/// applies the nearest one it has, so the level reported here can differ from the request.
/// </summary>
public readonly struct TransmitPowerReport
{
    private const int LevelMask = 0xFF;
    private const int AtMinimumFlag = 1 << 8;
    private const int AtMaximumFlag = 1 << 9;

    public TransmitPowerReport(int levelDbm, bool atMinimum, bool atMaximum, bool fromVendorCommand)
    {
        LevelDbm = levelDbm;
        AtMinimum = atMinimum;
        AtMaximum = atMaximum;
        FromVendorCommand = fromVendorCommand;
    }

    /// <summary>Level the device applied, in dBm.</summary>
    public int LevelDbm { get; }

    /// <summary>The level is the lowest the device has. Known only from the specification command.</summary>
    public bool AtMinimum { get; }

    /// <summary>The level is the highest the device has. Known only from the specification command.</summary>
    public bool AtMaximum { get; }

    /// <summary>The level was set with the Nordic vendor command because the device rejected setup 0x09.</summary>
    public bool FromVendorCommand { get; }

    /// <summary>Reads the response to a "set transmit power" setup command.</summary>
    /// <param name="response">Response field of the status event, see <see cref="DtmEvent.Response"/>.</param>
    public static TransmitPowerReport FromResponse(int response) =>
        new TransmitPowerReport(
            unchecked((sbyte)(response & LevelMask)),
            (response & AtMinimumFlag) != 0,
            (response & AtMaximumFlag) != 0,
            fromVendorCommand: false);

    /// <summary>The level a vendor command set. The vendor command accepts a level or rejects it, with no read-back.</summary>
    public static TransmitPowerReport FromVendorLevel(int levelDbm) =>
        new TransmitPowerReport(levelDbm, atMinimum: false, atMaximum: false, fromVendorCommand: true);
}
