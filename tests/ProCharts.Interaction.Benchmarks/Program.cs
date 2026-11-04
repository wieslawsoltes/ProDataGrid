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
Console.WriteLine("2 warmups; median of 7 measurements. Data construction excluded; cold/warm queries are separate. No GPU/frame-rate measurement.");
Console.WriteLine("scenario,median_ms,median_allocated_bytes");
foreach (int count in new[] { 10000, 100000 })
{
    double?[] values = new double?[count];
    double[] xs = new double[count];
    for (int i = 0; i < count; i++) { values[i] = 50 + 35 * Math.Sin(i * 0.01); xs[i] = i * 100d / (count - 1); }
    ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[] { new ChartSeriesSnapshot("Signal", ChartSeriesKind.Scatter, values, xs) });
    SkiaChartStyle style = new()
    {
        ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
        PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
        CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 0, CategoryAxisMaximum = 100,
        ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 8
    };
    SKRect bounds = SKRect.Create(1200, 600);
    SKPoint pointer = new(0, 300);
    Measure($"cold_first_hit_{count}", () =>
    {
        var hit = new SkiaChartRenderer().HitTest(pointer, bounds, snapshot, style);
        if (!hit.HasValue || hit.Value.PointIndex != 0) throw new InvalidOperationException("Known point changed.");
    });
    SkiaChartRenderer renderer = new();
    renderer.HitTest(pointer, bounds, snapshot, style);
    SKPoint[] pointers = new SKPoint[128];
    for (int i = 0; i < pointers.Length; i++)
    {
        int p = (i * 719) % count;
        pointers[i] = new SKPoint((float)(xs[p] / 100 * 1200), (float)(600 - values[p]!.Value / 100 * 600));
    }
    Measure($"warm_128_hits_{count}", () =>
    {
        foreach (SKPoint p in pointers)
            if (!renderer.HitTest(p, bounds, snapshot, style).HasValue) throw new InvalidOperationException("Point disappeared.");
    });
    Measure($"warm_32_viewports_{count}", () =>
    {
        for (int i = 0; i < 32; i++)
            if (!renderer.TryGetViewportInfo(bounds, snapshot, style, out var info) || info.MinValue != 0 || info.MaxValue != 100)
                throw new InvalidOperationException("Viewport changed.");
    });
}

static void Measure(string name, Action action)
{
    for (int i = 0; i < 2; i++) action();
    double[] times = new double[7];
    long[] bytes = new long[7];
    for (int i = 0; i < 7; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        action();
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes[i] = GC.GetAllocatedBytesForCurrentThread() - allocation;
    }
    Array.Sort(times); Array.Sort(bytes);
    Console.WriteLine($"{name},{times[3].ToString("F3", CultureInfo.InvariantCulture)},{bytes[3]}");
}
