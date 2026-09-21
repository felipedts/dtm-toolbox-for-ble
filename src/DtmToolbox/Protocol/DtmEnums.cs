using System;

namespace DtmToolbox.Protocol;

/// <summary>Command code, bits 15-14 of a command frame.</summary>
public enum DtmCommandCode
{
    TestSetup = 0,
    ReceiverTest = 1,
    TransmitterTest = 2,
    TestEnd = 3,
}

/// <summary>Control field of a test setup command, bits 13-8.</summary>
public enum SetupControl
{
    Reset = 0x00,
    SetUpperLengthBits = 0x01,
    SetPhy = 0x02,
    SetModulationIndex = 0x03,
    ReadFeatures = 0x04,
    ReadMaxSupported = 0x05,
    SetTransmitPower = 0x09,
}

public enum Phy
{
    Le1M = 1,
    Le2M = 2,
    LeCodedS8 = 3,
    LeCodedS2 = 4,
}

public enum ModulationIndex
{
    Standard = 0,
    Stable = 1,
}

/// <summary>Payload selector, bits 1-0 of a transmitter test command.</summary>
public enum PacketType
{
    Prbs9 = 0,
    Pattern11110000 = 1,
    Pattern10101010 = 2,

    /// <summary>
    /// All-ones payload on the LE Coded PHY. On LE 1M and LE 2M the same code marks a vendor
    /// command, so it is not a valid payload there.
    /// </summary>
    Pattern11111111 = 3,
}

/// <summary>Value selected by a "read supported maximum" setup command.</summary>
public enum MaxSupportedValue
{
    TxOctets = 0,
    TxTime = 1,
    RxOctets = 2,
    RxTime = 3,
    CteLength = 4,
}

/// <summary>Test cases the device reports as supported, from the "read features" response.</summary>
[Flags]
public enum DtmFeatures
{
    None = 0,
    DataLengthExtension = 1 << 0,
    Le2MPhy = 1 << 1,
    StableModulationIndex = 1 << 2,
    LeCodedPhy = 1 << 3,
    ConstantToneExtension = 1 << 4,
    AntennaSwitching = 1 << 5,
    Aod1UsTransmission = 1 << 6,
    Aod1UsReception = 1 << 7,
    Aoa1UsReception = 1 << 8,
}

/// <summary>Commands outside the Bluetooth specification that the application may use.</summary>
public enum VendorProfile
{
    /// <summary>Specification commands only.</summary>
    Generic = 0,

    /// <summary>Nordic Semiconductor nRF5x DTM firmware: adds the constant carrier.</summary>
    NordicNrf5x = 1,
}
