// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Rolling high/low extrema and midpoint, including the current aligned high/low/close observation.</summary>
    /// <remarks>
    /// Requires Period consecutive finite bars. Any missing/non-finite channel resets warmup. Finite inverted
    /// high/low pairs fail before changing state or the counter. Two monotonic queues retain at most Period
    /// candidates each: amortized O(1) Push, O(Period) worst case for one push, O(Period) storage. No Push/Reset
    /// allocation. Queue expiry uses bounded ring slots, independently of the saturating lifetime counter.
    /// Instances are single-writer and append-only; correcting old bars requires Reset/replay.
    /// </remarks>
    public sealed class StreamingDonchianChannels : IStreamingChartIndicator<ChartHighLowClose, ChartBandValue>
    {
        private readonly ExtremumQueue _high, _low;
        private int _slot;
        /// <summary>Creates current-bar-inclusive Donchian channels with a positive period.</summary>
        public StreamingDonchianChannels(int period = 20)
        {
            if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
            Period = period;
            _high = new ExtremumQueue(period, true); _low = new ExtremumQueue(period, false);
        }
        /// <summary>Gets the finite-bar window size.</summary>
        public int Period { get; }
        /// <summary>Gets the consecutive finite bar count, bounded by Period.</summary>
        public int Count { get; private set; }
        /// <summary>Gets whether a complete finite window is present.</summary>
        public bool IsReady => Count == Period;
        /// <inheritdoc />
        public ChartBandValue Current { get; private set; }
        /// <inheritdoc />
        public long SamplesProcessed { get; private set; }
        /// <summary>Consumes one aligned bar without constructing an array.</summary>
        public ChartBandValue Push(double? high, double? low, double? close) => Push(new ChartHighLowClose(high, low, close));
        /// <inheritdoc />
        public ChartBandValue Push(ChartHighLowClose observation)
        {
            if (observation.High is double h && double.IsFinite(h) && observation.Low is double l && double.IsFinite(l) && h < l)
                throw new ArgumentException("Finite high values cannot be below low values.", nameof(observation));
            if (SamplesProcessed < long.MaxValue) SamplesProcessed++;
            if (observation.High is not double high || !double.IsFinite(high) ||
                observation.Low is not double low || !double.IsFinite(low) ||
                observation.Close is not double close || !double.IsFinite(close))
            {
                ClearRun(); return Current;
            }
            _high.Add(_slot, high); _low.Add(_slot, low);
            if (++_slot == Period) _slot = 0;
            if (Count < Period) Count++;
            if (!IsReady) return Current = default;
            double lower = _low.Value, upper = _high.Value, difference = upper - lower;
            double middle = double.IsFinite(difference) ? Math.FusedMultiplyAdd(difference, 0.5, lower) : lower / 2 + upper / 2;
            return Current = new ChartBandValue(lower, middle, upper);
        }
        /// <inheritdoc />
        public void Reset() { ClearRun(); SamplesProcessed = 0; }
        private void ClearRun() { _high.Clear(); _low.Clear(); Count = _slot = 0; Current = default; }

        private sealed class ExtremumQueue
        {
            private readonly (int Slot, double Value)[] _items;
            private readonly bool _maximum;
            private int _head, _count;
            public ExtremumQueue(int period, bool maximum) { _items = new (int, double)[period]; _maximum = maximum; }
            public double Value => _items[_head].Value;
            public void Clear() { _head = _count = 0; }
            public void Add(int slot, double value)
            {
                if (_count != 0 && _items[_head].Slot == slot)
                { if (++_head == _items.Length) _head = 0; _count--; }
                while (_count != 0)
                {
                    int last = (int)(((long)_head + _count - 1) % _items.Length);
                    if (_maximum ? _items[last].Value > value : _items[last].Value < value) break;
                    _count--;
                }
                _items[(int)(((long)_head + _count) % _items.Length)] = (slot, value);
                _count++;
            }
        }
    }
}
