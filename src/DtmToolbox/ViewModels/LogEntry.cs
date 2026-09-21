using System;
using System.Globalization;

namespace DtmToolbox.ViewModels;

public enum LogKind
{
    Info,
    Frame,
    Error,
}

/// <summary>One line of the log pane.</summary>
public sealed class LogEntry
{
    public LogEntry(DateTime timestamp, LogKind kind, string message)
    {
        Timestamp = timestamp;
        Kind = kind;
        Message = message;
    }

    public DateTime Timestamp { get; }

    public LogKind Kind { get; }

    public string Message { get; }

    public string Time => Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>The line as it goes to a saved log file.</summary>
    public override string ToString() =>
        Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + Message;
}
