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
        private SkiaChartHitTestResult? HitTestIndexedRangeAreas(SKPoint point, ChartDataSnapshot snapshot,
            SkiaChartStyle style, RenderContext context)
        {
            for (int s = 0; s < snapshot.Series.Count; s++)
            {
                ChartSeriesSnapshot series = snapshot.Series[s];
                if (series.Kind != ChartSeriesKind.RangeArea) continue;
                _interactionRanges ??= BuildRangeInteractionIndices(snapshot, style, context);
                RangeAreaInteractionIndex? index = _interactionRanges[s];
                if (index != null)
                {
                    if (index.TryFind(point, Math.Max(0, style.HitTestRadius), out RangeAreaPoint found))
                        return RangeAreaHit(found, snapshot.Categories, series, s);
                }
                else
                {
                    // Raw snapshots may change X direction. Do not sort them: that would change
                    // painted runs, source-order precedence, and nearest-original-endpoint identity.
                    bool secondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    var hit = HitTestRangeArea(point, context.Plot, snapshot.Categories, series, s,
                        secondary ? context.MinSecondaryValue : context.MinValue,
                        secondary ? context.MaxSecondaryValue : context.MaxValue,
                        secondary ? style.SecondaryValueAxisKind : style.ValueAxisKind, style,
                        context.UseNumericCategoryAxis, context.CategoryAxisKind, context.MinCategory, context.MaxCategory);
                    if (hit.HasValue) return hit;
                }
            }
            return null;
        }

        private static RangeAreaInteractionIndex?[] BuildRangeInteractionIndices(ChartDataSnapshot snapshot,
            SkiaChartStyle style, RenderContext context)
        {
            RangeAreaInteractionIndex?[] indices = new RangeAreaInteractionIndex?[snapshot.Series.Count];
            for (int s = 0; s < indices.Length; s++)
            {
                ChartSeriesSnapshot series = snapshot.Series[s];
                if (series.Kind != ChartSeriesKind.RangeArea) continue;
                bool secondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                double min = secondary ? context.MinSecondaryValue : context.MinValue;
                double max = secondary ? context.MaxSecondaryValue : context.MaxValue;
                ChartAxisKind axis = secondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;
                RangeAreaPoint[] points = new RangeAreaPoint[series.Values.Count];
                int count = 0;
                bool monotonic = true;
                for (int p = 0; p < series.Values.Count; p++)
                {
                    if (!TryProjectRangeAreaPoint(context.Plot, series, p, min, max, axis,
                        context.UseNumericCategoryAxis, context.CategoryAxisKind, context.MinCategory, context.MaxCategory,
                        out RangeAreaPoint point)) continue;
                    if (count != 0 && point.X < points[count - 1].X) { monotonic = false; break; }
                    points[count++] = point;
                }
                if (monotonic) indices[s] = new RangeAreaInteractionIndex(points, count);
            }
            return indices;
        }

        // Only managed projected data is retained. One index is built per range series per context.
        // Valid points remain in source order; their original indices encode every intervening gap.
        private sealed class RangeAreaInteractionIndex
        {
            private readonly RangeAreaPoint[] _points;
            private readonly int _count;

            public RangeAreaInteractionIndex(RangeAreaPoint[] points, int count)
            { _points = points; _count = count; }

            public bool TryFind(SKPoint pointer, float radius, out RangeAreaPoint found)
            {
                found = default;
                double left = (double)pointer.X - radius, right = (double)pointer.X + radius;
                int first = 0, end = _count;
                while (first < end)
                {
                    int middle = first + (end - first) / 2;
                    if (_points[middle].X < left) first = middle + 1;
                    else end = middle;
                }
                for (int i = first; i < _count; i++)
                {
                    RangeAreaPoint current = _points[i];
                    // Check the vertical boundary before its preceding segment, exactly as the
                    // reference loop does. A wide radius intentionally favors earlier source points.
                    if (Math.Abs((double)pointer.X - current.X) <= radius &&
                        pointer.Y >= current.UpperY && pointer.Y <= current.LowerY)
                    { found = current; return true; }
                    if (i != 0)
                    {
                        RangeAreaPoint before = _points[i - 1];
                        if (current.Index == before.Index + 1 && current.X > before.X &&
                            pointer.X >= before.X && pointer.X <= current.X)
                        {
                            double fraction = ((double)pointer.X - before.X) / ((double)current.X - before.X);
                            double lowY = before.LowerY * (1 - fraction) + current.LowerY * fraction;
                            double highY = before.UpperY * (1 - fraction) + current.UpperY * fraction;
                            if (pointer.Y >= highY && pointer.Y <= lowY)
                            { found = fraction <= 0.5 ? before : current; return true; }
                        }
                    }
                    // Examine one boundary beyond the radius before stopping: it can be the
                    // right endpoint of a long segment that spans the pointer or the whole viewport.
                    if (current.X > right) break;
                }
                return false;
            }
        }
    }
}
