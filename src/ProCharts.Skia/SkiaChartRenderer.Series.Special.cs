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
        private static void DrawWaterfallSeries(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style)
        {
            var categoryCount = categories.Count;
            if (categoryCount == 0)
            {
                return;
            }

            var groupWidth = plot.Width / categoryCount;
            var barWidth = groupWidth * 0.6f;
            var offset = (groupWidth - barWidth) / 2f;
            var running = 0d;

            using var increasePaint = new SKPaint
            {
                Color = style.WaterfallIncreaseColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var decreasePaint = new SKPaint
            {
                Color = style.WaterfallDecreaseColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var connectorPaint = new SKPaint
            {
                Color = style.WaterfallConnectorColor,
                StrokeWidth = style.WaterfallConnectorStrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            var count = Math.Min(categoryCount, series.Values.Count);
            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var start = running;
                var end = running + value.Value;
                var x = plot.Left + (i * groupWidth) + offset;
                var y0 = MapY(plot, start, minValue, maxValue, valueAxisKind);
                var y1 = MapY(plot, end, minValue, maxValue, valueAxisKind);
                var top = Math.Min(y0, y1);
                var bottom = Math.Max(y0, y1);
                var rect = new SKRect(x, top, x + barWidth, bottom);
                canvas.DrawRect(rect, value.Value >= 0 ? increasePaint : decreasePaint);

                if (style.ShowWaterfallConnectors && i + 1 < count)
                {
                    var nextValue = series.Values[i + 1];
                    if (nextValue.HasValue && !IsInvalidAxisValue(nextValue.Value, valueAxisKind))
                    {
                        var centerX = x + (barWidth / 2f);
                        var nextCenterX = plot.Left + ((i + 1) * groupWidth) + offset + (barWidth / 2f);
                        var connectorY = MapY(plot, end, minValue, maxValue, valueAxisKind);
                        canvas.DrawLine(centerX, connectorY, nextCenterX, connectorY, connectorPaint);
                    }
                }

                running = end;
            }
        }

        private static void DrawHistogramSeries(
            SKCanvas canvas,
            SKRect plot,
            HistogramContext histogramContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style)
        {
            if (!histogramContext.TryGetSeries(seriesIndex, out var histogramSeries))
            {
                return;
            }

            var overrides = GetSeriesStyleOverrides(style, seriesIndex);
            var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
            var strokeColor = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);
            var fillColor = ResolveSeriesFillColor(strokeColor, overrides, themeStyle);
            var gradient = ResolveSeriesGradient(overrides, themeStyle);
            var lineStyle = ResolveSeriesLineStyle(overrides, themeStyle);
            var dashPattern = ResolveSeriesDashPattern(overrides, themeStyle);
            var explicitStrokeWidth = overrides?.StrokeWidth ?? themeStyle?.StrokeWidth;
            var hasStroke = explicitStrokeWidth.HasValue ||
                            overrides?.StrokeColor.HasValue == true ||
                            themeStyle?.StrokeColor.HasValue == true;
            var strokeWidth = explicitStrokeWidth ?? (hasStroke ? style.AxisStrokeWidth : 0f);

            var binCount = histogramContext.BinCount;
            if (binCount == 0)
            {
                return;
            }

            var seriesCount = Math.Max(1, histogramContext.Series.Count);
            var groupWidth = plot.Width / binCount;
            var barWidth = groupWidth / seriesCount * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;
            var baseline = valueAxisKind == ChartAxisKind.Logarithmic
                ? minValue
                : (minValue <= 0 && maxValue >= 0 ? 0d : minValue);
            var baselineY = MapY(plot, baseline, minValue, maxValue, valueAxisKind);

            using var barPaint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var strokeEffect = hasStroke && strokeWidth > 0f
                ? CreateLineEffect(lineStyle, strokeWidth, dashPattern)
                : null;

            using var strokePaint = hasStroke && strokeWidth > 0f
                ? new SKPaint
                {
                    Color = strokeColor,
                    StrokeWidth = strokeWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    PathEffect = strokeEffect
                }
                : null;

            for (var i = 0; i < binCount; i++)
            {
                var count = histogramSeries.Counts[i];
                var x = plot.Left + (i * groupWidth) + offset + (histogramSeries.OrderIndex * barWidth);
                var y = MapY(plot, count, minValue, maxValue, valueAxisKind);
                var top = Math.Min(y, baselineY);
                var bottom = Math.Max(y, baselineY);
                var rect = new SKRect(x, top, x + barWidth, bottom);
                if (gradient != null)
                {
                    using var shader = CreateGradientShader(rect, gradient, 1f);
                    if (shader != null)
                    {
                        barPaint.Shader = shader;
                    }

                    canvas.DrawRect(rect, barPaint);
                    barPaint.Shader = null;
                }
                else
                {
                    canvas.DrawRect(rect, barPaint);
                }

                if (strokePaint != null)
                {
                    canvas.DrawRect(rect, strokePaint);
                }
            }
        }

        private static void DrawParetoSeries(
            SKCanvas canvas,
            SKRect plot,
            HistogramContext histogramContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            double minSecondaryValue,
            double maxSecondaryValue,
            ChartAxisKind secondaryAxisKind,
            SkiaChartStyle style)
        {
            if (!histogramContext.TryGetSeries(seriesIndex, out var histogramSeries))
            {
                return;
            }

            DrawHistogramSeries(canvas, plot, histogramContext, seriesIndex, minValue, maxValue, valueAxisKind, style);

            var binCount = histogramContext.BinCount;
            if (binCount == 0 || histogramSeries.CumulativePercent.Length == 0)
            {
                return;
            }

            var useSecondaryAxis = style.ShowSecondaryValueAxis;
            var lineMin = useSecondaryAxis ? minSecondaryValue : 0d;
            var lineMax = useSecondaryAxis ? maxSecondaryValue : 100d;
            var lineAxisKind = useSecondaryAxis ? secondaryAxisKind : ChartAxisKind.Value;

            var lineSeriesIndex = seriesIndex + 1;
            var lineOverrides = GetSeriesStyleOverrides(style, lineSeriesIndex);
            var lineThemeStyle = GetThemeSeriesStyle(style, lineSeriesIndex);
            var lineColor = ResolveSeriesStrokeColor(style, lineSeriesIndex, lineOverrides, lineThemeStyle);
            var lineWidth = ResolveSeriesStrokeWidth(lineOverrides, lineThemeStyle, style.SeriesStrokeWidth);
            var lineStyle = ResolveSeriesLineStyle(lineOverrides, lineThemeStyle);
            var dashPattern = ResolveSeriesDashPattern(lineOverrides, lineThemeStyle);

            using var lineEffect = CreateLineEffect(lineStyle, lineWidth, dashPattern);
            using var linePaint = new SKPaint
            {
                Color = lineColor,
                StrokeWidth = lineWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                PathEffect = lineEffect
            };

            var path = SkiaChartPools.RentPath();
            try
            {
                var hasPath = false;
                for (var i = 0; i < binCount; i++)
                {
                    var percent = histogramSeries.CumulativePercent[i];
                    var x = MapX(plot, i, binCount);
                    var y = MapY(plot, percent, lineMin, lineMax, lineAxisKind);
                    if (!hasPath)
                    {
                        path.MoveTo(x, y);
                        hasPath = true;
                    }
                    else
                    {
                        path.LineTo(x, y);
                    }
                }

                if (hasPath)
                {
                    canvas.DrawPath(path, linePaint);
                }
            }
            finally
            {
                SkiaChartPools.ReturnPath(path);
            }
        }

        private static void DrawBoxWhiskerSeries(
            SKCanvas canvas,
            SKRect plot,
            BoxWhiskerContext boxWhiskerContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style)
        {
            if (!boxWhiskerContext.TryGetSeries(seriesIndex, out var boxSeries))
            {
                return;
            }

            var overrides = GetSeriesStyleOverrides(style, seriesIndex);
            var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
            var strokeColor = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);
            var fillColor = ResolveSeriesFillColor(strokeColor, overrides, themeStyle);
            var gradient = ResolveSeriesGradient(overrides, themeStyle);
            var strokeWidth = ResolveSeriesStrokeWidth(overrides, themeStyle, style.AxisStrokeWidth);
            var lineStyle = ResolveSeriesLineStyle(overrides, themeStyle);
            var dashPattern = ResolveSeriesDashPattern(overrides, themeStyle);

            var categoryCount = boxWhiskerContext.Categories.Count;
            if (categoryCount == 0)
            {
                return;
            }

            var groupWidth = plot.Width / categoryCount;
            var boxWidth = groupWidth * 0.5f;
            var offset = (groupWidth - boxWidth) / 2f;
            var x = plot.Left + (boxSeries.OrderIndex * groupWidth) + offset;
            var centerX = x + (boxWidth / 2f);
            var stats = boxSeries.Stats;
            var yMin = MapY(plot, stats.Min, minValue, maxValue, valueAxisKind);
            var yMax = MapY(plot, stats.Max, minValue, maxValue, valueAxisKind);
            var yQ1 = MapY(plot, stats.Q1, minValue, maxValue, valueAxisKind);
            var yQ3 = MapY(plot, stats.Q3, minValue, maxValue, valueAxisKind);
            var yMedian = MapY(plot, stats.Median, minValue, maxValue, valueAxisKind);

            using var strokeEffect = CreateLineEffect(lineStyle, strokeWidth, dashPattern);
            using var strokePaint = new SKPaint
            {
                Color = strokeColor,
                StrokeWidth = strokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                PathEffect = strokeEffect
            };

            using var fillPaint = new SKPaint
            {
                Color = ApplyOpacity(fillColor, style.BoxWhiskerFillOpacity),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var boxTop = Math.Min(yQ3, yQ1);
            var boxBottom = Math.Max(yQ3, yQ1);
            var boxRect = new SKRect(x, boxTop, x + boxWidth, boxBottom);
            using var fillShader = gradient != null
                ? CreateGradientShader(boxRect, gradient, style.BoxWhiskerFillOpacity)
                : null;

            if (fillShader != null)
            {
                fillPaint.Shader = fillShader;
            }
            canvas.DrawRect(boxRect, fillPaint);
            canvas.DrawRect(boxRect, strokePaint);

            canvas.DrawLine(centerX, Math.Min(yMax, yQ3), centerX, Math.Max(yMax, yQ3), strokePaint);
            canvas.DrawLine(centerX, Math.Min(yMin, yQ1), centerX, Math.Max(yMin, yQ1), strokePaint);
            canvas.DrawLine(x, yMedian, x + boxWidth, yMedian, strokePaint);

            var capHalf = boxWidth * 0.35f;
            canvas.DrawLine(centerX - capHalf, yMax, centerX + capHalf, yMax, strokePaint);
            canvas.DrawLine(centerX - capHalf, yMin, centerX + capHalf, yMin, strokePaint);

            if (style.BoxWhiskerShowOutliers && stats.Outliers.Length > 0)
            {
                using var outlierPaint = new SKPaint
                {
                    Color = strokeColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                foreach (var outlier in stats.Outliers)
                {
                    var y = MapY(plot, outlier, minValue, maxValue, valueAxisKind);
                    canvas.DrawCircle(centerX, y, style.BoxWhiskerOutlierRadius, outlierPaint);
                }
            }
        }

        private readonly struct RadarLayout
        {
            public RadarLayout(int categoryCount, SKPoint center, float radius, double minValue, double maxValue)
            {
                CategoryCount = categoryCount;
                Center = center;
                Radius = radius;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public int CategoryCount { get; }

            public SKPoint Center { get; }

            public float Radius { get; }

            public double MinValue { get; }

            public double MaxValue { get; }
        }

        private static bool TryGetRadarLayout(
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            out RadarLayout layout)
        {
            layout = default;
            var categories = snapshot.Categories;
            var categoryCount = categories.Count;
            if (categoryCount == 0)
            {
                categoryCount = 0;
                foreach (var series in snapshot.Series)
                {
                    categoryCount = Math.Max(categoryCount, series.Values.Count);
                }
            }

            if (categoryCount == 0)
            {
                return false;
            }

            var labelMargin = style.ShowCategoryLabels ? style.LabelSize * 2f : 0f;
            var radius = (Math.Min(plot.Width, plot.Height) / 2f) - labelMargin;
            if (radius <= 0f)
            {
                return false;
            }

            var minValue = style.ValueAxisMinimum ?? 0d;
            var maxValue = style.ValueAxisMaximum ?? GetMaxSeriesValue(snapshot);
            if (Math.Abs(maxValue - minValue) < double.Epsilon)
            {
                maxValue = minValue + 1d;
            }

            layout = new RadarLayout(categoryCount, new SKPoint(plot.MidX, plot.MidY), radius, minValue, maxValue);
            return true;
        }

        private static void DrawRadarGridlines(SKCanvas canvas, SKRect plot, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            if (!style.ShowGridlines)
            {
                return;
            }

            if (!TryGetRadarLayout(plot, snapshot, style, out var layout))
            {
                return;
            }

            using var gridPaint = new SKPaint
            {
                Color = style.Gridline,
                StrokeWidth = style.GridlineStrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            var rings = Math.Max(2, style.AxisTickCount);
            for (var ring = 1; ring <= rings; ring++)
            {
                var ringRadius = layout.Radius * ring / rings;
                var ringPath = SkiaChartPools.RentPath();
                try
                {
                    for (var i = 0; i <= layout.CategoryCount; i++)
                    {
                        var index = i == layout.CategoryCount ? 0 : i;
                        var angle = GetRadarAngle(index, layout.CategoryCount);
                        var x = layout.Center.X + (float)Math.Cos(angle) * ringRadius;
                        var y = layout.Center.Y + (float)Math.Sin(angle) * ringRadius;
                        if (i == 0)
                        {
                            ringPath.MoveTo(x, y);
                        }
                        else
                        {
                            ringPath.LineTo(x, y);
                        }
                    }

                    canvas.DrawPath(ringPath, gridPaint);
                }
                finally
                {
                    SkiaChartPools.ReturnPath(ringPath);
                }
            }

            for (var i = 0; i < layout.CategoryCount; i++)
            {
                var angle = GetRadarAngle(i, layout.CategoryCount);
                var x = layout.Center.X + (float)Math.Cos(angle) * layout.Radius;
                var y = layout.Center.Y + (float)Math.Sin(angle) * layout.Radius;
                canvas.DrawLine(layout.Center.X, layout.Center.Y, x, y, gridPaint);
            }
        }

        private static void DrawRadarCategoryLabels(SKCanvas canvas, SKRect plot, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            if (!style.ShowCategoryLabels)
            {
                return;
            }

            if (!TryGetRadarLayout(plot, snapshot, style, out var layout))
            {
                return;
            }

            var categories = snapshot.Categories;
            using var textPaint = CreateTextPaint(style.Text, style.LabelSize);
            for (var i = 0; i < layout.CategoryCount; i++)
            {
                var label = GetCategory(categories, i) ?? $"Category {i + 1}";
                var bounds = new SKRect();
                textPaint.MeasureText(label, ref bounds);
                var angle = GetRadarAngle(i, layout.CategoryCount);
                var x = layout.Center.X + (float)Math.Cos(angle) * (layout.Radius + (style.LabelSize * 0.8f));
                var y = layout.Center.Y + (float)Math.Sin(angle) * (layout.Radius + (style.LabelSize * 0.8f));
                var textX = AlignCenterX(x, bounds);
                var textY = AlignCenterY(y, bounds);
                canvas.DrawText(label, textX, textY, textPaint);
            }
        }

        private static void DrawRadarDataLabels(SKCanvas canvas, SKRect plot, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            if (!style.ShowDataLabels)
            {
                return;
            }

            if (!TryGetRadarLayout(plot, snapshot, style, out var layout))
            {
                return;
            }

            var placed = SkiaChartPools.RentList<SKRect>();
            try
            {
                using var dataLabelPaint = CreateTextPaint(style.DataLabelText, style.DataLabelTextSize);
                using var dataLabelBackground = new SKPaint
                {
                    Color = style.DataLabelBackground,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
                {
                    var series = snapshot.Series[seriesIndex];
                    if (series.Kind != ChartSeriesKind.Radar)
                    {
                        continue;
                    }

                    for (var i = 0; i < layout.CategoryCount; i++)
                    {
                        var value = i < series.Values.Count ? series.Values[i] : null;
                        if (!value.HasValue || IsInvalidNumber(value.Value))
                        {
                            continue;
                        }

                        var normalized = Clamp((value.Value - layout.MinValue) / (layout.MaxValue - layout.MinValue), 0d, 1d);
                        var angle = GetRadarAngle(i, layout.CategoryCount);
                        var x = layout.Center.X + (float)Math.Cos(angle) * (float)(layout.Radius * normalized);
                        var y = layout.Center.Y + (float)Math.Sin(angle) * (float)(layout.Radius * normalized);
                        var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                        TryDrawLabelWithFallback(canvas, plot, placed, dataLabelPaint, dataLabelBackground, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
                    }
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(placed);
            }
        }

        private static void DrawRadarSeries(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            if (!TryGetRadarLayout(plot, snapshot, style, out var layout))
            {
                return;
            }

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind != ChartSeriesKind.Radar)
                {
                    continue;
                }

                var overrides = GetSeriesStyleOverrides(style, seriesIndex);
                var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
                var strokeColor = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);
                var fillColor = ResolveSeriesFillColor(strokeColor, overrides, themeStyle);
                var gradient = ResolveSeriesGradient(overrides, themeStyle);
                var strokeWidth = ResolveSeriesStrokeWidth(overrides, themeStyle, style.SeriesStrokeWidth);
                var lineStyle = ResolveSeriesLineStyle(overrides, themeStyle);
                var dashPattern = ResolveSeriesDashPattern(overrides, themeStyle);
                var markerShape = ResolveMarkerShape(overrides, themeStyle, SkiaMarkerShape.Circle);
                var markerSize = ResolveMarkerSize(overrides, themeStyle, style.RadarPointRadius);
                var markerFill = ResolveMarkerFillColor(strokeColor, overrides, themeStyle);
                var markerStroke = ResolveMarkerStrokeColor(strokeColor, overrides, themeStyle);
                var markerStrokeWidth = ResolveMarkerStrokeWidth(overrides, themeStyle);

                using var fillPaint = new SKPaint
                {
                    Color = ApplyOpacity(fillColor, style.AreaFillOpacity),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                using var fillShader = gradient != null
                    ? CreateGradientShader(plot, gradient, style.AreaFillOpacity)
                    : null;

                if (fillShader != null)
                {
                    fillPaint.Shader = fillShader;
                }

                using var lineEffect = CreateLineEffect(lineStyle, strokeWidth, dashPattern);
                using var linePaint = new SKPaint
                {
                    Color = strokeColor,
                    StrokeWidth = strokeWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    PathEffect = lineEffect
                };

                using var markerPaint = new SKPaint
                {
                    Color = markerFill,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                using var markerStrokePaint = markerStrokeWidth > 0f
                    ? new SKPaint
                    {
                        Color = markerStroke,
                        StrokeWidth = markerStrokeWidth,
                        IsAntialias = true,
                        Style = SKPaintStyle.Stroke
                    }
                    : null;

                var path = SkiaChartPools.RentPath();
                try
                {
                    var hasPath = false;
                    for (var i = 0; i < layout.CategoryCount; i++)
                    {
                        var value = i < series.Values.Count ? series.Values[i] : null;
                        var v = value.HasValue && !IsInvalidNumber(value.Value) ? value.Value : layout.MinValue;
                        var normalized = Clamp((v - layout.MinValue) / (layout.MaxValue - layout.MinValue), 0d, 1d);
                        var angle = GetRadarAngle(i, layout.CategoryCount);
                        var x = layout.Center.X + (float)Math.Cos(angle) * (float)(layout.Radius * normalized);
                        var y = layout.Center.Y + (float)Math.Sin(angle) * (float)(layout.Radius * normalized);
                        if (!hasPath)
                        {
                            path.MoveTo(x, y);
                            hasPath = true;
                        }
                        else
                        {
                            path.LineTo(x, y);
                        }
                    }

                    if (!hasPath)
                    {
                        continue;
                    }

                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                    canvas.DrawPath(path, linePaint);
                }
                finally
                {
                    SkiaChartPools.ReturnPath(path);
                }

                for (var i = 0; i < layout.CategoryCount; i++)
                {
                    var value = i < series.Values.Count ? series.Values[i] : null;
                    var v = value.HasValue && !IsInvalidNumber(value.Value) ? value.Value : layout.MinValue;
                    var normalized = Clamp((v - layout.MinValue) / (layout.MaxValue - layout.MinValue), 0d, 1d);
                    var angle = GetRadarAngle(i, layout.CategoryCount);
                    var x = layout.Center.X + (float)Math.Cos(angle) * (float)(layout.Radius * normalized);
                    var y = layout.Center.Y + (float)Math.Sin(angle) * (float)(layout.Radius * normalized);
                    DrawMarker(canvas, new SKPoint(x, y), markerSize, markerShape, markerPaint, markerStrokePaint);
                }
            }
        }

        private static void DrawFunnelSeries(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            ChartSeriesSnapshot? funnelSeries = null;
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                if (snapshot.Series[i].Kind == ChartSeriesKind.Funnel)
                {
                    funnelSeries = snapshot.Series[i];
                    break;
                }
            }

            if (funnelSeries == null)
            {
                return;
            }

            var categories = snapshot.Categories;
            var count = categories.Count > 0
                ? Math.Min(categories.Count, funnelSeries.Values.Count)
                : funnelSeries.Values.Count;
            if (count == 0)
            {
                return;
            }

            var maxValue = 0d;
            for (var i = 0; i < count; i++)
            {
                var value = funnelSeries.Values[i];
                if (!value.HasValue || IsInvalidNumber(value.Value))
                {
                    continue;
                }

                maxValue = Math.Max(maxValue, Math.Abs(value.Value));
            }

            if (maxValue <= 0d)
            {
                return;
            }

            var gap = Math.Max(0f, style.FunnelGap);
            var totalGap = gap * Math.Max(0, count - 1);
            var segmentHeight = (plot.Height - totalGap) / count;
            if (segmentHeight <= 0f)
            {
                return;
            }

            var minWidth = plot.Width * Clamp(style.FunnelMinWidthRatio, 0.05f, 0.9f);
            var centerX = plot.MidX;
            var currentTop = plot.Top;
            var previousWidth = 0f;

            for (var i = 0; i < count; i++)
            {
                var value = funnelSeries.Values[i];
                var width = minWidth;
                if (value.HasValue && !IsInvalidNumber(value.Value))
                {
                    width = (float)(Math.Abs(value.Value) / maxValue * plot.Width);
                    if (width < minWidth)
                    {
                        width = minWidth;
                    }
                }

                var topWidth = i == 0 ? width : previousWidth;
                var topLeft = centerX - (topWidth / 2f);
                var bottomLeft = centerX - (width / 2f);
                var bottomY = currentTop + segmentHeight;

                var path = SkiaChartPools.RentPath();
                try
                {
                    path.MoveTo(topLeft, currentTop);
                    path.LineTo(topLeft + topWidth, currentTop);
                    path.LineTo(bottomLeft + width, bottomY);
                    path.LineTo(bottomLeft, bottomY);
                    path.Close();

                    using var fillPaint = new SKPaint
                    {
                        Color = GetSeriesColor(style, i),
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill
                    };

                    canvas.DrawPath(path, fillPaint);
                }
                finally
                {
                    SkiaChartPools.ReturnPath(path);
                }

                currentTop = bottomY + gap;
                previousWidth = width;
            }
        }

        private static void DrawFunnelDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            if (!style.ShowDataLabels)
            {
                return;
            }

            ChartSeriesSnapshot? funnelSeries = null;
            var seriesIndex = -1;
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                if (snapshot.Series[i].Kind == ChartSeriesKind.Funnel)
                {
                    funnelSeries = snapshot.Series[i];
                    seriesIndex = i;
                    break;
                }
            }

            if (funnelSeries == null)
            {
                return;
            }

            var categories = snapshot.Categories;
            var count = categories.Count > 0
                ? Math.Min(categories.Count, funnelSeries.Values.Count)
                : funnelSeries.Values.Count;
            if (count == 0)
            {
                return;
            }

            var maxValue = 0d;
            for (var i = 0; i < count; i++)
            {
                var value = funnelSeries.Values[i];
                if (!value.HasValue || IsInvalidNumber(value.Value))
                {
                    continue;
                }

                maxValue = Math.Max(maxValue, Math.Abs(value.Value));
            }

            if (maxValue <= 0d)
            {
                return;
            }

            var gap = Math.Max(0f, style.FunnelGap);
            var totalGap = gap * Math.Max(0, count - 1);
            var segmentHeight = (plot.Height - totalGap) / count;
            if (segmentHeight <= 0f)
            {
                return;
            }

            var minWidth = plot.Width * Clamp(style.FunnelMinWidthRatio, 0.05f, 0.9f);
            var centerX = plot.MidX;
            var currentTop = plot.Top;
            var previousWidth = 0f;
            var placed = SkiaChartPools.RentList<SKRect>();
            try
            {
                using var textPaint = CreateTextPaint(style.DataLabelText, style.DataLabelTextSize);
                using var backgroundPaint = new SKPaint
                {
                    Color = style.DataLabelBackground,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                for (var i = 0; i < count; i++)
                {
                    var value = funnelSeries.Values[i];
                    var width = minWidth;
                    if (value.HasValue && !IsInvalidNumber(value.Value))
                    {
                        width = (float)(Math.Abs(value.Value) / maxValue * plot.Width);
                        if (width < minWidth)
                        {
                            width = minWidth;
                        }
                    }

                    var bottomY = currentTop + segmentHeight;

                    if (value.HasValue && !IsInvalidNumber(value.Value))
                    {
                        var text = FormatDataLabel(funnelSeries, seriesIndex, value.Value, style);
                        TryDrawCenteredLabel(canvas, plot, placed, textPaint, backgroundPaint, text, centerX, currentTop + (segmentHeight / 2f), style.DataLabelPadding);
                    }

                    currentTop = bottomY + gap;
                    previousWidth = width;
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(placed);
            }
        }

        private static void DrawPieSeries(SKCanvas canvas, SKRect plot, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            var pieSeries = CollectPieSeries(snapshot);
            try
            {
                if (pieSeries.Count == 0)
                {
                    return;
                }

                var center = new SKPoint(plot.MidX, plot.MidY);
                var radius = Math.Min(plot.Width, plot.Height) * 0.45f;
                if (radius <= 0)
                {
                    return;
                }

                var ringThickness = radius / pieSeries.Count;
                var outerRadius = radius;

                foreach (var info in pieSeries)
                {
                    var series = info.Series;
                    var values = series.Values;
                    var total = 0d;
                    foreach (var value in values)
                    {
                        if (value.HasValue && !IsInvalidNumber(value.Value))
                        {
                            total += Math.Abs(value.Value);
                        }
                    }

                    if (total <= 0)
                    {
                        outerRadius -= ringThickness;
                        continue;
                    }

                    var innerRadius = GetPieInnerRadius(series, pieSeries.Count, outerRadius, ringThickness, style);

                    var startAngle = -90f;
                    var outerRect = new SKRect(center.X - outerRadius, center.Y - outerRadius, center.X + outerRadius, center.Y + outerRadius);
                    var innerRect = new SKRect(center.X - innerRadius, center.Y - innerRadius, center.X + innerRadius, center.Y + innerRadius);

                    for (var i = 0; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (!value.HasValue || IsInvalidNumber(value.Value) || Math.Abs(value.Value) <= double.Epsilon)
                        {
                            continue;
                        }

                        var sweep = (float)(Math.Abs(value.Value) / total * 360f);
                        var color = GetSeriesColor(style, i);
                        using var paint = new SKPaint
                        {
                            Color = color,
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill
                        };

                        var path = BuildPieSlicePath(center, outerRect, innerRect, startAngle, sweep);
                        try
                        {
                            canvas.DrawPath(path, paint);
                        }
                        finally
                        {
                            SkiaChartPools.ReturnPath(path);
                        }
                        startAngle += sweep;
                    }

                    outerRadius -= ringThickness;
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(pieSeries);
            }
        }

        private static void DrawPieDataLabels(SKCanvas canvas, SKRect plot, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            if (!style.ShowDataLabels)
            {
                return;
            }

            var pieSeries = CollectPieSeries(snapshot);
            try
            {
                if (pieSeries.Count == 0)
                {
                    return;
                }

                var center = new SKPoint(plot.MidX, plot.MidY);
                var radius = Math.Min(plot.Width, plot.Height) * 0.45f;
                if (radius <= 0)
                {
                    return;
                }

                var placement = style.PieLabelPlacement;
                var allowInside = placement != SkiaPieLabelPlacement.Outside;
                var allowOutside = placement != SkiaPieLabelPlacement.Inside;

                using var textPaint = CreateTextPaint(style.DataLabelText, style.DataLabelTextSize);
                using var backgroundPaint = new SKPaint
                {
                    Color = style.DataLabelBackground,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                using var leaderPaint = new SKPaint
                {
                    Color = style.PieLabelLeaderLineColor,
                    StrokeWidth = style.PieLabelLeaderLineWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };

                var insideLabels = SkiaChartPools.RentList<PieInsideLabel>();
                var insidePlaced = SkiaChartPools.RentList<SKRect>();
                var outsideLabels = SkiaChartPools.RentList<PieOutsideLabel>();
                try
                {
                    var anyInsideFailed = false;
                    var ringThickness = radius / pieSeries.Count;
                    var outerRadius = radius;
                    var leaderRadius = radius + Math.Max(0f, style.PieLabelLeaderLineLength);
                    var leaderOffset = Math.Max(0f, style.PieLabelLeaderLineOffset);

                    foreach (var info in pieSeries)
                    {
                    var series = info.Series;
                    var values = series.Values;
                    var total = 0d;
                    foreach (var value in values)
                    {
                        if (value.HasValue && !IsInvalidNumber(value.Value))
                        {
                            total += Math.Abs(value.Value);
                        }
                    }

                    if (total <= 0)
                    {
                        outerRadius -= ringThickness;
                        continue;
                    }

                    var innerRadius = GetPieInnerRadius(series, pieSeries.Count, outerRadius, ringThickness, style);
                    var labelRadius = innerRadius + ((outerRadius - innerRadius) * 0.5f);

                    var startAngle = -90f;
                    for (var i = 0; i < values.Count; i++)
                    {
                        var value = values[i];
                        if (!value.HasValue || IsInvalidNumber(value.Value) || Math.Abs(value.Value) <= double.Epsilon)
                        {
                            continue;
                        }

                        var sweep = (float)(Math.Abs(value.Value) / total * 360f);
                        var midAngle = startAngle + (sweep / 2f);
                        var radians = DegreesToRadians(midAngle);
                        var cos = (float)Math.Cos(radians);
                        var sin = (float)Math.Sin(radians);
                        var text = FormatDataLabel(series, info.SeriesIndex, value.Value, style);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            startAngle += sweep;
                            continue;
                        }

                        if (allowInside)
                        {
                            var x = center.X + cos * labelRadius;
                            var y = center.Y + sin * labelRadius;
                            if (TryComputeCenteredLabelRect(plot, insidePlaced, textPaint, text, x, y, style.DataLabelPadding, out var rect, out var textBounds))
                            {
                                insideLabels.Add(new PieInsideLabel(text, rect, textBounds));
                            }
                            else
                            {
                                anyInsideFailed = true;
                            }
                        }
                        else
                        {
                            anyInsideFailed = true;
                        }

                        if (allowOutside)
                        {
                            var edgePoint = new SKPoint(center.X + cos * outerRadius, center.Y + sin * outerRadius);
                            var anchorY = center.Y + sin * leaderRadius;
                            outsideLabels.Add(new PieOutsideLabel(text, edgePoint, anchorY, cos >= 0f));
                        }

                        startAngle += sweep;
                    }

                    outerRadius -= ringThickness;
                }

                if (allowOutside && outsideLabels.Count > 0 && (placement == SkiaPieLabelPlacement.Outside || anyInsideFailed))
                {
                    var placed = SkiaChartPools.RentList<SKRect>();
                    try
                    {
                        DrawPieOutsideLabels(canvas, plot, center, leaderRadius, leaderOffset, style, textPaint, backgroundPaint, leaderPaint, outsideLabels, placed);
                        return;
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(placed);
                    }
                }

                if (allowInside && insideLabels.Count > 0)
                {
                    var placed = SkiaChartPools.RentList<SKRect>();
                    try
                    {
                        foreach (var label in insideLabels)
                        {
                            DrawLabelRect(canvas, textPaint, backgroundPaint, label.Text, label.Rect, label.TextBounds, style.DataLabelPadding, placed);
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(placed);
                    }
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(insideLabels);
                    SkiaChartPools.ReturnList(insidePlaced);
                    SkiaChartPools.ReturnList(outsideLabels);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(pieSeries);
            }
        }

        private sealed class PieInsideLabel
        {
            public PieInsideLabel(string text, SKRect rect, SKRect textBounds)
            {
                Text = text;
                Rect = rect;
                TextBounds = textBounds;
            }

            public string Text { get; }

            public SKRect Rect { get; }

            public SKRect TextBounds { get; }
        }

        private sealed class PieOutsideLabel
        {
            public PieOutsideLabel(string text, SKPoint edgePoint, float anchorY, bool rightSide)
            {
                Text = text;
                EdgePoint = edgePoint;
                AnchorY = anchorY;
                RightSide = rightSide;
            }

            public string Text { get; set; }

            public SKPoint EdgePoint { get; }

            public float AnchorY { get; }

            public bool RightSide { get; }

            public SKRect TextBounds { get; set; }

            public float Width { get; set; }

            public float Height { get; set; }

            public float CenterY { get; set; }
        }

        private static bool TryComputeCenteredLabelRect(
            SKRect plot,
            List<SKRect> placed,
            SKPaint textPaint,
            string text,
            float centerX,
            float centerY,
            float padding,
            out SKRect rect,
            out SKRect textBounds)
        {
            rect = default;
            textBounds = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            textPaint.MeasureText(text, ref textBounds);
            var width = textBounds.Width + (padding * 2f);
            var height = textBounds.Height + (padding * 2f);
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            rect = new SKRect(
                centerX - (width / 2f),
                centerY - (height / 2f),
                centerX + (width / 2f),
                centerY + (height / 2f));

            if (!IsInside(plot, rect) || IntersectsAny(rect, placed))
            {
                return false;
            }

            placed.Add(rect);
            return true;
        }

        private static void DrawPieOutsideLabels(
            SKCanvas canvas,
            SKRect plot,
            SKPoint center,
            float leaderRadius,
            float leaderOffset,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            SKPaint leaderPaint,
            List<PieOutsideLabel> labels,
            List<SKRect> placed)
        {
            var rightLabels = SkiaChartPools.RentList<PieOutsideLabel>();
            var leftLabels = SkiaChartPools.RentList<PieOutsideLabel>();
            try
            {
                foreach (var label in labels)
                {
                    if (label.RightSide)
                    {
                        rightLabels.Add(label);
                    }
                    else
                    {
                        leftLabels.Add(label);
                    }
                }

                var rightEdgeX = center.X + leaderRadius + leaderOffset;
                var leftEdgeX = center.X - leaderRadius - leaderOffset;

                DrawPieOutsideLabelSide(canvas, plot, center, leaderRadius, rightEdgeX, true, style, textPaint, backgroundPaint, leaderPaint, rightLabels, placed);
                DrawPieOutsideLabelSide(canvas, plot, center, leaderRadius, leftEdgeX, false, style, textPaint, backgroundPaint, leaderPaint, leftLabels, placed);
            }
            finally
            {
                SkiaChartPools.ReturnList(rightLabels);
                SkiaChartPools.ReturnList(leftLabels);
            }
        }

        private static void DrawPieOutsideLabelSide(
            SKCanvas canvas,
            SKRect plot,
            SKPoint center,
            float leaderRadius,
            float edgeX,
            bool rightSide,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            SKPaint leaderPaint,
            List<PieOutsideLabel> labels,
            List<SKRect> placed)
        {
            if (labels.Count == 0)
            {
                return;
            }

            var padding = style.DataLabelPadding;
            var spacing = Math.Max(2f, padding);
            var availableWidth = rightSide ? plot.Right - edgeX : edgeX - plot.Left;
            var maxTextWidth = availableWidth - (padding * 2f);
            if (maxTextWidth <= 1f)
            {
                return;
            }

            var active = SkiaChartPools.RentList<PieOutsideLabel>();
            try
            {
                foreach (var label in labels)
                {
                    var text = label.Text;
                    if (textPaint.MeasureText(text) > maxTextWidth)
                    {
                        text = TrimWithEllipsis(text, textPaint, maxTextWidth);
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var bounds = new SKRect();
                    textPaint.MeasureText(text, ref bounds);
                    label.Text = text;
                    label.TextBounds = bounds;
                    label.Width = bounds.Width + (padding * 2f);
                    label.Height = bounds.Height + (padding * 2f);
                    active.Add(label);
                }

                if (active.Count == 0)
                {
                    return;
                }

                active.Sort((a, b) => a.AnchorY.CompareTo(b.AnchorY));

                for (var i = 0; i < active.Count; i++)
                {
                    var label = active[i];
                    var halfHeight = label.Height / 2f;
                    var minCenter = plot.Top + halfHeight;
                    var maxCenter = plot.Bottom - halfHeight;
                    var centerY = Clamp(label.AnchorY, minCenter, maxCenter);
                    if (i > 0)
                    {
                        var prev = active[i - 1];
                        var minAllowed = prev.CenterY + (prev.Height / 2f) + halfHeight + spacing;
                        if (centerY < minAllowed)
                        {
                            centerY = minAllowed;
                        }
                    }

                    label.CenterY = centerY;
                }

                for (var i = active.Count - 1; i >= 0; i--)
                {
                    var label = active[i];
                    var halfHeight = label.Height / 2f;
                    var minCenter = plot.Top + halfHeight;
                    var maxCenter = plot.Bottom - halfHeight;
                    if (i < active.Count - 1)
                    {
                        var next = active[i + 1];
                        var maxAllowed = next.CenterY - (next.Height / 2f) - halfHeight - spacing;
                        if (maxAllowed < maxCenter)
                        {
                            maxCenter = maxAllowed;
                        }
                    }

                    if (label.CenterY > maxCenter)
                    {
                        label.CenterY = maxCenter;
                    }

                    if (label.CenterY < minCenter)
                    {
                        label.CenterY = minCenter;
                    }
                }

                foreach (var label in active)
                {
                    var rect = rightSide
                        ? new SKRect(edgeX, label.CenterY - (label.Height / 2f), edgeX + label.Width, label.CenterY + (label.Height / 2f))
                        : new SKRect(edgeX - label.Width, label.CenterY - (label.Height / 2f), edgeX, label.CenterY + (label.Height / 2f));

                    if (rightSide && rect.Right > plot.Right)
                    {
                        var shift = rect.Right - plot.Right;
                        rect.Offset(-shift, 0);
                    }
                    else if (!rightSide && rect.Left < plot.Left)
                    {
                        var shift = plot.Left - rect.Left;
                        rect.Offset(shift, 0);
                    }

                    if (IntersectsAny(rect, placed))
                    {
                        continue;
                    }

                    DrawLabelRect(canvas, textPaint, backgroundPaint, label.Text, rect, label.TextBounds, padding, placed);

                    var elbowX = center.X + (rightSide ? leaderRadius : -leaderRadius);
                    var elbow = new SKPoint(elbowX, label.CenterY);
                    canvas.DrawLine(label.EdgePoint.X, label.EdgePoint.Y, elbow.X, elbow.Y, leaderPaint);
                    var labelEdgeX = rightSide ? rect.Left : rect.Right;
                    canvas.DrawLine(elbow.X, elbow.Y, labelEdgeX, label.CenterY, leaderPaint);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(active);
            }
        }

        private static void DrawLabelRect(
            SKCanvas canvas,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            string text,
            SKRect rect,
            SKRect textBounds,
            float padding,
            List<SKRect> placed)
        {
            if (backgroundPaint.Color.Alpha > 0)
            {
                canvas.DrawRoundRect(rect, 3f, 3f, backgroundPaint);
            }

            var textX = rect.Left + padding - textBounds.Left;
            var textY = rect.Top + padding - textBounds.Top;
            canvas.DrawText(text, textX, textY, textPaint);
            placed.Add(rect);
        }

        private static SKPath BuildPieSlicePath(
            SKPoint center,
            SKRect outerRect,
            SKRect innerRect,
            float startAngle,
            float sweep)
        {
            var path = SkiaChartPools.RentPath();
            path.Reset();
            var outerRadius = outerRect.Width / 2f;
            var innerRadius = innerRect.Width / 2f;
            var startRad = DegreesToRadians(startAngle);
            var endRad = DegreesToRadians(startAngle + sweep);

            var outerStart = new SKPoint(
                center.X + (float)Math.Cos(startRad) * outerRadius,
                center.Y + (float)Math.Sin(startRad) * outerRadius);

            path.MoveTo(outerStart);
            path.ArcTo(outerRect, startAngle, sweep, false);

            if (innerRadius > 0)
            {
                var innerEnd = new SKPoint(
                    center.X + (float)Math.Cos(endRad) * innerRadius,
                    center.Y + (float)Math.Sin(endRad) * innerRadius);

                path.LineTo(innerEnd);
                path.ArcTo(innerRect, startAngle + sweep, -sweep, false);
            }
            else
            {
                path.LineTo(center);
            }

            path.Close();
            return path;
        }

        private static float DegreesToRadians(float degrees)
        {
            return (float)(degrees * Math.PI / 180d);
        }

        private static double GetRadarAngle(int index, int count)
        {
            if (count <= 0)
            {
                return -Math.PI / 2d;
            }

            return (Math.PI * 2d * index / count) - (Math.PI / 2d);
        }

        private readonly struct PieSeriesInfo
        {
            public PieSeriesInfo(int seriesIndex, ChartSeriesSnapshot series)
            {
                SeriesIndex = seriesIndex;
                Series = series;
            }

            public int SeriesIndex { get; }

            public ChartSeriesSnapshot Series { get; }
        }

        private sealed class HistogramContext
        {
            private readonly Dictionary<int, HistogramSeries> _seriesMap;

            public HistogramContext(
                IReadOnlyList<string?> categories,
                double[] binEdges,
                IReadOnlyList<HistogramSeries> series,
                Dictionary<int, HistogramSeries> seriesMap)
            {
                Categories = categories;
                BinEdges = binEdges;
                Series = series;
                _seriesMap = seriesMap;
            }

            public IReadOnlyList<string?> Categories { get; }

            public double[] BinEdges { get; }

            public IReadOnlyList<HistogramSeries> Series { get; }

            public int BinCount => Math.Max(0, BinEdges.Length - 1);

            public bool TryGetSeries(int seriesIndex, out HistogramSeries series)
            {
                return _seriesMap.TryGetValue(seriesIndex, out series!);
            }
        }

        private sealed class HistogramSeries
        {
            public HistogramSeries(
                int seriesIndex,
                int orderIndex,
                ChartSeriesSnapshot series,
                double[] counts,
                double[] cumulativePercent)
            {
                SeriesIndex = seriesIndex;
                OrderIndex = orderIndex;
                Series = series;
                Counts = counts;
                CumulativePercent = cumulativePercent;
            }

            public int SeriesIndex { get; }

            public int OrderIndex { get; }

            public ChartSeriesSnapshot Series { get; }

            public double[] Counts { get; }

            public double[] CumulativePercent { get; }
        }

        private sealed class BoxWhiskerContext
        {
            private readonly Dictionary<int, BoxWhiskerSeries> _seriesMap;

            public BoxWhiskerContext(
                IReadOnlyList<string?> categories,
                IReadOnlyList<BoxWhiskerSeries> series,
                Dictionary<int, BoxWhiskerSeries> seriesMap)
            {
                Categories = categories;
                Series = series;
                _seriesMap = seriesMap;
            }

            public IReadOnlyList<string?> Categories { get; }

            public IReadOnlyList<BoxWhiskerSeries> Series { get; }

            public bool TryGetSeries(int seriesIndex, out BoxWhiskerSeries series)
            {
                return _seriesMap.TryGetValue(seriesIndex, out series!);
            }
        }

        private sealed class BoxWhiskerSeries
        {
            public BoxWhiskerSeries(int seriesIndex, int orderIndex, ChartSeriesSnapshot series, BoxWhiskerStats stats)
            {
                SeriesIndex = seriesIndex;
                OrderIndex = orderIndex;
                Series = series;
                Stats = stats;
            }

            public int SeriesIndex { get; }

            public int OrderIndex { get; }

            public ChartSeriesSnapshot Series { get; }

            public BoxWhiskerStats Stats { get; }
        }

        private readonly struct BoxWhiskerStats
        {
            public BoxWhiskerStats(double min, double max, double q1, double median, double q3, double[] outliers)
            {
                Min = min;
                Max = max;
                Q1 = q1;
                Median = median;
                Q3 = q3;
                Outliers = outliers;
            }

            public double Min { get; }

            public double Max { get; }

            public double Q1 { get; }

            public double Median { get; }

            public double Q3 { get; }

            public double[] Outliers { get; }
        }

        private static bool TryBuildHistogramContext(ChartDataSnapshot snapshot, SkiaChartStyle style, out HistogramContext context)
        {
            context = null!;
            if (!IsHistogramOnly(snapshot))
            {
                return false;
            }

            var allValues = new List<double>();
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                foreach (var value in series.Values)
                {
                    if (!value.HasValue || IsInvalidNumber(value.Value))
                    {
                        continue;
                    }

                    allValues.Add(value.Value);
                }
            }

            if (allValues.Count == 0)
            {
                return false;
            }

            var min = allValues[0];
            var max = allValues[0];
            for (var i = 1; i < allValues.Count; i++)
            {
                var value = allValues[i];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            var binCount = style.HistogramBinCount > 0
                ? style.HistogramBinCount
                : ComputeHistogramBinCount(allValues.Count);
            if (binCount < 1)
            {
                binCount = 1;
            }

            if (Math.Abs(max - min) < double.Epsilon)
            {
                max = min + 1d;
            }

            var range = max - min;
            var binWidth = range / binCount;
            if (binWidth <= 0d || double.IsNaN(binWidth) || double.IsInfinity(binWidth))
            {
                binWidth = 1d;
            }

            var binEdges = new double[binCount + 1];
            for (var i = 0; i <= binCount; i++)
            {
                binEdges[i] = min + (binWidth * i);
            }

            binEdges[binCount] = max;

            var categories = new List<string?>(binCount);
            for (var i = 0; i < binCount; i++)
            {
                categories.Add(FormatHistogramLabel(binEdges[i], binEdges[i + 1], style));
            }

            var seriesList = new List<HistogramSeries>(snapshot.Series.Count);
            var seriesMap = new Dictionary<int, HistogramSeries>(snapshot.Series.Count);
            var orderIndex = 0;
            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                var counts = new double[binCount];
                foreach (var value in series.Values)
                {
                    if (!value.HasValue || IsInvalidNumber(value.Value))
                    {
                        continue;
                    }

                    var binIndex = GetHistogramBinIndex(value.Value, min, binWidth, binCount);
                    counts[binIndex] += 1d;
                }

                var cumulative = new double[binCount];
                var total = 0d;
                for (var i = 0; i < counts.Length; i++)
                {
                    total += counts[i];
                }

                if (total > 0d)
                {
                    var running = 0d;
                    for (var i = 0; i < counts.Length; i++)
                    {
                        running += counts[i];
                        cumulative[i] = (running / total) * 100d;
                    }
                }

                var histogramSeries = new HistogramSeries(seriesIndex, orderIndex, series, counts, cumulative);
                seriesList.Add(histogramSeries);
                seriesMap[seriesIndex] = histogramSeries;
                orderIndex++;
            }

            context = new HistogramContext(categories, binEdges, seriesList, seriesMap);
            return true;
        }

        private static int ComputeHistogramBinCount(int sampleCount)
        {
            if (sampleCount <= 1)
            {
                return 1;
            }

            var count = (int)Math.Ceiling(Math.Log(sampleCount, 2) + 1);
            return Math.Max(1, Math.Min(60, count));
        }

        private static int GetHistogramBinIndex(double value, double min, double binWidth, int binCount)
        {
            if (binCount <= 1)
            {
                return 0;
            }

            var index = (int)((value - min) / binWidth);
            if (index < 0)
            {
                return 0;
            }

            return index >= binCount ? binCount - 1 : index;
        }

        private static string FormatHistogramLabel(double start, double end, SkiaChartStyle style)
        {
            var startText = FormatAxisValue(start, style.CategoryAxisKind, style.CategoryAxisLabelFormatter, style.CategoryAxisValueFormat);
            var endText = FormatAxisValue(end, style.CategoryAxisKind, style.CategoryAxisLabelFormatter, style.CategoryAxisValueFormat);
            return $"{startText} - {endText}";
        }

        private static bool TryBuildBoxWhiskerContext(ChartDataSnapshot snapshot, ChartAxisKind axisKind, out BoxWhiskerContext context)
        {
            context = null!;
            if (!IsBoxWhiskerOnly(snapshot))
            {
                return false;
            }

            var categories = new List<string?>(snapshot.Series.Count);
            var seriesList = new List<BoxWhiskerSeries>(snapshot.Series.Count);
            var seriesMap = new Dictionary<int, BoxWhiskerSeries>(snapshot.Series.Count);
            var orderIndex = 0;

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                categories.Add(string.IsNullOrWhiteSpace(series.Name) ? $"Series {seriesIndex + 1}" : series.Name);

                var values = new List<double>();
                foreach (var value in series.Values)
                {
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                    {
                        continue;
                    }

                    values.Add(value.Value);
                }

                values.Sort();
                var stats = ComputeBoxWhiskerStats(values);
                var boxSeries = new BoxWhiskerSeries(seriesIndex, orderIndex, series, stats);
                seriesList.Add(boxSeries);
                seriesMap[seriesIndex] = boxSeries;
                orderIndex++;
            }

            context = new BoxWhiskerContext(categories, seriesList, seriesMap);
            return true;
        }

        private static BoxWhiskerStats ComputeBoxWhiskerStats(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return new BoxWhiskerStats(0d, 0d, 0d, 0d, 0d, Array.Empty<double>());
            }

            if (values.Count == 1)
            {
                var value = values[0];
                return new BoxWhiskerStats(value, value, value, value, value, Array.Empty<double>());
            }

            var median = Median(values, 0, values.Count);
            var mid = values.Count / 2;
            var lowerHalf = mid;
            var upperStart = values.Count % 2 == 0 ? mid : mid + 1;
            var upperHalf = values.Count - upperStart;
            var q1 = Median(values, 0, lowerHalf);
            var q3 = Median(values, upperStart, upperHalf);

            var iqr = q3 - q1;
            var lowerFence = q1 - (1.5 * iqr);
            var upperFence = q3 + (1.5 * iqr);
            var whiskerMin = values[0];
            var whiskerMax = values[values.Count - 1];
            var outliers = new List<double>();

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value < lowerFence || value > upperFence)
                {
                    outliers.Add(value);
                    continue;
                }

                whiskerMin = Math.Min(whiskerMin, value);
                whiskerMax = Math.Max(whiskerMax, value);
            }

            return new BoxWhiskerStats(whiskerMin, whiskerMax, q1, median, q3, outliers.ToArray());
        }

        private static double Median(IReadOnlyList<double> values, int start, int length)
        {
            if (length <= 0)
            {
                return 0d;
            }

            var mid = length / 2;
            if (length % 2 == 0)
            {
                return (values[start + mid - 1] + values[start + mid]) / 2d;
            }

            return values[start + mid];
        }

    }
}
