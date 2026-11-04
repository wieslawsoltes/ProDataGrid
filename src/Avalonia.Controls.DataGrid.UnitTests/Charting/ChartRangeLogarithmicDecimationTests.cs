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
    public sealed class ChartRangeLogarithmicDecimationTests
    {
        [Fact]
        public void Logarithmic_Invalid_Intervals_Are_Gaps_Before_Selection_And_Rendering()
        {
            double?[] low = Enumerable.Repeat<double?>(10, 1000).ToArray(), high = Enumerable.Repeat<double?>(100, 1000).ToArray();
            for (int i = 400; i <= 600; i++) low[i] = i == 500 ? 0 : -1;
            RangeChartDataSource source = new("Log band", low, high, valueAxisKind: ChartAxisKind.Logarithmic);
            ChartRangeView view = source.BuildView(new ChartDataRequest { MaxPoints = 12 });
            int gap = view.SourcePointIndices.ToList().IndexOf(400);
            Assert.True(gap >= 0); Assert.Null(view.Snapshot.Series[0].LowValues![gap]); Assert.Null(view.Snapshot.Series[0].HighValues![gap]);
            Assert.Contains(399, view.SourcePointIndices); Assert.Contains(601, view.SourcePointIndices);
            SkiaChartStyle style = new()
            {
                ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
                PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
                CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 0, CategoryAxisMaximum = 999,
                ValueAxisKind = source.ValueAxisKind, ValueAxisMinimum = 1, ValueAxisMaximum = 1000, HitTestRadius = 0
            };
            using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(view.Snapshot, 600, 300, style));
            Assert.Equal(style.Background, bitmap.GetPixel(300, 150));
            Assert.NotEqual(style.Background, bitmap.GetPixel(100, 150));
            Assert.Null(new SkiaChartRenderer().HitTest(new SKPoint(300, 150), SKRect.Create(600, 300), view.Snapshot, style));
        }

        [Fact]
        public void Linear_Domain_Retains_Negative_Intervals_While_Log_Domain_Does_Not()
        {
            double?[] low = { -2, 0, 1 }, high = { -1, 10, 10 };
            RangeChartDataSource linear = new(null, low, high);
            RangeChartDataSource logarithmic = new(null, low, high, valueAxisKind: ChartAxisKind.Logarithmic);
            Assert.Equal(low, linear.BuildSnapshot(new ChartDataRequest()).Series[0].LowValues);
            Assert.Equal(new double?[] { null, null, 1 }, logarithmic.BuildSnapshot(new ChartDataRequest()).Series[0].LowValues);
            Assert.Equal(new double?[] { null, null, 10 }, logarithmic.BuildSnapshot(new ChartDataRequest()).Series[0].HighValues);
        }

        [Fact]
        public void Log_Filter_Does_Not_Hide_Invalid_Inverted_Pairs_Or_Mutate_Inputs()
        {
            double?[] low = { -2, 1 }, high = { -1, 10 };
            RangeChartDataSource source = new(null, low, high, valueAxisKind: ChartAxisKind.Logarithmic);
            Assert.Equal(new double?[] { -2, 1 }, low);
            var before = source.BuildSnapshot(new ChartDataRequest());
            Assert.Throws<ArgumentException>(() => source.ReplaceData(new double?[] { -1 }, new double?[] { -2 }));
            Assert.Same(before, source.BuildSnapshot(new ChartDataRequest()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RangeChartDataSource(null, low, high, valueAxisKind: ChartAxisKind.Category));
        }
    }
}
