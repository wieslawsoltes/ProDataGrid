// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>A bounded, thread-safe, single-series data source for live numeric charts.</summary>
    /// <remarks>
    /// Appending is O(1) per sample and does not shift retained history. AppendRange raises one
    /// invalidation, outside the lock, on the calling thread. UI hosts must marshal notifications
    /// to their UI thread. Returned snapshots own their data and remain stable after later appends.
    /// Configure a numeric or date category axis when samples do not provide category labels.
    /// </remarks>
    public sealed class StreamingChartDataSource : IChartDataSource, IChartWindowInfoProvider
    {
        private readonly object _gate = new();
        private readonly ChartSample[] _samples;
        private int _head;
        private int _count;
        private int _version;
        private long _totalSamples;
        private ChartDataSnapshot? _cachedSnapshot;
        private (int?, int?, int?, ChartDownsampleMode) _cachedRequest;

        /// <summary>Creates a fixed-capacity history. The oldest samples are replaced when full.</summary>
        public StreamingChartDataSource(int capacity, string? name = null,
            ChartSeriesKind kind = ChartSeriesKind.Line)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new ChartSample[capacity];
            Name = name;
            Kind = kind;
        }

        /// <inheritdoc />
        public event EventHandler? DataInvalidated;

        /// <summary>Gets the maximum retained sample count.</summary>
        public int Capacity => _samples.Length;

        /// <summary>Gets the retained sample count.</summary>
        public int Count { get { lock (_gate) return _count; } }

        /// <summary>Gets the number of samples appended since construction or Clear.</summary>
        public long TotalSamples { get { lock (_gate) return _totalSamples; } }

        /// <summary>Gets the series name.</summary>
        public string? Name { get; }

        /// <summary>Gets the series kind. Financial series requiring OHLC channels are not supplied by this source.</summary>
        public ChartSeriesKind Kind { get; }

        /// <summary>Appends one sample. Non-finite X coordinates are rejected; non-finite Y values become gaps.</summary>
        public void Append(ChartSample sample)
        {
            Validate(sample);
            lock (_gate)
            {
                if (_totalSamples == long.MaxValue) throw new InvalidOperationException("Sample counter exhausted.");
                AppendCore(sample);
                _totalSamples++;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Appends a batch atomically, keeping only its retained tail when larger than Capacity.
        /// An invalid X coordinate rejects the whole batch without changing history or raising an event.
        /// </summary>
        public void AppendRange(ReadOnlySpan<ChartSample> samples)
        {
            if (samples.IsEmpty) return;
            for (int i = 0; i < samples.Length; i++) Validate(samples[i]);
            lock (_gate)
            {
                if (_totalSamples > long.MaxValue - samples.Length)
                    throw new InvalidOperationException("Sample counter exhausted.");
                int start = Math.Max(0, samples.Length - Capacity);
                if (samples.Length >= Capacity) { _head = 0; _count = 0; }
                for (int i = start; i < samples.Length; i++) AppendCore(samples[i]);
                _totalSamples += samples.Length;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Clears history and the lifetime counter, releasing retained category references.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                if (_count == 0 && _totalSamples == 0) return;
                Array.Clear(_samples);
                _head = 0;
                _count = 0;
                _totalSamples = 0;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public int? GetTotalCategoryCount() => Count;

        /// <summary>
        /// Builds only the requested retained window. Repeated unchanged requests reuse the snapshot.
        /// Sampling preserves original X coordinates and gap topology; MaxPoints is a soft budget.
        /// Null or nonpositive MaxPoints disables sampling; a budget of one is promoted to two.
        /// </summary>
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var key = (request.WindowStart, request.WindowCount, request.MaxPoints, request.DownsampleMode);
            if ((uint)key.DownsampleMode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(request), "Unknown downsampling mode.");
            lock (_gate)
            {
                if (_cachedSnapshot != null && _cachedRequest == key) return _cachedSnapshot;
                int start = Math.Clamp(key.WindowStart ?? 0, 0, _count);
                int count = Math.Clamp(key.WindowCount ?? (_count - start), 0, _count - start);
                Window window = new(_samples, (int)(((long)_head + start) % Capacity), count);
                int[]? indices = key.MaxPoints is > 0 && key.MaxPoints.Value < count &&
                    key.DownsampleMode != ChartDownsampleMode.None
                    ? ChartSampleDecimator.SelectIndices(window, Math.Max(2, key.MaxPoints.Value), key.DownsampleMode)
                    : null;
                int outputCount = indices?.Length ?? count;
                double[] x = new double[outputCount];
                double?[] values = new double?[outputCount];
                string?[] categories = new string?[outputCount];
                for (int i = 0; i < outputCount; i++)
                {
                    ChartSample sample = window[indices == null ? i : indices[i]];
                    x[i] = sample.X;
                    values[i] = sample.Value;
                    categories[i] = sample.Category;
                }
                ChartSeriesSnapshot series = new(Name, Kind, Array.AsReadOnly(values), Array.AsReadOnly(x));
                _cachedSnapshot = new ChartDataSnapshot(Array.AsReadOnly(categories),
                    Array.AsReadOnly(new[] { series }), _version);
                _cachedRequest = key;
                return _cachedSnapshot;
            }
        }

        private static void Validate(ChartSample sample)
        {
            if (!double.IsFinite(sample.X))
                throw new ArgumentException("Sample X coordinates must be finite.", nameof(sample));
        }

        private void AppendCore(ChartSample sample)
        {
            if (sample.Value is double value && !double.IsFinite(value))
                sample = new ChartSample(sample.X, null, sample.Category);
            if (_count == Capacity)
            {
                _samples[_head] = sample;
                if (++_head == Capacity) _head = 0;
            }
            else
            {
                _samples[(int)(((long)_head + _count) % Capacity)] = sample;
                _count++;
            }
        }

        private void InvalidateCore()
        {
            _version = unchecked(_version + 1);
            _cachedSnapshot = null;
        }

        private sealed class Window : IReadOnlyList<ChartSample>
        {
            private readonly ChartSample[] _items;
            private readonly int _start;

            public Window(ChartSample[] items, int start, int count)
            { _items = items; _start = start; Count = count; }

            public int Count { get; }
            public ChartSample this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[(int)(((long)_start + index) % _items.Length)];
                }
            }
            public IEnumerator<ChartSample> GetEnumerator()
            { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
