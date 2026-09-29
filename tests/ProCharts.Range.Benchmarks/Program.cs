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
Console.WriteLine("Actual public HitTest/TryGetViewportInfo APIs. Reference = UseInteractionCache:false; indexed = true. Same data, axes and queries; source construction excluded.");
Console.WriteLine("Two paired warmups; seven paired measured repetitions with alternating reference/indexed order. GC before each measurement. Cold index construction is included only in cold_first_hit.");
Console.WriteLine("scenario,path,median_ms,median_allocated_bytes,checksum");
foreach (int count in new[] { 10000, 100000 })
{
    double?[] low = new double?[count], high = new double?[count];
    double[] x = new double[count];
    for (int i = 0; i < count; i++)
    {
        x[i] = i + 1;
        low[i] = 30 + Math.Sin(i * 0.007) * 15;
        high[i] = low[i] + 30;
        if (i % 4093 == 17) low[i] = null;
    }
    ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[] { ChartRangeSeries.CreateArea("Envelope", low, high, x) });
    SkiaChartStyle style = new()
    {
        ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
        PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
        CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 1, CategoryAxisMaximum = count,
        ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 5
    };
    SKRect bounds = SKRect.Create(1200, 600);
    SKPoint[] queries = new SKPoint[128];
    for (int i = 0; i < queries.Length; i++)
        queries[i] = new SKPoint(10 + (i * 937 % 1180), i % 4 == 0 ? 30 : 250 + i % 3 * 35);
    SkiaChartRenderer reference = new() { UseInteractionCache = false }, indexed = new();
    foreach (SKPoint query in queries)
    {
        var a = reference.HitTest(query, bounds, snapshot, style);
        var b = indexed.HitTest(query, bounds, snapshot, style);
        if (!a.Equals(b)) throw new InvalidOperationException($"Reference/index hit mismatch at {query}.");
    }
    Compare($"cold_first_hit_{count}",
        () => Digest(new SkiaChartRenderer { UseInteractionCache = false }.HitTest(queries[127], bounds, snapshot, style)),
        () => Digest(new SkiaChartRenderer().HitTest(queries[127], bounds, snapshot, style)));
    Compare($"warm_128_hits_{count}", () => HitBatch(reference), () => HitBatch(indexed));
    Compare($"warm_32_viewports_{count}", () => Viewports(reference), () => Viewports(indexed));

    long HitBatch(SkiaChartRenderer renderer)
    {
        long digest = 0;
        foreach (SKPoint query in queries) digest = unchecked(digest * 31 + Digest(renderer.HitTest(query, bounds, snapshot, style)));
        return digest;
    }
    long Viewports(SkiaChartRenderer renderer)
    {
        long digest = 0;
        for (int i = 0; i < 32; i++)
        {
            if (!renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport))
                throw new InvalidOperationException("Expected a finite viewport.");
            digest += (long)viewport.MaxValue;
        }
        return digest;
    }
}
Console.WriteLine("Original range geometry scans are retained unchanged when caching is disabled. These measurements are interaction/data preparation, not raster/GPU/FPS measurements. The index retains projected data; cold time and allocation costs are deliberately reported.");

static long Digest(SkiaChartHitTestResult? hit) => hit.HasValue ? hit.Value.PointIndex + 1L : -1;

static void Compare(string scenario, Func<long> reference, Func<long> indexed)
{
    for (int i = 0; i < 2; i++)
        if (reference() != indexed()) throw new InvalidOperationException("Warmup result mismatch.");
    double[] baselineTime = new double[7], indexedTime = new double[7];
    long[] baselineBytes = new long[7], indexedBytes = new long[7];
    long checksum = 0;
    for (int i = 0; i < 7; i++)
    {
        long a, b;
        if ((i & 1) == 0)
        {
            a = Measure(reference, out baselineTime[i], out baselineBytes[i]);
            b = Measure(indexed, out indexedTime[i], out indexedBytes[i]);
        }
        else
        {
            b = Measure(indexed, out indexedTime[i], out indexedBytes[i]);
            a = Measure(reference, out baselineTime[i], out baselineBytes[i]);
        }
        if (a != b) throw new InvalidOperationException("Measured result mismatch.");
        checksum = a;
    }
    Array.Sort(baselineTime); Array.Sort(indexedTime); Array.Sort(baselineBytes); Array.Sort(indexedBytes);
    Console.WriteLine($"{scenario},reference,{baselineTime[3].ToString("F3", CultureInfo.InvariantCulture)},{baselineBytes[3]},{checksum}");
    Console.WriteLine($"{scenario},indexed,{indexedTime[3].ToString("F3", CultureInfo.InvariantCulture)},{indexedBytes[3]},{checksum}");
}

static long Measure(Func<long> operation, out double milliseconds, out long bytes)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long allocation = GC.GetAllocatedBytesForCurrentThread();
    long start = Stopwatch.GetTimestamp();
    long result = operation();
    milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
    return result;
}
