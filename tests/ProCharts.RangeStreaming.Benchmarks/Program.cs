// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ProCharts;

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; processors: {Environment.ProcessorCount}");
Console.WriteLine("Actual RangeChartDataSource.ReplaceData versus StreamingRangeChartDataSource.AppendRange. Each operation ingests sixteen batches of 32 observations into already-seeded history.");
Console.WriteLine("Identical chronological windows. Input generation, initial construction and per-run seed/reset excluded; validation/copy/ingestion included. Separate scenario includes a reduced display snapshot after EVERY batch.");
Console.WriteLine("Two paired warmups; median of seven paired repetitions in alternating order. Managed allocation is not peak RSS; no native drawing, GPU or FPS measurement.");
Console.WriteLine("scenario,path,median_ms,median_allocated_bytes,checksum");
foreach (int capacity in new[] { 4096, 65536 })
{
    const int batches = 16, batchSize = 32;
    ChartRangeSample[] observations = new ChartRangeSample[capacity + batches * batchSize];
    for (int i = 0; i < observations.Length; i++)
    {
        double low = 30 + Math.Sin(i * 0.007) * 15;
        observations[i] = new ChartRangeSample(i, i % 4093 == 17 ? null : low, low + 30);
    }
    Input[] windows = new Input[batches + 1];
    for (int b = 0; b < windows.Length; b++)
    {
        double?[] low = new double?[capacity], high = new double?[capacity];
        double[] x = new double[capacity];
        for (int i = 0; i < capacity; i++)
        {
            ChartRangeSample sample = observations[b * batchSize + i];
            low[i] = sample.Lower; high[i] = sample.Upper; x[i] = sample.X;
        }
        windows[b] = new Input(low, high, x);
    }
    RangeChartDataSource replacement = new("Range", windows[0].Low, windows[0].High, windows[0].X);
    StreamingRangeChartDataSource stream = new(capacity, "Range");
    ChartDataRequest request = new() { MaxPoints = 256, DownsampleMode = ChartDownsampleMode.MinMax };
    foreach (bool display in new[] { false, true })
    {
        PrepareReplacement(); long a = Replace(display);
        PrepareStream(); long b = Append(display);
        if (a != b) throw new InvalidOperationException("Initial workload checksum mismatch.");
        Verify();
        Compare($"{(display ? "ingest_and_display" : "ingest_only")}_16x32_capacity_{capacity}",
            PrepareReplacement, () => Replace(display), PrepareStream, () => Append(display));
        Verify();
    }
    void PrepareReplacement() => replacement.ReplaceData(windows[0].Low, windows[0].High, windows[0].X);
    void PrepareStream() { stream.Clear(); stream.AppendRange(observations.AsSpan(0, capacity)); }
    long Replace(bool display)
    {
        long digest = 0;
        for (int b = 1; b < windows.Length; b++)
        {
            replacement.ReplaceData(windows[b].Low, windows[b].High, windows[b].X);
            if (display) digest += (long)replacement.BuildSnapshot(request).Series[0].XValues![^1];
        }
        return digest + replacement.Count;
    }
    long Append(bool display)
    {
        long digest = 0;
        for (int b = 0; b < batches; b++)
        {
            stream.AppendRange(observations.AsSpan(capacity + b * batchSize, batchSize));
            if (display) digest += (long)stream.BuildSnapshot(request).Series[0].XValues![^1];
        }
        return digest + stream.Count;
    }
    void Verify()
    {
        ChartDataSnapshot expected = replacement.BuildSnapshot(request);
        StreamingRangeChartView view = stream.BuildView(request);
        ChartSeriesSnapshot a = expected.Series[0], b = view.Snapshot.Series[0];
        if (a.Values.Count != b.Values.Count || view.TotalSamples != observations.Length)
            throw new InvalidOperationException("Different retained or selected counts.");
        for (int i = 0; i < a.Values.Count; i++)
            if (a.Values[i] != b.Values[i] || a.LowValues![i] != b.LowValues![i] || a.HighValues![i] != b.HighValues![i] ||
                a.XValues![i] != b.XValues![i] || b.XValues[i] != view.SourceSampleIndices[i])
                throw new InvalidOperationException($"Selected boundary/X/identity mismatch at {i}.");
    }
}
Console.WriteLine("Final retained/reduced boundaries, X values and stream identities matched the replacement source in every workload. Display preparation still scans its window under a lock; cheaper ingestion does not make snapshot building constant-time.");
DeliveryBenchmarks.Run();

static void Compare(string name, Action setupA, Func<long> actionA, Action setupB, Func<long> actionB)
{
    for (int i = 0; i < 2; i++)
    { setupA(); long a = actionA(); setupB(); if (a != actionB()) throw new InvalidOperationException("Warmup mismatch."); }
    double[] timeA = new double[7], timeB = new double[7];
    long[] bytesA = new long[7], bytesB = new long[7];
    long checksum = 0;
    for (int i = 0; i < 7; i++)
    {
        long a, b;
        if ((i & 1) == 0)
        {
            a = Measure(setupA, actionA, out timeA[i], out bytesA[i]);
            b = Measure(setupB, actionB, out timeB[i], out bytesB[i]);
        }
        else
        {
            b = Measure(setupB, actionB, out timeB[i], out bytesB[i]);
            a = Measure(setupA, actionA, out timeA[i], out bytesA[i]);
        }
        if (a != b) throw new InvalidOperationException("Measured pair mismatch.");
        checksum = a;
    }
    Array.Sort(timeA); Array.Sort(timeB); Array.Sort(bytesA); Array.Sort(bytesB);
    Console.WriteLine($"{name},replace,{timeA[3].ToString("F3", CultureInfo.InvariantCulture)},{bytesA[3]},{checksum}");
    Console.WriteLine($"{name},stream,{timeB[3].ToString("F3", CultureInfo.InvariantCulture)},{bytesB[3]},{checksum}");
}

static long Measure(Action setup, Func<long> action, out double time, out long bytes)
{
    setup();
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
    long result = action();
    time = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    return result;
}

internal sealed record Input(double?[] Low, double?[] High, double[] X);
