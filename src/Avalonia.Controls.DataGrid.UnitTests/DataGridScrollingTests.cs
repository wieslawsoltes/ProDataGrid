// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

/// <summary>
/// Tests for DataGrid scrolling behavior with ILogicalScrollable.
/// </summary>
public class DataGridScrollingTests
{
    #region Row Position Tests

    [AvaloniaFact]
    public void Rows_Are_Positioned_Sequentially_Without_Overlap()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act
        var rows = GetRows(target);
        
        // Assert - each row's top should be >= previous row's bottom
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5, // Allow small tolerance for rounding
                $"Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void Rows_Do_Not_Overlap_After_Vertical_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - scroll down
        target.ScrollIntoView(items[20], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap after scrolling
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After scroll: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void Rows_Do_Not_Overlap_After_Multiple_Scroll_Operations()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - scroll multiple times
        for (int scrollTo = 10; scrollTo <= 50; scrollTo += 10)
        {
            target.ScrollIntoView(items[scrollTo], target.ColumnDefinitions[0]);
            target.UpdateLayout();
            
            var rows = GetRows(target);
            
            // Assert - rows should not overlap after each scroll
            var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
            for (int i = 1; i < orderedRows.Count; i++)
            {
                var prevRow = orderedRows[i - 1];
                var currentRow = orderedRows[i];
                
                Assert.True(
                    currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                    $"After scrolling to {scrollTo}: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
            }
        }
    }

    [AvaloniaFact]
    public void Rows_Do_Not_Overlap_After_Scroll_Up_And_Down()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - scroll down then up
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        target.ScrollIntoView(items[10], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After scroll up/down: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    #endregion

    #region Row Height Consistency Tests

    [AvaloniaFact]
    public void All_Rows_Have_Consistent_Height()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act
        var rows = GetRows(target);
        
        // Assert - all rows should have roughly the same height
        var heights = rows.Select(r => r.Bounds.Height).ToList();
        var avgHeight = heights.Average();
        
        foreach (var height in heights)
        {
            Assert.True(
                Math.Abs(height - avgHeight) < 2.0, // Allow 2px tolerance
                $"Row height {height} differs significantly from average {avgHeight}");
        }
    }

    [AvaloniaFact]
    public void Row_Heights_Are_Preserved_After_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var initialRows = GetRows(target);
        var initialHeight = initialRows.First().Bounds.Height;
        
        // Act - scroll
        target.ScrollIntoView(items[30], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var scrolledRows = GetRows(target);
        
        // Assert - rows should maintain consistent height
        foreach (var row in scrolledRows)
        {
            Assert.True(
                Math.Abs(row.Bounds.Height - initialHeight) < 2.0,
                $"Row {row.Index} height {row.Bounds.Height} differs from initial height {initialHeight}");
        }
    }

    #endregion

    #region ILogicalScrollable Offset Tests

    [AvaloniaFact]
    public void RowsPresenter_Offset_Changes_On_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var presenter = GetRowsPresenter(target);
        var initialOffset = presenter.Offset;
        
        // Act
        target.ScrollIntoView(items[20], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Assert - offset should have changed
        Assert.True(presenter.Offset.Y > initialOffset.Y, 
            $"Offset should increase after scrolling down. Initial: {initialOffset.Y}, After: {presenter.Offset.Y}");
    }

    [AvaloniaFact]
    public void RowsPresenter_Offset_Is_Consistent_With_Row_Positions()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - scroll to middle
        target.ScrollIntoView(items[30], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var presenter = GetRowsPresenter(target);
        var rows = GetRows(target);
        var firstVisibleRow = rows.OrderBy(r => r.Index).First();
        
        // Assert - first visible row should be positioned near top of viewport
        Assert.True(
            firstVisibleRow.Bounds.Top <= 0 || Math.Abs(firstVisibleRow.Bounds.Top) < firstVisibleRow.Bounds.Height,
            $"First visible row (index {firstVisibleRow.Index}) top {firstVisibleRow.Bounds.Top} should be near viewport top");
    }

    [AvaloniaFact]
    public void Extent_Is_Larger_Than_Viewport_For_Large_Data()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var presenter = GetRowsPresenter(target);
        
        // Assert - extent should be larger than viewport for 100 items
        Assert.True(presenter.Extent.Height > presenter.Viewport.Height,
            $"Extent height ({presenter.Extent.Height}) should be larger than viewport ({presenter.Viewport.Height}) for 100 items");
    }

    #endregion

    #region PendingVerticalScrollHeight Accumulation Tests

    [AvaloniaFact]
    public void Scroll_Delta_Is_Accumulated_Not_Replaced()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var presenter = GetRowsPresenter(target);
        
        // Record initial row positions
        target.UpdateLayout();
        var initialRows = GetRows(target);
        var initialFirstIndex = initialRows.Min(r => r.Index);
        
        // Act - apply multiple scroll offsets in sequence
        presenter.Offset = new Vector(0, 50);
        target.UpdateLayout();
        
        presenter.Offset = new Vector(0, 100);
        target.UpdateLayout();
        
        presenter.Offset = new Vector(0, 150);
        target.UpdateLayout();
        
        var finalRows = GetRows(target);
        var finalFirstIndex = finalRows.Min(r => r.Index);
        
