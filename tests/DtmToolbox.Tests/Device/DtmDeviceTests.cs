using System.Collections.Generic;
using DtmToolbox.Device;
using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Device;

public class DtmDeviceTests
{
    [Fact]
    public void SetTransmitPower_ReturnsTheLevelTheDeviceApplied()
    {
        var link = new FakeLink { TransmitPowerResponse = new DtmFrame(0x0410) };
        var device = new DtmDevice(link);

        TransmitPowerReport report = device.SetTransmitPower(20);

        Assert.Equal(8, report.LevelDbm);
        Assert.True(report.AtMaximum);
    }

    [Fact]
    public void EndTest_ReturnsThePacketCountOfTheReport()
    {
        var link = new FakeLink();
        link.PacketCounts.Enqueue(1500);
        var device = new DtmDevice(link);

        Assert.Equal(1500, device.EndTest());
    }

    [Fact]
    public void RejectedCommand_ThrowsWithTheCommandAndTheResponse()
    {
        var link = new FakeLink { RejectedCommand = DtmCommand.SetPhy(Phy.LeCodedS8) };
        var device = new DtmDevice(link);

        var exception = Assert.Throws<DtmCommandRejectedException>(() => device.SetPhy(Phy.LeCodedS8));

        Assert.Equal(DtmCommand.SetPhy(Phy.LeCodedS8), exception.Command);
        Assert.True(exception.Response.IsError);
    }

    [Fact]
    public void ReadMaxSupported_ConvertsTimesToMicroseconds()
    {
        var device = new DtmDevice(new ConstantResponseLink(new DtmFrame(0x4290)));

        Assert.Equal(17040, device.ReadMaxSupported(MaxSupportedValue.TxTime));
        Assert.Equal(8520, device.ReadMaxSupported(MaxSupportedValue.TxOctets));
    }

    [Fact]
    public void FrameExchanged_ReportsSuccessfulRejectedAndUnansweredCommands()
    {
        var exchanges = new List<FrameExchangedEventArgs>();

        var accepted = new DtmDevice(new FakeLink());
        accepted.FrameExchanged += (sender, e) => exchanges.Add(e);
        accepted.Reset();

        var silent = new DtmDevice(new SilentLink());
        silent.FrameExchanged += (sender, e) => exchanges.Add(e);
        Assert.Throws<DtmTimeoutException>(() => silent.Reset());

        Assert.Equal(2, exchanges.Count);
        Assert.Null(exchanges[0].Failure);
        Assert.NotNull(exchanges[0].Response);
        Assert.IsType<DtmTimeoutException>(exchanges[1].Failure);
        Assert.Null(exchanges[1].Response);
    }

    [Fact]
    public void Dispose_ClosesTheLink()
    {
        var link = new FakeLink();

        new DtmDevice(link).Dispose();

        Assert.True(link.Disposed);
    }

    private sealed class ConstantResponseLink : IDtmLink
    {
        private readonly DtmFrame _response;

        public ConstantResponseLink(DtmFrame response)
        {
            _response = response;
        }

        public DtmEvent Exchange(DtmFrame command) => new DtmEvent(_response);

        public void Dispose()
        {
        }
    }
}
