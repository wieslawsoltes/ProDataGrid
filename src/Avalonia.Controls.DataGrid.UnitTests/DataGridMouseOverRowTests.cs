// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridMouseOverRowTests
{
    [AvaloniaFact]
    public void MouseOverRowIndex_Does_Not_Throw_When_Displayed_Element_Is_Not_Row()
    {
        var grid = new DataGrid();

        grid.DisplayData.LoadScrollingSlot(0, new DataGridRowGroupHeader(), updateSlotInformation: true);

        var exception = Record.Exception(() => grid.MouseOverRowIndex = 0);

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void MouseOverRowIndex_Reevaluates_When_Rows_Shift()
    {
        var root1 = new TreeItem("Root 1", new[]
        {
            new TreeItem("Child 1"),
            new TreeItem("Child 2")
        });
        var root2 = new TreeItem("Root 2");

        var roots = new ObservableCollection<TreeItem> { root1, root2 };
        var model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
        {
            ChildrenSelector = item => item.Children,
            AutoExpandRoot = false,
            VirtualizeChildren = false
        });

        model.SetRoots(roots);

        var (grid, root) = CreateGrid(model);
        try
        {
            grid.UpdateLayout();

            var row = FindRow(root2, grid);
            var point = row.TranslatePoint(new Point(5, 5), grid)!.Value;

            root.SetPointerOverElementForTests(grid);
            RaisePointerActivity(grid, point);
            grid.MouseOverRowIndex = row.Index;

            Assert.True(((IPseudoClasses)row.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, row);

            model.Expand(model.Flattened[0]);
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var expectedRow = FindRowAtPoint(grid, point);
            Assert.NotNull(expectedRow);
            Assert.Equal(expectedRow!.Index, grid.MouseOverRowIndex);
            Assert.True(((IPseudoClasses)expectedRow.Classes).Contains(":pointerover"));
            Assert.False(((IPseudoClasses)row.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, expectedRow);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void MouseOverRowIndex_Reevaluates_When_RowInserted_Above_With_Same_Index()
    {
        var child = new TreeItem("Child 1");
        var root1 = new TreeItem("Root 1", new[] { child });
        var root2 = new TreeItem("Root 2");

        var roots = new ObservableCollection<TreeItem> { root1, root2 };
        var model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
        {
            ChildrenSelector = item => item.Children,
            AutoExpandRoot = false,
            VirtualizeChildren = false
        });

        model.SetRoots(roots);

        var (grid, root) = CreateGrid(model);
        try
        {
            grid.UpdateLayout();

            var root2Row = FindRow(root2, grid);
            var point = root2Row.TranslatePoint(new Point(5, 5), grid)!.Value;
            var initialIndex = root2Row.Index;

            root.SetPointerOverElementForTests(grid);
            RaisePointerActivity(grid, point);
            grid.MouseOverRowIndex = root2Row.Index;

            Assert.True(((IPseudoClasses)root2Row.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, root2Row);

            model.Expand(model.Flattened[0]);
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var rowAtPoint = FindRowAtPoint(grid, point);
            Assert.NotNull(rowAtPoint);
            Assert.Equal(initialIndex, rowAtPoint!.Index);
            Assert.Same(child, GetRowItem(rowAtPoint));
            Assert.Equal(initialIndex, grid.MouseOverRowIndex);
            Assert.True(((IPseudoClasses)rowAtPoint.Classes).Contains(":pointerover"));

            var root2RowAfter = FindRow(root2, grid);
            Assert.False(((IPseudoClasses)root2RowAfter.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, rowAtPoint);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void MouseOverRowIndex_Reevaluates_When_RowRemoved_Above_With_Same_Index()
    {
        var child = new TreeItem("Child 1");
        var root1 = new TreeItem("Root 1", new[] { child });
        var root2 = new TreeItem("Root 2");

        var roots = new ObservableCollection<TreeItem> { root1, root2 };
        var model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
        {
            ChildrenSelector = item => item.Children,
            AutoExpandRoot = false,
            VirtualizeChildren = false
        });

        model.SetRoots(roots);
        model.Expand(model.Flattened[0]);

        var (grid, root) = CreateGrid(model);
        try
        {
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var childRow = FindRow(child, grid);
            var point = childRow.TranslatePoint(new Point(5, 5), grid)!.Value;
            var initialIndex = childRow.Index;

            root.SetPointerOverElementForTests(grid);
            RaisePointerActivity(grid, point);
            grid.MouseOverRowIndex = childRow.Index;

            Assert.True(((IPseudoClasses)childRow.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, childRow);

            model.Collapse(model.Flattened[0]);
            PumpLayout(grid);

            var rowAtPoint = FindRow(root2, grid);
            Assert.Equal(initialIndex, rowAtPoint!.Index);
            Assert.Same(root2, GetRowItem(rowAtPoint));
            Assert.Equal(initialIndex, grid.MouseOverRowIndex);
            Assert.True(((IPseudoClasses)rowAtPoint.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, rowAtPoint);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void MouseOverRowIndex_Reevaluates_When_Recycling_Rows_On_Expand()
    {
        var children = Enumerable.Range(1, 8).Select(i => new TreeItem($"Child {i}")).ToList();
        var root1 = new TreeItem("Root 1", children);
        var root2 = new TreeItem("Root 2");
        var root3 = new TreeItem("Root 3");

        var roots = new ObservableCollection<TreeItem> { root1, root2, root3 };
        var model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
        {
            ChildrenSelector = item => item.Children,
            AutoExpandRoot = false,
            VirtualizeChildren = true
        });

        model.SetRoots(roots);

        var (grid, root) = CreateGrid(
            model,
            height: 84,
            hideHeaders: true,
            recycledHidingMode: DataGridRecycleHidingMode.SetIsVisibleOnly,
            rowHeight: 24);
        try
        {
            grid.UpdateLayout();

            var root3Row = FindRow(root3, grid);
            var point = root3Row.TranslatePoint(new Point(5, 5), grid)!.Value;

            root.SetPointerOverElementForTests(grid);
            RaisePointerActivity(grid, point);
            grid.MouseOverRowIndex = root3Row.Index;

            Assert.True(((IPseudoClasses)root3Row.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, root3Row);

            model.Expand(model.Flattened[0]);
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var rowAtPoint = FindRowAtPoint(grid, point);
            Assert.NotNull(rowAtPoint);
            Assert.Equal(rowAtPoint!.Index, grid.MouseOverRowIndex);
            Assert.True(((IPseudoClasses)rowAtPoint.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, rowAtPoint);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void MouseOverRowIndex_Clears_When_Pointer_Moves_From_Row_To_GroupHeader()
    {
        var item1 = new GroupedItem("Alpha", "G1");
        var item2 = new GroupedItem("Beta", "G1");
        var item3 = new GroupedItem("Gamma", "G2");
        var item4 = new GroupedItem("Delta", "G2");
        var items = new ObservableCollection<GroupedItem> { item1, item2, item3, item4 };

        var (grid, root) = CreateGroupedGrid(items);
        try
        {
            PumpLayout(grid);

            var row = FindRow(item4, grid);
            var header = grid.GetVisualDescendants()
                .OfType<DataGridRowGroupHeader>()
                .First(candidate =>
                    candidate.IsVisible &&
                    Equals(candidate.RowGroupInfo?.CollectionViewGroup?.Key, item4.Group));

            var rowPoint = row.TranslatePoint(new Point(5, 5), grid);
            Assert.NotNull(rowPoint);

            var headerPoint = header.TranslatePoint(new Point(5, Math.Max(1, header.Bounds.Height / 2)), grid);
            Assert.NotNull(headerPoint);

            root.SetPointerOverElementForTests(row);
            RaisePointerMovedActivity(grid, rowPoint.Value);
            grid.MouseOverRowIndex = row.Index;

            Assert.Equal(row.Index, grid.MouseOverRowIndex);
            Assert.True(((IPseudoClasses)row.Classes).Contains(":pointerover"));
            AssertSinglePointerOverRow(grid, row);

            root.SetPointerOverElementForTests(header);
            RaisePointerMovedActivity(grid, headerPoint.Value);
            RefreshPointerOverRow(grid);

            Assert.Null(grid.MouseOverRowIndex);
            Assert.False(((IPseudoClasses)row.Classes).Contains(":pointerover"));
            Assert.Empty(GetPointerOverRows(grid));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGrid(
        IHierarchicalModel model,
        double width = 360,
        double height = 240,
        bool hideHeaders = false,
        DataGridRecycleHidingMode? recycledHidingMode = null,
        double? rowHeight = null)
    {
        var root = new Window
        {
            Width = width,
            Height = height
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        if (hideHeaders)
        {
            grid.HeadersVisibility = DataGridHeadersVisibility.None;
        }

        if (recycledHidingMode.HasValue)
        {
            grid.RecycledContainerHidingMode = recycledHidingMode.Value;
        }

        if (rowHeight.HasValue)
        {
            grid.RowHeight = rowHeight.Value;
        }

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        root.Content = grid;
        root.Show();
        return (grid, root);
    }

    private static (DataGrid grid, Window root) CreateGroupedGrid(ObservableCollection<GroupedItem> items)
    {
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedItem.Group)));

        var root = new Window
        {
            Width = 420,
            Height = 280
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(GroupedItem.Name))
        });

        root.Content = grid;
        root.Show();
        return (grid, root);
    }

    private static DataGridRow FindRow(object item, DataGrid grid)
    {
        DataGridRow fallback = null;
        foreach (var row in grid.GetSelfAndVisualDescendants().OfType<DataGridRow>())
        {
            var matches = row.DataContext is HierarchicalNode node
                ? ReferenceEquals(node.Item, item)
                : ReferenceEquals(row.DataContext, item);

            if (!matches)
            {
                continue;
            }

            if (row.IsVisible)
            {
                return row;
            }

            fallback ??= row;
        }

        return fallback ?? throw new InvalidOperationException("Sequence contains no matching element");
    }

    private static DataGridRow? FindRowAtPoint(DataGrid grid, Point point)
    {
        var visual = grid.GetVisualAt(point);
        if (visual is Visual hit)
        {
            var row = hit.GetSelfAndVisualAncestors()
                .OfType<DataGridRow>()
                .FirstOrDefault(candidate => candidate.OwningGrid == grid && candidate.IsVisible);

            if (row != null)
            {
                var topLeft = row.TranslatePoint(new Point(0, 0), grid);
                if (topLeft != null)
                {
                    var bounds = new Rect(topLeft.Value, row.Bounds.Size);
                    if (bounds.Contains(point))
                    {
                        return row;
                    }
                }
            }
        }

        var presenter = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().FirstOrDefault();
        if (presenter != null)
        {
            var presenterPoint = grid.TranslatePoint(point, presenter) ?? point;
            foreach (var row in presenter.Children.OfType<DataGridRow>())
            {
                if (!row.IsVisible)
                {
                    continue;
                }

                if (row.Bounds.Contains(presenterPoint))
                {
                    return row;
                }
            }
        }

        return null;
    }

    private static DataGridRow[] GetPointerOverRows(DataGrid grid)
    {
        return grid.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => ((IPseudoClasses)row.Classes).Contains(":pointerover"))
            .ToArray();
    }

    private static void AssertSinglePointerOverRow(DataGrid grid, DataGridRow expected)
    {
        var hovered = GetPointerOverRows(grid);
        Assert.Single(hovered);
        Assert.Same(expected, hovered[0]);
    }

    private static object? GetRowItem(DataGridRow row)
    {
        if (row.DataContext is HierarchicalNode node)
        {
            return node.Item;
        }

        return row.DataContext;
    }

    private static void RaisePointerActivity(DataGrid grid, Point point)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(grid, pointer, grid, point, 0, properties, KeyModifiers.None);
        grid.RaiseEvent(args);
    }

    private static void RaisePointerMovedActivity(DataGrid grid, Point point)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, grid, pointer, grid, point, 0, properties, KeyModifiers.None);
        grid.RaiseEvent(args);
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        if (control.GetVisualRoot() is Window window)
        {
            window.ApplyTemplate();
            window.UpdateLayout();
        }
        control.ApplyTemplate();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void RefreshPointerOverRow(DataGrid grid)
    {
        var method = typeof(DataGrid).GetMethod(
            "RefreshPointerOverRow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DataGrid.RefreshPointerOverRow was not found.");

        method.Invoke(grid, null);
    }

    private sealed class TreeItem
    {
        public TreeItem(string name, IEnumerable<TreeItem>? children = null)
        {
            Name = name;
            Children = new ObservableCollection<TreeItem>(children ?? Enumerable.Empty<TreeItem>());
        }

        public string Name { get; }

        public ObservableCollection<TreeItem> Children { get; }
    }

    private sealed record GroupedItem(string Name, string Group);
}
