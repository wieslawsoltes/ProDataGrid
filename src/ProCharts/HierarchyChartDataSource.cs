// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>A reusable hierarchical data source with stable-ID and hit-index drill-down navigation.</summary>
    /// <remarks>
    /// Notifications are synchronous on the caller's thread and occur outside the state lock.
    /// A hierarchy is structural: numeric point budgets and index windows are intentionally not
    /// applied, since sampling away a parent or child would change the represented totals.
    /// </remarks>
    public sealed class HierarchyChartDataSource : IChartDataSource, IChartHierarchyNavigator
    {
        private readonly object _gate = new();
        private readonly Stack<ChartHierarchySnapshot> _history = new();
        private ChartHierarchySnapshot _root;
        private ChartHierarchySnapshot _current;
        private ChartDataSnapshot? _snapshot;
        private ChartSeriesKind _kind;
        private int _version;

        /// <summary>Creates a source using a validated immutable tree.</summary>
        public HierarchyChartDataSource(ChartHierarchyNode root, ChartSeriesKind kind = ChartSeriesKind.Treemap)
        {
            ValidateKind(kind);
            _root = _current = new ChartHierarchySnapshot(root);
            _kind = kind;
        }

        /// <inheritdoc />
        public event EventHandler? DataInvalidated;

        /// <summary>Gets the original navigation root.</summary>
        public ChartHierarchyNode Root { get { lock (_gate) return _root.Root; } }

        /// <summary>Gets the currently displayed root.</summary>
        public ChartHierarchyNode CurrentRoot { get { lock (_gate) return _current.Root; } }

        /// <summary>Gets whether navigation can return to an ancestor.</summary>
        public bool CanDrillUp { get { lock (_gate) return _history.Count != 0; } }

        /// <summary>Gets or sets the Treemap/Sunburst presentation without changing navigation.</summary>
        public ChartSeriesKind Kind
        {
            get { lock (_gate) return _kind; }
            set
            {
                ValidateKind(value);
                lock (_gate)
                {
                    if (_kind == value) return;
                    _kind = value;
                    InvalidateCore();
                }
                DataInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Returns a cached immutable snapshot of the currently displayed subtree.</summary>
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (_gate) return _snapshot ??= new ChartDataSnapshot(_current, _kind, _version);
        }

        /// <summary>Drills into a non-root branch using a point index returned by the renderer.</summary>
        public bool TryDrillDown(int pointIndex)
        {
            lock (_gate)
            {
                if (!DrillDownCore(pointIndex)) return false;
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Drills into a branch identified within the current subtree.</summary>
        public bool TryDrillDown(string nodeId)
        {
            ArgumentNullException.ThrowIfNull(nodeId);
            lock (_gate)
            {
                if (!_current.TryGetNodeIndex(nodeId, out int index) || !DrillDownCore(index)) return false;
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Returns to the previous navigation root, reusing its hierarchy index.</summary>
        public bool TryDrillUp()
        {
            lock (_gate)
            {
                if (_history.Count == 0) return false;
                _current = _history.Pop();
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Returns to the original root and clears navigation history.</summary>
        public bool ResetNavigation()
        {
            lock (_gate)
            {
                if (_history.Count == 0) return false;
                _history.Clear();
                _current = _root;
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>Atomically replaces the validated tree and resets navigation.</summary>
        public void ReplaceRoot(ChartHierarchyNode root)
        {
            ChartHierarchySnapshot replacement = new(root);
            lock (_gate)
            {
                _root = _current = replacement;
                _history.Clear();
                InvalidateCore();
            }
            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Returns an owned root-to-current navigation path for breadcrumb controls.</summary>
        public IReadOnlyList<ChartHierarchyNode> GetPath()
        {
            lock (_gate)
            {
                ChartHierarchySnapshot[] history = _history.ToArray();
                ChartHierarchyNode[] path = new ChartHierarchyNode[history.Length + 1];
                for (int i = 0; i < history.Length; i++) path[i] = history[history.Length - 1 - i].Root;
                path[^1] = _current.Root;
                return Array.AsReadOnly(path);
            }
        }

        private bool DrillDownCore(int index)
        {
            if (index <= 0 || index >= _current.Nodes.Count || _current.Nodes[index].IsLeaf) return false;
            ChartHierarchySnapshot next = new(_current.Nodes[index]);
            _history.Push(_current);
            _current = next;
            InvalidateCore();
            return true;
        }

        private void InvalidateCore()
        {
            _version = unchecked(_version + 1);
            _snapshot = null;
        }

        private static void ValidateKind(ChartSeriesKind kind)
        {
            if (kind != ChartSeriesKind.Treemap && kind != ChartSeriesKind.Sunburst)
                throw new ArgumentOutOfRangeException(nameof(kind), "Hierarchy sources support Treemap and Sunburst.");
        }
    }
}
