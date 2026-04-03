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
        private static SKRect CalculatePlotRect(
            SKRect bounds,
            ChartDataSnapshot snapshot,
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool hasCartesianSeries,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            bool hasSecondaryAxis,
            double minCategory,
            double maxCategory,
            out SKRect? legendRect)
        {
            var padding = ResolveAutoPadding(
                bounds,
                categories,
                style,
                hasCartesianSeries,
                barOnly,
                useNumericCategoryAxis,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                hasSecondaryAxis,
                minCategory,
                maxCategory);

            var plot = new SKRect(
                bounds.Left + padding.Left,
                bounds.Top + padding.Top,
                bounds.Right - padding.Right,
                bounds.Bottom - padding.Bottom);

            legendRect = null;
            if (style.ShowLegend && snapshot.Series.Count > 0)
            {
                ResolveLegendConstraints(bounds, plot, style, out var legendMaxWidth, out var legendMaxHeight);
                var legendSize = MeasureLegend(snapshot, style, legendMaxWidth, legendMaxHeight);
                if (legendSize.Width > 0 && legendSize.Height > 0)
                {
                    var width = legendSize.Width;
                    var height = legendSize.Height;
                    if (legendMaxWidth > 0f)
                    {
                        width = Math.Min(width, legendMaxWidth);
                    }

                    if (legendMaxHeight > 0f)
                    {
                        height = Math.Min(height, legendMaxHeight);
                    }

                    if (width > 0f && height > 0f)
                    {
                        legendRect = LayoutLegend(bounds, ref plot, new SKSize(width, height), style);
                    }
                }
            }

            return plot;
        }

        private static ChartPadding ResolveAutoPadding(
            SKRect bounds,
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool hasCartesianSeries,
            bool barOnly,
            bool useNumericCategoryAxis,
            double minValue,
            double maxValue,
            double minSecondaryValue,
            double maxSecondaryValue,
            bool hasSecondaryAxis,
            double minCategory,
            double maxCategory)
        {
            var left = style.PaddingLeft;
            var right = style.PaddingRight;
            var top = style.PaddingTop;
            var bottom = style.PaddingBottom;

            if (!hasCartesianSeries)
            {
                return new ChartPadding(left, top, right, bottom);
            }

            using var textPaint = CreateTextPaint(style.Text, style.LabelSize);

            var availableWidth = Math.Max(0f, bounds.Width - left - right);
            var availableHeight = Math.Max(0f, bounds.Height - top - bottom);

            var leftLabelWidth = 0f;
            var bottomLabelHeight = 0f;
            var rightLabelWidth = 0f;
            var topLabelHeight = 0f;
            var verticalAscent = 0f;
            var verticalDescent = 0f;

            if (style.ShowAxisLabels)
            {
                MeasureAxisLabelBounds(
                    minValue,
                    maxValue,
                    style.ValueAxisKind,
                    style.AxisLabelFormatter,
                    style.AxisValueFormat,
                    style,
                    horizontal: barOnly,
                    availableLength: barOnly ? availableWidth : availableHeight,
                    out var maxWidth,
                    out var maxHeight,
                    out var maxAscent,
                    out var maxDescent);
                if (barOnly)
                {
                    bottomLabelHeight = Math.Max(bottomLabelHeight, maxHeight);
                }
                else
                {
                    leftLabelWidth = Math.Max(leftLabelWidth, maxWidth);
                    verticalAscent = Math.Max(verticalAscent, maxAscent);
                    verticalDescent = Math.Max(verticalDescent, maxDescent);
                }
            }

            if (hasSecondaryAxis && style.ShowSecondaryValueAxis)
            {
                MeasureAxisLabelBounds(
                    minSecondaryValue,
                    maxSecondaryValue,
                    style.SecondaryValueAxisKind,
                    style.SecondaryAxisLabelFormatter,
                    style.SecondaryAxisValueFormat,
                    style,
                    horizontal: barOnly,
                    availableLength: barOnly ? availableWidth : availableHeight,
                    out var maxWidth,
                    out var maxHeight,
                    out var maxAscent,
                    out var maxDescent);
                if (barOnly)
                {
                    topLabelHeight = Math.Max(topLabelHeight, maxHeight);
                }
                else
                {
                    rightLabelWidth = Math.Max(rightLabelWidth, maxWidth);
                    verticalAscent = Math.Max(verticalAscent, maxAscent);
                    verticalDescent = Math.Max(verticalDescent, maxDescent);
                }
            }

            if (style.ShowCategoryLabels)
            {
                MeasureCategoryLabelBounds(
                    categories,
                    style,
                    useNumericCategoryAxis,
                    minCategory,
                    maxCategory,
                    style.CategoryAxisKind,
                    style.CategoryAxisLabelFormatter,
                    style.CategoryAxisValueFormat,
                    horizontal: !barOnly,
                    availableLength: !barOnly ? availableWidth : availableHeight,
                    out var maxWidth,
                    out var maxHeight,
                    out var maxAscent,
                    out var maxDescent);

                if (barOnly)
                {
                    leftLabelWidth = Math.Max(leftLabelWidth, maxWidth);
                    verticalAscent = Math.Max(verticalAscent, maxAscent);
                    verticalDescent = Math.Max(verticalDescent, maxDescent);
                }
                else
                {
                    bottomLabelHeight = Math.Max(bottomLabelHeight, maxHeight);
                }
            }

            if (style.ShowSecondaryCategoryAxis)
            {
                MeasureCategoryLabelBounds(
                    categories,
                    style,
                    useNumericCategoryAxis,
                    minCategory,
                    maxCategory,
                    style.SecondaryCategoryAxisKind,
                    style.SecondaryCategoryAxisLabelFormatter,
                    style.SecondaryCategoryAxisValueFormat,
                    horizontal: !barOnly,
                    availableLength: !barOnly ? availableWidth : availableHeight,
                    out var maxWidth,
                    out var maxHeight,
                    out var maxAscent,
                    out var maxDescent);

                if (barOnly)
                {
                    rightLabelWidth = Math.Max(rightLabelWidth, maxWidth);
                    verticalAscent = Math.Max(verticalAscent, maxAscent);
                    verticalDescent = Math.Max(verticalDescent, maxDescent);
                }
                else
                {
                    topLabelHeight = Math.Max(topLabelHeight, maxHeight);
                }
            }

            if (leftLabelWidth > 0)
            {
                left = Math.Max(left, leftLabelWidth + 8f);
            }

            if (rightLabelWidth > 0)
            {
                right = Math.Max(right, rightLabelWidth + 8f);
            }

            if (bottomLabelHeight > 0)
            {
                bottom = Math.Max(bottom, bottomLabelHeight + 8f);
            }

            if (topLabelHeight > 0)
            {
                top = Math.Max(top, topLabelHeight + 8f);
            }

            if (!string.IsNullOrWhiteSpace(style.ValueAxisTitle))
            {
                var textBounds = new SKRect();
                textPaint.MeasureText(style.ValueAxisTitle, ref textBounds);
                var requiredLeft = (style.LabelSize * 3f) + (textBounds.Height / 2f) + 8f;
                left = Math.Max(left, requiredLeft);

                var verticalExtent = (textBounds.Width / 2f) + style.LabelSize + 4f;
                top = Math.Max(top, verticalExtent);
                bottom = Math.Max(bottom, verticalExtent);
            }

            if (style.ShowSecondaryValueAxis && !string.IsNullOrWhiteSpace(style.SecondaryValueAxisTitle))
            {
                var textBounds = new SKRect();
                textPaint.MeasureText(style.SecondaryValueAxisTitle, ref textBounds);
                if (barOnly)
                {
                    var extra = style.LabelSize + 10f + (textBounds.Height * 2f);
                    top = Math.Max(top, extra);
                }
                else
                {
                    var requiredRight = (style.LabelSize * 3f) + (textBounds.Height / 2f) + 8f;
                    right = Math.Max(right, requiredRight);

                    var verticalExtent = (textBounds.Width / 2f) + style.LabelSize + 4f;
                    top = Math.Max(top, verticalExtent);
                    bottom = Math.Max(bottom, verticalExtent);
                }
            }

            if (!string.IsNullOrWhiteSpace(style.CategoryAxisTitle))
            {
                var textBounds = new SKRect();
                textPaint.MeasureText(style.CategoryAxisTitle, ref textBounds);
                var extra = style.LabelSize + 10f + (textBounds.Height * 2f);
                if (bottomLabelHeight > 0)
                {
                    extra += bottomLabelHeight + 8f;
                }

                bottom = Math.Max(bottom, extra);
            }

            if (!string.IsNullOrWhiteSpace(style.SecondaryCategoryAxisTitle))
            {
                var textBounds = new SKRect();
                textPaint.MeasureText(style.SecondaryCategoryAxisTitle, ref textBounds);
                if (barOnly)
                {
                    var requiredRight = (style.LabelSize * 3f) + (textBounds.Height / 2f) + 8f;
                    right = Math.Max(right, requiredRight);

                    var verticalExtent = (textBounds.Width / 2f) + style.LabelSize + 4f;
                    top = Math.Max(top, verticalExtent);
                    bottom = Math.Max(bottom, verticalExtent);
                }
                else
                {
                    var extra = style.LabelSize + 10f + (textBounds.Height * 2f);
                    if (topLabelHeight > 0)
                    {
                        extra += topLabelHeight + 8f;
                    }

                    top = Math.Max(top, extra);
                }
            }

            if (verticalAscent > 0f)
            {
                top = Math.Max(top, verticalAscent + 4f);
            }

            if (verticalDescent > 0f)
            {
                bottom = Math.Max(bottom, verticalDescent + 4f);
            }

            return new ChartPadding(left, top, right, bottom);
        }

        private static void MeasureAxisLabelBounds(
            double minValue,
            double maxValue,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            ChartValueFormat? valueFormat,
            SkiaChartStyle style,
            bool horizontal,
            float availableLength,
            out float maxWidth,
            out float maxHeight,
            out float maxAscent,
            out float maxDescent)
        {
            maxWidth = 0f;
            maxHeight = 0f;
            maxAscent = 0f;
            maxDescent = 0f;
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
                        labels.Add(FormatAxisValue(value, axisKind, formatter, valueFormat, dateTimeFormat));
                    }

                    var spacing = GetAxisLabelSpacing(ticks.Count, availableLength, useBands: false);
                    var layout = horizontal
                        ? ResolveHorizontalLabelLayout(labels, spacing, style.LabelSize, allowRotation: false, allowEllipsis: false)
                        : ResolveVerticalLabelLayout(labels, spacing, style.LabelSize);

                    using var layoutPaint = CreateTextPaint(style.Text, layout.TextSize);
                    MeasureLabelsWithLayout(labels, layout, layoutPaint, out maxWidth, out maxHeight, out maxAscent, out maxDescent);
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

        private static void MeasureCategoryLabelBounds(
            IReadOnlyList<string?> categories,
            SkiaChartStyle style,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            ChartAxisKind axisKind,
            Func<double, string>? formatter,
            ChartValueFormat? valueFormat,
            bool horizontal,
            float availableLength,
            out float maxWidth,
            out float maxHeight,
            out float maxAscent,
            out float maxDescent)
        {
            maxWidth = 0f;
            maxHeight = 0f;
            maxAscent = 0f;
            maxDescent = 0f;

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
                            labels.Add(FormatCategoryAxisValue(value, axisKind, formatter, valueFormat, dateTimeFormat));
                        }

                        var spacing = GetAxisLabelSpacing(ticks.Count, availableLength, useBands: false);
                        var layout = horizontal
                            ? ResolveHorizontalLabelLayout(labels, spacing, style.LabelSize, allowRotation: true, allowEllipsis: false)
                            : ResolveVerticalLabelLayout(labels, spacing, style.LabelSize);

                        using var layoutPaint = CreateTextPaint(style.Text, layout.TextSize);
                        MeasureLabelsWithLayout(labels, layout, layoutPaint, out maxWidth, out maxHeight, out maxAscent, out maxDescent);
                        return;
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

            var stringLabels = SkiaChartPools.RentList<string>(categories.Count);
            try
            {
                foreach (var category in categories)
                {
                    stringLabels.Add(category ?? string.Empty);
                }

                var labelSpacing = GetAxisLabelSpacing(categories.Count, availableLength, useBands: !horizontal);
                var categoryLayout = horizontal
                    ? ResolveHorizontalLabelLayout(stringLabels, labelSpacing, style.LabelSize, allowRotation: true, allowEllipsis: true)
                    : ResolveVerticalLabelLayout(stringLabels, labelSpacing, style.LabelSize);

                using var categoryPaint = CreateTextPaint(style.Text, categoryLayout.TextSize);
                MeasureLabelsWithLayout(stringLabels, categoryLayout, categoryPaint, out maxWidth, out maxHeight, out maxAscent, out maxDescent);
            }
            finally
            {
                SkiaChartPools.ReturnList(stringLabels);
            }
        }

        private readonly struct ChartPadding
        {
            public ChartPadding(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public float Left { get; }

            public float Top { get; }

            public float Right { get; }

            public float Bottom { get; }
        }

        private static SKPaint CreateTextPaint(SKColor color, float textSize, bool bold = false)
        {
            return new SKPaint
            {
                Color = color,
                TextSize = textSize,
                IsAntialias = true,
                SubpixelText = true,
                HintingLevel = SKPaintHinting.Full,
                FakeBoldText = bold
            };
        }

        private static float AlignCenterX(float center, SKRect bounds)
        {
            return center - bounds.MidX;
        }

        private static float AlignCenterY(float center, SKRect bounds)
        {
            return center - bounds.MidY;
        }

        private static float AlignRightX(float right, SKRect bounds)
        {
            return right - bounds.Right;
        }

        private static float AlignLeftX(float left, SKRect bounds)
        {
            return left - bounds.Left;
        }

        private static float AlignTopY(float top, SKRect bounds)
        {
            return top - bounds.Top;
        }

        private static float AlignBottomY(float bottom, SKRect bounds)
        {
            return bottom - bounds.Bottom;
        }

    }
}
