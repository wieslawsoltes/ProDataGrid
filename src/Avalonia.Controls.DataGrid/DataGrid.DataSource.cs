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

        /// <summary>
        /// ItemsSourceProperty property changed handler.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnItemsSourcePropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            using var selectionScope = BeginSelectionChangeScope(DataGridSelectionChangeSource.ItemsSourceChange, sticky: true);

            var oldValue = (IEnumerable)e.OldValue;
            var newItemsSource = (IEnumerable)e.NewValue;
            var switchingFromOwnedHierarchical = ReferenceEquals(oldValue, _hierarchicalItemsSource) && _ownsHierarchicalItemsSource && !ReferenceEquals(oldValue, newItemsSource);

            _ownsHierarchicalItemsSource = ReferenceEquals(newItemsSource, _hierarchicalItemsSource);
            if (!_ownsHierarchicalItemsSource && !ReferenceEquals(newItemsSource, _hierarchicalItemsSource))
            {
                _hierarchicalItemsSource = null;
            }

            if (switchingFromOwnedHierarchical && _selectionModelAdapter?.Model != null)
            {
                _syncingSelectionModel = true;
                try
                {
                    _selectionModelAdapter.Model.Source = null;
                }
                finally
                {
                    _syncingSelectionModel = false;
                }
            }

            if (!_areHandlersSuspended)
            {
                Debug.Assert(DataConnection != null);

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

                DataConnection.UnWireEvents(DataConnection.DataSource);
                DataConnection.ClearDataProperties();
                ClearRowGroupHeadersTable();

                // The old selected indexes are no longer relevant. There's a perf benefit from
                // updating the selected indexes with a null DataSource, because we know that all
                // of the previously selected indexes have been removed from selection
                DataConnection.DataSource = null;
                _selectedItems.UpdateIndexes();
                CoerceSelectedItem();

                // Wrap an IEnumerable in an ICollectionView if it's not already one
                bool setDefaultSelection = false;
                if (newItemsSource is IDataGridCollectionView newCollectionView)
                {
                    setDefaultSelection = true;
                }
                else
                {
                    newCollectionView =  newItemsSource is not null
                        ? DataGridDataConnection.CreateView(newItemsSource)
                        : default;
                }

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

                UpdateSelectionModelSource();

                // Wait for the current cell to be set before we raise any SelectionChanged events
                _makeFirstDisplayedCellCurrentCellPending = true;

                // Clear out the old rows and remove the generated columns
                ClearRows(false); //recycle
                RemoveAutoGeneratedColumns();

                // Notify the estimator about the data source change
                RowHeightEstimator?.OnDataSourceChanged(DataConnection.Count);

                // Set the SlotCount (from the data count and number of row group headers) before we make the default selection
                PopulateRowGroupHeadersTable();
                var modelSelectionPending = _selectionModelAdapter?.Model != null &&
                    (_selectionModelAdapter.Model.SelectedIndex >= 0 ||
                     _selectionModelAdapter.Model.SelectedItems.Count > 0);

                if (!modelSelectionPending)
                {
                    SelectedItem = null;
                    if (DataConnection.CollectionView != null && setDefaultSelection)
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
                OnDataSourceChangedForSummaries();
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
                    IEnumerable? source = view;

                    if (view is DataGridCollectionView paged && paged.PageSize > 0)
                    {
                        _pagedSelectionSource?.Dispose();
                        _pagedSelectionSource = new DataGridSelection.DataGridPagedSelectionSource(paged);
                        source = _pagedSelectionSource;
                    }
                    else
                    {
                        _pagedSelectionSource?.Dispose();
                        _pagedSelectionSource = null;
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

            return null;
        }

        internal void RestoreSelectionFromSnapshot(IReadOnlyList<object> selectedItems)
        {
            if (_selectionModelAdapter == null || selectedItems == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
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
            if (_selectionModelAdapter == null || DataConnection?.CollectionView == null || _syncingSelectionModel)
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


        internal void RefreshRowsAndColumns(bool clearRows)
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

                    RefreshRows(recycleRows: false, clearRows: false);

                    if (ColumnDefinitions.Count > 0 && CurrentColumnIndex == -1)
                    {
                        MakeFirstDisplayedCellCurrentCell();
                    }
                    else
                    {
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
            using var selectionScope = BeginSelectionChangeScope(DataGridSelectionChangeSource.ItemsSourceChange);

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
                ClearRowSelection(true);
                SetCurrentCellCore(-1, -1);
                return;
            }

            if (_selectionModelAdapter != null &&
                _selectionModelAdapter.Model.SelectedIndexes.Count > 0 &&
                !currentInSelection)
            {
                ApplySelectionFromSelectionModel();
                return;
            }

            try
            {
                _noSelectionChangeCount++;
                _noCurrentCellChangeCount++;

                if (!CommitEdit())
                {
                    CancelEdit(DataGridEditingUnit.Row, false);
                }

                if (currentItem == null)
                {
                    ClearRowSelection(true);
                    SetCurrentCellCore(-1, -1);
                }
                else if (currentInSelection)
                {
                    ProcessSelectionAndCurrency(columnIndex, currentItem, slot, DataGridSelectionAction.None, false);
                }
                else
                {
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
