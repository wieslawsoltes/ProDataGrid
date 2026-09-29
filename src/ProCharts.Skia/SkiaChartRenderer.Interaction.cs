// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProCharts;
using SkiaSharp;

namespace ProCharts.Skia
{
    public sealed partial class SkiaChartRenderer
    {
        private bool _useInteractionCache = true;
        private ChartDataSnapshot? _interactionSnapshot;
        private InteractionKey _interactionKey;
        private RenderContext? _interactionContext;
        private SkiaChartPointIndex? _interactionIndex;
        private RangeAreaInteractionIndex?[]? _interactionRanges;

        /// <summary>Gets or sets whether line, area, scatter, bubble and range-area plots reuse layout and indexed queries.</summary>
        /// <remarks>
        /// Enabled by default. Snapshots and their collections must remain stable. Replace the snapshot after data
        /// changes, or call ClearInteractionCache after in-place edits. The renderer is not thread-safe.
        /// Unsupported mixed plots and plots exceeding one million source points retain the reference path.
        /// Range areas with decreasing projected X coordinates retain reference selection, with cached layout.
        /// </remarks>
        public bool UseInteractionCache
        {
            get => _useInteractionCache;
            set
            {
                if (_useInteractionCache == value) return;
                _useInteractionCache = value;
                ClearInteractionCache();
            }
        }

        /// <summary>Releases managed point/range-index and layout data, including advanced chart layouts.</summary>
        /// <remarks>Call after in-place changes to source collections or state captured by formatter delegates.</remarks>
        public void ClearInteractionCache()
        {
            _interactionSnapshot = null;
            _interactionContext = null;
            _interactionIndex = null;
            _interactionRanges = null;
            _advancedChartContext = null;
        }

        private bool CanIndexPoints(ChartDataSnapshot snapshot)
        {
            if (!_useInteractionCache || snapshot.Series.Count == 0) return false;
            long count = 0;
            for (int i = 0; i < snapshot.Series.Count; i++)
            {
                ChartSeriesSnapshot series = snapshot.Series[i];
                if (series.Kind is not (ChartSeriesKind.Line or ChartSeriesKind.Area or
                    ChartSeriesKind.Scatter or ChartSeriesKind.Bubble or ChartSeriesKind.RangeArea)) return false;
                count += series.Values.Count;
                if (count > 1_000_000) return false;
            }
            return true;
        }

        private bool TryGetRenderContext(SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle style,
            out RenderContext context)
        {
            if (!CanIndexPoints(snapshot))
            {
                _interactionSnapshot = null;
                _interactionIndex = null;
                _interactionRanges = null;
                _interactionContext = null;
                return TryBuildRenderContext(bounds, snapshot, style, out context);
            }
            InteractionKey key = new(bounds, ComputeStyleHash(style),
                style.CategoryAxisMinimum, style.CategoryAxisMaximum,
                style.SecondaryCategoryAxisMinimum, style.SecondaryCategoryAxisMaximum,
                style.ValueAxisMinimum, style.ValueAxisMaximum,
                style.SecondaryValueAxisMinimum, style.SecondaryValueAxisMaximum);
            if (ReferenceEquals(_interactionSnapshot, snapshot) && _interactionKey == key)
            {
                context = _interactionContext!;
                return context != null;
            }
            bool valid = TryBuildRenderContext(bounds, snapshot, style, out context);
            _interactionSnapshot = snapshot;
            _interactionKey = key;
            _interactionContext = valid ? context : null;
            _interactionIndex = null;
            _interactionRanges = null;
            return valid;
        }

