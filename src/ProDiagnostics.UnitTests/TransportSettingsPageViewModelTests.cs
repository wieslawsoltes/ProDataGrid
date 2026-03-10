using System;
using System.Collections.Generic;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class TransportSettingsPageViewModelTests
{
    [AvaloniaFact]
    public void SetOptions_Applies_Settings_And_Starts_When_Enabled()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher);

        var options = new DevToolsOptions
        {
            EnableTransportAtStartup = true,
            LaunchViewerOnStartup = true,
            ViewerCommand = "prodiagnostics-viewer",
            ViewerArguments = "--port 55000",
            ViewerTargetAppName = "MyApp",
            ViewerTargetProcessName = "MyProcess",
            ViewerTargetProcessId = 1234,
            TransportProtocol = "UDP",
            TransportHost = " 192.168.10.5 ",
            TransportPort = 55000,
            TransportMaxTagsPerMessage = 64,
            TransportHelloInterval = TimeSpan.FromSeconds(3),
            TransportIncludeActivityTags = false,
            TransportIncludeMetricTags = true,
            TransportActivitySourceNames = new[] { "MyApp.*", "System.Net.*" },
            TransportMeterNames = new[] { "MyApp.Metrics" }
        };

        viewModel.SetOptions(options);

        Assert.True(viewModel.StartTransportOnOpen);
        Assert.True(viewModel.LaunchViewerOnOpen);
        Assert.True(viewModel.IsRunning);
        Assert.Equal("udp", viewModel.TransportProtocol);
        Assert.Equal("MyApp", viewModel.ViewerTargetAppName);
        Assert.Equal("MyProcess", viewModel.ViewerTargetProcessName);
        Assert.Equal(1234, viewModel.ViewerTargetProcessId);
        Assert.Equal("Protocol: udp", viewModel.ProtocolSummary);
        Assert.Equal("Stop Export", viewModel.StartStopButtonText);
        Assert.Equal(1, service.ApplyCallCount);
        Assert.Equal(1, service.StartCallCount);
        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("prodiagnostics-viewer", launcher.LastCommand);
        Assert.Equal("--port 55000", launcher.LastArguments);
        Assert.Equal("Viewer launched (fake).", viewModel.ViewerStatusText);

        var settings = service.CurrentSettings;
        Assert.Equal("192.168.10.5", settings.Host);
        Assert.Equal(55000, settings.Port);
        Assert.Equal(64, settings.MaxTagsPerMessage);
        Assert.Equal(TimeSpan.FromSeconds(3), settings.HelloInterval);
        Assert.False(settings.IncludeActivityTags);
        Assert.True(settings.IncludeMetricTags);
        Assert.Equal(new[] { "MyApp.*", "System.Net.*" }, settings.ActivitySourceNames);
        Assert.Equal(new[] { "MyApp.Metrics" }, settings.MeterNames);
    }

    [AvaloniaFact]
    public void SetOptions_Does_Not_Launch_Viewer_When_Disabled()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher);

        var options = new DevToolsOptions
        {
            EnableTransportAtStartup = true,
            LaunchViewerOnStartup = false,
            ViewerCommand = "prodiagnostics-viewer",
            ViewerArguments = "--port 54831"
        };

        viewModel.SetOptions(options);

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.LaunchViewerOnOpen);
        Assert.Equal(0, launcher.LaunchCallCount);
        Assert.Equal("Viewer not launched.", viewModel.ViewerStatusText);
    }

    [AvaloniaFact]
    public void SetOptions_Switches_Transport_Service_When_Supported_Protocol_Is_Requested()
    {
        var factory = new FakeTransportExportServiceFactory(new[] { "udp", "mock" });
        using var viewModel = new TransportSettingsPageViewModel(
            service: null,
            viewerLauncher: null,
            transportFactory: factory);

        viewModel.SetOptions(new DevToolsOptions
        {
            TransportProtocol = "mock",
            TransportHost = "127.0.0.1",
            TransportPort = 54831
        });

        Assert.Equal("mock", viewModel.TransportProtocol);
        Assert.Contains("udp", factory.CreatedProtocols);
        Assert.Contains("mock", factory.CreatedProtocols);
    }

    [AvaloniaFact]
    public void SetOptions_Falls_Back_To_Udp_When_Protocol_Is_Not_Supported()
    {
        var factory = new FakeTransportExportServiceFactory(new[] { "udp", "mock" });
        using var viewModel = new TransportSettingsPageViewModel(
            service: null,
            viewerLauncher: null,
            transportFactory: factory);

        viewModel.SetOptions(new DevToolsOptions
        {
            TransportProtocol = "quic",
            TransportHost = "127.0.0.1",
            TransportPort = 54831
        });

        Assert.Equal("udp", viewModel.TransportProtocol);
        Assert.Contains("udp", factory.CreatedProtocols);
        Assert.Contains("quic", factory.RequestedProtocols);
    }

    [AvaloniaFact]
    public void SyncState_Exposes_Protocol_And_Packet_Counters()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service);

        service.SetRuntimeStats(protocolName: "udp", sentPackets: 42, failedPackets: 3, statusText: "Exporting to udp 127.0.0.1:54831");

        Assert.Equal("udp", viewModel.TransportProtocol);
        Assert.Equal(42, viewModel.SentPacketCount);
        Assert.Equal(3, viewModel.FailedPacketCount);
        Assert.Equal("Sent: 42 | Failed: 3", viewModel.PacketSummary);
    }

    [AvaloniaFact]
    public void Apply_Normalizes_Invalid_Values()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service)
        {
            Host = " ",
            Port = 0,
            MaxTagsPerMessage = -5,
            HelloIntervalSeconds = 0
        };

        viewModel.ActivitySourcePatterns = " , ; ";
        viewModel.MeterNamePatterns = "System.Net.*, MyApp.*, ; \n";

        viewModel.Apply();

        Assert.Equal(1, service.ApplyCallCount);
        var settings = service.CurrentSettings;

        Assert.Equal("127.0.0.1", settings.Host);
        Assert.Equal(1, settings.Port);
        Assert.Equal(0, settings.MaxTagsPerMessage);
        Assert.Equal(TimeSpan.FromSeconds(1), settings.HelloInterval);
        Assert.Equal(new[] { "*" }, settings.ActivitySourceNames);
        Assert.Equal(new[] { "System.Net.*", "MyApp.*" }, settings.MeterNames);
    }

    [AvaloniaFact]
    public void StartOrStop_Toggles_Export_State()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service);

        viewModel.StartOrStop();
        Assert.True(viewModel.IsRunning);
        Assert.Equal("Stop Export", viewModel.StartStopButtonText);
        Assert.Equal(1, service.StartCallCount);

        viewModel.StartOrStop();
        Assert.False(viewModel.IsRunning);
        Assert.Equal("Start Export", viewModel.StartStopButtonText);
        Assert.Equal(1, service.StopCallCount);
    }

    [AvaloniaFact]
    public void Apply_While_Running_Does_Not_Invoke_Start_Again()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service);

        viewModel.StartOrStop();
        Assert.Equal(1, service.StartCallCount);

        viewModel.Host = "10.0.0.15";
        viewModel.Apply();

        Assert.Equal(1, service.StartCallCount);
        Assert.Equal(2, service.ApplyCallCount);
        Assert.True(viewModel.IsRunning);
    }

    [AvaloniaFact]
    public void ResetDefaults_Reapplies_Default_Settings()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service)
        {
            Host = "10.0.0.20",
            Port = 9999,
            MaxTagsPerMessage = 10,
            HelloIntervalSeconds = 30,
            LaunchViewerOnOpen = true,
            ViewerCommand = "/custom/viewer",
            ViewerArguments = "--custom",
            ViewerTargetAppName = "SampleApp",
            ViewerTargetProcessName = "SampleProcess",
            ViewerTargetProcessId = 77
        };

        viewModel.ResetDefaults();

        Assert.Equal(1, service.ApplyCallCount);
        Assert.Equal(TransportExportSettings.Default.Host, service.CurrentSettings.Host);
        Assert.Equal(TransportExportSettings.Default.Port, service.CurrentSettings.Port);
        Assert.Equal(TransportExportSettings.Default.MaxTagsPerMessage, service.CurrentSettings.MaxTagsPerMessage);
        Assert.Equal(TransportExportSettings.Default.HelloInterval, service.CurrentSettings.HelloInterval);
        Assert.Equal("127.0.0.1", viewModel.Host);
        Assert.Equal(54831, viewModel.Port);
        Assert.False(viewModel.LaunchViewerOnOpen);
        Assert.Equal("prodiagnostics-viewer", viewModel.ViewerCommand);
        Assert.Equal(string.Empty, viewModel.ViewerArguments);
        Assert.Equal(string.Empty, viewModel.ViewerTargetAppName);
        Assert.Equal(string.Empty, viewModel.ViewerTargetProcessName);
        Assert.Equal(0, viewModel.ViewerTargetProcessId);
        Assert.Equal("Viewer not launched.", viewModel.ViewerStatusText);
    }

    [AvaloniaFact]
    public void Apply_Does_Not_Throw_For_NaN_HelloInterval()
    {
        var service = new FakeTransportExportService();
        using var viewModel = new TransportSettingsPageViewModel(service)
        {
            HelloIntervalSeconds = double.NaN
        };

        var ex = Record.Exception(() => viewModel.Apply());
        Assert.Null(ex);
        Assert.Equal(1, service.ApplyCallCount);
        Assert.Equal(TransportExportSettings.Default.HelloInterval, service.CurrentSettings.HelloInterval);
    }

    [AvaloniaFact]
    public void LaunchViewer_Uses_Default_Command_When_ViewerCommand_Is_Empty()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            ViewerCommand = " ",
            ViewerArguments = "--host 127.0.0.1 --port 54831"
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("prodiagnostics-viewer", launcher.LastCommand);
        Assert.Equal("--host 127.0.0.1 --port 54831", launcher.LastArguments);
        Assert.Equal("prodiagnostics-viewer", viewModel.ViewerCommand);
        Assert.Equal("Viewer launched (fake).", viewModel.ViewerStatusText);
    }

    [AvaloniaFact]
    public void LaunchViewer_Uses_Current_Port_When_ViewerArguments_Are_Empty()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Port = 55012,
            ViewerCommand = "prodiagnostics-viewer",
            ViewerArguments = " "
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(
            "--port 55012 --process-id " + viewModel.ProcessId,
            launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Expands_ViewerArgument_Placeholders()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Host = "192.168.0.22",
            Port = 55020,
            TransportProtocol = "udp",
            ViewerTargetAppName = "TargetApp",
            ViewerTargetProcessName = "TargetProcess",
            ViewerTargetProcessId = 9001,
            ViewerArguments = "--host {host} --port {port} --protocol {protocol} --app {appName} --process {processName} --pid {processId} --target-app {targetAppName} --target-process {targetProcessName} --target-pid {targetProcessId}"
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(
            "--host 192.168.0.22 --port 55020 --protocol udp --app " + viewModel.AppName + " --process "
            + viewModel.ProcessName + " --pid " + viewModel.ProcessId
            + " --target-app TargetApp --target-process TargetProcess --target-pid 9001",
            launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Uses_Normalized_Values_For_ViewerArgument_Placeholders()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Host = " ",
            Port = 0,
            TransportProtocol = " UDP ",
            ViewerArguments = "--host {host} --port {port} --protocol {protocol}"
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("--host 127.0.0.1 --port 1 --protocol udp", launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Uses_Normalized_Port_For_Default_Arguments()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Port = 0,
            ViewerArguments = string.Empty
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(
            "--port 1 --process-id " + viewModel.ProcessId,
            launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Uses_Explicit_Target_Filters_For_Default_Arguments()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Port = 55012,
            ViewerTargetAppName = "My App",
            ViewerTargetProcessName = "worker host",
            ViewerTargetProcessId = 3456,
            ViewerArguments = string.Empty
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(
            "--port 55012 --process-id 3456 --app-name \"My App\" --process-name \"worker host\"",
            launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Does_Not_Inject_Current_ProcessId_When_Only_Target_Names_Are_Configured()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Port = 55012,
            ViewerTargetAppName = "My App",
            ViewerTargetProcessName = "worker host",
            ViewerTargetProcessId = 0,
            ViewerArguments = string.Empty
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(
            "--port 55012 --app-name \"My App\" --process-name \"worker host\"",
            launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_TargetProcessId_Placeholder_Is_Empty_When_TargetPid_Not_Configured()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher();
        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            Host = "192.168.0.22",
            Port = 55020,
            TransportProtocol = "udp",
            ViewerTargetProcessId = 0,
            ViewerArguments = "--target-pid[{targetProcessId}]"
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("--target-pid[]", launcher.LastArguments);
    }

    [AvaloniaFact]
    public void LaunchViewer_Surfaces_Launcher_Failure_Status()
    {
        var service = new FakeTransportExportService();
        var launcher = new FakeExternalDiagnosticsViewerLauncher
        {
            ShouldSucceed = false,
            Status = "Viewer launch failed (fake)."
        };

        using var viewModel = new TransportSettingsPageViewModel(service, launcher)
        {
            ViewerCommand = "prodiagnostics-viewer",
            ViewerArguments = string.Empty
        };

        viewModel.LaunchViewer();

        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal("Viewer launch failed (fake).", viewModel.ViewerStatusText);
    }

    private sealed class FakeTransportExportService : ITransportExportService
    {
        public event EventHandler? StateChanged;

        public string ProtocolName { get; private set; } = "udp";

        public bool IsRunning { get; private set; }

        public string StatusText { get; private set; } = "Not running";

        public long SentPacketCount { get; private set; }

        public long FailedPacketCount { get; private set; }

        public TransportExportSettings CurrentSettings { get; private set; } = TransportExportSettings.Default;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int ApplyCallCount { get; private set; }

        public void Start(TransportExportSettings settings)
        {
            StartCallCount++;
            CurrentSettings = settings;
            IsRunning = true;
            StatusText = "Exporting to udp " + settings.Host + ":" + settings.Port;
            RaiseStateChanged();
        }

        public void Stop()
        {
            StopCallCount++;
            IsRunning = false;
            StatusText = "Stopped";
            RaiseStateChanged();
        }

        public void ApplySettings(TransportExportSettings settings)
        {
            ApplyCallCount++;
            CurrentSettings = settings;
            StatusText = "Configured for udp " + settings.Host + ":" + settings.Port;
            RaiseStateChanged();
        }

        public void Dispose()
        {
            IsRunning = false;
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetRuntimeStats(string protocolName, long sentPackets, long failedPackets, string statusText)
        {
            ProtocolName = protocolName;
            SentPacketCount = sentPackets;
            FailedPacketCount = failedPackets;
            StatusText = statusText;
            RaiseStateChanged();
        }
    }

    private sealed class FakeExternalDiagnosticsViewerLauncher : IExternalDiagnosticsViewerLauncher
    {
        public int LaunchCallCount { get; private set; }

        public string? LastCommand { get; private set; }

        public string? LastArguments { get; private set; }

        public bool ShouldSucceed { get; set; } = true;

        public string Status { get; set; } = "Viewer launched (fake).";

        public bool TryLaunch(string command, string arguments, out string status)
        {
            LaunchCallCount++;
            LastCommand = command;
            LastArguments = arguments;
            status = Status;
            return ShouldSucceed;
        }
    }

    private sealed class FakeTransportExportServiceFactory : ITransportExportServiceFactory
    {
        private readonly HashSet<string> _supportedProtocols;

        public FakeTransportExportServiceFactory(IEnumerable<string> supportedProtocols)
        {
            _supportedProtocols = new HashSet<string>(supportedProtocols, StringComparer.Ordinal);
            if (_supportedProtocols.Count == 0)
            {
                _supportedProtocols.Add("udp");
            }
        }

        public IReadOnlyList<string> SupportedProtocols
        {
            get
            {
                var list = new List<string>(_supportedProtocols);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        public List<string> RequestedProtocols { get; } = new();

        public List<string> CreatedProtocols { get; } = new();

        public bool TryCreate(string protocol, out ITransportExportService service)
        {
            var normalized = string.IsNullOrWhiteSpace(protocol)
                ? "udp"
                : protocol.Trim().ToLowerInvariant();
            RequestedProtocols.Add(normalized);

            if (_supportedProtocols.Contains(normalized))
            {
                CreatedProtocols.Add(normalized);
                service = new FakeProtocolTransportExportService(normalized);
                return true;
            }

            service = null!;
            return false;
        }
    }

    private sealed class FakeProtocolTransportExportService : ITransportExportService
    {
        public FakeProtocolTransportExportService(string protocolName)
        {
            ProtocolName = protocolName;
        }

        public event EventHandler? StateChanged;

        public string ProtocolName { get; }

        public bool IsRunning { get; private set; }

        public string StatusText { get; private set; } = "Not running";

        public long SentPacketCount { get; private set; }

        public long FailedPacketCount { get; private set; }

        public TransportExportSettings CurrentSettings { get; private set; } = TransportExportSettings.Default;

        public void Start(TransportExportSettings settings)
        {
            CurrentSettings = settings;
            IsRunning = true;
            StatusText = "Exporting to " + ProtocolName + " " + settings.Host + ":" + settings.Port;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            IsRunning = false;
            StatusText = "Stopped (" + ProtocolName + ")";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplySettings(TransportExportSettings settings)
        {
            CurrentSettings = settings;
            StatusText = "Configured for " + ProtocolName + " " + settings.Host + ":" + settings.Port;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            IsRunning = false;
        }
    }
}
