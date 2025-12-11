// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridCellStylingTests
{
    [AvaloniaFact]
    public void CellStyleClasses_Applied()
    {
        var (grid, root, column, _) = CreateGridWithCellClasses(layoutNow: false);
        grid.UpdateLayout();

        try
        {
            var cell = GetCellForColumn(grid, column);
            Assert.Contains("numeric", cell.Classes);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CellTheme_Precedence_Column_Overrides_Grid()
    {
        var gridCellTheme = new ControlTheme(typeof(DataGridCell));
        var columnCellTheme = new ControlTheme(typeof(DataGridCell));

        var (grid, root, column, items) = CreateGridWithCellClasses(layoutNow: false);
        grid.CellTheme = gridCellTheme;
        column.CellTheme = columnCellTheme;
        grid.ItemsSource = null;
        grid.ItemsSource = items;
        grid.UpdateLayout();

        try
        {
            var cell = GetCellForColumn(grid, column);
            Assert.Same(columnCellTheme, cell.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTextColumn column, ObservableCollection<Item> items) CreateGridWithCellClasses(bool layoutNow = true)
    {
        var items = new ObservableCollection<Item>
        {
            new("1", "G1"),
            new("2", "G2")
        };

        var root = new Window
        {
            Width = 300,
            Height = 200,
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
            ItemsSource = items
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(Item.Name))
        };
        column.CellStyleClasses.Add("numeric");

        grid.Columns.Add(column);
        root.Content = grid;
        root.Show();
        if (layoutNow)
        {
            grid.UpdateLayout();
        }
        return (grid, root, column, items);
    }

    private static DataGridCell GetCellForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => ReferenceEquals(c.OwningColumn, column));
    }

    private class Item
    {
        public Item(string name, string group = "")
        {
            Name = name;
            Group = group;
        }

        public string Name { get; }

        public string Group { get; }
    }
}

