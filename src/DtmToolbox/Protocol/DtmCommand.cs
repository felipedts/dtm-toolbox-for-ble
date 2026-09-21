using System;

namespace DtmToolbox.Protocol;

/// <summary>
/// Builds the command frames of the 2-wire UART interface (Bluetooth Core Specification,
/// Vol 6, Part F). Layout: bits 15-14 command code; for setup and end commands bits 13-8
/// control and bits 7-0 parameter; for receiver and transmitter commands bits 13-8 channel,
/// bits 7-2 length and bits 1-0 packet type.
/// </summary>
public static class DtmCommand
{
    public const int MaxPayloadLength = 255;
    public const int MinTransmitPowerDbm = -127;
    public const int MaxTransmitPowerDbm = 20;

    private const byte TransmitPowerMinimumParameter = 0x7E;
    private const byte TransmitPowerMaximumParameter = 0x7F;

    public static DtmFrame Setup(SetupControl control, byte parameter) =>
        Build(DtmCommandCode.TestSetup, (int)control, parameter);

    public static DtmFrame Reset() => Setup(SetupControl.Reset, 0);

    /// <summary>
    /// Sets the two most significant bits of the payload length. The transmitter command only
    /// carries the six least significant ones.
    /// </summary>
    public static DtmFrame SetUpperLengthBits(int payloadLength)
    {
        CheckPayloadLength(payloadLength);
        return Setup(SetupControl.SetUpperLengthBits, (byte)((payloadLength >> 6) << 2));
    }

    public static DtmFrame SetPhy(Phy phy) => Setup(SetupControl.SetPhy, (byte)((int)phy << 2));

    public static DtmFrame SetModulationIndex(ModulationIndex index) =>
        Setup(SetupControl.SetModulationIndex, (byte)((int)index << 2));

    public static DtmFrame ReadFeatures() => Setup(SetupControl.ReadFeatures, 0);

    public static DtmFrame ReadMaxSupported(MaxSupportedValue value) =>
        Setup(SetupControl.ReadMaxSupported, (byte)((int)value << 2));

    /// <summary>Requests a transmit power in dBm. The parameter is a signed 8-bit value.</summary>
    public static DtmFrame SetTransmitPower(int dbm)
    {
        if (dbm < MinTransmitPowerDbm || dbm > MaxTransmitPowerDbm)
        {
            throw new ArgumentOutOfRangeException(nameof(dbm), dbm, "Transmit power must be between -127 and +20 dBm.");
        }

        return Setup(SetupControl.SetTransmitPower, unchecked((byte)(sbyte)dbm));
    }

    public static DtmFrame SetTransmitPowerToMinimum() =>
        Setup(SetupControl.SetTransmitPower, TransmitPowerMinimumParameter);

    public static DtmFrame SetTransmitPowerToMaximum() =>
        Setup(SetupControl.SetTransmitPower, TransmitPowerMaximumParameter);

    /// <summary>Starts a receiver test. Length and packet type are not used by the receiver.</summary>
    public static DtmFrame ReceiverTest(int channel)
    {
        DtmChannel.Check(channel);
        return Build(DtmCommandCode.ReceiverTest, channel, 0);
    }

    /// <summary>
    /// Starts a transmitter test. Lengths above 63 also need <see cref="SetUpperLengthBits"/>.
    /// </summary>
    public static DtmFrame TransmitterTest(int channel, int payloadLength, PacketType packetType)
    {
        DtmChannel.Check(channel);
        CheckPayloadLength(payloadLength);
        return Build(DtmCommandCode.TransmitterTest, channel, (byte)(((payloadLength & 0x3F) << 2) | (int)packetType));
    }

    public static DtmFrame TestEnd() => Build(DtmCommandCode.TestEnd, 0, 0);

    internal static DtmFrame Build(DtmCommandCode code, int upperField, byte lowerByte) =>
        new DtmFrame((ushort)(((int)code << 14) | ((upperField & 0x3F) << 8) | lowerByte));

    private static void CheckPayloadLength(int payloadLength)
    {
        if (payloadLength < 0 || payloadLength > MaxPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength), payloadLength, "Payload length must be between 0 and 255 bytes.");
        }
    }
}
