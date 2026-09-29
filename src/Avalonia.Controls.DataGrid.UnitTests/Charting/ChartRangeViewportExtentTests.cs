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
    public sealed class ChartRangeViewportExtentTests
    {
        [Theory]
        [InlineData(ChartAxisKind.Value)]
        [InlineData(ChartAxisKind.DateTime)]
        [InlineData(ChartAxisKind.Logarithmic)]
        public void Missing_Window_Tails_Preserve_Their_Original_Automatic_X_Extent(ChartAxisKind axisKind)
        {
            double?[] low = Enumerable.Repeat<double?>(20, 1000).ToArray();
            double?[] high = Enumerable.Repeat<double?>(80, 1000).ToArray();
            double[] x = Enumerable.Range(0, 1000).Select(i => axisKind == ChartAxisKind.Logarithmic
                ? Math.Pow(10, i / 999d * 3) : axisKind == ChartAxisKind.DateTime ? 45000d + i : i).ToArray();
            for (int i = 800; i < 1000; i++) low[i] = null;
            RangeChartDataSource source = new("Missing tail", low, high, x);
            ChartRangeView full = source.BuildView(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None });
            ChartRangeView reduced = source.BuildView(new ChartDataRequest { MaxPoints = 12 });
            Assert.Equal(0, reduced.SourcePointIndices[0]);
            Assert.Equal(999, reduced.SourcePointIndices[^1]);
            Assert.Equal(full.Snapshot.Series[0].XValues![^1], reduced.Snapshot.Series[0].XValues![^1]);
            Assert.Null(reduced.Snapshot.Series[0].LowValues![^1]);
            SkiaChartStyle style = new()
            {
                ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowGridlines = false,
                PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
                CategoryAxisKind = axisKind, ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 0
            };
            // No explicit X domain: the renderer must derive the same domain from both views.
            using SKBitmap fullImage = SKBitmap.Decode(SkiaChartExporter.ExportPng(full.Snapshot, 600, 300, style));
            using SKBitmap reducedImage = SKBitmap.Decode(SkiaChartExporter.ExportPng(reduced.Snapshot, 600, 300, style));
            Assert.Equal(style.Background, fullImage.GetPixel(550, 155));
            Assert.Equal(style.Background, reducedImage.GetPixel(550, 155));
            Assert.NotEqual(style.Background, reducedImage.GetPixel(200, 155));
            SkiaChartRenderer renderer = new();
            Assert.Null(renderer.HitTest(new SKPoint(550, 155), SKRect.Create(600, 300), reduced.Snapshot, style));
        }

        [Fact]
        public void An_AllMissing_View_Retains_Both_Window_End_Coordinates()
        {
            RangeChartDataSource source = new(null, new double?[1000], new double?[1000]);
            ChartRangeView view = source.BuildView(new ChartDataRequest { WindowStart = 100, WindowCount = 500, MaxPoints = 6 });
            Assert.Equal(new[] { 100, 599 }, view.SourcePointIndices);
            Assert.Equal(new double[] { 100, 599 }, view.Snapshot.Series[0].XValues);
            Assert.All(view.Snapshot.Series[0].Values, value => Assert.Null(value));
            // The independent selector has no X domain and retains its documented single separator.
            Assert.Equal(new[] { 100 }, ChartRangeDecimator.SelectIndices(new double?[1000], new double?[1000], 6, 100, 500));
        }

        [Fact]
        public void Domain_Endpoint_Retention_Does_Not_Duplicate_A_OnePoint_Tail()
        {
            double?[] low = Enumerable.Repeat<double?>(1, 100).ToArray(), high = Enumerable.Repeat<double?>(2, 100).ToArray();
            low[^1] = null;
            RangeChartDataSource source = new(null, low, high);
            ChartRangeView view = source.BuildView(new ChartDataRequest { MaxPoints = 6 });
            Assert.Equal(99, view.SourcePointIndices[^1]);
            Assert.Equal(1, view.SourcePointIndices.Count(i => i == 99));
            Assert.True(view.SourcePointIndices.Zip(view.SourcePointIndices.Skip(1), (a, b) => a < b).All(v => v));
        }
    }
}
