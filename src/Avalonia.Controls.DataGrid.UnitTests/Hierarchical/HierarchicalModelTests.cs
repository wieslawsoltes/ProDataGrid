// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    public void FlattenedVersion_Increments_OnChanges()
    {
        var model = CreateModel();
        var root = new Item("root");
        root.Children.Add(new Item("child"));
        model.SetRoot(root);

        Assert.Equal(1, model.FlattenedVersion);

        model.Expand(model.Root!);
        Assert.Equal(2, model.FlattenedVersion);

        model.Collapse(model.Root!);
        Assert.Equal(3, model.FlattenedVersion);
    }

    [Fact]
    public void ObservableFlattened_RaisesChange_OnExpand()
    {
        var model = CreateModel();
        var root = new Item("root");
        root.Children.Add(new Item("child"));
        model.SetRoot(root);

        NotifyCollectionChangedEventArgs? change = null;
        ((INotifyCollectionChanged)model.ObservableFlattened).CollectionChanged += (_, e) => change = e;

        model.Expand(model.Root!);

        Assert.NotNull(change);
        Assert.Equal(NotifyCollectionChangedAction.Add, change!.Action);
        Assert.Equal(1, change.NewStartingIndex);
        Assert.NotNull(change.NewItems);
        Assert.Equal(1, change.NewItems!.Count);
    }

    [Fact]
    public void IndexOf_Returns_VisibleIndex()
    {
        var model = CreateModel();
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);
        model.SetRoot(root);

        Assert.Equal(0, model.IndexOf(model.Root!));
        Assert.Equal(-1, model.IndexOf(child));

        model.Expand(model.Root!);

        Assert.Equal(1, model.IndexOf(child));
        Assert.Equal(1, model.IndexOf(model.GetNode(1)));
    }

    [Fact]
    public void ApplySiblingComparer_SortsAndStoresComparer()
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var comparer = Comparer<object>.Create((x, y) =>
            string.Compare(((Item)x).Name, ((Item)y).Name, StringComparison.Ordinal));

        model.ApplySiblingComparer(comparer);

        Assert.Same(comparer, model.Options.SiblingComparer);
        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
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
    public void ExpandAll_Respects_MaxDepth()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);

        model.ExpandAll(maxDepth: 1);

        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.False(model.GetNode(2).IsExpanded);
        Assert.Equal(3, model.Count);
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

    [Fact]
    public async Task ChildrenSelectorAsync_LoadsChildren()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = async (item, ct) =>
            {
                await Task.Delay(10, ct);
                return ((Item)item).Children;
            }
        });

        model.SetRoot(root);
        await model.ExpandAsync(model.Root!);

        Assert.Equal(2, model.Count);
        Assert.False(model.Root!.IsLoading);
        Assert.True(model.Root!.IsExpanded);
    }

    [Fact]
    public async Task ChildrenSelectorAsync_CanBeCancelled()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = async (item, ct) =>
            {
                await Task.Delay(50, ct);
                return ((Item)item).Children;
            }
        });

        model.SetRoot(root);

        using var cts = new CancellationTokenSource();
        var expandTask = model.ExpandAsync(model.Root!, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => expandTask);
        Assert.False(model.Root!.IsLoading);
        Assert.False(model.Root!.IsExpanded);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public async Task ChildrenSelectorAsync_CancelledByCollapse_LeavesNodeUnloaded()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var tcs = new TaskCompletionSource<IEnumerable?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = (_, ct) =>
            {
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            }
        });

        model.SetRoot(root);

        var expandTask = model.ExpandAsync(model.Root!);
        await Task.Delay(10);
        Assert.True(model.Root!.IsLoading);

        model.Collapse(model.Root!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => expandTask);

        Assert.False(model.Root!.IsLoading);
        Assert.False(model.Root!.IsExpanded);
        Assert.Null(model.Root!.LoadError);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public async Task ChildrenSelectorAsync_RetryBackoff_IsHonored_And_Succeeds()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        int attempts = 0;
        TimeSpan? scheduledDelay = null;
        HierarchicalNodeLoadFailedEventArgs? failure = null;

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = (_, _) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("boom");
                }

                return Task.FromResult<IEnumerable?>(root.Children);
            }
        });

        model.NodeLoadRetryScheduled += (_, e) => scheduledDelay = e.Delay;
        model.NodeLoadFailed += (_, e) => failure = e;

        model.SetRoot(root);

        await model.ExpandAsync(model.Root!);
        Assert.Equal(1, attempts);
        Assert.NotNull(failure);
        Assert.NotNull(scheduledDelay);
        Assert.True(scheduledDelay!.Value > TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        await model.ExpandAsync(model.Root!);
        sw.Stop();

        Assert.True(sw.Elapsed >= scheduledDelay!.Value - TimeSpan.FromMilliseconds(10));
        Assert.Equal(2, attempts);
        Assert.True(model.Root!.IsExpanded);
        Assert.Equal(2, model.Count);
        Assert.Null(model.Root!.LoadError);
    }

    [Fact]
    public async Task ChildrenSelectorAsync_DeduplicatesConcurrentExpands()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var tcs = new TaskCompletionSource<IEnumerable?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = async (_, ct) =>
            {
                Interlocked.Increment(ref calls);
                using (ct.Register(() => tcs.TrySetCanceled(ct)))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
        });

        model.SetRoot(root);

        var expand1 = model.ExpandAsync(model.Root!);
        var expand2 = model.ExpandAsync(model.Root!);

        await Task.Delay(10);
        tcs.TrySetResult(root.Children);

        await Task.WhenAll(expand1, expand2);

        Assert.Equal(1, calls);
        Assert.True(model.Root!.IsExpanded);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void Expand_Collapse_RaiseFlattenedChanges()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));
        var model = CreateModel();
        model.SetRoot(root);

        List<FlattenedChange> changes = new();
        model.FlattenedChanged += (_, e) => changes.AddRange(e.Changes);

        model.Expand(model.Root!);
        model.Collapse(model.Root!);

        Assert.Collection(
            changes,
            c =>
            {
                Assert.Equal(1, c.Index);
                Assert.Equal(0, c.OldCount);
                Assert.Equal(1, c.NewCount);
            },
            c =>
            {
                Assert.Equal(1, c.Index);
                Assert.Equal(1, c.OldCount);
                Assert.Equal(0, c.NewCount);
            });
    }

    [Fact]
    public void Incc_Add_RaisesFlattenedChange()
    {
        var root = new Item("root");
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        root.Children.Add(new Item("child"));

        Assert.NotNull(args);
        var change = Assert.Single(args!.Changes);
        Assert.Equal(1, change.Index);
        Assert.Equal(0, change.OldCount);
        Assert.Equal(1, change.NewCount);
        Assert.Equal("child", ((Item)model.GetItem(1)!).Name);
    }

    [Fact]
    public void LoadFailure_MarksNodeAsLeaf_AndRaisesEvent()
    {
        var root = new Item("root");
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = _ => throw new InvalidOperationException("boom")
        });

        HierarchicalNodeLoadFailedEventArgs? failure = null;
        model.NodeLoadFailed += (_, e) => failure = e;

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.True(model.Root!.IsLeaf); // load failure marks as leaf
        Assert.False(model.Root!.IsExpanded); // expand aborted
        Assert.Equal(1, model.Count);
        Assert.NotNull(failure);
        Assert.Same(model.Root, failure!.Node);
        Assert.IsType<InvalidOperationException>(failure.Error);
        Assert.NotNull(model.Root!.LoadError);
    }

    [Fact]
    public void Incc_Replace_RaisesSingleRefreshChange()
    {
        var root = new Item("root");
        var c1 = new Item("c1");
        var c2 = new Item("c2");
        root.Children.Add(c1);
        root.Children.Add(c2);
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        FlattenedChangedEventArgs? args = null;
        HierarchyChangedEventArgs? hierarchyArgs = null;
        model.FlattenedChanged += (_, e) => args = e;
        model.HierarchyChanged += (_, e) => hierarchyArgs = e;

        root.Children[1] = new Item("c3"); // Replace action

        var change = Assert.Single(args!.Changes);
        Assert.Equal(2, change.Index);
        Assert.Equal(1, change.OldCount);
        Assert.Equal(1, change.NewCount);
        Assert.Equal("c1", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("c3", ((Item)model.GetItem(2)!).Name);
        Assert.NotNull(hierarchyArgs);
        Assert.Equal(NotifyCollectionChangedAction.Replace, hierarchyArgs!.Action);
    }

    [Fact]
    public void Incc_Move_RaisesSingleRefreshChange()
    {
        var root = new Item("root");
        root.Children.Add(new Item("a"));
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("c"));
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        FlattenedChangedEventArgs? args = null;
        HierarchyChangedEventArgs? hierarchyArgs = null;
        model.FlattenedChanged += (_, e) => args = e;
        model.HierarchyChanged += (_, e) => hierarchyArgs = e;

        root.Children.Move(0, 2);

        var changes = args!.Changes;
        Assert.Equal(2, changes.Count);
        Assert.Equal(1, changes[0].Index);
        Assert.Equal(1, changes[0].OldCount);
        Assert.Equal(0, changes[0].NewCount);
        Assert.Equal(3, changes[1].Index);
        Assert.Equal(0, changes[1].OldCount);
        Assert.Equal(1, changes[1].NewCount);
        Assert.Equal("b", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("c", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("a", ((Item)model.GetItem(3)!).Name);
        Assert.NotNull(hierarchyArgs);
        Assert.Equal(NotifyCollectionChangedAction.Move, hierarchyArgs!.Action);
    }

    [Fact]
    public void FlattenedIndexMap_TracksMove()
    {
        var root = new Item("root");
        root.Children.Add(new Item("a"));
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("c"));
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        root.Children.Move(0, 2);

        var map = args!.IndexMap;
        Assert.Equal(4, map.OldCount);
        Assert.Equal(4, map.NewCount);
        Assert.Equal(3, map.MapOldIndexToNew(1)); // a moves to the end
        Assert.Equal(1, map.MapOldIndexToNew(2)); // b moves up
        Assert.Equal(2, map.MapOldIndexToNew(3)); // c moves up
    }

    [Fact]
    public void FlattenedIndexMap_MarksReplacedItemAsRemoved()
    {
        var root = new Item("root");
        root.Children.Add(new Item("a"));
        root.Children.Add(new Item("b"));
        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        root.Children[1] = new Item("c");

        var map = args!.IndexMap;
        Assert.Equal(3, map.OldCount);
        Assert.Equal(3, map.NewCount);
        Assert.Equal(1, map.MapOldIndexToNew(1)); // first child unchanged
        Assert.Equal(-1, map.MapOldIndexToNew(2)); // replaced item removed
    }

    [Fact]
    public void Collapse_VirtualizesChildren()
    {
        var root = new Item("root");
        var child = new Item("child");
        child.Children.Add(new Item("grand"));
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            VirtualizeChildren = true
        });

        model.SetRoot(root);
        model.Expand(model.Root!);
        model.Expand(model.GetNode(1));
        Assert.Equal(3, model.Count);

        model.Collapse(model.Root!);

        Assert.Empty(model.Root!.Children);
        Assert.Equal(1, model.Count);
    }
}
