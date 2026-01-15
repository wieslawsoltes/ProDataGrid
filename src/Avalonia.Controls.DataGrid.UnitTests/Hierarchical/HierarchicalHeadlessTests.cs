// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class HierarchicalHeadlessTests
{
    private class Item
    {
        public Item(string name)
        {
            Name = name;
            Children = new ObservableCollection<Item>();
        }

        public string Name { get; set; }

        public ObservableCollection<Item> Children { get; }

        public double RowHeight { get; set; } = 24;
    }

    [AvaloniaFact]
    public void Header_Click_Toggles_Sort_And_Indicators()
    {
        RunSortScenario("Name");
    }

    [AvaloniaFact]
    public void Header_Click_Supports_Nested_SortMemberPath()
    {
        RunSortScenario("Item.Name");
    }

    [AvaloniaFact]
    public void Header_Click_Sorts_Templated_Columns_With_Item_Prefix()
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            CanUserSortColumns = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = model.ObservableFlattened
        };

        grid.ColumnsInternal.Add(new DataGridTemplateColumn
        {
            Header = "Name",
            SortMemberPath = "Item.Name",
            CellTemplate = new FuncDataTemplate<HierarchicalNode>((_, _) => new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Item.Name")
            })
        });

        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        ClickHeader(grid, "Name");
        grid.UpdateLayout();

        Assert.Equal(new[] { "a", "b" }, GetRowOrder(grid));

        window.Close();
    }

    [AvaloniaFact]
    public void Alt_SubtreeToggle_Expands_All_Nodes()
    {
        var root = new Item("root");
        var child = new Item("child");
        child.Children.Add(new Item("grand"));
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var toggled = (bool)toggleMethod!.Invoke(grid, new object[] { 0, true })!;

        Assert.True(toggled);
        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, model.Count);
    }

    [AvaloniaFact]
    public void NumpadMultiply_ExpandsEntireSubtree()
    {
        var root = new Item("root");
        var childA = new Item("a");
        childA.Children.Add(new Item("a1"));
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var setCurrent = typeof(DataGrid).GetMethod(
            "SetCurrentCellCore",
            BindingFlags.Instance | BindingFlags.NonPublic,
            new[] { typeof(int), typeof(int) });

        Assert.True((bool)setCurrent!.Invoke(grid, new object[] { 0, 0 })!);

        var processMultiply = typeof(DataGrid).GetMethod(
            "ProcessMultiplyKey",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var args = new KeyEventArgs
        {
            Key = Key.Multiply,
            RoutedEvent = InputElement.KeyDownEvent,
            Source = grid
        };

        Assert.True((bool)processMultiply!.Invoke(grid, new object[] { args })!);

        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(4, model.Count);
    }

    [AvaloniaFact]
    public void NodeIsExpanded_Updates_DataGrid_Rows()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var items = Assert.IsAssignableFrom<IReadOnlyList<HierarchicalNode>>(grid.ItemsSource);
        Assert.Equal(1, items.Count);

        Assert.False(model.Root!.IsExpanded);
        model.Root.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        Assert.True(model.Root.IsExpanded);
        Assert.Equal(2, model.Count);
        Assert.Equal(2, items.Count);
    }

    [AvaloniaFact]
    public void Expander_Click_Does_Not_Select_Row()
    {
        var items = new[] { new Item("root") };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            IsTabStop = false,
            HierarchicalRowsEnabled = true,
            ItemsSource = items
        };

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding(nameof(Item.Name))
        };
        grid.ColumnsInternal.Add(column);

        var row = new DataGridRow
        {
            OwningGrid = grid,
            Slot = 0,
            Index = 0,
            DataContext = items[0]
        };

        var presenter = new DataGridHierarchicalPresenter
        {
            Template = new FuncControlTemplate<DataGridHierarchicalPresenter>((owner, scope) =>
            {
                var expander = new ToggleButton
                {
                    Name = "PART_Expander",
                    Width = 16,
                    Height = 16
                };

                scope.Register(expander.Name, expander);

                return expander;
            })
        };

        var cell = new DataGridCell
        {
            OwningRow = row,
            OwningColumn = column,
            Content = presenter
        };
        row.Cells.Insert(0, cell);

        var window = new Window
        {
            Width = 200,
            Height = 100,
            Content = new StackPanel
            {
                Children =
                {
                    grid,
                    cell
                }
            }
        };

        window.SetThemeStyles();
        window.Show();
        PumpLayout(grid);

        cell.ApplyTemplate();
        presenter.ApplyTemplate();
        presenter.UpdateLayout();

        var expander = presenter.GetTemplateChildren()
            .OfType<ToggleButton>()
            .FirstOrDefault(control => control.Name == "PART_Expander");
        Assert.NotNull(expander);

        Assert.True(expander!.GetVisualAncestors().OfType<DataGridHierarchicalPresenter>().Any());

        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        grid.SelectedIndex = -1;
        cell.RaiseEvent(CreatePointerPressedArgs(cell, window, pointer, new Point(1, 1), KeyModifiers.None));
        Assert.Equal(0, grid.SelectedIndex);

        grid.SelectedIndex = -1;
        expander.RaiseEvent(CreatePointerPressedArgs(expander, window, pointer, new Point(1, 1), KeyModifiers.None));
        Assert.Equal(-1, grid.SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void Rapid_Toggle_Culls_And_Rebinds()
    {
        var root = new Item("root");
        var current = root;
        const int depth = 20;
        for (int i = 0; i < depth; i++)
        {
            var child = new Item($"n{i}");
            current.Children.Add(child);
            current = child;
        }

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            VirtualizeChildren = false // force cull via guard queue
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        for (int i = 0; i < 5; i++)
        {
            // Expand
            Assert.True((bool)toggleMethod!.Invoke(grid, new object[] { 0, true })!);
            grid.UpdateLayout();
            Assert.Equal(depth + 1, model.Count);
            ValidateDisplayedRows(grid, model);

            // Collapse
            Assert.True((bool)toggleMethod!.Invoke(grid, new object[] { 0, true })!);
            grid.UpdateLayout();
            Assert.Equal(1, model.Count);
            ValidateDisplayedRows(grid, model);
        }
    }

    [AvaloniaFact]
    public void LogicalScrollOffset_Remains_Aligned_After_Expand_And_Collapse()
    {
        var root = CreateTree("Root", childCount: 200, grandchildCount: 5);
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 0,
            VirtualizeChildren = true
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 420,
            Height = 260,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var presenter = GetRowsPresenter(grid);
        var scrollViewer = grid.ScrollViewer;
        Assert.NotNull(scrollViewer);
        scrollViewer!.Offset = new Vector(0, 247);
        PumpLayout(grid);

        var offsetBefore = presenter.Offset.Y;
        var anchorCandidate = GetVisibleRows(grid)
            .Select(row => new { Row = row, Node = row.DataContext as HierarchicalNode })
            .FirstOrDefault(candidate =>
                candidate.Node != null &&
                candidate.Node.Level > 0 &&
                candidate.Node.Item is Item item &&
                item.Children.Count > 0 &&
                !candidate.Node.IsExpanded);

        Assert.NotNull(anchorCandidate);

        var anchorRow = anchorCandidate!.Row;
        var anchorNode = anchorCandidate.Node!;
        var anchorY = anchorRow.Bounds.Y;
        Assert.True(offsetBefore > 0);

        model.Toggle(anchorNode);
        PumpLayout(grid);

        var anchorAfterExpand = FindVisibleRow(grid, anchorNode);
        Assert.NotNull(anchorAfterExpand);
        Assert.InRange(Math.Abs(anchorAfterExpand!.Bounds.Y - anchorY), 0, 0.5);
        Assert.InRange(Math.Abs(grid.GetVerticalOffset() - presenter.Offset.Y), 0, 0.01);
        Assert.InRange(Math.Abs(presenter.Offset.Y - offsetBefore), 0, 0.5);
        Assert.InRange(Math.Abs(scrollViewer.Offset.Y - presenter.Offset.Y), 0, 0.01);

        model.Toggle(anchorNode);
        PumpLayout(grid);

        var anchorAfterCollapse = FindVisibleRow(grid, anchorNode);
        Assert.NotNull(anchorAfterCollapse);
        Assert.InRange(Math.Abs(anchorAfterCollapse!.Bounds.Y - anchorY), 0, 0.5);
        Assert.InRange(Math.Abs(grid.GetVerticalOffset() - presenter.Offset.Y), 0, 0.01);
        Assert.InRange(Math.Abs(presenter.Offset.Y - offsetBefore), 0, 0.5);
        Assert.InRange(Math.Abs(scrollViewer.Offset.Y - presenter.Offset.Y), 0, 0.01);

        window.Close();
    }

    [AvaloniaFact]
    public void Expanding_Node_Above_Viewport_Preserves_Row_Position_And_ScrollOffset()
    {
        var roots = new ObservableCollection<Item>();
        for (int i = 0; i < 60; i++)
        {
            roots.Add(CreateTree($"Root {i + 1}", childCount: 6, grandchildCount: 0));
        }
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = false,
            VirtualizeChildren = true
        });
        model.SetRoots(roots);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 420,
            Height = 260,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var presenter = GetRowsPresenter(grid);
        var scrollViewer = grid.ScrollViewer;
        Assert.NotNull(scrollViewer);

        scrollViewer!.Offset = new Vector(0, 600);
        PumpLayout(grid);

        var firstSlot = grid.DisplayData.FirstScrollingSlot;
        Assert.True(firstSlot > 0);

        var anchorRow = Assert.IsType<DataGridRow>(grid.DisplayData.GetDisplayedElement(firstSlot));
        Assert.True(anchorRow.Index > 0);
        var anchorNode = Assert.IsType<HierarchicalNode>(anchorRow.DataContext);
        var anchorY = anchorRow.Bounds.Y;
        var offsetBefore = presenter.Offset.Y;
        var scrollOffsetBefore = scrollViewer.Offset.Y;

        var aboveNode = model.GetNode(anchorRow.Index - 1);
        Assert.NotNull(aboveNode);
        Assert.False(aboveNode.IsExpanded);
        Assert.True(((Item)aboveNode.Item).Children.Count > 0);

        model.Toggle(aboveNode);
        PumpLayout(grid);

        var anchorAfterExpand = FindVisibleRow(grid, anchorNode);
        Assert.NotNull(anchorAfterExpand);
        Assert.InRange(Math.Abs(anchorAfterExpand!.Bounds.Y - anchorY), 0, 0.5);
        Assert.InRange(Math.Abs(grid.GetVerticalOffset() - presenter.Offset.Y), 0, 0.01);
        Assert.InRange(Math.Abs(scrollViewer.Offset.Y - presenter.Offset.Y), 0, 0.01);
        Assert.True(presenter.Offset.Y > offsetBefore);
        Assert.True(scrollViewer.Offset.Y > scrollOffsetBefore);

        window.Close();
    }

    [AvaloniaFact]
    public void Expanding_Scrolled_Root_Preserves_Row_Position()
    {
        var roots = new ObservableCollection<Item>();
        for (int i = 0; i < 40; i++)
        {
            roots.Add(CreateTree($"Root {i + 1}", childCount: 5, grandchildCount: 0));
        }
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = false,
            VirtualizeChildren = true
        });
        model.SetRoots(roots);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 420,
            Height = 260,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.True((bool)toggleMethod!.Invoke(grid, new object[] { 0, false })!);
        PumpLayout(grid);

        var presenter = GetRowsPresenter(grid);
        var scrollViewer = grid.ScrollViewer;
        Assert.NotNull(scrollViewer);
        scrollViewer!.Offset = new Vector(0, 500);
        PumpLayout(grid);

        var anchorCandidate = GetVisibleRows(grid)
            .Select(row => new { Row = row, Node = row.DataContext as HierarchicalNode })
            .FirstOrDefault(candidate =>
                candidate.Node != null &&
                candidate.Node.Level == 0 &&
                !candidate.Node.IsExpanded);

        Assert.NotNull(anchorCandidate);

        var anchorRow = anchorCandidate!.Row;
        var anchorNode = anchorCandidate.Node!;
        var anchorY = anchorRow.Bounds.Y;

        var slotMethod = typeof(DataGrid).GetMethod(
            "SlotFromRowIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var anchorSlot = (int)slotMethod!.Invoke(grid, new object[] { anchorRow.Index })!;

        Assert.True((bool)toggleMethod.Invoke(grid, new object[] { anchorSlot, false })!);
        PumpLayout(grid);

        var anchorAfterExpand = FindVisibleRow(grid, anchorNode);
        Assert.NotNull(anchorAfterExpand);
        Assert.InRange(Math.Abs(anchorAfterExpand!.Bounds.Y - anchorY), 0, 0.5);
        Assert.InRange(Math.Abs(scrollViewer.Offset.Y - presenter.Offset.Y), 0, 0.01);

        window.Close();
    }

    [AvaloniaFact]
    public void Reparenting_Recycles_Rows_And_Reapplies_Indentation()
    {
        var roots = new ObservableCollection<Item>
        {
            CreateTree("RootA", childCount: 24, grandchildCount: 2),
            CreateTree("RootB", childCount: 24, grandchildCount: 2)
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            VirtualizeChildren = false
        });
        model.SetRoots(roots);
        model.ExpandAll();

        var window = new Window
        {
            Width = 420,
            Height = 220
        };

        window.SetThemeStyles();

        const double indent = 14;

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.None,
            RowHeight = 28
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name"),
            Indent = indent
        });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        var initialRows = GetVisibleRows(grid);
        var initialContexts = initialRows.ToDictionary(row => row, row => row.DataContext);

        var sourceChild = roots[0].Children[0];
        var movedItem = sourceChild.Children[0];

        sourceChild.Children.Remove(movedItem);
        roots[1].Children.Add(movedItem);

        model.ExpandAll();
        PumpLayout(grid);

        var lastNode = model.GetNode(model.Count - 1);
        grid.ScrollIntoView(lastNode, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        var recycled = GetVisibleRows(grid).Any(row =>
            initialContexts.TryGetValue(row, out var oldContext) &&
            oldContext != null &&
            !ReferenceEquals(oldContext, row.DataContext));

        Assert.True(recycled);

        var movedNode = model.FindNode(movedItem);
        Assert.NotNull(movedNode);

        grid.ScrollIntoView(movedNode!, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        AssertVisibleRowsHaveCorrectIndent(grid, indent);

        window.Close();
    }

    [AvaloniaFact]
    public void Templated_Hierarchical_Cell_Rebinds_When_Row_Recycled()
    {
        var root = CreateTree("Root", childCount: 120, grandchildCount: 0);
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
            VirtualizeChildren = true
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            CellTemplate = new FuncDataTemplate<HierarchicalNode>((_, _) => new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Item.Name")
            })
        });

        var window = new Window
        {
            Width = 420,
            Height = 220,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var initialRows = GetVisibleRows(grid);
        var initialContexts = initialRows.ToDictionary(row => row, row => row.DataContext);

        var lastNode = model.GetNode(model.Count - 1);
        grid.ScrollIntoView(lastNode, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        var scrolledRows = GetVisibleRows(grid);
        var recycledRows = scrolledRows
            .Where(row =>
                initialContexts.TryGetValue(row, out var oldContext) &&
                !ReferenceEquals(oldContext, row.DataContext))
            .ToList();

        Assert.NotEmpty(recycledRows);

        foreach (var row in recycledRows)
        {
            var node = Assert.IsType<HierarchicalNode>(row.DataContext);
            var presenter = GetHierarchicalPresenter(row);
            Assert.Same(node, presenter.Content);

            var textBlock = presenter.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            Assert.NotNull(textBlock);
            Assert.Equal(((Item)node.Item).Name, textBlock!.Text);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void Collapsing_Expanded_Node_After_Recycling_Does_Not_Leave_Gaps()
    {
        var root = CreateTree("Root", childCount: 180, grandchildCount: 4);
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
            VirtualizeChildren = true
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 420,
            Height = 240,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var initialRows = GetVisibleRows(grid);
        var initialContexts = initialRows.ToDictionary(row => row, row => row.DataContext);

        var scrollViewer = grid.ScrollViewer;
        Assert.NotNull(scrollViewer);
        scrollViewer!.Offset = new Vector(0, 600);
        PumpLayout(grid);

        var recycled = GetVisibleRows(grid).Any(row =>
            initialContexts.TryGetValue(row, out var oldContext) &&
            !ReferenceEquals(oldContext, row.DataContext));
        Assert.True(recycled);

        var node = FindExpandableNode(grid);
        grid.ScrollIntoView(node, grid.ColumnsInternal[0]);
        PumpLayout(grid);
        if (node.IsExpanded)
        {
            model.Collapse(node);
            PumpLayout(grid);
        }
        model.Expand(node);
        PumpLayout(grid);

        var lastGrandchild = ((Item)node.Item).Children.LastOrDefault();
        Assert.NotNull(lastGrandchild);
        var lastGrandchildNode = model.FindNode(lastGrandchild!);
        Assert.NotNull(lastGrandchildNode);

        grid.ScrollIntoView(lastGrandchildNode!, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        grid.ScrollIntoView(node, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        model.Collapse(node);
        PumpLayout(grid);

        ValidateDisplayedRows(grid, model);
        AssertVisibleRowsAreContiguous(grid);

        window.Close();
    }

    [AvaloniaFact]
    public void Collapsing_Templated_Node_With_Variable_Heights_Does_Not_Leave_Gaps()
    {
        var root = CreateTreeWithRowHeights("Root", childCount: 160, grandchildCount: 3);
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
            VirtualizeChildren = true
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            UseLogicalScrollable = true,
            RowHeight = 24
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            CellTemplate = new FuncDataTemplate<HierarchicalNode>((_, _) => new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Item.Name")
            })
        });

        grid.Styles.Add(new Style(x => x.OfType<DataGridRow>())
        {
            Setters =
            {
                new Setter(DataGridRow.HeightProperty, new Binding("Item.RowHeight"))
            }
        });

        var window = new Window
        {
            Width = 420,
            Height = 240,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        window.Show();
        PumpLayout(grid);

        var initialRows = GetVisibleRows(grid);
        var initialContexts = initialRows.ToDictionary(row => row, row => row.DataContext);

        var scrollViewer = grid.ScrollViewer;
        Assert.NotNull(scrollViewer);
        scrollViewer!.Offset = new Vector(0, 580);
        PumpLayout(grid);

        var recycled = GetVisibleRows(grid).Any(row =>
            initialContexts.TryGetValue(row, out var oldContext) &&
            !ReferenceEquals(oldContext, row.DataContext));
        Assert.True(recycled);

        var node = FindExpandableNode(grid);
        grid.ScrollIntoView(node, grid.ColumnsInternal[0]);
        PumpLayout(grid);
        if (node.IsExpanded)
        {
            model.Collapse(node);
            PumpLayout(grid);
        }
        model.Expand(node);
        PumpLayout(grid);

        var lastGrandchild = ((Item)node.Item).Children.LastOrDefault();
        Assert.NotNull(lastGrandchild);
        var lastGrandchildNode = model.FindNode(lastGrandchild!);
        Assert.NotNull(lastGrandchildNode);

        grid.ScrollIntoView(lastGrandchildNode!, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        grid.ScrollIntoView(node, grid.ColumnsInternal[0]);
        PumpLayout(grid);

        model.Collapse(node);
        PumpLayout(grid);

        ValidateDisplayedRows(grid, model);
        AssertVisibleRowsAreContiguous(grid);
        AssertVisibleRowsHaveExpectedText(grid);

        window.Close();
    }

    private static void RunSortScenario(string sortMemberPath)
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));

        var grid = CreateGrid(root, sortMemberPath);

        ClickHeader(grid, "Name");
        grid.UpdateLayout();
        var sorting = Assert.IsAssignableFrom<ISortingModel>(grid.SortingModel);
        Assert.NotEmpty(sorting.Descriptors);
        Assert.Equal(new[] { "a", "b" }, GetRowOrder(grid));
        AssertHeaderSort(grid, "Name", asc: true, desc: false);

        ClickHeader(grid, "Name");
        grid.UpdateLayout();
        Assert.Equal(new[] { "b", "a" }, GetRowOrder(grid));
        AssertHeaderSort(grid, "Name", asc: false, desc: true);
    }

    private static DataGrid CreateGrid(Item root, string sortMemberPath)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300,
        };

        window.SetThemeStyles();

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened);
        var sortingModel = new SortingModel();
        sortingModel.SortingChanged += (_, e) =>
        {
            if (e.NewDescriptors.Count == 0)
            {
                return;
            }

            var descriptor = e.NewDescriptors[0];
            var comparer = Comparer<object>.Create((x, y) =>
            {
                var a = x as Item;
                var b = y as Item;
                var result = string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);
                return descriptor.Direction == ListSortDirection.Descending ? -result : result;
            });

            model.ApplySiblingComparer(comparer, recursive: true);
            view.Refresh();
        };

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            CanUserSortColumns = true,
            AutoGenerateColumns = false
        };

        grid.SortingAdapterFactory = new HierarchicalSortingAdapterFactory();
        grid.ItemsSource = view;
        grid.SortingModel = sortingModel;
        EnsureSortingAdapter(grid);

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name"),
            SortMemberPath = sortMemberPath,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        window.Content = grid;
        
        window.Show();
        grid.UpdateLayout();
        return grid;
    }

    private static void ValidateDisplayedRows(DataGrid grid, HierarchicalModel model)
    {
        var display = grid.DisplayData;
        var seen = new HashSet<int>();

        foreach (Control element in display.GetScrollingElements())
        {
            if (element is DataGridRow row)
            {
                Assert.InRange(row.Index, 0, model.Count - 1);
                Assert.True(seen.Add(row.Index));

                var node = row.DataContext as HierarchicalNode;
                Assert.NotNull(node);
                Assert.Same(model.GetNode(row.Index), node);
            }
        }
    }

    private static void PumpLayout(DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        if (grid.GetVisualRoot() is Window window)
        {
            window.ApplyTemplate();
            window.UpdateLayout();
        }
        grid.ApplyTemplate();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static IDisposable UseApplicationTheme(DataGridTheme theme)
    {
        var styles = ThemeHelper.GetThemeStyles(theme);
        var appStyles = Application.Current?.Styles;
        appStyles?.Add(styles);
        return new ThemeScope(appStyles, styles);
    }

    private sealed class ThemeScope : IDisposable
    {
        private readonly Styles? _appStyles;
        private readonly Styles _styles;

        public ThemeScope(Styles? appStyles, Styles styles)
        {
            _appStyles = appStyles;
            _styles = styles;
        }

        public void Dispose()
        {
            _appStyles?.Remove(_styles);
        }
    }

    private static DataGridRowsPresenter GetRowsPresenter(DataGrid grid)
    {
        var presenter = grid.ScrollViewer?.Content as DataGridRowsPresenter;
        presenter ??= grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().FirstOrDefault();
        presenter ??= typeof(DataGrid)
            .GetField("_rowsPresenter", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(grid) as DataGridRowsPresenter;
        Assert.NotNull(presenter);
        return presenter!;
    }

    private static IReadOnlyList<DataGridRow> GetVisibleRows(DataGrid grid)
    {
        return grid.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => row.IsVisible)
            .ToList();
    }

    private static DataGridRow? FindVisibleRow(DataGrid grid, object dataContext)
    {
        if (dataContext is HierarchicalNode node)
        {
            return GetVisibleRows(grid)
                .FirstOrDefault(row =>
                    ReferenceEquals(row.DataContext, node) ||
                    (row.DataContext is HierarchicalNode rowNode && ReferenceEquals(rowNode.Item, node.Item)));
        }

        return GetVisibleRows(grid)
            .FirstOrDefault(row => ReferenceEquals(row.DataContext, dataContext));
    }

    private static void AssertVisibleRowsHaveCorrectIndent(DataGrid grid, double indent)
    {
        foreach (var row in GetVisibleRows(grid))
        {
            var node = Assert.IsType<HierarchicalNode>(row.DataContext);
            var presenter = GetHierarchicalPresenter(row);
            Assert.Equal(new Thickness(node.Level * indent, 0, 0, 0), presenter.Padding);
        }
    }

    private static DataGridHierarchicalPresenter GetHierarchicalPresenter(DataGridRow row)
    {
        if (row.Cells.Count > 0 && row.Cells[0].Content is DataGridHierarchicalPresenter presenter)
        {
            return presenter;
        }

        presenter = row.GetVisualDescendants()
            .OfType<DataGridHierarchicalPresenter>()
            .FirstOrDefault();

        Assert.NotNull(presenter);
        return presenter!;
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, modifiers);
    }

    private static Item CreateTree(string name, int childCount, int grandchildCount)
    {
        var root = new Item(name);
        for (int i = 0; i < childCount; i++)
        {
            var child = new Item($"{name} {i + 1}");
            for (int j = 0; j < grandchildCount; j++)
            {
                child.Children.Add(new Item($"{name} {i + 1}.{j + 1}"));
            }
            root.Children.Add(child);
        }

        return root;
    }

    private static Item CreateTreeWithRowHeights(string name, int childCount, int grandchildCount)
    {
        var root = new Item(name) { RowHeight = 28 };
        for (int i = 0; i < childCount; i++)
        {
            var child = new Item($"{name} {i + 1}")
            {
                RowHeight = 20 + (i % 3) * 4
            };
            for (int j = 0; j < grandchildCount; j++)
            {
                child.Children.Add(new Item($"{name} {i + 1}.{j + 1}")
                {
                    RowHeight = 18 + (j % 2) * 6
                });
            }
            root.Children.Add(child);
        }

        return root;
    }

    private static HierarchicalNode FindExpandableNode(DataGrid grid)
    {
        var candidate = GetVisibleRows(grid)
            .Select(row => row.DataContext as HierarchicalNode)
            .FirstOrDefault(node =>
                node != null &&
                node.Level > 0 &&
                node.Item is Item item &&
                item.Children.Count > 0);

        if (candidate != null)
        {
            return candidate;
        }

        if (grid.HierarchicalModel is HierarchicalModel model)
        {
            candidate = model.Flattened.FirstOrDefault(node =>
                node.Level > 0 &&
                node.Item is Item item &&
                item.Children.Count > 0);
        }

        Assert.NotNull(candidate);
        return candidate!;
    }

    private static void AssertVisibleRowsAreContiguous(DataGrid grid, double tolerance = 0.5)
    {
        var rows = GetVisibleRows(grid)
            .OrderBy(row => row.Bounds.Y)
            .ToList();

        for (var i = 1; i < rows.Count; i++)
        {
            var previous = rows[i - 1];
            var next = rows[i];
            var height = previous.Bounds.Height;
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
            {
                continue;
            }

            var expected = previous.Bounds.Y + height;
            Assert.InRange(Math.Abs(next.Bounds.Y - expected), 0, tolerance);
        }
    }

    private static void AssertVisibleRowsHaveExpectedText(DataGrid grid)
    {
        foreach (var row in GetVisibleRows(grid))
        {
            var node = Assert.IsType<HierarchicalNode>(row.DataContext);
            var presenter = GetHierarchicalPresenter(row);
            var expected = ((Item)node.Item).Name;
            var hasMatch = presenter.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(textBlock => textBlock.Text == expected);
            Assert.True(hasMatch);
        }
    }

    private static void ClickHeader(DataGrid grid, string header, KeyModifiers modifiers = KeyModifiers.None)
    {
        var headerCell = GetHeaderCell(grid, header);
        var sortingModel = Assert.IsAssignableFrom<ISortingModel>(grid.SortingModel);
        var before = sortingModel.Descriptors.ToList();

        var method = typeof(DataGridColumnHeader).GetMethod(
            "ProcessSort",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method!.Invoke(headerCell, new object[] { modifiers, null });

        if (before.SequenceEqual(sortingModel.Descriptors))
        {
            var adapterField = typeof(DataGrid).GetField("_sortingAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
            var adapter = adapterField?.GetValue(grid) as DataGridSortingAdapter;
            var column = grid.ColumnDefinitions.First(c => Equals(c.Header, header));
            adapter?.HandleHeaderClick(column, modifiers);
        }
    }

    private static string[] GetRowOrder(DataGrid grid)
    {
        var source = (IEnumerable?)grid.ItemsSource ?? Array.Empty<object>();

        return source.OfType<HierarchicalNode>()
            .Select(n => ((Item)n.Item).Name)
            .Skip(1) // skip root
            .ToArray();
    }

    private static void AssertHeaderSort(DataGrid grid, string header, bool asc, bool desc)
    {
        var headerCell = GetHeaderCell(grid, header);

        Assert.Equal(asc, HasPseudo(headerCell, ":sortascending"));
        Assert.Equal(desc, HasPseudo(headerCell, ":sortdescending"));
    }

    private static DataGridColumnHeader GetHeaderCell(DataGrid grid, string header)
    {
        grid.ApplyTemplate();
        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        if (grid.GetVisualRoot() is null)
        {
            throw new InvalidOperationException("DataGrid is not attached to a visual root.");
        }

        var descendants = grid.GetVisualDescendants().ToList();
        var headerCell = descendants
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault(h => Equals(h.Content, header));

        if (headerCell == null)
        {
            var presenterProp = typeof(DataGrid).GetProperty(
                "ColumnHeaders",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var presenter = presenterProp?.GetValue(grid) as Visual;
            headerCell = presenter?
                .GetVisualDescendants()
                .OfType<DataGridColumnHeader>()
                .FirstOrDefault(h => header == null || Equals(h.Content, header));
        }

        if (headerCell == null)
        {
            var names = descendants.Select(d => d.GetType().Name).Distinct().OrderBy(n => n);
            throw new InvalidOperationException($"No DataGridColumnHeader found. Descendants: {string.Join(", ", names)}");
        }

        return headerCell!;
    }

    private static bool HasPseudo(StyledElement element, string name)
    {
        var prop = typeof(StyledElement).GetProperty("PseudoClasses", BindingFlags.Instance | BindingFlags.NonPublic);
        var pseudo = prop!.GetValue(element);
        var contains = pseudo!.GetType().GetMethod("Contains", new[] { typeof(string) });
        return (bool)contains!.Invoke(pseudo, new object[] { name });
    }

    private static void EnsureSortingAdapter(DataGrid grid)
    {
        var adapterField = typeof(DataGrid).GetField("_sortingAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        var adapter = adapterField?.GetValue(grid);
        if (adapter != null)
        {
            return;
        }

        var createMethod = typeof(DataGrid).GetMethod("CreateSortingAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        var updateMethod = typeof(DataGrid).GetMethod("UpdateSortingAdapterView", BindingFlags.Instance | BindingFlags.NonPublic);
        var sortingModel = Assert.IsAssignableFrom<ISortingModel>(grid.SortingModel);

        var created = (DataGridSortingAdapter)createMethod!.Invoke(grid, new object[] { sortingModel })!;
        adapterField!.SetValue(grid, created);
        updateMethod?.Invoke(grid, null);
    }

    // --- Multi-Root Headless Tests ---

    [AvaloniaFact]
    public void MultiRoot_Grid_Displays_All_Root_Items()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Root1"),
            new Item("Root2"),
            new Item("Root3")
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(3, model.Count);
        Assert.True(model.IsVirtualRoot);
    }

    [AvaloniaFact]
    public void MultiRoot_TypedModel_Tracks_RootCollection_Changes()
    {
        var roots = new ObservableCollection<Item>();

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });
        model.SetRoots(roots);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = ((IHierarchicalModel)model).Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = grid
        };

        window.SetThemeStyles();
        window.Show();

        grid.ApplyTemplate();
        grid.Measure(new Size(400, 300));
        grid.Arrange(new Rect(0, 0, 400, 300));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var items = Assert.IsAssignableFrom<IReadOnlyList<HierarchicalNode>>(grid.ItemsSource);
        Assert.Equal(0, items.Count);

        roots.Add(new Item("Root1"));
        roots.Add(new Item("Root2"));

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        Assert.Equal(2, model.Count);
        Assert.Equal(2, items.Count);

        roots.RemoveAt(0);

        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        Assert.Equal(1, model.Count);
        Assert.Equal(1, items.Count);
    }

    [AvaloniaFact]
    public void MultiRoot_Expand_Root_Item_Shows_Children()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        root1.Children.Add(new Item("Child2"));
        var root2 = new Item("Root2");

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(2, model.Count);

        model.Expand(model.GetNode(0));

        Assert.Equal(4, model.Count);
        Assert.Equal("Child1", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("Child2", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("Root2", ((Item)model.GetItem(3)!).Name);
    }

    [AvaloniaFact]
    public void MultiRoot_Toggle_Via_Grid_Works()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));

        var items = new ObservableCollection<Item> { root1 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        // Expand
        var toggled = (bool)toggleMethod!.Invoke(grid, new object[] { 0, false })!;
        Assert.True(toggled);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.Equal(2, model.Count);

        // Collapse
        toggled = (bool)toggleMethod!.Invoke(grid, new object[] { 0, false })!;
        Assert.True(toggled);
        Assert.False(model.GetNode(0).IsExpanded);
        Assert.Equal(1, model.Count);
    }

    [AvaloniaFact]
    public void MultiRoot_Alt_Toggle_Expands_Subtree()
    {
        var root1 = new Item("Root1");
        var child1 = new Item("Child1");
        child1.Children.Add(new Item("Grandchild1"));
        root1.Children.Add(child1);

        var items = new ObservableCollection<Item> { root1 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        // Alt+Toggle expands entire subtree
        var toggled = (bool)toggleMethod!.Invoke(grid, new object[] { 0, true })!;

        Assert.True(toggled);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, model.Count);
    }

    [AvaloniaFact]
    public void MultiRoot_INCC_Add_Updates_Grid()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Root1")
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(1, model.Count);

        items.Add(new Item("Root2"));

        Assert.Equal(2, model.Count);
        Assert.Equal("Root2", ((Item)model.GetItem(1)!).Name);
    }

    [AvaloniaFact]
    public void MultiRoot_INCC_Remove_Updates_Grid()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Root1"),
            new Item("Root2")
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(2, model.Count);

        items.RemoveAt(0);

        Assert.Equal(1, model.Count);
        Assert.Equal("Root2", ((Item)model.GetItem(0)!).Name);
    }

    [AvaloniaFact]
    public void MultiRoot_Level_Zero_Items_Have_Correct_Indent()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        var root2 = new Item("Root2");

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        model.Expand(model.GetNode(0));

        // Root items should be at level 0
        Assert.Equal(0, model.GetNode(0).Level);
        Assert.Equal(3, model.Count); // Root1, Child1, Root2

        // Root2 is now at index 2 (after Root1 and its Child1)
        Assert.Equal(0, model.GetNode(2).Level);

        // Child should be at level 1
        Assert.Equal(1, model.GetNode(1).Level);
    }

    [AvaloniaFact]
    public void MultiRoot_ExpandAll_Expands_All_Root_Items()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        var root2 = new Item("Root2");
        root2.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(2, model.Count);

        model.ExpandAll();

        Assert.Equal(4, model.Count);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.True(model.GetNode(2).IsExpanded);
    }

    [AvaloniaFact]
    public void MultiRoot_CollapseAll_Collapses_All_Root_Items()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        var root2 = new Item("Root2");
        root2.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        // First expand all
        model.ExpandAll();
        Assert.Equal(4, model.Count);

        model.CollapseAll();

        Assert.Equal(2, model.Count);
        Assert.False(model.GetNode(0).IsExpanded);
        Assert.False(model.GetNode(1).IsExpanded);
    }

    [AvaloniaFact]
    public void MultiRoot_AutoExpandRoot_Expands_All_Root_Items()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        var root2 = new Item("Root2");
        root2.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            AutoExpandRoot = true
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        // All root items should be auto-expanded
        Assert.Equal(4, model.Count);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.True(model.GetNode(2).IsExpanded);
    }

    [AvaloniaFact]
    public void MultiRoot_Switch_Between_Single_And_Multi_Root()
    {
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });

        // Start with single root
        var singleRoot = new Item("SingleRoot");
        singleRoot.Children.Add(new Item("Child"));
        model.SetRoot(singleRoot);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.False(model.IsVirtualRoot);
        Assert.Equal(1, model.Count);

        // Switch to multi-root
        var multiItems = new ObservableCollection<Item>
        {
            new Item("Root1"),
            new Item("Root2")
        };
        model.SetRoots(multiItems);

        Assert.True(model.IsVirtualRoot);
        Assert.Equal(2, model.Count);

        // Switch back to single root
        model.SetRoot(new Item("AnotherSingleRoot"));

        Assert.False(model.IsVirtualRoot);
        Assert.Equal(1, model.Count);
    }

    [AvaloniaFact]
    public void MultiRoot_VirtualRoot_Not_Visible_In_Grid()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Root1"),
            new Item("Root2")
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        // Virtual root should not be in the flattened list
        Assert.DoesNotContain(model.Root, model.Flattened);

        // Only the actual root items should be visible
        Assert.Equal(2, model.Count);
        Assert.All(model.Flattened, node => Assert.NotSame(model.Root, node));
    }

    [AvaloniaFact]
    public void MultiRoot_Keyboard_Navigation_Works()
    {
        var root1 = new Item("Root1");
        root1.Children.Add(new Item("Child1"));
        var root2 = new Item("Root2");

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
        model.SetRoots(items);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        var toggleMethod = typeof(DataGrid).GetMethod(
            "TryToggleHierarchicalAtSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        // Expand using the toggle method (simulating keyboard navigation)
        var toggled = (bool)toggleMethod!.Invoke(grid, new object[] { 0, false })!;
        Assert.True(toggled);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.Equal(3, model.Count);
    }

    private sealed class HierarchicalSortingAdapterFactory : IDataGridSortingAdapterFactory
    {
        public DataGridSortingAdapter Create(DataGrid grid, ISortingModel model)
        {
            return new HierarchicalSortingAdapter(model, () => grid.ColumnDefinitions, null, null);
        }
    }

    private sealed class HierarchicalSortingAdapter : DataGridSortingAdapter
    {
        public HierarchicalSortingAdapter(
            ISortingModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            Action beforeViewRefresh,
            Action afterViewRefresh)
            : base(model, columnProvider, beforeViewRefresh, afterViewRefresh)
        {
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<SortingDescriptor> descriptors,
            IReadOnlyList<SortingDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = true;
            return true;
        }
    }
}
