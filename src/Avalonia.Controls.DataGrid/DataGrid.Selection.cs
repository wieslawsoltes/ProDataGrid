// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Utils;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
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
                if (slot < 0 || slot > SlotCount)
                {
                    return;
                }

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
                        if (CurrentColumnIndex != -1)
                        {
                            columnIndex = CurrentColumnIndex;
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
                        scrollColumnIndex = CurrentColumnIndex;
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
            }
        }

        private void ApplySelectionFromSelectionModel()
        {
            var previousSync = _syncingSelectionModel;
            _syncingSelectionModel = true;
            try
            {
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
            if (_syncingSelectionModel || _selectionModelAdapter == null)
            {
                return;
            }

            var snapshot = _selectionModelSnapshot
                ?? _selectionModelAdapter.SelectedItemsView.Cast<object>().ToList();
            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            try
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.SelectionModelSync);
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

                _preferredSelectionIndex = _selectionModelAdapter.Model.SelectedIndex;
                ApplySelectionFromSelectionModel();
                UpdateSelectionSnapshot();
            }
            finally
            {
                _syncingSelectionModel = false;
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
                _selectedItemsBindingNotifications.CollectionChanged += OnBoundSelectedItemsCollectionChanged;
            }
        }

        private void DetachBoundSelectedItems()
        {
            if (_selectedItemsBindingNotifications != null)
            {
                _selectedItemsBindingNotifications.CollectionChanged -= OnBoundSelectedItemsCollectionChanged;
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
                _selectedCellsBindingNotifications.CollectionChanged += OnBoundSelectedCellsCollectionChanged;
            }
        }

        private void DetachBoundSelectedCells()
        {
            if (_selectedCellsBindingNotifications != null)
            {
                _selectedCellsBindingNotifications.CollectionChanged -= OnBoundSelectedCellsCollectionChanged;
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
            if (_selectedCells.Count == 0 && _selectedCellsView.Count == 0)
            {
                return;
            }

            var removed = _selectedCellsView.ToList();

            _selectedCells.Clear();
            _selectedCellsView.Clear();

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

            return true;
        }

        private void RaiseSelectedCellsChanged(IReadOnlyList<DataGridCellInfo> addedCells, IReadOnlyList<DataGridCellInfo> removedCells)
        {
            if ((addedCells.Count == 0 && removedCells.Count == 0) || SelectedCellsChanged == null)
            {
                return;
            }

            SelectedCellsChanged?.Invoke(this, new DataGridSelectedCellsChangedEventArgs(addedCells, removedCells));
        }

        internal bool IsCellSelected(int rowIndex, int columnIndex)
        {
            return _selectedCells.TryGetValue(rowIndex, out var columns) && columns.Contains(columnIndex);
        }

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
