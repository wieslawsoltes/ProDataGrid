// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; processors: {Environment.ProcessorCount}");
Console.WriteLine("Paired full-resolution / coordinated-MinMax display experiment; approximate reduced geometry, NOT an equivalent-output renderer optimization.");
Console.WriteLine("Budget: 2000. Two paired warmups, seven paired measurements with alternating execution order. Input ownership/construction excluded; all per-view selection and copying included in preparation cases.");
Console.WriteLine("Raster: 1200x600 CPU SKBitmap/SKCanvas, prepared snapshots and warm renderer layout, no picture/render-cache replay, native drawing included. No physical GPU, window presentation, FPS or frame pacing measurement.");
Console.WriteLine("scenario,path,median_ms,median_allocated_bytes");
foreach (int count in new[] { 10000, 100000 })
{
    double?[] low = new double?[count], high = new double?[count];
    double[] x = new double[count];
    for (int i = 0; i < count; i++)
    {
        x[i] = i + 1;
        low[i] = 80 + Math.Sin(i * 0.0007) * 15 + Math.Cos(i * 0.031) * 0.5;
        high[i] = low[i] + 20 + Math.Sin(i * 0.0011) * 4;
        if (i >= count / 2 && i < count / 2 + 25) low[i] = null;
    }
    low[count / 3] = 30; high[count * 2 / 3] = 150;
    RangeChartDataSource fullSource = new("Envelope", low, high, x);
    RangeChartDataSource reducedSource = new("Envelope", low, high, x);
    ChartDataRequest fullRequest = new() { DownsampleMode = ChartDownsampleMode.None };
    ChartDataRequest reducedRequest = new() { MaxPoints = 2000, DownsampleMode = ChartDownsampleMode.MinMax };
    ChartRangeView full = fullSource.BuildView(fullRequest), reduced = reducedSource.BuildView(reducedRequest);
    Validate(reduced, low, high, x);
    Console.WriteLine($"Input {count}, actual reduced display intervals {reduced.SourcePointIndices.Count}, full intervals {full.SourcePointIndices.Count}.");
    if (!Contains(reduced.SourcePointIndices, count / 3) || !Contains(reduced.SourcePointIndices, count * 2 / 3) ||
        !Contains(reduced.SourcePointIndices, count / 2)) throw new InvalidOperationException("An extremum or gap separator disappeared.");

    ChartDataRequest[] fullWindows = { new() { WindowStart = 0, WindowCount = count - 1, DownsampleMode = ChartDownsampleMode.None },
        new() { WindowStart = 1, WindowCount = count - 1, DownsampleMode = ChartDownsampleMode.None } };
    ChartDataRequest[] reducedWindows = { new() { WindowStart = 0, WindowCount = count - 1, MaxPoints = 2000 },
        new() { WindowStart = 1, WindowCount = count - 1, MaxPoints = 2000 } };
    Compare($"two_uncached_window_views_{count}", () => Prepare(fullSource, fullWindows), () => Prepare(reducedSource, reducedWindows));

    full = fullSource.BuildView(fullRequest); reduced = reducedSource.BuildView(reducedRequest);
    Compare($"cached_view_reads_10000_{count}", () => ReadCached(fullSource, fullRequest, full), () => ReadCached(reducedSource, reducedRequest, reduced));
    using SKBitmap fullBitmap = new(1200, 600), reducedBitmap = new(1200, 600);
    using SKCanvas fullCanvas = new(fullBitmap), reducedCanvas = new(reducedBitmap);
    SkiaChartRenderer fullRenderer = new(), reducedRenderer = new();
    SkiaChartStyle style = new()
    {
        ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
        PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
        CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 1, CategoryAxisMaximum = count,
        ValueAxisMinimum = 0, ValueAxisMaximum = 200, AreaFillOpacity = 0.35f
    };
    SKRect bounds = SKRect.Create(1200, 600);
    Compare($"cpu_raster_draw_{count}",
        () => fullRenderer.Render(fullCanvas, bounds, full.Snapshot, style),
        () => reducedRenderer.Render(reducedCanvas, bounds, reduced.Snapshot, style));
}
Console.WriteLine("All retained bound/X/source-index and gap/extremum checks passed. Reduction trades omitted geometric detail for fewer draw vertices. Full-resolution data remains available with None. Shared-runner time and managed allocation are not peak RSS or retained native memory.");

static void Validate(ChartRangeView view, double?[] low, double?[] high, double[] x)
{
    ChartSeriesSnapshot series = view.Snapshot.Series[0];
    int previous = -1;
    for (int i = 0; i < view.SourcePointIndices.Count; i++)
    {
        int index = view.SourcePointIndices[i];
        if (index <= previous || series.XValues![i] != x[index] || series.LowValues![i] != low[index] ||
            series.HighValues![i] != (low[index].HasValue ? high[index] : null))
            throw new InvalidOperationException("Selected channels lost their original identity.");
        previous = index;
    }
}

static bool Contains(System.Collections.Generic.IReadOnlyList<int> values, int target)
{
    for (int i = 0; i < values.Count; i++) if (values[i] == target) return true;
    return false;
}

static void Prepare(RangeChartDataSource source, ChartDataRequest[] requests)
{
    foreach (ChartDataRequest request in requests)
    {
        ChartRangeView view = source.BuildView(request);
        if (view.SourcePointIndices[0] != request.WindowStart ||
            view.SourcePointIndices[^1] != request.WindowStart + request.WindowCount - 1)
            throw new InvalidOperationException("Window endpoints changed during preparation.");
    }
}

static void ReadCached(RangeChartDataSource source, ChartDataRequest request, ChartRangeView expected)
{
    for (int i = 0; i < 10000; i++)
        if (!ReferenceEquals(source.BuildView(request), expected)) throw new InvalidOperationException("Cached view was replaced.");
}

static void Compare(string scenario, Action full, Action reduced)
{
    for (int i = 0; i < 2; i++) { full(); reduced(); }
    double[] fullTimes = new double[7], reducedTimes = new double[7];
    long[] fullBytes = new long[7], reducedBytes = new long[7];
    for (int i = 0; i < 7; i++)
    {
        if ((i & 1) == 0)
        {
            Measure(full, out fullTimes[i], out fullBytes[i]);
            Measure(reduced, out reducedTimes[i], out reducedBytes[i]);
        }
        else
        {
            Measure(reduced, out reducedTimes[i], out reducedBytes[i]);
            Measure(full, out fullTimes[i], out fullBytes[i]);
        }
    }
    Array.Sort(fullTimes); Array.Sort(reducedTimes); Array.Sort(fullBytes); Array.Sort(reducedBytes);
    Console.WriteLine($"{scenario},full,{fullTimes[3].ToString("F3", CultureInfo.InvariantCulture)},{fullBytes[3]}");
    Console.WriteLine($"{scenario},reduced,{reducedTimes[3].ToString("F3", CultureInfo.InvariantCulture)},{reducedBytes[3]}");
}

static void Measure(Action operation, out double milliseconds, out long allocated)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
    operation();
    milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
}