        // Assert - we should have scrolled past initial rows
        Assert.True(finalFirstIndex > initialFirstIndex,
            $"After scrolling, first visible row index ({finalFirstIndex}) should be greater than initial ({initialFirstIndex})");
    }

    [AvaloniaFact]
    public void Rapid_Scroll_Changes_Result_In_Correct_Position()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        var presenter = GetRowsPresenter(target);
        
        // Act - rapidly change offset without waiting for layout
        for (int i = 1; i <= 10; i++)
        {
            presenter.Offset = new Vector(0, i * 20);
        }
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After rapid scroll: Row {currentRow.Index} overlaps with row {prevRow.Index}");
        }
    }

    #endregion

    #region Scroll To Beginning/End Tests

    [AvaloniaFact]
    public void Scroll_To_Beginning_Shows_First_Row()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Scroll to middle first
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Act - scroll back to beginning
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var firstVisibleIndex = rows.Min(r => r.Index);
        
        // Assert - first row should be visible
        Assert.Equal(0, firstVisibleIndex);
    }

    [AvaloniaFact]
    public void Scroll_To_End_Shows_Last_Row()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - scroll to end
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var lastVisibleIndex = rows.Max(r => r.Index);
        
        // Assert - last row should be visible
        Assert.Equal(99, lastVisibleIndex);
    }

    #endregion

    #region UseLogicalScrollable Property Tests

    [AvaloniaFact]
    public void UseLogicalScrollable_Is_False_By_Default()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Assert - default is false for backward compatibility
        Assert.False(target.UseLogicalScrollable);
    }

    [AvaloniaFact]
    public void UseLogicalScrollable_Can_Be_Set_To_True()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        
        // Assert
        Assert.True(target.UseLogicalScrollable);
    }

    [AvaloniaFact]
    public void IsLogicalScrollEnabled_Matches_UseLogicalScrollable()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Assert
        Assert.Equal(target.UseLogicalScrollable, presenter.IsLogicalScrollEnabled);
    }

    #endregion

    #region Viewport Resize Recycling

    [AvaloniaFact]
    public void Rows_Recycle_When_Viewport_Shrinks_With_LogicalScrollable()
    {
        // Arrange
        var items = Enumerable.Range(0, 500).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 300, useLogicalScrollable: true);
        target.TrimRecycledContainers = true;
        target.KeepRecycledContainersInVisualTree = false;
        var root = (Window)target.GetVisualRoot()!;
        var presenter = GetRowsPresenter(target);

        root.UpdateLayout();
        root.UpdateLayout(); // allow invalidated measure to run after arrange height mismatch
        var initialVisible = GetRows(target).Count;
        var initialChildren = presenter.Children.OfType<DataGridRow>().Count();
        var initialRecycled = GetRecycledRowCount(target);

        // Act - expand viewport significantly
        root.Height = 900;
        root.UpdateLayout();
        var expandedVisible = GetRows(target).Count;
        var expandedChildren = presenter.Children.OfType<DataGridRow>().Count();
        var expandedRecycled = GetRecycledRowCount(target);

        // Sanity check that expansion realized more rows
        Assert.True(expandedVisible > initialVisible,
            $"Expected more realized rows after expansion. Initial: {initialVisible}, Expanded: {expandedVisible}, RootHeight: {root.Bounds.Height}, GridHeight: {target.Bounds.Height}, Viewport: {presenter.Viewport.Height}, Available: {target.RowsPresenterAvailableSize?.Height}");

        // Act - shrink back to original height
        root.Height = 300;
        root.UpdateLayout();
        var shrunkVisible = GetRows(target).Count;
        var shrunkChildren = presenter.Children.OfType<DataGridRow>().Count();
        var shrunkRecycled = GetRecycledRowCount(target);

        // Assert - rows should be recycled back down near the initial viewport size
        Assert.True(shrunkVisible <= initialVisible + 2,
            $"Rows were not recycled after shrinking. Initial: {initialVisible}, Shrunk: {shrunkVisible}, Expanded: {expandedVisible}");
        Assert.True(shrunkVisible < expandedVisible,
            $"Expected fewer realized rows after shrinking. Shrunk: {shrunkVisible}, Expanded: {expandedVisible}");

        // Hidden/recycled containers should also be trimmed so the child collection does not grow unbounded
        Assert.True(shrunkChildren <= initialChildren + 4,
            $"RowsPresenter kept too many recycled children after shrinking. Initial children: {initialChildren}, Expanded: {expandedChildren}, Shrunk: {shrunkChildren}");

        // The recycle pool should also be trimmed when the viewport contracts
        const int recyclePoolLimit = 8;
        Assert.True(shrunkRecycled <= recyclePoolLimit,
            $"Recycle pool grew from {initialRecycled} to {shrunkRecycled} after shrink (expanded to {expandedRecycled}).");
    }

    [AvaloniaFact]
    public void Recycling_Trim_Can_Be_Disabled()
    {
        // Arrange
        var items = Enumerable.Range(0, 500).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 300, useLogicalScrollable: true);
        target.TrimRecycledContainers = false;
        var root = (Window)target.GetVisualRoot()!;

        root.UpdateLayout();
        var initialRecycled = GetRecycledRowCount(target);

        // Act - expand then shrink
        root.Height = 900;
        root.UpdateLayout();
        var expandedRecycled = GetRecycledRowCount(target);

        root.Height = 300;
        root.UpdateLayout();
        var shrunkRecycled = GetRecycledRowCount(target);

        // Assert - with trimming disabled the recycle pool should stay large
        Assert.True(shrunkRecycled >= expandedRecycled - 1,
            $"Expected recycle pool to remain large when trimming is disabled. Expanded: {expandedRecycled}, Shrunk: {shrunkRecycled}, Initial: {initialRecycled}");
    }

    [AvaloniaFact]
    public void Tiny_Viewport_Does_Not_Realize_Excess_Rows()
    {
        // Arrange
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 40, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);

        // Act
        target.UpdateLayout();
        var realizedRows = GetRows(target).Count;
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);

        // Assert - viewport only fits ~2 rows; allow small buffer for prefetch/prefill
        Assert.True(realizedRows <= 8, $"Realized rows={realizedRows}, presenter children={presenterChildren}, recycled={recycled}");
        Assert.True(presenterChildren <= 12, $"Presenter children grew unexpectedly for tiny viewport: {presenterChildren}");
    }

    [AvaloniaFact]
    public void Fixed_Height_In_StackPanel_Does_Not_Realize_Whole_Window()
    {
        // Arrange
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 400,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            Height = 40,
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { target }
        };

        root.Show();

        // Act
        root.UpdateLayout();
        var rows = GetRows(target);
        var presenter = GetRowsPresenter(target);
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);

        // Assert - viewport is limited by Height=40; should not realize near full window height
        Assert.True(rows.Count <= 8,
            $"Realized rows={rows.Count}, presenter children={presenterChildren}, recycled={recycled}, RootHeight: {root.Bounds.Height}, GridHeight: {target.Bounds.Height}, Viewport: {presenter.Viewport.Height}, Available: {target.RowsPresenterAvailableSize?.Height}");
        Assert.True(presenterChildren <= 12, $"Presenter children grew unexpectedly for fixed-height stackpanel: {presenterChildren}");
    }

    [AvaloniaFact]
    public void Wrapped_ScrollViewer_With_Small_Viewport_Does_Not_Realize_Excess_Rows()
    {
        // Arrange
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 300,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        var scrollViewer = new ScrollViewer
        {
            Height = 40,
            Content = target,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        root.Content = scrollViewer;
        root.Show();

        // Act
        root.UpdateLayout();
        var rows = GetRows(target);
        var presenter = GetRowsPresenter(target);
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);

        // Assert - wrap in external ScrollViewer should still respect viewport size
        Assert.True(rows.Count <= 17, $"Realized rows={rows.Count}, presenter children={presenterChildren}, recycled={recycled}");
        Assert.True(presenterChildren <= 17, $"Presenter children grew unexpectedly for wrapped scrollviewer: {presenterChildren}");
    }

    [AvaloniaFact]
    public void Shrinking_Viewport_To_Zero_Does_Not_Over_Materialize()
    {
        // Arrange
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 400, useLogicalScrollable: true);
        target.TrimRecycledContainers = true;
        target.KeepRecycledContainersInVisualTree = false;
        var root = (Window)target.GetVisualRoot()!;
        var presenter = GetRowsPresenter(target);
        target.TrimRecycledContainers = true;
        target.KeepRecycledContainersInVisualTree = false;

        root.UpdateLayout();

        // Gradually shrink the host height
        root.Height = 250;
        root.UpdateLayout();

        root.Height = 120;
        root.UpdateLayout();

        root.Height = 1;
        root.UpdateLayout();
        target.UpdateLayout(); // ensure measure reruns after arrange change

        var rows = GetRows(target);
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);

        // Assert - with effectively zero viewport, realized rows should stay near prefetch budget
        Assert.True(rows.Count <= 8, $"Realized rows={rows.Count}, presenter children={presenterChildren}, recycled={recycled}");
        Assert.True(presenterChildren <= 12, $"Presenter children grew unexpectedly after shrinking to zero: {presenterChildren}");
        Assert.True(recycled <= 12, $"Recycle pool grew unexpectedly after shrinking to zero: {recycled}");
    }

    [AvaloniaFact]
    public void Infinite_Measure_With_Small_Arrange_Does_Not_Over_Materialize()
    {
        // Arrange - host measures with Infinity but arranges to a small height to mimic nested scroll viewers
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 300,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
            TrimRecycledContainers = true,
            KeepRecycledContainersInVisualTree = false,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        var host = new InfiniteMeasureHost
        {
            HostHeight = 40,
            Child = target,
        };

        var spacer = new Border();
        Grid.SetRow(spacer, 1);

        root.Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { host, spacer }
        };

        root.Show();

        // Act
        root.UpdateLayout();
        var rows = GetRows(target);
        var presenter = GetRowsPresenter(target);
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);

        // Assert - even with infinite measure input, realized containers should match the small viewport
        Assert.True(rows.Count <= 8, $"Realized rows={rows.Count}, presenter children={presenterChildren}, recycled={recycled}");
        Assert.True(presenterChildren <= 12, $"Presenter children grew unexpectedly for infinite-measure host: {presenterChildren}");
    }

    [AvaloniaFact]
    public void Large_Measure_Small_Arrange_Does_Not_Over_Materialize()
    {
        // Arrange - host measures larger than it arranges to simulate constrained layout
        var items = Enumerable.Range(0, 1000).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 300,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
            TrimRecycledContainers = true,
            KeepRecycledContainersInVisualTree = false,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        var host = new MismatchedMeasureHost
        {
            MeasureHeight = 400,
            ArrangeHeight = 40,
            Child = target,
        };

        root.Content = host;
        root.Show();

        // Act
        root.UpdateLayout();
        var rows = GetRows(target);
        var presenter = GetRowsPresenter(target);
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();
        var recycled = GetRecycledRowCount(target);
        var viewportHeight = presenter.Viewport.Height;
        var availableHeight = target.RowsPresenterAvailableSize?.Height;
        var presenterHeight = presenter.Bounds.Height;
        var targetHeight = target.Bounds.Height;

        // Assert - realized containers should respect the arranged viewport
        Assert.True(rows.Count <= 8,
            $"Realized rows={rows.Count}, presenter children={presenterChildren}, recycled={recycled}, viewport={viewportHeight}, available={availableHeight}, presenterHeight={presenterHeight}, targetHeight={targetHeight}");
        Assert.True(presenterChildren <= 12, $"Presenter children grew unexpectedly for mismatched measure/arrange: {presenterChildren}");
    }

    [AvaloniaFact]
    public void Measure_With_Larger_Available_Height_Does_Not_Clamp_To_Stale_Bounds()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        root.UpdateLayout();

        var initialHeight = target.Bounds.Height;
        Assert.True(initialHeight > 0, "Expected a non-zero initial bounds height.");

        // Act - force a measure pass with a larger available height while arrange is invalid.
        target.InvalidateMeasure();
        var measureHeight = initialHeight + 200;
        target.Measure(new Size(root.Width, measureHeight));

        // Assert
        var available = target.RowsPresenterAvailableSize;
        Assert.True(available.HasValue, "Expected RowsPresenterAvailableSize to be set during measure.");
        Assert.True(available.Value.Height > initialHeight + 1,
            $"Expected available height to exceed initial bounds. Initial={initialHeight}, Available={available.Value.Height}, Measure={measureHeight}.");
    }

    [AvaloniaFact]
    public void Recycled_Rows_Remain_In_VisualTree_When_Flag_Is_Enabled()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 400, useLogicalScrollable: true);
        target.KeepRecycledContainersInVisualTree = true;
        target.TrimRecycledContainers = false;
        var root = (Window)target.GetVisualRoot()!;

        root.UpdateLayout();

        // Shrink to force recycling
        root.Height = 1;
        root.UpdateLayout();
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var recycledRows = GetRecycledRows(target);

        Assert.NotEmpty(recycledRows);

        Assert.All(recycledRows, recycled => Assert.Contains(recycled, presenter.Children));
        Assert.All(recycledRows, recycled => Assert.False(recycled.IsVisible));
    }

    [AvaloniaFact]
    public void Recycled_Rows_Are_Removed_When_Flag_Is_Disabled()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 400, useLogicalScrollable: true);
        target.KeepRecycledContainersInVisualTree = false;
        target.TrimRecycledContainers = false; // keep the pool so we can inspect it
        var root = (Window)target.GetVisualRoot()!;

        root.UpdateLayout();

        // Shrink to force recycling
        root.Height = 1;
        root.UpdateLayout();
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var recycledRows = GetRecycledRows(target);

        Assert.NotEmpty(recycledRows);
        Assert.All(recycledRows, recycled => Assert.DoesNotContain(recycled, presenter.Children));
        Assert.All(recycledRows, recycled => Assert.False(recycled.IsVisible));
    }

    [AvaloniaFact]
    public void Recycled_Rows_Are_Trimmed_When_Keeping_In_VisualTree()
    {
        // Arrange
        var items = Enumerable.Range(0, 300).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateV2Target(items, height: 250, useLogicalScrollable: true);
        target.KeepRecycledContainersInVisualTree = true;
        target.TrimRecycledContainers = true;
        var root = (Window)target.GetVisualRoot()!;
        var presenter = GetRowsPresenter(target);

        root.UpdateLayout();
        root.Height = 700;
        root.UpdateLayout();

        root.Height = 200;
        root.UpdateLayout();
        target.UpdateLayout();

        var recycledRows = GetRecycledRows(target);
        var shrunkRecycled = recycledRows.Count;
        var presenterChildren = presenter.Children.OfType<DataGridRow>().Count();

        Assert.True(shrunkRecycled <= 12, $"Expected trimmed recycle pool. Shrunk: {shrunkRecycled}");
        Assert.True(presenterChildren <= GetRows(target).Count + 12, $"Presenter retained too many rows: {presenterChildren}");
        Assert.All(recycledRows, recycled => Assert.Contains(recycled, presenter.Children));
    }

    [AvaloniaFact]
    public void HidingMode_MoveOffscreen_Moves_Recycled_Bounds()
    {
        // Arrange
        var target = CreateTarget(Enumerable.Range(0, 20).Select(x => new ScrollTestModel($"Item {x}")).ToList(), height: 200);
        target.RecycledContainerHidingMode = DataGridRecycleHidingMode.MoveOffscreen;
        target.KeepRecycledContainersInVisualTree = true;
        target.TrimRecycledContainers = false;
        target.UpdateLayout();

        var row = GetRows(target).First();
        var before = row.Bounds;

        var recycleRow = typeof(DataGridDisplayData).GetMethod("RecycleRow", BindingFlags.Instance | BindingFlags.NonPublic);
        recycleRow!.Invoke(target.DisplayData, new object[] { row });

        Assert.False(row.IsVisible);
    }

    [AvaloniaFact]
    public void HidingMode_SetIsVisibleOnly_Keeps_Last_Bounds()
    {
        // Arrange
        var target = CreateTarget(Enumerable.Range(0, 20).Select(x => new ScrollTestModel($"Item {x}")).ToList(), height: 200);
        target.RecycledContainerHidingMode = DataGridRecycleHidingMode.SetIsVisibleOnly;
        target.KeepRecycledContainersInVisualTree = true;
        target.TrimRecycledContainers = false;
        target.UpdateLayout();

        var row = GetRows(target).First();
        var before = row.Bounds;

        var recycleRow = typeof(DataGridDisplayData).GetMethod("RecycleRow", BindingFlags.Instance | BindingFlags.NonPublic);
        recycleRow!.Invoke(target.DisplayData, new object[] { row });

        Assert.Equal(before, row.Bounds);
    }

    #endregion

    #region Scroll State Preservation Tests

    [AvaloniaFact]
    public void Reattaching_With_Mutated_ItemsSource_Does_Not_Preserve_Scroll_State()
    {
        // Arrange
        var items = new ObservableCollection<ScrollTestModel>(
            Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")));
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        root.UpdateLayout();

        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        root.UpdateLayout();

        Assert.True(GetFirstVisibleRowIndex(target) > 0, "Expected to scroll away from the first row before detaching.");

        // Detach and mutate the source collection (keep count the same).
        root.Content = null;
        root.UpdateLayout();
        items[0] = new ScrollTestModel("Item replacement");

        // Reattach
        root.Content = target;
        root.UpdateLayout();
        target.UpdateLayout();

        var firstVisibleIndex = GetFirstVisibleRowIndex(target);
        Assert.Equal(0, firstVisibleIndex);
    }

    [AvaloniaFact]
    public void Reattaching_Preserves_RowHeightEstimator_State()
    {
        // Arrange
        var items = new ObservableCollection<ScrollTestModel>(
            Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")));
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var estimator = new StateTrackingRowHeightEstimator();
        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowHeightEstimator = estimator,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        root.UpdateLayout();

        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        root.UpdateLayout();

        estimator.StateToken = 42;

        // Detach to capture state, then simulate a reset.
        root.Content = null;
        root.UpdateLayout();
        Assert.True(estimator.CaptureCount > 0, "Expected estimator state capture on detach.");

        estimator.Reset();
        Assert.Equal(0, estimator.StateToken);

        // Reattach and restore.
        root.Content = target;
        root.UpdateLayout();
        target.UpdateLayout();

        Assert.True(estimator.RestoreCount > 0, "Expected estimator state restore on reattach.");
        Assert.Equal(42, estimator.StateToken);
    }

    [AvaloniaFact]
    public void Hiding_DataGrid_Clears_Displayed_Rows()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            KeepRecycledContainersInVisualTree = false,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        root.UpdateLayout();

        Assert.True(target.DisplayData.FirstScrollingSlot >= 0, "Expected rows to be realized before hiding.");

        // Act
        target.IsVisible = false;
        root.UpdateLayout();
        target.UpdateLayout();

        // Assert
        Assert.Equal(-1, target.DisplayData.FirstScrollingSlot);
        Assert.Equal(0, target.DisplayData.NumDisplayedScrollingElements);
    }

    #endregion

    #region Mouse Wheel Scrolling Tests

    [AvaloniaFact]
    public void MouseWheel_Scrolls_In_Legacy_Mode()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140);
        target.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
        target.UpdateLayout();

        var root = (TopLevel)target.GetVisualRoot()!;
        var wheelPoint = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
            root)!.Value;

        var verticalBar = target.GetSelfAndVisualDescendants()
            .OfType<ScrollBar>()
            .FirstOrDefault(sb => sb.Orientation == Orientation.Vertical);

        // Sanity
        Assert.NotNull(verticalBar);

        var initialIndex = GetFirstVisibleRowIndex(target);
        var initialBarValue = verticalBar!.Value;

        // Act
        root.MouseWheel(wheelPoint, new Vector(0, -3));
        target.UpdateLayout();

        var scrolledIndex = GetFirstVisibleRowIndex(target);
        var scrolledBarValue = verticalBar.Value;

        // Assert
        Assert.True(scrolledIndex > initialIndex,
            $"Expected wheel scroll to advance visible rows. Before: {initialIndex}, After: {scrolledIndex}");

        if (verticalBar.Maximum > 0)
        {
            Assert.True(scrolledBarValue > initialBarValue,
                $"Expected legacy scrollbar to move. Before: {initialBarValue}, After: {scrolledBarValue}, Max: {verticalBar.Maximum}");
        }
    }

    [AvaloniaFact]
    public void MouseWheel_Scrolls_In_Logical_Mode()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140, useLogicalScrollable: true);
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var root = (TopLevel)target.GetVisualRoot()!;
        var wheelPoint = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
            root)!.Value;

        var initialIndex = GetFirstVisibleRowIndex(target);
        var initialOffset = presenter.Offset.Y;

        // Act
        root.MouseWheel(wheelPoint, new Vector(0, -3));
        target.UpdateLayout();

        var scrolledIndex = GetFirstVisibleRowIndex(target);
        var scrolledOffset = presenter.Offset.Y;

        // Assert
        Assert.True(scrolledIndex > initialIndex,
            $"Expected wheel scroll to advance visible rows. Before: {initialIndex}, After: {scrolledIndex}");
        Assert.True(scrolledOffset > initialOffset,
            $"Expected wheel scroll to update logical offset. Before: {initialOffset}, After: {scrolledOffset}");
    }

    #endregion

    #region ILogicalScrollable Scrolling Tests

    [AvaloniaFact]
    public void LogicalScrollable_Rows_Are_Positioned_Sequentially_Without_Overlap()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        
        // Act
        var rows = GetRows(target);
        
        // Assert - each row's top should be >= previous row's bottom
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Rows_Do_Not_Overlap_After_Vertical_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        
        // Act - scroll down via ILogicalScrollable
        var presenter = GetRowsPresenter(target);
        presenter.Offset = new Vector(0, 100);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap after scrolling
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After scroll: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Rows_Do_Not_Overlap_After_Multiple_Offset_Changes()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        target.TrimRecycledContainers = true;
        target.KeepRecycledContainersInVisualTree = false;
        var presenter = GetRowsPresenter(target);
        
        // Act - change offset multiple times
        for (int y = 50; y <= 300; y += 50)
        {
            presenter.Offset = new Vector(0, y);
            target.UpdateLayout();
            
            var rows = GetRows(target);
            
            // Assert - rows should not overlap after each scroll
            var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
            for (int i = 1; i < orderedRows.Count; i++)
            {
                var prevRow = orderedRows[i - 1];
                var currentRow = orderedRows[i];
                
                Assert.True(
                    currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                    $"At offset {y}: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
            }
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Offset_Accumulates_Correctly()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - set offset progressively
        presenter.Offset = new Vector(0, 100);
        target.UpdateLayout();
        
        presenter.Offset = new Vector(0, 200);
        target.UpdateLayout();
        
        presenter.Offset = new Vector(0, 300);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var firstVisibleIndex = rows.Min(r => r.Index);
        
        // Assert - should have scrolled past initial rows
        Assert.True(firstVisibleIndex > 0,
            $"After scrolling to offset 300, first visible row index ({firstVisibleIndex}) should be > 0");
    }

    [AvaloniaFact]
    public void LogicalScrollable_Rows_Do_Not_Overlap_After_Scroll_Up()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - scroll down then up
        presenter.Offset = new Vector(0, 300);
        target.UpdateLayout();
        
        presenter.Offset = new Vector(0, 100);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After scroll up: Row {currentRow.Index} (top: {currentRow.Bounds.Top}) overlaps with row {prevRow.Index} (bottom: {prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Rapid_Offset_Changes_Do_Not_Cause_Overlap()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - rapidly change offset multiple times without UpdateLayout between them
        for (int i = 1; i <= 10; i++)
        {
            presenter.Offset = new Vector(0, i * 30);
        }
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - rows should not overlap
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"After rapid scroll: Row {currentRow.Index} (top:{currentRow.Bounds.Top}, bottom:{currentRow.Bounds.Bottom}) overlaps with row {prevRow.Index} (top:{prevRow.Bounds.Top}, bottom:{prevRow.Bounds.Bottom})");
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Row_Top_Positions_Are_Distinct()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - scroll down
        presenter.Offset = new Vector(0, 150);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - each row should have a distinct top position
        var topPositions = rows.Select(r => r.Bounds.Top).ToList();
        var distinctPositions = topPositions.Distinct().ToList();
        
        Assert.Equal(rows.Count, distinctPositions.Count);
    }

    [AvaloniaFact]
    public void LogicalScrollable_Rows_Have_Sequential_Indices()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - scroll down
        presenter.Offset = new Vector(0, 150);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var orderedIndices = rows.OrderBy(r => r.Bounds.Top).Select(r => r.Index).ToList();
        
        // Assert - indices should be sequential (no gaps)
        for (int i = 1; i < orderedIndices.Count; i++)
        {
            Assert.Equal(orderedIndices[i - 1] + 1, orderedIndices[i]);
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Scroll_To_End_Shows_Last_Row()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - scroll to max offset
        var maxOffset = presenter.Extent.Height - presenter.Viewport.Height;
        presenter.Offset = new Vector(0, maxOffset);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var lastVisibleIndex = rows.Max(r => r.Index);
        
        // Assert - last row should be visible
        Assert.Equal(99, lastVisibleIndex);
    }

    [AvaloniaFact]
    public void LogicalScrollable_Scroll_Back_To_Beginning()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Scroll to middle first
        presenter.Offset = new Vector(0, 200);
        target.UpdateLayout();
        
        // Act - scroll back to beginning
        presenter.Offset = new Vector(0, 0);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        var firstVisibleIndex = rows.Min(r => r.Index);
        
        // Assert - first row should be visible
        Assert.Equal(0, firstVisibleIndex);
    }

    [AvaloniaFact]
    public void LogicalScrollable_Fast_Scroll_To_Bottom_Then_Top_No_Ghost_Rows()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, useLogicalScrollable: true);
        var presenter = GetRowsPresenter(target);
        
        // Act - fast scroll to bottom
        var maxOffset = Math.Max(0, presenter.Extent.Height - presenter.Viewport.Height);
        presenter.Offset = new Vector(0, maxOffset);
        target.UpdateLayout();
        
        // Then fast scroll back to top
        presenter.Offset = new Vector(0, 0);
        target.UpdateLayout();
        
        var rows = GetRows(target);
        
        // Assert - all visible rows should have sequential indices starting from 0
        var orderedIndices = rows.OrderBy(r => r.Index).Select(r => r.Index).ToList();
        Assert.Equal(0, orderedIndices.First());
        
        // Verify no gaps in indices (no ghost rows from the bottom)
        for (int i = 1; i < orderedIndices.Count; i++)
        {
            Assert.Equal(orderedIndices[i - 1] + 1, orderedIndices[i]);
        }
        
        // Verify all rows are properly positioned without overlap
        var orderedByPosition = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedByPosition.Count; i++)
        {
            var prevRow = orderedByPosition[i - 1];
            var currentRow = orderedByPosition[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"Row {currentRow.Index} overlaps with row {prevRow.Index}");
        }
    }

    [AvaloniaFact]
    public void Rows_Are_Hidden_When_Scrolled_Out_Of_View()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Get initial rows
        var initialRows = GetRows(target);
        var initialMaxIndex = initialRows.Max(r => r.Index);
        
        // Act - scroll down significantly
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all DataGridRow controls (including hidden ones)
        var allRowControls = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        
        // Filter to only visible rows
        var visibleRows = allRowControls.Where(r => r.IsVisible).ToList();
        
        // Assert - visible rows should only be around index 50
        var minVisibleIndex = visibleRows.Min(r => r.Index);
        var maxVisibleIndex = visibleRows.Max(r => r.Index);
        
        // The old rows (0 to initialMaxIndex) should not be visible
        Assert.True(minVisibleIndex > initialMaxIndex,
            $"After scrolling, minimum visible index ({minVisibleIndex}) should be greater than initial max ({initialMaxIndex})");
    }

    [AvaloniaFact]
    public void ScrollIntoView_Realizes_Target_Row()
    {
        // Arrange
        var items = Enumerable.Range(0, 300).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140);
        target.UpdateLayout();
        var targetItem = items[200];

        // Sanity - initial viewport starts at 0
        Assert.True(GetFirstVisibleRowIndex(target) <= 1);
        Assert.Null(FindRowForItem(target, targetItem));

        // Act
        target.ScrollIntoView(targetItem, target.ColumnDefinitions[0]);
        target.UpdateLayout();

        // Assert
        var row = FindRowForItem(target, targetItem);
        Assert.NotNull(row);
        Assert.Equal(200, row!.Index);
        Assert.True(GetFirstVisibleRowIndex(target) >= 180);
    }

    [AvaloniaFact]
    public void AutoScrollToSelectedItem_Scrolls_On_Selection()
    {
        // Arrange
        var items = Enumerable.Range(0, 300).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140);
        target.AutoScrollToSelectedItem = true;
        target.UpdateLayout();
        var targetItem = items[220];

        // Act
        target.SelectedItem = targetItem;
        Dispatcher.UIThread.RunJobs();
        target.UpdateLayout();

        // Assert
        var row = FindRowForItem(target, targetItem);
        Assert.NotNull(row);
        Assert.Equal(220, row!.Index);
        Assert.True(GetFirstVisibleRowIndex(target) >= 200);
    }

    [AvaloniaFact]
    public void AutoScrollToSelectedItem_Off_Does_Not_Scroll_On_Selection()
    {
        // Arrange
        var items = Enumerable.Range(0, 300).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140);
        target.AutoScrollToSelectedItem = false;
        target.UpdateLayout();
        var targetItem = items[220];

        // Act
        target.SelectedItem = targetItem;
        Dispatcher.UIThread.RunJobs();
        target.UpdateLayout();

        // Assert - selection alone should not scroll without opt-in
        Assert.True(GetFirstVisibleRowIndex(target) < 50);
        Assert.Null(FindRowForItem(target, targetItem));
    }

    [AvaloniaFact]
    public void Keyboard_Navigation_Scrolls_When_AutoScroll_Disabled()
    {
        // Arrange
        var items = Enumerable.Range(0, 300).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140, useLogicalScrollable: true);
        target.AutoScrollToSelectedItem = false;
        target.UpdateLayout();

        var initialFirstVisible = GetFirstVisibleRowIndex(target);
        Assert.True(target.DisplayData.LastScrollingSlot >= 0, $"Expected rows to be realized. LastScrollingSlot: {target.DisplayData.LastScrollingSlot}");

        // Act - move down beyond the viewport
        for (var i = 0; i < 40; i++)
        {
            PressKey(target, Key.Down);
        }
        Dispatcher.UIThread.RunJobs();
        target.UpdateLayout();

        // Assert - viewport should advance with keyboard navigation
        Assert.True(target.SelectedIndex > 0, $"Expected selection to move. SelectedIndex: {target.SelectedIndex}");
        Assert.True(target.CurrentSlot > 0, $"Expected current slot to move. CurrentSlot: {target.CurrentSlot}");
        var firstVisibleAfter = GetFirstVisibleRowIndex(target);
        Assert.True(firstVisibleAfter > initialFirstVisible,
            $"Expected viewport to advance. Before: {initialFirstVisible}, After: {firstVisibleAfter}. " +
            $"SelectedIndex: {target.SelectedIndex}, CurrentSlot: {target.CurrentSlot}");
    }

    [AvaloniaFact]
    public void AutoScrollToSelectedItem_Does_Not_Fight_User_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 400).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140, useLogicalScrollable: true);
        target.AutoScrollToSelectedItem = true;
        target.UpdateLayout();

        // Auto-scroll to a distant item
        target.SelectedItem = items[250];
        Dispatcher.UIThread.RunJobs();
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var offsetAfterAutoScroll = presenter.Offset.Y;
        var firstAfterAutoScroll = GetFirstVisibleRowIndex(target);

        Assert.True(offsetAfterAutoScroll > 0);
        Assert.True(firstAfterAutoScroll > 150);

        // Simulate user wheel scroll upward by adjusting offset
        presenter.Offset = new Vector(presenter.Offset.X, Math.Max(0, presenter.Offset.Y - 60));
        target.UpdateLayout();

        var offsetAfterUser = presenter.Offset.Y;
        var firstAfterUser = GetFirstVisibleRowIndex(target);

        // Assert user scroll applies and does not get reset by auto-scroll logic
        Assert.True(offsetAfterUser < offsetAfterAutoScroll, $"Expected offset to decrease after user scroll. Before: {offsetAfterAutoScroll}, After: {offsetAfterUser}");
        Assert.True(firstAfterUser < firstAfterAutoScroll, $"Expected first visible row to move upward after user scroll. Before: {firstAfterAutoScroll}, After: {firstAfterUser}");
        Assert.True(firstAfterUser > 0);
    }

    [AvaloniaFact]
    public void AutoScrollToSelectedItem_Updates_ScrollBars()
    {
        // Arrange
        var items = Enumerable.Range(0, 400).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items, height: 140, useLogicalScrollable: true);
        target.AutoScrollToSelectedItem = true;
        target.UpdateLayout();
        var presenter = GetRowsPresenter(target);
        var verticalBar = target.GetSelfAndVisualDescendants().OfType<ScrollBar>().FirstOrDefault(sb => sb.Orientation == Orientation.Vertical);
        var scrollViewer = target.GetSelfAndVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        // Sanity
        Assert.NotNull(verticalBar);
        var initialOffset = presenter.Offset.Y;
        var initialBarValue = verticalBar!.Value;
        var initialViewerOffset = scrollViewer?.Offset.Y ?? 0;

        // Act
        target.SelectedItem = items[300];
        Dispatcher.UIThread.RunJobs();
        target.UpdateLayout();

        var offsetAfter = presenter.Offset.Y;
        var barValueAfter = verticalBar.Value;
        var viewerOffsetAfter = scrollViewer?.Offset.Y ?? 0;

        // Assert - both logical offset and scrollbar value should advance
        Assert.True(offsetAfter > initialOffset, $"Expected presenter offset to increase. Before: {initialOffset}, After: {offsetAfter}");
        if (verticalBar.Maximum > 0)
        {
            Assert.True(barValueAfter > initialBarValue, $"Expected scrollbar value to increase. Before: {initialBarValue}, After: {barValueAfter}, Max: {verticalBar.Maximum}");
        }

        if (scrollViewer != null)
        {
            Assert.True(viewerOffsetAfter > initialViewerOffset, $"Expected ScrollViewer offset to increase. Before: {initialViewerOffset}, After: {viewerOffsetAfter}");
        }
    }

    [AvaloniaFact]
    public void LogicalScrollable_Recycled_Row_Handles_New_Template_Column()
    {
        // Arrange
        var items = Enumerable.Range(0, 200).Select(x => new ScrollTestModel($"Item {x}")).ToList();

        var root = new Window
        {
            Width = 300,
            Height = 140,
        };

        root.SetThemeStyles();

        var initialTemplate = new DataGridTemplateColumn
        {
            Header = "Template",
            CellTemplate = new FuncDataTemplate<ScrollTestModel>((item, _) => new TextBlock { Text = item.Name }),
        };

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });
        target.ColumnsInternal.Add(initialTemplate);

        root.Content = target;
        root.Show();
        target.UpdateLayout();

        // Scroll far enough to recycle the initial rows
        target.ScrollIntoView(items[^1], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        Assert.True(GetFirstVisibleRowIndex(target) > 0);

        // Act - add another template column while rows are recycled, then scroll back
        target.ColumnsInternal.Add(new DataGridTemplateColumn
        {
            Header = "Extra",
            CellTemplate = new FuncDataTemplate<ScrollTestModel>((item, _) => new TextBlock { Text = item.Name }),
        });

        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();

        // Assert - rows are realized without throwing during recycling
        var rows = GetRows(target);
        Assert.NotEmpty(rows);
    }

    #endregion

    #region Helper Methods

    private static DataGrid CreateTarget(IList<ScrollTestModel> items, int height = 100, bool useLogicalScrollable = false)
    {
        var root = new Window
        {
            Width = 300,
            Height = height,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = useLogicalScrollable,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        return target;
    }

    private class InfiniteMeasureHost : Decorator
    {
        public double HostHeight { get; set; } = 40;

        protected override Size MeasureOverride(Size availableSize)
        {
            Child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(availableSize.Width, HostHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = new Size(finalSize.Width, HostHeight);
            Child?.Arrange(new Rect(size));
            return size;
        }
    }

    private class MismatchedMeasureHost : Decorator
    {
        public double MeasureHeight { get; set; } = 300;
        public double ArrangeHeight { get; set; } = 40;

        protected override Size MeasureOverride(Size availableSize)
        {
            var measureSize = new Size(availableSize.Width, MeasureHeight);
            Child?.Measure(measureSize);
            return measureSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var arrangeSize = new Size(finalSize.Width, ArrangeHeight);
            Child?.Arrange(new Rect(arrangeSize));
            return arrangeSize;
        }
    }

    private sealed class StateTrackingRowHeightEstimator : IDataGridRowHeightEstimator, IDataGridRowHeightEstimatorStateful
    {
        private const double DefaultHeight = 22.0;
        private readonly Dictionary<int, double> _measuredHeights = new();
        private int _totalItemCount;
        private double _rowHeightEstimate = DefaultHeight;
        private double _rowDetailsHeightEstimate;

        public int StateToken { get; set; }
        public int CaptureCount { get; private set; }
        public int RestoreCount { get; private set; }

        public double DefaultRowHeight { get; set; } = DefaultHeight;

        public double RowHeightEstimate => _rowHeightEstimate;

        public double RowDetailsHeightEstimate => _rowDetailsHeightEstimate;

        public double GetRowGroupHeaderHeightEstimate(int level) => DefaultHeight;

        public void RecordMeasuredHeight(int slot, double measuredHeight, bool hasDetails = false, double detailsHeight = 0)
        {
            _measuredHeights[slot] = measuredHeight;
            _rowHeightEstimate = measuredHeight;
            if (hasDetails && detailsHeight > 0)
            {
                _rowDetailsHeightEstimate = detailsHeight;
            }
        }

        public void RecordRowGroupHeaderHeight(int slot, int level, double measuredHeight)
        {
        }

        public double GetEstimatedHeight(int slot, bool isRowGroupHeader = false, int rowGroupLevel = 0, bool hasDetails = false)
        {
            if (_measuredHeights.TryGetValue(slot, out var measured))
            {
                return measured + (hasDetails ? _rowDetailsHeightEstimate : 0);
            }

            return _rowHeightEstimate + (hasDetails ? _rowDetailsHeightEstimate : 0);
        }

        public double CalculateTotalHeight(int totalSlotCount, int collapsedSlotCount, int[] rowGroupHeaderCounts, int detailsVisibleCount)
        {
            if (totalSlotCount <= 0)
            {
                return 0;
            }

            int visibleSlotCount = totalSlotCount - collapsedSlotCount;
            return visibleSlotCount * _rowHeightEstimate + detailsVisibleCount * _rowDetailsHeightEstimate;
        }

        public int EstimateSlotAtOffset(double verticalOffset, int totalSlotCount)
        {
            if (totalSlotCount <= 0 || _rowHeightEstimate <= 0)
            {
                return 0;
            }

            int slot = (int)(verticalOffset / _rowHeightEstimate);
            return Math.Min(Math.Max(0, slot), totalSlotCount - 1);
        }

        public double EstimateOffsetToSlot(int slot)
        {
            if (slot <= 0)
            {
                return 0;
            }

            return slot * _rowHeightEstimate;
        }

        public void UpdateFromDisplayedRows(int firstDisplayedSlot, int lastDisplayedSlot, double[] displayedHeights, double verticalOffset, double negVerticalOffset, int collapsedSlotCount, int detailsCount)
        {
            if (displayedHeights == null || displayedHeights.Length == 0)
            {
                return;
            }

            double total = 0;
            for (int i = 0; i < displayedHeights.Length; i++)
            {
                total += displayedHeights[i];
            }

            _rowHeightEstimate = total / displayedHeights.Length;
        }

        public void Reset()
        {
            _measuredHeights.Clear();
            _rowHeightEstimate = DefaultRowHeight;
            _rowDetailsHeightEstimate = 0;
            _totalItemCount = 0;
            StateToken = 0;
        }

        public void OnDataSourceChanged(int newItemCount)
        {
            _totalItemCount = newItemCount;
        }

        public void OnItemsInserted(int startIndex, int count)
        {
            _totalItemCount += count;
        }

        public void OnItemsRemoved(int startIndex, int count)
        {
            _totalItemCount = Math.Max(0, _totalItemCount - count);
        }

        public RowHeightEstimatorDiagnostics GetDiagnostics()
        {
            return new RowHeightEstimatorDiagnostics
            {
                AlgorithmName = "Tracking",
                CurrentRowHeightEstimate = _rowHeightEstimate,
                CachedHeightCount = _measuredHeights.Count,
                TotalRowCount = _totalItemCount,
                EstimatedTotalHeight = _totalItemCount * _rowHeightEstimate,
                MinMeasuredHeight = _rowHeightEstimate,
                MaxMeasuredHeight = _rowHeightEstimate,
                AverageMeasuredHeight = _rowHeightEstimate,
                AdditionalInfo = $"StateToken: {StateToken}"
            };
        }

        public RowHeightEstimatorState CaptureState()
        {
            CaptureCount++;
            return new TrackingState(_rowHeightEstimate, _rowDetailsHeightEstimate, new Dictionary<int, double>(_measuredHeights), _totalItemCount, StateToken);
        }

        public bool TryRestoreState(RowHeightEstimatorState state)
        {
            if (state is not TrackingState snapshot)
            {
                return false;
            }

            RestoreCount++;
            _rowHeightEstimate = snapshot.RowHeightEstimate;
            _rowDetailsHeightEstimate = snapshot.RowDetailsHeightEstimate;
            _totalItemCount = snapshot.TotalItemCount;
            StateToken = snapshot.StateToken;

            _measuredHeights.Clear();
            foreach (var entry in snapshot.MeasuredHeights)
            {
                _measuredHeights[entry.Key] = entry.Value;
            }

            return true;
        }

        private sealed class TrackingState : RowHeightEstimatorState
        {
            public TrackingState(double rowHeightEstimate, double rowDetailsHeightEstimate, Dictionary<int, double> measuredHeights, int totalItemCount, int stateToken)
                : base(nameof(StateTrackingRowHeightEstimator))
            {
                RowHeightEstimate = rowHeightEstimate;
                RowDetailsHeightEstimate = rowDetailsHeightEstimate;
                MeasuredHeights = measuredHeights;
                TotalItemCount = totalItemCount;
                StateToken = stateToken;
            }

            public double RowHeightEstimate { get; }
            public double RowDetailsHeightEstimate { get; }
            public Dictionary<int, double> MeasuredHeights { get; }
            public int TotalItemCount { get; }
            public int StateToken { get; }
        }
    }

    private static int GetRecycledRowCount(DataGrid target)
    {
        var displayData = target.DisplayData;
        var field = typeof(DataGridDisplayData).GetField("_recycledRows", BindingFlags.NonPublic | BindingFlags.Instance);
        var recycledRows = (Stack<DataGridRow>)field!.GetValue(displayData)!;
        return recycledRows.Count;
    }

    private static IReadOnlyList<DataGridRow> GetRecycledRows(DataGrid target)
    {
        var displayData = target.DisplayData;
        var field = typeof(DataGridDisplayData).GetField("_recycledRows", BindingFlags.NonPublic | BindingFlags.Instance);
        var recycledRows = (Stack<DataGridRow>)field!.GetValue(displayData)!;
        return recycledRows.ToArray();
    }

    private static DataGrid CreateV2Target(IList<ScrollTestModel> items, int height = 300, bool useLogicalScrollable = true)
    {
        var root = new Window
        {
            Width = 300,
            Height = height,
        };

        root.SetThemeStyles();

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = useLogicalScrollable,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        root.Content = target;
        root.Show();
        return target;
    }

    private static DataGridRowsPresenter GetRowsPresenter(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowsPresenter>()
            .First();
    }

    private static IReadOnlyList<DataGridRow> GetRows(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
    }

    private static void InvokeHideRecycledElement(DataGrid target, Control element)
    {
        var method = typeof(DataGrid).GetMethod("HideRecycledElement", BindingFlags.Instance | BindingFlags.NonPublic);
        method!.Invoke(target, new object[] { element });
    }

    private static IReadOnlyList<DataGridRowGroupHeader> GetGroupHeaders(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToList();
    }

    private static int GetFirstVisibleRowIndex(DataGrid target)
    {
        return GetRows(target).Min(r => r.Index);
    }

    private static DataGridRow? FindRowForItem(DataGrid target, object item)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(r => ReferenceEquals(r.DataContext, item));
    }

    private static void PressKey(DataGrid target, Key key)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = key,
            KeyModifiers = KeyModifiers.None,
            Source = target,
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        var method = typeof(DataGrid).GetMethod(
            "DataGrid_KeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, new object[] { target, args });
    }


    private static DataGrid CreateGroupedTarget(IList<GroupableTestModel> items, int height = 200, bool useLogicalScrollable = false)
    {
        var root = new Window
        {
            Width = 300,
            Height = height,
        };

        root.SetThemeStyles();

        var collectionView = new DataGridCollectionView(items);
        collectionView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupableTestModel.Group)));

        var target = new DataGrid
        {
            ItemsSource = collectionView,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = useLogicalScrollable,
        };
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Group", Binding = new Binding("Group") });

        root.Content = target;
        root.Show();
        return target;
    }

    private static DataGrid CreateVariableHeightTarget(IList<VariableHeightModel> items, int height = 200)
    {
        var root = new Window
        {
            Width = 300,
            Height = height,
        };

        root.SetThemeStyles();

        var templateColumn = new DataGridTemplateColumn
        {
            Header = "Content",
            CellTemplate = new FuncDataTemplate<VariableHeightModel>((item, _) => new TextBlock
            {
                Text = item.Content,
                TextWrapping = TextWrapping.Wrap,
                Width = 120
            }),
        };

        var target = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
        };
        target.ColumnsInternal.Add(templateColumn);

        root.Content = target;
        root.Show();
        return target;
    }

    #endregion

    #region Ghost Row Tests

    [AvaloniaFact]
    public void Recycled_Rows_Are_Immediately_Hidden()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Get all row controls before scroll
        var allRowsBefore = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        
        // Act - scroll to bottom
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all row controls after scroll (including hidden recycled ones)
        var allRowsAfter = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        
        // Filter to only visible rows
        var visibleRowsAfter = allRowsAfter.Where(r => r.IsVisible).ToList();
        
        // Assert - visible rows should only be near the end
        var minVisibleIndex = visibleRowsAfter.Min(r => r.Index);
        Assert.True(minVisibleIndex > 50, 
            $"After scrolling to bottom, minimum visible index ({minVisibleIndex}) should be > 50");
        
        // Assert - all other rows should be hidden
        var hiddenRows = allRowsAfter.Where(r => !r.IsVisible).ToList();
        foreach (var row in hiddenRows)
        {
            Assert.False(row.IsVisible, $"Recycled row {row.Index} should be hidden");
        }
    }

    [AvaloniaFact]
    public void No_Ghost_Rows_After_Fast_Scroll_To_Bottom_And_Back()
    {
        // Arrange
        var items = Enumerable.Range(0, 100).Select(x => new ScrollTestModel($"Item {x}")).ToList();
        var target = CreateTarget(items);
        
        // Act - fast scroll to bottom
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Then fast scroll back to top
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all visible rows
        var allRows = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        var visibleRows = allRows.Where(r => r.IsVisible).ToList();
        
        // Assert - visible rows should only be at the top (indices near 0)
        var maxVisibleIndex = visibleRows.Max(r => r.Index);
        Assert.True(maxVisibleIndex < 20, 
            $"After scrolling back to top, maximum visible index ({maxVisibleIndex}) should be < 20. Possible ghost rows from bottom.");
        
        // Assert - indices should be sequential (no ghost rows with high indices)
        var orderedIndices = visibleRows.OrderBy(r => r.Index).Select(r => r.Index).ToList();
        Assert.Equal(0, orderedIndices.First());
        for (int i = 1; i < orderedIndices.Count; i++)
        {
            Assert.Equal(orderedIndices[i - 1] + 1, orderedIndices[i]);
        }
    }

    #region Hit Test

    [AvaloniaFact]
    public void HitTest_Returns_Visible_Row_After_Scroll_With_Variable_Heights()
    {
        // Arrange - items with alternating short/long content to force varying row heights
        var items = Enumerable.Range(0, 120)
            .Select(i => new VariableHeightModel
            {
                Title = $"Item {i}",
                Content = (i % 2 == 0) ? "short" : string.Join(' ', Enumerable.Repeat("very long wrapped content", 6))
            })
            .ToList();

        var target = CreateVariableHeightTarget(items, height: 200);

        // Scroll somewhere into the list to exercise virtualization with variable heights
        target.ScrollIntoView(items[60], target.ColumnDefinitions[0]);
        target.UpdateLayout();

        // Act - pick a visible row and hit-test at its center
        var rows = GetRows(target).OrderBy(r => r.Bounds.Top).ToList();
        Assert.True(rows.Count >= 2);

        var expectedRow = rows[1];
        var root = (TopLevel)target.GetVisualRoot()!;
        var testPoint = expectedRow.TranslatePoint(
            new Point(expectedRow.Bounds.Width / 2, expectedRow.Bounds.Height / 2), root)!.Value;

        DataGridRow? containingRow = null;
        foreach (var row in rows)
        {
            var origin = row.TranslatePoint(new Point(0, 0), root)!.Value;
            var rectInRoot = new Rect(origin, row.Bounds.Size);
            if (rectInRoot.Contains(testPoint))
            {
                containingRow = row;
                break;
            }
        }

        // Assert
        Assert.Same(expectedRow, containingRow);
    }

    #endregion

    #endregion

    #region Grouping Scrolling Tests

    [AvaloniaFact]
    public void Grouped_DataGrid_Rows_Do_Not_Overlap()
    {
        // Arrange
        var items = Enumerable.Range(0, 50)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items);
        
        // Act - get visible elements
        var rows = GetRows(target);
        var groupHeaders = GetGroupHeaders(target);
        
        // Assert - rows should not overlap
        var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
        for (int i = 1; i < orderedRows.Count; i++)
        {
            var prevRow = orderedRows[i - 1];
            var currentRow = orderedRows[i];
            
            Assert.True(
                currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                $"Row at index {currentRow.Index} overlaps with row at index {prevRow.Index}");
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_No_Ghost_Rows_After_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items);
        
        // Act - scroll to bottom
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Then scroll back to top
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all visible elements
        var allRows = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        var visibleRows = allRows.Where(r => r.IsVisible).ToList();
        
        var allHeaders = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToList();
        var visibleHeaders = allHeaders.Where(h => h.IsVisible).ToList();
        
        // Assert - visible rows should be at the top
        if (visibleRows.Any())
        {
            var maxVisibleRowIndex = visibleRows.Max(r => r.Index);
            Assert.True(maxVisibleRowIndex < 30,
                $"After scrolling back to top, max visible row index ({maxVisibleRowIndex}) should be < 30");
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_Recycled_Group_Headers_Are_Hidden()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items);
        
        // Act - scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all group headers
        var allHeaders = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToList();
        
        var visibleHeaders = allHeaders.Where(h => h.IsVisible).ToList();
        var hiddenHeaders = allHeaders.Where(h => !h.IsVisible).ToList();
        
        // Assert - hidden headers should not be visible
        foreach (var header in hiddenHeaders)
        {
            Assert.False(header.IsVisible, "Recycled group header should be hidden");
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_Fast_Scroll_No_Ghost_Group_Headers()
    {
        // Arrange - create data with many groups
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 5}"))
            .ToList(); // 20 groups
        var target = CreateGroupedTarget(items, height: 300);
        
        // Act - fast scroll to bottom
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Then fast scroll back to top
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all visible group headers
        var allHeaders = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToList();
        var visibleHeaders = allHeaders.Where(h => h.IsVisible).ToList();
        
        // Assert - visible headers should be for early groups only
        foreach (var header in visibleHeaders)
        {
            var groupInfo = header.RowGroupInfo;
            if (groupInfo != null)
            {
                // The visible headers should be for the first few groups
                Assert.True(header.Bounds.Top < target.Bounds.Height + 100,
                    $"Visible group header at slot {groupInfo.Slot} is positioned too far down (possible ghost header)");
            }
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_Multiple_Scroll_Operations_No_Overlap()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 400);
        
        // Act - perform multiple scroll operations
        for (int scrollTo = 20; scrollTo <= 80; scrollTo += 20)
        {
            target.ScrollIntoView(items[scrollTo], target.ColumnDefinitions[0]);
            target.UpdateLayout();
            
            var rows = GetRows(target).Where(r => r.IsVisible).ToList();
            var headers = GetGroupHeaders(target).Where(h => h.IsVisible).ToList();
            
            // Assert - no overlap among visible rows
            var orderedRows = rows.OrderBy(r => r.Bounds.Top).ToList();
            for (int i = 1; i < orderedRows.Count; i++)
            {
                var prevRow = orderedRows[i - 1];
                var currentRow = orderedRows[i];
                
                Assert.True(
                    currentRow.Bounds.Top >= prevRow.Bounds.Bottom - 0.5,
                    $"At scroll position {scrollTo}: Row overlaps with previous row");
            }
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_All_Offscreen_Rows_Are_Hidden()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 300);
        
        // Act - scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all row elements (including hidden ones)
        var allRows = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        
        // Check each row - if it's visible, it should be within viewport bounds
        var viewportHeight = target.Bounds.Height;
        foreach (var row in allRows)
        {
            if (row.IsVisible)
            {
                // Visible row should be at least partially within viewport
                Assert.True(
                    row.Bounds.Bottom >= 0 && row.Bounds.Top < viewportHeight + 50,
                    $"Visible row {row.Index} at position {row.Bounds.Top} is outside viewport (0 to {viewportHeight})");
            }
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_Scroll_Down_Then_Up_All_Old_Rows_Hidden()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 300);
        
        // Act - scroll to bottom
        target.ScrollIntoView(items[99], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get rows visible at bottom
        var rowsAtBottom = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(r => r.IsVisible)
            .Select(r => r.Index)
            .ToHashSet();
        
        // Scroll back to top
        target.ScrollIntoView(items[0], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get all rows and their visibility
        var allRows = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();
        
        // Rows that were visible at bottom should NOT be visible at top (unless they're reused)
        var visibleRowsAtTop = allRows.Where(r => r.IsVisible).ToList();
        foreach (var row in visibleRowsAtTop)
        {
            // Visible rows at top should have low indices
            Assert.True(row.Index < 20,
                $"Row with index {row.Index} is visible at top position but should be hidden (was visible at bottom)");
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_DisplayData_Consistency_After_Scroll()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 300);
        
        // Act - scroll to middle
        target.ScrollIntoView(items[50], target.ColumnDefinitions[0]);
        target.UpdateLayout();
        
        // Get DisplayData info
        var displayData = target.DisplayData;
        var firstSlot = displayData.FirstScrollingSlot;
        var lastSlot = displayData.LastScrollingSlot;
        
        // Get all visible rows from visual tree
        var visibleRows = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(r => r.IsVisible)
            .ToList();
        
        var visibleHeaders = target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .Where(h => h.IsVisible)
            .ToList();
        
        // All visible elements should have slots within the DisplayData range
        foreach (var row in visibleRows)
        {
            Assert.True(row.Slot >= firstSlot && row.Slot <= lastSlot,
                $"Visible row slot {row.Slot} is outside DisplayData range [{firstSlot}, {lastSlot}]");
        }
        
        foreach (var header in visibleHeaders)
        {
            var slot = header.RowGroupInfo?.Slot ?? -1;
            Assert.True(slot >= firstSlot && slot <= lastSlot,
                $"Visible group header slot {slot} is outside DisplayData range [{firstSlot}, {lastSlot}]");
        }
    }

    [AvaloniaFact]
    public void Grouped_DataGrid_No_Duplicate_Visible_Slots()
    {
        // Arrange
        var items = Enumerable.Range(0, 100)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 400);
        
        // Act - scroll multiple times
        int[] scrollPositions = { 0, 30, 60, 90, 45, 15, 75, 0 };
        foreach (var pos in scrollPositions)
        {
            target.ScrollIntoView(items[pos], target.ColumnDefinitions[0]);
            target.UpdateLayout();
            
            // Get all visible rows
            var visibleRows = target.GetSelfAndVisualDescendants()
                .OfType<DataGridRow>()
                .Where(r => r.IsVisible)
                .ToList();
            
            // Check for duplicate slots
            var slots = visibleRows.Select(r => r.Slot).ToList();
            var duplicateSlots = slots.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            
            Assert.Empty(duplicateSlots);
        }
    }

    [AvaloniaFact]
    public void Collapsed_Group_Does_Not_Cause_Scroll_Jump_With_Logical_Scrollable()
    {
        // Arrange
        var items = Enumerable.Range(0, 60)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 300, useLogicalScrollable: true);
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var headers = GetGroupHeaders(target)
            .OrderBy(h => h.RowGroupInfo?.Slot ?? int.MaxValue)
            .ToList();

        Assert.True(headers.Count >= 2, "Expected at least two group headers to exercise scrolling across groups.");

        // Collapse the first group so that its rows are hidden but its header stays visible.
        headers[0].ToggleExpandCollapse(isVisible: false, setCurrent: true);
        target.UpdateLayout();

        var offsets = new List<double> { presenter.Offset.Y };

        // Scroll in small increments until we move past the collapsed group's content.
        for (int i = 0; i < 3; i++)
        {
            Assert.True(target.UpdateScroll(new Vector(0, -30)), "Scroll request should be handled.");
            target.UpdateLayout();
            offsets.Add(presenter.Offset.Y);
        }

        // We should end up with the next group's header as the first visible slot.
        Assert.True(target.DisplayData.FirstScrollingSlot >= headers[1].RowGroupInfo!.Slot,
            "Expected to reach the second group after scrolling.");

        var deltas = offsets.Zip(offsets.Skip(1), (previous, current) => current - previous).ToList();

        // Offsets should increase smoothly without a large jump when collapsed rows are skipped.
        Assert.All(deltas, delta => Assert.True(delta >= 0, $"Offset decreased unexpectedly by {delta}"));

        var maxAllowedDelta = target.RowHeightEstimate * 3;
        var maxDelta = deltas.Max();
        Assert.True(maxDelta < maxAllowedDelta,
            $"Scroll jump detected when collapsed groups are in view. Max delta {maxDelta} exceeded threshold {maxAllowedDelta}.");
    }

    [AvaloniaFact]
    public void Collapsed_Group_Offset_Reflects_Only_Visible_Content()
    {
        // Arrange
        var items = Enumerable.Range(0, 60)
            .Select(x => new GroupableTestModel($"Item {x}", $"Group {x / 10}"))
            .ToList();
        var target = CreateGroupedTarget(items, height: 300, useLogicalScrollable: true);
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var headers = GetGroupHeaders(target)
            .OrderBy(h => h.RowGroupInfo?.Slot ?? int.MaxValue)
            .ToList();

        Assert.True(headers.Count >= 2, "Expected at least two group headers to test offset calculation.");

        var firstHeader = headers[0];
        var secondHeader = headers[1];

        firstHeader.ToggleExpandCollapse(isVisible: false, setCurrent: true);
        target.UpdateLayout();

        // Scroll just past the first header so that the second header becomes the first visible slot.
        var scrollAmount = -(firstHeader.Bounds.Height + 10);
        Assert.True(target.UpdateScroll(new Vector(0, scrollAmount)), "Scroll request should be handled.");
        target.UpdateLayout();

        Assert.Equal(secondHeader.RowGroupInfo!.Slot, target.DisplayData.FirstScrollingSlot);

        // The vertical offset should only account for the visible header above plus the partial scroll into the next header.
        var offset = presenter.Offset.Y;
        var lowerBound = Math.Max(0, firstHeader.Bounds.Height - 1);
        var upperBound = firstHeader.Bounds.Height + secondHeader.Bounds.Height + target.RowHeightEstimate;

        Assert.InRange(offset, lowerBound, upperBound);
    }

    #endregion

    #region Test Model

    private class VariableHeightModel
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class ScrollTestModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;

        public ScrollTestModel(string name) => _name = name;

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
    }

    private class GroupableTestModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _name;
        private string _group;

        public GroupableTestModel(string name, string group)
        {
            _name = name;
            _group = group;
        }

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

        public string Group
        {
            get => _group;
            set
            {
                if (_group != value)
                {
                    _group = value;
                    RaisePropertyChanged();
                }
            }
        }
    }

    #endregion
}
