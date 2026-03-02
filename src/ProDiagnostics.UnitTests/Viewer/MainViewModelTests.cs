using System;
using System.Linq;
using Avalonia.Headless.XUnit;
using ProDiagnostics.Transport;
using ProDiagnostics.Viewer.Models;
using ProDiagnostics.Viewer.ViewModels;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer;

public class MainViewModelTests
{
    [AvaloniaFact]
    public void MainViewModel_Default_TrendRange_Is_5s()
    {
        using var viewModel = new MainViewModel(startListening: false);

        Assert.NotNull(viewModel.SelectedTrendRange);
        Assert.Equal(TimeSpan.FromSeconds(5), viewModel.SelectedTrendRange?.Range);
    }

    [AvaloniaFact]
    public void MainViewModel_Default_Column_Visibility()
    {
        using var viewModel = new MainViewModel(startListening: false);

        Assert.True(viewModel.GetMetricColumn("metric")?.IsVisible);
        Assert.False(viewModel.GetMetricColumn("description")?.IsVisible);
        Assert.True(viewModel.GetActivityColumn("activity")?.IsVisible);
    }

    [AvaloniaFact]
    public void MainViewModel_Constructor_Applies_Custom_Port()
    {
        using var viewModel = new MainViewModel(port: 55123, startListening: false);

        Assert.Equal(55123, viewModel.Port);
        Assert.Equal("Not listening", viewModel.StatusText);
    }

    [AvaloniaFact]
    public void MainViewModel_Constructor_Normalizes_Invalid_Port_To_Default()
    {
        using var viewModel = new MainViewModel(port: 70000, startListening: false);

        Assert.Equal(TelemetryProtocol.DefaultPort, viewModel.Port);
    }

    [AvaloniaFact]
    public void MainViewModel_Port_Setter_Normalizes_Invalid_Values()
    {
        using var viewModel = new MainViewModel(startListening: false)
        {
            Port = -42
        };

        Assert.Equal(TelemetryProtocol.DefaultPort, viewModel.Port);
    }

    [AvaloniaFact]
    public void MainViewModel_TargetSummary_Uses_Target_Constraints()
    {
        using var viewModel = new MainViewModel(
            port: TelemetryProtocol.DefaultPort,
            startListening: false,
            targetAppName: "MyApp",
            targetProcessName: "MyProcess",
            targetProcessId: 42);

        Assert.Equal("Target: App=MyApp | Process=MyProcess | PID=42", viewModel.TargetSummary);
    }

    [AvaloniaFact]
    public void MainViewModel_Drops_Session_When_Target_Filter_Does_Not_Match_Hello()
    {
        using var viewModel = new MainViewModel(
            port: TelemetryProtocol.DefaultPort,
            startListening: false,
            targetAppName: "MyApp",
            targetProcessName: null,
            targetProcessId: null);

        var sessionId = Guid.NewGuid();
        viewModel.HandlePacketForTests(CreateHello(sessionId, processId: 10, processName: "ProcessA", appName: "OtherApp"));

        Assert.Empty(viewModel.Sessions);
        Assert.Null(viewModel.SelectedSession);
    }

    [AvaloniaFact]
    public void MainViewModel_Accepts_Session_When_Target_Filter_Matches_Hello()
    {
        using var viewModel = new MainViewModel(
            port: TelemetryProtocol.DefaultPort,
            startListening: false,
            targetAppName: "MyApp",
            targetProcessName: null,
            targetProcessId: null);

        var sessionId = Guid.NewGuid();
        viewModel.HandlePacketForTests(CreateHello(sessionId, processId: 10, processName: "ProcessA", appName: "MyApp"));
        viewModel.HandlePacketForTests(CreateMetric(sessionId, "cpu.usage", DateTimeOffset.UtcNow));

        Assert.Single(viewModel.Sessions);
        Assert.NotNull(viewModel.SelectedSession);
    }

    [AvaloniaFact]
    public void MainViewModel_Drops_NonHello_Packets_Until_Hello_When_Target_Filter_Is_Active()
    {
        using var viewModel = new MainViewModel(
            port: TelemetryProtocol.DefaultPort,
            startListening: false,
            targetAppName: null,
            targetProcessName: null,
            targetProcessId: 11);

        var sessionId = Guid.NewGuid();
        viewModel.HandlePacketForTests(CreateMetric(sessionId, "cpu.usage", DateTimeOffset.UtcNow));
        Assert.Empty(viewModel.Sessions);

        viewModel.HandlePacketForTests(CreateHello(sessionId, processId: 11, processName: "ProcessA", appName: "AppA"));
        viewModel.HandlePacketForTests(CreateMetric(sessionId, "cpu.usage", DateTimeOffset.UtcNow.AddMilliseconds(1)));

        Assert.Single(viewModel.Sessions);
        Assert.Single(viewModel.SelectedSession!.Metrics);
    }

    [AvaloniaFact]
    public void MainViewModel_Applies_Preset_And_Filters()
    {
        using var viewModel = new MainViewModel(startListening: false);
        var session = new SessionViewModel(Guid.NewGuid());

        session.AddMetric(CreateMetric("cpu.usage", DateTimeOffset.UtcNow));
        session.AddMetric(CreateMetric("mem.usage", DateTimeOffset.UtcNow.AddMilliseconds(10)));
        session.AddActivity(CreateActivity("http.request"));
        session.AddActivity(CreateActivity("db.query"));

        viewModel.Sessions.Add(session);
        viewModel.SelectedSession = session;
        viewModel.SelectedPreset = new PresetDefinition
        {
            IncludeMetrics = new[] { "cpu*" },
            IncludeActivities = new[] { "http*" },
            MetricAliases = { ["cpu.usage"] = "CPU Usage" }
        };

        viewModel.MetricsFilter = "Usage";
        viewModel.ActivitiesFilter = "request";

        var metrics = viewModel.SelectedSession!.MetricsView.Cast<MetricSeriesViewModel>().ToList();
        var activities = viewModel.SelectedSession.ActivitiesView.Cast<ActivityEventViewModel>().ToList();

        Assert.Single(metrics);
        Assert.Equal("CPU Usage", metrics[0].DisplayName);
        Assert.Single(activities);
        Assert.Equal("http.request", activities[0].Name);
    }

    private static TelemetryHello CreateHello(Guid sessionId, int processId, string processName, string appName)
    {
        return new TelemetryHello(
            sessionId,
            DateTimeOffset.UtcNow,
            processId,
            processName,
            appName,
            "machine",
            "runtime");
    }

    private static TelemetryMetric CreateMetric(Guid sessionId, string name, DateTimeOffset timestamp)
    {
        return new TelemetryMetric(
            sessionId,
            timestamp,
            "Meter",
            name,
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());
    }

    private static TelemetryMetric CreateMetric(string name, DateTimeOffset timestamp)
    {
        return CreateMetric(Guid.NewGuid(), name, timestamp);
    }

    private static TelemetryActivity CreateActivity(string name)
    {
        return new TelemetryActivity(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Source",
            name,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(1),
            Array.Empty<TelemetryTag>());
    }
}
