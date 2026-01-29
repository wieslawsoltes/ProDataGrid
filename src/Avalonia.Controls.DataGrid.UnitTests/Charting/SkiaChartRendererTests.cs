// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class SkiaChartRendererTests
    {
        [Fact]
        public void HitTest_Returns_Column_Series_Point()
        {
            var values = new double?[] { 5d, 10d, 15d };
            var snapshot = new ChartDataSnapshot(
                new[] { "A", "B", "C" },
                new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Column, values) });

            var style = new SkiaChartStyle
            {
                ShowLegend = false,
                ShowAxisLabels = false,
                ShowCategoryLabels = false,
                ShowCategoryAxisLine = false,
                ShowValueAxisLine = false,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                PaddingBottom = 0,
                ValueAxisMinimum = 0
            };

            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 400, 300);

            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));

            var categoryIndex = 1;
            var value = values[categoryIndex]!.Value;
            var seriesCount = snapshot.Series.Count;
            var categoryCount = snapshot.Categories.Count;
            var minValue = 0d;
            var maxValue = values.Max(v => v ?? 0d);
            var plot = viewport.Plot;

            var groupWidth = plot.Width / categoryCount;
            var barWidth = groupWidth / Math.Max(1, seriesCount) * 0.75f;
            var offset = (groupWidth - (barWidth * seriesCount)) / 2f;
            var barLeft = plot.Left + (categoryIndex * groupWidth) + offset;
            var baselineY = MapY(plot, 0d, minValue, maxValue, ChartAxisKind.Value);
            var valueY = MapY(plot, value, minValue, maxValue, ChartAxisKind.Value);
            var top = Math.Min(valueY, baselineY);
            var bottom = Math.Max(valueY, baselineY);
            var point = new SKPoint(barLeft + (barWidth / 2f), (top + bottom) / 2f);

            var hit = renderer.HitTest(point, bounds, snapshot, style);

            Assert.True(hit.HasValue);
            var result = hit!.Value;
            Assert.Equal(0, result.SeriesIndex);
            Assert.Equal(categoryIndex, result.PointIndex);
            Assert.Equal("B", result.Category);
            Assert.Equal(value, result.Value);
        }

        private static float MapY(SKRect plot, double value, double minValue, double maxValue, ChartAxisKind axisKind)
        {
            var normalized = NormalizeAxisValue(value, minValue, maxValue, axisKind);
            return (float)(plot.Bottom - normalized * plot.Height);
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

        private static bool IsInvalidNumber(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}
