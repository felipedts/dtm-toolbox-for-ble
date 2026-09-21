using System;
using System.Globalization;

namespace DtmToolbox.Protocol;

/// <summary>
/// A 16-bit word of the 2-wire UART interface: a command sent to the device or the event it
/// answers with. The most significant byte goes first on the wire.
/// </summary>
public readonly struct DtmFrame : IEquatable<DtmFrame>
{
    public DtmFrame(ushort value)
    {
        Value = value;
    }

    public DtmFrame(byte high, byte low)
        : this((ushort)((high << 8) | low))
    {
    }

    public ushort Value { get; }

    public byte High => (byte)(Value >> 8);

    public byte Low => (byte)(Value & 0xFF);

    public byte[] ToBytes() => new[] { High, Low };

    public bool Equals(DtmFrame other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is DtmFrame other && Equals(other);

    public override int GetHashCode() => Value;

    /// <summary>Two hexadecimal bytes in wire order, for example "80 94".</summary>
    public override string ToString() =>
        High.ToString("X2", CultureInfo.InvariantCulture) + " " + Low.ToString("X2", CultureInfo.InvariantCulture);

    public static bool operator ==(DtmFrame left, DtmFrame right) => left.Equals(right);

    public static bool operator !=(DtmFrame left, DtmFrame right) => !left.Equals(right);
}
