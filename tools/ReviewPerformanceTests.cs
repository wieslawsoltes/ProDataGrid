using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Linq;
using System.Text.Json;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Data.Core;
using System.Collections.Generic;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class ReviewPerformanceTests
{
    [AvaloniaFact]
    public void MatchedWorkload()
    {
        string scenario = Environment.GetEnvironmentVariable("REVIEW_SCENARIO") ?? "scroll";
        if (scenario == "lookup" || scenario == "lookup-dense" || scenario == "clipboard") { RunModelScenario(scenario); return; }
        int count = scenario == "selection" || scenario.StartsWith("cells") ? 2000 : 10000;
        var items = new ObservableCollection<string>(Enumerable.Range(0, count).Select(i => "Row " + i));
        var grid = new DataGrid { AutoGenerateColumns = false, RowHeight = 24, UseLogicalScrollable = true,
            SelectionMode = DataGridSelectionMode.Extended, ItemsSource = items };
        for (int i = 0; i < 8; i++)
            grid.Columns.Add(new DataGridTextColumn { Header = "C" + i, Width = new DataGridLength(100),
                Binding = new CompiledBindingExtension(new CompiledBindingPathBuilder().Build()) });
        HierarchicalModel? hierarchy = null;
        if (scenario == "hierarchy")
        {
            var roots = new List<ReviewNode>();
            for (int i = 0; i < 100; i++)
            {
                var node = new ReviewNode("Root " + i);
                for (int c = 0; c < 10; c++) node.Children.Add(new ReviewNode("Child " + c));
                roots.Add(node);
            }
            hierarchy = new HierarchicalModel(new HierarchicalOptions { ChildrenSelector = item => ((ReviewNode)item).Children,
                IsLeafSelector = item => ((ReviewNode)item).Children.Count == 0 });
            hierarchy.SetRoots(roots);
            grid.Columns.Clear();
            var path = new CompiledBindingPathBuilder()
                .Property(new ClrPropertyInfo("Item", item => ((HierarchicalNode)item).Item, null, typeof(object)), PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                .Property(new ClrPropertyInfo("Name", item => ((ReviewNode)item).Name, null, typeof(string)), PropertyInfoAccessorFactory.CreateInpcPropertyAccessor).Build();
            var column = new DataGridHierarchicalColumn { Header = "Name", Width = new DataGridLength(300), Binding = new CompiledBindingExtension(path) };
            DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<HierarchicalNode, string>(node => ((ReviewNode)node.Item).Name));
            grid.Columns.Add(column);
            grid.HierarchicalModel = hierarchy; grid.HierarchicalRowsEnabled = true;
            grid.ItemsSource = hierarchy.ObservableFlattened;
            grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        }
        if (scenario == "details")
        {
            grid.RowDetailsTemplate = new FuncDataTemplate<string>((_, _) => new Border { Height = 80 });
            grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
        }
        if (scenario == "groups")
        {
            var view = new DataGridCollectionView(items);
            view.GroupDescriptions.Add(new ReviewGroupDescription());
            grid.ItemsSource = view;
        }
        var window = new Window { Width = 900, Height = 500, Content = grid };
        window.SetThemeStyles(scenario == "hierarchy" ? DataGridTheme.SimpleFlat : DataGridTheme.SimpleV2);
        Assert.True(grid.TryFindResource(scenario == "hierarchy" ? "DataGridFlatTheme" : typeof(DataGrid), out object? resource));
        grid.Theme = Assert.IsType<Avalonia.Styling.ControlTheme>(resource);
        try
        {
            window.Show(); Pump(window);
            if (scenario == "selection")
                for (int i = 0; i < count; i += 10) grid.SelectedItems.Add(items[i]);
#if REVIEW_TRACKED_CELLS
            if (scenario == "cells-tracked") grid.TrackSelectedCellsByItem = true;
#endif
            if (scenario.StartsWith("cells")) { grid.SelectionUnit = DataGridSelectionUnit.Cell; grid.SelectAllCells(); }
            if (scenario == "focused")
            {
                grid.ScrollIntoView(items[5000], grid.Columns[0]); Pump(window);
                var row = grid.DisplayData.GetScrollingRows().First(r => ReferenceEquals(r.DataContext, items[5000]));
                row.Focusable = true; Assert.True(row.Focus());
            }
            int selected = grid.SelectedItems.Count;
            int selectedCells = grid.SelectedCells.Count;
            string[] alternate = items.ToArray();
            var header = grid.GetVisualDescendants().OfType<DataGridColumnHeader>().First(h => h.OwningColumn == grid.Columns[0]);
            Point headerPoint = header.TranslatePoint(new Point(30, 10), window)!.Value;
            Action<int> operation = scenario switch
            {
                "selection" or "idle" or "cells" or "cells-tracked" => i => { items.Insert(0, "inserted"); items.RemoveAt(0); Pump(window); },
                "hierarchy" => i => { hierarchy!.ExpandAll(); Pump(window); Assert.Equal(1100, hierarchy.Count); hierarchy.CollapseAll(); Pump(window); Assert.Equal(100, hierarchy.Count); },
                "header" => i => { window.MouseMove(headerPoint); window.MouseDown(headerPoint, MouseButton.Left); window.MouseMove(headerPoint + new Vector(35, 0), RawInputModifiers.LeftMouseButton); window.MouseUp(headerPoint + new Vector(35, 0), MouseButton.Left); Pump(window); },
                "groups" => i => { grid.CollapseAllGroups(); Pump(window); grid.ExpandAllGroups(); Pump(window); },
                "source" => i => { grid.ItemsSource = i % 2 == 0 ? alternate : items; Pump(window); },
                "create" => i => { for (int j = 0; j < 100; j++) _ = new DataGrid(); },
                _ => i => { grid.ScrollIntoView(items[i % 2 == 0 ? 5000 : 0], grid.Columns[0]); Pump(window); }
            };
            for (int i = 0; i < 32; i++) operation(i);
            var times = new double[128]; var bytes = new long[128];
            for (int i = 0; i < times.Length; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
                operation(i);
                times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.Equal(count, items.Count);
            if (scenario != "groups" && scenario != "hierarchy") Assert.Equal(count, grid.SlotCount);
            if (scenario.StartsWith("cells")) Assert.Equal(selectedCells, grid.SelectedCells.Count);
            if (scenario == "selection") Assert.Equal(selected, grid.SelectedItems.Count);
            Assert.True(grid.DisplayData.NumDisplayedScrollingElements > 0);
            string output = Environment.GetEnvironmentVariable("REVIEW_OUTPUT")!;
            File.AppendAllText(output, JsonSerializer.Serialize(new { scenario, times, bytes, count, selected, selectedCells, virtualSurface = grid.DisplayData.HasVirtualScrollingElements }) + Environment.NewLine);
        }
        finally { window.Close(); }
    }
    private static void RunModelScenario(string scenario)
    {
        object[] items = new object[20000];
        for (int i = 0; i < items.Length; i++) items[i] = new object();
        var connection = new DataGridDataConnection(new DataGrid()) { DataSource = items };
        var rows = new List<DataGridRowClipboardEventArgs>();
        for (int i = 0; i < 2000; i++)
        {
            var row = new DataGridRowClipboardEventArgs(null, false);
            for (int c = 0; c < 8; c++) row.ClipboardRowContent.Add(new DataGridClipboardCellContent(null, null!, "Alpha"));
            rows.Add(row);
        }
        var context = new DataGridClipboardExportContext(new DataGrid(), rows, DataGridClipboardCopyMode.ExcludeHeader,
            DataGridClipboardExportFormat.Text, DataGridSelectionUnit.FullRow);
        var exporter = new TextClipboardFormatExporter();
        Action operation = scenario == "clipboard" ? () => {
            var transfer = new DataTransferItem(); Assert.True(exporter.TryExport(context, transfer));
        } : () => {
            connection.DataSource = items;
            int queries = scenario == "lookup-dense" ? 1000 : 4;
            for (int i = 0; i < queries; i++)
            {
                int expected = (i * 7919) % items.Length;
                Assert.Equal(expected, connection.IndexOf(items[expected]));
            }
        };
        for (int i = 0; i < 6; i++) operation();
        var times = new double[128]; var bytes = new long[128];
        for (int i = 0; i < times.Length; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            operation(); times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        File.AppendAllText(Environment.GetEnvironmentVariable("REVIEW_OUTPUT")!, JsonSerializer.Serialize(new { scenario, times, bytes }) + Environment.NewLine);
    }

    private sealed class ReviewNode
    {
        public ReviewNode(string name) => Name = name;
        public string Name { get; }
        public List<ReviewNode> Children { get; } = new();
    }
    private sealed class ReviewGroupDescription : DataGridGroupDescription
    {
        public override object GroupKeyFromItem(object item, int level, CultureInfo culture) => ((string)item).Length;
    }
    private static void Pump(Window window)
    {
        // Process bindings/layout and exactly one rendered frame per operation.
        // Exclude the wall-clock UiThreadRender timer: otherwise slower processes
        // render more frames, so allocation comparisons do not measure equal work.
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
    }
}
