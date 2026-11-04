// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using Avalonia.Controls.DataGridHierarchical;

namespace Avalonia.Controls
{
    /// <summary>
    /// Identifies the source of a committed value reported by
    /// <see cref="DataGrid.CellValueChanged"/>.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridCellValueChangeOrigin
    {
        /// <summary>
        /// The value was committed through a DataGrid cell editor.
        /// </summary>
        EditCommit,
    }

    /// <summary>
    /// Provides data for cell realization lifecycle events.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridCellLifecycleEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellLifecycleEventArgs"/> class.
        /// </summary>
        /// <param name="cell">The realized cell container.</param>
        /// <param name="row">The row that owns the cell.</param>
        /// <param name="rowDataContext">The data context assigned to the row.</param>
        /// <param name="item">The underlying row item.</param>
        /// <param name="hierarchicalNode">The hierarchy node, or null for a flat row.</param>
        /// <param name="hierarchyPath">The root-to-node hierarchy path.</param>
        public DataGridCellLifecycleEventArgs(
            DataGridCell cell,
            DataGridRow row,
            object rowDataContext,
            object item,
            HierarchicalNode hierarchicalNode,
            IReadOnlyList<HierarchicalNode> hierarchyPath)
        {
            Cell = cell;
            Row = row;
            Column = cell.OwningColumn;
            RowDataContext = rowDataContext;
            Item = item;
            HierarchicalNode = hierarchicalNode;
            HierarchyPath = hierarchyPath;
        }

        /// <summary>
        /// Gets the realized cell container.
        /// </summary>
        public DataGridCell Cell { get; }

        /// <summary>
        /// Gets the row container that owns the cell.
        /// </summary>
        public DataGridRow Row { get; }

        /// <summary>
        /// Gets the column represented by the cell.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Gets the row data context. In hierarchical mode this is the
        /// <see cref="HierarchicalNode"/> wrapper.
        /// </summary>
        public object RowDataContext { get; }

        /// <summary>
        /// Gets the underlying row item. In hierarchical mode this is
        /// <see cref="HierarchicalNode.Item"/>; otherwise it is the row data context.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the hierarchy node, or <see langword="null"/> for a flat row.
        /// </summary>
        public HierarchicalNode HierarchicalNode { get; }

