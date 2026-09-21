using System;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DtmToolbox.Serial;

/// <summary>
/// Tells when devices were plugged or unplugged, so that a port list can be refreshed. Windows
/// broadcasts WM_DEVICECHANGE to top-level windows; this class owns a hidden one to receive it.
/// Create and dispose it on the UI thread.
/// </summary>
public sealed class DeviceChangeWatcher : IDisposable
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDevNodesChanged = 0x0007;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;

    // One plug or unplug produces a burst of messages, and a new port takes a moment to show up.
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    private readonly HwndSource _window;
    private readonly DispatcherTimer _settleTimer;

    public DeviceChangeWatcher()
    {
        var parameters = new HwndSourceParameters("DtmToolbox.DeviceChangeWatcher")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _window = new HwndSource(parameters);
        _window.AddHook(WindowProc);

        _settleTimer = new DispatcherTimer { Interval = SettleTime };
        _settleTimer.Tick += OnSettled;
    }

    /// <summary>Raised on the UI thread once the burst of change messages has settled.</summary>
    public event EventHandler? DevicesChanged;

    public void Dispose()
    {
        _settleTimer.Stop();
        _settleTimer.Tick -= OnSettled;
        _window.RemoveHook(WindowProc);
        _window.Dispose();
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmDeviceChange)
        {
            int change = wParam.ToInt32();
            if (change == DbtDevNodesChanged || change == DbtDeviceArrival || change == DbtDeviceRemoveComplete)
            {
                _settleTimer.Stop();
                _settleTimer.Start();
            }
        }

        return IntPtr.Zero;
    }

    private void OnSettled(object? sender, EventArgs e)
    {
        _settleTimer.Stop();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }
}
