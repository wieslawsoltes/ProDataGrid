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
    public sealed class StreamingRollingIndicatorTests
    {
        [Fact]
        public void Complete_Window_Conventions_And_Scalar_Adapters_Agree()
        {
            StreamingRollingStatistics statistics = new(3);
            StreamingSimpleMovingAverage sma = new(3);
            StreamingWeightedMovingAverage wma = new(3);
            StreamingRollingStandardDeviation population = new(3), sample = new(3, true);
            StreamingBollingerBands bands = new(3);
            for (int i = 1; i <= 12; i++)
            {
                var value = statistics.Push(i);
                Near(value.Mean, sma.Push(i)); Near(value.WeightedMean, wma.Push(i));
                Near(value.PopulationStandardDeviation, population.Push(i)); Near(value.SampleStandardDeviation, sample.Push(i));
                var band = bands.Push(i);
                Assert.Equal(i >= 3, statistics.IsReady); Assert.Equal(Math.Min(i, 3), statistics.Count);
                if (i < 3) { Assert.Equal(default(ChartRollingStatisticsValue), value); Assert.Equal(default(ChartBandValue), band); }
                else
                {
                    Near(i - 1d, value.Mean); Near(i - 2d / 3, value.WeightedMean);
                    Near(Math.Sqrt(2d / 3), value.PopulationStandardDeviation); Near(1, value.SampleStandardDeviation);
                    Near(i - 1 - 2 * Math.Sqrt(2d / 3), band.Lower); Near(i - 1, band.Middle);
                    Near(i - 1 + 2 * Math.Sqrt(2d / 3), band.Upper);
                }
                Assert.Equal(i, statistics.SamplesProcessed); Assert.Equal(i, bands.SamplesProcessed);
            }
            Assert.True(sma.IsReady && wma.IsReady && population.IsReady && sample.IsReady);
            Assert.Equal(3, sma.Period); Assert.Equal(3, wma.Period); Assert.True(sample.Sample);
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(17)] [InlineData(128)] [InlineData(513)]
        public void Seeded_All_Window_Statistics_Match_Independent_Scans_Across_Stack_Transfers(int period)
        {
            Random random = new(70691 + period);
            StreamingRollingStatistics statistics = new(period);
            StreamingBollingerBands bands = new(period, 1.75);
            Queue<double> window = new();
            for (int i = 0; i < 4000; i++)
            {
                double? input = i % 1009 == 5 ? null : random.NextDouble() * 200 - 70;
                var actual = statistics.Push(input); var band = bands.Push(input);
                if (input is double v) { window.Enqueue(v); if (window.Count > period) window.Dequeue(); }
                else window.Clear();
                if (window.Count != period)
                {
                    Assert.Equal(default(ChartRollingStatisticsValue), actual); Assert.Equal(default(ChartBandValue), band); continue;
                }
                double[] data = window.ToArray();
                var expected = Scan(data);
                Near(expected.Mean, actual.Mean); Near(expected.WeightedMean, actual.WeightedMean);
                Near(expected.PopulationStandardDeviation, actual.PopulationStandardDeviation);
                Near(expected.SampleStandardDeviation, actual.SampleStandardDeviation);
                Near(expected.Mean - 1.75 * expected.PopulationStandardDeviation, band.Lower);
                Near(expected.Mean, band.Middle); Near(expected.Mean + 1.75 * expected.PopulationStandardDeviation, band.Upper);
            }
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(127)]
        public void Ordinary_Sequences_Match_Existing_Batch_Indicators_With_Explicit_Tolerance(int period)
        {
            Random random = new(3916);
            double?[] input = Enumerable.Range(0, 1500).Select(i => i % 311 == 10 ? (double?)null : 100 + random.NextDouble() * 20).ToArray();
            ChartSeriesSnapshot source = new("Signal", ChartSeriesKind.Line, input);
            var sma = ChartIndicators.SimpleMovingAverage(source, period);
            var wma = ChartIndicators.WeightedMovingAverage(source, period);
            var deviation = ChartIndicators.RollingStandardDeviation(source, period);
            var bands = ChartIndicators.BollingerBands(source, period);
            StreamingRollingStatistics streaming = new(period); StreamingBollingerBands liveBands = new(period);
            for (int i = 0; i < input.Length; i++)
            {
                var actual = streaming.Push(input[i]); var band = liveBands.Push(input[i]);
                Near(sma.Values[i], actual.Mean, 1e-8); Near(wma.Values[i], actual.WeightedMean, 1e-8);
                Near(deviation.Values[i], actual.PopulationStandardDeviation, 1e-8);
                Near(bands.Lower.Values[i], band.Lower, 1e-8); Near(bands.Upper.Values[i], band.Upper, 1e-8);
            }
        }

        [Theory]
        [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
        public void Nonfinite_Values_And_Nulls_Consume_Observations_And_Restart_Warmup(double missing)
        {
            StreamingRollingStatistics stats = new(2); StreamingBollingerBands bands = new(2, sample: true);
            double?[] input = { 1, 2, missing, 4, 5, null, 6, 7 };
            for (int i = 0; i < input.Length; i++)
            {
                var value = stats.Push(input[i]); var band = bands.Push(input[i]);
                bool ready = i is 1 or 4 or 7;
                Assert.Equal(ready, stats.IsReady); Assert.Equal(ready, bands.IsReady);
                Assert.Equal(ready, value.Mean.HasValue); Assert.Equal(ready, band.Middle.HasValue);
                Assert.Equal(i + 1, stats.SamplesProcessed);
            }
            stats.Reset(); bands.Reset();
            Assert.Equal(0, stats.Count); Assert.Equal(0, stats.SamplesProcessed); Assert.Equal(0, bands.SamplesProcessed);
            Assert.False(stats.IsReady); Assert.Equal(default(ChartRollingStatisticsValue), stats.Current);
            Assert.Equal(default(ChartBandValue), bands.Current);
            Assert.Null(stats.Push(10).Mean); Near(15, stats.Push(20).Mean);
        }

        [Theory]
        [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(257)]
        public void Large_Common_Offset_Does_Not_Erase_Current_Window_Variance(int period)
        {
            StreamingRollingStatistics stats = new(period);
            Queue<double> window = new();
            for (int i = 0; i < 2000; i++)
            {
                double price = 1e12 + (i % 137) * 0.25;
                window.Enqueue(price); if (window.Count > period) window.Dequeue();
                var value = stats.Push(price);
                if (window.Count != period) continue;
                var expected = Scan(window.ToArray());
                Near(expected.Mean, value.Mean, 0.0002, relative: false);
                Near(expected.WeightedMean, value.WeightedMean, 0.0002, relative: false);
                Near(expected.PopulationStandardDeviation, value.PopulationStandardDeviation, 1e-9, relative: false);
            }
        }

        [Theory]
        [InlineData(1e308)] [InlineData(-1e308)]
        public void Expired_Extreme_Outliers_Do_Not_Contaminate_Later_Small_Windows(double outlier)
        {
            foreach (int position in new[] { 0, 1, 2, 3, 4, 7, 8 })
            {
                StreamingRollingStatistics stats = new(3);
                for (int i = 0; i < position; i++) stats.Push(i + 10);
                stats.Push(outlier); stats.Push(1); stats.Push(2);
                var current = stats.Push(3);
                Near(2, current.Mean); Near(14d / 6, current.WeightedMean);
                Near(Math.Sqrt(2d / 3), current.PopulationStandardDeviation); Near(1, current.SampleStandardDeviation);
                Near(3, stats.Push(4).Mean);
            }
        }

        [Fact]
        public void Extreme_Finite_Windows_And_Subnormals_Produce_Finite_Or_Explicit_Missing_Channels()
        {
            StreamingRollingStatistics stats = new(2);
            stats.Push(-double.MaxValue); var value = stats.Push(double.MaxValue);
            Near(0, value.Mean); Near(double.MaxValue / 3, value.WeightedMean);
            Near(double.MaxValue, value.PopulationStandardDeviation); Assert.Null(value.SampleStandardDeviation);
            stats.Reset(); stats.Push(double.MaxValue); value = stats.Push(double.MaxValue);
            Assert.Equal(double.MaxValue, value.Mean); Assert.Equal(double.MaxValue, value.WeightedMean);
            Assert.Equal(0, value.PopulationStandardDeviation);
            stats.Reset(); stats.Push(double.Epsilon); value = stats.Push(double.Epsilon);
            Assert.Equal(double.Epsilon, value.Mean); Assert.Equal(double.Epsilon, value.WeightedMean);
            Assert.Equal(0, value.PopulationStandardDeviation);
            StreamingBollingerBands zero = new(2, 0, true);
            zero.Push(-double.MaxValue); Assert.Equal(new ChartBandValue(0, 0, 0), zero.Push(double.MaxValue));
            StreamingBollingerBands hugeMultiplier = new(2, double.MaxValue, true);
            hugeMultiplier.Push(-double.Epsilon); var tinyBands = hugeMultiplier.Push(double.Epsilon);
            Assert.True(tinyBands.Lower < 0 && tinyBands.Upper > 0);
            Assert.True(double.IsFinite(tinyBands.Lower!.Value) && double.IsFinite(tinyBands.Upper!.Value));
            StreamingBollingerBands wide = new(2, 2);
            wide.Push(-double.MaxValue); var wideBands = wide.Push(double.MaxValue);
            Assert.Equal(0, wideBands.Middle); Assert.Null(wideBands.Lower); Assert.Null(wideBands.Upper);
        }

        [Fact]
        public void Period_One_Defines_Zero_Population_Deviation_And_Undefined_Sample_Deviation()
        {
            StreamingRollingStatistics stats = new(1); StreamingBollingerBands bands = new(1);
            foreach (double v in new[] { -9d, 0, double.Epsilon, double.MaxValue })
            {
                Assert.Equal(new ChartRollingStatisticsValue(v, v, 0, null), stats.Push(v));
                Assert.Equal(new ChartBandValue(v, v, v), bands.Push(v));
            }
        }

        [Fact]
        public void Scalar_Adapters_Reset_All_State_And_Counters()
        {
            IStreamingChartIndicator<double?, double?>[] calculators =
            {
                new StreamingSimpleMovingAverage(2), new StreamingWeightedMovingAverage(2),
                new StreamingRollingStandardDeviation(2), new StreamingRollingStandardDeviation(2, true)
            };
            foreach (var calculator in calculators)
            {
                calculator.Push(1); calculator.Push(2); Assert.NotNull(calculator.Current);
                calculator.Push(null); Assert.Null(calculator.Current); Assert.Equal(3, calculator.SamplesProcessed);
                calculator.Reset(); Assert.Equal(0, calculator.SamplesProcessed); Assert.Null(calculator.Current);
                Assert.Null(calculator.Push(9)); Assert.NotNull(calculator.Push(10));
            }
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(17)] [InlineData(128)] [InlineData(511)]
        public void Donchian_Queues_Match_Independent_Current_Bar_Inclusive_Scans(int period)
        {
            StreamingDonchianChannels channels = new(period);
            Queue<ChartHighLowClose> window = new(); Random random = new(3521);
            for (int i = 0; i < 3000; i++)
            {
                double close = random.Next(0, 10); // duplicates exercise expiry after dominated/equal values
                ChartHighLowClose observation = new(close + random.Next(0, 4), close - random.Next(0, 4), i % 1009 == 2 ? null : close);
                var actual = channels.Push(observation);
                if (!observation.Close.HasValue) window.Clear();
                else { window.Enqueue(observation); if (window.Count > period) window.Dequeue(); }
                Assert.Equal(window.Count, channels.Count);
                if (window.Count != period) { Assert.Equal(default(ChartBandValue), actual); continue; }
                double low = window.Min(v => v.Low!.Value), high = window.Max(v => v.High!.Value);
                Assert.Equal(low, actual.Lower); Assert.Equal(high, actual.Upper); Assert.Equal((low + high) / 2, actual.Middle);
            }
        }

        [Fact]
        public void Donchian_Finite_Inversion_Rejects_Before_Mutation_Even_With_A_Missing_Close()
        {
            StreamingDonchianChannels channels = new(2);
            channels.Push(11, 9, 10); var current = channels.Push(13, 11, 12);
            Assert.Throws<ArgumentException>(() => channels.Push(1, 2, null));
            Assert.Equal(2, channels.SamplesProcessed); Assert.Equal(current, channels.Current);
            Assert.Equal(2, channels.Count); Assert.True(channels.IsReady);
            Assert.Equal(default(ChartBandValue), channels.Push(null, 1, 1));
            Assert.Equal(3, channels.SamplesProcessed); Assert.Equal(0, channels.Count);
            channels.Push(2, 1, 1.5); Assert.Equal(new ChartBandValue(1, 2, 3), channels.Push(3, 2, 2.5));
            channels.Reset(); Assert.Equal(0, channels.SamplesProcessed); Assert.Equal(0, channels.Count);
            Assert.Equal(default(ChartBandValue), channels.Current);
            StreamingDonchianChannels one = new(1);
            Assert.Equal(new ChartBandValue(-double.MaxValue, 0, double.MaxValue), one.Push(double.MaxValue, -double.MaxValue, 0));
            Assert.Equal(default(ChartBandValue), one.Push(1, 0, double.NaN));
            Assert.Equal(new ChartBandValue(double.Epsilon, double.Epsilon, double.Epsilon), one.Push(double.Epsilon, double.Epsilon, double.Epsilon));
        }

        [Fact]
        public void Streaming_Donchian_Agrees_With_Existing_Batch_Channel_Conventions()
        {
            double?[] close = Enumerable.Range(0, 600).Select(i => (double?)(100 + Math.Sin(i * 0.1) * 15)).ToArray();
            double?[] high = close.Select(v => v + 2).ToArray(), low = close.Select(v => v - 3).ToArray();
            high[57] = null; low[158] = double.NaN;
            var source = new ChartSeriesSnapshot("Bars", ChartSeriesKind.Candlestick, close, highValues: high, lowValues: low);
            var expected = ChartIndicators.DonchianChannels(source, 20); StreamingDonchianChannels actual = new(20);
            for (int i = 0; i < close.Length; i++)
            {
                var value = actual.Push(high[i], low[i], close[i]);
                Near(expected.Lower.Values[i], value.Lower); Near(expected.Middle.Values[i], value.Middle); Near(expected.Upper.Values[i], value.Upper);
            }
        }

        [Fact]
        public void All_Updates_And_Resets_Allocate_Zero_Managed_Bytes_After_Construction()
        {
            StreamingRollingStatistics statistics = new(64); StreamingBollingerBands bands = new(64);
            StreamingDonchianChannels channels = new(64);
            StreamingSimpleMovingAverage sma = new(64); StreamingWeightedMovingAverage wma = new(64);
            StreamingRollingStandardDeviation deviation = new(64, true);
            for (int i = 0; i < 1000; i++)
            { statistics.Push(i); bands.Push(i); channels.Push(i + 2, i, i + 1); sma.Push(i); wma.Push(i); deviation.Push(i); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++)
            {
                double? input = i % 509 == 0 ? null : i * 0.25;
                statistics.Push(input); bands.Push(input); channels.Push(input + 2, input, input + 1);
                sma.Push(input); wma.Push(input); deviation.Push(input);
                if (i % 1009 == 0)
                { statistics.Reset(); bands.Reset(); channels.Reset(); sma.Reset(); wma.Reset(); deviation.Reset(); }
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Invalid_Parameters_Fail_Before_Allocating_Window_State()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRollingStatistics(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingSimpleMovingAverage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingWeightedMovingAverage(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRollingStandardDeviation(1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingBollingerBands(1, sample: true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingBollingerBands(2, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingBollingerBands(2, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingBollingerBands(2, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingDonchianChannels(0));
        }

        private static ChartRollingStatisticsValue Scan(double[] window)
        {
            double origin = window[0], center = window.Sum(v => v - origin) / window.Length;
            double m2 = window.Sum(v => ((v - origin) - center) * ((v - origin) - center));
            double weighted = origin + window.Select((v, i) => (v - origin) * (i + 1d)).Sum() / (window.Length * (window.Length + 1d) / 2);
            return new ChartRollingStatisticsValue(origin + center, weighted, Math.Sqrt(m2 / window.Length),
                window.Length > 1 ? Math.Sqrt(m2 / (window.Length - 1)) : null);
        }

        private static void Near(double? expected, double? actual, double tolerance = 1e-10, bool relative = true)
        {
            Assert.Equal(expected.HasValue, actual.HasValue);
            if (!expected.HasValue) return;
            Assert.True(double.IsFinite(actual!.Value));
            double error = Math.Abs(expected.Value - actual.Value);
            double limit = tolerance * (relative ? Math.Max(1, Math.Abs(expected.Value)) : 1);
            Assert.True(error <= limit, $"Expected {expected:R}; actual {actual:R}; error {error:R}, tolerance {limit:R}");
        }
    }
}
