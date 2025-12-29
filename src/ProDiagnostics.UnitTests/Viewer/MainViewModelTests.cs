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

    private static TelemetryMetric CreateMetric(string name, DateTimeOffset timestamp)
    {
        return new TelemetryMetric(
            Guid.NewGuid(),
            timestamp,
            "Meter",
            name,
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());
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
