// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.IO;
using System.Xml.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class AdvancedChartPresentationTests
    {
        [Theory]
        [InlineData(ChartSeriesKind.Heatmap)]
        [InlineData(ChartSeriesKind.Treemap)]
        [InlineData(ChartSeriesKind.Sunburst)]
        [InlineData(ChartSeriesKind.Gauge)]
        public void Labeled_Presentations_Export_And_Do_Not_Modify_The_Canvas_Outside_Their_Bounds(ChartSeriesKind kind)
        {
            ChartDataSnapshot snapshot = CreateSnapshot(kind);
            SkiaChartStyle style = new()
            {
                ShowDataLabels = true,
                ShowLegend = kind == ChartSeriesKind.Gauge,
                LabelSize = 14,
                DataLabelTextSize = 14,
                Background = new SKColor(248, 250, 253),
                Text = new SKColor(25, 32, 47),
                Advanced = new SkiaAdvancedChartStyle
                {
                    HeatmapLowColor = new SKColor(226, 239, 250),
                    HeatmapHighColor = new SKColor(42, 165, 175),
                    TreemapHeaderHeight = 28,
                    TreemapGap = 4
                }
            };
            byte[] png = SkiaChartExporter.ExportPng(snapshot, 960, 600, style);
            using SKBitmap image = SKBitmap.Decode(png);
            Assert.Equal(960, image.Width);
            Assert.Equal(600, image.Height);
            string svg = SkiaChartExporter.ExportSvg(snapshot, 960, 600, style);
            Assert.Equal("svg", XDocument.Parse(svg).Root!.Name.LocalName);

            using SKBitmap bitmap = new(1000, 640);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Magenta);
            int saveCount = canvas.SaveCount;
            SkiaChartRenderer renderer = new();
            SKRect bounds = new(20, 20, 980, 620);
            renderer.Render(canvas, bounds, snapshot, style);
            Assert.Equal(saveCount, canvas.SaveCount);
            Assert.Equal(SKColors.Magenta, bitmap.GetPixel(0, 0));
            Assert.Equal(SKColors.Magenta, bitmap.GetPixel(999, 639));
            Assert.NotEqual(SKColors.Magenta, bitmap.GetPixel(30, 30));
            // Reuse managed layout, then switch to a different snapshot/style on the same renderer.
            renderer.Render(canvas, bounds, snapshot, new SkiaChartStyle(style));
            renderer.Render(canvas, bounds, CreateSnapshot(ChartSeriesKind.Heatmap), style);
            Assert.Equal(saveCount, canvas.SaveCount);

            // Optional diagnostics use a known CI workspace, never an input-controlled path.
            // The charting workflow uploads this folder with its TRX and benchmark evidence.
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "gallery");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, kind + ".png"), png);
                File.WriteAllText(Path.Combine(directory, kind + ".svg"), svg);
            }
        }

        [Fact]
        public void Diverging_And_Extreme_Heatmap_Intervals_Are_Renderable()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "Low", "Center", "High" }, new[]
            {
                new ChartSeriesSnapshot("Deviation", ChartSeriesKind.Heatmap,
                    new double?[] { -double.MaxValue, 0, double.MaxValue })
            });
            SkiaChartStyle style = new()
            {
                ShowLegend = false,
                Advanced = new SkiaAdvancedChartStyle
                {
                    HeatmapMinimum = -double.MaxValue,
                    HeatmapMaximum = double.MaxValue,
                    HeatmapCenterValue = 0,
                    HeatmapCenterColor = SKColors.White
                }
            };
            Assert.NotEmpty(SkiaChartExporter.ExportPng(snapshot, 400, 300, style));
            style.Advanced = style.Advanced with { HeatmapMinimum = 0, HeatmapMaximum = 1, HeatmapCenterValue = 2 };
            Assert.Throws<ArgumentException>(() => SkiaChartExporter.ExportPng(snapshot, 400, 300, style));
        }

        private static ChartDataSnapshot CreateSnapshot(ChartSeriesKind kind)
        {
            if (kind is ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst)
            {
                ChartHierarchyNode root = new("root", "Portfolio", new[]
                {
                    new ChartHierarchyNode("platform", "Platform", new[]
                    {
                        new ChartHierarchyNode("compute", "Compute", 34),
                        new ChartHierarchyNode("storage", "Storage", 21),
                        new ChartHierarchyNode("network", "Network", 15)
                    }),
                    new ChartHierarchyNode("products", "Products", new[]
                    {
                        new ChartHierarchyNode("analytics", "Analytics", 27),
                        new ChartHierarchyNode("automation", "Automation", 19)
                    }),
                    new ChartHierarchyNode("support", "Support", 16)
                });
                return new ChartDataSnapshot(new ChartHierarchySnapshot(root), kind);
            }
            return new ChartDataSnapshot(new string?[] { "Mon", "Tue", "Wed", "Thu", "Fri" }, new[]
            {
                new ChartSeriesSnapshot("North", kind, new double?[] { 25, 48, 62, 35, 81 }),
                new ChartSeriesSnapshot("South", kind, new double?[] { 39, 65, 20, null, 72 }),
                new ChartSeriesSnapshot("West", kind, new double?[] { 55, 31, 74, 91, 48 })
            });
        }
    }
}
