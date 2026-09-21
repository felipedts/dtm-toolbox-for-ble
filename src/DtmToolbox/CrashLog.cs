using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace DtmToolbox;

/// <summary>Appends unexpected errors to %APPDATA%\DtmToolbox\crash.log.</summary>
internal static class CrashLog
{
    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DtmToolbox", "crash.log");

    /// <summary>Writes the error with its stack trace. Returns false when the file cannot be written.</summary>
    public static bool Write(string origin, Exception error)
    {
        try
        {
            string? folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var text = new StringBuilder();
            text.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + origin);
            text.AppendLine("DTM Toolbox for BLE " + Version() + ", " + Environment.OSVersion + ", CLR " + Environment.Version);
            text.AppendLine(error.ToString());
            text.AppendLine();
            File.AppendAllText(FilePath, text.ToString(), Encoding.UTF8);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException
            || ex is System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string Version() =>
        typeof(CrashLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
