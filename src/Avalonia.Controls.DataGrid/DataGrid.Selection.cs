// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Utils;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using Avalonia.Utilities;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Selection management
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {

        public void SelectAll()
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Command);
            SetRowsSelection(0, SlotCount - 1);
        }


        /// <summary>
        /// Selects items and updates currency based on parameters
        /// </summary>
        /// <param name="columnIndex">column index to make current</param>
        /// <param name="item">data item or CollectionViewGroup to make current</param>
        /// <param name="backupSlot">slot to use in case the item is no longer valid</param>
        /// <param name="action">selection action to perform</param>
        /// <param name="scrollIntoView">whether or not the new current item should be scrolled into view</param>
        internal void ProcessSelectionAndCurrency(int columnIndex, object item, int backupSlot, DataGridSelectionAction action, bool scrollIntoView)
        {
            _noSelectionChangeCount++;
            _noCurrentCellChangeCount++;
            try
            {
                int slot = -1;
                if (item is DataGridCollectionViewGroup group)
                {
                    DataGridRowGroupInfo groupInfo = RowGroupInfoFromCollectionViewGroup(group);
                    if (groupInfo != null)
                    {
                        slot = groupInfo.Slot;
                    }
                }
                else
                {
                    slot = SlotFromRowIndex(DataConnection.IndexOf(item));
                }
                if (slot == -1)
                {
                    slot = backupSlot;
                }
                if (slot < 0 || slot >= SlotCount)
                {
                    return;
                }

                columnIndex = CoerceColumnIndexToVisible(columnIndex);

                ApplySelectionActionToSelectionModel(slot, action);

                switch (action)
                {
                    case DataGridSelectionAction.AddCurrentToSelection:
                        SetRowSelection(slot, isSelected: true, setAnchorSlot: true);
                        break;
                    case DataGridSelectionAction.RemoveCurrentFromSelection:
                        SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                        break;
                    case DataGridSelectionAction.SelectFromAnchorToCurrent:
                        if (SelectionMode == DataGridSelectionMode.Extended && AnchorSlot != -1)
                        {
                            int anchorSlot = AnchorSlot;
                            if (slot <= anchorSlot)
                            {
                                SetRowsSelection(slot, anchorSlot);
                            }
                            else
                            {
                                SetRowsSelection(anchorSlot, slot);
                            }
                        }
                        else
                        {
                            goto case DataGridSelectionAction.SelectCurrent;
                        }
                        break;
                    case DataGridSelectionAction.SelectCurrent:
                        ClearRowSelection(slot, setAnchorSlot: true);
                        break;
                    case DataGridSelectionAction.None:
                        break;
                }

                bool currentCellChanged = CurrentSlot != slot || (CurrentColumnIndex != columnIndex && columnIndex != -1);
                int scrollColumnIndex = columnIndex;

                if (currentCellChanged)
                {
                    if (columnIndex == -1)
                    {
                        var fallbackColumnIndex = CoerceColumnIndexToVisible(CurrentColumnIndex);
                        if (fallbackColumnIndex != -1)
                        {
                            columnIndex = fallbackColumnIndex;
                        }
                        else
                        {
                            DataGridColumn firstVisibleColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                            if (firstVisibleColumn != null)
                            {
                                columnIndex = firstVisibleColumn.Index;
                            }
                        }
                    }
                    if (columnIndex != -1)
                    {
                        if (!SetCurrentCellCore(
                                columnIndex, slot,
                                commitEdit: true,
                                endRowEdit: SlotFromRowIndex(SelectedIndex) != slot))
                        {
                            return;
                        }
                    }

                    scrollColumnIndex = columnIndex;
                }

                if (scrollIntoView)
                {
                    if (scrollColumnIndex == -1)
                    {
                        scrollColumnIndex = CoerceColumnIndexToVisible(CurrentColumnIndex);
                        if (scrollColumnIndex == -1)
                        {
                            DataGridColumn firstVisibleColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                            if (firstVisibleColumn != null)
                            {
                                scrollColumnIndex = firstVisibleColumn.Index;
                            }
                        }
                    }

                    if (scrollColumnIndex != -1 &&
                        !ScrollSlotIntoView(
                            scrollColumnIndex, slot,
                            forCurrentCellChange: currentCellChanged,
                            forceHorizontalScroll: false))
                    {
                        return;
                    }
                }
                _successfullyUpdatedSelection = true;
            }
            finally
            {
                NoCurrentCellChangeCount--;
                NoSelectionChangeCount--;
            }
        }

        private int CoerceColumnIndexToVisible(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
            {
                return -1;
            }

            if (ColumnsItemsInternal[columnIndex].IsVisible)
            {
                return columnIndex;
            }

            var fallbackColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            return fallbackColumn?.Index ?? -1;
        }

        private void ApplySelectionActionToSelectionModel(int slot, DataGridSelectionAction action)
        {
            if (_syncingSelectionModel)
            {
                return;
            }

            if (_selectionModelAdapter == null || DataConnection?.CollectionView == null)
            {
                return;
            }

            int rowIndex = SelectionIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return;
            }

            using (_selectionModelAdapter.Model.BatchUpdate())
            {
                _syncingSelectionModel = true;
                try
                {
                    switch (action)
                    {
                        case DataGridSelectionAction.AddCurrentToSelection:
                            _selectionModelAdapter.Select(rowIndex);
                            break;
                        case DataGridSelectionAction.RemoveCurrentFromSelection:
                            _selectionModelAdapter.Deselect(rowIndex);
                            break;
                        case DataGridSelectionAction.SelectFromAnchorToCurrent:
                            if (_selectionModelAdapter.Model.SingleSelect)
                            {
                                _selectionModelAdapter.Clear();
                                _selectionModelAdapter.Select(rowIndex);
                                break;
                            }

                            if (AnchorSlot != -1)
                            {
                                int anchorIndex = SelectionIndexFromSlot(AnchorSlot);
                                if (anchorIndex >= 0)
                                {
                                    int start = Math.Min(anchorIndex, rowIndex);
                                    int end = Math.Max(anchorIndex, rowIndex);
                                    _selectionModelAdapter.SelectRange(start, end);
                                }
                            }
                            else
                            {
                                _selectionModelAdapter.Clear();
                                _selectionModelAdapter.Select(rowIndex);
                            }
                            break;
                        case DataGridSelectionAction.SelectCurrent:
                            _selectionModelAdapter.Clear();
                            _selectionModelAdapter.Select(rowIndex);
                            break;
                        case DataGridSelectionAction.None:
                            break;
                    }
                }
                finally
                {
                    _syncingSelectionModel = false;
                }
            }
        }

        private void SelectionModel_SelectionChanged(object sender, SelectionModelSelectionChangedEventArgs e)
        {
            if (_syncingSelectionModel)
            {
                return;
            }

            try
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
                _syncingSelectionModel = true;

                ApplySelectionFromSelectionModel();
                UpdateSelectionSnapshot();
            }
            finally
            {
                _syncingSelectionModel = false;
                ClearPendingHierarchicalSelection();
            }
        }

        private void ApplySelectionFromSelectionModel()
        {
            var previousSync = _syncingSelectionModel;
            _syncingSelectionModel = true;
            try
            {
                if (CurrentColumnIndex > -1 && (CurrentSlot < 0 || CurrentSlot >= SlotCount))
                {
                    CurrentColumnIndex = -1;
                    CurrentSlot = -1;
                }

                var indexes = _selectionModelAdapter?.Model.SelectedIndexes;
                if (_selectionModelAdapter?.Model.Source == null || indexes == null || indexes.Count == 0)
                {
                    ClearRowSelection(resetAnchorSlot: true);
                    SetCurrentCellCore(-1, -1);
                    return;
                }

                int preferredIndex = _preferredSelectionIndex >= 0
                    ? _preferredSelectionIndex
                    : _selectionModelAdapter.Model.SelectedIndex;

                var mapped = new List<(int RowIndex, int Slot)>();
                foreach (int rowIndex in indexes)
                {
                    int slot = SlotFromSelectionIndex(rowIndex);
                    if (slot >= 0 && slot < SlotCount)
                    {
                        mapped.Add((rowIndex, slot));
                    }
                }

                if (mapped.Count == 0)
                {
                    _preferredSelectionIndex = preferredIndex;
                    ClearRowSelection(resetAnchorSlot: true);
                    SetCurrentCellCore(-1, -1);
                    return;
                }

                ClearRowSelection(resetAnchorSlot: true);

                int? firstSlot = null;
                foreach (var entry in mapped)
                {
                    int slot = entry.Slot;
                    int rowIndex = entry.RowIndex;
                    if (firstSlot == null || (preferredIndex >= 0 && rowIndex == preferredIndex))
                    {
                        firstSlot = slot;
                    }
                    SetRowSelection(slot, isSelected: true, setAnchorSlot: firstSlot == slot);
                }

                if (firstSlot.HasValue)
                {
                    int columnIndex = CurrentColumnIndex != -1 ? CurrentColumnIndex : FirstDisplayedNonFillerColumnIndex;
                    UpdateSelectionAndCurrency(columnIndex, firstSlot.Value, DataGridSelectionAction.None, scrollIntoView: false);
                }
                else
                {
                    SetCurrentCellCore(-1, -1);
                }

                _preferredSelectionIndex = -1;

                RefreshVisibleSelection();
            }
            finally
            {
                _syncingSelectionModel = previousSync;
            }
        }

        internal void RefreshVisibleSelection()
        {
            if (DisplayData == null)
            {
                return;
            }

            for (int slot = DisplayData.FirstScrollingSlot;
                slot > -1 && slot <= DisplayData.LastScrollingSlot;
                slot++)
            {
                var element = DisplayData.GetDisplayedElement(slot);
                if (element is DataGridRow row)
                {
                    row.ApplyState();
                    row.ApplyCellsState();
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.UpdatePseudoClasses();
                }
            }
        }

        private void SelectionModel_IndexesChanged(object sender, SelectionModelIndexesChangedEventArgs e)
        {
            if (_syncingSelectionModel)
            {
                return;
            }

            try
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
                _syncingSelectionModel = true;
                ApplySelectionFromSelectionModel();
                UpdateSelectionSnapshot();
            }
            finally
            {
                _syncingSelectionModel = false;
            }
        }

        private void SelectionModel_LostSelection(object sender, EventArgs e)
        {
            if (_syncingSelectionModel)
            {
                return;
            }

            try
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
                _syncingSelectionModel = true;
                ClearRowSelection(resetAnchorSlot: true);
                SetCurrentCellCore(-1, -1);
            }
            finally
            {
                _syncingSelectionModel = false;
            }
        }

        private void SelectionModel_SourceReset(object sender, EventArgs e)
        {
            if (_selectionModelAdapter == null)
            {
                ClearPendingHierarchicalSelection();
                return;
            }

            if (_syncingSelectionModel)
            {
                ClearPendingHierarchicalSelection();
                return;
            }

            var snapshot = CaptureSelectionSnapshot();
            if ((snapshot == null || snapshot.Count == 0) &&
                _hierarchicalRowsEnabled && _hierarchicalModel != null &&
                _pendingHierarchicalSelectionSnapshot is { Count: > 0 } pendingSnapshot)
            {
                snapshot = new List<object>(pendingSnapshot);
            }
            if (snapshot == null || snapshot.Count == 0)
            {
                if (!HasInvalidSelectionIndexes(_selectionModelAdapter.Model))
                {
                    ClearPendingHierarchicalSelection();
                    return;
                }
            }

            try
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
                _syncingSelectionModel = true;
                using (_selectionModelAdapter.Model.BatchUpdate())
                {
                    _selectionModelAdapter.Model.Clear();
                    if (snapshot is { Count: > 0 })
                    {
                        foreach (var item in snapshot)
                        {
                            int index = GetSelectionModelIndexOfItem(item);
                            if (index >= 0)
                            {
                                _selectionModelAdapter.Select(index);
                            }
                        }
                    }
                }

                _preferredSelectionIndex = _selectionModelAdapter.Model.SelectedIndex;
                ApplySelectionFromSelectionModel();
                UpdateSelectionSnapshot();
            }
            finally
            {
                _syncingSelectionModel = false;
                ClearPendingHierarchicalSelection();
            }
        }

        internal void UpdateSelectionSnapshot()
        {
            if (_suppressSelectionSnapshotUpdates)
            {
                return;
            }

            if (_selectionModelAdapter != null)
            {
                _selectionModelSnapshot = _selectionModelAdapter.SelectedItemsView.Cast<object>().ToList();
            }
            else
            {
                _selectionModelSnapshot = null;
            }
        }

        private void RestoreSelectionFromSnapshot()
        {
            if (_selectionModelAdapter == null)
            {
                return;
            }

            var snapshot = _selectionModelSnapshot;
            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            try
            {
                _syncingSelectionModel = true;
                using (_selectionModelAdapter.Model.BatchUpdate())
                {
                    _selectionModelAdapter.Model.Clear();
                    foreach (var item in snapshot)
                    {
                        int index = GetSelectionModelIndexOfItem(item);
                        if (index >= 0)
                        {
                            _selectionModelAdapter.Select(index);
                        }
                    }
                }
            }
            finally
            {
                _syncingSelectionModel = false;
            }
        }

        internal IDisposable BeginSelectionSnapshotSuppression()
        {
            _suppressSelectionSnapshotUpdates = true;
            return new SelectionSnapshotSuppression(this);
        }

        private sealed class SelectionSnapshotSuppression : IDisposable
        {
            private readonly DataGrid _owner;
            private bool _disposed;

            public SelectionSnapshotSuppression(DataGrid owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner._suppressSelectionSnapshotUpdates = false;
                _disposed = true;
            }
        }

        private void SelectionModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_syncingSelectionModel)
            {
                return;
            }

            if (e.PropertyName == nameof(ISelectionModel.SingleSelect))
            {
                try
                {
                    _syncingSelectionModel = true;
                    SelectionMode = _selectionModelAdapter.Model.SingleSelect
                        ? DataGridSelectionMode.Single
                        : DataGridSelectionMode.Extended;
                }
                finally
                {
                    _syncingSelectionModel = false;
                }
            }
        }

        internal void RefreshSelectionFromModel()
        {
            if (_selectionModelAdapter == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
            _syncingSelectionModel = true;
            try
            {
                ApplySelectionFromSelectionModel();
            }
            finally
            {
                _syncingSelectionModel = false;
            }
        }


        internal bool UpdateSelectionAndCurrency(int columnIndex, int slot, DataGridSelectionAction action, bool scrollIntoView)
        {
            _successfullyUpdatedSelection = false;
            bool effectiveScrollIntoView = scrollIntoView || AutoScrollToSelectedItem;

            _noSelectionChangeCount++;
            _noCurrentCellChangeCount++;
            try
            {
                if (ColumnsInternal.RowGroupSpacerColumn.IsRepresented &&
                    columnIndex == ColumnsInternal.RowGroupSpacerColumn.Index)
                {
                    columnIndex = -1;
                }
                if (IsSlotOutOfSelectionBounds(slot) || (columnIndex != -1 && IsColumnOutOfBounds(columnIndex)))
                {
                    return false;
                }

                int newCurrentPosition = -1;
                object item = ItemFromSlot(slot, ref newCurrentPosition);

                if (EditingRow != null && slot != EditingRow.Slot && !CommitEdit(DataGridEditingUnit.Row, true))
                {
                    return false;
                }

                if (DataConnection.CollectionView != null &&
                    DataConnection.CollectionView.CurrentPosition != newCurrentPosition)
                {
                    DataConnection.MoveCurrentTo(item, slot, columnIndex, action, effectiveScrollIntoView);
                }
                else
                {
                    ProcessSelectionAndCurrency(columnIndex, item, slot, action, effectiveScrollIntoView);
                }
            }
            finally
            {
                NoCurrentCellChangeCount--;
                NoSelectionChangeCount--;
            }

            return _successfullyUpdatedSelection;
        }


        private void SetAndSelectCurrentCell(int columnIndex,
                                             int slot,
                                             bool forceCurrentCellSelection)
        {
            DataGridSelectionAction action = forceCurrentCellSelection ? DataGridSelectionAction.SelectCurrent : DataGridSelectionAction.None;
            UpdateSelectionAndCurrency(columnIndex, slot, action, scrollIntoView: false);
        }


        private void FlushSelectionChanged()
        {
            if (SelectionHasChanged && _noSelectionChangeCount == 0 && !_makeFirstDisplayedCellCurrentCellPending)
            {
                CoerceSelectedItem();
                if (AutoScrollToSelectedItem)
                {
                    RequestAutoScrollToSelection();
                }
                if (NoCurrentCellChangeCount != 0)
                {
                    // current cell is changing, don't raise SelectionChanged until it's done
                    return;
                }
                SelectionHasChanged = false;

                if (_flushCurrentCellChanged)
                {
                    FlushCurrentCellChanged();
                }

                SelectionChangedEventArgs e = _selectedItems.GetSelectionChangedEventArgs(
                    CurrentSelectionChangeSource,
                    CurrentSelectionTriggerEvent);
                if (e.AddedItems.Count > 0 || e.RemovedItems.Count > 0)
                {
                    OnSelectionChanged(e);
                }

                SyncSelectionModelFromGridSelection();
                UpdateSelectionSnapshot();
            }
        }

        private void SetSelectedItemsCollection(IList value)
        {
            IList newValue = value ?? (IList)_selectedItems;
            IList oldValue = SelectedItems;

            if (ReferenceEquals(oldValue, newValue))
            {
                return;
            }

            DetachBoundSelectedItems();
            _selectedItemsBinding = ReferenceEquals(newValue, _selectedItems) ? null : newValue;
            AttachBoundSelectedItems();

            RaisePropertyChanged(SelectedItemsProperty, oldValue, SelectedItems);

            if (_selectedItemsBinding != null)
            {
                ApplySelectedItemsFromBinding(_selectedItemsBinding);
            }
        }

        private void AttachBoundSelectedItems()
        {
            if (_selectedItemsBinding is INotifyCollectionChanged incc)
            {
                _selectedItemsBindingNotifications = incc;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedItemsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedItemsCollectionChanged);
            }
        }

        private void DetachBoundSelectedItems()
        {
            if (_selectedItemsBindingNotifications != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedItemsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedItemsCollectionChanged);
                _selectedItemsBindingNotifications = null;
            }
        }

        private void OnBoundSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedItems || _selectedItemsBinding == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            try
            {
                _syncingSelectedItems = true;
                ApplySelectedItemsChangeFromBinding(e);
            }
            finally
            {
                _syncingSelectedItems = false;
            }
        }

        private void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedItems || _selectedItemsBinding == null)
            {
                return;
            }

            try
            {
                _syncingSelectedItems = true;
                ApplySelectionChangeToBinding(e);
            }
            finally
            {
                _syncingSelectedItems = false;
            }
        }

        private void ApplySelectedItemsFromBinding(IList boundItems)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            bool previousSync = _syncingSelectedItems;
            _syncingSelectedItems = true;
            try
            {
                if (_selectionModelAdapter != null && DataConnection?.CollectionView != null)
                {
                    _syncingSelectionModel = true;
                    try
                    {
                        using (_selectionModelAdapter.Model.BatchUpdate())
                        {
                            _selectionModelAdapter.Model.Clear();
                            foreach (object item in boundItems)
                            {
                                int index = GetSelectionModelIndexOfItem(item);
                                if (index >= 0)
                                {
                                    _selectionModelAdapter.Select(index);
                                }
                            }
                        }

                        ApplySelectionFromSelectionModel();
                    }
                    finally
                    {
                        _syncingSelectionModel = false;
                    }
                    return;
                }

                if (SelectionMode == DataGridSelectionMode.Single)
                {
                    SelectedItem = boundItems.Count > 0 ? boundItems[boundItems.Count - 1] : null;
                    NormalizeBoundSelectionForSingleMode();
                    return;
                }

                ClearRowSelection(resetAnchorSlot: true);

                foreach (object item in boundItems)
                {
                    _selectedItems.Add(item);
                }
            }
            finally
            {
                _syncingSelectedItems = previousSync;
            }
        }

        private void ApplySelectedItemsChangeFromBinding(NotifyCollectionChangedEventArgs e)
        {
            if (ReferenceEquals(_selectedItemsBinding, _selectedItems))
            {
                return;
            }

            if (_selectionModelAdapter != null && DataConnection?.CollectionView == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ApplySelectedItemsFromBinding(_selectedItemsBinding);
                    break;
                case NotifyCollectionChangedAction.Add:
                    if (SelectionMode == DataGridSelectionMode.Single)
                    {
                        if (e.NewItems != null && e.NewItems.Count > 0)
                        {
                            SelectedItem = e.NewItems[e.NewItems.Count - 1];
                        }
                        NormalizeBoundSelectionForSingleMode();
                        break;
                    }

                    if (e.NewItems != null)
                    {
                        if (_selectionModelAdapter != null && DataConnection?.CollectionView != null)
                        {
                            _syncingSelectionModel = true;
                            try
                            {
                                using (_selectionModelAdapter.Model.BatchUpdate())
                                {
                                    foreach (object item in e.NewItems)
                                    {
                                        int index = GetSelectionModelIndexOfItem(item);
                                        if (index >= 0)
                                        {
                                            _selectionModelAdapter.Select(index);
                                        }
                                    }
                                }

                                ApplySelectionFromSelectionModel();
                            }
                            finally
                            {
                                _syncingSelectionModel = false;
                            }
                        }
                        else
                        {
                            foreach (object item in e.NewItems)
                            {
                                _selectedItems.Add(item);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        if (_selectionModelAdapter != null && DataConnection?.CollectionView != null)
                        {
                            _syncingSelectionModel = true;
                            try
                            {
                                using (_selectionModelAdapter.Model.BatchUpdate())
                                {
                                    foreach (object item in e.OldItems)
                                    {
                                        int index = GetSelectionModelIndexOfItem(item);
                                        if (index >= 0)
                                        {
                                            _selectionModelAdapter.Deselect(index);
                                        }
                                    }
                                }

                                ApplySelectionFromSelectionModel();
                            }
                            finally
                            {
                                _syncingSelectionModel = false;
                            }
                        }
                        else
                        {
                            foreach (object item in e.OldItems)
                            {
                                _selectedItems.Remove(item);
                            }
                        }
                    }

                    if (SelectionMode == DataGridSelectionMode.Single)
                    {
                        if ((_selectedItemsBinding?.Count ?? 0) == 0)
                        {
                            SelectedItem = null;
                        }
                        NormalizeBoundSelectionForSingleMode();
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (SelectionMode == DataGridSelectionMode.Single)
                    {
                        SelectedItem = e.NewItems != null && e.NewItems.Count > 0 ? e.NewItems[0] : null;
                        NormalizeBoundSelectionForSingleMode();
                    }
                    else if (_selectionModelAdapter != null && DataConnection?.CollectionView != null)
                    {
                        _syncingSelectionModel = true;
                        try
                        {
                            using (_selectionModelAdapter.Model.BatchUpdate())
                            {
                                if (e.OldItems != null)
                                {
                                    foreach (object item in e.OldItems)
                                    {
                                        int index = GetSelectionModelIndexOfItem(item);
                                        if (index >= 0)
                                        {
                                            _selectionModelAdapter.Deselect(index);
                                        }
                                    }
                                }

                                if (e.NewItems != null)
                                {
                                    foreach (object item in e.NewItems)
                                    {
                                        int index = GetSelectionModelIndexOfItem(item);
                                        if (index >= 0)
                                        {
                                            _selectionModelAdapter.Select(index);
                                        }
                                    }
                                }
                            }

                            ApplySelectionFromSelectionModel();
                        }
                        finally
                        {
                            _syncingSelectionModel = false;
                        }
                    }
                    else
                    {
                        if (e.OldItems != null)
                        {
                            foreach (object item in e.OldItems)
                            {
                                _selectedItems.Remove(item);
                            }
                        }

                        if (e.NewItems != null)
                        {
                            foreach (object item in e.NewItems)
                            {
                                _selectedItems.Add(item);
                            }
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    // Order does not impact grid selection; no action required.
                    break;
            }
        }

        private void ApplySelectionChangeToBinding(NotifyCollectionChangedEventArgs e)
        {
            if (ReferenceEquals(_selectedItemsBinding, _selectedItems))
            {
                return;
            }

            if (_selectionModelAdapter != null)
            {
                _selectedItemsBinding.Clear();
                foreach (object item in _selectionModelAdapter.SelectedItemsView)
                {
                    _selectedItemsBinding.Add(item);
                }
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    _selectedItemsBinding.Clear();
                    foreach (object item in _selectedItems)
                    {
                        _selectedItemsBinding.Add(item);
                    }
                    break;
                case NotifyCollectionChangedAction.Add:
                    InsertItemsIntoBinding(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    RemoveItemsFromBinding(e.OldItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    RemoveItemsFromBinding(e.OldItems);
                    InsertItemsIntoBinding(e.NewItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    _selectedItemsBinding.Clear();
                    foreach (object item in _selectedItems)
                    {
                        _selectedItemsBinding.Add(item);
                    }
                    break;
            }
        }

        private void InsertItemsIntoBinding(IList items, int index)
        {
            if (items == null || _selectedItemsBinding == null)
            {
                return;
            }

            int insertIndex = index >= 0 && index <= _selectedItemsBinding.Count ? index : _selectedItemsBinding.Count;
            foreach (object item in items)
            {
                if (_selectedItemsBinding.Contains(item))
                {
                    continue;
                }

                _selectedItemsBinding.Insert(insertIndex, item);
                insertIndex++;
            }
        }

        private void RemoveItemsFromBinding(IList items)
        {
            if (items == null || _selectedItemsBinding == null)
            {
                return;
            }

            foreach (object item in items)
            {
                if (_selectedItemsBinding.Contains(item))
                {
                    _selectedItemsBinding.Remove(item);
                }
            }
        }

        private void SetSelectedCellsCollection(IList<DataGridCellInfo> value)
        {
            IList<DataGridCellInfo> newValue = value ?? (IList<DataGridCellInfo>)_selectedCellsView;
            IList<DataGridCellInfo> oldValue = SelectedCells;

            if (ReferenceEquals(oldValue, newValue))
            {
                return;
            }

            DetachBoundSelectedCells();
            _selectedCellsBinding = ReferenceEquals(newValue, _selectedCellsView) ? null : newValue;
            AttachBoundSelectedCells();

            RaisePropertyChanged(SelectedCellsProperty, oldValue, SelectedCells);

            if (_selectedCellsBinding != null)
            {
                ApplySelectedCellsFromBinding(_selectedCellsBinding);
            }
        }

        private void AttachBoundSelectedCells()
        {
            if (_selectedCellsBinding is INotifyCollectionChanged incc)
            {
                _selectedCellsBindingNotifications = incc;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedCellsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedCellsCollectionChanged);
            }
        }

        private void DetachBoundSelectedCells()
        {
            if (_selectedCellsBindingNotifications != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedCellsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedCellsCollectionChanged);
                _selectedCellsBindingNotifications = null;
            }
        }

        private void OnBoundSelectedCellsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedCells || _selectedCellsBinding == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            try
            {
                _syncingSelectedCells = true;
                ApplySelectedCellsChangeFromBinding(e);
            }
            finally
            {
                _syncingSelectedCells = false;
            }
        }

        private void OnSelectedCellsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedCells || _selectedCellsBinding == null)
            {
                return;
            }

            try
            {
                _syncingSelectedCells = true;
                ApplySelectedCellsChangeToBinding(e);
            }
            finally
            {
                _syncingSelectedCells = false;
            }
        }

        private void ApplySelectedCellsFromBinding(IList<DataGridCellInfo> boundCells)
        {
            bool previousSync = _syncingSelectedCells;
            _syncingSelectedCells = true;
            try
            {
                var removed = _selectedCellsView.ToList();
                ClearCellSelectionInternal(clearRows: true, raiseEvent: false);

                var added = new List<DataGridCellInfo>();
                foreach (var cell in boundCells)
                {
                    if (!TryNormalizeCell(cell, out var normalized))
                    {
                        continue;
                    }

                    if (AddCellSelectionInternal(normalized, added))
                    {
                        SetRowSelection(SlotFromRowIndex(normalized.RowIndex), isSelected: true, setAnchorSlot: false);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    RaiseSelectedCellsChanged(added, removed);
                }
            }
            finally
            {
                _syncingSelectedCells = previousSync;
            }
        }

        private void ApplySelectedCellsChangeFromBinding(NotifyCollectionChangedEventArgs e)
        {
            if (ReferenceEquals(_selectedCellsBinding, _selectedCellsView))
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ApplySelectedCellsFromBinding(_selectedCellsBinding);
                    break;
                default:
                    ApplySelectedCellsFromBinding(_selectedCellsBinding);
                    break;
            }
        }

        private void ApplySelectedCellsChangeToBinding(NotifyCollectionChangedEventArgs e)
        {
            if (_selectedCellsBinding == null || ReferenceEquals(_selectedCellsBinding, _selectedCellsView))
            {
                return;
            }

            void CopyToBinding()
            {
                _selectedCellsBinding.Clear();
                foreach (var cell in _selectedCellsView)
                {
                    _selectedCellsBinding.Add(cell);
                }
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    CopyToBinding();
                    break;
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    CopyToBinding();
                    break;
            }
        }

        private void SetSelectedColumnsCollection(IList<DataGridColumn> value)
        {
            IList<DataGridColumn> newValue = value ?? (IList<DataGridColumn>)_selectedColumnsView;
            IList<DataGridColumn> oldValue = SelectedColumns;

            if (ReferenceEquals(oldValue, newValue))
            {
                return;
            }

            DetachBoundSelectedColumns();
            _selectedColumnsBinding = ReferenceEquals(newValue, _selectedColumnsView) ? null : newValue;
            AttachBoundSelectedColumns();

            RaisePropertyChanged(SelectedColumnsProperty, oldValue, SelectedColumns);

            if (_selectedColumnsBinding != null)
            {
                ApplySelectedColumnsFromBinding(_selectedColumnsBinding);
            }
        }

        private void AttachBoundSelectedColumns()
        {
            if (_selectedColumnsBinding is INotifyCollectionChanged incc)
            {
                _selectedColumnsBindingNotifications = incc;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedColumnsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedColumnsCollectionChanged);
            }
        }

        private void DetachBoundSelectedColumns()
        {
            if (_selectedColumnsBindingNotifications != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGrid>(
                    _selectedColumnsBindingNotifications,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnBoundSelectedColumnsCollectionChanged);
                _selectedColumnsBindingNotifications = null;
            }
        }

        private void OnBoundSelectedColumnsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedColumns || _selectedColumnsBinding == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            try
            {
                _syncingSelectedColumns = true;
                ApplySelectedColumnsChangeFromBinding(e);
            }
            finally
            {
                _syncingSelectedColumns = false;
            }
        }

        private void OnSelectedColumnsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_syncingSelectedColumns || _selectedColumnsBinding == null)
            {
                return;
            }

            try
            {
                _syncingSelectedColumns = true;
                ApplySelectedColumnsChangeToBinding(e);
            }
            finally
            {
                _syncingSelectedColumns = false;
            }
        }

        private void ApplySelectedColumnsFromBinding(IList<DataGridColumn> boundColumns)
        {
            bool previousSync = _syncingSelectedColumns;
            _syncingSelectedColumns = true;
            try
            {
                var removedColumns = _selectedColumnsView.ToList();
                var removedCells = _selectedCellsView.ToList();

                ClearCellSelectionInternal(clearRows: true, raiseEvent: false);

                var addedCells = new List<DataGridCellInfo>();
                var rowCount = DataConnection?.Count ?? 0;
                if (rowCount <= 0)
                {
                    if (removedColumns.Count > 0 || removedCells.Count > 0)
                    {
                        RaiseSelectedCellsChanged(Array.Empty<DataGridCellInfo>(), removedCells);
                        RaiseSelectedColumnsChanged(Array.Empty<DataGridColumn>(), removedColumns);
                    }

                    return;
                }

                foreach (var column in boundColumns)
                {
                    if (column == null)
                    {
                        continue;
                    }

                    var columnIndex = column.Index;
                    if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
                    {
                        continue;
                    }

                    SelectCellRangeInternal(0, rowCount - 1, columnIndex, columnIndex, addedCells);
                }

                if (addedCells.Count > 0 || removedCells.Count > 0)
                {
                    RaiseSelectedCellsChanged(addedCells, removedCells);
                }

                var newColumns = _selectedColumnsView.ToList();
                var removedSet = new HashSet<DataGridColumn>(removedColumns);
                var newSet = new HashSet<DataGridColumn>(newColumns);

                var addedColumns = new List<DataGridColumn>();
                foreach (var column in newColumns)
                {
                    if (!removedSet.Contains(column))
                    {
                        addedColumns.Add(column);
                    }
                }

                var removedDelta = new List<DataGridColumn>();
                foreach (var column in removedColumns)
                {
                    if (!newSet.Contains(column))
                    {
                        removedDelta.Add(column);
                    }
                }

                if (addedColumns.Count > 0 || removedDelta.Count > 0)
                {
                    RaiseSelectedColumnsChanged(addedColumns, removedDelta);
                }
            }
            finally
            {
                _syncingSelectedColumns = previousSync;
            }
        }

        private void ApplySelectedColumnsChangeFromBinding(NotifyCollectionChangedEventArgs e)
        {
            if (ReferenceEquals(_selectedColumnsBinding, _selectedColumnsView))
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    ApplySelectedColumnsFromBinding(_selectedColumnsBinding);
                    break;
                default:
                    ApplySelectedColumnsFromBinding(_selectedColumnsBinding);
                    break;
            }
        }

        private void ApplySelectedColumnsChangeToBinding(NotifyCollectionChangedEventArgs e)
        {
            if (_selectedColumnsBinding == null || ReferenceEquals(_selectedColumnsBinding, _selectedColumnsView))
            {
                return;
            }

            void CopyToBinding()
            {
                _selectedColumnsBinding.Clear();
                foreach (var column in _selectedColumnsView)
                {
                    _selectedColumnsBinding.Add(column);
                }
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    CopyToBinding();
                    break;
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    CopyToBinding();
                    break;
            }
        }

        private bool TryNormalizeCell(DataGridCellInfo cell, out DataGridCellInfo normalized)
        {
            normalized = default;

            if (cell.ColumnIndex < 0 ||
                cell.RowIndex < 0 ||
                ColumnsItemsInternal == null ||
                cell.ColumnIndex >= ColumnsItemsInternal.Count)
            {
                return false;
            }

            var column = ColumnsItemsInternal[cell.ColumnIndex];
            if (column == null || !column.IsVisible)
            {
                return false;
            }

            if (DataConnection == null || cell.RowIndex >= DataConnection.Count)
            {
                return false;
            }

            var item = DataConnection.GetDataItem(cell.RowIndex);
            normalized = new DataGridCellInfo(item, column, cell.RowIndex, cell.ColumnIndex, isValid: true);
            return true;
        }

        private void ClearCellSelectionInternal(bool clearRows, bool raiseEvent = true)
        {
            if (_selectedCells.Count == 0 &&
                _selectedCellsView.Count == 0 &&
                _selectedColumnCounts.Count == 0 &&
                _selectedColumnsView.Count == 0)
            {
                return;
            }

            var removed = _selectedCellsView.ToList();

            _selectedCells.Clear();
            _selectedCellsView.Clear();
            ClearHeaderSelectionTracking();
            ClearSelectedColumnsInternal(raiseEvent);

            if (clearRows && SelectionUnit != DataGridSelectionUnit.FullRow)
            {
                foreach (var cell in removed)
                {
                    int slot = SlotFromRowIndex(cell.RowIndex);
                    if (slot >= 0)
                    {
                        SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                    }
                }
            }

            if (raiseEvent)
            {
                RaiseSelectedCellsChanged(Array.Empty<DataGridCellInfo>(), removed);
            }
        }

        private bool AddCellSelectionInternal(DataGridCellInfo cell, List<DataGridCellInfo>? addedCollector = null)
        {
            if (!_selectedCells.TryGetValue(cell.RowIndex, out var columns))
            {
                columns = new HashSet<int>();
                _selectedCells[cell.RowIndex] = columns;
            }

            if (!columns.Add(cell.ColumnIndex))
            {
                return false;
            }

            _selectedCellsView.Add(cell);
            UpdateSelectedColumnCount(cell.ColumnIndex, delta: 1);

            addedCollector?.Add(cell);
            return true;
        }

        private bool RemoveCellSelectionInternal(int rowIndex, int columnIndex, List<DataGridCellInfo>? removedCollector = null)
        {
            if (!_selectedCells.TryGetValue(rowIndex, out var columns) || !columns.Remove(columnIndex))
            {
                return false;
            }

            if (columns.Count == 0)
            {
                _selectedCells.Remove(rowIndex);
            }

            var existing = _selectedCellsView.FirstOrDefault(x => x.RowIndex == rowIndex && x.ColumnIndex == columnIndex);
            if (existing.IsValid)
            {
                _selectedCellsView.Remove(existing);
                removedCollector?.Add(existing);
            }

            UpdateSelectedColumnCount(columnIndex, delta: -1);

            if (_selectedRowHeaderIndices.Contains(rowIndex) && !IsRowFullySelectedByCells(rowIndex))
            {
                _selectedRowHeaderIndices.Remove(rowIndex);
            }

            if (_selectedColumnHeaderIndices.Contains(columnIndex) && !IsColumnFullySelectedByCells(columnIndex))
            {
                _selectedColumnHeaderIndices.Remove(columnIndex);
            }

            return true;
        }

        private void RaiseSelectedCellsChanged(IReadOnlyList<DataGridCellInfo> addedCells, IReadOnlyList<DataGridCellInfo> removedCells)
        {
            if (addedCells.Count == 0 && removedCells.Count == 0)
            {
                return;
            }

            UpdateSelectionVisuals(addedCells);
            UpdateSelectionVisuals(removedCells);
            UpdateRowSelectionVisuals(addedCells);
            UpdateRowSelectionVisuals(removedCells);
            UpdateColumnHeaderSelectionVisuals(addedCells);
            UpdateColumnHeaderSelectionVisuals(removedCells);

            RequestSelectionOverlayRefresh();
            SelectedCellsChanged?.Invoke(this, new DataGridSelectedCellsChangedEventArgs(addedCells, removedCells));
        }

        private void RaiseSelectedColumnsChanged(IReadOnlyList<DataGridColumn> addedColumns, IReadOnlyList<DataGridColumn> removedColumns)
        {
            if (addedColumns.Count == 0 && removedColumns.Count == 0)
            {
                return;
            }

            SelectedColumnsChanged?.Invoke(this, new DataGridSelectedColumnsChangedEventArgs(addedColumns, removedColumns));
        }

        private void ClearSelectedColumnsInternal(bool raiseEvent)
        {
            if (_selectedColumnCounts.Count == 0 && _selectedColumnsView.Count == 0 && _selectedColumnIndices.Count == 0)
            {
                return;
            }

            var removed = _selectedColumnsView.ToList();
            if (removed.Count == 0 && _selectedColumnIndices.Count > 0)
            {
                foreach (var index in _selectedColumnIndices)
                {
                    var column = GetColumnForIndex(index);
                    if (column != null)
                    {
                        removed.Add(column);
                    }
                }
            }
            _selectedColumnCounts.Clear();
            _selectedColumnIndices.Clear();
            _selectedColumnHeaderIndices.Clear();
            _selectedColumnsView.Clear();

            foreach (var column in removed)
            {
                column?.HeaderCell?.UpdatePseudoClasses();
            }

            if (raiseEvent)
            {
                RaiseSelectedColumnsChanged(Array.Empty<DataGridColumn>(), removed);
            }
        }

        private void UpdateSelectedColumnCount(int columnIndex, int delta)
        {
            if (DataConnection == null)
            {
                return;
            }

            var rowCount = DataConnection.Count;
            if (rowCount <= 0)
            {
                return;
            }

            var nextCount = delta;
            if (_selectedColumnCounts.TryGetValue(columnIndex, out var current))
            {
                nextCount = current + delta;
            }

            if (nextCount <= 0)
            {
                _selectedColumnCounts.Remove(columnIndex);
            }
            else
            {
                _selectedColumnCounts[columnIndex] = nextCount;
            }

            var shouldSelect = nextCount >= rowCount;
            if (shouldSelect)
            {
                MarkColumnSelected(columnIndex);
            }
            else
            {
                MarkColumnUnselected(columnIndex);
            }
        }

        private void MarkColumnSelected(int columnIndex)
        {
            if (!_selectedColumnIndices.Add(columnIndex))
            {
                return;
            }

            var column = GetColumnForIndex(columnIndex);
            if (column == null)
            {
                return;
            }

            InsertSelectedColumn(column);

            if (!_syncingSelectedColumns)
            {
                RaiseSelectedColumnsChanged(new[] { column }, Array.Empty<DataGridColumn>());
            }

            column.HeaderCell?.UpdatePseudoClasses();
        }

        private void MarkColumnUnselected(int columnIndex)
        {
            if (!_selectedColumnIndices.Remove(columnIndex))
            {
                return;
            }

            var column = GetColumnForIndex(columnIndex);
            if (column == null)
            {
                return;
            }

            _selectedColumnsView.Remove(column);

            if (!_syncingSelectedColumns)
            {
                RaiseSelectedColumnsChanged(Array.Empty<DataGridColumn>(), new[] { column });
            }

            column.HeaderCell?.UpdatePseudoClasses();
        }

        private void InsertSelectedColumn(DataGridColumn column)
        {
            var insertIndex = 0;
            while (insertIndex < _selectedColumnsView.Count &&
                   _selectedColumnsView[insertIndex].Index < column.Index)
            {
                insertIndex++;
            }

            if (insertIndex >= _selectedColumnsView.Count)
            {
                _selectedColumnsView.Add(column);
            }
            else
            {
                _selectedColumnsView.Insert(insertIndex, column);
            }
        }

        private DataGridColumn GetColumnForIndex(int columnIndex)
        {
            if (ColumnsItemsInternal == null ||
                columnIndex < 0 ||
                columnIndex >= ColumnsItemsInternal.Count)
            {
                return null;
            }

            return ColumnsItemsInternal[columnIndex];
        }

        internal bool IsColumnSelected(DataGridColumn column)
        {
            if (column == null || SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return false;
            }

            return _selectedColumnIndices.Contains(column.Index);
        }

        internal bool IsColumnCurrent(DataGridColumn column)
        {
            if (column == null || SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return false;
            }

            return ReferenceEquals(CurrentColumn, column);
        }

        internal void RefreshSelectedColumnsFromCounts()
        {
            var rowCount = DataConnection?.Count ?? 0;
            if (rowCount <= 0)
            {
                ClearSelectedColumnsInternal(raiseEvent: true);
                return;
            }

            var selectedSnapshot = _selectedColumnIndices.ToList();
            foreach (var index in selectedSnapshot)
            {
                var count = _selectedColumnCounts.TryGetValue(index, out var stored) ? stored : 0;
                if (count < rowCount)
                {
                    MarkColumnUnselected(index);
                }
            }

            foreach (var pair in _selectedColumnCounts)
            {
                if (pair.Value >= rowCount)
                {
                    MarkColumnSelected(pair.Key);
                }
            }
        }

        private void UpdateSelectionVisuals(IReadOnlyList<DataGridCellInfo> cells)
        {
            if (cells.Count == 0 || DisplayData == null)
            {
                return;
            }

            foreach (var cell in cells)
            {
                if (!cell.IsValid)
                {
                    continue;
                }

                int slot = SlotFromRowIndex(cell.RowIndex);
                if (!IsSlotVisible(slot))
                {
                    continue;
                }

                if (DisplayData.GetDisplayedElement(slot) is DataGridRow row &&
                    cell.ColumnIndex >= 0 &&
                    cell.ColumnIndex < row.Cells.Count)
                {
                    row.Cells[cell.ColumnIndex].UpdatePseudoClasses();
                }
            }
        }

        private void UpdateRowSelectionVisuals(IReadOnlyList<DataGridCellInfo> cells)
        {
            if (cells.Count == 0 || DisplayData == null)
            {
                return;
            }

            HashSet<int>? updated = null;
            foreach (var cell in cells)
            {
                if (!cell.IsValid)
                {
                    continue;
                }

                int slot = SlotFromRowIndex(cell.RowIndex);
                if (!IsSlotVisible(slot))
                {
                    continue;
                }

                if (DisplayData.GetDisplayedElement(slot) is DataGridRow row)
                {
                    updated ??= new HashSet<int>();
                    if (!updated.Add(slot))
                    {
                        continue;
                    }

                    row.UpdateSelectionPseudoClasses();
                }
            }
        }

        private void UpdateColumnHeaderSelectionVisuals(IReadOnlyList<DataGridCellInfo> cells)
        {
            if (cells.Count == 0 || ColumnsItemsInternal == null)
            {
                return;
            }

            HashSet<int>? updated = null;
            foreach (var cell in cells)
            {
                if (!cell.IsValid)
                {
                    continue;
                }

                var columnIndex = cell.ColumnIndex;
                if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
                {
                    continue;
                }

                updated ??= new HashSet<int>();
                if (!updated.Add(columnIndex))
                {
                    continue;
                }

                ColumnsItemsInternal[columnIndex]?.HeaderCell?.UpdatePseudoClasses();
            }
        }

        internal bool IsCellSelected(int rowIndex, int columnIndex)
        {
            return _selectedCells.TryGetValue(rowIndex, out var columns) && columns.Contains(columnIndex);
        }

        internal bool IsRowFullySelected(int slot)
        {
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return GetRowSelection(slot);
            }

            if (slot < 0 || ColumnsItemsInternal == null || ColumnsItemsInternal.Count == 0)
            {
                return false;
            }

            if (!GetRowSelection(slot))
            {
                return false;
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return false;
            }

            if (!_selectedCells.TryGetValue(rowIndex, out var columns) || columns.Count == 0)
            {
                return SelectionUnit == DataGridSelectionUnit.CellOrRowHeader ||
                       SelectionUnit == DataGridSelectionUnit.CellOrRowOrColumnHeader;
            }

            int visibleColumnCount = GetVisibleSelectableColumnCount();
            return visibleColumnCount > 0 && columns.Count >= visibleColumnCount;
        }

        private int GetVisibleSelectableColumnCount()
        {
            if (ColumnsItemsInternal == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ColumnsItemsInternal.Count; i++)
            {
                var column = ColumnsItemsInternal[i];
                if (column != null && column.IsVisible && column is not DataGridFillerColumn)
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearHeaderSelectionTracking()
        {
            _selectedRowHeaderIndices.Clear();
            _selectedColumnHeaderIndices.Clear();
        }

        private void SetRowHeaderSelectionRange(int startRow, int endRow, bool append)
        {
            if (!append)
            {
                _selectedRowHeaderIndices.Clear();
            }

            if (DataConnection == null || startRow > endRow)
            {
                return;
            }

            var first = Math.Max(0, startRow);
            var last = Math.Min(endRow, DataConnection.Count - 1);
            for (var rowIndex = first; rowIndex <= last; rowIndex++)
            {
                _selectedRowHeaderIndices.Add(rowIndex);
            }
        }

        private void SetColumnHeaderSelectionRange(int startColumn, int endColumn, bool append)
        {
            if (!append)
            {
                _selectedColumnHeaderIndices.Clear();
            }

            if (ColumnsInternal == null)
            {
                return;
            }

            var first = Math.Min(startColumn, endColumn);
            var last = Math.Max(startColumn, endColumn);
            for (var displayIndex = first; displayIndex <= last; displayIndex++)
            {
                var column = ColumnsInternal.GetColumnAtDisplayIndex(displayIndex);
                if (column == null || column is DataGridFillerColumn)
                {
                    continue;
                }

                _selectedColumnHeaderIndices.Add(column.Index);
            }
        }

        private bool IsRowHeaderSelectionActive(int rowIndex) => _selectedRowHeaderIndices.Contains(rowIndex);

        private bool IsColumnHeaderSelectionActive(int columnIndex) => _selectedColumnHeaderIndices.Contains(columnIndex);

        private int GetColumnDisplayIndex(int columnIndex)
        {
            if (ColumnsItemsInternal == null || columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
            {
                return -1;
            }

            var column = ColumnsItemsInternal[columnIndex];
            return column?.DisplayIndex ?? -1;
        }

        private int GetColumnIndexFromDisplayIndex(int displayIndex)
        {
            if (ColumnsInternal == null || displayIndex < 0 || displayIndex >= ColumnsInternal.DisplayIndexMap.Count)
            {
                return -1;
            }

            var column = ColumnsInternal.GetColumnAtDisplayIndex(displayIndex);
            if (column == null || column is DataGridFillerColumn)
            {
                return -1;
            }

            return column.Index;
        }

        private bool IsRowFullySelectedByCells(int rowIndex)
        {
            if (!_selectedCells.TryGetValue(rowIndex, out var columns) || columns.Count == 0)
            {
                return false;
            }

            var visibleColumnCount = GetVisibleSelectableColumnCount();
            return visibleColumnCount > 0 && columns.Count >= visibleColumnCount;
        }

        private bool IsColumnFullySelectedByCells(int columnIndex)
        {
            if (DataConnection == null)
            {
                return false;
            }

            return _selectedColumnCounts.TryGetValue(columnIndex, out var count) && count >= DataConnection.Count;
        }

        internal bool AllowsRowHeaderSelection =>
            CanUserSelectRows &&
            (SelectionUnit == DataGridSelectionUnit.FullRow ||
             SelectionUnit == DataGridSelectionUnit.CellOrRowHeader ||
             SelectionUnit == DataGridSelectionUnit.CellOrRowOrColumnHeader);

        internal bool AllowsColumnHeaderSelection =>
            CanUserSelectColumns &&
            (SelectionUnit == DataGridSelectionUnit.CellOrColumnHeader ||
             SelectionUnit == DataGridSelectionUnit.CellOrRowOrColumnHeader);

        internal bool GetCellSelectionFromSlot(int slot, int columnIndex)
        {
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return GetRowSelection(slot);
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return false;
            }

            return IsCellSelected(rowIndex, columnIndex);
        }

        public void SelectAllCells()
        {
            if (DataConnection == null || ColumnsInternal == null)
            {
                return;
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Command);
            var removed = _selectedCellsView.ToList();
            ClearCellSelectionInternal(clearRows: true, raiseEvent: false);

            var added = new List<DataGridCellInfo>();
            var visibleColumns = ColumnsInternal.GetVisibleColumns().ToList();

            for (int rowIndex = 0; rowIndex < DataConnection.Count; rowIndex++)
            {
                int slot = SlotFromRowIndex(rowIndex);
                if (slot < 0 || IsGroupSlot(slot))
                {
                    continue;
                }

                foreach (var column in visibleColumns)
                {
                    var cell = new DataGridCellInfo(
                        DataConnection.GetDataItem(rowIndex),
                        column,
                        rowIndex,
                        column.Index,
                        isValid: true);
                    AddCellSelectionInternal(cell, added);
                }

                SetRowSelection(slot, isSelected: true, setAnchorSlot: false);
            }

            if (visibleColumns.Count > 0 && DataConnection.Count > 0)
            {
                _cellAnchor = new DataGridCellCoordinates(visibleColumns[0].Index, SlotFromRowIndex(0));
            }

            RaiseSelectedCellsChanged(added, removed);
            _successfullyUpdatedSelection = true;
        }

        private void AddSingleCellSelection(int columnIndex, int slot, List<DataGridCellInfo> addedCollector)
        {
            if (DataConnection == null)
            {
                return;
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
            {
                return;
            }

            var column = ColumnsItemsInternal[columnIndex];
            if (column == null || !column.IsVisible)
            {
                return;
            }

            var item = DataConnection.GetDataItem(rowIndex);
            var cell = new DataGridCellInfo(item, column, rowIndex, columnIndex, isValid: true);

            if (AddCellSelectionInternal(cell, addedCollector))
            {
                _cellAnchor = new DataGridCellCoordinates(columnIndex, slot);
                SetRowSelection(slot, isSelected: true, setAnchorSlot: false);
            }
        }

        private void RemoveCellSelectionFromSlot(int slot, int columnIndex, List<DataGridCellInfo> removedCollector)
        {
            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return;
            }

            if (RemoveCellSelectionInternal(rowIndex, columnIndex, removedCollector))
            {
                if (!_selectedCells.TryGetValue(rowIndex, out var remaining) || remaining.Count == 0)
                {
                    SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                }
            }
        }

        private void SelectCellRangeInternal(int startRowIndex, int endRowIndex, int startColumnIndex, int endColumnIndex, List<DataGridCellInfo> addedCollector)
        {
            if (DataConnection == null)
            {
                return;
            }

            for (int rowIndex = startRowIndex; rowIndex <= endRowIndex; rowIndex++)
            {
                if (rowIndex < 0 || rowIndex >= DataConnection.Count)
                {
                    continue;
                }

                int slot = SlotFromRowIndex(rowIndex);
                if (slot < 0 || IsGroupSlot(slot))
                {
                    continue;
                }

                for (int columnIndex = startColumnIndex; columnIndex <= endColumnIndex; columnIndex++)
                {
                    if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
                    {
                        continue;
                    }

                    var column = ColumnsItemsInternal[columnIndex];
                    if (column == null || !column.IsVisible)
                    {
                        continue;
                    }

                    var item = DataConnection.GetDataItem(rowIndex);
                    var cell = new DataGridCellInfo(item, column, rowIndex, columnIndex, isValid: true);
                    AddCellSelectionInternal(cell, addedCollector);
                }

                SetRowSelection(slot, isSelected: true, setAnchorSlot: false);
            }
        }

        private void NormalizeBoundSelectionForSingleMode()
        {
            if (_selectedItemsBinding == null || ReferenceEquals(_selectedItemsBinding, _selectedItems))
            {
                return;
            }

            _selectedItemsBinding.Clear();
            if (_selectedItems.Count > 0)
            {
                _selectedItemsBinding.Add(_selectedItems[0]);
            }
        }


        /// <summary>
        /// call when: selection changes or SelectedItems object changes
        /// </summary>
        internal void CoerceSelectedItem()
        {
            if (_preferredSelectionIndex >= 0)
            {
                object preferredItem = DataConnection.GetDataItem(_preferredSelectionIndex);
                if (preferredItem != null)
                {
                    SetValueNoCallback(SelectedItemProperty, ProjectSelectionItem(preferredItem));
                    SetValueNoCallback(SelectedIndexProperty, _preferredSelectionIndex);
                    _preferredSelectionIndex = -1;
                    return;
                }
            }

            object selectedItem = null;

            if (SelectionMode == DataGridSelectionMode.Extended &&
                CurrentSlot != -1 &&
                GetRowSelection(CurrentSlot))
            {
                selectedItem = CurrentItem;
            }
            else if (_selectionModelAdapter != null && _selectionModelAdapter.Model.SelectedIndex >= 0)
            {
                selectedItem = _selectionModelAdapter.Model.SelectedItem;
            }
            else if (_selectedItems.Count > 0)
            {
                selectedItem = _selectedItems[0];
            }

            SetValueNoCallback(SelectedItemProperty, ProjectSelectionItem(selectedItem));

            // Update the SelectedIndex
            int newIndex = -1;

            if (selectedItem != null)
            {
                newIndex = GetSelectionModelIndexOfItem(selectedItem);
            }

            SetValueNoCallback(SelectedIndexProperty, newIndex);
        }


        internal IEnumerable<object> GetSelectionInclusive(int startRowIndex, int endRowIndex)
        {
            int endSlot = SlotFromRowIndex(endRowIndex);
            foreach (int slot in _selectedItems.GetSlots(SlotFromRowIndex(startRowIndex)))
            {
                if (slot > endSlot)
                {
                    break;
                }
                yield return DataConnection.GetDataItem(RowIndexFromSlot(slot));
            }
        }


        /// <summary>
        /// Raises the SelectionChanged event and clears the _selectionChanged.
        /// This event won't get raised again until after _selectionChanged is set back to true.
        /// </summary>
        protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            using var activity = DataGridDiagnostics.SelectionChanged();
            using var _ = DataGridDiagnostics.BeginSelectionChanged();

            var dataGridArgs = e as DataGridSelectionChangedEventArgs;
            if (activity != null)
            {
                activity.SetTag(DataGridDiagnostics.Tags.AddedCount, e.AddedItems?.Count ?? 0);
                activity.SetTag(DataGridDiagnostics.Tags.RemovedCount, e.RemovedItems?.Count ?? 0);
                if (dataGridArgs != null)
                {
                    activity.SetTag(DataGridDiagnostics.Tags.SelectionSource, dataGridArgs.Source.ToString());
                    activity.SetTag(DataGridDiagnostics.Tags.UserInitiated, dataGridArgs.IsUserInitiated);
                }
            }

            DataGridDiagnostics.RecordSelectionChanged(dataGridArgs?.Source ?? DataGridSelectionChangeSource.Unknown);
            RaiseEvent(e);
        }

        private int _noSelectionChangeCount;

        private bool _successfullyUpdatedSelection;


        /// <summary>
        /// Occurs when the <see cref="P:Avalonia.Controls.DataGrid.SelectedItem" /> or
        /// <see cref="P:Avalonia.Controls.DataGrid.SelectedItems" /> property value changes.
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }


        private int NoSelectionChangeCount
        {
            get
            {
                return _noSelectionChangeCount;
            }
            set
            {
                _noSelectionChangeCount = value;
                if (value == 0)
                {
                    FlushSelectionChanged();
                }
            }
        }


        // This flag indicates whether selection has actually changed during a selection operation,
        // and exists to ensure that FlushSelectionChanged doesn't unnecessarily raise SelectionChanged.
        internal bool SelectionHasChanged
        {
            get;
            set;
        }


        internal int AnchorSlot
        {
            get;
            private set;
        }


        private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
                int index = (int)e.NewValue;

                // GetDataItem returns null if index is >= Count, we do not check newValue
                // against Count here to avoid enumerating through an Enumerable twice
                // Setting SelectedItem coerces the finally value of the SelectedIndex
                object newSelectedItem = (index < 0) ? null : DataConnection.GetDataItem(index);
                var projectedItem = ProjectSelectionItem(newSelectedItem);
                SelectedItem = projectedItem;
                if (!Equals(SelectedItem, projectedItem))
                {
                    SetValueNoCallback(SelectedIndexProperty, (int)e.OldValue);
                }
            }
        }

        private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
                var normalizedItem = ProjectSelectionItem(e.NewValue);
                var normalizedOld = ProjectSelectionItem(e.OldValue);
                if (!Equals(normalizedItem, e.NewValue))
                {
                    SetValueNoCallback(SelectedItemProperty, normalizedItem);
                }

                int selectionIndex = (normalizedItem == null) ? -1 : GetSelectionModelIndexOfItem(normalizedItem);
                if (selectionIndex == -1 && normalizedItem != null)
                {
                    if (TryAutoExpandSelectionItem(normalizedItem))
                    {
                        selectionIndex = GetSelectionModelIndexOfItem(normalizedItem);
                    }
                }
                if (selectionIndex == -1)
                {
                    // If the Item is null or it's not found, clear the Selection
                    if (!CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
                    {
                        // Edited value couldn't be committed or aborted
                        SetValueNoCallback(SelectedItemProperty, normalizedOld);
                        return;
                    }

                    // Clear all row selections
                    ClearRowSelection(resetAnchorSlot: true);

                    if (DataConnection.CollectionView != null)
                    {
                        DataConnection.CollectionView.MoveCurrentTo(null);
                    }
                }
                else
                {
                    int slot = SlotFromSelectionIndex(selectionIndex);
                    if (slot == -1)
                    {
                        SetValueNoCallback(SelectedIndexProperty, selectionIndex);
                        _preferredSelectionIndex = selectionIndex;
                        return;
                    }
                    if (slot != CurrentSlot)
                    {
                        if (!CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
                        {
                            // Edited value couldn't be committed or aborted
                            SetValueNoCallback(SelectedItemProperty, normalizedOld);
                            return;
                        }
                        if (slot >= SlotCount || slot < -1)
                        {
                            if (DataConnection.CollectionView != null)
                            {
                                int moveIndex = RowIndexFromSlot(slot);
                                DataConnection.CollectionView.MoveCurrentToPosition(moveIndex);
                            }
                        }
                    }

                    int oldSelectedIndex = SelectedIndex;
                    SetValueNoCallback(SelectedIndexProperty, selectionIndex);
                    try
                    {
                        _noSelectionChangeCount++;
                        int columnIndex = CurrentColumnIndex;

                        if (columnIndex == -1)
                        {
                            columnIndex = FirstDisplayedNonFillerColumnIndex;
                        }
                        if (IsSlotOutOfSelectionBounds(slot))
                        {
                            ClearRowSelection(slotException: slot, setAnchorSlot: true);
                            return;
                        }

                        UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
                    }
                    finally
                    {
                        NoSelectionChangeCount--;
                    }

                    if (!_successfullyUpdatedSelection)
                    {
                        SetValueNoCallback(SelectedIndexProperty, oldSelectedIndex);
                        SetValueNoCallback(SelectedItemProperty, normalizedOld);
                    }
                    else
                    {
                        RequestAutoScrollToSelection();
                    }
                }
            }
        }

        private bool TryAutoExpandSelectionItem(object item)
        {
            if (!AutoExpandSelectedItem || !_hierarchicalRowsEnabled || _hierarchicalModel == null)
            {
                return false;
            }

            if (_autoExpandingSelection)
            {
                return false;
            }

            if (IsHierarchicalItemVisible(item))
            {
                return false;
            }

            if (_hierarchicalModel is Avalonia.Controls.DataGridHierarchical.IHierarchicalModelExpander expander)
            {
                _autoExpandingSelection = true;
                try
                {
                    return expander.TryExpandToItem(item, out _);
                }
                finally
                {
                    _autoExpandingSelection = false;
                }
            }

            return false;
        }

        private bool IsHierarchicalItemVisible(object item)
        {
            if (_hierarchicalModel == null)
            {
                return false;
            }

            foreach (var node in _hierarchicalModel.Flattened)
            {
                if (ReferenceEquals(node, item) || ReferenceEquals(node.Item, item))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnSelectionModeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
                ClearRowSelection(resetAnchorSlot: true);
                if (_selectionModelAdapter != null)
                {
                    _selectionModelAdapter.Model.SingleSelect = SelectionMode == DataGridSelectionMode.Single;
                    SyncSelectionModelFromGridSelection();
                }
            }
        }

        private void OnSelectionUnitChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            var newValue = (DataGridSelectionUnit)e.NewValue;
            if (newValue == DataGridSelectionUnit.FullRow)
            {
                ClearCellSelectionInternal(clearRows: false);
            }
            else
            {
                ClearRowSelection(resetAnchorSlot: true);
                _cellAnchor = new DataGridCellCoordinates(-1, -1);
            }

            RefreshVisibleSelection();
            RequestSelectionOverlayRefresh();
        }

        private void OnAutoScrollToSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            if (AutoScrollToSelectedItem)
            {
                RequestAutoScrollToSelection();
            }
            else
            {
                _autoScrollPending = false;
            }
        }

        private void OnAutoExpandSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            if (!AutoExpandSelectedItem)
            {
                return;
            }

            var current = ProjectSelectionItem(SelectedItem);
            if (current != null)
            {
                TryAutoExpandSelectionItem(current);
            }
        }

        private void RequestAutoScrollToSelection()
        {
            if (_autoScrollPending || !AutoScrollToSelectedItem)
            {
                return;
            }

            _autoScrollPending = true;

            if (!IsAttachedToVisualTree || _rowsPresenter == null)
            {
                return;
            }

            ScheduleAutoScrollToSelection();
        }

        private void TryExecutePendingAutoScroll()
        {
            if (!_autoScrollPending || !AutoScrollToSelectedItem)
            {
                return;
            }

            if (!IsAttachedToVisualTree || _rowsPresenter == null)
            {
                return;
            }

            ScheduleAutoScrollToSelection();
        }

        private void ScheduleAutoScrollToSelection()
        {
            var token = ++_autoScrollRequestToken;
            Dispatcher.UIThread.Post(_ => PerformAutoScrollToSelection(token), DispatcherPriority.Background);
        }

        private void PerformAutoScrollToSelection(int token)
        {
            if (token != _autoScrollRequestToken)
            {
                return;
            }

            _autoScrollPending = false;

            if (!AutoScrollToSelectedItem || !IsAttachedToVisualTree || _rowsPresenter == null)
            {
                return;
            }

            if (!TryGetAutoScrollTarget(out var item, out var column))
            {
                return;
            }

            ScrollIntoView(item, column);
            ComputeScrollBarsLayout();

            if (UseLogicalScrollable && _rowsPresenter != null)
            {
                _rowsPresenter.SyncOffset(HorizontalOffset, GetVerticalOffset());
                _rowsPresenter.RaiseScrollInvalidated(EventArgs.Empty);
            }
        }

        private bool TryGetAutoScrollTarget(out object item, out DataGridColumn column)
        {
            item = null;
            column = null;

            if (DisplayData == null || ColumnsInternal == null)
            {
                return false;
            }

            if (CurrentSlot != -1 && GetRowSelection(CurrentSlot))
            {
                item = CurrentItem;
            }
            else
            {
                item = SelectedItem;
            }

            if (item == null || DataConnection == null || !TryGetRowIndexFromItem(item, out _))
            {
                return false;
            }

            column = CurrentColumn;

            if (column == null || !column.IsVisible)
            {
                column = ColumnsInternal.FirstVisibleNonFillerColumn;
            }

            return true;
        }

        private void CancelPendingAutoScroll()
        {
            if (_autoScrollPending)
            {
                _autoScrollPending = false;
            }

            _autoScrollRequestToken++;
        }

    }
}
