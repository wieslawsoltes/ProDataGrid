// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using ProCharts;

internal static class IndicatorBenchmarks
{
    public static void Run(Action<string, Action> measure)
    {
        const int count = 200000;
        double?[] close = new double?[count], high = new double?[count], low = new double?[count];
        for (int i = 0; i < count; i++)
        {
            double value = 100 + Math.Sin(i * 0.003) * 10 + i * 0.0001;
            close[i] = value;
            high[i] = value + 2;
            low[i] = value - 2;
        }
        ChartSeriesSnapshot source = new("Price", ChartSeriesKind.Candlestick, close, highValues: high, lowValues: low);
        Console.WriteLine($"Indicator input: {count}; source construction excluded; normalization, output ownership and allocations included.");
        foreach (int period in new[] { 20, 2000 })
        {
            measure($"SMA_200000_period_{period}", () => Check(ChartIndicators.SimpleMovingAverage(source, period), count));
            measure($"WMA_200000_period_{period}", () => Check(ChartIndicators.WeightedMovingAverage(source, period), count));
            measure($"StandardDeviation_200000_period_{period}", () => Check(ChartIndicators.RollingStandardDeviation(source, period), count));
            measure($"Donchian_200000_period_{period}", () =>
            {
                ChartBandSeries bands = ChartIndicators.DonchianChannels(source, period);
                Check(bands.Lower, count); Check(bands.Middle, count); Check(bands.Upper, count);
            });
        }
        measure("Bollinger_200000_period_20", () =>
        {
            ChartBandSeries bands = ChartIndicators.BollingerBands(source);
            Check(bands.Lower, count); Check(bands.Middle, count); Check(bands.Upper, count);
        });
        measure("RSI_200000_period_14", () => Check(ChartIndicators.RelativeStrengthIndex(source), count));
        measure("ATR_200000_period_14", () => Check(ChartIndicators.AverageTrueRange(source), count));
        measure("MACD_200000_12_26_9", () =>
        {
            ChartMacdSeries macd = ChartIndicators.MovingAverageConvergenceDivergence(source);
            Check(macd.Line, count); Check(macd.Signal, count); Check(macd.Histogram, count);
        });

        const int referenceCount = 20000, referencePeriod = 2000;
        double?[] referenceInput = new double?[referenceCount];
        Array.Copy(close, referenceInput, referenceCount);
        ChartSeriesSnapshot referenceSource = new("Reference", ChartSeriesKind.Line, referenceInput);
        var expected = WindowScan(referenceInput, referencePeriod);
        var actual = ChartIndicators.SimpleMovingAverage(referenceSource, referencePeriod).Values;
        for (int i = 0; i < referenceCount; i++)
            if (expected[i].HasValue != actual[i].HasValue ||
                (expected[i] is double value && Math.Abs(value - actual[i]!.Value) > 1e-8))
                throw new InvalidOperationException($"Reference-window comparison failed at {i}.");
        measure("SMA_window_scan_reference_20000_period_2000", () =>
        {
            var result = WindowScan(referenceInput, referencePeriod);
            if (!result[^1].HasValue) throw new InvalidOperationException("Reference output is missing.");
        });
        measure("SMA_rolling_20000_period_2000", () => Check(ChartIndicators.SimpleMovingAverage(referenceSource, referencePeriod), referenceCount));
        Console.WriteLine("Window-scan baseline is an independent O(n * period) reference, not the prior chart renderer. No shared-runner timing thresholds or GPU/frame-rate claims are made.");
    }

    private static void Check(ChartSeriesSnapshot series, int count)
    {
        if (series.Values.Count != count || series.Values[^1] is not double value || !double.IsFinite(value))
            throw new InvalidOperationException("Indicator result is not finite and aligned.");
    }

    private static double?[] WindowScan(IReadOnlyList<double?> values, int period)
    {
        double?[] output = new double?[values.Count];
        for (int i = period - 1; i < values.Count; i++)
        {
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++) sum += values[j]!.Value;
            output[i] = sum / period;
        }
        return output;
    }
}
