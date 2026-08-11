// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Utilities;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Represents a change in the flattened list of visible nodes.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class FlattenedChange
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
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class FlattenedIndexMap
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

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class FlattenedChangedEventArgs : EventArgs
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

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeEventArgs : EventArgs
    {
        public HierarchicalNodeEventArgs(HierarchicalNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public HierarchicalNode Node { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeLoadFailedEventArgs : HierarchicalNodeEventArgs
    {
        public HierarchicalNodeLoadFailedEventArgs(HierarchicalNode node, Exception error)
            : base(node)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public Exception Error { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeRetryEventArgs : HierarchicalNodeEventArgs
    {
        public HierarchicalNodeRetryEventArgs(HierarchicalNode node, TimeSpan delay)
            : base(node)
        {
            Delay = delay;
        }

        public TimeSpan Delay { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchyChangedEventArgs : HierarchicalNodeEventArgs
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
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IHierarchicalModel
    {
        /// <summary>
        /// Sets the root item that will act as the entry point for the hierarchy.
        /// </summary>
        /// <param name="rootItem">Root item.</param>
        void SetRoot(object rootItem);

        /// <summary>
        /// Sets multiple root items that will be displayed at the top level.
        /// Creates a virtual root container that holds all items.
        /// </summary>
        /// <param name="rootItems">Collection of items to display at root level.</param>
        void SetRoots(IEnumerable rootItems);

        /// <summary>
        /// Gets the collection of items displayed at root level.
        /// When <see cref="IsVirtualRoot"/> is true, returns the children of the virtual root.
        /// When false, returns the single root item.
        /// </summary>
        IEnumerable? RootItems { get; }

        /// <summary>
        /// Gets a value indicating whether the model has a virtual root containing multiple top-level items.
        /// When true, the <see cref="Root"/> node is a container that is not displayed in the grid.
        /// </summary>
        bool IsVirtualRoot { get; }

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
        /// Expands the nodes corresponding to the provided items.
        /// </summary>
        /// <param name="items">Items or nodes to expand.</param>
        void Expand(IEnumerable items);

        /// <summary>
        /// Asynchronously expands a node and realizes its visible children.
        /// </summary>
        /// <param name="node">Node to expand.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExpandAsync(HierarchicalNode node, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously expands the nodes corresponding to the provided items.
        /// </summary>
        /// <param name="items">Items or nodes to expand.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExpandAsync(IEnumerable items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Collapses a node, removing its descendants from the flattened list.
        /// </summary>
        /// <param name="node">Node to collapse.</param>
        void Collapse(HierarchicalNode node);

        /// <summary>
        /// Collapses the nodes corresponding to the provided items.
        /// </summary>
        /// <param name="items">Items or nodes to collapse.</param>
        void Collapse(IEnumerable items);

        /// <summary>
        /// Asynchronously collapses the nodes corresponding to the provided items.
        /// </summary>
        /// <param name="items">Items or nodes to collapse.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CollapseAsync(IEnumerable items, CancellationToken cancellationToken = default);

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
        /// Sorts siblings under the specified node (or root when null) using the provided comparer or the configured options (`SiblingComparerSelector` first, then <see cref="HierarchicalOptions.SiblingComparer"/>).
        /// Only orders siblings within the same parent; cross-level sorting is not performed.
        /// </summary>
        /// <param name="node">Parent node whose children should be sorted; null for root.</param>
        /// <param name="comparer">Comparer to apply; defaults to the per-node selector or <see cref="HierarchicalOptions.SiblingComparer"/>.</param>
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
        /// Asynchronously expands the specified node (or root when null) and all descendants up to the provided depth.
        /// Child selectors are resolved sequentially and visible nodes are committed once after loading completes.
        /// </summary>
        /// <param name="node">Starting node; null for root.</param>
        /// <param name="maxDepth">Maximum depth to expand (inclusive), or null for no limit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes after the coherent visible-node commit.</returns>
        Task ExpandAllAsync(
            HierarchicalNode? node = null,
            int? maxDepth = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Collapses the specified node's descendants (or the root subtree) starting at the provided depth.
        /// Depth is relative to the starting node; null collapses the starting node and all descendants.
        /// </summary>
        /// <param name="node">Starting node; null for root.</param>
        /// <param name="minDepth">Depth at which collapse begins (inclusive); null collapses from the starting node.</param>
        void CollapseAll(HierarchicalNode? node = null, int? minDepth = null);
    }

    /// <summary>
    /// Optional interface for models that can expand to a specific item on demand.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IHierarchicalModelExpander
    {
        /// <summary>
        /// Expands ancestors so the specified item becomes visible, if possible.
        /// </summary>
        /// <param name="item">Target item.</param>
        /// <param name="node">Resolved node, when successful.</param>
        /// <returns>True when the item was expanded into view.</returns>
        bool TryExpandToItem(object item, out HierarchicalNode? node);
    }

    /// <summary>
    /// Factory hook to allow replacing the default hierarchical model.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridHierarchicalModelFactory
    {
        IHierarchicalModel Create();
    }

    /// <summary>
    /// Default hierarchical model implementation (initial scaffolding).
    /// </summary>
    [RequiresUnreferencedCode("HierarchicalModel uses reflection to access property paths and is not compatible with trimming.")]
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalModel : IHierarchicalModel, IHierarchicalModelExpander, IHierarchicalStateProviderWithKeyMode
    {
        private readonly ObservableRangeCollection<HierarchicalNode> _flattened;
        private readonly ReadOnlyObservableCollection<HierarchicalNode> _flattenedObservableView;
        private readonly IReadOnlyList<HierarchicalNode> _flattenedView;
        private readonly Dictionary<(Type, string), Func<object, object?>> _propertyPathCache;
        private readonly Dictionary<(Type, string), Action<object, object?>> _propertyPathSetterCache;
        private readonly HashSet<HierarchicalNode> _pendingCullNodes = new();
        private readonly HashSet<HierarchicalNode> _expandedStateUpdates = new();
        private readonly Dictionary<HierarchicalNode, NodeLoadState> _loadStates = new();
        private SemaphoreSlim? _bulkExpandGate;
        private int _synchronousBulkExpandActive;
        private int _bulkMaterializationCommitGeneration;
        // Cached lookups for flattened items to avoid repeated linear scans.
        private int _flattenedLookupVersion = -1;
        private Dictionary<object, int>? _flattenedReferenceIndexLookup;
        private Dictionary<object, int>? _flattenedEqualityIndexLookup;
        private Dictionary<HierarchicalNode, int>? _flattenedNodeIndexLookup;
        private int _virtualizationGuardDepth;
        private IEnumerable? _rootItems;
        private bool _isVirtualRoot;

        public HierarchicalModel(HierarchicalOptions? options = null)
        {
            Options = options ?? new HierarchicalOptions();
            _flattened = new ObservableRangeCollection<HierarchicalNode>();
            _flattened.CollectionChanged += OnFlattenedCollectionChanged;
            _flattenedObservableView = new ReadOnlyObservableCollection<HierarchicalNode>(_flattened);
            _flattenedView = new ReadOnlyListWrapper<HierarchicalNode>(_flattened);
            _propertyPathCache = new Dictionary<(Type, string), Func<object, object?>>();
            _propertyPathSetterCache = new Dictionary<(Type, string), Action<object, object?>>();
        }

        public HierarchicalOptions Options { get; }

        private bool HasExpandedStateSelector =>
            Options.IsExpandedSelector != null || !string.IsNullOrEmpty(Options.IsExpandedPropertyPath);

        private bool HasExpandedStateSetter =>
            Options.IsExpandedSetter != null || !string.IsNullOrEmpty(Options.IsExpandedPropertyPath);

        public HierarchicalNode? Root { get; private set; }

        public IEnumerable? RootItems => _rootItems;

        public bool IsVirtualRoot => _isVirtualRoot;

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

        public ExpandedStateKeyMode ExpandedStateKeyMode => Options.ExpandedStateKeyMode;

        public void SetRoot(object rootItem)
        {
            if (rootItem == null)
            {
                throw new ArgumentNullException(nameof(rootItem));
            }

            var expandedItems = CaptureExpandedItems();
            _isVirtualRoot = false;
            _rootItems = null;

            var root = new HierarchicalNode(rootItem, parent: null, level: 0, isLeaf: DetermineInitialLeaf(rootItem));
            InitializeNode(root);
            SetRoot(root, rebuildFlattened: false);

            var expandedNodes = new List<HierarchicalNode>();
            if (Options.ExpandedStateKeyMode == ExpandedStateKeyMode.Path)
            {
                var path = new List<int> { 0 };
                RestoreExpandedStateByPath(root, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, path);
            }
            else
            {
                RestoreExpandedState(root, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes);
            }
            RecalculateExpandedCountsFrom(root);
            ReplaceFlattened(BuildFlattenedFromRoot(root));
            foreach (var expandedNode in expandedNodes)
            {
                OnNodeExpanded(expandedNode);
            }
        }

        public void SetRoots(IEnumerable rootItems)
        {
            if (rootItems == null)
            {
                throw new ArgumentNullException(nameof(rootItems));
            }

            var expandedItems = CaptureExpandedItems();
            _isVirtualRoot = true;
            _rootItems = rootItems;

            // Create a virtual root node that holds all root items.
            // The virtual root itself will not be displayed; only its children at level 0.
            var virtualRootItem = new VirtualRootContainer(rootItems);
            var virtualRoot = new HierarchicalNode(virtualRootItem, parent: null, level: -1, isLeaf: false);
            InitializeNode(virtualRoot);

            // Set the source for children resolution.
            virtualRoot.ChildrenSource = rootItems;
            virtualRoot.HasMaterializedChildren = true;
            SetNodeExpandedState(virtualRoot, true);

            SetRoot(virtualRoot, rebuildFlattened: false);

            // Build child nodes for each root item at level 0.
            var children = new List<HierarchicalNode>();
            foreach (var item in rootItems)
            {
                var childNode = new HierarchicalNode(item, parent: virtualRoot, level: 0, isLeaf: DetermineInitialLeaf(item));
                InitializeNode(childNode);
                virtualRoot.MutableChildren.Add(childNode);
                children.Add(childNode);
            }

            // Subscribe to INCC on rootItems if applicable.
            AttachChildrenNotifier(virtualRoot, rootItems);

            var expandedNodes = new List<HierarchicalNode>();
            if (Options.ExpandedStateKeyMode == ExpandedStateKeyMode.Path)
            {
                var path = new List<int>();
                for (int i = 0; i < virtualRoot.MutableChildren.Count; i++)
                {
                    path.Add(i);
                    RestoreExpandedStateByPath(virtualRoot.MutableChildren[i], expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, path);
                    path.RemoveAt(path.Count - 1);
                }
            }
            else
            {
                foreach (var child in virtualRoot.MutableChildren)
                {
                    RestoreExpandedState(child, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes);
                }
            }
            RecalculateExpandedCountsFrom(virtualRoot);

            // Flatten: the virtual root is not included, only its children.
            ReplaceFlattened(BuildFlattenedFromVirtualRoot(virtualRoot));

            foreach (var expandedNode in expandedNodes)
            {
                OnNodeExpanded(expandedNode);
            }
        }

        private IEnumerable<HierarchicalNode> BuildFlattenedFromVirtualRoot(HierarchicalNode virtualRoot)
        {
            var result = new List<HierarchicalNode>();
            foreach (var child in virtualRoot.Children)
            {
                result.Add(child);
                if (child.IsExpanded)
                {
                    CollectVisibleChildren(child, result);
                }
            }
            return result;
        }

        private IEnumerable<HierarchicalNode> BuildFlattenedFromRoot(HierarchicalNode root)
        {
            var result = new List<HierarchicalNode> { root };
            if (root.IsExpanded)
            {
                CollectVisibleChildren(root, result);
            }
            return result;
        }

        private HashSet<object> CaptureExpandedItems()
        {
            var expanded = new HashSet<object>(EqualityComparer<object>.Default);
            if (Root == null)
            {
                return expanded;
            }

            if (Options.ExpandedStateKeyMode == ExpandedStateKeyMode.Path)
            {
                CaptureExpandedPaths(expanded);
                return expanded;
            }

            var stack = new Stack<HierarchicalNode>();
            stack.Push(Root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.IsExpanded && TryGetExpandedStateKey(node, out var key))
                {
                    expanded.Add(key);
                }

                foreach (var child in node.Children)
                {
                    stack.Push(child);
                }
            }

            return expanded;
        }

        private void CaptureExpandedPaths(ISet<object> expanded)
        {
            if (Root == null)
            {
                return;
            }

            var path = new List<int>();

            if (_isVirtualRoot)
            {
                var children = Root.MutableChildren;
                for (int i = 0; i < children.Count; i++)
                {
                    path.Add(i);
                    CaptureExpandedPathsRecursive(children[i], expanded, path);
                    path.RemoveAt(path.Count - 1);
                }

                return;
            }

            path.Add(0);
            CaptureExpandedPathsRecursive(Root, expanded, path);
        }

        private void CaptureExpandedPathsRecursive(
            HierarchicalNode node,
            ISet<object> expanded,
            List<int> path)
        {
            if (node.IsExpanded)
            {
                expanded.Add(new ExpandedNodePath(path.ToArray()));
            }

            var children = node.MutableChildren;
            for (int i = 0; i < children.Count; i++)
            {
                path.Add(i);
                CaptureExpandedPathsRecursive(children[i], expanded, path);
                path.RemoveAt(path.Count - 1);
            }
        }

        private bool TryGetItemExpandedState(object item, out bool isExpanded)
        {
            isExpanded = false;

            if (!HasExpandedStateSelector || item is VirtualRootContainer)
            {
                return false;
            }

            if (Options.IsExpandedSelector != null)
            {
                try
                {
                    var value = Options.IsExpandedSelector(item);
                    if (value.HasValue)
                    {
                        isExpanded = value.Value;
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(Options.IsExpandedPropertyPath))
            {
                return false;
            }

            var resolved = GetPropertyPathValue(item, Options.IsExpandedPropertyPath!);
            if (resolved is bool boolValue)
            {
                isExpanded = boolValue;
                return true;
            }

            if (resolved == null)
            {
                return false;
            }

            throw new InvalidOperationException(
                $"Property path '{Options.IsExpandedPropertyPath}' on type '{item.GetType().FullName}' does not resolve to a boolean value.");
        }

        private bool TryGetExpandedStateKey(HierarchicalNode node, out object key)
        {
            return TryGetExpandedStateKey(node, Options.ExpandedStateKeyMode, out key);
        }

        private bool TryGetExpandedStateKey(HierarchicalNode node, ExpandedStateKeyMode keyMode, out object key)
        {
            key = null;
            switch (keyMode)
            {
                case ExpandedStateKeyMode.Custom:
                    if (Options.ExpandedStateKeySelector == null)
                    {
                        key = node.Item;
                        return true;
                    }

                    key = Options.ExpandedStateKeySelector(node.Item);
                    return key != null;
                default:
                    key = node.Item;
                    return true;
            }
        }


        private void RestoreExpandedState(
            HierarchicalNode node,
            ISet<object> expandedItems,
            int depth,
            bool applyAutoExpand,
            List<HierarchicalNode> expandedNodes)
        {
            RestoreExpandedState(node, expandedItems, depth, applyAutoExpand, expandedNodes, Options.ExpandedStateKeyMode);
        }

        private void RestoreExpandedState(
            HierarchicalNode node,
            ISet<object> expandedItems,
            int depth,
            bool applyAutoExpand,
            List<HierarchicalNode> expandedNodes,
            ExpandedStateKeyMode keyMode)
        {
            var shouldExpand = false;
            if (TryGetItemExpandedState(node.Item, out var itemExpanded))
            {
                shouldExpand = itemExpanded;
            }
            else if (TryGetExpandedStateKey(node, keyMode, out var key) && expandedItems.Contains(key))
            {
                shouldExpand = true;
            }
            else if (applyAutoExpand && WithinAutoExpandDepth(depth))
            {
                shouldExpand = true;
            }

            if (!shouldExpand)
            {
                SetNodeExpandedState(node, false);
                return;
            }

            SetNodeExpandedState(node, true);
            expandedNodes.Add(node);
            EnsureChildrenMaterialized(node);

            foreach (var child in node.Children)
            {
                RestoreExpandedState(child, expandedItems, depth + 1, applyAutoExpand, expandedNodes, keyMode);
            }
        }

        private void RestoreExpandedStateByPath(
            HierarchicalNode node,
            ISet<object> expandedItems,
            int depth,
            bool applyAutoExpand,
            List<HierarchicalNode> expandedNodes,
            List<int> path)
        {
            var shouldExpand = false;
            if (TryGetItemExpandedState(node.Item, out var itemExpanded))
            {
                shouldExpand = itemExpanded;
            }
            else if (expandedItems.Contains(new ExpandedNodePath(path.ToArray())))
            {
                shouldExpand = true;
            }
            else if (applyAutoExpand && WithinAutoExpandDepth(depth))
            {
                shouldExpand = true;
            }

            if (!shouldExpand)
            {
                SetNodeExpandedState(node, false);
                return;
            }

            SetNodeExpandedState(node, true);
            expandedNodes.Add(node);
            EnsureChildrenMaterialized(node);

            var children = node.MutableChildren;
            for (int i = 0; i < children.Count; i++)
            {
                path.Add(i);
                RestoreExpandedStateByPath(children[i], expandedItems, depth + 1, applyAutoExpand, expandedNodes, path);
                path.RemoveAt(path.Count - 1);
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

            return GetFlattenedIndex(node);
        }

        public int IndexOf(object item)
        {
            if (item == null)
            {
                return -1;
            }

            EnsureFlattenedLookup();

            if (_flattenedReferenceIndexLookup != null &&
                _flattenedReferenceIndexLookup.TryGetValue(item, out var index))
            {
                return index;
            }

            if (_flattenedEqualityIndexLookup != null &&
                _flattenedEqualityIndexLookup.TryGetValue(item, out index))
            {
                return index;
            }

            return -1;
        }

        public void Expand(HierarchicalNode node)
        {
            ExpandAsync(node, CancellationToken.None).GetAwaiter().GetResult();
        }

        public void Expand(IEnumerable items)
        {
            ExpandAsync(items, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task ExpandAsync(HierarchicalNode node, CancellationToken cancellationToken = default)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var loadState = GetLoadState(node);
            await loadState.ExpandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var parentIndex = GetFlattenedIndex(node);
                var hasVisibleDescendants = GetVisibleDescendantCount(node, parentIndex) > 0;

                if (node.IsExpanded && node.LoadError == null)
                {
                    if (node.IsLeaf || parentIndex < 0)
                    {
                        if (node.IsLeaf && parentIndex >= 0)
                        {
                            node.HasPendingBulkMaterializationCommit = false;
                        }

                        return;
                    }

                    if (node.HasMaterializedChildren && hasVisibleDescendants)
                    {
                        node.HasPendingBulkMaterializationCommit = false;
                        return;
                    }
                }

                var wasExpanded = node.IsExpanded;
                if (!wasExpanded)
                {
                    SetNodeExpandedState(node, true); // gate concurrent expand attempts
                }

                try
                {
                    await EnsureChildrenMaterializedAsync(node, forceReload: false, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    SetNodeExpandedState(node, wasExpanded);
                    throw;
                }

                if (node.LoadError != null || !node.HasMaterializedChildren)
                {
                    SetNodeExpandedState(node, wasExpanded);
                    return;
                }

                parentIndex = GetFlattenedIndex(node);
                var inserted = 0;
                List<HierarchicalNode>? visibleMaterializationCommits = null;

                if (parentIndex >= 0 && !node.IsLeaf)
                {
                    var hasVisible = GetVisibleDescendantCount(node, parentIndex) > 0;
                    if (!hasVisible)
                    {
                        inserted = InsertVisibleChildren(
                            node,
                            parentIndex + 1,
                            ref visibleMaterializationCommits);
                        if (inserted > 0)
                        {
                            OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, 0, inserted) });
                        }
                    }
                }

                SetNodeExpandedState(node, true);
                RecalculateExpandedCountsFrom(node);
                OnNodeExpanded(node);
                if (parentIndex >= 0)
                {
                    node.HasPendingBulkMaterializationCommit = false;
                    SetPendingBulkMaterializationCommit(visibleMaterializationCommits, value: false);
                }
            }
            finally
            {
                loadState.ExpandGate.Release();
            }
        }

        public async Task ExpandAsync(IEnumerable items, CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var visited = new HashSet<HierarchicalNode>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryResolveNodeFromItem(item, expandAncestors: true, out var node))
                {
                    continue;
                }

                if (!visited.Add(node))
                {
                    continue;
                }

                await ExpandAsync(node, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Collapse(HierarchicalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (_isVirtualRoot && ReferenceEquals(node, Root))
            {
                return;
            }

            if (node.IsLeaf)
            {
                return;
            }

            var parentIndex = GetFlattenedIndex(node);
            var visibleCount = GetVisibleDescendantCount(node, parentIndex);

            if (!node.IsExpanded && visibleCount == 0)
            {
                return;
            }

            CancelPendingLoad(node);

            if (parentIndex >= 0 && visibleCount > 0)
            {
                var removed = RemoveVisibleDescendants(node, parentIndex, detachDescendants: false);
                if (removed > 0)
                {
                    OnFlattenedChanged(new[] { new FlattenedChange(parentIndex + 1, removed, 0) });
                }
            }

            SetNodeExpandedState(node, false);
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

        public void Collapse(IEnumerable items)
        {
            CollapseAsync(items, CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task CollapseAsync(IEnumerable items, CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var visited = new HashSet<HierarchicalNode>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryResolveNodeFromItem(item, expandAncestors: false, out var node))
                {
                    continue;
                }

                if (!visited.Add(node))
                {
                    continue;
                }

                Collapse(node);
            }

            return Task.CompletedTask;
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

            var parentIndex = GetFlattenedIndex(target);
            var wasExpanded = target.IsExpanded;
            var canUpdateFlattened = wasExpanded && (parentIndex >= 0 || IsVirtualRootNode(target));
            var removeStart = parentIndex + 1;
            IList<HierarchicalNode>? oldVisibleNodes = null;

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

            if (canUpdateFlattened)
            {
                var visibleCount = GetVisibleDescendantCount(target, parentIndex);
                if (visibleCount > 0 && removeStart >= 0 && removeStart + visibleCount <= _flattened.Count)
                {
                    oldVisibleNodes = _flattened.GetRange(removeStart, visibleCount);
                }
                removedCount = RemoveVisibleDescendants(target, parentIndex, detachDescendants: false);
            }

            if (canUpdateFlattened && !target.IsLeaf)
            {
                insertedCount = InsertVisibleChildren(target, removeStart);
            }

            if (canUpdateFlattened && (removedCount > 0 || insertedCount > 0))
            {
                var indexMap = oldVisibleNodes != null && insertedCount > 0
                    ? BuildIndexMap(oldVisibleNodes, removeStart, _flattened.GetRange(removeStart, insertedCount), removeStart)
                    : null;
                OnFlattenedChanged(new[] { new FlattenedChange(removeStart, removedCount, insertedCount) }, indexMap);
            }

            RecalculateExpandedCountsFrom(target);
            target.HasPendingBulkMaterializationCommit = false;
        }

        public HierarchicalNode? FindNode(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            EnsureFlattenedLookup();

            if (_flattenedReferenceIndexLookup != null &&
                _flattenedReferenceIndexLookup.TryGetValue(item, out var index))
            {
                return _flattened[index];
            }

            if (_flattenedEqualityIndexLookup != null &&
                _flattenedEqualityIndexLookup.TryGetValue(item, out index))
            {
                return _flattened[index];
            }

            return null;
        }

        private void EnsureFlattenedLookup()
        {
            if (_flattenedLookupVersion == FlattenedVersion &&
                _flattenedReferenceIndexLookup != null &&
                _flattenedEqualityIndexLookup != null &&
                _flattenedNodeIndexLookup != null)
            {
                return;
            }

            var referenceLookup = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            var equalityLookup = new Dictionary<object, int>();
            var nodeLookup = new Dictionary<HierarchicalNode, int>();

            for (int i = 0; i < _flattened.Count; i++)
            {
                var node = _flattened[i];
                nodeLookup[node] = i;

                var item = node.Item;
                if (item == null)
                {
                    continue;
                }

                if (!referenceLookup.ContainsKey(item))
                {
                    referenceLookup[item] = i;
                }

                if (!equalityLookup.ContainsKey(item))
                {
                    equalityLookup[item] = i;
                }
            }

            _flattenedReferenceIndexLookup = referenceLookup;
            _flattenedEqualityIndexLookup = equalityLookup;
            _flattenedNodeIndexLookup = nodeLookup;
            _flattenedLookupVersion = FlattenedVersion;
        }

        private void OnFlattenedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateFlattenedLookup();
        }

        private void InvalidateFlattenedLookup()
        {
            _flattenedLookupVersion = -1;
            _flattenedReferenceIndexLookup = null;
            _flattenedEqualityIndexLookup = null;
            _flattenedNodeIndexLookup = null;
        }

        private bool TryResolveNodeFromItem(object? item, bool expandAncestors, out HierarchicalNode? node)
        {
            node = null;
            if (item == null)
            {
                return false;
            }

            if (item is HierarchicalNode directNode)
            {
                node = directNode;
                return true;
            }

            if (item is IHierarchicalNodeItem nodeItem)
            {
                item = nodeItem.Item;
                if (item == null)
                {
                    return false;
                }
            }

            node = FindNode(item);
            if (node != null)
            {
                return true;
            }

            if (!expandAncestors)
            {
                return false;
            }

            return TryExpandToItem(item, out node);
        }

        public bool TryExpandToItem(object item)
        {
            return TryExpandToItem(item, out _);
        }

        public bool TryExpandToItem(object item, out HierarchicalNode? node)
        {
            node = null;
            if (item == null || Root == null)
            {
                return false;
            }

            if (item is HierarchicalNode candidate)
            {
                node = candidate;
                ExpandAncestors(candidate);
                return true;
            }

            node = FindNode(item);
            if (node != null)
            {
                return true;
            }

            if (Options.ItemPathSelector != null)
            {
                var path = Options.ItemPathSelector(item);
                if (path != null && TryExpandToPath(path, out var foundByPath))
                {
                    if (foundByPath != null &&
                        (ReferenceEquals(foundByPath.Item, item) || Equals(foundByPath.Item, item)))
                    {
                        node = foundByPath;
                        return true;
                    }
                }
            }

            if (!Options.AllowExpandToItemSearch)
            {
                return false;
            }

            if (TryFindAndExpand(Root, item, out var found))
            {
                node = found;
                return true;
            }

            return false;
        }

        private bool TryFindAndExpand(HierarchicalNode current, object item, out HierarchicalNode? node)
        {
            if (ReferenceEquals(current.Item, item) || Equals(current.Item, item))
            {
                node = current;
                return true;
            }

            node = null;
            if (current.IsLeaf)
            {
                return false;
            }

            EnsureChildrenMaterialized(current);
            foreach (var child in current.Children)
            {
                if (TryFindAndExpand(child, item, out node))
                {
                    if (!_isVirtualRoot || !ReferenceEquals(current, Root))
                    {
                        Expand(current);
                    }
                    return true;
                }
            }

            return false;
        }

        private bool TryExpandToPath(IReadOnlyList<int> path, out HierarchicalNode? node)
        {
            node = null;
            if (Root == null || path == null)
            {
                return false;
            }

            if (path.Count == 0)
            {
                if (_isVirtualRoot)
                {
                    return false;
                }

                node = Root;
                return true;
            }

            var current = Root;
            var startIndex = 0;

            if (!_isVirtualRoot)
            {
                if (path[0] != 0)
                {
                    return false;
                }

                startIndex = 1;
            }

            for (int i = startIndex; i < path.Count; i++)
            {
                var childIndex = path[i];
                if (childIndex < 0)
                {
                    return false;
                }

                if (!(_isVirtualRoot && ReferenceEquals(current, Root)))
                {
                    if (!current.IsExpanded)
                    {
                        Expand(current);
                    }

                    if (current.IsLeaf)
                    {
                        return false;
                    }

                    EnsureChildrenMaterialized(current);
                }

                var children = current.MutableChildren;
                if (childIndex >= children.Count)
                {
                    return false;
                }

                current = children[childIndex];
            }

            node = current;
            return true;
        }

        private void ExpandAncestors(HierarchicalNode node)
        {
            for (var current = node.Parent; current != null; current = current.Parent)
            {
                if (_isVirtualRoot && ReferenceEquals(current, Root))
                {
                    continue;
                }

                Expand(current);
            }
        }

        public void Sort(HierarchicalNode? node = null, IComparer<object>? comparer = null, bool recursive = true)
        {
            var target = node ?? Root;
            if (target == null)
            {
                return;
            }

            var hasComparer = comparer != null || Options.SiblingComparerSelector != null || Options.SiblingComparer != null;
            if (!hasComparer)
            {
                return;
            }

            EnsureChildrenMaterialized(target);
            SortChildren(target, comparer, recursive);

            if (_isVirtualRoot && ReferenceEquals(target, Root))
            {
                ReplaceFlattened(BuildFlattenedFromVirtualRoot(target));
                return;
            }

            if (target.IsExpanded)
            {
                var parentIndex = GetFlattenedIndex(target);
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
            if (Options.ChildrenSelectorAsync == null)
            {
                EnterSynchronousBulkExpansion();
                try
                {
                    ExpandAllSynchronously(start, limit);
                }
                finally
                {
                    ExitSynchronousBulkExpansion();
                }
                return;
            }

            var gate = GetBulkExpandGate();
            if (!gate.Wait(0))
            {
                // A blocking wait can deadlock when the active asynchronous operation owns the
                // caller's synchronization context. The model is not safe to mutate concurrently,
                // so the legacy synchronous entry point fails deterministically instead.
                throw new InvalidOperationException("A bulk hierarchy expansion is already in progress.");
            }
            try
            {
                // Invoke asynchronous selectors on the calling thread while suppressing ordinary
                // synchronization-context capture inside the selector. Observable model mutation
                // and event publication remain inline on the calling thread after each result is
                // resolved.
                ExpandAllSynchronously(start, limit, resolveAsyncChildrenOffContext: true);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <inheritdoc />
        public async Task ExpandAllAsync(
            HierarchicalNode? node = null,
            int? maxDepth = null,
            CancellationToken cancellationToken = default)
        {
            var start = node ?? Root;
            if (start == null)
            {
                return;
            }

            var limit = maxDepth ?? int.MaxValue;
            cancellationToken.ThrowIfCancellationRequested();
            if (Options.ChildrenSelectorAsync == null)
            {
                EnterSynchronousBulkExpansion();
                try
                {
                    ExpandAllSynchronously(start, limit);
                }
                finally
                {
                    ExitSynchronousBulkExpansion();
                }
                return;
            }

            var continueOnCapturedContext = SynchronizationContext.Current != null;
            var gate = GetBulkExpandGate();
            await gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext);
            try
            {
                await ExpandAllAsynchronously(
                        start,
                        limit,
                        cancellationToken,
                        continueOnCapturedContext)
                    .ConfigureAwait(continueOnCapturedContext);
            }
            finally
            {
                gate.Release();
            }
        }

        private void EnterSynchronousBulkExpansion()
        {
            if (Interlocked.CompareExchange(ref _synchronousBulkExpandActive, 1, 0) != 0)
            {
                throw new InvalidOperationException("A bulk hierarchy expansion is already in progress.");
            }
        }

        private void ExitSynchronousBulkExpansion()
        {
            Volatile.Write(ref _synchronousBulkExpandActive, 0);
        }

        private async Task ExpandAllAsynchronously(
            HierarchicalNode start,
            int limit,
            CancellationToken cancellationToken,
            bool continueOnCapturedContext)
        {
            var nodesToExpand = new List<HierarchicalNode>();
            var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var ancestor = start.Parent;
            while (ancestor != null)
            {
                ancestors.Add(ancestor.Item);
                ancestor = ancestor.Parent;
            }
            var stack = new Stack<(HierarchicalNode Node, int Depth, bool Exit)>();
            var materializationChanged = false;
            var materializationCommitGeneration = GetNextBulkMaterializationCommitGeneration();
            var requiresVisibleMaterializationCommitClear = false;
            var expandedNodesHavePropertyObservers = false;
            var traversedNodeCount = 0;
            stack.Push((start, 0, false));

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (current, depth, exit) = stack.Pop();
                if (exit)
                {
                    ancestors.Remove(current.Item);
                    continue;
                }

                if (depth > limit)
                {
                    continue;
                }

                traversedNodeCount++;
                ancestors.Add(current.Item);
                var isVirtualRoot = _isVirtualRoot && ReferenceEquals(current, Root);
                var hadMaterializedChildren = current.HasMaterializedChildren;

                if (!isVirtualRoot &&
                    (!current.HasMaterializedChildren || current.LoadError != null))
                {
                    await EnsureChildrenMaterializedAsync(
                            current,
                            forceReload: false,
                            cancellationToken,
                            ancestors,
                            continueOnCapturedContext)
                        .ConfigureAwait(continueOnCapturedContext);
                }

                var materializedDuringOperation =
                    !hadMaterializedChildren && current.HasMaterializedChildren;

                if (materializedDuringOperation || current.HasPendingBulkMaterializationCommit)
                {
                    materializationChanged = true;
                    current.PendingBulkMaterializationCommitGeneration =
                        materializationCommitGeneration;
                    if ((current.IsExpanded ||
                         isVirtualRoot ||
                         current.LoadError != null ||
                         !current.HasMaterializedChildren) &&
                        !ReferenceEquals(current, start))
                    {
                        requiresVisibleMaterializationCommitClear = true;
                    }
                }

                if (current.LoadError != null || !current.HasMaterializedChildren)
                {
                    ancestors.Remove(current.Item);
                    continue;
                }

                if (!current.IsExpanded && !isVirtualRoot)
                {
                    nodesToExpand.Add(current);
                    expandedNodesHavePropertyObservers |= current.HasPropertyChangedObservers;
                }

                if (current.IsLeaf || depth >= limit)
                {
                    ancestors.Remove(current.Item);
                    continue;
                }

                var children = current.Children;
                stack.Push((current, depth, true));
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    stack.Push((children[i], depth + 1, false));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (nodesToExpand.Count == 0 && !materializationChanged)
            {
                return;
            }

            CommitBulkExpansion(
                start,
                nodesToExpand,
                expansionStatesAlreadyApplied: false,
                materializationChanged,
                traversedNodeCount,
                materializationCommitGeneration,
                requiresVisibleMaterializationCommitClear,
                expandedNodesHavePropertyObservers);
        }

        private SemaphoreSlim GetBulkExpandGate()
        {
            var gate = Volatile.Read(ref _bulkExpandGate);
            if (gate != null)
            {
                return gate;
            }

            var created = new SemaphoreSlim(1, 1);
            gate = Interlocked.CompareExchange(ref _bulkExpandGate, created, null);
            if (gate != null)
            {
                created.Dispose();
                return gate;
            }

            return created;
        }

        private int GetNextBulkMaterializationCommitGeneration()
        {
            var generation = Interlocked.Increment(ref _bulkMaterializationCommitGeneration);
            if (generation == 0)
            {
                generation = Interlocked.Increment(ref _bulkMaterializationCommitGeneration);
            }

            return generation;
        }

        private void ExpandAllSynchronously(
            HierarchicalNode start,
            int limit,
            bool resolveAsyncChildrenOffContext = false)
        {
            List<HierarchicalNode>? expandedNodes = null;
            var materializationCommitGeneration = GetNextBulkMaterializationCommitGeneration();
            const int hashedCycleDepth = 32;
            HashSet<object>? ancestors = null;
            if (start.Level >= hashedCycleDepth)
            {
                ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var ancestor = start.Parent;
                while (ancestor != null)
                {
                    ancestors.Add(ancestor.Item);
                    ancestor = ancestor.Parent;
                }
            }
            var stack = new Stack<(HierarchicalNode Node, int Depth, bool Exit)>();
            var anyExpanded = false;
            var materializationChanged = false;
            var requiresVisibleMaterializationCommitClear = false;
            var expandedNodesHavePropertyObservers = false;
            var traversedNodeCount = 0;
            stack.Push((start, 0, false));

            while (stack.Count > 0)
            {
                var (current, depth, exit) = stack.Pop();
                if (exit)
                {
                    if (current.Level >= hashedCycleDepth)
                    {
                        ancestors!.Remove(current.Item);
                    }
                    continue;
                }

                if (depth > limit)
                {
                    continue;
                }
                traversedNodeCount++;
                if (current.Level >= hashedCycleDepth)
                {
                    if (ancestors == null)
                    {
                        ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
                        var ancestor = current.Parent;
                        while (ancestor != null)
                        {
                            ancestors.Add(ancestor.Item);
                            ancestor = ancestor.Parent;
                        }
                    }

                    ancestors.Add(current.Item);
                }

                bool isVirtualRoot = _isVirtualRoot && ReferenceEquals(current, Root);
                bool wasExpanded = current.IsExpanded;
                bool hadMaterializedChildren = current.HasMaterializedChildren;

                if (!isVirtualRoot &&
                    (!current.HasMaterializedChildren || current.LoadError != null))
                {
                    EnsureChildrenMaterializedSynchronously(
                        current,
                        forceReload: false,
                        current.Level >= hashedCycleDepth ? ancestors : null,
                        resolveAsyncChildrenOffContext);
                    if (current.LoadError != null || !current.HasMaterializedChildren)
                    {
                        if (current.Level >= hashedCycleDepth)
                        {
                            ancestors!.Remove(current.Item);
                        }
                        continue;
                    }
                }

                var materializedDuringOperation =
                    !hadMaterializedChildren && current.HasMaterializedChildren;
                if (materializedDuringOperation || current.HasPendingBulkMaterializationCommit)
                {
                    materializationChanged = true;
                    current.PendingBulkMaterializationCommitGeneration =
                        materializationCommitGeneration;
                    if ((wasExpanded || isVirtualRoot) &&
                        !ReferenceEquals(current, start))
                    {
                        requiresVisibleMaterializationCommitClear = true;
                    }
                }

                if (!wasExpanded && !isVirtualRoot)
                {
                    anyExpanded = true;
                    (expandedNodes ??= new List<HierarchicalNode>()).Add(current);
                    expandedNodesHavePropertyObservers |= current.HasPropertyChangedObservers;
                }

                if (current.IsLeaf || depth >= limit)
                {
                    if (current.Level >= hashedCycleDepth)
                    {
                        ancestors!.Remove(current.Item);
                    }
                    continue;
                }

                var children = current.MutableChildren;
                var allChildrenAreLeaves = children.Count > 0;
                for (int i = 0; i < children.Count; i++)
                {
                    if (!children[i].IsLeaf)
                    {
                        allChildrenAreLeaves = false;
                        break;
                    }
                }

                if (allChildrenAreLeaves)
                {
                    // Preserve leaf expansion state and preorder events without pushing a stack
                    // frame for every terminal node in wide hierarchies.
                    traversedNodeCount += children.Count;
                    for (int i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        if (child.HasPendingBulkMaterializationCommit)
                        {
                            materializationChanged = true;
                            child.PendingBulkMaterializationCommitGeneration =
                                materializationCommitGeneration;
                            if (child.IsExpanded)
                            {
                                requiresVisibleMaterializationCommitClear = true;
                            }
                        }

                        if (!child.IsExpanded)
                        {
                            anyExpanded = true;
                            (expandedNodes ??= new List<HierarchicalNode>()).Add(child);
                            expandedNodesHavePropertyObservers |= child.HasPropertyChangedObservers;
                        }
                    }

                    if (current.Level >= hashedCycleDepth)
                    {
                        ancestors!.Remove(current.Item);
                    }
                    continue;
                }

                stack.Push((current, depth, true));
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    stack.Push((children[i], depth + 1, false));
                }
            }

            if (!anyExpanded && !materializationChanged)
            {
                return;
            }

            CommitBulkExpansion(
                start,
                expandedNodes,
                expansionStatesAlreadyApplied: false,
                materializationChanged,
                traversedNodeCount,
                materializationCommitGeneration,
                requiresVisibleMaterializationCommitClear,
                expandedNodesHavePropertyObservers);
        }

        private static void SetPendingBulkMaterializationCommit(
            IList<HierarchicalNode>? nodes,
            bool value)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].HasPendingBulkMaterializationCommit = value;
            }
        }

        private void CommitBulkExpansion(
            HierarchicalNode start,
            IList<HierarchicalNode>? expandedNodes,
            bool expansionStatesAlreadyApplied,
            bool materializationChanged,
            int traversedNodeCount,
            int materializationCommitGeneration,
            bool requiresVisibleMaterializationCommitClear,
            bool expandedNodesHavePropertyObservers)
        {
            var isVirtualRoot = IsVirtualRootNode(start);
            var startIndex = isVirtualRoot ? -1 : GetFlattenedIndex(start);
            var isVisible = isVirtualRoot || startIndex >= 0;
            var rangeStart = isVirtualRoot ? 0 : startIndex + 1;
            var oldVisibleCount = isVisible
                ? CountVisibleDescendantsInFlattened(start, startIndex)
                : 0;
            IList<HierarchicalNode>? oldVisibleNodes = oldVisibleCount > 0
                ? _flattened.GetRange(rangeStart, oldVisibleCount)
                : null;
            var oldExpandedCount = start.ExpandedCount;

            if (!expansionStatesAlreadyApplied && expandedNodes != null)
            {
                for (int i = 0; i < expandedNodes.Count; i++)
                {
                    expandedNodes[i].SetExpandedFromOwnerSilently(true);
                }
            }

            var capacity = Math.Max(oldVisibleCount, traversedNodeCount - (isVirtualRoot ? 1 : 0));
            var visibleNodes = new List<HierarchicalNode>(Math.Max(0, capacity));
            CollectVisibleChildrenAndUpdateCounts(
                start,
                visibleNodes,
                materializeMissingChildren: false);

            var expandedCountDelta = start.ExpandedCount - oldExpandedCount;
            if (start.Parent != null && expandedCountDelta != 0)
            {
                ApplyExpandedCountDelta(start.Parent, expandedCountDelta);
            }

            var flattenedChanged = isVisible &&
                !ReferenceSequenceEqual(oldVisibleNodes, visibleNodes);
            if (flattenedChanged)
            {
                _flattened.ReplaceRange(rangeStart, oldVisibleCount, visibleNodes);
                var indexMap = oldVisibleNodes != null && visibleNodes.Count > 0
                    ? BuildIndexMap(oldVisibleNodes, rangeStart, visibleNodes, rangeStart)
                    : null;
                OnFlattenedChanged(
                    new[] { new FlattenedChange(rangeStart, oldVisibleCount, visibleNodes.Count) },
                    indexMap);
            }
            else if (materializationChanged)
            {
                // Materialization can change internal node state without changing visibility.
                // Counts were updated above; no flattened notification is warranted.
                InvalidateFlattenedLookup();
            }

            if (expandedNodes != null)
            {
                var raiseNodeExpanded = HasNodeExpandedObservers;
                var deferExpandedNodeMaterializationCommitClear =
                    raiseNodeExpanded || expandedNodesHavePropertyObservers;
                for (int i = 0; i < expandedNodes.Count; i++)
                {
                    var expandedNode = expandedNodes[i];
                    if (expandedNode.HasPropertyChangedObservers)
                    {
                        expandedNode.RaiseExpandedChanged();
                    }

                    if (raiseNodeExpanded)
                    {
                        OnNodeExpanded(expandedNode);
                    }

                    if (!deferExpandedNodeMaterializationCommitClear)
                    {
                        ClearPendingBulkMaterializationCommit(
                            expandedNode,
                            materializationCommitGeneration);
                    }
                }

                if (deferExpandedNodeMaterializationCommitClear)
                {
                    for (int i = 0; i < expandedNodes.Count; i++)
                    {
                        ClearPendingBulkMaterializationCommit(
                            expandedNodes[i],
                            materializationCommitGeneration);
                    }
                }
            }

            ClearPendingBulkMaterializationCommit(start, materializationCommitGeneration);
            if (requiresVisibleMaterializationCommitClear)
            {
                for (int i = 0; i < visibleNodes.Count; i++)
                {
                    ClearPendingBulkMaterializationCommit(
                        visibleNodes[i],
                        materializationCommitGeneration);
                }
            }
        }

        private static void ClearPendingBulkMaterializationCommit(
            HierarchicalNode node,
            int materializationCommitGeneration)
        {
            if (node.PendingBulkMaterializationCommitGeneration ==
                materializationCommitGeneration)
            {
                node.PendingBulkMaterializationCommitGeneration = 0;
            }
        }

        private static bool ReferenceSequenceEqual(
            IList<HierarchicalNode>? oldNodes,
            IList<HierarchicalNode> newNodes)
        {
            var oldCount = oldNodes?.Count ?? 0;
            if (oldCount != newNodes.Count)
            {
                return false;
            }

            for (int i = 0; i < oldCount; i++)
            {
                if (!ReferenceEquals(oldNodes![i], newNodes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public void CollapseAll(HierarchicalNode? node = null, int? minDepth = null)
        {
            var start = node ?? Root;
            if (start == null)
            {
                return;
            }

            var targetDepth = minDepth ?? 0;
            var hasVirtualRoot = _isVirtualRoot && ReferenceEquals(start, Root);
            if (hasVirtualRoot)
            {
                targetDepth++;
            }
            var stack = new Stack<(HierarchicalNode Node, int Depth, bool Visited)>();
            stack.Push((start, 0, false));

            while (stack.Count > 0)
            {
                var (current, depth, visited) = stack.Pop();

                if (!visited)
                {
                    if (current.IsExpanded || (hasVirtualRoot && ReferenceEquals(current, Root)))
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
            InvalidateFlattenedLookup();
            var version = ++FlattenedVersion;
            var args = new FlattenedChangedEventArgs(changes, version, _flattened.Count, indexMapOverride);
            OnFlattenedChangedTyped(args);
            FlattenedChanged?.Invoke(this, args);
        }

        protected virtual void OnFlattenedChangedTyped(FlattenedChangedEventArgs args)
        {
        }

        protected virtual void OnNodeExpanded(HierarchicalNode node)
        {
            SyncItemExpandedState(node, isExpanded: true);
            NodeExpanded?.Invoke(this, new HierarchicalNodeEventArgs(node));
        }

        protected virtual bool HasNodeExpandedObservers => HasExpandedStateSetter || NodeExpanded != null;

        protected virtual void OnNodeCollapsed(HierarchicalNode node)
        {
            SyncItemExpandedState(node, isExpanded: false);
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

        private void SyncItemExpandedState(HierarchicalNode node, bool isExpanded)
        {
            if (!HasExpandedStateSetter || node.Item is VirtualRootContainer)
            {
                return;
            }

            if (TryGetItemExpandedState(node.Item, out var current) && current == isExpanded)
            {
                return;
            }

            if (!_expandedStateUpdates.Add(node))
            {
                return;
            }

            try
            {
                if (Options.IsExpandedSetter != null)
                {
                    Options.IsExpandedSetter(node.Item, isExpanded);
                }
                else if (!string.IsNullOrEmpty(Options.IsExpandedPropertyPath))
                {
                    SetPropertyPathValue(node.Item, Options.IsExpandedPropertyPath!, isExpanded);
                }
            }
            finally
            {
                _expandedStateUpdates.Remove(node);
            }
        }

        private void SetNodeExpandedState(HierarchicalNode node, bool isExpanded)
        {
            node.SetExpandedFromOwner(isExpanded);
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
            List<HierarchicalNode>? oldNodes = null;
            if (notify && oldCount > 0)
            {
                oldNodes = new List<HierarchicalNode>(oldCount);
                oldNodes.AddRange(_flattened);
            }
            _flattened.ResetWith(nodes ?? Array.Empty<HierarchicalNode>());

            if (notify)
            {
                var indexMap = oldNodes != null && _flattened.Count > 0
                    ? BuildIndexMap(oldNodes, 0, _flattened, 0)
                    : null;
                OnFlattenedChanged(new[] { new FlattenedChange(0, oldCount, _flattened.Count) }, indexMap);
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
            List<HierarchicalNode>? ignoredMaterializationCommits = null;
            return InsertVisibleChildren(
                parent,
                insertIndex,
                collectMaterializationCommits: false,
                ref ignoredMaterializationCommits);
        }

        private int InsertVisibleChildren(
            HierarchicalNode parent,
            int insertIndex,
            ref List<HierarchicalNode>? materializationCommits)
        {
            return InsertVisibleChildren(
                parent,
                insertIndex,
                collectMaterializationCommits: true,
                ref materializationCommits);
        }

        private int InsertVisibleChildren(
            HierarchicalNode parent,
            int insertIndex,
            bool collectMaterializationCommits,
            ref List<HierarchicalNode>? materializationCommits)
        {
            var buffer = new List<HierarchicalNode>();
            CollectVisibleChildren(
                parent,
                buffer,
                collectMaterializationCommits,
                ref materializationCommits);

            if (buffer.Count > 0)
            {
                _flattened.InsertRange(insertIndex, buffer);
            }

            return buffer.Count;
        }

        private void CollectVisibleChildren(
            HierarchicalNode parent,
            List<HierarchicalNode> buffer)
        {
            List<HierarchicalNode>? ignoredMaterializationCommits = null;
            CollectVisibleChildren(
                parent,
                buffer,
                collectMaterializationCommits: false,
                ref ignoredMaterializationCommits);
        }

        private void CollectVisibleChildren(
            HierarchicalNode parent,
            List<HierarchicalNode> buffer,
            bool collectMaterializationCommits,
            ref List<HierarchicalNode>? materializationCommits)
        {
            if (!parent.IsExpanded || parent.IsLeaf)
            {
                return;
            }

            if (!parent.HasMaterializedChildren)
            {
                EnsureChildrenMaterialized(parent);
            }

            var stack = new Stack<HierarchicalNode>();
            var children = parent.MutableChildren;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                buffer.Add(current);
                if (collectMaterializationCommits && current.HasPendingBulkMaterializationCommit)
                {
                    (materializationCommits ??= new List<HierarchicalNode>()).Add(current);
                }

                if (!current.IsExpanded || current.IsLeaf)
                {
                    continue;
                }

                if (!current.HasMaterializedChildren)
                {
                    EnsureChildrenMaterialized(current);
                }

                var currentChildren = current.MutableChildren;
                for (int i = currentChildren.Count - 1; i >= 0; i--)
                {
                    stack.Push(currentChildren[i]);
                }
            }
        }

        private int CollectVisibleChildrenAndUpdateCounts(
            HierarchicalNode parent,
            List<HierarchicalNode> buffer,
            bool materializeMissingChildren = true)
        {
            if (!parent.IsExpanded || parent.IsLeaf)
            {
                parent.ExpandedCount = 0;
                return 0;
            }

            if (!parent.HasMaterializedChildren)
            {
                if (materializeMissingChildren)
                {
                    EnsureChildrenMaterialized(parent);
                }

                if (!parent.HasMaterializedChildren)
                {
                    parent.ExpandedCount = 0;
                    return 0;
                }
            }

            var firstBufferIndex = buffer.Count;
            var stack = new Stack<HierarchicalNode>();
            var children = parent.MutableChildren;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                buffer.Add(current);

                if (!current.IsExpanded || current.IsLeaf)
                {
                    current.ExpandedCount = 0;
                    continue;
                }

                if (!current.HasMaterializedChildren && materializeMissingChildren)
                {
                    EnsureChildrenMaterialized(current);
                }

                if (!current.HasMaterializedChildren)
                {
                    current.ExpandedCount = 0;
                    continue;
                }

                var currentChildren = current.MutableChildren;
                for (int i = currentChildren.Count - 1; i >= 0; i--)
                {
                    stack.Push(currentChildren[i]);
                }
            }

            for (int i = buffer.Count - 1; i >= firstBufferIndex; i--)
            {
                UpdateExpandedCountFromMaterializedChildren(buffer[i]);
            }

            UpdateExpandedCountFromMaterializedChildren(parent);
            return buffer.Count - firstBufferIndex;
        }

        private static void UpdateExpandedCountFromMaterializedChildren(HierarchicalNode node)
        {
            if (!node.IsExpanded || node.IsLeaf || !node.HasMaterializedChildren)
            {
                node.ExpandedCount = 0;
                return;
            }

            var total = 0;
            var children = node.MutableChildren;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                total += 1 + (child.IsExpanded ? child.ExpandedCount : 0);
            }

            node.ExpandedCount = total;
        }

        private int CountVisibleDescendantsIterative(HierarchicalNode node)
        {
            int count = 0;
            var stack = new Stack<HierarchicalNode>();
            var children = node.MutableChildren;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }

            while (stack.Count > 0)
            {
                var child = stack.Pop();
                count++;
                if (!child.IsExpanded || child.IsLeaf)
                {
                    continue;
                }

                if (!child.HasMaterializedChildren)
                {
                    EnsureChildrenMaterialized(child);
                }

                var childNodes = child.MutableChildren;
                for (int i = childNodes.Count - 1; i >= 0; i--)
                {
                    stack.Push(childNodes[i]);
                }
            }

            return count;
        }

        private int GetVisibleVisibleCount(HierarchicalNode node)
        {
            return 1 + (node.IsExpanded ? node.ExpandedCount : 0);
        }

        private bool IsVirtualRootNode(HierarchicalNode node)
        {
            return _isVirtualRoot && ReferenceEquals(node, Root);
        }

        private int GetFlattenedIndex(HierarchicalNode node)
        {
            if (IsVirtualRootNode(node))
            {
                return -1;
            }

            EnsureFlattenedLookup();
            if (_flattenedNodeIndexLookup != null &&
                _flattenedNodeIndexLookup.TryGetValue(node, out var index))
            {
                return index;
            }

            return -1;
        }

        private int GetVisibleDescendantCount(HierarchicalNode node, int parentIndex)
        {
            if (parentIndex < 0 && !IsVirtualRootNode(node))
            {
                return 0;
            }

            if (node.IsExpanded)
            {
                return node.ExpandedCount;
            }

            return CountVisibleDescendantsInFlattened(node, parentIndex);
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
            var removeCount = GetVisibleDescendantCount(node, parentIndex);
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
            var children = parent.MutableChildren;
            if (children.Count == 0 || childIndex <= 0)
            {
                return 0;
            }

            if (!parent.IsExpanded)
            {
                return 0;
            }

            var capped = Math.Min(childIndex, children.Count);
            if (capped == 0)
            {
                return 0;
            }

            var totalVisible = parent.ExpandedCount;
            if (capped == children.Count)
            {
                return totalVisible;
            }

            var forwardCount = capped;
            var backwardCount = children.Count - capped;
            if (forwardCount <= backwardCount)
            {
                int offset = 0;
                for (int i = 0; i < capped; i++)
                {
                    var child = children[i];
                    offset += 1 + (child.IsExpanded ? child.ExpandedCount : 0);
                }

                return offset;
            }

            int tail = 0;
            for (int i = children.Count - 1; i >= capped; i--)
            {
                var child = children[i];
                tail += 1 + (child.IsExpanded ? child.ExpandedCount : 0);
            }

            return totalVisible - tail;
        }

        private bool TryNormalizeAddChange(HierarchicalNode parent, ref NotifyCollectionChangedEventArgs e)
        {
            if (e.NewStartingIndex >= 0)
            {
                return true;
            }

            if (e.NewItems == null || e.NewItems.Count == 0)
            {
                return true;
            }

            if (parent.ChildrenSource is not IList list)
            {
                return false;
            }

            var firstIndex = list.IndexOf(e.NewItems[0]);
            if (firstIndex < 0)
            {
                return false;
            }

            for (int i = 1; i < e.NewItems.Count; i++)
            {
                if (list.IndexOf(e.NewItems[i]) != firstIndex + i)
                {
                    return false;
                }
            }

            e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, firstIndex);
            return true;
        }

        private bool ShouldRefreshForChildrenChange(HierarchicalNode parent, ref NotifyCollectionChangedEventArgs e)
        {
            if (GetComparerForParent(parent) != null)
            {
                return true;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    return !TryNormalizeAddChange(parent, ref e);
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    return e.OldStartingIndex < 0;
                case NotifyCollectionChangedAction.Move:
                    return e.OldStartingIndex < 0 || e.NewStartingIndex < 0;
                case NotifyCollectionChangedAction.Reset:
                default:
                    return true;
            }
        }

        private void OnChildrenCollectionChanged(HierarchicalNode parent, NotifyCollectionChangedEventArgs e)
        {
            if (ShouldRefreshForChildrenChange(parent, ref e))
            {
                Refresh(parent);
                return;
            }

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
                InitializeNode(childNode);
                newNodes.Add(childNode);
            }

            if (newNodes.Count == 0)
            {
                return;
            }

            var addedVisibleCount = 0;
            var isVirtualRootParent = IsVirtualRootNode(parent);
            var parentIndex = -1;
            var canUpdateFlattened = parent.IsExpanded;
            var visibleOffset = 0;
            var flattenedIndex = -1;
            if (canUpdateFlattened)
            {
                if (!isVirtualRootParent)
                {
                    parentIndex = GetFlattenedIndex(parent);
                }

                canUpdateFlattened = parentIndex >= 0 || isVirtualRootParent;
                if (canUpdateFlattened)
                {
                    visibleOffset = GetVisibleOffsetForChildIndex(parent, insertIndex);
                    flattenedIndex = isVirtualRootParent
                        ? visibleOffset
                        : parentIndex + 1 + visibleOffset;
                }
            }

            parent.MutableChildren.InsertRange(insertIndex, newNodes);
            parent.IsLeaf = parent.MutableChildren.Count == 0;

            if (canUpdateFlattened)
            {
                var visibleNodes = new List<HierarchicalNode>();
                foreach (var child in newNodes)
                {
                    visibleNodes.Add(child);
                    if (child.IsExpanded && !child.IsLeaf)
                    {
                        var childDesc = CollectVisibleChildrenAndUpdateCounts(child, visibleNodes);
                        addedVisibleCount += 1 + childDesc;
                    }
                    else
                    {
                        child.ExpandedCount = 0;
                        addedVisibleCount += 1;
                    }
                }

                if (visibleNodes.Count > 0)
                {
                    _flattened.InsertRange(flattenedIndex, visibleNodes);
                    OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, 0, visibleNodes.Count) });
                }
            }
            else if (parent.IsExpanded)
            {
                foreach (var child in newNodes)
                {
                    if (child.IsExpanded && !child.IsLeaf)
                    {
                        var childDesc = RecalculateExpandedCountIterative(child);
                        addedVisibleCount += 1 + childDesc;
                    }
                    else
                    {
                        child.ExpandedCount = 0;
                        addedVisibleCount += 1;
                    }
                }
            }
            else
            {
                foreach (var child in newNodes)
                {
                    child.ExpandedCount = 0;
                }
            }

            ApplyExpandedCountDelta(parent, addedVisibleCount);
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
            if (removeCount <= 0)
            {
                return;
            }

            var isVirtualRootParent = IsVirtualRootNode(parent);
            var parentIndex = -1;
            var canUpdateFlattened = parent.IsExpanded;
            if (canUpdateFlattened)
            {
                if (!isVirtualRootParent)
                {
                    parentIndex = GetFlattenedIndex(parent);
                }

                canUpdateFlattened = parentIndex >= 0 || isVirtualRootParent;
            }

            var visibleOffset = 0;
            if (canUpdateFlattened)
            {
                visibleOffset = GetVisibleOffsetForChildIndex(parent, removeIndex);
            }
            var removedNodes = parent.MutableChildren.GetRange(removeIndex, removeCount);
            parent.MutableChildren.RemoveRange(removeIndex, removeCount);

            parent.IsLeaf = parent.MutableChildren.Count == 0;

            if (!parent.IsExpanded || removedNodes.Count == 0)
            {
                foreach (var removed in removedNodes)
                {
                    DetachHierarchy(removed);
                }
                ApplyExpandedCountDelta(parent, 0);
                OnHierarchyChanged(parent, NotifyCollectionChangedAction.Remove);
                return;
            }

            var totalRemoved = 0;
            foreach (var child in removedNodes)
            {
                totalRemoved += GetVisibleVisibleCount(child);
            }

            if (canUpdateFlattened && totalRemoved > 0)
            {
                var flattenedIndex = isVirtualRootParent
                    ? visibleOffset
                    : parentIndex + 1 + visibleOffset;
                _flattened.RemoveRange(flattenedIndex, totalRemoved);
                OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, totalRemoved, 0) });
            }

            foreach (var removed in removedNodes)
            {
                DetachHierarchy(removed);
            }

            ApplyExpandedCountDelta(parent, -totalRemoved);
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
            if (removeCount <= 0)
            {
                Refresh(parent);
                return;
            }
            var isVirtualRootParent = IsVirtualRootNode(parent);
            var parentIndex = -1;
            var canUpdateFlattened = parent.IsExpanded;
            if (canUpdateFlattened)
            {
                if (!isVirtualRootParent)
                {
                    parentIndex = GetFlattenedIndex(parent);
                }

                canUpdateFlattened = parentIndex >= 0 || isVirtualRootParent;
            }

            var visibleOffset = 0;
            if (canUpdateFlattened)
            {
                visibleOffset = GetVisibleOffsetForChildIndex(parent, replaceIndex);
            }
            var removedNodes = parent.MutableChildren.GetRange(replaceIndex, removeCount);
            parent.MutableChildren.RemoveRange(replaceIndex, removeCount);
            foreach (var old in removedNodes)
            {
                DetachHierarchy(old);
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
                InitializeNode(node);
                newNodes.Add(node);
            }

            parent.MutableChildren.InsertRange(replaceIndex, newNodes);
            parent.IsLeaf = parent.MutableChildren.Count == 0;

            var insertedVisible = 0;
            if (canUpdateFlattened)
            {
                var flattenedIndex = isVirtualRootParent
                    ? visibleOffset
                    : parentIndex + 1 + visibleOffset;

                if (removedVisible > 0)
                {
                    _flattened.RemoveRange(flattenedIndex, removedVisible);
                }

                var visibleNodes = new List<HierarchicalNode>();
                foreach (var node in newNodes)
                {
                    visibleNodes.Add(node);
                    if (node.IsExpanded && !node.IsLeaf)
                    {
                        var nodeDesc = CollectVisibleChildrenAndUpdateCounts(node, visibleNodes);
                        insertedVisible += 1 + nodeDesc;
                    }
                    else
                    {
                        node.ExpandedCount = 0;
                        insertedVisible += 1;
                    }
                }

                if (visibleNodes.Count > 0)
                {
                    _flattened.InsertRange(flattenedIndex, visibleNodes);
                }

                OnFlattenedChanged(new[] { new FlattenedChange(flattenedIndex, removedVisible, insertedVisible) });
            }
            else if (parent.IsExpanded)
            {
                foreach (var node in newNodes)
                {
                    if (node.IsExpanded && !node.IsLeaf)
                    {
                        var nodeDesc = RecalculateExpandedCountIterative(node);
                        insertedVisible += 1 + nodeDesc;
                    }
                    else
                    {
                        node.ExpandedCount = 0;
                        insertedVisible += 1;
                    }
                }
            }
            else
            {
                foreach (var node in newNodes)
                {
                    node.ExpandedCount = 0;
                }
            }

            ApplyExpandedCountDelta(parent, insertedVisible - removedVisible);
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

            var isVirtualRootParent = IsVirtualRootNode(parent);
            var parentIndex = -1;
            var expandedAndVisible = parent.IsExpanded;
            if (expandedAndVisible)
            {
                if (!isVirtualRootParent)
                {
                    parentIndex = GetFlattenedIndex(parent);
                }

                expandedAndVisible = parentIndex >= 0 || isVirtualRootParent;
            }

            var removeOffset = 0;
            if (expandedAndVisible)
            {
                removeOffset = GetVisibleOffsetForChildIndex(parent, moveIndex);
            }
            var movedNodes = parent.MutableChildren.GetRange(moveIndex, moveCount);
            parent.MutableChildren.RemoveRange(moveIndex, moveCount);

            var removedVisible = expandedAndVisible ? movedNodes.Sum(GetVisibleVisibleCount) : 0;

            var insertIndex = Math.Min(Math.Max(0, targetIndex), parent.MutableChildren.Count);
            parent.MutableChildren.InsertRange(insertIndex, movedNodes);

            if (expandedAndVisible)
            {
                var removedAt = isVirtualRootParent
                    ? removeOffset
                    : parentIndex + 1 + removeOffset;

                IList<HierarchicalNode>? visibleNodes = null;
                if (removedVisible > 0)
                {
                    visibleNodes = _flattened.GetRange(removedAt, removedVisible);
                    _flattened.RemoveRange(removedAt, removedVisible);
                }

                var insertOffset = GetVisibleOffsetForChildIndex(parent, insertIndex);
                var insertAt = isVirtualRootParent
                    ? insertOffset
                    : parentIndex + 1 + insertOffset;

                if (visibleNodes != null && visibleNodes.Count > 0)
                {
                    _flattened.InsertRange(insertAt, visibleNodes);
                }

                var insertedVisible = visibleNodes?.Count ?? 0;
                var indexMap = new Dictionary<int, int>();

                for (int i = 0; i < insertedVisible; i++)
                {
                    indexMap[removedAt + i] = insertAt + i;
                }

                OnFlattenedChanged(new[]
                {
                    new FlattenedChange(removedAt, removedVisible, 0),
                    new FlattenedChange(insertAt, 0, insertedVisible)
                }, indexMap.Count > 0 ? indexMap : null);
            }

            OnHierarchyChanged(parent, NotifyCollectionChangedAction.Move);
        }

        private void InitializeNode(HierarchicalNode node)
        {
            node.Owner = this;
            if (!HasExpandedStateSelector)
            {
                return;
            }

            ApplyExpandedStateFromItem(node);
            AttachExpandedStateNotifier(node);
        }

        private void ApplyExpandedStateFromItem(HierarchicalNode node)
        {
            if (TryGetItemExpandedState(node.Item, out var isExpanded))
            {
                SetNodeExpandedState(node, isExpanded);
            }
        }

        private sealed class ExpandedStateHandler
        {
            private readonly HierarchicalModel _owner;
            private readonly HierarchicalNode _node;

            public ExpandedStateHandler(HierarchicalModel owner, HierarchicalNode node)
            {
                _owner = owner;
                _node = node;
            }

            public void Handle(object? sender, PropertyChangedEventArgs e)
            {
                _owner.OnItemExpandedStateChanged(_node, e);
            }
        }

        private sealed class ChildrenChangedHandler
        {
            private readonly HierarchicalModel _owner;
            private readonly HierarchicalNode _node;

            public ChildrenChangedHandler(HierarchicalModel owner, HierarchicalNode node)
            {
                _owner = owner;
                _node = node;
            }

            public void Handle(object? sender, NotifyCollectionChangedEventArgs e)
            {
                _owner.OnChildrenCollectionChanged(_node, e);
            }
        }

        private void AttachExpandedStateNotifier(HierarchicalNode node)
        {
            if (node.Item is not INotifyPropertyChanged notifier)
            {
                return;
            }

            DetachExpandedStateNotifier(node);

            var handler = new ExpandedStateHandler(this, node);
            EventHandler<PropertyChangedEventArgs> handlerDelegate = handler.Handle;
            WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, ExpandedStateHandler>(
                notifier,
                nameof(INotifyPropertyChanged.PropertyChanged),
                handlerDelegate);
            node.ExpandedStateNotifier = notifier;
            node.ExpandedStateChangedHandler = handlerDelegate;
        }

        private void DetachExpandedStateNotifier(HierarchicalNode node)
        {
            if (node.ExpandedStateNotifier != null && node.ExpandedStateChangedHandler != null)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, ExpandedStateHandler>(
                    node.ExpandedStateNotifier,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    node.ExpandedStateChangedHandler);
            }

            node.ExpandedStateNotifier = null;
            node.ExpandedStateChangedHandler = null;
        }

        internal void OnNodeExpandedStateChanged(HierarchicalNode node)
        {
            if (_isVirtualRoot && ReferenceEquals(node, Root))
            {
                return;
            }

            if (node.IsExpanded)
            {
                Expand(node);
            }
            else
            {
                Collapse(node);
            }
        }

        private void OnItemExpandedStateChanged(HierarchicalNode node, PropertyChangedEventArgs e)
        {
            if (!HasExpandedStateSelector || _expandedStateUpdates.Contains(node))
            {
                return;
            }

            if (!ShouldProcessExpandedStateChange(e))
            {
                return;
            }

            if (!TryGetItemExpandedState(node.Item, out var desired))
            {
                return;
            }

            if (desired == node.IsExpanded)
            {
                return;
            }

            if (desired)
            {
                Expand(node);
            }
            else
            {
                Collapse(node);
            }
        }

        private bool ShouldProcessExpandedStateChange(PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                return true;
            }

            if (Options.IsExpandedSelector != null)
            {
                return true;
            }

            var propertyName = GetExpandedStatePropertyName();
            if (string.IsNullOrEmpty(propertyName))
            {
                return true;
            }

            return string.Equals(e.PropertyName, propertyName, StringComparison.Ordinal);
        }

        private string? GetExpandedStatePropertyName()
        {
            var path = Options.IsExpandedPropertyPath;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var index = path.LastIndexOf('.');
            return index >= 0 ? path.Substring(index + 1) : path;
        }

        private void AttachChildrenNotifier(HierarchicalNode node, IEnumerable children)
        {
            if (children is INotifyCollectionChanged notifier)
            {
                DetachChildrenNotifier(node);

                var handler = new ChildrenChangedHandler(this, node);
                EventHandler<NotifyCollectionChangedEventArgs> handlerDelegate = handler.Handle;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, ChildrenChangedHandler>(
                    notifier,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    handlerDelegate);
                node.ChildrenNotifier = notifier;
                node.ChildrenChangedHandler = handlerDelegate;
            }
        }

        private void DetachChildrenNotifier(HierarchicalNode node)
        {
            if (node.ChildrenNotifier != null && node.ChildrenChangedHandler != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, ChildrenChangedHandler>(
                    node.ChildrenNotifier,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    node.ChildrenChangedHandler);
            }

            node.ChildrenNotifier = null;
            node.ChildrenChangedHandler = null;
        }

        private void DetachHierarchy(HierarchicalNode node)
        {
            CancelPendingLoad(node);
            ClearLoadState(node);
            DetachChildrenNotifier(node);
            DetachExpandedStateNotifier(node);
            node.Owner = null;

            foreach (var child in node.Children)
            {
                DetachHierarchy(child);
            }
        }

        private void DematerializeDescendants(HierarchicalNode node, bool detachSelf = false)
        {
            CancelPendingLoad(node);
            ClearLoadState(node);
            foreach (var child in node.MutableChildren)
            {
                SetNodeExpandedState(child, false);
                DematerializeDescendants(child, detachSelf: true);
            }

            DetachChildrenNotifier(node);
            if (detachSelf)
            {
                DetachExpandedStateNotifier(node);
                node.Owner = null;
            }
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
            if (Options.ChildrenSelectorAsync == null)
            {
                return EnsureChildrenMaterializedSynchronously(node, forceReload);
            }

            return EnsureChildrenMaterializedAsync(node, forceReload, CancellationToken.None).GetAwaiter().GetResult();
        }

        private IReadOnlyList<HierarchicalNode> EnsureChildrenMaterializedSynchronously(
            HierarchicalNode node,
            bool forceReload,
            ISet<object>? ancestors = null,
            bool resolveAsyncChildrenOffContext = false)
        {
            if (node.HasMaterializedChildren && !forceReload)
            {
                return node.Children;
            }

            if (resolveAsyncChildrenOffContext &&
                _loadStates.TryGetValue(node, out var pendingState) &&
                pendingState.Task is { IsCompleted: false })
            {
                // Waiting here can deadlock when the pending load needs the caller's UI context,
                // while starting another selector would race two writers into the same node.
                throw new InvalidOperationException(
                    "A child load is already in progress. Await ExpandAllAsync before using synchronous bulk expansion.");
            }

            if (_loadStates.TryGetValue(node, out var existingState) && existingState.NextRetryUtc.HasValue)
            {
                var now = DateTime.UtcNow;
                if (existingState.NextRetryUtc.Value > now)
                {
                    var delay = existingState.NextRetryUtc.Value - now;
                    OnNodeLoadRetryScheduled(node, delay);
                    Thread.Sleep(delay);
                }
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
                    if (existingState != null)
                    {
                        ResetRetryState(existingState);
                    }
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
                var children = resolveAsyncChildrenOffContext
                    ? ResolveAsyncChildrenWithoutCallerContext(node.Item)
                    : ResolveChildrenSynchronously(node.Item);
                if (children == null)
                {
                    node.ChildrenSource = null;
                    DetachChildrenNotifier(node);
                    node.IsLeaf = true;
                    node.HasMaterializedChildren = true;
                    OnNodeLoaded(node);
                    if (existingState != null)
                    {
                        ResetRetryState(existingState);
                    }
                    return node.Children;
                }

                node.ChildrenSource = children;
                AttachChildrenNotifier(node, children);

                var mutableChildren = node.MutableChildren;
                if (children is ICollection collection && collection.Count > mutableChildren.Capacity)
                {
                    mutableChildren.Capacity = collection.Count;
                }

                if (children is IList childList)
                {
                    for (int i = 0; i < childList.Count; i++)
                    {
                        AddMaterializedChild(node, childList[i], ancestors);
                    }
                }
                else
                {
                    foreach (var childItem in children)
                    {
                        AddMaterializedChild(node, childItem, ancestors);
                    }
                }

                var comparer = GetComparerForParent(node);
                if (comparer != null)
                {
                    node.MutableChildren.Sort((x, y) => comparer.Compare(x.Item, y.Item));
                }

                node.HasMaterializedChildren = true;
                node.IsLeaf = node.MutableChildren.Count == 0;
                OnNodeLoaded(node);
                if (existingState != null)
                {
                    ResetRetryState(existingState);
                }
                return node.Children;
            }
            catch (Exception ex)
            {
                node.ChildrenSource = null;
                DetachChildrenNotifier(node);
                node.IsLeaf = true;
                node.HasMaterializedChildren = false;
                node.LoadError = ex;
                var state = existingState ?? GetLoadState(node);
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

        private IEnumerable? ResolveAsyncChildrenWithoutCallerContext(object item)
        {
            // Preserve selector invocation affinity (callers commonly read UI-owned view-model
            // state before their first await), but prevent an ordinary await from capturing the
            // synchronization context that this compatibility API must block. Selectors that
            // explicitly marshal back to the blocked caller should use ExpandAllAsync instead.
            var callerContext = SynchronizationContext.Current;
            Task<IEnumerable?> resolution;
            try
            {
                if (callerContext != null)
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }

                resolution = ResolveChildrenAsync(item, CancellationToken.None);
            }
            finally
            {
                if (callerContext != null)
                {
                    SynchronizationContext.SetSynchronizationContext(callerContext);
                }
            }

            return resolution.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void AddMaterializedChild(
            HierarchicalNode parent,
            object? childItem,
            ISet<object>? ancestors)
        {
            if (childItem == null)
            {
                return;
            }

            if (ancestors?.Contains(childItem) ?? CreatesCycle(parent, childItem))
            {
                OnNodeLoadFailed(parent, new InvalidOperationException("Cycle detected in hierarchical data."));
                return;
            }

            var childNode = new HierarchicalNode(
                childItem,
                parent,
                parent.Level + 1,
                isLeaf: DetermineInitialLeaf(childItem));

            if (HasReachedMaxDepth(childNode))
            {
                childNode.IsLeaf = true;
                childNode.HasMaterializedChildren = true;
            }

            InitializeNode(childNode);
            parent.MutableChildren.Add(childNode);
        }

        private async Task<IReadOnlyList<HierarchicalNode>> EnsureChildrenMaterializedAsync(
            HierarchicalNode node,
            bool forceReload,
            CancellationToken cancellationToken,
            ISet<object>? ancestors = null,
            bool continueOnCapturedContext = false)
        {
            if (node.HasMaterializedChildren && !forceReload)
            {
                return node.Children;
            }

            var state = GetLoadState(node);
            if (state.Task != null && !state.Task.IsCompleted && !forceReload)
            {
                return await state.Task.ConfigureAwait(continueOnCapturedContext);
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
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(continueOnCapturedContext);
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

            var loadTask = LoadChildrenWithStateAsync(
                node,
                forceReload,
                state,
                linkedCts.Token,
                ancestors,
                continueOnCapturedContext);
            state.Task = loadTask;

            try
            {
                return await loadTask.ConfigureAwait(continueOnCapturedContext);
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
            CancellationToken cancellationToken,
            ISet<object>? ancestors,
            bool continueOnCapturedContext)
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
                var children = await ResolveChildrenAsync(node.Item, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext);

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

                var mutableChildren = node.MutableChildren;
                if (children is ICollection collection && collection.Count > mutableChildren.Capacity)
                {
                    mutableChildren.Capacity = collection.Count;
                }

                if (children is IList childList)
                {
                    for (int i = 0; i < childList.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddMaterializedChild(node, childList[i], ancestors);
                    }
                }
                else
                {
                    foreach (var childItem in children)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddMaterializedChild(node, childItem, ancestors);
                    }
                }

                var comparer = GetComparerForParent(node);
                if (comparer != null)
                {
                    node.MutableChildren.Sort((x, y) => comparer.Compare(x.Item, y.Item));
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

        protected virtual bool DetermineInitialLeaf(object item)
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

        private IComparer<object>? GetComparerForParent(HierarchicalNode parent)
        {
            if (Options.SiblingComparerSelector != null)
            {
                try
                {
                    var comparer = Options.SiblingComparerSelector(parent.Item);
                    if (comparer != null)
                    {
                        return comparer;
                    }
                }
                catch
                {
                    // Ignore selector failures and fall back.
                }
            }

            return Options.SiblingComparer;
        }

        private Task<IEnumerable?> ResolveChildrenAsync(object item, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                return Task.FromResult<IEnumerable?>(null);
            }

            if (item is VirtualRootContainer virtualRoot)
            {
                return Task.FromResult<IEnumerable?>(virtualRoot.Items);
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

        protected virtual IEnumerable? ResolveChildrenSynchronously(object item)
        {
            if (item is VirtualRootContainer virtualRoot)
            {
                return virtualRoot.Items;
            }

            if (Options.ChildrenSelector != null)
            {
                return Options.ChildrenSelector(item);
            }

            if (Options.TreatGroupsAsNodes)
            {
                if (item is DataGridCollectionViewGroup group)
                {
                    return group.Items;
                }

                if (item is DataGridCollectionView view &&
                    (view.GroupDescriptions?.Count > 0 || view.Groups != null))
                {
                    return view.Groups;
                }
            }

            if (Options.ItemsSelector != null)
            {
                return Options.ItemsSelector(item);
            }

            if (!string.IsNullOrEmpty(Options.ChildrenPropertyPath))
            {
                return GetPropertyPathValue(item, Options.ChildrenPropertyPath!) as IEnumerable;
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

        private void SortChildren(HierarchicalNode parent, IComparer<object>? comparerOverride, bool recursive)
        {
            var comparer = comparerOverride ?? GetComparerForParent(parent);
            if (comparer != null)
            {
                parent.MutableChildren.Sort((x, y) => comparer.Compare(x.Item, y.Item));
            }

            if (!recursive)
            {
                return;
            }

            foreach (var child in parent.MutableChildren)
            {
                if (child.IsExpanded)
                {
                    EnsureChildrenMaterialized(child);
                    SortChildren(child, comparerOverride, recursive);
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

        private void SetPropertyPathValue(object target, string propertyPath, object? value)
        {
            var key = (target.GetType(), propertyPath);

            if (!_propertyPathSetterCache.TryGetValue(key, out var setter))
            {
                setter = CreatePropertyPathSetter(target.GetType(), propertyPath);
                _propertyPathSetterCache[key] = setter;
            }

            setter(target, value);
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

        private static Action<object, object?> CreatePropertyPathSetter(Type targetType, string propertyPath)
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

            var targetProperty = properties[properties.Length - 1];
            if (!targetProperty.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{targetProperty.Name}' on type '{targetProperty.DeclaringType?.FullName}' does not have a setter.");
            }

            return (instance, value) =>
            {
                object? current = instance;
                for (int i = 0; i < properties.Length - 1; i++)
                {
                    if (current == null)
                    {
                        return;
                    }

                    current = properties[i].GetValue(current);
                }

                if (current == null)
                {
                    return;
                }

                targetProperty.SetValue(current, value);
            };
        }

        internal static IReadOnlyDictionary<int, int>? BuildIndexMap(
            IList<HierarchicalNode> oldNodes,
            int oldStartIndex,
            IList<HierarchicalNode> newNodes,
            int newStartIndex)
        {
            if (oldNodes.Count == 0 || newNodes.Count == 0)
            {
                return null;
            }

            // Expansion normally preserves every previously visible node in the same order and
            // only inserts newly visible descendants between them. Handle that dominant case in
            // one linear reference-identity pass. Besides avoiding the general-purpose identity
            // and item-equality dictionaries, this guarantees that an inserted equal item cannot
            // steal a retained node's mapping.
            var orderedIdentityMap = TryBuildOrderedIdentityMap(
                oldNodes,
                oldStartIndex,
                newNodes,
                newStartIndex);
            if (orderedIdentityMap != null)
            {
                return orderedIdentityMap;
            }

            var matchedOld = new bool[oldNodes.Count];
            var matchedNew = new bool[newNodes.Count];
            var map = new Dictionary<int, int>(Math.Min(oldNodes.Count, newNodes.Count));

            // First preserve exact wrapper identity. This must be a complete pass before item
            // equality matching, otherwise a newly inserted equal value can steal a retained row.
            var nodeLookup = new Dictionary<HierarchicalNode, int>(oldNodes.Count);
            for (int i = 0; i < oldNodes.Count; i++)
            {
                if (!nodeLookup.ContainsKey(oldNodes[i]))
                {
                    nodeLookup.Add(oldNodes[i], i);
                }
            }

            for (int i = 0; i < newNodes.Count; i++)
            {
                if (nodeLookup.TryGetValue(newNodes[i], out var oldIndex) && !matchedOld[oldIndex])
                {
                    matchedOld[oldIndex] = true;
                    matchedNew[i] = true;
                    map[oldStartIndex + oldIndex] = newStartIndex + i;
                }
            }

            MatchUnmappedItems(
                oldNodes,
                oldStartIndex,
                newNodes,
                newStartIndex,
                matchedOld,
                matchedNew,
                map,
                ReferenceEqualityComparer.Instance);

            MatchUnmappedItems(
                oldNodes,
                oldStartIndex,
                newNodes,
                newStartIndex,
                matchedOld,
                matchedNew,
                map,
                EqualityComparer<object>.Default);

            return map.Count > 0 ? map : null;
        }

        private static IReadOnlyDictionary<int, int>? TryBuildOrderedIdentityMap(
            IList<HierarchicalNode> oldNodes,
            int oldStartIndex,
            IList<HierarchicalNode> newNodes,
            int newStartIndex)
        {
            var map = new Dictionary<int, int>(oldNodes.Count);
            var oldIndex = 0;
            for (int newIndex = 0; newIndex < newNodes.Count; newIndex++)
            {
                if (!ReferenceEquals(oldNodes[oldIndex], newNodes[newIndex]))
                {
                    continue;
                }

                map.Add(oldStartIndex + oldIndex, newStartIndex + newIndex);
                oldIndex++;
                if (oldIndex == oldNodes.Count)
                {
                    return map;
                }
            }

            return null;
        }

        private static void MatchUnmappedItems(
            IList<HierarchicalNode> oldNodes,
            int oldStartIndex,
            IList<HierarchicalNode> newNodes,
            int newStartIndex,
            bool[] matchedOld,
            bool[] matchedNew,
            IDictionary<int, int> map,
            IEqualityComparer<object> comparer)
        {
            var lookup = new Dictionary<object, Queue<int>>(oldNodes.Count, comparer);
            var nullKey = new object();
            for (int i = 0; i < oldNodes.Count; i++)
            {
                if (matchedOld[i])
                {
                    continue;
                }

                var item = oldNodes[i].Item;
                var key = item ?? nullKey;
                if (!lookup.TryGetValue(key, out var queue))
                {
                    queue = new Queue<int>();
                    lookup[key] = queue;
                }

                queue.Enqueue(i);
            }

            for (int i = 0; i < newNodes.Count; i++)
            {
                if (matchedNew[i])
                {
                    continue;
                }

                var item = newNodes[i].Item;
                var key = item ?? nullKey;
                if (!lookup.TryGetValue(key, out var queue) || queue.Count == 0)
                {
                    continue;
                }

                var oldIndex = queue.Dequeue();
                matchedOld[oldIndex] = true;
                matchedNew[i] = true;
                map[oldStartIndex + oldIndex] = newStartIndex + i;
            }
        }

        private void ApplyExpandedCountDelta(HierarchicalNode parent, int delta)
        {
            if (parent == null)
            {
                return;
            }

            if (!parent.IsExpanded)
            {
                parent.ExpandedCount = 0;
                return;
            }

            if (delta == 0)
            {
                return;
            }

            parent.ExpandedCount += delta;

            var current = parent.Parent;
            while (current != null)
            {
                if (!current.IsExpanded)
                {
                    break;
                }

                current.ExpandedCount += delta;
                current = current.Parent;
            }
        }

        private void RecalculateExpandedCountsFrom(HierarchicalNode node)
        {
            if (node == null)
            {
                return;
            }

            RecalculateExpandedCountIterative(node);
            RecalculateExpandedCountsUpwards(node.Parent);
        }

        private int RecalculateExpandedCountIterative(HierarchicalNode node)
        {
            if (!node.IsExpanded || node.IsLeaf)
            {
                node.ExpandedCount = 0;
                return 0;
            }

            if (!node.HasMaterializedChildren)
            {
                EnsureChildrenMaterialized(node);
                if (!node.HasMaterializedChildren)
                {
                    node.ExpandedCount = 0;
                    return 0;
                }
            }

            // Recalculate post-order with a depth-sized stack. The flattened-sequence collector
            // also records every visible node; using it for count-only updates retained an
            // unnecessary full-subtree List<HierarchicalNode> on collection-change hot paths.
            var stack = new Stack<(HierarchicalNode Node, int NextChildIndex)>();
            stack.Push((node, 0));
            while (stack.Count > 0)
            {
                var (current, nextChildIndex) = stack.Pop();
                var children = current.MutableChildren;
                if (nextChildIndex >= children.Count)
                {
                    UpdateExpandedCountFromMaterializedChildren(current);
                    continue;
                }

                var child = children[nextChildIndex];
                stack.Push((current, nextChildIndex + 1));
                if (!child.IsExpanded || child.IsLeaf)
                {
                    child.ExpandedCount = 0;
                    continue;
                }

                if (!child.HasMaterializedChildren)
                {
                    EnsureChildrenMaterialized(child);
                    if (!child.HasMaterializedChildren)
                    {
                        child.ExpandedCount = 0;
                        continue;
                    }
                }

                stack.Push((child, 0));
            }

            return node.ExpandedCount;
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

        private readonly struct ExpandedNodePath : IEquatable<ExpandedNodePath>
        {
            private readonly int[] _segments;

            public ExpandedNodePath(int[] segments)
            {
                _segments = segments ?? Array.Empty<int>();
            }

            public bool Equals(ExpandedNodePath other)
            {
                if (_segments.Length != other._segments.Length)
                {
                    return false;
                }

                for (int i = 0; i < _segments.Length; i++)
                {
                    if (_segments[i] != other._segments[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object? obj)
            {
                return obj is ExpandedNodePath other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    for (int i = 0; i < _segments.Length; i++)
                    {
                        hash = (hash * 31) + _segments[i];
                    }
                    return hash;
                }
            }
        }

        private sealed class NodeLoadState
        {
            public SemaphoreSlim ExpandGate { get; } = new SemaphoreSlim(1, 1);

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

        public IReadOnlyCollection<object> CaptureExpandedState()
        {
            return CaptureExpandedItems().ToList();
        }

        public void RestoreExpandedState(IEnumerable<object> keys)
        {
            RestoreExpandedStateInternal(keys, Options.ExpandedStateKeyMode);
        }

        public void RestoreExpandedState(IEnumerable<object> keys, ExpandedStateKeyMode keyMode)
        {
            RestoreExpandedStateInternal(keys, keyMode);
        }

        private void RestoreExpandedStateInternal(IEnumerable<object> keys, ExpandedStateKeyMode keyMode)
        {
            if (Root == null)
            {
                return;
            }

            var expandedItems = keys != null
                ? new HashSet<object>(keys)
                : new HashSet<object>(EqualityComparer<object>.Default);
            var expandedNodes = new List<HierarchicalNode>();

            if (_isVirtualRoot)
            {
                SetNodeExpandedState(Root, true);

                if (keyMode == ExpandedStateKeyMode.Path)
                {
                    var path = new List<int>();
                    for (int i = 0; i < Root.MutableChildren.Count; i++)
                    {
                        path.Add(i);
                        RestoreExpandedStateByPath(Root.MutableChildren[i], expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, path);
                        path.RemoveAt(path.Count - 1);
                    }
                }
                else
                {
                    foreach (var child in Root.MutableChildren)
                    {
                        RestoreExpandedState(child, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, keyMode);
                    }
                }

                RecalculateExpandedCountsFrom(Root);
                ReplaceFlattened(BuildFlattenedFromVirtualRoot(Root));
            }
            else
            {
                if (keyMode == ExpandedStateKeyMode.Path)
                {
                    var path = new List<int> { 0 };
                    RestoreExpandedStateByPath(Root, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, path);
                }
                else
                {
                    RestoreExpandedState(Root, expandedItems, depth: 0, Options.AutoExpandRoot, expandedNodes, keyMode);
                }

                RecalculateExpandedCountsFrom(Root);
                ReplaceFlattened(BuildFlattenedFromRoot(Root));
            }

            foreach (var expandedNode in expandedNodes)
            {
                OnNodeExpanded(expandedNode);
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

    /// <summary>
    /// Marker type used as the item for a virtual root node when multiple root items are set via <see cref="IHierarchicalModel.SetRoots"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class VirtualRootContainer
    {
        public VirtualRootContainer(IEnumerable items)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /// <summary>
        /// Gets the underlying collection of root items.
        /// </summary>
        public IEnumerable Items { get; }
    }
}
