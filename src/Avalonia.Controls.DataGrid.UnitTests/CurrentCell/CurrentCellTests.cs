// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.CurrentCell;

public class CurrentCellTests
{
    [AvaloniaFact]
    public void CurrentCell_Property_Changes_When_Selection_Moves()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
            new() { Name = "C" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        var changes = new List<DataGridCellInfo>();
        grid.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataGrid.CurrentCellProperty)
            {
                changes.Add(e.GetNewValue<DataGridCellInfo>());
            }
        };

        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.CurrentCell.Item);
        Assert.Contains(changes, c => Equals(c.Item, items[1]) && c.RowIndex == 1);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_Moves_Currency_And_Raises_Event()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
            new() { Name = "C" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var targetColumn = grid.Columns.First();
        var eventHits = 0;
        grid.CurrentCellChanged += (_, e) =>
        {
            eventHits++;
            Assert.Same(targetColumn, e.NewColumn);
            Assert.Equal(items[1], e.NewItem);
        };

        grid.CurrentCell = new DataGridCellInfo(items[1], targetColumn, 1, targetColumn.Index, isValid: true);
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(targetColumn, grid.CurrentColumn);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.SelectedItem);
        Assert.True(eventHits >= 1);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_To_Unset_Clears_Currency_But_Not_Selection()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);

        grid.CurrentCell = DataGridCellInfo.Unset;
        grid.UpdateLayout();

        Assert.False(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.SelectedIndex);
        Assert.Equal(items[1], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Foreign_Column_Throws()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        var foreignColumn = new DataGridTextColumn();

        Assert.Throws<ArgumentException>(() =>
            grid.CurrentCell = new DataGridCellInfo(items[0], foreignColumn, 0, 0, isValid: true));
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Out_Of_Range_Row_Does_Not_Change_Current()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[0], column, 0, columnIndex: 0, isValid: true);
        var baseline = grid.CurrentCell;

        grid.CurrentCell = new DataGridCellInfo(items[0], column, rowIndex: 5, columnIndex: 0, isValid: true);

        Assert.Equal(baseline, grid.CurrentCell);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Item_Recalculates_RowIndex()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[1], column, rowIndex: 0, columnIndex: 0, isValid: true);

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.CurrentCell.Item);
    }

    [AvaloniaFact]
    public void Clearing_ItemsSource_Resets_CurrentCell()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[0], column, 0, 0, isValid: true);
        Assert.True(grid.CurrentCell.IsValid);

        grid.ItemsSource = null;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.False(grid.CurrentCell.IsValid);
    }

    private static DataGrid CreateGrid(IEnumerable<Item> items)
    {
        var root = new Window
        {
            Width = 320,
            Height = 240,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = true,
            CanUserAddRows = false,
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return grid;
    }

    private class Item
    {
        public string Name { get; set; } = string.Empty;
    }
}
