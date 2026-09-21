using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DtmToolbox;

public partial class App : Application
{
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(2);

    private bool _showingError;
    private string? _lastError;
    private DateTime _lastErrorTime;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    // An error on the UI thread. The application stays open: losing the window in the middle of
    // a measurement is worse than a failed action, and the message says what happened.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        // The same error can come back on every timer tick while the message box is open.
        string signature = e.Exception.GetType().FullName + ": " + e.Exception.Message;
        bool repeated = signature == _lastError && DateTime.Now - _lastErrorTime < RepeatWindow;
        _lastError = signature;
        _lastErrorTime = DateTime.Now;
        if (repeated || _showingError)
        {
            return;
        }

        bool saved = CrashLog.Write("UI thread", e.Exception);
        _showingError = true;
        try
        {
            MessageBox.Show(
                "An unexpected error occurred. The application stays open, but the action that caused it did not complete.\n\n" +
                e.Exception.Message + "\n\n" +
                (saved ? "Details were saved to " + CrashLog.FilePath : "The details could not be saved to " + CrashLog.FilePath),
                "DTM Toolbox for BLE",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _showingError = false;
        }
    }

    // An error on another thread ends the process. The log is what is left to tell why.
    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception error)
        {
            CrashLog.Write("background thread", error);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashLog.Write("unobserved task", e.Exception);
        e.SetObserved();
    }
}
