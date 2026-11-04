// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Constant-space MACD with SMA-seeded fast/slow EMAs, EMA signal and aligned histogram.</summary>
    /// <remarks>
    /// Conventions match ChartIndicators.MovingAverageConvergenceDivergence. Null/non-finite prices
    /// restart every channel's warmup. Differences are calculated before reconstructing the original
    /// price scale, retaining small oscillations on large price offsets. A genuinely unrepresentable
    /// result is null without discarding the finite internal recurrence. Instances are single-writer.
    /// </remarks>
    public sealed class StreamingMacd : IStreamingChartIndicator<double?, ChartMacdValue>
    {
        private StreamingIndicatorScale _scale;
        private StreamingIndicatorAverage _fast;
        private StreamingIndicatorAverage _slow;
        private StreamingIndicatorAverage _signal;
        private readonly double _fastAlpha;
        private readonly double _slowAlpha;
        private readonly double _signalAlpha;

        /// <summary>Creates positive-period MACD state; fastPeriod must be smaller than slowPeriod.</summary>
        public StreamingMacd(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            if (fastPeriod < 1) throw new ArgumentOutOfRangeException(nameof(fastPeriod));
            if (slowPeriod < 1) throw new ArgumentOutOfRangeException(nameof(slowPeriod));
            if (signalPeriod < 1) throw new ArgumentOutOfRangeException(nameof(signalPeriod));
            if (fastPeriod >= slowPeriod) throw new ArgumentException("The fast period must be smaller than the slow period.", nameof(fastPeriod));
            FastPeriod = fastPeriod;
            SlowPeriod = slowPeriod;
            SignalPeriod = signalPeriod;
            _fastAlpha = 2d / (fastPeriod + 1d);
            _slowAlpha = 2d / (slowPeriod + 1d);
            _signalAlpha = 2d / (signalPeriod + 1d);
        }

        /// <summary>Gets the fast EMA period.</summary>
        public int FastPeriod { get; }
        /// <summary>Gets the slow EMA period.</summary>
        public int SlowPeriod { get; }
        /// <summary>Gets the signal EMA period.</summary>
        public int SignalPeriod { get; }
        /// <inheritdoc />
        public ChartMacdValue Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed { get; private set; }

        /// <inheritdoc />
        public ChartMacdValue Push(double? observation)
        {
            if (SamplesProcessed < long.MaxValue) SamplesProcessed++;
            if (observation is not double value || !double.IsFinite(value))
            {
                ClearRun();
                return Current;
            }
            if (_scale.Include(value, out double factor, out double shift))
            {
                _fast.Transform(factor, shift);
                _slow.Transform(factor, shift);
                _signal.Transform(factor);
            }
            double normalized = _scale.Encode(value);
            _fast.Add(normalized, FastPeriod, _fastAlpha);
            if (_slow.Add(normalized, SlowPeriod, _slowAlpha))
            {
                double line = _fast.Mean - _slow.Mean;
                bool ready = _signal.Add(line, SignalPeriod, _signalAlpha);
                Current = new ChartMacdValue(_scale.DecodeDifference(line),
                    ready ? _scale.DecodeDifference(_signal.Mean) : null,
                    ready ? _scale.DecodeDifference(line - _signal.Mean) : null);
            }
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
            _fast = _slow = _signal = default;
            Current = default;
        }
    }
}
