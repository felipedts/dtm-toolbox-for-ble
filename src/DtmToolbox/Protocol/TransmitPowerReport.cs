namespace DtmToolbox.Protocol;

/// <summary>
/// Response to a "set transmit power" command. A device that does not support the requested
/// level applies the nearest one it has, so the level reported here can differ from the request.
/// </summary>
public readonly struct TransmitPowerReport
{
    private const int LevelMask = 0xFF;
    private const int AtMinimumFlag = 1 << 8;
    private const int AtMaximumFlag = 1 << 9;

    /// <param name="response">Response field of the status event, see <see cref="DtmEvent.Response"/>.</param>
    public TransmitPowerReport(int response)
    {
        LevelDbm = unchecked((sbyte)(response & LevelMask));
        AtMinimum = (response & AtMinimumFlag) != 0;
        AtMaximum = (response & AtMaximumFlag) != 0;
    }

    /// <summary>Level the device applied, in dBm.</summary>
    public int LevelDbm { get; }

    public bool AtMinimum { get; }

    public bool AtMaximum { get; }
}
