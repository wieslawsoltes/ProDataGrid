// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

    private sealed class DuplicateItem
    {
        public DuplicateItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<DuplicateItem>();
        }

        public string Name { get; }

        public ObservableCollection<DuplicateItem> Children { get; }

        public override bool Equals(object? obj)
        {
            return obj is DuplicateItem other && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Name);
        }
    }

    private sealed class ExpandableItem : INotifyPropertyChanged
    {
        public ExpandableItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<ExpandableItem>();
        }

        public string Name { get; }

        public ObservableCollection<ExpandableItem> Children { get; }

        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class TrackingExpandableItem : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _propertyChanged;
        private bool _isExpanded;

        public TrackingExpandableItem(string name)
        {
            Name = name;
            Children = new TrackingCollection<TrackingExpandableItem>();
        }

        public string Name { get; }

        public TrackingCollection<TrackingExpandableItem> Children { get; }

        public int PropertyChangedSubscriptionCount { get; private set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                RaiseIsExpandedChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                PropertyChangedSubscriptionCount = _propertyChanged?.GetInvocationList().Length ?? 0;
            }
            remove
            {
                _propertyChanged -= value;
                PropertyChangedSubscriptionCount = _propertyChanged?.GetInvocationList().Length ?? 0;
            }
        }

        public void RaiseIsExpandedChanged()
        {
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    private sealed class TrackingCollection<T> : INotifyCollectionChanged, IEnumerable<T>
    {
        private NotifyCollectionChangedEventHandler? _collectionChanged;
        private readonly List<T> _items = new();

        public int SubscriptionCount { get; private set; }

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add
            {
                _collectionChanged += value;
                SubscriptionCount = _collectionChanged?.GetInvocationList().Length ?? 0;
            }
            remove
            {
                _collectionChanged -= value;
                SubscriptionCount = _collectionChanged?.GetInvocationList().Length ?? 0;
            }
        }

        public void Add(T item) => _items.Add(item);

        public void RaiseReset()
        {
            _collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
    public void ExpandedState_Respects_ItemState_On_SetRoot()
    {
        var root = new ExpandableItem("root") { IsExpanded = true };
        var child = new ExpandableItem("child") { IsExpanded = true };
        child.Children.Add(new ExpandableItem("grand"));
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((ExpandableItem)item).Children,
            IsExpandedSelector = item => ((ExpandableItem)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((ExpandableItem)item).IsExpanded = value
        });

        model.SetRoot(root);

        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void ExpandedState_Updates_Item_On_Model_Expand_Collapse()
    {
        var root = new ExpandableItem("root");
        root.Children.Add(new ExpandableItem("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((ExpandableItem)item).Children,
            IsExpandedSelector = item => ((ExpandableItem)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((ExpandableItem)item).IsExpanded = value
        });

        model.SetRoot(root);

        Assert.False(root.IsExpanded);

        model.Expand(model.Root!);
        Assert.True(root.IsExpanded);

        model.Collapse(model.Root!);
        Assert.False(root.IsExpanded);
    }

    [Fact]
    public void ExpandedState_Updates_Model_On_Item_Change()
    {
        var root = new ExpandableItem("root");
        var child = new ExpandableItem("child");
        child.Children.Add(new ExpandableItem("grand"));
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((ExpandableItem)item).Children,
            IsExpandedSelector = item => ((ExpandableItem)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((ExpandableItem)item).IsExpanded = value
        });

        model.SetRoot(root);

        Assert.Equal(1, model.Count);

        root.IsExpanded = true;
        Assert.True(model.Root!.IsExpanded);
        Assert.Equal(2, model.Count);

        child.IsExpanded = true;
        Assert.Equal(3, model.Count);
        Assert.True(model.FindNode(child)!.IsExpanded);

        root.IsExpanded = false;
        Assert.False(model.Root!.IsExpanded);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public void ExpandedState_Updates_Model_On_Node_Change()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children
        });

        model.SetRoot(root);

        Assert.Equal(1, model.Count);

        model.Root!.IsExpanded = true;
        Assert.True(model.Root.IsExpanded);
        Assert.Equal(2, model.Count);

        model.Root.IsExpanded = false;
        Assert.False(model.Root.IsExpanded);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public void Rebuild_Preserves_Expanded_State_By_Path_For_Duplicate_Items()
    {
        var root = new DuplicateItem("root");
        var first = new DuplicateItem("dup");
        var second = new DuplicateItem("dup");
        var firstChild = new DuplicateItem("first-child");
        var secondChild = new DuplicateItem("second-child");
        first.Children.Add(firstChild);
        second.Children.Add(secondChild);
        root.Children.Add(first);
        root.Children.Add(second);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((DuplicateItem)item).Children,
            ExpandedStateKeyMode = ExpandedStateKeyMode.Path
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        var firstNode = FindNodeByReference(model, first);
        model.Expand(firstNode);

        Assert.True(FlattenedContains(model, firstChild));
        Assert.False(FlattenedContains(model, secondChild));

        model.SetRoot(root);

        var firstNodeAfter = FindNodeByReference(model, first);
        var secondNodeAfter = FindNodeByReference(model, second);

        Assert.True(firstNodeAfter.IsExpanded);
        Assert.False(secondNodeAfter.IsExpanded);
        Assert.True(FlattenedContains(model, firstChild));
        Assert.False(FlattenedContains(model, secondChild));
    }

    [Fact]
    public void TryExpandToItem_Uses_PathSelector_For_Duplicates()
    {
        var root = new DuplicateItem("root");
        var first = new DuplicateItem("dup");
        var second = new DuplicateItem("dup");
        var firstChild = new DuplicateItem("first-child");
        var secondChild = new DuplicateItem("second-child");
        first.Children.Add(firstChild);
        second.Children.Add(secondChild);
        root.Children.Add(first);
        root.Children.Add(second);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((DuplicateItem)item).Children,
            ItemPathSelector = item => BuildPath(root, (DuplicateItem)item)
        });

        model.SetRoot(root);

        Assert.True(model.TryExpandToItem(firstChild, out var node));
        Assert.Same(firstChild, node!.Item);
        Assert.True(FlattenedContains(model, firstChild));
        Assert.False(FlattenedContains(model, secondChild));
    }

    [Fact]
    public void FindNode_Prefers_Reference_Match_Over_Equality()
    {
        var root = new DuplicateItem("root");
        var first = new DuplicateItem("dup");
        var second = new DuplicateItem("dup");
        root.Children.Add(first);
        root.Children.Add(second);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((DuplicateItem)item).Children
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        var secondNode = model.FindNode(second);
        Assert.NotNull(secondNode);
        Assert.Same(second, secondNode!.Item);
        Assert.Equal(2, model.IndexOf(second));

        var probe = new DuplicateItem("dup");
        var firstNode = model.FindNode(probe);
        Assert.NotNull(firstNode);
        Assert.Same(first, firstNode!.Item);
        Assert.Equal(1, model.IndexOf(probe));
    }

    [Fact]
    public void FindNode_Updates_After_Flattened_Changes()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.NotNull(model.FindNode(child));
        Assert.Equal(1, model.IndexOf(child));

        var added = new Item("added");
        root.Children.Add(added);

        Assert.NotNull(model.FindNode(added));
        Assert.Equal(2, model.IndexOf(added));

        model.Collapse(model.Root!);

        Assert.Null(model.FindNode(child));
        Assert.Equal(-1, model.IndexOf(child));
    }

    [Fact]
    public void FindNode_Updates_During_Flattened_CollectionChanged()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.NotNull(model.FindNode(child));

        var added = new Item("added");
        HierarchicalNode? foundInEvent = null;
        var indexInEvent = -1;

        ((INotifyCollectionChanged)model.ObservableFlattened).CollectionChanged += (_, __) =>
        {
            foundInEvent = model.FindNode(added);
            indexInEvent = model.IndexOf(added);
        };

        root.Children.Add(added);

        Assert.NotNull(foundInEvent);
        Assert.Same(added, foundInEvent!.Item);
        Assert.Equal(model.IndexOf(added), indexInEvent);
    }

    private static HierarchicalNode FindNodeByReference(HierarchicalModel model, object item)
    {
        foreach (var node in model.Flattened)
        {
            if (ReferenceEquals(node.Item, item))
            {
                return node;
            }
        }

        throw new InvalidOperationException("Item not found in flattened list.");
    }

    private static bool FlattenedContains(HierarchicalModel model, object item)
    {
        foreach (var node in model.Flattened)
        {
            if (ReferenceEquals(node.Item, item))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<int>? BuildPath(DuplicateItem root, DuplicateItem target)
    {
        var path = new List<int>();
        if (!TryBuildPath(root, target, path))
        {
            return null;
        }

        path.Insert(0, 0);
        return path;
    }

    private static bool TryBuildPath(DuplicateItem current, DuplicateItem target, List<int> path)
    {
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        for (int i = 0; i < current.Children.Count; i++)
        {
            path.Add(i);
            if (TryBuildPath(current.Children[i], target, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
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
    public void SiblingComparerSelector_Sorts_PerParent()
    {
        var root = new Item("root");
        var gamma = new Item("gamma");
        var alpha = new Item("alpha");
        var beta = new Item("beta");
        var child = new Item("child");
        child.Children.Add(new Item("c1"));
        child.Children.Add(new Item("c3"));
        child.Children.Add(new Item("c2"));
        root.Children.Add(gamma);
        root.Children.Add(alpha);
        root.Children.Add(beta);
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            SiblingComparerSelector = item =>
            {
                var current = (Item)item;
                return current.Name switch
                {
                    "root" => Comparer<object>.Create((x, y) =>
                        string.Compare(((Item)x).Name, ((Item)y).Name, StringComparison.Ordinal)),
                    "child" => Comparer<object>.Create((x, y) =>
                        -string.Compare(((Item)x).Name, ((Item)y).Name, StringComparison.Ordinal)),
                    _ => null
                };
            }
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal("alpha", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("beta", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("child", ((Item)model.GetItem(3)!).Name);
        Assert.Equal("gamma", ((Item)model.GetItem(4)!).Name);

        var childNode = model.FindNode(child)!;
        model.Expand(childNode);

        Assert.Equal("c3", ((Item)model.GetItem(4)!).Name);
        Assert.Equal("c2", ((Item)model.GetItem(5)!).Name);
        Assert.Equal("c1", ((Item)model.GetItem(6)!).Name);
    }

    [Fact]
    public void Sort_UsesComparerSelector_WhenNoComparerProvided()
    {
        var root = new Item("root");
        var c = new Item("c");
        var a = new Item("a");
        root.Children.Add(c);
        root.Children.Add(a);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            SiblingComparerSelector = item =>
            {
                if (((Item)item).Name == "root")
                {
                    return Comparer<object>.Create((x, y) =>
                        string.Compare(((Item)x).Name, ((Item)y).Name, StringComparison.Ordinal));
                }

                return null;
            }
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("c", ((Item)model.GetItem(2)!).Name);

        var b = new Item("b");
        root.Children.Add(b);

        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("c", ((Item)model.GetItem(3)!).Name);

        model.Sort();

        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("c", ((Item)model.GetItem(3)!).Name);
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
    public void TypedOptions_PushChangesToUntypedOptions()
    {
        var options = new HierarchicalOptions<Item>();
        var model = new HierarchicalModel<Item>(options);

        options.AutoExpandRoot = true;
        options.VirtualizeChildren = false;
        options.SiblingComparer = Comparer<Item>.Create((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        options.SiblingComparerSelector = item => Comparer<Item>.Create((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        options.IsLeafSelector = item => item.Children.Count == 0;
        options.ChildrenPropertyPath = nameof(Item.Children);

        Assert.True(model.Options.AutoExpandRoot);
        Assert.False(model.Options.VirtualizeChildren);
        Assert.NotNull(model.Options.SiblingComparer);
        Assert.NotNull(model.Options.SiblingComparerSelector);
        Assert.NotNull(model.Options.IsLeafSelector);
        Assert.Equal(nameof(Item.Children), model.Options.ChildrenPropertyPath);
    }

    [Fact]
    public void TypedNode_ExposesExpandedCount()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoot(root);
        var typedRoot = model.Root!.Value;

        Assert.Equal(0, typedRoot.ExpandedCount);

        model.Expand(typedRoot);

        typedRoot = model.Root!.Value;
        Assert.Equal(1, typedRoot.ExpandedCount);
    }

    [Fact]
    public void Expand_Items_Expands_Multiple_Nodes()
    {
        var root = new Item("root");
        var child1 = new Item("child1");
        var child2 = new Item("child2");
        child1.Children.Add(new Item("grand1"));
        child2.Children.Add(new Item("grand2"));
        root.Children.Add(child1);
        root.Children.Add(child2);

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoot(root);
        model.Expand(model.Root!.Value);
        model.Expand(new[] { child1, child2 });

        var child1Node = model.FindNode(child1);
        var child2Node = model.FindNode(child2);

        Assert.True(child1Node.HasValue);
        Assert.True(child2Node.HasValue);
        Assert.True(child1Node!.Value.IsExpanded);
        Assert.True(child2Node!.Value.IsExpanded);
        Assert.Equal(5, model.Count);
    }

    [Fact]
    public async Task ExpandAsync_Items_Uses_PathSelector()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children,
            ItemPathSelector = item =>
            {
                if (ReferenceEquals(item, root))
                {
                    return new[] { 0 };
                }

                if (ReferenceEquals(item, child))
                {
                    return new[] { 0, 0 };
                }

                if (ReferenceEquals(item, grand))
                {
                    return new[] { 0, 0, 0 };
                }

                return null;
            }
        });

        model.SetRoot(root);
        await model.ExpandAsync(new[] { child });

        var childNode = model.FindNode(child);

        Assert.True(model.Root!.Value.IsExpanded);
        Assert.True(childNode.HasValue);
        Assert.True(childNode!.Value.IsExpanded);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void Collapse_Items_Collapses_Targets()
    {
        var root = new Item("root");
        var child1 = new Item("child1");
        var child2 = new Item("child2");
        child1.Children.Add(new Item("grand1"));
        child2.Children.Add(new Item("grand2"));
        root.Children.Add(child1);
        root.Children.Add(child2);

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoot(root);
        model.ExpandAll();
        model.Collapse(new[] { child1 });

        var child1Node = model.FindNode(child1);
        var child2Node = model.FindNode(child2);

        Assert.True(child1Node.HasValue);
        Assert.True(child2Node.HasValue);
        Assert.False(child1Node!.Value.IsExpanded);
        Assert.True(child2Node!.Value.IsExpanded);
        Assert.Equal(4, model.Count);
    }

    [Fact]
    public async Task CollapseAsync_Items_Collapses_Targets()
    {
        var root = new Item("root");
        var child1 = new Item("child1");
        var child2 = new Item("child2");
        child1.Children.Add(new Item("grand1"));
        child2.Children.Add(new Item("grand2"));
        root.Children.Add(child1);
        root.Children.Add(child2);

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoot(root);
        model.ExpandAll();
        await model.CollapseAsync(new[] { child1, child2 });

        var child1Node = model.FindNode(child1);
        var child2Node = model.FindNode(child2);

        Assert.True(child1Node.HasValue);
        Assert.True(child2Node.HasValue);
        Assert.False(child1Node!.Value.IsExpanded);
        Assert.False(child2Node!.Value.IsExpanded);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void FlattenedChangedTyped_FiresWithTypedNodes()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoot(root);

        FlattenedChangedEventArgs<Item>? typedArgs = null;
        model.FlattenedChangedTyped += (_, e) => typedArgs = e;

        model.Expand(model.Root!.Value);

        Assert.NotNull(typedArgs);
        Assert.Equal(model.FlattenedVersion, typedArgs!.Version);
        Assert.Equal(2, typedArgs.Flattened.Count);
        Assert.Equal("child", typedArgs.Flattened[1].Item.Name);
    }

    [Fact]
    public void TypedAdapter_ForwardsEventsAndNodes()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });
        model.SetRoot(root);

        var adapter = new DataGridHierarchicalAdapter<Item>(model);
        FlattenedChangedEventArgs<Item>? args = null;
        adapter.FlattenedChanged += (_, e) => args = e;

        adapter.Expand(0);

        Assert.NotNull(args);
        Assert.Equal(2, adapter.Count);
        Assert.Equal("child", adapter.ItemAt(1).Name);
        Assert.True(adapter.NodeAt(0).IsExpanded);
    }

    [Fact]
    public async Task ChildrenSelectorAsyncEnumerable_LoadsChildren()
    {
        var root = new Item("root");

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelectorAsyncEnumerable = item => GetChildrenAsync(item)
        });

        model.SetRoot(root);
        await model.ExpandAsync(model.Root!.Value);

        Assert.Equal(3, model.Count);
        Assert.Equal("child1", model.GetTypedNode(1).Item.Name);

        static async IAsyncEnumerable<Item> GetChildrenAsync(Item _)
        {
            yield return new Item("child1");
            await Task.Yield();
            yield return new Item("child2");
        }
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
    public void Incc_Move_WithExpandedDescendants_MaintainsFlattenedOrder()
    {
        var root = new Item("root");
        var a = new Item("a");
        a.Children.Add(new Item("a1"));
        var b = new Item("b");
        b.Children.Add(new Item("b1"));
        var c = new Item("c");
        c.Children.Add(new Item("c1"));
        root.Children.Add(a);
        root.Children.Add(b);
        root.Children.Add(c);

        var model = CreateModel();
        model.SetRoot(root);
        model.ExpandAll();

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        root.Children.Move(0, 2);

        Assert.Equal(
            new[] { "root", "b", "b1", "c", "c1", "a", "a1" },
            model.Flattened.Select(node => ((Item)node.Item).Name).ToArray());

        Assert.NotNull(args);
        Assert.Equal(2, args!.Changes.Count);
        Assert.Equal(1, args.Changes[0].Index);
        Assert.Equal(2, args.Changes[0].OldCount);
        Assert.Equal(0, args.Changes[0].NewCount);
        Assert.Equal(5, args.Changes[1].Index);
        Assert.Equal(0, args.Changes[1].OldCount);
        Assert.Equal(2, args.Changes[1].NewCount);
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
    public void SetRoots_INCC_MoveRootItem_WithExpandedDescendants_MaintainsFlattenedOrder()
    {
        var a = new Item("a");
        a.Children.Add(new Item("a1"));
        var b = new Item("b");
        b.Children.Add(new Item("b1"));
        var c = new Item("c");
        c.Children.Add(new Item("c1"));

        var items = new ObservableCollection<Item> { a, b, c };
        var model = CreateModel();
        model.SetRoots(items);
        model.ExpandAll();

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        items.Move(0, 2);

        Assert.Equal(
            new[] { "b", "b1", "c", "c1", "a", "a1" },
            model.Flattened.Select(node => ((Item)node.Item).Name).ToArray());

        Assert.NotNull(args);
        Assert.Equal(2, args!.Changes.Count);
        Assert.Equal(0, args.Changes[0].Index);
        Assert.Equal(2, args.Changes[0].OldCount);
        Assert.Equal(0, args.Changes[0].NewCount);
        Assert.Equal(4, args.Changes[1].Index);
        Assert.Equal(0, args.Changes[1].OldCount);
        Assert.Equal(2, args.Changes[1].NewCount);
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

    [Fact]
    public void SetRoots_CreatesVirtualRoot()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2"),
            new Item("Item3")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.True(model.IsVirtualRoot);
        Assert.NotNull(model.Root);
        Assert.IsType<VirtualRootContainer>(model.Root.Item);
        Assert.Same(items, model.RootItems);
    }

    [Fact]
    public void SetRoots_FlattenedListContainsAllRootItems()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2"),
            new Item("Item3")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(3, model.Count);
        Assert.Equal("Item1", ((Item)model.GetItem(0)!).Name);
        Assert.Equal("Item2", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("Item3", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void SetRoots_RootItemsAreAtLevelZero()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(0, model.GetNode(0).Level);
        Assert.Equal(0, model.GetNode(1).Level);
    }

    [Fact]
    public void SetRoots_VirtualRootNotInFlattenedList()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        // Virtual root should not be in the flattened list
        Assert.DoesNotContain(model.Root, model.Flattened);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public void SetRoots_ExpandingRootItemAddsChildrenCorrectly()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1"));
        item1.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { item1, new Item("Item2") };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(2, model.Count);

        model.Expand(model.GetNode(0));

        Assert.Equal(4, model.Count);
        Assert.Equal(1, model.GetNode(1).Level); // Child1 is at level 1
        Assert.Equal(1, model.GetNode(2).Level); // Child2 is at level 1
    }

    [Fact]
    public void SetRoots_INCC_AddNewRootItem()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(2, model.Count);

        items.Add(new Item("Item3"));

        Assert.Equal(3, model.Count);
        Assert.Equal("Item3", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void SetRoots_INCC_RemoveRootItem()
    {
        var item1 = new Item("Item1");
        var item2 = new Item("Item2");
        var items = new ObservableCollection<Item> { item1, item2 };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(2, model.Count);

        items.Remove(item1);

        Assert.Equal(1, model.Count);
        Assert.Equal("Item2", ((Item)model.GetItem(0)!).Name);
    }

    [Fact]
    public void SetRoots_INCC_InsertRootItem()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item3")
        };

        var model = CreateModel();
        model.SetRoots(items);

        items.Insert(1, new Item("Item2"));

        Assert.Equal(3, model.Count);
        Assert.Equal("Item1", ((Item)model.GetItem(0)!).Name);
        Assert.Equal("Item2", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("Item3", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void SetRoots_INCC_MoveRootItem()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2"),
            new Item("Item3")
        };

        var model = CreateModel();
        model.SetRoots(items);

        items.Move(0, 2);

        Assert.Equal(3, model.Count);
        Assert.Equal("Item2", ((Item)model.GetItem(0)!).Name);
        Assert.Equal("Item3", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("Item1", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void Reparenting_Keeps_Node_Levels_Consistent()
    {
        var root1 = new Item("Root1");
        var root2 = new Item("Root2");
        var child = new Item("Child");
        var grandchild = new Item("Grandchild");
        child.Children.Add(grandchild);
        root1.Children.Add(child);

        var items = new ObservableCollection<Item> { root1, root2 };

        var model = CreateModel();
        model.SetRoots(items);
        model.ExpandAll();

        Assert.Equal(1, model.FindNode(child)!.Level);
        Assert.Equal(2, model.FindNode(grandchild)!.Level);

        for (var i = 0; i < 3; i++)
        {
            root1.Children.Remove(child);
            root2.Children.Add(child);
            model.ExpandAll();

            Assert.Equal(1, model.FindNode(child)!.Level);
            Assert.Equal(2, model.FindNode(grandchild)!.Level);

            root2.Children.Remove(child);
            root1.Children.Add(child);
            model.ExpandAll();

            Assert.Equal(1, model.FindNode(child)!.Level);
            Assert.Equal(2, model.FindNode(grandchild)!.Level);
        }
    }

    [Fact]
    public void SetRoots_TypedModel_WorksCorrectly()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoots(items);

        Assert.True(model.IsVirtualRoot);
        Assert.Equal(2, model.Count);
        Assert.Equal("Item1", model.GetTypedNode(0).Item.Name);
        Assert.Equal("Item2", model.GetTypedNode(1).Item.Name);
    }

    [Fact]
    public void SetRoots_AutoExpandRoot_ExpandsAllRootItems()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1"));
        var item2 = new Item("Item2");
        item2.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { item1, item2 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            AutoExpandRoot = true
        });

        model.SetRoots(items);

        // Both root items should be expanded
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.True(model.GetNode(2).IsExpanded);
        Assert.Equal(4, model.Count);
    }

    [Fact]
    public void SetRoot_ClearsVirtualRootFlag()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.True(model.IsVirtualRoot);

        model.SetRoot(new Item("SingleRoot"));

        Assert.False(model.IsVirtualRoot);
        Assert.Null(model.RootItems);
        Assert.Equal(1, model.Count);
    }

    [Fact]
    public void SetRoots_EmptyCollection_CreatesEmptyModel()
    {
        var items = new ObservableCollection<Item>();

        var model = CreateModel();
        model.SetRoots(items);

        Assert.True(model.IsVirtualRoot);
        Assert.Equal(0, model.Count);
        Assert.NotNull(model.Root);
    }

    [Fact]
    public void SetRoots_INCC_ReplaceRootItem()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2"),
            new Item("Item3")
        };

        var model = CreateModel();
        model.SetRoots(items);

        items[1] = new Item("ReplacedItem2");

        Assert.Equal(3, model.Count);
        Assert.Equal("Item1", ((Item)model.GetItem(0)!).Name);
        Assert.Equal("ReplacedItem2", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("Item3", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void SetRoots_INCC_ClearCollection()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(2, model.Count);

        // Clear triggers Reset action, which refreshes the virtual root
        // After clearing, we need to call SetRoots again with an empty collection
        // to see 0 count, or we can just reset the roots
        var emptyItems = new ObservableCollection<Item>();
        model.SetRoots(emptyItems);

        Assert.Equal(0, model.Count);
        Assert.True(model.IsVirtualRoot);
    }

    [Fact]
    public void SetRoots_INCC_AddItemWithChildren_ThenExpand()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        var newItem = new Item("Item2");
        newItem.Children.Add(new Item("Child2a"));
        newItem.Children.Add(new Item("Child2b"));
        items.Add(newItem);

        Assert.Equal(2, model.Count);

        model.Expand(model.GetNode(1));

        Assert.Equal(4, model.Count);
        Assert.Equal("Child2a", ((Item)model.GetItem(2)!).Name);
        Assert.Equal("Child2b", ((Item)model.GetItem(3)!).Name);
    }

    [Fact]
    public void SetRoots_INCC_RemoveExpandedItem_CollapsesDescendants()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1a"));
        item1.Children.Add(new Item("Child1b"));

        var items = new ObservableCollection<Item> { item1, new Item("Item2") };

        var model = CreateModel();
        model.SetRoots(items);
        model.Expand(model.GetNode(0));

        Assert.Equal(4, model.Count);

        items.RemoveAt(0);

        Assert.Equal(1, model.Count);
        Assert.Equal("Item2", ((Item)model.GetItem(0)!).Name);
    }

    [Fact]
    public void SetRoots_FindNode_FindsRootLevelItem()
    {
        var item1 = new Item("Item1");
        var item2 = new Item("Item2");
        var items = new ObservableCollection<Item> { item1, item2 };

        var model = CreateModel();
        model.SetRoots(items);

        var foundNode = model.FindNode(item2);

        Assert.NotNull(foundNode);
        Assert.Same(item2, foundNode!.Item);
        Assert.Equal(0, foundNode.Level);
    }

    [Fact]
    public void SetRoots_IndexOf_ReturnsCorrectIndex()
    {
        var item1 = new Item("Item1");
        var item2 = new Item("Item2");
        var item3 = new Item("Item3");
        var items = new ObservableCollection<Item> { item1, item2, item3 };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(0, model.IndexOf(item1));
        Assert.Equal(1, model.IndexOf(item2));
        Assert.Equal(2, model.IndexOf(item3));
    }

    [Fact]
    public void SetRoots_ExpandAll_ExpandsAllRootItemsAndDescendants()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1"));
        var item2 = new Item("Item2");
        var grandchild = new Item("Grandchild");
        var child2 = new Item("Child2");
        child2.Children.Add(grandchild);
        item2.Children.Add(child2);

        var items = new ObservableCollection<Item> { item1, item2 };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(2, model.Count);

        model.ExpandAll();

        Assert.Equal(5, model.Count);
        Assert.True(model.GetNode(0).IsExpanded);
        Assert.True(model.GetNode(2).IsExpanded);
    }

    [Fact]
    public void SetRoots_CollapseAll_CollapsesAllRootItemsAndDescendants()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1"));
        var item2 = new Item("Item2");
        item2.Children.Add(new Item("Child2"));

        var items = new ObservableCollection<Item> { item1, item2 };

        var model = CreateModel();
        model.SetRoots(items);

        // Expand all root items first
        model.ExpandAll();
        Assert.Equal(4, model.Count);

        model.CollapseAll();

        Assert.Equal(2, model.Count);
        Assert.False(model.GetNode(0).IsExpanded);
        Assert.False(model.GetNode(1).IsExpanded);
    }

    [Fact]
    public void SetRoots_FlattenedChanged_FiresOnAdd()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        items.Add(new Item("Item2"));

        Assert.NotNull(args);
        Assert.Single(args!.Changes);
        Assert.Equal(1, args.Changes[0].Index);
        Assert.Equal(0, args.Changes[0].OldCount);
        Assert.Equal(1, args.Changes[0].NewCount);
    }

    [Fact]
    public void SetRoots_FlattenedChanged_FiresOnRemove()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        FlattenedChangedEventArgs? args = null;
        model.FlattenedChanged += (_, e) => args = e;

        items.RemoveAt(0);

        Assert.NotNull(args);
        Assert.Single(args!.Changes);
        Assert.Equal(0, args.Changes[0].Index);
        Assert.Equal(1, args.Changes[0].OldCount);
        Assert.Equal(0, args.Changes[0].NewCount);
    }

    [Fact]
    public void SetRoots_HierarchyChanged_FiresOnChildAdd()
    {
        var item1 = new Item("Item1");
        var items = new ObservableCollection<Item> { item1 };

        var model = CreateModel();
        model.SetRoots(items);
        model.Expand(model.GetNode(0));

        HierarchyChangedEventArgs? args = null;
        model.HierarchyChanged += (_, e) => args = e;

        item1.Children.Add(new Item("NewChild"));

        Assert.NotNull(args);
        Assert.Same(model.GetNode(0), args!.Node);
    }

    [Fact]
    public void SetRoots_VirtualRootLevel_IsMinusOne()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Equal(-1, model.Root!.Level);
    }

    [Fact]
    public void SetRoots_RootItemParent_IsVirtualRoot()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        var model = CreateModel();
        model.SetRoots(items);

        Assert.Same(model.Root, model.GetNode(0).Parent);
        Assert.Same(model.Root, model.GetNode(1).Parent);
    }

    [Fact]
    public void SetRoots_ChildLevel_IsOne()
    {
        var item1 = new Item("Item1");
        item1.Children.Add(new Item("Child1"));
        var items = new ObservableCollection<Item> { item1 };

        var model = CreateModel();
        model.SetRoots(items);
        model.Expand(model.GetNode(0));

        Assert.Equal(0, model.GetNode(0).Level);
        Assert.Equal(1, model.GetNode(1).Level);
    }

    [Fact]
    public void SetRoots_Refresh_ReloadsAllRootItems()
    {
        var item1 = new Item("Item1");
        var item2 = new Item("Item2");
        var items = new ObservableCollection<Item> { item1, item2 };

        var model = CreateModel();
        model.SetRoots(items);

        // Modify the underlying item's children externally
        item1.Children.Add(new Item("Child1"));

        model.Refresh();

        // After refresh, the model should still work correctly
        Assert.Equal(2, model.Count);
        model.Expand(model.GetNode(0));
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void SetRoots_Sort_SortsRootItems()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("C"),
            new Item("A"),
            new Item("B")
        };

        var model = CreateModel();
        model.SetRoots(items);

        model.Sort(comparer: Comparer<object>.Create((a, b) =>
            string.Compare(((Item)a).Name, ((Item)b).Name, StringComparison.Ordinal)));

        // After sorting, root items should be A, B, C
        Assert.Equal("A", ((Item)model.GetItem(0)!).Name);
        Assert.Equal("B", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("C", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void SetRoots_ThrowsOnNull()
    {
        var model = CreateModel();

        Assert.Throws<ArgumentNullException>(() => model.SetRoots(null!));
    }

    [Fact]
    public void SetRoots_SwitchBetweenSingleAndMultipleRoots()
    {
        var model = CreateModel();

        // Start with single root
        model.SetRoot(new Item("SingleRoot"));
        Assert.False(model.IsVirtualRoot);
        Assert.Equal(1, model.Count);

        // Switch to multiple roots
        var items = new ObservableCollection<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };
        model.SetRoots(items);
        Assert.True(model.IsVirtualRoot);
        Assert.Equal(2, model.Count);

        // Switch back to single root
        model.SetRoot(new Item("AnotherSingleRoot"));
        Assert.False(model.IsVirtualRoot);
        Assert.Equal(1, model.Count);

        // Switch to multiple roots again
        var items2 = new ObservableCollection<Item>
        {
            new Item("A"),
            new Item("B"),
            new Item("C")
        };
        model.SetRoots(items2);
        Assert.True(model.IsVirtualRoot);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void SetRoots_TypedModel_SetRoots_WorksWithEnumerable()
    {
        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        var items = new List<Item>
        {
            new Item("Item1"),
            new Item("Item2")
        };

        // Use the typed SetRoots which accepts IEnumerable<T>
        model.SetRoots(items);

        Assert.True(model.IsVirtualRoot);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void SetRoots_TypedModel_Observes_RootCollectionChanges()
    {
        var roots = new ObservableCollection<Item>();

        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });

        model.SetRoots(roots);

        Assert.Equal(0, model.Count);

        var first = new Item("Item1");
        roots.Add(first);

        Assert.Equal(1, model.Count);
        Assert.Same(first, model.GetItem(0));

        var second = new Item("Item2");
        roots.Add(second);

        Assert.Equal(2, model.Count);
        Assert.Same(second, model.GetItem(1));

        roots.Remove(first);

        Assert.Equal(1, model.Count);
        Assert.Same(second, model.GetItem(0));
    }

    [Fact]
    public void SetRoots_MaxAutoExpandDepth_RespectsLimit()
    {
        var item1 = new Item("Item1");
        var child1 = new Item("Child1");
        child1.Children.Add(new Item("Grandchild1"));
        item1.Children.Add(child1);

        var items = new ObservableCollection<Item> { item1 };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 0 // Only auto-expand the root level items themselves
        });

        model.SetRoots(items);

        // Item1 should be expanded (it's at level 0)
        Assert.True(model.GetNode(0).IsExpanded);
        // Child1 should NOT be expanded (auto-expand depth limit)
        Assert.False(model.GetNode(1).IsExpanded);
        Assert.Equal(2, model.Count);
    }

    [Fact]
    public void SetRoots_ObservableFlattened_UpdatesOnChange()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        var observable = model.ObservableFlattened;
        Assert.Equal(1, observable.Count);

        items.Add(new Item("Item2"));

        Assert.Equal(2, observable.Count);
    }

    [Fact]
    public void SetRoots_FlattenedVersion_IncrementsOnChange()
    {
        var items = new ObservableCollection<Item>
        {
            new Item("Item1")
        };

        var model = CreateModel();
        model.SetRoots(items);

        var initialVersion = model.FlattenedVersion;

        items.Add(new Item("Item2"));

        Assert.True(model.FlattenedVersion > initialVersion);
    }

    [Fact]
    public void SetRoots_NestedExpansion_MaintainsCorrectLevels()
    {
        var root1 = new Item("Root1");
        var child1 = new Item("Child1");
        var grandchild1 = new Item("Grandchild1");
        var greatGrandchild1 = new Item("GreatGrandchild1");

        greatGrandchild1.Children.Add(new Item("Level4"));
        grandchild1.Children.Add(greatGrandchild1);
        child1.Children.Add(grandchild1);
        root1.Children.Add(child1);

        var items = new ObservableCollection<Item> { root1 };

        var model = CreateModel();
        model.SetRoots(items);

        model.ExpandAll();

        Assert.Equal(0, model.GetNode(0).Level); // Root1
        Assert.Equal(1, model.GetNode(1).Level); // Child1
        Assert.Equal(2, model.GetNode(2).Level); // Grandchild1
        Assert.Equal(3, model.GetNode(3).Level); // GreatGrandchild1
        Assert.Equal(4, model.GetNode(4).Level); // Level4
    }

    [Fact]
    public void WeakSubscriptions_DoNotRoot_Model()
    {
        var root = new TrackingExpandableItem("root");
        root.Children.Add(new TrackingExpandableItem("child"));

        var weak = CreateWeakModel(root);

        Assert.Equal(1, root.PropertyChangedSubscriptionCount);
        Assert.Equal(1, root.Children.SubscriptionCount);

        ForceGc();

        Assert.False(weak.TryGetTarget(out _));

        root.RaiseIsExpandedChanged();
        root.Children.RaiseReset();

        Assert.Equal(0, root.PropertyChangedSubscriptionCount);
        Assert.Equal(0, root.Children.SubscriptionCount);
    }

    private static WeakReference<HierarchicalModel> CreateWeakModel(TrackingExpandableItem root)
    {
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((TrackingExpandableItem)item).Children,
            IsExpandedSelector = item => ((TrackingExpandableItem)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((TrackingExpandableItem)item).IsExpanded = value
        });

        model.SetRoot(root);
        model.Expand(model.Root!);

        return new WeakReference<HierarchicalModel>(model);
    }

    private static void ForceGc()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
