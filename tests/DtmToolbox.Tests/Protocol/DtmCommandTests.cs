using System;
using DtmToolbox.Protocol;
using Xunit;

namespace DtmToolbox.Tests.Protocol;

public class DtmCommandTests
{
    [Fact]
    public void Reset_IsAllZeros()
    {
        Assert.Equal(0x0000, DtmCommand.Reset().Value);
    }

    [Fact]
    public void TestEnd_HasOnlyTheCommandCode()
    {
        Assert.Equal(0xC000, DtmCommand.TestEnd().Value);
    }

    [Theory]
    [InlineData(37, 0x0100)]
    [InlineData(63, 0x0100)]
    [InlineData(64, 0x0104)]
    [InlineData(255, 0x010C)]
    public void SetUpperLengthBits_PutsBits7And6OfTheLengthInParameterBits3And2(int payloadLength, int expected)
    {
        Assert.Equal(expected, DtmCommand.SetUpperLengthBits(payloadLength).Value);
    }

    [Theory]
    [InlineData(Phy.Le1M, 0x0204)]
    [InlineData(Phy.Le2M, 0x0208)]
    [InlineData(Phy.LeCodedS8, 0x020C)]
    [InlineData(Phy.LeCodedS2, 0x0210)]
    public void SetPhy_UsesTheParameterValuesOfTheSpecification(Phy phy, int expected)
    {
        Assert.Equal(expected, DtmCommand.SetPhy(phy).Value);
    }

    [Theory]
    [InlineData(ModulationIndex.Standard, 0x0300)]
    [InlineData(ModulationIndex.Stable, 0x0304)]
    public void SetModulationIndex_Encodes(ModulationIndex index, int expected)
    {
        Assert.Equal(expected, DtmCommand.SetModulationIndex(index).Value);
    }

    [Fact]
    public void ReadFeatures_Encodes()
    {
        Assert.Equal(0x0400, DtmCommand.ReadFeatures().Value);
    }

    [Theory]
    [InlineData(MaxSupportedValue.TxOctets, 0x0500)]
    [InlineData(MaxSupportedValue.TxTime, 0x0504)]
    [InlineData(MaxSupportedValue.RxOctets, 0x0508)]
    [InlineData(MaxSupportedValue.RxTime, 0x050C)]
    [InlineData(MaxSupportedValue.CteLength, 0x0510)]
    public void ReadMaxSupported_Encodes(MaxSupportedValue value, int expected)
    {
        Assert.Equal(expected, DtmCommand.ReadMaxSupported(value).Value);
    }

    [Theory]
    [InlineData(0, 0x0900)]
    [InlineData(8, 0x0908)]
    [InlineData(20, 0x0914)]
    [InlineData(-4, 0x09FC)]
    [InlineData(-40, 0x09D8)]
    [InlineData(-127, 0x0981)]
    public void SetTransmitPower_SendsTheLevelAsASigned8BitParameter(int dbm, int expected)
    {
        Assert.Equal(expected, DtmCommand.SetTransmitPower(dbm).Value);
    }

    [Fact]
    public void SetTransmitPower_HasParametersForTheDeviceLimits()
    {
        Assert.Equal(0x097E, DtmCommand.SetTransmitPowerToMinimum().Value);
        Assert.Equal(0x097F, DtmCommand.SetTransmitPowerToMaximum().Value);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(-128)]
    public void SetTransmitPower_RejectsLevelsOutsideTheSpecification(int dbm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DtmCommand.SetTransmitPower(dbm));
    }

    [Theory]
    [InlineData(0, 37, PacketType.Prbs9, 0x8094)]
    [InlineData(17, 37, PacketType.Pattern11110000, 0x9195)]
    [InlineData(39, 255, PacketType.Pattern10101010, 0xA7FE)]
    [InlineData(19, 0, PacketType.Pattern11111111, 0x9303)]
    public void TransmitterTest_Encodes(int channel, int payloadLength, PacketType packetType, int expected)
    {
        Assert.Equal(expected, DtmCommand.TransmitterTest(channel, payloadLength, packetType).Value);
    }

    [Theory]
    [InlineData(0, 0x4000)]
    [InlineData(19, 0x5300)]
    [InlineData(39, 0x6700)]
    public void ReceiverTest_CarriesOnlyTheChannel(int channel, int expected)
    {
        Assert.Equal(expected, DtmCommand.ReceiverTest(channel).Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(40)]
    public void TestCommands_RejectChannelsOutsideTheBand(int channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DtmCommand.ReceiverTest(channel));
        Assert.Throws<ArgumentOutOfRangeException>(() => DtmCommand.TransmitterTest(channel, 37, PacketType.Prbs9));
    }

    [Fact]
    public void TransmitterTest_RejectsLengthsAbove255()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DtmCommand.TransmitterTest(0, 256, PacketType.Prbs9));
    }

    [Fact]
    public void NordicConstantCarrier_IsATransmitterCommandWithVendorPacketTypeAndCommandZero()
    {
        Assert.Equal(0x9103, NordicVendorCommand.ConstantCarrier(17).Value);
    }

    [Fact]
    public void Frame_GoesOnTheWireMostSignificantByteFirst()
    {
        DtmFrame frame = DtmCommand.TransmitterTest(0, 37, PacketType.Prbs9);

        Assert.Equal(new byte[] { 0x80, 0x94 }, frame.ToBytes());
        Assert.Equal("80 94", frame.ToString());
    }
}
