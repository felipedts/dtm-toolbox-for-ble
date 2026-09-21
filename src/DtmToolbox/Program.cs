using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DtmToolbox;

/// <summary>
/// Entry point. Verifies the installed .NET Framework before any WPF type is loaded,
/// so that a machine below the required version gets a readable message instead of
/// a type load failure.
/// </summary>
internal static class Program
{
    private const string ProductName = "DTM Toolbox for BLE";

    // "Release" value of .NET Framework 4.6.2, the oldest version this application runs on.
    // Values for every release: https://learn.microsoft.com/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
    private const int RequiredFrameworkRelease = 394802;
    private const string RequiredFrameworkName = ".NET Framework 4.6.2";

    private const uint MessageBoxOk = 0x00000000;
    private const uint MessageBoxIconError = 0x00000010;

    [STAThread]
    private static int Main()
    {
        if (InstalledFrameworkRelease() < RequiredFrameworkRelease)
        {
            MessageBoxW(
                IntPtr.Zero,
                ProductName + " requires " + RequiredFrameworkName + " or later.\n\n" +
                "Install it from https://dotnet.microsoft.com/download/dotnet-framework and start the application again.",
                ProductName,
                MessageBoxOk | MessageBoxIconError);
            return 1;
        }

        return RunApplication();
    }

    // Kept out of Main so the JIT does not touch WPF assemblies before the version check runs.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication()
    {
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static int InstalledFrameworkRelease()
    {
        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
        {
            return key?.GetValue("Release") as int? ?? 0;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
