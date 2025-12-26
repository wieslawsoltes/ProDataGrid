// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionGroupingTests
{
    [AvaloniaFact]
    public void Grouped_Selection_Preserves_Selected_Items()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Group = "A", Name = "One" },
            new() { Group = "A", Name = "Two" },
            new() { Group = "B", Name = "Three" },
            new() { Group = "B", Name = "Four" }
        };

        dynamic view = CreateGroupedView(items, nameof(Item.Group));
        var selection = new SelectionModel<Item> { SingleSelect = false };
        var grid = CreateGrid(view, selection);

        selection.Select(0);
        selection.Select(2);
        grid.UpdateLayout();

        var selectedItems = ((System.Collections.IEnumerable)grid.SelectedItems).Cast<Item>().ToList();
        var names = new List<string>();
        foreach (var it in selectedItems)
        {
            names.Add(it.Name);
        }
        names.Sort();
        Assert.Equal(new[] { "One", "Three" }, names);
        Assert.Equal(2, selection.SelectedItems.Count);
    }

    [AvaloniaFact]
    public void Grouping_Added_And_Removed_Does_Not_Clear_Selection()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Group = "A", Name = "One" },
            new() { Group = "A", Name = "Two" },
            new() { Group = "B", Name = "Three" },
        };

        dynamic view = CreateGroupedView(items, null); // start ungrouped
        var selection = new SelectionModel<Item> { SingleSelect = false };
        var grid = CreateGrid(view, selection);

        selection.Select(1);
        grid.UpdateLayout();

        // Add grouping
        SetGroup(view, nameof(Item.Group));
        grid.UpdateLayout();
        Assert.Contains(items[1], selection.SelectedItems);
        Assert.Single(grid.SelectedItems);

        // Remove grouping
        ClearGrouping(view);
        grid.UpdateLayout();
        Assert.Contains(items[1], selection.SelectedItems);
        Assert.Single(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void Setting_Grouped_ItemsSource_When_Attached_Does_Not_Throw()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Group = "A", Name = "One" },
            new() { Group = "A", Name = "Two" },
            new() { Group = "B", Name = "Three" },
        };

        dynamic view = CreateGroupedView(items, nameof(Item.Group));
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            Selection = new SelectionModel<Item>(),
            SelectionMode = DataGridSelectionMode.Single,
            AutoScrollToSelectedItem = true,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Group",
            Binding = new Binding(nameof(Item.Group))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        try
        {
            var exception = Record.Exception(() => grid.ItemsSource = (System.Collections.IEnumerable)view);
            Assert.Null(exception);
            grid.UpdateLayout();
        }
        finally
        {
            root.Close();
        }
    }

    private static dynamic CreateGroupedView(IEnumerable<Item> items, string? groupPath)
    {
        var assembly = typeof(DataGrid).Assembly;
        var viewType = assembly.GetType("Avalonia.Collections.DataGridCollectionView")
                      ?? throw new InvalidOperationException("Could not locate DataGridCollectionView type.");
        dynamic view = Activator.CreateInstance(viewType, items)!;
        if (!string.IsNullOrEmpty(groupPath))
        {
            SetGroup(view, groupPath!);
        }
        return view;
    }

    private static void SetGroup(dynamic view, string groupPath)
    {
        var assembly = typeof(DataGrid).Assembly;
        var groupType = assembly.GetType("Avalonia.Collections.DataGridPathGroupDescription")
                        ?? throw new InvalidOperationException("Could not locate DataGridPathGroupDescription type.");
        dynamic group = Activator.CreateInstance(groupType, groupPath)!;
        var groups = (System.Collections.IList)view.GroupDescriptions;
        groups.Clear();
        groups.Add(group);
    }

    private static void ClearGrouping(dynamic view)
    {
        ((System.Collections.IList)view.GroupDescriptions).Clear();
    }

    private static DataGrid CreateGrid(System.Collections.IEnumerable items, SelectionModel<Item> selection)
    {
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Extended
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Group",
            Binding = new Binding(nameof(Item.Group))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();

        return grid;
    }

    private class Item
    {
        public string Group { get; set; } = "";

        public string Name { get; set; } = "";
    }
}
