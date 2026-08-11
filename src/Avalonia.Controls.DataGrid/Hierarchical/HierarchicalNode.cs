// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Represents a single node in the hierarchical data model.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNode : INotifyPropertyChanged, IHierarchicalNodeItem
    {
        private List<HierarchicalNode>? _children;
        private bool _isExpanded;
        private bool _isLeaf;
        private int _level;
        private bool _isLoading;
        private NodeLoadInfo? _loadInfo;
        // Collection and item notification state is absent for most nodes. Keeping it in a
        // sidecar avoids four unused references on every immutable hierarchy node.
        private NodeConnectionInfo? _connectionInfo;
        private IEnumerable? _childrenSource;
        private int _expandedCount;

        public HierarchicalNode(object item, HierarchicalNode? parent = null, int level = 0, bool isLeaf = false)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Parent = parent;
            _level = level;
            _isLeaf = isLeaf;
            if (!isLeaf)
            {
                _children = new List<HierarchicalNode>();
            }
            HasMaterializedChildren = isLeaf;
        }

        /// <summary>
        /// Gets the item represented by this node.
        /// </summary>
        public object Item { get; }

        HierarchicalNode IHierarchicalNodeItem.Node => this;

        /// <summary>
        /// Gets the parent node or null when at the root.
        /// </summary>
        public HierarchicalNode? Parent { get; internal set; }

        /// <summary>
        /// Gets the realized children of this node.
        /// </summary>
        public IReadOnlyList<HierarchicalNode> Children =>
            _children is null ? Array.Empty<HierarchicalNode>() : _children;

        /// <summary>
        /// Exposes the mutable children list for the owning model.
        /// </summary>
        internal List<HierarchicalNode> MutableChildren => _children ??= new List<HierarchicalNode>();

        /// <summary>
        /// Gets a value indicating whether the node is expanded.
        /// </summary>
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
                Owner?.OnNodeExpandedStateChanged(this);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the node is a leaf.
        /// </summary>
        public bool IsLeaf
        {
            get => _isLeaf;
            internal set => SetField(ref _isLeaf, value);
        }

        /// <summary>
        /// Gets the zero-based depth of the node.
        /// </summary>
        public int Level
        {
            get => _level;
            internal set => SetField(ref _level, value);
        }

        /// <summary>
        /// Gets a value indicating whether the node is currently loading its children.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            internal set => SetField(ref _isLoading, value);
        }

        /// <summary>
        /// Gets the number of visible descendant nodes under this node (based on current expansion state).
        /// </summary>
        public int ExpandedCount
        {
            get => _expandedCount;
            internal set => SetField(ref _expandedCount, value);
        }

        /// <summary>
        /// Gets the last error encountered while loading children, if any.
        /// </summary>
        public Exception? LoadError
        {
            get => _loadInfo?.Error;
            internal set
            {
                var current = _loadInfo?.Error;
                if (ReferenceEquals(current, value))
                {
                    return;
                }

                if (value == null)
                {
                    _loadInfo!.Error = null;
                    TrimLoadInfo();
                }
                else
                {
                    (_loadInfo ??= new NodeLoadInfo()).Error = value;
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tracks the source used to produce children, when available.
        /// </summary>
        internal IEnumerable? ChildrenSource
        {
            get => _childrenSource;
            set => _childrenSource = value;
        }

        /// <summary>
        /// Subscribed notifier for child collection changes.
        /// </summary>
        internal INotifyCollectionChanged? ChildrenNotifier
        {
            get => _connectionInfo?.ChildrenNotifier;
            set
            {
                if (ReferenceEquals(_connectionInfo?.ChildrenNotifier, value))
                {
                    return;
                }

                if (value == null)
                {
                    _connectionInfo!.ChildrenNotifier = null;
                    TrimConnectionInfo();
                }
                else
                {
                    (_connectionInfo ??= new NodeConnectionInfo()).ChildrenNotifier = value;
                }
            }
        }

        /// <summary>
        /// Cached handler to detach collection change subscription.
        /// </summary>
        internal EventHandler<NotifyCollectionChangedEventArgs>? ChildrenChangedHandler
        {
            get => _connectionInfo?.ChildrenChangedHandler;
            set
            {
                if (ReferenceEquals(_connectionInfo?.ChildrenChangedHandler, value))
                {
                    return;
                }

                if (value == null)
                {
                    _connectionInfo!.ChildrenChangedHandler = null;
                    TrimConnectionInfo();
                }
                else
                {
                    (_connectionInfo ??= new NodeConnectionInfo()).ChildrenChangedHandler = value;
                }
            }
        }

        /// <summary>
        /// Subscribed notifier for expanded state changes.
        /// </summary>
        internal INotifyPropertyChanged? ExpandedStateNotifier
        {
            get => _connectionInfo?.ExpandedStateNotifier;
            set
            {
                if (ReferenceEquals(_connectionInfo?.ExpandedStateNotifier, value))
                {
                    return;
                }

                if (value == null)
                {
                    _connectionInfo!.ExpandedStateNotifier = null;
                    TrimConnectionInfo();
                }
                else
                {
                    (_connectionInfo ??= new NodeConnectionInfo()).ExpandedStateNotifier = value;
                }
            }
        }

        /// <summary>
        /// Cached handler to detach expanded state subscription.
        /// </summary>
        internal EventHandler<PropertyChangedEventArgs>? ExpandedStateChangedHandler
        {
            get => _connectionInfo?.ExpandedStateChangedHandler;
            set
            {
                if (ReferenceEquals(_connectionInfo?.ExpandedStateChangedHandler, value))
                {
                    return;
                }

                if (value == null)
                {
                    _connectionInfo!.ExpandedStateChangedHandler = null;
                    TrimConnectionInfo();
                }
                else
                {
                    (_connectionInfo ??= new NodeConnectionInfo()).ExpandedStateChangedHandler = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the model that owns this node.
        /// </summary>
        internal HierarchicalModel? Owner { get; set; }

        /// <summary>
        /// Tracks in-flight load cancellation for this node.
        /// </summary>
        internal CancellationTokenSource? LoadCancellation
        {
            get => _loadInfo?.Cancellation;
            set
            {
                if (value == null)
                {
                    if (_loadInfo != null)
                    {
                        _loadInfo.Cancellation = null;
                        TrimLoadInfo();
                    }
                }
                else
                {
                    (_loadInfo ??= new NodeLoadInfo()).Cancellation = value;
                }
            }
        }

        /// <summary>
        /// Tracks whether children were materialized.
        /// </summary>
        internal bool HasMaterializedChildren { get; set; }

        /// <summary>
        /// Tracks materialization completed by a transactional bulk expansion that has not yet
        /// committed its flattened snapshot.
        /// </summary>
        internal bool HasPendingBulkMaterializationCommit
        {
            get => _loadInfo?.PendingBulkMaterializationCommit ?? false;
            set
            {
                if (value)
                {
                    (_loadInfo ??= new NodeLoadInfo()).PendingBulkMaterializationCommit = true;
                }
                else if (_loadInfo != null)
                {
                    _loadInfo.PendingBulkMaterializationCommit = false;
                    TrimLoadInfo();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets whether expansion-state changes currently have property observers.
        /// </summary>
        internal bool HasPropertyChangedObservers => PropertyChanged != null;

        internal void SetExpandedFromOwner(bool value)
        {
            if (!SetExpandedFromOwnerSilently(value))
            {
                return;
            }

            RaiseExpandedChanged();
        }

        /// <summary>
        /// Changes expansion state without publishing a property notification. Bulk operations
        /// use this while building their final flattened snapshot, then notify after commit.
        /// </summary>
        /// <param name="value">New expansion state.</param>
        /// <returns>True when the state changed.</returns>
        internal bool SetExpandedFromOwnerSilently(bool value)
        {
            if (_isExpanded == value)
            {
                return false;
            }

            _isExpanded = value;
            return true;
        }

        /// <summary>
        /// Publishes the expansion-state notification after a bulk model commit.
        /// </summary>
        internal void RaiseExpandedChanged()
        {
            OnPropertyChanged(nameof(IsExpanded));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(storage, value))
            {
                storage = value;
                OnPropertyChanged(propertyName);
            }
        }

        private void TrimLoadInfo()
        {
            if (_loadInfo is
                {
                    Error: null,
                    Cancellation: null,
                    PendingBulkMaterializationCommit: false
                })
            {
                _loadInfo = null;
            }
        }

        private void TrimConnectionInfo()
        {
            if (_connectionInfo is
                {
                    ChildrenNotifier: null,
                    ChildrenChangedHandler: null,
                    ExpandedStateNotifier: null,
                    ExpandedStateChangedHandler: null
                })
            {
                _connectionInfo = null;
            }
        }

        private sealed class NodeLoadInfo
        {
            public Exception? Error;
            public CancellationTokenSource? Cancellation;
            public bool PendingBulkMaterializationCommit;
        }

        private sealed class NodeConnectionInfo
        {
            public INotifyCollectionChanged? ChildrenNotifier;
            public EventHandler<NotifyCollectionChangedEventArgs>? ChildrenChangedHandler;
            public INotifyPropertyChanged? ExpandedStateNotifier;
            public EventHandler<PropertyChangedEventArgs>? ExpandedStateChangedHandler;
        }
    }
}
