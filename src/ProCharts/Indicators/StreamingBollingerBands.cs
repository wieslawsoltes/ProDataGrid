// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Persistent rolling SMA and symmetric standard-deviation bands, including the current observation.</summary>
    /// <remarks>
    /// A full consecutive finite window is required. Gaps restart warmup. Population deviation is the default;
    /// sample mode requires at least two observations. Zero multiplier produces the center on all channels.
    /// Shares the bounded two-stack rolling engine: amortized O(1) updates, occasional O(Period) transfers and
    /// O(Period) storage. Push/Reset allocate nothing. Single-writer; earlier corrections require Reset/replay.
    /// </remarks>
    public sealed class StreamingBollingerBands : IStreamingChartIndicator<double?, ChartBandValue>
    {
        private readonly StreamingRollingStatistics _statistics;
        /// <summary>Creates rolling bands with positive period and finite nonnegative deviation multiplier.</summary>
        public StreamingBollingerBands(int period = 20, double standardDeviations = 2, bool sample = false)
        {
            if (period < (sample ? 2 : 1)) throw new ArgumentOutOfRangeException(nameof(period));
            if (!double.IsFinite(standardDeviations) || standardDeviations < 0)
                throw new ArgumentOutOfRangeException(nameof(standardDeviations));
            StandardDeviations = standardDeviations; Sample = sample;
            _statistics = new StreamingRollingStatistics(period);
        }
        /// <summary>Gets the finite-window period.</summary>
        public int Period => _statistics.Period;
        /// <summary>Gets the standard-deviation multiplier.</summary>
        public double StandardDeviations { get; }
        /// <summary>Gets whether sample deviation rather than population deviation is used.</summary>
        public bool Sample { get; }
        /// <summary>Gets whether a complete finite window is present.</summary>
        public bool IsReady => _statistics.IsReady;
        /// <inheritdoc />
        public ChartBandValue Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed => _statistics.SamplesProcessed;
        /// <inheritdoc />
        public ChartBandValue Push(double? observation)
        {
            _statistics.Push(observation);
            return Current = _statistics.Bands(StandardDeviations, Sample);
        }
        /// <inheritdoc />
        public void Reset() { _statistics.Reset(); Current = default; }
    }
}
