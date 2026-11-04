// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>A bounded, thread-safe range-area stream with atomic batches and coordinated boundary reduction.</summary>
    /// <remarks>
    /// Append is O(1); AppendRange validates O(input count) and writes at most Capacity observations.
    /// Successful batches raise one notification outside the lock on the caller's thread. UI hosts must
    /// marshal notifications appropriately. Use numeric/date/log X positioning, not category-index spacing.
    /// Snapshot preparation holds the state lock while scanning its requested window. Inputs and requests
    /// must remain stable during a call. This is append-only; corrections require Clear and replay.
    /// </remarks>
    public sealed class StreamingRangeChartDataSource : IChartDataSource, IChartWindowInfoProvider
    {
        private readonly object _gate = new();
        private readonly ChartRangeSample[] _samples;
        private readonly BoundaryWindow _lower;
        private readonly BoundaryWindow _upper;
        private int _head, _count, _version;
        private long _totalSamples;
        private StreamingRangeChartView? _cached;
        private (int Start, int Count, int Budget) _cachedKey;

        /// <summary>Creates fixed-capacity history, validating the linear/logarithmic value domain and axis assignment.</summary>
        /// <remarks>Capacity must be at least two. Configure the renderer's assigned value axis to match ValueAxisKind.</remarks>
        public StreamingRangeChartDataSource(int capacity, string? name = null,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary,
            ChartSeriesStyle? style = null, Func<double, string>? dataLabelFormatter = null,
            ChartAxisKind valueAxisKind = ChartAxisKind.Value)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (valueAxisKind is not (ChartAxisKind.Value or ChartAxisKind.Logarithmic))
                throw new ArgumentOutOfRangeException(nameof(valueAxisKind));
            if (valueAxisAssignment is not (ChartValueAxisAssignment.Primary or ChartValueAxisAssignment.Secondary))
                throw new ArgumentOutOfRangeException(nameof(valueAxisAssignment));
            _samples = new ChartRangeSample[capacity];
            _lower = new BoundaryWindow(_samples, false);
            _upper = new BoundaryWindow(_samples, true);
            Name = name; ValueAxisAssignment = valueAxisAssignment; ValueAxisKind = valueAxisKind;
            Style = style; DataLabelFormatter = dataLabelFormatter;
        }

        /// <inheritdoc />
        public event EventHandler? DataInvalidated;
        /// <summary>Gets the fixed maximum retained observation count.</summary>
        public int Capacity => _samples.Length;
        /// <summary>Gets the retained observation count, including gaps.</summary>
        public int Count { get { lock (_gate) return _count; } }
        /// <summary>Gets observations appended since construction or Clear, including evicted observations.</summary>
        public long TotalSamples { get { lock (_gate) return _totalSamples; } }
        /// <summary>Gets the first retained observation's zero-based stream index.</summary>
        public long FirstRetainedSampleIndex { get { lock (_gate) return _totalSamples - _count; } }
        /// <summary>Gets the optional series name.</summary>
        public string? Name { get; }
        /// <summary>Gets the primary/secondary value-axis assignment forwarded to snapshots.</summary>
        public ChartValueAxisAssignment ValueAxisAssignment { get; }
        /// <summary>Gets the input validity domain, applied before decimation.</summary>
        public ChartAxisKind ValueAxisKind { get; }
        /// <summary>Gets the shared presentation style forwarded to snapshots.</summary>
        public ChartSeriesStyle? Style { get; }
        /// <summary>Gets the shared boundary-label formatter.</summary>
        public Func<double, string>? DataLabelFormatter { get; }
        /// <inheritdoc />
        public int? GetTotalCategoryCount() => Count;

        /// <summary>Appends one observation, rejecting non-increasing/non-finite X or finite inverted bounds atomically.</summary>
        public void Append(ChartRangeSample sample)
        {
            Validate(sample);
            lock (_gate)
            {
                ValidateStart(sample.X);
                if (_totalSamples == long.MaxValue) throw new InvalidOperationException("Sample counter exhausted.");
                AppendCore(sample);
                _totalSamples++;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Validates a complete batch before committing; oversized batches retain only their chronological tail.</summary>
        /// <remarks>
        /// Even discarded observations are validated and counted. Invalid input leaves state, versions and events
        /// unchanged. Non-finite/incomplete bounds become gaps; logarithmic nonpositive pairs become gaps before
        /// reduction. X must increase across gaps, batch boundaries and retained-history eviction. Empty input is a no-op.
        /// </remarks>
        public void AppendRange(ReadOnlySpan<ChartRangeSample> samples)
        {
            if (samples.IsEmpty) return;
            for (int i = 0; i < samples.Length; i++)
            {
                Validate(samples[i]);
                if (i != 0 && samples[i].X <= samples[i - 1].X)
                    throw new ArgumentException("Stream X coordinates must be strictly increasing.", nameof(samples));
            }
            lock (_gate)
            {
                ValidateStart(samples[0].X);
                if (_totalSamples > long.MaxValue - samples.Length) throw new InvalidOperationException("Sample counter exhausted.");
                int start = Math.Max(0, samples.Length - Capacity);
                if (samples.Length >= Capacity) { _head = 0; _count = 0; }
                for (int i = start; i < samples.Length; i++) AppendCore(samples[i]);
                _totalSamples += samples.Length;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears retained references and resets observation numbering and X ordering for a new session.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                if (_totalSamples == 0) return;
                Array.Clear(_samples);
                _head = _count = 0; _totalSamples = 0;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => BuildView(request).Snapshot;

        /// <summary>Builds or reuses an owned display view, preserving both boundary indices and missing terminal X coordinates.</summary>
        /// <remarks>
        /// Windows index currently retained history, not lifetime sample numbers. None keeps every interval;
        /// MinMax/Adaptive share paired extrema. Positive budgets promote to six; gaps/endpoints can exceed that
        /// soft budget. Bucket/Lttb are rejected. Cached normalized requests allocate nothing. Reduction approximates
        /// omitted detail; it does not certify an envelope enclosing all omitted observations.
        /// </remarks>
        public StreamingRangeChartView BuildView(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ChartDownsampleMode mode = request.DownsampleMode;
            if (mode is not (ChartDownsampleMode.None or ChartDownsampleMode.MinMax or ChartDownsampleMode.Adaptive))
                throw new ArgumentOutOfRangeException(nameof(request), "Range streams support None, MinMax and Adaptive.");
            int? requestedStart = request.WindowStart, requestedCount = request.WindowCount, requestedBudget = request.MaxPoints;
            lock (_gate)
            {
                int start = Math.Clamp(requestedStart ?? 0, 0, _count);
                int count = Math.Clamp(requestedCount ?? (_count - start), 0, _count - start);
                int budget = mode == ChartDownsampleMode.None || requestedBudget is null or <= 0 ? 0 : Math.Max(6, requestedBudget.Value);
                if (budget >= count) budget = 0;
                var key = (start, count, budget);
                if (_cached != null && _cachedKey == key) return _cached;
                int[]? selected = null;
                if (budget != 0)
                {
                    _lower.SetWindow(RingIndex(start), count); _upper.SetWindow(RingIndex(start), count);
                    selected = ChartRangeDecimator.SelectIndices(_lower, _upper, budget);
                    if (selected.Length != 0 && selected[^1] != count - 1)
                    {
                        Array.Resize(ref selected, selected.Length + 1);
                        selected[^1] = count - 1;
                    }
                }
                int outputCount = selected?.Length ?? count;
                double?[] values = new double?[outputCount], low = new double?[outputCount], high = new double?[outputCount];
                double[] x = new double[outputCount];
                string?[] labels = new string?[outputCount];
                long[] identities = new long[outputCount];
                long firstRetained = _totalSamples - _count;
                for (int i = 0; i < outputCount; i++)
                {
                    int position = start + (selected == null ? i : selected[i]);
                    ChartRangeSample sample = _samples[RingIndex(position)];
                    low[i] = sample.Lower; high[i] = sample.Upper; x[i] = sample.X; labels[i] = sample.Category;
                    identities[i] = firstRetained + position;
                    if (sample.Lower is double l && sample.Upper is double h)
                    {
                        double difference = h - l;
                        values[i] = double.IsFinite(difference) ? Math.FusedMultiplyAdd(difference, 0.5, l) : l / 2 + h / 2;
                    }
                }
                ChartSeriesSnapshot series = new(Name, ChartSeriesKind.RangeArea, Array.AsReadOnly(values), Array.AsReadOnly(x),
                    dataLabelFormatter: DataLabelFormatter, valueAxisAssignment: ValueAxisAssignment, style: Style,
                    lowValues: Array.AsReadOnly(low), highValues: Array.AsReadOnly(high));
                ChartDataSnapshot snapshot = new(Array.AsReadOnly(labels), Array.AsReadOnly(new[] { series }), _version);
                _cachedKey = key;
                return _cached = new StreamingRangeChartView(snapshot, Array.AsReadOnly(identities), firstRetained, _count, start, count);
            }
        }

        private static void Validate(ChartRangeSample sample)
        {
            if (!double.IsFinite(sample.X)) throw new ArgumentException("Stream X coordinates must be finite.", nameof(sample));
            if (sample.Lower is double l && sample.Upper is double h && double.IsFinite(l) && double.IsFinite(h) && l > h)
                throw new ArgumentException("A finite lower bound cannot exceed its upper bound.", nameof(sample));
        }

        private void ValidateStart(double x)
        {
            if (_count != 0 && x <= _samples[RingIndex(_count - 1)].X)
                throw new ArgumentException("Stream X must increase beyond the last appended observation.", nameof(x));
        }

        private int RingIndex(int logical) => (int)(((long)_head + logical) % Capacity);

        private void AppendCore(ChartRangeSample sample)
        {
            if (sample.Lower is not double l || sample.Upper is not double h || !double.IsFinite(l) || !double.IsFinite(h) ||
                (ValueAxisKind == ChartAxisKind.Logarithmic && l <= 0))
                sample = new ChartRangeSample(sample.X, null, null, sample.Category);
            if (_count == Capacity)
            {
                _samples[_head] = sample;
                if (++_head == Capacity) _head = 0;
            }
            else { _samples[RingIndex(_count)] = sample; _count++; }
        }

        private void InvalidateCore() { _version = unchecked(_version + 1); _cached = null; }

        // Reused only while holding the source lock; these wrappers never escape to a returned view.
        private sealed class BoundaryWindow : IReadOnlyList<double?>
        {
            private readonly ChartRangeSample[] _items;
            private readonly bool _isUpper;
            private int _start;
            public BoundaryWindow(ChartRangeSample[] items, bool upper) { _items = items; _isUpper = upper; }
            public void SetWindow(int start, int count) { _start = start; Count = count; }
            public int Count { get; private set; }
            public double? this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    ChartRangeSample sample = _items[(int)(((long)_start + index) % _items.Length)];
                    return _isUpper ? sample.Upper : sample.Lower;
                }
            }
            public IEnumerator<double?> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
