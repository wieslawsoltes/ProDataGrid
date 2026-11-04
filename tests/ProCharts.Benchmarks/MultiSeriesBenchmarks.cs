// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using ProCharts;

internal static class MultiSeriesBenchmarks
{
    public static void Run()
    {
        Console.WriteLine("MULTI-SERIES: actual independent StreamingChartDataSource instances versus one synchronized source. Same input rows and final X/value arrays; sixteen batches of 32 rows after seeding.");
        Console.WriteLine("No-reduction snapshots after every batch are included only in ingest_and_snapshot. Source construction, input preparation and per-run reset/seeding excluded. Notifications counted; no model, dispatcher or drawing. Independent sources are called serially by this harness, NOT a cross-source atomic API.");
        Console.WriteLine("Two paired warmups, seven alternating-order measured pairs. Final arrays, retained/lifetime counts and all-missing values checked after every round. Diagnostics, not fixed CI thresholds or a universal speedup.");
        Console.WriteLine("multiseries_capacity,channels,scenario,path,median_ms,median_allocated_bytes,notifications,snapshots");
        foreach (int capacity in new[] { 8192, 32768 })
        foreach (int channels in new[] { 4, 8 })
        {
            const int incoming = 512;
            double[] x = new double[capacity + incoming];
            double?[] cells = new double?[x.Length * channels];
            ChartSample[][] independentInput = new ChartSample[channels][];
            for (int s = 0; s < channels; s++) independentInput[s] = new ChartSample[x.Length];
            for (int row = 0; row < x.Length; row++)
            {
                x[row] = row * 0.25 + 1;
                for (int s = 0; s < channels; s++)
                {
                    double? value = (row + s * 17) % 251 < 3 ? null : Math.Sin(row * 0.019 + s) * 20 + s * 30;
                    cells[row * channels + s] = value;
                    independentInput[s][row] = new ChartSample(x[row], value);
                }
            }
            Harness separate = new(capacity, channels, x, cells, independentInput, false);
            Harness aligned = new(capacity, channels, x, cells, independentInput, true);
            foreach (bool display in new[] { false, true })
            {
                for (int warm = 0; warm < 2; warm++)
                { separate.Prepare(); separate.Run(display); aligned.Prepare(); aligned.Run(display); Verify(separate, aligned, x, cells, channels); }
                double[] timeA = new double[7], timeB = new double[7];
                long[] bytesA = new long[7], bytesB = new long[7];
                for (int run = 0; run < 7; run++)
                {
                    if ((run & 1) == 0)
                    { Measure(separate, display, out timeA[run], out bytesA[run]); Measure(aligned, display, out timeB[run], out bytesB[run]); }
                    else
                    { Measure(aligned, display, out timeB[run], out bytesB[run]); Measure(separate, display, out timeA[run], out bytesA[run]); }
                    Verify(separate, aligned, x, cells, channels);
                }
                Array.Sort(timeA); Array.Sort(timeB); Array.Sort(bytesA); Array.Sort(bytesB);
                string scenario = display ? "ingest_and_snapshot" : "ingest_only";
                Print(capacity, channels, scenario, "independent_sources", timeA[3], bytesA[3], separate);
                Print(capacity, channels, scenario, "synchronized_source", timeB[3], bytesB[3], aligned);
            }
            // Independent reduction does not generally produce a common X grid; do not compare it as equivalent work.
            StreamingMultiSeriesChartView full = aligned.Source!.BuildView(new() { DownsampleMode = ChartDownsampleMode.None });
            StreamingMultiSeriesChartView reduced = aligned.Source.BuildView(new() { MaxPoints = 256, DownsampleMode = ChartDownsampleMode.MinMax });
            for (int i = 0; i < reduced.SourceSampleIndices.Count; i++)
            {
                int row = checked((int)reduced.SourceSampleIndices[i]);
                for (int s = 0; s < channels; s++)
                    if (reduced.Snapshot.Series[s].XValues![i] != x[row] || reduced.Snapshot.Series[s].Values[i] != cells[row * channels + s])
                        throw new InvalidOperationException("Union reduction changed original row alignment.");
            }
            Console.WriteLine($"UNION capacity={capacity}; channels={channels}; per_series_soft_budget=256; full_rows={full.WindowCount}; common_output_rows={reduced.SourceSampleIndices.Count}; no reduction timing equivalence claimed.");
        }
    }

