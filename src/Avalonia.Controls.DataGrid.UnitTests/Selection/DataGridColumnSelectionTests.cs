// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridColumnSelectionTests
{
    [AvaloniaFact]
    public void SelectedColumns_Binding_Selects_Column_And_Raises_Event()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
            new() { Name = "C" }
        };

        var (grid, root) = CreateGrid(items);
        try
        {
            grid.SelectionUnit = DataGridSelectionUnit.CellOrColumnHeader;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.CanUserSelectColumns = true;
            grid.UpdateLayout();

            var events = new List<DataGridSelectedColumnsChangedEventArgs>();
            grid.SelectedColumnsChanged += (_, e) => events.Add(e);

            var firstColumn = grid.ColumnsInternal[0];
            grid.SelectedColumns = new ObservableCollection<DataGridColumn> { firstColumn };
            grid.UpdateLayout();

            Assert.Single(grid.SelectedColumns);
            Assert.Contains(firstColumn, grid.SelectedColumns);
            Assert.Equal(items.Count, grid.SelectedCells.Count);

            Assert.Single(events);
            Assert.Single(events[0].AddedColumns);
            Assert.Empty(events[0].RemovedColumns);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_To_FullRow_Clears_SelectedColumns()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" }
        };

        var (grid, root) = CreateGrid(items);
        try
        {
            grid.SelectionUnit = DataGridSelectionUnit.CellOrColumnHeader;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.CanUserSelectColumns = true;
            grid.UpdateLayout();

            var firstColumn = grid.ColumnsInternal[0];
            grid.SelectedColumns = new ObservableCollection<DataGridColumn> { firstColumn };
            grid.UpdateLayout();
            Assert.NotEmpty(grid.SelectedColumns);

            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.UpdateLayout();

            Assert.Empty(grid.SelectedColumns);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGrid(IEnumerable<Item> items)
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
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Extended,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All
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
        return (grid, root);
    }

    private sealed class Item
    {
        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}
