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
        private static void DrawWaterfallDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
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
            var count = Math.Min(categoryCount, series.Values.Count);

            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                running += value.Value;
                var x = plot.Left + (i * groupWidth) + offset + (barWidth / 2f);
                var y = MapY(plot, running, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawHistogramDataLabels(
            SKCanvas canvas,
            SKRect plot,
            HistogramContext histogramContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            if (!histogramContext.TryGetSeries(seriesIndex, out var histogramSeries))
            {
                return;
            }

            var binCount = histogramContext.BinCount;
            if (binCount == 0)
            {
                return;
            }

            var seriesCount = Math.Max(1, histogramContext.Series.Count);
            var groupWidth = plot.Width / binCount;
            var barWidth = groupWidth / seriesCount * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;

            for (var i = 0; i < binCount; i++)
            {
                var count = histogramSeries.Counts[i];
                if (count <= 0d)
                {
                    continue;
                }

                var x = plot.Left + (i * groupWidth) + offset + (histogramSeries.OrderIndex * barWidth) + (barWidth / 2f);
                var y = MapY(plot, count, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(histogramSeries.Series, seriesIndex, count, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawBoxWhiskerDataLabels(
            SKCanvas canvas,
            SKRect plot,
            BoxWhiskerContext boxWhiskerContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            if (!boxWhiskerContext.TryGetSeries(seriesIndex, out var boxSeries))
            {
                return;
            }

            var categoryCount = boxWhiskerContext.Categories.Count;
            if (categoryCount == 0)
            {
                return;
            }

            var groupWidth = plot.Width / categoryCount;
            var boxWidth = groupWidth * 0.5f;
            var offset = (groupWidth - boxWidth) / 2f;
            var x = plot.Left + (boxSeries.OrderIndex * groupWidth) + offset + (boxWidth / 2f);
            var y = MapY(plot, boxSeries.Stats.Median, minValue, maxValue, valueAxisKind);
            var text = FormatDataLabel(boxSeries.Series, seriesIndex, boxSeries.Stats.Median, style);
            TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
        }

        private static void DrawDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            HistogramContext? histogramContext,
            BoxWhiskerContext? boxWhiskerContext)
        {
            if (!style.ShowDataLabels)
            {
                return;
            }

            using var textPaint = CreateTextPaint(style.DataLabelText, style.DataLabelTextSize);

            using var backgroundPaint = new SKPaint
            {
                Color = style.DataLabelBackground,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var placed = SkiaChartPools.RentList<SKRect>();
            var stackedColumnsPrimary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Primary);
            var stackedColumnsSecondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Secondary);
            var stackedColumns100Primary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Primary);
            var stackedColumns100Secondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Secondary);
            var stackedBarsPrimary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Primary);
            var stackedBarsSecondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Secondary);
            var stackedBars100Primary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Primary);
            var stackedBars100Secondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Secondary);
            var stackedAreasPrimary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Primary);
            var stackedAreasSecondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Secondary);
            var stackedAreas100Primary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Primary);
            var stackedAreas100Secondary = CollectSeriesIndicesPooled(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Secondary);
            try
            {
                var categoryCount = categories.Count;
                var seriesCount = snapshot.Series.Count;
                TryGetBubbleSizeRange(snapshot, out var minBubbleSize, out var maxBubbleSize);
                var stackedColumnsPrimaryDrawn = false;
                var stackedColumnsSecondaryDrawn = false;
                var stackedColumns100PrimaryDrawn = false;
                var stackedColumns100SecondaryDrawn = false;
                var stackedBarsPrimaryDrawn = false;
                var stackedBarsSecondaryDrawn = false;
                var stackedBars100PrimaryDrawn = false;
                var stackedBars100SecondaryDrawn = false;
                var stackedAreasPrimaryDrawn = false;
                var stackedAreasSecondaryDrawn = false;
                var stackedAreas100PrimaryDrawn = false;
                var stackedAreas100SecondaryDrawn = false;

                for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
                {
                    var series = snapshot.Series[seriesIndex];
                    if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                    {
                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedColumn)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedColumnsSecondaryDrawn)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedColumnsSecondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                                stackedColumnsSecondaryDrawn = true;
                            }
                        }
                        else if (!stackedColumnsPrimaryDrawn)
                        {
                            DrawStackedColumnDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedColumnsPrimary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: false);
                            stackedColumnsPrimaryDrawn = true;
                        }

                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedColumn100)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedColumns100SecondaryDrawn)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedColumns100Secondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                                stackedColumns100SecondaryDrawn = true;
                            }
                        }
                        else if (!stackedColumns100PrimaryDrawn)
                        {
                            DrawStackedColumnDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedColumns100Primary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: true);
                            stackedColumns100PrimaryDrawn = true;
                        }

                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedBar)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedBarsSecondaryDrawn)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedBarsSecondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                                stackedBarsSecondaryDrawn = true;
                            }
                        }
                        else if (!stackedBarsPrimaryDrawn)
                        {
                            DrawStackedBarDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedBarsPrimary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: false);
                            stackedBarsPrimaryDrawn = true;
                        }

                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedBar100)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedBars100SecondaryDrawn)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedBars100Secondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                                stackedBars100SecondaryDrawn = true;
                            }
                        }
                        else if (!stackedBars100PrimaryDrawn)
                        {
                            DrawStackedBarDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedBars100Primary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: true);
                            stackedBars100PrimaryDrawn = true;
                        }

                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedArea)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedAreasSecondaryDrawn)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedAreasSecondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                                stackedAreasSecondaryDrawn = true;
                            }
                        }
                        else if (!stackedAreasPrimaryDrawn)
                        {
                            DrawStackedAreaDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedAreasPrimary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: false);
                            stackedAreasPrimaryDrawn = true;
                        }

                        continue;
                    }

                    if (series.Kind == ChartSeriesKind.StackedArea100)
                    {
                        var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                        if (isSecondary)
                        {
                            if (!stackedAreas100SecondaryDrawn)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    plot,
                                    snapshot,
                                    stackedAreas100Secondary,
                                    minSecondaryValue,
                                    maxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                                stackedAreas100SecondaryDrawn = true;
                            }
                        }
                        else if (!stackedAreas100PrimaryDrawn)
                        {
                            DrawStackedAreaDataLabels(
                                canvas,
                                plot,
                                snapshot,
                                stackedAreas100Primary,
                                minValue,
                                maxValue,
                                style.ValueAxisKind,
                                style,
                                textPaint,
                                backgroundPaint,
                                placed,
                                normalizeToPercent: true);
                            stackedAreas100PrimaryDrawn = true;
                        }

                        continue;
                    }
                    var seriesIsSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    var seriesMinValue = seriesIsSecondary ? minSecondaryValue : minValue;
                    var seriesMaxValue = seriesIsSecondary ? maxSecondaryValue : maxValue;
                    var seriesAxisKind = seriesIsSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;

                    DrawSeriesDataLabels(
                        canvas,
                        plot,
                        series,
                        seriesIndex,
                        seriesCount,
                        categories,
                        seriesMinValue,
                        seriesMaxValue,
                        seriesAxisKind,
                        style,
                        useNumericCategoryAxis,
                        categoryAxisKind,
                        minCategory,
                        maxCategory,
                        histogramContext,
                        boxWhiskerContext,
                        minBubbleSize,
                        maxBubbleSize,
                        textPaint,
                        backgroundPaint,
                        placed);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(stackedColumnsPrimary);
                SkiaChartPools.ReturnList(stackedColumnsSecondary);
                SkiaChartPools.ReturnList(stackedColumns100Primary);
                SkiaChartPools.ReturnList(stackedColumns100Secondary);
                SkiaChartPools.ReturnList(stackedBarsPrimary);
                SkiaChartPools.ReturnList(stackedBarsSecondary);
                SkiaChartPools.ReturnList(stackedBars100Primary);
                SkiaChartPools.ReturnList(stackedBars100Secondary);
                SkiaChartPools.ReturnList(stackedAreasPrimary);
                SkiaChartPools.ReturnList(stackedAreasSecondary);
                SkiaChartPools.ReturnList(stackedAreas100Primary);
                SkiaChartPools.ReturnList(stackedAreas100Secondary);
                SkiaChartPools.ReturnList(placed);
            }
        }

        private static List<int> CollectSeriesIndicesPooled(
            ChartDataSnapshot snapshot,
            ChartSeriesKind kind,
            ChartValueAxisAssignment? axisAssignment = null)
        {
            var indices = SkiaChartPools.RentList<int>(snapshot.Series.Count);
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                if (series.Kind == kind && (!axisAssignment.HasValue || series.ValueAxisAssignment == axisAssignment.Value))
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private static void DrawSeriesDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            int seriesCount,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            HistogramContext? histogramContext,
            BoxWhiskerContext? boxWhiskerContext,
            double minBubbleSize,
            double maxBubbleSize,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            switch (series.Kind)
            {
                case ChartSeriesKind.Column:
                    DrawColumnDataLabels(canvas, plot, series, seriesIndex, seriesCount, categories.Count, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    break;
                case ChartSeriesKind.Bar:
                    DrawBarDataLabels(canvas, plot, series, seriesIndex, seriesCount, categories.Count, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    break;
                case ChartSeriesKind.Waterfall:
                    DrawWaterfallDataLabels(canvas, plot, series, seriesIndex, categories, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    break;
                case ChartSeriesKind.Histogram:
                    if (histogramContext != null)
                    {
                        DrawHistogramDataLabels(canvas, plot, histogramContext, seriesIndex, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    }
                    break;
                case ChartSeriesKind.Pareto:
                    if (histogramContext != null)
                    {
                        DrawHistogramDataLabels(canvas, plot, histogramContext, seriesIndex, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    }
                    break;
                case ChartSeriesKind.BoxWhisker:
                    if (boxWhiskerContext != null)
                    {
                        DrawBoxWhiskerDataLabels(canvas, plot, boxWhiskerContext, seriesIndex, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    }
                    break;
                case ChartSeriesKind.Candlestick:
                case ChartSeriesKind.HollowCandlestick:
                case ChartSeriesKind.Ohlc:
                case ChartSeriesKind.Hlc:
                case ChartSeriesKind.HeikinAshi:
                case ChartSeriesKind.Renko:
                case ChartSeriesKind.Range:
                case ChartSeriesKind.LineBreak:
                case ChartSeriesKind.Kagi:
                case ChartSeriesKind.PointFigure:
                    DrawFinancialDataLabels(canvas, plot, series, seriesIndex, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    break;
                case ChartSeriesKind.Scatter:
                    DrawScatterDataLabels(
                        canvas,
                        plot,
                        series,
                        seriesIndex,
                        minValue,
                        maxValue,
                        valueAxisKind,
                        style,
                        useNumericCategoryAxis,
                        categoryAxisKind,
                        minCategory,
                        maxCategory,
                        textPaint,
                        backgroundPaint,
                        placed);
                    break;
                case ChartSeriesKind.Bubble:
                    DrawBubbleDataLabels(
                        canvas,
                        plot,
                        series,
                        seriesIndex,
                        minValue,
                        maxValue,
                        valueAxisKind,
                        style,
                        useNumericCategoryAxis,
                        categoryAxisKind,
                        minCategory,
                        maxCategory,
                        minBubbleSize,
                        maxBubbleSize,
                        textPaint,
                        backgroundPaint,
                        placed);
                    break;
                case ChartSeriesKind.StackedColumn:
                case ChartSeriesKind.StackedBar:
                case ChartSeriesKind.StackedArea:
                case ChartSeriesKind.StackedColumn100:
                case ChartSeriesKind.StackedBar100:
                case ChartSeriesKind.StackedArea100:
                    break;
                default:
                    DrawLineDataLabels(canvas, plot, series, seriesIndex, minValue, maxValue, valueAxisKind, style, textPaint, backgroundPaint, placed);
                    break;
            }
        }

        private static void RenderDataLabelSegment(
            SKCanvas canvas,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context,
            DataSegment segment,
            List<SKRect> placed,
            SKPaint textPaint,
            SKPaint backgroundPaint)
        {
            if (!style.ShowDataLabels)
            {
                return;
            }

            switch (context.RenderKind)
            {
                case RenderKind.Cartesian:
                    switch (segment.Kind)
                    {
                        case SkiaChartDataSegmentKind.Series:
                        {
                            if (segment.SeriesIndex < 0 || segment.SeriesIndex >= snapshot.Series.Count)
                            {
                                return;
                            }

                            var series = snapshot.Series[segment.SeriesIndex];
                            if (series.Kind == ChartSeriesKind.StackedColumn ||
                                series.Kind == ChartSeriesKind.StackedBar ||
                                series.Kind == ChartSeriesKind.StackedArea)
                            {
                                return;
                            }

                            var seriesIsSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                            var seriesMinValue = seriesIsSecondary ? context.MinSecondaryValue : context.MinValue;
                            var seriesMaxValue = seriesIsSecondary ? context.MaxSecondaryValue : context.MaxValue;
                            var seriesAxisKind = seriesIsSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;

                            DrawSeriesDataLabels(
                                canvas,
                                context.Plot,
                                series,
                                segment.SeriesIndex,
                                snapshot.Series.Count,
                                context.RenderCategories,
                                seriesMinValue,
                                seriesMaxValue,
                                seriesAxisKind,
                                style,
                                context.UseNumericCategoryAxis,
                                context.CategoryAxisKind,
                                context.MinCategory,
                                context.MaxCategory,
                                context.HistogramContext,
                                context.BoxWhiskerContext,
                                context.MinBubbleSize,
                                context.MaxBubbleSize,
                                textPaint,
                                backgroundPaint,
                                placed);
                            break;
                        }
                        case SkiaChartDataSegmentKind.StackedColumnPrimary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedColumnSecondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedColumn100Primary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedColumn100Secondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedColumnDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedBarPrimary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedBarSecondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedBar100Primary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedBar100Secondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedBarDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedAreaPrimary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedAreaSecondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: false);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedArea100Primary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinValue,
                                    context.MaxValue,
                                    style.ValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                        case SkiaChartDataSegmentKind.StackedArea100Secondary:
                            if (segment.GroupIndices != null && segment.GroupIndices.Count > 0)
                            {
                                DrawStackedAreaDataLabels(
                                    canvas,
                                    context.Plot,
                                    snapshot,
                                    segment.GroupIndices,
                                    context.MinSecondaryValue,
                                    context.MaxSecondaryValue,
                                    style.SecondaryValueAxisKind,
                                    style,
                                    textPaint,
                                    backgroundPaint,
                                    placed,
                                    normalizeToPercent: true);
                            }
                            break;
                    }
                    break;
                case RenderKind.Pie:
                    DrawPieDataLabels(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Radar:
                    DrawRadarDataLabels(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Funnel:
                    DrawFunnelDataLabels(canvas, context.Plot, snapshot, style);
                    break;
            }
        }

        private static void DrawLineDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            var count = series.Values.Count;
            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var x = MapX(plot, i, count);
                var y = MapY(plot, value.Value, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawFinancialDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            var count = GetFinancialPointCount(series, series.Kind);
            for (var i = 0; i < count; i++)
            {
                if (!TryGetFinancialPoint(series, series.Kind, i, valueAxisKind, out _, out var high, out _, out var close))
                {
                    continue;
                }

                var x = MapX(plot, i, count);
                var y = MapY(plot, high, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, close, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawScatterDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            var count = series.Values.Count;
            var xValues = series.XValues;
            var hasX = xValues != null && xValues.Count == count;
            var hasValidX = false;
            double minX = 0;
            double maxX = 1;
            if (hasX)
            {
                if (useNumericCategoryAxis)
                {
                    minX = minCategory;
                    maxX = maxCategory;
                    hasValidX = !IsInvalidAxisValue(minX, categoryAxisKind) &&
                                !IsInvalidAxisValue(maxX, categoryAxisKind) &&
                                maxX > minX;
                }
                else
                {
                    minX = double.MaxValue;
                    maxX = double.MinValue;
                    foreach (var x in xValues!)
                    {
                        if (IsInvalidAxisValue(x, categoryAxisKind))
                        {
                            continue;
                        }

                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        hasValidX = true;
                    }

                    if (hasValidX && Math.Abs(maxX - minX) < double.Epsilon)
                    {
                        maxX = minX + 1d;
                    }
                }
            }

            if (!hasValidX)
            {
                hasX = false;
            }

            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var xValue = hasX ? xValues![i] : 0d;
                if (hasX && IsInvalidAxisValue(xValue, categoryAxisKind))
                {
                    continue;
                }

                var x = hasX
                    ? MapValueX(plot, xValue, minX, maxX, categoryAxisKind)
                    : MapX(plot, i, count);
                var y = MapY(plot, value.Value, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawBubbleDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            double minBubbleSize,
            double maxBubbleSize,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            var count = series.Values.Count;
            var xValues = series.XValues;
            var hasValidX = false;
            double minX = 0;
            double maxX = 1;
            if (xValues != null && xValues.Count == count)
            {
                if (useNumericCategoryAxis)
                {
                    minX = minCategory;
                    maxX = maxCategory;
                    hasValidX = !IsInvalidAxisValue(minX, categoryAxisKind) &&
                                !IsInvalidAxisValue(maxX, categoryAxisKind) &&
                                maxX > minX;
                }
                else
                {
                    minX = double.MaxValue;
                    maxX = double.MinValue;
                    foreach (var x in xValues)
                    {
                        if (IsInvalidAxisValue(x, categoryAxisKind))
                        {
                            continue;
                        }

                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        hasValidX = true;
                    }

                    if (hasValidX && Math.Abs(maxX - minX) < double.Epsilon)
                    {
                        maxX = minX + 1d;
                    }
                }
            }

            var sizeValues = series.SizeValues;
            var hasSizes = sizeValues != null && sizeValues.Count == count;

            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var xValue = hasValidX ? xValues![i] : 0d;
                if (hasValidX && IsInvalidAxisValue(xValue, categoryAxisKind))
                {
                    continue;
                }

                double sizeValue;
                if (hasSizes)
                {
                    var size = sizeValues![i];
                    if (!size.HasValue || IsInvalidNumber(size.Value) || size.Value <= 0)
                    {
                        continue;
                    }

                    sizeValue = size.Value;
                }
                else
                {
                    sizeValue = minBubbleSize;
                }

                var radius = GetBubbleRadius(sizeValue, minBubbleSize, maxBubbleSize, style);
                if (radius <= 0f)
                {
                    continue;
                }

                var x = hasValidX
                    ? MapValueX(plot, xValue, minX, maxX, categoryAxisKind)
                    : MapX(plot, i, count);
                var y = MapY(plot, value.Value, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                var offset = radius + style.DataLabelOffset;
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, offset);
            }
        }

        private static void DrawColumnDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            int seriesCount,
            int categoryCount,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            if (categoryCount == 0)
            {
                return;
            }

            var groupWidth = plot.Width / categoryCount;
            var barWidth = groupWidth / Math.Max(1, seriesCount) * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;

            for (var i = 0; i < categoryCount && i < series.Values.Count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var x = plot.Left + (i * groupWidth) + offset + (seriesIndex * barWidth) + (barWidth / 2f);
                var y = MapY(plot, value.Value, minValue, maxValue, valueAxisKind);
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawBarDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            int seriesIndex,
            int seriesCount,
            int categoryCount,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed)
        {
            if (categoryCount == 0)
            {
                return;
            }

            var groupHeight = plot.Height / categoryCount;
            var barHeight = groupHeight / Math.Max(1, seriesCount) * 0.75f;
            var offset = (groupHeight - (barHeight * seriesCount)) / 2f;

            for (var i = 0; i < categoryCount && i < series.Values.Count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var y = plot.Top + (i * groupHeight) + offset + (seriesIndex * barHeight) + (barHeight / 2f);
                var valueX = MapValueX(plot, value.Value, minValue, maxValue, valueAxisKind);
                var x = valueX;
                var text = FormatDataLabel(series, seriesIndex, value.Value, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, true, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private static void DrawStackedColumnDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed,
            bool normalizeToPercent)
        {
            var categoryCount = snapshot.Categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return;
            }

            var groupWidth = plot.Width / categoryCount;
            var barWidth = groupWidth * 0.75f;
            var offset = (groupWidth - barWidth) / 2f;

            foreach (var stackedSeries in stacked)
            {
                var series = stackedSeries.Series;
                var count = Math.Min(categoryCount, series.Values.Count);
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                    {
                        continue;
                    }

                    var y0 = MapY(plot, stackedSeries.Start[i], minValue, maxValue, valueAxisKind);
                    var y1 = MapY(plot, stackedSeries.End[i], minValue, maxValue, valueAxisKind);
                    var y = (y0 + y1) / 2f;
                    var x = plot.Left + (i * groupWidth) + offset + (barWidth / 2f);
                    var text = FormatDataLabel(series, stackedSeries.SeriesIndex, value.Value, style);
                    TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
                }
            }
        }

        private static void DrawStackedBarDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed,
            bool normalizeToPercent)
        {
            var categoryCount = snapshot.Categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return;
            }

            var groupHeight = plot.Height / categoryCount;
            var barHeight = groupHeight * 0.75f;
            var offset = (groupHeight - barHeight) / 2f;

            foreach (var stackedSeries in stacked)
            {
                var series = stackedSeries.Series;
                var count = Math.Min(categoryCount, series.Values.Count);
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                    {
                        continue;
                    }

                    var x0 = MapValueX(plot, stackedSeries.Start[i], minValue, maxValue, valueAxisKind);
                    var x1 = MapValueX(plot, stackedSeries.End[i], minValue, maxValue, valueAxisKind);
                    var x = (x0 + x1) / 2f;
                    var y = plot.Top + (i * groupHeight) + offset + (barHeight / 2f);
                    var text = FormatDataLabel(series, stackedSeries.SeriesIndex, value.Value, style);
                    TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, true, style.DataLabelPadding, style.DataLabelOffset);
                }
            }
        }

        private static void DrawStackedAreaDataLabels(
            SKCanvas canvas,
            SKRect plot,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            List<SKRect> placed,
            bool normalizeToPercent)
        {
            var categoryCount = snapshot.Categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return;
            }

            foreach (var stackedSeries in stacked)
            {
                var series = stackedSeries.Series;
                var count = Math.Min(categoryCount, series.Values.Count);
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                    {
                        continue;
                    }

                    if (Math.Abs(value.Value) < double.Epsilon)
                    {
                        continue;
                    }

                    var mid = (stackedSeries.Start[i] + stackedSeries.End[i]) / 2d;
                    var x = MapX(plot, i, categoryCount);
                    var y = MapY(plot, mid, minValue, maxValue, valueAxisKind);
                    var text = FormatDataLabel(series, stackedSeries.SeriesIndex, value.Value, style);
                    TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, x, y, false, style.DataLabelPadding, style.DataLabelOffset);
                }
            }
        }

        private static void TryDrawLabelWithFallback(
            SKCanvas canvas,
            SKRect plot,
            List<SKRect> placed,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            string text,
            float anchorX,
            float anchorY,
            bool horizontal,
            float padding,
            float offset)
        {
            var bounds = new SKRect();
            textPaint.MeasureText(text, ref bounds);
            var width = bounds.Width + (padding * 2f);
            var height = bounds.Height + (padding * 2f);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (horizontal)
            {
                var rect = new SKRect(anchorX + offset, anchorY - (height / 2f), anchorX + offset + width, anchorY + (height / 2f));
                if (!TryPlaceLabel(canvas, plot, placed, textPaint, backgroundPaint, text, bounds, rect, padding))
                {
                    rect = new SKRect(anchorX - offset - width, anchorY - (height / 2f), anchorX - offset, anchorY + (height / 2f));
                    TryPlaceLabel(canvas, plot, placed, textPaint, backgroundPaint, text, bounds, rect, padding);
                }
            }
            else
            {
                var rect = new SKRect(anchorX - (width / 2f), anchorY - offset - height, anchorX + (width / 2f), anchorY - offset);
                if (!TryPlaceLabel(canvas, plot, placed, textPaint, backgroundPaint, text, bounds, rect, padding))
                {
                    rect = new SKRect(anchorX - (width / 2f), anchorY + offset, anchorX + (width / 2f), anchorY + offset + height);
                    TryPlaceLabel(canvas, plot, placed, textPaint, backgroundPaint, text, bounds, rect, padding);
                }
            }
        }

        private static void TryDrawCenteredLabel(
            SKCanvas canvas,
            SKRect plot,
            List<SKRect> placed,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            string text,
            float centerX,
            float centerY,
            float padding)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var bounds = new SKRect();
            textPaint.MeasureText(text, ref bounds);
            var width = bounds.Width + (padding * 2f);
            var height = bounds.Height + (padding * 2f);
            if (width <= 0f || height <= 0f)
            {
                return;
            }

            var rect = new SKRect(
                centerX - (width / 2f),
                centerY - (height / 2f),
                centerX + (width / 2f),
                centerY + (height / 2f));

            TryPlaceLabel(canvas, plot, placed, textPaint, backgroundPaint, text, bounds, rect, padding);
        }

        private static bool TryPlaceLabel(
            SKCanvas canvas,
            SKRect plot,
            List<SKRect> placed,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            string text,
            SKRect textBounds,
            SKRect rect,
            float padding)
        {
            if (!IsInside(plot, rect) || IntersectsAny(rect, placed))
            {
                return false;
            }

            if (backgroundPaint.Color.Alpha > 0)
            {
                canvas.DrawRoundRect(rect, 3f, 3f, backgroundPaint);
            }

            var textX = rect.Left + padding - textBounds.Left;
            var textY = rect.Top + padding - textBounds.Top;
            canvas.DrawText(text, textX, textY, textPaint);
            placed.Add(rect);
            return true;
        }

        private static bool IsInside(SKRect container, SKRect rect)
        {
            return rect.Left >= container.Left &&
                   rect.Right <= container.Right &&
                   rect.Top >= container.Top &&
                   rect.Bottom <= container.Bottom;
        }

        private static bool IntersectsAny(SKRect rect, List<SKRect> placed)
        {
            foreach (var existing in placed)
            {
                if (RectsIntersect(rect, existing))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RectsIntersect(SKRect a, SKRect b)
        {
            return a.Left < b.Right &&
                   a.Right > b.Left &&
                   a.Top < b.Bottom &&
                   a.Bottom > b.Top;
        }

        private static string FormatDataLabel(ChartSeriesSnapshot series, int seriesIndex, double value, SkiaChartStyle style)
        {
            if (series.DataLabelFormatter != null)
            {
                return series.DataLabelFormatter(value);
            }

            if (style.SeriesDataLabelFormatter != null)
            {
                return style.SeriesDataLabelFormatter(seriesIndex, value);
            }

            return style.DataLabelFormatter?.Invoke(value) ??
                   ChartValueFormatter.Format(
                       value,
                       series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary
                           ? style.SecondaryAxisValueFormat
                           : style.AxisValueFormat);
        }

    }
}
