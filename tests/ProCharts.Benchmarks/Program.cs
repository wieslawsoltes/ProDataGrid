// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ProCharts;

const int capacity = 4096;
const int appendCount = 100000;
const int sampleCount = 1000000;
const int budget = 2000;
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; processors: {Environment.ProcessorCount}");
Console.WriteLine($"Warmup: 2; measured repetitions: 7; append capacity: {capacity}; append operations: {appendCount}; decimation input: {sampleCount}; budget: {budget}");
Console.WriteLine("scenario,median_ms,median_allocated_bytes");

StreamingChartDataSource ring = new(capacity);
for (int i = 0; i < capacity; i++) ring.Append(new ChartSample(i, i));
List<ChartSample> shifting = new(capacity);
for (int i = 0; i < capacity; i++) shifting.Add(new ChartSample(i, i));

Measure("shifting_list_append_baseline", () =>
{
    for (int i = 0; i < appendCount; i++)
    {
        shifting.RemoveAt(0);
        shifting.Add(new ChartSample(i, i));
    }
    if (shifting[0].X != appendCount - capacity || shifting[^1].X != appendCount - 1)
        throw new InvalidOperationException("Baseline tail changed.");
});
Measure("bounded_ring_append", () =>
{
    for (int i = 0; i < appendCount; i++) ring.Append(new ChartSample(i, i));
});
ChartDataSnapshot retained = ring.BuildSnapshot(new ChartDataRequest());
if (retained.Series[0].XValues![0] != shifting[0].X || retained.Series[0].XValues![^1] != shifting[^1].X)
    throw new InvalidOperationException("Compared append implementations produced different tails.");

ChartDataRequest window = new() { WindowStart = 2000, WindowCount = 128 };
ChartDataSnapshot cached = ring.BuildSnapshot(window);
Measure("cached_window_100000_reads", () =>
{
    for (int i = 0; i < 100000; i++)
        if (!ReferenceEquals(cached, ring.BuildSnapshot(window)))
            throw new InvalidOperationException("Unchanged request was not cached.");
});

ChartSample[] samples = new ChartSample[sampleCount];
for (int i = 0; i < samples.Length; i++)
    samples[i] = new ChartSample(i, Math.Sin(i * 0.001) + (i % 7919 == 0 ? 10 : 0));
foreach (ChartDownsampleMode mode in new[] { ChartDownsampleMode.MinMax, ChartDownsampleMode.Lttb, ChartDownsampleMode.Bucket })
{
    Measure($"{mode}_million_to_2000", () =>
    {
        int[] indices = ChartSampleDecimator.SelectIndices(samples, budget, mode);
        if (indices.Length > budget || indices[0] != 0 || indices[^1] != sampleCount - 1)
            throw new InvalidOperationException("Decimation violated its finite-input contract.");
        for (int i = 1; i < indices.Length; i++)
            if (indices[i] <= indices[i - 1]) throw new InvalidOperationException("Indices are not increasing.");
    });
}
Console.WriteLine("Append baseline is an intentionally shifting List<T>, not a measurement of the previous grid adapter. Timings exclude construction and do not measure GPU rendering or UI frame rate.");

static void Measure(string name, Action operation)
{
    for (int i = 0; i < 2; i++) operation();
    double[] times = new double[7];
    long[] allocations = new long[7];
    for (int i = 0; i < times.Length; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        operation();
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        allocations[i] = GC.GetAllocatedBytesForCurrentThread() - bytes;
    }
    Array.Sort(times);
    Array.Sort(allocations);
    Console.WriteLine($"{name},{times[3].ToString("F3", CultureInfo.InvariantCulture)},{allocations[3]}");
}
