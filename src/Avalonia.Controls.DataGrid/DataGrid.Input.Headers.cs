// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private void DataGrid_ColumnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Handled || !AllowsColumnHeaderSelection)
            {
                return;
            }

            var point = e.GetCurrentPoint(this);
            var isTouchLike = e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen;
            if (!point.Properties.IsLeftButtonPressed && !(isTouchLike && AllowTouchDragSelection))
            {
                return;
            }

            var header = e.Source as DataGridColumnHeader;
            if (header == null || header.OwningGrid != this)
            {
                if (e.Source is Visual sourceVisual)
                {
                    header = sourceVisual.GetSelfAndVisualAncestors()
                        .OfType<DataGridColumnHeader>()
                        .FirstOrDefault();
                }

                if (header == null || header.OwningGrid != this)
                {
                    return;
                }
            }

            if (header.IsResizing)
            {
                return;
            }

            var column = header.OwningColumn;
            if (column == null || !column.IsVisible || DataConnection == null || DataConnection.Count == 0)
            {
                return;
            }

            var dragHandleHit = ColumnDragHandle == DataGridColumnDragHandle.DragHandle &&
                                header.IsDragGripHit(e.Source, e);
            if (dragHandleHit)
            {
                header.SuppressSortOnClick();
            }

            var anchorSlot = SlotFromRowIndex(0);
            _successfullyUpdatedSelection = false;
            if (SuppressSortOnColumnHeaderSelection)
            {
                header.SuppressSortOnClick();
            }

            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out var ctrl, out _);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (dragHandleHit)
            {
                ctrl = false;
                shift = false;
            }

            if (SelectionMode == DataGridSelectionMode.Single)
            {
                ctrl = false;
                shift = false;
            }

            if (SelectionMode == DataGridSelectionMode.Extended && ctrl && !shift && IsColumnSelected(column))
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Pointer, e);
                List<DataGridCellInfo> proposed = BuildNormalizedCellSelectionProposal(_selectedCellsView);
                for (int rowIndex = 0; rowIndex < DataConnection.Count; rowIndex++)
                {
                    if (!IsRowHeaderSelectionActive(rowIndex))
                    {
                        RemoveCellFromProposal(proposed, rowIndex, column.Index);
                    }
                }
                DataGridSelectionAnchorInfo proposedAnchor = proposed.Count == 0
                    ? DataGridSelectionAnchorInfo.Unset
                    : GetCurrentSelectionAnchorInfo();
                if (!TryPreviewCellSelection(proposed, CurrentCell, proposedAnchor))
                {
                    e.Handled = true;
                    return;
                }
                if (EditingRow != null && anchorSlot != EditingRow.Slot && !CommitEdit(DataGridEditingUnit.Row, true))
                {
                    return;
                }

                using var commit = BeginSelectionCommit();
                var removed = new List<DataGridCellInfo>();
                var rowCount = DataConnection.Count;

                _selectedColumnHeaderIndices.Remove(column.Index);

                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    var slot = SlotFromRowIndex(rowIndex);
                    if (slot < 0 || IsGroupSlot(slot))
                    {
                        continue;
                    }

                    if (IsRowHeaderSelectionActive(rowIndex))
                    {
                        continue;
                    }

                    RemoveCellSelectionFromSlot(slot, column.Index, removed);
                }

                if (removed.Count > 0)
                {
                    RaiseSelectedCellsChanged(Array.Empty<DataGridCellInfo>(), removed);
                    _successfullyUpdatedSelection = true;
                }

                if (_selectedCellsView.Count == 0)
                {
                    _cellAnchor = new DataGridCellCoordinates(-1, -1);
                    _columnHeaderAnchorIndex = -1;
                    _rowHeaderAnchorIndex = -1;
                    AnchorSlot = -1;
                }

                _columnHeaderAnchorIndex = column.Index;
                e.Handled = true;
                return;
            }

            var currentDisplayIndex = column.DisplayIndex;
            var anchorDisplayIndex = currentDisplayIndex;
            if (SelectionMode == DataGridSelectionMode.Extended && shift && _columnHeaderAnchorIndex >= 0)
            {
                var resolvedAnchorDisplay = GetColumnDisplayIndex(_columnHeaderAnchorIndex);
                if (resolvedAnchorDisplay >= 0)
                {
                    anchorDisplayIndex = resolvedAnchorDisplay;
                }
            }

            var startDisplayIndex = Math.Min(anchorDisplayIndex, currentDisplayIndex);
            var endDisplayIndex = Math.Max(anchorDisplayIndex, currentDisplayIndex);
            var append = SelectionMode == DataGridSelectionMode.Extended && ctrl;

            if (ApplyColumnHeaderSelectionRange(startDisplayIndex, endDisplayIndex, append, DataGridSelectionChangeSource.Pointer, e))
            {
                _successfullyUpdatedSelection = true;
                if (!shift)
                {
                    _columnHeaderAnchorIndex = column.Index;
                }
                if (anchorSlot >= 0 && !IsGroupSlot(anchorSlot))
                {
                    var startColumnIndex = GetColumnIndexFromDisplayIndex(startDisplayIndex);
                    if (startColumnIndex < 0)
                    {
                        startColumnIndex = column.Index;
                    }

                    _cellAnchor = new DataGridCellCoordinates(startColumnIndex, anchorSlot);
                    SetCurrentCellCore(startColumnIndex, anchorSlot, commitEdit: true, endRowEdit: false);
                }

                SetColumnHeaderSelectionRange(startDisplayIndex, endDisplayIndex, append);
            }

            if (_successfullyUpdatedSelection &&
                (!CanUserReorderColumns || ColumnDragHandle == DataGridColumnDragHandle.DragHandle) &&
                !dragHandleHit)
            {
                TryBeginColumnHeaderSelectionDrag(e, column.Index);
            }

            e.Handled = true;
        }

        internal bool TryHandleRowHeaderSelection(PointerPressedEventArgs e, int slot, bool ignoreModifiers = false)
        {
            if (!AllowsRowHeaderSelection || SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return false;
            }

            if (DataConnection == null || ColumnsItemsInternal == null || ColumnsItemsInternal.Count == 0)
            {
                return false;
            }

            if (IsSlotOutOfBounds(slot) || IsGroupSlot(slot))
            {
                return false;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return false;
            }

            _successfullyUpdatedSelection = false;

            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out var ctrl, out _);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (ignoreModifiers)
            {
                ctrl = false;
                shift = false;
            }

            if (SelectionMode == DataGridSelectionMode.Single)
            {
                ctrl = false;
                shift = false;
            }

            if (SelectionMode == DataGridSelectionMode.Extended && ctrl && !shift &&
                IsRowFullySelectedByCells(rowIndex))
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Pointer, e);
                List<DataGridCellInfo> proposed = BuildNormalizedCellSelectionProposal(_selectedCellsView);
                for (int columnIndex = 0; columnIndex < ColumnsItemsInternal.Count; columnIndex++)
                {
                    if (!IsColumnHeaderSelectionActive(columnIndex))
                    {
                        RemoveCellFromProposal(proposed, rowIndex, columnIndex);
                    }
                }
                DataGridSelectionAnchorInfo proposedAnchor = proposed.Count == 0
                    ? DataGridSelectionAnchorInfo.Unset
                    : GetCurrentSelectionAnchorInfo();
                if (!TryPreviewCellSelection(proposed, CurrentCell, proposedAnchor))
                {
                    return true;
                }
                if (EditingRow != null && slot != EditingRow.Slot && !CommitEdit(DataGridEditingUnit.Row, true))
                {
                    return false;
                }

                using var commit = BeginSelectionCommit();
                var removed = new List<DataGridCellInfo>();

                _selectedRowHeaderIndices.Remove(SelectedRowKeyAt(rowIndex));

                for (var columnIndex = 0; columnIndex < ColumnsItemsInternal.Count; columnIndex++)
                {
                    var column = ColumnsItemsInternal[columnIndex];
                    if (column == null || !column.IsVisible)
                    {
                        continue;
                    }

                    if (IsColumnHeaderSelectionActive(columnIndex))
                    {
                        continue;
                    }

                    RemoveCellSelectionFromSlot(slot, columnIndex, removed);
                }

                if (removed.Count > 0)
                {
                    RaiseSelectedCellsChanged(Array.Empty<DataGridCellInfo>(), removed);
                    _successfullyUpdatedSelection = true;
                }

                if (_selectedCellsView.Count == 0)
                {
                    _cellAnchor = new DataGridCellCoordinates(-1, -1);
                    _columnHeaderAnchorIndex = -1;
                    _rowHeaderAnchorIndex = -1;
                    AnchorSlot = -1;
                }

                _rowHeaderAnchorIndex = rowIndex;
                _successfullyUpdatedSelection = true;
                return true;
            }

            var startRow = rowIndex;
            var endRow = rowIndex;

            if (SelectionMode == DataGridSelectionMode.Extended && shift && _rowHeaderAnchorIndex >= 0)
            {
                startRow = Math.Min(_rowHeaderAnchorIndex, rowIndex);
                endRow = Math.Max(_rowHeaderAnchorIndex, rowIndex);
            }

            var range = new DataGridCellRange(startRow, endRow, 0, ColumnsItemsInternal.Count - 1);
            var append = SelectionMode == DataGridSelectionMode.Extended && ctrl;

            if (ApplyCellSelectionRange(range, append, DataGridSelectionChangeSource.Pointer, e))
            {
                _successfullyUpdatedSelection = true;
                if (!shift)
                {
                    _rowHeaderAnchorIndex = rowIndex;
                }
                var anchorSlot = SlotFromRowIndex(startRow);
                if (anchorSlot >= 0 && !IsGroupSlot(anchorSlot))
                {
                    var anchorColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                    var anchorColumnIndex = anchorColumn?.Index ?? range.StartColumn;
                    _cellAnchor = new DataGridCellCoordinates(anchorColumnIndex, anchorSlot);
                    SetCurrentCellCore(anchorColumnIndex, anchorSlot, commitEdit: true, endRowEdit: false);
                }

                SetRowHeaderSelectionRange(startRow, endRow, append);
                return true;
            }

            // The row-header selection path owned the gesture even when the proposal was vetoed.
            return true;
        }

        private void TopLeftCornerHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                SelectAll();
            }
            else
            {
                SelectAllCells();
            }

            e.Handled = true;
        }
    }
}
