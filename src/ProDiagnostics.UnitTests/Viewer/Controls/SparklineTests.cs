using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Headless.XUnit;
using ProDiagnostics.Viewer.Controls;
using ProDiagnostics.Viewer.Models;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Viewer.Controls;

public class SparklineTests
{
    [AvaloniaFact]
    public void Sparkline_VisibleRange_Respects_TimeRange()
    {
        var sparkline = new Sparkline
        {
            TimeRange = TimeSpan.FromSeconds(2)
        };

        var baseTime = DateTimeOffset.UtcNow;
        var samples = new List<MetricSample>
        {
            new(baseTime.AddSeconds(-4), 1),
            new(baseTime.AddSeconds(-3), 2),
            new(baseTime.AddSeconds(-2), 3),
            new(baseTime.AddSeconds(-1), 4),
            new(baseTime, 5)
        };

        Assert.True(TryGetVisibleRange(sparkline, samples, out var startIndex, out var endIndex));
        Assert.Equal(2, startIndex);
        Assert.Equal(4, endIndex);
    }

    [AvaloniaFact]
    public void Sparkline_VisibleRange_Fails_When_Too_Few_Samples()
    {
        var sparkline = new Sparkline
        {
            TimeRange = TimeSpan.FromSeconds(1)
        };

        var samples = new List<MetricSample>
        {
            new(DateTimeOffset.UtcNow, 1)
        };

        Assert.False(TryGetVisibleRange(sparkline, samples, out _, out _));
    }

    private static bool TryGetVisibleRange(
        Sparkline sparkline,
        IReadOnlyList<MetricSample> samples,
        out int startIndex,
        out int endIndex)
    {
        var method = typeof(Sparkline).GetMethod(
            "TryGetVisibleRange",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var args = new object[] { samples, 0, 0 };
        var result = (bool)method!.Invoke(sparkline, args)!;
        startIndex = (int)args[1];
        endIndex = (int)args[2];
        return result;
    }
}
