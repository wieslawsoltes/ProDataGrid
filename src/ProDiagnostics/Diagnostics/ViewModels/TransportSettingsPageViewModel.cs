using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class TransportSettingsPageViewModel : ViewModelBase, IDisposable
{
    private readonly ITransportExportServiceFactory _transportFactory;
    private readonly IExternalDiagnosticsViewerLauncher _viewerLauncher;
    private readonly bool _canSwitchTransportProtocol;
    private readonly IReadOnlyList<string> _supportedTransportProtocols;
    private ITransportExportService _transportService;
    private string _host = TransportExportSettings.Default.Host;
    private int _port = TransportExportSettings.Default.Port;
    private int _maxTagsPerMessage = TransportExportSettings.Default.MaxTagsPerMessage;
    private double _helloIntervalSeconds = TransportExportSettings.Default.HelloInterval.TotalSeconds;
    private bool _includeActivityTags = TransportExportSettings.Default.IncludeActivityTags;
    private bool _includeMetricTags = TransportExportSettings.Default.IncludeMetricTags;
    private string _activitySourcePatterns = "*";
    private string _meterNamePatterns = "*";
    private bool _isRunning;
    private string _statusText = "Not running";
    private bool _startTransportOnOpen;
    private bool _launchViewerOnOpen;
    private string _transportProtocol = "udp";
    private long _sentPacketCount;
    private long _failedPacketCount;
    private string _viewerCommand = "prodiagnostics-viewer";
    private string _viewerArguments = string.Empty;
    private string _viewerTargetAppName = string.Empty;
    private string _viewerTargetProcessName = string.Empty;
    private int _viewerTargetProcessId;
    private string _viewerStatusText = "Viewer not launched.";

    public TransportSettingsPageViewModel()
        : this(service: null, viewerLauncher: null, transportFactory: null)
    {
    }

    internal TransportSettingsPageViewModel(ITransportExportService? service)
        : this(service, viewerLauncher: null, transportFactory: null)
    {
    }

    internal TransportSettingsPageViewModel(
        ITransportExportService? service,
        IExternalDiagnosticsViewerLauncher? viewerLauncher)
        : this(service, viewerLauncher, transportFactory: null)
    {
    }

    internal TransportSettingsPageViewModel(
        ITransportExportService? service,
        IExternalDiagnosticsViewerLauncher? viewerLauncher,
        ITransportExportServiceFactory? transportFactory)
    {
        _transportFactory = transportFactory ?? new TransportExportServiceFactory();
        _supportedTransportProtocols = _transportFactory.SupportedProtocols.Count == 0
            ? new[] { UdpTransportExportService.UdpProtocolName }
            : _transportFactory.SupportedProtocols;

        if (service is null)
        {
            _transportService = CreateTransportServiceOrFallback(UdpTransportExportService.UdpProtocolName, out _);
            _canSwitchTransportProtocol = true;
        }
        else
        {
            _transportService = service;
            _canSwitchTransportProtocol = transportFactory is not null;
        }

        _viewerLauncher = viewerLauncher ?? new ExternalDiagnosticsViewerProcessLauncher();
        _transportService.StateChanged += OnTransportStateChanged;

        using var process = Process.GetCurrentProcess();
        ProcessId = process.Id;
        ProcessName = process.ProcessName;
        AppName = AppDomain.CurrentDomain.FriendlyName;
        MachineName = Environment.MachineName;
        RuntimeVersion = Environment.Version.ToString();

        LoadFromSettings(_transportService.CurrentSettings);
        SyncStateFromService();
    }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public string AppName { get; }

    public string MachineName { get; }

    public string RuntimeVersion { get; }

    public string ProcessSummary => ProcessName + " (PID: " + ProcessId + ")";

    public string RuntimeSummary => RuntimeVersion + " on " + MachineName;

    public IReadOnlyList<string> SupportedTransportProtocols => _supportedTransportProtocols;

    public string TransportProtocol
    {
        get => _transportProtocol;
        set
        {
            if (RaiseAndSetIfChanged(ref _transportProtocol, NormalizeTransportProtocol(value)))
            {
                RaisePropertyChanged(nameof(ProtocolSummary));
            }
        }
    }

    public string ProtocolSummary => "Protocol: " + TransportProtocol;

    public long SentPacketCount
    {
        get => _sentPacketCount;
        private set
        {
            if (RaiseAndSetIfChanged(ref _sentPacketCount, value))
            {
                RaisePropertyChanged(nameof(PacketSummary));
            }
        }
    }

    public long FailedPacketCount
    {
        get => _failedPacketCount;
        private set
        {
            if (RaiseAndSetIfChanged(ref _failedPacketCount, value))
            {
                RaisePropertyChanged(nameof(PacketSummary));
            }
        }
    }

    public string PacketSummary => "Sent: " + SentPacketCount + " | Failed: " + FailedPacketCount;

    public string Host
    {
        get => _host;
        set => RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => RaiseAndSetIfChanged(ref _port, value);
    }

    public int MaxTagsPerMessage
    {
        get => _maxTagsPerMessage;
        set => RaiseAndSetIfChanged(ref _maxTagsPerMessage, value);
    }

    public double HelloIntervalSeconds
    {
        get => _helloIntervalSeconds;
        set => RaiseAndSetIfChanged(ref _helloIntervalSeconds, value);
    }

    public bool IncludeActivityTags
    {
        get => _includeActivityTags;
        set => RaiseAndSetIfChanged(ref _includeActivityTags, value);
    }

    public bool IncludeMetricTags
    {
        get => _includeMetricTags;
        set => RaiseAndSetIfChanged(ref _includeMetricTags, value);
    }

    public string ActivitySourcePatterns
    {
        get => _activitySourcePatterns;
        set => RaiseAndSetIfChanged(ref _activitySourcePatterns, value);
    }

    public string MeterNamePatterns
    {
        get => _meterNamePatterns;
        set => RaiseAndSetIfChanged(ref _meterNamePatterns, value);
    }

    public bool StartTransportOnOpen
    {
        get => _startTransportOnOpen;
        set => RaiseAndSetIfChanged(ref _startTransportOnOpen, value);
    }

    public bool LaunchViewerOnOpen
    {
        get => _launchViewerOnOpen;
        set => RaiseAndSetIfChanged(ref _launchViewerOnOpen, value);
    }

    public string ViewerCommand
    {
        get => _viewerCommand;
        set => RaiseAndSetIfChanged(ref _viewerCommand, value);
    }

    public string ViewerArguments
    {
        get => _viewerArguments;
        set => RaiseAndSetIfChanged(ref _viewerArguments, value);
    }

    public string ViewerTargetAppName
    {
        get => _viewerTargetAppName;
        set => RaiseAndSetIfChanged(ref _viewerTargetAppName, value);
    }

    public string ViewerTargetProcessName
    {
        get => _viewerTargetProcessName;
        set => RaiseAndSetIfChanged(ref _viewerTargetProcessName, value);
    }

    public int ViewerTargetProcessId
    {
        get => _viewerTargetProcessId;
        set => RaiseAndSetIfChanged(ref _viewerTargetProcessId, value > 0 ? value : 0);
    }

    public string ViewerStatusText
    {
        get => _viewerStatusText;
        private set => RaiseAndSetIfChanged(ref _viewerStatusText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isRunning, value))
            {
                RaisePropertyChanged(nameof(StartStopButtonText));
            }
        }
    }

    public string StartStopButtonText => IsRunning ? "Stop Export" : "Start Export";

    public string StatusText
    {
        get => _statusText;
        private set => RaiseAndSetIfChanged(ref _statusText, value);
    }

    public void Apply()
    {
        var settings = BuildCurrentSettings();
        LoadFromSettings(settings);
        ConfigureTransport(TransportProtocol, settings, _transportService.IsRunning);
        SyncStateFromService();
    }

    public void StartOrStop()
    {
        if (_transportService.IsRunning)
        {
            _transportService.Stop();
        }
        else
        {
            var settings = BuildCurrentSettings();
            LoadFromSettings(settings);
            ConfigureTransport(TransportProtocol, settings, runAfterConfigure: true);
        }

        SyncStateFromService();
    }

    public void ResetDefaults()
    {
        LoadFromSettings(TransportExportSettings.Default);
        TransportProtocol = UdpTransportExportService.UdpProtocolName;
        LaunchViewerOnOpen = false;
        ViewerCommand = "prodiagnostics-viewer";
        ViewerArguments = string.Empty;
        ViewerTargetAppName = string.Empty;
        ViewerTargetProcessName = string.Empty;
        ViewerTargetProcessId = 0;
        ViewerStatusText = "Viewer not launched.";
        Apply();
    }

    public void SetOptions(DevToolsOptions options)
    {
        StartTransportOnOpen = options.EnableTransportAtStartup;
        LaunchViewerOnOpen = options.LaunchViewerOnStartup;
        ViewerCommand = string.IsNullOrWhiteSpace(options.ViewerCommand)
            ? "prodiagnostics-viewer"
            : options.ViewerCommand.Trim();
        ViewerArguments = options.ViewerArguments ?? string.Empty;
        ViewerTargetAppName = options.ViewerTargetAppName ?? string.Empty;
        ViewerTargetProcessName = options.ViewerTargetProcessName ?? string.Empty;
        ViewerTargetProcessId = options.ViewerTargetProcessId ?? 0;

        var settings = NormalizeSettings(new TransportExportSettings
        {
            Host = options.TransportHost,
            Port = options.TransportPort,
            MaxTagsPerMessage = options.TransportMaxTagsPerMessage,
            HelloInterval = options.TransportHelloInterval,
            IncludeActivityTags = options.TransportIncludeActivityTags,
            IncludeMetricTags = options.TransportIncludeMetricTags,
            ActivitySourceNames = options.TransportActivitySourceNames ?? new[] { "*" },
            MeterNames = options.TransportMeterNames ?? new[] { "*" }
        });

        LoadFromSettings(settings);
        ConfigureTransport(options.TransportProtocol, settings, options.EnableTransportAtStartup);

        if (options.LaunchViewerOnStartup)
        {
            LaunchViewer();
        }
        else
        {
            ViewerStatusText = "Viewer not launched.";
        }

        SyncStateFromService();
    }

    public void LaunchViewer()
    {
        var command = string.IsNullOrWhiteSpace(ViewerCommand)
            ? "prodiagnostics-viewer"
            : ViewerCommand.Trim();
        ViewerCommand = command;

        var arguments = BuildViewerArguments();
        _viewerLauncher.TryLaunch(command, arguments, out var status);
        ViewerStatusText = status;
    }

    public void Dispose()
    {
        _transportService.StateChanged -= OnTransportStateChanged;
        _transportService.Dispose();
    }

    private void OnTransportStateChanged(object? sender, EventArgs e)
    {
        SyncStateFromService();
    }

    private void SyncStateFromService()
    {
        TransportProtocol = NormalizeTransportProtocol(_transportService.ProtocolName);
        IsRunning = _transportService.IsRunning;
        StatusText = _transportService.StatusText;
        SentPacketCount = _transportService.SentPacketCount;
        FailedPacketCount = _transportService.FailedPacketCount;
    }

    private void ConfigureTransport(string? requestedProtocol, TransportExportSettings settings, bool runAfterConfigure)
    {
        var normalizedRequestedProtocol = NormalizeTransportProtocol(requestedProtocol);
        var wasRunningBefore = _transportService.IsRunning;
        var switchedService = EnsureTransportService(normalizedRequestedProtocol, out _);
        _transportService.ApplySettings(settings);

        var shouldStart =
            runAfterConfigure &&
            (switchedService || !wasRunningBefore || !_transportService.IsRunning);

        if (shouldStart)
        {
            _transportService.Start(settings);
        }

        TransportProtocol = NormalizeTransportProtocol(_transportService.ProtocolName);
    }

    private bool EnsureTransportService(string protocol, out bool fallbackUsed)
    {
        fallbackUsed = false;

        if (!_canSwitchTransportProtocol)
        {
            return false;
        }

        var currentProtocol = NormalizeTransportProtocol(_transportService.ProtocolName);
        if (string.Equals(currentProtocol, protocol, StringComparison.Ordinal))
        {
            return false;
        }

        var replacement = CreateTransportServiceOrFallback(protocol, out fallbackUsed);
        _transportService.StateChanged -= OnTransportStateChanged;
        _transportService.Dispose();
        _transportService = replacement;
        _transportService.StateChanged += OnTransportStateChanged;
        return true;
    }

    private ITransportExportService CreateTransportServiceOrFallback(string protocol, out bool fallbackUsed)
    {
        if (_transportFactory.TryCreate(protocol, out var service))
        {
            fallbackUsed = false;
            return service;
        }

        if (_transportFactory.TryCreate(UdpTransportExportService.UdpProtocolName, out service))
        {
            fallbackUsed = true;
            return service;
        }

        fallbackUsed = false;
        throw new InvalidOperationException("No compatible transport export service is available.");
    }

    private TransportExportSettings BuildCurrentSettings()
    {
        var helloSeconds = HelloIntervalSeconds;
        if (double.IsNaN(helloSeconds) || double.IsInfinity(helloSeconds))
        {
            helloSeconds = TransportExportSettings.Default.HelloInterval.TotalSeconds;
        }

        return NormalizeSettings(new TransportExportSettings
        {
            Host = string.IsNullOrWhiteSpace(Host) ? TransportExportSettings.Default.Host : Host.Trim(),
            Port = Port,
            MaxTagsPerMessage = MaxTagsPerMessage,
            HelloInterval = TimeSpan.FromSeconds(helloSeconds),
            IncludeActivityTags = IncludeActivityTags,
            IncludeMetricTags = IncludeMetricTags,
            ActivitySourceNames = ParsePatterns(ActivitySourcePatterns),
            MeterNames = ParsePatterns(MeterNamePatterns)
        });
    }

    private static TransportExportSettings NormalizeSettings(TransportExportSettings settings)
    {
        var clampedPort = settings.Port;
        if (clampedPort < 1)
        {
            clampedPort = 1;
        }
        else if (clampedPort > 65535)
        {
            clampedPort = 65535;
        }

        var clampedMaxTags = settings.MaxTagsPerMessage;
        if (clampedMaxTags < 0)
        {
            clampedMaxTags = 0;
        }
        else if (clampedMaxTags > 256)
        {
            clampedMaxTags = 256;
        }

        var helloInterval = settings.HelloInterval;
        if (helloInterval <= TimeSpan.Zero)
        {
            helloInterval = TimeSpan.FromSeconds(1);
        }

        var normalizedHost = string.IsNullOrWhiteSpace(settings.Host)
            ? TransportExportSettings.Default.Host
            : settings.Host.Trim();
        var activitySources = settings.ActivitySourceNames.Count == 0
            ? new[] { "*" }
            : settings.ActivitySourceNames;
        var meterNames = settings.MeterNames.Count == 0
            ? new[] { "*" }
            : settings.MeterNames;

        return new TransportExportSettings
        {
            Host = normalizedHost,
            Port = clampedPort,
            MaxTagsPerMessage = clampedMaxTags,
            HelloInterval = helloInterval,
            IncludeActivityTags = settings.IncludeActivityTags,
            IncludeMetricTags = settings.IncludeMetricTags,
            ActivitySourceNames = activitySources,
            MeterNames = meterNames
        };
    }

    private void LoadFromSettings(TransportExportSettings settings)
    {
        Host = settings.Host;
        Port = settings.Port;
        MaxTagsPerMessage = settings.MaxTagsPerMessage;
        HelloIntervalSeconds = settings.HelloInterval.TotalSeconds;
        IncludeActivityTags = settings.IncludeActivityTags;
        IncludeMetricTags = settings.IncludeMetricTags;
        ActivitySourcePatterns = string.Join(", ", settings.ActivitySourceNames);
        MeterNamePatterns = string.Join(", ", settings.MeterNames);
    }

    private static IReadOnlyList<string> ParsePatterns(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { "*" };
        }

        var parts = text.Split(
            new[] { ',', ';', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return new[] { "*" };
        }

        return parts;
    }

    private static string NormalizeTransportProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return UdpTransportExportService.UdpProtocolName;
        }

        return protocol.Trim().ToLowerInvariant();
    }

    private string BuildViewerArguments()
    {
        var settings = NormalizeSettings(BuildCurrentSettings());
        var protocol = NormalizeTransportProtocol(TransportProtocol);
        var appName = string.IsNullOrWhiteSpace(AppName) ? string.Empty : AppName.Trim();
        var processName = string.IsNullOrWhiteSpace(ProcessName) ? string.Empty : ProcessName.Trim();
        var processId = ProcessId > 0 ? ProcessId : 0;
        var targetAppName = NormalizeViewerTargetName(ViewerTargetAppName);
        var targetProcessName = NormalizeViewerTargetName(ViewerTargetProcessName);
        var explicitTargetProcessId = NormalizeViewerTargetProcessId(ViewerTargetProcessId);
        var hasExplicitTargetFilter = targetAppName != null || targetProcessName != null || explicitTargetProcessId.HasValue;
        var defaultTargetProcessId = hasExplicitTargetFilter ? explicitTargetProcessId : processId;
        var targetProcessIdToken = explicitTargetProcessId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ViewerArguments))
        {
            return BuildDefaultViewerArguments(settings.Port, targetAppName, targetProcessName, defaultTargetProcessId);
        }

        return ViewerArguments
            .Replace("{host}", settings.Host, StringComparison.OrdinalIgnoreCase)
            .Replace("{port}", settings.Port.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{protocol}", protocol, StringComparison.OrdinalIgnoreCase)
            .Replace("{appName}", appName, StringComparison.OrdinalIgnoreCase)
            .Replace("{processName}", processName, StringComparison.OrdinalIgnoreCase)
            .Replace("{processId}", processId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{targetAppName}", targetAppName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{targetProcessName}", targetProcessName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{targetProcessId}", targetProcessIdToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDefaultViewerArguments(int port, string? targetAppName, string? targetProcessName, int? targetProcessId)
    {
        var builder = new StringBuilder();
        builder.Append("--port ");
        builder.Append(port.ToString(CultureInfo.InvariantCulture));

        if (targetProcessId is > 0)
        {
            builder.Append(" --process-id ");
            builder.Append(targetProcessId.Value.ToString(CultureInfo.InvariantCulture));
        }

        AppendTextOption(builder, "--app-name", targetAppName);
        AppendTextOption(builder, "--process-name", targetProcessName);
        return builder.ToString();
    }

    private static void AppendTextOption(StringBuilder builder, string optionName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(' ');
        builder.Append(optionName);
        builder.Append(' ');
        builder.Append(EscapeArgument(value));
    }

    private static string EscapeArgument(string value)
    {
        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return value;
        }

        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            + "\"";
    }

    private static string? NormalizeViewerTargetName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static int? NormalizeViewerTargetProcessId(int value)
    {
        return value > 0 ? value : null;
    }
}
