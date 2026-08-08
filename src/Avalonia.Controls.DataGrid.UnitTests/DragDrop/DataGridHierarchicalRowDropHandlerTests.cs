// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using AvaloniaDragDrop = Avalonia.Input.DragDrop;

namespace Avalonia.Controls.DataGridTests.DragDrop;

public class DataGridHierarchicalRowDropHandlerTests
{
    [AvaloniaFact]
    public void Execute_Moves_Node_Within_Same_Parent()
    {
        var (grid, model, root, _, handler) = CreateGrid();
        var nodeA = root.MutableChildren[0];
        var target = root.MutableChildren[2];

        var args = CreateArgs(grid, model, new[] { nodeA }, target, DataGridRowDropPosition.After);

        Assert.True(handler.Execute(args));
        Assert.Equal(new[] { "B", "C", "A" }, GetChildNames(root));
    }

    [AvaloniaFact]
    public void Execute_Moves_Node_To_Different_Parent()
    {
        var (grid, model, root, secondary, handler) = CreateGrid();
        var nodeA1 = root.MutableChildren[0].MutableChildren[0];
        var target = secondary.MutableChildren[0];

        var args = CreateArgs(grid, model, new[] { nodeA1 }, target, DataGridRowDropPosition.Before);

        Assert.True(handler.Execute(args));
        Assert.Equal(new[] { "A2" }, GetChildNames(root.MutableChildren[0]));
        Assert.Equal(new[] { "A1", "B1" }, GetChildNames(secondary));
    }

    [AvaloniaFact]
    public void Validate_Fails_When_Target_Is_Descendant()
    {
        var (grid, model, root, _, handler) = CreateGrid();
        var parent = root.MutableChildren[0];
        var child = parent.MutableChildren[0];

        var args = CreateArgs(grid, model, new[] { parent }, child, DataGridRowDropPosition.Before);

        Assert.False(handler.Validate(args));
        Assert.False(handler.Execute(args));
    }

    [AvaloniaFact]
    public void Execute_Drops_Node_Inside_Target()
    {
        var (grid, model, root, _, handler) = CreateGrid();
        var nodeB = root.MutableChildren[1];
        var target = root.MutableChildren[0];

        var args = CreateArgs(grid, model, new[] { nodeB }, target, DataGridRowDropPosition.Inside);

        Assert.True(handler.Execute(args));
        Assert.Equal(new[] { "A", "C" }, GetChildNames(root));
        Assert.Equal(new[] { "A1", "A2", "B" }, GetChildNames(target));
    }

    private static (DataGrid Grid, HierarchicalModel Model, HierarchicalNode Root, HierarchicalNode Secondary, DataGridHierarchicalRowReorderHandler Handler) CreateGrid()
    {
        var rootItem = new TreeNode("Root", new ObservableCollection<TreeNode>
        {
            new("A", new ObservableCollection<TreeNode>
            {
                new("A1"),
                new("A2"),
            }),
            new("B", new ObservableCollection<TreeNode>
            {
                new("B1"),
            }),
            new("C")
        });

        var options = new HierarchicalOptions<TreeNode>
        {
            ChildrenSelector = x => x.Children,
            AutoExpandRoot = true
        };

        var model = new HierarchicalModel<TreeNode>(options);
        model.SetRoot(rootItem);
        HierarchicalModel untyped = model;

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            CanUserReorderRows = true,
            AutoGenerateColumns = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var handler = new DataGridHierarchicalRowReorderHandler();

        return (grid, untyped, untyped.Root!, untyped.Root!.MutableChildren[1], handler);
    }

    private static DataGridRowDropEventArgs CreateArgs(
        DataGrid grid,
        HierarchicalModel model,
        IReadOnlyList<HierarchicalNode> dragNodes,
        HierarchicalNode target,
        DataGridRowDropPosition position)
    {
        IList? list = grid.ItemsSource as IList;
        var items = dragNodes.Cast<object>().ToList();
        var indices = dragNodes.Select(model.IndexOf).ToList();
        var targetIndex = model.IndexOf(target);
        var insertIndex = position switch
        {
            DataGridRowDropPosition.After => targetIndex + 1,
            DataGridRowDropPosition.Inside => target.MutableChildren.Count,
            _ => targetIndex
        };

        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DropEvent,
            new DataTransfer(),
            grid,
            new Avalonia.Point(),
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DropEvent,
            Source = grid
        };

