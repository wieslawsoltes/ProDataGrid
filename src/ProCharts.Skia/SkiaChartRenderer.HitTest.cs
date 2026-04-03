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
        private static SkiaChartHitTestResult? HitTestCartesian(
            SKPoint point,
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
            double minBubbleSize,
            double maxBubbleSize,
            HistogramContext? histogramContext,
            BoxWhiskerContext? boxWhiskerContext)
        {
            var hitRadius = style.HitTestRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            SkiaChartHitTestResult? best = null;
            var bestDistance = float.MaxValue;
            var categoryCount = categories.Count;
            var seriesCount = snapshot.Series.Count;
            var stackedColumnsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Primary);
            var stackedColumnsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Secondary);
            var stackedColumns100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Primary);
            var stackedColumns100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Secondary);
            var stackedBarsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Primary);
            var stackedBarsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Secondary);
            var stackedBars100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Primary);
            var stackedBars100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Secondary);
            var stackedAreasPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Primary);
            var stackedAreasSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Secondary);
            var stackedAreas100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Primary);
            var stackedAreas100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Secondary);
            var stackedColumnsPrimaryChecked = false;
            var stackedColumnsSecondaryChecked = false;
            var stackedColumns100PrimaryChecked = false;
            var stackedColumns100SecondaryChecked = false;
            var stackedBarsPrimaryChecked = false;
            var stackedBarsSecondaryChecked = false;
            var stackedBars100PrimaryChecked = false;
            var stackedBars100SecondaryChecked = false;
            var stackedAreasPrimaryChecked = false;
            var stackedAreasSecondaryChecked = false;
            var stackedAreas100PrimaryChecked = false;
            var stackedAreas100SecondaryChecked = false;

            for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                {
                    continue;
                }

                var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                var axisMin = isSecondary ? minSecondaryValue : minValue;
                var axisMax = isSecondary ? maxSecondaryValue : maxValue;
                var axisKind = isSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;

                switch (series.Kind)
                {
                    case ChartSeriesKind.Column:
                        if (TryHitColumn(point, plot, categories, series, seriesIndex, seriesCount, categoryCount, axisMin, axisMax, axisKind, out var columnHit))
                        {
                            return columnHit;
                        }
                        break;
                    case ChartSeriesKind.Bar:
                        if (TryHitBar(point, plot, categories, series, seriesIndex, seriesCount, categoryCount, axisMin, axisMax, axisKind, out var barHit))
                        {
                            return barHit;
                        }
                        break;
                    case ChartSeriesKind.Candlestick:
                    case ChartSeriesKind.HollowCandlestick:
                    case ChartSeriesKind.HeikinAshi:
                    case ChartSeriesKind.Range:
                        if (TryHitCandlestick(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, out var candlestickHit))
                        {
                            return candlestickHit;
                        }
                        break;
                    case ChartSeriesKind.Ohlc:
                        if (TryHitOhlc(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, out var ohlcHit))
                        {
                            return ohlcHit;
                        }
                        break;
                    case ChartSeriesKind.Hlc:
                        if (TryHitHlc(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, out var hlcHit))
                        {
                            return hlcHit;
                        }
                        break;
                    case ChartSeriesKind.Renko:
                    case ChartSeriesKind.LineBreak:
                        if (TryHitFinancialBodySeries(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, useBoxWidth: true, includeWicks: false, out var financialBodyHit))
                        {
                            return financialBodyHit;
                        }
                        break;
                    case ChartSeriesKind.Kagi:
                        if (TryHitFinancialBodySeries(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, useBoxWidth: false, includeWicks: false, out var kagiHit))
                        {
                            return kagiHit;
                        }
                        break;
                    case ChartSeriesKind.PointFigure:
                        if (TryHitFinancialBodySeries(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, style, useBoxWidth: true, includeWicks: false, out var pointFigureHit))
                        {
                            return pointFigureHit;
                        }
                        break;
                    case ChartSeriesKind.Waterfall:
                        if (TryHitWaterfall(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, out var waterfallHit))
                        {
                            return waterfallHit;
                        }
                        break;
                    case ChartSeriesKind.Histogram:
                    case ChartSeriesKind.Pareto:
                        if (histogramContext != null &&
                            TryHitHistogram(point, plot, categories, histogramContext, seriesIndex, axisMin, axisMax, axisKind, out var histogramHit))
                        {
                            return histogramHit;
                        }
                        break;
                    case ChartSeriesKind.BoxWhisker:
                        if (boxWhiskerContext != null &&
                            TryHitBoxWhisker(point, plot, categories, boxWhiskerContext, seriesIndex, axisMin, axisMax, axisKind, style, out var boxHit))
                        {
                            return boxHit;
                        }
                        break;
                    case ChartSeriesKind.StackedColumn:
                        if (isSecondary)
                        {
                            if (!stackedColumnsSecondaryChecked)
                            {
                                stackedColumnsSecondaryChecked = true;
                                if (TryHitStackedColumn(point, plot, categories, snapshot, stackedColumnsSecondary, axisMin, axisMax, axisKind, normalizeToPercent: false, out var stackedColumnHit))
                                {
                                    return stackedColumnHit;
                                }
                            }
                        }
                        else if (!stackedColumnsPrimaryChecked)
                        {
                            stackedColumnsPrimaryChecked = true;
                            if (TryHitStackedColumn(point, plot, categories, snapshot, stackedColumnsPrimary, axisMin, axisMax, axisKind, normalizeToPercent: false, out var stackedColumnHit))
                            {
                                return stackedColumnHit;
                            }
                        }
                        break;
                    case ChartSeriesKind.StackedColumn100:
                        if (isSecondary)
                        {
                            if (!stackedColumns100SecondaryChecked)
                            {
                                stackedColumns100SecondaryChecked = true;
                                if (TryHitStackedColumn(point, plot, categories, snapshot, stackedColumns100Secondary, axisMin, axisMax, axisKind, normalizeToPercent: true, out var stackedColumnHit))
                                {
                                    return stackedColumnHit;
                                }
                            }
                        }
                        else if (!stackedColumns100PrimaryChecked)
                        {
                            stackedColumns100PrimaryChecked = true;
                            if (TryHitStackedColumn(point, plot, categories, snapshot, stackedColumns100Primary, axisMin, axisMax, axisKind, normalizeToPercent: true, out var stackedColumnHit))
                            {
                                return stackedColumnHit;
                            }
                        }
                        break;
                    case ChartSeriesKind.StackedBar:
                        if (isSecondary)
                        {
                            if (!stackedBarsSecondaryChecked)
                            {
                                stackedBarsSecondaryChecked = true;
                                if (TryHitStackedBar(point, plot, categories, snapshot, stackedBarsSecondary, axisMin, axisMax, axisKind, normalizeToPercent: false, out var stackedBarHit))
                                {
                                    return stackedBarHit;
                                }
                            }
                        }
                        else if (!stackedBarsPrimaryChecked)
                        {
                            stackedBarsPrimaryChecked = true;
                            if (TryHitStackedBar(point, plot, categories, snapshot, stackedBarsPrimary, axisMin, axisMax, axisKind, normalizeToPercent: false, out var stackedBarHit))
                            {
                                return stackedBarHit;
                            }
                        }
                        break;
                    case ChartSeriesKind.StackedBar100:
                        if (isSecondary)
                        {
                            if (!stackedBars100SecondaryChecked)
                            {
                                stackedBars100SecondaryChecked = true;
                                if (TryHitStackedBar(point, plot, categories, snapshot, stackedBars100Secondary, axisMin, axisMax, axisKind, normalizeToPercent: true, out var stackedBarHit))
                                {
                                    return stackedBarHit;
                                }
                            }
                        }
                        else if (!stackedBars100PrimaryChecked)
                        {
                            stackedBars100PrimaryChecked = true;
                            if (TryHitStackedBar(point, plot, categories, snapshot, stackedBars100Primary, axisMin, axisMax, axisKind, normalizeToPercent: true, out var stackedBarHit))
                            {
                                return stackedBarHit;
                            }
                        }
                        break;
                    case ChartSeriesKind.StackedArea:
                        if (isSecondary)
                        {
                            if (!stackedAreasSecondaryChecked)
                            {
                                stackedAreasSecondaryChecked = true;
                                if (TryHitStackedArea(point, plot, categories, snapshot, stackedAreasSecondary, axisMin, axisMax, axisKind, normalizeToPercent: false, hitRadiusSquared, ref best, ref bestDistance))
                                {
                                    return best;
                                }
                            }
                        }
                        else if (!stackedAreasPrimaryChecked)
                        {
                            stackedAreasPrimaryChecked = true;
                            if (TryHitStackedArea(point, plot, categories, snapshot, stackedAreasPrimary, axisMin, axisMax, axisKind, normalizeToPercent: false, hitRadiusSquared, ref best, ref bestDistance))
                            {
                                return best;
                            }
                        }
                        break;
                    case ChartSeriesKind.StackedArea100:
                        if (isSecondary)
                        {
                            if (!stackedAreas100SecondaryChecked)
                            {
                                stackedAreas100SecondaryChecked = true;
                                if (TryHitStackedArea(point, plot, categories, snapshot, stackedAreas100Secondary, axisMin, axisMax, axisKind, normalizeToPercent: true, hitRadiusSquared, ref best, ref bestDistance))
                                {
                                    return best;
                                }
                            }
                        }
                        else if (!stackedAreas100PrimaryChecked)
                        {
                            stackedAreas100PrimaryChecked = true;
                            if (TryHitStackedArea(point, plot, categories, snapshot, stackedAreas100Primary, axisMin, axisMax, axisKind, normalizeToPercent: true, hitRadiusSquared, ref best, ref bestDistance))
                            {
                                return best;
                            }
                        }
                        break;
                    case ChartSeriesKind.Scatter:
                        TryHitScatter(
                            point,
                            plot,
                            categories,
                            series,
                            seriesIndex,
                            axisMin,
                            axisMax,
                            useNumericCategoryAxis,
                            minCategory,
                            maxCategory,
                            categoryAxisKind,
                            axisKind,
                            hitRadiusSquared,
                            ref best,
                            ref bestDistance);
                        break;
                    case ChartSeriesKind.Bubble:
                        TryHitBubble(
                            point,
                            plot,
                            categories,
                            series,
                            seriesIndex,
                            axisMin,
                            axisMax,
                            useNumericCategoryAxis,
                            minCategory,
                            maxCategory,
                            categoryAxisKind,
                            minBubbleSize,
                            maxBubbleSize,
                            style,
                            ref best,
                            ref bestDistance);
                        break;
                    default:
                        TryHitLine(point, plot, categories, series, seriesIndex, axisMin, axisMax, axisKind, hitRadiusSquared, ref best, ref bestDistance);
                        break;
                }
            }

            return best;
        }

        private static void TryHitLine(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            float hitRadiusSquared,
            ref SkiaChartHitTestResult? best,
            ref float bestDistance)
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
                var distance = DistanceSquared(point, x, y);
                if (distance > hitRadiusSquared || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    value.Value,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(x, y));
            }
        }

        private static void TryHitScatter(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            ChartAxisKind categoryAxisKind,
            ChartAxisKind valueAxisKind,
            float hitRadiusSquared,
            ref SkiaChartHitTestResult? best,
            ref float bestDistance)
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
                var distance = DistanceSquared(point, x, y);
                if (distance > hitRadiusSquared || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    value.Value,
                    hasX ? xValues![i] : null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(x, y));
            }
        }

        private static void TryHitBubble(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            ChartAxisKind categoryAxisKind,
            double minBubbleSize,
            double maxBubbleSize,
            SkiaChartStyle style,
            ref SkiaChartHitTestResult? best,
            ref float bestDistance)
        {
            var count = series.Values.Count;
            var xValues = series.XValues;
            var hasValidX = false;
            var valueAxisKind = style.ValueAxisKind;
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
                var hitRadius = Math.Max(style.HitTestRadius, radius);
                var distance = DistanceSquared(point, x, y);
                if (distance > hitRadius * hitRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    value.Value,
                    hasValidX ? xValues![i] : null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(x, y));
            }
        }

        private static bool TryHitColumn(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            int seriesCount,
            int categoryCount,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            if (categoryCount == 0)
            {
                return false;
            }

            var groupWidth = plot.Width / categoryCount;
            var barWidth = groupWidth / Math.Max(1, seriesCount) * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;
            var baseline = valueAxisKind == ChartAxisKind.Logarithmic
                ? minValue
                : (minValue <= 0 && maxValue >= 0 ? 0d : minValue);
            var baselineY = MapY(plot, baseline, minValue, maxValue, valueAxisKind);

            for (var i = 0; i < categoryCount && i < series.Values.Count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var x = plot.Left + (i * groupWidth) + offset + (seriesIndex * barWidth);
                var y = MapY(plot, value.Value, minValue, maxValue, valueAxisKind);
                var top = Math.Min(y, baselineY);
                var bottom = Math.Max(y, baselineY);
                var rect = new SKRect(x, top, x + barWidth, bottom);
                if (!rect.Contains(point))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    value.Value,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(x + (barWidth / 2f), y));
                return true;
            }

            return false;
        }

        private static bool TryHitBar(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            int seriesCount,
            int categoryCount,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            if (categoryCount == 0)
            {
                return false;
            }

            var groupHeight = plot.Height / categoryCount;
            var barHeight = groupHeight / Math.Max(1, seriesCount) * 0.75f;
            var offset = (groupHeight - (barHeight * seriesCount)) / 2f;
            var baseline = valueAxisKind == ChartAxisKind.Logarithmic
                ? minValue
                : (minValue <= 0 && maxValue >= 0 ? 0d : minValue);
            var baselineX = MapValueX(plot, baseline, minValue, maxValue, valueAxisKind);

            for (var i = 0; i < categoryCount && i < series.Values.Count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var y = plot.Top + (i * groupHeight) + offset + (seriesIndex * barHeight);
                var valueX = MapValueX(plot, value.Value, minValue, maxValue, valueAxisKind);
                var left = Math.Min(baselineX, valueX);
                var right = Math.Max(baselineX, valueX);
                var rect = new SKRect(left, y, right, y + barHeight);
                if (!rect.Contains(point))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    value.Value,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(valueX, y + (barHeight / 2f)));
                return true;
            }

            return false;
        }

        private static bool TryHitWaterfall(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var categoryCount = categories.Count;
            if (categoryCount == 0)
            {
                return false;
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

                var start = running;
                var end = running + value.Value;
                var y0 = MapY(plot, start, minValue, maxValue, valueAxisKind);
                var y1 = MapY(plot, end, minValue, maxValue, valueAxisKind);
                var top = Math.Min(y0, y1);
                var bottom = Math.Max(y0, y1);
                var x = plot.Left + (i * groupWidth) + offset;
                var rect = new SKRect(x, top, x + barWidth, bottom);
                if (rect.Contains(point))
                {
                    hit = new SkiaChartHitTestResult(
                        seriesIndex,
                        i,
                        value.Value,
                        null,
                        GetCategory(categories, i),
                        series.Name,
                        series.Kind,
                        new SKPoint(x + (barWidth / 2f), (top + bottom) / 2f));
                    return true;
                }

                running = end;
            }

            return false;
        }

        private static bool TryHitCandlestick(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            out SkiaChartHitTestResult hit)
        {
            return TryHitFinancialBodySeries(point, plot, categories, series, seriesIndex, minValue, maxValue, valueAxisKind, style, useBoxWidth: false, includeWicks: true, out hit);
        }

        private static bool TryHitOhlc(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var count = GetFinancialPointCount(series, series.Kind);
            if (count == 0)
            {
                return false;
            }

            var span = GetFinancialCategorySpan(plot, count);
            var tickHalfWidth = count <= 1
                ? plot.Width * 0.08f
                : span * (float)Clamp(style.FinancialTickWidthRatio, 0.08f, 0.45f);
            tickHalfWidth = Math.Max(2f, Math.Min(plot.Width * 0.12f, tickHalfWidth));
            var hitRadius = Math.Max(style.HitTestRadius, tickHalfWidth);

            for (var i = 0; i < count; i++)
            {
                if (!TryGetFinancialPoint(series, series.Kind, i, valueAxisKind, out var open, out var high, out var low, out var close) ||
                    !open.HasValue)
                {
                    continue;
                }

                var centerX = MapX(plot, i, count);
                var yHigh = MapY(plot, high, minValue, maxValue, valueAxisKind);
                var yLow = MapY(plot, low, minValue, maxValue, valueAxisKind);
                var yOpen = MapY(plot, open.Value, minValue, maxValue, valueAxisKind);
                var yClose = MapY(plot, close, minValue, maxValue, valueAxisKind);
                var stemRect = new SKRect(centerX - style.HitTestRadius, Math.Min(yHigh, yLow), centerX + style.HitTestRadius, Math.Max(yHigh, yLow));
                var openRect = new SKRect(centerX - tickHalfWidth, yOpen - hitRadius, centerX, yOpen + hitRadius);
                var closeRect = new SKRect(centerX, yClose - hitRadius, centerX + tickHalfWidth, yClose + hitRadius);

                if (!stemRect.Contains(point) && !openRect.Contains(point) && !closeRect.Contains(point))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    close,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(centerX, yClose),
                    open.Value,
                    high,
                    low,
                    close);
                return true;
            }

            return false;
        }

        private static bool TryHitHlc(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var count = GetFinancialPointCount(series, series.Kind);
            if (count == 0)
            {
                return false;
            }

            var span = GetFinancialCategorySpan(plot, count);
            var tickHalfWidth = count <= 1
                ? plot.Width * 0.08f
                : span * (float)Clamp(style.FinancialTickWidthRatio, 0.08f, 0.45f);
            tickHalfWidth = Math.Max(2f, Math.Min(plot.Width * 0.12f, tickHalfWidth));
            var hitRadius = Math.Max(style.HitTestRadius, tickHalfWidth);

            for (var i = 0; i < count; i++)
            {
                if (!TryGetFinancialPoint(series, series.Kind, i, valueAxisKind, out _, out var high, out var low, out var close))
                {
                    continue;
                }

                var centerX = MapX(plot, i, count);
                var yHigh = MapY(plot, high, minValue, maxValue, valueAxisKind);
                var yLow = MapY(plot, low, minValue, maxValue, valueAxisKind);
                var yClose = MapY(plot, close, minValue, maxValue, valueAxisKind);
                var stemRect = new SKRect(centerX - style.HitTestRadius, Math.Min(yHigh, yLow), centerX + style.HitTestRadius, Math.Max(yHigh, yLow));
                var closeRect = new SKRect(centerX, yClose - hitRadius, centerX + tickHalfWidth, yClose + hitRadius);

                if (!stemRect.Contains(point) && !closeRect.Contains(point))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    close,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(centerX, yClose),
                    null,
                    high,
                    low,
                    close);
                return true;
            }

            return false;
        }

        private static bool TryHitFinancialBodySeries(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useBoxWidth,
            bool includeWicks,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var count = GetFinancialPointCount(series, series.Kind);
            if (count == 0)
            {
                return false;
            }

            var span = GetFinancialCategorySpan(plot, count);
            var widthRatio = useBoxWidth ? style.FinancialBoxWidthRatio : style.FinancialBodyWidthRatio;
            var bodyWidth = count <= 1
                ? plot.Width * (useBoxWidth ? 0.28f : 0.18f)
                : span * (float)Clamp(widthRatio, 0.1f, 0.95f);
            bodyWidth = Math.Max(3f, Math.Min(plot.Width * (useBoxWidth ? 0.35f : 0.25f), bodyWidth));
            var halfBodyWidth = bodyWidth / 2f;
            var hitHalfWidth = Math.Max(halfBodyWidth, style.HitTestRadius);

            for (var i = 0; i < count; i++)
            {
                if (!TryGetFinancialPoint(series, series.Kind, i, valueAxisKind, out var open, out var high, out var low, out var close))
                {
                    continue;
                }

                var openValue = open ?? close;
                var centerX = MapX(plot, i, count);
                var yHigh = MapY(plot, high, minValue, maxValue, valueAxisKind);
                var yLow = MapY(plot, low, minValue, maxValue, valueAxisKind);
                var yOpen = MapY(plot, openValue, minValue, maxValue, valueAxisKind);
                var yClose = MapY(plot, close, minValue, maxValue, valueAxisKind);
                var top = Math.Min(yOpen, yClose);
                var bottom = Math.Max(yOpen, yClose);
                if (bottom - top < 1f)
                {
                    top -= 0.5f;
                    bottom += 0.5f;
                }

                var expandedBodyRect = new SKRect(centerX - hitHalfWidth, top, centerX + hitHalfWidth, bottom);
                var wickRect = includeWicks
                    ? new SKRect(centerX - style.HitTestRadius, Math.Min(yHigh, yLow), centerX + style.HitTestRadius, Math.Max(yHigh, yLow))
                    : SKRect.Empty;

                if (!expandedBodyRect.Contains(point) && (!includeWicks || !wickRect.Contains(point)))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    close,
                    null,
                    GetCategory(categories, i),
                    series.Name,
                    series.Kind,
                    new SKPoint(centerX, (top + bottom) / 2f),
                    open,
                    high,
                    low,
                    close);
                return true;
            }

            return false;
        }

        private static bool TryHitHistogram(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            HistogramContext histogramContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            if (!histogramContext.TryGetSeries(seriesIndex, out var histogramSeries))
            {
                return false;
            }

            var binCount = histogramContext.BinCount;
            if (binCount == 0)
            {
                return false;
            }

            var seriesCount = Math.Max(1, histogramContext.Series.Count);
            var groupWidth = plot.Width / binCount;
            var barWidth = groupWidth / seriesCount * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;
            var baseline = valueAxisKind == ChartAxisKind.Logarithmic
                ? minValue
                : (minValue <= 0 && maxValue >= 0 ? 0d : minValue);
            var baselineY = MapY(plot, baseline, minValue, maxValue, valueAxisKind);

            for (var i = 0; i < binCount; i++)
            {
                var count = histogramSeries.Counts[i];
                var x = plot.Left + (i * groupWidth) + offset + (histogramSeries.OrderIndex * barWidth);
                var y = MapY(plot, count, minValue, maxValue, valueAxisKind);
                var top = Math.Min(y, baselineY);
                var bottom = Math.Max(y, baselineY);
                var rect = new SKRect(x, top, x + barWidth, bottom);
                if (!rect.Contains(point))
                {
                    continue;
                }

                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    i,
                    count,
                    null,
                    GetCategory(categories, i),
                    histogramSeries.Series.Name,
                    histogramSeries.Series.Kind,
                    new SKPoint(x + (barWidth / 2f), (top + bottom) / 2f));
                return true;
            }

            return false;
        }

        private static bool TryHitBoxWhisker(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            BoxWhiskerContext boxWhiskerContext,
            int seriesIndex,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            if (!boxWhiskerContext.TryGetSeries(seriesIndex, out var boxSeries))
            {
                return false;
            }

            var categoryCount = boxWhiskerContext.Categories.Count;
            if (categoryCount == 0)
            {
                return false;
            }

            var groupWidth = plot.Width / categoryCount;
            var boxWidth = groupWidth * 0.5f;
            var offset = (groupWidth - boxWidth) / 2f;
            var x = plot.Left + (boxSeries.OrderIndex * groupWidth) + offset;
            var centerX = x + (boxWidth / 2f);
            var stats = boxSeries.Stats;
            var yQ1 = MapY(plot, stats.Q1, minValue, maxValue, valueAxisKind);
            var yQ3 = MapY(plot, stats.Q3, minValue, maxValue, valueAxisKind);
            var top = Math.Min(yQ1, yQ3);
            var bottom = Math.Max(yQ1, yQ3);
            var boxRect = new SKRect(x, top, x + boxWidth, bottom);
            if (boxRect.Contains(point))
            {
                hit = new SkiaChartHitTestResult(
                    seriesIndex,
                    0,
                    stats.Median,
                    null,
                    GetCategory(categories, boxSeries.OrderIndex),
                    boxSeries.Series.Name,
                    boxSeries.Series.Kind,
                    new SKPoint(centerX, MapY(plot, stats.Median, minValue, maxValue, valueAxisKind)));
                return true;
            }

            var hitRadius = Math.Max(style.HitTestRadius, style.BoxWhiskerOutlierRadius);
            var hitRadiusSquared = hitRadius * hitRadius;
            foreach (var outlier in stats.Outliers)
            {
                var y = MapY(plot, outlier, minValue, maxValue, valueAxisKind);
                var distance = DistanceSquared(point, centerX, y);
                if (distance <= hitRadiusSquared)
                {
                    hit = new SkiaChartHitTestResult(
                        seriesIndex,
                        0,
                        outlier,
                        null,
                        GetCategory(categories, boxSeries.OrderIndex),
                        boxSeries.Series.Name,
                        boxSeries.Series.Kind,
                        new SKPoint(centerX, y));
                    return true;
                }
            }

            return false;
        }

        private static bool TryHitStackedColumn(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            bool normalizeToPercent,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var categoryCount = categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return false;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return false;
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
                    var top = Math.Min(y0, y1);
                    var bottom = Math.Max(y0, y1);
                    var x = plot.Left + (i * groupWidth) + offset;
                    var rect = new SKRect(x, top, x + barWidth, bottom);
                    if (rect.Contains(point))
                    {
                        hit = new SkiaChartHitTestResult(
                            stackedSeries.SeriesIndex,
                            i,
                            value.Value,
                            null,
                            GetCategory(categories, i),
                            series.Name,
                            series.Kind,
                            new SKPoint(x + (barWidth / 2f), (top + bottom) / 2f));
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryHitStackedBar(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            bool normalizeToPercent,
            out SkiaChartHitTestResult hit)
        {
            hit = default;
            var categoryCount = categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return false;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return false;
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
                    var left = Math.Min(x0, x1);
                    var right = Math.Max(x0, x1);
                    var y = plot.Top + (i * groupHeight) + offset;
                    var rect = new SKRect(left, y, right, y + barHeight);
                    if (rect.Contains(point))
                    {
                        hit = new SkiaChartHitTestResult(
                            stackedSeries.SeriesIndex,
                            i,
                            value.Value,
                            null,
                            GetCategory(categories, i),
                            series.Name,
                            series.Kind,
                            new SKPoint((left + right) / 2f, y + (barHeight / 2f)));
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryHitStackedArea(
            SKPoint point,
            SKRect plot,
            IReadOnlyList<string?> categories,
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            bool normalizeToPercent,
            float hitRadiusSquared,
            ref SkiaChartHitTestResult? best,
            ref float bestDistance)
        {
            var categoryCount = categories.Count;
            if (categoryCount == 0 || seriesIndices.Count == 0)
            {
                return false;
            }

            var stacked = BuildStackedSeriesValues(snapshot, seriesIndices, valueAxisKind, normalizeToPercent);
            if (stacked.Count == 0)
            {
                return false;
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

                    var x = MapX(plot, i, categoryCount);
                    var y = MapY(plot, stackedSeries.End[i], minValue, maxValue, valueAxisKind);
                    var distance = DistanceSquared(point, x, y);
                    if (distance > hitRadiusSquared || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    best = new SkiaChartHitTestResult(
                        stackedSeries.SeriesIndex,
                        i,
                        value.Value,
                        null,
                        GetCategory(categories, i),
                        series.Name,
                        series.Kind,
                        new SKPoint(x, y));
                }
            }

            return best.HasValue;
        }

        private static SkiaChartHitTestResult? HitTestPie(
            SKPoint point,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            var pieSeries = CollectPieSeries(snapshot);
            try
            {
                if (pieSeries.Count == 0)
                {
                    return null;
                }

                var center = new SKPoint(plot.MidX, plot.MidY);
                var radius = Math.Min(plot.Width, plot.Height) * 0.45f;
                if (radius <= 0)
                {
                    return null;
                }

                var dx = point.X - center.X;
                var dy = point.Y - center.Y;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > radius)
                {
                    return null;
                }

                var angle = Math.Atan2(dy, dx) * 180d / Math.PI;
                var normalized = (angle + 90d + 360d) % 360d;
                var ringThickness = radius / pieSeries.Count;
                var outerRadius = radius;
                var categories = snapshot.Categories;

                foreach (var info in pieSeries)
                {
                    var series = info.Series;
                    var total = 0d;
                    foreach (var value in series.Values)
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

                    if (distance < innerRadius || distance > outerRadius)
                    {
                        outerRadius -= ringThickness;
                        continue;
                    }

                    var start = 0d;
                    for (var i = 0; i < series.Values.Count; i++)
                    {
                        var value = series.Values[i];
                        if (!value.HasValue || IsInvalidNumber(value.Value) || Math.Abs(value.Value) <= double.Epsilon)
                        {
                            continue;
                        }

                        var sweep = Math.Abs(value.Value) / total * 360d;
                        var end = start + sweep;
                        if (normalized >= start && normalized <= end)
                        {
                            var category = GetCategory(categories, i);
                            return new SkiaChartHitTestResult(
                                info.SeriesIndex,
                                i,
                                value.Value,
                                null,
                                category,
                                series.Name,
                                series.Kind,
                                new SKPoint(point.X, point.Y));
                        }

                        start = end;
                    }

                    outerRadius -= ringThickness;
                }

                return null;
            }
            finally
            {
                SkiaChartPools.ReturnList(pieSeries);
            }
        }

        private static SkiaChartHitTestResult? HitTestRadar(
            SKPoint point,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
            var categories = snapshot.Categories;
            var categoryCount = categories.Count;
            if (categoryCount == 0)
            {
                foreach (var series in snapshot.Series)
                {
                    categoryCount = Math.Max(categoryCount, series.Values.Count);
                }
            }

            if (categoryCount == 0)
            {
                return null;
            }

            var center = new SKPoint(plot.MidX, plot.MidY);
            var labelMargin = style.ShowCategoryLabels ? style.LabelSize * 2f : 0f;
            var radius = (Math.Min(plot.Width, plot.Height) / 2f) - labelMargin;
            if (radius <= 0f)
            {
                return null;
            }

            var minValue = style.ValueAxisMinimum ?? 0d;
            var maxValue = style.ValueAxisMaximum ?? GetMaxSeriesValue(snapshot);
            if (maxValue <= minValue || Math.Abs(maxValue - minValue) < double.Epsilon)
            {
                maxValue = minValue + 1d;
            }

            var hitRadius = style.HitTestRadius;
            var hitRadiusSquared = hitRadius * hitRadius;
            SkiaChartHitTestResult? best = null;
            var bestDistance = float.MaxValue;

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind != ChartSeriesKind.Radar)
                {
                    continue;
                }

                for (var i = 0; i < categoryCount; i++)
                {
                    var value = i < series.Values.Count ? series.Values[i] : null;
                    if (!value.HasValue || IsInvalidNumber(value.Value))
                    {
                        continue;
                    }

                    var normalized = Clamp((value.Value - minValue) / (maxValue - minValue), 0d, 1d);
                    var angle = GetRadarAngle(i, categoryCount);
                    var x = center.X + (float)Math.Cos(angle) * (float)(radius * normalized);
                    var y = center.Y + (float)Math.Sin(angle) * (float)(radius * normalized);
                    var distance = DistanceSquared(point, x, y);
                    if (distance > hitRadiusSquared || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    best = new SkiaChartHitTestResult(
                        seriesIndex,
                        i,
                        value.Value,
                        null,
                        GetCategory(categories, i),
                        series.Name,
                        series.Kind,
                        new SKPoint(x, y));
                }
            }

            return best;
        }

        private static SkiaChartHitTestResult? HitTestFunnel(
            SKPoint point,
            SKRect plot,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style)
        {
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
                return null;
            }

            var categories = snapshot.Categories;
            var count = categories.Count > 0
                ? Math.Min(categories.Count, funnelSeries.Values.Count)
                : funnelSeries.Values.Count;
            if (count == 0)
            {
                return null;
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
                return null;
            }

            var gap = Math.Max(0f, style.FunnelGap);
            var totalGap = gap * Math.Max(0, count - 1);
            var segmentHeight = (plot.Height - totalGap) / count;
            if (segmentHeight <= 0f)
            {
                return null;
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

                    if (path.Contains(point.X, point.Y))
                    {
                        var hitValue = value.HasValue ? value.Value : 0d;
                        return new SkiaChartHitTestResult(
                            seriesIndex,
                            i,
                            hitValue,
                            null,
                            GetCategory(categories, i),
                            funnelSeries.Name,
                            funnelSeries.Kind,
                            new SKPoint(point.X, point.Y));
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnPath(path);
                }

                currentTop = bottomY + gap;
                previousWidth = width;
            }

            return null;
        }

        private static float DistanceSquared(SKPoint point, float x, float y)
        {
            var dx = point.X - x;
            var dy = point.Y - y;
            return (dx * dx) + (dy * dy);
        }

        private static string? GetCategory(IReadOnlyList<string?> categories, int index)
        {
            if (index < 0 || index >= categories.Count)
            {
                return null;
            }

            return categories[index];
        }

    }
}
