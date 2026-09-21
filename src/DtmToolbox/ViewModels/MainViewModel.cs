using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using DtmToolbox.Device;
using DtmToolbox.Protocol;
using DtmToolbox.Serial;
using DtmToolbox.Settings;

namespace DtmToolbox.ViewModels;

public enum MainTab
{
    Transmitter,
    Receiver,
    About,
}

/// <summary>
/// State of the main window: serial port, test settings, the running test, chart data and log.
/// Work on the device happens on worker threads; their notifications are queued and applied on
/// the UI thread by a timer, which also keeps the chart and the log from redrawing per frame.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    public const int MinTransmitPowerDbm = -40;
    public const int MaxTransmitPowerDbm = DtmCommand.MaxTransmitPowerDbm;
    public const int MinDwellTimeMs = 20;
    public const int MaxDwellTimeMs = 20000;
    public const int MinPayloadLength = 1;
    public const int MaxPayloadLength = DtmCommand.MaxPayloadLength;
    public const int MaxTimeoutSeconds = 20;

    private const int MaxLogEntries = 2000;
    private const int LogTrimCount = 200;
    private const string NoResponseHelp =
        "Check that the port is not open in another application, that the right serial port and baud rate are " +
        "selected, and that the device runs a Direct Test Mode firmware.";

    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(2);

    private readonly SettingsStore _store;
    private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
    private readonly DispatcherTimer _pump;
    private readonly int[] _received = new int[DtmChannel.Count];

    private string? _preferredPortName;
    private SerialPortInfo? _selectedPort;
    private int _baudRate;
    private Option<VendorProfile> _vendorProfile;
    private MainTab _tab = MainTab.Transmitter;
    private bool _isSweep;
    private int _channel;
    private int _sweepFirstChannel;
    private int _sweepLastChannel;
    private int _dwellTimeMs;
    private int _transmitPowerDbm;
    private Option<Phy> _phy;
    private Option<Payload> _payload;
    private bool _rebuildingPayloads;
    private int _payloadLength;
    private int _timeoutSeconds;

    private bool _isRunning;
    private bool _isBusy;
    private TestMode? _runningMode;
    private bool _runningSweep;
    private CancellationTokenSource? _stop;
    private Task? _runTask;
    private int _activeChannel = -1;
    private int? _appliedPowerDbm;
    private bool _chartDirty;
    private double[] _transmitterValues = EmptyValues();
    private double[] _receiverValues = new double[DtmChannel.Count];

    private string? _errorMessage;
    private string _deviceSummary = "Not read yet. Use Identify in the side panel.";
    private bool _showLog;
    private bool _autoScrollLog;
    private bool _showRawFrames;
    private bool _showSidePanel = true;

    public MainViewModel()
        : this(new SettingsStore())
    {
    }

    public MainViewModel(SettingsStore store)
    {
        _store = store;
        AppSettings settings = store.Load();

        BaudRates = new[] { 115200, 57600, 38400, 19200, 14400, 9600, 2400, 1200 };
        VendorProfiles = new[]
        {
            new Option<VendorProfile>("Nordic nRF5x", VendorProfile.NordicNrf5x),
            new Option<VendorProfile>("Generic", VendorProfile.Generic),
        };
        PhyOptions = new[]
        {
            new Option<Phy>("LE 1Mbps", Phy.Le1M),
            new Option<Phy>("LE 2Mbps", Phy.Le2M),
            new Option<Phy>("LE Coded S8", Phy.LeCodedS8),
            new Option<Phy>("LE Coded S2", Phy.LeCodedS2),
        };

        _preferredPortName = settings.PortName;
        _baudRate = BaudRates.Contains(settings.BaudRate) ? settings.BaudRate : SerialDtmLink.DefaultBaudRate;
        _vendorProfile = VendorProfiles.FirstOrDefault(option => option.Value == settings.VendorProfile) ?? VendorProfiles[0];
        _isSweep = settings.IsSweep;
        _channel = Clamp(settings.Channel, DtmChannel.Min, DtmChannel.Max);
        _sweepFirstChannel = Clamp(settings.SweepFirstChannel, DtmChannel.Min, DtmChannel.Max);
        _sweepLastChannel = Clamp(settings.SweepLastChannel, _sweepFirstChannel, DtmChannel.Max);
        _dwellTimeMs = Clamp(settings.DwellTimeMs, MinDwellTimeMs, MaxDwellTimeMs);
        _transmitPowerDbm = Clamp(settings.TransmitPowerDbm, MinTransmitPowerDbm, MaxTransmitPowerDbm);
        _phy = PhyOptions.FirstOrDefault(option => option.Value == settings.Phy) ?? PhyOptions[0];
        _payloadLength = Clamp(settings.PayloadLength, MinPayloadLength, MaxPayloadLength);
        _timeoutSeconds = Clamp(settings.TimeoutSeconds, 0, MaxTimeoutSeconds);
        _showLog = settings.ShowLog;
        _autoScrollLog = settings.AutoScrollLog;
        _showRawFrames = settings.ShowRawFrames;

        _payload = new Option<Payload>("PRBS9", Payload.Prbs9);
        RebuildPayloadOptions(settings.Payload);

        StartStopCommand = new RelayCommand(StartStop, () => IsRunning || (HasPort && !IsBusy && !IsAboutTab));
        IdentifyCommand = new RelayCommand(Identify, () => HasPort && !IsRunning && !IsBusy);
        RefreshPortsCommand = new RelayCommand(RefreshPorts, () => !IsRunning && !IsBusy);
        ClearLogCommand = new RelayCommand(() => Log.Clear());
        SaveLogCommand = new RelayCommand(SaveLog, () => Log.Count > 0);

        _pump = new DispatcherTimer { Interval = PumpInterval };
        _pump.Tick += (sender, e) => DrainPending();
        _pump.Start();

        RefreshPorts();
        AddLog(LogKind.Info, "DTM Toolbox for BLE " + ProductVersion + ", " + Ports.Count + " serial ports found");
    }

    /// <summary>Asks the user where to save the log. Set by the view. Returns null when cancelled.</summary>
    public Func<string?>? AskLogFilePath { get; set; }

    public ObservableCollection<SerialPortInfo> Ports { get; } = new ObservableCollection<SerialPortInfo>();

    public IReadOnlyList<int> BaudRates { get; }

    public IReadOnlyList<Option<VendorProfile>> VendorProfiles { get; }

    public IReadOnlyList<Option<Phy>> PhyOptions { get; }

    public ObservableCollection<Option<Payload>> PayloadOptions { get; } = new ObservableCollection<Option<Payload>>();

    public ObservableCollection<LogEntry> Log { get; } = new ObservableCollection<LogEntry>();

    public ICommand StartStopCommand { get; }

    public ICommand IdentifyCommand { get; }

    public ICommand RefreshPortsCommand { get; }

    public ICommand ClearLogCommand { get; }

    public ICommand SaveLogCommand { get; }

    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                if (value != null)
                {
                    _preferredPortName = value.PortName;
                }

                OnPropertyChanged(nameof(HasPort));
            }
        }
    }

    public bool HasPort => _selectedPort != null;

    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public Option<VendorProfile> SelectedVendorProfile
    {
        get => _vendorProfile;
        set
        {
            if (value != null && SetProperty(ref _vendorProfile, value))
            {
                RebuildPayloadOptions(_payload.Value);
            }
        }
    }

    public bool IsTransmitterTab
    {
        get => _tab == MainTab.Transmitter;
        set => SelectTab(value, MainTab.Transmitter);
    }

    public bool IsReceiverTab
    {
        get => _tab == MainTab.Receiver;
        set => SelectTab(value, MainTab.Receiver);
    }

    public bool IsAboutTab
    {
        get => _tab == MainTab.About;
        set => SelectTab(value, MainTab.About);
    }

    public bool IsTestTab => _tab != MainTab.About;

    public bool IsSingle
    {
        get => !_isSweep;
        set
        {
            if (value)
            {
                IsSweep = false;
            }
        }
    }

    public bool IsSweep
    {
        get => _isSweep;
        set
        {
            if (SetProperty(ref _isSweep, value))
            {
                OnPropertyChanged(nameof(IsSingle));
            }
        }
    }

    public string ChannelLabel => IsReceiverTab ? "Receive on channel" : "Transmit on channel";

    public string PeriodLabel => IsReceiverTab ? "Receive period" : "Transmit period";

    /// <summary>RF channel of a single channel test, 0 to 39 in frequency order.</summary>
    public int Channel
    {
        get => _channel;
        set
        {
            if (SetProperty(ref _channel, Clamp(value, DtmChannel.Min, DtmChannel.Max)))
            {
                OnPropertyChanged(nameof(ChannelIndex));
                OnPropertyChanged(nameof(ChannelFrequencyText));
            }
        }
    }

    /// <summary>The same channel as a link layer index, the number shown to the user.</summary>
    public int ChannelIndex
    {
        get => DtmChannel.ToLinkLayerIndex(_channel);
        set => Channel = DtmChannel.FromLinkLayerIndex(Clamp(value, 0, DtmChannel.Max));
    }

    public string ChannelFrequencyText => DtmChannel.FrequencyMhz(_channel) + " MHz";

    public int SweepFirstChannel
    {
        get => _sweepFirstChannel;
        set
        {
            if (SetProperty(ref _sweepFirstChannel, Clamp(value, DtmChannel.Min, _sweepLastChannel)))
            {
                OnPropertyChanged(nameof(SweepFirstIndex));
                OnPropertyChanged(nameof(SweepRangeText));
            }
        }
    }

    public int SweepLastChannel
    {
        get => _sweepLastChannel;
        set
        {
            if (SetProperty(ref _sweepLastChannel, Clamp(value, _sweepFirstChannel, DtmChannel.Max)))
            {
                OnPropertyChanged(nameof(SweepLastIndex));
                OnPropertyChanged(nameof(SweepRangeText));
            }
        }
    }

    public int SweepFirstIndex
    {
        get => DtmChannel.ToLinkLayerIndex(_sweepFirstChannel);
        set => SweepFirstChannel = DtmChannel.FromLinkLayerIndex(Clamp(value, 0, DtmChannel.Max));
    }

    public int SweepLastIndex
    {
        get => DtmChannel.ToLinkLayerIndex(_sweepLastChannel);
        set => SweepLastChannel = DtmChannel.FromLinkLayerIndex(Clamp(value, 0, DtmChannel.Max));
    }

    public string SweepRangeText =>
        DtmChannel.FrequencyMhz(_sweepFirstChannel) + " to " + DtmChannel.FrequencyMhz(_sweepLastChannel) + " MHz";

    public int DwellTimeMs
    {
        get => _dwellTimeMs;
        set => SetProperty(ref _dwellTimeMs, Clamp(value, MinDwellTimeMs, MaxDwellTimeMs));
    }

    public int TransmitPowerDbm
    {
        get => _transmitPowerDbm;
        set => SetProperty(ref _transmitPowerDbm, Clamp(value, MinTransmitPowerDbm, MaxTransmitPowerDbm));
    }

    public Option<Phy> SelectedPhy
    {
        get => _phy;
        set
        {
            if (value != null && SetProperty(ref _phy, value))
            {
                RebuildPayloadOptions(_payload.Value);
            }
        }
    }

    public Option<Payload> SelectedPayload
    {
        get => _payload;
        set
        {
            // The dropdown clears its selection while the option list is rebuilt.
            if (value != null && !_rebuildingPayloads && SetProperty(ref _payload, value))
            {
                OnPropertyChanged(nameof(ShowPayloadLength));
            }
        }
    }

    public bool ShowPayloadLength => _payload.Value != Payload.ConstantCarrier;

    public int PayloadLength
    {
        get => _payloadLength;
        set => SetProperty(ref _payloadLength, Clamp(value, MinPayloadLength, MaxPayloadLength));
    }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set
        {
            if (SetProperty(ref _timeoutSeconds, Clamp(value, 0, MaxTimeoutSeconds)))
            {
                OnPropertyChanged(nameof(HasTimeout));
                OnPropertyChanged(nameof(HasNoTimeout));
            }
        }
    }

    public bool HasTimeout => _timeoutSeconds > 0;

    public bool HasNoTimeout => _timeoutSeconds == 0;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(StartStopText));
                OnPropertyChanged(nameof(ShowWrongTabNotice));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>An identify request is talking to the device.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Settings can be edited: no test is running and no identify request is pending.</summary>
    public bool IsIdle => !_isRunning && !_isBusy;

    public string StartStopText => _isRunning ? "Stop test" : "Start test";

    public double[] ChartValues => IsReceiverTab ? _receiverValues : _transmitterValues;

    public bool ChartIsReceiver => IsReceiverTab;

    /// <summary>The test runs in the other tab, so the chart on screen is not the live one.</summary>
    public bool ShowWrongTabNotice =>
        _isRunning && ((_runningMode == TestMode.Transmitter && IsReceiverTab) || (_runningMode == TestMode.Receiver && IsTransmitterTab));

    public string WrongTabText => _runningMode == TestMode.Receiver
        ? "The device is running a receiver test. Switch to the RECEIVER tab to see the results."
        : "The device is running a transmitter test. Switch to the TRANSMITTER tab to see the results.";

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public string DeviceSummary
    {
        get => _deviceSummary;
        private set => SetProperty(ref _deviceSummary, value);
    }

    public bool ShowLog
    {
        get => _showLog;
        set => SetProperty(ref _showLog, value);
    }

    public bool AutoScrollLog
    {
        get => _autoScrollLog;
        set => SetProperty(ref _autoScrollLog, value);
    }

    public bool ShowRawFrames
    {
        get => _showRawFrames;
        set => SetProperty(ref _showRawFrames, value);
    }

    public bool ShowSidePanel
    {
        get => _showSidePanel;
        set => SetProperty(ref _showSidePanel, value);
    }

    /// <summary>Version and, when the build knows it, the start of the source commit: "0.1.0 (e103346)".</summary>
    public string ProductVersion
    {
        get
        {
            string version = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            int separator = version.IndexOf('+');
            if (separator < 0)
            {
                return version;
            }

            string commit = version.Substring(separator + 1);
            return version.Substring(0, separator) + " (" + commit.Substring(0, Math.Min(7, commit.Length)) + ")";
        }
    }

    /// <summary>Reads the port list again and keeps the current or last used port selected when it is still there.</summary>
    public void RefreshPorts()
    {
        string? wanted = _selectedPort?.PortName ?? _preferredPortName;
        IReadOnlyList<SerialPortInfo> found = SerialPortEnumerator.GetPorts();

        if (!found.SequenceEqual(Ports))
        {
            Ports.Clear();
            foreach (SerialPortInfo port in found)
            {
                Ports.Add(port);
            }
        }

        SelectedPort = Ports.FirstOrDefault(port => string.Equals(port.PortName, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Stops the running test, waits for the device to be left idle and saves the settings.</summary>
    public void Dispose()
    {
        _stop?.Cancel();
        try
        {
            _runTask?.Wait(ShutdownWait);
        }
        catch (AggregateException)
        {
            // The run already failed. Its error was shown when it happened.
        }

        _pump.Stop();
        _store.Save(CurrentSettings());
    }

    private static double[] EmptyValues()
    {
        var values = new double[DtmChannel.Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = double.NaN;
        }

        return values;
    }

    private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));

    private static string ChannelText(int channel) =>
        DtmChannel.ToLinkLayerIndex(channel).ToString(CultureInfo.InvariantCulture) + " (" + DtmChannel.FrequencyMhz(channel) + " MHz)";

    private void SelectTab(bool selected, MainTab tab)
    {
        if (!selected || _tab == tab)
        {
            return;
        }

        _tab = tab;
        OnPropertyChanged(nameof(IsTransmitterTab));
        OnPropertyChanged(nameof(IsReceiverTab));
        OnPropertyChanged(nameof(IsAboutTab));
        OnPropertyChanged(nameof(IsTestTab));
        OnPropertyChanged(nameof(ChannelLabel));
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(ChartValues));
        OnPropertyChanged(nameof(ChartIsReceiver));
        OnPropertyChanged(nameof(ShowWrongTabNotice));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RebuildPayloadOptions(Payload wanted)
    {
        bool coded = _phy.Value == Phy.LeCodedS8 || _phy.Value == Phy.LeCodedS2;
        var options = new List<Option<Payload>>
        {
            new Option<Payload>("PRBS9", Payload.Prbs9),
            new Option<Payload>("11110000", Payload.Pattern11110000),
            new Option<Payload>("10101010", Payload.Pattern10101010),
        };

        if (coded)
        {
            options.Add(new Option<Payload>("11111111", Payload.Pattern11111111));
        }
        else if (_vendorProfile.Value == VendorProfile.NordicNrf5x)
        {
            options.Add(new Option<Payload>("Constant carrier", Payload.ConstantCarrier));
        }

        _rebuildingPayloads = true;
        PayloadOptions.Clear();
        foreach (Option<Payload> option in options)
        {
            PayloadOptions.Add(option);
        }

        _rebuildingPayloads = false;
        _payload = options.FirstOrDefault(option => option.Value == wanted) ?? options[0];
        OnPropertyChanged(nameof(SelectedPayload));
        OnPropertyChanged(nameof(ShowPayloadLength));
    }

    private TestPlan BuildPlan()
    {
        var plan = new TestPlan
        {
            Mode = IsReceiverTab ? TestMode.Receiver : TestMode.Transmitter,
            FirstChannel = _isSweep ? _sweepFirstChannel : _channel,
            LastChannel = _isSweep ? _sweepLastChannel : _channel,
            DwellTimeMs = _dwellTimeMs,
            TimeoutMs = _timeoutSeconds * 1000,
            Phy = _phy.Value,
            PayloadLength = _payloadLength,
            TransmitPowerDbm = _transmitPowerDbm,
        };

        switch (_payload.Value)
        {
            case Payload.ConstantCarrier:
                plan.ConstantCarrier = plan.Mode == TestMode.Transmitter;
                break;
            case Payload.Pattern11110000:
                plan.PacketType = PacketType.Pattern11110000;
                break;
            case Payload.Pattern10101010:
                plan.PacketType = PacketType.Pattern10101010;
                break;
            case Payload.Pattern11111111:
                plan.PacketType = PacketType.Pattern11111111;
                break;
            default:
                plan.PacketType = PacketType.Prbs9;
                break;
        }

        return plan;
    }

    private async void StartStop()
    {
        if (_isRunning)
        {
            _stop?.Cancel();
            return;
        }

        SerialPortInfo? port = _selectedPort;
        if (port == null)
        {
            return;
        }

        TestPlan plan = BuildPlan();
        int baudRate = _baudRate;

        ErrorMessage = null;
        Array.Clear(_received, 0, _received.Length);
        _activeChannel = -1;
        _appliedPowerDbm = null;
        _runningMode = plan.Mode;
        _runningSweep = plan.IsSweep;
        OnPropertyChanged(nameof(WrongTabText));
        PublishChart();

        var stop = new CancellationTokenSource();
        _stop = stop;
        IsRunning = true;

        DtmDevice? device = null;
        try
        {
            AddLog(LogKind.Info, "Opening " + port.PortName + " at " + baudRate + " baud");
            device = await Task.Run(() => new DtmDevice(new SerialDtmLink(port.PortName, baudRate)));
            device.FrameExchanged += OnFrameExchanged;

            AddLog(LogKind.Info, DescribePlan(plan));
            Task<TestResult> run = new TestRunner(device).RunAsync(plan, new QueuedProgress(this), stop.Token);
            _runTask = run;
            TestResult result = await run;

            DrainPending();
            AddLog(LogKind.Info, DescribeResult(result));
        }
        catch (DtmException ex)
        {
            DrainPending();
            Fail(ex);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            AddLog(LogKind.Error, ex.Message);
        }
        finally
        {
            if (device != null)
            {
                device.FrameExchanged -= OnFrameExchanged;
                device.Dispose();
            }

            _runTask = null;
            _stop = null;
            stop.Dispose();
            _activeChannel = -1;
            IsRunning = false;
            PublishChart();
        }
    }

    private async void Identify()
    {
        SerialPortInfo? port = _selectedPort;
        if (port == null)
        {
            return;
        }

        int baudRate = _baudRate;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            AddLog(LogKind.Info, "Identifying the device on " + port.PortName + " at " + baudRate + " baud");
            string summary = await Task.Run(() => ReadDeviceSummary(port.PortName, baudRate));
            DrainPending();
            DeviceSummary = summary;
            foreach (string line in summary.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                AddLog(LogKind.Info, line);
            }
        }
        catch (DtmException ex)
        {
            DrainPending();
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string ReadDeviceSummary(string portName, int baudRate)
    {
        using (var device = new DtmDevice(new SerialDtmLink(portName, baudRate)))
        {
            device.FrameExchanged += OnFrameExchanged;
            device.Reset();
            DtmFeatures features = device.ReadFeatures();

            var text = new StringBuilder();
            text.AppendLine("Port: " + portName + ", " + baudRate + " baud");
            text.AppendLine("Supported features: " + (features == DtmFeatures.None ? "none reported" : features.ToString()));
            text.AppendLine("Max TX: " + ReadMax(device, MaxSupportedValue.TxOctets, "octets") + ", " + ReadMax(device, MaxSupportedValue.TxTime, "us"));
            text.Append("Max RX: " + ReadMax(device, MaxSupportedValue.RxOctets, "octets") + ", " + ReadMax(device, MaxSupportedValue.RxTime, "us"));
            return text.ToString();
        }
    }

    private static string ReadMax(DtmDevice device, MaxSupportedValue value, string unit)
    {
        try
        {
            return device.ReadMaxSupported(value).ToString(CultureInfo.InvariantCulture) + " " + unit;
        }
        catch (DtmCommandRejectedException)
        {
            return "not reported";
        }
    }

    private void Fail(DtmException ex)
    {
        string message = ex is DtmTimeoutException ? "No response from the device. " + NoResponseHelp : ex.Message;
        ErrorMessage = message;
        AddLog(LogKind.Error, ex.Message);
    }

    private string DescribePlan(TestPlan plan)
    {
        string what = plan.Mode == TestMode.Receiver
            ? "Receiver test"
            : plan.ConstantCarrier ? "Constant carrier" : "Transmitter test, " + _payload.Label + ", " + plan.PayloadLength + " bytes";
        string where = plan.IsSweep
            ? "sweep from channel " + ChannelText(plan.FirstChannel) + " to " + ChannelText(plan.LastChannel) + ", " + plan.DwellTimeMs + " ms per channel"
            : "channel " + ChannelText(plan.FirstChannel);
        string until = plan.TimeoutMs > 0 ? ", timeout " + (plan.TimeoutMs / 1000) + " s" : string.Empty;
        return what + ", " + _phy.Label + ", " + where + until;
    }

    private static string DescribeResult(TestResult result)
    {
        string reason = result.StopReason == TestStopReason.TimedOut ? "Test ended by timeout" : "Test stopped";
        return result.Mode == TestMode.Receiver
            ? reason + ". Packets received: " + result.TotalPackets.ToString(CultureInfo.InvariantCulture)
            : reason + ".";
    }

    // Called on worker threads.
    private void OnFrameExchanged(object? sender, FrameExchangedEventArgs e)
    {
        if (!_showRawFrames)
        {
            return;
        }

        string response = e.Response.HasValue ? e.Response.Value.ToString() : "no response";
        var entry = new LogEntry(e.Timestamp, LogKind.Frame, "> " + e.Command + "  < " + response + "  " + e.Operation);
        _pending.Enqueue(() => Append(entry));
    }

    private void OnProgress(TestProgress progress)
    {
        switch (progress.Kind)
        {
            case TestProgressKind.PowerApplied:
                TransmitPowerReport power = progress.Power.GetValueOrDefault();
                _appliedPowerDbm = power.LevelDbm;
                string limit = power.AtMaximum ? " (device maximum)" : power.AtMinimum ? " (device minimum)" : string.Empty;
                AddLog(LogKind.Info, "Transmit power applied: " + power.LevelDbm + " dBm" + limit);
                break;

            case TestProgressKind.ChannelStarted:
                _activeChannel = progress.Channel;
                if (!_runningSweep)
                {
                    AddLog(LogKind.Info, "Test running on channel " + ChannelText(progress.Channel));
                }

                break;

            case TestProgressKind.ChannelEnded:
                _received[progress.Channel] += progress.Packets;
                break;
        }

        _chartDirty = true;
    }

    private void DrainPending()
    {
        while (_pending.TryDequeue(out Action action))
        {
            action();
        }

        if (_chartDirty)
        {
            PublishChart();
        }
    }

    private void PublishChart()
    {
        _chartDirty = false;

        double[] transmitter = EmptyValues();
        if (_isRunning && _runningMode == TestMode.Transmitter && _activeChannel >= 0)
        {
            transmitter[_activeChannel] = _appliedPowerDbm ?? _transmitPowerDbm;
        }

        _transmitterValues = transmitter;
        _receiverValues = _received.Select(count => (double)count).ToArray();
        OnPropertyChanged(nameof(ChartValues));
    }

    private void AddLog(LogKind kind, string message) => Append(new LogEntry(DateTime.Now, kind, message));

    private void Append(LogEntry entry)
    {
        if (Log.Count >= MaxLogEntries)
        {
            for (int i = 0; i < LogTrimCount; i++)
            {
                Log.RemoveAt(0);
            }
        }

        Log.Add(entry);
    }

    private void SaveLog()
    {
        string? path = AskLogFilePath?.Invoke();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllLines(path, Log.Select(entry => entry.ToString()), Encoding.UTF8);
            AddLog(LogKind.Info, "Log saved to " + path);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            AddLog(LogKind.Error, "Cannot save the log: " + ex.Message);
        }
    }

    private AppSettings CurrentSettings() => new AppSettings
    {
        PortName = _selectedPort?.PortName ?? _preferredPortName,
        BaudRate = _baudRate,
        VendorProfile = _vendorProfile.Value,
        IsSweep = _isSweep,
        Channel = _channel,
        SweepFirstChannel = _sweepFirstChannel,
        SweepLastChannel = _sweepLastChannel,
        DwellTimeMs = _dwellTimeMs,
        TransmitPowerDbm = _transmitPowerDbm,
        Phy = _phy.Value,
        Payload = _payload.Value,
        PayloadLength = _payloadLength,
        TimeoutSeconds = _timeoutSeconds,
        ShowLog = _showLog,
        AutoScrollLog = _autoScrollLog,
        ShowRawFrames = _showRawFrames,
    };

    // Progress reports arrive on the worker thread. They are applied in order on the UI thread.
    private sealed class QueuedProgress : IProgress<TestProgress>
    {
        private readonly MainViewModel _owner;

        public QueuedProgress(MainViewModel owner)
        {
            _owner = owner;
        }

        public void Report(TestProgress value) => _owner._pending.Enqueue(() => _owner.OnProgress(value));
    }
}
