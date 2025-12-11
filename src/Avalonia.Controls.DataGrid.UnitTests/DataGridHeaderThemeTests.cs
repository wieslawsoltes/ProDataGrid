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

public class DataGridHeaderThemeTests
{
    [AvaloniaFact]
    public void Column_HeaderTheme_Overrides_Grid_Theme()
    {
        var gridTheme = new ControlTheme(typeof(DataGridColumnHeader));
        var columnTheme = new ControlTheme(typeof(DataGridColumnHeader));

        var (grid, root) = CreateGrid(gridTheme, columnTheme);

        try
        {
            var firstHeader = GetHeaderForColumn(grid, grid.Columns[0]);
            var secondHeader = GetHeaderForColumn(grid, grid.Columns[1]);

            Assert.Same(columnTheme, firstHeader.GetValue(StyledElement.ThemeProperty));
            Assert.Same(gridTheme, secondHeader.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Column_HeaderTheme_Runtime_Update_Is_Applied()
    {
        var gridTheme = new ControlTheme(typeof(DataGridColumnHeader));
        var columnTheme = new ControlTheme(typeof(DataGridColumnHeader));

        var (grid, root) = CreateGrid(gridTheme, null);

        try
        {
            var firstHeader = GetHeaderForColumn(grid, grid.Columns[0]);
            Assert.Same(gridTheme, firstHeader.GetValue(StyledElement.ThemeProperty));

            grid.Columns[0].HeaderTheme = columnTheme;
            grid.UpdateLayout();

            firstHeader = GetHeaderForColumn(grid, grid.Columns[0]);
            Assert.Same(columnTheme, firstHeader.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void OwningColumn_Is_Public_And_Assigned()
    {
        var gridTheme = new ControlTheme(typeof(DataGridColumnHeader));

        var (grid, root) = CreateGrid(gridTheme, null);

        try
        {
            var header = GetHeaderForColumn(grid, grid.Columns[0]);

            Assert.NotNull(header.OwningColumn);
            Assert.Same(grid.Columns[0], header.OwningColumn);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGrid(ControlTheme gridHeaderTheme, ControlTheme? columnHeaderTheme)
    {
        var items = new ObservableCollection<Item>
        {
            new("A", "B"),
            new("C", "D")
        };

        var root = new Window
        {
            Width = 400,
            Height = 300,
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
            ColumnHeaderTheme = gridHeaderTheme
        };

        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        };
        if (columnHeaderTheme != null)
        {
            nameColumn.HeaderTheme = columnHeaderTheme;
        }
        grid.Columns.Add(nameColumn);

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Group",
            Binding = new Binding(nameof(Item.Group))
        });

        root.Content = grid;
        root.Show();

        grid.UpdateLayout();
        return (grid, root);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private class Item
    {
        public Item(string name, string group)
        {
            Name = name;
            Group = group;
        }

        public string Name { get; }

        public string Group { get; }
    }
}
