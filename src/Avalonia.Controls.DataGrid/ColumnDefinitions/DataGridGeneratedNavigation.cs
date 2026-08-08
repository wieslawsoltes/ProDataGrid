// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Identifies a navigation operation requested through a generated view interaction.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedNavigationAction
    {
        /// <summary>No navigation operation.</summary>
        None,
        /// <summary>Returns the current-cell snapshot without changing the grid.</summary>
        QueryCurrentCell,
        /// <summary>Brings an item and optional stable column key into view.</summary>
        ScrollIntoView,
        /// <summary>Selects an item and makes its stable column key current.</summary>
        SetCurrentCell,
        /// <summary>Moves the current cell by row and visible-column offsets.</summary>
        MoveCurrentCell,
        /// <summary>Captures the current scroll state.</summary>
        CaptureScrollState,
        /// <summary>Restores a previously captured scroll state.</summary>
        RestoreScrollState
    }

    /// <summary>Describes the outcome of a generated navigation request without allocating an error message.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedNavigationStatus
    {
        /// <summary>The request completed successfully.</summary>
        Succeeded,
        /// <summary>The generated view activation was canceled.</summary>
        Cancelled,
        /// <summary>The requested item is not present in the active collection view.</summary>
        ItemNotFound,
        /// <summary>The requested stable column key is not present or is not visible.</summary>
        ColumnNotFound,
        /// <summary>The grid does not currently expose a valid current cell.</summary>
        CurrentCellUnavailable,
        /// <summary>The requested XY movement crossed a collection or visible-column boundary.</summary>
        BoundaryReached,
        /// <summary>Scroll state is unavailable or could not be restored.</summary>
        ScrollStateUnavailable,
        /// <summary>The request does not contain the values required by its action.</summary>
        InvalidRequest
    }

    /// <summary>
    /// Carries a typed navigation request from a ViewModel to an activation-scoped generated view.
    /// </summary>
    /// <typeparam name="TItem">The generated grid item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedNavigationRequest<TItem>
    {
        private DataGridGeneratedNavigationRequest(DataGridGeneratedNavigationAction action)
        {
            Action = action;
            ColumnKey = string.Empty;
        }

        /// <summary>Gets the requested navigation action.</summary>
        public DataGridGeneratedNavigationAction Action { get; }

        /// <summary>Gets whether the request carries an item, including the default value of a value type.</summary>
        public bool HasItem { get; private set; }

        /// <summary>Gets the item associated with the request.</summary>
        public TItem Item { get; private set; }

        /// <summary>Gets the stable generated column key, or an empty string when no column is requested.</summary>
        public string ColumnKey { get; private set; }

        /// <summary>Gets the visible-column offset used by XY current-cell movement.</summary>
        public int ColumnOffset { get; private set; }

        /// <summary>Gets the collection-view row offset used by XY current-cell movement.</summary>
        public int RowOffset { get; private set; }

        /// <summary>Gets whether the grid should receive focus after a current-cell change.</summary>
        public bool Focus { get; private set; }

        /// <summary>Gets the scroll state supplied to a restore request.</summary>
        public DataGridScrollState ScrollState { get; private set; }

        /// <summary>Gets optional stable key selectors and resolvers used by scroll-state operations.</summary>
        public DataGridStateOptions StateOptions { get; private set; }

        /// <summary>Creates a request that returns the current cell.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> QueryCurrentCell() =>
            new(DataGridGeneratedNavigationAction.QueryCurrentCell);

        /// <summary>Creates a request that brings an item and optional stable column key into view.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> ScrollIntoView(
            TItem item,
            string columnKey = null) =>
            new(DataGridGeneratedNavigationAction.ScrollIntoView)
            {
                HasItem = true,
                Item = item,
                ColumnKey = columnKey ?? string.Empty
            };

        /// <summary>Creates a request that selects an item and makes its stable column key current.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> SetCurrentCell(
            TItem item,
            string columnKey,
            bool focus = false) =>
            new(DataGridGeneratedNavigationAction.SetCurrentCell)
            {
                HasItem = true,
                Item = item,
                ColumnKey = columnKey ?? string.Empty,
                Focus = focus
            };

        /// <summary>Creates an XY current-cell movement request.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> MoveCurrentCell(
            int columnOffset,
            int rowOffset,
            bool focus = false) =>
            new(DataGridGeneratedNavigationAction.MoveCurrentCell)
            {
                ColumnOffset = columnOffset,
                RowOffset = rowOffset,
                Focus = focus
            };

        /// <summary>Creates a request that captures the current scroll state.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> CaptureScrollState(
            DataGridStateOptions stateOptions = null) =>
            new(DataGridGeneratedNavigationAction.CaptureScrollState)
            {
                StateOptions = stateOptions
            };

        /// <summary>Creates a request that restores a previously captured scroll state.</summary>
        public static DataGridGeneratedNavigationRequest<TItem> RestoreScrollState(
            DataGridScrollState scrollState,
            DataGridStateOptions stateOptions = null) =>
            new(DataGridGeneratedNavigationAction.RestoreScrollState)
            {
                ScrollState = scrollState,
                StateOptions = stateOptions
            };
    }

    /// <summary>
    /// Returns a typed current-cell or scroll-state snapshot from a generated navigation interaction.
    /// </summary>
    /// <typeparam name="TItem">The generated grid item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedNavigationResult<TItem> : IEquatable<DataGridGeneratedNavigationResult<TItem>>
    {
        internal DataGridGeneratedNavigationResult(
            DataGridGeneratedNavigationAction action,
            DataGridGeneratedNavigationStatus status,
            bool hasItem,
            TItem item,
            string columnKey,
            int rowIndex,
            int columnDisplayIndex,
            DataGridScrollState scrollState)
        {
            Action = action;
            Status = status;
            HasItem = hasItem;
            Item = item;
            ColumnKey = columnKey ?? string.Empty;
            RowIndex = rowIndex;
            ColumnDisplayIndex = columnDisplayIndex;
            ScrollState = scrollState;
        }

        /// <summary>Gets the completed action.</summary>
        public DataGridGeneratedNavigationAction Action { get; }

        /// <summary>Gets the non-allocating result status.</summary>
        public DataGridGeneratedNavigationStatus Status { get; }

        /// <summary>Gets whether the operation completed successfully.</summary>
        public bool Succeeded => Status == DataGridGeneratedNavigationStatus.Succeeded;

        /// <summary>Gets whether the result carries an item.</summary>
        public bool HasItem { get; }

        /// <summary>Gets the resulting current or requested item.</summary>
        public TItem Item { get; }

        /// <summary>Gets the resulting stable column key.</summary>
        public string ColumnKey { get; }

        /// <summary>Gets the resulting collection-view row index, or -1 when unavailable.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the resulting column display index, or -1 when unavailable.</summary>
        public int ColumnDisplayIndex { get; }

        /// <summary>Gets the captured or restored scroll state.</summary>
        public DataGridScrollState ScrollState { get; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedNavigationResult<TItem> other) =>
            Action == other.Action &&
            Status == other.Status &&
            HasItem == other.HasItem &&
            EqualityComparer<TItem>.Default.Equals(Item, other.Item) &&
            string.Equals(ColumnKey, other.ColumnKey, StringComparison.Ordinal) &&
            RowIndex == other.RowIndex &&
            ColumnDisplayIndex == other.ColumnDisplayIndex &&
            ReferenceEquals(ScrollState, other.ScrollState);

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is DataGridGeneratedNavigationResult<TItem> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Action;
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ HasItem.GetHashCode();
                hash = (hash * 397) ^ EqualityComparer<TItem>.Default.GetHashCode(Item);
                hash = (hash * 397) ^ (ColumnKey == null ? 0 : StringComparer.Ordinal.GetHashCode(ColumnKey));
                hash = (hash * 397) ^ RowIndex;
                hash = (hash * 397) ^ ColumnDisplayIndex;
                hash = (hash * 397) ^ (ScrollState?.GetHashCode() ?? 0);
                return hash;
            }
        }

        /// <summary>Compares two generated navigation results.</summary>
        public static bool operator ==(
            DataGridGeneratedNavigationResult<TItem> left,
            DataGridGeneratedNavigationResult<TItem> right) => left.Equals(right);

        /// <summary>Compares two generated navigation results.</summary>
        public static bool operator !=(
            DataGridGeneratedNavigationResult<TItem> left,
            DataGridGeneratedNavigationResult<TItem> right) => !left.Equals(right);
    }

    /// <summary>
    /// Executes generated current-cell and scroll requests at the view boundary without exposing a
    /// <see cref="DataGrid"/> instance to the ViewModel.
    /// </summary>
    /// <typeparam name="TItem">The generated grid item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridGeneratedNavigationHandler<TItem> :
        IDataGridGeneratedViewInteractionHandler<
            DataGridGeneratedNavigationRequest<TItem>,
            DataGridGeneratedNavigationResult<TItem>>
    {
        /// <inheritdoc />
        public virtual ValueTask<DataGridGeneratedNavigationResult<TItem>> HandleAsync(
            DataGridGeneratedViewInteractionContext<DataGridGeneratedNavigationRequest<TItem>> context)
        {
            DataGridGeneratedNavigationRequest<TItem> request = context.Input;
            if (request == null)
            {
                return new ValueTask<DataGridGeneratedNavigationResult<TItem>>(
                    CreateResult(default, DataGridGeneratedNavigationStatus.InvalidRequest));
            }

            if (context.CancellationToken.IsCancellationRequested)
            {
                return new ValueTask<DataGridGeneratedNavigationResult<TItem>>(
                    CreateResult(request.Action, DataGridGeneratedNavigationStatus.Cancelled));
            }

            return new ValueTask<DataGridGeneratedNavigationResult<TItem>>(
                Execute(context.DataGrid, request));
        }

        private static DataGridGeneratedNavigationResult<TItem> Execute(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request) =>
            request.Action switch
            {
                DataGridGeneratedNavigationAction.QueryCurrentCell => SnapshotCurrentCell(dataGrid, request.Action),
                DataGridGeneratedNavigationAction.ScrollIntoView => ScrollIntoView(dataGrid, request),
                DataGridGeneratedNavigationAction.SetCurrentCell => SetCurrentCell(dataGrid, request),
                DataGridGeneratedNavigationAction.MoveCurrentCell => MoveCurrentCell(dataGrid, request),
                DataGridGeneratedNavigationAction.CaptureScrollState => CaptureScrollState(dataGrid, request),
                DataGridGeneratedNavigationAction.RestoreScrollState => RestoreScrollState(dataGrid, request),
                _ => CreateResult(request.Action, DataGridGeneratedNavigationStatus.InvalidRequest)
            };

        private static DataGridGeneratedNavigationResult<TItem> ScrollIntoView(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request)
        {
            if (!request.HasItem || !TryFindRow(dataGrid, request.Item, out int rowIndex))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ItemNotFound);
            }

            DataGridColumn column = null;
            if (request.ColumnKey.Length != 0 && !TryFindColumn(dataGrid, request.ColumnKey, out column))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ColumnNotFound);
            }

            dataGrid.ScrollIntoView(request.Item, column);
            return CreateResult(
                request.Action,
                DataGridGeneratedNavigationStatus.Succeeded,
                true,
                request.Item,
                GetColumnKey(column),
                rowIndex,
                column?.DisplayIndex ?? -1,
                null);
        }

        private static DataGridGeneratedNavigationResult<TItem> SetCurrentCell(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request)
        {
            if (!request.HasItem || !TryFindRow(dataGrid, request.Item, out int rowIndex))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ItemNotFound);
            }

            if (!TryFindColumn(dataGrid, request.ColumnKey, out DataGridColumn column))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ColumnNotFound);
            }

            dataGrid.CurrentCell = new DataGridCellInfo(request.Item, column, rowIndex, column.Index);
            if (request.Focus)
            {
                dataGrid.Focus();
            }

            return SnapshotCurrentCell(dataGrid, request.Action);
        }

        private static DataGridGeneratedNavigationResult<TItem> MoveCurrentCell(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request)
        {
            DataGridCellInfo current = dataGrid.CurrentCell;
            if (!current.IsValid || current.Column == null || current.Item is not TItem)
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.CurrentCellUnavailable);
            }

            int currentColumnOrdinal = GetVisibleColumnOrdinal(dataGrid, current.Column);
            int targetColumnOrdinal = currentColumnOrdinal + request.ColumnOffset;
            int targetRowIndex = current.RowIndex + request.RowOffset;
            if (currentColumnOrdinal < 0 ||
                !TryGetVisibleColumn(dataGrid, targetColumnOrdinal, out DataGridColumn targetColumn) ||
                !TryGetItemAt(dataGrid, targetRowIndex, out TItem targetItem))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.BoundaryReached);
            }

            dataGrid.CurrentCell = new DataGridCellInfo(
                targetItem,
                targetColumn,
                targetRowIndex,
                targetColumn.Index);
            if (request.Focus)
            {
                dataGrid.Focus();
            }

            return SnapshotCurrentCell(dataGrid, request.Action);
        }

        private static DataGridGeneratedNavigationResult<TItem> CaptureScrollState(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request)
        {
            DataGridScrollState scrollState = dataGrid.CaptureScrollState(request.StateOptions);
            if (scrollState == null)
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ScrollStateUnavailable);
            }

            DataGridGeneratedNavigationResult<TItem> current = SnapshotCurrentCell(dataGrid, request.Action);
            return CreateResult(
                request.Action,
                DataGridGeneratedNavigationStatus.Succeeded,
                current.HasItem,
                current.Item,
                current.ColumnKey,
                current.RowIndex,
                current.ColumnDisplayIndex,
                scrollState);
        }

        private static DataGridGeneratedNavigationResult<TItem> RestoreScrollState(
            DataGrid dataGrid,
            DataGridGeneratedNavigationRequest<TItem> request)
        {
            if (request.ScrollState == null ||
                !dataGrid.TryRestoreScrollState(request.ScrollState, request.StateOptions))
            {
                return CreateResult(request.Action, DataGridGeneratedNavigationStatus.ScrollStateUnavailable);
            }

            DataGridGeneratedNavigationResult<TItem> current = SnapshotCurrentCell(dataGrid, request.Action);
            return CreateResult(
                request.Action,
                DataGridGeneratedNavigationStatus.Succeeded,
                current.HasItem,
                current.Item,
                current.ColumnKey,
                current.RowIndex,
                current.ColumnDisplayIndex,
                request.ScrollState);
        }

        private static DataGridGeneratedNavigationResult<TItem> SnapshotCurrentCell(
            DataGrid dataGrid,
            DataGridGeneratedNavigationAction action)
        {
            DataGridCellInfo current = dataGrid.CurrentCell;
            if (!current.IsValid || current.Column == null || current.Item is not TItem item)
            {
                return CreateResult(action, DataGridGeneratedNavigationStatus.CurrentCellUnavailable);
            }

            return CreateResult(
                action,
                DataGridGeneratedNavigationStatus.Succeeded,
                true,
                item,
                GetColumnKey(current.Column),
                current.RowIndex,
                current.Column.DisplayIndex,
                null);
        }

        private static bool TryFindColumn(DataGrid dataGrid, string columnKey, out DataGridColumn column)
        {
            column = null;
            if (string.IsNullOrWhiteSpace(columnKey))
            {
                return false;
            }

            foreach (DataGridColumn candidate in dataGrid.Columns)
            {
                if (candidate.IsVisible && string.Equals(GetColumnKey(candidate), columnKey, StringComparison.Ordinal))
                {
                    column = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string GetColumnKey(DataGridColumn column) =>
            column?.ColumnKey?.ToString() ?? column?.SortMemberPath ?? string.Empty;

        private static int GetVisibleColumnOrdinal(DataGrid dataGrid, DataGridColumn column)
        {
            int ordinal = 0;
            int displayIndex = column.DisplayIndex;
            foreach (DataGridColumn candidate in dataGrid.Columns)
            {
                if (!candidate.IsVisible)
                {
                    continue;
                }

                if (candidate.DisplayIndex < displayIndex)
                {
                    ordinal++;
                }
            }

            return column.IsVisible ? ordinal : -1;
        }

        private static bool TryGetVisibleColumn(DataGrid dataGrid, int ordinal, out DataGridColumn column)
        {
            column = null;
            if (ordinal < 0)
            {
                return false;
            }

            int bestDisplayIndex = int.MaxValue;
            int previousDisplayIndex = -1;
            for (int currentOrdinal = 0; currentOrdinal <= ordinal; currentOrdinal++)
            {
                column = null;
                bestDisplayIndex = int.MaxValue;
                foreach (DataGridColumn candidate in dataGrid.Columns)
                {
                    if (!candidate.IsVisible ||
                        candidate.DisplayIndex <= previousDisplayIndex || candidate.DisplayIndex >= bestDisplayIndex)
                    {
                        continue;
                    }

                    column = candidate;
                    bestDisplayIndex = candidate.DisplayIndex;
                }

                if (column == null)
                {
                    return false;
                }

                previousDisplayIndex = bestDisplayIndex;
            }

            return true;
        }

        private static bool TryFindRow(DataGrid dataGrid, TItem item, out int rowIndex)
        {
            IEnumerable items = GetActiveItems(dataGrid);
            if (items is IList list)
            {
                rowIndex = list.IndexOf(item);
                return rowIndex >= 0;
            }

            rowIndex = 0;
            foreach (object candidate in items)
            {
                if (Equals(candidate, item))
                {
                    return true;
                }
                rowIndex++;
            }

            rowIndex = -1;
            return false;
        }

        private static bool TryGetItemAt(DataGrid dataGrid, int rowIndex, out TItem item)
        {
            item = default;
            if (rowIndex < 0)
            {
                return false;
            }

            IEnumerable items = GetActiveItems(dataGrid);
            if (items is IList list)
            {
                if (rowIndex >= list.Count || list[rowIndex] is not TItem typedItem)
                {
                    return false;
                }

                item = typedItem;
                return true;
            }

            int index = 0;
            foreach (object candidate in items)
            {
                if (index++ != rowIndex)
                {
                    continue;
                }

                if (candidate is TItem typedItem)
                {
                    item = typedItem;
                    return true;
                }

                return false;
            }

            return false;
        }

        private static IEnumerable GetActiveItems(DataGrid dataGrid) =>
            dataGrid.CollectionView ?? dataGrid.ItemsSource ?? Array.Empty<object>();

        private static DataGridGeneratedNavigationResult<TItem> CreateResult(
            DataGridGeneratedNavigationAction action,
            DataGridGeneratedNavigationStatus status,
            bool hasItem = false,
            TItem item = default,
            string columnKey = null,
            int rowIndex = -1,
            int columnDisplayIndex = -1,
            DataGridScrollState scrollState = null) =>
            new(
                action,
                status,
                hasItem,
                item,
                columnKey,
                rowIndex,
                columnDisplayIndex,
                scrollState);
    }
}
