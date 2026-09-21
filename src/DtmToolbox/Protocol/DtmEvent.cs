namespace DtmToolbox.Protocol;

/// <summary>
/// The event a device answers a command with. Bit 15 set: packet report, bits 14-0 hold the
/// number of packets received. Bit 15 clear: status event, bit 0 is the error flag and
/// bits 14-1 the response to the setup command, when it has one.
/// </summary>
public readonly struct DtmEvent
{
    private const int PacketReportFlag = 0x8000;
    private const int PacketCountMask = 0x7FFF;
    private const int ErrorFlag = 0x0001;
    private const int ResponseMask = 0x3FFF;

    public DtmEvent(DtmFrame frame)
    {
        Frame = frame;
    }

    public DtmFrame Frame { get; }

    public bool IsPacketReport => (Frame.Value & PacketReportFlag) != 0;

    /// <summary>Packets received during the test that was ended. Zero for a status event.</summary>
    public int PacketCount => IsPacketReport ? Frame.Value & PacketCountMask : 0;

    /// <summary>True for a status event that reports an error.</summary>
    public bool IsError => !IsPacketReport && (Frame.Value & ErrorFlag) != 0;

    /// <summary>Response field of a status event, bits 14-1.</summary>
    public int Response => IsPacketReport ? 0 : (Frame.Value >> 1) & ResponseMask;

    public override string ToString() => Frame.ToString();
}
