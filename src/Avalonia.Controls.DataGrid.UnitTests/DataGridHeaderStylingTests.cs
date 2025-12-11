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

public class DataGridHeaderStylingTests
{
    [AvaloniaFact]
    public void HeaderStyleClasses_Applied_And_Update_When_Collection_Changes()
    {
        var (grid, root, column) = CreateGridWithHeaderClasses();

        try
        {
            var header = GetHeaderForColumn(grid, column);
            Assert.Contains("pill", header.Classes);
            Assert.Contains("accent-border", header.Classes);
            Assert.DoesNotContain("strong-text", header.Classes);

            column.HeaderStyleClasses.Add("strong-text");
            grid.UpdateLayout();

            header = GetHeaderForColumn(grid, column);
            Assert.Contains("strong-text", header.Classes);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_ColumnHeaderTheme_Change_Propagates_When_Column_Has_No_HeaderTheme()
    {
        var theme1 = new ControlTheme(typeof(DataGridColumnHeader));
        var theme2 = new ControlTheme(typeof(DataGridColumnHeader));
        var columnTheme = new ControlTheme(typeof(DataGridColumnHeader));

        var (grid, root, themedColumn, defaultColumn) = CreateGridForThemePropagation(theme1, theme2, columnTheme);

        try
        {
            var defaultHeader = GetHeaderForColumn(grid, defaultColumn);
            var themedHeader = GetHeaderForColumn(grid, themedColumn);

            Assert.Same(theme1, defaultHeader.GetValue(StyledElement.ThemeProperty));
            Assert.Same(columnTheme, themedHeader.GetValue(StyledElement.ThemeProperty));

            grid.ColumnHeaderTheme = theme2;
            grid.UpdateLayout();

            defaultHeader = GetHeaderForColumn(grid, defaultColumn);
            themedHeader = GetHeaderForColumn(grid, themedColumn);

            Assert.Same(theme2, defaultHeader.GetValue(StyledElement.ThemeProperty));
            Assert.Same(columnTheme, themedHeader.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTextColumn column) CreateGridWithHeaderClasses()
    {
        var items = new ObservableCollection<Item>
        {
            new("A"),
            new("B")
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
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        };
        column.HeaderStyleClasses.Add("pill");
        column.HeaderStyleClasses.Add("accent-border");

        grid.Columns.Add(column);
        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return (grid, root, column);
    }

    private static (DataGrid grid, Window root, DataGridTextColumn themedColumn, DataGridTextColumn defaultColumn) CreateGridForThemePropagation(
        ControlTheme theme1,
        ControlTheme theme2,
        ControlTheme columnTheme)
    {
        var items = new ObservableCollection<Item>
        {
            new("A", "G1"),
            new("B", "G2")
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
            ItemsSource = items,
            ColumnHeaderTheme = theme1
        };

        var defaultColumn = new DataGridTextColumn
        {
            Header = "Default",
            Binding = new Binding(nameof(Item.Name))
        };

        var themedColumn = new DataGridTextColumn
        {
            Header = "Themed",
            Binding = new Binding(nameof(Item.Group)),
            HeaderTheme = columnTheme
        };

        grid.Columns.Add(defaultColumn);
        grid.Columns.Add(themedColumn);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, themedColumn, defaultColumn);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
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

