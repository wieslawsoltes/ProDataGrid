// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>An owned, windowable range-area source with coordinated boundary reduction.</summary>
    /// <remarks>
    /// Configure a numeric/date/log category axis: original X coordinates are retained after selection.
    /// When X is omitted, zero-based original indices become numeric X coordinates. Categories remain
    /// optional display labels. MinMax and Adaptive use paired extrema; None retains every interval.
    /// Bucket/Lttb are rejected rather than independently sampling boundaries. Notifications occur on
    /// the caller's thread outside the state lock. UI hosts must marshal worker-thread replacements.
    /// </remarks>
    public sealed class RangeChartDataSource : IChartDataSource, IChartWindowInfoProvider
    {
        private readonly object _gate = new();
        private OwnedRange _data;
        private ChartRangeView? _cached;
        private (int Start, int Count, int Budget) _cachedKey;
        private int _version;

        /// <summary>Creates a source by copying and validating aligned lower/upper/X/category channels.</summary>
        /// <remarks>
        /// Finite inverted pairs are rejected. Partial/non-finite pairs normalize to gaps.
        /// Set valueAxisKind to Logarithmic when using a logarithmic value axis: nonpositive pairs
        /// become gaps before reduction, so they cannot disappear and reconnect two visible runs.
        /// </remarks>
        public RangeChartDataSource(string? name, IReadOnlyList<double?> lower, IReadOnlyList<double?> upper,
            IReadOnlyList<double>? xValues = null, IReadOnlyList<string?>? categories = null,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary,
            ChartSeriesStyle? style = null, Func<double, string>? dataLabelFormatter = null,
            ChartAxisKind valueAxisKind = ChartAxisKind.Value)
        {
            if (valueAxisKind is not (ChartAxisKind.Value or ChartAxisKind.Logarithmic))
                throw new ArgumentOutOfRangeException(nameof(valueAxisKind), "Range value axes must be linear or logarithmic.");
            Name = name;
            ValueAxisAssignment = valueAxisAssignment;
            ValueAxisKind = valueAxisKind;
            Style = style;
            DataLabelFormatter = dataLabelFormatter;
            _data = CopyData(lower, upper, xValues, categories);
        }

        /// <inheritdoc />
        public event EventHandler? DataInvalidated;
        /// <summary>Gets the series name.</summary>
        public string? Name { get; }
        /// <summary>Gets the range's primary or secondary value-axis assignment.</summary>
        public ChartValueAxisAssignment ValueAxisAssignment { get; }
        /// <summary>Gets the input validity domain. Configure the renderer's assigned value axis to match.</summary>
        public ChartAxisKind ValueAxisKind { get; }
        /// <summary>Gets the presentation options forwarded to each display series.</summary>
        public ChartSeriesStyle? Style { get; }
        /// <summary>Gets the formatter used for the two actual interval boundaries.</summary>
        public Func<double, string>? DataLabelFormatter { get; }
        /// <summary>Gets the total input interval count, including gaps.</summary>
        public int Count { get { lock (_gate) return _data.Series.Values.Count; } }
        /// <inheritdoc />
        public int? GetTotalCategoryCount() => Count;

        /// <summary>Atomically replaces the copied input and raises one invalidation after validation succeeds.</summary>
        /// <remarks>
        /// Invalid channels leave state and prior views unchanged and raise no event. Old views and their
        /// source indices remain tied to their original snapshot version, even after a replacement.
        /// </remarks>
        public void ReplaceData(IReadOnlyList<double?> lower, IReadOnlyList<double?> upper,
            IReadOnlyList<double>? xValues = null, IReadOnlyList<string?>? categories = null)
        {
            OwnedRange replacement = CopyData(lower, upper, xValues, categories);
            lock (_gate)
            {
                _data = replacement;
                _version = unchecked(_version + 1);
                _cached = null;
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => BuildView(request).Snapshot;

        /// <summary>Builds or reuses a display view with an owned original-index map.</summary>
        /// <remarks>
        /// Windows apply before reduction. Positive point budgets are promoted to six; zero, negative
        /// or omitted budgets disable reduction. Run endpoints, both boundary extrema and gap separators
        /// take precedence over the budget. An unchanged normalized request returns the same view with
        /// no managed allocation. Changing the window scans only that window, not the full source.
        /// A reduced fill approximates omitted detail; use None for exact full-resolution geometry.
        /// </remarks>
        public ChartRangeView BuildView(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ChartDownsampleMode mode = request.DownsampleMode;
            if (mode is not (ChartDownsampleMode.None or ChartDownsampleMode.MinMax or ChartDownsampleMode.Adaptive))
                throw new ArgumentOutOfRangeException(nameof(request), "Range sources support None, MinMax and Adaptive.");
            int? requestedStart = request.WindowStart, requestedCount = request.WindowCount, requestedBudget = request.MaxPoints;
            lock (_gate)
            {
                ChartSeriesSnapshot source = _data.Series;
                int start = Math.Clamp(requestedStart ?? 0, 0, source.Values.Count);
                int count = Math.Clamp(requestedCount ?? (source.Values.Count - start), 0, source.Values.Count - start);
                int budget = mode == ChartDownsampleMode.None || requestedBudget is null or <= 0
                    ? 0 : Math.Max(6, requestedBudget.Value);
                if (budget >= count) budget = 0;
                var key = (start, count, budget);
                if (_cached != null && _cachedKey == key) return _cached;
                int[] indices;
                if (budget > 0)
                    indices = ChartRangeDecimator.SelectIndices(source.LowValues!, source.HighValues!, budget, start, count);
                else
                {
                    indices = new int[count];
                    for (int i = 0; i < count; i++) indices[i] = start + i;
                }
                int outputCount = indices.Length;
                double?[] values = new double?[outputCount], lower = new double?[outputCount], upper = new double?[outputCount];
                double[] x = new double[outputCount];
                string?[] labels = new string?[outputCount];
                for (int i = 0; i < outputCount; i++)
                {
                    int original = indices[i];
                    values[i] = source.Values[original];
                    lower[i] = source.LowValues![original]; upper[i] = source.HighValues![original];
                    x[i] = source.XValues![original];
                    if (_data.Categories.Count != 0) labels[i] = _data.Categories[original];
                }
                ChartSeriesSnapshot series = new(Name, ChartSeriesKind.RangeArea, Array.AsReadOnly(values),
                    Array.AsReadOnly(x), dataLabelFormatter: DataLabelFormatter, valueAxisAssignment: ValueAxisAssignment,
                    style: Style, lowValues: Array.AsReadOnly(lower), highValues: Array.AsReadOnly(upper));
                ChartDataSnapshot snapshot = new(Array.AsReadOnly(labels), Array.AsReadOnly(new[] { series }), _version);
                _cachedKey = key;
                return _cached = new ChartRangeView(snapshot, Array.AsReadOnly(indices), source.Values.Count, start, count);
            }
        }

        private OwnedRange CopyData(IReadOnlyList<double?> lower, IReadOnlyList<double?> upper,
            IReadOnlyList<double>? xValues, IReadOnlyList<string?>? categories)
        {
            ArgumentNullException.ThrowIfNull(lower);
            ArgumentNullException.ThrowIfNull(upper);
            if (lower.Count != upper.Count) throw new ArgumentException("Boundary counts must match.", nameof(upper));
            if (categories != null && categories.Count != lower.Count)
                throw new ArgumentException("Categories must align with every input interval.", nameof(categories));
            if (ValueAxisKind == ChartAxisKind.Logarithmic)
            {
                double?[] positiveLower = new double?[lower.Count];
                for (int i = 0; i < positiveLower.Length; i++)
                {
                    double? low = lower[i], high = upper[i];
                    // Validate before masking so logarithmic filtering cannot hide an inverted pair.
                    if (low is double l && high is double h && double.IsFinite(l) && double.IsFinite(h) && l > h)
                        throw new ArgumentException("A finite lower boundary cannot exceed its upper boundary.", nameof(lower));
                    if (low is > 0) positiveLower[i] = low;
                }
                lower = positiveLower;
            }
            if (xValues == null)
            {
                double[] positions = new double[lower.Count];
                for (int i = 0; i < positions.Length; i++) positions[i] = i;
                xValues = positions;
            }
            ChartSeriesSnapshot series = ChartRangeSeries.CreateArea(Name, lower, upper, xValues, ValueAxisAssignment, Style);
            string?[] labels = categories == null ? Array.Empty<string?>() : new string?[categories.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = categories![i];
            return new OwnedRange(series, Array.AsReadOnly(labels));
        }

        private sealed record OwnedRange(ChartSeriesSnapshot Series, IReadOnlyList<string?> Categories);
    }
}
