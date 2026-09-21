using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using DtmToolbox.Protocol;
using DtmToolbox.ViewModels;

namespace DtmToolbox.Settings;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as a small XML file under %APPDATA%\DtmToolbox.
/// Settings are a convenience: a missing, unreadable or unwritable file never stops the application.
/// </summary>
public sealed class SettingsStore
{
    private const string RootName = "DtmToolbox";

    private readonly string _path;

    public SettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DtmToolbox", "settings.xml"))
    {
    }

    public SettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        var settings = new AppSettings();
        XElement? root;
        try
        {
            if (!File.Exists(_path))
            {
                return settings;
            }

            root = XDocument.Load(_path).Root;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            return settings;
        }

        if (root == null)
        {
            return settings;
        }

        settings.PortName = Text(root, nameof(AppSettings.PortName)) ?? settings.PortName;
        settings.BaudRate = Int(root, nameof(AppSettings.BaudRate), settings.BaudRate);
        settings.VendorProfile = EnumValue(root, nameof(AppSettings.VendorProfile), settings.VendorProfile);
        settings.IsSweep = Bool(root, nameof(AppSettings.IsSweep), settings.IsSweep);
        settings.Channel = Int(root, nameof(AppSettings.Channel), settings.Channel);
        settings.SweepFirstChannel = Int(root, nameof(AppSettings.SweepFirstChannel), settings.SweepFirstChannel);
        settings.SweepLastChannel = Int(root, nameof(AppSettings.SweepLastChannel), settings.SweepLastChannel);
        settings.DwellTimeMs = Int(root, nameof(AppSettings.DwellTimeMs), settings.DwellTimeMs);
        settings.TransmitPowerDbm = Int(root, nameof(AppSettings.TransmitPowerDbm), settings.TransmitPowerDbm);
        settings.Phy = EnumValue(root, nameof(AppSettings.Phy), settings.Phy);
        settings.Payload = EnumValue(root, nameof(AppSettings.Payload), settings.Payload);
        settings.PayloadLength = Int(root, nameof(AppSettings.PayloadLength), settings.PayloadLength);
        settings.TimeoutSeconds = Int(root, nameof(AppSettings.TimeoutSeconds), settings.TimeoutSeconds);
        settings.ShowLog = Bool(root, nameof(AppSettings.ShowLog), settings.ShowLog);
        settings.AutoScrollLog = Bool(root, nameof(AppSettings.AutoScrollLog), settings.AutoScrollLog);
        settings.ShowRawFrames = Bool(root, nameof(AppSettings.ShowRawFrames), settings.ShowRawFrames);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var root = new XElement(
            RootName,
            new XElement(nameof(AppSettings.PortName), settings.PortName ?? string.Empty),
            new XElement(nameof(AppSettings.BaudRate), settings.BaudRate),
            new XElement(nameof(AppSettings.VendorProfile), settings.VendorProfile),
            new XElement(nameof(AppSettings.IsSweep), settings.IsSweep),
            new XElement(nameof(AppSettings.Channel), settings.Channel),
            new XElement(nameof(AppSettings.SweepFirstChannel), settings.SweepFirstChannel),
            new XElement(nameof(AppSettings.SweepLastChannel), settings.SweepLastChannel),
            new XElement(nameof(AppSettings.DwellTimeMs), settings.DwellTimeMs),
            new XElement(nameof(AppSettings.TransmitPowerDbm), settings.TransmitPowerDbm),
            new XElement(nameof(AppSettings.Phy), settings.Phy),
            new XElement(nameof(AppSettings.Payload), settings.Payload),
            new XElement(nameof(AppSettings.PayloadLength), settings.PayloadLength),
            new XElement(nameof(AppSettings.TimeoutSeconds), settings.TimeoutSeconds),
            new XElement(nameof(AppSettings.ShowLog), settings.ShowLog),
            new XElement(nameof(AppSettings.AutoScrollLog), settings.AutoScrollLog),
            new XElement(nameof(AppSettings.ShowRawFrames), settings.ShowRawFrames));

        try
        {
            string? folder = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            new XDocument(root).Save(_path);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // Nothing to do: the next run starts from the defaults.
        }
    }

    private static string? Text(XElement root, string name)
    {
        string? value = root.Element(name)?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int Int(XElement root, string name, int fallback) =>
        int.TryParse(root.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    private static bool Bool(XElement root, string name, bool fallback) =>
        bool.TryParse(root.Element(name)?.Value, out bool value) ? value : fallback;

    private static T EnumValue<T>(XElement root, string name, T fallback)
        where T : struct =>
        Enum.TryParse(root.Element(name)?.Value, out T value) && Enum.IsDefined(typeof(T), value) ? value : fallback;

    private static bool IsFileFailure(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException || ex is XmlException || ex is NotSupportedException
        || ex is System.Security.SecurityException;
}
