// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridDuplicateItemCellSelectionTests
{
    [AvaloniaFact]
    public void Default_Cell_Selection_Distinguishes_Duplicate_Item_Occurrences()
    {
        object repeated = new object();
        var (window, grid, column) = CreateGrid(new ObservableCollection<object> { repeated, repeated, new object() });
        try
        {
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(repeated, column, 1, 0) };

            Assert.False(grid.TrackSelectedCellsByItem);
            Assert.True(grid.IsCellSelected(1, 0));
            Assert.False(grid.IsCellSelected(0, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tracked_Cell_Selection_Distinguishes_Duplicate_Item_Occurrences(bool tracksItem)
    {
        object repeated = new object();
        var (window, grid, column) = CreateGrid(new ObservableCollection<object> { repeated, repeated, new object() }, trackSelectedCellsByItem: true);
        try
        {
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(repeated, column, 1, 0, isValid: true, tracksItem) };

            Assert.True(grid.IsCellSelected(1, 0));
            Assert.False(grid.IsCellSelected(0, 0));
            Assert.Equal(1, Assert.Single(grid.SelectedCells).RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_Tracking_On_Keeps_Duplicate_Item_Occurrences_Apart()
    {
        object repeated = new object();
        var (window, grid, column) = CreateGrid(new ObservableCollection<object> { repeated, repeated, new object() });
        try
        {
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(repeated, column, 1, 0) };

            grid.TrackSelectedCellsByItem = true;

            Assert.True(grid.IsCellSelected(1, 0));
            Assert.False(grid.IsCellSelected(0, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Insert_Selected_Item_Above_Itself")]
    [InlineData("Insert_Selected_Item_Below_Itself")]
    [InlineData("Replace_Row_With_Selected_Item")]
    [InlineData("Insert_Above_Second_Occurrence")]
    [InlineData("Remove_First_Occurrence")]
    [InlineData("Move_Second_Occurrence_To_Top")]
    public void Tracked_Cells_Of_Duplicate_Items_Behave_As_With_Tracking_Off(string scenario)
    {
        var expected = RunScenario(scenario, trackSelectedCellsByItem: false);
        var actual = RunScenario(scenario, trackSelectedCellsByItem: true);

        Assert.Equal(expected.Selected, actual.Selected);
        Assert.Equal(expected.Events, actual.Events);
        Assert.Single(actual.Selected);
    }

    [AvaloniaFact]
    public void Clearing_The_Cell_Selection_Resumes_Tracking_By_Item()
    {
        var items = new ObservableCollection<object> { new object(), new object(), new object() };
        var (window, grid, column) = CreateGrid(items, trackSelectedCellsByItem: true);
        try
        {
            object selected = items[1];
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(selected, column, 1, 0) };
            items.Insert(0, selected);
            grid.UpdateLayout();
            Assert.Single(new[] { 0, 2 }, row => grid.IsCellSelected(row, 0));

            grid.SelectedCells.Clear();
            object unique = items[3];
            grid.SelectedCells.Add(new DataGridCellInfo(unique, column, 3, 0, isValid: true, tracksItem: true));
            var events = new List<DataGridSelectedCellsChangedEventArgs>();
            grid.SelectedCellsChanged += (_, e) => events.Add(e);

            items.Insert(0, new object());
            grid.UpdateLayout();

            Assert.Empty(events);
            DataGridCellInfo cell = Assert.Single(grid.SelectedCells);
            Assert.Same(unique, cell.Item);
            Assert.Equal(4, cell.RowIndex);
            Assert.True(grid.IsCellSelected(4, 0));
        }
        finally
        {
            window.Close();
        }
    }

    private static (List<(int Row, int Column)> Selected, List<string> Events) RunScenario(string scenario, bool trackSelectedCellsByItem)
    {
        object a = new object();
        object b = new object();
        object c = new object();
        var items = new ObservableCollection<object> { a, b, c };
        if (scenario is "Insert_Above_Second_Occurrence" or "Remove_First_Occurrence" or "Move_Second_Occurrence_To_Top")
        {
            items.Insert(2, b);
        }

        var (window, grid, column) = CreateGrid(items, trackSelectedCellsByItem);
        try
        {
            int selectedRow = items.Count == 4 ? 2 : 1;
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(b, column, selectedRow, 0) };
            var events = new List<string>();
            grid.SelectedCellsChanged += (_, e) => events.Add(
                $"+{string.Join(",", e.AddedCells.Select(cell => cell.RowIndex))} -{string.Join(",", e.RemovedCells.Select(cell => cell.RowIndex))}");

            switch (scenario)
            {
                case "Insert_Selected_Item_Above_Itself":
                    items.Insert(0, b);
                    break;
                case "Insert_Selected_Item_Below_Itself":
                    items.Add(b);
                    break;
                case "Replace_Row_With_Selected_Item":
                    items[0] = b;
                    break;
                case "Insert_Above_Second_Occurrence":
                    items.Insert(0, new object());
                    break;
                case "Remove_First_Occurrence":
                    items.RemoveAt(1);
                    break;
                case "Move_Second_Occurrence_To_Top":
                    items.Move(2, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario));
            }

            grid.UpdateLayout();
            var selected = new List<(int Row, int Column)>();
            for (int row = 0; row < items.Count; row++)
            {
                if (grid.IsCellSelected(row, 0))
                {
                    selected.Add((row, 0));
                }
            }

            Assert.Equal(selected, grid.SelectedCells.Select(cell => (cell.RowIndex, cell.ColumnIndex)).ToList());
            return (selected, events);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid, DataGridColumn Column) CreateGrid(
        ObservableCollection<object> items,
        bool trackSelectedCellsByItem = false)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            TrackSelectedCellsByItem = trackSelectedCellsByItem,
            ItemsSource = items,
        };
        var column = new DataGridTextColumn();
        grid.Columns.Add(column);
        var window = new Window { Width = 400, Height = 300, Content = grid };
        window.SetThemeStyles();
        window.Show();
        grid.UpdateLayout();
        return (window, grid, column);
    }
}
