// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridHeaderContextMenuTests
{
    [AvaloniaFact]
    public void ColumnHeaderContextMenu_Uses_Grid_ContextMenu_When_Not_Overridden()
    {
        var gridMenu = new ContextMenu();
        var (grid, root, defaultColumn, _) = CreateGrid(gridMenu, null);

        try
        {
            var header = GetHeaderForColumn(grid, defaultColumn);

            Assert.Same(gridMenu, header.ContextMenu);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void ColumnHeaderContextMenu_Uses_Column_ContextMenu_When_Overridden()
    {
        var gridMenu = new ContextMenu();
        var columnMenu = new ContextMenu();
        var (grid, root, _, overrideColumn) = CreateGrid(gridMenu, columnMenu);

        try
        {
            var header = GetHeaderForColumn(grid, overrideColumn);

            Assert.Same(columnMenu, header.ContextMenu);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTextColumn defaultColumn, DataGridTextColumn overrideColumn) CreateGrid(
        ContextMenu gridMenu,
        ContextMenu? columnMenu)
    {
        var items = new ObservableCollection<Item>
        {
            new("Ada"),
            new("Grace")
        };

        var root = new Window
        {
            Width = 400,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            ColumnHeaderContextMenu = gridMenu
        };

        var defaultColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        };

        var overrideColumn = new DataGridTextColumn
        {
            Header = "Group",
            Binding = new Binding(nameof(Item.Name)),
            HeaderContextMenu = columnMenu
        };

        grid.ColumnsInternal.Add(defaultColumn);
        grid.ColumnsInternal.Add(overrideColumn);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, defaultColumn, overrideColumn);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private sealed class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
