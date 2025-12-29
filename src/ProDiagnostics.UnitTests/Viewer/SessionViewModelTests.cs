using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Headless.XUnit;
using ProDiagnostics.Transport;
using ProDiagnostics.Viewer.Models;
using ProDiagnostics.Viewer.ViewModels;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer;

public class SessionViewModelTests
{
    [AvaloniaFact]
    public void AddMetric_Reuses_Series_For_Same_Tag_Key()
    {
        var session = new SessionViewModel(Guid.NewGuid());
        var timestamp = DateTimeOffset.UtcNow;

        var tagsA = new List<TelemetryTag> { new("b", 2), new("a", 1) };
        var tagsB = new List<TelemetryTag> { new("a", 1), new("b", 2) };

        session.AddMetric(CreateMetric("Counter", timestamp, tagsA));
        session.AddMetric(CreateMetric("Counter", timestamp.AddMilliseconds(20), tagsB));

        Assert.Single(session.Metrics);
        Assert.Equal(2, session.Metrics[0].SampleCount);
    }

    [AvaloniaFact]
    public void AddMetric_Prefills_Timeline_For_New_Series()
    {
        var session = new SessionViewModel(Guid.NewGuid());
        var baseTime = DateTimeOffset.UtcNow;

        session.AddMetric(CreateMetric("Counter", baseTime, Array.Empty<TelemetryTag>()));
        session.AddMetric(CreateMetric("Counter", baseTime.AddMilliseconds(400), Array.Empty<TelemetryTag>()));

        var newSeries = session.AddMetric(CreateMetric("Histogram", baseTime.AddMilliseconds(500), Array.Empty<TelemetryTag>()));

        Assert.True(session.Metrics[0].TimelineSamples.Count > 0);
        Assert.Equal(session.Metrics[0].TimelineSamples.Count, newSeries.TimelineSamples.Count);
        Assert.Equal(session.Metrics[0].TimelineSamples[0].Timestamp, newSeries.TimelineSamples[0].Timestamp);
    }

    [AvaloniaFact]
    public void AddActivity_Caps_Max_Items()
    {
        var session = new SessionViewModel(Guid.NewGuid());
        for (var i = 0; i < 610; i++)
        {
            session.AddActivity(new TelemetryActivity(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Source",
                $"Activity{i}",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(1),
                Array.Empty<TelemetryTag>()));
        }

        Assert.Equal(600, session.Activities.Count);
    }

    [AvaloniaFact]
    public void OpenMetricTab_Deduplicates_Series()
    {
        var session = new SessionViewModel(Guid.NewGuid());
        var series = session.AddMetric(CreateMetric("Counter", DateTimeOffset.UtcNow, Array.Empty<TelemetryTag>()));

        session.OpenMetricTab(series);
        session.OpenMetricTab(series);

        Assert.Single(session.MetricTabs);
        Assert.Same(series, session.SelectedMetricTab?.Series);
    }

    [AvaloniaFact]
    public void ApplyPreset_Updates_Aliases()
    {
        var session = new SessionViewModel(Guid.NewGuid());
        var metric = session.AddMetric(CreateMetric("Counter", DateTimeOffset.UtcNow, Array.Empty<TelemetryTag>()));
        var activity = session.AddActivity(new TelemetryActivity(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(1),
            Array.Empty<TelemetryTag>()));

        var preset = new PresetDefinition
        {
            MetricAliases = { [metric.Name] = "Counter Alias" },
            ActivityAliases = { [activity.Name] = "Activity Alias" }
        };

        session.ApplyPreset(preset);

        Assert.Equal("Counter Alias", metric.DisplayName);
        Assert.Equal("Activity Alias", activity.DisplayName);
    }

    [AvaloniaFact]
    public void TrendRange_Clamps_To_Default()
    {
        var session = new SessionViewModel(Guid.NewGuid())
        {
            TrendRange = TimeSpan.Zero
        };

        Assert.Equal(TimeSpan.FromSeconds(5), session.TrendRange);
    }

    private static TelemetryMetric CreateMetric(string name, DateTimeOffset timestamp, IReadOnlyList<TelemetryTag> tags)
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
            tags);
    }
}
