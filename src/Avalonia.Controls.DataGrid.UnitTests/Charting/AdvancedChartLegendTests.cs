// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class AdvancedChartLegendTests
    {
        private static ChartDataSnapshot Hierarchy(ChartSeriesKind kind) => new(
            new ChartHierarchySnapshot(new ChartHierarchyNode("root", "Root", new[]
            {
                new ChartHierarchyNode("alpha", "Alpha", 40),
                new ChartHierarchyNode("beta", "Beta", 60)
            })), kind);

        [Theory]
        [InlineData(ChartSeriesKind.Treemap)]
        [InlineData(ChartSeriesKind.Sunburst)]
        public void Hierarchy_Legends_Name_Colored_Root_Branches_Not_The_Synthetic_Series(ChartSeriesKind kind)
        {
            SkiaChartStyle style = new() { ShowLegend = true, ShowCategoryLabels = false, ShowDataLabels = false };
            var labels = Text(SkiaChartExporter.ExportSvg(Hierarchy(kind), 640, 400, style));
            Assert.Contains(labels, element => element.Value.Trim() == "Alpha");
            Assert.Contains(labels, element => element.Value.Trim() == "Beta");
            Assert.DoesNotContain(labels, element => element.Value.Trim() == "Root");
        }

        [Fact]
        public void Heatmap_Legend_Is_A_Color_Scale_And_Its_Maximum_Labels_The_Right_End()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "Category" }, new[]
            {
                new ChartSeriesSnapshot("Not a color category", ChartSeriesKind.Heatmap, new double?[] { 50 })
            });
            SkiaChartStyle style = new()
            {
                ShowLegend = true, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
                Advanced = new SkiaAdvancedChartStyle { HeatmapMinimum = 0, HeatmapMaximum = 100 }
            };
            var labels = Text(SkiaChartExporter.ExportSvg(snapshot, 640, 400, style));
            Assert.DoesNotContain(labels, element => element.Value.Trim() == "Not a color category");
            XElement minimum = Assert.Single(labels.Where(element => element.Value.Trim() == "0"));
            XElement maximum = Assert.Single(labels.Where(element => element.Value.Trim() == "100"));
            Assert.True(Coordinate(minimum, "x") < 100);
            Assert.True(Coordinate(maximum, "x") > 550);
        }

        [Fact]
        public void Treemap_Category_And_Value_Occupy_Separate_Text_Lines()
        {
            SkiaChartStyle style = new() { ShowLegend = false, ShowCategoryLabels = true, ShowDataLabels = true, LabelSize = 14 };
            var labels = Text(SkiaChartExporter.ExportSvg(Hierarchy(ChartSeriesKind.Treemap), 640, 400, style));
            XElement name = Assert.Single(labels.Where(element => element.Value.Trim() == "Alpha"));
            XElement value = Assert.Single(labels.Where(element => element.Value.Trim() == "40"));
            Assert.True(Coordinate(value, "y") - Coordinate(name, "y") >= style.LabelSize);
        }

        [Fact]
        public void Changing_Gauge_Overrides_Updates_The_Cached_Viewport_Interval()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Gauge", ChartSeriesKind.Gauge, new double?[] { 50 }) });
            SkiaChartStyle style = new() { ShowLegend = false };
            SkiaChartRenderer renderer = new();
            Assert.True(renderer.TryGetViewportInfo(SKRect.Create(200, 200), snapshot, style, out var first));
            Assert.Equal(0, first.MinValue);
            Assert.Equal(100, first.MaxValue);
            style.ValueAxisMinimum = -100;
            style.ValueAxisMaximum = 0;
            Assert.True(renderer.TryGetViewportInfo(SKRect.Create(200, 200), snapshot, style, out var second));
            Assert.Equal(-100, second.MinValue);
            Assert.Equal(0, second.MaxValue);
        }

        private static XElement[] Text(string svg) => XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").ToArray();

        private static double Coordinate(XElement text, string name) => double.Parse(
            text.Attribute(name)!.Value.Split(',')[0], CultureInfo.InvariantCulture);
    }
}
