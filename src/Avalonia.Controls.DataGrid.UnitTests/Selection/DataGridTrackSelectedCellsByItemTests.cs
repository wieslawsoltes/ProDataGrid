// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridTrackSelectedCellsByItemTests
{
    [AvaloniaFact]
    public void Inserting_Rows_Above_Moves_Tracked_Cells_Without_Reporting_Them()
    {
        var items = CreateItems(5);
        var grid = CreateGrid(items);
        var cells = SelectCells(grid, items[1], items[3]);
        var events = new List<DataGridSelectedCellsChangedEventArgs>();
        grid.SelectedCellsChanged += (_, e) => events.Add(e);
        int collectionChanges = 0;
        cells.CollectionChanged += (_, _) => collectionChanges++;

        items.Insert(0, new Item("new 0"));
        items.Insert(0, new Item("new 1"));
        grid.UpdateLayout();

        Assert.Empty(events);
        Assert.Equal(0, collectionChanges);
        Assert.Equal(new[] { 3, 5 }, grid.SelectedCells.Select(cell => cell.RowIndex));
        Assert.True(grid.IsCellSelected(3, 0));
        Assert.True(grid.IsCellSelected(5, 0));
        Assert.False(grid.IsCellSelected(1, 0));
    }

    [AvaloniaFact]
    public void Removing_A_Row_Reports_Only_The_Cells_Of_That_Row()
    {
        var items = CreateItems(5);
        var grid = CreateGrid(items);
        Item removed = items[1];
        Item kept = items[3];
        var cells = SelectCells(grid, removed, kept);
        var events = new List<DataGridSelectedCellsChangedEventArgs>();
        grid.SelectedCellsChanged += (_, e) => events.Add(e);

        items.RemoveAt(0);
        grid.UpdateLayout();
        Assert.Empty(events);

        items.Remove(removed);
        grid.UpdateLayout();

        var change = Assert.Single(events);
        Assert.Empty(change.AddedCells);
        Assert.Same(removed, Assert.Single(change.RemovedCells).Item);
        DataGridCellInfo remaining = Assert.Single(grid.SelectedCells);
        Assert.Same(kept, remaining.Item);
        Assert.Equal(1, remaining.RowIndex);
        Assert.Same(kept, Assert.Single(cells).Item);
    }

    [AvaloniaFact]
    public void Sorting_Moves_Tracked_Cells_To_The_Current_Row_Of_Their_Item()
    {
        var items = new ObservableCollection<Item> { new("c"), new("a"), new("b") };
        var view = new DataGridCollectionView(items);
        var grid = CreateGrid(view);
        Item selected = items[0];
        SelectCells(grid, selected);
        var events = new List<DataGridSelectedCellsChangedEventArgs>();
        grid.SelectedCellsChanged += (_, e) => events.Add(e);

        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Item.Name), ListSortDirection.Ascending));
        grid.UpdateLayout();

        Assert.Empty(events);
        DataGridCellInfo cell = Assert.Single(grid.SelectedCells);
        Assert.Same(selected, cell.Item);
        Assert.Equal(2, cell.RowIndex);
        Assert.True(grid.IsCellSelected(2, 0));
    }

    [AvaloniaFact]
    public void Tracked_Cells_Move_To_Regenerated_Columns()
    {
        var items = new ObservableCollection<Item> { new("c"), new("a"), new("b") };
        var view = new DataGridCollectionView(items);
        var grid = CreateGrid(view, autoGenerateColumns: true);
        Item selected = items[0];
        SelectCells(grid, selected);

        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Item.Name), ListSortDirection.Ascending));
        grid.UpdateLayout();

        DataGridCellInfo cell = Assert.Single(grid.SelectedCells);
        Assert.Same(grid.Columns.First(), cell.Column);
        Assert.Same(selected, cell.Item);
        Assert.Equal(2, cell.RowIndex);
    }

    [AvaloniaFact]
    public void Turning_Tracking_Off_Reports_Moved_Cells_Again()
    {
        var items = CreateItems(3);
        var grid = CreateGrid(items);
        SelectCells(grid, items[1]);
        grid.TrackSelectedCellsByItem = false;
        var events = new List<DataGridSelectedCellsChangedEventArgs>();
        grid.SelectedCellsChanged += (_, e) => events.Add(e);

        items.Insert(0, new Item("new"));
        grid.UpdateLayout();

        var change = Assert.Single(events);
        Assert.Equal(1, Assert.Single(change.RemovedCells).RowIndex);
        Assert.Equal(2, Assert.Single(change.AddedCells).RowIndex);
        Assert.Equal(2, Assert.Single(grid.SelectedCells).RowIndex);
    }

    private static ObservableCollection<DataGridCellInfo> SelectCells(DataGrid grid, params Item[] rows)
    {
        DataGridColumn column = grid.Columns.First();
        var cells = new ObservableCollection<DataGridCellInfo>(
            rows.Select(row => new DataGridCellInfo(row, column, grid.DataConnection.IndexOf(row), column.Index, isValid: true, tracksItem: true)));
        grid.SelectedCells = cells;
        grid.UpdateLayout();
        return cells;
    }

    private static ObservableCollection<Item> CreateItems(int count) =>
        new(Enumerable.Range(0, count).Select(i => new Item($"item {i}")));

    private static DataGrid CreateGrid(IEnumerable items, bool autoGenerateColumns = false)
    {
        var root = new Window
        {
            Width = 320,
            Height = 240,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = autoGenerateColumns,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            TrackSelectedCellsByItem = true,
            CanUserAddRows = false,
        };

        if (!autoGenerateColumns)
        {
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Item.Name)) });
        }

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return grid;
    }

    private class Item
    {
        public Item(string name) => Name = name;

        public string Name { get; set; }
    }
}
