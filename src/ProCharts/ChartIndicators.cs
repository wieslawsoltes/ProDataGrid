// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>UI-independent, gap-aware statistical and technical chart transforms.</summary>
    /// <remarks>
    /// Results keep the input point count and owned X coordinates. Null/non-finite observations reset
    /// each calculation and its warmup; they are never silently bridged or treated as zero.
    /// Input collections must remain stable during a call. Output collections are read-only.
    /// Algorithms run in O(n) time, including periodic rolling-statistic rebasing; working windows
    /// use O(period) memory in addition to the input normalization and output arrays.
    /// </remarks>
    public static class ChartIndicators
    {
        /// <summary>Computes a simple moving average after each complete consecutive window.</summary>
        public static ChartSeriesSnapshot SimpleMovingAverage(ChartSeriesSnapshot source, int period = 20)
        {
            ValidatePeriod(period);
            Input input = new(source);
            double?[] means = new double?[input.Count];
            Moments(input, period, means, null, false);
            return input.Series($"SMA ({period})", Decode(input, means));
        }

        /// <summary>Computes an EMA seeded with a full-window SMA and alpha 2/(period+1).</summary>
        public static ChartSeriesSnapshot ExponentialMovingAverage(ChartSeriesSnapshot source, int period = 20)
        {
            ValidatePeriod(period);
            Input input = new(source);
            return input.Series($"EMA ({period})", Decode(input, Ema(input.Values, period, 2d / (period + 1d))));
        }

        /// <summary>Computes a linearly weighted moving average with oldest weight 1 and newest weight period.</summary>
        public static ChartSeriesSnapshot WeightedMovingAverage(ChartSeriesSnapshot source, int period = 20)
        {
            ValidatePeriod(period);
            Input input = new(source);
            double?[] output = new double?[input.Count];
            if (period > input.Count) return input.Series($"WMA ({period})", output);
            double[] window = new double[period];
            int count = 0, next = 0, updates = 0;
            double sum = 0, weighted = 0;
            double denominator = period * (period + 1d) / 2;
            for (int i = 0; i < input.Count; i++)
            {
                if (input.Values[i] is not double value)
                { count = next = updates = 0; sum = weighted = 0; continue; }
                if (count == period)
                {
                    weighted -= sum;
                    sum -= window[next];
                }
                else count++;
                window[next] = value;
                if (++next == period) next = 0;
                sum += value;
                weighted += count * value;
                if (count != period) continue;
                // Rebase every period updates: O(n) overall, without unbounded cancellation drift.
                if (++updates >= period)
                {
                    sum = weighted = 0;
                    int index = next;
                    for (int j = 0; j < period; j++)
                    {
                        sum += window[index];
                        weighted += (j + 1d) * window[index];
                        if (++index == period) index = 0;
                    }
                    updates = 0;
                }
                output[i] = input.Normalizer.Decode(weighted / denominator);
            }
            return input.Series($"WMA ({period})", output);
        }

        /// <summary>Computes rolling population or sample standard deviation. Sample periods must be at least two.</summary>
        public static ChartSeriesSnapshot RollingStandardDeviation(ChartSeriesSnapshot source, int period = 20, bool sample = false)
        {
            ValidatePeriod(period, sample ? 2 : 1);
            Input input = new(source);
            double?[] deviations = new double?[input.Count];
            Moments(input, period, null, deviations, sample);
            return input.Series($"Standard deviation ({period})", Decode(input, deviations, difference: true));
        }

        /// <summary>Computes an SMA and symmetric standard-deviation bands, using population variance by default.</summary>
        public static ChartBandSeries BollingerBands(ChartSeriesSnapshot source, int period = 20,
            double standardDeviations = 2, bool sample = false)
        {
            ValidatePeriod(period, sample ? 2 : 1);
            if (!double.IsFinite(standardDeviations) || standardDeviations < 0)
                throw new ArgumentOutOfRangeException(nameof(standardDeviations));
            Input input = new(source);
            double?[] means = new double?[input.Count], deviations = new double?[input.Count];
            Moments(input, period, means, deviations, sample);
            double?[] lower = new double?[input.Count], upper = new double?[input.Count];
            for (int i = 0; i < input.Count; i++)
                if (means[i] is double mean && deviations[i] is double deviation)
                {
                    // Keep calculations in normalized coordinates until the final fused conversion.
                    lower[i] = input.Normalizer.Decode(mean - standardDeviations * deviation);
                    upper[i] = input.Normalizer.Decode(mean + standardDeviations * deviation);
                }
            return new ChartBandSeries(input.Series($"Bollinger lower ({period})", lower),
                input.Series($"Bollinger middle ({period})", Decode(input, means)),
                input.Series($"Bollinger upper ({period})", upper));
        }

        /// <summary>
        /// Computes Wilder-smoothed RSI after period consecutive price changes (period+1 prices).
        /// All-flat windows return 50, all-gain windows 100, and all-loss windows zero.
        /// </summary>
        public static ChartSeriesSnapshot RelativeStrengthIndex(ChartSeriesSnapshot source, int period = 14)
        {
            ValidatePeriod(period);
            Input input = new(source);
            double?[] output = new double?[input.Count];
            double? previous = null;
            int changes = 0;
            double gain = 0, loss = 0;
            for (int i = 0; i < input.Count; i++)
            {
                if (input.Values[i] is not double value)
                { previous = null; changes = 0; gain = loss = 0; continue; }
                if (previous is not double before) { previous = value; continue; }
                double delta = value - before;
                previous = value;
                double up = Math.Max(0, delta), down = Math.Max(0, -delta);
                if (changes < period)
                {
                    changes++;
                    gain += (up - gain) / changes;
                    loss += (down - loss) / changes;
                }
                else
                {
                    gain += (up - gain) / period;
                    loss += (down - loss) / period;
                }
                if (changes == period)
                    output[i] = gain == 0 && loss == 0 ? 50 : Math.Clamp(100 * (gain / (gain + loss)), 0, 100);
            }
            return input.Series($"RSI ({period})", output);
        }

        /// <summary>Computes fast/slow SMA-seeded EMAs, their difference, its EMA signal and a column histogram.</summary>
        public static ChartMacdSeries MovingAverageConvergenceDivergence(ChartSeriesSnapshot source,
            int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            ValidatePeriod(fastPeriod);
            ValidatePeriod(slowPeriod);
            ValidatePeriod(signalPeriod);
            if (fastPeriod >= slowPeriod)
                throw new ArgumentException("The fast period must be smaller than the slow period.", nameof(fastPeriod));
            Input input = new(source);
            double?[] fast = Ema(input.Values, fastPeriod, 2d / (fastPeriod + 1d));
            double?[] slow = Ema(input.Values, slowPeriod, 2d / (slowPeriod + 1d));
            double?[] line = new double?[input.Count];
            for (int i = 0; i < input.Count; i++)
                if (fast[i] is double a && slow[i] is double b) line[i] = a - b;
            double?[] signal = Ema(line, signalPeriod, 2d / (signalPeriod + 1d));
            double?[] histogram = new double?[input.Count];
            for (int i = 0; i < input.Count; i++)
                if (line[i] is double a && signal[i] is double b) histogram[i] = a - b;
            return new ChartMacdSeries(input.Series("MACD", Decode(input, line, difference: true)),
                input.Series("MACD signal", Decode(input, signal, difference: true)),
                input.Series("MACD histogram", Decode(input, histogram, difference: true), ChartSeriesKind.Column));
        }

        /// <summary>
        /// Computes Wilder ATR from high/low/close channels. The first bar in each finite run uses
        /// high-low; later bars also consider distances from the preceding close. A complete period
        /// of true ranges seeds the first average. Finite inverted high/low pairs are rejected.
        /// </summary>
        public static ChartSeriesSnapshot AverageTrueRange(ChartSeriesSnapshot source, int period = 14)
        {
            ValidatePeriod(period);
            Input input = new(source, range: true);
            double?[] ranges = new double?[input.Count];
            double? previousClose = null;
            for (int i = 0; i < input.Count; i++)
            {
                if (input.Values[i] is not double close || input.High![i] is not double high || input.Low![i] is not double low)
                { previousClose = null; continue; }
                double range = high - low;
                if (previousClose is double previous)
                    range = Math.Max(range, Math.Max(Math.Abs(high - previous), Math.Abs(low - previous)));
                ranges[i] = range;
                previousClose = close;
            }
            return input.Series($"ATR ({period})", Decode(input, Ema(ranges, period, 1d / period), difference: true));
        }

        /// <summary>
        /// Computes rolling high/low extrema and their midpoint using monotonic queues.
        /// High, low and close must all be finite throughout a complete window; any gap resets it.
        /// The current bar is included in the window.
        /// </summary>
        public static ChartBandSeries DonchianChannels(ChartSeriesSnapshot source, int period = 20)
        {
            ValidatePeriod(period);
            Input input = new(source, range: true);
            double?[] lower = new double?[input.Count], middle = new double?[input.Count], upper = new double?[input.Count];
            if (period <= input.Count)
            {
                ExtremumQueue highs = new(period, maximum: true), lows = new(period, maximum: false);
                int count = 0;
                for (int i = 0; i < input.Count; i++)
                {
                    if (!input.Values[i].HasValue || input.High![i] is not double high || input.Low![i] is not double low)
                    { count = 0; highs.Clear(); lows.Clear(); continue; }
                    highs.Add(i, high);
                    lows.Add(i, low);
                    if (count < period) count++;
                    if (count != period) continue;
                    lower[i] = input.Normalizer.Decode(lows.Value);
                    middle[i] = input.Normalizer.Decode(lows.Value / 2 + highs.Value / 2);
                    upper[i] = input.Normalizer.Decode(highs.Value);
                }
            }
            return new ChartBandSeries(input.Series($"Donchian lower ({period})", lower),
                input.Series($"Donchian middle ({period})", middle), input.Series($"Donchian upper ({period})", upper));
        }

        private static void ValidatePeriod(int period, int minimum = 1)
        {
            if (period < minimum) throw new ArgumentOutOfRangeException(nameof(period), $"Period must be at least {minimum}.");
        }

        private static double?[] Decode(Input input, double?[] normalized, bool difference = false)
        {
            for (int i = 0; i < normalized.Length; i++)
                if (normalized[i] is double value)
                    normalized[i] = difference ? input.Normalizer.DecodeDifference(value) : input.Normalizer.Decode(value);
            return normalized;
        }

        private static double?[] Ema(IReadOnlyList<double?> values, int period, double alpha)
        {
            double?[] output = new double?[values.Count];
            int count = 0;
            double mean = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not double value || !double.IsFinite(value)) { count = 0; mean = 0; continue; }
                if (count < period) { count++; mean += (value - mean) / count; }
                else mean += alpha * (value - mean);
                if (count == period) output[i] = mean;
            }
            return output;
        }

        private static void Moments(Input input, int period, double?[]? means, double?[]? deviations, bool sample)
        {
            if (period > input.Count) return;
            MomentWindow window = new(period);
            for (int i = 0; i < input.Count; i++)
            {
                if (input.Values[i] is not double value) { window.Clear(); continue; }
                window.Add(value);
                if (window.Count != period) continue;
                if (means != null) means[i] = window.Mean;
                if (deviations != null) deviations[i] = Math.Sqrt(Math.Max(0, window.M2) / (sample ? period - 1d : period));
            }
        }

        private sealed class Input
        {
            private readonly ChartSeriesSnapshot _source;
            private readonly IReadOnlyList<double>? _x;

            public Input(ChartSeriesSnapshot source, bool range = false)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                if (source.XValues != null)
                {
                    if (source.XValues.Count != source.Values.Count)
                        throw new ArgumentException("X coordinates must align with every input value.", nameof(source));
                    double[] x = new double[source.XValues.Count];
                    for (int i = 0; i < x.Length; i++) x[i] = source.XValues[i];
                    _x = Array.AsReadOnly(x);
                }
                if (range)
                {
                    if (source.HighValues == null || source.LowValues == null ||
                        source.HighValues.Count != source.Values.Count || source.LowValues.Count != source.Values.Count)
                        throw new ArgumentException("Aligned high and low channels are required.", nameof(source));
                    for (int i = 0; i < source.Values.Count; i++)
                        if (source.HighValues[i] is double high && double.IsFinite(high) &&
                            source.LowValues[i] is double low && double.IsFinite(low) && high < low)
                            throw new ArgumentException("Finite high values cannot be lower than their corresponding low values.", nameof(source));
                    Normalizer = new ValueNormalizer(source.Values, source.HighValues, source.LowValues);
                    High = Normalizer.Normalize(source.HighValues);
                    Low = Normalizer.Normalize(source.LowValues);
                }
                else Normalizer = new ValueNormalizer(source.Values);
                Values = Normalizer.Normalize(source.Values);
            }

            public int Count => Values.Length;
            public double?[] Values { get; }
            public double?[]? High { get; }
            public double?[]? Low { get; }
            public ValueNormalizer Normalizer { get; }

            public ChartSeriesSnapshot Series(string suffix, double?[] values, ChartSeriesKind kind = ChartSeriesKind.Line)
            {
                string name = string.IsNullOrWhiteSpace(_source.Name) ? suffix : _source.Name + " " + suffix;
                return new ChartSeriesSnapshot(name, kind, Array.AsReadOnly(values), _x,
                    valueAxisAssignment: _source.ValueAxisAssignment);
            }
        }

        private sealed class ValueNormalizer
        {
            private readonly double _origin;
            private readonly double _scale;

            public ValueNormalizer(params IReadOnlyList<double?>[] channels)
            {
                bool found = false, overflow = false;
                double origin = 0, difference = 0, magnitude = 0;
                foreach (IReadOnlyList<double?> channel in channels)
                    for (int i = 0; i < channel.Count; i++)
                        if (channel[i] is double value && double.IsFinite(value))
                        {
                            if (!found) { origin = value; found = true; }
                            magnitude = Math.Max(magnitude, Math.Abs(value));
                            double distance = Math.Abs(value - origin);
                            if (!double.IsFinite(distance)) overflow = true;
                            else difference = Math.Max(difference, distance);
                        }
                _origin = overflow ? 0 : origin;
                _scale = overflow ? magnitude : difference;
                if (_scale == 0) _scale = 1;
            }

            public double?[] Normalize(IReadOnlyList<double?> values)
            {
                double?[] normalized = new double?[values.Count];
                for (int i = 0; i < values.Count; i++)
                    if (values[i] is double value && double.IsFinite(value)) normalized[i] = (value - _origin) / _scale;
                return normalized;
            }

            public double? Decode(double value) => Finite(Math.FusedMultiplyAdd(value, _scale, _origin));
            public double? DecodeDifference(double value) => Finite(value * _scale);
            private static double? Finite(double value) => double.IsFinite(value) ? value : null;
        }

        private sealed class MomentWindow
        {
            private readonly double[] _values;
            private int _next;
            private int _updates;
            public MomentWindow(int capacity) { _values = new double[capacity]; }
            public int Count { get; private set; }
            public double Mean { get; private set; }
            public double M2 { get; private set; }
            public void Clear() { _next = _updates = Count = 0; Mean = M2 = 0; }
            public void Add(double value)
            {
                if (Count == _values.Length)
                {
                    double old = _values[_next];
                    if (Count == 1) { Mean = M2 = 0; Count = 0; }
                    else
                    {
                        double nextMean = Mean + (Mean - old) / (Count - 1);
                        M2 -= (old - Mean) * (old - nextMean);
                        Mean = nextMean;
                        Count--;
                    }
                }
                _values[_next] = value;
                if (++_next == _values.Length) _next = 0;
                Count++;
                double delta = value - Mean;
                Mean += delta / Count;
                M2 += delta * (value - Mean);
                if (Count == _values.Length && ++_updates >= _values.Length)
                {
                    Mean = M2 = 0;
                    for (int i = 0; i < _values.Length; i++)
                    {
                        double d = _values[i] - Mean;
                        Mean += d / (i + 1d);
                        M2 += d * (_values[i] - Mean);
                    }
                    _updates = 0;
                }
            }
        }

        private sealed class ExtremumQueue
        {
            private readonly (int Index, double Value)[] _items;
            private readonly bool _maximum;
            private int _head;
            private int _count;
            public ExtremumQueue(int period, bool maximum) { _items = new (int, double)[period]; _maximum = maximum; }
            public double Value => _items[_head].Value;
            public void Clear() { _head = _count = 0; }
            public void Add(int index, double value)
            {
                while (_count > 0 && _items[_head].Index <= index - _items.Length)
                { if (++_head == _items.Length) _head = 0; _count--; }
                while (_count > 0)
                {
                    int last = (int)(((long)_head + _count - 1) % _items.Length);
                    if (_maximum ? _items[last].Value > value : _items[last].Value < value) break;
                    _count--;
                }
                int next = (int)(((long)_head + _count) % _items.Length);
                _items[next] = (index, value);
                _count++;
            }
        }
    }
}
