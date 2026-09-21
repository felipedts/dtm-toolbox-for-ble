using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>
/// A device that exists only in memory and answers like the Nordic DTM firmware of an nRF52840.
/// It lets the whole application run, from the Start button to the chart, without hardware.
/// The legacy variant has no transmit power setup command (0x09), like firmware that predates
/// Bluetooth 5.2, and takes the power only through the vendor command.
/// </summary>
public sealed class SimulatedDtmLink : IDtmLink
{
    // Transmit power levels of the nRF52840 radio, in dBm.
    private static readonly int[] PowerLevels = { -40, -20, -16, -12, -8, -4, 0, 2, 3, 4, 5, 6, 7, 8 };

    // A tester sends one packet every 625 microseconds.
    private const double PacketsPerMillisecond = 1.6;
    private const int MaxPacketCount = 0x7FFF;
    private const int ResponseDelayMs = 2;

    private const int Success = 0x0000;
    private const int Error = 0x0001;
    private const int PacketReport = 0x8000;

    private readonly bool _legacyFirmware;
    private readonly Stopwatch _testClock = new Stopwatch();
    private readonly Random _random = new Random(1);
    private readonly object _sync = new object();
    private bool _testRunning;
    private bool _receiving;
    private bool _codedPhy;

    public SimulatedDtmLink(bool legacyFirmware)
    {
        _legacyFirmware = legacyFirmware;
    }

    public DtmEvent Exchange(DtmFrame command)
    {
        Thread.Sleep(ResponseDelayMs);
        lock (_sync)
        {
            int upperField = (command.Value >> 8) & 0x3F;
            int lowerByte = command.Value & 0xFF;
            int answer;
            switch ((DtmCommandCode)(command.Value >> 14))
            {
                case DtmCommandCode.TestSetup:
                    answer = Setup((SetupControl)upperField, lowerByte);
                    break;
                case DtmCommandCode.ReceiverTest:
                    answer = StartTest(receiving: true);
                    break;
                case DtmCommandCode.TransmitterTest:
                    answer = Transmit(upperField, lowerByte >> 2, lowerByte & 0x03);
                    break;
                default:
                    answer = EndTest();
                    break;
            }

            return new DtmEvent(new DtmFrame((ushort)answer));
        }
    }

    public void Dispose()
    {
    }

    private int Setup(SetupControl control, int parameter)
    {
        switch (control)
        {
            case SetupControl.Reset:
                _testRunning = false;
                _codedPhy = false;
                return Success;

            case SetupControl.SetUpperLengthBits:
                return parameter <= 0x0F ? Success : Error;

            case SetupControl.SetPhy:
                if (parameter < 0x04 || parameter > 0x13)
                {
                    return Error;
                }

                _codedPhy = parameter >= 0x0C;
                return Success;

            case SetupControl.SetModulationIndex:
                // The nRF52840 has the standard modulation index only.
                return parameter <= 0x03 ? Success : Error;

            case SetupControl.ReadFeatures:
                return (int)(DtmFeatures.DataLengthExtension | DtmFeatures.Le2MPhy | DtmFeatures.LeCodedPhy) << 1;

            case SetupControl.ReadMaxSupported:
                // 251 octets; 17040 microseconds, reported in units of 2 microseconds.
                return parameter <= 0x0F ? ((parameter & 0x04) != 0 ? 8520 : 251) << 1 : Error;

            case SetupControl.SetTransmitPower:
                return _legacyFirmware ? Error : SetTransmitPower(parameter);

            default:
                return Error;
        }
    }

    // Setup 0x09: applies the nearest level of the radio and reports it with the limit flags.
    private static int SetTransmitPower(int parameter)
    {
        int requested = parameter == 0x7E ? PowerLevels[0] : parameter == 0x7F ? PowerLevels[PowerLevels.Length - 1] : unchecked((sbyte)parameter);
        if (requested > DtmCommand.MaxTransmitPowerDbm)
        {
            return Error;
        }

        int applied = PowerLevels.OrderBy(level => Math.Abs(level - requested)).First();
        int answer = (applied & 0xFF) << 1;
        if (applied == PowerLevels[0])
        {
            answer |= 1 << 9;
        }

        if (applied == PowerLevels[PowerLevels.Length - 1])
        {
            answer |= 1 << 10;
        }

        return answer;
    }

    private int Transmit(int channelField, int lengthField, int packetType)
    {
        bool vendorCommand = packetType == 3 && !_codedPhy;
        if (!vendorCommand)
        {
            return StartTest(receiving: false);
        }

        switch (lengthField)
        {
            case 0:
                return StartTest(receiving: false);

            case 2:
                // Six bits, negative when bit 5 or bit 4 is set. Only the levels of the radio are accepted.
                int level = (channelField & 0x30) != 0 ? unchecked((sbyte)(channelField | 0xC0)) : channelField;
                return !_testRunning && PowerLevels.Contains(level) ? Success : Error;

            default:
                return Error;
        }
    }

    private int StartTest(bool receiving)
    {
        if (_testRunning)
        {
            return Error;
        }

        _testRunning = true;
        _receiving = receiving;
        _testClock.Restart();
        return Success;
    }

    private int EndTest()
    {
        int packets = 0;
        if (_testRunning && _receiving)
        {
            // A little packet loss, so that the counts are not perfectly regular.
            double received = _testClock.ElapsedMilliseconds * PacketsPerMillisecond * (0.97 + (_random.NextDouble() * 0.03));
            packets = (int)Math.Min(MaxPacketCount, received);
        }

        _testRunning = false;
        return PacketReport | packets;
    }
}
