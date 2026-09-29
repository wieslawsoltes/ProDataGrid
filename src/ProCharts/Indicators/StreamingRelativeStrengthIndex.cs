// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Constant-space Wilder RSI requiring period price changes (period+1 prices) for warmup.</summary>
    /// <remarks>
    /// Flat runs return 50, gain-only runs 100 and loss-only runs zero. Null/non-finite inputs reset
    /// the preceding price and warmup. Conventions match ChartIndicators.RelativeStrengthIndex.
    /// Each Push takes constant work, allocates no managed memory and does not retain history.
    /// Instances are single-writer, not thread-safe.
    /// </remarks>
    public sealed class StreamingRelativeStrengthIndex : IStreamingChartIndicator<double?, double?>
    {
        private StreamingIndicatorScale _scale;
        private StreamingIndicatorAverage _gain;
        private StreamingIndicatorAverage _loss;
        private double? _previous;
        private readonly double _alpha;

        /// <summary>Creates an RSI with a strictly positive price-change period.</summary>
        public StreamingRelativeStrengthIndex(int period = 14)
        {
            if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
            Period = period;
            _alpha = 1d / period;
        }

        /// <summary>Gets the number of consecutive price changes needed for the initial averages.</summary>
        public int Period { get; }
        /// <inheritdoc />
        public double? Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed { get; private set; }

        /// <inheritdoc />
        public double? Push(double? observation)
        {
            if (SamplesProcessed < long.MaxValue) SamplesProcessed++;
            if (observation is not double value || !double.IsFinite(value))
            {
                ClearRun();
                return null;
            }
            if (_scale.Include(value, out double factor, out double shift))
            {
                _gain.Transform(factor);
                _loss.Transform(factor);
                if (_previous.HasValue) _previous = Math.FusedMultiplyAdd(_previous.Value, factor, shift);
            }
            double normalized = _scale.Encode(value);
            if (_previous is double previous)
            {
                double change = normalized - previous;
                bool ready = _gain.Add(Math.Max(0, change), Period, _alpha);
                _loss.Add(Math.Max(0, -change), Period, _alpha);
                if (ready)
                {
                    double total = _gain.Mean + _loss.Mean;
                    Current = total == 0 ? 50 : Math.Clamp(100 * (_gain.Mean / total), 0, 100);
                }
            }
            _previous = normalized;
            return Current;
        }

        /// <inheritdoc />
        public void Reset()
        {
            ClearRun();
            SamplesProcessed = 0;
        }

        private void ClearRun()
        {
            _scale = default;
            _gain = _loss = default;
            _previous = null;
            Current = null;
        }
    }
}
