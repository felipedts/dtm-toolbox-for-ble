using System.Linq;
using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Protocol;

public class DtmChannelTests
{
    [Theory]
    [InlineData(0, 2402)]
    [InlineData(19, 2440)]
    [InlineData(39, 2480)]
    public void FrequencyMhz_StartsAt2402AndStepsBy2(int channel, int expected)
    {
        Assert.Equal(expected, DtmChannel.FrequencyMhz(channel));
    }

    [Theory]
    [InlineData(0, 37)]
    [InlineData(1, 0)]
    [InlineData(11, 10)]
    [InlineData(12, 38)]
    [InlineData(13, 11)]
    [InlineData(38, 36)]
    [InlineData(39, 39)]
    public void ToLinkLayerIndex_PlacesTheAdvertisingChannelsAt2402And2426And2480(int channel, int expected)
    {
        Assert.Equal(expected, DtmChannel.ToLinkLayerIndex(channel));
    }

    [Fact]
    public void LinkLayerIndex_MapsBackToTheSameChannel()
    {
        foreach (int channel in Enumerable.Range(DtmChannel.Min, DtmChannel.Count))
        {
            Assert.Equal(channel, DtmChannel.FromLinkLayerIndex(DtmChannel.ToLinkLayerIndex(channel)));
        }
    }
}