    private static void Print(int capacity, int channels, string scenario, string path, double time, long bytes, Harness harness)
        => Console.WriteLine($"{capacity},{channels},{scenario},{path},{time.ToString("F3", CultureInfo.InvariantCulture)},{bytes},{harness.Notifications},{harness.Snapshots}");

    private static void Measure(Harness harness, bool display, out double time, out long bytes)
    {
        harness.Prepare(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        harness.Run(display);
        time = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void Verify(Harness separate, Harness aligned, double[] x, double?[] cells, int channels)
    {
        StreamingMultiSeriesChartView view = aligned.Source!.BuildView(aligned.Request);
        if (view.TotalSamples != x.Length || view.RetainedCount != aligned.Capacity)
            throw new InvalidOperationException("Synchronized lifetime/retention count mismatch.");
        int first = x.Length - aligned.Capacity;
        for (int s = 0; s < channels; s++)
        {
            StreamingChartDataSource source = separate.Independent![s];
            ChartSeriesSnapshot expected = source.BuildSnapshot(separate.Request).Series[0], actual = view.Snapshot.Series[s];
            if (source.TotalSamples != x.Length || expected.Values.Count != view.RetainedCount || actual.Values.Count != view.RetainedCount)
                throw new InvalidOperationException("Independent or output count mismatch.");
            if (!ReferenceEquals(view.Snapshot.Series[0].XValues, actual.XValues))
                throw new InvalidOperationException("Synchronized X collection was duplicated.");
            for (int i = 0; i < view.RetainedCount; i++)
                if (actual.XValues![i] != x[first + i] || expected.XValues![i] != actual.XValues[i] ||
                    actual.Values[i] != cells[(first + i) * channels + s] || expected.Values[i] != actual.Values[i] ||
                    view.SourceSampleIndices[i] != first + i)
                    throw new InvalidOperationException($"Final source mismatch in series {s}, retained row {i}.");
        }
    }

    private sealed class Harness
    {
        private readonly int _channels;
        private readonly double[] _x;
        private readonly double?[] _cells;
        private readonly ChartSample[][] _samples;
        public Harness(int capacity, int channels, double[] x, double?[] cells, ChartSample[][] samples, bool aligned)
        {
            Capacity = capacity; _channels = channels; _x = x; _cells = cells; _samples = samples;
            if (aligned)
            {
                StreamingChartSeries[] definitions = new StreamingChartSeries[channels];
                for (int s = 0; s < channels; s++) definitions[s] = new($"Channel {s}");
                Source = new(capacity, definitions); Source.DataInvalidated += (_, _) => Notifications++;
            }
            else
            {
                Independent = new StreamingChartDataSource[channels];
                for (int s = 0; s < channels; s++)
                { Independent[s] = new(capacity, $"Channel {s}"); Independent[s].DataInvalidated += (_, _) => Notifications++; }
            }
        }
        public int Capacity { get; }
        public StreamingMultiSeriesChartDataSource? Source { get; }
        public StreamingChartDataSource[]? Independent { get; }
        public ChartDataRequest Request { get; } = new() { DownsampleMode = ChartDownsampleMode.None };
        public int Notifications { get; private set; }
        public int Snapshots { get; private set; }
        public void Prepare()
        {
            if (Source != null) { Source.Clear(); Source.AppendRange(_x.AsSpan(0, Capacity), _cells.AsSpan(0, Capacity * _channels)); }
            else for (int s = 0; s < _channels; s++) { Independent![s].Clear(); Independent[s].AppendRange(_samples[s].AsSpan(0, Capacity)); }
            Notifications = Snapshots = 0;
        }
        public void Run(bool display)
        {
            for (int row = Capacity; row < _x.Length; row += 32)
            {
                if (Source != null)
                {
                    Source.AppendRange(_x.AsSpan(row, 32), _cells.AsSpan(row * _channels, 32 * _channels));
                    if (display) { Source.BuildSnapshot(Request); Snapshots++; }
                }
                else
                {
                    for (int s = 0; s < _channels; s++)
                    {
                        Independent![s].AppendRange(_samples[s].AsSpan(row, 32));
                        if (display) { Independent[s].BuildSnapshot(Request); Snapshots++; }
                    }
                }
            }
            int expected = 16 * (Source != null ? 1 : _channels);
            if (Notifications != expected || Snapshots != (display ? expected : 0))
                throw new InvalidOperationException("Incorrect notification/snapshot count.");
        }
    }
}