        /// <summary>
        /// Gets the root-to-node hierarchy path, or an empty list for a flat row.
        /// </summary>
        public IReadOnlyList<HierarchicalNode> HierarchyPath { get; }
    }

    /// <summary>
    /// Provides data for a committed grid-originated cell value change.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridCellValueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellValueChangedEventArgs"/> class.
        /// </summary>
        /// <param name="cell">The cell whose editor committed the value.</param>
        /// <param name="row">The row that owns the cell.</param>
        /// <param name="rowDataContext">The data context assigned to the row.</param>
        /// <param name="item">The underlying row item.</param>
        /// <param name="oldValue">The value before the commit.</param>
        /// <param name="newValue">The value after the commit.</param>
        /// <param name="origin">The origin of the committed change.</param>
        /// <param name="hierarchicalNode">The hierarchy node, or null for a flat row.</param>
        /// <param name="hierarchyPath">The root-to-node hierarchy path.</param>
        public DataGridCellValueChangedEventArgs(
            DataGridCell cell,
            DataGridRow row,
            object rowDataContext,
            object item,
            object oldValue,
            object newValue,
            DataGridCellValueChangeOrigin origin,
            HierarchicalNode hierarchicalNode,
            IReadOnlyList<HierarchicalNode> hierarchyPath)
        {
            Cell = cell;
            Row = row;
            Column = cell.OwningColumn;
            RowDataContext = rowDataContext;
            Item = item;
            OldValue = oldValue;
            NewValue = newValue;
            Origin = origin;
            HierarchicalNode = hierarchicalNode;
            HierarchyPath = hierarchyPath;
        }

        /// <summary>
        /// Gets the cell whose editor committed the value.
        /// </summary>
        public DataGridCell Cell { get; }

        /// <summary>
        /// Gets the row containing the cell.
        /// </summary>
        public DataGridRow Row { get; }

        /// <summary>
        /// Gets the column containing the cell.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Gets the row data context used by the column binding.
        /// </summary>
        public object RowDataContext { get; }

        /// <summary>
        /// Gets the underlying row item.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the value before the edit was committed.
        /// </summary>
        public object OldValue { get; }

        /// <summary>
        /// Gets the value after the edit was committed.
        /// </summary>
        public object NewValue { get; }

        /// <summary>
        /// Gets the origin of the committed change.
        /// </summary>
        public DataGridCellValueChangeOrigin Origin { get; }

        /// <summary>
        /// Gets the hierarchy node, or <see langword="null"/> for a flat row.
        /// </summary>
        public HierarchicalNode HierarchicalNode { get; }

        /// <summary>
        /// Gets the root-to-node hierarchy path, or an empty list for a flat row.
        /// </summary>
        public IReadOnlyList<HierarchicalNode> HierarchyPath { get; }
    }

    partial class DataGrid
    {
        private EventHandler<DataGridCellLifecycleEventArgs> _cellPrepared;
        private EventHandler<DataGridCellLifecycleEventArgs> _cellClearing;

        /// <summary>
        /// Occurs once after a cell container is assigned to a realized row item, including
        /// when an existing container is recycled for another item.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellLifecycleEventArgs> CellPrepared
        {
            add
            {
                _cellPrepared += value;
                RefreshVirtualCellBackendAfterColumnsChanged();
            }
            remove
            {
                _cellPrepared -= value;
                RefreshVirtualCellBackendAfterColumnsChanged();
            }
        }

        /// <summary>
        /// Occurs once before a realized cell loses its row data context and ownership
        /// association because the row is unrealized or recycled.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellLifecycleEventArgs> CellClearing
        {
            add
            {
                _cellClearing += value;
                RefreshVirtualCellBackendAfterColumnsChanged();
            }
            remove
            {
                _cellClearing -= value;
                RefreshVirtualCellBackendAfterColumnsChanged();
            }
        }

        /// <summary>
        /// Occurs after a changed value is successfully committed through a DataGrid editor.
        /// Cancelled edits, validation failures, direct model updates, formula recalculation,
        /// and undo or redo operations do not raise this event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellValueChangedEventArgs> CellValueChanged;

        private void NotifyCellsPrepared(DataGridRow row)
        {
            if (!HasCellPreparedHandlers)
            {
                return;
            }

            CreateCellEventContext(
                row.DataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);

            foreach (DataGridCell cell in row.Cells)
            {
                _cellPrepared?.Invoke(this, new DataGridCellLifecycleEventArgs(
                    cell, row, row.DataContext, item, node, path));
            }
        }

        private void NotifyCellsClearing(DataGridRow row)
        {
            if (!HasCellClearingHandlers)
            {
                return;
            }

            CreateCellEventContext(
                row.DataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);

            foreach (DataGridCell cell in row.Cells)
            {
                _cellClearing?.Invoke(this, new DataGridCellLifecycleEventArgs(
                    cell, row, row.DataContext, item, node, path));
            }
        }

        private void NotifyCellPrepared(DataGridRow row, DataGridCell cell)
        {
            if (!HasCellPreparedHandlers)
            {
                return;
            }

            CreateCellEventContext(
                row.DataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);
            _cellPrepared?.Invoke(this, new DataGridCellLifecycleEventArgs(
                cell, row, row.DataContext, item, node, path));
        }

        private void NotifyCellClearing(DataGridRow row, DataGridCell cell)
        {
            if (!HasCellClearingHandlers)
            {
                return;
            }

            CreateCellEventContext(
                row.DataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);
            _cellClearing?.Invoke(this, new DataGridCellLifecycleEventArgs(
                cell, row, row.DataContext, item, node, path));
        }

        private static void CreateCellEventContext(
            object rowDataContext,
            out object item,
            out HierarchicalNode node,
            out IReadOnlyList<HierarchicalNode> hierarchyPath)
        {
            node = rowDataContext as HierarchicalNode;
            item = node?.Item ?? rowDataContext;
            if (node == null)
            {
                hierarchyPath = Array.Empty<HierarchicalNode>();
                return;
            }

            int count = 0;
            for (HierarchicalNode current = node; current != null; current = current.Parent)
            {
                if (current.Level >= 0)
                {
                    count++;
                }
            }

            var path = new HierarchicalNode[count];
            for (HierarchicalNode current = node; current != null; current = current.Parent)
            {
                if (current.Level >= 0)
                {
                    path[--count] = current;
                }
            }

            hierarchyPath = path;
        }

        private object GetCommittedCellValue(DataGridColumn column, object rowDataContext)
        {
            IDataGridColumnValueAccessor accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null &&
                (rowDataContext == null || accessor.ItemType.IsInstanceOfType(rowDataContext)))
            {
                return accessor.GetValue(rowDataContext);
            }

            return column.GetCellValue(rowDataContext, column.ClipboardContentBinding);
        }

        private void NotifyCommittedCellValueChanged(
            DataGridCell cell,
            DataGridRow row,
            object oldValue,
            object newValue)
        {
            CreateCellEventContext(
                row.DataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);

            CellValueChanged?.Invoke(this, new DataGridCellValueChangedEventArgs(
                cell,
                row,
                row.DataContext,
                item,
                oldValue,
                newValue,
                DataGridCellValueChangeOrigin.EditCommit,
                node,
                path));
        }

        private bool HasCellPreparedHandlers => _cellPrepared != null;

        private bool HasCellClearingHandlers => _cellClearing != null;

        private bool HasCellValueChangedHandlers => CellValueChanged != null;
    }
}