        return new DataGridRowDropEventArgs(
            grid,
            list,
            items,
            indices,
            target,
            targetIndex,
            insertIndex,
            null,
            position,
            isSameGrid: true,
            DragDropEffects.Move,
            dragEvent);
    }

    private static IReadOnlyList<string> GetChildNames(HierarchicalNode node)
    {
        return node.MutableChildren.Select(x => x.Item).OfType<TreeNode>().Select(x => x.Name).ToList();
    }

    [AvaloniaFact]
    public void Execute_MultiRoot_Moves_Node_To_Root_Level()
    {
        var (grid, model, items, handler) = CreateMultiRootGrid();

        // Move child "A1" to root level
        var nodeA = model.Root!.MutableChildren[0];
        model.Expand(nodeA);
        var nodeA1 = nodeA.MutableChildren[0];
        var targetB = model.Root.MutableChildren[1];

        var args = CreateMultiRootArgs(grid, model, new[] { nodeA1 }, targetB, DataGridRowDropPosition.After);

        Assert.True(handler.Execute(args));

        // A1 should now be at root level
        Assert.Equal(3, items.Count);
        Assert.Equal("A", items[0].Name);
        Assert.Equal("B", items[1].Name);
        Assert.Equal("A1", items[2].Name);
    }

    [AvaloniaFact]
    public void Execute_MultiRoot_Moves_Root_Item_Between_Siblings()
    {
        var (grid, model, items, handler) = CreateMultiRootGrid();

        var nodeA = model.Root!.MutableChildren[0];
        var targetB = model.Root.MutableChildren[1];

        var args = CreateMultiRootArgs(grid, model, new[] { nodeA }, targetB, DataGridRowDropPosition.After);

        Assert.True(handler.Execute(args));
        Assert.Equal(new[] { "B", "A" }, items.Select(x => x.Name).ToArray());
    }

    [AvaloniaFact]
    public void Execute_MultiRoot_Moves_Node_Into_Another_Root()
    {
        var (grid, model, items, handler) = CreateMultiRootGrid();

        var nodeA = model.Root!.MutableChildren[0];
        model.Expand(nodeA);
        var nodeA1 = nodeA.MutableChildren[0];
        var targetB = model.Root.MutableChildren[1];

        // Expand target to materialize its children collection
        model.Expand(targetB);

        var args = CreateMultiRootArgs(grid, model, new[] { nodeA1 }, targetB, DataGridRowDropPosition.Inside);

        Assert.True(handler.Execute(args));

        Assert.Equal(new[] { "A2" }, nodeA.MutableChildren.Select(x => ((TreeNode)x.Item).Name).ToArray());
        Assert.Equal(new[] { "A1" }, targetB.MutableChildren.Select(x => ((TreeNode)x.Item).Name).ToArray());
    }

    [AvaloniaFact]
    public void Validate_MultiRoot_Fails_For_VirtualRoot_Drag()
    {
        var (grid, model, _, handler) = CreateMultiRootGrid();

        // Attempting to drag the virtual root should fail
        var args = CreateMultiRootArgs(grid, model, new[] { model.Root! }, model.Root!.MutableChildren[0], DataGridRowDropPosition.After);

        Assert.False(handler.Validate(args));
    }

    private static (DataGrid Grid, HierarchicalModel Model, ObservableCollection<TreeNode> Items, DataGridHierarchicalRowReorderHandler Handler) CreateMultiRootGrid()
    {
        var items = new ObservableCollection<TreeNode>
        {
            new("A", new ObservableCollection<TreeNode>
            {
                new("A1"),
                new("A2"),
            }),
            new("B")
        };

        var options = new HierarchicalOptions<TreeNode>
        {
            ChildrenSelector = x => x.Children,
            AutoExpandRoot = false
        };

        var model = new HierarchicalModel<TreeNode>(options);
        model.SetRoots(items);
        HierarchicalModel untyped = model;

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            CanUserReorderRows = true,
            AutoGenerateColumns = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var handler = new DataGridHierarchicalRowReorderHandler();

        return (grid, untyped, items, handler);
    }

    private static DataGridRowDropEventArgs CreateMultiRootArgs(
        DataGrid grid,
        HierarchicalModel model,
        IReadOnlyList<HierarchicalNode> dragNodes,
        HierarchicalNode target,
        DataGridRowDropPosition position)
    {
        IList? list = grid.ItemsSource as IList;
        var items = dragNodes.Cast<object>().ToList();
        var indices = dragNodes.Select(model.IndexOf).ToList();
        var targetIndex = model.IndexOf(target);
        var insertIndex = position switch
        {
            DataGridRowDropPosition.After => targetIndex + 1,
            DataGridRowDropPosition.Inside => target.MutableChildren.Count,
            _ => targetIndex
        };

        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DropEvent,
            new DataTransfer(),
            grid,
            new Avalonia.Point(),
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DropEvent,
            Source = grid
        };

        return new DataGridRowDropEventArgs(
            grid,
            list,
            items,
            indices,
            target,
            targetIndex,
            insertIndex,
            null,
            position,
            isSameGrid: true,
            DragDropEffects.Move,
            dragEvent);
    }

    private class TreeNode
    {
        public TreeNode(string name, ObservableCollection<TreeNode>? children = null)
        {
            Name = name;
            Children = children ?? new ObservableCollection<TreeNode>();
        }

        public string Name { get; }

        public ObservableCollection<TreeNode> Children { get; }
    }
}