        private bool TryHitTestIndexed(SKPoint point, SKRect bounds, ChartDataSnapshot snapshot,
            SkiaChartStyle style, out SkiaChartHitTestResult? hit)
        {
            hit = null;
            if (!CanIndexPoints(snapshot) || !float.IsFinite(style.HitTestRadius) ||
                !float.IsFinite(style.BubbleMinRadius) || !float.IsFinite(style.BubbleMaxRadius)) return false;
            if (!TryGetRenderContext(bounds, snapshot, style, out RenderContext context) ||
                !context.Plot.Contains(point)) return true;

            // The reference Cartesian loop returns the first filled interval hit, regardless of
            // any closer point candidate accumulated earlier. Keep that precedence in mixed plots.
            hit = HitTestIndexedRangeAreas(point, snapshot, style, context);
            if (hit.HasValue) return true;

            _interactionIndex ??= BuildPointIndex(snapshot, style, context);
            if (!_interactionIndex.TryFind(point, out SkiaChartIndexedPoint found)) return true;
            ChartSeriesSnapshot series = snapshot.Series[found.SeriesIndex];
            hit = new SkiaChartHitTestResult(found.SeriesIndex, found.PointIndex,
                series.Values[found.PointIndex]!.Value,
                found.HasX ? series.XValues![found.PointIndex] : null,
                GetCategory(snapshot.Categories, found.PointIndex), series.Name, series.Kind, found.Position);
            return true;
        }

        private static SkiaChartPointIndex BuildPointIndex(ChartDataSnapshot snapshot, SkiaChartStyle style,
            RenderContext context)
        {
            int capacity = 0;
            for (int s = 0; s < snapshot.Series.Count; s++)
                if (snapshot.Series[s].Kind != ChartSeriesKind.RangeArea) capacity += snapshot.Series[s].Values.Count;
            SkiaChartPointIndex index = new(context.Plot, capacity);
            for (int s = 0; s < snapshot.Series.Count; s++)
            {
                ChartSeriesSnapshot series = snapshot.Series[s];
                // A range midpoint is not a separately painted marker or a selectable point.
                if (series.Kind == ChartSeriesKind.RangeArea) continue;
                bool secondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                ChartAxisKind axis = secondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;
                double min = secondary ? context.MinSecondaryValue : context.MinValue;
                double max = secondary ? context.MaxSecondaryValue : context.MaxValue;
                int count = series.Values.Count;
                bool bubble = series.Kind == ChartSeriesKind.Bubble;
                double minX = 0, maxX = 1;
                bool hasX = (bubble || series.Kind == ChartSeriesKind.Scatter) &&
                    series.XValues?.Count == count && TryGetScatterAxisRange(series,
                        context.UseNumericCategoryAxis, context.MinCategory, context.MaxCategory,
                        context.CategoryAxisKind, out minX, out maxX);
                bool hasSizes = series.SizeValues?.Count == count;
                for (int p = 0; p < count; p++)
                {
                    if (series.Values[p] is not double value || IsInvalidAxisValue(value, axis)) continue;
                    double xValue = hasX ? series.XValues![p] : 0;
                    if (hasX && IsInvalidAxisValue(xValue, context.CategoryAxisKind)) continue;
                    float radius = Math.Abs(style.HitTestRadius);
                    if (bubble)
                    {
                        double? size = hasSizes ? series.SizeValues![p] : context.MinBubbleSize;
                        if (!size.HasValue || !double.IsFinite(size.Value) || size.Value <= 0) continue;
                        float drawnRadius = GetBubbleRadius(size.Value, context.MinBubbleSize, context.MaxBubbleSize, style);
                        if (drawnRadius <= 0 || !float.IsFinite(drawnRadius)) continue;
                        radius = Math.Max(style.HitTestRadius, drawnRadius);
                    }
                    float x = hasX ? MapValueX(context.Plot, xValue, minX, maxX, context.CategoryAxisKind)
                        : MapX(context.Plot, p, count);
                    float y = MapY(context.Plot, value, min, max, axis);
                    index.Add(new SkiaChartIndexedPoint(new SKPoint(x, y), s, p, radius * radius, hasX), radius);
                }
            }
            return index;
        }

        // Nullable endpoints are compared exactly: their ordinary hash cannot distinguish null from zero.
        private readonly record struct InteractionKey(SKRect Bounds, int StyleHash,
            double? XMin, double? XMax, double? X2Min, double? X2Max,
            double? YMin, double? YMax, double? Y2Min, double? Y2Max);
    }
}
