using System;
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using ProDiagnostics.Viewer.Models;
using ProDiagnostics.Viewer.ViewModels;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer;

public class MetricSeriesViewModelTests
{
    [AvaloniaFact]
    public void AddSample_Updates_Statistics()
    {
        var series = CreateSeries();

        series.AddSample(new MetricSample(DateTimeOffset.UtcNow, 2));
        series.AddSample(new MetricSample(DateTimeOffset.UtcNow.AddSeconds(1), 5));
        series.AddSample(new MetricSample(DateTimeOffset.UtcNow.AddSeconds(2), 1));

        Assert.Equal(3, series.SampleCount);
        Assert.Equal(1, series.LastValue);
        Assert.Equal(1, series.MinValue);
        Assert.Equal(5, series.MaxValue);
        Assert.Equal(8.0 / 3.0, series.Average, 6);
    }

    [AvaloniaFact]
    public void Interval_Aggregation_Tracks_Average_And_Resets()
    {
        var series = CreateSeries();

        series.AddSample(new MetricSample(DateTimeOffset.UtcNow, 4));
        series.AddSample(new MetricSample(DateTimeOffset.UtcNow.AddSeconds(1), 6));

        Assert.Equal(5, series.GetIntervalValue(), 6);

        series.ResetInterval();
        Assert.Equal(series.LastValue, series.GetIntervalValue(), 6);
    }

    [AvaloniaFact]
    public void PrefillTimelineSamples_Copies_Template_Timestamps()
    {
        var series = CreateSeries();
        var template = new ObservableCollection<MetricSample>
        {
            new(DateTimeOffset.UtcNow, 10),
            new(DateTimeOffset.UtcNow.AddSeconds(1), 11)
        };

        series.PrefillTimelineSamples(template, new MetricSample(DateTimeOffset.UtcNow.AddSeconds(2), 42));

        Assert.Equal(template.Count, series.TimelineSamples.Count);
        Assert.Equal(template[0].Timestamp, series.TimelineSamples[0].Timestamp);
        Assert.Equal(42, series.TimelineSamples[0].Value);
    }

    private static MetricSeriesViewModel CreateSeries()
        => new("key", "meter", "name", "desc", "unit", "counter", string.Empty);
}
