// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Controls how collection change notifications are handled when sibling comparers are active.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum SiblingComparerCollectionChangeMode
    {
        /// <summary>
        /// Always use refresh fallback when a comparer is active.
        /// </summary>
        RefreshFallback,

        /// <summary>
        /// Attempts incremental add/remove handling when the change keeps comparer order.
        /// Falls back to refresh when order cannot be preserved safely.
        /// </summary>
        IncrementalMonotonic
    }

    /// <summary>
    /// Defines how expanded nodes are matched when restoring expansion state.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum ExpandedStateKeyMode
    {
        /// <summary>
        /// Uses the item instance (or its equality semantics) as the key.
        /// </summary>
        Item,

        /// <summary>
        /// Uses the index path from the root to the node as the key.
        /// </summary>
        Path,

        /// <summary>
        /// Uses <see cref="HierarchicalOptions.ExpandedStateKeySelector"/> as the key source.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Options controlling hierarchical data resolution and behavior.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalOptions
    {
        /// <summary>
        /// Delegate used to resolve children for a given item.
        /// </summary>
        public Func<object, IEnumerable?>? ChildrenSelector { get; set; }

        /// <summary>
        /// Async delegate used to resolve children for a given item.
        /// </summary>
        public Func<object, CancellationToken, Task<IEnumerable?>>? ChildrenSelectorAsync { get; set; }

        /// <summary>
        /// Property path used to fetch children when <see cref="ChildrenSelector"/> is not supplied.
        /// </summary>
        public string? ChildrenPropertyPath { get; set; }

        /// <summary>
        /// Whether the root node should automatically start expanded.
        /// </summary>
        public bool AutoExpandRoot { get; set; }

        /// <summary>
        /// Optional depth limit for automatic expansion.
        /// </summary>
        public int? MaxAutoExpandDepth { get; set; }

        /// <summary>
        /// Whether children should be virtualized (lazy materialization).
        /// </summary>
        public bool VirtualizeChildren { get; set; } = true;

        /// <summary>
        /// Optional comparer applied to siblings when ordering is required.
        /// </summary>
        public IComparer<object>? SiblingComparer { get; set; }

        /// <summary>
        /// Optional selector for per-node sibling comparers. When provided, this takes precedence over <see cref="SiblingComparer"/>.
        /// </summary>
        public Func<object, IComparer<object>?>? SiblingComparerSelector { get; set; }

        /// <summary>
        /// Controls collection-change handling when a sibling comparer is active.
        /// </summary>
        public SiblingComparerCollectionChangeMode SiblingComparerCollectionChangeMode { get; set; }

        /// <summary>
        /// Optional delegate that determines whether an item should be treated as a leaf before
        /// children are materialized. Return true for leaf nodes, false for containers. When null,
        /// the model will defer the decision until children are resolved.
        /// </summary>
        public Func<object, bool>? IsLeafSelector { get; set; }

        /// <summary>
        /// Delegate mirroring Avalonia's <c>IHierarchicalDataTemplate.ItemsSelector</c> pattern. Invoked
        /// when <see cref="ChildrenSelector"/> is null.
        /// </summary>
        public Func<object, IEnumerable?>? ItemsSelector { get; set; }

        /// <summary>
        /// Optional depth guard (root is level 0). When set, nodes at or beyond this level are treated as leaves.
        /// </summary>
        public int? MaxDepth { get; set; }

        /// <summary>
        /// When true, grouping rows may be projected as hierarchical nodes (future integration path).
        /// Default is false to keep grouping separate.
        /// </summary>
        public bool TreatGroupsAsNodes { get; set; }

        /// <summary>
        /// Controls how expanded nodes are matched when restoring expansion state.
        /// </summary>
        public ExpandedStateKeyMode ExpandedStateKeyMode { get; set; } = ExpandedStateKeyMode.Item;

        /// <summary>
        /// Optional selector used when <see cref="ExpandedStateKeyMode"/> is set to <see cref="ExpandedStateKeyMode.Custom"/>.
        /// Return a stable, unique identifier for each item to preserve expansion state across rebuilds.
        /// </summary>
        public Func<object, object?>? ExpandedStateKeySelector { get; set; }

        /// <summary>
        /// Optional selector used to read the expanded state from an item. Return null to fall back to model state.
        /// </summary>
        public Func<object, bool?>? IsExpandedSelector { get; set; }

        /// <summary>
        /// Optional setter used to write expanded state back to an item.
        /// </summary>
        public Action<object, bool>? IsExpandedSetter { get; set; }

        /// <summary>
        /// Optional property path used to read/write expanded state when selectors are not provided.
        /// </summary>
        public string? IsExpandedPropertyPath { get; set; }

        /// <summary>
        /// Optional selector that returns the index path to an item from the root items collection.
        /// For single-root models, the root item is index 0 in the path.
        /// Use this to expand to a selected item without searching the entire hierarchy.
        /// </summary>
        public Func<object, IReadOnlyList<int>?>? ItemPathSelector { get; set; }

        /// <summary>
        /// When true, allows full hierarchy traversal to locate an item when expanding to selection.
        /// Use with care on large or virtualized trees.
        /// </summary>
        public bool AllowExpandToItemSearch { get; set; }
    }
}
