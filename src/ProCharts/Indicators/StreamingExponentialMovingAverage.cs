// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>An allocation-free, constant-space EMA seeded with a complete-window arithmetic mean.</summary>
    /// <remarks>
    /// Uses alpha 2/(period+1), matching ChartIndicators.ExponentialMovingAverage conventions.
    /// Null and non-finite inputs reset warmup. State survives any unrelated display-ring eviction.
    /// Each Push takes constant work and allocates no managed memory. Instances are single-writer.
    /// </remarks>
    public sealed class StreamingExponentialMovingAverage : IStreamingChartIndicator<double?, double?>
    {
        private StreamingIndicatorScale _scale;
        private StreamingIndicatorAverage _average;
        private readonly double _alpha;

        /// <summary>Creates an EMA with a strictly positive observation period.</summary>
        public StreamingExponentialMovingAverage(int period = 20)
        {
            if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
            Period = period;
            _alpha = 2d / (period + 1d);
        }

        /// <summary>Gets the number of consecutive prices needed to seed the EMA.</summary>
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
            if (_scale.Include(value, out double factor, out double shift)) _average.Transform(factor, shift);
            Current = _average.Add(_scale.Encode(value), Period, _alpha) ? _scale.Decode(_average.Mean) : null;
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
            _average = default;
            Current = null;
        }
    }
}
