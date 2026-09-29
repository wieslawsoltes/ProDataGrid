// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>A bounded scalar-series stream that commits all values at an X coordinate as one atomic row.</summary>
    /// <remarks>
    /// Series order is fixed. X is shared, finite and strictly increasing. Per-series missing values remain independent.
    /// Storage is O(Capacity * SeriesCount), with shared X/categories and O(Capacity) selection scratch. Append is O(series).
    /// BuildView scans under the state lock; callers must keep supplied spans, definitions and requests stable during a call.
    /// Events run outside the lock on the successful caller's thread. Use CoalescingChartDataSource for UI delivery.
    /// This is append-only, not a timestamp join, interpolation engine, indicator transaction or late-data correction store.
    /// </remarks>
    public sealed class StreamingMultiSeriesChartDataSource : IChartDataSource, IChartWindowInfoProvider
    {
        private readonly object _gate = new();
        private readonly StreamingChartSeries[] _definitions;
        private readonly double[] _x;
        private readonly string?[] _categories;
        private readonly double?[][] _values;
        private readonly bool[] _selected;
        private readonly SeriesWindow _window;
        private int _head, _count, _version;
        private long _totalSamples;
        private StreamingMultiSeriesChartView? _cached;
        private (int Start, int Count, int Budget, ChartDownsampleMode Mode) _cachedKey;

        /// <summary>Copies a nonempty series schema and allocates fixed-capacity history; capacity must be at least two.</summary>
        public StreamingMultiSeriesChartDataSource(int capacity, IReadOnlyList<StreamingChartSeries> series)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            ArgumentNullException.ThrowIfNull(series);
            if (series.Count == 0) throw new ArgumentException("At least one series is required.", nameof(series));
            _definitions = new StreamingChartSeries[series.Count];
            for (int i = 0; i < _definitions.Length; i++)
                _definitions[i] = series[i] ?? throw new ArgumentException("Series definitions cannot contain null.", nameof(series));
            Series = Array.AsReadOnly(_definitions);
            _x = new double[capacity]; _categories = new string?[capacity]; _selected = new bool[capacity];
            _values = new double?[_definitions.Length][];
            for (int i = 0; i < _values.Length; i++) _values[i] = new double?[capacity];
            _window = new SeriesWindow(this);
        }

        /// <inheritdoc />
        public event EventHandler? DataInvalidated;
        /// <summary>Gets the immutable ordered schema. Numerical values in Append use this order.</summary>
        public IReadOnlyList<StreamingChartSeries> Series { get; }
        /// <summary>Gets the fixed number of series in every row.</summary>
        public int SeriesCount => _definitions.Length;
        /// <summary>Gets the maximum retained row count.</summary>
        public int Capacity => _x.Length;
        /// <summary>Gets retained rows, including rows where all series are missing.</summary>
        public int Count { get { lock (_gate) return _count; } }
        /// <summary>Gets accepted rows since construction or Clear, including evicted rows.</summary>
        public long TotalSamples { get { lock (_gate) return _totalSamples; } }
        /// <summary>Gets the zero-based first retained row number.</summary>
        public long FirstRetainedSampleIndex { get { lock (_gate) return _totalSamples - _count; } }
        /// <inheritdoc />
        public int? GetTotalCategoryCount() => Count;

        /// <summary>Commits exactly one value per series at a shared X coordinate, then raises one invalidation.</summary>
        /// <remarks>
        /// Missing/nonfinite values become gaps only in their series; nonpositive logarithmic values also become gaps.
        /// Invalid shape/X/order rejects the row without changing state. Subscriber exceptions occur after commit.
        /// </remarks>
        public void Append(double x, ReadOnlySpan<double?> values, string? category = null)
        {
            if (values.Length != SeriesCount) throw new ArgumentException("Supply exactly one value per series.", nameof(values));
            ValidateX(x);
            lock (_gate)
            {
                ValidateStart(x);
                if (_totalSamples == long.MaxValue) throw new InvalidOperationException("Sample counter exhausted.");
                AppendCore(x, values, category);
                _totalSamples++;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Atomically appends rows from X coordinates and flattened row-major values, raising one invalidation.</summary>
        /// <remarks>
        /// rowMajorValues[row * SeriesCount + series] identifies each cell. Categories are empty or exactly one per row.
        /// All X coordinates are validated, including discarded prefixes of oversized batches. Only the last Capacity
        /// rows need copying. Invalid shape/X/order preserves rows, counters, cached views and events. Empty valid input
        /// is a no-op. Sources accept concurrent callers but do not reorder competing producers; serialize feed ordering.
        /// </remarks>
        public void AppendRange(ReadOnlySpan<double> xValues, ReadOnlySpan<double?> rowMajorValues,
            ReadOnlySpan<string?> categories = default)
        {
            if ((long)xValues.Length * SeriesCount != rowMajorValues.Length)
                throw new ArgumentException("The value count must equal row count times series count.", nameof(rowMajorValues));
            if (!categories.IsEmpty && categories.Length != xValues.Length)
                throw new ArgumentException("Supply one category per row or no categories.", nameof(categories));
            if (xValues.IsEmpty) return;
            for (int i = 0; i < xValues.Length; i++)
            {
                ValidateX(xValues[i]);
                if (i != 0 && xValues[i] <= xValues[i - 1])
                    throw new ArgumentException("Stream X must be strictly increasing.", nameof(xValues));
            }
            lock (_gate)
            {
                ValidateStart(xValues[0]);
                if (_totalSamples > long.MaxValue - xValues.Length) throw new InvalidOperationException("Sample counter exhausted.");
                int start = Math.Max(0, xValues.Length - Capacity);
                if (xValues.Length >= Capacity) { _head = 0; _count = 0; }
                for (int row = start; row < xValues.Length; row++)
                    AppendCore(xValues[row], rowMajorValues.Slice(row * SeriesCount, SeriesCount), categories.IsEmpty ? null : categories[row]);
                _totalSamples += xValues.Length;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears all retained cells/references and starts a new numbering and X-ordering session.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                if (_totalSamples == 0) return;
                Array.Clear(_x); Array.Clear(_categories);
                for (int s = 0; s < SeriesCount; s++) Array.Clear(_values[s]);
                _head = _count = 0; _totalSamples = 0;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => BuildView(request).Snapshot;

        /// <summary>Captures aligned owned values and a common original-row identity map.</summary>
        /// <remarks>
        /// MaxPoints is a PER-SERIES soft selection budget, not a final common-point cap. The union of each series'
        /// original indices preserves its selected extrema, run endpoints and gaps; every series is copied at that union.
        /// First/last window rows are always retained, including all-missing terminal rows. No values are averaged or
        /// interpolated. More series/disjoint gaps can make the union approach the full window. None disables selection;
        /// null/nonpositive budgets disable it; one promotes to two. Adaptive normalizes to MinMax. Cache-equivalent
        /// requests reuse the view without allocation. Snapshot scanning/selection is O(window rows * series count).
        /// Unreduced windows use at most two contiguous span copies per ring channel, including wrapped windows.
        /// </remarks>
        public StreamingMultiSeriesChartView BuildView(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ChartDownsampleMode mode = request.DownsampleMode;
            if ((uint)mode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(request), "Unknown downsampling mode.");
            int? requestedStart = request.WindowStart, requestedCount = request.WindowCount, requestedBudget = request.MaxPoints;
            lock (_gate)
            {
                int start = Math.Clamp(requestedStart ?? 0, 0, _count);
                int count = Math.Clamp(requestedCount ?? (_count - start), 0, _count - start);
                int budget = mode == ChartDownsampleMode.None || requestedBudget is null or <= 0 ? 0 : Math.Max(2, requestedBudget.Value);
                if (budget >= count) budget = 0;
                if (budget == 0) mode = ChartDownsampleMode.None;
                else if (mode == ChartDownsampleMode.Adaptive) mode = ChartDownsampleMode.MinMax;
                var key = (start, count, budget, mode);
                if (_cached != null && _cachedKey == key) return _cached;

                int outputCount = count;
                if (budget != 0)
                {
                    Array.Clear(_selected, 0, count);
                    _selected[0] = _selected[count - 1] = true;
                    for (int s = 0; s < SeriesCount; s++)
                    {
                        _window.Set(s, start, count);
                        int[] indices = ChartSampleDecimator.SelectIndices(_window, budget, mode);
                        for (int i = 0; i < indices.Length; i++) _selected[indices[i]] = true;
                    }
                    outputCount = 0;
                    for (int i = 0; i < count; i++) if (_selected[i]) outputCount++;
                }

                double[] x = new double[outputCount];
                string?[] labels = new string?[outputCount];
                long[] identities = new long[outputCount];
                double?[][] values = new double?[SeriesCount][];
                for (int s = 0; s < SeriesCount; s++) values[s] = new double?[outputCount];
                long firstRetained = _totalSamples - _count;
                if (budget == 0)
                {
                    CopyWindow(_x, start, x);
                    CopyWindow(_categories, start, labels);
                    for (int s = 0; s < SeriesCount; s++) CopyWindow(_values[s], start, values[s]);
                    for (int i = 0; i < count; i++) identities[i] = firstRetained + start + i;
                }
                else
                {
                    int output = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (!_selected[i]) continue;
                        int ring = RingIndex(start + i);
                        x[output] = _x[ring]; labels[output] = _categories[ring]; identities[output] = firstRetained + start + i;
                        for (int s = 0; s < SeriesCount; s++) values[s][output] = _values[s][ring];
                        output++;
                    }
                }
                IReadOnlyList<double> sharedX = Array.AsReadOnly(x);
                ChartSeriesSnapshot[] series = new ChartSeriesSnapshot[SeriesCount];
                for (int s = 0; s < SeriesCount; s++)
                {
                    StreamingChartSeries definition = _definitions[s];
                    series[s] = new ChartSeriesSnapshot(definition.Name, definition.Kind, Array.AsReadOnly(values[s]), sharedX,
                        dataLabelFormatter: definition.DataLabelFormatter, valueAxisAssignment: definition.ValueAxisAssignment,
                        style: definition.Style);
                }
                ChartDataSnapshot snapshot = new(Array.AsReadOnly(labels), Array.AsReadOnly(series), _version);
                _cachedKey = key;
                return _cached = new StreamingMultiSeriesChartView(snapshot, Array.AsReadOnly(identities), firstRetained, _count, start, count);
            }
        }

        // The caller holds _gate, and normalized windows never exceed the retained ring suffix.
        private void CopyWindow<T>(T[] ring, int logicalStart, T[] destination)
        {
            if (destination.Length == 0) return;
            int start = RingIndex(logicalStart);
            int tail = Math.Min(destination.Length, Capacity - start);
            ring.AsSpan(start, tail).CopyTo(destination);
            ring.AsSpan(0, destination.Length - tail).CopyTo(destination.AsSpan(tail));
        }

        private static void ValidateX(double x)
        {
            if (!double.IsFinite(x)) throw new ArgumentException("Stream X coordinates must be finite.", nameof(x));
        }

        private void ValidateStart(double x)
        {
            if (_count != 0 && x <= _x[RingIndex(_count - 1)])
                throw new ArgumentException("Stream X must increase beyond the last accepted row.", nameof(x));
        }

        private int RingIndex(int logical) => (int)(((long)_head + logical) % Capacity);

        private void AppendCore(double x, ReadOnlySpan<double?> values, string? category)
        {
            int index = _count == Capacity ? _head : RingIndex(_count);
            _x[index] = x; _categories[index] = category;
            for (int s = 0; s < SeriesCount; s++)
            {
                double? value = values[s];
                _values[s][index] = value is double v && double.IsFinite(v) &&
                    (_definitions[s].ValueAxisKind != ChartAxisKind.Logarithmic || v > 0) ? value : null;
            }
            if (_count == Capacity) { if (++_head == Capacity) _head = 0; }
            else _count++;
        }

        private void InvalidateCore() { _version = unchecked(_version + 1); _cached = null; }

        // Mutable wrappers and selection scratch are used only under _gate and never escape into snapshots.
        private sealed class SeriesWindow : IReadOnlyList<ChartSample>
        {
            private readonly StreamingMultiSeriesChartDataSource _owner;
            private int _series, _start;
            public SeriesWindow(StreamingMultiSeriesChartDataSource owner) => _owner = owner;
            public void Set(int series, int start, int count) { _series = series; _start = start; Count = count; }
            public int Count { get; private set; }
            public ChartSample this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    int ring = _owner.RingIndex(_start + index);
                    return new ChartSample(_owner._x[ring], _owner._values[_series][ring]);
                }
            }
            public IEnumerator<ChartSample> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
