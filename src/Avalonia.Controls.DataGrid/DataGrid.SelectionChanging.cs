// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Utils;
using Avalonia.Input;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    partial class DataGrid
    {
        private int _selectionCommitDepth;
        private bool _raisingSelectionChanging;
        private int _itemsSourceMutationDeferralDepth;
        private object _itemsSourceMutationOldSelectedItem;
        private int _itemsSourceMutationOldSelectedIndex;
        private DataGridCellInfo _itemsSourceMutationOldCurrentCell;
        private bool _selectionModelSourceResetPreflightPending;
        private bool _builtInSourceMutationPreflightActive;
        private bool _builtInSourceMutationScopesActive;
        private SelectionCommitScope _builtInSourceSelectionCommitScope;
        private ItemsSourceMutationDeferralScope _builtInSourceMutationDeferralScope;
        private ItemsSourceSelectionTransaction _pendingItemsSourceSelectionTransaction;

        /// <summary>
        /// Occurs after a complete selection proposal has been computed and before any grid
        /// selection, current-cell, anchor, currency, focus, or scrolling state is committed.
        /// Set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to reject the proposal.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridSelectionChangingEventArgs> SelectionChanging;

        private bool HasSelectionChangingHandlers => SelectionChanging != null;

        private void SetSelectedItemWithPreview(object value)
        {
            if (_itemsSourceMutationDeferralDepth > 0)
            {
                _selectedItem = value;
                return;
            }

            if (_areHandlersSuspended || !HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
                return;
            }

            using var origin = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            object normalizedItem = ProjectSelectionItem(value);
            int selectionIndex = normalizedItem == null ? -1 : GetSelectionModelIndexOfItem(normalizedItem);
            int slot = selectionIndex < 0 ? -1 : SlotFromSelectionIndex(selectionIndex);
            int columnIndex = CurrentColumnIndex >= 0
                ? CurrentColumnIndex
                : FirstDisplayedNonFillerColumnIndex;
            bool accepted;
            HierarchicalNode node = normalizedItem == null
                ? null
                : FindMaterializedHierarchyNode(normalizedItem);
            if (node != null && !IsHierarchicalItemVisible(normalizedItem))
            {
                CreateSelectionItemContext(
                    node,
                    out object item,
                    out HierarchicalNode resolvedNode,
                    out IReadOnlyList<HierarchicalNode> path);
                var unrealizedRow = new DataGridSelectionRowInfo(node, item, -1, resolvedNode, path);
                var proposedAnchor = new DataGridSelectionAnchorInfo(
                    node,
                    item,
                    -1,
                    columnIndex,
                    resolvedNode,
                    path,
                    isValid: false);
                accepted = TryRaiseSelectionChanging(
                    CaptureSelectedRowIndexes(),
                    new HashSet<int>(),
                    Array.Empty<DataGridCellInfo>(),
                    Array.Empty<DataGridCellInfo>(),
                    Array.Empty<DataGridColumn>(),
                    Array.Empty<DataGridColumn>(),
                    DataGridCellInfo.Unset,
                    proposedAnchor,
                    unrealizedRow,
                    item);
            }
            else
            {
                accepted = TryPreviewRowSelection(columnIndex, slot, DataGridSelectionAction.SelectCurrent);
            }

            if (!accepted)
            {
                return;
            }

            using var commit = BeginSelectionCommit();
            SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
        }

        private HierarchicalNode FindMaterializedHierarchyNode(object item)
        {
            if (_hierarchicalModel == null || item == null)
            {
                return null;
            }

            HierarchicalNode direct = _hierarchicalModel.FindNode(item);
            if (direct != null)
            {
                return direct;
            }

            HierarchicalNode root = _hierarchicalModel.Root;
            if (root == null)
            {
                return null;
            }

            var pending = new Stack<HierarchicalNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                HierarchicalNode current = pending.Pop();
                if (ReferenceEquals(current, item) || ReferenceEquals(current.Item, item))
                {
                    return current;
                }

                IReadOnlyList<HierarchicalNode> children = current.Children;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    pending.Push(children[i]);
                }
            }

            return null;
        }

        private void SetSelectedIndexWithPreview(int value)
        {
            if (_itemsSourceMutationDeferralDepth > 0)
            {
                _selectedIndex = value;
                return;
            }

            if (_areHandlersSuspended || !HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                SetAndRaise(SelectedIndexProperty, ref _selectedIndex, value);
                return;
            }

            using var origin = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            int slot = value < 0 ? -1 : SlotFromSelectionIndex(value);
            int columnIndex = CurrentColumnIndex >= 0
                ? CurrentColumnIndex
                : FirstDisplayedNonFillerColumnIndex;
            if (!TryPreviewRowSelection(columnIndex, slot, DataGridSelectionAction.SelectCurrent))
            {
                return;
            }

            using var commit = BeginSelectionCommit();
            SetAndRaise(SelectedIndexProperty, ref _selectedIndex, value);
        }

        internal SelectionCommitScope BeginSelectionCommit()
        {
            _selectionCommitDepth++;
            return new SelectionCommitScope(this);
        }

        internal ItemsSourceMutationDeferralScope BeginItemsSourceMutationDeferral()
        {
            if (_itemsSourceMutationDeferralDepth++ == 0)
            {
                _itemsSourceMutationOldSelectedItem = _selectedItem;
                _itemsSourceMutationOldSelectedIndex = _selectedIndex;
                _itemsSourceMutationOldCurrentCell = _currentCell;
                _noSelectionChangeCount++;
                _noCurrentCellChangeCount++;
            }

            return new ItemsSourceMutationDeferralScope(this);
        }

        internal bool ShouldDeferPointerFocusForSelectionChanging =>
            HasSelectionChangingHandlers && _selectionCommitDepth == 0;

        internal bool TryPreviewPointerPressedSelection(
            PointerPressedEventArgs pointerPressedEventArgs,
            int columnIndex,
            int slot,
            out bool previewed)
        {
            previewed = false;
            if (!HasSelectionChangingHandlers ||
                _selectionCommitDepth > 0 ||
                pointerPressedEventArgs == null ||
                slot < 0 ||
                IsSlotOutOfBounds(slot))
            {
                return true;
            }

            PointerPoint point = pointerPressedEventArgs.GetCurrentPoint(this);
            bool isTouchLike = pointerPressedEventArgs.Pointer.Type is PointerType.Touch or PointerType.Pen;
            bool isPrimaryPressed = point.Properties.IsLeftButtonPressed ||
                                    (isTouchLike && AllowTouchDragSelection);
            bool isRightPressed = point.Properties.IsRightButtonPressed;
            if (!isPrimaryPressed && !isRightPressed)
            {
                return true;
            }

            KeyboardHelper.GetMetaKeyState(
                this,
                pointerPressedEventArgs.KeyModifiers,
                out bool ctrl,
                out bool shift);

            using var origin = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.Pointer,
                pointerPressedEventArgs);

            if (isPrimaryPressed)
            {
                previewed = true;
                if (SelectionUnit != DataGridSelectionUnit.FullRow && columnIndex >= 0)
                {
                    List<DataGridCellInfo> proposed = BuildPointerCellSelectionProposal(
                        pointerPressedEventArgs,
                        columnIndex,
                        slot,
                        shift,
                        ctrl,
                        out DataGridSelectionAnchorInfo proposedAnchor);
                    return TryPreviewCellSelection(
                        proposed,
                        CreateCellInfo(columnIndex, slot),
                        proposedAnchor);
                }

                return TryPreviewRowSelection(
                    columnIndex,
                    slot,
                    GetPointerRowSelectionAction(slot, shift, ctrl));
            }

            if (shift || ctrl)
            {
                return true;
            }

            if (SelectionUnit != DataGridSelectionUnit.FullRow && columnIndex >= 0)
            {
                if (CurrentSlot == slot && CurrentColumnIndex == columnIndex)
                {
                    return true;
                }

                previewed = true;
                return TryPreviewCurrentCell(CreateCellInfo(columnIndex, slot));
            }

            if (GetRowSelection(slot))
            {
                return true;
            }

            previewed = true;
            return TryPreviewRowSelection(
                columnIndex,
                slot,
                DataGridSelectionAction.SelectCurrent);
        }

        internal bool TryPreviewSetRowSelection(int slot, bool isSelected, bool setAnchorSlot)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();

            var currentRows = CaptureSelectedRowIndexes();
            var proposedRows = new HashSet<int>(currentRows);
            int rowIndex = slot >= 0 && slot < SlotCount && !IsGroupSlot(slot)
                ? RowIndexFromSlot(slot)
                : -1;
            if (rowIndex >= 0)
            {
                if (isSelected)
                {
                    if (SelectionMode == DataGridSelectionMode.Single)
                    {
                        proposedRows.Clear();
                    }
                    proposedRows.Add(rowIndex);
                }
                else
                {
                    proposedRows.Remove(rowIndex);
                }
            }

            return TryRaiseSelectionChanging(
                currentRows,
                proposedRows,
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                CurrentCell,
                setAnchorSlot ? CreateAnchorInfo(slot, CurrentColumnIndex) : GetCurrentSelectionAnchorInfo());
        }

        internal bool TryPreviewClearRowSelection(bool resetAnchorSlot)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            return TryRaiseSelectionChanging(
                CaptureSelectedRowIndexes(),
                new HashSet<int>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                CurrentCell,
                resetAnchorSlot ? DataGridSelectionAnchorInfo.Unset : GetCurrentSelectionAnchorInfo());
        }

        private bool TryPreviewClearSelectionAndCurrent()
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            return TryRaiseSelectionChanging(
                CaptureSelectedRowIndexes(),
                new HashSet<int>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                DataGridCellInfo.Unset,
                DataGridSelectionAnchorInfo.Unset);
        }

        internal ItemsSourceSelectionTransaction BeginItemsSourceSelectionTransaction(
            IReadOnlyList<object> selectionSnapshot,
            IEnumerable prospectiveSource,
            bool preserveSelection,
            bool requireSelectionChangingHandler = true)
        {
            if ((requireSelectionChangingHandler && !HasSelectionChangingHandlers) ||
                _selectionCommitDepth > 0)
            {
                return null;
            }

            ThrowIfSelectionChangingReentrant();

            DataGridSelectionAnchorInfo oldAnchor = GetCurrentSelectionAnchorInfo();
            object oldAnchorItem = CaptureSourceMutationAnchorItem(selectionSnapshot, oldAnchor);
            int oldAnchorColumnIndex = oldAnchor.IsValid
                ? oldAnchor.ColumnIndex
                : CurrentColumnIndex;
            DataGridColumn oldAnchorColumn = oldAnchorColumnIndex >= 0 &&
                oldAnchorColumnIndex < ColumnsItemsInternal.Count
                    ? ColumnsItemsInternal[oldAnchorColumnIndex]
                    : CurrentCell.Column;
            List<ItemsSourceSelectionCell> selectedCells = CaptureItemsSourceSelectionCells();
            object oldCurrentItem = ProjectSelectionItem(CurrentCell.Item);
            object oldCurrencyItem = ProjectSelectionItem(DataConnection?.CollectionView?.CurrentItem);

            int prospectiveCapacity = prospectiveSource is ICollection collection
                ? collection.Count
                : 0;
            var prospectiveRows = prospectiveCapacity > 0
                ? new List<ProspectiveSelectionRow>(prospectiveCapacity)
                : new List<ProspectiveSelectionRow>();
            if (prospectiveSource != null)
            {
                foreach (object rowDataContext in prospectiveSource)
                {
                    CreateSelectionItemContext(
                        rowDataContext,
                        out object item,
                        out HierarchicalNode node,
                        out IReadOnlyList<HierarchicalNode> path);
                    prospectiveRows.Add(new ProspectiveSelectionRow(rowDataContext, item, node, path));
                }
            }

            var selectedItems = selectionSnapshot ?? Array.Empty<object>();
            var projectedSelectedItems = new List<object>(selectedItems.Count);
            var requestedIdentities = new List<object>(
                selectedItems.Count + selectedCells.Count + 3);
            for (int i = 0; i < selectedItems.Count; i++)
            {
                object selectedItem = ProjectSelectionItem(selectedItems[i]);
                projectedSelectedItems.Add(selectedItem);
                AddReferenceIdentity(requestedIdentities, selectedItem);
            }
            AddReferenceIdentity(requestedIdentities, oldCurrentItem);
            AddReferenceIdentity(requestedIdentities, oldAnchorItem);
            AddReferenceIdentity(requestedIdentities, oldCurrencyItem);
            for (int i = 0; i < selectedCells.Count; i++)
            {
                AddReferenceIdentity(requestedIdentities, selectedCells[i].Item);
            }

            var identityMap = new ProspectiveIdentityMap(prospectiveRows, requestedIdentities);
            var oldSelectedRowIndexes = new List<int>();
            foreach (int selectedSlot in _selectedItems.GetIndexes())
            {
                if (selectedSlot >= 0 && selectedSlot < SlotCount && !IsGroupSlot(selectedSlot))
                {
                    oldSelectedRowIndexes.Add(RowIndexFromSlot(selectedSlot));
                }
            }
            oldSelectedRowIndexes.Sort();
            var survivors = new List<ItemsSourceSelectionRow>(selectedItems.Count);
            var removedItems = new List<object>();
            var removedRows = new List<DataGridSelectionRowInfo>();
            for (int i = 0; i < selectedItems.Count; i++)
            {
                object selectedItem = projectedSelectedItems[i];
                int prospectiveIndex = identityMap.GetIndex(selectedItem);
                if (prospectiveIndex >= 0)
                {
                    survivors.Add(new ItemsSourceSelectionRow(selectedItem, prospectiveIndex));
                }

                if (!preserveSelection || prospectiveIndex < 0)
                {
                    removedItems.Add(selectedItem);
                    int oldRowIndex = i < oldSelectedRowIndexes.Count
                        ? oldSelectedRowIndexes[i]
                        : -1;
                    removedRows.Add(CreateSourceMutationRowInfo(selectedItem, oldRowIndex));
                }
            }

            var removedCells = new List<DataGridCellInfo>();
            var survivingSelectedCells = new List<ItemsSourceSelectionCell>(selectedCells.Count);
            bool selectedCellsChanged = false;
            for (int i = 0; i < selectedCells.Count; i++)
            {
                ItemsSourceSelectionCell selectedCell = selectedCells[i];
                int prospectiveIndex = identityMap.GetIndex(selectedCell.Item);
                if (prospectiveIndex >= 0)
                {
                    selectedCellsChanged |= prospectiveIndex != selectedCell.RowIndex;
                    survivingSelectedCells.Add(new ItemsSourceSelectionCell(
                        selectedCell.Item,
                        selectedCell.Column,
                        selectedCell.ColumnIndex,
                        prospectiveIndex));
                }
            }
            if (SelectedCells != null)
            {
                foreach (DataGridCellInfo selectedCell in SelectedCells)
                {
                    object selectedCellItem = ProjectSelectionItem(selectedCell.Item);
                    if (!preserveSelection ||
                        identityMap.GetIndex(selectedCellItem) < 0)
                    {
                        removedCells.Add(selectedCell);
                    }
                }
            }

            object proposedCurrentItem = null;
            int proposedCurrentIndex = -1;
            if (preserveSelection && oldCurrentItem != null)
            {
                proposedCurrentIndex = identityMap.GetIndex(oldCurrentItem);
                if (proposedCurrentIndex >= 0)
                {
                    proposedCurrentItem = oldCurrentItem;
                }
            }

            if (preserveSelection && proposedCurrentIndex < 0 && survivors.Count > 0)
            {
                proposedCurrentItem = survivors[0].Item;
                proposedCurrentIndex = survivors[0].RowIndex;
            }

            int proposedColumnIndex = CurrentColumnIndex >= 0
                ? CurrentColumnIndex
                : FirstDisplayedNonFillerColumnIndex;
            DataGridColumn proposedColumn = CurrentCell.Column;
            if (proposedColumn == null && proposedColumnIndex >= 0 &&
                proposedColumnIndex < ColumnsItemsInternal.Count)
            {
                proposedColumn = ColumnsItemsInternal[proposedColumnIndex];
            }
            DataGridCellInfo proposedCurrentCell = CreateProspectiveCellInfo(
                prospectiveRows,
                proposedCurrentIndex,
                proposedColumnIndex);
            int proposedAnchorIndex = preserveSelection
                ? identityMap.GetIndex(oldAnchorItem)
                : -1;
            if (preserveSelection && proposedAnchorIndex < 0)
            {
                proposedAnchorIndex = proposedCurrentIndex;
            }
            DataGridSelectionAnchorInfo proposedAnchor = CreateProspectiveAnchorInfo(
                prospectiveRows,
                proposedAnchorIndex,
                oldAnchor.IsValid ? oldAnchor.ColumnIndex : proposedColumnIndex);

            bool currentChanged = CurrentCell.IsValid != proposedCurrentCell.IsValid ||
                CurrentCell.RowIndex != proposedCurrentCell.RowIndex ||
                CurrentCell.ColumnIndex != proposedCurrentCell.ColumnIndex ||
                !ReferenceEquals(ProjectSelectionItem(CurrentCell.Item), proposedCurrentItem);
            bool anchorChanged = oldAnchor.IsValid != proposedAnchor.IsValid ||
                oldAnchor.RowIndex != proposedAnchor.RowIndex ||
                oldAnchor.ColumnIndex != proposedAnchor.ColumnIndex ||
                !ReferenceEquals(oldAnchorItem, proposedAnchor.Item);
            if (removedItems.Count == 0 && removedCells.Count == 0 &&
                !selectedCellsChanged &&
                !currentChanged && !anchorChanged)
            {
                return null;
            }

            HierarchicalNode hierarchyNode = null;
            IReadOnlyList<HierarchicalNode> hierarchyPath = Array.Empty<HierarchicalNode>();
            if (proposedCurrentIndex >= 0)
            {
                hierarchyNode = prospectiveRows[proposedCurrentIndex].Node;
                hierarchyPath = prospectiveRows[proposedCurrentIndex].HierarchyPath;
            }

            var args = new DataGridSelectionChangingEventArgs(
                Array.Empty<object>(),
                removedItems,
                Array.Empty<DataGridSelectionRowInfo>(),
                removedRows,
                Array.Empty<DataGridCellInfo>(),
                removedCells,
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                proposedCurrentItem,
                proposedCurrentCell,
                proposedAnchor,
                hierarchyNode,
                hierarchyPath,
                CurrentSelectionChangeSource,
                CurrentSelectionTriggerEvent,
                CurrentSelectionChangingGuarantee);

            _raisingSelectionChanging = true;
            try
            {
                SelectionChanging?.Invoke(this, args);
            }
            finally
            {
                _raisingSelectionChanging = false;
            }

            return new ItemsSourceSelectionTransaction(
                accepted: !args.Cancel,
                survivors,
                identityMap.GetIndex(oldCurrentItem),
                identityMap.GetIndex(oldAnchorItem),
                oldAnchorColumnIndex,
                oldAnchorColumn,
                proposedCurrentIndex,
                proposedAnchorIndex,
                proposedColumnIndex,
                proposedColumn,
                GetVerticalOffset(),
                HorizontalOffset,
                identityMap.GetIndex(oldCurrencyItem),
                survivingSelectedCells,
                preserveSelection);
        }

        internal void PrepareSelectionModelSourceResetPreflight(
            IReadOnlyList<object> selectionSnapshot)
        {
            if (_selectionModelSourceResetPreflightPending ||
                _builtInSourceMutationPreflightActive ||
                _selectionCommitDepth > 0 ||
                DataConnection?.UsesHierarchicalItemsSource == true)
            {
                return;
            }

            PrepareItemsSourceMutationPreflight(
                selectionSnapshot,
                DataConnection?.CollectionView);
        }

        internal void PrepareBuiltInCollectionViewMutationPreflight(
            IReadOnlyList<object> selectionSnapshot,
            IEnumerable prospectiveSource)
        {
            if (_builtInSourceMutationPreflightActive ||
                _selectionCommitDepth > 0 ||
                DataConnection?.UsesHierarchicalItemsSource == true)
            {
                return;
            }

            _builtInSourceMutationPreflightActive = true;
            try
            {
                PrepareItemsSourceMutationPreflight(
                    selectionSnapshot,
                    prospectiveSource);
                if (_pendingItemsSourceSelectionTransaction != null)
                {
                    // Keep grid-owned selection/current state silent until the built-in view has
                    // notified every listener, including the caller-owned SelectionModel. The
                    // final identity reconciliation therefore observes the model's final indexes
                    // and publishes only the old-to-final grid transition.
                    _builtInSourceSelectionCommitScope = BeginSelectionCommit();
                    _builtInSourceMutationDeferralScope = BeginItemsSourceMutationDeferral();
                    _builtInSourceMutationScopesActive = true;
                }
            }
            catch
            {
                _pendingItemsSourceSelectionTransaction = null;
                _selectionModelSourceResetPreflightPending = false;
                _builtInSourceMutationPreflightActive = false;
                DisposeBuiltInSourceMutationScopes();
                throw;
            }
        }

        private void PrepareItemsSourceMutationPreflight(
            IReadOnlyList<object> selectionSnapshot,
            IEnumerable prospectiveSource)
        {
            using var origin = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.ItemsSourceChange,
                guarantee: DataGridSelectionChangingGuarantee.PostChangeReconciliation);
            ItemsSourceSelectionTransaction transaction = BeginItemsSourceSelectionTransaction(
                selectionSnapshot,
                prospectiveSource,
                preserveSelection: true,
                requireSelectionChangingHandler: false);
            if (transaction != null)
            {
                _pendingItemsSourceSelectionTransaction = transaction;
                _selectionModelSourceResetPreflightPending = true;
            }
        }

        internal void CompleteBuiltInCollectionViewMutationPreflight()
        {
            ItemsSourceSelectionTransaction transaction = null;
            if (_selectionModelSourceResetPreflightPending)
            {
                transaction = _pendingItemsSourceSelectionTransaction;
                _pendingItemsSourceSelectionTransaction = null;
                _selectionModelSourceResetPreflightPending = false;
            }

            try
            {
                if (transaction != null)
                {
                    CompleteItemsSourceSelectionTransaction(transaction);
                }
            }
            finally
            {
                _builtInSourceMutationPreflightActive = false;
                DisposeBuiltInSourceMutationScopes();
            }
        }

        private void DisposeBuiltInSourceMutationScopes()
        {
            if (!_builtInSourceMutationScopesActive)
            {
                return;
            }

            _builtInSourceMutationScopesActive = false;
            _builtInSourceMutationDeferralScope.Dispose();
            _builtInSourceSelectionCommitScope.Dispose();
            _builtInSourceMutationDeferralScope = default;
            _builtInSourceSelectionCommitScope = default;
        }

        internal ItemsSourceSelectionTransaction TakeItemsSourceSelectionTransaction(
            IReadOnlyList<object> selectionSnapshot,
            IEnumerable prospectiveSource)
        {
            // The built-in view completion boundary runs after all CollectionChanged listeners.
            // Retain its transaction until then so a SelectionModel subscribed after the grid
            // cannot overwrite the identity-restored selection with stale incremental indexes.
            if (_builtInSourceMutationPreflightActive)
            {
                return null;
            }

            if (_selectionModelSourceResetPreflightPending)
            {
                ItemsSourceSelectionTransaction transaction =
                    _pendingItemsSourceSelectionTransaction;
                _pendingItemsSourceSelectionTransaction = null;
                _selectionModelSourceResetPreflightPending = false;
                return transaction;
            }

            return BeginItemsSourceSelectionTransaction(
                selectionSnapshot,
                prospectiveSource,
                preserveSelection: true,
                requireSelectionChangingHandler: false);
        }

        internal bool IsSelectionModelSourceResetPreflightPending =>
            _selectionModelSourceResetPreflightPending ||
            _builtInSourceMutationPreflightActive;

        internal bool UseCachedSelectedItemsDuringSourceMutation =>
            _selectionModelSourceResetPreflightPending ||
            _builtInSourceMutationPreflightActive;

        internal void CompleteItemsSourceSelectionTransaction(ItemsSourceSelectionTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }

            // Paging uses a global selection source while the visible rows remain page-local.
            // Refresh that source before restoring final row indexes after a mutation.
            UpdateSelectionModelSource();

            IReadOnlyList<ItemsSourceSelectionRow> selectedRows =
                transaction.PreserveSelection || !transaction.Accepted
                ? transaction.SurvivingSelectedItems
                : Array.Empty<ItemsSourceSelectionRow>();
            RestoreItemsSourceSelectionRows(selectedRows);

            int currentIndex = transaction.Accepted
                ? transaction.ProposedCurrentIndex
                : transaction.OldCurrentIndex >= 0
                    ? transaction.OldCurrentIndex
                    : transaction.ProposedCurrentIndex;

            int columnIndex = transaction.Column?.Index ?? transaction.ColumnIndex;
            if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count ||
                !ColumnsItemsInternal[columnIndex].IsVisible)
            {
                columnIndex = FirstDisplayedNonFillerColumnIndex;
            }

            if (currentIndex >= 0 && columnIndex >= 0)
            {
                // Source-reconciliation indexes are always indexes in the final view. Convert
                // them through the row/slot mapping so paging does not interpret a page-local
                // row as a global SelectionModel index.
                int slot = SlotFromRowIndex(currentIndex);
                SetCurrentCellCore(columnIndex, slot, commitEdit: false, endRowEdit: false);
            }
            else
            {
                SetCurrentCellCore(-1, -1, commitEdit: false, endRowEdit: false);
            }

            int anchorIndex = transaction.Accepted
                ? transaction.ProposedAnchorIndex
                : transaction.OldAnchorIndex >= 0
                    ? transaction.OldAnchorIndex
                    : currentIndex;
            int anchorSlot = anchorIndex >= 0 ? SlotFromRowIndex(anchorIndex) : -1;
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                AnchorSlot = anchorSlot;
            }
            else
            {
                int anchorColumnIndex = transaction.OldAnchorColumn?.Index ??
                    transaction.OldAnchorColumnIndex;
                _cellAnchor = anchorSlot >= 0 &&
                    anchorColumnIndex >= 0 &&
                    anchorColumnIndex < ColumnsItemsInternal.Count
                        ? new DataGridCellCoordinates(anchorColumnIndex, anchorSlot)
                        : new DataGridCellCoordinates(-1, -1);
            }

            if (transaction.CurrencyIndex >= 0 &&
                transaction.CurrencyIndex < (DataConnection?.Count ?? 0))
            {
                DataConnection.CollectionView.MoveCurrentToPosition(transaction.CurrencyIndex);
            }

            if (!transaction.Accepted)
            {
                UpdateHorizontalOffset(transaction.HorizontalOffset);
                SetVerticalOffset(transaction.VerticalOffset);
            }

            // Notify cell/column observers last. They can synchronously query row selection,
            // current cell, anchor, currency, and both observable selection views and see only
            // the reconciled transaction state.
            IReadOnlyList<ItemsSourceSelectionCell> selectedCells =
                transaction.PreserveSelection || !transaction.Accepted
                ? transaction.SelectedCells
                : Array.Empty<ItemsSourceSelectionCell>();
            RestoreItemsSourceSelectionCells(selectedCells);

        }

        private void RestoreItemsSourceSelectionRows(
            IReadOnlyList<ItemsSourceSelectionRow> selectedRows)
        {
            if (_selectionModelAdapter == null || selectedRows == null)
            {
                return;
            }

            bool previousSync = _syncingSelectionModel;
            _syncingSelectionModel = true;
            try
            {
                int firstSelectionIndex = -1;
                ISelectionModel selectionModel = _selectionModelAdapter.Model;
                IEnumerable selectionSource = selectionModel.Source;
                bool detachGroupedSource =
                    DataConnection?.CollectionView?.IsGrouping == true && selectionSource != null;
                if (detachGroupedSource)
                {
                    selectionModel.Source = null;
                }

                try
                {
                    using (_selectionModelAdapter.SelectedItemsView.SuppressNotifications())
                    using (selectionModel.BatchUpdate())
                    {
                        selectionModel.Clear();
                        for (int i = 0; i < selectedRows.Count; i++)
                        {
                            int rowIndex = selectedRows[i].RowIndex;
                            if (rowIndex < 0 || rowIndex >= (DataConnection?.Count ?? 0))
                            {
                                continue;
                            }

                            int selectionIndex = GetSelectionIndexFromRowIndex(rowIndex);
                            if (selectionIndex < 0)
                            {
                                continue;
                            }

                            if (firstSelectionIndex < 0)
                            {
                                firstSelectionIndex = selectionIndex;
                            }
                            _selectionModelAdapter.Select(selectionIndex);
                        }
                    }
                }
                finally
                {
                    if (detachGroupedSource)
                    {
                        selectionModel.Source = selectionSource;
                    }
                }

                // Some SelectionModel implementations publish the final source Reset as their
                // outer batch closes. Reassert any missing final indexes after that boundary,
                // while grid callbacks and SelectedItems notifications remain suppressed.
                using (_selectionModelAdapter.SelectedItemsView.SuppressNotifications())
                {
                    for (int i = 0; i < selectedRows.Count; i++)
                    {
                        int rowIndex = selectedRows[i].RowIndex;
                        if (rowIndex < 0 || rowIndex >= (DataConnection?.Count ?? 0))
                        {
                            continue;
                        }

                        int selectionIndex = GetSelectionIndexFromRowIndex(rowIndex);
                        if (selectionIndex >= 0 && !_selectionModelAdapter.IsSelected(selectionIndex))
                        {
                            _selectionModelAdapter.Select(selectionIndex);
                        }
                    }
                }

                _preferredSelectionIndex = firstSelectionIndex;
                ApplySelectionFromSelectionModel();
                if (selectedRows.Count > 0)
                {
                    int firstRowIndex = selectedRows[0].RowIndex;
                    if (firstRowIndex >= 0 && firstRowIndex < (DataConnection?.Count ?? 0))
                    {
                        SetValueNoCallback(
                            SelectedItemProperty,
                            ProjectSelectionItem(DataConnection.GetDataItem(firstRowIndex)));
                        SetValueNoCallback(
                            SelectedIndexProperty,
                            GetSelectionIndexFromRowIndex(firstRowIndex));
                    }
                }
            }
            finally
            {
                _syncingSelectionModel = previousSync;
            }
        }

        private List<ItemsSourceSelectionCell> CaptureItemsSourceSelectionCells()
        {
            var result = new List<ItemsSourceSelectionCell>(_selectedCellsView.Count);
            for (int i = 0; i < _selectedCellsView.Count; i++)
            {
                DataGridCellInfo cell = _selectedCellsView[i];
                if (!cell.IsValid || cell.Item == null || cell.Column == null)
                {
                    continue;
                }

                result.Add(new ItemsSourceSelectionCell(
                    ProjectSelectionItem(cell.Item),
                    cell.Column,
                    cell.ColumnIndex,
                    cell.RowIndex));
            }

            return result;
        }

        private object CaptureSourceMutationAnchorItem(
            IReadOnlyList<object> selectionSnapshot,
            DataGridSelectionAnchorInfo fallback)
        {
            int anchorSlot = SelectionUnit == DataGridSelectionUnit.FullRow
                ? AnchorSlot
                : _cellAnchor.Slot;
            int anchorColumnIndex = SelectionUnit == DataGridSelectionUnit.FullRow
                ? fallback.ColumnIndex
                : _cellAnchor.ColumnIndex;
            int anchorRowIndex = anchorSlot >= 0 && anchorSlot < SlotCount && !IsGroupSlot(anchorSlot)
                ? RowIndexFromSlot(anchorSlot)
                : -1;

            if (anchorRowIndex >= 0)
            {
                if (_currentCell.IsValid && _currentCell.RowIndex == anchorRowIndex)
                {
                    return ProjectSelectionItem(_currentCell.Item);
                }

                for (int i = 0; i < _selectedCellsView.Count; i++)
                {
                    DataGridCellInfo cell = _selectedCellsView[i];
                    if (cell.RowIndex == anchorRowIndex &&
                        (anchorColumnIndex < 0 || cell.ColumnIndex == anchorColumnIndex))
                    {
                        return ProjectSelectionItem(cell.Item);
                    }
                }

                if (selectionSnapshot != null)
                {
                    var oldRows = new List<int>();
                    foreach (int selectedSlot in _selectedItems.GetIndexes())
                    {
                        if (selectedSlot >= 0 && selectedSlot < SlotCount && !IsGroupSlot(selectedSlot))
                        {
                            oldRows.Add(RowIndexFromSlot(selectedSlot));
                        }
                    }
                    oldRows.Sort();
                    int count = Math.Min(oldRows.Count, selectionSnapshot.Count);
                    for (int i = 0; i < count; i++)
                    {
                        if (oldRows[i] == anchorRowIndex)
                        {
                            return ProjectSelectionItem(selectionSnapshot[i]);
                        }
                    }
                }
            }

            return ProjectSelectionItem(fallback.Item);
        }

        private void RestoreItemsSourceSelectionCells(
            IReadOnlyList<ItemsSourceSelectionCell> selectedCells)
        {
            var remapped = new List<DataGridCellInfo>(selectedCells.Count);
            var seen = new HashSet<long>();
            for (int i = 0; i < selectedCells.Count; i++)
            {
                ItemsSourceSelectionCell selectedCell = selectedCells[i];
                int rowIndex = selectedCell.RowIndex;
                int columnIndex = selectedCell.ColumnIndex;
                int liveColumnIndex = selectedCell.Column?.Index ?? -1;
                if (liveColumnIndex >= 0 &&
                    liveColumnIndex < ColumnsItemsInternal.Count &&
                    ReferenceEquals(ColumnsItemsInternal[liveColumnIndex], selectedCell.Column))
                {
                    columnIndex = liveColumnIndex;
                }
                else if (columnIndex < 0)
                {
                    columnIndex = liveColumnIndex;
                }
                if (rowIndex < 0 ||
                    columnIndex < 0 ||
                    columnIndex >= ColumnsItemsInternal.Count)
                {
                    continue;
                }

                DataGridColumn column = ColumnsItemsInternal[columnIndex];
                if (column == null || !column.IsVisible || column is DataGridFillerColumn)
                {
                    continue;
                }

                long key = GetCellCoordinateKey(rowIndex, columnIndex);
                if (!seen.Add(key))
                {
                    continue;
                }

                object rowDataContext = DataConnection.GetDataItem(rowIndex);
                remapped.Add(new DataGridCellInfo(
                    rowDataContext,
                    column,
                    rowIndex,
                    columnIndex,
                    isValid: true));
            }

            ReplaceSelectedCellsAfterItemsSourceMutation(remapped);
        }

        private static void AddReferenceIdentity(List<object> identities, object item)
        {
            if (item == null)
            {
                return;
            }

            for (int i = 0; i < identities.Count; i++)
            {
                if (ReferenceEquals(identities[i], item))
                {
                    return;
                }
            }

            identities.Add(item);
        }

        private DataGridSelectionRowInfo CreateSourceMutationRowInfo(object item, int oldRowIndex)
        {
            HierarchicalNode node = FindMaterializedHierarchyNode(item);
            object rowDataContext = node ?? item;
            CreateSelectionItemContext(
                rowDataContext,
                out object projectedItem,
                out HierarchicalNode resolvedNode,
                out IReadOnlyList<HierarchicalNode> path);
            int rowIndex = oldRowIndex;
            if (rowIndex < 0 && DataConnection != null)
            {
                rowIndex = DataConnection.IndexOf(rowDataContext);
                if (rowIndex < 0 && !ReferenceEquals(rowDataContext, item))
                {
                    rowIndex = DataConnection.IndexOf(item);
                }
            }
            if (rowIndex < 0 &&
                (Equals(CurrentCell.Item, rowDataContext) || Equals(CurrentCell.Item, item)))
            {
                rowIndex = CurrentCell.RowIndex;
            }
            return new DataGridSelectionRowInfo(
                rowDataContext,
                projectedItem,
                rowIndex,
                resolvedNode,
                path);
        }

        private DataGridCellInfo CreateProspectiveCellInfo(
            List<ProspectiveSelectionRow> rows,
            int rowIndex,
            int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= rows.Count ||
                columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
            {
                return DataGridCellInfo.Unset;
            }

            DataGridColumn column = ColumnsItemsInternal[columnIndex];
            if (column == null || !column.IsVisible || column is DataGridFillerColumn)
            {
                return DataGridCellInfo.Unset;
            }

            return new DataGridCellInfo(
                rows[rowIndex].RowDataContext,
                column,
                rowIndex,
                columnIndex,
                isValid: true);
        }

        private static DataGridSelectionAnchorInfo CreateProspectiveAnchorInfo(
            List<ProspectiveSelectionRow> rows,
            int rowIndex,
            int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= rows.Count || columnIndex < 0)
            {
                return DataGridSelectionAnchorInfo.Unset;
            }

            ProspectiveSelectionRow row = rows[rowIndex];
            return new DataGridSelectionAnchorInfo(
                row.RowDataContext,
                row.Item,
                rowIndex,
                columnIndex,
                row.Node,
                row.HierarchyPath,
                isValid: true);
        }

        private bool TryPreviewRowSet(
            HashSet<int> proposedRows,
            DataGridCellInfo proposedCurrentCell,
            DataGridSelectionAnchorInfo proposedAnchor)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            return TryRaiseSelectionChanging(
                CaptureSelectedRowIndexes(),
                proposedRows,
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                proposedCurrentCell,
                proposedAnchor);
        }

        private bool TryPreviewSelectionModelSelection()
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            var proposedRows = new HashSet<int>();
            int preferredSelectionIndex = _preferredSelectionIndex >= 0
                ? _preferredSelectionIndex
                : _selectionModelAdapter?.Model.SelectedIndex ?? -1;
            int proposedSlot = -1;
            IReadOnlyList<int> selectedIndexes = _selectionModelAdapter?.Model.SelectedIndexes;
            if (selectedIndexes != null)
            {
                for (int i = 0; i < selectedIndexes.Count; i++)
                {
                    int selectionIndex = selectedIndexes[i];
                    int slot = SlotFromSelectionIndex(selectionIndex);
                    if (slot < 0 || slot >= SlotCount || IsGroupSlot(slot))
                    {
                        continue;
                    }

                    int rowIndex = RowIndexFromSlot(slot);
                    if (rowIndex >= 0)
                    {
                        proposedRows.Add(rowIndex);
                        if (proposedSlot < 0 || selectionIndex == preferredSelectionIndex)
                        {
                            proposedSlot = slot;
                        }
                    }
                }
            }

            int columnIndex = CurrentColumnIndex >= 0
                ? CurrentColumnIndex
                : FirstDisplayedNonFillerColumnIndex;
            return TryPreviewRowSet(
                proposedRows,
                CreateCellInfo(columnIndex, proposedSlot),
                CreateAnchorInfo(proposedSlot, columnIndex));
        }

        private bool TryPreviewCurrentCell(DataGridCellInfo proposedCurrentCell)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            HashSet<int> rows = CaptureSelectedRowIndexes();
            return TryRaiseSelectionChanging(
                rows,
                new HashSet<int>(rows),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                proposedCurrentCell,
                GetCurrentSelectionAnchorInfo());
        }

        private void AddSlotRange(HashSet<int> rows, int firstSlot, int lastSlot)
        {
            int start = Math.Max(0, Math.Min(firstSlot, lastSlot));
            int end = Math.Min(SlotCount - 1, Math.Max(firstSlot, lastSlot));
            for (int slot = start; slot <= end; slot++)
            {
                if (!IsGroupSlot(slot))
                {
                    int rowIndex = RowIndexFromSlot(slot);
                    if (rowIndex >= 0)
                    {
                        rows.Add(rowIndex);
                    }
                }
            }
        }

        private void RemoveSlotRange(HashSet<int> rows, int firstSlot, int lastSlot)
        {
            int start = Math.Max(0, Math.Min(firstSlot, lastSlot));
            int end = Math.Min(SlotCount - 1, Math.Max(firstSlot, lastSlot));
            for (int slot = start; slot <= end; slot++)
            {
                if (!IsGroupSlot(slot))
                {
                    int rowIndex = RowIndexFromSlot(slot);
                    if (rowIndex >= 0)
                    {
                        rows.Remove(rowIndex);
                    }
                }
            }
        }

        private List<DataGridCellInfo> BuildCellSelectionProposal(
            bool append,
            int startRowIndex,
            int endRowIndex,
            int startColumnIndex,
            int endColumnIndex,
            bool columnIndexesAreDisplayIndexes)
        {
            var proposed = new List<DataGridCellInfo>();
            var coordinates = new HashSet<long>();
            if (append)
            {
                for (int i = 0; i < _selectedCellsView.Count; i++)
                {
                    DataGridCellInfo cell = _selectedCellsView[i];
                    if (coordinates.Add(GetCellCoordinateKey(cell.RowIndex, cell.ColumnIndex)))
                    {
                        proposed.Add(cell);
                    }
                }
            }

            if (DataConnection == null || ColumnsInternal == null)
            {
                return proposed;
            }

            int firstRow = Math.Max(0, Math.Min(startRowIndex, endRowIndex));
            int lastRow = Math.Min(DataConnection.Count - 1, Math.Max(startRowIndex, endRowIndex));
            int firstColumn = Math.Min(startColumnIndex, endColumnIndex);
            int lastColumn = Math.Max(startColumnIndex, endColumnIndex);
            for (int rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
            {
                int slot = SlotFromRowIndex(rowIndex);
                if (slot < 0 || IsGroupSlot(slot))
                {
                    continue;
                }

                object item = DataConnection.GetDataItem(rowIndex);
                for (int position = firstColumn; position <= lastColumn; position++)
                {
                    DataGridColumn column = columnIndexesAreDisplayIndexes
                        ? ColumnsInternal.GetColumnAtDisplayIndex(position)
                        : position >= 0 && position < ColumnsItemsInternal.Count
                            ? ColumnsItemsInternal[position]
                            : null;
                    if (column == null || !column.IsVisible || column is DataGridFillerColumn)
                    {
                        continue;
                    }

                    long key = GetCellCoordinateKey(rowIndex, column.Index);
                    if (coordinates.Add(key))
                    {
                        proposed.Add(new DataGridCellInfo(item, column, rowIndex, column.Index, isValid: true));
                    }
                }
            }

            return proposed;
        }

        private List<DataGridCellInfo> BuildNormalizedCellSelectionProposal(IEnumerable<DataGridCellInfo> cells)
        {
            int capacity = cells is ICollection<DataGridCellInfo> collection ? collection.Count : 0;
            var proposed = new List<DataGridCellInfo>(capacity);
            var coordinates = new HashSet<long>();
            if (cells == null)
            {
                return proposed;
            }

            foreach (DataGridCellInfo cell in cells)
            {
                if (TryNormalizeCell(cell, out DataGridCellInfo normalized) &&
                    coordinates.Add(GetCellCoordinateKey(normalized.RowIndex, normalized.ColumnIndex)))
                {
                    proposed.Add(normalized);
                }
            }

            return proposed;
        }

        private List<DataGridCellInfo> BuildColumnSelectionProposal(IList<DataGridColumn> columns)
        {
            var proposed = new List<DataGridCellInfo>();
            var coordinates = new HashSet<long>();
            int rowCount = DataConnection?.Count ?? 0;
            if (columns == null || rowCount <= 0)
            {
                return proposed;
            }

            for (int columnPosition = 0; columnPosition < columns.Count; columnPosition++)
            {
                DataGridColumn column = columns[columnPosition];
                if (column == null || !column.IsVisible || column is DataGridFillerColumn ||
                    column.Index < 0 || column.Index >= ColumnsItemsInternal.Count)
                {
                    continue;
                }

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    int slot = SlotFromRowIndex(rowIndex);
                    if (slot < 0 || IsGroupSlot(slot))
                    {
                        continue;
                    }

                    long key = GetCellCoordinateKey(rowIndex, column.Index);
                    if (coordinates.Add(key))
                    {
                        proposed.Add(new DataGridCellInfo(
                            DataConnection.GetDataItem(rowIndex),
                            column,
                            rowIndex,
                            column.Index,
                            isValid: true));
                    }
                }
            }

            return proposed;
        }

        private static void RemoveCellFromProposal(
            List<DataGridCellInfo> proposed,
            int rowIndex,
            int columnIndex)
        {
            for (int i = proposed.Count - 1; i >= 0; i--)
            {
                DataGridCellInfo cell = proposed[i];
                if (cell.RowIndex == rowIndex && cell.ColumnIndex == columnIndex)
                {
                    proposed.RemoveAt(i);
                }
            }
        }

        private static void AddCellsToProposal(
            List<DataGridCellInfo> proposed,
            IReadOnlyList<DataGridCellInfo> cells)
        {
            var coordinates = new HashSet<long>();
            for (int i = 0; i < proposed.Count; i++)
            {
                coordinates.Add(GetCellCoordinateKey(proposed[i].RowIndex, proposed[i].ColumnIndex));
            }

            for (int i = 0; i < cells.Count; i++)
            {
                DataGridCellInfo cell = cells[i];
                if (coordinates.Add(GetCellCoordinateKey(cell.RowIndex, cell.ColumnIndex)))
                {
                    proposed.Add(cell);
                }
            }
        }

        private void RemoveDisplayRangeFromProposal(
            List<DataGridCellInfo> proposed,
            int startRowIndex,
            int endRowIndex,
            int startDisplayIndex,
            int endDisplayIndex)
        {
            int firstRow = Math.Min(startRowIndex, endRowIndex);
            int lastRow = Math.Max(startRowIndex, endRowIndex);
            int firstDisplay = Math.Min(startDisplayIndex, endDisplayIndex);
            int lastDisplay = Math.Max(startDisplayIndex, endDisplayIndex);
            for (int i = proposed.Count - 1; i >= 0; i--)
            {
                DataGridCellInfo cell = proposed[i];
                int displayIndex = GetColumnDisplayIndex(cell.ColumnIndex);
                if (cell.RowIndex >= firstRow && cell.RowIndex <= lastRow &&
                    displayIndex >= firstDisplay && displayIndex <= lastDisplay)
                {
                    proposed.RemoveAt(i);
                }
            }
        }

        private bool TryPreviewCellSelection(
            IReadOnlyList<DataGridCellInfo> proposedCells,
            DataGridCellInfo proposedCurrentCell,
            DataGridSelectionAnchorInfo proposedAnchor,
            HashSet<int> proposedRowsOverride = null)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            var currentCoordinates = new HashSet<long>();
            var proposedCoordinates = new HashSet<long>();
            var currentCellRows = new HashSet<int>();
            var proposedCellRows = new HashSet<int>();
            var addedCells = new List<DataGridCellInfo>();
            var removedCells = new List<DataGridCellInfo>();

            for (int i = 0; i < _selectedCellsView.Count; i++)
            {
                DataGridCellInfo cell = _selectedCellsView[i];
                currentCoordinates.Add(GetCellCoordinateKey(cell.RowIndex, cell.ColumnIndex));
                currentCellRows.Add(cell.RowIndex);
            }

            for (int i = 0; i < proposedCells.Count; i++)
            {
                DataGridCellInfo cell = proposedCells[i];
                long key = GetCellCoordinateKey(cell.RowIndex, cell.ColumnIndex);
                if (proposedCoordinates.Add(key))
                {
                    proposedCellRows.Add(cell.RowIndex);
                    if (!currentCoordinates.Contains(key))
                    {
                        addedCells.Add(cell);
                    }
                }
            }

            for (int i = 0; i < _selectedCellsView.Count; i++)
            {
                DataGridCellInfo cell = _selectedCellsView[i];
                if (!proposedCoordinates.Contains(GetCellCoordinateKey(cell.RowIndex, cell.ColumnIndex)))
                {
                    removedCells.Add(cell);
                }
            }

            var currentRows = CaptureSelectedRowIndexes();
            var proposedRows = new HashSet<int>(currentRows);
            foreach (int rowIndex in currentCellRows)
            {
                if (!proposedCellRows.Contains(rowIndex))
                {
                    proposedRows.Remove(rowIndex);
                }
            }
            proposedRows.UnionWith(proposedCellRows);
            if (proposedRowsOverride != null)
            {
                proposedRows = new HashSet<int>(proposedRowsOverride);
                proposedRows.UnionWith(proposedCellRows);
            }

            var currentColumns = new HashSet<DataGridColumn>(_selectedColumnsView);
            var proposedColumnCounts = new Dictionary<DataGridColumn, int>();
            for (int i = 0; i < proposedCells.Count; i++)
            {
                DataGridColumn column = proposedCells[i].Column;
                proposedColumnCounts.TryGetValue(column, out int count);
                proposedColumnCounts[column] = count + 1;
            }

            var proposedColumns = new HashSet<DataGridColumn>();
            int rowCount = DataConnection?.Count ?? 0;
            if (rowCount > 0)
            {
                foreach (KeyValuePair<DataGridColumn, int> entry in proposedColumnCounts)
                {
                    if (entry.Value >= rowCount)
                    {
                        proposedColumns.Add(entry.Key);
                    }
                }
            }

            var addedColumns = new List<DataGridColumn>();
            var removedColumns = new List<DataGridColumn>();
            foreach (DataGridColumn column in proposedColumns)
            {
                if (!currentColumns.Contains(column))
                {
                    addedColumns.Add(column);
                }
            }
            foreach (DataGridColumn column in currentColumns)
            {
                if (!proposedColumns.Contains(column))
                {
                    removedColumns.Add(column);
                }
            }

            return TryRaiseSelectionChanging(
                currentRows,
                proposedRows,
                addedCells,
                removedCells,
                addedColumns,
                removedColumns,
                proposedCurrentCell,
                proposedAnchor);
        }

        private HashSet<int> BuildRowSelectionProposal(IList items, out int proposedSlot)
        {
            var proposedRows = new HashSet<int>();
            proposedSlot = -1;
            if (items == null || DataConnection == null)
            {
                return proposedRows;
            }

            int start = SelectionMode == DataGridSelectionMode.Single && items.Count > 0
                ? items.Count - 1
                : 0;
            for (int i = start; i < items.Count; i++)
            {
                object item = items[i];
                int selectionIndex = GetSelectionModelIndexOfItem(item);
                int slot = selectionIndex < 0 ? -1 : SlotFromSelectionIndex(selectionIndex);
                if (slot < 0 || slot >= SlotCount || IsGroupSlot(slot))
                {
                    continue;
                }

                int rowIndex = RowIndexFromSlot(slot);
                if (rowIndex >= 0)
                {
                    proposedRows.Add(rowIndex);
                    if (proposedSlot < 0)
                    {
                        proposedSlot = slot;
                    }
                }
            }

            return proposedRows;
        }

        private static long GetCellCoordinateKey(int rowIndex, int columnIndex) =>
            ((long)rowIndex << 32) | (uint)columnIndex;

        private bool TryPreviewRowSelection(
            int columnIndex,
            int slot,
            DataGridSelectionAction action)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();

            var currentRows = CaptureSelectedRowIndexes();
            var proposedRows = new HashSet<int>(currentRows);
            int rowIndex = slot >= 0 && slot < SlotCount && !IsGroupSlot(slot)
                ? RowIndexFromSlot(slot)
                : -1;
            int proposedAnchorSlot = AnchorSlot;

            switch (action)
            {
                case DataGridSelectionAction.AddCurrentToSelection:
                    if (rowIndex >= 0)
                    {
                        proposedRows.Add(rowIndex);
                        proposedAnchorSlot = slot;
                    }
                    break;
                case DataGridSelectionAction.RemoveCurrentFromSelection:
                    if (rowIndex >= 0)
                    {
                        proposedRows.Remove(rowIndex);
                    }
                    break;
                case DataGridSelectionAction.SelectFromAnchorToCurrent:
                    proposedRows.Clear();
                    if (SelectionMode == DataGridSelectionMode.Extended && AnchorSlot >= 0 && rowIndex >= 0)
                    {
                        int anchorRowIndex = RowIndexFromSlot(AnchorSlot);
                        AddRowRange(proposedRows, anchorRowIndex, rowIndex);
                    }
                    else if (rowIndex >= 0)
                    {
                        proposedRows.Add(rowIndex);
                        proposedAnchorSlot = slot;
                    }
                    break;
                case DataGridSelectionAction.SelectCurrent:
                    proposedRows.Clear();
                    proposedAnchorSlot = -1;
                    if (rowIndex >= 0)
                    {
                        proposedRows.Add(rowIndex);
                        proposedAnchorSlot = slot;
                    }
                    break;
                case DataGridSelectionAction.None:
                    break;
            }

            DataGridCellInfo proposedCurrentCell = CreateCellInfo(columnIndex, slot);
            DataGridSelectionAnchorInfo proposedAnchor = CreateAnchorInfo(
                proposedAnchorSlot,
                columnIndex >= 0 ? columnIndex : CurrentColumnIndex);

            return TryRaiseSelectionChanging(
                currentRows,
                proposedRows,
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                proposedCurrentCell,
                proposedAnchor);
        }

        private bool TryPreviewAllRows()
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            ThrowIfSelectionChangingReentrant();
            var currentRows = CaptureSelectedRowIndexes();
            var proposedRows = new HashSet<int>();
            if (DataConnection != null)
            {
                for (int rowIndex = 0; rowIndex < DataConnection.Count; rowIndex++)
                {
                    int slot = SlotFromRowIndex(rowIndex);
                    if (slot >= 0 && !IsGroupSlot(slot))
                    {
                        proposedRows.Add(rowIndex);
                    }
                }
            }

            return TryRaiseSelectionChanging(
                currentRows,
                proposedRows,
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridCellInfo>(),
                Array.Empty<DataGridColumn>(),
                Array.Empty<DataGridColumn>(),
                CurrentCell,
                CreateAnchorInfo(AnchorSlot, CurrentColumnIndex));
        }

        private bool TryRaiseSelectionChanging(
            HashSet<int> currentRows,
            HashSet<int> proposedRows,
            IReadOnlyList<DataGridCellInfo> addedCells,
            IReadOnlyList<DataGridCellInfo> removedCells,
            IReadOnlyList<DataGridColumn> addedColumns,
            IReadOnlyList<DataGridColumn> removedColumns,
            DataGridCellInfo proposedCurrentCell,
            DataGridSelectionAnchorInfo proposedAnchor,
            DataGridSelectionRowInfo proposedUnrealizedRow = null,
            object proposedCurrentItemOverride = null)
        {
            if (!HasSelectionChangingHandlers || _selectionCommitDepth > 0)
            {
                return true;
            }

            var addedRows = new List<DataGridSelectionRowInfo>();
            var removedRows = new List<DataGridSelectionRowInfo>();
            var addedItems = new List<object>();
            var removedItems = new List<object>();

            AppendRowDelta(proposedRows, currentRows, addedRows, addedItems);
            AppendRowDelta(currentRows, proposedRows, removedRows, removedItems);
            if (proposedUnrealizedRow != null)
            {
                addedRows.Add(proposedUnrealizedRow);
                addedItems.Add(proposedUnrealizedRow.Item);
            }

            bool currentChanged = !CurrentCell.Equals(proposedCurrentCell) ||
                CurrentCell.RowIndex != proposedCurrentCell.RowIndex ||
                CurrentCell.ColumnIndex != proposedCurrentCell.ColumnIndex ||
                CurrentCell.IsValid != proposedCurrentCell.IsValid;
            DataGridSelectionAnchorInfo currentAnchor = GetCurrentSelectionAnchorInfo();
            bool anchorChanged = currentAnchor.IsValid != proposedAnchor.IsValid ||
                currentAnchor.RowIndex != proposedAnchor.RowIndex ||
                currentAnchor.ColumnIndex != proposedAnchor.ColumnIndex;

            if (addedRows.Count == 0 && removedRows.Count == 0 &&
                addedCells.Count == 0 && removedCells.Count == 0 &&
                addedColumns.Count == 0 && removedColumns.Count == 0 &&
                !currentChanged && !anchorChanged)
            {
                return true;
            }

            object proposedCurrentItem = proposedCurrentItemOverride;
            HierarchicalNode hierarchyNode = null;
            IReadOnlyList<HierarchicalNode> hierarchyPath = Array.Empty<HierarchicalNode>();
            if (proposedCurrentCell.IsValid)
            {
                CreateSelectionItemContext(
                    proposedCurrentCell.Item,
                    out proposedCurrentItem,
                    out hierarchyNode,
                    out hierarchyPath);
            }
            else if (proposedUnrealizedRow != null)
            {
                hierarchyNode = proposedUnrealizedRow.HierarchicalNode;
                hierarchyPath = proposedUnrealizedRow.HierarchyPath;
            }

            var args = new DataGridSelectionChangingEventArgs(
                addedItems,
                removedItems,
                addedRows,
                removedRows,
                addedCells,
                removedCells,
                addedColumns,
                removedColumns,
                proposedCurrentItem,
                proposedCurrentCell,
                proposedAnchor,
                hierarchyNode,
                hierarchyPath,
                CurrentSelectionChangeSource,
                CurrentSelectionTriggerEvent,
                CurrentSelectionChangingGuarantee);

            _raisingSelectionChanging = true;
            try
            {
                SelectionChanging?.Invoke(this, args);
            }
            finally
            {
                _raisingSelectionChanging = false;
            }

            return !args.Cancel;
        }

        private HashSet<int> CaptureSelectedRowIndexes()
        {
            var result = new HashSet<int>();
            if (DataConnection == null)
            {
                return result;
            }

            foreach (int slot in _selectedItems.GetIndexes())
            {
                if (slot >= 0 && slot < SlotCount && !IsGroupSlot(slot))
                {
                    int rowIndex = RowIndexFromSlot(slot);
                    if (rowIndex >= 0 && rowIndex < DataConnection.Count)
                    {
                        result.Add(rowIndex);
                    }
                }
            }

            return result;
        }

        private void AppendRowDelta(
            HashSet<int> source,
            HashSet<int> excluded,
            List<DataGridSelectionRowInfo> rows,
            List<object> items)
        {
            if (DataConnection == null || source.Count == 0)
            {
                return;
            }

            var indexes = new List<int>(source.Count);
            foreach (int rowIndex in source)
            {
                if (!excluded.Contains(rowIndex))
                {
                    indexes.Add(rowIndex);
                }
            }
            indexes.Sort();

            for (int i = 0; i < indexes.Count; i++)
            {
                DataGridSelectionRowInfo row = CreateRowInfo(indexes[i]);
                if (row != null)
                {
                    rows.Add(row);
                    items.Add(row.Item);
                }
            }
        }

        private DataGridSelectionRowInfo CreateRowInfo(int rowIndex)
        {
            if (DataConnection == null || rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return null;
            }

            object rowDataContext = DataConnection.GetDataItem(rowIndex);
            CreateSelectionItemContext(
                rowDataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);
            return new DataGridSelectionRowInfo(rowDataContext, item, rowIndex, node, path);
        }

        private DataGridCellInfo CreateCellInfo(int columnIndex, int slot)
        {
            if (DataConnection == null || columnIndex < 0 || slot < 0 ||
                columnIndex >= ColumnsItemsInternal.Count || slot >= SlotCount ||
                IsGroupSlot(slot) || IsSlotOutOfSelectionBounds(slot))
            {
                return DataGridCellInfo.Unset;
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return DataGridCellInfo.Unset;
            }

            DataGridColumn column = ColumnsItemsInternal[columnIndex];
            if (column == null || !column.IsVisible || column is DataGridFillerColumn)
            {
                return DataGridCellInfo.Unset;
            }

            return new DataGridCellInfo(
                DataConnection.GetDataItem(rowIndex),
                column,
                rowIndex,
                columnIndex,
                isValid: true);
        }

        private DataGridSelectionAnchorInfo CreateAnchorInfo(int slot, int columnIndex)
        {
            if (DataConnection == null || slot < 0 || slot >= SlotCount || IsGroupSlot(slot))
            {
                return DataGridSelectionAnchorInfo.Unset;
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return DataGridSelectionAnchorInfo.Unset;
            }

            object rowDataContext = DataConnection.GetDataItem(rowIndex);
            CreateSelectionItemContext(
                rowDataContext,
                out object item,
                out HierarchicalNode node,
                out IReadOnlyList<HierarchicalNode> path);
            return new DataGridSelectionAnchorInfo(
                rowDataContext,
                item,
                rowIndex,
                columnIndex,
                node,
                path,
                isValid: true);
        }

        private DataGridSelectionAnchorInfo GetCurrentSelectionAnchorInfo()
        {
            if (SelectionUnit != DataGridSelectionUnit.FullRow && _cellAnchor.Slot >= 0)
            {
                return CreateAnchorInfo(_cellAnchor.Slot, _cellAnchor.ColumnIndex);
            }

            return CreateAnchorInfo(AnchorSlot, CurrentColumnIndex);
        }

        private static void CreateSelectionItemContext(
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

        private void AddRowRange(HashSet<int> rows, int first, int last)
        {
            if (DataConnection == null || first < 0 || last < 0)
            {
                return;
            }

            int start = Math.Min(first, last);
            int end = Math.Min(Math.Max(first, last), DataConnection.Count - 1);
            for (int rowIndex = start; rowIndex <= end; rowIndex++)
            {
                int slot = SlotFromRowIndex(rowIndex);
                if (slot >= 0 && !IsGroupSlot(slot))
                {
                    rows.Add(rowIndex);
                }
            }
        }

        private void ThrowIfSelectionChangingReentrant()
        {
            if (_raisingSelectionChanging)
            {
                throw new InvalidOperationException(
                    "Selection cannot be modified while SelectionChanging is being raised.");
            }
        }

        private readonly struct ProspectiveSelectionRow
        {
            public ProspectiveSelectionRow(
                object rowDataContext,
                object item,
                HierarchicalNode node,
                IReadOnlyList<HierarchicalNode> hierarchyPath)
            {
                RowDataContext = rowDataContext;
                Item = item;
                Node = node;
                HierarchyPath = hierarchyPath;
            }

            public object RowDataContext { get; }

            public object Item { get; }

            public HierarchicalNode Node { get; }

            public IReadOnlyList<HierarchicalNode> HierarchyPath { get; }
        }

        /// <summary>
        /// Resolves every captured identity as one occurrence-aware set. Exact references are
        /// assigned before equality fallback, so a removed item cannot steal the surviving row
        /// that belongs to a different equal-but-distinct selected/current/cell identity.
        /// Repeated uses of the same reference (selection, current cell, anchor, currency, and
        /// selected cells) intentionally share one resolved row.
        /// </summary>
        private sealed class ProspectiveIdentityMap
        {
            private readonly IReadOnlyList<object> _identities;
            private readonly int[] _indexes;

            public ProspectiveIdentityMap(
                List<ProspectiveSelectionRow> rows,
                IReadOnlyList<object> identities)
            {
                _identities = identities;
                _indexes = new int[identities.Count];
                Array.Fill(_indexes, -1);
                var consumedRows = new bool[rows.Count];

                for (int identityIndex = 0; identityIndex < identities.Count; identityIndex++)
                {
                    object identity = identities[identityIndex];
                    for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        if (!consumedRows[rowIndex] &&
                            ReferenceEquals(rows[rowIndex].Item, identity))
                        {
                            _indexes[identityIndex] = rowIndex;
                            consumedRows[rowIndex] = true;
                            break;
                        }
                    }
                }

                for (int identityIndex = 0; identityIndex < identities.Count; identityIndex++)
                {
                    if (_indexes[identityIndex] >= 0)
                    {
                        continue;
                    }

                    object identity = identities[identityIndex];
                    for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        if (!consumedRows[rowIndex] &&
                            Equals(rows[rowIndex].Item, identity))
                        {
                            _indexes[identityIndex] = rowIndex;
                            consumedRows[rowIndex] = true;
                            break;
                        }
                    }
                }
            }

            public int GetIndex(object item)
            {
                if (item == null)
                {
                    return -1;
                }

                for (int i = 0; i < _identities.Count; i++)
                {
                    if (ReferenceEquals(_identities[i], item))
                    {
                        return _indexes[i];
                    }
                }

                return -1;
            }
        }

        internal sealed class ItemsSourceSelectionTransaction
        {
            public ItemsSourceSelectionTransaction(
                bool accepted,
                IReadOnlyList<ItemsSourceSelectionRow> survivingSelectedItems,
                int oldCurrentIndex,
                int oldAnchorIndex,
                int oldAnchorColumnIndex,
                DataGridColumn oldAnchorColumn,
                int proposedCurrentIndex,
                int proposedAnchorIndex,
                int columnIndex,
                DataGridColumn column,
                double verticalOffset,
                double horizontalOffset,
                int currencyIndex,
                IReadOnlyList<ItemsSourceSelectionCell> selectedCells,
                bool preserveSelection)
            {
                Accepted = accepted;
                SurvivingSelectedItems = survivingSelectedItems;
                OldCurrentIndex = oldCurrentIndex;
                OldAnchorIndex = oldAnchorIndex;
                OldAnchorColumnIndex = oldAnchorColumnIndex;
                OldAnchorColumn = oldAnchorColumn;
                ProposedCurrentIndex = proposedCurrentIndex;
                ProposedAnchorIndex = proposedAnchorIndex;
                ColumnIndex = columnIndex;
                Column = column;
                VerticalOffset = verticalOffset;
                HorizontalOffset = horizontalOffset;
                CurrencyIndex = currencyIndex;
                SelectedCells = selectedCells;
                PreserveSelection = preserveSelection;
            }

            public bool Accepted { get; }

            public IReadOnlyList<ItemsSourceSelectionRow> SurvivingSelectedItems { get; }

            public int OldCurrentIndex { get; }

            public int OldAnchorIndex { get; }

            public int OldAnchorColumnIndex { get; }

            public DataGridColumn OldAnchorColumn { get; }

            public int ProposedCurrentIndex { get; }

            public int ProposedAnchorIndex { get; }

            public int ColumnIndex { get; }

            public DataGridColumn Column { get; }

            public double VerticalOffset { get; }

            public double HorizontalOffset { get; }

            public int CurrencyIndex { get; }

            public IReadOnlyList<ItemsSourceSelectionCell> SelectedCells { get; }

            public bool PreserveSelection { get; }
        }

        internal sealed class ItemsSourceSelectionRow
        {
            public ItemsSourceSelectionRow(object item, int rowIndex)
            {
                Item = item;
                RowIndex = rowIndex;
            }

            public object Item { get; }

            public int RowIndex { get; }
        }

        internal sealed class ItemsSourceSelectionCell
        {
            public ItemsSourceSelectionCell(
                object item,
                DataGridColumn column,
                int columnIndex,
                int rowIndex)
            {
                Item = item;
                Column = column;
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
            }

            public object Item { get; }

            public DataGridColumn Column { get; }

            public int ColumnIndex { get; }

            public int RowIndex { get; }
        }

        internal readonly struct SelectionCommitScope : IDisposable
        {
            private readonly DataGrid _owner;

            public SelectionCommitScope(DataGrid owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner != null)
                {
                    _owner._selectionCommitDepth--;
                }
            }
        }

        internal readonly struct ItemsSourceMutationDeferralScope : IDisposable
        {
            private readonly DataGrid _owner;

            public ItemsSourceMutationDeferralScope(DataGrid owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null || --_owner._itemsSourceMutationDeferralDepth != 0)
                {
                    return;
                }

                // Flush routed selection/current notifications while direct-property writes
                // are still silent, then publish one old-to-final direct-property transition.
                _owner.NoCurrentCellChangeCount--;
                _owner.NoSelectionChangeCount--;

                object oldSelectedItem = _owner._itemsSourceMutationOldSelectedItem;
                int oldSelectedIndex = _owner._itemsSourceMutationOldSelectedIndex;
                DataGridCellInfo oldCurrentCell = _owner._itemsSourceMutationOldCurrentCell;
                _owner._itemsSourceMutationOldSelectedItem = null;
                _owner._itemsSourceMutationOldCurrentCell = DataGridCellInfo.Unset;

                if (!Equals(oldSelectedItem, _owner._selectedItem))
                {
                    _owner.RaisePropertyChanged(SelectedItemProperty, oldSelectedItem, _owner._selectedItem);
                }
                if (oldSelectedIndex != _owner._selectedIndex)
                {
                    _owner.RaisePropertyChanged(SelectedIndexProperty, oldSelectedIndex, _owner._selectedIndex);
                }
                if (oldCurrentCell.RowIndex != _owner._currentCell.RowIndex ||
                    oldCurrentCell.ColumnIndex != _owner._currentCell.ColumnIndex ||
                    oldCurrentCell.IsValid != _owner._currentCell.IsValid ||
                    !oldCurrentCell.Equals(_owner._currentCell))
                {
                    _owner.RaisePropertyChanged(CurrentCellProperty, oldCurrentCell, _owner._currentCell);
                }
            }
        }
    }
}
