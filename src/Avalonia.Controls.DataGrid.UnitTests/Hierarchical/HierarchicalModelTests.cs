// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class HierarchicalModelTests
{
    private class Item
    {
        public Item(string name)
        {
            Name = name;
            Children = new ObservableCollection<Item>();
        }

        public string Name { get; }

        public ObservableCollection<Item> Children { get; set; }
    }

    private static HierarchicalModel CreateModel()
    {
        return new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children
        });
    }

    [Fact]
    public void ChildrenPropertyPath_ResolvesChildren()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenPropertyPath = nameof(Item.Children)
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal(2, model.Count);
        Assert.Same(root.Children[0], model.GetItem(1));
    }

    [Fact]
    public void ItemsSelector_ResolvesChildren()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ItemsSelector = item => ((Item)item).Children
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal(2, model.Count);
        Assert.Same(root.Children[0], model.GetItem(1));
    }

    [Fact]
    public void MaxDepth_TreatsNodesAsLeaves()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            MaxDepth = 1
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal(2, model.Count);
        Assert.True(model.GetNode(1).IsLeaf);

        model.Expand(model.GetNode(1));

        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void CycleDetection_SkipsChild_AndRaisesLoadFailed()
    {
        var root = new Item("root");
        root.Children.Add(root);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children
        });

        HierarchicalNodeLoadFailedEventArgs? loadFailedArgs = null;
        model.NodeLoadFailed += (_, e) => loadFailedArgs = e;

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal(1, model.Count);
        Assert.True(model.Root!.IsLeaf);
        Assert.NotNull(loadFailedArgs);
        Assert.Same(model.Root, loadFailedArgs!.Node);
    }

    [Fact]
    public void AutoExpandRoot_Respects_MaxDepth()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1,
            ChildrenSelector = item => ((Item)item).Children
        });

        model.SetRoot(root);

        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, model.Count);
        Assert.Same(grand, model.GetItem(2));
    }

    [Fact]
    public void Sort_Reorders_Siblings()
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        model.Sort(comparer: Comparer<object>.Create((x, y) =>
            string.Compare(((Item)x).Name, ((Item)y).Name, StringComparison.Ordinal)));

        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void Incc_Remove_RemovesVisibleChild()
    {
        var root = new Item("root");
        var c1 = new Item("c1");
        var c2 = new Item("c2");
        root.Children.Add(c1);
        root.Children.Add(c2);
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);
        Assert.Equal(3, model.Count);

        root.Children.Remove(c1);

        Assert.Equal(2, model.Count);
        Assert.Equal("c2", ((Item)model.GetItem(1)!).Name);
    }

    [Fact]
    public void Refresh_RebuildsChildren()
    {
        var root = new Item("root");
        root.Children.Add(new Item("old"));
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        var rootNode = model.Root!;
        root.Children = new ObservableCollection<Item> { new Item("new") };

        model.Refresh(rootNode);

        Assert.Equal(2, model.Count);
        Assert.Equal("new", ((Item)model.GetItem(1)!).Name);
    }

    [Fact]
    public void Adapter_Forwards_FlattenedChanged()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));
        var model = CreateModel();
        model.SetRoot(root);

        var adapter = new DataGridHierarchicalAdapter(model);
        FlattenedChangedEventArgs? args = null;
        adapter.FlattenedChanged += (_, e) => args = e;

        adapter.Expand(0);

        Assert.NotNull(args);
        Assert.Equal(2, adapter.Count);
        var change = Assert.Single(args!.Changes);
        Assert.Equal(1, change.Index);
    }

    [Fact]
    public void Expand_AddsChildrenToFlattened()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child1"));
        root.Children.Add(new Item("child2"));
        var model = CreateModel();

        model.SetRoot(root);

        Assert.Equal(1, model.Count);
        model.Expand(model.Root!);

        Assert.Equal(3, model.Count);
        Assert.Equal("child1", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("child2", ((Item)model.GetItem(2)!).Name);
        Assert.Equal(1, model.GetNode(1).Level);
    }

    [Fact]
    public void Collapse_RemovesDescendants_AndVirtualizes()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);
        child.Children.Add(new Item("grand"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            VirtualizeChildren = true
        });

        model.SetRoot(root);
        model.Expand(model.Root!);
        model.Expand(model.GetNode(1));

        var initialChildNode = model.GetNode(1);
        Assert.Equal(3, model.Count);

        model.Collapse(model.Root!);
        Assert.Equal(1, model.Count);

        model.Expand(model.Root!);
        var refreshedChildNode = model.GetNode(1);

        Assert.NotSame(initialChildNode, refreshedChildNode);
        Assert.Equal("child", ((Item)refreshedChildNode.Item).Name);
    }

    [Fact]
    public void Incc_Add_AddsVisibleChildUnderExpandedParent()
    {
        var root = new Item("root");
        var model = CreateModel();
        model.SetRoot(root);

        model.Expand(model.Root!);
        Assert.Equal(1, model.Count);

        root.Children.Add(new Item("new child"));

        Assert.Equal(2, model.Count);
        Assert.Equal("new child", ((Item)model.GetItem(1)!).Name);
    }

    [Fact]
    public void Expand_RematerializesChildren_AfterVirtualize()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            VirtualizeChildren = true,
            IsLeafSelector = item => ((Item)item).Children.Count == 0
        });

        model.SetRoot(root);
        model.Expand(model.Root!);           // realize first-level children
        model.Collapse(model.Root!);         // virtualize children

        // child should be rematerialized on next expand
        model.Expand(model.Root!);

        Assert.Equal(2, model.Count);
        Assert.Same(child, model.GetItem(1));
    }

    [Fact]
    public void ExpandedCount_TracksVisibleDescendants()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);

        Assert.Equal(0, model.Root!.ExpandedCount);

        model.Expand(model.Root!);
        Assert.Equal(1, model.Root!.ExpandedCount);
        Assert.Equal(0, model.GetNode(1).ExpandedCount);

        model.Expand(model.GetNode(1));
        Assert.Equal(2, model.Root!.ExpandedCount);
        Assert.Equal(1, model.GetNode(1).ExpandedCount);

        model.Collapse(model.GetNode(1));
        Assert.Equal(1, model.Root!.ExpandedCount);
        Assert.Equal(0, model.GetNode(1).ExpandedCount);
    }

    [Fact]
    public void ExpandAll_Expands_Subtree()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);

        model.ExpandAll();

        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, model.Count);
        Assert.Equal(2, model.Root!.ExpandedCount);
    }

    [Fact]
    public void ExpandAll_WithDepthLimit_DoesNotExpandBeyondLimit()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);

        model.ExpandAll(maxDepth: 0);

        Assert.True(model.Root!.IsExpanded);
        Assert.False(model.GetNode(1).IsExpanded);
        Assert.Equal(2, model.Count);
        Assert.Equal(1, model.Root!.ExpandedCount);
    }

    [Fact]
    public void CollapseAll_Default_CollapsesEverything()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);
        model.ExpandAll();
        Assert.Equal(3, model.Count);

        model.CollapseAll();

        Assert.False(model.Root!.IsExpanded);
        Assert.Equal(1, model.Count);
        Assert.Equal(0, model.Root!.ExpandedCount);
    }

    [Fact]
    public void CollapseAll_FromDepth_CollapsesDescendantsOnly()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);
        model.ExpandAll();

        model.CollapseAll(minDepth: 1);

        Assert.True(model.Root!.IsExpanded);
        Assert.False(model.GetNode(1).IsExpanded);
        Assert.Equal(2, model.Count);
        Assert.Equal(1, model.Root!.ExpandedCount);
    }
}
