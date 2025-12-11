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

public class DataGridRowStylingTests
{
    [AvaloniaFact]
    public void RowTheme_Is_Applied_From_Grid()
    {
        var rowTheme = new ControlTheme(typeof(DataGridRow));
        var (grid, root, items) = CreateGrid();
        grid.RowTheme = rowTheme;
        grid.ItemsSource = null;
        grid.ItemsSource = items;
        grid.UpdateLayout();

        try
        {
            var row = grid.GetVisualDescendants().OfType<DataGridRow>().First();
            Assert.Same(rowTheme, row.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, ObservableCollection<Item> items) CreateGrid()
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

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        return (grid, root, items);
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

