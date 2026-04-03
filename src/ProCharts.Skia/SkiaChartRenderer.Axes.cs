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
        private static void DrawAxes(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            double minCategory,
            double maxCategory,
            bool hasSecondaryAxis)
        {
            using var axisPaint = new SKPaint
            {
                Color = style.Axis,
                StrokeWidth = style.AxisStrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            var categoryCount = Math.Max(1, categories.Count);
            var secondaryValueMin = hasSecondaryAxis ? minSecondaryValue : minValue;
            var secondaryValueMax = hasSecondaryAxis ? maxSecondaryValue : maxValue;
            var secondaryValueKind = hasSecondaryAxis ? style.SecondaryValueAxisKind : style.ValueAxisKind;
            var categoryAxisLine = ResolveCategoryAxisLine(plot, style, barOnly, secondary: false, minValue, maxValue, style.ValueAxisKind);
            var secondaryCategoryAxisLine = ResolveCategoryAxisLine(plot, style, barOnly, secondary: true, secondaryValueMin, secondaryValueMax, secondaryValueKind);
            var valueAxisLine = ResolveValueAxisLine(plot, style, barOnly, secondary: false, useNumericCategoryAxis, categoryCount, minCategory, maxCategory);
            var secondaryValueAxisLine = ResolveValueAxisLine(plot, style, barOnly, secondary: true, useNumericCategoryAxis, categoryCount, minCategory, maxCategory);

            if (barOnly)
            {
                if (style.ShowCategoryAxisLine)
                {
                    canvas.DrawLine(categoryAxisLine, plot.Top, categoryAxisLine, plot.Bottom, axisPaint);
                }

                if (style.ShowValueAxisLine)
                {
                    canvas.DrawLine(plot.Left, valueAxisLine, plot.Right, valueAxisLine, axisPaint);
                }

                if (style.ShowSecondaryValueAxis && hasSecondaryAxis)
                {
                    canvas.DrawLine(plot.Left, secondaryValueAxisLine, plot.Right, secondaryValueAxisLine, axisPaint);
                }

                if (style.ShowSecondaryCategoryAxis)
                {
                    canvas.DrawLine(secondaryCategoryAxisLine, plot.Top, secondaryCategoryAxisLine, plot.Bottom, axisPaint);
                }
            }
            else
            {
                if (style.ShowCategoryAxisLine)
                {
                    canvas.DrawLine(plot.Left, categoryAxisLine, plot.Right, categoryAxisLine, axisPaint);
                }

                if (style.ShowValueAxisLine)
                {
                    canvas.DrawLine(valueAxisLine, plot.Top, valueAxisLine, plot.Bottom, axisPaint);
                }

                if (style.ShowSecondaryValueAxis && hasSecondaryAxis)
                {
                    canvas.DrawLine(secondaryValueAxisLine, plot.Top, secondaryValueAxisLine, plot.Bottom, axisPaint);
                }

                if (style.ShowSecondaryCategoryAxis)
                {
                    canvas.DrawLine(plot.Left, secondaryCategoryAxisLine, plot.Right, secondaryCategoryAxisLine, axisPaint);
                }
            }

            DrawMinorAxisTicks(
                canvas,
                plot,
                categories,
                style,
                barOnly,
                useNumericCategoryAxis,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                minCategory,
                maxCategory,
                hasSecondaryAxis,
                categoryAxisLine,
                secondaryCategoryAxisLine,
                valueAxisLine,
                secondaryValueAxisLine);
        }

        private static float ResolveCategoryAxisLine(
            SKRect plot,
            SkiaChartStyle style,
            bool barOnly,
            bool secondary,
            double minValue,
            double maxValue,
            ChartAxisKind valueAxisKind)
        {
            var crossing = secondary ? style.SecondaryCategoryAxisCrossing : style.CategoryAxisCrossing;
            var crossingValue = secondary ? style.SecondaryCategoryAxisCrossingValue : style.CategoryAxisCrossingValue;
            var useZero = valueAxisKind != ChartAxisKind.Logarithmic && minValue <= 0d && maxValue >= 0d;
            var fallback = useZero ? 0d : (secondary ? maxValue : minValue);
            var resolved = crossing switch
            {
                ChartAxisCrossing.Minimum => minValue,
                ChartAxisCrossing.Maximum => maxValue,
                ChartAxisCrossing.Value => crossingValue ?? fallback,
                _ => fallback
            };

            if (IsInvalidAxisValue(resolved, valueAxisKind))
            {
                resolved = fallback;
            }

            float position;
            if (barOnly)
            {
                position = MapValueX(plot, resolved, minValue, maxValue, valueAxisKind);
                var offset = secondary ? style.SecondaryCategoryAxisOffset : style.CategoryAxisOffset;
                position += secondary ? offset : -offset;
                return Clamp(position, plot.Left, plot.Right);
            }

            position = MapY(plot, resolved, minValue, maxValue, valueAxisKind);
            var yOffset = secondary ? style.SecondaryCategoryAxisOffset : style.CategoryAxisOffset;
            position += secondary ? -yOffset : yOffset;
            return Clamp(position, plot.Top, plot.Bottom);
        }

        private static float ResolveValueAxisLine(
            SKRect plot,
            SkiaChartStyle style,
            bool barOnly,
            bool secondary,
            bool useNumericCategoryAxis,
            int categoryCount,
            double minCategory,
            double maxCategory)
        {
            var crossing = secondary ? style.SecondaryValueAxisCrossing : style.ValueAxisCrossing;
            var crossingValue = secondary ? style.SecondaryValueAxisCrossingValue : style.ValueAxisCrossingValue;
            var categoryAxisKind = style.CategoryAxisKind;
            var hasCategories = categoryCount > 0;
            var categoryMin = hasCategories ? 0d : 0d;
            var categoryMax = hasCategories ? Math.Max(0, categoryCount - 1) : 1d;

            var fallback = secondary ? (useNumericCategoryAxis ? maxCategory : categoryMax) : (useNumericCategoryAxis ? minCategory : categoryMin);
            var resolved = crossing switch
            {
                ChartAxisCrossing.Minimum => useNumericCategoryAxis ? minCategory : categoryMin,
                ChartAxisCrossing.Maximum => useNumericCategoryAxis ? maxCategory : categoryMax,
                ChartAxisCrossing.Value => crossingValue ?? fallback,
                _ => fallback
            };

            if (useNumericCategoryAxis)
            {
                if (IsInvalidAxisValue(resolved, categoryAxisKind))
                {
                    resolved = fallback;
                }
            }
            else
            {
                if (double.IsNaN(resolved) || double.IsInfinity(resolved))
                {
                    resolved = fallback;
                }

                if (hasCategories)
                {
                    resolved = Clamp(resolved, categoryMin, categoryMax);
                }
            }

            float position;
            if (barOnly)
            {
                if (useNumericCategoryAxis)
                {
                    position = MapCategoryValueY(plot, resolved, minCategory, maxCategory, categoryAxisKind);
                }
                else
                {
                    var index = (int)Math.Round(resolved);
                    index = Clamp(index, 0, Math.Max(0, categoryCount - 1));
                    position = MapCategoryY(plot, index, Math.Max(1, categoryCount));
                }

                var offset = secondary ? style.SecondaryValueAxisOffset : style.ValueAxisOffset;
                position += secondary ? -offset : offset;
                return Clamp(position, plot.Top, plot.Bottom);
            }

            if (useNumericCategoryAxis)
            {
                position = MapValueX(plot, resolved, minCategory, maxCategory, categoryAxisKind);
            }
            else
            {
                var index = (int)Math.Round(resolved);
                index = Clamp(index, 0, Math.Max(0, categoryCount - 1));
                position = MapX(plot, index, Math.Max(1, categoryCount));
            }

            var xOffset = secondary ? style.SecondaryValueAxisOffset : style.ValueAxisOffset;
            position += secondary ? xOffset : -xOffset;
            return Clamp(position, plot.Left, plot.Right);
        }

        private static void DrawMinorAxisTicks(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            double minCategory,
            double maxCategory,
            bool hasSecondaryAxis,
            float categoryAxisLine,
            float secondaryCategoryAxisLine,
            float valueAxisLine,
            float secondaryValueAxisLine)
        {
            if (!style.ShowValueMinorTicks &&
                !style.ShowCategoryMinorTicks &&
                !style.ShowSecondaryValueMinorTicks &&
                !style.ShowSecondaryCategoryMinorTicks)
            {
                return;
            }

            using var tickPaint = new SKPaint
            {
                Color = style.Axis,
                StrokeWidth = Math.Max(1f, style.AxisStrokeWidth),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            const float tickLength = 4f;

            if (style.ShowValueMinorTicks)
            {
                DrawAxisMinorTicks(
                    canvas,
                    plot,
                    barOnly,
                    minValue,
                    maxValue,
                    style.ValueAxisKind,
                    valueAxisLine,
                    isSecondary: false,
                    majorTickCount: Math.Max(2, style.AxisTickCount),
                    minorTickCount: Math.Max(0, style.ValueAxisMinorTickCount),
                    tickLength,
                    tickPaint);
            }

            if (style.ShowSecondaryValueMinorTicks && style.ShowSecondaryValueAxis && hasSecondaryAxis)
            {
                DrawAxisMinorTicks(
                    canvas,
                    plot,
                    barOnly,
                    minSecondaryValue,
                    maxSecondaryValue,
                    style.SecondaryValueAxisKind,
                    secondaryValueAxisLine,
                    isSecondary: true,
                    majorTickCount: Math.Max(2, style.AxisTickCount),
                    minorTickCount: Math.Max(0, style.SecondaryValueAxisMinorTickCount),
                    tickLength,
                    tickPaint);
            }

            if (style.ShowCategoryMinorTicks && useNumericCategoryAxis)
            {
                DrawCategoryAxisMinorTicks(
                    canvas,
                    plot,
                    barOnly,
                    minCategory,
                    maxCategory,
                    style.CategoryAxisKind,
                    categoryAxisLine,
                    isSecondary: false,
                    majorTickCount: Math.Max(2, style.AxisTickCount),
                    minorTickCount: Math.Max(0, style.CategoryAxisMinorTickCount),
                    tickLength,
                    tickPaint);
            }

            if (style.ShowSecondaryCategoryMinorTicks && useNumericCategoryAxis && style.ShowSecondaryCategoryAxis)
            {
                DrawCategoryAxisMinorTicks(
                    canvas,
                    plot,
                    barOnly,
                    minCategory,
                    maxCategory,
                    style.SecondaryCategoryAxisKind,
                    secondaryCategoryAxisLine,
                    isSecondary: true,
                    majorTickCount: Math.Max(2, style.AxisTickCount),
                    minorTickCount: Math.Max(0, style.SecondaryCategoryAxisMinorTickCount),
                    tickLength,
                    tickPaint);
            }
        }

        private static void DrawAxisMinorTicks(
            SKCanvas canvas,
            SKRect plot,
            bool barOnly,
            double minValue,
            double maxValue,
            ChartAxisKind axisKind,
            float axisLine,
            bool isSecondary,
            int majorTickCount,
            int minorTickCount,
            float tickLength,
            SKPaint paint)
        {
            if (minValue == maxValue || minorTickCount <= 0)
            {
                return;
            }

            var majorTicks = GetAxisTicks(minValue, maxValue, axisKind, majorTickCount, out _);
            try
            {
                var minorTicks = GetMinorTicks(majorTicks, axisKind, minorTickCount);
                try
                {
                    if (barOnly)
                    {
                        var direction = isSecondary ? -tickLength : tickLength;
                        foreach (var value in minorTicks)
                        {
                            if (IsInvalidAxisValue(value, axisKind))
                            {
                                continue;
                            }

                            var x = MapValueX(plot, value, minValue, maxValue, axisKind);
                            canvas.DrawLine(x, axisLine, x, axisLine + direction, paint);
                        }
                    }
                    else
                    {
                        var direction = isSecondary ? tickLength : -tickLength;
                        foreach (var value in minorTicks)
                        {
                            if (IsInvalidAxisValue(value, axisKind))
                            {
                                continue;
                            }

                            var y = MapY(plot, value, minValue, maxValue, axisKind);
                            canvas.DrawLine(axisLine, y, axisLine + direction, y, paint);
                        }
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(minorTicks);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(majorTicks);
            }
        }

        private static void DrawCategoryAxisMinorTicks(
            SKCanvas canvas,
            SKRect plot,
            bool barOnly,
            double minCategory,
            double maxCategory,
            ChartAxisKind axisKind,
            float axisLine,
            bool isSecondary,
            int majorTickCount,
            int minorTickCount,
            float tickLength,
            SKPaint paint)
        {
            if (minCategory == maxCategory || minorTickCount <= 0)
            {
                return;
            }

            var majorTicks = GetAxisTicks(minCategory, maxCategory, axisKind, majorTickCount, out _);
            try
            {
                var minorTicks = GetMinorTicks(majorTicks, axisKind, minorTickCount);
                try
                {
                    if (barOnly)
                    {
                        var direction = isSecondary ? tickLength : -tickLength;
                        foreach (var value in minorTicks)
                        {
                            if (IsInvalidAxisValue(value, axisKind))
                            {
                                continue;
                            }

                            var y = MapCategoryValueY(plot, value, minCategory, maxCategory, axisKind);
                            canvas.DrawLine(axisLine, y, axisLine + direction, y, paint);
                        }
                    }
                    else
                    {
                        var direction = isSecondary ? -tickLength : tickLength;
                        foreach (var value in minorTicks)
                        {
                            if (IsInvalidAxisValue(value, axisKind))
                            {
                                continue;
                            }

                            var x = MapValueX(plot, value, minCategory, maxCategory, axisKind);
                            canvas.DrawLine(x, axisLine, x, axisLine + direction, paint);
                        }
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(minorTicks);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(majorTicks);
            }
        }


        private static float MapY(SKRect plot, double value, double minValue, double maxValue, ChartAxisKind axisKind)
        {
            var normalized = NormalizeAxisValue(value, minValue, maxValue, axisKind);
            return (float)(plot.Bottom - normalized * plot.Height);
        }

        private static float MapValueX(SKRect plot, double value, double minValue, double maxValue, ChartAxisKind axisKind)
        {
            var normalized = NormalizeAxisValue(value, minValue, maxValue, axisKind);
            return (float)(plot.Left + normalized * plot.Width);
        }

        private static double NormalizeAxisValue(double value, double minValue, double maxValue, ChartAxisKind axisKind)
        {
            var transformedValue = TransformAxisValue(value, axisKind);
            var transformedMin = TransformAxisValue(minValue, axisKind);
            var transformedMax = TransformAxisValue(maxValue, axisKind);

            if (IsInvalidNumber(transformedValue) || IsInvalidNumber(transformedMin) || IsInvalidNumber(transformedMax))
            {
                return 0d;
            }

            if (Math.Abs(transformedMax - transformedMin) < double.Epsilon)
            {
                transformedMax = transformedMin + 1d;
            }

            var normalized = (transformedValue - transformedMin) / (transformedMax - transformedMin);
            return Clamp(normalized, 0d, 1d);
        }

        private static double TransformAxisValue(double value, ChartAxisKind axisKind)
        {
            return axisKind == ChartAxisKind.Logarithmic ? Math.Log10(value) : value;
        }

        private static float MapX(SKRect plot, int index, int count)
        {
            if (count <= 1)
            {
                return plot.MidX;
            }

            var step = plot.Width / (count - 1);
            return plot.Left + (index * step);
        }

        private static float MapCategoryY(SKRect plot, int index, int count)
        {
            if (count <= 0)
            {
                return plot.Top;
            }

            var step = plot.Height / count;
            return plot.Top + (index * step) + (step / 2f);
        }

        private static float MapCategoryValueY(SKRect plot, double value, double minValue, double maxValue, ChartAxisKind axisKind)
        {
            var normalized = NormalizeAxisValue(value, minValue, maxValue, axisKind);
            return (float)(plot.Top + normalized * plot.Height);
        }

        private static void DrawGridlines(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            SkiaChartStyle style,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            bool hasSecondaryAxis)
        {
            using var gridPaint = new SKPaint
            {
                Color = style.Gridline,
                StrokeWidth = style.GridlineStrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            using var minorGridPaint = new SKPaint
            {
                Color = ApplyOpacity(style.Gridline, 0.6f),
                StrokeWidth = Math.Max(1f, style.GridlineStrokeWidth * 0.6f),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            if (style.ShowGridlines)
            {
                var ticks = GetAxisTicks(minValue, maxValue, style.ValueAxisKind, Math.Max(2, style.AxisTickCount), out _);
                try
                {
                    if (ticks.Count > 0)
                    {
                        if (barOnly)
                        {
                            foreach (var value in ticks)
                            {
                                var x = MapValueX(plot, value, minValue, maxValue, style.ValueAxisKind);
                                canvas.DrawLine(x, plot.Top, x, plot.Bottom, gridPaint);
                            }
                        }
                        else
                        {
                            foreach (var value in ticks)
                            {
                                var y = MapY(plot, value, minValue, maxValue, style.ValueAxisKind);
                                canvas.DrawLine(plot.Left, y, plot.Right, y, gridPaint);
                            }
                        }
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }
            }

            if (style.ShowValueMinorGridlines)
            {
                var ticks = GetAxisTicks(minValue, maxValue, style.ValueAxisKind, Math.Max(2, style.AxisTickCount), out _);
                try
                {
                    var minorTicks = GetMinorTicks(ticks, style.ValueAxisKind, Math.Max(0, style.ValueAxisMinorTickCount));
                    try
                    {
                        if (minorTicks.Count > 0)
                        {
                            if (barOnly)
                            {
                                foreach (var value in minorTicks)
                                {
                                    var x = MapValueX(plot, value, minValue, maxValue, style.ValueAxisKind);
                                    canvas.DrawLine(x, plot.Top, x, plot.Bottom, minorGridPaint);
                                }
                            }
                            else
                            {
                                foreach (var value in minorTicks)
                                {
                                    var y = MapY(plot, value, minValue, maxValue, style.ValueAxisKind);
                                    canvas.DrawLine(plot.Left, y, plot.Right, y, minorGridPaint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(minorTicks);
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }
            }

            if (style.ShowSecondaryValueMinorGridlines && style.ShowSecondaryValueAxis && hasSecondaryAxis)
            {
                var ticks = GetAxisTicks(minSecondaryValue, maxSecondaryValue, style.SecondaryValueAxisKind, Math.Max(2, style.AxisTickCount), out _);
                try
                {
                    var minorTicks = GetMinorTicks(ticks, style.SecondaryValueAxisKind, Math.Max(0, style.SecondaryValueAxisMinorTickCount));
                    try
                    {
                        if (minorTicks.Count > 0)
                        {
                            if (barOnly)
                            {
                                foreach (var value in minorTicks)
                                {
                                    var x = MapValueX(plot, value, minSecondaryValue, maxSecondaryValue, style.SecondaryValueAxisKind);
                                    canvas.DrawLine(x, plot.Top, x, plot.Bottom, minorGridPaint);
                                }
                            }
                            else
                            {
                                foreach (var value in minorTicks)
                                {
                                    var y = MapY(plot, value, minSecondaryValue, maxSecondaryValue, style.SecondaryValueAxisKind);
                                    canvas.DrawLine(plot.Left, y, plot.Right, y, minorGridPaint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(minorTicks);
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }
            }

            if (style.ShowCategoryGridlines)
            {
                if (useNumericCategoryAxis)
                {
                    var ticks = GetAxisTicks(minCategory, maxCategory, style.CategoryAxisKind, Math.Max(2, style.AxisTickCount), out _);
                    try
                    {
                        if (ticks.Count > 0)
                        {
                            foreach (var value in ticks)
                            {
                                if (barOnly)
                                {
                                    var y = MapCategoryValueY(plot, value, minCategory, maxCategory, style.CategoryAxisKind);
                                    canvas.DrawLine(plot.Left, y, plot.Right, y, gridPaint);
                                }
                                else
                                {
                                    var x = MapValueX(plot, value, minCategory, maxCategory, style.CategoryAxisKind);
                                    canvas.DrawLine(x, plot.Top, x, plot.Bottom, gridPaint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(ticks);
                    }
                }
                else
                {
                    if (categories.Count == 0)
                    {
                        return;
                    }

                    var step = CalculateCategoryStep(categories.Count, plot, style, barOnly);
                    if (barOnly)
                    {
                        for (var i = 0; i < categories.Count; i += step)
                        {
                            var y = MapCategoryY(plot, i, categories.Count);
                            canvas.DrawLine(plot.Left, y, plot.Right, y, gridPaint);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < categories.Count; i += step)
                        {
                            var x = MapX(plot, i, categories.Count);
                            canvas.DrawLine(x, plot.Top, x, plot.Bottom, gridPaint);
                        }
                    }
                }
            }

            if (style.ShowCategoryMinorGridlines && useNumericCategoryAxis)
            {
                var ticks = GetAxisTicks(minCategory, maxCategory, style.CategoryAxisKind, Math.Max(2, style.AxisTickCount), out _);
                try
                {
                    var minorTicks = GetMinorTicks(ticks, style.CategoryAxisKind, Math.Max(0, style.CategoryAxisMinorTickCount));
                    try
                    {
                        if (minorTicks.Count > 0)
                        {
                            foreach (var value in minorTicks)
                            {
                                if (barOnly)
                                {
                                    var y = MapCategoryValueY(plot, value, minCategory, maxCategory, style.CategoryAxisKind);
                                    canvas.DrawLine(plot.Left, y, plot.Right, y, minorGridPaint);
                                }
                                else
                                {
                                    var x = MapValueX(plot, value, minCategory, maxCategory, style.CategoryAxisKind);
                                    canvas.DrawLine(x, plot.Top, x, plot.Bottom, minorGridPaint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(minorTicks);
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }
            }

            if (style.ShowSecondaryCategoryMinorGridlines && useNumericCategoryAxis && style.ShowSecondaryCategoryAxis)
            {
                var ticks = GetAxisTicks(minCategory, maxCategory, style.SecondaryCategoryAxisKind, Math.Max(2, style.AxisTickCount), out _);
                try
                {
                    var minorTicks = GetMinorTicks(ticks, style.SecondaryCategoryAxisKind, Math.Max(0, style.SecondaryCategoryAxisMinorTickCount));
                    try
                    {
                        if (minorTicks.Count > 0)
                        {
                            foreach (var value in minorTicks)
                            {
                                if (barOnly)
                                {
                                    var y = MapCategoryValueY(plot, value, minCategory, maxCategory, style.SecondaryCategoryAxisKind);
                                    canvas.DrawLine(plot.Left, y, plot.Right, y, minorGridPaint);
                                }
                                else
                                {
                                    var x = MapValueX(plot, value, minCategory, maxCategory, style.SecondaryCategoryAxisKind);
                                    canvas.DrawLine(x, plot.Top, x, plot.Bottom, minorGridPaint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(minorTicks);
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }
            }
        }

        private static void DrawAxisLabels(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            bool hasSecondaryAxis,
            SkiaChartStyle style,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory)
        {
            if (!style.ShowAxisLabels && !style.ShowCategoryLabels && !style.ShowSecondaryCategoryAxis)
            {
                return;
            }

            var categoryCount = Math.Max(1, categories.Count);
            var secondaryValueMin = hasSecondaryAxis ? minSecondaryValue : minValue;
            var secondaryValueMax = hasSecondaryAxis ? maxSecondaryValue : maxValue;
            var secondaryValueKind = hasSecondaryAxis ? style.SecondaryValueAxisKind : style.ValueAxisKind;
            var categoryAxisLine = ResolveCategoryAxisLine(plot, style, barOnly, secondary: false, minValue, maxValue, style.ValueAxisKind);
            var secondaryCategoryAxisLine = ResolveCategoryAxisLine(plot, style, barOnly, secondary: true, secondaryValueMin, secondaryValueMax, secondaryValueKind);
            var valueAxisLine = ResolveValueAxisLine(plot, style, barOnly, secondary: false, useNumericCategoryAxis, categoryCount, minCategory, maxCategory);
            var secondaryValueAxisLine = ResolveValueAxisLine(plot, style, barOnly, secondary: true, useNumericCategoryAxis, categoryCount, minCategory, maxCategory);

            if (style.ShowAxisLabels)
            {
                DrawValueAxisLabels(canvas, plot, minValue, maxValue, style, barOnly, style.ValueAxisKind, style.AxisLabelFormatter, isSecondary: false, axisLine: valueAxisLine);
                if (hasSecondaryAxis && style.ShowSecondaryValueAxis)
                {
                    DrawValueAxisLabels(canvas, plot, minSecondaryValue, maxSecondaryValue, style, barOnly, style.SecondaryValueAxisKind, style.SecondaryAxisLabelFormatter, isSecondary: true, axisLine: secondaryValueAxisLine);
                }
            }

            if (style.ShowCategoryLabels)
            {
                DrawCategoryLabels(
                    canvas,
                    plot,
                    categories,
                    style,
                    barOnly,
                    useNumericCategoryAxis,
                    minCategory,
                    maxCategory,
                    style.CategoryAxisKind,
                    style.CategoryAxisLabelFormatter,
                    isSecondary: false,
                    axisLine: categoryAxisLine);
            }

            if (style.ShowSecondaryCategoryAxis)
            {
                DrawCategoryLabels(
                    canvas,
                    plot,
                    categories,
                    style,
                    barOnly,
                    useNumericCategoryAxis,
                    minCategory,
                    maxCategory,
                    style.SecondaryCategoryAxisKind,
                    style.SecondaryCategoryAxisLabelFormatter,
                    isSecondary: true,
                    axisLine: secondaryCategoryAxisLine);
            }
        }

        private static void DrawValueAxisLabels(
            SKCanvas canvas,
            SKRect plot,
            double minValue,
            double maxValue,
            SkiaChartStyle style,
            bool barOnly,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            bool isSecondary,
            float axisLine)
        {
            var ticks = GetAxisTicks(minValue, maxValue, axisKind, Math.Max(2, style.AxisTickCount), out var dateTimeFormat);
            try
            {
                if (ticks.Count == 0)
                {
                    return;
                }

                var labels = SkiaChartPools.RentList<string>(ticks.Count);
                try
                {
                    foreach (var value in ticks)
                    {
                        labels.Add(FormatAxisValue(
                            value,
                            axisKind,
                            formatter,
                            isSecondary ? style.SecondaryAxisValueFormat : style.AxisValueFormat,
                            dateTimeFormat));
                    }

                    var spacing = GetAxisLabelSpacing(ticks.Count, barOnly ? plot.Width : plot.Height, useBands: false);
                    var layout = barOnly
                        ? ResolveHorizontalLabelLayout(labels, spacing, style.LabelSize, allowRotation: false, allowEllipsis: false)
                        : ResolveVerticalLabelLayout(labels, spacing, style.LabelSize);

                    using var textPaint = CreateTextPaint(style.Text, layout.TextSize);

                    if (barOnly)
                    {
                        for (var i = 0; i < ticks.Count; i += layout.Step)
                        {
                            var value = ticks[i];
                            var text = labels[i];
                            if (layout.UseEllipsis)
                            {
                                text = TrimWithEllipsis(text, textPaint, layout.MaxLabelWidth);
                            }

                            if (string.IsNullOrEmpty(text))
                            {
                                continue;
                            }

                            var x = MapValueX(plot, value, minValue, maxValue, axisKind);
                            var anchor = isSecondary ? AxisLabelAnchor.BottomCenter : AxisLabelAnchor.TopCenter;
                            var y = isSecondary ? axisLine - 4f : axisLine + 4f;
                            DrawAxisLabelText(canvas, textPaint, text, x, y, anchor, layout.RotationDegrees);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < ticks.Count; i += layout.Step)
                        {
                            var value = ticks[i];
                            var text = labels[i];
                            if (layout.UseEllipsis)
                            {
                                text = TrimWithEllipsis(text, textPaint, layout.MaxLabelWidth);
                            }

                            if (string.IsNullOrEmpty(text))
                            {
                                continue;
                            }

                            var y = MapY(plot, value, minValue, maxValue, axisKind);
                            var x = isSecondary ? axisLine + 6f : axisLine - 6f;
                            var anchor = isSecondary ? AxisLabelAnchor.CenterLeft : AxisLabelAnchor.CenterRight;
                            DrawAxisLabelText(canvas, textPaint, text, x, y, anchor, layout.RotationDegrees);
                        }
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(labels);
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(ticks);
            }
        }

        private static void DrawCategoryLabels(
            SKCanvas canvas,
            SKRect plot,
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            bool isSecondary,
            float axisLine)
        {
            if (useNumericCategoryAxis)
            {
                var ticks = GetAxisTicks(minCategory, maxCategory, axisKind, Math.Max(2, style.AxisTickCount), out var dateTimeFormat);
                try
                {
                    if (ticks.Count == 0)
                    {
                        return;
                    }

                    var labels = SkiaChartPools.RentList<string>(ticks.Count);
                    try
                    {
                        foreach (var value in ticks)
                        {
                            labels.Add(FormatCategoryAxisValue(value, axisKind, formatter, style.CategoryAxisValueFormat, dateTimeFormat));
                        }

                        var numericSpacing = GetAxisLabelSpacing(ticks.Count, barOnly ? plot.Height : plot.Width, useBands: false);
                        var numericLayout = barOnly
                            ? ResolveVerticalLabelLayout(labels, numericSpacing, style.LabelSize)
                            : ResolveHorizontalLabelLayout(labels, numericSpacing, style.LabelSize, allowRotation: true, allowEllipsis: false);

                        using var numericPaint = CreateTextPaint(style.Text, numericLayout.TextSize);

                        for (var i = 0; i < ticks.Count; i += numericLayout.Step)
                        {
                            var value = ticks[i];
                            var text = labels[i];
                            if (numericLayout.UseEllipsis)
                            {
                                text = TrimWithEllipsis(text, numericPaint, numericLayout.MaxLabelWidth);
                            }

                            if (string.IsNullOrEmpty(text))
                            {
                                continue;
                            }

                            if (barOnly)
                            {
                                var y = MapCategoryValueY(plot, value, minCategory, maxCategory, axisKind);
                                var x = isSecondary ? axisLine + 6f : axisLine - 6f;
                                var anchor = isSecondary ? AxisLabelAnchor.CenterLeft : AxisLabelAnchor.CenterRight;
                                DrawAxisLabelText(canvas, numericPaint, text, x, y, anchor, numericLayout.RotationDegrees);
                            }
                            else
                            {
                                var x = MapValueX(plot, value, minCategory, maxCategory, axisKind);
                                var y = isSecondary ? axisLine - 4f : axisLine + 4f;
                                var anchor = isSecondary ? AxisLabelAnchor.BottomCenter : AxisLabelAnchor.TopCenter;
                                DrawAxisLabelText(canvas, numericPaint, text, x, y, anchor, numericLayout.RotationDegrees);
                            }
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(labels);
                    }
                }
                finally
                {
                    SkiaChartPools.ReturnList(ticks);
                }

                return;
            }

            if (categories.Count == 0)
            {
                return;
            }

            var labelStrings = SkiaChartPools.RentList<string>(categories.Count);
            try
            {
                foreach (var category in categories)
                {
                    labelStrings.Add(category ?? string.Empty);
                }

                var categorySpacing = GetAxisLabelSpacing(categories.Count, barOnly ? plot.Height : plot.Width, useBands: barOnly);
                var categoryLayout = barOnly
                    ? ResolveVerticalLabelLayout(labelStrings, categorySpacing, style.LabelSize)
                    : ResolveHorizontalLabelLayout(labelStrings, categorySpacing, style.LabelSize, allowRotation: true, allowEllipsis: true);

                using var categoryPaint = CreateTextPaint(style.Text, categoryLayout.TextSize);

                for (var i = 0; i < categories.Count; i += categoryLayout.Step)
                {
                    var text = labelStrings[i];
                    if (categoryLayout.UseEllipsis)
                    {
                        text = TrimWithEllipsis(text, categoryPaint, categoryLayout.MaxLabelWidth);
                    }

                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (barOnly)
                    {
                        var y = MapCategoryY(plot, i, categories.Count);
                        var x = isSecondary ? axisLine + 6f : axisLine - 6f;
                        var anchor = isSecondary ? AxisLabelAnchor.CenterLeft : AxisLabelAnchor.CenterRight;
                        DrawAxisLabelText(canvas, categoryPaint, text, x, y, anchor, categoryLayout.RotationDegrees);
                    }
                    else
                    {
                        var x = MapX(plot, i, categories.Count);
                        var y = isSecondary ? axisLine - 4f : axisLine + 4f;
                        var anchor = isSecondary ? AxisLabelAnchor.BottomCenter : AxisLabelAnchor.TopCenter;
                        DrawAxisLabelText(canvas, categoryPaint, text, x, y, anchor, categoryLayout.RotationDegrees);
                    }
                }
            }
            finally
            {
                SkiaChartPools.ReturnList(labelStrings);
            }
        }

        private static void DrawAxisTitles(SKCanvas canvas, SKRect plot, SkiaChartStyle style, bool barOnly)
        {
            if (string.IsNullOrWhiteSpace(style.CategoryAxisTitle) &&
                string.IsNullOrWhiteSpace(style.SecondaryCategoryAxisTitle) &&
                string.IsNullOrWhiteSpace(style.ValueAxisTitle) &&
                string.IsNullOrWhiteSpace(style.SecondaryValueAxisTitle))
            {
                return;
            }

            using var textPaint = CreateTextPaint(style.Text, style.LabelSize);

            if (!string.IsNullOrWhiteSpace(style.CategoryAxisTitle))
            {
                var text = style.CategoryAxisTitle!;
                var bounds = new SKRect();
                textPaint.MeasureText(text, ref bounds);
                var titleTop = plot.Bottom + bounds.Height + style.LabelSize + 10f;
                var textX = AlignCenterX(plot.MidX, bounds);
                var textY = AlignTopY(titleTop, bounds);
                canvas.DrawText(text, textX, textY, textPaint);
            }

            if (!string.IsNullOrWhiteSpace(style.SecondaryCategoryAxisTitle))
            {
                var text = style.SecondaryCategoryAxisTitle!;
                var bounds = new SKRect();
                textPaint.MeasureText(text, ref bounds);
                if (barOnly)
                {
                    var x = plot.Right + (style.LabelSize * 3f);
                    var y = plot.MidY;
                    canvas.Save();
                    canvas.Translate(x, y);
                    canvas.RotateDegrees(90f);
                    canvas.DrawText(text, -bounds.MidX, -bounds.MidY, textPaint);
                    canvas.Restore();
                }
                else
                {
                    var titleBottom = plot.Top - style.LabelSize - 6f;
                    var textX = AlignCenterX(plot.MidX, bounds);
                    var textY = AlignBottomY(titleBottom, bounds);
                    canvas.DrawText(text, textX, textY, textPaint);
                }
            }

            if (!string.IsNullOrWhiteSpace(style.ValueAxisTitle))
            {
                var text = style.ValueAxisTitle!;
                var bounds = new SKRect();
                textPaint.MeasureText(text, ref bounds);
                var x = plot.Left - (style.LabelSize * 3f);
                var y = plot.MidY;
                canvas.Save();
                canvas.Translate(x, y);
                canvas.RotateDegrees(-90f);
                canvas.DrawText(text, -bounds.MidX, -bounds.MidY, textPaint);
                canvas.Restore();
            }

            if (!string.IsNullOrWhiteSpace(style.SecondaryValueAxisTitle))
            {
                var text = style.SecondaryValueAxisTitle!;
                var bounds = new SKRect();
                textPaint.MeasureText(text, ref bounds);
                if (barOnly)
                {
                    var titleTop = plot.Top - bounds.Height - style.LabelSize - 6f;
                    var textX = AlignCenterX(plot.MidX, bounds);
                    var textY = AlignTopY(titleTop, bounds);
                    canvas.DrawText(text, textX, textY, textPaint);
                }
                else
                {
                    var x = plot.Right + (style.LabelSize * 3f);
                    var y = plot.MidY;
                    canvas.Save();
                    canvas.Translate(x, y);
                    canvas.RotateDegrees(90f);
                    canvas.DrawText(text, -bounds.MidX, -bounds.MidY, textPaint);
                    canvas.Restore();
                }
            }
        }

        private static bool IsInvalidNumber(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value);
        }

        private static bool IsInvalidAxisValue(double value, ChartAxisKind axisKind)
        {
            if (IsInvalidNumber(value))
            {
                return true;
            }

            if (axisKind == ChartAxisKind.Logarithmic)
            {
                return value <= 0d;
            }

            return false;
        }

        private static string FormatValue(double value)
        {
            return ChartValueFormatter.Format(value, null);
        }

        private static string FormatAxisValue(double value, SkiaChartStyle style)
        {
            return FormatAxisValue(value, style.ValueAxisKind, style.AxisLabelFormatter, style.AxisValueFormat, null);
        }

        private static string FormatAxisValue(double value, ChartAxisKind axisKind, Func<double, string>? formatter)
        {
            return FormatAxisValue(value, axisKind, formatter, null, null);
        }

        private static string FormatAxisValue(double value, ChartAxisKind axisKind, Func<double, string>? formatter, ChartValueFormat? valueFormat)
        {
            return FormatAxisValue(value, axisKind, formatter, valueFormat, null);
        }

        private static string FormatAxisValue(
            double value,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            ChartValueFormat? valueFormat,
            DateTimeAxisFormat? dateTimeFormat)
        {
            if (formatter != null)
            {
                return formatter(value);
            }

            if (axisKind == ChartAxisKind.DateTime)
            {
                return FormatDateTimeValue(value, dateTimeFormat);
            }

            return ChartValueFormatter.Format(value, valueFormat);
        }

        private static string FormatCategoryAxisValue(double value, SkiaChartStyle style)
        {
            return FormatCategoryAxisValue(value, style.CategoryAxisKind, style.CategoryAxisLabelFormatter, style.CategoryAxisValueFormat, null);
        }

        private static string FormatCategoryAxisValue(double value, SkiaChartStyle style, DateTimeAxisFormat? dateTimeFormat)
        {
            return FormatCategoryAxisValue(value, style.CategoryAxisKind, style.CategoryAxisLabelFormatter, style.CategoryAxisValueFormat, dateTimeFormat);
        }

        private static string FormatCategoryAxisValue(
            double value,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            ChartValueFormat? valueFormat,
            DateTimeAxisFormat? dateTimeFormat)
        {
            if (formatter != null)
            {
                return formatter(value);
            }

            if (axisKind == ChartAxisKind.DateTime)
            {
                return FormatDateTimeValue(value, dateTimeFormat);
            }

            return ChartValueFormatter.Format(value, valueFormat);
        }

        private static string FormatDateTimeValue(double value, DateTimeAxisFormat? dateTimeFormat)
        {
            try
            {
                var dateTime = DateTime.FromOADate(value);
                if (dateTimeFormat.HasValue)
                {
                    return dateTime.ToString(dateTimeFormat.Value.FormatString, CultureInfo.CurrentCulture);
                }

                return dateTime.ToString("d", CultureInfo.CurrentCulture);
            }
            catch (ArgumentException)
            {
                return FormatValue(value);
            }
        }

        private static int CalculateCategoryStep(int categoryCount, SKRect plot, SkiaChartStyle style, bool barOnly)
        {
            if (categoryCount <= 0)
            {
                return 1;
            }

            var available = barOnly ? plot.Height : plot.Width;
            var minSpacing = Math.Max(10f, style.LabelSize * 2f);
            var maxLabels = Math.Max(1, (int)(available / minSpacing));
            var step = (int)Math.Ceiling(categoryCount / (double)maxLabels);
            return Math.Max(1, step);
        }

        private enum AxisLabelAnchor
        {
            TopCenter,
            BottomCenter,
            CenterLeft,
            CenterRight
        }

        private readonly struct AxisLabelLayout
        {
            public AxisLabelLayout(float textSize, float rotationDegrees, int step, bool useEllipsis, float maxLabelWidth)
            {
                TextSize = textSize;
                RotationDegrees = rotationDegrees;
                Step = Math.Max(1, step);
                UseEllipsis = useEllipsis;
                MaxLabelWidth = maxLabelWidth;
            }

            public float TextSize { get; }

            public float RotationDegrees { get; }

            public int Step { get; }

            public bool UseEllipsis { get; }

            public float MaxLabelWidth { get; }
        }

        private static AxisLabelLayout ResolveHorizontalLabelLayout(
            IReadOnlyList<string> labels,
            float spacing,
            float labelSize,
            bool allowRotation,
            bool allowEllipsis)
        {
            var layout = new AxisLabelLayout(labelSize, 0f, 1, false, 0f);
            if (labels.Count == 0)
            {
                return layout;
            }

            spacing = Math.Max(1f, spacing - 4f);

            using var paint = CreateTextPaint(SKColors.Black, labelSize);
            MeasureLabels(labels, paint, out var maxWidth, out var maxHeight);
            if (maxWidth <= spacing)
            {
                return layout;
            }

            var minSize = ResolveMinLabelSize(labelSize);
            if (labelSize > minSize)
            {
                var targetSize = labelSize * (spacing / Math.Max(1f, maxWidth));
                if (targetSize >= minSize)
                {
                    using var autoPaint = CreateTextPaint(SKColors.Black, targetSize);
                    MeasureLabels(labels, autoPaint, out maxWidth, out maxHeight);
                    if (maxWidth <= spacing)
                    {
                        return new AxisLabelLayout(targetSize, 0f, 1, false, 0f);
                    }
                }
            }

            if (allowRotation)
            {
                if (CanFitRotated(maxWidth, maxHeight, 45f, spacing))
                {
                    return new AxisLabelLayout(labelSize, -45f, 1, false, 0f);
                }

                if (CanFitRotated(maxWidth, maxHeight, 90f, spacing))
                {
                    return new AxisLabelLayout(labelSize, -90f, 1, false, 0f);
                }
            }

            if (allowEllipsis)
            {
                var ellipsisWidth = paint.MeasureText("...");
                if (ellipsisWidth <= spacing)
                {
                    return new AxisLabelLayout(labelSize, 0f, 1, true, spacing);
                }
            }

            var step = CalculateStep(maxWidth, spacing);
            return new AxisLabelLayout(labelSize, 0f, step, false, 0f);
        }

        private static AxisLabelLayout ResolveVerticalLabelLayout(
            IReadOnlyList<string> labels,
            float spacing,
            float labelSize)
        {
            var layout = new AxisLabelLayout(labelSize, 0f, 1, false, 0f);
            if (labels.Count == 0)
            {
                return layout;
            }

            spacing = Math.Max(1f, spacing - 4f);

            using var paint = CreateTextPaint(SKColors.Black, labelSize);
            MeasureLabels(labels, paint, out var maxWidth, out var maxHeight);
            if (maxHeight <= spacing)
            {
                return layout;
            }

            var minSize = ResolveMinLabelSize(labelSize);
            if (labelSize > minSize)
            {
                var targetSize = labelSize * (spacing / Math.Max(1f, maxHeight));
                if (targetSize >= minSize)
                {
                    using var autoPaint = CreateTextPaint(SKColors.Black, targetSize);
                    MeasureLabels(labels, autoPaint, out maxWidth, out maxHeight);
                    if (maxHeight <= spacing)
                    {
                        return new AxisLabelLayout(targetSize, 0f, 1, false, 0f);
                    }
                }
            }

            var step = CalculateStep(maxHeight, spacing);
            return new AxisLabelLayout(labelSize, 0f, step, false, 0f);
        }

        private static void MeasureLabels(
            IReadOnlyList<string> labels,
            SKPaint paint,
            out float maxWidth,
            out float maxHeight)
        {
            maxWidth = 0f;
            maxHeight = 0f;
            for (var i = 0; i < labels.Count; i++)
            {
                var text = labels[i] ?? string.Empty;
                if (text.Length == 0)
                {
                    continue;
                }

                var bounds = new SKRect();
                paint.MeasureText(text, ref bounds);
                maxWidth = Math.Max(maxWidth, bounds.Width);
                maxHeight = Math.Max(maxHeight, bounds.Height);
            }
        }

        private static void MeasureLabelsWithLayout(
            IReadOnlyList<string> labels,
            AxisLabelLayout layout,
            SKPaint paint,
            out float maxWidth,
            out float maxHeight,
            out float maxAscent,
            out float maxDescent)
        {
            maxWidth = 0f;
            maxHeight = 0f;
            maxAscent = 0f;
            maxDescent = 0f;

            for (var i = 0; i < labels.Count; i += layout.Step)
            {
                var text = labels[i] ?? string.Empty;
                if (layout.UseEllipsis)
                {
                    text = TrimWithEllipsis(text, paint, layout.MaxLabelWidth);
                }

                if (text.Length == 0)
                {
                    continue;
                }

                var bounds = new SKRect();
                paint.MeasureText(text, ref bounds);
                var width = bounds.Width;
                var height = bounds.Height;

                if (Math.Abs(layout.RotationDegrees) > 0.01f)
                {
                    ComputeRotatedSize(width, height, layout.RotationDegrees, out width, out height);
                }

                maxWidth = Math.Max(maxWidth, width);
                maxHeight = Math.Max(maxHeight, height);

                if (Math.Abs(layout.RotationDegrees) <= 0.01f)
                {
                    maxAscent = Math.Max(maxAscent, -bounds.Top);
                    maxDescent = Math.Max(maxDescent, bounds.Bottom);
                }
            }
        }

        private static float ResolveMinLabelSize(float labelSize)
        {
            return Math.Max(8f, labelSize * 0.7f);
        }

        private static int CalculateStep(float extent, float spacing)
        {
            if (spacing <= 0f)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Ceiling(extent / spacing));
        }

        private static bool CanFitRotated(float width, float height, float rotationDegrees, float spacing)
        {
            ComputeRotatedSize(width, height, rotationDegrees, out var rotatedWidth, out _);
            return rotatedWidth <= spacing;
        }

        private static void ComputeRotatedSize(float width, float height, float rotationDegrees, out float rotatedWidth, out float rotatedHeight)
        {
            var radians = Math.Abs(rotationDegrees) * (Math.PI / 180d);
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            rotatedWidth = (float)(Math.Abs(width * cos) + Math.Abs(height * sin));
            rotatedHeight = (float)(Math.Abs(width * sin) + Math.Abs(height * cos));
        }

        private static float GetAxisLabelSpacing(int count, float length, bool useBands)
        {
            if (length <= 0f)
            {
                return 1f;
            }

            if (useBands)
            {
                return count <= 0 ? length : Math.Max(1f, length / count);
            }

            return count <= 1 ? length : Math.Max(1f, length / (count - 1));
        }

        private static string TrimWithEllipsis(string text, SKPaint paint, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            {
                return string.Empty;
            }

            if (paint.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            var ellipsisWidth = paint.MeasureText(ellipsis);
            if (ellipsisWidth > maxWidth)
            {
                return string.Empty;
            }

            var left = 0;
            var right = text.Length;
            while (left < right)
            {
                var mid = (left + right + 1) / 2;
                var candidate = text.Substring(0, mid) + ellipsis;
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    left = mid;
                }
                else
                {
                    right = mid - 1;
                }
            }

            if (left <= 0)
            {
                return ellipsis;
            }

            return text.Substring(0, left) + ellipsis;
        }

        private static void DrawAxisLabelText(
            SKCanvas canvas,
            SKPaint paint,
            string text,
            float x,
            float y,
            AxisLabelAnchor anchor,
            float rotationDegrees)
        {
            var bounds = new SKRect();
            paint.MeasureText(text, ref bounds);
            var offset = GetAnchorOffset(bounds, anchor);

            if (Math.Abs(rotationDegrees) > 0.01f)
            {
                canvas.Save();
                canvas.Translate(x, y);
                canvas.RotateDegrees(rotationDegrees);
                canvas.DrawText(text, -offset.X, -offset.Y, paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawText(text, x - offset.X, y - offset.Y, paint);
            }
        }

        private static SKPoint GetAnchorOffset(SKRect bounds, AxisLabelAnchor anchor)
        {
            return anchor switch
            {
                AxisLabelAnchor.TopCenter => new SKPoint(bounds.MidX, bounds.Top),
                AxisLabelAnchor.BottomCenter => new SKPoint(bounds.MidX, bounds.Bottom),
                AxisLabelAnchor.CenterLeft => new SKPoint(bounds.Left, bounds.MidY),
                AxisLabelAnchor.CenterRight => new SKPoint(bounds.Right, bounds.MidY),
                _ => new SKPoint(bounds.MidX, bounds.MidY)
            };
        }

        private enum DateTimeTickUnit
        {
            Second,
            Minute,
            Hour,
            Day,
            Week,
            Month,
            Year
        }

        private readonly struct DateTimeAxisFormat
        {
            public DateTimeAxisFormat(string formatString)
            {
                FormatString = formatString;
            }

            public string FormatString { get; }
        }

        private static List<double> GetAxisTicks(
            double min,
            double max,
            ChartAxisKind axisKind,
            int desiredTicks,
            out DateTimeAxisFormat? dateTimeFormat)
        {
            var ticks = SkiaChartPools.RentList<double>();
            if (axisKind == ChartAxisKind.DateTime)
            {
                FillDateTimeTicks(ticks, min, max, desiredTicks, out dateTimeFormat);
                return ticks;
            }

            dateTimeFormat = null;
            FillAxisTicks(ticks, min, max, axisKind, desiredTicks);
            return ticks;
        }

        private static List<double> GetMinorTicks(
            IReadOnlyList<double> majorTicks,
            ChartAxisKind axisKind,
            int minorTickCount)
        {
            var ticks = SkiaChartPools.RentList<double>();
            if (minorTickCount <= 0 || majorTicks.Count < 2)
            {
                return ticks;
            }

            for (var i = 0; i < majorTicks.Count - 1; i++)
            {
                var start = majorTicks[i];
                var end = majorTicks[i + 1];
                if (IsInvalidAxisValue(start, axisKind) || IsInvalidAxisValue(end, axisKind))
                {
                    continue;
                }

                var startT = TransformAxisValue(start, axisKind);
                var endT = TransformAxisValue(end, axisKind);
                if (IsInvalidNumber(startT) || IsInvalidNumber(endT))
                {
                    continue;
                }

                var step = (endT - startT) / (minorTickCount + 1);
                for (var j = 1; j <= minorTickCount; j++)
                {
                    var valueT = startT + (step * j);
                    var value = axisKind == ChartAxisKind.Logarithmic
                        ? Math.Pow(10d, valueT)
                        : valueT;
                    ticks.Add(value);
                }
            }

            return ticks;
        }

        private static void FillAxisTicks(List<double> ticks, double min, double max, ChartAxisKind axisKind, int desiredTicks)
        {
            if (axisKind == ChartAxisKind.Logarithmic)
            {
                FillLogTicks(ticks, min, max, desiredTicks);
                return;
            }

            FillTicks(ticks, min, max, desiredTicks);
        }

        private static void FillLogTicks(List<double> ticks, double min, double max, int desiredTicks)
        {
            ticks.Clear();
            if (IsInvalidNumber(min) || IsInvalidNumber(max))
            {
                return;
            }

            if (min <= 0d || max <= 0d)
            {
                return;
            }

            if (max < min)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            var minPower = Math.Floor(Math.Log10(min));
            var maxPower = Math.Ceiling(Math.Log10(max));
            var span = Math.Max(1, (int)(maxPower - minPower + 1));
            var includeMinor = desiredTicks > span;
            var multipliers = includeMinor ? new[] { 1d, 2d, 5d } : new[] { 1d };

            for (var power = minPower; power <= maxPower; power++)
            {
                var decade = Math.Pow(10d, power);
                foreach (var multiplier in multipliers)
                {
                    var value = decade * multiplier;
                    if (value < min || value > max)
                    {
                        continue;
                    }

                    ticks.Add(value);
                }
            }

            ticks.Sort();
        }

        private static void FillDateTimeTicks(
            List<double> ticks,
            double min,
            double max,
            int desiredTicks,
            out DateTimeAxisFormat? dateTimeFormat)
        {
            dateTimeFormat = null;
            ticks.Clear();
            if (IsInvalidNumber(min) || IsInvalidNumber(max))
            {
                return;
            }

            if (max < min)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            if (desiredTicks < 2)
            {
                desiredTicks = 2;
            }

            var rangeDays = max - min;
            if (rangeDays <= 0d)
            {
                rangeDays = 1d;
            }

            var targetDays = rangeDays / (desiredTicks - 1);
            var unit = ChooseDateTimeTickUnit(targetDays);
            var step = ChooseDateTimeTickStep(unit, targetDays);
            dateTimeFormat = new DateTimeAxisFormat(ResolveDateTimeFormat(unit));

            if (unit == DateTimeTickUnit.Second ||
                unit == DateTimeTickUnit.Minute ||
                unit == DateTimeTickUnit.Hour ||
                unit == DateTimeTickUnit.Day)
            {
                var stepDays = GetFixedStepDays(unit, step);
                if (stepDays <= 0d)
                {
                    return;
                }

                var start = Math.Floor(min / stepDays) * stepDays;
                for (var value = start; value <= max + stepDays * 0.5; value += stepDays)
                {
                    ticks.Add(value);
                }

                return;
            }

            DateTime minDate;
            DateTime maxDate;
            try
            {
                minDate = DateTime.FromOADate(min);
                maxDate = DateTime.FromOADate(max);
            }
            catch (ArgumentException)
            {
                FillTicks(ticks, min, max, desiredTicks);
                return;
            }

            minDate = minDate.Date;
            maxDate = maxDate.Date;

            switch (unit)
            {
                case DateTimeTickUnit.Week:
                    {
                        var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
                        var baseline = AlignToWeek(new DateTime(1899, 12, 30), firstDayOfWeek);
                        var start = AlignToWeek(minDate, firstDayOfWeek);
                        var weekOffset = (int)((start - baseline).TotalDays / 7d);
                        var adjust = weekOffset % step;
                        if (adjust != 0)
                        {
                            start = start.AddDays(-adjust * 7d);
                        }

                        for (var dt = start; dt <= maxDate.AddDays(step * 7d); dt = dt.AddDays(step * 7d))
                        {
                            ticks.Add(dt.ToOADate());
                        }

                        break;
                    }
                case DateTimeTickUnit.Month:
                    {
                        var start = new DateTime(minDate.Year, minDate.Month, 1);
                        var monthIndex = (start.Year - 1) * 12 + (start.Month - 1);
                        var adjust = monthIndex % step;
                        if (adjust != 0)
                        {
                            start = start.AddMonths(-adjust);
                        }

                        for (var dt = start; dt <= maxDate.AddMonths(step); dt = dt.AddMonths(step))
                        {
                            ticks.Add(dt.ToOADate());
                        }

                        break;
                    }
                case DateTimeTickUnit.Year:
                    {
                        var start = new DateTime(minDate.Year, 1, 1);
                        var adjust = (start.Year - 1) % step;
                        if (adjust != 0)
                        {
                            start = start.AddYears(-adjust);
                        }

                        for (var dt = start; dt <= maxDate.AddYears(step); dt = dt.AddYears(step))
                        {
                            ticks.Add(dt.ToOADate());
                        }

                        break;
                    }
            }
        }

        private static DateTimeTickUnit ChooseDateTimeTickUnit(double targetDays)
        {
            if (targetDays <= 1d / 1440d)
            {
                return DateTimeTickUnit.Second;
            }

            if (targetDays <= 1d / 24d)
            {
                return DateTimeTickUnit.Minute;
            }

            if (targetDays <= 1d)
            {
                return DateTimeTickUnit.Hour;
            }

            if (targetDays <= 7d)
            {
                return DateTimeTickUnit.Day;
            }

            if (targetDays <= 31d)
            {
                return DateTimeTickUnit.Week;
            }

            if (targetDays <= 365d)
            {
                return DateTimeTickUnit.Month;
            }

            return DateTimeTickUnit.Year;
        }

        private static int ChooseDateTimeTickStep(DateTimeTickUnit unit, double targetDays)
        {
            switch (unit)
            {
                case DateTimeTickUnit.Second:
                    return ChooseStep(targetDays * 86400d, new[] { 1, 2, 5, 10, 15, 30 });
                case DateTimeTickUnit.Minute:
                    return ChooseStep(targetDays * 1440d, new[] { 1, 2, 5, 10, 15, 30 });
                case DateTimeTickUnit.Hour:
                    return ChooseStep(targetDays * 24d, new[] { 1, 2, 3, 4, 6, 12 });
                case DateTimeTickUnit.Day:
                    return ChooseStep(targetDays, new[] { 1, 2, 3, 4, 5, 7, 10, 14 });
                case DateTimeTickUnit.Week:
                    return ChooseStep(targetDays / 7d, new[] { 1, 2, 3, 4, 6 });
                case DateTimeTickUnit.Month:
                    return ChooseStep(targetDays / 30d, new[] { 1, 2, 3, 4, 6 });
                case DateTimeTickUnit.Year:
                    return ChooseStep(targetDays / 365d, new[] { 1, 2, 5, 10, 20, 50 });
                default:
                    return 1;
            }
        }

        private static int ChooseStep(double target, int[] steps)
        {
            foreach (var step in steps)
            {
                if (step >= target)
                {
                    return step;
                }
            }

            return steps[steps.Length - 1];
        }

        private static string ResolveDateTimeFormat(DateTimeTickUnit unit)
        {
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            return unit switch
            {
                DateTimeTickUnit.Second => format.LongTimePattern,
                DateTimeTickUnit.Minute => format.ShortTimePattern,
                DateTimeTickUnit.Hour => format.ShortTimePattern,
                DateTimeTickUnit.Day => format.ShortDatePattern,
                DateTimeTickUnit.Week => format.ShortDatePattern,
                DateTimeTickUnit.Month => format.YearMonthPattern,
                DateTimeTickUnit.Year => "yyyy",
                _ => format.ShortDatePattern
            };
        }

        private static double GetFixedStepDays(DateTimeTickUnit unit, int step)
        {
            return unit switch
            {
                DateTimeTickUnit.Second => step / 86400d,
                DateTimeTickUnit.Minute => step / 1440d,
                DateTimeTickUnit.Hour => step / 24d,
                DateTimeTickUnit.Day => step,
                _ => 0d
            };
        }

        private static DateTime AlignToWeek(DateTime value, DayOfWeek firstDayOfWeek)
        {
            var date = value.Date;
            while (date.DayOfWeek != firstDayOfWeek)
            {
                date = date.AddDays(-1);
            }

            return date;
        }

        private static void FillTicks(List<double> ticks, double min, double max, int desiredTicks)
        {
            ticks.Clear();
            if (desiredTicks < 2)
            {
                desiredTicks = 2;
            }

            var range = NiceNumber(max - min, false);
            var step = NiceNumber(range / (desiredTicks - 1), true);
            var tickMin = Math.Floor(min / step) * step;
            var tickMax = Math.Ceiling(max / step) * step;

            for (var value = tickMin; value <= tickMax + step * 0.5; value += step)
            {
                ticks.Add(value);
            }
        }

        private static double NiceNumber(double value, bool round)
        {
            if (value <= 0)
            {
                return 1;
            }

            var exponent = Math.Floor(Math.Log10(value));
            var fraction = value / Math.Pow(10, exponent);
            double niceFraction;
            if (round)
            {
                if (fraction < 1.5)
                {
                    niceFraction = 1;
                }
                else if (fraction < 3)
                {
                    niceFraction = 2;
                }
                else if (fraction < 7)
                {
                    niceFraction = 5;
                }
                else
                {
                    niceFraction = 10;
                }
            }
            else
            {
                if (fraction <= 1)
                {
                    niceFraction = 1;
                }
                else if (fraction <= 2)
                {
                    niceFraction = 2;
                }
                else if (fraction <= 5)
                {
                    niceFraction = 5;
                }
                else
                {
                    niceFraction = 10;
                }
            }

            return niceFraction * Math.Pow(10, exponent);
        }

    }
}
