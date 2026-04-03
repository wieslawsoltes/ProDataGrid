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

        [Fact]
        public void HitTest_Returns_Candlestick_Point_With_Financial_Values()
        {
            var snapshot = CreateFinancialSnapshot(ChartSeriesKind.Candlestick);
            var style = CreateFinancialStyle();
            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 400, 300);

            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));

            var series = snapshot.Series[0];
            const int pointIndex = 1;
            var range = GetFinancialRange(series);
            var centerX = MapX(viewport.Plot, pointIndex, series.Values.Count);
            var open = series.OpenValues![pointIndex]!.Value;
            var close = series.Values[pointIndex]!.Value;
            var point = new SKPoint(centerX, (MapY(viewport.Plot, open, range.Min, range.Max, ChartAxisKind.Value) + MapY(viewport.Plot, close, range.Min, range.Max, ChartAxisKind.Value)) / 2f);

            var hit = renderer.HitTest(point, bounds, snapshot, style);

            Assert.True(hit.HasValue);
            var result = hit!.Value;
            Assert.Equal(ChartSeriesKind.Candlestick, result.SeriesKind);
            Assert.Equal(pointIndex, result.PointIndex);
            Assert.Equal("B", result.Category);
            Assert.Equal(open, result.OpenValue);
            Assert.Equal(series.HighValues![pointIndex], result.HighValue);
            Assert.Equal(series.LowValues![pointIndex], result.LowValue);
            Assert.Equal(close, result.CloseValue);
            Assert.Equal(close, result.Value);
        }

        [Fact]
        public void HitTest_Returns_Ohlc_Point_With_Financial_Values()
        {
            var snapshot = CreateFinancialSnapshot(ChartSeriesKind.Ohlc);
            var style = CreateFinancialStyle();
            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 400, 300);

            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));

            var series = snapshot.Series[0];
            const int pointIndex = 1;
            var range = GetFinancialRange(series);
            var centerX = MapX(viewport.Plot, pointIndex, series.Values.Count);
            var close = series.Values[pointIndex]!.Value;
            var point = new SKPoint(centerX, MapY(viewport.Plot, close, range.Min, range.Max, ChartAxisKind.Value));

            var hit = renderer.HitTest(point, bounds, snapshot, style);

            Assert.True(hit.HasValue);
            var result = hit!.Value;
            Assert.Equal(ChartSeriesKind.Ohlc, result.SeriesKind);
            Assert.Equal(pointIndex, result.PointIndex);
            Assert.Equal("B", result.Category);
            Assert.Equal(series.OpenValues![pointIndex], result.OpenValue);
            Assert.Equal(series.HighValues![pointIndex], result.HighValue);
            Assert.Equal(series.LowValues![pointIndex], result.LowValue);
            Assert.Equal(close, result.CloseValue);
            Assert.Equal(close, result.Value);
        }

        [Fact]
        public void HitTest_Returns_Hlc_Point_With_Financial_Values()
        {
            var snapshot = CreateFinancialSnapshot(ChartSeriesKind.Hlc);
            var style = CreateFinancialStyle();
            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 400, 300);

            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));

            var series = snapshot.Series[0];
            const int pointIndex = 1;
            var range = GetFinancialRange(series);
            var centerX = MapX(viewport.Plot, pointIndex, series.Values.Count);
            var close = series.Values[pointIndex]!.Value;
            var point = new SKPoint(centerX, MapY(viewport.Plot, close, range.Min, range.Max, ChartAxisKind.Value));

            var hit = renderer.HitTest(point, bounds, snapshot, style);

            Assert.True(hit.HasValue);
            var result = hit!.Value;
            Assert.Equal(ChartSeriesKind.Hlc, result.SeriesKind);
            Assert.Equal(pointIndex, result.PointIndex);
            Assert.Equal("B", result.Category);
            Assert.Null(result.OpenValue);
            Assert.Equal(series.HighValues![pointIndex], result.HighValue);
            Assert.Equal(series.LowValues![pointIndex], result.LowValue);
            Assert.Equal(close, result.CloseValue);
            Assert.Equal(close, result.Value);
        }

        [Theory]
        [InlineData(ChartSeriesKind.HollowCandlestick)]
        [InlineData(ChartSeriesKind.HeikinAshi)]
        [InlineData(ChartSeriesKind.Range)]
        [InlineData(ChartSeriesKind.Renko)]
        [InlineData(ChartSeriesKind.LineBreak)]
        [InlineData(ChartSeriesKind.Kagi)]
        [InlineData(ChartSeriesKind.PointFigure)]
        public void HitTest_Returns_FinancialBodySeries_Point_With_Financial_Values(ChartSeriesKind kind)
        {
            var snapshot = CreateFinancialSnapshot(kind);
            var style = CreateFinancialStyle();
            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 400, 300);

            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));

            var series = snapshot.Series[0];
            const int pointIndex = 1;
            var range = GetFinancialRange(series);
            var centerX = MapX(viewport.Plot, pointIndex, series.Values.Count);
            var open = series.OpenValues![pointIndex]!.Value;
            var close = series.Values[pointIndex]!.Value;
            var point = new SKPoint(centerX, (MapY(viewport.Plot, open, range.Min, range.Max, ChartAxisKind.Value) + MapY(viewport.Plot, close, range.Min, range.Max, ChartAxisKind.Value)) / 2f);

            var hit = renderer.HitTest(point, bounds, snapshot, style);

            Assert.True(hit.HasValue);
            var result = hit!.Value;
            Assert.Equal(kind, result.SeriesKind);
            Assert.Equal(pointIndex, result.PointIndex);
            Assert.Equal("B", result.Category);
            Assert.Equal(open, result.OpenValue);
            Assert.Equal(series.HighValues![pointIndex], result.HighValue);
            Assert.Equal(series.LowValues![pointIndex], result.LowValue);
            Assert.Equal(close, result.CloseValue);
            Assert.Equal(close, result.Value);
        }

        [Fact]
        public void Render_Draws_FinancialLastPriceLabel_Inside_Tight_Right_Bounds()
        {
            var snapshot = CreateFinancialSnapshot(ChartSeriesKind.Candlestick);
            var style = CreateFinancialStyle();
            style.FinancialShowLastPriceLine = true;
            style.FinancialIncreaseColor = new SKColor(30, 212, 171);
            style.FinancialDecreaseColor = new SKColor(255, 89, 111);
            var renderer = new SkiaChartRenderer();
            var bounds = new SKRect(0, 0, 92, 64);

            using var bitmap = new SKBitmap((int)bounds.Width, (int)bounds.Height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            renderer.Render(canvas, bounds, snapshot, style);

            var maxVerticalRun = 0;
            for (var x = bitmap.Width - 22; x < bitmap.Width; x++)
            {
                var currentRun = 0;
                for (var y = 0; y < bitmap.Height; y++)
                {
                    if (bitmap.GetPixel(x, y).Alpha > 0)
                    {
                        currentRun++;
                        if (currentRun > maxVerticalRun)
                        {
                            maxVerticalRun = currentRun;
                        }
                    }
                    else
                    {
                        currentRun = 0;
                    }
                }
            }

            Assert.True(maxVerticalRun >= 6);
        }

        private static ChartDataSnapshot CreateFinancialSnapshot(ChartSeriesKind kind)
        {
            double?[]? openValues = kind == ChartSeriesKind.Hlc
                ? null
                : new double?[] { 9d, 10d, 8.5d };

            return new ChartDataSnapshot(
                new[] { "A", "B", "C" },
                new[]
                {
                    new ChartSeriesSnapshot(
                        "Price",
                        kind,
                        new double?[] { 10d, 12d, 9d },
                        openValues: openValues,
                        highValues: new double?[] { 11d, 13d, 10d },
                        lowValues: new double?[] { 8d, 9d, 8d })
                });
        }

        private static SkiaChartStyle CreateFinancialStyle()
        {
            return new SkiaChartStyle
            {
                ShowLegend = false,
                ShowAxisLabels = false,
                ShowCategoryLabels = false,
                ShowCategoryAxisLine = false,
                ShowValueAxisLine = false,
                PaddingLeft = 0,
                PaddingRight = 0,
                PaddingTop = 0,
                PaddingBottom = 0
            };
        }

        private static (double Min, double Max) GetFinancialRange(ChartSeriesSnapshot series)
        {
            var min = series.LowValues!.Min(v => v ?? 0d);
            var max = series.HighValues!.Max(v => v ?? 0d);
            return (min, max);
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
