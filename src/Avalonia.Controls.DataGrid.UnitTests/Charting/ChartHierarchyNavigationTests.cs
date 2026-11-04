// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartHierarchyNavigationTests
    {
        private static ChartHierarchyNode Tree() => new("root", "Root", new[]
        {
            new ChartHierarchyNode("a", "Alpha", new[]
            {
                new ChartHierarchyNode("deep", "Deep", new[] { new ChartHierarchyNode("leaf", "Leaf", 40) }),
                new ChartHierarchyNode("a2", "Second", 10)
            }),
            new ChartHierarchyNode("b", "Beta", 50)
        });

        [AvaloniaFact]
        public void Unlabeled_Numeric_And_Advanced_Charts_Expose_Real_Hits_And_Tooltips()
        {
            foreach (ChartSeriesKind kind in new[] { ChartSeriesKind.Scatter, ChartSeriesKind.Gauge, ChartSeriesKind.Heatmap })
            {
                var data = new ChartDataSnapshot(Array.Empty<string?>(), new[]
                    { new ChartSeriesSnapshot("Value", kind, new double?[] { 50 }, new double[] { 50 }) });
                using ChartModel model = new() { DataSource = new FixedSource(data) };
                var view = CreateView(model);
                using var host = new Host(view);
                Point point = Find(view, _ => true);
                Assert.NotNull(view.HitTest(point));
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
                view.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, view, pointer, view, point, 0, properties, KeyModifiers.None));
                Assert.Contains("50", Assert.IsType<string>(ToolTip.GetTip(view)));
                Assert.Null(view.HitTest(new Point(double.NaN, 50)));
                Assert.Null(view.HitTest(new Point(-1, -1)));
            }
        }

        [AvaloniaFact]
        public void OptIn_DoubleClick_Uses_Typed_Branch_Identity_For_Both_Hierarchy_Kinds()
        {
            foreach (ChartSeriesKind kind in new[] { ChartSeriesKind.Treemap, ChartSeriesKind.Sunburst })
            {
                HierarchyChartDataSource source = new(Tree(), kind);
                using ChartModel model = new() { DataSource = source };
                var view = CreateView(model);
                using var host = new Host(view);
                Point branch = Find(view, hit => hit.Category == "Alpha");
                DoubleClick(view, branch);
                Assert.Equal("root", source.CurrentRoot.Id);
                ChartHierarchyNavigation.SetIsEnabled(view, true);
                DoubleClick(view, branch);
                Assert.Equal("a", source.CurrentRoot.Id);
                Assert.Equal("a", model.Snapshot.Hierarchy!.Root.Id);
                Assert.True(ChartHierarchyNavigation.TryDrillUp(view));
                ChartHierarchyNavigation.SetIsEnabled(view, false);
                DoubleClick(view, branch);
                Assert.Equal("root", source.CurrentRoot.Id);
            }
        }

        [AvaloniaFact]
        public void Keyboard_Return_Reset_And_Keyboard_Disable_Respect_Hierarchy_State()
        {
            HierarchyChartDataSource source = new(Tree());
            using ChartModel model = new() { DataSource = source };
            var view = CreateView(model);
            using var host = new Host(view);
            ChartHierarchyNavigation.SetIsEnabled(view, true);
            Assert.True(source.TryDrillDown("a"));
            Assert.True(source.TryDrillDown("deep"));
            SendKey(view, Key.Back);
            Assert.Equal("a", source.CurrentRoot.Id);
            SendKey(view, Key.Left, KeyModifiers.Alt);
            Assert.Equal("root", source.CurrentRoot.Id);
            source.TryDrillDown("a"); source.TryDrillDown("deep");
            view.EnableKeyboardNavigation = false;
            SendKey(view, Key.Home);
            Assert.Equal("deep", source.CurrentRoot.Id);
            view.EnableKeyboardNavigation = true;
            SendKey(view, Key.Home);
            Assert.Equal("root", source.CurrentRoot.Id);
            Assert.False(ChartHierarchyNavigation.Reset(view));
        }

        [AvaloniaFact]
        public void Leaf_Clicks_And_Stale_Displayed_Roots_Do_Not_Navigate()
        {
            HierarchyChartDataSource source = new(Tree());
            using ChartModel model = new() { DataSource = source };
            var view = CreateView(model);
            using var host = new Host(view);
            Assert.False(ChartHierarchyNavigation.TryDrillDown(view, Find(view, hit => hit.Category == "Beta")));
            model.AutoRefresh = false;
            var branch = Find(view, hit => hit.Category == "Alpha");
            source.TryDrillDown("a");
            Assert.False(ChartHierarchyNavigation.TryDrillDown(view, branch));
            Assert.False(ChartHierarchyNavigation.TryDrillUp(view));
            Assert.False(ChartHierarchyNavigation.Reset(view));
            model.Refresh();
            Assert.True(ChartHierarchyNavigation.TryDrillUp(view));
        }

        [AvaloniaFact]
        public void Matrix_Projections_Are_Not_Mistaken_For_Navigation_Sources()
        {
            var data = new ChartDataSnapshot(new string?[] { "A", "B" }, new[]
                { new ChartSeriesSnapshot("Matrix", ChartSeriesKind.Treemap, new double?[] { 30, 70 }) });
            using ChartModel model = new() { DataSource = new FixedSource(data) };
            var view = CreateView(model);
            using var host = new Host(view);
            ChartHierarchyNavigation.SetIsEnabled(view, true);
            Assert.False(ChartHierarchyNavigation.TryDrillDown(view, Find(view, _ => true)));
            Assert.False(ChartHierarchyNavigation.TryDrillUp(view));
            Assert.False(ChartHierarchyNavigation.Reset(view));
        }

        [AvaloniaFact]
        public void Repeated_Enable_Is_Idempotent_And_Detach_Reattach_Rebuilds_The_View()
        {
            HierarchyChartDataSource source = new(Tree());
            using ChartModel model = new() { DataSource = source };
            var view = CreateView(model);
            using var host = new Host(view);
            ChartHierarchyNavigation.SetIsEnabled(view, true);
            ChartHierarchyNavigation.SetIsEnabled(view, true);
            source.TryDrillDown("a"); source.TryDrillDown("deep");
            SendKey(view, Key.Back);
            Assert.Equal("a", source.CurrentRoot.Id);
            Assert.NotEmpty(view.ExportPng());
            host.Window.Content = null;
            host.Window.Content = view;
            view.UpdateLayout();
            Assert.NotEmpty(view.ExportPng());
            Assert.NotNull(view.HitTest(Find(view, _ => true)));
        }

        private static ProChartView CreateView(ChartModel model)
        {
            model.Legend.IsVisible = false;
            model.CategoryAxis.IsVisible = false; model.ValueAxis.IsVisible = false;
            model.CategoryAxis.Kind = ChartAxisKind.Value;
            model.CategoryAxis.Minimum = 0; model.CategoryAxis.Maximum = 100;
            model.ValueAxis.Minimum = 0; model.ValueAxis.Maximum = 100;
            return new ProChartView
            {
                Width = 400, Height = 300, ChartModel = model, ShowCrosshair = false,
                ChartStyle = new SkiaChartStyle
                {
                    PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
                    Advanced = new SkiaAdvancedChartStyle { ShowHeatmapColorScale = false, TreemapHeaderHeight = 30 }
                }
            };
        }
        private static Point Find(ProChartView view, Func<SkiaChartHitTestResult, bool> matches)
        {
            for (int y = 3; y < 300; y += 5)
                for (int x = 3; x < 400; x += 5)
                {
                    Point point = new(x, y);
                    if (view.HitTest(point) is { } hit && matches(hit)) return point;
                }
            throw new InvalidOperationException("No expected painted data target found.");
        }
        private static void DoubleClick(ProChartView view, Point point)
        {
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            view.RaiseEvent(new PointerPressedEventArgs(view, pointer, view, point, 0, properties, KeyModifiers.None, 2));
        }
        private static void SendKey(ProChartView view, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
            view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = view, Key = key, KeyModifiers = modifiers });
        private sealed class FixedSource : IChartDataSource
        {
            private readonly ChartDataSnapshot _snapshot;
            public FixedSource(ChartDataSnapshot snapshot) { _snapshot = snapshot; }
            public event EventHandler? DataInvalidated { add { } remove { } }
            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => _snapshot;
        }
        private sealed class Host : IDisposable
        {
            public Host(ProChartView view) { Window = new Window { Width = 400, Height = 300, Content = view }; Window.Show(); view.UpdateLayout(); }
            public Window Window { get; }
            public void Dispose() => Window.Close();
        }
    }
}
