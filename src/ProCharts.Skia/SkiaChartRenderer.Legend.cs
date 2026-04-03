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
        private static string FitLegendText(string text, SKPaint textPaint, float maxWidth)
        {
            if (maxWidth <= 0f)
            {
                return string.Empty;
            }

            var bounds = new SKRect();
            textPaint.MeasureText(text, ref bounds);
            if (bounds.Width <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            textPaint.MeasureText(ellipsis, ref bounds);
            if (bounds.Width > maxWidth)
            {
                return string.Empty;
            }

            var lo = 0;
            var hi = text.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                var candidate = text.Substring(0, mid) + ellipsis;
                textPaint.MeasureText(candidate, ref bounds);
                if (bounds.Width <= maxWidth)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo == 0 ? ellipsis : text.Substring(0, lo) + ellipsis;
        }

        private static string GetLegendDisplayName(
            string name,
            SKPaint textPaint,
            SkiaChartStyle style,
            float availableWidth,
            out SKRect bounds)
        {
            string displayName;
            if (availableWidth > 0f)
            {
                var maxTextWidth = Math.Max(0f, availableWidth - style.LegendSwatchSize - style.LegendSpacing);
                displayName = FitLegendText(name, textPaint, maxTextWidth);
            }
            else
            {
                displayName = name;
            }

            bounds = new SKRect();
            textPaint.MeasureText(displayName, ref bounds);
            return displayName;
        }

        private static void DrawLegend(
            SKCanvas canvas,
            SKRect rect,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            if (snapshot.Series.Count == 0)
            {
                return;
            }

            if (UsesCategoryLegend(snapshot))
            {
                var entries = BuildPieLegendEntries(snapshot, style);
                try
                {
                    if (entries.Count == 0)
                    {
                        return;
                    }

                    using var pieTextPaint = CreateTextPaint(style.Text, style.LegendTextSize);
                    using var pieSwatchPaint = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill
                    };

                    DrawLegendEntries(canvas, rect, entries, style, pieTextPaint, pieSwatchPaint, rect.Left, rect.Top);
                    return;
                }
                finally
                {
                    SkiaChartPools.ReturnList(entries);
                }
            }

            using var textPaint = CreateTextPaint(style.Text, style.LegendTextSize);
            using var headerPaint = CreateTextPaint(style.Text, style.LegendGroupHeaderTextSize, bold: true);

            var allIndices = BuildLegendIndices(snapshot);
            try
            {
                if (!style.LegendGroupStackedSeries)
                {
                    DrawLegendItems(canvas, rect, snapshot, allIndices, style, textPaint, rect.Left, rect.Top);
                    return;
                }

                BuildLegendGroups(snapshot, out var standardIndices, out var stackedIndices);
                try
                {
                    if (standardIndices.Count == 0 || stackedIndices.Count == 0)
                    {
                        DrawLegendItems(canvas, rect, snapshot, allIndices, style, textPaint, rect.Left, rect.Top);
                        return;
                    }

                    var y = rect.Top;
                    y = DrawLegendGroup(canvas, rect, snapshot, standardIndices, style.LegendStandardGroupTitle, style, textPaint, headerPaint, y);
                    y += style.LegendGroupSpacing;
                    DrawLegendGroup(canvas, rect, snapshot, stackedIndices, style.LegendStackedGroupTitle, style, textPaint, headerPaint, y);
                }
                finally
                {
                    SkiaChartPools.ReturnList(standardIndices);
                    SkiaChartPools.ReturnList(stackedIndices);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(allIndices);
            }
        }

        private static SKSize MeasureLegend(ChartDataSnapshot snapshot, SkiaChartStyle style, float availableWidth, float availableHeight)
        {
            if (snapshot.Series.Count == 0)
            {
                return new SKSize(0, 0);
            }

            if (UsesCategoryLegend(snapshot))
            {
                var entries = BuildPieLegendEntries(snapshot, style);
                try
                {
                    if (entries.Count == 0)
                    {
                        return new SKSize(0, 0);
                    }

                    using var pieTextPaint = CreateTextPaint(style.Text, style.LegendTextSize);
                    return MeasureLegendEntries(entries, style, pieTextPaint, availableWidth, availableHeight);
                }
                finally
                {
                    SkiaChartPools.ReturnList(entries);
                }
            }

            using var textPaint = CreateTextPaint(style.Text, style.LegendTextSize);
            using var headerPaint = CreateTextPaint(style.Text, style.LegendGroupHeaderTextSize, bold: true);

            var allIndices = BuildLegendIndices(snapshot);
            try
            {
                if (!style.LegendGroupStackedSeries)
                {
                    return MeasureLegendItems(snapshot, allIndices, style, textPaint, availableWidth, availableHeight);
                }

                BuildLegendGroups(snapshot, out var standardIndices, out var stackedIndices);
                try
                {
                    if (standardIndices.Count == 0 || stackedIndices.Count == 0)
                    {
                        return MeasureLegendItems(snapshot, allIndices, style, textPaint, availableWidth, availableHeight);
                    }

                    var maxWidthGroup = 0f;
                    var totalHeight = 0f;

                    totalHeight += MeasureLegendGroup(snapshot, standardIndices, style.LegendStandardGroupTitle, style, textPaint, headerPaint, availableWidth, availableHeight, ref maxWidthGroup);
                    totalHeight += style.LegendGroupSpacing;
                    totalHeight += MeasureLegendGroup(snapshot, stackedIndices, style.LegendStackedGroupTitle, style, textPaint, headerPaint, availableWidth, availableHeight, ref maxWidthGroup);

                    return new SKSize(maxWidthGroup, totalHeight);
                }
                finally
                {
                    SkiaChartPools.ReturnList(standardIndices);
                    SkiaChartPools.ReturnList(stackedIndices);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(allIndices);
            }
        }

        private static float DrawLegendGroup(
            SKCanvas canvas,
            SKRect rect,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> indices,
            string? header,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint headerPaint,
            float startY)
        {
            var y = startY;
            if (!string.IsNullOrWhiteSpace(header))
            {
                var bounds = new SKRect();
                headerPaint.MeasureText(header, ref bounds);
                var textX = rect.Left - bounds.Left;
                var textY = AlignTopY(y, bounds);
                canvas.DrawText(header, textX, textY, headerPaint);
                y += bounds.Height + style.LegendSpacing;
            }

            var layout = DrawLegendItems(canvas, rect, snapshot, indices, style, textPaint, rect.Left, y);
            return y + layout.Height;
        }

        private static float MeasureLegendGroup(
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> indices,
            string? header,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint headerPaint,
            float availableWidth,
            float availableHeight,
            ref float maxWidth)
        {
            var totalHeight = 0f;
            var remainingHeight = availableHeight;
            if (!string.IsNullOrWhiteSpace(header))
            {
                var bounds = new SKRect();
                headerPaint.MeasureText(header, ref bounds);
                maxWidth = Math.Max(maxWidth, bounds.Width);
                totalHeight += bounds.Height + style.LegendSpacing;
                if (remainingHeight > 0f)
                {
                    remainingHeight = Math.Max(0f, remainingHeight - bounds.Height - style.LegendSpacing);
                }
            }

            var size = MeasureLegendItems(snapshot, indices, style, textPaint, availableWidth, remainingHeight);
            maxWidth = Math.Max(maxWidth, size.Width);
            totalHeight += size.Height;
            return totalHeight;
        }

        private static List<LegendEntry> BuildPieLegendEntries(ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            var entries = SkiaChartPools.RentList<LegendEntry>();
            var pieSeries = CollectPieSeries(snapshot);
            try
            {
                if (pieSeries.Count > 0)
                {
                    var valuesCount = pieSeries[0].Series.Values.Count;
                    var categories = snapshot.Categories;
                    var count = categories.Count > 0 ? Math.Min(categories.Count, valuesCount) : valuesCount;
                    if (entries.Capacity < count)
                    {
                        entries.Capacity = count;
                    }

                    for (var i = 0; i < count; i++)
                    {
                        var name = categories.Count > i ? categories[i] : null;
                        var entryName = string.IsNullOrWhiteSpace(name) ? $"Category {i + 1}" : name!;
                        entries.Add(new LegendEntry(entryName, GetSeriesColor(style, i)));
                    }

                    return entries;
                }

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
                    return entries;
                }

                var funnelCategories = snapshot.Categories;
                var funnelCount = funnelCategories.Count > 0
                    ? Math.Min(funnelCategories.Count, funnelSeries.Values.Count)
                    : funnelSeries.Values.Count;
                if (entries.Capacity < funnelCount)
                {
                    entries.Capacity = funnelCount;
                }

                for (var i = 0; i < funnelCount; i++)
                {
                    var name = funnelCategories.Count > i ? funnelCategories[i] : null;
                    var entryName = string.IsNullOrWhiteSpace(name) ? $"Stage {i + 1}" : name!;
                    entries.Add(new LegendEntry(entryName, GetSeriesColor(style, i)));
                }

                return entries;
            }
            finally
            {
                SkiaChartPools.ReturnList(pieSeries);
            }
        }

        private static LegendLayout DrawLegendEntries(
            SKCanvas canvas,
            SKRect rect,
            IReadOnlyList<LegendEntry> entries,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint swatchPaint,
            float startX,
            float startY)
        {
            var x = startX;
            var y = startY;
            var lineHeight = Math.Max(style.LegendSwatchSize, style.LegendTextSize) + style.LegendSpacing;
            var maxLineWidth = 0f;
            var columnWidth = 0f;
            var maxHeight = 0f;
            var availableWidth = rect.Width;

            for (var k = 0; k < entries.Count; k++)
            {
                var entry = entries[k];
                var displayName = GetLegendDisplayName(entry.Name, textPaint, style, availableWidth, out var bounds);
                var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;
                var itemHeight = Math.Max(style.LegendSwatchSize, bounds.Height);

                if (style.LegendFlow == SkiaLegendFlow.Row)
                {
                    if (style.LegendWrap && rect.Width > 0f && x + itemWidth > rect.Right && x > startX)
                    {
                        maxLineWidth = Math.Max(maxLineWidth, x - startX);
                        x = startX;
                        y += lineHeight;
                    }
                }

                swatchPaint.Color = entry.Color;
                var swatchRect = new SKRect(x, y, x + style.LegendSwatchSize, y + style.LegendSwatchSize);
                canvas.DrawRect(swatchRect, swatchPaint);

                var textX = x + style.LegendSwatchSize + style.LegendSpacing - bounds.Left;
                var textY = AlignCenterY(y + (style.LegendSwatchSize / 2f), bounds);
                canvas.DrawText(displayName, textX, textY, textPaint);

                if (style.LegendFlow == SkiaLegendFlow.Row)
                {
                    x += itemWidth + style.LegendSpacing;
                    maxLineWidth = Math.Max(maxLineWidth, x - startX);
                }
                else
                {
                    var needsWrap = style.LegendWrap && rect.Height > 0f && y + itemHeight > rect.Bottom && y > startY;
                    if (needsWrap)
                    {
                        var nextX = x + columnWidth + style.LegendSpacing;
                        if (rect.Width <= 0f || nextX + itemWidth <= rect.Right)
                        {
                            maxHeight = Math.Max(maxHeight, y - startY);
                            x = nextX;
                            y = startY;
                            columnWidth = 0f;
                        }
                    }

                    columnWidth = Math.Max(columnWidth, itemWidth);
                    y += Math.Max(lineHeight, itemHeight + style.LegendSpacing);
                }
            }

            if (style.LegendFlow == SkiaLegendFlow.Row)
            {
                var height = entries.Count == 0 ? 0f : (y - startY + lineHeight);
                var width = Math.Max(maxLineWidth, x - startX);
                return new LegendLayout(width, height);
            }

            maxHeight = Math.Max(maxHeight, y - startY);
            var totalWidth = (x - startX) + columnWidth;
            return new LegendLayout(totalWidth, maxHeight);
        }

        private static SKSize MeasureLegendEntries(
            IReadOnlyList<LegendEntry> entries,
            SkiaChartStyle style,
            SKPaint textPaint,
            float availableWidth,
            float availableHeight)
        {
            if (entries.Count == 0)
            {
                return new SKSize(0, 0);
            }

            var lineHeight = Math.Max(style.LegendSwatchSize, style.LegendTextSize) + style.LegendSpacing;
            if (style.LegendFlow == SkiaLegendFlow.Column)
            {
                var maxWidth = 0f;
                var height = 0f;
                var maxHeight = 0f;
                var columnWidth = 0f;
                var columnX = 0f;
                for (var k = 0; k < entries.Count; k++)
                {
                    var entry = entries[k];
                    _ = GetLegendDisplayName(entry.Name, textPaint, style, availableWidth, out var bounds);
                    var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;
                    var itemHeight = Math.Max(style.LegendSwatchSize, bounds.Height);
                    var needsWrap = style.LegendWrap && availableHeight > 0f && height + itemHeight > availableHeight && height > 0f;
                    if (needsWrap)
                    {
                        var nextX = columnX + columnWidth + style.LegendSpacing;
                        if (availableWidth <= 0f || nextX + itemWidth <= availableWidth)
                        {
                            maxHeight = Math.Max(maxHeight, height);
                            columnX = nextX;
                            height = 0f;
                            columnWidth = 0f;
                        }
                    }

                    columnWidth = Math.Max(columnWidth, itemWidth);
                    height += Math.Max(lineHeight, itemHeight + style.LegendSpacing);
                    maxWidth = Math.Max(maxWidth, columnX + columnWidth);
                }

                maxHeight = Math.Max(maxHeight, height);
                return new SKSize(maxWidth, maxHeight);
            }

            var x = 0f;
            var y = 0f;
            var maxLineWidth = 0f;
            for (var k = 0; k < entries.Count; k++)
            {
                var entry = entries[k];
                _ = GetLegendDisplayName(entry.Name, textPaint, style, availableWidth, out var bounds);
                var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;

                if (style.LegendWrap && availableWidth > 0f && x + itemWidth > availableWidth && x > 0)
                {
                    maxLineWidth = Math.Max(maxLineWidth, x);
                    x = 0f;
                    y += lineHeight;
                }

                x += itemWidth + style.LegendSpacing;
                maxLineWidth = Math.Max(maxLineWidth, x);
            }

            var totalHeight = y + lineHeight;
            var totalWidth = style.LegendWrap && availableWidth > 0f ? Math.Min(availableWidth, maxLineWidth) : maxLineWidth;
            return new SKSize(totalWidth, totalHeight);
        }

        private static LegendLayout DrawLegendItems(
            SKCanvas canvas,
            SKRect rect,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> indices,
            SkiaChartStyle style,
            SKPaint textPaint,
            float startX,
            float startY)
        {
            var x = startX;
            var y = startY;
            var lineHeight = Math.Max(style.LegendSwatchSize, style.LegendTextSize) + style.LegendSpacing;
            var maxLineWidth = 0f;
            var columnWidth = 0f;
            var maxHeight = 0f;
            var availableWidth = rect.Width;

            for (var k = 0; k < indices.Count; k++)
            {
                var seriesIndex = indices[k];
                var name = GetLegendSeriesName(snapshot, seriesIndex);
                var displayName = GetLegendDisplayName(name, textPaint, style, availableWidth, out var bounds);
                var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;
                var itemHeight = Math.Max(style.LegendSwatchSize, bounds.Height);

                if (style.LegendFlow == SkiaLegendFlow.Row)
                {
                    if (style.LegendWrap && rect.Width > 0f && x + itemWidth > rect.Right && x > startX)
                    {
                        maxLineWidth = Math.Max(maxLineWidth, x - startX);
                        x = startX;
                        y += lineHeight;
                    }
                }

                var swatchRect = new SKRect(x, y, x + style.LegendSwatchSize, y + style.LegendSwatchSize);
                if (seriesIndex >= 0 && seriesIndex < snapshot.Series.Count)
                {
                    DrawLegendSeriesSwatch(canvas, swatchRect, snapshot.Series[seriesIndex], seriesIndex, style);
                }

                var textX = x + style.LegendSwatchSize + style.LegendSpacing - bounds.Left;
                var textY = AlignCenterY(y + (style.LegendSwatchSize / 2f), bounds);
                canvas.DrawText(displayName, textX, textY, textPaint);

                if (style.LegendFlow == SkiaLegendFlow.Row)
                {
                    x += itemWidth + style.LegendSpacing;
                    maxLineWidth = Math.Max(maxLineWidth, x - startX);
                }
                else
                {
                    var needsWrap = style.LegendWrap && rect.Height > 0f && y + itemHeight > rect.Bottom && y > startY;
                    if (needsWrap)
                    {
                        var nextX = x + columnWidth + style.LegendSpacing;
                        if (rect.Width <= 0f || nextX + itemWidth <= rect.Right)
                        {
                            maxHeight = Math.Max(maxHeight, y - startY);
                            x = nextX;
                            y = startY;
                            columnWidth = 0f;
                        }
                    }

                    columnWidth = Math.Max(columnWidth, itemWidth);
                    y += Math.Max(lineHeight, itemHeight + style.LegendSpacing);
                }
            }

            if (style.LegendFlow == SkiaLegendFlow.Row)
            {
                var height = indices.Count == 0 ? 0f : (y - startY + lineHeight);
                var width = Math.Max(maxLineWidth, x - startX);
                return new LegendLayout(width, height);
            }

            maxHeight = Math.Max(maxHeight, y - startY);
            var totalWidth = (x - startX) + columnWidth;
            return new LegendLayout(totalWidth, maxHeight);
        }

        private static void DrawLegendSeriesSwatch(
            SKCanvas canvas,
            SKRect rect,
            ChartSeriesSnapshot series,
            int seriesIndex,
            SkiaChartStyle style)
        {
            var overrides = GetSeriesStyleOverrides(style, seriesIndex);
            var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
            var strokeColor = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);
            var fillColor = ResolveSeriesFillColor(strokeColor, overrides, themeStyle);
            var gradient = ResolveSeriesGradient(overrides, themeStyle);
            var lineStyle = ResolveSeriesLineStyle(overrides, themeStyle);
            var dashPattern = ResolveSeriesDashPattern(overrides, themeStyle);
            var defaultMarkerSize = Math.Max(2f, rect.Height * 0.25f);
            var markerFallback = series.Kind == ChartSeriesKind.Area ||
                                 series.Kind == ChartSeriesKind.StackedArea ||
                                 series.Kind == ChartSeriesKind.StackedArea100
                ? SkiaMarkerShape.None
                : SkiaMarkerShape.Circle;
            var markerShape = ResolveMarkerShape(overrides, themeStyle, markerFallback);
            var markerSize = ResolveMarkerSize(overrides, themeStyle, defaultMarkerSize);
            var markerFill = ResolveMarkerFillColor(strokeColor, overrides, themeStyle);
            var markerStroke = ResolveMarkerStrokeColor(strokeColor, overrides, themeStyle);
            var markerStrokeWidth = ResolveMarkerStrokeWidth(overrides, themeStyle);

            var strokeWidth = ResolveSeriesStrokeWidth(overrides, themeStyle, style.SeriesStrokeWidth);
            strokeWidth = Math.Max(1f, Math.Min(strokeWidth, rect.Height * 0.4f));

            var center = new SKPoint(rect.MidX, rect.MidY);
            var lineY = rect.MidY;
            var lineLeft = rect.Left + (rect.Width * 0.1f);
            var lineRight = rect.Right - (rect.Width * 0.1f);

            var fillOpacity = 1f;
            if (series.Kind == ChartSeriesKind.Area ||
                series.Kind == ChartSeriesKind.StackedArea ||
                series.Kind == ChartSeriesKind.StackedArea100)
            {
                fillOpacity = style.AreaFillOpacity;
            }
            else if (series.Kind == ChartSeriesKind.Bubble)
            {
                fillOpacity = style.BubbleFillOpacity;
            }
            else if (series.Kind == ChartSeriesKind.BoxWhisker)
            {
                fillOpacity = style.BoxWhiskerFillOpacity;
            }

            using var fillPaint = new SKPaint
            {
                Color = ApplyOpacity(fillColor, fillOpacity),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var fillShader = gradient != null
                ? CreateGradientShader(rect, gradient, fillOpacity)
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

            switch (series.Kind)
            {
                case ChartSeriesKind.Line:
                case ChartSeriesKind.Radar:
                    canvas.DrawLine(lineLeft, lineY, lineRight, lineY, linePaint);
                    DrawMarker(canvas, center, markerSize, markerShape, markerPaint, markerStrokePaint);
                    break;
                case ChartSeriesKind.Area:
                case ChartSeriesKind.StackedArea:
                case ChartSeriesKind.StackedArea100:
                    canvas.DrawRect(rect, fillPaint);
                    canvas.DrawLine(lineLeft, lineY, lineRight, lineY, linePaint);
                    DrawMarker(canvas, center, markerSize, markerShape, markerPaint, markerStrokePaint);
                    break;
                case ChartSeriesKind.Scatter:
                    DrawMarker(canvas, center, markerSize, markerShape, markerPaint, markerStrokePaint);
                    break;
                case ChartSeriesKind.Bubble:
                    {
                        var radius = Math.Max(2f, Math.Min(rect.Width, rect.Height) * 0.35f);
                        canvas.DrawCircle(center, radius, fillPaint);
                        canvas.DrawCircle(center, radius, linePaint);
                        break;
                    }
                case ChartSeriesKind.Candlestick:
                case ChartSeriesKind.HeikinAshi:
                case ChartSeriesKind.Range:
                    {
                        using var wickPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        using var bodyFillPaint = new SKPaint
                        {
                            Color = ApplyOpacity(style.FinancialIncreaseColor, style.FinancialBodyFillOpacity),
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill
                        };

                        using var bodyStrokePaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialBodyStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var bodyWidth = rect.Width * 0.42f;
                        var bodyRect = new SKRect(center.X - bodyWidth / 2f, rect.Top + rect.Height * 0.28f, center.X + bodyWidth / 2f, rect.Bottom - rect.Height * 0.28f);
                        canvas.DrawLine(center.X, rect.Top + rect.Height * 0.1f, center.X, rect.Bottom - rect.Height * 0.1f, wickPaint);
                        if (!style.FinancialHollowBullishBodies)
                        {
                            canvas.DrawRect(bodyRect, bodyFillPaint);
                        }
                        canvas.DrawRect(bodyRect, bodyStrokePaint);
                        break;
                    }
                case ChartSeriesKind.HollowCandlestick:
                    {
                        using var bullishWickPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        using var bearishWickPaint = new SKPaint
                        {
                            Color = style.FinancialDecreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        using var bearishFillPaint = new SKPaint
                        {
                            Color = ApplyOpacity(style.FinancialDecreaseColor, style.FinancialBodyFillOpacity),
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill
                        };

                        using var bullishBodyPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialBodyStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        using var bearishBodyPaint = new SKPaint
                        {
                            Color = style.FinancialDecreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialBodyStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var bullishRect = new SKRect(center.X - rect.Width * 0.34f, rect.Top + rect.Height * 0.24f, center.X - rect.Width * 0.02f, rect.Bottom - rect.Height * 0.32f);
                        var bearishRect = new SKRect(center.X + rect.Width * 0.02f, rect.Top + rect.Height * 0.34f, center.X + rect.Width * 0.34f, rect.Bottom - rect.Height * 0.22f);

                        canvas.DrawLine(bullishRect.MidX, rect.Top + rect.Height * 0.1f, bullishRect.MidX, rect.Bottom - rect.Height * 0.14f, bullishWickPaint);
                        canvas.DrawRect(bullishRect, bullishBodyPaint);

                        canvas.DrawLine(bearishRect.MidX, rect.Top + rect.Height * 0.14f, bearishRect.MidX, rect.Bottom - rect.Height * 0.1f, bearishWickPaint);
                        canvas.DrawRect(bearishRect, bearishFillPaint);
                        canvas.DrawRect(bearishRect, bearishBodyPaint);
                        break;
                    }
                case ChartSeriesKind.Ohlc:
                    {
                        using var financialPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var tick = rect.Width * 0.18f;
                        var high = rect.Top + rect.Height * 0.1f;
                        var low = rect.Bottom - rect.Height * 0.1f;
                        var open = rect.Top + rect.Height * 0.35f;
                        var close = rect.Bottom - rect.Height * 0.35f;
                        canvas.DrawLine(center.X, high, center.X, low, financialPaint);
                        canvas.DrawLine(center.X - tick, open, center.X, open, financialPaint);
                        canvas.DrawLine(center.X, close, center.X + tick, close, financialPaint);
                        break;
                    }
                case ChartSeriesKind.Hlc:
                    {
                        using var financialPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var tick = rect.Width * 0.18f;
                        var high = rect.Top + rect.Height * 0.1f;
                        var low = rect.Bottom - rect.Height * 0.1f;
                        var close = rect.Bottom - rect.Height * 0.35f;
                        canvas.DrawLine(center.X, high, center.X, low, financialPaint);
                        canvas.DrawLine(center.X, close, center.X + tick, close, financialPaint);
                        break;
                    }
                case ChartSeriesKind.Renko:
                case ChartSeriesKind.LineBreak:
                    {
                        using var bodyFillPaint = new SKPaint
                        {
                            Color = ApplyOpacity(style.FinancialIncreaseColor, style.FinancialBodyFillOpacity),
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill
                        };

                        using var bodyStrokePaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialBodyStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var boxRect = new SKRect(rect.Left + rect.Width * 0.14f, rect.Top + rect.Height * 0.2f, rect.Right - rect.Width * 0.14f, rect.Bottom - rect.Height * 0.2f);
                        if (!style.FinancialHollowBullishBodies)
                        {
                            canvas.DrawRect(boxRect, bodyFillPaint);
                        }
                        canvas.DrawRect(boxRect, bodyStrokePaint);
                        break;
                    }
                case ChartSeriesKind.Kagi:
                    {
                        using var kagiPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialBodyStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var leftX = rect.Left + rect.Width * 0.2f;
                        var rightX = rect.Right - rect.Width * 0.2f;
                        var midX = rect.MidX;
                        var top = rect.Top + rect.Height * 0.18f;
                        var midHigh = rect.Top + rect.Height * 0.34f;
                        var midLow = rect.Bottom - rect.Height * 0.34f;
                        var bottom = rect.Bottom - rect.Height * 0.16f;

                        canvas.DrawLine(leftX, midLow, midX, midLow, kagiPaint);
                        canvas.DrawLine(midX, midLow, midX, top, kagiPaint);
                        canvas.DrawLine(midX, midHigh, rightX, midHigh, kagiPaint);
                        canvas.DrawLine(rightX, midHigh, rightX, bottom, kagiPaint);
                        break;
                    }
                case ChartSeriesKind.PointFigure:
                    {
                        using var bullishPaint = new SKPaint
                        {
                            Color = style.FinancialIncreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        using var bearishPaint = new SKPaint
                        {
                            Color = style.FinancialDecreaseColor,
                            StrokeWidth = Math.Max(1f, style.FinancialWickStrokeWidth),
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke
                        };

                        var xSize = Math.Min(rect.Width, rect.Height) * 0.24f;
                        var leftCenter = new SKPoint(rect.Left + rect.Width * 0.32f, rect.MidY);
                        var rightCenter = new SKPoint(rect.Right - rect.Width * 0.3f, rect.MidY);
                        canvas.DrawLine(leftCenter.X - xSize, leftCenter.Y - xSize, leftCenter.X + xSize, leftCenter.Y + xSize, bullishPaint);
                        canvas.DrawLine(leftCenter.X + xSize, leftCenter.Y - xSize, leftCenter.X - xSize, leftCenter.Y + xSize, bullishPaint);
                        canvas.DrawOval(new SKRect(rightCenter.X - xSize, rightCenter.Y - xSize, rightCenter.X + xSize, rightCenter.Y + xSize), bearishPaint);
                        break;
                    }
                case ChartSeriesKind.Pareto:
                    canvas.DrawRect(rect, fillPaint);
                    canvas.DrawLine(lineLeft, rect.Top + rect.Height * 0.25f, lineRight, rect.Top + rect.Height * 0.25f, linePaint);
                    break;
                default:
                    canvas.DrawRect(rect, fillPaint);
                    break;
            }
        }

        private static SKSize MeasureLegendItems(
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> indices,
            SkiaChartStyle style,
            SKPaint textPaint,
            float availableWidth,
            float availableHeight)
        {
            if (indices.Count == 0)
            {
                return new SKSize(0, 0);
            }

            var lineHeight = Math.Max(style.LegendSwatchSize, style.LegendTextSize) + style.LegendSpacing;
            if (style.LegendFlow == SkiaLegendFlow.Column)
            {
                var maxWidth = 0f;
                var height = 0f;
                var maxHeight = 0f;
                var columnWidth = 0f;
                var columnX = 0f;
                for (var k = 0; k < indices.Count; k++)
                {
                    var seriesIndex = indices[k];
                    var name = GetLegendSeriesName(snapshot, seriesIndex);
                    _ = GetLegendDisplayName(name, textPaint, style, availableWidth, out var bounds);
                    var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;
                    var itemHeight = Math.Max(style.LegendSwatchSize, bounds.Height);
                    var needsWrap = style.LegendWrap && availableHeight > 0f && height + itemHeight > availableHeight && height > 0f;
                    if (needsWrap)
                    {
                        var nextX = columnX + columnWidth + style.LegendSpacing;
                        if (availableWidth <= 0f || nextX + itemWidth <= availableWidth)
                        {
                            maxHeight = Math.Max(maxHeight, height);
                            columnX = nextX;
                            height = 0f;
                            columnWidth = 0f;
                        }
                    }

                    columnWidth = Math.Max(columnWidth, itemWidth);
                    height += Math.Max(lineHeight, itemHeight + style.LegendSpacing);
                    maxWidth = Math.Max(maxWidth, columnX + columnWidth);
                }

                maxHeight = Math.Max(maxHeight, height);
                return new SKSize(maxWidth, maxHeight);
            }

            var x = 0f;
            var y = 0f;
            var maxLineWidth = 0f;
            for (var k = 0; k < indices.Count; k++)
            {
                var seriesIndex = indices[k];
                var name = GetLegendSeriesName(snapshot, seriesIndex);
                _ = GetLegendDisplayName(name, textPaint, style, availableWidth, out var bounds);
                var itemWidth = style.LegendSwatchSize + style.LegendSpacing + bounds.Width;

                if (style.LegendWrap && availableWidth > 0f && x + itemWidth > availableWidth && x > 0)
                {
                    maxLineWidth = Math.Max(maxLineWidth, x);
                    x = 0f;
                    y += lineHeight;
                }

                x += itemWidth + style.LegendSpacing;
                maxLineWidth = Math.Max(maxLineWidth, x);
            }

            var totalHeight = y + lineHeight;
            var totalWidth = style.LegendWrap && availableWidth > 0f ? Math.Min(availableWidth, maxLineWidth) : maxLineWidth;
            return new SKSize(totalWidth, totalHeight);
        }

        private static List<int> BuildLegendIndices(ChartDataSnapshot snapshot)
        {
            var indices = SkiaChartPools.RentList<int>(snapshot.Series.Count);
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                indices.Add(i);
            }

            return indices;
        }

        private static void BuildLegendGroups(
            ChartDataSnapshot snapshot,
            out List<int> standardIndices,
            out List<int> stackedIndices)
        {
            standardIndices = SkiaChartPools.RentList<int>();
            stackedIndices = SkiaChartPools.RentList<int>();

            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                if (IsStackedKind(series.Kind))
                {
                    stackedIndices.Add(i);
                }
                else
                {
                    standardIndices.Add(i);
                }
            }
        }

        private static bool IsStackedKind(ChartSeriesKind kind)
        {
            return kind == ChartSeriesKind.StackedColumn ||
                   kind == ChartSeriesKind.StackedBar ||
                   kind == ChartSeriesKind.StackedArea ||
                   kind == ChartSeriesKind.StackedColumn100 ||
                   kind == ChartSeriesKind.StackedBar100 ||
                   kind == ChartSeriesKind.StackedArea100;
        }

        private static string GetLegendSeriesName(ChartDataSnapshot snapshot, int seriesIndex)
        {
            if (seriesIndex < 0 || seriesIndex >= snapshot.Series.Count)
            {
                return $"Series {seriesIndex + 1}";
            }

            var name = snapshot.Series[seriesIndex].Name;
            return string.IsNullOrWhiteSpace(name) ? $"Series {seriesIndex + 1}" : name!;
        }

        private readonly struct LegendEntry
        {
            public LegendEntry(string name, SKColor color)
            {
                Name = name;
                Color = color;
            }

            public string Name { get; }

            public SKColor Color { get; }
        }

        private readonly struct LegendLayout
        {
            public LegendLayout(float width, float height)
            {
                Width = width;
                Height = height;
            }

            public float Width { get; }

            public float Height { get; }
        }

        private static float ResolveLegendMaxWidth(SkiaChartStyle style, SKRect bounds)
        {
            if (style.LegendMaxWidth > 0)
            {
                return style.LegendMaxWidth;
            }

            if (style.LegendPosition == ChartLegendPosition.Top || style.LegendPosition == ChartLegendPosition.Bottom)
            {
                return Math.Max(0, bounds.Width - (style.PaddingLeft + style.PaddingRight));
            }

            return Math.Max(0, bounds.Width * 0.35f);
        }

        private static void ResolveLegendConstraints(
            SKRect bounds,
            SKRect plot,
            SkiaChartStyle style,
            out float maxWidth,
            out float maxHeight)
        {
            var innerWidth = Math.Max(0f, plot.Width);
            var innerHeight = Math.Max(0f, plot.Height);
            var minPlotSize = Math.Max(16f, style.LabelSize * 4f);

            maxWidth = ResolveLegendMaxWidth(style, bounds);
            maxHeight = innerHeight;

            if (style.LegendPosition == ChartLegendPosition.Left || style.LegendPosition == ChartLegendPosition.Right)
            {
                maxHeight = Math.Max(0f, plot.Height);
                var maxLegendWidth = Math.Max(0f, innerWidth - minPlotSize);
                maxWidth = Math.Min(maxWidth, maxLegendWidth);
            }
            else
            {
                maxWidth = Math.Max(0f, plot.Width);
                var maxLegendHeight = Math.Max(0f, innerHeight - minPlotSize);
                maxHeight = Math.Min(maxHeight, maxLegendHeight);
            }
        }

        private static SKRect LayoutLegend(SKRect bounds, ref SKRect plot, SKSize size, SkiaChartStyle style)
        {
            var padding = style.LegendPadding;
            switch (style.LegendPosition)
            {
                case ChartLegendPosition.Left:
                    plot.Left += size.Width + padding;
                    return new SKRect(bounds.Left + padding, plot.Top, bounds.Left + padding + size.Width, plot.Top + size.Height);
                case ChartLegendPosition.Top:
                    plot.Top += size.Height + padding;
                    return new SKRect(plot.Left, bounds.Top + padding, plot.Left + size.Width, bounds.Top + padding + size.Height);
                case ChartLegendPosition.Bottom:
                    plot.Bottom -= size.Height + padding;
                    return new SKRect(plot.Left, bounds.Bottom - padding - size.Height, plot.Left + size.Width, bounds.Bottom - padding);
                case ChartLegendPosition.Right:
                default:
                    plot.Right -= size.Width + padding;
                    return new SKRect(plot.Right + padding, plot.Top, plot.Right + padding + size.Width, plot.Top + size.Height);
            }
        }

    }
}
