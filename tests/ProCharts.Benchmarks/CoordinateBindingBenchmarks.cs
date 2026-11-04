// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using ProCharts;

internal static class CoordinateBindingBenchmarks
{
    public static void Run()
    {
        Console.WriteLine("COORDINATE-BINDING: sixteen 32-row appends, each followed by a trailing 64-X-unit capture, three channels, per-series budget 128.");
        Console.WriteLine("Compare a manual full-capture/linear-scan integration, existing direct BuildLatestViewByX, and the new bound adapter. All use the same accepted rows and verify identical selected values/identities.");
        Console.WriteLine("Inputs, constructors and per-run seed/reset excluded; append, lookup and all capture allocations included. Two warmups, seven rounds with rotating order. This is not a model/UI/GPU timing or a faster-than-direct-query claim.");
        Console.WriteLine("coordinate_binding_scenario,path,median_ms,median_allocated_bytes,checksum");
        foreach (int capacity in new[] { 8192, 65536 })
        {
            double[] x = new double[capacity + 512]; double?[] cells = new double?[x.Length * 3];
            for (int row = 0; row < x.Length; row++)
            {
                x[row] = row * 0.25;
                for (int s = 0; s < 3; s++) cells[row * 3 + s] = row % (179 + s * 13) < 3 ? null : 20 * s + Math.Sin(row * (0.01 + s * 0.007));
            }
            using Harness scan = new(0, capacity, x, cells);
            using Harness direct = new(1, capacity, x, cells);
            using Harness bound = new(2, capacity, x, cells);
            Harness[] paths = { scan, direct, bound };
            double[][] times = { new double[7], new double[7], new double[7] };
            long[][] bytes = { new long[7], new long[7], new long[7] };
            for (int warm = 0; warm < 2; warm++)
            {
                long? expected = null;
                foreach (Harness path in paths)
                {
                    path.Prepare(); long result = path.Run(validateEveryCapture: true);
                    if (expected.HasValue && result != expected) throw new InvalidOperationException("Warmup capture mismatch.");
                    expected = result;
                }
            }
            long checksum = 0;
            for (int round = 0; round < 7; round++)
            {
                long? expected = null;
                for (int order = 0; order < paths.Length; order++)
                {
                    int p = (round + order) % paths.Length;
                    Harness path = paths[p]; path.Prepare();
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                    long result = path.Run(validateEveryCapture: false);
                    times[p][round] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    bytes[p][round] = GC.GetAllocatedBytesForCurrentThread() - before;
                    path.VerifyLast();
                    if (expected.HasValue && result != expected) throw new InvalidOperationException("Measured capture mismatch.");
                    expected = checksum = result;
                }
            }
            string[] names = { "manual_full_capture_scan", "existing_direct_query", "bound_coordinate_adapter" };
            for (int p = 0; p < paths.Length; p++)
            {
                Array.Sort(times[p]); Array.Sort(bytes[p]);
                Console.WriteLine($"capture_after_append_capacity_{capacity},{names[p]},{times[p][3].ToString("F3", CultureInfo.InvariantCulture)},{bytes[p][3]},{checksum}");
            }
        }
        Console.WriteLine("The manual integration is an explicitly expensive example, not the previous production renderer. Direct binary APIs already avoid its full-history copies; the bound adapter makes that path usable through IChartDataSource and filtered count/window contracts.");
    }

    private sealed class Harness : IDisposable
    {
        private readonly int _mode, _capacity;
        private readonly double[] _x;
        private readonly double?[] _cells;
        private readonly StreamingMultiSeriesChartDataSource _source;
        private readonly CoordinateWindowChartDataSource? _adapter;
        private readonly ChartDataRequest _full = new() { DownsampleMode = ChartDownsampleMode.None };
        private readonly ChartDataRequest _request = new() { MaxPoints = 128, DownsampleMode = ChartDownsampleMode.MinMax };
        private StreamingMultiSeriesChartView? _last;

        public Harness(int mode, int capacity, double[] x, double?[] cells)
        {
            _mode = mode; _capacity = capacity; _x = x; _cells = cells;
            _source = new(capacity, new[]
            {
                new StreamingChartSeries("A", ChartSeriesKind.Scatter), new StreamingChartSeries("B", ChartSeriesKind.Scatter),
                new StreamingChartSeries("C", ChartSeriesKind.Scatter)
            });
            if (mode == 2) _adapter = new CoordinateWindowChartDataSource(_source, ChartCoordinateWindow.Latest(64));
        }

        public void Prepare()
        {
            _last = null; _source.Clear(); _source.AppendRange(_x.AsSpan(0, _capacity), _cells.AsSpan(0, _capacity * 3));
        }

        public long Run(bool validateEveryCapture)
        {
            long checksum = 0;
            for (int batch = 0; batch < 16; batch++)
            {
                int first = _capacity + batch * 32;
                _source.AppendRange(_x.AsSpan(first, 32), _cells.AsSpan(first * 3, 96));
                if (_mode == 0)
                {
                    var full = _source.BuildView(_full); var x = full.Snapshot.Series[0].XValues!;
                    double minimum = x[^1] - 64; int start = 0;
                    while (start < x.Count && x[start] < minimum) start++;
                    _request.WindowStart = start; _request.WindowCount = x.Count - start;
                    _last = _source.BuildView(_request);
                }
                else if (_mode == 1) _last = _source.BuildLatestViewByX(64, 128, ChartDownsampleMode.MinMax);
                else _last = _adapter!.BuildView(_request);
                checksum = unchecked(checksum * 31 + _last.SourceSampleIndices[0] + _last.SourceSampleIndices[^1] + _last.SourceSampleIndices.Count);
                if (validateEveryCapture) VerifyLast();
            }
            return checksum;
        }

        public void VerifyLast()
        {
            var view = _last ?? throw new InvalidOperationException("Missing capture.");
            // Both lookup paths must normalize to the exact same cached view as the existing direct query,
            // not merely agree on endpoints/counts while choosing different interior rows. This check is
            // outside measured runs (and on every warmup capture); the next measured operation resets history.
            var expected = _source.BuildLatestViewByX(64, 128, ChartDownsampleMode.MinMax);
            if (!ReferenceEquals(view, expected))
                throw new InvalidOperationException("Lookup did not resolve the same complete selected-row capture.");
            if (view.WindowCount != 257 || view.SourceSampleIndices[^1] != view.TotalSamples - 1)
                throw new InvalidOperationException("The trailing capture lost an endpoint.");
            for (int i = 0; i < view.SourceSampleIndices.Count; i++)
            {
                int original = checked((int)view.SourceSampleIndices[i]);
                for (int s = 0; s < 3; s++)
                    if (view.Snapshot.Series[s].XValues![i] != _x[original] || view.Snapshot.Series[s].Values[i] != _cells[original * 3 + s])
                        throw new InvalidOperationException("Original coordinates/values/gaps were changed.");
            }
        }

        public void Dispose() => _adapter?.Dispose();
    }
}
