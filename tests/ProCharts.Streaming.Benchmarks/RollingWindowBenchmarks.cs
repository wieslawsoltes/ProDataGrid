// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using ProCharts;

internal static class RollingWindowBenchmarks
{
    public static void Run()
    {
        const int updates = 512;
        Console.WriteLine("BOUNDED-WINDOW: 512 ready updates. Actual batch APIs see ONLY the current period-sized window, not full history. Persistent state is preseeded. Construction, seed work and source-array shifts/copies excluded for BOTH paths; calculation and per-output validation included.");
        Console.WriteLine("Two paired warmups, seven paired measurements with alternating order. Source slice offsets change only between synchronous batch calls; output data is owned. These slices are not used in render caches.");
        Console.WriteLine("window_scenario,path,median_ms,median_allocated_bytes");
        foreach (int period in new[] { 32, 512 })
        {
            double?[] prices = new double?[period + updates], highs = new double?[period + updates], lows = new double?[period + updates];
            for (int i = 0; i < prices.Length; i++)
            { prices[i] = 100 + Math.Sin(i * 0.07) * 10; highs[i] = prices[i] + 3; lows[i] = prices[i] - 2; }
            ChartSeriesSnapshot full = new("Price", ChartSeriesKind.Line, prices, highValues: highs, lowValues: lows);
            var expectedBands = ChartIndicators.BollingerBands(full, period);
            var expectedChannels = ChartIndicators.DonchianChannels(full, period);
            Slice priceWindow = new(prices, period), highWindow = new(highs, period), lowWindow = new(lows, period);
            ChartSeriesSnapshot window = new("Price", ChartSeriesKind.Line, priceWindow, highValues: highWindow, lowValues: lowWindow);
            StreamingBollingerBands bands = new(period);
            StreamingDonchianChannels channels = new(period);
            Compare($"window_bollinger_p{period}", () => SetWindow(0), () =>
            {
                for (int i = period; i < prices.Length; i++)
                {
                    SetWindow(i - period + 1);
                    ChartBandSeries result = ChartIndicators.BollingerBands(window, period);
                    Check(expectedBands, i, new ChartBandValue(result.Lower.Values[^1], result.Middle.Values[^1], result.Upper.Values[^1]));
                }
            }, () =>
            {
                bands.Reset(); for (int i = 0; i < period; i++) bands.Push(prices[i]);
                if (!bands.IsReady) throw new InvalidOperationException("Bollinger seed was not ready.");
            }, () =>
            {
                for (int i = period; i < prices.Length; i++) Check(expectedBands, i, bands.Push(prices[i]));
            });
            Compare($"window_donchian_p{period}", () => SetWindow(0), () =>
            {
                for (int i = period; i < prices.Length; i++)
                {
                    SetWindow(i - period + 1);
                    ChartBandSeries result = ChartIndicators.DonchianChannels(window, period);
                    Check(expectedChannels, i, new ChartBandValue(result.Lower.Values[^1], result.Middle.Values[^1], result.Upper.Values[^1]));
                }
            }, () =>
            {
                channels.Reset(); for (int i = 0; i < period; i++) channels.Push(highs[i], lows[i], prices[i]);
                if (!channels.IsReady) throw new InvalidOperationException("Donchian seed was not ready.");
            }, () =>
            {
                for (int i = period; i < prices.Length; i++) Check(expectedChannels, i, channels.Push(highs[i], lows[i], prices[i]));
            });
            void SetWindow(int start) { priceWindow.Start = highWindow.Start = lowWindow.Start = start; }
        }
        Console.WriteLine("Window-only and prefix comparisons have different seed/update schedules and must not be combined into one ratio. This measures calculator APIs, not a prior app, UI drawing, GPU execution or frame rate.");
    }

    private static void Check(ChartBandSeries expected, int index, ChartBandValue actual)
    { Near(expected.Lower.Values[index], actual.Lower); Near(expected.Middle.Values[index], actual.Middle); Near(expected.Upper.Values[index], actual.Upper); }

    private static void Near(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue || !double.IsFinite(b.Value) || Math.Abs(a.Value - b.Value) > 1e-8 * Math.Max(1, Math.Abs(a.Value)))
            throw new InvalidOperationException($"Ready-window result mismatch: {a:R} versus {b:R}.");
    }

    private static void Compare(string name, Action setupBatch, Action batch, Action setupLive, Action live)
    {
        for (int i = 0; i < 2; i++) { setupBatch(); batch(); setupLive(); live(); }
        double[] a = new double[7], b = new double[7]; long[] ba = new long[7], bb = new long[7];
        for (int i = 0; i < 7; i++)
        {
            if ((i & 1) == 0) { Measure(setupBatch, batch, out a[i], out ba[i]); Measure(setupLive, live, out b[i], out bb[i]); }
            else { Measure(setupLive, live, out b[i], out bb[i]); Measure(setupBatch, batch, out a[i], out ba[i]); }
        }
        Array.Sort(a); Array.Sort(b); Array.Sort(ba); Array.Sort(bb);
        Console.WriteLine($"{name},batch_window,{a[3].ToString("F3", CultureInfo.InvariantCulture)},{ba[3]}");
        Console.WriteLine($"{name},persistent,{b[3].ToString("F3", CultureInfo.InvariantCulture)},{bb[3]}");
    }

    private static void Measure(Action setup, Action operation, out double time, out long bytes)
    {
        setup(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        operation(); time = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class Slice : IReadOnlyList<double?>
    {
        private readonly double?[] _values;
        public Slice(double?[] values, int count) { _values = values; Count = count; }
        public int Start { get; set; }
        public int Count { get; }
        public double? this[int index] => (uint)index < (uint)Count ? _values[Start + index] : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<double?> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
