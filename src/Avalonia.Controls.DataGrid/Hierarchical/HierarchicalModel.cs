// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Represents a change in the flattened list of visible nodes.
    /// </summary>
    public sealed class FlattenedChange
    {
        public FlattenedChange(int index, int oldCount, int newCount)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (oldCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(oldCount));
            }

            if (newCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newCount));
            }

            Index = index;
            OldCount = oldCount;
            NewCount = newCount;
        }

        public int Index { get; }

        public int OldCount { get; }

        public int NewCount { get; }
    }

    public class FlattenedChangedEventArgs : EventArgs
    {
        public FlattenedChangedEventArgs(IReadOnlyList<FlattenedChange> changes)
        {
            Changes = changes ?? throw new ArgumentNullException(nameof(changes));
        }

        public IReadOnlyList<FlattenedChange> Changes { get; }
    }

    public class HierarchicalNodeEventArgs : EventArgs
    {
        public HierarchicalNodeEventArgs(HierarchicalNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public HierarchicalNode Node { get; }
    }

    public class HierarchicalNodeLoadFailedEventArgs : HierarchicalNodeEventArgs
    {
        public HierarchicalNodeLoadFailedEventArgs(HierarchicalNode node, Exception error)
            : base(node)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public Exception Error { get; }
    }

    /// <summary>
    /// Core contract for hierarchical models powering ProDataGrid.
    /// </summary>
    public interface IHierarchicalModel
    {
        /// <summary>
        /// Sets the root item that will act as the entry point for the hierarchy.
        /// </summary>
        /// <param name="rootItem">Root item.</param>
        void SetRoot(object rootItem);

        /// <summary>
        /// Gets configuration options for the model.
        /// </summary>
        HierarchicalOptions Options { get; }

        /// <summary>
        /// Gets the current root node, if any.
        /// </summary>
        HierarchicalNode? Root { get; }

        /// <summary>
        /// Gets the list of visible nodes in display order.
        /// </summary>
        IReadOnlyList<HierarchicalNode> Flattened { get; }

        /// <summary>
        /// Gets the total number of visible nodes.
        /// </summary>
        int Count { get; }

        event EventHandler<FlattenedChangedEventArgs>? FlattenedChanged;

        event EventHandler<HierarchicalNodeEventArgs>? NodeExpanded;

        event EventHandler<HierarchicalNodeEventArgs>? NodeCollapsed;

        event EventHandler<HierarchicalNodeEventArgs>? NodeLoading;

        event EventHandler<HierarchicalNodeEventArgs>? NodeLoaded;

        event EventHandler<HierarchicalNodeLoadFailedEventArgs>? NodeLoadFailed;

        /// <summary>
        /// Retrieves the item at the given visible index.
        /// </summary>
        /// <param name="index">Visible index.</param>
        /// <returns>Item at the index.</returns>
        object? GetItem(int index);

        /// <summary>
        /// Retrieves the node at the given visible index.
        /// </summary>
        /// <param name="index">Visible index.</param>
        /// <returns>Node at the index.</returns>
        HierarchicalNode GetNode(int index);

        /// <summary>
        /// Expands a node and realizes its visible children.
        /// </summary>
        /// <param name="node">Node to expand.</param>
        void Expand(HierarchicalNode node);

        /// <summary>
        /// Collapses a node, removing its descendants from the flattened list.
        /// </summary>
        /// <param name="node">Node to collapse.</param>
        void Collapse(HierarchicalNode node);

        /// <summary>
        /// Toggles expansion state of a node.
        /// </summary>
        /// <param name="node">Node to toggle.</param>
        void Toggle(HierarchicalNode node);

        /// <summary>
        /// Refreshes children for the given node (or root when null).
        /// </summary>
        /// <param name="node">Node to refresh; null for root.</param>
        void Refresh(HierarchicalNode? node = null);

        /// <summary>
        /// Finds the node wrapping the given item, if realized.
        /// </summary>
        /// <param name="item">Item to search for.</param>
        /// <returns>Node instance or null.</returns>
        HierarchicalNode? FindNode(object item);

        /// <summary>
        /// Sorts siblings under the specified node (or root when null) using the provided comparer or <see cref="HierarchicalOptions.SiblingComparer"/>.
        /// Only orders siblings within the same parent; cross-level sorting is not performed.
        /// </summary>
        /// <param name="node">Parent node whose children should be sorted; null for root.</param>
        /// <param name="comparer">Comparer to apply; defaults to <see cref="HierarchicalOptions.SiblingComparer"/>.</param>
        /// <param name="recursive">When true, recursively sorts expanded descendants.</param>
        void Sort(HierarchicalNode? node = null, IComparer<object>? comparer = null, bool recursive = true);

        /// <summary>
        /// Expands the specified node (or root when null) and all descendants up to the provided depth.
        /// Depth is relative to the starting node; null expands the full subtree.
        /// </summary>
        /// <param name="node">Starting node; null for root.</param>
        /// <param name="maxDepth">Maximum depth to expand (inclusive), or null for no limit.</param>
        void ExpandAll(HierarchicalNode? node = null, int? maxDepth = null);

        /// <summary>
        /// Collapses the specified node's descendants (or the root subtree) starting at the provided depth.
        /// Depth is relative to the starting node; null collapses the starting node and all descendants.
        /// </summary>
        /// <param name="node">Starting node; null for root.</param>
        /// <param name="minDepth">Depth at which collapse begins (inclusive); null collapses from the starting node.</param>
        void CollapseAll(HierarchicalNode? node = null, int? minDepth = null);
    }

    /// <summary>
    /// Factory hook to allow replacing the default hierarchical model.
    /// </summary>
    public interface IDataGridHierarchicalModelFactory
    {
        IHierarchicalModel Create();
    }

    /// <summary>
    /// Default hierarchical model implementation (initial scaffolding).
    /// </summary>
    public class HierarchicalModel : IHierarchicalModel
    {
        private readonly List<HierarchicalNode> _flattened;
        private readonly IReadOnlyList<HierarchicalNode> _flattenedView;
        private readonly Dictionary<(Type, string), Func<object, object?>> _propertyPathCache;

        public HierarchicalModel(HierarchicalOptions? options = null)
        {
            Options = options ?? new HierarchicalOptions();
            _flattened = new List<HierarchicalNode>();
            _flattenedView = _flattened.AsReadOnly();
            _propertyPathCache = new Dictionary<(Type, string), Func<object, object?>>();
        }

        public HierarchicalOptions Options { get; }

        public HierarchicalNode? Root { get; private set; }

        public IReadOnlyList<HierarchicalNode> Flattened => _flattenedView;

        public int Count => _flattened.Count;

        public event EventHandler<FlattenedChangedEventArgs>? FlattenedChanged;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeExpanded;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeCollapsed;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeLoading;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeLoaded;

        public event EventHandler<HierarchicalNodeLoadFailedEventArgs>? NodeLoadFailed;

        public void SetRoot(object rootItem)
        {
            if (rootItem == null)
            {
                throw new ArgumentNullException(nameof(rootItem));
            }

            var root = new HierarchicalNode(rootItem, parent: null, level: 0, isLeaf: DetermineInitialLeaf(rootItem));
            SetRoot(root);

            if (Options.AutoExpandRoot && WithinAutoExpandDepth(0))
            {
                AutoExpand(root, 0);
            }
        }

        public object? GetItem(int index)
        {
            return GetNode(index).Item;
        }

        public HierarchicalNode GetNode(int index)
        {
            if (index < 0 || index >= _flattened.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _flattened[index];
        }

        public void Expand(HierarchicalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.IsExpanded)
            {
                return;
            }

            EnsureChildrenMaterialized(node);

            var parentIndex = _flattened.IndexOf(node);
            var inserted = 0;

            if (parentIndex >= 0 && !node.IsLeaf)
            {
                inserted = InsertVisibleChildren(node, parentIndex + 1);
                if (inserted > 0)
                {
                    OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, 0, inserted) });
                }
            }

            node.IsExpanded = true;
            RecalculateExpandedCountsFrom(node);
            OnNodeExpanded(node);
        }

        public void Collapse(HierarchicalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.IsLeaf)
            {
                return;
            }

            if (!node.IsExpanded)
            {
                return;
            }

            var parentIndex = _flattened.IndexOf(node);
            if (parentIndex >= 0)
            {
                var removed = RemoveVisibleDescendants(node, parentIndex, detachDescendants: false);
                if (removed > 0)
                {
                    OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, removed, 0) });
                }
            }

            node.IsExpanded = false;
            node.ExpandedCount = 0;
            RecalculateExpandedCountsUpwards(node.Parent);
            OnNodeCollapsed(node);

            if (Options.VirtualizeChildren)
            {
                DematerializeDescendants(node);
            }
        }

        public void Toggle(HierarchicalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.IsExpanded)
            {
                Collapse(node);
            }
            else
            {
                Expand(node);
            }
        }

        public void Refresh(HierarchicalNode? node = null)
        {
            var target = node ?? Root;
            if (target == null)
            {
                return;
            }

            var parentIndex = _flattened.IndexOf(target);
            var wasExpanded = target.IsExpanded;

            var oldChildren = target.MutableChildren.ToArray();
            foreach (var child in oldChildren)
            {
                DetachHierarchy(child);
            }

            DetachChildrenNotifier(target);
            target.MutableChildren.Clear();
            target.IsLeaf = false;
            target.LoadError = null;
            EnsureChildrenMaterialized(target, forceReload: true);

            var removedCount = 0;
            var insertedCount = 0;

            if (wasExpanded && parentIndex >= 0)
            {
                removedCount = RemoveVisibleDescendants(target, parentIndex, detachDescendants: false);
            }

            if (wasExpanded && parentIndex >= 0 && !target.IsLeaf)
            {
                insertedCount = InsertVisibleChildren(target, parentIndex + 1);
            }

            if (wasExpanded && parentIndex >= 0 && (removedCount > 0 || insertedCount > 0))
            {
                OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, removedCount, insertedCount) });
            }

            RecalculateExpandedCountsFrom(target);
        }

        public HierarchicalNode? FindNode(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            for (int i = 0; i < _flattened.Count; i++)
            {
                if (Equals(_flattened[i].Item, item))
                {
                    return _flattened[i];
                }
            }

            return null;
        }

        public void Sort(HierarchicalNode? node = null, IComparer<object>? comparer = null, bool recursive = true)
        {
            var target = node ?? Root;
            if (target == null)
            {
                return;
            }

            var sortComparer = comparer ?? Options.SiblingComparer;
            if (sortComparer == null)
            {
                return;
            }

            EnsureChildrenMaterialized(target);
            SortChildren(target, sortComparer, recursive);

            if (target.IsExpanded)
            {
                var parentIndex = _flattened.IndexOf(target);
                if (parentIndex >= 0)
                {
                    var removed = RemoveVisibleDescendants(target, parentIndex, detachDescendants: false);
                    var inserted = InsertVisibleChildren(target, parentIndex + 1);
                    if (removed > 0 || inserted > 0)
                    {
                        OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, removed, inserted) });
                    }
                }
            }
        }

        public void ExpandAll(HierarchicalNode? node = null, int? maxDepth = null)
        {
            var start = node ?? Root;
            if (start == null)
            {
                return;
            }

            var limit = maxDepth ?? int.MaxValue;
            var stack = new Stack<(HierarchicalNode Node, int Depth)>();
            stack.Push((start, 0));

            while (stack.Count > 0)
            {
                var (current, depth) = stack.Pop();
                if (depth > limit)
                {
                    continue;
                }

                Expand(current);

                if (current.IsLeaf || depth >= limit)
                {
                    continue;
                }

                EnsureChildrenMaterialized(current);
                var children = current.Children;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    stack.Push((children[i], depth + 1));
                }
            }
        }

        public void CollapseAll(HierarchicalNode? node = null, int? minDepth = null)
        {
            var start = node ?? Root;
            if (start == null)
            {
                return;
            }

            var targetDepth = minDepth ?? 0;
            var stack = new Stack<(HierarchicalNode Node, int Depth, bool Visited)>();
            stack.Push((start, 0, false));

            while (stack.Count > 0)
            {
                var (current, depth, visited) = stack.Pop();

                if (!visited)
                {
                    if (current.IsExpanded)
                    {
                        EnsureChildrenMaterialized(current);
                        var children = current.Children;
                        for (int i = children.Count - 1; i >= 0; i--)
                        {
                            stack.Push((children[i], depth + 1, false));
                        }
                    }

                    stack.Push((current, depth, true));
                    continue;
                }

                if (depth >= targetDepth && current.IsExpanded)
                {
                    Collapse(current);
                }
            }
        }

        protected virtual void OnFlattenedChanged(IReadOnlyList<FlattenedChange> changes)
        {
            FlattenedChanged?.Invoke(this, new FlattenedChangedEventArgs(changes));
        }

        protected virtual void OnNodeExpanded(HierarchicalNode node)
        {
            NodeExpanded?.Invoke(this, new HierarchicalNodeEventArgs(node));
        }

        protected virtual void OnNodeCollapsed(HierarchicalNode node)
        {
            NodeCollapsed?.Invoke(this, new HierarchicalNodeEventArgs(node));
        }

        protected virtual void OnNodeLoading(HierarchicalNode node)
        {
            NodeLoading?.Invoke(this, new HierarchicalNodeEventArgs(node));
        }

        protected virtual void OnNodeLoaded(HierarchicalNode node)
        {
            NodeLoaded?.Invoke(this, new HierarchicalNodeEventArgs(node));
        }

        protected virtual void OnNodeLoadFailed(HierarchicalNode node, Exception error)
        {
            NodeLoadFailed?.Invoke(this, new HierarchicalNodeLoadFailedEventArgs(node, error));
        }

        internal void SetRoot(HierarchicalNode root, bool rebuildFlattened = true)
        {
            if (Root != null && !ReferenceEquals(Root, root))
            {
                DetachHierarchy(Root);
            }

            Root = root ?? throw new ArgumentNullException(nameof(root));
            Root.ExpandedCount = 0;

            if (rebuildFlattened)
            {
                ReplaceFlattened(new[] { root });
            }
        }

        internal void ReplaceFlattened(IEnumerable<HierarchicalNode> nodes, bool notify = true)
        {
            var oldCount = _flattened.Count;
            _flattened.Clear();
            _flattened.AddRange(nodes ?? Array.Empty<HierarchicalNode>());

            if (notify)
            {
                OnFlattenedChanged(new[] { new FlattenedChange(0, oldCount, _flattened.Count) });
            }
        }

        private void AutoExpand(HierarchicalNode node, int depth)
        {
            Expand(node);

            foreach (var child in node.Children)
            {
                if (WithinAutoExpandDepth(depth + 1))
                {
                    AutoExpand(child, depth + 1);
                }
            }
        }

        private bool WithinAutoExpandDepth(int depth)
        {
            if (!Options.AutoExpandRoot)
            {
                return false;
            }

            if (Options.MaxAutoExpandDepth == null)
            {
                return true;
            }

            return depth <= Options.MaxAutoExpandDepth.Value;
        }

        private int InsertVisibleChildren(HierarchicalNode parent, int insertIndex)
        {
            var buffer = new List<HierarchicalNode>();
            CollectVisibleChildren(parent, buffer);

            if (buffer.Count > 0)
            {
                _flattened.InsertRange(insertIndex, buffer);
            }

            return buffer.Count;
        }

        private void CollectVisibleChildren(HierarchicalNode parent, List<HierarchicalNode> buffer)
        {
            foreach (var child in parent.Children)
            {
                buffer.Add(child);

                if (child.IsExpanded)
                {
                    EnsureChildrenMaterialized(child);
                    CollectVisibleChildren(child, buffer);
                }
            }
        }

        private int CountVisibleDescendantsRecursive(HierarchicalNode node)
        {
            int count = 0;
            foreach (var child in node.Children)
            {
                count++;
                if (child.IsExpanded)
                {
                    EnsureChildrenMaterialized(child);
                    count += CountVisibleDescendantsRecursive(child);
                }
            }

            return count;
        }

        private int CountVisibleDescendantsInFlattened(HierarchicalNode node, int parentIndex)
        {
            int count = 0;
            var level = node.Level;

            for (int i = parentIndex + 1; i < _flattened.Count; i++)
            {
                var current = _flattened[i];
                if (current.Level <= level)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private int RemoveVisibleDescendants(HierarchicalNode node, int parentIndex, bool detachDescendants)
        {
            var removeStart = parentIndex + 1;
            var removeCount = CountVisibleDescendantsInFlattened(node, parentIndex);
            if (removeCount <= 0)
            {
                return 0;
            }

            if (detachDescendants)
            {
                var removedNodes = _flattened.GetRange(removeStart, removeCount);
                foreach (var removed in removedNodes)
                {
                    DetachHierarchy(removed);
                }
            }

            _flattened.RemoveRange(removeStart, removeCount);
            return removeCount;
        }

        private int GetVisibleOffsetForChildIndex(HierarchicalNode parent, int childIndex)
        {
            int offset = 0;
            var children = parent.MutableChildren;
            var capped = Math.Min(childIndex, children.Count);

            for (int i = 0; i < capped; i++)
            {
                offset++;

                if (children[i].IsExpanded)
                {
                    offset += CountVisibleDescendantsRecursive(children[i]);
                }
            }

            return offset;
        }

        private void OnChildrenCollectionChanged(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleChildrenAdded(parent, e);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    HandleChildrenRemoved(parent, e);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Refresh(parent);
                    break;
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    Refresh(parent);
                    break;
            }
        }

        private void HandleChildrenAdded(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null || e.NewItems.Count == 0)
            {
                return;
            }

            var insertIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : parent.MutableChildren.Count;
            insertIndex = Math.Min(insertIndex, parent.MutableChildren.Count);

            var visibleOffset = GetVisibleOffsetForChildIndex(parent, insertIndex);
            var newNodes = new List<HierarchicalNode>();
            foreach (var item in e.NewItems)
            {
                if (item == null)
                {
                    continue;
                }

                var childNode = new HierarchicalNode(
                    item,
                    parent,
                    parent.Level + 1,
                    isLeaf: DetermineInitialLeaf(item));
                newNodes.Add(childNode);
            }

            if (newNodes.Count == 0)
            {
                return;
            }

            parent.MutableChildren.InsertRange(insertIndex, newNodes);
            parent.IsLeaf = parent.MutableChildren.Count == 0;

            if (parent.IsExpanded)
            {
                var parentIndex = _flattened.IndexOf(parent);
                if (parentIndex >= 0)
                {
                    var flattenedIndex = parentIndex + 1 + visibleOffset;
                    var visibleNodes = new List<HierarchicalNode>();
                    foreach (var child in newNodes)
                    {
                        visibleNodes.Add(child);
                        if (child.IsExpanded)
                        {
                            EnsureChildrenMaterialized(child);
                            CollectVisibleChildren(child, visibleNodes);
                        }
                    }

                    if (visibleNodes.Count > 0)
                    {
                        _flattened.InsertRange(flattenedIndex, visibleNodes);
                        OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, 0, visibleNodes.Count) });
                    }
                }
            }

            RecalculateExpandedCountsFrom(parent);
        }

        private void HandleChildrenRemoved(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems == null || e.OldItems.Count == 0)
            {
                return;
            }

            var removeIndex = e.OldStartingIndex >= 0 ? e.OldStartingIndex : 0;
            removeIndex = Math.Min(removeIndex, parent.MutableChildren.Count);
            var removeCount = Math.Min(e.OldItems.Count, parent.MutableChildren.Count - removeIndex);

            var visibleOffset = GetVisibleOffsetForChildIndex(parent, removeIndex);
            var removedNodes = new List<HierarchicalNode>();

            for (int i = 0; i < removeCount; i++)
            {
                if (removeIndex < parent.MutableChildren.Count)
                {
                    var child = parent.MutableChildren[removeIndex];
                    removedNodes.Add(child);
                    parent.MutableChildren.RemoveAt(removeIndex);
                }
            }

            parent.IsLeaf = parent.MutableChildren.Count == 0;

            if (!parent.IsExpanded || removedNodes.Count == 0)
            {
                foreach (var removed in removedNodes)
                {
                    DetachHierarchy(removed);
                }
                RecalculateExpandedCountsFrom(parent);
                return;
            }

            var parentIndex = _flattened.IndexOf(parent);
            if (parentIndex < 0)
            {
                foreach (var removed in removedNodes)
                {
                    DetachHierarchy(removed);
                }
                RecalculateExpandedCountsFrom(parent);
                return;
            }

            var flattenedIndex = parentIndex + 1 + visibleOffset;
            var totalRemoved = 0;
            foreach (var child in removedNodes)
            {
                totalRemoved += 1;
                if (child.IsExpanded)
                {
                    totalRemoved += CountVisibleDescendantsRecursive(child);
                }
            }

            if (totalRemoved > 0)
            {
                _flattened.RemoveRange(flattenedIndex, totalRemoved);
                OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, totalRemoved, 0) });
            }

            foreach (var removed in removedNodes)
            {
                DetachHierarchy(removed);
            }

            RecalculateExpandedCountsFrom(parent);
        }

        private void AttachChildrenNotifier(HierarchicalNode node, IEnumerable children)
        {
            if (children is INotifyCollectionChanged notifier)
            {
                DetachChildrenNotifier(node);

                NotifyCollectionChangedEventHandler handler = (s, e) => OnChildrenCollectionChanged(node, e);
                notifier.CollectionChanged += handler;
                node.ChildrenNotifier = notifier;
                node.ChildrenChangedHandler = handler;
            }
        }

        private void DetachChildrenNotifier(HierarchicalNode node)
        {
            if (node.ChildrenNotifier != null && node.ChildrenChangedHandler != null)
            {
                node.ChildrenNotifier.CollectionChanged -= node.ChildrenChangedHandler;
            }

            node.ChildrenNotifier = null;
            node.ChildrenChangedHandler = null;
        }

        private void DetachHierarchy(HierarchicalNode node)
        {
            DetachChildrenNotifier(node);

            foreach (var child in node.Children)
            {
                DetachHierarchy(child);
            }
        }

        private void DematerializeDescendants(HierarchicalNode node)
        {
            foreach (var child in node.MutableChildren)
            {
                child.IsExpanded = false;
                DematerializeDescendants(child);
            }

            DetachChildrenNotifier(node);
            node.ChildrenSource = null;
            node.MutableChildren.Clear();
            node.HasMaterializedChildren = false;
            node.IsLeaf = false;
        }

        private IReadOnlyList<HierarchicalNode> EnsureChildrenMaterialized(HierarchicalNode node, bool forceReload = false)
        {
            if (node.HasMaterializedChildren && node.MutableChildren.Count > 0 && !forceReload)
            {
                return node.Children;
            }

            node.IsLoading = true;
            node.LoadError = null;
            OnNodeLoading(node);

            try
            {
                if (HasReachedMaxDepth(node))
                {
                    node.ChildrenSource = null;
                    DetachChildrenNotifier(node);
                    foreach (var child in node.MutableChildren)
                    {
                        DetachHierarchy(child);
                    }
                    node.MutableChildren.Clear();
                    node.HasMaterializedChildren = true;
                    node.IsLeaf = true;
                    OnNodeLoaded(node);
                    return node.Children;
                }

                if (forceReload && node.MutableChildren.Count > 0)
                {
                    foreach (var child in node.MutableChildren)
                    {
                        DetachHierarchy(child);
                    }
                }

                node.MutableChildren.Clear();
                var children = ResolveChildren(node.Item);

                if (children == null)
                {
                    node.ChildrenSource = null;
                    DetachChildrenNotifier(node);
                    node.IsLeaf = true;
                    node.HasMaterializedChildren = true;
                    OnNodeLoaded(node);
                    return node.Children;
                }

                node.ChildrenSource = children;
                AttachChildrenNotifier(node, children);

                foreach (var childItem in children)
                {
                    if (childItem == null)
                    {
                        continue;
                    }

                    if (CreatesCycle(node, childItem))
                    {
                        OnNodeLoadFailed(node, new InvalidOperationException("Cycle detected in hierarchical data."));
                        continue;
                    }

                    var childNode = new HierarchicalNode(
                        childItem,
                        node,
                        node.Level + 1,
                        isLeaf: DetermineInitialLeaf(childItem));
                    node.MutableChildren.Add(childNode);
                }

                if (Options.SiblingComparer != null)
                {
                    node.MutableChildren.Sort((x, y) => Options.SiblingComparer!.Compare(x.Item, y.Item));
                }

                node.HasMaterializedChildren = true;
                node.IsLeaf = node.MutableChildren.Count == 0;
                OnNodeLoaded(node);

                return node.Children;
            }
            catch (Exception ex)
            {
                node.ChildrenSource = null;
                DetachChildrenNotifier(node);
                node.IsLeaf = true;
                node.HasMaterializedChildren = false;
                node.LoadError = ex;
                OnNodeLoadFailed(node, ex);
                return node.Children;
            }
            finally
            {
                node.IsLoading = false;
            }
        }

        private bool DetermineInitialLeaf(object item)
        {
            if (item is null)
            {
                return true;
            }

            if (Options.IsLeafSelector != null)
            {
                try
                {
                    return Options.IsLeafSelector(item);
                }
                catch
                {
                    // ignore selector errors and fall through
                }
            }

            return false;
        }

        private IEnumerable? ResolveChildren(object item)
        {
            if (item == null)
            {
                return null;
            }

            if (Options.ChildrenSelector != null)
            {
                return Options.ChildrenSelector(item);
            }

            if (Options.ItemsSelector != null)
            {
                return Options.ItemsSelector(item);
            }

            if (!string.IsNullOrEmpty(Options.ChildrenPropertyPath))
            {
                var value = GetPropertyPathValue(item, Options.ChildrenPropertyPath!);
                return value as IEnumerable;
            }

            throw new InvalidOperationException("Provide ChildrenSelector, ItemsSelector, or ChildrenPropertyPath to resolve children.");
        }

        private void SortChildren(HierarchicalNode parent, IComparer<object> comparer, bool recursive)
        {
            parent.MutableChildren.Sort((x, y) => comparer.Compare(x.Item, y.Item));

            if (!recursive)
            {
                return;
            }

            foreach (var child in parent.MutableChildren)
            {
                if (child.IsExpanded)
                {
                    EnsureChildrenMaterialized(child);
                    SortChildren(child, comparer, recursive);
                }
            }
        }

        private bool HasReachedMaxDepth(HierarchicalNode node)
        {
            if (Options.MaxDepth == null)
            {
                return false;
            }

            return node.Level >= Options.MaxDepth.Value;
        }

        private bool CreatesCycle(HierarchicalNode parent, object childItem)
        {
            var current = parent;
            while (current != null)
            {
                if (ReferenceEquals(current.Item, childItem))
                {
                    return true;
                }

                current = current.Parent!;
            }

            return false;
        }

        private object? GetPropertyPathValue(object target, string propertyPath)
        {
            var key = (target.GetType(), propertyPath);

            if (!_propertyPathCache.TryGetValue(key, out var accessor))
            {
                accessor = CreatePropertyPathAccessor(target.GetType(), propertyPath);
                _propertyPathCache[key] = accessor;
            }

            return accessor(target);
        }

        private static Func<object, object?> CreatePropertyPathAccessor(Type targetType, string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                throw new ArgumentException("Property path cannot be null or whitespace.", nameof(propertyPath));
            }

            var parts = propertyPath.Split('.');
            var properties = new PropertyInfo[parts.Length];
            var currentType = targetType;

            for (int i = 0; i < parts.Length; i++)
            {
                var propertyName = parts[i].Trim();
                var property = currentType.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"Property '{propertyName}' was not found on type '{currentType.FullName}'.");
                }

                properties[i] = property;
                currentType = property.PropertyType;
            }

            return instance =>
            {
                object? current = instance;
                foreach (var property in properties)
                {
                    if (current == null)
                    {
                        return null;
                    }

                    current = property.GetValue(current);
                }

                return current;
            };
        }

        private void RecalculateExpandedCountsFrom(HierarchicalNode node)
        {
            if (node == null)
            {
                return;
            }

            RecalculateExpandedCountRecursive(node);
            RecalculateExpandedCountsUpwards(node.Parent);
        }

        private int RecalculateExpandedCountRecursive(HierarchicalNode node)
        {
            if (!node.IsExpanded || node.IsLeaf)
            {
                node.ExpandedCount = 0;
                return 0;
            }

            EnsureChildrenMaterialized(node);

            var total = 0;
            foreach (var child in node.Children)
            {
                var childDescendants = 0;
                if (child.IsExpanded && !child.IsLeaf)
                {
                    childDescendants = RecalculateExpandedCountRecursive(child);
                }
                else
                {
                    child.ExpandedCount = 0;
                }

                total += 1 + childDescendants;
            }

            node.ExpandedCount = total;
            return total;
        }

        private void RecalculateExpandedCountsUpwards(HierarchicalNode? node)
        {
            var current = node;
            while (current != null)
            {
                if (!current.IsExpanded || current.IsLeaf)
                {
                    current.ExpandedCount = 0;
                    current = current.Parent;
                    continue;
                }

                EnsureChildrenMaterialized(current);

                int total = 0;
                foreach (var child in current.Children)
                {
                    total += 1 + (child.IsExpanded ? child.ExpandedCount : 0);
                }

                current.ExpandedCount = total;
                current = current.Parent;
            }
        }
    }
}
