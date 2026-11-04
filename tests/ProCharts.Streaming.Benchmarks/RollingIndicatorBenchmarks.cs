// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using ProCharts;

internal static class RollingIndicatorBenchmarks
{
    public static void Run()
    {
        const int initial = 2048, updates = 256, count = initial + updates;
        double?[] prices = new double?[count], highs = new double?[count], lows = new double?[count];
        for (int i = 0; i < count; i++)
        {
            double value = 100 + Math.Sin(i * 0.07) * 10 + Math.Cos(i * 0.031);
            // Exercise seed restart, but ensure BOTH periods are ready throughout measured appends.
            prices[i] = i == 137 ? null : value;
            highs[i] = value + 3; lows[i] = value - 2;
        }
        Console.WriteLine("ROLLING: actual existing batch APIs recomputed per append versus persistent calculators; 2048 seed prices + 256 updates. Seed processing included in both timings; input/calculator construction excluded.");
        Console.WriteLine("Paired 2 warmups, 7 measured repetitions with alternating order. Every output checked against full-batch references. All appended results are ready; construction memory and transfer bursts reported separately.");
        Console.WriteLine("rolling_scenario,path,median_ms,median_allocated_bytes");
        foreach (int period in new[] { 32, 512 })
        {
            Prefix prefix = new(prices), high = new(highs), low = new(lows);
            ChartSeriesSnapshot source = new("Price", ChartSeriesKind.Line, prefix, highValues: high, lowValues: low);
            var expectedSma = ChartIndicators.SimpleMovingAverage(source, period);
            var expectedWma = ChartIndicators.WeightedMovingAverage(source, period);
            var expectedPopulation = ChartIndicators.RollingStandardDeviation(source, period);
            var expectedSample = ChartIndicators.RollingStandardDeviation(source, period, true);
            var expectedBands = ChartIndicators.BollingerBands(source, period);
            var expectedChannels = ChartIndicators.DonchianChannels(source, period);
            StreamingRollingStatistics statistics = new(period);
            StreamingBollingerBands bands = new(period);
            StreamingDonchianChannels channels = new(period);
            for (int i = 0; i < count; i++)
            {
                CheckStatistics(i, statistics.Push(prices[i]));
                CheckBands(expectedBands, i, bands.Push(prices[i]));
                CheckBands(expectedChannels, i, channels.Push(highs[i], lows[i], prices[i]));
                if (i >= initial && (!statistics.IsReady || !bands.IsReady || !channels.IsReady))
                    throw new InvalidOperationException("The append fixture must have complete windows, not only null warmup outputs.");
            }
            Compare($"rolling_statistics_p{period}", () =>
            {
                SetCount(initial);
                _ = ChartIndicators.SimpleMovingAverage(source, period);
                _ = ChartIndicators.WeightedMovingAverage(source, period);
                _ = ChartIndicators.RollingStandardDeviation(source, period);
                _ = ChartIndicators.RollingStandardDeviation(source, period, true);
                for (int i = initial; i < count; i++)
                {
                    SetCount(i + 1);
                    var a = ChartIndicators.SimpleMovingAverage(source, period);
                    var b = ChartIndicators.WeightedMovingAverage(source, period);
                    var c = ChartIndicators.RollingStandardDeviation(source, period);
                    var d = ChartIndicators.RollingStandardDeviation(source, period, true);
                    CheckStatistics(i, new ChartRollingStatisticsValue(a.Values[i], b.Values[i], c.Values[i], d.Values[i]));
                }
            }, () =>
            {
                statistics.Reset();
                for (int i = 0; i < count; i++)
                {
                    var value = statistics.Push(prices[i]);
                    if (i >= initial) CheckStatistics(i, value);
                }
            });
            Compare($"bollinger_p{period}", () =>
            {
                SetCount(initial); _ = ChartIndicators.BollingerBands(source, period);
                for (int i = initial; i < count; i++)
                {
                    SetCount(i + 1); var result = ChartIndicators.BollingerBands(source, period);
                    CheckBands(expectedBands, i, new ChartBandValue(result.Lower.Values[i], result.Middle.Values[i], result.Upper.Values[i]));
                }
            }, () =>
            {
                bands.Reset();
                for (int i = 0; i < count; i++)
                {
                    var value = bands.Push(prices[i]);
                    if (i >= initial) CheckBands(expectedBands, i, value);
                }
            });
            Compare($"donchian_p{period}", () =>
            {
                SetCount(initial); _ = ChartIndicators.DonchianChannels(source, period);
                for (int i = initial; i < count; i++)
                {
                    SetCount(i + 1); var result = ChartIndicators.DonchianChannels(source, period);
                    CheckBands(expectedChannels, i, new ChartBandValue(result.Lower.Values[i], result.Middle.Values[i], result.Upper.Values[i]));
                }
            }, () =>
            {
                channels.Reset();
                for (int i = 0; i < count; i++)
                {
                    var value = channels.Push(highs[i], lows[i], prices[i]);
                    if (i >= initial) CheckBands(expectedChannels, i, value);
                }
            });
            void SetCount(int n) { prefix.Count = high.Count = low.Count = n; }
            void CheckStatistics(int i, ChartRollingStatisticsValue v)
            {
                Check(expectedSma.Values[i], v.Mean); Check(expectedWma.Values[i], v.WeightedMean);
                Check(expectedPopulation.Values[i], v.PopulationStandardDeviation); Check(expectedSample.Values[i], v.SampleStandardDeviation);
            }
        }
        foreach (int period in new[] { 32, 2048, 16384 }) MeasureTransfer(period);
        Console.WriteLine("Batch recomputation is a measured scheduling alternative, not a claim about prior host behavior. Persistent windows trade O(period) retained state and occasional O(period) transfers for amortized O(1) update work. No raster/GPU/FPS or peak-RSS measurement.");
    }

