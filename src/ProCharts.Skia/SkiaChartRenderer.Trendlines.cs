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
        private static void DrawTrendlines(
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
            double maxCategory)
        {
            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.TrendlineType == ChartTrendlineType.None)
                {
                    continue;
                }

                if (series.Kind == ChartSeriesKind.Pie ||
                    series.Kind == ChartSeriesKind.Donut ||
                    series.Kind == ChartSeriesKind.StackedColumn ||
                    series.Kind == ChartSeriesKind.StackedBar ||
                    series.Kind == ChartSeriesKind.StackedArea ||
                    series.Kind == ChartSeriesKind.StackedColumn100 ||
                    series.Kind == ChartSeriesKind.StackedBar100 ||
                    series.Kind == ChartSeriesKind.StackedArea100 ||
                    series.Kind == ChartSeriesKind.Histogram ||
                    series.Kind == ChartSeriesKind.Pareto ||
                    series.Kind == ChartSeriesKind.BoxWhisker ||
                    series.Kind == ChartSeriesKind.Candlestick ||
                    series.Kind == ChartSeriesKind.HollowCandlestick ||
                    series.Kind == ChartSeriesKind.Ohlc ||
                    series.Kind == ChartSeriesKind.Hlc ||
                    series.Kind == ChartSeriesKind.HeikinAshi ||
                    series.Kind == ChartSeriesKind.Renko ||
                    series.Kind == ChartSeriesKind.Range ||
                    series.Kind == ChartSeriesKind.LineBreak ||
                    series.Kind == ChartSeriesKind.Kagi ||
                    series.Kind == ChartSeriesKind.PointFigure ||
                    series.Kind == ChartSeriesKind.Waterfall ||
                    series.Kind == ChartSeriesKind.Radar ||
                    series.Kind == ChartSeriesKind.Funnel)
                {
                    continue;
                }

                var seriesIsSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                var seriesMinValue = seriesIsSecondary ? minSecondaryValue : minValue;
                var seriesMaxValue = seriesIsSecondary ? maxSecondaryValue : maxValue;
                var seriesAxisKind = seriesIsSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;
                var isBar = series.Kind == ChartSeriesKind.Bar;
                var overrides = GetSeriesStyleOverrides(style, seriesIndex);
                var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
                var color = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);

                using var linePaint = new SKPaint
                {
                    Color = color,
                    StrokeWidth = style.TrendlineStrokeWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0)
                };

                switch (series.TrendlineType)
                {
                    case ChartTrendlineType.Linear:
                        if (!TryDrawLinearTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint))
                        {
                            continue;
                        }
                        break;
                    case ChartTrendlineType.Exponential:
                        if (!TryDrawExponentialTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint))
                        {
                            continue;
                        }
                        break;
                    case ChartTrendlineType.Logarithmic:
                        if (!TryDrawLogarithmicTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint))
                        {
                            continue;
                        }
                        break;
                    case ChartTrendlineType.Polynomial:
                        if (!TryDrawPolynomialTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint))
                        {
                            continue;
                        }
                        break;
                    case ChartTrendlineType.Power:
                        if (!TryDrawPowerTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint))
                        {
                            continue;
                        }
                        break;
                    case ChartTrendlineType.MovingAverage:
                        TryDrawMovingAverageTrendline(canvas, plot, series, categories, seriesMinValue, seriesMaxValue, seriesAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, isBar, linePaint);
                        break;
                }
            }
        }

        private static void DrawErrorBars(
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
            double maxCategory)
        {
            var categoryCount = categories.Count;
            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.ErrorBarType == ChartErrorBarType.None || series.ErrorBarValue <= 0d)
                {
                    continue;
                }

                if (series.Kind == ChartSeriesKind.Pie ||
                    series.Kind == ChartSeriesKind.Donut ||
                    series.Kind == ChartSeriesKind.StackedColumn ||
                    series.Kind == ChartSeriesKind.StackedBar ||
                    series.Kind == ChartSeriesKind.StackedArea ||
                    series.Kind == ChartSeriesKind.StackedColumn100 ||
                    series.Kind == ChartSeriesKind.StackedBar100 ||
                    series.Kind == ChartSeriesKind.StackedArea100 ||
                    series.Kind == ChartSeriesKind.Histogram ||
                    series.Kind == ChartSeriesKind.Pareto ||
                    series.Kind == ChartSeriesKind.BoxWhisker ||
                    series.Kind == ChartSeriesKind.Candlestick ||
                    series.Kind == ChartSeriesKind.HollowCandlestick ||
                    series.Kind == ChartSeriesKind.Ohlc ||
                    series.Kind == ChartSeriesKind.Hlc ||
                    series.Kind == ChartSeriesKind.HeikinAshi ||
                    series.Kind == ChartSeriesKind.Renko ||
                    series.Kind == ChartSeriesKind.Range ||
                    series.Kind == ChartSeriesKind.LineBreak ||
                    series.Kind == ChartSeriesKind.Kagi ||
                    series.Kind == ChartSeriesKind.PointFigure ||
                    series.Kind == ChartSeriesKind.Waterfall ||
                    series.Kind == ChartSeriesKind.Radar ||
                    series.Kind == ChartSeriesKind.Funnel)
                {
                    continue;
                }

                var seriesIsSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                var seriesMinValue = seriesIsSecondary ? minSecondaryValue : minValue;
                var seriesMaxValue = seriesIsSecondary ? maxSecondaryValue : maxValue;
                var seriesAxisKind = seriesIsSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;
                var isBar = series.Kind == ChartSeriesKind.Bar;
                var isScatter = series.Kind == ChartSeriesKind.Scatter || series.Kind == ChartSeriesKind.Bubble;
                var hasX = isScatter && series.XValues != null && series.XValues.Count == series.Values.Count;
                var hasValidX = false;
                double minX = 0;
                double maxX = 1;
                if (hasX)
                {
                    hasValidX = TryGetScatterAxisRange(series, useNumericCategoryAxis, minCategory, maxCategory, categoryAxisKind, out minX, out maxX);
                }

                var standardDeviation = 0d;
                var standardError = 0d;
                if (series.ErrorBarType == ChartErrorBarType.StandardDeviation)
                {
                    standardDeviation = ComputeStandardDeviation(series.Values, seriesAxisKind) * series.ErrorBarValue;
                }
                else if (series.ErrorBarType == ChartErrorBarType.StandardError)
                {
                    standardError = ComputeStandardError(series.Values, seriesAxisKind) * series.ErrorBarValue;
                }

                var overrides = GetSeriesStyleOverrides(style, seriesIndex);
                var themeStyle = GetThemeSeriesStyle(style, seriesIndex);
                var color = ResolveSeriesStrokeColor(style, seriesIndex, overrides, themeStyle);

                using var paint = new SKPaint
                {
                    Color = color,
                    StrokeWidth = style.ErrorBarStrokeWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };

                var capSize = style.ErrorBarCapSize;
                var count = series.Values.Count;
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, seriesAxisKind))
                    {
                        continue;
                    }

                    double error = series.ErrorBarType switch
                    {
                        ChartErrorBarType.Fixed => series.ErrorBarValue,
                        ChartErrorBarType.Percentage => Math.Abs(value.Value) * (series.ErrorBarValue / 100d),
                        ChartErrorBarType.StandardDeviation => standardDeviation,
                        ChartErrorBarType.StandardError => standardError,
                        _ => 0d
                    };

                    if (error <= 0d || double.IsNaN(error) || double.IsInfinity(error))
                    {
                        continue;
                    }

                    var low = value.Value - error;
                    var high = value.Value + error;

                    if (isBar)
                    {
                        var y = MapCategoryY(plot, i, Math.Max(1, categoryCount));
                        var xLow = MapValueX(plot, low, seriesMinValue, seriesMaxValue, seriesAxisKind);
                        var xHigh = MapValueX(plot, high, seriesMinValue, seriesMaxValue, seriesAxisKind);
                        canvas.DrawLine(xLow, y, xHigh, y, paint);
                        canvas.DrawLine(xLow, y - capSize / 2f, xLow, y + capSize / 2f, paint);
                        canvas.DrawLine(xHigh, y - capSize / 2f, xHigh, y + capSize / 2f, paint);
                        continue;
                    }

                    if (isScatter && hasX && !hasValidX)
                    {
                        continue;
                    }

                    var x = isScatter && hasX && hasValidX
                        ? MapValueX(plot, series.XValues![i], minX, maxX, categoryAxisKind)
                        : MapX(plot, i, Math.Max(1, categoryCount));
                    var yLow = MapY(plot, low, seriesMinValue, seriesMaxValue, seriesAxisKind);
                    var yHigh = MapY(plot, high, seriesMinValue, seriesMaxValue, seriesAxisKind);
                    canvas.DrawLine(x, yLow, x, yHigh, paint);
                    canvas.DrawLine(x - capSize / 2f, yLow, x + capSize / 2f, yLow, paint);
                    canvas.DrawLine(x - capSize / 2f, yHigh, x + capSize / 2f, yHigh, paint);
                }
            }
        }

        private static bool TryDrawLinearTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var count = series.Values.Count;
            if (count < 2)
            {
                return false;
            }

            var xValues = series.XValues;
            var hasX = (series.Kind == ChartSeriesKind.Scatter || series.Kind == ChartSeriesKind.Bubble) &&
                       xValues != null &&
                       xValues.Count == count;

            var xs = SkiaChartPools.RentList<double>();
            var ys = SkiaChartPools.RentList<double>();
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                    {
                        continue;
                    }

                    var x = hasX ? xValues![i] : i;
                    if (hasX && IsInvalidAxisValue(x, categoryAxisKind))
                    {
                        continue;
                    }

                    xs.Add(x);
                    ys.Add(value.Value);
                }

                if (xs.Count < 2)
                {
                    return false;
                }

                var n = xs.Count;
                var sumX = 0d;
                var sumY = 0d;
                var sumXY = 0d;
                var sumX2 = 0d;
                for (var i = 0; i < n; i++)
                {
                    var x = xs[i];
                    var y = ys[i];
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                }

                var denom = (n * sumX2) - (sumX * sumX);
                if (Math.Abs(denom) < double.Epsilon)
                {
                    return false;
                }

                var slope = ((n * sumXY) - (sumX * sumY)) / denom;
                var intercept = (sumY - (slope * sumX)) / n;

                if (hasX)
                {
                    if (!TryGetScatterAxisRange(series, useNumericCategoryAxis, minCategory, maxCategory, categoryAxisKind, out var minX, out var maxX))
                    {
                        minX = xs[0];
                        maxX = xs[0];
                        for (var i = 1; i < xs.Count; i++)
                        {
                            minX = Math.Min(minX, xs[i]);
                            maxX = Math.Max(maxX, xs[i]);
                        }

                        if (Math.Abs(maxX - minX) < double.Epsilon)
                        {
                            maxX = minX + 1d;
                        }
                    }

                    var y0 = slope * minX + intercept;
                    var y1 = slope * maxX + intercept;
                    if (IsInvalidAxisValue(y0, valueAxisKind) || IsInvalidAxisValue(y1, valueAxisKind))
                    {
                        return false;
                    }

                    var x0 = MapValueX(plot, minX, minX, maxX, categoryAxisKind);
                    var x1 = MapValueX(plot, maxX, minX, maxX, categoryAxisKind);
                    var screenY0 = MapY(plot, y0, minValue, maxValue, valueAxisKind);
                    var screenY1 = MapY(plot, y1, minValue, maxValue, valueAxisKind);
                    canvas.DrawLine(x0, screenY0, x1, screenY1, linePaint);
                    return true;
                }

                var categoryCount = Math.Max(1, categories.Count > 0 ? categories.Count : count);
                var path = SkiaChartPools.RentPath();
                try
                {
                    var hasPath = false;
                    for (var i = 0; i < count; i++)
                    {
                        var predicted = slope * i + intercept;
                        if (IsInvalidAxisValue(predicted, valueAxisKind))
                        {
                            continue;
                        }

                        var x = isBar ? MapValueX(plot, predicted, minValue, maxValue, valueAxisKind) : MapX(plot, i, categoryCount);
                        var y = isBar ? MapCategoryY(plot, i, categoryCount) : MapY(plot, predicted, minValue, maxValue, valueAxisKind);
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

                    return hasPath;
                }
                finally
                {
                    SkiaChartPools.ReturnPath(path);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(xs);
                SkiaChartPools.ReturnList(ys);
            }
        }

        private static bool TryDrawExponentialTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var xs = SkiaChartPools.RentList<double>();
            var ys = SkiaChartPools.RentList<double>();
            try
            {
                if (!TryCollectTrendlinePoints(series, valueAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, xs, ys, out var hasX, out var minX, out var maxX))
                {
                    return false;
                }

                var logXs = SkiaChartPools.RentList<double>(xs.Count);
                var logYs = SkiaChartPools.RentList<double>(ys.Count);
                try
                {
                    for (var i = 0; i < ys.Count && i < xs.Count; i++)
                    {
                        var y = ys[i];
                        if (y <= 0d || double.IsNaN(y) || double.IsInfinity(y))
                        {
                            continue;
                        }

                        logXs.Add(xs[i]);
                        logYs.Add(Math.Log(y));
                    }

                    if (logYs.Count < 2 || logXs.Count != logYs.Count)
                    {
                        return false;
                    }

                    if (!TryComputeLinearRegression(logXs, logYs, out var slope, out var intercept))
                    {
                        return false;
                    }

                    return DrawTrendlinePath(
                        canvas,
                        plot,
                        series,
                        categories,
                        minValue,
                        maxValue,
                        valueAxisKind,
                        style,
                        isBar,
                        linePaint,
                        x => Math.Exp(intercept + slope * x),
                        hasX,
                        minX,
                        maxX,
                        categoryAxisKind);
                }
                finally
                {
                    SkiaChartPools.ReturnList(logXs);
                    SkiaChartPools.ReturnList(logYs);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(xs);
                SkiaChartPools.ReturnList(ys);
            }
        }

        private static bool TryDrawLogarithmicTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var xs = SkiaChartPools.RentList<double>();
            var ys = SkiaChartPools.RentList<double>();
            try
            {
                if (!TryCollectTrendlinePoints(series, valueAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, xs, ys, out var hasX, out var minX, out var maxX))
                {
                    return false;
                }

                var logXs = SkiaChartPools.RentList<double>(xs.Count);
                var logYs = SkiaChartPools.RentList<double>(ys.Count);
                try
                {
                    var rawMinX = double.MaxValue;
                    var rawMaxX = double.MinValue;
                    for (var i = 0; i < xs.Count; i++)
                    {
                        var x = xs[i];
                        if (x <= 0d || double.IsNaN(x) || double.IsInfinity(x))
                        {
                            continue;
                        }

                        logXs.Add(Math.Log(x));
                        logYs.Add(ys[i]);
                        rawMinX = Math.Min(rawMinX, x);
                        rawMaxX = Math.Max(rawMaxX, x);
                    }

                    if (logXs.Count < 2 || rawMinX == double.MaxValue || rawMaxX == double.MinValue)
                    {
                        return false;
                    }

                    minX = rawMinX;
                    maxX = rawMaxX;

                    if (!TryComputeLinearRegression(logXs, logYs, out var slope, out var intercept))
                    {
                        return false;
                    }

                    return DrawTrendlinePath(
                        canvas,
                        plot,
                        series,
                        categories,
                        minValue,
                        maxValue,
                        valueAxisKind,
                        style,
                        isBar,
                        linePaint,
                        x => x > 0d ? (slope * Math.Log(x) + intercept) : double.NaN,
                        hasX,
                        minX,
                        maxX,
                        categoryAxisKind);
                }
                finally
                {
                    SkiaChartPools.ReturnList(logXs);
                    SkiaChartPools.ReturnList(logYs);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(xs);
                SkiaChartPools.ReturnList(ys);
            }
        }

        private static bool TryDrawPowerTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var xs = SkiaChartPools.RentList<double>();
            var ys = SkiaChartPools.RentList<double>();
            try
            {
                if (!TryCollectTrendlinePoints(series, valueAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, xs, ys, out var hasX, out var minX, out var maxX))
                {
                    return false;
                }

                var logXs = SkiaChartPools.RentList<double>(xs.Count);
                var logYs = SkiaChartPools.RentList<double>(ys.Count);
                try
                {
                    var rawMinX = double.MaxValue;
                    var rawMaxX = double.MinValue;
                    for (var i = 0; i < xs.Count; i++)
                    {
                        var x = xs[i];
                        var y = ys[i];
                        if (x <= 0d || y <= 0d || double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                        {
                            continue;
                        }

                        logXs.Add(Math.Log(x));
                        logYs.Add(Math.Log(y));
                        rawMinX = Math.Min(rawMinX, x);
                        rawMaxX = Math.Max(rawMaxX, x);
                    }

                    if (logXs.Count < 2 || rawMinX == double.MaxValue || rawMaxX == double.MinValue)
                    {
                        return false;
                    }

                    minX = rawMinX;
                    maxX = rawMaxX;

                    if (!TryComputeLinearRegression(logXs, logYs, out var slope, out var intercept))
                    {
                        return false;
                    }

                    var scale = Math.Exp(intercept);
                    return DrawTrendlinePath(
                        canvas,
                        plot,
                        series,
                        categories,
                        minValue,
                        maxValue,
                        valueAxisKind,
                        style,
                        isBar,
                        linePaint,
                        x => x > 0d ? scale * Math.Pow(x, slope) : double.NaN,
                        hasX,
                        minX,
                        maxX,
                        categoryAxisKind);
                }
                finally
                {
                    SkiaChartPools.ReturnList(logXs);
                    SkiaChartPools.ReturnList(logYs);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(xs);
                SkiaChartPools.ReturnList(ys);
            }
        }

        private static bool TryDrawPolynomialTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var xs = SkiaChartPools.RentList<double>();
            var ys = SkiaChartPools.RentList<double>();
            try
            {
                if (!TryCollectTrendlinePoints(series, valueAxisKind, style, useNumericCategoryAxis, categoryAxisKind, minCategory, maxCategory, xs, ys, out var hasX, out var minX, out var maxX))
                {
                    return false;
                }

                var degree = Clamp(series.TrendlinePeriod, 2, 6);
                if (!TryComputePolynomial(xs, ys, degree, out var coefficients))
                {
                    return false;
                }

                return DrawTrendlinePath(
                    canvas,
                    plot,
                    series,
                    categories,
                    minValue,
                    maxValue,
                    valueAxisKind,
                    style,
                    isBar,
                    linePaint,
                    x => EvaluatePolynomial(coefficients, x),
                    hasX,
                    minX,
                    maxX,
                    categoryAxisKind);
            }
            finally
            {
                SkiaChartPools.ReturnList(xs);
                SkiaChartPools.ReturnList(ys);
            }
        }

        private static bool TryCollectTrendlinePoints(
            ChartSeriesSnapshot series,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            List<double> xs,
            List<double> ys,
            out bool hasX,
            out double minX,
            out double maxX)
        {
            xs.Clear();
            ys.Clear();
            minX = 0d;
            maxX = 1d;
            hasX = false;

            var count = series.Values.Count;
            if (count < 2)
            {
                return false;
            }

            var xValues = series.XValues;
            hasX = (series.Kind == ChartSeriesKind.Scatter || series.Kind == ChartSeriesKind.Bubble) &&
                   xValues != null &&
                   xValues.Count == count;

            for (var i = 0; i < count; i++)
            {
                var value = series.Values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                {
                    continue;
                }

                var x = hasX ? xValues![i] : i;
                if (hasX && IsInvalidAxisValue(x, categoryAxisKind))
                {
                    continue;
                }

                xs.Add(x);
                ys.Add(value.Value);
            }

            if (xs.Count < 2)
            {
                return false;
            }

            if (hasX)
            {
                var hasValidX = TryGetScatterAxisRange(series, useNumericCategoryAxis, minCategory, maxCategory, categoryAxisKind, out minX, out maxX);
                if (!hasValidX)
                {
                    minX = xs[0];
                    maxX = xs[0];
                    for (var i = 1; i < xs.Count; i++)
                    {
                        minX = Math.Min(minX, xs[i]);
                        maxX = Math.Max(maxX, xs[i]);
                    }

                    if (Math.Abs(maxX - minX) < double.Epsilon)
                    {
                        maxX = minX + 1d;
                    }
                }
            }

            return true;
        }

        private static bool DrawTrendlinePath(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool isBar,
            SKPaint linePaint,
            Func<double, double> predictor,
            bool hasX,
            double minX,
            double maxX,
            ChartAxisKind categoryAxisKind)
        {
            var categoryCount = Math.Max(1, categories.Count > 0 ? categories.Count : series.Values.Count);
            var path = SkiaChartPools.RentPath();
            try
            {
                var hasPath = false;
                if (hasX)
                {
                    if (IsInvalidAxisValue(minX, categoryAxisKind) || IsInvalidAxisValue(maxX, categoryAxisKind))
                    {
                        return false;
                    }

                    if (maxX <= minX || Math.Abs(maxX - minX) < double.Epsilon)
                    {
                        maxX = minX + 1d;
                    }

                    var steps = Math.Max(8, Math.Min(128, series.Values.Count * 4));
                    for (var i = 0; i < steps; i++)
                    {
                        var t = steps == 1 ? 0d : i / (double)(steps - 1);
                        var xValue = minX + (maxX - minX) * t;
                        if (IsInvalidAxisValue(xValue, categoryAxisKind))
                        {
                            continue;
                        }

                        var predicted = predictor(xValue);
                        if (IsInvalidAxisValue(predicted, valueAxisKind))
                        {
                            continue;
                        }

                        var x = MapValueX(plot, xValue, minX, maxX, categoryAxisKind);
                        var y = MapY(plot, predicted, minValue, maxValue, valueAxisKind);
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
                }
                else
                {
                    var count = series.Values.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var predicted = predictor(i);
                        if (IsInvalidAxisValue(predicted, valueAxisKind))
                        {
                            continue;
                        }

                        var x = isBar ? MapValueX(plot, predicted, minValue, maxValue, valueAxisKind) : MapX(plot, i, categoryCount);
                        var y = isBar ? MapCategoryY(plot, i, categoryCount) : MapY(plot, predicted, minValue, maxValue, valueAxisKind);
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
                }

                if (hasPath)
                {
                    canvas.DrawPath(path, linePaint);
                }

                return hasPath;
            }
            finally
            {
                SkiaChartPools.ReturnPath(path);
            }
        }

        private static bool TryComputeLinearRegression(
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            out double slope,
            out double intercept)
        {
            slope = 0d;
            intercept = 0d;
            if (xs.Count != ys.Count || xs.Count < 2)
            {
                return false;
            }

            var n = xs.Count;
            var sumX = 0d;
            var sumY = 0d;
            var sumXY = 0d;
            var sumX2 = 0d;
            for (var i = 0; i < n; i++)
            {
                var x = xs[i];
                var y = ys[i];
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var denom = (n * sumX2) - (sumX * sumX);
            if (Math.Abs(denom) < double.Epsilon)
            {
                return false;
            }

            slope = ((n * sumXY) - (sumX * sumY)) / denom;
            intercept = (sumY - (slope * sumX)) / n;
            return true;
        }

        private static bool TryComputePolynomial(
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            int degree,
            out double[] coefficients)
        {
            coefficients = Array.Empty<double>();
            if (xs.Count != ys.Count || xs.Count < degree + 1)
            {
                return false;
            }

            var order = degree + 1;
            var matrix = new double[order, order];
            var vector = new double[order];
            var sums = new double[(degree * 2) + 1];

            for (var i = 0; i < xs.Count; i++)
            {
                var x = xs[i];
                var y = ys[i];
                var xPower = 1d;
                for (var k = 0; k < sums.Length; k++)
                {
                    sums[k] += xPower;
                    xPower *= x;
                }

                xPower = 1d;
                for (var k = 0; k < order; k++)
                {
                    vector[k] += y * xPower;
                    xPower *= x;
                }
            }

            for (var row = 0; row < order; row++)
            {
                for (var col = 0; col < order; col++)
                {
                    matrix[row, col] = sums[row + col];
                }
            }

            if (!SolveLinearSystem(matrix, vector, out coefficients))
            {
                coefficients = Array.Empty<double>();
                return false;
            }

            return true;
        }

        private static bool SolveLinearSystem(double[,] matrix, double[] vector, out double[] solution)
        {
            var size = vector.Length;
            solution = new double[size];
            var a = (double[,])matrix.Clone();
            var b = (double[])vector.Clone();

            for (var i = 0; i < size; i++)
            {
                var maxRow = i;
                var maxValue = Math.Abs(a[i, i]);
                for (var row = i + 1; row < size; row++)
                {
                    var value = Math.Abs(a[row, i]);
                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxRow = row;
                    }
                }

                if (maxValue < 1e-12)
                {
                    return false;
                }

                if (maxRow != i)
                {
                    for (var col = i; col < size; col++)
                    {
                        (a[i, col], a[maxRow, col]) = (a[maxRow, col], a[i, col]);
                    }

                    (b[i], b[maxRow]) = (b[maxRow], b[i]);
                }

                var pivot = a[i, i];
                for (var col = i; col < size; col++)
                {
                    a[i, col] /= pivot;
                }

                b[i] /= pivot;

                for (var row = 0; row < size; row++)
                {
                    if (row == i)
                    {
                        continue;
                    }

                    var factor = a[row, i];
                    if (Math.Abs(factor) < double.Epsilon)
                    {
                        continue;
                    }

                    for (var col = i; col < size; col++)
                    {
                        a[row, col] -= factor * a[i, col];
                    }

                    b[row] -= factor * b[i];
                }
            }

            Array.Copy(b, solution, size);
            return true;
        }

        private static double EvaluatePolynomial(IReadOnlyList<double> coefficients, double x)
        {
            var result = 0d;
            var power = 1d;
            for (var i = 0; i < coefficients.Count; i++)
            {
                result += coefficients[i] * power;
                power *= x;
            }

            return result;
        }

        private static void TryDrawMovingAverageTrendline(
            SKCanvas canvas,
            SKRect plot,
            ChartSeriesSnapshot series,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            ChartAxisKind categoryAxisKind,
            double minCategory,
            double maxCategory,
            bool isBar,
            SKPaint linePaint)
        {
            var count = series.Values.Count;
            if (count < 2)
            {
                return;
            }

            var period = Math.Max(2, series.TrendlinePeriod);
            if (count < period)
            {
                return;
            }

            var xValues = series.XValues;
            var hasX = (series.Kind == ChartSeriesKind.Scatter || series.Kind == ChartSeriesKind.Bubble) &&
                       xValues != null &&
                       xValues.Count == count;
            var hasValidX = false;
            double minX = 0;
            double maxX = 1;
            if (hasX)
            {
                hasValidX = TryGetScatterAxisRange(series, useNumericCategoryAxis, minCategory, maxCategory, categoryAxisKind, out minX, out maxX);
            }

            var categoryCount = Math.Max(1, categories.Count > 0 ? categories.Count : count);
            var path = SkiaChartPools.RentPath();
            try
            {
                var hasPath = false;
                for (var i = period - 1; i < count; i++)
                {
                    var sum = 0d;
                    var valid = 0;
                    for (var j = i - period + 1; j <= i; j++)
                    {
                        var value = series.Values[j];
                        if (!value.HasValue || IsInvalidAxisValue(value.Value, valueAxisKind))
                        {
                            continue;
                        }

                        sum += value.Value;
                        valid++;
                    }

                    if (valid == 0)
                    {
                        continue;
                    }

                    var average = sum / valid;
                    if (IsInvalidAxisValue(average, valueAxisKind))
                    {
                        continue;
                    }

                    float x;
                    float y;
                    if (hasX && hasValidX)
                    {
                        var xValue = xValues![i];
                        if (IsInvalidAxisValue(xValue, categoryAxisKind))
                        {
                            continue;
                        }

                        x = MapValueX(plot, xValue, minX, maxX, categoryAxisKind);
                        y = MapY(plot, average, minValue, maxValue, valueAxisKind);
                    }
                    else if (isBar)
                    {
                        x = MapValueX(plot, average, minValue, maxValue, valueAxisKind);
                        y = MapCategoryY(plot, i, categoryCount);
                    }
                    else
                    {
                        x = MapX(plot, i, categoryCount);
                        y = MapY(plot, average, minValue, maxValue, valueAxisKind);
                    }

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

    }
}
