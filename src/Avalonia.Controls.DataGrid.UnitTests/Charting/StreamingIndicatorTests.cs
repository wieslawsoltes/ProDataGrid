// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Linq;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingIndicatorTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(14)]
        [InlineData(99)]
        public void Scalar_Recurrences_Match_Batch_At_Every_Index_Across_Gaps(int period)
        {
            Random random = new(7903);
            double?[] close = new double?[2000], high = new double?[2000], low = new double?[2000];
            for (int i = 0; i < close.Length; i++)
            {
                close[i] = 100 + (random.NextDouble() - 0.5) * (i + 1);
                high[i] = close[i] + random.NextDouble() * 10;
                low[i] = close[i] - random.NextDouble() * 10;
                if (i % 211 == 17) close[i] = null;
                if (i % 313 == 5) high[i] = double.NaN;
                if (i % 401 == 3) low[i] = double.NegativeInfinity;
            }
            ChartSeriesSnapshot source = new("Price", ChartSeriesKind.Candlestick, close, highValues: high, lowValues: low);
            var emaReference = ChartIndicators.ExponentialMovingAverage(source, period).Values;
            var rsiReference = ChartIndicators.RelativeStrengthIndex(source, period).Values;
            var atrReference = ChartIndicators.AverageTrueRange(source, period).Values;
            StreamingExponentialMovingAverage ema = new(period);
            StreamingRelativeStrengthIndex rsi = new(period);
            StreamingAverageTrueRange atr = new(period);
            for (int i = 0; i < close.Length; i++)
            {
                Equal(emaReference[i], ema.Push(close[i]));
                Equal(rsiReference[i], rsi.Push(close[i]));
                Equal(atrReference[i], atr.Push(high[i], low[i], close[i]));
            }
            Assert.Equal(close.Length, ema.SamplesProcessed);
            Assert.Equal(close.Length, rsi.SamplesProcessed);
            Assert.Equal(close.Length, atr.SamplesProcessed);
        }

        [Theory]
        [InlineData(1, 2, 1)]
        [InlineData(2, 3, 2)]
        [InlineData(12, 26, 9)]
        public void Macd_Channels_Match_Batch_At_Every_Index(int fast, int slow, int signal)
        {
            Random random = new(8447);
            double?[] values = Enumerable.Range(0, 1800)
                .Select(i => i % 207 == 19 ? (double?)null : 100 + Math.Sin(i * 0.07) * 20 + random.NextDouble()).ToArray();
            ChartMacdSeries expected = ChartIndicators.MovingAverageConvergenceDivergence(
                new ChartSeriesSnapshot("Price", ChartSeriesKind.Line, values), fast, slow, signal);
            StreamingMacd indicator = new(fast, slow, signal);
            for (int i = 0; i < values.Length; i++)
            {
                ChartMacdValue actual = indicator.Push(values[i]);
                Equal(expected.Line.Values[i], actual.Line);
                Equal(expected.Signal.Values[i], actual.Signal);
                Equal(expected.Histogram.Values[i], actual.Histogram);
                Assert.Equal(actual, indicator.Current);
            }
            Assert.Equal(values.Length, indicator.SamplesProcessed);
        }

        [Fact]
        public void Recurrences_Have_Explicit_Known_Warmup_And_Wilder_Values()
        {
            StreamingExponentialMovingAverage ema = new(3);
            Assert.Null(ema.Push(1)); Assert.Null(ema.Push(2)); Equal(2, ema.Push(3)); Equal(3, ema.Push(4));
            StreamingRelativeStrengthIndex rsi = new(2);
            Assert.Null(rsi.Push(1)); Assert.Null(rsi.Push(2)); Equal(50, rsi.Push(1));
            Equal(75, rsi.Push(2)); Equal(37.5, rsi.Push(1));
            StreamingAverageTrueRange atr = new(2);
            Assert.Null(atr.Push(11, 9, 10)); Equal(2.5, atr.Push(13, 11, 12));
            Equal(2.25, atr.Push(12, 10, 11)); Equal(3.625, atr.Push(16, 14, 15));
            StreamingMacd macd = new(2, 3, 2);
            Assert.Equal(default, macd.Push(1)); Assert.Equal(default, macd.Push(2));
            Equal(0.5, macd.Push(3).Line); Assert.Null(macd.Current.Signal);
            ChartMacdValue ready = macd.Push(4);
            Equal(0.5, ready.Line); Equal(0.5, ready.Signal); Equal(0, ready.Histogram);
        }

        [Fact]
        public void Flat_Gain_And_Loss_Rsi_Are_Defined()
        {
            foreach (int slope in new[] { -1, 0, 1 })
            {
                StreamingRelativeStrengthIndex rsi = new(14);
                for (int i = 0; i < 100; i++)
                {
                    var value = rsi.Push(100 + i * slope);
                    if (i < 14) Assert.Null(value);
                    else Equal(slope == 0 ? 50 : slope > 0 ? 100 : 0, value);
                }
            }
        }

        [Fact]
        public void Reset_And_All_Gap_Kinds_Restart_Warmup_Without_Retaining_Price_State()
        {
            StreamingExponentialMovingAverage ema = new(2);
            StreamingRelativeStrengthIndex rsi = new(1);
            StreamingMacd macd = new(1, 2, 1);
            StreamingAverageTrueRange atr = new(2);
            foreach (double? gap in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                for (int i = 0; i < 10; i++) { ema.Push(i); rsi.Push(i); macd.Push(i); atr.Push(i + 1, i - 1, i); }
                Assert.Null(ema.Push(gap)); Assert.Null(rsi.Push(gap)); Assert.Equal(default, macd.Push(gap));
                Assert.Null(atr.Push(10001, 9999, gap));
                Assert.Null(ema.Push(100)); Assert.Null(rsi.Push(100)); Assert.Equal(default, macd.Push(100));
                Assert.Null(atr.Push(101, 99, 100)); Equal(2, atr.Push(102, 100, 101));
                ema.Reset(); rsi.Reset(); macd.Reset(); atr.Reset();
                Assert.Equal(0, ema.SamplesProcessed); Assert.Equal(0, rsi.SamplesProcessed);
                Assert.Equal(0, macd.SamplesProcessed); Assert.Equal(0, atr.SamplesProcessed);
                Assert.Null(ema.Current); Assert.Null(rsi.Current); Assert.Equal(default, macd.Current); Assert.Null(atr.Current);
            }
        }

        [Fact]
        public void Atr_Invalid_Finite_Ranges_Are_Atomic_Even_With_Missing_Close()
        {
            StreamingAverageTrueRange atr = new(2), reference = new(2);
            atr.Push(11, 9, 10); reference.Push(11, 9, 10);
            Assert.Throws<ArgumentException>(() => atr.Push(5, 6, null));
            Assert.Equal(1, atr.SamplesProcessed); Assert.Null(atr.Current);
            Equal(reference.Push(13, 11, 12), atr.Push(13, 11, 12));
            Assert.Throws<ArgumentException>(() => atr.Push(5, 6, 5));
            Assert.Equal(2, atr.SamplesProcessed); Equal(2.5, atr.Current);
            Equal(reference.Push(12, 10, 11), atr.Push(12, 10, 11));
        }

        [Fact]
        public void Large_Common_Offsets_Preserve_Small_Macd_Differences()
        {
            StreamingMacd macd = new(2, 3, 2);
            StreamingAverageTrueRange atr = new(2);
            for (int i = 0; i < 10000; i++)
            {
                double price = 1e12 + i * 0.25;
                ChartMacdValue result = macd.Push(price);
                var range = atr.Push(price + 0.125, price - 0.125, price);
                if (i >= 2) Equal(0.125, result.Line, 2e-9);
                if (i > 100) Equal(0.375, range, 2e-9);
            }
        }

        [Fact]
        public void Normalization_Handles_First_Subnormal_Change_And_Extreme_Scale_Switch()
        {
            StreamingExponentialMovingAverage ema = new(2);
            Assert.Null(ema.Push(0));
            Equal(double.Epsilon * 2, ema.Push(double.Epsilon * 4), 0);
            ema.Reset();
            Assert.Null(ema.Push(-double.MaxValue)); Equal(0, ema.Push(double.MaxValue), 0);
            Assert.True(double.IsFinite(ema.Push(double.MaxValue)!.Value));
            StreamingRelativeStrengthIndex rsi = new(1);
            Assert.Null(rsi.Push(-double.MaxValue)); Equal(100, rsi.Push(double.MaxValue)); Equal(0, rsi.Push(-double.MaxValue));
            StreamingAverageTrueRange atr = new(1);
            Assert.Null(atr.Push(double.MaxValue, -double.MaxValue, 0));
            Equal(2, atr.Push(1, -1, 0), 1e-9);
            StreamingExponentialMovingAverage constant = new(2);
            Assert.Null(constant.Push(double.MaxValue)); Equal(double.MaxValue, constant.Push(double.MaxValue), 0);
        }

        [Fact]
        public void Previously_Emitted_Results_Do_Not_Depend_On_Future_Observations()
        {
            StreamingMacd first = new(2, 3, 2), replay = new(2, 3, 2);
            ChartMacdValue[] saved = Enumerable.Range(0, 80).Select(i => first.Push(100 + Math.Sin(i))).ToArray();
            first.Push(double.MaxValue); first.Push(-double.MaxValue);
            for (int i = 0; i < saved.Length; i++) Assert.Equal(saved[i], replay.Push(100 + Math.Sin(i)));
        }

        [Fact]
        public void Push_And_Reset_Allocate_No_Managed_Memory_After_Warmup()
        {
            StreamingExponentialMovingAverage ema = new(20);
            StreamingRelativeStrengthIndex rsi = new(14);
            StreamingMacd macd = new();
            StreamingAverageTrueRange atr = new(14);
            for (int i = 0; i < 20000; i++) PushAll(i, ema, rsi, macd, atr);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++) PushAll(i, ema, rsi, macd, atr);
            ema.Reset(); rsi.Reset(); macd.Reset(); atr.Reset();
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Interfaces_Compose_With_The_Bounded_Streaming_Source()
        {
            IStreamingChartIndicator<double?, double?> indicator = new StreamingExponentialMovingAverage(2);
            StreamingChartDataSource source = new(16);
            for (int i = 0; i < 100; i++) source.Append(new ChartSample(i, indicator.Push(i)));
            ChartDataSnapshot snapshot = source.BuildSnapshot(new ChartDataRequest());
            Assert.Equal(16, snapshot.Series[0].Values.Count);
            Equal(83.5, snapshot.Series[0].Values[0]); Equal(98.5, snapshot.Series[0].Values[^1]);
            Assert.Equal(100, indicator.SamplesProcessed);
            indicator.Reset(); Assert.Equal(0, indicator.SamplesProcessed);
        }

        [Fact]
        public void Invalid_And_Huge_Periods_Do_Not_Allocate_Period_Sized_History()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingExponentialMovingAverage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRelativeStrengthIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingAverageTrueRange(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingMacd(0, 2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingMacd(1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingMacd(1, 2, 0));
            Assert.Throws<ArgumentException>(() => new StreamingMacd(2, 2, 1));
            StreamingExponentialMovingAverage ema = new(int.MaxValue);
            StreamingMacd macd = new(1, int.MaxValue, int.MaxValue);
            for (int i = 0; i < 100; i++) { Assert.Null(ema.Push(i)); Assert.Equal(default, macd.Push(i)); }
        }

        private static void PushAll(int i, StreamingExponentialMovingAverage ema, StreamingRelativeStrengthIndex rsi,
            StreamingMacd macd, StreamingAverageTrueRange atr)
        {
            double? value = i % 97 == 0 ? null : 100 + Math.Sin(i * 0.03);
            ema.Push(value); rsi.Push(value); macd.Push(value); atr.Push(value + 2, value - 2, value);
        }

        private static void Equal(double? expected, double? actual, double tolerance = 1e-9)
        {
            Assert.Equal(expected.HasValue, actual.HasValue);
            if (expected is double value)
                Assert.True(double.IsFinite(actual!.Value) && Math.Abs(actual.Value - value) <= tolerance * Math.Max(1, Math.Abs(value)),
                    $"Expected {expected:R}, actual {actual:R}.");
        }
    }
}
