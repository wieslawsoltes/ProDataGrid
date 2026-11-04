// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Bounded rolling arithmetic/weighted means and population/sample deviations in one calculator.</summary>
    /// <remarks>
    /// Requires Period consecutive finite values; a missing/non-finite value consumes an observation and
    /// restarts warmup. Only the active window contributes. A two-stack moment queue avoids subtractive
    /// variance drift on eviction: O(Period) storage and amortized O(1) Push, with O(Period) transfer bursts.
    /// Construction allocates the window; Push and Reset allocate no managed objects. Instances are
    /// single-writer and append-only. Correcting older observations requires Reset/replay. Floating-point
    /// results are not arbitrary precision; unrepresentable channels are null. Period-one sample deviation
    /// is undefined/null while population deviation is zero. Counts include gaps and saturate at long.MaxValue.
    /// </remarks>
    public sealed class StreamingRollingStatistics : IStreamingChartIndicator<double?, ChartRollingStatisticsValue>
    {
        private readonly RollingMomentWindow _window;
        private RollingMoments _moments;

        /// <summary>Creates a fixed-capacity rolling calculator. Period must be positive and its storage must fit memory.</summary>
        public StreamingRollingStatistics(int period = 20)
        {
            if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
            Period = period;
            _window = new RollingMomentWindow(period);
        }

        /// <summary>Gets the observation window size, including the current finite input.</summary>
        public int Period { get; }
        /// <summary>Gets the retained consecutive finite observation count, bounded by Period.</summary>
        public int Count => _window.Count;
        /// <summary>Gets whether a complete finite window is present, independently of numerical output overflow.</summary>
        public bool IsReady => Count == Period;
        /// <inheritdoc />
        public ChartRollingStatisticsValue Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed { get; private set; }

        /// <inheritdoc />
        public ChartRollingStatisticsValue Push(double? observation)
        {
            if (SamplesProcessed < long.MaxValue) SamplesProcessed++;
            if (observation is not double value || !double.IsFinite(value))
            {
                ClearRun();
                return Current;
            }
            _moments = _window.Add(value);
            return Current = IsReady ? _moments.Statistics() : default;
        }

        /// <inheritdoc />
        public void Reset() { ClearRun(); SamplesProcessed = 0; }

        internal ChartBandValue Bands(double multiplier, bool sample) => IsReady ? _moments.Bands(multiplier, sample) : default;
        private void ClearRun() { _window.Clear(); _moments = default; Current = default; }
    }
}
