// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartIndicatorTests
    {
        private static ChartSeriesSnapshot Series(params double?[] values) => new("Input", ChartSeriesKind.Line, values);

        [Fact]
        public void Moving_Averages_Have_Explicit_Complete_Window_Seeding()
        {
            ChartSeriesSnapshot source = Series(1, 2, 3, 4, 5, 6);
            AssertValues(new double?[] { null, null, 2, 3, 4, 5 }, ChartIndicators.SimpleMovingAverage(source, 3).Values);
            AssertValues(new double?[] { null, null, 2, 3, 4, 5 }, ChartIndicators.ExponentialMovingAverage(source, 3).Values);
            AssertValues(new double?[] { null, null, 14d / 6, 20d / 6, 26d / 6, 32d / 6 },
                ChartIndicators.WeightedMovingAverage(source, 3).Values);
            AssertValues(source.Values, ChartIndicators.SimpleMovingAverage(source, 1).Values);
            AssertValues(source.Values, ChartIndicators.ExponentialMovingAverage(source, 1).Values);
            AssertValues(source.Values, ChartIndicators.WeightedMovingAverage(source, 1).Values);
        }

        [Fact]
        public void Variance_And_Bollinger_Bands_Use_Documented_Denominators()
        {
            ChartSeriesSnapshot source = Series(1, 2, 3);
            double population = Math.Sqrt(2d / 3);
            AssertValues(new double?[] { null, null, population }, ChartIndicators.RollingStandardDeviation(source, 3).Values);
            AssertValues(new double?[] { null, null, 1 }, ChartIndicators.RollingStandardDeviation(source, 3, sample: true).Values);
            ChartBandSeries bands = ChartIndicators.BollingerBands(source, 3);
            AssertValues(new double?[] { null, null, 2 }, bands.Middle.Values);
            AssertValues(new double?[] { null, null, 2 - 2 * population }, bands.Lower.Values);
            AssertValues(new double?[] { null, null, 2 + 2 * population }, bands.Upper.Values);
            AssertValues(new double?[] { 0, 0, 0 }, ChartIndicators.RollingStandardDeviation(source, 1).Values);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(17)]
        [InlineData(100)]
        public void Rolling_Moments_And_Weighted_Averages_Match_Slow_Seeded_Reference(int period)
        {
            Random random = new(1973 + period);
            for (int pass = 0; pass < 8; pass++)
            {
                double?[] values = Enumerable.Range(0, 700)
                    .Select(i => i % 137 == 12 ? (double?)null : (random.NextDouble() - 0.5) * 200).ToArray();
                ChartSeriesSnapshot source = Series(values);
                double?[] mean = new double?[values.Length], weighted = new double?[values.Length], std = new double?[values.Length];
                for (int i = period - 1; i < values.Length; i++)
                {
                    double[] window = values.Skip(i - period + 1).Take(period).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                    if (window.Length != period) continue;
                    double origin = window[0];
                    double center = window.Sum(v => v - origin) / period;
                    mean[i] = origin + center;
                    weighted[i] = origin + window.Select((v, index) => (v - origin) * (index + 1d)).Sum() / (period * (period + 1d) / 2);
                    std[i] = Math.Sqrt(window.Sum(v => Math.Pow((v - origin) - center, 2)) / period);
                }
                AssertValues(mean, ChartIndicators.SimpleMovingAverage(source, period).Values, 1e-8);
                AssertValues(weighted, ChartIndicators.WeightedMovingAverage(source, period).Values, 1e-8);
                AssertValues(std, ChartIndicators.RollingStandardDeviation(source, period).Values, 1e-8);
            }
        }

        [Fact]
        public void Gaps_Reset_Warmup_Without_Changing_Indices()
        {
            ChartSeriesSnapshot source = Series(1, 2, 3, null, 4, 5, 6, double.NaN, 7, 8, 9, double.PositiveInfinity, 10);
            double?[] means = { null, null, 2, null, null, null, 5, null, null, null, 8, null, null };
            AssertValues(means, ChartIndicators.SimpleMovingAverage(source, 3).Values);
            AssertValues(means, ChartIndicators.ExponentialMovingAverage(source, 3).Values);
            AssertValues(new double?[] { null, null, 100, null, null, null, 100, null, null, null, 100, null, null },
                ChartIndicators.RelativeStrengthIndex(source, 2).Values);
            ChartBandSeries bands = ChartIndicators.BollingerBands(source, 3);
            for (int i = 0; i < means.Length; i++)
            {
                Assert.Equal(means[i].HasValue, bands.Lower.Values[i].HasValue);
                Assert.Equal(means[i].HasValue, bands.Upper.Values[i].HasValue);
            }
        }

        [Theory]
        [InlineData(0, 50)]
        [InlineData(1, 100)]
        [InlineData(-1, 0)]
        public void Rsi_Defines_Flat_Gain_And_Loss_Limits(int slope, double expected)
        {
            ChartSeriesSnapshot source = Series(Enumerable.Range(0, 40).Select(i => (double?)(100 + i * slope)).ToArray());
            var values = ChartIndicators.RelativeStrengthIndex(source, 14).Values;
            Assert.All(values.Take(14), value => Assert.Null(value));
            Assert.All(values.Skip(14), value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Rsi_Uses_Wilder_Smoothing_And_Not_A_Sliding_Sma()
        {
            AssertValues(new double?[] { null, null, 50, 75, 37.5 },
                ChartIndicators.RelativeStrengthIndex(Series(1, 2, 1, 2, 1), 2).Values);
        }

        [Fact]
        public void Macd_Aligns_Line_Signal_And_Histogram_Warmups()
        {
            ChartMacdSeries macd = ChartIndicators.MovingAverageConvergenceDivergence(Series(1, 2, 3, 4, 5, 6), 2, 3, 2);
            AssertValues(new double?[] { null, null, 0.5, 0.5, 0.5, 0.5 }, macd.Line.Values);
            AssertValues(new double?[] { null, null, null, 0.5, 0.5, 0.5 }, macd.Signal.Values);
            AssertValues(new double?[] { null, null, null, 0, 0, 0 }, macd.Histogram.Values);
            Assert.Equal(ChartSeriesKind.Line, macd.Line.Kind);
            Assert.Equal(ChartSeriesKind.Column, macd.Histogram.Kind);
        }

        [Fact]
        public void Ema_And_Macd_Match_An_Independent_Reference_Across_Gaps()
        {
            Random random = new(9617);
            double?[] values = Enumerable.Range(0, 500).Select(i => i % 61 == 0 ? (double?)null : random.NextDouble() * 100).ToArray();
            double?[] fast = ReferenceEma(values, 5), slow = ReferenceEma(values, 12);
            AssertValues(fast, ChartIndicators.ExponentialMovingAverage(Series(values), 5).Values);
            double?[] line = fast.Zip(slow, (a, b) => a.HasValue && b.HasValue ? a - b : null).ToArray();
            double?[] signal = ReferenceEma(line, 4);
            double?[] histogram = line.Zip(signal, (a, b) => a.HasValue && b.HasValue ? a - b : null).ToArray();
            ChartMacdSeries result = ChartIndicators.MovingAverageConvergenceDivergence(Series(values), 5, 12, 4);
            AssertValues(line, result.Line.Values);
            AssertValues(signal, result.Signal.Values);
            AssertValues(histogram, result.Histogram.Values);
        }

        [Fact]
        public void Atr_Uses_Previous_Close_And_Wilder_Seed()
        {
            ChartSeriesSnapshot source = new("OHLC", ChartSeriesKind.Candlestick, new double?[] { 10, 12, 11, 15 },
                highValues: new double?[] { 11, 13, 12, 16 }, lowValues: new double?[] { 9, 11, 10, 14 });
            AssertValues(new double?[] { null, 2.5, 2.25, 3.625 }, ChartIndicators.AverageTrueRange(source, 2).Values);
            ChartBandSeries range = ChartIndicators.DonchianChannels(source, 2);
            AssertValues(new double?[] { null, 9, 10, 10 }, range.Lower.Values);
            AssertValues(new double?[] { null, 13, 13, 16 }, range.Upper.Values);
            AssertValues(new double?[] { null, 11, 11.5, 13 }, range.Middle.Values);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(19)]
        [InlineData(100)]
        public void Donchian_Monotonic_Queues_Match_Window_Scans(int period)
        {
            Random random = new(3476);
            double?[] close = new double?[800], high = new double?[800], low = new double?[800];
            for (int i = 0; i < close.Length; i++)
            {
                close[i] = random.NextDouble() * 100;
                high[i] = close[i] + random.NextDouble() * 10;
                low[i] = close[i] - random.NextDouble() * 10;
                if (i % 173 == 17) high[i] = null;
            }
            ChartSeriesSnapshot source = new("Range", ChartSeriesKind.Candlestick, close, highValues: high, lowValues: low);
            ChartBandSeries result = ChartIndicators.DonchianChannels(source, period);
            double?[] expectedHigh = new double?[close.Length], expectedLow = new double?[close.Length];
            for (int i = period - 1; i < close.Length; i++)
            {
                var indices = Enumerable.Range(i - period + 1, period).ToArray();
                if (indices.Any(j => !close[j].HasValue || !high[j].HasValue || !low[j].HasValue)) continue;
                expectedHigh[i] = indices.Max(j => high[j]);
                expectedLow[i] = indices.Min(j => low[j]);
            }
            AssertValues(expectedHigh, result.Upper.Values);
            AssertValues(expectedLow, result.Lower.Values);
        }

        [Fact]
        public void Incomplete_Ohlc_Bars_Reset_Previous_Close_And_Warmup()
        {
            ChartSeriesSnapshot source = new("Range", ChartSeriesKind.Candlestick, new double?[] { 10, 12, 9000, 20, 21 },
                highValues: new double?[] { 11, 13, null, 21, 22 }, lowValues: new double?[] { 9, 11, 8999, 19, 20 });
            AssertValues(new double?[] { null, 2.5, null, null, 2 }, ChartIndicators.AverageTrueRange(source, 2).Values);
            AssertValues(new double?[] { null, 13, null, null, 22 }, ChartIndicators.DonchianChannels(source, 2).Upper.Values);
        }

        [Fact]
        public void Large_Offsets_Do_Not_Destroy_Small_Rolling_Variance()
        {
            double?[] values = Enumerable.Range(0, 200).Select(i => (double?)(1e12 + i * 0.25)).ToArray();
            ChartSeriesSnapshot source = Series(values);
            var deviations = ChartIndicators.RollingStandardDeviation(source, 10).Values;
            double expected = Math.Sqrt(8.25 * 0.25 * 0.25);
            for (int i = 9; i < deviations.Count; i++) Assert.InRange(Math.Abs(deviations[i]!.Value - expected), 0, 1e-10);
            ChartMacdSeries macd = ChartIndicators.MovingAverageConvergenceDivergence(source, 2, 3, 2);
            for (int i = 3; i < values.Length; i++) Assert.InRange(Math.Abs(macd.Line.Values[i]!.Value - 0.125), 0, 1e-10);
        }

        [Fact]
        public void Finite_Extreme_Coordinates_Do_Not_Overflow_Intermediate_Means_Or_Rsi()
        {
            ChartSeriesSnapshot source = Series(-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue);
            AssertValues(new double?[] { null, 0, 0, 0 }, ChartIndicators.SimpleMovingAverage(source, 2).Values);
            Assert.All(ChartIndicators.ExponentialMovingAverage(source, 2).Values.Skip(1), value => Assert.True(double.IsFinite(value!.Value)));
            Assert.All(ChartIndicators.WeightedMovingAverage(source, 2).Values.Skip(1), value => Assert.True(double.IsFinite(value!.Value)));
            Assert.All(ChartIndicators.RelativeStrengthIndex(source, 2).Values.Skip(2), value => Assert.InRange(value!.Value, 0, 100));
            Assert.All(ChartIndicators.RollingStandardDeviation(source, 2, sample: true).Values, value => Assert.Null(value));
            ChartSeriesSnapshot constant = Series(double.MaxValue, double.MaxValue, double.MaxValue);
            Assert.Equal(double.MaxValue, ChartIndicators.SimpleMovingAverage(constant, 2).Values[1]);
            Assert.Equal(0, ChartIndicators.RollingStandardDeviation(constant, 2).Values[1]);
        }

        [Fact]
        public void Results_Own_Their_Arrays_And_Keep_X_Alignment_And_Axis_Assignment()
        {
            double?[] values = { 10, 20, 30 };
            double[] x = { 100, 200, 300 };
            ChartSeriesSnapshot source = new("Price", ChartSeriesKind.Line, values, x,
                valueAxisAssignment: ChartValueAxisAssignment.Secondary);
            ChartBandSeries result = ChartIndicators.BollingerBands(source, 2);
            values[1] = 999;
            x[1] = 999;
            Assert.Equal(15, result.Middle.Values[1]);
            Assert.Equal(new double[] { 100, 200, 300 }, result.Middle.XValues);
            Assert.Same(result.Middle.XValues, result.Upper.XValues);
            Assert.Equal(ChartValueAxisAssignment.Secondary, result.Middle.ValueAxisAssignment);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)result.Middle.Values)[1] = 999);
            Assert.Throws<NotSupportedException>(() => ((IList<double>)result.Middle.XValues!)[1] = 999);
        }

        [Fact]
        public void Empty_Huge_Period_And_Invalid_Inputs_Are_Handled_Without_Oversized_Windows()
        {
            Assert.Empty(ChartIndicators.SimpleMovingAverage(Series(), 20).Values);
            Assert.All(ChartIndicators.SimpleMovingAverage(Series(1, 2, 3), int.MaxValue).Values, value => Assert.Null(value));
            Assert.All(ChartIndicators.WeightedMovingAverage(Series(1, 2, 3), int.MaxValue).Values, value => Assert.Null(value));
            Assert.Throws<ArgumentNullException>(() => ChartIndicators.SimpleMovingAverage(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartIndicators.SimpleMovingAverage(Series(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartIndicators.RollingStandardDeviation(Series(1), 1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartIndicators.BollingerBands(Series(1), 1, double.NaN));
            Assert.Throws<ArgumentException>(() => ChartIndicators.MovingAverageConvergenceDivergence(Series(1), 5, 5));
            Assert.Throws<ArgumentException>(() => ChartIndicators.AverageTrueRange(Series(1)));
            Assert.Throws<ArgumentException>(() => ChartIndicators.SimpleMovingAverage(new ChartSeriesSnapshot("Ragged", ChartSeriesKind.Line,
                new double?[] { 1, 2 }, new double[] { 1 })));
            ChartSeriesSnapshot inverted = new("Invalid", ChartSeriesKind.Candlestick, new double?[] { 1 },
                highValues: new double?[] { 0 }, lowValues: new double?[] { 2 });
            Assert.Throws<ArgumentException>(() => ChartIndicators.AverageTrueRange(inverted));
            Assert.Throws<ArgumentException>(() => ChartIndicators.DonchianChannels(inverted));
        }

        private static double?[] ReferenceEma(IReadOnlyList<double?> values, int period)
        {
            double?[] result = new double?[values.Count];
            Queue<double> seed = new();
            double? previous = null;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not double value || !double.IsFinite(value)) { seed.Clear(); previous = null; continue; }
                if (!previous.HasValue)
                {
                    seed.Enqueue(value);
                    if (seed.Count == period) previous = seed.Average();
                }
                else previous = value * (2d / (period + 1)) + previous * (1 - 2d / (period + 1));
                result[i] = previous;
            }
            return result;
        }

        private static void AssertValues(IReadOnlyList<double?> expected, IReadOnlyList<double?> actual, double tolerance = 1e-10)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].HasValue, actual[i].HasValue);
                if (expected[i] is double value)
                    Assert.True(double.IsFinite(actual[i]!.Value) && Math.Abs(value - actual[i]!.Value) <= tolerance * Math.Max(1, Math.Abs(value)),
                        $"Index {i}: expected {value:R}, actual {actual[i]:R}");
            }
        }
    }
}
