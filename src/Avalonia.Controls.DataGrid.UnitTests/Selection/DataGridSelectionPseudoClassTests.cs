// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionPseudoClassTests
{
    [AvaloniaFact]
    public void Clearing_Row_Selection_Updates_Cell_PseudoClasses()
    {
        var (grid, root, items) = CreateGrid(DataGridSelectionUnit.FullRow);
        try
        {
            grid.SelectedItem = items[0];
            grid.UpdateLayout();

            var cell = GetCell(grid, items[0], 0);
            Assert.True(HasPseudoClass(cell, ":selected"));
            Assert.True(HasPseudoClass(cell, ":row-selected"));
            Assert.False(HasPseudoClass(cell, ":cell-selected"));

            grid.SelectedItems.Clear();
            grid.UpdateLayout();

            Assert.False(HasPseudoClass(cell, ":selected"));
            Assert.False(HasPseudoClass(cell, ":row-selected"));
            Assert.False(HasPseudoClass(cell, ":cell-selected"));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_Cell_Selection_Updates_Cell_PseudoClasses()
    {
        var (grid, root, items) = CreateGrid(DataGridSelectionUnit.Cell);
        try
        {
            var bound = new ObservableCollection<DataGridCellInfo>();
            grid.SelectedCells = bound;
            grid.UpdateLayout();

            var column = grid.ColumnsInternal[0];
            bound.Add(new DataGridCellInfo(items[0], column, 0, column.Index, isValid: true));
            grid.UpdateLayout();

            var cell = GetCell(grid, items[0], 0);
            Assert.True(HasPseudoClass(cell, ":selected"));
            Assert.True(HasPseudoClass(cell, ":row-selected"));
            Assert.True(HasPseudoClass(cell, ":cell-selected"));

            bound.Clear();
            grid.UpdateLayout();

            Assert.False(HasPseudoClass(cell, ":selected"));
            Assert.False(HasPseudoClass(cell, ":row-selected"));
            Assert.False(HasPseudoClass(cell, ":cell-selected"));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_To_FullRow_Clears_Cell_Selected_PseudoClass()
    {
        var (grid, root, items) = CreateGrid(DataGridSelectionUnit.Cell);
        try
        {
            var bound = new ObservableCollection<DataGridCellInfo>();
            grid.SelectedCells = bound;
            grid.UpdateLayout();

            var column = grid.ColumnsInternal[0];
            bound.Add(new DataGridCellInfo(items[0], column, 0, column.Index, isValid: true));
            grid.UpdateLayout();

            var cell = GetCell(grid, items[0], 0);
            Assert.True(HasPseudoClass(cell, ":cell-selected"));

            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.UpdateLayout();

            Assert.False(HasPseudoClass(cell, ":cell-selected"));
            Assert.True(HasPseudoClass(cell, ":selected"));
            Assert.True(HasPseudoClass(cell, ":row-selected"));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CellOrRowHeader_Row_Selection_Does_Not_Mark_Cell_As_Selected()
    {
        var (grid, root, items) = CreateGrid(DataGridSelectionUnit.CellOrRowHeader);
        try
        {
            grid.SelectedItem = items[0];
            grid.UpdateLayout();

            var cell = GetCell(grid, items[0], 0);
            Assert.False(HasPseudoClass(cell, ":selected"));
            Assert.True(HasPseudoClass(cell, ":row-selected"));
            Assert.False(HasPseudoClass(cell, ":cell-selected"));

            var bound = new ObservableCollection<DataGridCellInfo>();
            grid.SelectedCells = bound;
            grid.UpdateLayout();

            var column = grid.ColumnsInternal[0];
            bound.Add(new DataGridCellInfo(items[0], column, 0, column.Index, isValid: true));
            grid.UpdateLayout();

            Assert.True(HasPseudoClass(cell, ":selected"));
            Assert.True(HasPseudoClass(cell, ":row-selected"));
            Assert.True(HasPseudoClass(cell, ":cell-selected"));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, ObservableCollection<Item> items) CreateGrid(DataGridSelectionUnit selectionUnit)
    {
        var items = new ObservableCollection<Item>
        {
            new("A", "1"),
            new("B", "2"),
        };

        var root = new Window
        {
            Width = 320,
            Height = 240,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionUnit = selectionUnit,
            SelectionMode = DataGridSelectionMode.Extended,
            CanUserAddRows = false,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(Item.Value))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return (grid, root, items);
    }

    private static DataGridCell GetCell(DataGrid grid, Item item, int columnIndex)
    {
        var row = grid.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => ReferenceEquals(r.DataContext, item));

        return row.Cells[columnIndex];
    }

    private static bool HasPseudoClass(Control control, string pseudoClass)
    {
        return ((IPseudoClasses)control.Classes).Contains(pseudoClass);
    }

    private sealed class Item
    {
        public Item(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }
}
