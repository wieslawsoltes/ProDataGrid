// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowDetailsRecyclingTests
{
    [AvaloniaFact]
    public void Reset_Reuses_The_Details_Of_Items_Still_Present()
    {
        var items = new ResettableCollection(Enumerable.Range(0, 6).Select(i => new Item(i)));
        var (window, grid, builds) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Visible, height: 600);
        try
        {
            var before = ProbesByItem(grid);
            Assert.Equal(6, before.Count);
            var history = before.ToDictionary(p => p.Key, p => p.Value.Contexts.Count);
            int buildsBefore = builds.Count;
            int loading = 0;
            int unloading = 0;
            grid.LoadingRowDetails += (_, e) => loading += before.ContainsValue((DetailsProbe)e.DetailsElement) ? 1 : 0;
            grid.UnloadingRowDetails += (_, _) => unloading++;

            items.ReverseAndReset();
            Pump(window);

            Assert.Equal(items, DisplayedRows(grid).Select(r => r.DataContext));
            var after = ProbesByItem(grid);
            foreach (var item in items)
            {
                Assert.Same(before[item], after[item]);
                Assert.Same(item, after[item].DataContext);
                Assert.Equal(history[item], after[item].Contexts.Count);
            }
            Assert.Equal(buildsBefore, builds.Count);
            Assert.Equal(6, unloading);
            Assert.Equal(6, loading);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Keeps_The_State_Of_An_Expanded_Nested_Details_Grid()
    {
        var items = new ResettableCollection(Enumerable.Range(0, 300).Select(i => new Item(i)));
        var window = new Window { Width = 400, Height = 400 };
        window.SetThemeStyles();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = new DataGridCollectionView(items),
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed,
            RowDetailsTemplate = new FuncDataTemplate<Item>((_, _) =>
            {
                var nested = new DataGrid { AutoGenerateColumns = false, Height = 120 };
                nested.Columns.Add(new DataGridTextColumn { Header = "Child", Binding = new Binding(".") });
                nested.Bind(DataGrid.ItemsSourceProperty, new Binding(nameof(Item.Children)));
                return nested;
            }),
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Item.Name)) });
        grid.LoadingRow += ShowDetailsOfExpandedItems;
        window.Content = grid;
        window.Show();
        Pump(window);
        try
        {
            var expanded = items[150];
            grid.ScrollIntoView(expanded, grid.Columns[0]);
            Pump(window);
            expanded.IsExpanded = true;
            RowOf(grid, expanded).AreDetailsVisible = true;
            Pump(window);
            var nested = NestedGridOf(grid, expanded);
            nested.SelectedIndex = 2;
            Pump(window);
            var nestedRows = nested.DisplayData.GetScrollingRows().ToArray();
            Assert.NotEmpty(nestedRows);

            // The reset displays the first rows before the grid gets back to the expanded row.
            items.ReverseAndReset();
            grid.ScrollIntoView(expanded, grid.Columns[0]);
            Pump(window);

            Assert.Same(nested, NestedGridOf(grid, expanded));
            Assert.Same(expanded, nested.DataContext);
            Assert.Equal(2, nested.SelectedIndex);
            Assert.Equal(nestedRows, nested.DisplayData.GetScrollingRows().ToArray());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Current_Row_Scrolled_Out_And_Back_Keeps_Its_Details()
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i) { IsExpanded = i == 0 }));
        var (window, grid, builds) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Collapsed, height: 300, ShowDetailsOfExpandedItems);
        try
        {
            var current = items[0];
            Assert.Equal(0, grid.CurrentSlot);
            var probe = ProbeOf(RowOf(grid, current));
            int history = probe.Contexts.Count;
            int buildsBefore = builds.Count;

            for (int i = DisplayedRows(grid).Count; DisplayedRows(grid).Any(row => row.DataContext == current); i++)
            {
                grid.ScrollIntoView(items[i], grid.Columns[0]);
                Pump(window);
            }
            for (int i = DisplayData(grid).FirstScrollingSlot - 1; i >= 0; i--)
            {
                grid.ScrollIntoView(items[i], grid.Columns[0]);
                Pump(window);
            }

            Assert.Equal(0, grid.CurrentSlot);
            Assert.Same(probe, ProbeOf(RowOf(grid, current)));
            Assert.Same(current, probe.DataContext);
            Assert.Equal(history, probe.Contexts.Count);
            Assert.Equal(buildsBefore, builds.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Reused_For_Another_Item_Rebinds_Its_Details()
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i)));
        var (window, grid, _) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Visible, height: 300);
        try
        {
            var first = ProbesByItem(grid);

            grid.ScrollIntoView(items[60], grid.Columns[0]);
            Pump(window);
            grid.ScrollIntoView(items[30], grid.Columns[0]);
            Pump(window);

            var rows = DisplayedRows(grid);
            Assert.Contains(rows, row => first.ContainsValue(ProbeOf(row)));
            foreach (var row in rows)
            {
                var probe = ProbeOf(row);
                Assert.Same(row.DataContext, probe.DataContext);
                if (first.FirstOrDefault(p => p.Value == probe).Key is { } previous)
                {
                    Assert.Equal(new object?[] { previous, null, row.DataContext }, probe.Contexts.TakeLast(3));
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsed_Details_Of_A_Reused_Row_Are_Not_Bound_To_Its_Previous_Item()
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i) { IsExpanded = i < 30 }));
        var (window, grid, _) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Collapsed, height: 300, ShowDetailsOfExpandedItems);
        try
        {
            // Scrolling through the expanded rows leaves more rows with kept details than the
            // grid shows, so the jump to the collapsed rows reuses some of them.
            for (int i = DisplayedRows(grid).Count; i < 30; i++)
            {
                grid.ScrollIntoView(items[i], grid.Columns[0]);
                Pump(window);
            }
            grid.ScrollIntoView(items[70], grid.Columns[0]);
            Pump(window);

            var rows = DisplayedRows(grid);
            Assert.Contains(rows, row => row.GetVisualDescendants().OfType<DetailsProbe>().Any());
            foreach (var row in rows)
            {
                Assert.False(row.AreDetailsVisible);
                foreach (var probe in row.GetVisualDescendants().OfType<DetailsProbe>())
                {
                    Assert.True(probe.DataContext is null || ReferenceEquals(probe.DataContext, row.DataContext));
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_An_Item_Releases_Its_Kept_Details()
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i)));
        var (window, grid, _) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Visible, height: 300);
        try
        {
            var displayed = items[1];
            var scrolledOut = items[0];
            var probes = ProbesByItem(grid);

            grid.ScrollIntoView(items[60], grid.Columns[0]);
            Pump(window);
            Assert.Same(scrolledOut, probes[scrolledOut].DataContext);
            items.Remove(scrolledOut);
            Assert.Null(probes[scrolledOut].DataContext);

            grid.ScrollIntoView(displayed, grid.Columns[0]);
            Pump(window);
            Assert.Same(probes[displayed], ProbesByItem(grid)[displayed]);
            items.Remove(displayed);
            Pump(window);

            Assert.DoesNotContain(DisplayedRows(grid), row => ReferenceEquals(row.DataContext, displayed));
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<DetailsProbe>(),
                probe => ReferenceEquals(probe.DataContext, displayed) || ReferenceEquals(probe.DataContext, scrolledOut));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Releases_The_Kept_Details_Of_Items_It_Removed()
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 6).Select(i => new Item(i)));
        var view = new DataGridCollectionView(items);
        var (window, grid, _) = CreateGrid(view, DataGridRowDetailsVisibilityMode.Visible, height: 600);
        try
        {
            var probes = ProbesByItem(grid);

            view.Filter = item => ((Item)item).Value != 2;
            Pump(window);

            Assert.Null(probes[items[2]].DataContext);
            foreach (var item in items.Where(i => i.Value != 2))
            {
                Assert.Same(probes[item], ProbesByItem(grid)[item]);
                Assert.Same(item, probes[item].DataContext);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Replacing_The_Items_Source_Releases_Kept_Details(bool keepRecycledContainers)
    {
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i)));
        var (window, grid, _) = CreateGrid(items, DataGridRowDetailsVisibilityMode.Visible, height: 300);
        grid.KeepRecycledContainersInVisualTree = keepRecycledContainers;
        try
        {
            var probes = ProbesByItem(grid);
            grid.ScrollIntoView(items[60], grid.Columns[0]);
            Pump(window);
            Assert.All(probes, p => Assert.Same(p.Key, p.Value.DataContext));

            grid.ItemsSource = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(i => new Item(i)));
            Pump(window);

            Assert.All(probes, p => Assert.Null(p.Value.DataContext));
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid, List<object> Builds) CreateGrid(
        IEnumerable source,
        DataGridRowDetailsVisibilityMode mode,
        double height,
        System.EventHandler<DataGridRowEventArgs>? loadingRow = null)
    {
        var builds = new List<object>();
        var window = new Window { Width = 400, Height = height };
        window.SetThemeStyles();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = source,
            RowDetailsVisibilityMode = mode,
            RowDetailsTemplate = new FuncDataTemplate<Item>((item, _) =>
            {
                builds.Add(item);
                return new DetailsProbe();
            }),
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Item.Name)) });
        if (loadingRow != null)
        {
            grid.LoadingRow += loadingRow;
        }
        window.Content = grid;
        window.Show();
        Pump(window);
        return (window, grid, builds);
    }

    private static void ShowDetailsOfExpandedItems(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is Item item)
        {
            e.Row.AreDetailsVisible = item.IsExpanded;
        }
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGridDisplayData DisplayData(DataGrid grid) => grid.DisplayData;

    private static List<DataGridRow> DisplayedRows(DataGrid grid) =>
        grid.DisplayData.GetScrollingRows().Cast<DataGridRow>().ToList();

    private static DataGridRow RowOf(DataGrid grid, Item item) =>
        DisplayedRows(grid).Single(row => ReferenceEquals(row.DataContext, item));

    private static DetailsProbe ProbeOf(DataGridRow row) =>
        row.GetVisualDescendants().OfType<DetailsProbe>().Single();

    private static DataGrid NestedGridOf(DataGrid grid, Item item) =>
        RowOf(grid, item).GetVisualDescendants().OfType<DataGrid>().Single();

    private static Dictionary<Item, DetailsProbe> ProbesByItem(DataGrid grid) =>
        DisplayedRows(grid).ToDictionary(row => (Item)row.DataContext, ProbeOf);

    private sealed class DetailsProbe : Border
    {
        public DetailsProbe()
        {
            Height = 30;
            DataContextChanged += (_, _) => Contexts.Add(DataContext);
        }

        public List<object?> Contexts { get; } = new();
    }

    private sealed class Item
    {
        public Item(int value)
        {
            Value = value;
            Name = "Item " + value;
            Children = new ObservableCollection<string>(Enumerable.Range(0, 5).Select(i => Name + "." + i));
        }

        public int Value { get; }

        public string Name { get; }

        public bool IsExpanded { get; set; }

        public ObservableCollection<string> Children { get; }
    }

    private sealed class ResettableCollection : ObservableCollection<Item>
    {
        public ResettableCollection(IEnumerable<Item> items)
            : base(items)
        {
        }

        public void ReverseAndReset()
        {
            var reversed = Items.Reverse().ToList();
            Items.Clear();
            foreach (var item in reversed)
            {
                Items.Add(item);
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
