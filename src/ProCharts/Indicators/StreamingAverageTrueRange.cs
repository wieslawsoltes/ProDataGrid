// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Constant-space Wilder ATR over aligned high/low/close observations.</summary>
    /// <remarks>
    /// The first bar in each finite run uses high-low; later bars include distances from the preceding
    /// close. A complete period of true ranges seeds the SMA, followed by alpha 1/period smoothing.
    /// Missing/non-finite channels restart warmup. Inverted finite high/low pairs throw before changing
    /// any state. Conventions match ChartIndicators.AverageTrueRange. Instances are single-writer.
    /// </remarks>
    public sealed class StreamingAverageTrueRange : IStreamingChartIndicator<ChartHighLowClose, double?>
    {
        private StreamingIndicatorScale _scale;
        private StreamingIndicatorAverage _average;
        private double? _previousClose;
        private readonly double _alpha;

        /// <summary>Creates an ATR with a strictly positive observation period.</summary>
        public StreamingAverageTrueRange(int period = 14)
        {
            if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
            Period = period;
            _alpha = 1d / period;
        }

        /// <summary>Gets the number of true ranges needed to seed the average.</summary>
        public int Period { get; }
        /// <inheritdoc />
        public double? Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed { get; private set; }

        /// <summary>Consumes an aligned high/low/close observation without constructing a collection.</summary>
        public double? Push(double? high, double? low, double? close) => Push(new ChartHighLowClose(high, low, close));

        /// <inheritdoc />
        public double? Push(ChartHighLowClose observation)
        {
            if (observation.High is double h && double.IsFinite(h) && observation.Low is double l && double.IsFinite(l) && h < l)
                throw new ArgumentException("Finite high values cannot be lower than low values.", nameof(observation));
            if (SamplesProcessed < long.MaxValue) SamplesProcessed++;
            if (observation.High is not double high || !double.IsFinite(high) ||
                observation.Low is not double low || !double.IsFinite(low) ||
                observation.Close is not double close || !double.IsFinite(close))
            {
                ClearRun();
                return null;
            }
            Include(high);
            Include(low);
            Include(close);
            double normalizedHigh = _scale.Encode(high), normalizedLow = _scale.Encode(low);
            double range = normalizedHigh - normalizedLow;
            if (_previousClose is double previous)
                range = Math.Max(range, Math.Max(Math.Abs(normalizedHigh - previous), Math.Abs(normalizedLow - previous)));
            _previousClose = _scale.Encode(close);
            Current = _average.Add(range, Period, _alpha) ? _scale.DecodeDifference(_average.Mean) : null;
            return Current;
        }

        /// <inheritdoc />
        public void Reset()
        {
            ClearRun();
            SamplesProcessed = 0;
        }

        private void Include(double value)
        {
            if (!_scale.Include(value, out double factor, out double shift)) return;
            _average.Transform(factor);
            if (_previousClose.HasValue) _previousClose = Math.FusedMultiplyAdd(_previousClose.Value, factor, shift);
        }

        private void ClearRun()
        {
            _scale = default;
            _average = default;
            _previousClose = null;
            Current = null;
        }
    }
}
