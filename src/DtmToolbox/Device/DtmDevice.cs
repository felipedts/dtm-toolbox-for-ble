using System;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>
/// The operations of a device in Direct Test Mode. Every method sends one command, checks the
/// status of the event that answers it and reports the exchange through <see cref="FrameExchanged"/>.
/// </summary>
public sealed class DtmDevice : IDisposable
{
    private readonly IDtmLink _link;

    public DtmDevice(IDtmLink link)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
    }

    /// <summary>Raised on the calling thread after each exchange, failed ones included.</summary>
    public event EventHandler<FrameExchangedEventArgs>? FrameExchanged;

    public void Reset() => Send("Reset", DtmCommand.Reset());

    public void SetPayloadLength(int payloadLength) =>
        Send("Set upper length bits", DtmCommand.SetUpperLengthBits(payloadLength));

    public void SetPhy(Phy phy) => Send("Set PHY " + phy, DtmCommand.SetPhy(phy));

    public void SetModulationIndex(ModulationIndex index) =>
        Send("Set modulation index " + index, DtmCommand.SetModulationIndex(index));

    /// <summary>Requests a transmit power and returns the level the device applied.</summary>
    public TransmitPowerReport SetTransmitPower(int dbm)
    {
        DtmEvent response = Send("Set transmit power " + dbm + " dBm", DtmCommand.SetTransmitPower(dbm));
        return new TransmitPowerReport(response.Response);
    }

    public DtmFeatures ReadFeatures() =>
        (DtmFeatures)Send("Read features", DtmCommand.ReadFeatures()).Response;

    /// <summary>
    /// Octet counts come back as they are. Times come back in units of 2 microseconds, as the
    /// specification defines them, and are converted to microseconds here.
    /// </summary>
    public int ReadMaxSupported(MaxSupportedValue value)
    {
        int response = Send("Read max supported " + value, DtmCommand.ReadMaxSupported(value)).Response;
        bool isTime = value == MaxSupportedValue.TxTime || value == MaxSupportedValue.RxTime;
        return isTime ? response * 2 : response;
    }

    public void StartReceiver(int channel) =>
        Send("Receiver test, channel " + channel, DtmCommand.ReceiverTest(channel));

    public void StartTransmitter(int channel, int payloadLength, PacketType packetType) =>
        Send("Transmitter test, channel " + channel, DtmCommand.TransmitterTest(channel, payloadLength, packetType));

    /// <summary>Nordic nRF5x vendor command: unmodulated carrier until the test is ended.</summary>
    public void StartConstantCarrier(int channel) =>
        Send("Constant carrier, channel " + channel, NordicVendorCommand.ConstantCarrier(channel));

    /// <summary>Ends the running test and returns the packets received, zero after a transmitter test.</summary>
    public int EndTest() => Send("Test end", DtmCommand.TestEnd()).PacketCount;

    public void Dispose() => _link.Dispose();

    private DtmEvent Send(string operation, DtmFrame command)
    {
        DtmEvent response;
        try
        {
            response = _link.Exchange(command);
        }
        catch (DtmException ex)
        {
            FrameExchanged?.Invoke(this, new FrameExchangedEventArgs(operation, command, null, ex));
            throw;
        }

        if (response.IsError)
        {
            var rejected = new DtmCommandRejectedException(operation, command, response);
            FrameExchanged?.Invoke(this, new FrameExchangedEventArgs(operation, command, response, rejected));
            throw rejected;
        }

        FrameExchanged?.Invoke(this, new FrameExchangedEventArgs(operation, command, response, null));
        return response;
    }
}
