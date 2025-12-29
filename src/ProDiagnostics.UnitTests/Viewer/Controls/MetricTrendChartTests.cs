using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Headless.XUnit;
using ProDiagnostics.Viewer.Controls;
using ProDiagnostics.Viewer.Models;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer.Controls;

public class MetricTrendChartTests
{
    [AvaloniaFact]
    public void MetricTrendChart_Clamps_Zoom_And_Viewport()
    {
        var chart = new MetricTrendChart
        {
            MinZoom = 2,
            MaxZoom = 4
        };

        chart.Zoom = 1;
        Assert.Equal(2, chart.Zoom, 6);

        chart.ViewportStart = 2;
        Assert.Equal(1, chart.ViewportStart, 6);
    }

    [AvaloniaFact]
    public void MetricTrendChart_VisibleRange_Uses_Zoom_And_Viewport()
    {
        var chart = new MetricTrendChart
        {
            Zoom = 2,
            ViewportStart = 1
        };

        var samples = new List<MetricSample>();
        var start = DateTimeOffset.UtcNow.AddSeconds(-9);
        for (var i = 0; i < 10; i++)
        {
            samples.Add(new MetricSample(start.AddSeconds(i), i));
        }

        Assert.True(TryGetVisibleRange(
            chart,
            samples,
            out var startIndex,
            out var endIndex,
            out var visibleCount,
            out var maxStartIndex,
            out _,
            out _));

        Assert.Equal(5, visibleCount);
        Assert.Equal(5, maxStartIndex);
        Assert.Equal(5, startIndex);
        Assert.Equal(9, endIndex);
    }

    [AvaloniaFact]
    public void MetricTrendChart_Computes_Window_Stats()
    {
        var chart = new MetricTrendChart();

        var samples = new List<MetricSample>
        {
            new(DateTimeOffset.UtcNow.AddSeconds(-2), 10),
            new(DateTimeOffset.UtcNow.AddSeconds(-1), 20),
            new(DateTimeOffset.UtcNow, 30)
        };

        Assert.True(TryGetWindowStats(chart, samples, 0, 2, out var min, out var max, out var average));
        Assert.Equal(10, min, 6);
        Assert.Equal(30, max, 6);
        Assert.Equal(20, average, 6);
    }

    private static bool TryGetWindowStats(
        MetricTrendChart chart,
        IReadOnlyList<MetricSample> samples,
        int startIndex,
        int endIndex,
        out double min,
        out double max,
        out double average)
    {
        var method = typeof(MetricTrendChart).GetMethod(
            "TryGetWindowStats",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var args = new object[] { samples, startIndex, endIndex, 0d, 0d, 0d };
        var result = (bool)method!.Invoke(chart, args)!;
        min = (double)args[3];
        max = (double)args[4];
        average = (double)args[5];
        return result;
    }

    private static bool TryGetVisibleRange(
        MetricTrendChart chart,
        IReadOnlyList<MetricSample> samples,
        out int startIndex,
        out int endIndex,
        out int visibleCount,
        out int maxStartIndex,
        out double zoom,
        out double exactStartIndex)
    {
        var method = typeof(MetricTrendChart).GetMethod(
            "TryGetVisibleRange",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var args = new object[] { samples, 0, 0, 0, 0, 0d, 0d };
        var result = (bool)method!.Invoke(chart, args)!;
        startIndex = (int)args[1];
        endIndex = (int)args[2];
        visibleCount = (int)args[3];
        maxStartIndex = (int)args[4];
        zoom = (double)args[5];
        exactStartIndex = (double)args[6];
        return result;
    }
}
