// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using ProCharts;

internal static class DeliveryBenchmarks
{
    public static void Run()
    {
        Console.WriteLine("DELIVERY: actual ChartModel + StreamingRangeChartDataSource. 1024 inputs in eight 128-input bursts. Immediate scalar notifications versus queued scalar notifications versus AppendRange producer batching.");
        Console.WriteLine("Same accepted observations and final numeric snapshots; intermediate snapshots intentionally differ. Context is a deterministic same-thread pump, NOT a physical UI dispatcher timing. Source/model construction and per-run seeding excluded; ingestion, notifications and every resulting snapshot included.");
        Console.WriteLine("Two warmups, seven repetitions with rotating execution order; no forced render/SKPicture caching. Final arrays and expected delivery counts verified. No drawing/GPU/FPS measurement.");
        Console.WriteLine("delivery_scenario,path,median_ms,median_allocated_bytes,snapshots,posts");
        foreach (int capacity in new[] { 1024, 8192 })
        {
            ChartRangeSample[] seed = new ChartRangeSample[capacity], input = new ChartRangeSample[1024];
            for (int i = 0; i < seed.Length; i++) seed[i] = Sample(i);
            for (int i = 0; i < input.Length; i++) input[i] = Sample(capacity + i);
            using Harness immediate = new(capacity, 0, seed, input);
            using Harness coalesced = new(capacity, 1, seed, input);
            using Harness batched = new(capacity, 2, seed, input);
            Harness[] paths = { immediate, coalesced, batched };
            double[][] times = { new double[7], new double[7], new double[7] };
            long[][] bytes = { new long[7], new long[7], new long[7] };
            for (int warmup = 0; warmup < 2; warmup++)
            {
                foreach (Harness path in paths) { path.Prepare(); path.Run(); }
                Verify(paths);
            }
            for (int run = 0; run < 7; run++)
            {
                for (int j = 0; j < paths.Length; j++)
                {
                    int p = (run + j) % paths.Length;
                    Harness path = paths[p];
                    path.Prepare();
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                    path.Run();
                    times[p][run] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    bytes[p][run] = GC.GetAllocatedBytesForCurrentThread() - before;
                }
                Verify(paths);
            }
            string[] names = { "immediate_scalar", "coalesced_scalar", "producer_batch" };
            for (int p = 0; p < paths.Length; p++)
            {
                Array.Sort(times[p]); Array.Sort(bytes[p]);
                Console.WriteLine($"delivery_capacity_{capacity},{names[p]},{times[p][3].ToString("F3", CultureInfo.InvariantCulture)},{bytes[p][3]},{paths[p].Snapshots},{paths[p].Posts}");
            }
        }
        Console.WriteLine("Producer batching is included as the existing alternative, not hidden. Coalescing reduces presentation deliveries, not data ingestion or individual snapshot complexity. Its latency is set by the context's service schedule, not this benchmark's operation time.");
    }

    private static ChartRangeSample Sample(int index)
    {
        double center = 100 + Math.Sin(index * 0.023) * 20;
        return new ChartRangeSample(index, index % 271 == 17 ? null : center - 5, center + 5);
    }

    private static void Verify(Harness[] paths)
    {
        ChartSeriesSnapshot expected = paths[0].Snapshot.Series[0];
        for (int p = 1; p < paths.Length; p++)
        {
            ChartSeriesSnapshot actual = paths[p].Snapshot.Series[0];
            if (expected.Values.Count != actual.Values.Count) throw new InvalidOperationException("Different selected counts.");
            for (int i = 0; i < expected.Values.Count; i++)
                if (expected.Values[i] != actual.Values[i] || expected.LowValues![i] != actual.LowValues![i] ||
                    expected.HighValues![i] != actual.HighValues![i] || expected.XValues![i] != actual.XValues![i])
                    throw new InvalidOperationException($"Final snapshot mismatch in path {p}, point {i}.");
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly int _mode;
        private readonly ChartRangeSample[] _seed, _input;
        private readonly StreamingRangeChartDataSource _source;
        private readonly PumpContext _context = new();
        private readonly CoalescingChartDataSource? _adapter;
        private readonly ChartModel _model = new();
        public Harness(int capacity, int mode, ChartRangeSample[] seed, ChartRangeSample[] input)
        {
            _mode = mode; _seed = seed; _input = input;
            _source = new(capacity, "Envelope");
            _source.AppendRange(seed);
            if (mode == 1) _adapter = new CoalescingChartDataSource(_source, _context);
            using (_model.DeferRefresh())
            {
                _model.Request.MaxPoints = 256; _model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
                _model.CategoryAxis.Kind = ChartAxisKind.Value;
                _model.DataSource = (IChartDataSource?)_adapter ?? _source;
            }
            _model.SnapshotChanged += (_, _) => Snapshots++;
        }
        public int Snapshots { get; private set; }
        public int Posts => _context.Posts;
        public ChartDataSnapshot Snapshot => _model.Snapshot;
        public void Prepare()
        {
            using (_model.DeferRefresh()) { _source.Clear(); _source.AppendRange(_seed); }
            _context.Drain(); _context.Posts = 0; Snapshots = 0;
        }
        public void Run()
        {
            for (int start = 0; start < _input.Length; start += 128)
            {
                if (_mode == 2) _source.AppendRange(_input.AsSpan(start, 128));
                else for (int i = start; i < start + 128; i++) _source.Append(_input[i]);
                if (_mode == 1) _context.Drain();
            }
            int expectedSnapshots = _mode == 0 ? _input.Length : _input.Length / 128;
            if (Snapshots != expectedSnapshots || _source.TotalSamples != _seed.Length + _input.Length ||
                _model.Snapshot.Series[0].XValues![^1] != _seed.Length + _input.Length - 1 ||
                (_mode == 1 && Posts != expectedSnapshots))
                throw new InvalidOperationException("Unexpected delivery count or lost input.");
        }
        public void Dispose() { _model.Dispose(); _adapter?.Dispose(); }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        private SendOrPostCallback? _callback;
        private object? _state;
        public int Posts { get; set; }
        public override void Post(SendOrPostCallback d, object? state)
        {
            if (_callback != null) throw new InvalidOperationException("More than one queued delivery.");
            _callback = d; _state = state; Posts++;
        }
        public void Drain()
        {
            while (_callback is { } callback)
            {
                object? state = _state; _callback = null; _state = null;
                callback(state);
            }
        }
    }
}
