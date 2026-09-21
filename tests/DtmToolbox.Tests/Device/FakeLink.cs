using System;
using System.Collections.Generic;
using System.Linq;
using DtmToolbox.Device;
using DtmToolbox.Protocol;

namespace DtmToolbox.Tests.Device;

/// <summary>
/// Link to a device that accepts every command. Records the commands it gets and answers each
/// test end with the next packet count queued in <see cref="PacketCounts"/>, zero when empty.
/// </summary>
internal sealed class FakeLink : IDtmLink
{
    private readonly object _sync = new object();
    private readonly List<DtmFrame> _commands = new List<DtmFrame>();

    public Queue<int> PacketCounts { get; } = new Queue<int>();

    /// <summary>Answers this command with an error status.</summary>
    public DtmFrame? RejectedCommand { get; set; }

    /// <summary>Answers with an error status every command this function returns true for.</summary>
    public Func<DtmFrame, bool>? RejectWhen { get; set; }

    /// <summary>Response to the transmit power command.</summary>
    public DtmFrame TransmitPowerResponse { get; set; } = new DtmFrame(0x0000);

    public bool Disposed { get; private set; }

    public IReadOnlyList<DtmFrame> Commands
    {
        get
        {
            lock (_sync)
            {
                return _commands.ToList();
            }
        }
    }

    public IReadOnlyList<int> CommandValues => Commands.Select(command => (int)command.Value).ToList();

    public DtmEvent Exchange(DtmFrame command)
    {
        lock (_sync)
        {
            _commands.Add(command);

            if ((RejectedCommand.HasValue && RejectedCommand.Value == command) || (RejectWhen != null && RejectWhen(command)))
            {
                return new DtmEvent(new DtmFrame(0x0001));
            }

            if (command == DtmCommand.TestEnd())
            {
                int count = PacketCounts.Count > 0 ? PacketCounts.Dequeue() : 0;
                return new DtmEvent(new DtmFrame((ushort)(0x8000 | count)));
            }

            if (command.High == (byte)SetupControl.SetTransmitPower)
            {
                return new DtmEvent(TransmitPowerResponse);
            }

            return new DtmEvent(new DtmFrame(0x0000));
        }
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Link whose device never answers.</summary>
internal sealed class SilentLink : IDtmLink
{
    public DtmEvent Exchange(DtmFrame command) => throw new DtmTimeoutException(command);

    public void Dispose()
    {
    }
}
