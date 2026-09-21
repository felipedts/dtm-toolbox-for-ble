using System;
using System.IO;
using System.Linq;
using DtmToolbox.Protocol;
using DtmToolbox.Settings;
using DtmToolbox.ViewModels;
using Xunit;

namespace DtmToolbox.Tests.ViewModels;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "DtmToolbox.Tests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Defaults_MatchTheMiddleOfTheBandAndTheStandardPacket()
    {
        using MainViewModel viewModel = Create();

        Assert.Equal(19, viewModel.Channel);
        Assert.Equal(17, viewModel.ChannelIndex);
        Assert.Equal("2440 MHz", viewModel.ChannelFrequencyText);
        Assert.Equal(19200, viewModel.BaudRate);
        Assert.Equal(37, viewModel.PayloadLength);
        Assert.Equal(Payload.Prbs9, viewModel.SelectedPayload.Value);
        Assert.True(viewModel.IsSingle);
        Assert.True(viewModel.HasNoTimeout);
    }

    [Theory]
    [InlineData(37, 0)]
    [InlineData(0, 1)]
    [InlineData(38, 12)]
    [InlineData(39, 39)]
    public void ChannelIndex_SetsTheRfChannelOfThatLinkLayerIndex(int index, int expectedChannel)
    {
        using MainViewModel viewModel = Create();

        viewModel.ChannelIndex = index;

        Assert.Equal(expectedChannel, viewModel.Channel);
        Assert.Equal(index, viewModel.ChannelIndex);
    }

    [Fact]
    public void Values_AreKeptInsideTheirRanges()
    {
        using MainViewModel viewModel = Create();

        viewModel.Channel = 99;
        viewModel.PayloadLength = 0;
        viewModel.TransmitPowerDbm = 50;
        viewModel.DwellTimeMs = 1;
        viewModel.TimeoutSeconds = 99;

        Assert.Equal(39, viewModel.Channel);
        Assert.Equal(1, viewModel.PayloadLength);
        Assert.Equal(20, viewModel.TransmitPowerDbm);
        Assert.Equal(20, viewModel.DwellTimeMs);
        Assert.Equal(20, viewModel.TimeoutSeconds);
        Assert.True(viewModel.HasTimeout);
    }

    [Fact]
    public void SweepRange_NeverInverts()
    {
        using MainViewModel viewModel = Create();
        viewModel.SweepFirstChannel = 10;
        viewModel.SweepLastChannel = 20;

        viewModel.SweepFirstChannel = 30;
        Assert.Equal(20, viewModel.SweepFirstChannel);

        viewModel.SweepLastChannel = 5;
        Assert.Equal(20, viewModel.SweepLastChannel);
        Assert.Equal("2442 to 2442 MHz", viewModel.SweepRangeText);
    }

    [Fact]
    public void PayloadOptions_OfferTheConstantCarrierOnlyWithTheNordicProfileOnUncodedPhy()
    {
        using MainViewModel viewModel = Create();
        Assert.Contains(viewModel.PayloadOptions, option => option.Value == Payload.ConstantCarrier);
        Assert.DoesNotContain(viewModel.PayloadOptions, option => option.Value == Payload.Pattern11111111);

        viewModel.SelectedVendorProfile = viewModel.VendorProfiles.Single(option => option.Value == VendorProfile.Generic);
        Assert.DoesNotContain(viewModel.PayloadOptions, option => option.Value == Payload.ConstantCarrier);

        viewModel.SelectedVendorProfile = viewModel.VendorProfiles.Single(option => option.Value == VendorProfile.NordicNrf5x);
        viewModel.SelectedPhy = viewModel.PhyOptions.Single(option => option.Value == Phy.LeCodedS8);
        Assert.DoesNotContain(viewModel.PayloadOptions, option => option.Value == Payload.ConstantCarrier);
        Assert.Contains(viewModel.PayloadOptions, option => option.Value == Payload.Pattern11111111);
    }

    [Fact]
    public void SelectedPayload_FallsBackToPrbs9WhenItsOptionGoesAway()
    {
        using MainViewModel viewModel = Create();
        viewModel.SelectedPayload = viewModel.PayloadOptions.Single(option => option.Value == Payload.ConstantCarrier);
        Assert.False(viewModel.ShowPayloadLength);

        viewModel.SelectedPhy = viewModel.PhyOptions.Single(option => option.Value == Phy.LeCodedS2);

        Assert.Equal(Payload.Prbs9, viewModel.SelectedPayload.Value);
        Assert.True(viewModel.ShowPayloadLength);
    }

    [Fact]
    public void Tabs_ChangeTheLabelsAndTheChartKind()
    {
        using MainViewModel viewModel = Create();
        Assert.Equal("Transmit on channel", viewModel.ChannelLabel);
        Assert.False(viewModel.ChartIsReceiver);

        viewModel.IsReceiverTab = true;

        Assert.False(viewModel.IsTransmitterTab);
        Assert.Equal("Receive on channel", viewModel.ChannelLabel);
        Assert.Equal("Receive period", viewModel.PeriodLabel);
        Assert.True(viewModel.ChartIsReceiver);
        Assert.Equal(DtmChannel.Count, viewModel.ChartValues.Length);

        viewModel.IsAboutTab = true;
        Assert.False(viewModel.IsTestTab);
    }

    [Fact]
    public void Dispose_SavesTheSettingsForTheNextRun()
    {
        MainViewModel first = Create();
        first.IsSweep = true;
        first.SweepFirstChannel = 4;
        first.TransmitPowerDbm = -8;
        first.ShowRawFrames = true;
        first.Dispose();

        using MainViewModel second = Create();

        Assert.True(second.IsSweep);
        Assert.Equal(4, second.SweepFirstChannel);
        Assert.Equal(-8, second.TransmitPowerDbm);
        Assert.True(second.ShowRawFrames);
    }

    private MainViewModel Create() => new MainViewModel(new SettingsStore(Path.Combine(_folder, "settings.xml")));
}
