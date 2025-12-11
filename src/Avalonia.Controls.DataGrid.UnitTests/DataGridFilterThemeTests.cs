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

public class DataGridFilterThemeTests
{
    [AvaloniaFact]
    public void Column_Uses_Grid_FilterTheme_When_Not_Overridden()
    {
        var gridTheme = new ControlTheme(typeof(Button));
        var columnTheme = new ControlTheme(typeof(Button));

        var (grid, root, defaultColumn, overrideColumn) = CreateGrid(gridTheme, columnTheme);

        try
        {
            var defaultHeader = GetHeaderForColumn(grid, defaultColumn);
            var overrideHeader = GetHeaderForColumn(grid, overrideColumn);

            var defaultFilter = GetFilterButton(defaultHeader);
            var overrideFilter = GetFilterButton(overrideHeader);

            Assert.Same(gridTheme, defaultFilter.GetValue(StyledElement.ThemeProperty));
            Assert.Same(columnTheme, overrideFilter.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTextColumn defaultColumn, DataGridTextColumn overrideColumn) CreateGrid(ControlTheme gridTheme, ControlTheme columnTheme)
    {
        var items = new ObservableCollection<Item>
        {
            new("A"),
            new("B")
        };

        var root = new Window
        {
            Width = 400,
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
            ItemsSource = items,
            ColumnHeaderFilterTheme = gridTheme
        };

        var defaultColumn = new DataGridTextColumn
        {
            Header = "Default",
            Binding = new Binding(nameof(Item.Name)),
            ShowFilterButton = true
        };

        var overrideColumn = new DataGridTextColumn
        {
            Header = "Override",
            Binding = new Binding(nameof(Item.Name)),
            FilterTheme = columnTheme,
            ShowFilterButton = true
        };

        grid.Columns.Add(defaultColumn);
        grid.Columns.Add(overrideColumn);

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

    private static Button GetFilterButton(DataGridColumnHeader header)
    {
        return header.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Name == "PART_FilterButton");
    }

    private class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}

