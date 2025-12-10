// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;

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

    /// <summary>
    /// Represents a mapping between the previous flattened list and the current one.
    /// </summary>
    public sealed class FlattenedIndexMap
    {
        private readonly IReadOnlyList<FlattenedChange> _changes;
        private readonly IReadOnlyDictionary<int, int>? _explicitMap;

        internal FlattenedIndexMap(int oldCount, int newCount, IReadOnlyList<FlattenedChange> changes, IReadOnlyDictionary<int, int>? explicitMap = null)
        {
            OldCount = oldCount;
            NewCount = newCount;
            _changes = changes ?? Array.Empty<FlattenedChange>();
            _explicitMap = explicitMap;
        }

        /// <summary>
        /// Gets the number of items in the flattened list prior to the change.
        /// </summary>
        public int OldCount { get; }

        /// <summary>
        /// Gets the number of items in the flattened list after the change.
        /// </summary>
        public int NewCount { get; }

        /// <summary>
        /// Maps an index from the previous flattened view to the current view. Returns -1 when the
        /// index was removed or when counts are unavailable.
        /// </summary>
        public int MapOldIndexToNew(int oldIndex)
        {
            if (_explicitMap != null && _explicitMap.TryGetValue(oldIndex, out var mapped))
            {
                return mapped >= 0 && mapped < NewCount ? mapped : -1;
            }

            if (OldCount < 0 || NewCount < 0)
            {
                return -1;
            }

            if (oldIndex < 0 || oldIndex >= OldCount)
            {
                return -1;
            }

            var current = oldIndex;

            foreach (var change in _changes)
            {
                if (current < change.Index)
                {
                    continue;
                }

                if (current < change.Index + change.OldCount)
                {
                    return -1;
                }

                current += change.NewCount - change.OldCount;
            }

            return current >= 0 && current < NewCount ? current : -1;
        }
    }

    public class FlattenedChangedEventArgs : EventArgs
    {
        public FlattenedChangedEventArgs(
            IReadOnlyList<FlattenedChange> changes,
            int version = 0,
            int currentCount = -1,
            IReadOnlyDictionary<int, int>? indexMapOverride = null)
        {
            Changes = changes ?? throw new ArgumentNullException(nameof(changes));
            Version = version;
            var delta = changes.Sum(x => x.NewCount - x.OldCount);
            var newCount = currentCount;
            var oldCount = currentCount >= 0 ? currentCount - delta : -1;
            IndexMap = new FlattenedIndexMap(oldCount, newCount, changes, indexMapOverride);
        }

        public IReadOnlyList<FlattenedChange> Changes { get; }

        /// <summary>
        /// Monotonically increasing version number for the flattened list.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Provides a mapping from the previous flattened list to the current one.
        /// </summary>
        public FlattenedIndexMap IndexMap { get; }
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

    public class HierarchicalNodeRetryEventArgs : HierarchicalNodeEventArgs
    {
        public HierarchicalNodeRetryEventArgs(HierarchicalNode node, TimeSpan delay)
            : base(node)
        {
            Delay = delay;
        }

        public TimeSpan Delay { get; }
    }

    public class HierarchyChangedEventArgs : HierarchicalNodeEventArgs
    {
        public HierarchyChangedEventArgs(HierarchicalNode node, NotifyCollectionChangedAction action)
            : base(node)
        {
            Action = action;
        }

        public NotifyCollectionChangedAction Action { get; }
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
        /// Gets an observable view of the flattened nodes in display order.
        /// </summary>
        ReadOnlyObservableCollection<HierarchicalNode> ObservableFlattened { get; }

        /// <summary>
        /// Gets the total number of visible nodes.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets a monotonically increasing version for the flattened list. Incremented on every flattened change notification.
        /// </summary>
        int FlattenedVersion { get; }

        event EventHandler<FlattenedChangedEventArgs>? FlattenedChanged;

        event EventHandler<HierarchicalNodeEventArgs>? NodeExpanded;

        event EventHandler<HierarchicalNodeEventArgs>? NodeCollapsed;

        event EventHandler<HierarchicalNodeEventArgs>? NodeLoading;

        event EventHandler<HierarchicalNodeEventArgs>? NodeLoaded;

        event EventHandler<HierarchicalNodeLoadFailedEventArgs>? NodeLoadFailed;

        event EventHandler<HierarchicalNodeRetryEventArgs>? NodeLoadRetryScheduled;

        /// <summary>
        /// Raised when the hierarchy mutates (structure changes), independent from visible flattening changes.
        /// </summary>
        event EventHandler<HierarchyChangedEventArgs>? HierarchyChanged;

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
        /// Retrieves the visible index of the specified node, or -1 when not visible.
        /// </summary>
        /// <param name="node">Node instance.</param>
        /// <returns>Visible index or -1.</returns>
        int IndexOf(HierarchicalNode node);

        /// <summary>
        /// Retrieves the visible index of the specified item, or -1 when not visible.
        /// </summary>
        /// <param name="item">Item instance.</param>
        /// <returns>Visible index or -1.</returns>
        int IndexOf(object item);

        /// <summary>
        /// Creates a guard scope to throttle virtualization-sensitive work (e.g., rapid expand/collapse).
        /// </summary>
        IDisposable BeginVirtualizationGuard();

        /// <summary>
        /// Expands a node and realizes its visible children.
        /// </summary>
        /// <param name="node">Node to expand.</param>
        void Expand(HierarchicalNode node);

        /// <summary>
        /// Asynchronously expands a node and realizes its visible children.
        /// </summary>
        /// <param name="node">Node to expand.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExpandAsync(HierarchicalNode node, CancellationToken cancellationToken = default);

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
        /// Asynchronously toggles expansion state of a node.
        /// </summary>
        /// <param name="node">Node to toggle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ToggleAsync(HierarchicalNode node, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes children for the given node (or root when null).
        /// </summary>
        /// <param name="node">Node to refresh; null for root.</param>
        void Refresh(HierarchicalNode? node = null);

        /// <summary>
        /// Asynchronously refreshes children for the given node (or root when null).
        /// </summary>
        /// <param name="node">Node to refresh; null for root.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RefreshAsync(HierarchicalNode? node = null, CancellationToken cancellationToken = default);

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
        /// Applies the specified comparer to siblings (optionally recursive) and stores it in <see cref="HierarchicalOptions.SiblingComparer"/>.
        /// </summary>
        /// <param name="comparer">Comparer to apply.</param>
        /// <param name="recursive">When true, recursively sorts expanded descendants.</param>
        void ApplySiblingComparer(IComparer<object>? comparer, bool recursive = true);

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
        private readonly ObservableRangeCollection<HierarchicalNode> _flattened;
        private readonly ReadOnlyObservableCollection<HierarchicalNode> _flattenedObservableView;
        private readonly IReadOnlyList<HierarchicalNode> _flattenedView;
        private readonly Dictionary<(Type, string), Func<object, object?>> _propertyPathCache;
        private readonly HashSet<HierarchicalNode> _pendingCullNodes = new();
        private readonly Dictionary<HierarchicalNode, NodeLoadState> _loadStates = new();
        private int _virtualizationGuardDepth;

        public HierarchicalModel(HierarchicalOptions? options = null)
        {
            Options = options ?? new HierarchicalOptions();
            _flattened = new ObservableRangeCollection<HierarchicalNode>();
            _flattenedObservableView = new ReadOnlyObservableCollection<HierarchicalNode>(_flattened);
            _flattenedView = new ReadOnlyListWrapper<HierarchicalNode>(_flattened);
            _propertyPathCache = new Dictionary<(Type, string), Func<object, object?>>();
        }

        public HierarchicalOptions Options { get; }

        public HierarchicalNode? Root { get; private set; }

        public IReadOnlyList<HierarchicalNode> Flattened => _flattenedView;

        public ReadOnlyObservableCollection<HierarchicalNode> ObservableFlattened => _flattenedObservableView;

        public int Count => _flattened.Count;

        public int FlattenedVersion { get; private set; }

        public event EventHandler<FlattenedChangedEventArgs>? FlattenedChanged;

        internal bool IsVirtualizationGuardActive => _virtualizationGuardDepth > 0;

        public IDisposable BeginVirtualizationGuard()
        {
            _virtualizationGuardDepth++;
            return new ActionDisposable(() =>
            {
                _virtualizationGuardDepth = Math.Max(0, _virtualizationGuardDepth - 1);
                if (_virtualizationGuardDepth == 0)
                {
                    CullPendingDescendants();
                }
            });
        }

        public event EventHandler<HierarchicalNodeEventArgs>? NodeExpanded;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeCollapsed;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeLoading;

        public event EventHandler<HierarchicalNodeEventArgs>? NodeLoaded;

        public event EventHandler<HierarchicalNodeLoadFailedEventArgs>? NodeLoadFailed;

        public event EventHandler<HierarchyChangedEventArgs>? HierarchyChanged;

        public event EventHandler<HierarchicalNodeRetryEventArgs>? NodeLoadRetryScheduled;

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

        public int IndexOf(HierarchicalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return _flattened.IndexOf(node);
        }

        public int IndexOf(object item)
        {
            if (item == null)
            {
                return -1;
            }

            var node = FindNode(item);
            return node != null ? IndexOf(node) : -1;
        }

        public void Expand(HierarchicalNode node)
        {
            ExpandAsync(node, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task ExpandAsync(HierarchicalNode node, CancellationToken cancellationToken = default)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.IsExpanded && node.HasMaterializedChildren && node.LoadError == null)
            {
                return;
            }

            var wasExpanded = node.IsExpanded;
            if (!wasExpanded)
            {
                node.IsExpanded = true; // gate concurrent expand attempts
            }

            try
            {
                await EnsureChildrenMaterializedAsync(node, forceReload: false, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                node.IsExpanded = wasExpanded;
                throw;
            }

            if (node.LoadError != null || !node.HasMaterializedChildren)
            {
                node.IsExpanded = wasExpanded;
                return;
            }

            var parentIndex = _flattened.IndexOf(node);
            var inserted = 0;

            if (!wasExpanded && parentIndex >= 0 && !node.IsLeaf)
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

            CancelPendingLoad(node);

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

            if (Options.VirtualizeChildren || IsVirtualizationGuardActive)
            {
                if (Options.VirtualizeChildren)
                {
                    DematerializeDescendants(node);
                }
                else
                {
                    _pendingCullNodes.Add(node);
                }
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

        public async Task ToggleAsync(HierarchicalNode node, CancellationToken cancellationToken = default)
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
                await ExpandAsync(node, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Refresh(HierarchicalNode? node = null)
        {
            RefreshAsync(node, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task RefreshAsync(HierarchicalNode? node = null, CancellationToken cancellationToken = default)
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
            await EnsureChildrenMaterializedAsync(target, forceReload: true, cancellationToken).ConfigureAwait(false);

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

        public void ApplySiblingComparer(IComparer<object>? comparer, bool recursive = true)
        {
            Options.SiblingComparer = comparer;
            Sort(null, comparer, recursive);
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

        protected virtual void OnFlattenedChanged(
            IReadOnlyList<FlattenedChange> changes,
            IReadOnlyDictionary<int, int>? indexMapOverride = null)
        {
            var version = ++FlattenedVersion;
            FlattenedChanged?.Invoke(this, new FlattenedChangedEventArgs(changes, version, _flattened.Count, indexMapOverride));
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

        protected virtual void OnNodeLoadRetryScheduled(HierarchicalNode node, TimeSpan delay)
        {
            NodeLoadRetryScheduled?.Invoke(this, new HierarchicalNodeRetryEventArgs(node, delay));
        }

        protected virtual void OnHierarchyChanged(HierarchicalNode node, NotifyCollectionChangedAction action)
        {
            HierarchyChanged?.Invoke(this, new HierarchyChangedEventArgs(node, action));
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
            _flattened.ResetWith(nodes ?? Array.Empty<HierarchicalNode>());

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

        private int GetVisibleVisibleCount(HierarchicalNode node)
        {
            return 1 + (node.IsExpanded ? node.ExpandedCount : 0);
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
                    HandleChildrenReplaced(parent, e);
                    break;
                case NotifyCollectionChangedAction.Move:
                    HandleChildrenMoved(parent, e);
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
            OnHierarchyChanged(parent, NotifyCollectionChangedAction.Add);
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
            OnHierarchyChanged(parent, NotifyCollectionChangedAction.Remove);
        }

        private void HandleChildrenReplaced(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null || e.OldItems == null || e.NewItems.Count == 0 || e.OldItems.Count == 0)
            {
                Refresh(parent);
                return;
            }

            var replaceIndex = e.OldStartingIndex >= 0 ? e.OldStartingIndex : 0;
            replaceIndex = Math.Min(replaceIndex, parent.MutableChildren.Count);

            var removeCount = Math.Min(e.OldItems.Count, parent.MutableChildren.Count - replaceIndex);
            var parentIndex = _flattened.IndexOf(parent);
            var visibleOffset = GetVisibleOffsetForChildIndex(parent, replaceIndex);
            var removedNodes = new List<HierarchicalNode>();

            for (int i = 0; i < removeCount; i++)
            {
                if (replaceIndex < parent.MutableChildren.Count)
                {
                    var old = parent.MutableChildren[replaceIndex];
                    removedNodes.Add(old);
                    parent.MutableChildren.RemoveAt(replaceIndex);
                    DetachHierarchy(old);
                }
            }

            var removedVisible = removedNodes.Sum(GetVisibleVisibleCount);

            var newNodes = new List<HierarchicalNode>();
            foreach (var item in e.NewItems.Cast<object>())
            {
                if (item == null)
                {
                    continue;
                }

                var node = new HierarchicalNode(item, parent, parent.Level + 1, isLeaf: DetermineInitialLeaf(item));
                newNodes.Add(node);
            }

            parent.MutableChildren.InsertRange(replaceIndex, newNodes);
            parent.IsLeaf = parent.MutableChildren.Count == 0;

            if (parent.IsExpanded && parentIndex >= 0)
            {
                var flattenedIndex = parentIndex + 1 + visibleOffset;
                if (removedVisible > 0)
                {
                    _flattened.RemoveRange(flattenedIndex, removedVisible);
                }

                var visibleNodes = new List<HierarchicalNode>();
                foreach (var node in newNodes)
                {
                    visibleNodes.Add(node);
                    if (node.IsExpanded)
                    {
                        EnsureChildrenMaterialized(node);
                        CollectVisibleChildren(node, visibleNodes);
                    }
                }

                if (visibleNodes.Count > 0)
                {
                    _flattened.InsertRange(flattenedIndex, visibleNodes);
                }

                var insertedVisible = parent.IsExpanded ? visibleNodes.Count : 0;
                OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, removedVisible, insertedVisible) });
            }

            RecalculateExpandedCountsFrom(parent);
            OnHierarchyChanged(parent, NotifyCollectionChangedAction.Replace);
        }

        private void HandleChildrenMoved(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null || e.NewItems.Count == 0)
            {
                return;
            }

            var moveIndex = e.OldStartingIndex >= 0 ? e.OldStartingIndex : 0;
            moveIndex = Math.Min(moveIndex, parent.MutableChildren.Count);
            var moveCount = Math.Min(e.NewItems.Count, parent.MutableChildren.Count - moveIndex);
            if (moveCount <= 0)
            {
                return;
            }

            var targetIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : parent.MutableChildren.Count - moveCount;
            targetIndex = Math.Max(0, targetIndex);
            targetIndex = Math.Min(targetIndex, parent.MutableChildren.Count - moveCount);
            if (targetIndex == moveIndex)
            {
                return;
            }

            var parentIndex = _flattened.IndexOf(parent);
            var expandedAndVisible = parent.IsExpanded && parentIndex >= 0;

            var removeOffset = GetVisibleOffsetForChildIndex(parent, moveIndex);
            var movedNodes = new List<HierarchicalNode>();

            for (int i = 0; i < moveCount; i++)
            {
                var node = parent.MutableChildren[moveIndex];
                movedNodes.Add(node);
                parent.MutableChildren.RemoveAt(moveIndex);
            }

            var removedVisible = expandedAndVisible ? movedNodes.Sum(GetVisibleVisibleCount) : 0;

            var insertIndex = Math.Min(Math.Max(0, targetIndex), parent.MutableChildren.Count);
            parent.MutableChildren.InsertRange(insertIndex, movedNodes);

            if (expandedAndVisible)
            {
                var removedAt = parentIndex + 1 + removeOffset;
                if (removedVisible > 0)
                {
                    _flattened.RemoveRange(removedAt, removedVisible);
                }

                var visibleNodes = new List<HierarchicalNode>();
                foreach (var node in movedNodes)
                {
                    visibleNodes.Add(node);
                    if (node.IsExpanded)
                    {
                        EnsureChildrenMaterialized(node);
                        CollectVisibleChildren(node, visibleNodes);
                    }
                }

                var insertOffset = GetVisibleOffsetForChildIndex(parent, insertIndex);
                var insertAt = parentIndex + 1 + insertOffset;
                if (visibleNodes.Count > 0)
                {
                    _flattened.InsertRange(insertAt, visibleNodes);
                }

                var insertedVisible = expandedAndVisible ? visibleNodes.Count : 0;
                var indexMap = new Dictionary<int, int>();

                for (int i = 0; i < visibleNodes.Count; i++)
                {
                    indexMap[removedAt + i] = insertAt + i;
                }

                OnFlattenedChanged(new[]
                {
                    new FlattenedChange(removedAt, removedVisible, 0),
                    new FlattenedChange(insertAt, 0, insertedVisible)
                }, indexMap.Count > 0 ? indexMap : null);
            }

            RecalculateExpandedCountsFrom(parent);
            OnHierarchyChanged(parent, NotifyCollectionChangedAction.Move);
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
            CancelPendingLoad(node);
            ClearLoadState(node);
            DetachChildrenNotifier(node);

            foreach (var child in node.Children)
            {
                DetachHierarchy(child);
            }
        }

        private void DematerializeDescendants(HierarchicalNode node)
        {
            CancelPendingLoad(node);
            ClearLoadState(node);
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

        private void CullPendingDescendants()
        {
            if (_pendingCullNodes.Count == 0)
            {
                return;
            }

            foreach (var node in _pendingCullNodes.ToArray())
            {
                if (!node.IsExpanded)
                {
                    DematerializeDescendants(node);
                }
            }

            _pendingCullNodes.Clear();
        }

        private IReadOnlyList<HierarchicalNode> EnsureChildrenMaterialized(HierarchicalNode node, bool forceReload = false)
        {
            return EnsureChildrenMaterializedAsync(node, forceReload, CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task<IReadOnlyList<HierarchicalNode>> EnsureChildrenMaterializedAsync(
            HierarchicalNode node,
            bool forceReload,
            CancellationToken cancellationToken)
        {
            if (node.HasMaterializedChildren && node.MutableChildren.Count > 0 && !forceReload)
            {
                return node.Children;
            }

            var state = GetLoadState(node);
            if (state.Task != null && !state.Task.IsCompleted && !forceReload)
            {
                return await state.Task.ConfigureAwait(false);
            }

            if (state.Task != null && !state.Task.IsCompleted && forceReload)
            {
                CancelPendingLoad(node);
            }

            if (state.NextRetryUtc.HasValue)
            {
                var now = DateTime.UtcNow;
                if (state.NextRetryUtc.Value > now)
                {
                    var delay = state.NextRetryUtc.Value - now;
                    OnNodeLoadRetryScheduled(node, delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            node.IsLoading = true;
            node.LoadError = null;
            OnNodeLoading(node);

            node.LoadCancellation?.Cancel();
            node.LoadCancellation?.Dispose();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            node.LoadCancellation = linkedCts;
            state.Cancellation = linkedCts;

            var loadTask = LoadChildrenWithStateAsync(node, forceReload, state, linkedCts.Token);
            state.Task = loadTask;

            try
            {
                return await loadTask.ConfigureAwait(false);
            }
            finally
            {
                if (state.Task == loadTask)
                {
                    state.Task = null;
                }
                node.LoadCancellation = null;
                state.Cancellation?.Dispose();
                state.Cancellation = null;
            }
        }

        private async Task<IReadOnlyList<HierarchicalNode>> LoadChildrenWithStateAsync(
            HierarchicalNode node,
            bool forceReload,
            NodeLoadState state,
            CancellationToken cancellationToken)
        {
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
                    ResetRetryState(state);
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
                var children = await ResolveChildrenAsync(node.Item, cancellationToken).ConfigureAwait(false);

                if (children == null)
                {
                    node.ChildrenSource = null;
                    DetachChildrenNotifier(node);
                    node.IsLeaf = true;
                    node.HasMaterializedChildren = true;
                    OnNodeLoaded(node);
                    ResetRetryState(state);
                    return node.Children;
                }

                node.ChildrenSource = children;
                AttachChildrenNotifier(node, children);

                foreach (var childItem in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                    if (HasReachedMaxDepth(childNode))
                    {
                        childNode.IsLeaf = true;
                        childNode.HasMaterializedChildren = true;
                    }

                    node.MutableChildren.Add(childNode);
                }

                if (Options.SiblingComparer != null)
                {
                    node.MutableChildren.Sort((x, y) => Options.SiblingComparer!.Compare(x.Item, y.Item));
                }

                node.HasMaterializedChildren = true;
                node.IsLeaf = node.MutableChildren.Count == 0;
                OnNodeLoaded(node);
                ResetRetryState(state);

                return node.Children;
            }
            catch (OperationCanceledException)
            {
                node.ChildrenSource = null;
                DetachChildrenNotifier(node);
                node.HasMaterializedChildren = false;
                node.IsLeaf = node.MutableChildren.Count == 0;
                node.LoadError = null;
                state.RetryCount = 0;
                state.NextRetryUtc = null;
                throw;
            }
            catch (Exception ex)
            {
                node.ChildrenSource = null;
                DetachChildrenNotifier(node);
                node.IsLeaf = true;
                node.HasMaterializedChildren = false;
                node.LoadError = ex;
                state.RetryCount++;
                var delay = ComputeRetryDelay(state.RetryCount);
                state.NextRetryUtc = DateTime.UtcNow + delay;
                OnNodeLoadFailed(node, ex);
                if (delay > TimeSpan.Zero)
                {
                    OnNodeLoadRetryScheduled(node, delay);
                }
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

            if (Options.TreatGroupsAsNodes)
            {
                if (item is DataGridCollectionViewGroup)
                {
                    return false;
                }

                if (item is DataGridCollectionView view &&
                    (view.GroupDescriptions?.Count > 0 || view.Groups != null))
                {
                    return false;
                }
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

        private Task<IEnumerable?> ResolveChildrenAsync(object item, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                return Task.FromResult<IEnumerable?>(null);
            }

            if (Options.ChildrenSelectorAsync != null)
            {
                return Options.ChildrenSelectorAsync(item, cancellationToken);
            }

            if (Options.ChildrenSelector != null)
            {
                return Task.FromResult(Options.ChildrenSelector(item));
            }

            if (Options.TreatGroupsAsNodes)
            {
                if (item is DataGridCollectionViewGroup group)
                {
                    return Task.FromResult<IEnumerable?>(group.Items);
                }

                if (item is DataGridCollectionView view &&
                    (view.GroupDescriptions?.Count > 0 || view.Groups != null))
                {
                    return Task.FromResult<IEnumerable?>(view.Groups);
                }
            }

            if (Options.ItemsSelector != null)
            {
                return Task.FromResult(Options.ItemsSelector(item));
            }

            if (!string.IsNullOrEmpty(Options.ChildrenPropertyPath))
            {
                var value = GetPropertyPathValue(item, Options.ChildrenPropertyPath!);
                return Task.FromResult(value as IEnumerable);
            }

            throw new InvalidOperationException("Provide ChildrenSelector, ItemsSelector, or ChildrenPropertyPath to resolve children.");
        }

        private void CancelPendingLoad(HierarchicalNode node)
        {
            if (_loadStates.TryGetValue(node, out var state) && state.Task != null && !state.Task.IsCompleted)
            {
                state.Cancellation?.Cancel();
            }
        }

        private NodeLoadState GetLoadState(HierarchicalNode node)
        {
            if (!_loadStates.TryGetValue(node, out var state))
            {
                state = new NodeLoadState();
                _loadStates[node] = state;
            }

            return state;
        }

        private void ClearLoadState(HierarchicalNode node)
        {
            if (_loadStates.TryGetValue(node, out var state))
            {
                state.Cancellation?.Cancel();
                state.Cancellation?.Dispose();
                _loadStates.Remove(node);
            }
        }

        private static TimeSpan ComputeRetryDelay(int attempt)
        {
            var clampedAttempt = Math.Max(1, Math.Min(6, attempt));
            var ms = 100 * (int)Math.Pow(2, clampedAttempt - 1);
            return TimeSpan.FromMilliseconds(Math.Min(ms, 5000));
        }

        private static void ResetRetryState(NodeLoadState state)
        {
            state.RetryCount = 0;
            state.NextRetryUtc = null;
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

        private sealed class NodeLoadState
        {
            public CancellationTokenSource? Cancellation { get; set; }

            public Task<IReadOnlyList<HierarchicalNode>>? Task { get; set; }

            public int RetryCount { get; set; }

            public DateTime? NextRetryUtc { get; set; }
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _onDispose;

            public ActionDisposable(Action onDispose)
            {
                _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
                _onDispose = null;
            }
        }

        private sealed class ReadOnlyListWrapper<T> : IReadOnlyList<T>
        {
            private readonly IList<T> _inner;

            public ReadOnlyListWrapper(IList<T> inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public T this[int index] => _inner[index];

            public int Count => _inner.Count;

            public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