    private static void CheckBands(ChartBandSeries expected, int i, ChartBandValue actual)
    { Check(expected.Lower.Values[i], actual.Lower); Check(expected.Middle.Values[i], actual.Middle); Check(expected.Upper.Values[i], actual.Upper); }

    private static void Check(double? expected, double? actual)
    {
        if (expected.HasValue != actual.HasValue || (expected.HasValue &&
            (!double.IsFinite(actual!.Value) || Math.Abs(expected.Value - actual.Value) > 1e-8 * Math.Max(1, Math.Abs(expected.Value)))))
            throw new InvalidOperationException($"Rolling result mismatch: expected {expected:R}, got {actual:R}.");
    }

    private static void Compare(string name, Action batch, Action persistent)
    {
        for (int i = 0; i < 2; i++) { batch(); persistent(); }
        double[] a = new double[7], b = new double[7]; long[] ba = new long[7], bb = new long[7];
        for (int i = 0; i < 7; i++)
        {
            if ((i & 1) == 0) { Measure(batch, out a[i], out ba[i]); Measure(persistent, out b[i], out bb[i]); }
            else { Measure(persistent, out b[i], out bb[i]); Measure(batch, out a[i], out ba[i]); }
        }
        Array.Sort(a); Array.Sort(b); Array.Sort(ba); Array.Sort(bb);
        Console.WriteLine($"{name},batch_prefix,{a[3].ToString("F3", CultureInfo.InvariantCulture)},{ba[3]}");
        Console.WriteLine($"{name},persistent,{b[3].ToString("F3", CultureInfo.InvariantCulture)},{bb[3]}");
    }

    private static void Measure(Action action, out double time, out long bytes)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        action(); time = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void MeasureTransfer(int period)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        StreamingRollingStatistics calculator = new(period);
        long constructionBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double[] transfer = new double[7], next = new double[7];
        for (int pass = -2; pass < 7; pass++)
        {
            calculator.Reset();
            for (int i = 0; i < period; i++) calculator.Push(i);
            long start = Stopwatch.GetTimestamp();
            var first = calculator.Push(period);
            double firstTime = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = Stopwatch.GetTimestamp();
            var second = calculator.Push(period + 1);
            double secondTime = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Check((period + 1d) / 2, first.Mean); Check((period + 3d) / 2, second.Mean);
            if (pass >= 0) { transfer[pass] = firstTime; next[pass] = secondTime; }
        }
        Array.Sort(transfer); Array.Sort(next);
        Console.WriteLine($"rolling_storage_p{period},construction_bytes,{constructionBytes}");
        Console.WriteLine($"rolling_eviction_p{period},transfer_push_ms,{transfer[3].ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"rolling_eviction_p{period},following_push_ms,{next[3].ToString("F6", CultureInfo.InvariantCulture)}");
    }
}
