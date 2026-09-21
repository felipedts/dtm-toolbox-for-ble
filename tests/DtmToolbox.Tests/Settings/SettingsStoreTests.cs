using System;
using System.IO;
using DtmToolbox.Protocol;
using DtmToolbox.Settings;
using DtmToolbox.ViewModels;
using Xunit;

namespace DtmToolbox.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "DtmToolbox.Tests." + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_folder, "settings.xml");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutAFile_ReturnsTheDefaults()
    {
        AppSettings settings = new SettingsStore(SettingsPath).Load();

        Assert.Null(settings.PortName);
        Assert.Equal(19200, settings.BaudRate);
        Assert.Equal(19, settings.Channel);
        Assert.Equal(VendorProfile.NordicNrf5x, settings.VendorProfile);
        Assert.True(settings.ShowLog);
    }

    [Fact]
    public void Save_ThenLoad_KeepsEveryValue()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppSettings
        {
            PortName = "COM10",
            BaudRate = 115200,
            VendorProfile = VendorProfile.Generic,
            IsSweep = true,
            Channel = 39,
            SweepFirstChannel = 3,
            SweepLastChannel = 30,
            DwellTimeMs = 250,
            TransmitPowerDbm = -20,
            Phy = Phy.LeCodedS8,
            Payload = Payload.Pattern11111111,
            PayloadLength = 255,
            TimeoutSeconds = 12,
            ShowLog = false,
            AutoScrollLog = false,
            ShowRawFrames = true,
        });

        AppSettings loaded = store.Load();

        Assert.Equal("COM10", loaded.PortName);
        Assert.Equal(115200, loaded.BaudRate);
        Assert.Equal(VendorProfile.Generic, loaded.VendorProfile);
        Assert.True(loaded.IsSweep);
        Assert.Equal(39, loaded.Channel);
        Assert.Equal(3, loaded.SweepFirstChannel);
        Assert.Equal(30, loaded.SweepLastChannel);
        Assert.Equal(250, loaded.DwellTimeMs);
        Assert.Equal(-20, loaded.TransmitPowerDbm);
        Assert.Equal(Phy.LeCodedS8, loaded.Phy);
        Assert.Equal(Payload.Pattern11111111, loaded.Payload);
        Assert.Equal(255, loaded.PayloadLength);
        Assert.Equal(12, loaded.TimeoutSeconds);
        Assert.False(loaded.ShowLog);
        Assert.False(loaded.AutoScrollLog);
        Assert.True(loaded.ShowRawFrames);
    }

    [Fact]
    public void Load_FromADamagedFile_ReturnsTheDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "<DtmToolbox><BaudRate>fast</BaudRate><Phy>Le9M</Phy>");

        AppSettings settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal(19200, settings.BaudRate);
        Assert.Equal(Phy.Le1M, settings.Phy);
    }

    [Fact]
    public void Load_IgnoresValuesItCannotRead_AndKeepsTheOthers()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "<DtmToolbox><BaudRate>fast</BaudRate><Phy>99</Phy><Channel>7</Channel></DtmToolbox>");

        AppSettings settings = new SettingsStore(SettingsPath).Load();

        Assert.Equal(19200, settings.BaudRate);
        Assert.Equal(Phy.Le1M, settings.Phy);
        Assert.Equal(7, settings.Channel);
    }
}
