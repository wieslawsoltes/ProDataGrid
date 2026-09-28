// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class AdvancedChartTests
    {
        private static ChartHierarchyNode Tree() => new("root", "Portfolio", new[]
        {
            new ChartHierarchyNode("a", "Alpha", new[]
            {
                new ChartHierarchyNode("a1", "First", 30),
                new ChartHierarchyNode("a2", "Second", 20)
            }),
            new ChartHierarchyNode("b", "Beta", 50)
        });

        private static SkiaChartStyle PlainStyle() => new()
        {
            ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
            PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
            Advanced = new SkiaAdvancedChartStyle { ShowHeatmapColorScale = false, TreemapGap = 0, TreemapHeaderHeight = 0 }
        };

        [Fact]
        public void Hierarchy_Owns_Children_And_Indexes_Stable_Identifiers()
        {
            ChartHierarchyNode[] children = { new("a", "A", 1), new("b", "B", 2) };
            ChartHierarchyNode root = new("root", null, children);
            children[0] = new ChartHierarchyNode("replacement", null, 99);
            ChartHierarchySnapshot snapshot = new(root);
            Assert.Equal(3, root.TotalValue);
            Assert.Equal("a", root.Children[0].Id);
            Assert.Equal(new[] { "root", "a", "b" }, snapshot.Nodes.Select(n => n.Id));
            Assert.Equal(new[] { -1, 0, 0 }, snapshot.ParentIndices);
            Assert.True(snapshot.TryGetNodeIndex("b", out int index));
            Assert.Equal(2, index);
            Assert.Throws<NotSupportedException>(() => ((IList<ChartHierarchyNode>)root.Children)[0] = children[0]);
        }

        [Fact]
        public void Invalid_Weights_Duplicate_Ids_Overflow_And_Excessive_Depth_Are_Rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChartHierarchyNode("a", null, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChartHierarchyNode("a", null, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChartHierarchyNode("a", null, double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new ChartHierarchySnapshot(new ChartHierarchyNode("root", null,
                new[] { new ChartHierarchyNode("a", null, 1), new ChartHierarchyNode("a", null, 2) })));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChartHierarchyNode("root", null,
                new[] { new ChartHierarchyNode("a", null, double.MaxValue), new ChartHierarchyNode("b", null, double.MaxValue) }));
            ChartHierarchyNode current = new("leaf", null, 1);
            for (int i = 0; i < 64; i++) current = new ChartHierarchyNode("level" + i, null, new[] { current });
            Assert.Equal(64, current.Height);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChartHierarchyNode("tooDeep", null, new[] { current }));
        }

        [Fact]
        public void Treemap_Leaf_Areas_Partition_Weights_And_Hits_Return_Deepest_Node()
        {
            ChartHierarchySnapshot hierarchy = new(Tree());
            var cells = ChartHierarchyLayout.CreateTreemap(hierarchy, 800, 600, 0, 0);
            double totalArea = 0;
            foreach (ChartTreemapCell cell in cells)
            {
                Assert.InRange(cell.Bounds.Left, 0, 800);
                Assert.InRange(cell.Bounds.Right, cell.Bounds.Left, 800);
                Assert.InRange(cell.Bounds.Top, 0, 600);
                Assert.InRange(cell.Bounds.Bottom, cell.Bounds.Top, 600);
                if (!hierarchy.Nodes[cell.NodeIndex].IsLeaf) continue;
                double area = cell.Bounds.Width * cell.Bounds.Height;
                Assert.InRange(Math.Abs(area / (800 * 600) - hierarchy.Nodes[cell.NodeIndex].TotalValue / 100), 0, 1e-10);
                totalArea += area;
                Assert.Equal(cell.NodeIndex, ChartHierarchyLayout.HitTestTreemap(cells,
                    (cell.Bounds.Left + cell.Bounds.Right) / 2, (cell.Bounds.Top + cell.Bounds.Bottom) / 2));
            }
            Assert.InRange(Math.Abs(totalArea - 480000), 0, 1e-7);
            Assert.Equal(-1, ChartHierarchyLayout.HitTestTreemap(cells, -1, 20));
        }

        [Fact]
        public void Seeded_Treemap_Layouts_Preserve_Leaf_Area_And_Do_Not_Overlap()
        {
            Random random = new(6742);
            for (int pass = 0; pass < 15; pass++)
            {
                ChartHierarchyNode[] leaves = Enumerable.Range(0, 40).Select(i => new ChartHierarchyNode(i.ToString(), null, random.NextDouble() * 100 + 1)).ToArray();
                ChartHierarchySnapshot tree = new(new ChartHierarchyNode("root", null, leaves));
                var cells = ChartHierarchyLayout.CreateTreemap(tree, 731, 379, 0, 0);
                Assert.Equal(leaves.Length, cells.Count);
                foreach (ChartTreemapCell cell in cells)
                {
                    double fraction = cell.Bounds.Width * cell.Bounds.Height / (731 * 379);
                    Assert.InRange(Math.Abs(fraction - tree.Nodes[cell.NodeIndex].TotalValue / tree.Root.TotalValue), 0, 1e-9);
                    Assert.Equal(cell.NodeIndex, ChartHierarchyLayout.HitTestTreemap(cells,
                        (cell.Bounds.Left + cell.Bounds.Right) / 2, (cell.Bounds.Top + cell.Bounds.Bottom) / 2));
                }
            }
        }

        [Fact]
        public void Sunburst_Partitions_Parent_Sweeps_And_Respects_Hole_And_Rings()
        {
            ChartHierarchySnapshot hierarchy = new(Tree());
            var sectors = ChartHierarchyLayout.CreateSunburst(hierarchy, 0.2);
            Assert.Equal(360, sectors.Where(s => hierarchy.Depths[s.NodeIndex] == 1).Sum(s => s.SweepAngle), 9);
            foreach (ChartSunburstSector sector in sectors)
            {
                double angle = (sector.StartAngle + sector.SweepAngle / 2) * Math.PI / 180;
                double radius = (sector.InnerRadius + sector.OuterRadius) / 2;
                Assert.Equal(sector.NodeIndex, ChartHierarchyLayout.HitTestSunburst(sectors,
                    Math.Cos(angle) * radius, Math.Sin(angle) * radius));
            }
            Assert.Equal(-1, ChartHierarchyLayout.HitTestSunburst(sectors, 0, 0));
            Assert.Equal(-1, ChartHierarchyLayout.HitTestSunburst(sectors, 2, 0));
        }

        [Fact]
        public void Zero_And_Extreme_Layouts_Are_Finite_And_Deterministic()
        {
            ChartHierarchySnapshot zero = new(new ChartHierarchyNode("zero", null, 0));
            Assert.Empty(ChartHierarchyLayout.CreateTreemap(zero, 200, 100));
            Assert.Empty(ChartHierarchyLayout.CreateSunburst(zero));
            ChartHierarchySnapshot tree = new(Tree());
            Assert.Empty(ChartHierarchyLayout.CreateTreemap(tree, 0, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchyLayout.CreateTreemap(tree, double.NaN, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartHierarchyLayout.CreateSunburst(tree, 1));
            var cells = ChartHierarchyLayout.CreateTreemap(tree, 1e200, 1e200, 0, 0);
            Assert.NotEmpty(cells);
            Assert.All(cells, cell => Assert.True(double.IsFinite(cell.Bounds.Right) && double.IsFinite(cell.Bounds.Bottom)));
        }

        [Fact]
        public void Navigation_Preserves_Old_Snapshots_And_Structural_Data()
        {
            HierarchyChartDataSource source = new(Tree());
            int changes = 0;
            source.DataInvalidated += (_, _) => { changes++; source.BuildSnapshot(new ChartDataRequest()); };
            ChartDataSnapshot original = source.BuildSnapshot(new ChartDataRequest { MaxPoints = 1, WindowCount = 1 });
            Assert.Equal(5, original.Hierarchy!.Nodes.Count);
            Assert.Same(original, source.BuildSnapshot(new ChartDataRequest()));
            Assert.False(source.TryDrillDown("b"));
            Assert.True(source.TryDrillDown("a"));
            Assert.Equal(new[] { "root", "a" }, source.GetPath().Select(n => n.Id));
            ChartDataSnapshot child = source.BuildSnapshot(new ChartDataRequest());
            Assert.Equal(3, child.Hierarchy!.Nodes.Count);
            Assert.Equal(5, original.Hierarchy.Nodes.Count);
            Assert.NotEqual(original.Version, child.Version);
            Assert.True(source.TryDrillUp());
            Assert.False(source.TryDrillUp());
            source.Kind = ChartSeriesKind.Sunburst;
            Assert.Equal(ChartSeriesKind.Sunburst, source.BuildSnapshot(new ChartDataRequest()).Series[0].Kind);
            Assert.Equal(3, changes);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Kind = ChartSeriesKind.Line);
        }

        [Fact]
        public void Matrix_Hierarchy_Preserves_Original_Data_Indices()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B", "C" }, new[]
            {
                new ChartSeriesSnapshot("First", ChartSeriesKind.Treemap, new double?[] { null, 20, 30 }),
                new ChartSeriesSnapshot("Second", ChartSeriesKind.Treemap, new double?[] { 50, null, double.NaN })
            });
            ChartHierarchySnapshot hierarchy = ChartHierarchySnapshot.FromChartData(snapshot);
            Assert.Equal(100, hierarchy.Root.TotalValue);
            Assert.True(hierarchy.TryGetNodeIndex("series/0/1", out int node));
            Assert.Equal(0, hierarchy.SourceSeriesIndices[node]);
            Assert.Equal(1, hierarchy.SourcePointIndices[node]);
            Assert.Equal("B", hierarchy.Categories[node]);
        }

        [Theory]
        [InlineData(ChartSeriesKind.Heatmap)]
        [InlineData(ChartSeriesKind.Treemap)]
        [InlineData(ChartSeriesKind.Sunburst)]
        [InlineData(ChartSeriesKind.Gauge)]
        public void All_Advanced_Families_Render_Export_And_Hit_Test_Actual_Data(ChartSeriesKind kind)
        {
            ChartDataSnapshot snapshot = kind is ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst
                ? new ChartDataSnapshot(new ChartHierarchySnapshot(Tree()), kind)
                : new ChartDataSnapshot(new string?[] { "A", "B", "C" }, new[]
                    { new ChartSeriesSnapshot("Values", kind, new double?[] { 25, 50, 75 }) });
            SkiaChartStyle style = PlainStyle();
            byte[] png = SkiaChartExporter.ExportPng(snapshot, 320, 240, style);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
            using SKBitmap bitmap = SKBitmap.Decode(png);
            Assert.NotNull(bitmap);
            Assert.Contains("<svg", SkiaChartExporter.ExportSvg(snapshot, 320, 240, style));
            SkiaChartRenderer renderer = new();
            SKRect bounds = SKRect.Create(320, 240);
            Assert.True(renderer.TryGetViewportInfo(bounds, snapshot, style, out var viewport));
            Assert.False(viewport.HasCartesianSeries);
            int hits = 0;
            for (int y = 3; y < 240; y += 9)
                for (int x = 3; x < 320; x += 9)
                    if (renderer.HitTest(new SKPoint(x + 0.5f, y + 0.5f), bounds, snapshot, style) is { } hit)
                    {
                        Assert.Equal(kind, hit.SeriesKind);
                        Assert.InRange(hit.PointIndex, 0, snapshot.Series[hit.SeriesIndex].Values.Count - 1);
                        Assert.Equal(snapshot.Series[hit.SeriesIndex].Values[hit.PointIndex], hit.Value);
                        Assert.NotEqual(style.Background, bitmap.GetPixel(x, y));
                        hits++;
                    }
            Assert.True(hits > 10);
        }

        [Fact]
        public void Heatmap_Skips_Gaps_And_Resolves_Ragged_Rows_And_Original_X()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B" }, new[]
            {
                new ChartSeriesSnapshot("First", ChartSeriesKind.Heatmap, new double?[] { 1, null }, new double[] { 10, 20 }),
                new ChartSeriesSnapshot("Second", ChartSeriesKind.Heatmap, new double?[] { 5 })
            });
            SkiaChartRenderer renderer = new();
            SkiaChartStyle style = PlainStyle();
            SKRect bounds = SKRect.Create(200, 200);
            Assert.Equal(10, renderer.HitTest(new SKPoint(50, 50), bounds, snapshot, style)!.Value.XValue);
            Assert.Equal(5, renderer.HitTest(new SKPoint(50, 150), bounds, snapshot, style)!.Value.Value);
            Assert.Null(renderer.HitTest(new SKPoint(150, 50), bounds, snapshot, style));
            Assert.Null(renderer.HitTest(new SKPoint(150, 150), bounds, snapshot, style));
            Assert.Null(renderer.HitTest(new SKPoint(0, 0), bounds, snapshot, style));
        }

        [Fact]
        public void Gauge_Clamps_Painted_Values_But_Preserves_Tooltip_Values()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Gauge", ChartSeriesKind.Gauge, new double?[] { 150 }) });
            SkiaChartStyle style = PlainStyle();
            SkiaChartRenderer renderer = new();
            var hit = renderer.HitTest(new SKPoint(187, 100), SKRect.Create(200, 200), snapshot, style);
            Assert.NotNull(hit);
            Assert.Equal(150, hit.Value.Value);
            Assert.Null(renderer.HitTest(new SKPoint(100, 100), SKRect.Create(200, 200), snapshot, style));
            style.ValueAxisMaximum = 0;
            Assert.Throws<ArgumentException>(() => renderer.HitTest(new SKPoint(187, 100), SKRect.Create(200, 200), snapshot, style));
        }

        [Fact]
        public void Advanced_Style_Copy_And_Changed_Geometry_Invalidate_Layout()
        {
            SkiaChartStyle style = PlainStyle();
            Assert.Same(style.Advanced, new SkiaChartStyle(style).Advanced);
            ChartDataSnapshot snapshot = new(new ChartHierarchySnapshot(new ChartHierarchyNode("root", null, 1)), ChartSeriesKind.Sunburst);
            SkiaChartRenderer renderer = new();
            SKRect bounds = SKRect.Create(200, 200);
            Assert.NotNull(renderer.HitTest(new SKPoint(150, 100), bounds, snapshot, style));
            style.Advanced = style.Advanced with { SunburstInnerRadius = 0.8 };
            Assert.Null(renderer.HitTest(new SKPoint(150, 100), bounds, snapshot, style));
        }

        [Fact]
        public void Invalid_Mixed_Families_And_Negative_Hierarchy_Data_Are_Not_Silently_Reinterpreted()
        {
            ChartDataSnapshot mixed = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Line", ChartSeriesKind.Line, new double?[] { 1 }),
                new ChartSeriesSnapshot("Heatmap", ChartSeriesKind.Heatmap, new double?[] { 1 })
            });
            Assert.Throws<ArgumentException>(() => SkiaChartExporter.ExportPng(mixed, 200, 200));
            ChartDataSnapshot negative = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Negative", ChartSeriesKind.Treemap, new double?[] { -1 }) });
            Assert.Throws<ArgumentException>(() => SkiaChartExporter.ExportPng(negative, 200, 200));
        }

        [Fact]
        public void Empty_And_Nonfinite_Heatmap_Data_Render_Without_Hits()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Missing", ChartSeriesKind.Heatmap, new double?[] { null, double.NaN, double.PositiveInfinity }) });
            SkiaChartRenderer renderer = new();
            Assert.NotEmpty(SkiaChartExporter.ExportPng(snapshot, 200, 200, PlainStyle()));
            Assert.Null(renderer.HitTest(new SKPoint(20, 20), SKRect.Create(200, 200), snapshot, PlainStyle()));
        }
    }
}
