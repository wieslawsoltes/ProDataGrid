// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using ProCharts;
using SkiaSharp;

namespace ProCharts.Skia
{
    public sealed partial class SkiaChartRenderer
    {
        // Managed geometry only: no retained native canvas, paint, image, or path resources.
        // Like the normal render cache, reuse requires stable snapshot contents/version.
        private AdvancedChartContext? _advancedChartContext;

        private static bool IsAdvancedKind(ChartSeriesKind kind) => kind is ChartSeriesKind.Heatmap
            or ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst or ChartSeriesKind.Gauge;

        private static bool IsAdvancedChart(ChartDataSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Series.Count; i++)
                if (IsAdvancedKind(snapshot.Series[i].Kind)) return true;
            return false;
        }

        private bool TryRenderAdvanced(SKCanvas canvas, SKRect bounds, ChartDataSnapshot snapshot,
            SkiaChartStyle style, ChartDataDelta delta)
        {
            if (!IsAdvancedChart(snapshot))
            {
                _advancedChartContext = null;
                return false;
            }
            AdvancedChartContext context = GetAdvancedContext(bounds, snapshot, style);
            canvas.Save();
            try
            {
                canvas.ClipRect(bounds);
                using SKPaint paint = new() { IsAntialias = true, Color = style.Background };
                canvas.DrawRect(bounds, paint);
                if (context.Plot.Width <= 0 || context.Plot.Height <= 0) return true;
                using SKPaint text = new() { IsAntialias = true, Color = style.Text };
                using SKFont font = new(SKTypeface.Default, Math.Max(1, style.LabelSize)) { Subpixel = true };
                using SKPath path = new();
                switch (context.Kind)
                {
                    case ChartSeriesKind.Heatmap:
                        DrawAdvancedHeatmap(canvas, context, style, paint, text, font);
                        break;
                    case ChartSeriesKind.Treemap:
                        DrawAdvancedTreemap(canvas, context, style, paint, text, font);
                        break;
                    case ChartSeriesKind.Sunburst:
                        DrawAdvancedSunburst(canvas, context, style, paint, text, font, path);
                        break;
                    case ChartSeriesKind.Gauge:
                        DrawAdvancedGauges(canvas, context, style, paint, text, font, path);
                        break;
                }
                if (context.LegendRect is SKRect legend)
                    DrawLegend(canvas, legend, context.LegendSnapshot, style);
            }
            finally { canvas.Restore(); }
            return true;
        }

        private bool TryGetAdvancedViewport(SKRect bounds, ChartDataSnapshot snapshot,
            SkiaChartStyle style, out SkiaChartViewportInfo info)
        {
            AdvancedChartContext context = GetAdvancedContext(bounds, snapshot, style);
            info = new SkiaChartViewportInfo(context.Plot, false, false, context.Minimum, context.Maximum);
            return context.Plot.Width > 0 && context.Plot.Height > 0;
        }

