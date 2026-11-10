// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

/// <summary>
/// Tests for DataGridDisplayData functionality including row recycling,
/// element tracking, and slot management.
/// </summary>
public class DataGridDisplayDataTests
{
    [AvaloniaFact]
    public void ScrollingElementMembership_DoesNotAllocate_AndTracksRemoval()
    {
        var grid = new DataGrid();
        var display = grid.DisplayData;
        var first = new DataGridRow();
        var second = new DataGridRow();
        var absent = new DataGridRow();
        Assert.False(display.ContainsScrollingElement(first));
        display.LoadScrollingSlot(0, first, updateSlotInformation: true);
        display.LoadScrollingSlot(1, second, updateSlotInformation: true);
        Assert.True(display.ContainsScrollingElement(first));
        Assert.True(display.ContainsScrollingElement(second));
        Assert.False(display.ContainsScrollingElement(absent));

        // Warm both present and missing lookups before measuring the hot path.
        for (int index = 0; index < 100; index++)
        {
            display.ContainsScrollingElement(second);
            display.ContainsScrollingElement(absent);
        }
        int found = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            if (display.ContainsScrollingElement(second)) found++;
            if (display.ContainsScrollingElement(absent)) found++;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1000, found);
        Assert.Equal(0, allocated);

        display.ClearElements(recycle: false);
        Assert.False(display.ContainsScrollingElement(first));
        Assert.False(display.ContainsScrollingElement(second));
    }

    #region Row Recycling Tests

    [AvaloniaFact]
    public void Rows_Are_Recycled_When_Scrolling_Down()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var initialRows = GetRows(target);
        var initialRowCount = initialRows.Count;
        
        Assert.True(initialRowCount > 0);
        Assert.True(initialRowCount < items.Count); // Not all rows should be realized
        
        // Scroll down
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var newRows = GetRows(target);
        
        // Row count should be similar (recycled, not created new)
        Assert.InRange(newRows.Count, initialRowCount - 2, initialRowCount + 2);
    }

    [AvaloniaFact]
    public void Rows_Are_Recycled_When_Scrolling_Up()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // First scroll down
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var midScrollRows = GetRows(target);
        var midScrollRowCount = midScrollRows.Count;
        
        // Now scroll back up
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var finalRows = GetRows(target);
        
        // Row count should be similar (recycled, not created new)
        Assert.InRange(finalRows.Count, midScrollRowCount - 2, midScrollRowCount + 2);
    }

    [AvaloniaFact]
    public void Recycled_Rows_Are_Hidden()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var initialRows = GetRows(target);
        
        // All initially visible rows should be visible
        Assert.All(initialRows, row => Assert.True(row.IsVisible));
        
        // Scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all rows including recycled ones from Children
        var allRows = GetAllRowsFromPresenter(target);
        var visibleRows = allRows.Where(r => r.IsVisible).ToList();
        
        // Only displayed rows should be visible
        foreach (var row in visibleRows)
        {
            Assert.InRange(row.Index, GetFirstRealizedRowIndex(target), GetLastRealizedRowIndex(target));
        }
    }

    #endregion

    #region Slot Tracking Tests

    [AvaloniaFact]
    public void FirstScrollingSlot_Is_Correct_Initially()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        Assert.Equal(0, GetFirstRealizedRowIndex(target));
    }

    [AvaloniaFact]
    public void LastScrollingSlot_Is_Correct_Initially()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var lastIndex = GetLastRealizedRowIndex(target);
        Assert.True(lastIndex > 0);
        Assert.True(lastIndex < items.Count);
    }

    [AvaloniaFact]
    public void Slot_Indexes_Update_After_Scroll()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        Assert.Equal(0, GetFirstRealizedRowIndex(target));
        
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var firstIndex = GetFirstRealizedRowIndex(target);
        var lastIndex = GetLastRealizedRowIndex(target);
        
        Assert.True(firstIndex > 0);
        Assert.True(lastIndex >= 50);
        Assert.True(firstIndex <= 50);
    }

    #endregion

    #region Element Display Tests

    [AvaloniaFact]
    public void Displayed_Rows_Have_Correct_DataContext()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var rows = GetRows(target);
        
        foreach (var row in rows)
        {
            var expectedItem = items[row.Index];
            Assert.Equal(expectedItem, row.DataContext);
        }
    }

    [AvaloniaFact]
    public void Displayed_Rows_Have_Correct_Index_After_Scroll()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        foreach (var row in rows)
        {
            var expectedItem = items[row.Index];
            Assert.Equal(expectedItem, row.DataContext);
            Assert.Equal(expectedItem.Name, $"Item {row.Index}");
        }
    }

    #endregion

    #region Fast Scrolling Tests

    [AvaloniaFact]
    public void Fast_Scroll_To_End_Works()
    {
        var items = Enumerable.Range(0, 1000).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Fast scroll to end
        target.ScrollIntoView(items[999], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var lastIndex = GetLastRealizedRowIndex(target);
        Assert.Equal(999, lastIndex);
        
        var rows = GetRows(target);
        Assert.All(rows, row => Assert.True(row.IsVisible));
    }

    [AvaloniaFact]
    public void Fast_Scroll_To_Beginning_Works()
    {
        var items = Enumerable.Range(0, 1000).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Scroll to end first
        target.ScrollIntoView(items[999], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Then fast scroll back to beginning
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var firstIndex = GetFirstRealizedRowIndex(target);
        Assert.Equal(0, firstIndex);
        
        var rows = GetRows(target);
        Assert.All(rows, row => Assert.True(row.IsVisible));
    }

    [AvaloniaFact]
    public void Fast_Scroll_Multiple_Times_Works()
    {
        var items = Enumerable.Range(0, 1000).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Scroll multiple times
        for (int i = 0; i < 5; i++)
        {
            target.ScrollIntoView(items[999], target.ColumnDefinitions[0]);
            target.UpdateLayout();
            
            target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
            target.UpdateLayout();
            
            target.ScrollIntoView(items[500], target.ColumnDefinitions[0]);
            target.UpdateLayout();
        }
        
        var rows = GetRows(target);
        
        // All visible rows should be visible and have correct data
        Assert.All(rows, row =>
        {
            Assert.True(row.IsVisible);
            Assert.Equal(items[row.Index], row.DataContext);
        });
    }

    #endregion

    #region Item Insertion/Deletion Tests

    [AvaloniaFact]
    public void Slots_Update_After_Item_Insertion_Above_Viewport()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var firstIndexBefore = GetFirstRealizedRowIndex(target);
        
        // Insert item at beginning (above viewport)
        items.Insert(0, new TestItem("New Item"));
        target.ItemsSource = null;
        target.ItemsSource = items;
        target.UpdateLayout();
        
        // Scroll back to where we were (adjusted for insertion)
        target.ScrollIntoView(items[51], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        Assert.All(rows, row => Assert.True(row.IsVisible));
    }

    [AvaloniaFact]
    public void Slots_Update_After_Item_Deletion()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var rowCountBefore = GetRows(target).Count;
        
        // Delete first item
        items.RemoveAt(0);
        target.ItemsSource = null;
        target.ItemsSource = items;
        target.UpdateLayout();
        
        var rows = GetRows(target);
        Assert.All(rows, row =>
        {
            Assert.True(row.IsVisible);
            Assert.Equal(items[row.Index], row.DataContext);
        });
    }

    #endregion

    #region Grouping Tests

    [AvaloniaFact]
    public void Grouped_Data_Displays_Group_Headers()
    {
        var items = Enumerable.Range(0, 50).Select(x => new TestItem($"Item {x}") { Category = x < 25 ? "A" : "B" }).ToList();
        var target = CreateGroupedTarget(items, "Category");
        
        var groupHeaders = GetGroupHeaders(target);
        
        Assert.True(groupHeaders.Count > 0);
    }

    [AvaloniaFact]
    public void Grouped_Data_Scrolling_Works()
    {
        var items = Enumerable.Range(0, 100).Select(x => new TestItem($"Item {x}") { Category = (x / 10).ToString() }).ToList();
        var target = CreateGroupedTarget(items, "Category");
        
        // Scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var groupHeaders = GetGroupHeaders(target);
        
        // All should be visible
        Assert.All(rows, row => Assert.True(row.IsVisible));
        Assert.All(groupHeaders, header => Assert.True(header.IsVisible));
    }

    [AvaloniaFact]
    public void Group_Headers_Are_Recycled_When_Scrolling()
    {
        var items = Enumerable.Range(0, 200).Select(x => new TestItem($"Item {x}") { Category = (x / 10).ToString() }).ToList();
        var target = CreateGroupedTarget(items, "Category");
        
        var initialHeaders = GetGroupHeaders(target);
        var initialCount = initialHeaders.Count;
        
        // Scroll to end
        target.ScrollIntoView(items[199], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Scroll back
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var finalHeaders = GetGroupHeaders(target);
        
        // Header count should be similar (recycled)
        Assert.InRange(finalHeaders.Count, initialCount - 2, initialCount + 2);
    }

    #endregion

    #region Helper Methods

    private static DataGrid CreateTarget(IList<TestItem> items)
    {
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        return target;
    }

    private static DataGrid CreateGroupedTarget(IList<TestItem> items, string groupProperty)
    {
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var collectionView = new DataGridCollectionView(items);
        collectionView.GroupDescriptions.Add(new DataGridPathGroupDescription(groupProperty));

        var target = new DataGrid
        {
            ItemsSource = collectionView,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        return target;
    }

    private static int GetFirstRealizedRowIndex(DataGrid target)
    {
        var rows = GetRows(target);
        return rows.Count > 0 ? rows.Select(x => x.Index).Min() : -1;
    }

    private static int GetLastRealizedRowIndex(DataGrid target)
    {
        var rows = GetRows(target);
        return rows.Count > 0 ? rows.Select(x => x.Index).Max() : -1;
    }

    private static IReadOnlyList<DataGridRow> GetRows(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(r => r.IsVisible)
            .ToList();
    }

    private static IReadOnlyList<DataGridRow> GetAllRowsFromPresenter(DataGrid target)
    {
        var presenter = target.GetSelfAndVisualDescendants()
            .OfType<Primitives.DataGridRowsPresenter>()
            .FirstOrDefault();
        
        if (presenter == null)
            return Array.Empty<DataGridRow>();
        
        return presenter.Children.OfType<DataGridRow>().ToList();
    }

    private static IReadOnlyList<DataGridRowGroupHeader> GetGroupHeaders(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .Where(h => h.IsVisible)
            .ToList();
    }

    #endregion

    #region Test Model

    private class TestItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private string _category = "Default";

        public TestItem(string name) => _name = name;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    RaisePropertyChanged();
                }
            }
        }
    }

    #endregion
}
