using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using DtmToolbox.Serial;
using DtmToolbox.Settings;
using DtmToolbox.ViewModels;
using Microsoft.Win32;

namespace DtmToolbox;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DeviceChangeWatcher _deviceChanges;

    public MainWindow()
    {
        InitializeComponent();

        // "--simulate" adds two devices that exist only in memory, to run the application without hardware.
        bool simulate = Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--simulate", StringComparison.OrdinalIgnoreCase));
        _viewModel = new MainViewModel(new SettingsStore(), simulate) { AskLogFilePath = AskLogFilePath };
        DataContext = _viewModel;

        _deviceChanges = new DeviceChangeWatcher();
        _deviceChanges.DevicesChanged += OnDevicesChanged;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        _deviceChanges.DevicesChanged -= OnDevicesChanged;
        _deviceChanges.Dispose();
        _viewModel.Dispose();
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // The port in use stays selected. A test that lost its port fails on its own.
        if (_viewModel.IsIdle)
        {
            _viewModel.RefreshPorts();
        }
    }

    private void OnCopyLogClick(object sender, RoutedEventArgs e) => LogView.CopyAll();

    private string? AskLogFilePath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save log",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "dtm-toolbox-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt",
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
}
