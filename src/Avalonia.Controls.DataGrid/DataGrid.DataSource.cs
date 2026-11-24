// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Utils;
using Avalonia.Controls.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;

namespace Avalonia.Controls
{
    /// <summary>
    /// Data source management
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private bool _selectionModelSourceLayoutPending;
        private List<int> _selectionModelSourceIndexesPending;

        /// <summary>
        /// ItemsSourceProperty property changed handler.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnItemsSourcePropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            using var selectionScope = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.ItemsSourceChange,
                sticky: true,
                guarantee: DataGridSelectionChangingGuarantee.PostChangeReconciliation);

            _pendingGroupingState = null;

            var oldValue = (IEnumerable)e.OldValue;
            var newItemsSource = (IEnumerable)e.NewValue;
            List<int> selectionIndexesSnapshot = _selectionModelExplicitlySet &&
                _selectionModelAdapter?.Model.SelectedIndexes is { Count: > 0 } selectedIndexes
                ? new List<int>(selectedIndexes)
                : null;
            _selectionModelSourceLayoutPending = selectionIndexesSnapshot is { Count: > 0 };
            _selectionModelSourceIndexesPending = selectionIndexesSnapshot;
            _ownsHierarchicalItemsSource = ReferenceEquals(newItemsSource, _hierarchicalItemsSource);
            if (!_ownsHierarchicalItemsSource && !ReferenceEquals(newItemsSource, _hierarchicalItemsSource))
            {
                _hierarchicalItemsSource = null;
            }

            if (!_areHandlersSuspended)
            {
                Debug.Assert(DataConnection != null);

                // Keep transient row/column refreshes from synchronizing an empty realized
                // selection back into the external selection model before the replacement
                // source has populated its slots and current cell.
                _makeFirstDisplayedCellCurrentCellPending = true;

                var oldCollectionView = DataConnection.CollectionView;

                if (LoadingOrUnloadingRow)
                {
                    SetValueNoCallback(ItemsSourceProperty, oldValue);
                    throw DataGridError.DataGrid.CannotChangeItemsWhenLoadingRows();
                }

                // Try to commit edit on the old DataSource, but force a cancel if it fails
                if (!CommitEdit())
                {
                    CancelEdit(DataGridEditingUnit.Row, false);
                }

                // Build the replacement view once and use that same materialization for both
                // the proposal and commit. A SelectionChanging subscriber must not cause a
                // lazy or single-pass ItemsSource to be enumerated an extra time.
                bool setDefaultSelection = false;
                IDataGridCollectionView newCollectionView;
                if (newItemsSource is IDataGridCollectionView suppliedCollectionView)
                {
                    setDefaultSelection = true;
                    newCollectionView = suppliedCollectionView;
                }
                else
                {
                    newCollectionView = newItemsSource is not null
                        ? DataGridDataConnection.CreateView(newItemsSource)
                        : default;
                }

                List<object> selectionSnapshot = CaptureSelectionSnapshot();
                ItemsSourceSelectionTransaction selectionTransaction =
                    oldValue != null || selectionSnapshot != null || CurrentCell.IsValid
                        ? BeginItemsSourceSelectionTransaction(
                            selectionSnapshot,
                            newCollectionView,
                            preserveSelection: false)
                        : null;
                using var selectionCommit = selectionTransaction != null
                    ? BeginSelectionCommit()
                    : default;
                using var sourceMutationDeferral = selectionTransaction != null
                    ? BeginItemsSourceMutationDeferral()
                    : default;

                DataConnection.UnWireEvents(DataConnection.DataSource);
                DataConnection.ClearDataProperties();
                ClearRowGroupHeadersTable();

                // The old selected indexes are no longer relevant. There's a perf benefit from
                // updating the selected indexes with a null DataSource, because we know that all
                // of the previously selected indexes have been removed from selection
                DataConnection.DataSource = null;
                _selectedItems.UpdateIndexes();
                CoerceSelectedItem();

                DataConnection.DataSource = newCollectionView;

                if (oldCollectionView != DataConnection.CollectionView)
                {
                    RaisePropertyChanged(CollectionViewProperty, 
                        oldCollectionView, 
                        newCollectionView);
                }

                UpdateSortingAdapterView();
                UpdateFilteringAdapterView();
                UpdateSearchAdapterView();
                UpdateConditionalFormattingAdapterView();

                // SelectionModel must observe source mutations before DataConnection. Its
                // SourceReset preflight computes the single public SelectionChanging proposal;
                // the DataConnection handler then consumes that pending transaction while it
                // performs the structural row update. Wiring in the opposite order exposes a
                // first proposal from DataConnection followed by reset/selection proposals.
                UpdateSelectionModelSource();

                if (selectionTransaction == null &&
                    selectionIndexesSnapshot is { Count: > 0 } &&
                    _selectionModelAdapter?.Model is { SelectedIndexes.Count: 0 } selectionModel)
                {
                    _syncingSelectionModel = true;
                    try
                    {
                        using (selectionModel.BatchUpdate())
                        {
                            int sourceCount = selectionModel.Source is ICollection collection
                                ? collection.Count
                                : DataConnection?.Count ?? 0;
                            for (int i = 0; i < selectionIndexesSnapshot.Count; i++)
                            {
                                int index = selectionIndexesSnapshot[i];
                                if (index >= 0 && index < sourceCount)
                                {
                                    selectionModel.Select(index);
                                }
                            }
                        }
                    }
                    finally
                    {
                        _syncingSelectionModel = false;
                    }
                }

                if (DataConnection.DataSource != null)
                {
                    // Setup the column headers
                    if (DataConnection.DataType != null)
                    {
                        foreach (var column in ColumnsInternal.GetDisplayedColumns())
                        {
                            if (column is DataGridBoundColumn boundColumn)
                            {
                                boundColumn.SetHeaderFromBinding();
                            }
                        }
                    }
                    DataConnection.WireEvents(DataConnection.DataSource);
                }

                var modelSelectionPending = _selectionModelAdapter?.Model != null &&
                    (_selectionModelAdapter.Model.SelectedIndex >= 0 ||
                     _selectionModelAdapter.Model.SelectedItems.Count > 0);

                // Clear out the old rows and remove the generated columns
                bool previousSelectionSync = false;
                if (modelSelectionPending)
                {
                    previousSelectionSync = PushSelectionSync();
                }
                try
                {
                    ClearRows(false); //recycle
                }
                finally
                {
                    if (modelSelectionPending)
                    {
                        PopSelectionSync(previousSelectionSync);
                    }
                }
                RemoveAutoGeneratedColumns();

                // Notify the estimator about the data source change
                RowHeightEstimator?.OnDataSourceChanged(DataConnection.Count);
                _scrollHeightIndexDirty = true;

                // Set the SlotCount (from the data count and number of row group headers) before we make the default selection
                PopulateRowGroupHeadersTable();

                if (!modelSelectionPending)
                {
                    SelectedItem = null;
                    if (DataConnection.CollectionView != null &&
                        setDefaultSelection &&
                        AutoSelectCurrentItemOnItemsSourceChange)
                    {
                        SelectedItem = ProjectSelectionItem(DataConnection.CollectionView.CurrentItem);
                    }

                    SyncSelectionModelFromGridSelection();

                    if (_selectedItemsBinding != null && _selectedItemsBinding.Count > 0)
                    {
                        ApplySelectedItemsFromBinding(_selectedItemsBinding);
                    }
                }
                else
                {
                    ApplySelectionFromSelectionModel();
                }

                // Treat this like the DataGrid has never been measured because all calculations at
                // this point are invalid until the next layout cycle.  For instance, the ItemsSource
                // can be set when the DataGrid is not part of the visual tree
                _measured = false;
                InvalidateMeasure();

                UpdatePseudoClasses();
                CompleteItemsSourceSelectionTransaction(selectionTransaction);
                OnDataSourceChangedForSummaries();
                OnDataSourceChangedForValidation();
                RaiseAutomationStructureChanged();

                if (_selectionModelSourceLayoutPending)
                {
                    Dispatcher.UIThread.Post(
                        CompletePendingSelectionModelSourceLayout,
                        DispatcherPriority.SystemIdle);
                }
            }
        }

        private void UpdateSelectionModelSource()
        {
            if (_selectionModelAdapter != null)
            {
                _syncingSelectionModel = true;
                try
                {
                    var view = DataConnection?.CollectionView;
                    IEnumerable source = view;

                    if (view is DataGridCollectionView projected && projected.PageSize > 0)
                    {
                        if (_pagedSelectionSource == null || !ReferenceEquals(_pagedSelectionSourceView, projected))
                        {
                            _pagedSelectionSource?.Dispose();
                            _pagedSelectionSource = new DataGridSelection.DataGridPagedSelectionSource(projected);
                            _pagedSelectionSourceView = projected;
                        }
                        source = _pagedSelectionSource;
                    }
                    else
                    {
                        _pagedSelectionSource?.Dispose();
                        _pagedSelectionSource = null;
                        _pagedSelectionSourceView = null;
                    }

                    _selectionModelAdapter.Model.Source = source;
                }
                finally
                {
                    _syncingSelectionModel = false;
                }
            }
        }

        internal List<object> CaptureSelectionSnapshot()
        {
            // Prefer capturing via the selection model to avoid losing selection when the view
            // issues a Reset (sorting/filtering/paging).
            if (_selectionModelAdapter?.Model is { } model)
            {
                if (_selectionModelSnapshot is { Count: > 0 })
                {
                    return new List<object>(_selectionModelSnapshot);
                }

                if (model.SelectedIndexes is { Count: > 0 } indexes &&
                    model.Source is IList list &&
                    list.Count > 0)
                {
                    var snapshot = new List<object>();
                    foreach (var index in indexes)
                    {
                        if (index >= 0 && index < list.Count)
                        {
                            snapshot.Add(list[index]);
                        }
                    }

                    if (snapshot.Count > 0)
                    {
                        return snapshot;
                    }
                }

                if (_selectionModelSnapshot is { Count: > 0 })
                {
                    return new List<object>(_selectionModelSnapshot);
                }
            }

            if (SelectedItems is { Count: > 0 } selected)
            {
                return new List<object>(selected.Cast<object>());
            }

            if (_hierarchicalRowsEnabled && _hierarchicalModel != null &&
                _pendingHierarchicalSelectionSnapshot is { Count: > 0 })
            {
                return new List<object>(_pendingHierarchicalSelectionSnapshot);
            }

            return null;
        }

        internal void CacheHierarchicalSelectionSnapshot(IReadOnlyList<object> snapshot)
        {
            _pendingHierarchicalSelectionSnapshot = snapshot != null && snapshot.Count > 0
                ? new List<object>(snapshot)
                : null;
        }

        internal void CacheHierarchicalSelectionIndexes(IReadOnlyList<int> indexes)
        {
            _pendingHierarchicalSelectionIndexes = indexes != null && indexes.Count > 0
                ? new List<int>(indexes)
                : null;
        }

        internal void RestoreSelectionFromSnapshot(IReadOnlyList<object> selectedItems)
        {
            if (_selectionModelAdapter == null || selectedItems == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
            var proposedItems = selectedItems as IList ?? selectedItems.ToList();
            HashSet<int> proposedRows = BuildRowSelectionProposal(proposedItems, out int proposedSlot);
            int proposedColumnIndex = CurrentColumnIndex >= 0
                ? CurrentColumnIndex
                : FirstDisplayedNonFillerColumnIndex;
            if (!TryPreviewRowSet(
                    proposedRows,
                    CreateCellInfo(proposedColumnIndex, proposedSlot),
                    CreateAnchorInfo(proposedSlot, proposedColumnIndex)))
            {
                return;
            }

            using var commit = BeginSelectionCommit();
            _syncingSelectionModel = true;
            try
            {
                int firstIndex = -1;

                using (_selectionModelAdapter.SelectedItemsView.SuppressNotifications())
                using (_selectionModelAdapter.Model.BatchUpdate())
                {
                    _selectionModelAdapter.Model.Clear();
                    foreach (object item in selectedItems)
                    {
                        int index = GetSelectionModelIndexOfItem(item);
                        if (index >= 0)
                        {
                            if (firstIndex == -1)
                            {
                                firstIndex = index;
                            }

                            _selectionModelAdapter.Select(index);
                        }
                    }
                }

                if (firstIndex >= 0)
                {
                    _preferredSelectionIndex = firstIndex;
                }

                ApplySelectionFromSelectionModel();

                foreach (object item in selectedItems)
                {
                    int index = GetSelectionModelIndexOfItem(item);
                    if (index >= 0)
                    {
                        SetValueNoCallback(SelectedItemProperty, ProjectSelectionItem(item));
                        SetValueNoCallback(SelectedIndexProperty, index);
                        break;
                    }
                }
            }
            finally
            {
                _syncingSelectionModel = false;
            }
        }

        private void SyncSelectionModelFromGridSelection()
        {
            if (_selectionModelAdapter == null ||
                DataConnection?.CollectionView == null ||
                _syncingSelectionModel ||
                _selectionModelSourceLayoutPending)
            {
                return;
            }

            _selectionModelAdapter.Model.BeginBatchUpdate();
            _syncingSelectionModel = true;
            try
            {
                _selectionModelAdapter.Model.Clear();
                foreach (object item in _selectedItems)
                {
                    int index = GetSelectionModelIndexOfItem(item);
                    if (index >= 0)
                    {
                        _selectionModelAdapter.Model.Select(index);
                    }
                }
            }
            finally
            {
                _selectionModelAdapter.Model.EndBatchUpdate();
                _syncingSelectionModel = false;
            }

            UpdateSelectionSnapshot();
        }

        internal void ResyncSelectionModelFromGridSelection()
        {
            SyncSelectionModelFromGridSelection();
        }


        internal void RefreshRowsAndColumns(bool clearRows, bool recycleDisplayedRows = false)
        {
            using var activity = DataGridDiagnostics.RefreshRowsAndColumns();
            using var _ = DataGridDiagnostics.BeginDataGridRefresh();
            activity?.SetTag(DataGridDiagnostics.Tags.ClearRows, clearRows);
            activity?.SetTag(DataGridDiagnostics.Tags.AutoGenerateColumns, AutoGenerateColumns);
            activity?.SetTag(DataGridDiagnostics.Tags.Columns, ColumnsItemsInternal.Count);
            activity?.SetTag(DataGridDiagnostics.Tags.Rows, DataConnection?.Count ?? 0);
            activity?.SetTag(DataGridDiagnostics.Tags.SlotCount, SlotCount);

            if (_measured)
            {
                try
                {
                    _noCurrentCellChangeCount++;

                    if (clearRows)
                    {
                        ClearRows(false);
                        ClearRowGroupHeadersTable();
                        PopulateRowGroupHeadersTable();
                    }
                    if (AutoGenerateColumns)
                    {
                        //Column auto-generation refreshes the rows too
                        AutoGenerateColumnsPrivate();
                    }
                    foreach (DataGridColumn column in ColumnsItemsInternal)
                    {
                        //We don't need to refresh the state of AutoGenerated column headers because they're up-to-date
                        if (!column.IsAutoGenerated && column.HasHeaderCell)
                        {
                            column.HeaderCell.UpdatePseudoClasses();
                        }
                    }

                    RefreshRows(
                        recycleRows: recycleDisplayedRows,
                        clearRows: false,
                        recycleDisplayedRows: recycleDisplayedRows);

                    if (ColumnDefinitions.Count > 0 && CurrentColumnIndex == -1)
                    {
                        MakeFirstDisplayedCellCurrentCell();
                    }
                    else
                    {
                        RestoreSelectionModelBeforeCompletingPendingLayout();
                        _makeFirstDisplayedCellCurrentCellPending = false;
                        _desiredCurrentColumnIndex = -1;
                        FlushCurrentCellChanged();
                    }
                }
                finally
                {
                    NoCurrentCellChangeCount--;
                }
            }
            else
            {
                if (clearRows)
                {
                    ClearRows(recycle: false);
                }
                ClearRowGroupHeadersTable();
                PopulateRowGroupHeadersTable();
            }

            RequestPointerOverRefresh();

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
        }


        internal void UpdateStateOnCurrentChanged(object currentItem, int currentPosition)
        {
            using var selectionScope = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.ItemsSourceChange,
                guarantee: DataGridSelectionChangingGuarantee.PostChangeReconciliation);

            var currentSelectionIndex = currentPosition;
            if (_selectionModelAdapter != null && TryGetPagingInfo(out _, out var pageStart))
            {
                currentSelectionIndex = pageStart + currentPosition;
            }

            if (currentItem == CurrentItem && currentItem == SelectedItem && currentSelectionIndex == SelectedIndex)
            {
                // The DataGrid's CurrentItem is already up-to-date, so we don't need to do anything
                return;
            }

            int columnIndex = CurrentColumnIndex;
            if (columnIndex == -1)
            {
                if (IsColumnOutOfBounds(_desiredCurrentColumnIndex) ||
                    (ColumnsInternal.RowGroupSpacerColumn.IsRepresented && _desiredCurrentColumnIndex == ColumnsInternal.RowGroupSpacerColumn.Index))
                {
                    columnIndex = FirstDisplayedNonFillerColumnIndex;
                }
                else
                {
                    columnIndex = _desiredCurrentColumnIndex;
                }
            }
            _desiredCurrentColumnIndex = -1;

            int slot = currentItem != null ? SlotFromSelectionIndex(currentSelectionIndex) : -1;
            bool currentInSelection = currentItem != null &&
                slot >= 0 &&
                GetRowSelection(slot);

            if (currentItem != null && (slot < 0 || slot >= SlotCount))
            {
                if (!TryPreviewClearSelectionAndCurrent())
                {
                    return;
                }

                using var commit = BeginSelectionCommit();
                ClearRowSelection(true);
                SetCurrentCellCore(-1, -1);
                return;
            }

            if (_selectionModelAdapter != null &&
                _selectionModelAdapter.Model.SelectedIndexes.Count > 0 &&
                !currentInSelection)
            {
                if (!TryPreviewSelectionModelSelection())
                {
                    return;
                }

                using var commit = BeginSelectionCommit();
                ApplySelectionFromSelectionModel();
                return;
            }

            try
            {
                _noSelectionChangeCount++;
                _noCurrentCellChangeCount++;

                if (currentItem == null)
                {
                    if (!TryPreviewClearSelectionAndCurrent())
                    {
                        return;
                    }

                    using var commit = BeginSelectionCommit();
                    if (!CommitEdit())
                    {
                        CancelEdit(DataGridEditingUnit.Row, false);
                    }
                    ClearRowSelection(true);
                    SetCurrentCellCore(-1, -1);
                }
                else if (currentInSelection)
                {
                    ProcessSelectionAndCurrency(columnIndex, currentItem, slot, DataGridSelectionAction.None, false);
                }
                else
                {
                    if (!TryPreviewRowSelection(columnIndex, slot, DataGridSelectionAction.SelectCurrent))
                    {
                        return;
                    }

                    using var commit = BeginSelectionCommit();
                    if (!CommitEdit())
                    {
                        CancelEdit(DataGridEditingUnit.Row, false);
                    }
                    ClearRowSelection(true);
                    ProcessSelectionAndCurrency(columnIndex, currentItem, slot, DataGridSelectionAction.SelectCurrent, false);
                }
            }
            finally
            {
                NoCurrentCellChangeCount--;
                NoSelectionChangeCount--;
            }
        }


        // Returns the item or the CollectionViewGroup that is used as the DataContext for a given slot.
        // If the DataContext is an item, rowIndex is set to the index of the item within the collection
        internal object ItemFromSlot(int slot, ref int rowIndex)
        {
            if (IsGroupSlot(slot))
            {
                var info = RowGroupHeadersTable.GetValueAt(slot) ?? RowGroupFootersTable.GetValueAt(slot);
                return info?.CollectionViewGroup;
            }
            else
            {
                rowIndex = RowIndexFromSlot(slot);
                return DataConnection.GetDataItem(rowIndex);
            }
        }


        private void ColumnsInternal_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnColumnsInternalBindingChanged(e);
            OnColumnsChangedForValidation();

            if (e.Action == NotifyCollectionChangedAction.Add
                || e.Action == NotifyCollectionChangedAction.Remove
                || e.Action == NotifyCollectionChangedAction.Reset)
            {
                UpdatePseudoClasses();
                UpdateSearchAdapterView();
            }
        }

    }
}
