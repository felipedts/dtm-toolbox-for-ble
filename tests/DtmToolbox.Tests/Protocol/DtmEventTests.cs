using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Protocol;

public class DtmEventTests
{
    [Fact]
    public void StatusEvent_WithoutErrorFlag_IsSuccess()
    {
        var statusEvent = new DtmEvent(new DtmFrame(0x00, 0x00));

        Assert.False(statusEvent.IsPacketReport);
        Assert.False(statusEvent.IsError);
        Assert.Equal(0, statusEvent.PacketCount);
    }

    [Fact]
    public void StatusEvent_WithErrorFlag_IsError()
    {
        var statusEvent = new DtmEvent(new DtmFrame(0x00, 0x01));

        Assert.True(statusEvent.IsError);
    }

    [Theory]
    [InlineData(0x80, 0x00, 0)]
    [InlineData(0x85, 0xDC, 1500)]
    [InlineData(0xFF, 0xFF, 32767)]
    public void PacketReport_CarriesTheCountInBits14To0(int high, int low, int expected)
    {
        var report = new DtmEvent(new DtmFrame((byte)high, (byte)low));

        Assert.True(report.IsPacketReport);
        Assert.False(report.IsError);
        Assert.Equal(expected, report.PacketCount);
    }

    [Fact]
    public void PacketReport_WithAnOddCount_IsNotAnError()
    {
        var report = new DtmEvent(new DtmFrame(0x80, 0x01));

        Assert.False(report.IsError);
        Assert.Equal(1, report.PacketCount);
    }

    [Fact]
    public void Response_IsBits14To1OfAStatusEvent()
    {
        var statusEvent = new DtmEvent(new DtmFrame(0x00, 0x1E));

        Assert.Equal(0x0F, statusEvent.Response);
        Assert.Equal(
            DtmFeatures.DataLengthExtension | DtmFeatures.Le2MPhy | DtmFeatures.StableModulationIndex | DtmFeatures.LeCodedPhy,
            (DtmFeatures)statusEvent.Response);
    }

    [Fact]
    public void TransmitPowerReport_ReadsAPositiveLevelAtTheDeviceMaximum()
    {
        var report = new TransmitPowerReport(new DtmEvent(new DtmFrame(0x04, 0x10)).Response);

        Assert.Equal(8, report.LevelDbm);
        Assert.True(report.AtMaximum);
        Assert.False(report.AtMinimum);
    }

    [Fact]
    public void TransmitPowerReport_ReadsANegativeLevelAtTheDeviceMinimum()
    {
        var report = new TransmitPowerReport(new DtmEvent(new DtmFrame(0x03, 0xB0)).Response);

        Assert.Equal(-40, report.LevelDbm);
        Assert.True(report.AtMinimum);
        Assert.False(report.AtMaximum);
    }
}
