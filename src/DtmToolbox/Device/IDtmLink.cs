using System;
using DtmToolbox.Protocol;

namespace DtmToolbox.Device;

/// <summary>Transport that carries one command to the device and brings its event back.</summary>
public interface IDtmLink : IDisposable
{
    /// <summary>Sends a command and waits for the event that answers it.</summary>
    /// <exception cref="DtmTimeoutException">No complete event arrived in time.</exception>
    /// <exception cref="DtmLinkException">The transport failed.</exception>
    DtmEvent Exchange(DtmFrame command);
}
