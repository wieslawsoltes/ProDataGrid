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
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
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

    [AvaloniaFact]
    public void OptimizedRowTheme_Propagates_RowBackground_Initially_And_After_Change()
    {
        var initialBackground = new SolidColorBrush(Colors.Crimson);
        var updatedBackground = new SolidColorBrush(Colors.CornflowerBlue);
        var (grid, root, items) = CreateGrid();

        try
        {
            Assert.True(grid.TryFindResource("DataGridOptimizedRowTheme", out var rowTheme));
            grid.RowBackground = initialBackground;
            grid.RowTheme = Assert.IsType<ControlTheme>(rowTheme);
            grid.ItemsSource = null;
            grid.ItemsSource = items;
            Dispatcher.UIThread.RunJobs();
            root.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.IsType<DataGridRow>(grid.GetRowFromItem(items[0]));
            var rowRoot = Assert.Single(row.GetVisualDescendants().OfType<DataGridFrozenGrid>());
            Assert.Same(initialBackground, row.Background);
            Assert.Same(initialBackground, rowRoot.Background);

            grid.RowBackground = updatedBackground;
            Dispatcher.UIThread.RunJobs();
            root.UpdateLayout();
            grid.UpdateLayout();

            Assert.Same(row, grid.GetRowFromItem(items[0]));
            Assert.Same(updatedBackground, row.Background);
            Assert.Same(updatedBackground, rowRoot.Background);
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
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
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

