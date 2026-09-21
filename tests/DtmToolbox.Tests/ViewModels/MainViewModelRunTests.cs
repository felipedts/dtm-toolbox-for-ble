using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using DtmToolbox.Protocol;
using DtmToolbox.Settings;
using DtmToolbox.ViewModels;
using Xunit;

namespace DtmToolbox.Tests.ViewModels;

/// <summary>
/// Runs tests from the view model against the simulated devices: what is chosen on screen has to
/// reach the device, and the run has to end with the view model idle again.
/// </summary>
public sealed class MainViewModelRunTests : IDisposable
{
    private static readonly TimeSpan RunLimit = TimeSpan.FromSeconds(10);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "DtmToolbox.Tests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ConstantCarrierOn2MWithPowerAndTimeout_ReachesTheDeviceAndEndsByItself()
    {
        using MainViewModel viewModel = Create("SIM-CURRENT");
        viewModel.ShowRawFrames = true;
        viewModel.SelectedPhy = viewModel.PhyOptions.Single(option => option.Value == Phy.Le2M);
        viewModel.SelectedPayload = viewModel.PayloadOptions.Single(option => option.Value == Payload.ConstantCarrier);
        viewModel.ChannelIndex = 39;
        viewModel.TransmitPowerDbm = -40;
        viewModel.TimeoutSeconds = 1;

        RunToTheEnd(viewModel, stopAfter: null);

        string log = LogText(viewModel);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Contains("Constant carrier, LE 2Mbps, channel 39 (2480 MHz), timeout 1 s", log);
        Assert.Contains("> 09 D8", log);
        Assert.Contains("Transmit power applied: -40 dBm (device minimum)", log);
        Assert.Contains("> 02 08", log);
        Assert.Contains("> A7 03", log);
        Assert.Contains("Test ended by timeout", log);
    }

    [Fact]
    public void LegacyFirmware_GetsThePowerThroughTheVendorCommand()
    {
        using MainViewModel viewModel = Create("SIM-LEGACY");
        viewModel.ShowRawFrames = true;
        viewModel.TransmitPowerDbm = -3;
        viewModel.PayloadLength = 255;

        RunToTheEnd(viewModel, stopAfter: TimeSpan.FromMilliseconds(200));

        string log = LogText(viewModel);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Contains("Transmit power applied: -4 dBm (Nordic vendor command", log);
        Assert.Contains("> 01 0C", log);
        Assert.Contains("Test stopped.", log);
    }

    [Fact]
    public void GenericProfileOnLegacyFirmware_StopsWithAnExplanation()
    {
        using MainViewModel viewModel = Create("SIM-LEGACY");
        viewModel.SelectedVendorProfile = viewModel.VendorProfiles.Single(option => option.Value == VendorProfile.Generic);

        RunToTheEnd(viewModel, stopAfter: null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Firmware older than Bluetooth 5.2", viewModel.ErrorMessage);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public void ReceiverSweep_FillsTheReceiverChart()
    {
        using MainViewModel viewModel = Create("SIM-CURRENT");
        viewModel.IsReceiverTab = true;
        viewModel.IsSweep = true;
        viewModel.SweepFirstChannel = 0;
        viewModel.SweepLastChannel = 2;
        viewModel.DwellTimeMs = 20;

        RunToTheEnd(viewModel, stopAfter: TimeSpan.FromMilliseconds(600));

        Assert.Null(viewModel.ErrorMessage);
        Assert.True(viewModel.ChartIsReceiver);
        Assert.All(viewModel.ChartValues.Take(3), count => Assert.True(count > 0));
        Assert.All(viewModel.ChartValues.Skip(3), count => Assert.Equal(0, count));
        Assert.Contains("Packets received:", LogText(viewModel));
    }

    private MainViewModel Create(string portName)
    {
        var viewModel = new MainViewModel(new SettingsStore(Path.Combine(_folder, "settings.xml")), simulate: true);
        viewModel.SelectedPort = viewModel.Ports.Single(port => port.PortName == portName);
        return viewModel;
    }

    // Starts a test, optionally asks it to stop, and waits until the view model is idle again.
    private static void RunToTheEnd(MainViewModel viewModel, TimeSpan? stopAfter)
    {
        viewModel.StartStopCommand.Execute(null);
        if (stopAfter.HasValue)
        {
            Thread.Sleep(stopAfter.Value);
            if (viewModel.IsRunning)
            {
                viewModel.StartStopCommand.Execute(null);
            }
        }

        var clock = Stopwatch.StartNew();
        while (viewModel.IsRunning && clock.Elapsed < RunLimit)
        {
            Thread.Sleep(20);
        }

        Assert.False(viewModel.IsRunning, "The run did not end.");

        // The last log lines are written right after the running flag goes down.
        Thread.Sleep(100);
    }

    private static string LogText(MainViewModel viewModel) =>
        string.Join(Environment.NewLine, viewModel.Log.ToList().Select(entry => entry.Message));
}
