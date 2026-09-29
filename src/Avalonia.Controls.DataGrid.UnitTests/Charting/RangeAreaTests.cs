// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class RangeAreaTests
    {
        private static SkiaChartStyle Plain() => new()
        {
            ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
            ShowGridlines = false, ShowCategoryGridlines = false, ShowCategoryAxis = false, ShowValueAxis = false,
            PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
            Background = SKColors.White, SeriesColors = new[] { SKColors.CornflowerBlue }, AreaFillOpacity = 0.6f,
            HitTestRadius = 0
        };

        private static ChartDataSnapshot Snapshot(ChartSeriesSnapshot series) => new(Array.Empty<string?>(), new[] { series });

        [Fact]
        public void Factory_Owns_Aligned_Boundaries_Midpoints_And_X_And_Normalizes_Gaps()
        {
            double?[] low = { 1, null, 3, double.NaN }, high = { 5, 6, 7, 8 };
            double[] x = { 10, 20, 30, 40 };
            ChartSeriesSnapshot series = ChartRangeSeries.CreateArea("Interval", low, high, x, ChartValueAxisAssignment.Secondary);
            low[0] = 99; high[0] = 99; x[0] = 99;
            Assert.Equal(ChartSeriesKind.RangeArea, series.Kind);
            Assert.Equal(ChartValueAxisAssignment.Secondary, series.ValueAxisAssignment);
            Assert.Equal(new double?[] { 3, null, 5, null }, series.Values);
            Assert.Equal(new double?[] { 1, null, 3, null }, series.LowValues);
            Assert.Equal(new double?[] { 5, null, 7, null }, series.HighValues);
            Assert.Equal(new double[] { 10, 20, 30, 40 }, series.XValues);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)series.LowValues!)[0] = 0);
            Assert.Throws<NotSupportedException>(() => ((IList<double>)series.XValues!)[0] = 0);
        }

        [Fact]
        public void Factory_Rejects_Inverted_Intervals_Ragged_Channels_And_Nonmonotonic_X()
        {
            Assert.Throws<ArgumentNullException>(() => ChartRangeSeries.CreateArea(null, null!, new double?[] { 1 }));
            Assert.Throws<ArgumentException>(() => ChartRangeSeries.CreateArea(null, new double?[] { 1 }, Array.Empty<double?>()));
            Assert.Throws<ArgumentException>(() => ChartRangeSeries.CreateArea(null, new double?[] { 3 }, new double?[] { 1 }));
            Assert.Throws<ArgumentException>(() => ChartRangeSeries.CreateArea(null, new double?[] { 1 }, new double?[] { 2 }, Array.Empty<double>()));
            foreach (double[] x in new[] { new[] { 1d, 1d }, new[] { 2d, 1d }, new[] { 1d, double.NaN } })
                Assert.Throws<ArgumentException>(() => ChartRangeSeries.CreateArea(null, new double?[] { 1, 1 }, new double?[] { 2, 2 }, x));
            Assert.Empty(ChartRangeSeries.CreateArea(null, Array.Empty<double?>(), Array.Empty<double?>()).Values);
        }

        [Fact]
        public void Bollinger_And_Donchian_Bands_Convert_Without_Changing_Their_Source()
        {
            double?[] values = Enumerable.Range(0, 30).Select(i => (double?)(100 + Math.Sin(i))).ToArray();
            double[] x = Enumerable.Range(0, 30).Select(i => i * 10d).ToArray();
            ChartSeriesSnapshot price = new("Price", ChartSeriesKind.Line, values, x,
                valueAxisAssignment: ChartValueAxisAssignment.Secondary,
                highValues: values.Select(v => v + 2).ToArray(), lowValues: values.Select(v => v - 2).ToArray());
            foreach (ChartBandSeries bands in new[] { ChartIndicators.BollingerBands(price, 5), ChartIndicators.DonchianChannels(price, 5) })
            {
                ChartSeriesSnapshot range = bands.ToRangeArea("Band");
                Assert.Equal(bands.Lower.Values, range.LowValues);
                Assert.Equal(bands.Upper.Values, range.HighValues);
                Assert.Equal(x, range.XValues);
                Assert.NotSame(bands.Lower.Values, range.LowValues);
                Assert.Equal(ChartValueAxisAssignment.Secondary, range.ValueAxisAssignment);
            }
        }

        [Fact]
        public void Automatic_Value_Range_Uses_Both_Boundaries_Not_Midpoints()
        {
            var snapshot = Snapshot(ChartRangeSeries.CreateArea("Range", new double?[] { -20, 10 }, new double?[] { 30, 90 }));
            Assert.True(new SkiaChartRenderer().TryGetViewportInfo(SKRect.Create(400, 300), snapshot, Plain(), out var info));
            Assert.Equal(-20, info.MinValue); Assert.Equal(90, info.MaxValue); Assert.True(info.HasCartesianSeries);
        }

        [Fact]
        public void Filled_Interior_Is_Selectable_With_Original_Endpoint_Values_And_Pixels()
        {
            var snapshot = Snapshot(ChartRangeSeries.CreateArea("Range", new double?[] { 2, 2 }, new double?[] { 8, 8 }, new double[] { 10, 30 }));
            SkiaChartStyle style = Plain(); style.CategoryAxisKind = ChartAxisKind.Value;
            style.ValueAxisMinimum = 0; style.ValueAxisMaximum = 10;
            SkiaChartRenderer renderer = new(); SKRect bounds = SKRect.Create(400, 300);
            var hit = renderer.HitTest(new SKPoint(100, 150), bounds, snapshot, style);
            Assert.NotNull(hit); Assert.Equal(0, hit.Value.PointIndex);
            Assert.Equal(2, hit.Value.LowValue); Assert.Equal(8, hit.Value.HighValue);
            Assert.Equal(5, hit.Value.Value); Assert.Equal(10, hit.Value.XValue); Assert.Null(hit.Value.CloseValue);
            Assert.Null(renderer.HitTest(new SKPoint(100, 20), bounds, snapshot, style));
            using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(snapshot, 400, 300, style));
            Assert.NotEqual(style.Background, bitmap.GetPixel(100, 150));
            Assert.Equal(style.Background, bitmap.GetPixel(100, 20));
        }

        [Fact]
        public void Missing_Or_Invalid_Boundaries_Do_Not_Bridge_Gaps()
        {
            var series = new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[] { 5, 5, 5, 5, 5 },
                highValues: new double?[] { 8, 8, null, 8, 8 }, lowValues: new double?[] { 2, 2, 2, 2, 2 });
            var snapshot = Snapshot(series); SkiaChartStyle style = Plain();
            style.ValueAxisMinimum = 0; style.ValueAxisMaximum = 10;
            SkiaChartRenderer renderer = new(); SKRect bounds = SKRect.Create(400, 300);
            Assert.NotNull(renderer.HitTest(new SKPoint(50, 150), bounds, snapshot, style));
            Assert.NotNull(renderer.HitTest(new SKPoint(350, 150), bounds, snapshot, style));
            Assert.Null(renderer.HitTest(new SKPoint(200, 150), bounds, snapshot, style));
            using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(snapshot, 400, 300, style));
            Assert.Equal(style.Background, bitmap.GetPixel(200, 150));
            Assert.NotEqual(style.Background, bitmap.GetPixel(350, 150));
        }

        [Fact]
        public void Numeric_Viewport_Clips_Original_Slopes_Without_Clamping_Endpoints()
        {
            var snapshot = Snapshot(ChartRangeSeries.CreateArea("Slope", new double?[] { 0, 10 }, new double?[] { 2, 12 }, new double[] { 0, 10 }));
            SkiaChartStyle style = Plain(); style.CategoryAxisKind = ChartAxisKind.Value;
            style.CategoryAxisMinimum = 4; style.CategoryAxisMaximum = 6;
            style.ValueAxisMinimum = 0; style.ValueAxisMaximum = 12;
            SkiaChartRenderer renderer = new(); SKRect bounds = SKRect.Create(400, 300);
            Assert.NotNull(renderer.HitTest(new SKPoint(40, 170), bounds, snapshot, style));
            Assert.Null(renderer.HitTest(new SKPoint(40, 260), bounds, snapshot, style));
            using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(snapshot, 400, 300, style));
            Assert.NotEqual(style.Background, bitmap.GetPixel(40, 170));
            Assert.Equal(style.Background, bitmap.GetPixel(40, 260));
        }

        [Fact]
        public void Secondary_Logarithmic_Axis_Maps_Both_Boundaries_And_Rejects_Nonpositive_Intervals()
        {
            var snapshot = Snapshot(ChartRangeSeries.CreateArea("Log", new double?[] { 10, 10 }, new double?[] { 100, 100 },
                valueAxisAssignment: ChartValueAxisAssignment.Secondary));
            SkiaChartStyle style = Plain(); style.SecondaryValueAxisKind = ChartAxisKind.Logarithmic;
            style.SecondaryValueAxisMinimum = 1; style.SecondaryValueAxisMaximum = 1000;
            style.ShowSecondaryValueAxis = false;
            var hit = new SkiaChartRenderer().HitTest(new SKPoint(200, 150), SKRect.Create(400, 300), snapshot, style);
            Assert.NotNull(hit); Assert.Equal(10, hit.Value.LowValue); Assert.Equal(100, hit.Value.HighValue);
            Assert.Null(new SkiaChartRenderer().HitTest(new SKPoint(200, 25), SKRect.Create(400, 300), snapshot, style));
            var invalid = Snapshot(ChartRangeSeries.CreateArea("Invalid log", new double?[] { -1, -1 }, new double?[] { 10, 10 }));
            style.ValueAxisKind = ChartAxisKind.Logarithmic;
            Assert.Null(new SkiaChartRenderer().HitTest(new SKPoint(200, 150), SKRect.Create(400, 300), invalid, style));
        }

        [Fact]
        public void Cache_And_Uncached_Rendering_Agree_And_Changed_Boundaries_Invalidate_Data()
        {
            SkiaChartStyle style = Plain(); style.ValueAxisMinimum = 0; style.ValueAxisMaximum = 10;
            var first = Snapshot(ChartRangeSeries.CreateArea("First", new double?[] { 2, 2 }, new double?[] { 4, 4 }));
            var second = Snapshot(ChartRangeSeries.CreateArea("Second", new double?[] { 6, 6 }, new double?[] { 8, 8 }));
            SkiaChartRenderer renderer = new(); using SkiaChartRenderCache cache = new();
            using SKBitmap bitmap = new(400, 300); using SKCanvas canvas = new(bitmap);
            renderer.Render(canvas, SKRect.Create(400, 300), first, style, cache);
            renderer.Render(canvas, SKRect.Create(400, 300), second, style, cache);
            using SKBitmap direct = SKBitmap.Decode(SkiaChartExporter.ExportPng(second, 400, 300, style));
            for (int y = 10; y < 290; y += 11)
                for (int x = 10; x < 390; x += 11) Assert.Equal(direct.GetPixel(x, y), bitmap.GetPixel(x, y));
        }

        [Fact]
        public void Range_Legend_Labels_And_Svg_Are_Rendered_By_The_Standard_Pipeline()
        {
            var snapshot = new ChartDataSnapshot(new string?[] { "A", "B", "C" }, new[]
                { ChartRangeSeries.CreateArea("Forecast interval", new double?[] { 10, 20, 15 }, new double?[] { 30, 45, 40 }) });
            SkiaChartStyle style = new() { ShowDataLabels = true, AreaFillOpacity = 0.4f };
            string svg = SkiaChartExporter.ExportSvg(snapshot, 800, 480, style);
            var texts = XDocument.Parse(svg).Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToArray();
            Assert.Contains(texts, t => t.Contains("Forecast interval", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("10 – 30", StringComparison.Ordinal));
            Assert.NotEmpty(SkiaChartExporter.ExportPng(snapshot, 800, 480, style));
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "gallery"); Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "RangeArea.svg"), svg);
                File.WriteAllBytes(Path.Combine(directory, "RangeArea.png"), SkiaChartExporter.ExportPng(snapshot, 800, 480, style));
            }
        }

        [Fact]
        public void Raw_Ragged_And_Inverted_Pairs_Are_Gaps_And_A_Single_Interval_Is_Selectable()
        {
            var raw = Snapshot(new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[] { 0, 0, 0 },
                highValues: new double?[] { 8, 2 }, lowValues: new double?[] { 2, 8, 2 }));
            SkiaChartStyle style = Plain(); style.ValueAxisMinimum = 0; style.ValueAxisMaximum = 10;
            SkiaChartRenderer renderer = new();
            Assert.Null(renderer.HitTest(new SKPoint(200, 150), SKRect.Create(400, 300), raw, style));
            var single = Snapshot(ChartRangeSeries.CreateArea("Single", new double?[] { 2 }, new double?[] { 8 }));
            Assert.NotNull(renderer.HitTest(new SKPoint(200, 150), SKRect.Create(400, 300), single, style));
        }
    }
}
