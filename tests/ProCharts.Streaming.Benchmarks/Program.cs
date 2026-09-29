// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ProCharts;

const int initial = 4096, updates = 512, count = initial + updates;
double?[] prices = new double?[count];
for (int i = 0; i < count; i++) prices[i] = i % 503 == 17 ? null : 100 + Math.Sin(i * 0.07) * 10;
Prefix prefix = new(prices);
ChartSeriesSnapshot source = new("Price", ChartSeriesKind.Line, prefix);
StreamingMacd incremental = new();
// Compare every emitted channel before benchmarking, not only a final checksum.
ChartMacdSeries reference = ChartIndicators.MovingAverageConvergenceDivergence(source);
for (int i = 0; i < count; i++)
{
    ChartMacdValue value = incremental.Push(prices[i]);
    Check(reference.Line.Values[i], value.Line);
    Check(reference.Signal.Values[i], value.Signal);
    Check(reference.Histogram.Values[i], value.Histogram);
}
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; processors: {Environment.ProcessorCount}");
Console.WriteLine($"2 warmups; median of 7 measurements. MACD 12/26/9: {initial} initial observations followed by {updates} updates. Source construction excluded; 4096-observation seeding included in both timed implementations.");
Console.WriteLine("scenario,median_ms,median_allocated_bytes");
Measure("batch_MACD_recomputed_per_append", () =>
{
    prefix.Count = initial;
    _ = ChartIndicators.MovingAverageConvergenceDivergence(source);
    for (int i = initial; i < count; i++)
    {
        prefix.Count = i + 1;
        ChartMacdSeries result = ChartIndicators.MovingAverageConvergenceDivergence(source);
        Check(reference.Line.Values[i], result.Line.Values[i]);
        Check(reference.Signal.Values[i], result.Signal.Values[i]);
        Check(reference.Histogram.Values[i], result.Histogram.Values[i]);
    }
});
Measure("incremental_MACD_seed_and_append", () =>
{
    incremental.Reset();
    for (int i = 0; i < count; i++)
    {
        ChartMacdValue value = incremental.Push(prices[i]);
        if (i < initial) continue;
        Check(reference.Line.Values[i], value.Line);
        Check(reference.Signal.Values[i], value.Signal);
        Check(reference.Histogram.Values[i], value.Histogram);
    }
});
StreamingExponentialMovingAverage ema = new();
StreamingRelativeStrengthIndex rsi = new();
StreamingAverageTrueRange atr = new();
Measure("four_indicators_100000_observations", () =>
{
    ema.Reset(); rsi.Reset(); atr.Reset(); incremental.Reset();
    for (int i = 0; i < 100000; i++)
    {
        double? value = prices[i % prices.Length];
        ema.Push(value); rsi.Push(value); atr.Push(value + 2, value - 2, value); incremental.Push(value);
    }
    if (incremental.SamplesProcessed != 100000 || ema.SamplesProcessed != 100000 || rsi.SamplesProcessed != 100000 || atr.SamplesProcessed != 100000)
        throw new InvalidOperationException("An observation was dropped.");
});
Console.WriteLine("The baseline is the existing batch API invoked after each append, not a claim that a host previously used that schedule. One-off historical batch analysis remains appropriate. No UI/GPU/frame-rate claim or timing threshold is made.");

static void Check(double? expected, double? actual)
{
    if (expected.HasValue != actual.HasValue ||
        (expected.HasValue && (!double.IsFinite(actual!.Value) || Math.Abs(expected.Value - actual.Value) > 1e-9 * Math.Max(1, Math.Abs(expected.Value)))))
        throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}.");
}

static void Measure(string name, Action operation)
{
    for (int i = 0; i < 2; i++) operation();
    double[] times = new double[7]; long[] bytes = new long[7];
    for (int i = 0; i < 7; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        operation();
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes[i] = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
    Array.Sort(times); Array.Sort(bytes);
    Console.WriteLine($"{name},{times[3].ToString("F3", CultureInfo.InvariantCulture)},{bytes[3]}");
}

internal sealed class Prefix : IReadOnlyList<double?>
{
    private readonly double?[] _values;
    public Prefix(double?[] values) { _values = values; Count = values.Length; }
    public int Count { get; set; }
    public double? this[int index] => (uint)index < (uint)Count ? _values[index] : throw new ArgumentOutOfRangeException(nameof(index));
    public IEnumerator<double?> GetEnumerator() { for (int i = 0; i < Count; i++) yield return _values[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
