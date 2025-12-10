// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Represents a single node in the hierarchical data model.
    /// </summary>
    public class HierarchicalNode : INotifyPropertyChanged
    {
        private readonly List<HierarchicalNode> _children;
        private bool _isExpanded;
        private bool _isLeaf;
        private int _level;
        private bool _isLoading;
        private Exception? _loadError;
        private int _expandedCount;

        public HierarchicalNode(object item, HierarchicalNode? parent = null, int level = 0, bool isLeaf = false)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Parent = parent;
            _level = level;
            _isLeaf = isLeaf;
            _children = new List<HierarchicalNode>();
        }

        /// <summary>
        /// Gets the item represented by this node.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the parent node or null when at the root.
        /// </summary>
        public HierarchicalNode? Parent { get; internal set; }

        /// <summary>
        /// Gets the realized children of this node.
        /// </summary>
        public IReadOnlyList<HierarchicalNode> Children => _children;

        /// <summary>
        /// Exposes the mutable children list for the owning model.
        /// </summary>
        internal List<HierarchicalNode> MutableChildren => _children;

        /// <summary>
        /// Gets a value indicating whether the node is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            internal set => SetField(ref _isExpanded, value);
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
            get => _loadError;
            internal set => SetField(ref _loadError, value);
        }

        /// <summary>
        /// Tracks the source used to produce children, when available.
        /// </summary>
        internal IEnumerable? ChildrenSource { get; set; }

        /// <summary>
        /// Subscribed notifier for child collection changes.
        /// </summary>
        internal INotifyCollectionChanged? ChildrenNotifier { get; set; }

        /// <summary>
        /// Cached handler to detach collection change subscription.
        /// </summary>
        internal NotifyCollectionChangedEventHandler? ChildrenChangedHandler { get; set; }

        /// <summary>
        /// Tracks whether children were materialized.
        /// </summary>
        internal bool HasMaterializedChildren { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

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
    }
}
