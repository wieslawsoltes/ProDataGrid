// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using ProCharts;

internal static class XRangeBenchmarks
{
    private const int QueryCount = 256;
    private const int Width = 128;
    private const int Channels = 3;

    public static void Run()
    {
        Console.WriteLine("X-RANGE: linear lookup over a previously captured X array plus ordinal BuildView versus atomic binary ring lookup plus BuildViewByX. Same static wrapped source, 256 distinct 128-row windows and three channels, no reduction.");
        Console.WriteLine("Source creation, seed/capture, query preparation and full numerical/identity validation excluded. The baseline already has an owned coordinate array; its acquisition cost is NOT charged. Two paired warmups and seven alternating-order measured pairs. Capture costs are included in both paths; these are diagnostics, not universal latency or concurrency guarantees.");
        Console.WriteLine("x_range_capacity,path,queries,window_rows,median_ms,median_allocated_bytes");
        foreach (int capacity in new[] { 8192, 65536, 262144 })
        {
            StreamingMultiSeriesChartDataSource source = Create(capacity);
            IReadOnlyList<double> x = source.BuildSnapshot(new()).Series[0].XValues!;
            (double Minimum, double Maximum)[] queries = new (double, double)[QueryCount];
            for (int i = 0; i < queries.Length; i++)
            {
                int start = (int)(((long)i * 104729 + 17) % (capacity - Width));
                queries[i] = (x[start] - 0.01, x[start + Width - 1] + 0.01);
            }
            ChartDataRequest request = new() { DownsampleMode = ChartDownsampleMode.None };
            foreach ((double minimum, double maximum) in queries)
            {
                StreamingMultiSeriesChartView linear = CaptureLinear(source, x, request, minimum, maximum);
                StreamingMultiSeriesChartView binary = source.BuildViewByX(minimum, maximum, downsampleMode: ChartDownsampleMode.None);
                if (!ReferenceEquals(linear, binary)) throw new InvalidOperationException("Equivalent queries did not share the normalized cache.");
                Validate(binary, minimum, maximum);
            }
            double[][] times = { new double[7], new double[7] };
            long[][] allocations = { new long[7], new long[7] };
            for (int pair = -2; pair < 7; pair++)
            {
                long? expectedChecksum = null;
                for (int order = 0; order < 2; order++)
                {
                    int path = (pair & 1) == 0 ? order : 1 - order;
                    (double milliseconds, long bytes, long checksum) = Measure(source, x, request, queries, path == 1);
                    if (expectedChecksum.HasValue && expectedChecksum.Value != checksum)
                        throw new InvalidOperationException("Measured query paths selected different identities.");
                    expectedChecksum = checksum;
                    if (pair >= 0) { times[path][pair] = milliseconds; allocations[path][pair] = bytes; }
                }
            }
            for (int path = 0; path < 2; path++)
            {
                Array.Sort(times[path]); Array.Sort(allocations[path]);
                Console.WriteLine($"{capacity},{(path == 0 ? "linear_lookup_and_capture" : "binary_lookup_and_capture")},{QueryCount},{Width},{times[path][3].ToString("F3", CultureInfo.InvariantCulture)},{allocations[path][3]}");
            }
        }
    }

    private static (double Milliseconds, long Bytes, long Checksum) Measure(
        StreamingMultiSeriesChartDataSource source, IReadOnlyList<double> x, ChartDataRequest request,
        (double Minimum, double Maximum)[] queries, bool binary)
    {
        long checksum = 0;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        foreach ((double minimum, double maximum) in queries)
        {
            StreamingMultiSeriesChartView view = binary
                ? source.BuildViewByX(minimum, maximum, downsampleMode: ChartDownsampleMode.None)
                : CaptureLinear(source, x, request, minimum, maximum);
            checksum += view.SourceSampleIndices[0] + view.SourceSampleIndices[^1] + view.WindowCount;
        }
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return (elapsed, GC.GetAllocatedBytesForCurrentThread() - bytes, checksum);
    }

    private static StreamingMultiSeriesChartView CaptureLinear(StreamingMultiSeriesChartDataSource source,
        IReadOnlyList<double> x, ChartDataRequest request, double minimum, double maximum)
    {
        int start = 0;
        while (start < x.Count && x[start] < minimum) start++;
        int end = start;
        while (end < x.Count && x[end] <= maximum) end++;
        request.WindowStart = start; request.WindowCount = end - start;
        return source.BuildView(request);
    }

    private static StreamingMultiSeriesChartDataSource Create(int capacity)
    {
        StreamingMultiSeriesChartDataSource source = new(capacity, new[]
        {
            new StreamingChartSeries("A"), new StreamingChartSeries("B"), new StreamingChartSeries("C")
        });
        double[] x = new double[capacity]; double?[] values = new double?[capacity * Channels];
        for (int row = 0; row < capacity; row++)
        {
            x[row] = Coordinate(row);
            for (int s = 0; s < Channels; s++) values[row * Channels + s] = Value(row, s);
        }
        source.AppendRange(x, values);
        Span<double?> tail = stackalloc double?[Channels];
        for (int row = capacity; row < capacity + 173; row++)
        {
            for (int s = 0; s < Channels; s++) tail[s] = Value(row, s);
            source.Append(Coordinate(row), tail);
        }
        return source;
    }

    private static void Validate(StreamingMultiSeriesChartView view, double minimum, double maximum)
    {
        if (view.WindowCount != Width || view.SourceSampleIndices.Count != Width)
            throw new InvalidOperationException("Query selected the wrong number of observations.");
        for (int i = 0; i < Width; i++)
        {
            long row = view.FirstRetainedSampleIndex + view.WindowStart + i;
            double x = Coordinate(row);
            if (view.SourceSampleIndices[i] != row || x < minimum || x > maximum)
                throw new InvalidOperationException("Query identity or bound mismatch.");
            for (int s = 0; s < Channels; s++)
            {
                ChartSeriesSnapshot series = view.Snapshot.Series[s];
                if (series.XValues![i] != x || series.Values[i] != Value(row, s) ||
                    !ReferenceEquals(series.XValues, view.Snapshot.Series[0].XValues))
                    throw new InvalidOperationException("Query changed an original coordinate, value, gap or shared X collection.");
            }
        }
    }

    private static double Coordinate(long row) => row + row % 7 * 0.05;
    private static double? Value(long row, int series) => (row + series) % 23 == 0 ? null : row * 0.25 + series;
}
