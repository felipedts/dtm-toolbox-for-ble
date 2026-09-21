using System;
using System.IO;
using System.IO.Ports;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>2-wire UART link over a serial port: 8 data bits, no parity, 1 stop bit, no flow control.</summary>
public sealed class SerialDtmLink : IDtmLink
{
    public const int DefaultBaudRate = 19200;
    public const int DefaultResponseTimeoutMs = 500;

    private const int FrameSize = 2;

    private readonly SerialPort _port;
    private readonly object _exchangeLock = new object();

    public SerialDtmLink(string portName, int baudRate = DefaultBaudRate, int responseTimeoutMs = DefaultResponseTimeoutMs)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = responseTimeoutMs,
            WriteTimeout = responseTimeoutMs,
        };

        try
        {
            _port.Open();
        }
        catch (Exception ex) when (IsPortFailure(ex))
        {
            _port.Dispose();
            throw new DtmLinkException("Cannot open " + portName + ": " + ex.Message, ex);
        }
    }

    public string PortName => _port.PortName;

    public DtmEvent Exchange(DtmFrame command)
    {
        lock (_exchangeLock)
        {
            try
            {
                // Bytes left over from an earlier exchange would be taken for this response.
                _port.DiscardInBuffer();

                // One write for both bytes: a device drops a command whose second byte
                // arrives more than 5 ms after the first.
                _port.Write(command.ToBytes(), 0, FrameSize);

                var response = new byte[FrameSize];
                int received = 0;
                while (received < FrameSize)
                {
                    received += _port.Read(response, received, FrameSize - received);
                }

                return new DtmEvent(new DtmFrame(response[0], response[1]));
            }
            catch (TimeoutException)
            {
                throw new DtmTimeoutException(command);
            }
            catch (Exception ex) when (IsPortFailure(ex))
            {
                throw new DtmLinkException("Serial port " + _port.PortName + " failed: " + ex.Message, ex);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _port.Dispose();
        }
        catch (IOException)
        {
            // Closing a port whose USB adapter was unplugged throws. There is nothing left to release.
        }
    }

    private static bool IsPortFailure(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException;
}
