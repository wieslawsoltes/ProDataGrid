// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowGroupThemeTests
{
    [AvaloniaFact]
    public void RowGroupTheme_Is_Applied()
    {
        var rowGroupTheme = new ControlTheme(typeof(DataGridRowGroupHeader));
        var (grid, root) = CreateGroupedGrid(rowGroupTheme);

        try
        {
            var header = grid.GetVisualDescendants().OfType<DataGridRowGroupHeader>().First();
            Assert.Same(rowGroupTheme, header.GetValue(StyledElement.ThemeProperty));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGroupedGrid(ControlTheme rowGroupTheme)
    {
        var items = new ObservableCollection<Item>
        {
            new("A", "G1"),
            new("B", "G1"),
            new("C", "G2")
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Item.Group)));

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
            ItemsSource = view,
            RowGroupTheme = rowGroupTheme
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return (grid, root);
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