        private AdvancedChartContext GetAdvancedContext(SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            ArgumentNullException.ThrowIfNull(style.Advanced);
            style.Advanced.Validate();
            ChartSeriesKind kind = snapshot.Series[0].Kind;
            for (int i = 0; i < snapshot.Series.Count; i++)
                if (snapshot.Series[i].Kind != kind)
                    throw new ArgumentException("Heatmap, treemap, sunburst, and gauge charts cannot mix incompatible series kinds in one plot.", nameof(snapshot));
            int hash = ComputeStyleHash(style);
            if (_advancedChartContext is { } cached && ReferenceEquals(cached.Snapshot, snapshot) &&
                cached.Version == snapshot.Version && cached.Bounds == bounds && cached.StyleHash == hash &&
                cached.Options == style.Advanced &&
                cached.GaugeMinimumOverride == style.ValueAxisMinimum && cached.GaugeMaximumOverride == style.ValueAxisMaximum)
                return cached;

            AdvancedChartContext result = new(snapshot, bounds, hash, style.Advanced, kind)
            {
                GaugeMinimumOverride = style.ValueAxisMinimum,
                GaugeMaximumOverride = style.ValueAxisMaximum
            };
            if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) ||
                !float.IsFinite(bounds.Bottom) || bounds.Width <= 0 || bounds.Height <= 0 ||
                !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
                return _advancedChartContext = result;
            result.LegendSnapshot = snapshot;
  if (kind is ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst)
  {
      result.Hierarchy = snapshot.Hierarchy ?? ChartHierarchySnapshot.FromChartData(snapshot);
      ChartHierarchyNode root = result.Hierarchy.Root;
      int count = root.IsLeaf ? 1 : root.Children.Count;
      ChartSeriesSnapshot[] entries = new ChartSeriesSnapshot[count];
      for (int i = 0; i < count; i++)
      {
          ChartHierarchyNode node = root.IsLeaf ? root : root.Children[i];
          entries[i] = new ChartSeriesSnapshot(node.Label ?? node.Id, kind, Array.Empty<double?>());
      }
      result.LegendSnapshot = new ChartDataSnapshot(Array.Empty<string?>(), Array.AsReadOnly(entries));
  }
  // Heatmaps use the continuous color scale, not unrelated per-row series colors.
  SkiaChartStyle layoutStyle = kind == ChartSeriesKind.Heatmap ? new SkiaChartStyle(style) { ShowLegend = false } : style;
  result.Plot = CalculatePlotRect(bounds, result.LegendSnapshot, snapshot.Categories, layoutStyle,
                false, false, false, 0, 1, 0, 1, false, 0, 1, out SKRect? legend);
            result.LegendRect = legend;
            if (result.Plot.Width <= 0 || result.Plot.Height <= 0) return _advancedChartContext = result;

