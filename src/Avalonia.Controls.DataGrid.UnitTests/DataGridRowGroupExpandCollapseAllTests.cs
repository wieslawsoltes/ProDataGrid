// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowGroupExpandCollapseAllTests
{
    [AvaloniaFact]
    public void CollapseAllGroups_Collapses_All_Groups_And_Hides_SubGroups()
    {
        var (grid, view, root) = CreateNestedGroupedGrid();

        try
        {
            var topGroups = GetTopLevelGroups(view);
            var totalGroups = CountAllGroups(topGroups);

            Assert.Equal(totalGroups, GetGroupHeaders(grid).Count);

            grid.CollapseAllGroups();
            grid.UpdateLayout();

            var collapsedHeaders = GetGroupHeaders(grid);
            Assert.All(collapsedHeaders, header => Assert.NotNull(header.RowGroupInfo));
            Assert.All(collapsedHeaders, header => Assert.False(header.RowGroupInfo!.IsVisible));

            var visibleHeaders = collapsedHeaders.Where(header => header.IsVisible).ToList();
            Assert.Equal(topGroups.Count, visibleHeaders.Count);
            Assert.All(visibleHeaders, header => Assert.Equal(0, header.RowGroupInfo!.Level));

            var subGroupHeaders = collapsedHeaders.Where(header => header.RowGroupInfo!.Level > 0).ToList();
            Assert.NotEmpty(subGroupHeaders);
            Assert.All(subGroupHeaders, header => Assert.False(header.IsVisible));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandAllGroups_Expands_All_Groups_After_Collapse()
    {
        var (grid, view, root) = CreateNestedGroupedGrid();

        try
        {
            var totalGroups = CountAllGroups(GetTopLevelGroups(view));

            grid.CollapseAllGroups();
            grid.UpdateLayout();
            grid.ExpandAllGroups();
            grid.UpdateLayout();

            var expandedHeaders = GetGroupHeaders(grid);
            Assert.Equal(totalGroups, expandedHeaders.Count);
            Assert.All(expandedHeaders, header => Assert.NotNull(header.RowGroupInfo));
            Assert.All(expandedHeaders, header => Assert.True(header.RowGroupInfo!.IsVisible));

            var visibleHeaders = expandedHeaders.Where(header => header.IsVisible).ToList();
            Assert.Contains(visibleHeaders, header => header.RowGroupInfo!.Level > 0);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandCollapseAllGroups_With_No_Grouping_Is_NoOp()
    {
        var items = new List<Item>
        {
            new("A", "X", "One"),
            new("B", "Y", "Two")
        };

        var root = new Window
        {
            Width = 400,
            Height = 300,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };

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
            Assert.Empty(GetGroupHeaders(grid));

            grid.CollapseAllGroups();
            grid.ExpandAllGroups();
            grid.UpdateLayout();

            Assert.Empty(GetGroupHeaders(grid));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, DataGridCollectionView view, Window root) CreateNestedGroupedGrid()
    {
        var items = new List<Item>
        {
            new("A", "X", "One"),
            new("A", "Y", "Two"),
            new("A", "Y", "Three"),
            new("B", "X", "Four"),
            new("B", "Z", "Five"),
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Item.Category)));
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Item.SubCategory)));
        view.Refresh();

        var root = new Window
        {
            Width = 600,
            Height = 400,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Category",
            Binding = new Binding(nameof(Item.Category))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "SubCategory",
            Binding = new Binding(nameof(Item.SubCategory))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, view, root);
    }

    private static IReadOnlyList<DataGridRowGroupHeader> GetGroupHeaders(DataGrid grid)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToList();
    }

    private static IReadOnlyList<DataGridCollectionViewGroup> GetTopLevelGroups(DataGridCollectionView view)
    {
        return view.Groups?.Cast<DataGridCollectionViewGroup>().ToList()
               ?? new List<DataGridCollectionViewGroup>();
    }

    private static int CountAllGroups(IEnumerable<DataGridCollectionViewGroup> groups)
    {
        var count = 0;
        foreach (var group in groups)
        {
            count++;
            count += CountAllGroups(group.Items.OfType<DataGridCollectionViewGroup>());
        }
        return count;
    }

    private record Item(string Category, string SubCategory, string Name);
}
