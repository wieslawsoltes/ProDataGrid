// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Rolling SMA with complete-window warmup and gap restart.</summary>
    /// <remarks>Composes StreamingRollingStatistics: bounded O(Period) storage, amortized O(1) updates with transfer bursts, no Push/Reset allocation. Single-writer.</remarks>
    public sealed class StreamingSimpleMovingAverage : IStreamingChartIndicator<double?, double?>
    {
        private readonly StreamingRollingStatistics _statistics;
        /// <summary>Creates an SMA with a positive window period.</summary>
        public StreamingSimpleMovingAverage(int period = 20) { _statistics = new StreamingRollingStatistics(period); }
        /// <summary>Gets the window period.</summary>
        public int Period => _statistics.Period;
        /// <summary>Gets whether a complete finite window is present.</summary>
        public bool IsReady => _statistics.IsReady;
        /// <inheritdoc />
        public double? Current => _statistics.Current.Mean;
        /// <inheritdoc />
        public long SamplesProcessed => _statistics.SamplesProcessed;
        /// <inheritdoc />
        public double? Push(double? observation) => _statistics.Push(observation).Mean;
        /// <inheritdoc />
        public void Reset() => _statistics.Reset();
    }

    /// <summary>Rolling linearly weighted mean with weights one through Period, oldest to newest.</summary>
    /// <remarks>Composes StreamingRollingStatistics; identical warmup/gap, storage, transfer-burst and single-writer contracts. No Push/Reset allocation.</remarks>
    public sealed class StreamingWeightedMovingAverage : IStreamingChartIndicator<double?, double?>
    {
        private readonly StreamingRollingStatistics _statistics;
        /// <summary>Creates a weighted mean with a positive period.</summary>
        public StreamingWeightedMovingAverage(int period = 20) { _statistics = new StreamingRollingStatistics(period); }
        /// <summary>Gets the window period.</summary>
        public int Period => _statistics.Period;
        /// <summary>Gets whether a complete finite window is present.</summary>
        public bool IsReady => _statistics.IsReady;
        /// <inheritdoc />
        public double? Current => _statistics.Current.WeightedMean;
        /// <inheritdoc />
        public long SamplesProcessed => _statistics.SamplesProcessed;
        /// <inheritdoc />
        public double? Push(double? observation) => _statistics.Push(observation).WeightedMean;
        /// <inheritdoc />
        public void Reset() => _statistics.Reset();
    }

    /// <summary>Rolling population or sample standard deviation with gap restart and complete-window warmup.</summary>
    /// <remarks>Composes StreamingRollingStatistics. Sample periods must be at least two. Results outside binary64 become null. Single-writer.</remarks>
    public sealed class StreamingRollingStandardDeviation : IStreamingChartIndicator<double?, double?>
    {
        private readonly StreamingRollingStatistics _statistics;
        /// <summary>Creates a rolling deviation; sample mode uses divisor Period minus one.</summary>
        public StreamingRollingStandardDeviation(int period = 20, bool sample = false)
        {
            if (period < (sample ? 2 : 1)) throw new ArgumentOutOfRangeException(nameof(period));
            Sample = sample;
            _statistics = new StreamingRollingStatistics(period);
        }
        /// <summary>Gets the window period.</summary>
        public int Period => _statistics.Period;
        /// <summary>Gets whether sample correction is applied.</summary>
        public bool Sample { get; }
        /// <summary>Gets whether a complete finite window is present.</summary>
        public bool IsReady => _statistics.IsReady;
        /// <inheritdoc />
        public double? Current => Sample ? _statistics.Current.SampleStandardDeviation : _statistics.Current.PopulationStandardDeviation;
        /// <inheritdoc />
        public long SamplesProcessed => _statistics.SamplesProcessed;
        /// <inheritdoc />
        public double? Push(double? observation) { _statistics.Push(observation); return Current; }
        /// <inheritdoc />
        public void Reset() => _statistics.Reset();
    }
}
