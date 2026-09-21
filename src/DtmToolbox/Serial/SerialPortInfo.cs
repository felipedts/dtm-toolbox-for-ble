using System;

namespace DtmToolbox.Serial;

/// <summary>A serial port present on the machine.</summary>
public sealed class SerialPortInfo : IEquatable<SerialPortInfo>
{
    public SerialPortInfo(string portName, string? friendlyName)
    {
        PortName = portName ?? throw new ArgumentNullException(nameof(portName));
        FriendlyName = friendlyName;
    }

    /// <summary>Name used to open the port, for example "COM10".</summary>
    public string PortName { get; }

    /// <summary>Name Windows shows in Device Manager, for example "USB Serial Port (COM10)". Null when unknown.</summary>
    public string? FriendlyName { get; }

    /// <summary>Text for a port list: the friendly name when there is one, the port name otherwise.</summary>
    public string DisplayName => string.IsNullOrEmpty(FriendlyName) ? PortName : FriendlyName!;

    /// <summary>Number in the port name, for sorting. Ports without one sort last.</summary>
    public int PortNumber
    {
        get
        {
            string digits = PortName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ? PortName.Substring(3) : string.Empty;
            return int.TryParse(digits, out int number) ? number : int.MaxValue;
        }
    }

    public bool Equals(SerialPortInfo? other) =>
        other != null
        && string.Equals(PortName, other.PortName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(FriendlyName, other.FriendlyName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as SerialPortInfo);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(PortName);

    public override string ToString() => DisplayName;
}
