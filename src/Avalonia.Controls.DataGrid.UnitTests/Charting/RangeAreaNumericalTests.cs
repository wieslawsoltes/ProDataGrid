// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class RangeAreaNumericalTests
    {
        private static SkiaChartStyle Plain() => new()
        {
            ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
            ShowGridlines = false, ShowCategoryGridlines = false, ShowCategoryAxisLine = false, ShowValueAxisLine = false,
            PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
            CategoryAxisKind = ChartAxisKind.Value, ValueAxisMinimum = 0, ValueAxisMaximum = 100,
            Background = SKColors.White, HitTestRadius = 0
        };

        [Fact]
        public void Factory_Rounds_Subnormal_And_Extreme_Midpoints_Without_Intermediate_Overflow()
        {
            var series = ChartRangeSeries.CreateArea("Extreme", new double?[]
                { double.Epsilon, -2 * double.Epsilon, -double.MaxValue, double.MaxValue },
                new double?[] { 2 * double.Epsilon, -double.Epsilon, double.MaxValue, double.MaxValue });
            Assert.Equal(2 * double.Epsilon, series.Values[0]);
            Assert.Equal(-2 * double.Epsilon, series.Values[1]);
            Assert.Equal(0, series.Values[2]);
            Assert.Equal(double.MaxValue, series.Values[3]);
        }

        [Fact]
        public void Seeded_Nonuniform_Intervals_Match_Independent_Interior_And_Gap_Queries()
        {
            Random random = new(30697);
            SKRect bounds = SKRect.Create(840, 500);
            for (int pass = 0; pass < 12; pass++)
            {
                double[] x = new double[26];
                double?[] lows = new double?[26], highs = new double?[26];
                for (int i = 0; i < x.Length; i++)
                {
                    x[i] = i == 0 ? 0 : x[i - 1] + 1 + random.NextDouble() * 2;
                    if (i % 11 == 5) continue;
                    lows[i] = 20 + random.NextDouble() * 40;
                    highs[i] = lows[i] + 8 + random.NextDouble() * 12;
                }
                ChartSeriesSnapshot series = ChartRangeSeries.CreateArea("Intervals", lows, highs, x);
                ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[] { series });
                SkiaChartStyle style = Plain();
                style.CategoryAxisMinimum = x[0]; style.CategoryAxisMaximum = x[^1];
                SkiaChartRenderer renderer = new();
                using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(snapshot, 840, 500, style));
                for (int i = 1; i < x.Length; i++)
                    foreach (double fraction in new[] { 0.2, 0.8 })
                    {
                        double coordinate = x[i - 1] * (1 - fraction) + x[i] * fraction;
                        float pixelX = (float)(coordinate / x[^1] * bounds.Width);
                        if (!lows[i - 1].HasValue || !lows[i].HasValue)
                        {
                            Assert.Null(renderer.HitTest(new SKPoint(pixelX, 250), bounds, snapshot, style));
                            continue;
                        }
                        double low = lows[i - 1]!.Value * (1 - fraction) + lows[i]!.Value * fraction;
                        double high = highs[i - 1]!.Value * (1 - fraction) + highs[i]!.Value * fraction;
                        float pixelY = (float)((100 - (low + high) / 2) * 5);
                        var hit = renderer.HitTest(new SKPoint(pixelX, pixelY), bounds, snapshot, style);
                        Assert.NotNull(hit);
                        int expectedIndex = fraction < 0.5 ? i - 1 : i;
                        Assert.Equal(expectedIndex, hit.Value.PointIndex);
                        Assert.Equal(x[expectedIndex], hit.Value.XValue);
                        Assert.Equal(lows[expectedIndex], hit.Value.LowValue);
                        Assert.Equal(highs[expectedIndex], hit.Value.HighValue);
                        Assert.NotEqual(style.Background, bitmap.GetPixel((int)pixelX, (int)pixelY));
                        Assert.Null(renderer.HitTest(new SKPoint(pixelX, (float)((100 - high - 5) * 5)), bounds, snapshot, style));
                    }
            }
        }

        [Theory]
        [InlineData(ChartAxisKind.Value)]
        [InlineData(ChartAxisKind.DateTime)]
        [InlineData(ChartAxisKind.Logarithmic)]
        public void Numeric_Date_And_Log_Category_Axes_Share_The_Projected_Interval(ChartAxisKind kind)
        {
            double start = kind == ChartAxisKind.DateTime ? 45000 : 1;
            double end = kind == ChartAxisKind.DateTime ? 45010 : 100;
            var series = ChartRangeSeries.CreateArea("Range", new double?[] { 20, 50 }, new double?[] { 40, 80 }, new[] { start, end });
            var snapshot = new ChartDataSnapshot(Array.Empty<string?>(), new[] { series });
            SkiaChartStyle style = Plain(); style.CategoryAxisKind = kind;
            style.CategoryAxisMinimum = start; style.CategoryAxisMaximum = end;
            var hit = new SkiaChartRenderer().HitTest(new SKPoint(100, 300), SKRect.Create(400, 500), snapshot, style);
            Assert.NotNull(hit); Assert.Equal(0, hit.Value.PointIndex);
            Assert.Equal(start, hit.Value.XValue); Assert.Equal(20, hit.Value.LowValue); Assert.Equal(40, hit.Value.HighValue);
        }

        [Fact]
        public void Offset_Plot_Rendering_And_Hit_Testing_Preserve_Canvas_State_And_Clip()
        {
            var snapshot = new ChartDataSnapshot(Array.Empty<string?>(), new[]
                { ChartRangeSeries.CreateArea("Range", new double?[] { 20, 20 }, new double?[] { 80, 80 }) });
            SkiaChartStyle style = Plain(); style.CategoryAxisKind = ChartAxisKind.Category;
            SKRect bounds = new(20, 30, 420, 330);
            using SKBitmap bitmap = new(450, 360);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Magenta);
            int saves = canvas.SaveCount;
            SkiaChartRenderer renderer = new();
            renderer.Render(canvas, bounds, snapshot, style);
            Assert.Equal(saves, canvas.SaveCount);
            Assert.Equal(SKColors.Magenta, bitmap.GetPixel(0, 0));
            Assert.Equal(SKColors.Magenta, bitmap.GetPixel(449, 359));
            var hit = renderer.HitTest(new SKPoint(220, 180), bounds, snapshot, style);
            Assert.NotNull(hit); Assert.Equal(20, hit.Value.LowValue); Assert.Equal(80, hit.Value.HighValue);
            Assert.Null(renderer.HitTest(new SKPoint(10, 180), bounds, snapshot, style));
        }
    }
}
