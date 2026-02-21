// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridContainerLifecycleTests
{
    [AvaloniaFact]
    public void NotifyRowPrepared_invokes_override_and_sets_state()
    {
        var grid = new TrackingDataGrid();
        var row = new DataGridRow();
        var item = "Item 0";

        grid.NotifyRowPrepared(row, item);

        Assert.Equal(item, row.DataContext);
        Assert.False(row.IsPlaceholder);
        Assert.Contains(item, grid.PreparedItems);
    }

    [AvaloniaFact]
    public void NotifyRowRecycling_invokes_clear_and_cleanup_hooks()
    {
        var grid = new TrackingDataGrid();
        var item = "Item 1";
        var row = new DataGridRow
        {
            DataContext = item,
            IsSelected = true,
            IsPlaceholder = true,
            Index = 5,
            Slot = 5
        };

        grid.NotifyRowRecycling(row);

        Assert.Contains(item, grid.ClearedItems);
        Assert.Contains(item, grid.CleanedItems);
        Assert.False(row.IsSelected);
        Assert.False(row.IsPlaceholder);
        Assert.Null(row.DataContext);
    }

    [AvaloniaFact]
    public void Prepare_sets_placeholder_flag_for_new_item_placeholder()
    {
        var grid = new TrackingDataGrid();
        var row = new DataGridRow();
        var placeholder = GetPlaceholder();

        grid.NotifyRowPrepared(row, placeholder);

        Assert.True(row.IsPlaceholder);
        Assert.Same(placeholder, row.DataContext);
    }

    [AvaloniaFact]
    public void IsItemItsOwnContainerOverride_returns_true_for_rows()
    {
        var grid = new TrackingDataGrid();

        Assert.True(grid.IsItemItsOwnContainer(new DataGridRow()));
        Assert.False(grid.IsItemItsOwnContainer(new object()));
    }

    [AvaloniaFact]
    public void Own_container_is_prepared_and_loaded()
    {
        var grid = new TrackingDataGrid();
        var row = new DataGridRow();

        var generated = grid.InvokeGenerateRow(rowIndex: 0, slot: 0, dataContext: row);

        Assert.Same(row, generated);
        Assert.Equal(grid, row.OwningGrid);
        Assert.Equal(0, row.Index);
        Assert.Equal(0, row.Slot);
        Assert.Equal(row, row.DataContext);
        Assert.Contains(row, grid.LoadedRows);
        Assert.Contains(row, grid.PreparedItems);
    }

    [AvaloniaFact]
    public void Removing_item_clears_recycled_row_indices()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 3).Select(i => $"Item {i}"));
        var grid = CreateGrid(items, out var window);
        PumpLayout(grid);

        var row = grid.GetSelfAndVisualDescendants().OfType<DataGridRow>().First();

        items.RemoveAt(0);
        PumpLayout(grid);

        Assert.Null(row.DataContext);
        Assert.False(row.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void Cleanup_sees_item_before_clear()
    {
        var grid = new TrackingDataGrid();
        var row = new DataGridRow();
        var item = "Item 2";
        row.DataContext = item;

        grid.NotifyRowRecycling(row);

        Assert.Contains(item, grid.CleanedItems);
        Assert.DoesNotContain(item, grid.CleanedAfterClear);
        Assert.Null(row.DataContext);
    }

    [AvaloniaFact]
    public void UnloadElements_recycle_invokes_cleanup_and_clear()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 2).Select(i => $"Item {i}"));
        var grid = CreateGrid(items, out var window);
        PumpLayout(grid);

        var unload = typeof(DataGrid).GetMethod("UnloadElements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException("UnloadElements not found");
        unload.Invoke(grid, new object[] { true });

        Assert.Contains("Item 0", grid.ClearedItems);
        Assert.Contains("Item 1", grid.ClearedItems);
        Assert.Contains("Item 0", grid.CleanedItems);
        Assert.Contains("Item 1", grid.CleanedItems);

        window.Close();
    }

    [AvaloniaFact]
    public void UnloadElements_non_recycle_invokes_clear()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 2).Select(i => $"Item {i}"));
        var grid = CreateGrid(items, out var window);
        PumpLayout(grid);

        var unload = typeof(DataGrid).GetMethod("UnloadElements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException("UnloadElements not found");
        unload.Invoke(grid, new object[] { false });

        Assert.Contains("Item 0", grid.ClearedItems);
        Assert.Contains("Item 1", grid.ClearedItems);

        window.Close();
    }

    [AvaloniaFact]
    public void Detaching_from_visual_tree_clears_row_containers()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 2).Select(i => $"Item {i}"));
        var grid = CreateGrid(items, out var window);
        PumpLayout(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Item 0", grid.ClearedItems);
        Assert.Contains("Item 1", grid.ClearedItems);

        window.Close();
    }

    [AvaloniaFact]
    public void Detaching_from_visual_tree_clears_row_details_subscription()
    {
        var items = new ObservableCollection<string> { "Item 0" };
        var window = new Window
        {
            Width = 240,
            Height = 120,
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible,
            RowDetailsTemplate = new FuncDataTemplate<string>((_, _) => new Border())
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(".") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        var row = grid.GetSelfAndVisualDescendants().OfType<DataGridRow>().First();
        var subscriptionField = typeof(DataGridRow).GetField(
            "_detailsContentSizeSubscription",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(subscriptionField);
        Assert.NotNull(subscriptionField.GetValue(row));

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(subscriptionField.GetValue(row));

        window.Close();
    }

    [AvaloniaFact]
    public void Recycled_row_context_change_skips_template_refresh_without_visual_root()
    {
        var grid = new DataGrid();
        var column = new TrackingTemplateColumn
        {
            CellTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock())
        };
        grid.ColumnsInternal.Add(column);
        column.Index = 0;

        var row = new DataGridRow
        {
            OwningGrid = grid,
            Index = 0,
            Slot = 0
        };

        var cell = new DataGridCell
        {
            OwningColumn = column,
            Content = new TextBlock { Text = "initial" }
        };
        row.Cells.Insert(0, cell);

        row.DetachFromDataGrid(recycle: true);
        Assert.True(row.IsRecycled);

        row.DataContext = new object();

        Assert.False(column.RefreshCalled);
    }

    [AvaloniaFact]
    public void Placeholder_recycle_regenerates_cells_for_real_item()
    {
        var grid = new TrackingDataGrid();
        var column = new TrackingTextColumn();
        grid.ColumnsInternal.Add(column);
        var placeholder = GetPlaceholder();

        var placeholderRow = grid.InvokeGenerateRow(rowIndex: 0, slot: 0, dataContext: placeholder);
        Assert.Contains(placeholder, column.GeneratedItems);

        grid.DisplayData.RecycleRow(placeholderRow);

        var newItem = "Real item";
        var generatedRow = grid.InvokeGenerateRow(rowIndex: 1, slot: 1, dataContext: newItem);
        var cellText = (generatedRow.Cells[0].Content as TextBlock)?.Text;

        Assert.Equal($"item:{newItem}", cellText);
        Assert.Contains(newItem, column.GeneratedItems);
        Assert.Null(generatedRow.RecycledDataContext);
        Assert.False(generatedRow.RecycledIsPlaceholder);
    }

    [AvaloniaFact]
    public void Recycled_real_row_to_placeholder_with_compiled_binding_does_not_throw()
    {
        var grid = new TrackingDataGrid();
        var placeholder = GetPlaceholder();
        var column = new DataGridTextColumn
        {
            Binding = DataGridBindingDefinition.Create<CompiledBindingItem, string>(item => item.Name).CreateBinding()
        };
        grid.ColumnsInternal.Add(column);

        var item = new CompiledBindingItem { Name = "Row 0" };
        var generatedRow = grid.InvokeGenerateRow(rowIndex: 0, slot: 0, dataContext: item);
        grid.DisplayData.RecycleRow(generatedRow);

        var exception = Record.Exception(() => grid.InvokeGenerateRow(rowIndex: 1, slot: 1, dataContext: placeholder));

        Assert.Null(exception);
    }

    private static TrackingDataGrid CreateGrid(IList<string> items, out Window window)
    {
        window = new Window
        {
            Width = 240,
            Height = 120,
        };

        window.SetThemeStyles();

        var grid = new TrackingDataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(".") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        return grid;
    }

    private static void PumpLayout(DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static object GetPlaceholder()
    {
        var type = typeof(DataGrid).Assembly.GetType("Avalonia.Collections.DataGridCollectionView")
                   ?? throw new InvalidOperationException("DataGridCollectionView not found");
        var property = type.GetProperty("NewItemPlaceholder", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                       ?? throw new InvalidOperationException("NewItemPlaceholder not found");
        return property.GetValue(null)!;
    }

    private sealed class TrackingDataGrid : DataGrid
    {
        public List<object> PreparedItems { get; } = new();
        public List<object> ClearedItems { get; } = new();
        public List<object> CleanedItems { get; } = new();
        public List<object> CleanedAfterClear { get; } = new();
        public List<DataGridRow> LoadedRows { get; } = new();
        protected override Type StyleKeyOverride => typeof(DataGrid);

        protected override void PrepareContainerForItemOverride(DataGridRow element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            PreparedItems.Add(item);
        }

        protected override void ClearContainerForItemOverride(DataGridRow element, object item)
        {
            ClearedItems.Add(item);
            base.ClearContainerForItemOverride(element, item);
        }

        protected override void OnCleanUpVirtualizedItem(DataGridRow element)
        {
            if (element.DataContext is { } item)
            {
                CleanedItems.Add(item);
            }
            else
            {
                CleanedAfterClear.Add("cleared");
            }
            base.OnCleanUpVirtualizedItem(element);
        }

        public bool IsItemItsOwnContainer(object item) => IsItemItsOwnContainerOverride(item);

        public DataGridRow InvokeGenerateRow(int rowIndex, int slot, object dataContext) =>
            typeof(DataGrid)
                .GetMethod("GenerateRow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(int), typeof(int), typeof(object) }, null)!
                .Invoke(this, new object[] { rowIndex, slot, dataContext }) as DataGridRow
            ?? throw new System.InvalidOperationException("GenerateRow returned null");

        protected override void OnLoadingRow(DataGridRowEventArgs e)
        {
            LoadedRows.Add(e.Row);
            base.OnLoadingRow(e);
        }
    }

    private sealed class TrackingTextColumn : DataGridTextColumn
    {
        public List<object> GeneratedItems { get; } = new();

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            GeneratedItems.Add(dataItem);
            return new TextBlock { Text = $"item:{dataItem}" };
        }
    }

    private sealed class TrackingTemplateColumn : DataGridTemplateColumn
    {
        public bool RefreshCalled { get; private set; }

        protected internal override void RefreshCellContent(Control element, string propertyName)
        {
            RefreshCalled = true;
            base.RefreshCellContent(element, propertyName);
        }
    }

    private sealed class CompiledBindingItem
    {
        public string Name { get; init; } = string.Empty;
    }
}
