using System;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

public enum TestMode
{
    Transmitter,
    Receiver,
}

/// <summary>Everything a test run needs: what to do, on which channels and for how long.</summary>
public sealed class TestPlan
{
    public TestMode Mode { get; set; } = TestMode.Transmitter;

    /// <summary>First RF channel, 0 to 39. Default 19, 2440 MHz. A plan with the same first and last channel stays on it.</summary>
    public int FirstChannel { get; set; } = 19;

    /// <summary>Last RF channel of a sweep, 0 to 39.</summary>
    public int LastChannel { get; set; } = 19;

    /// <summary>Time spent on each channel of a sweep, in milliseconds.</summary>
    public int DwellTimeMs { get; set; } = 30;

    /// <summary>Total duration after which the run stops by itself, in milliseconds. Zero runs until stopped.</summary>
    public int TimeoutMs { get; set; }

    public Phy Phy { get; set; } = Phy.Le1M;

    public ModulationIndex ModulationIndex { get; set; } = ModulationIndex.Standard;

    public PacketType PacketType { get; set; } = PacketType.Prbs9;

    public int PayloadLength { get; set; } = 37;

    /// <summary>Transmit power to request, in dBm. Null leaves the device at its default.</summary>
    public int? TransmitPowerDbm { get; set; } = 0;

    /// <summary>Transmit an unmodulated carrier (Nordic nRF5x vendor command) instead of packets.</summary>
    public bool ConstantCarrier { get; set; }

    /// <summary>Vendor commands the run may use besides the ones of the specification.</summary>
    public VendorProfile VendorProfile { get; set; } = VendorProfile.Generic;

    public bool IsSweep => LastChannel != FirstChannel;

    /// <exception cref="ArgumentException">A value is out of range or a combination is not valid.</exception>
    public void Validate()
    {
        DtmChannel.Check(FirstChannel);
        DtmChannel.Check(LastChannel);
        if (LastChannel < FirstChannel)
        {
            throw new ArgumentException("The last channel of a sweep cannot be below the first.");
        }

        if (IsSweep && DwellTimeMs < 1)
        {
            throw new ArgumentException("The dwell time of a sweep must be at least 1 ms.");
        }

        if (TimeoutMs < 0)
        {
            throw new ArgumentException("The timeout cannot be negative.");
        }

        if (PayloadLength < 0 || PayloadLength > DtmCommand.MaxPayloadLength)
        {
            throw new ArgumentException("Payload length must be between 0 and 255 bytes.");
        }

        if (TransmitPowerDbm is int dbm && (dbm < DtmCommand.MinTransmitPowerDbm || dbm > DtmCommand.MaxTransmitPowerDbm))
        {
            throw new ArgumentException("Transmit power must be between -127 and +20 dBm.");
        }

        bool codedPhy = Phy == Phy.LeCodedS8 || Phy == Phy.LeCodedS2;
        if (ConstantCarrier && (Mode != TestMode.Transmitter || codedPhy))
        {
            throw new ArgumentException("The constant carrier is a transmitter test on the LE 1M or LE 2M PHY.");
        }

        if (ConstantCarrier && VendorProfile != VendorProfile.NordicNrf5x)
        {
            throw new ArgumentException("The constant carrier is a Nordic nRF5x vendor command.");
        }

        if (!ConstantCarrier && Mode == TestMode.Transmitter && PacketType == PacketType.Pattern11111111 && !codedPhy)
        {
            throw new ArgumentException("The 11111111 payload exists only on the LE Coded PHY.");
        }
    }
}
