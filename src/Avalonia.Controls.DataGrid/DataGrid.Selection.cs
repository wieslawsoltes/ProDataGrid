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

                if (CurrentSlot != slot || (CurrentColumnIndex != columnIndex && columnIndex != -1))
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
                                endRowEdit: SlotFromRowIndex(SelectedIndex) != slot)
                            || (scrollIntoView &&
                                !ScrollSlotIntoView(
                                    columnIndex, slot,
                                    forCurrentCellChange: true,
                                    forceHorizontalScroll: false)))
                        {
                            return;
                        }
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

        private void UpdateSelectionSnapshot()
        {
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
                    SetValueNoCallback(SelectedItemProperty, preferredItem);
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

            SetValueNoCallback(SelectedItemProperty, selectedItem);

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
                SelectedItem = newSelectedItem;
                if (SelectedItem != newSelectedItem)
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
                int selectionIndex = (e.NewValue == null) ? -1 : GetSelectionModelIndexOfItem(e.NewValue);
                if (selectionIndex == -1)
                {
                    // If the Item is null or it's not found, clear the Selection
                    if (!CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
                    {
                        // Edited value couldn't be committed or aborted
                        SetValueNoCallback(SelectedItemProperty, e.OldValue);
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
                            SetValueNoCallback(SelectedItemProperty, e.OldValue);
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
                        SetValueNoCallback(SelectedItemProperty, e.OldValue);
                    }
                    else
                    {
                        RequestAutoScrollToSelection();
                    }
                }
            }
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

            if (item == null || DataConnection == null || DataConnection.IndexOf(item) == -1)
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
