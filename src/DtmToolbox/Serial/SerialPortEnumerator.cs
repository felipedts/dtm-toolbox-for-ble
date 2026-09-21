using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DtmToolbox.Serial;

/// <summary>
/// Lists the serial ports Windows knows, whatever driver or vendor they belong to. Port names
/// come from <see cref="SerialPort.GetPortNames"/> and friendly names from the device tree in
/// the registry.
/// </summary>
public static class SerialPortEnumerator
{
    private const string DeviceTreePath = @"SYSTEM\CurrentControlSet\Enum";

    private static readonly Regex PortNamePattern = new Regex(@"^COM\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SerialPortInfo> GetPorts()
    {
        IDictionary<string, string> friendlyNames = ReadFriendlyNames();

        return SerialPort.GetPortNames()
            .Select(CleanPortName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new SerialPortInfo(name, friendlyNames.TryGetValue(name, out string? friendly) ? friendly : null))
            .OrderBy(port => port.PortNumber)
            .ThenBy(port => port.PortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Some Bluetooth serial drivers register their port name with trailing garbage characters.
    private static string CleanPortName(string name)
    {
        Match match = PortNamePattern.Match(name ?? string.Empty);
        return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
    }

    // Device tree: Enum\<enumerator>\<device>\<instance>. An instance that owns a serial port has
    // a "Device Parameters\PortName" value, and its own "FriendlyName" is what Device Manager shows.
    private static IDictionary<string, string> ReadFriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using (RegistryKey? tree = OpenSubKey(Registry.LocalMachine, DeviceTreePath))
        {
            foreach (RegistryKey enumerator in SubKeys(tree))
            {
                foreach (RegistryKey device in SubKeys(enumerator))
                {
                    foreach (RegistryKey instance in SubKeys(device))
                    {
                        AddFriendlyName(instance, names);
                    }
                }
            }
        }

        return names;
    }

    private static void AddFriendlyName(RegistryKey instance, IDictionary<string, string> names)
    {
        using (RegistryKey? parameters = OpenSubKey(instance, "Device Parameters"))
        {
            string portName = CleanPortName(parameters?.GetValue("PortName") as string ?? string.Empty);
            if (portName.Length > 0 && instance.GetValue("FriendlyName") is string friendlyName && friendlyName.Length > 0)
            {
                names[portName] = friendlyName;
            }
        }
    }

    // Yields each readable subkey and disposes it after the caller is done with it.
    private static IEnumerable<RegistryKey> SubKeys(RegistryKey? parent)
    {
        if (parent == null)
        {
            yield break;
        }

        string[] subKeyNames;
        try
        {
            subKeyNames = parent.GetSubKeyNames();
        }
        catch (Exception ex) when (IsAccessFailure(ex))
        {
            yield break;
        }

        foreach (string name in subKeyNames)
        {
            using (RegistryKey? subKey = OpenSubKey(parent, name))
            {
                if (subKey != null)
                {
                    yield return subKey;
                }
            }
        }
    }

    private static RegistryKey? OpenSubKey(RegistryKey parent, string name)
    {
        try
        {
            return parent.OpenSubKey(name, writable: false);
        }
        catch (Exception ex) when (IsAccessFailure(ex))
        {
            return null;
        }
    }

    private static bool IsAccessFailure(Exception ex) =>
        ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException;
}
