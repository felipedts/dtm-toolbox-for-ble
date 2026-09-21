using System;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>Base class of the failures raised while talking to a device.</summary>
public class DtmException : Exception
{
    public DtmException(string message)
        : base(message)
    {
    }

    public DtmException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The serial port could not be opened, or failed during an exchange.</summary>
public sealed class DtmLinkException : DtmException
{
    public DtmLinkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The device did not answer a command within the response timeout.</summary>
public sealed class DtmTimeoutException : DtmException
{
    public DtmTimeoutException(DtmFrame command)
        : base("No response to command " + command + ".")
    {
        Command = command;
    }

    public DtmFrame Command { get; }
}

/// <summary>The device answered a command with the error flag set.</summary>
public sealed class DtmCommandRejectedException : DtmException
{
    public DtmCommandRejectedException(string operation, DtmFrame command, DtmEvent response)
        : base("The device rejected \"" + operation + "\" (command " + command + ", response " + response + ").")
    {
        Command = command;
        Response = response;
    }

    public DtmFrame Command { get; }

    public DtmEvent Response { get; }
}
