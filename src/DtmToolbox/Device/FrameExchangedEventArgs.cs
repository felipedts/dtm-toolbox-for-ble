using System;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>One command sent to the device and what came back, for the log.</summary>
public sealed class FrameExchangedEventArgs : EventArgs
{
    public FrameExchangedEventArgs(string operation, DtmFrame command, DtmEvent? response, DtmException? failure)
    {
        Timestamp = DateTime.Now;
        Operation = operation;
        Command = command;
        Response = response;
        Failure = failure;
    }

    public DateTime Timestamp { get; }

    /// <summary>What the command does, in words.</summary>
    public string Operation { get; }

    public DtmFrame Command { get; }

    /// <summary>Null when the exchange failed before an event arrived.</summary>
    public DtmEvent? Response { get; }

    public DtmException? Failure { get; }
}