            if (kind == ChartSeriesKind.Heatmap)
            {
                result.Rows = snapshot.Series.Count;
                result.Columns = snapshot.Categories.Count;
                double min = double.PositiveInfinity, max = double.NegativeInfinity;
                for (int s = 0; s < snapshot.Series.Count; s++)
                {
                    IReadOnlyList<double?> values = snapshot.Series[s].Values;
                    result.Columns = Math.Max(result.Columns, values.Count);
                    for (int p = 0; p < values.Count; p++)
                        if (values[p] is double value && double.IsFinite(value))
                        { min = Math.Min(min, value); max = Math.Max(max, value); }
                }
                result.Minimum = style.Advanced.HeatmapMinimum ?? (double.IsFinite(min) ? min : 0);
                result.Maximum = style.Advanced.HeatmapMaximum ?? (double.IsFinite(max) ? max : result.Minimum);
                if (result.Minimum > result.Maximum)
                    throw new ArgumentException("The resolved heatmap minimum cannot exceed its maximum.", nameof(style));
                double center = style.Advanced.HeatmapCenterValue ?? (result.Minimum / 2 + result.Maximum / 2);
                if (style.Advanced.HeatmapCenterColor.HasValue && (center < result.Minimum || center > result.Maximum))
                    throw new ArgumentException("The diverging center must lie within the heatmap interval.", nameof(style));
                result.OuterPlot = result.Plot;
                float labelSize = Math.Max(1, style.LabelSize);
                float bottom = (style.ShowCategoryLabels ? labelSize + 8 : 0) +
                    (style.Advanced.ShowHeatmapColorScale ? 2 * labelSize + 14 : 0);
                float left = style.ShowAxisLabels ? Math.Min(110, result.Plot.Width * 0.22f) : 0;
                if (result.Plot.Height > bottom + 12)
                    result.Plot = new SKRect(result.Plot.Left + left, result.Plot.Top,
                        result.Plot.Right, result.Plot.Bottom - bottom);
                else result.Plot = SKRect.Empty;
            }
            else if (kind is ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst)
            {
                if (kind == ChartSeriesKind.Treemap)
                    result.Treemap = ChartHierarchyLayout.CreateTreemap(result.Hierarchy, result.Plot.Width,
                        result.Plot.Height, style.Advanced.TreemapGap, style.Advanced.TreemapHeaderHeight);
                else
                    result.Sunburst = ChartHierarchyLayout.CreateSunburst(result.Hierarchy, style.Advanced.SunburstInnerRadius);
            }
            else if (kind == ChartSeriesKind.Gauge)
            {
                result.Minimum = style.ValueAxisMinimum ?? 0;
                result.Maximum = style.ValueAxisMaximum ?? 100;
                if (!double.IsFinite(result.Minimum) || !double.IsFinite(result.Maximum) || result.Maximum <= result.Minimum)
                    throw new ArgumentException("Gauge bounds must be finite and strictly increasing.", nameof(style));
                for (int s = 0; s < snapshot.Series.Count; s++)
                    for (int p = 0; p < snapshot.Series[s].Values.Count; p++)
                        result.Gauges.Add((s, p));
                if (result.Gauges.Count != 0)
                {
                    double desired = Math.Sqrt(result.Gauges.Count * (double)result.Plot.Width / result.Plot.Height);
                    result.Columns = (int)Math.Clamp(Math.Ceiling(desired), 1, result.Gauges.Count);
                    result.Rows = (int)(((long)result.Gauges.Count + result.Columns - 1) / result.Columns);
                }
            }
            return _advancedChartContext = result;
        }

        private static void DrawAdvancedHeatmap(SKCanvas canvas, AdvancedChartContext context,
            SkiaChartStyle style, SKPaint paint, SKPaint text, SKFont font)
        {
            if (context.Rows == 0 || context.Columns == 0) return;
            float cellWidth = context.Plot.Width / context.Columns;
            float cellHeight = context.Plot.Height / context.Rows;
            paint.IsAntialias = false;
            for (int row = 0; row < context.Rows; row++)
            {
                ChartSeriesSnapshot series = context.Snapshot.Series[row];
                for (int column = 0; column < series.Values.Count; column++)
                {
                    if (series.Values[column] is not double value || !double.IsFinite(value)) continue;
                    SKRect rect = AdvancedHeatmapCell(context, row, column);
                    paint.Color = AdvancedHeatColor(value, context);
                    canvas.DrawRect(rect, paint);
                    if (style.ShowDataLabels && rect.Width >= 28 && rect.Height >= font.Size + 4)
                        DrawAdvancedLabel(canvas, rect, FormatDataLabel(series, row, value, style), text, font, true);
                }
            }
            paint.IsAntialias = true;
            int rowStride = Math.Max(1, (int)Math.Ceiling((font.Size + 6) / cellHeight));
            if (style.ShowAxisLabels)
                for (int row = 0; row < context.Rows; row += rowStride)
                    DrawAdvancedLabel(canvas, new SKRect(context.OuterPlot.Left,
                        context.Plot.Top + row * cellHeight, context.Plot.Left - 4,
                        context.Plot.Top + (row + 1) * cellHeight),
                        context.Snapshot.Series[row].Name ?? (row + 1).ToString(CultureInfo.InvariantCulture), text, font, false);
            int columnStride = Math.Max(1, (int)Math.Ceiling(64 / cellWidth));
            if (style.ShowCategoryLabels)
                for (int column = 0; column < context.Columns; column += columnStride)
                {
                    float x = context.Plot.Left + column * cellWidth;
                    string label = column < context.Snapshot.Categories.Count
                        ? context.Snapshot.Categories[column] ?? string.Empty : (column + 1).ToString(CultureInfo.InvariantCulture);
                    DrawAdvancedLabel(canvas, new SKRect(x, context.Plot.Bottom + 2,
                        Math.Min(context.Plot.Right, x + cellWidth * columnStride), context.Plot.Bottom + font.Size + 8), label, text, font, true);
                }
            if (context.Options.ShowHeatmapColorScale)
            {
                float top = context.Plot.Bottom + (style.ShowCategoryLabels ? font.Size + 10 : 4);
                float height = 8;
                const int steps = 128;
                paint.IsAntialias = false;
                for (int i = 0; i < steps; i++)
                {
                    double t = i / (steps - 1d);
                    double value = context.Minimum * (1 - t) + context.Maximum * t;
                    paint.Color = AdvancedHeatColor(value, context);
                    canvas.DrawRect(new SKRect(context.Plot.Left + context.Plot.Width * i / steps, top,
                        context.Plot.Left + context.Plot.Width * (i + 1) / steps, top + height), paint);
                }
                DrawAdvancedLabel(canvas, new SKRect(context.Plot.Left, top + height,
                    context.Plot.MidX, top + height + font.Size + 6), context.Minimum.ToString("G5", CultureInfo.InvariantCulture), text, font, false);
                string maximumLabel = context.Maximum.ToString("G5", CultureInfo.InvariantCulture);
                float maximumWidth = font.MeasureText(maximumLabel, text) + 6;
                DrawAdvancedLabel(canvas, new SKRect(Math.Max(context.Plot.MidX, context.Plot.Right - maximumWidth), top + height,
                    context.Plot.Right, top + height + font.Size + 6), maximumLabel, text, font, false);
                paint.IsAntialias = true;
            }
        }

        private static SKRect AdvancedHeatmapCell(AdvancedChartContext context, int row, int column)
        {
            float width = context.Plot.Width / context.Columns, height = context.Plot.Height / context.Rows;
            float inset = Math.Min(context.Options.HeatmapCellGap / 2, Math.Min(width, height) * 0.45f);
            return new SKRect(context.Plot.Left + column * width + inset, context.Plot.Top + row * height + inset,
                context.Plot.Left + (column + 1) * width - inset, context.Plot.Top + (row + 1) * height - inset);
        }

        private static SKColor AdvancedHeatColor(double value, AdvancedChartContext context)
        {
            SkiaAdvancedChartStyle options = context.Options;
            if (options.HeatmapCenterColor is SKColor centerColor)
            {
                double center = options.HeatmapCenterValue ?? (context.Minimum / 2 + context.Maximum / 2);
                return value <= center
                    ? AdvancedLerpColor(options.HeatmapLowColor, centerColor, AdvancedFraction(value, context.Minimum, center))
                    : AdvancedLerpColor(centerColor, options.HeatmapHighColor, AdvancedFraction(value, center, context.Maximum));
            }
            return AdvancedLerpColor(options.HeatmapLowColor, options.HeatmapHighColor,
                AdvancedFraction(value, context.Minimum, context.Maximum));
        }

        private static double AdvancedFraction(double value, double minimum, double maximum)
        {
            if (minimum == maximum) return 0.5;
            if (value <= minimum) return 0;
            if (value >= maximum) return 1;
            double scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            return Math.Clamp((value / scale - minimum / scale) / (maximum / scale - minimum / scale), 0, 1);
        }

        private static SKColor AdvancedLerpColor(SKColor a, SKColor b, double t) => new(
            (byte)Math.Round(a.Red + (b.Red - a.Red) * t), (byte)Math.Round(a.Green + (b.Green - a.Green) * t),
            (byte)Math.Round(a.Blue + (b.Blue - a.Blue) * t), (byte)Math.Round(a.Alpha + (b.Alpha - a.Alpha) * t));

        private static void DrawAdvancedTreemap(SKCanvas canvas, AdvancedChartContext context,
            SkiaChartStyle style, SKPaint paint, SKPaint text, SKFont font)
        {
            ChartHierarchySnapshot hierarchy = context.Hierarchy!;
            foreach (ChartTreemapCell cell in context.Treemap)
            {
                ChartHierarchyNode node = hierarchy.Nodes[cell.NodeIndex];
                SKRect rect = AdvancedTreemapRect(cell.Bounds, context.Plot);
                paint.Color = AdvancedHierarchyColor(hierarchy, cell.NodeIndex, style);
                canvas.DrawRect(rect, paint);
                if (!node.IsLeaf)
                    rect.Bottom = Math.Min(rect.Bottom, rect.Top + context.Options.TreemapHeaderHeight);
                if (style.ShowCategoryLabels)
                {
          SKRect labelRect = rect;
          labelRect.Bottom = Math.Min(rect.Bottom, rect.Top + font.Size + 10);
          DrawAdvancedLabel(canvas, labelRect, node.Label ?? node.Id, text, font, false);
      }
                if (node.IsLeaf && style.ShowDataLabels && rect.Height > font.Size * 3)
                {
                    rect.Top += style.ShowCategoryLabels ? font.Size + 10 : 0;
                    rect.Bottom = Math.Min(rect.Bottom, rect.Top + font.Size + 10);
                    DrawAdvancedLabel(canvas, rect, node.TotalValue.ToString("G5", CultureInfo.InvariantCulture), text, font, false);
                }
            }
        }

        private static SKRect AdvancedTreemapRect(ChartLayoutRect rect, SKRect plot) => new(
            plot.Left + (float)rect.Left, plot.Top + (float)rect.Top,
            plot.Left + (float)rect.Right, plot.Top + (float)rect.Bottom);

        private static SKColor AdvancedHierarchyColor(ChartHierarchySnapshot hierarchy, int node, SkiaChartStyle style)
        {
            SKColor basis = GetSeriesColor(style, hierarchy.BranchIndices[node]);
            double lightness = Math.Min(0.42, Math.Max(0, hierarchy.Depths[node] - 1) * 0.10);
            return AdvancedLerpColor(basis, SKColors.White.WithAlpha(basis.Alpha), lightness);
        }

        private static void DrawAdvancedSunburst(SKCanvas canvas, AdvancedChartContext context,
            SkiaChartStyle style, SKPaint paint, SKPaint text, SKFont font, SKPath path)
        {
            ChartHierarchySnapshot hierarchy = context.Hierarchy!;
            SKPoint center = new(context.Plot.MidX, context.Plot.MidY);
            float radius = Math.Min(context.Plot.Width, context.Plot.Height) / 2;
            foreach (ChartSunburstSector sector in context.Sunburst)
            {
                CreateAdvancedSector(path, center, radius * (float)sector.InnerRadius, radius * (float)sector.OuterRadius,
                    (float)sector.StartAngle, (float)sector.SweepAngle);
                paint.Color = AdvancedHierarchyColor(hierarchy, sector.NodeIndex, style);
                canvas.DrawPath(path, paint);
                double labelRadius = (sector.InnerRadius + sector.OuterRadius) * radius / 2;
                double length = sector.SweepAngle * Math.PI / 180 * labelRadius;
                double thickness = (sector.OuterRadius - sector.InnerRadius) * radius;
                if (style.ShowCategoryLabels && length > 28 && thickness > font.Size + 6)
                {
                    double radians = (sector.StartAngle + sector.SweepAngle / 2) * Math.PI / 180;
                    float x = center.X + (float)(Math.Cos(radians) * labelRadius);
                    float y = center.Y + (float)(Math.Sin(radians) * labelRadius);
                    float width = (float)Math.Min(length * 0.75, 100);
                    canvas.Save();
                    try
                    {
                        canvas.ClipPath(path, SKClipOperation.Intersect, true);
                        DrawAdvancedLabel(canvas, new SKRect(x - width / 2, y - font.Size,
                            x + width / 2, y + font.Size), hierarchy.Categories[sector.NodeIndex], text, font, true);
                    }
                    finally { canvas.Restore(); }
                }
            }
        }

        private static void DrawAdvancedGauges(SKCanvas canvas, AdvancedChartContext context,
            SkiaChartStyle style, SKPaint paint, SKPaint text, SKFont font, SKPath path)
        {
            for (int i = 0; i < context.Gauges.Count; i++)
            {
                var item = context.Gauges[i];
                ChartSeriesSnapshot series = context.Snapshot.Series[item.Series];
                if (series.Values[item.Point] is not double value || !double.IsFinite(value)) continue;
                SKRect rect = AdvancedGaugeRect(context, i);
                SKPoint center = new(rect.MidX, rect.MidY);
                float radius = Math.Max(0, Math.Min(rect.Width, rect.Height) / 2 - 4);
                if (radius <= 0) continue;
                float inner = radius * (1 - context.Options.GaugeThickness);
                CreateAdvancedSector(path, center, inner, radius, context.Options.GaugeStartAngle, context.Options.GaugeSweepAngle);
                paint.Color = style.Axis.WithAlpha(36);
                canvas.DrawPath(path, paint);
                float sweep = (float)(context.Options.GaugeSweepAngle * AdvancedFraction(value, context.Minimum, context.Maximum));
                if (sweep > 0)
                {
                    CreateAdvancedSector(path, center, inner, radius, context.Options.GaugeStartAngle, sweep);
                    paint.Color = GetSeriesColor(style, item.Series);
                    canvas.DrawPath(path, paint);
                }
                float labelWidth = inner * 1.55f;
                if (style.ShowDataLabels)
                    DrawAdvancedLabel(canvas, new SKRect(center.X - labelWidth / 2, center.Y - font.Size - 3,
                        center.X + labelWidth / 2, center.Y + font.Size + 3),
                        FormatDataLabel(series, item.Series, value, style), text, font, true);
                if (style.ShowCategoryLabels)
                {
                    string? category = item.Point < context.Snapshot.Categories.Count ? context.Snapshot.Categories[item.Point] : null;
                    DrawAdvancedLabel(canvas, new SKRect(center.X - labelWidth / 2, center.Y + font.Size + 5,
                        center.X + labelWidth / 2, center.Y + font.Size * 3 + 5), category ?? series.Name, text, font, true);
                }
            }
        }

        private static SKRect AdvancedGaugeRect(AdvancedChartContext context, int index)
        {
            float width = context.Plot.Width / context.Columns, height = context.Plot.Height / context.Rows;
            int row = index / context.Columns, column = index % context.Columns;
            return new SKRect(context.Plot.Left + column * width, context.Plot.Top + row * height,
                context.Plot.Left + (column + 1) * width, context.Plot.Top + (row + 1) * height);
        }

        private static void CreateAdvancedSector(SKPath path, SKPoint center, float inner, float outer, float start, float sweep)
        {
            path.Reset();
            path.FillType = SKPathFillType.EvenOdd;
            if (sweep >= 359.99999f)
            {
                path.AddCircle(center.X, center.Y, outer);
                if (inner > 0) path.AddCircle(center.X, center.Y, inner);
                return;
            }
            path.ArcTo(new SKRect(center.X - outer, center.Y - outer, center.X + outer, center.Y + outer), start, sweep, true);
            double angle = (start + sweep) * Math.PI / 180;
            path.LineTo(center.X + inner * (float)Math.Cos(angle), center.Y + inner * (float)Math.Sin(angle));
            if (inner > 0)
                path.ArcTo(new SKRect(center.X - inner, center.Y - inner, center.X + inner, center.Y + inner), start + sweep, -sweep, false);
            else path.LineTo(center);
            path.Close();
        }

        private static void DrawAdvancedLabel(SKCanvas canvas, SKRect bounds, string? label, SKPaint text, SKFont font, bool centered)
        {
            if (string.IsNullOrEmpty(label) || bounds.Width < 8 || bounds.Height < font.Size + 2) return;
            // Reject labels that do not fit; clipping alone would display misleading partial numbers.
            float width = font.MeasureText(label, text);
            if (width > bounds.Width - 6) return;
            float x = centered ? bounds.MidX - width / 2 : bounds.Left + 3;
            float y = bounds.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2;
            canvas.Save();
            try { canvas.ClipRect(bounds); canvas.DrawText(label, x, y, SKTextAlign.Left, font, text); }
            finally { canvas.Restore(); }
        }

        private SkiaChartHitTestResult? HitTestAdvanced(SKPoint point, SKRect bounds,
            ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            AdvancedChartContext context = GetAdvancedContext(bounds, snapshot, style);
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || context.Plot.Width <= 0 ||
                context.Plot.Height <= 0 || !context.Plot.Contains(point)) return null;
            if (context.Kind == ChartSeriesKind.Heatmap)
            {
                if (context.Columns == 0 || context.Rows == 0) return null;
                int column = (int)((point.X - context.Plot.Left) / context.Plot.Width * context.Columns);
                int row = (int)((point.Y - context.Plot.Top) / context.Plot.Height * context.Rows);
                if ((uint)row >= (uint)context.Rows || (uint)column >= (uint)snapshot.Series[row].Values.Count ||
                    !AdvancedHeatmapCell(context, row, column).Contains(point)) return null;
                return AdvancedValueHit(snapshot, row, column, point);
            }
            if (context.Kind == ChartSeriesKind.Gauge)
            {
                if (context.Columns == 0 || context.Rows == 0) return null;
                int column = (int)((point.X - context.Plot.Left) / context.Plot.Width * context.Columns);
                int row = (int)((point.Y - context.Plot.Top) / context.Plot.Height * context.Rows);
                long index = (long)row * context.Columns + column;
                if (index < 0 || index >= context.Gauges.Count) return null;
                SKRect rect = AdvancedGaugeRect(context, (int)index);
                double radius = Math.Min(rect.Width, rect.Height) / 2 - 4;
                if (radius <= 0) return null;
                double x = point.X - rect.MidX, y = point.Y - rect.MidY;
                double distance = Math.Sqrt(x * x + y * y);
                double angle = ((Math.Atan2(y, x) * 180 / Math.PI - context.Options.GaugeStartAngle) % 360 + 360) % 360;
                if (distance < radius * (1 - context.Options.GaugeThickness) || distance > radius || angle >= context.Options.GaugeSweepAngle)
                    return null;
                var item = context.Gauges[(int)index];
                return AdvancedValueHit(snapshot, item.Series, item.Point, point);
            }
            int node;
            if (context.Kind == ChartSeriesKind.Treemap)
                node = ChartHierarchyLayout.HitTestTreemap(context.Treemap, point.X - context.Plot.Left, point.Y - context.Plot.Top);
            else
            {
                double radius = Math.Min(context.Plot.Width, context.Plot.Height) / 2;
                node = ChartHierarchyLayout.HitTestSunburst(context.Sunburst,
                    (point.X - context.Plot.MidX) / radius, (point.Y - context.Plot.MidY) / radius);
            }
            if (node < 0) return null;
            ChartHierarchySnapshot hierarchy = context.Hierarchy!;
            int seriesIndex = snapshot.Hierarchy != null ? 0 : hierarchy.SourceSeriesIndices[node];
            int pointIndex = snapshot.Hierarchy != null ? node : hierarchy.SourcePointIndices[node];
            return new SkiaChartHitTestResult(seriesIndex, pointIndex, hierarchy.Nodes[node].TotalValue, null,
                hierarchy.Categories[node], snapshot.Series[seriesIndex].Name, context.Kind, point);
        }

        private static SkiaChartHitTestResult? AdvancedValueHit(ChartDataSnapshot snapshot, int seriesIndex, int pointIndex, SKPoint point)
        {
            ChartSeriesSnapshot series = snapshot.Series[seriesIndex];
            if (series.Values[pointIndex] is not double value || !double.IsFinite(value)) return null;
            double? x = series.XValues != null && pointIndex < series.XValues.Count ? series.XValues[pointIndex] : null;
            string? category = pointIndex < snapshot.Categories.Count ? snapshot.Categories[pointIndex] : null;
            return new SkiaChartHitTestResult(seriesIndex, pointIndex, value, x, category, series.Name, series.Kind, point);
        }

        private sealed class AdvancedChartContext
        {
            public AdvancedChartContext(ChartDataSnapshot snapshot, SKRect bounds, int hash, SkiaAdvancedChartStyle options, ChartSeriesKind kind)
            { Snapshot = snapshot; Version = snapshot.Version; Bounds = bounds; StyleHash = hash; Options = options; Kind = kind; }
            public ChartDataSnapshot Snapshot { get; }
            public int Version { get; }
            public SKRect Bounds { get; }
            public int StyleHash { get; }
            public SkiaAdvancedChartStyle Options { get; }
            public ChartSeriesKind Kind { get; }
            public SKRect Plot { get; set; }
            public SKRect OuterPlot { get; set; }
            public SKRect? LegendRect { get; set; }
            public double? GaugeMinimumOverride { get; init; }
            public double? GaugeMaximumOverride { get; init; }
            public ChartDataSnapshot LegendSnapshot { get; set; } = ChartDataSnapshot.Empty;
            public double Minimum { get; set; }
            public double Maximum { get; set; } = 1;
            public int Rows { get; set; }
            public int Columns { get; set; }
            public ChartHierarchySnapshot? Hierarchy { get; set; }
            public IReadOnlyList<ChartTreemapCell> Treemap { get; set; } = Array.Empty<ChartTreemapCell>();
            public IReadOnlyList<ChartSunburstSector> Sunburst { get; set; } = Array.Empty<ChartSunburstSector>();
            public List<(int Series, int Point)> Gauges { get; } = new();
        }
    }
}
