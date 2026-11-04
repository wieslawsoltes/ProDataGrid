// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Controls.DataGridInteractions;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Utilities;
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
        private bool _isDraggingSelection;
        private bool _isRowSelectionDragging;
        private IPointer _dragPointer;
        private int? _dragPointerId;
        private Point? _dragStartPoint;
        private Point? _dragLastPoint;
        private KeyModifiers _dragLastModifiers;
        private RoutedEventArgs _dragTriggerEvent;
        private DispatcherTimer _dragAutoScrollTimer;
        private int _dragAutoScrollDirectionX;
        private int _dragAutoScrollDirectionY;
        private int _dragAnchorSlot = -1;
        private DataGridCellPosition? _dragAnchorCell;
        private int _dragLastSlot = -1;
        private int _dragLastColumnIndex = -1;
        private bool _dragCapturePending;
        private HeaderSelectionDragMode _headerSelectionDragMode;
        private int _dragHeaderAnchorRowIndex = -1;
        private int _dragHeaderAnchorColumnIndex = -1;

        internal bool IsSelectionDragActive => _isDraggingSelection;

        private enum HeaderSelectionDragMode
        {
            None,
            RowHeader,
            ColumnHeader
        }

        internal bool IsHeaderSelectionDragActive => _headerSelectionDragMode != HeaderSelectionDragMode.None;

        internal void TryBeginSelectionDrag(PointerPressedEventArgs e, int columnIndex, bool startDragging)
        {
            if (!startDragging)
            {
                return;
            }

            if ((e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen) && !AllowTouchDragSelection)
            {
                return;
            }

            if (!IsEnabled)
            {
                return;
            }

            if (e.Pointer.Captured is Visual captured &&
                !captured.GetSelfAndVisualAncestors().Contains(this))
            {
                return;
            }

            EndSelectionDrag();

            _isDraggingSelection = true;
            _isRowSelectionDragging = ShouldDragSelectRows(columnIndex);
            _dragPointer = e.Pointer;
            _dragPointerId = e.Pointer.Id;
            _dragStartPoint = e.GetPosition(this);
            _dragLastPoint = _dragStartPoint;
            _dragLastModifiers = e.KeyModifiers;
            _dragTriggerEvent = e;
            _dragLastSlot = CurrentSlot;
            _dragLastColumnIndex = CurrentColumnIndex;
            _dragAnchorSlot = AnchorSlot != -1 ? AnchorSlot : CurrentSlot;
            _dragAnchorCell = null;
            // Defer capture until drag threshold so taps/double taps keep original source.
            _dragCapturePending = true;

            if (!_isRowSelectionDragging)
            {
                DataGridCellPosition? existingAnchor = null;
                if (_cellAnchor.Slot != -1 && _cellAnchor.ColumnIndex >= 0)
                {
                    var anchorRowIndex = RowIndexFromSlot(_cellAnchor.Slot);
                    if (anchorRowIndex >= 0)
                    {
                        existingAnchor = new DataGridCellPosition(anchorRowIndex, _cellAnchor.ColumnIndex);
                    }
                }

                var currentRowIndex = RowIndexFromSlot(CurrentSlot);
                var currentColumnIndex = CurrentColumnIndex;
                if (currentRowIndex < 0 || currentColumnIndex < 0)
                {
                    if (existingAnchor.HasValue)
                    {
                        currentRowIndex = existingAnchor.Value.RowIndex;
                        currentColumnIndex = existingAnchor.Value.ColumnIndex;
                    }
                }

                if (currentRowIndex >= 0 && currentColumnIndex >= 0)
                {
                    var currentCell = new DataGridCellPosition(currentRowIndex, currentColumnIndex);
                    var model = RangeInteractionModel;
                    _dragAnchorCell = model != null
                        ? model.ResolveSelectionAnchor(new DataGridSelectionAnchorContext(this, existingAnchor, currentCell, e.KeyModifiers))
                        : existingAnchor ?? currentCell;
                }
            }

            // Capture happens once the drag threshold is met.
        }

        internal void TryBeginRowHeaderSelectionDrag(PointerPressedEventArgs e, int rowIndex)
        {
            if (SelectionMode != DataGridSelectionMode.Extended)
            {
                return;
            }

            if (rowIndex < 0)
            {
                return;
            }

            BeginHeaderSelectionDrag(e, HeaderSelectionDragMode.RowHeader, rowIndex, columnIndex: -1);
        }

        internal void TryBeginColumnHeaderSelectionDrag(PointerPressedEventArgs e, int columnIndex)
        {
            if (SelectionMode != DataGridSelectionMode.Extended)
            {
                return;
            }

            if (columnIndex < 0)
            {
                return;
            }

            BeginHeaderSelectionDrag(e, HeaderSelectionDragMode.ColumnHeader, rowIndex: -1, columnIndex);
        }

        private void BeginHeaderSelectionDrag(PointerPressedEventArgs e, HeaderSelectionDragMode mode, int rowIndex, int columnIndex)
        {
            if ((e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen) && !AllowTouchDragSelection)
            {
                return;
            }

            if (!IsEnabled)
            {
                return;
            }

            if (e.Pointer.Captured is Visual captured &&
                !captured.GetSelfAndVisualAncestors().Contains(this))
            {
                return;
            }

            EndSelectionDrag();

            _isDraggingSelection = true;
            _isRowSelectionDragging = false;
            _headerSelectionDragMode = mode;
            _dragHeaderAnchorRowIndex = rowIndex;
            _dragHeaderAnchorColumnIndex = columnIndex;
            _dragPointer = e.Pointer;
            _dragPointerId = e.Pointer.Id;
            _dragStartPoint = e.GetPosition(this);
            _dragLastPoint = _dragStartPoint;
            _dragLastModifiers = e.KeyModifiers;
            _dragTriggerEvent = e;
            _dragLastSlot = rowIndex >= 0 ? SlotFromRowIndex(rowIndex) : CurrentSlot;
            _dragLastColumnIndex = columnIndex >= 0 ? columnIndex : CurrentColumnIndex;
            _dragAnchorSlot = AnchorSlot != -1 ? AnchorSlot : CurrentSlot;
            _dragAnchorCell = null;
            _dragCapturePending = true;
        }

        private bool ShouldDragSelectRows(int columnIndex)
        {
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                return true;
            }

            return columnIndex < 0 && AllowsRowHeaderSelection;
        }

        private void EndSelectionDrag()
        {
            var wasDragging = _isDraggingSelection;
            var lastPoint = _dragLastPoint;
            StopDragAutoScroll();

            if (_dragPointer != null && ReferenceEquals(_dragPointer.Captured, this))
            {
                _dragPointer.Capture(null);
            }

            _dragPointer = null;
            _dragPointerId = null;
            _isDraggingSelection = false;
            _isRowSelectionDragging = false;
            _dragCapturePending = false;
            _dragStartPoint = null;
            _dragLastPoint = null;
            _dragLastModifiers = KeyModifiers.None;
            _dragTriggerEvent = null;
            _dragLastSlot = -1;
            _dragLastColumnIndex = -1;
            _dragAnchorSlot = -1;
            _dragAnchorCell = null;
            _headerSelectionDragMode = HeaderSelectionDragMode.None;
            _dragHeaderAnchorRowIndex = -1;
            _dragHeaderAnchorColumnIndex = -1;

            if (wasDragging)
            {
                if (lastPoint.HasValue)
                {
                    if (Bounds.Contains(lastPoint.Value))
                    {
                        _lastPointerPosition = lastPoint;
                    }
                    else
                    {
                        _lastPointerPosition = null;
                    }
                }

                RequestPointerOverRefresh();
            }
        }

        private void DataGrid_DragSelectionPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDraggingSelection || _dragPointerId != e.Pointer.Id)
            {
                return;
            }

            var current = e.GetCurrentPoint(this);
            var isTouchLike = e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen;
            if (!current.Properties.IsLeftButtonPressed && !(isTouchLike && AllowTouchDragSelection))
            {
                EndSelectionDrag();
                return;
            }

            if (_dragCapturePending && _dragStartPoint.HasValue)
            {
                var model = RangeInteractionModel;
                var thresholdMet = model != null
                    ? model.IsSelectionDragThresholdMet(_dragStartPoint.Value, current.Position)
                    : Math.Abs(current.Position.X - _dragStartPoint.Value.X) >= 4 ||
                      Math.Abs(current.Position.Y - _dragStartPoint.Value.Y) >= 4;
                if (!thresholdMet)
                {
                    return;
                }

                _dragLastModifiers = e.KeyModifiers;
                _dragTriggerEvent = e;
                if (!UpdateSelectionForDrag(current.Position, e.KeyModifiers, force: false))
                {
                    // The first drag proposal is still transactional. A veto (or an invalid
                    // target) must not capture the pointer or leave a latent drag/autoscroll
                    // session behind.
                    EndSelectionDrag();
                    return;
                }

                _dragCapturePending = false;
                if (_dragPointer != null && _dragPointer.Captured == null)
                {
                    _dragPointer.Capture(this);
                }

                e.Handled = true;
                return;
            }

            _dragLastModifiers = e.KeyModifiers;
            _dragTriggerEvent = e;

            if (UpdateSelectionForDrag(current.Position, e.KeyModifiers, force: false))
            {
                e.Handled = true;
            }
        }

        private void DataGrid_DragSelectionPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isDraggingSelection || _dragPointerId != e.Pointer.Id)
            {
                return;
            }

            EndSelectionDrag();
        }

        private void DataGrid_DragSelectionPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (!_isDraggingSelection || _dragPointerId != e.Pointer.Id)
            {
                return;
            }

            EndSelectionDrag();
        }

        private bool UpdateSelectionForDrag(Point position, KeyModifiers modifiers, bool force)
        {
            if (!force && _dragLastPoint.HasValue && ArePointsClose(position, _dragLastPoint.Value))
            {
                UpdateDragAutoScroll(position);
                return false;
            }

            var updated = _headerSelectionDragMode != HeaderSelectionDragMode.None
                ? UpdateHeaderSelectionForDrag(position, modifiers)
                : _isRowSelectionDragging
                    ? UpdateRowSelectionForDrag(position, modifiers)
                    : UpdateCellSelectionForDrag(position, modifiers);

            if (updated)
            {
                _dragLastPoint = position;
                UpdateDragAutoScroll(position);
            }
            else
            {
                StopDragAutoScroll();
            }
            return updated;
        }

        private bool UpdateHeaderSelectionForDrag(Point position, KeyModifiers modifiers)
        {
            if (DataConnection == null || DataConnection.Count == 0 || ColumnsItemsInternal == null || ColumnsItemsInternal.Count == 0)
            {
                return false;
            }

            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out _);
            var append = SelectionMode == DataGridSelectionMode.Extended && ctrl;

            if (_headerSelectionDragMode == HeaderSelectionDragMode.RowHeader)
            {
                if (!TryGetDragRowTarget(position, out var slot))
                {
                    return false;
                }

                if (slot == _dragLastSlot)
                {
                    // A force tick can legitimately resolve to the same edge row while the
                    // viewport continues scrolling. This is an accepted no-op, not a veto.
                    return true;
                }

                var rowIndex = RowIndexFromSlot(slot);
                if (rowIndex < 0)
                {
                    return false;
                }

                var anchorRowIndex = _dragHeaderAnchorRowIndex >= 0 ? _dragHeaderAnchorRowIndex : rowIndex;
                var startRow = Math.Min(anchorRowIndex, rowIndex);
                var endRow = Math.Max(anchorRowIndex, rowIndex);
                var range = new DataGridCellRange(startRow, endRow, 0, ColumnsItemsInternal.Count - 1);

                if (!ApplyCellSelectionRange(
                        range,
                        append,
                        DataGridSelectionChangeSource.Pointer | DataGridSelectionChangeSource.DragInteraction,
                        _dragTriggerEvent))
                {
                    return false;
                }

                SetRowHeaderSelectionRange(startRow, endRow, append);

                var anchorSlot = SlotFromRowIndex(startRow);
                if (anchorSlot >= 0 && !IsGroupSlot(anchorSlot))
                {
                    var anchorColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                    var anchorColumnIndex = anchorColumn?.Index ?? range.StartColumn;
                    SetCurrentCellCore(anchorColumnIndex, anchorSlot, commitEdit: true, endRowEdit: false);
                }

                _dragLastSlot = slot;
                _rowHeaderAnchorIndex = anchorRowIndex;
                return true;
            }

            if (_headerSelectionDragMode == HeaderSelectionDragMode.ColumnHeader)
            {
                if (!TryGetColumnIndexFromPoint(position, out var columnIndex))
                {
                    return false;
                }

                if (columnIndex == _dragLastColumnIndex)
                {
                    return true;
                }

                var anchorDisplayIndex = _dragHeaderAnchorColumnIndex >= 0
                    ? GetColumnDisplayIndex(_dragHeaderAnchorColumnIndex)
                    : GetColumnDisplayIndex(columnIndex);
                if (anchorDisplayIndex < 0)
                {
                    anchorDisplayIndex = GetColumnDisplayIndex(columnIndex);
                }

                var currentDisplayIndex = GetColumnDisplayIndex(columnIndex);
                if (currentDisplayIndex < 0)
                {
                    return false;
                }

                var startDisplayIndex = Math.Min(anchorDisplayIndex, currentDisplayIndex);
                var endDisplayIndex = Math.Max(anchorDisplayIndex, currentDisplayIndex);

                if (!ApplyColumnHeaderSelectionRange(
                        startDisplayIndex,
                        endDisplayIndex,
                        append,
                        DataGridSelectionChangeSource.Pointer | DataGridSelectionChangeSource.DragInteraction,
                        _dragTriggerEvent))
                {
                    return false;
                }

                SetColumnHeaderSelectionRange(startDisplayIndex, endDisplayIndex, append);

                var anchorSlot = SlotFromRowIndex(0);
                if (anchorSlot >= 0 && !IsGroupSlot(anchorSlot))
                {
                    var startColumnIndex = GetColumnIndexFromDisplayIndex(startDisplayIndex);
                    if (startColumnIndex >= 0)
                    {
                        SetCurrentCellCore(startColumnIndex, anchorSlot, commitEdit: true, endRowEdit: false);
                    }
                }

                _dragLastColumnIndex = columnIndex;
                _columnHeaderAnchorIndex = _dragHeaderAnchorColumnIndex >= 0 ? _dragHeaderAnchorColumnIndex : columnIndex;
                return true;
            }

            return false;
        }

        private static bool ArePointsClose(Point left, Point right)
        {
            return MathUtilities.AreClose(left.X, right.X) && MathUtilities.AreClose(left.Y, right.Y);
        }

        private void DeselectRowsOutsideRange(int startSlot, int endSlot)
        {
            if (_selectedItems.Count == 0)
            {
                return;
            }

            var selectedSlots = _selectedItems.GetIndexes().ToList();
            foreach (var slot in selectedSlots)
            {
                if (slot < startSlot || slot > endSlot)
                {
                    SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                }
            }
        }

        private void DeselectRowsInRange(int startSlot, int endSlot)
        {
            if (_selectedItems.Count == 0 || startSlot > endSlot)
            {
                return;
            }

            var selectedSlots = _selectedItems.GetIndexes().ToList();
            foreach (var slot in selectedSlots)
            {
                if (slot >= startSlot && slot <= endSlot)
                {
                    SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                }
            }
        }

        private void DeselectRowsForCtrlDrag(int previousSlot, int rangeStart, int rangeEnd)
        {
            if (previousSlot < 0)
            {
                return;
            }

            if (previousSlot < rangeStart)
            {
                DeselectRowsInRange(previousSlot, rangeStart - 1);
            }
            else if (previousSlot > rangeEnd)
            {
                DeselectRowsInRange(rangeEnd + 1, previousSlot);
            }
        }

        private bool UpdateRowSelectionForDrag(Point position, KeyModifiers modifiers)
        {
            if (!TryGetDragRowTarget(position, out var slot))
            {
                return false;
            }

            if (slot == _dragLastSlot)
            {
                return true;
            }

            var previousSlot = _dragLastSlot;

            using var selectionScope = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.Pointer | DataGridSelectionChangeSource.DragInteraction,
                _dragTriggerEvent);
            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out _);
            int anchorSlot = _dragAnchorSlot != -1
                ? _dragAnchorSlot
                : AnchorSlot != -1
                    ? AnchorSlot
                    : slot;
            int rangeStart = Math.Min(anchorSlot, slot);
            int rangeEnd = Math.Max(anchorSlot, slot);
            SelectionCommitScope selectionCommit = default;
            if (HasSelectionChangingHandlers)
            {
                HashSet<int> proposedRows = CaptureSelectedRowIndexes();
                if (!ctrl)
                {
                    proposedRows.Clear();
                }
                AddSlotRange(proposedRows, rangeStart, rangeEnd);
                if (ctrl && previousSlot >= 0)
                {
                    if (previousSlot < rangeStart)
                    {
                        RemoveSlotRange(proposedRows, previousSlot, rangeStart - 1);
                    }
                    else if (previousSlot > rangeEnd)
                    {
                        RemoveSlotRange(proposedRows, rangeEnd + 1, previousSlot);
                    }
                }

                int proposedColumnIndex = CurrentColumnIndex >= 0
                    ? CurrentColumnIndex
                    : FirstDisplayedNonFillerColumnIndex;
                if (!TryPreviewRowSet(
                        proposedRows,
                        CreateCellInfo(proposedColumnIndex, slot),
                        CreateAnchorInfo(anchorSlot, proposedColumnIndex)))
                {
                    return false;
                }

                selectionCommit = BeginSelectionCommit();
            }

            using var selectionTransaction = selectionCommit;
            _noSelectionChangeCount++;
            try
            {
                var selectionAction = ctrl
                    ? DataGridSelectionAction.None
                    : DataGridSelectionAction.SelectFromAnchorToCurrent;

                if (!UpdateSelectionAndCurrency(-1, slot, selectionAction, scrollIntoView: false))
                {
                    return false;
                }

                if (SelectionMode == DataGridSelectionMode.Extended)
                {
                    if (ctrl)
                    {
                        SetRowsSelection(rangeStart, rangeEnd);
                        DeselectRowsForCtrlDrag(previousSlot, rangeStart, rangeEnd);
                    }
                    else
                    {
                        DeselectRowsOutsideRange(rangeStart, rangeEnd);
                    }

                    if (anchorSlot >= 0 && !GetRowSelection(anchorSlot))
                    {
                        SetRowSelection(anchorSlot, isSelected: true, setAnchorSlot: false);
                    }
                }

                _dragLastSlot = slot;
                return true;
            }
            finally
            {
                NoSelectionChangeCount--;
            }
        }

        private bool UpdateCellSelectionForDrag(Point position, KeyModifiers modifiers)
        {
            if (!TryGetDragCellTarget(position, out var slot, out var columnIndex))
            {
                return false;
            }

            if (slot == _dragLastSlot && columnIndex == _dragLastColumnIndex)
            {
                return true;
            }

            using var _ = BeginSelectionChangeScope(
                DataGridSelectionChangeSource.Pointer | DataGridSelectionChangeSource.DragInteraction,
                _dragTriggerEvent);
            _successfullyUpdatedSelection = false;
            UpdateCellSelectionForDragCore(slot, columnIndex, modifiers);
            if (_successfullyUpdatedSelection)
            {
                _dragLastSlot = slot;
                _dragLastColumnIndex = columnIndex;
            }
            return _successfullyUpdatedSelection;
        }

        private bool TryGetDragRowTarget(Point point, out int slot)
        {
            slot = -1;

            if (_rowsPresenter != null &&
                DisplayData?.HasVirtualScrollingElements == true)
            {
                Point virtualPresenterPoint = this.TranslatePoint(point, _rowsPresenter) ?? point;
                return _rowsPresenter.TryGetLightweightVirtualRowSlot(virtualPresenterPoint.Y, out slot) &&
                    slot >= 0 &&
                    slot < SlotCount &&
                    !IsGroupSlot(slot);
            }

            if (TryGetRowFromPoint(point, out var row))
            {
                slot = row.Slot;
                return slot >= 0 && slot < SlotCount && !IsGroupSlot(slot);
            }

            if (_rowsPresenter == null || DisplayData == null)
            {
                return false;
            }

            var presenterPoint = this.TranslatePoint(point, _rowsPresenter) ?? point;
            var rows = DisplayData.GetScrollingRows()
                .OfType<DataGridRow>()
                .Where(r => r.IsVisible)
                .OrderBy(r => r.Bounds.Top)
                .ToList();

            if (rows.Count == 0)
            {
                return false;
            }

            foreach (var candidate in rows)
            {
                if (presenterPoint.Y >= candidate.Bounds.Top && presenterPoint.Y <= candidate.Bounds.Bottom)
                {
                    slot = candidate.Slot;
                    return slot >= 0 && slot < SlotCount && !IsGroupSlot(slot);
                }
            }

            if (presenterPoint.Y < rows[0].Bounds.Top)
            {
                slot = rows[0].Slot;
                return slot >= 0 && slot < SlotCount && !IsGroupSlot(slot);
            }

            if (presenterPoint.Y > rows[rows.Count - 1].Bounds.Bottom)
            {
                slot = rows[rows.Count - 1].Slot;
                return slot >= 0 && slot < SlotCount && !IsGroupSlot(slot);
            }

            return false;
        }

        private bool TryGetDragCellTarget(Point point, out int slot, out int columnIndex)
        {
            slot = -1;
            columnIndex = -1;

            if (!TryGetDragRowTarget(point, out slot))
            {
                return false;
            }

            if (!TryGetColumnIndexFromPoint(point, out columnIndex))
            {
                return false;
            }

            if (IsColumnOutOfBounds(columnIndex))
            {
                return false;
            }

            return true;
        }

        private bool TryGetColumnIndexFromPoint(Point point, out int columnIndex)
        {
            columnIndex = -1;

            var firstColumn = ColumnsInternal?.FirstVisibleNonFillerColumn;
            if (firstColumn == null)
            {
                return false;
            }

            var x = point.X - ActualRowHeaderWidth;
            var frozenLeftWidth = GetVisibleFrozenColumnsWidthLeft();
            var frozenRightWidth = GetVisibleFrozenColumnsWidthRight();
            var rightFrozenStart = frozenRightWidth > 0
                ? Math.Max(0, CellsWidth - frozenRightWidth)
                : double.PositiveInfinity;

            if (x < 0)
            {
                columnIndex = firstColumn.Index;
                return true;
            }

            if (frozenLeftWidth > 0 && x < frozenLeftWidth)
            {
                columnIndex = HitTestFrozenColumns(x, ColumnsInternal.GetVisibleFrozenLeftColumns(), 0);
                if (columnIndex == -1)
                {
                    columnIndex = firstColumn.Index;
                }
                return columnIndex != -1;
            }

            if (x >= rightFrozenStart)
            {
                columnIndex = HitTestFrozenColumns(x, ColumnsInternal.GetVisibleFrozenRightColumns(), rightFrozenStart);
                if (columnIndex != -1)
                {
                    return true;
                }

                var lastColumn = GetLastVisibleNonFillerColumn();
                if (lastColumn != null)
                {
                    columnIndex = lastColumn.Index;
                    return true;
                }

                return false;
            }

            double leftEdge = -HorizontalOffset;
            foreach (var column in ColumnsInternal.GetVisibleScrollingColumns())
            {
                if (column is DataGridFillerColumn)
                {
                    leftEdge += column.ActualWidth;
                    continue;
                }

                var rightEdge = leftEdge + column.ActualWidth;
                if (x >= leftEdge && x < rightEdge)
                {
                    columnIndex = column.Index;
                    return true;
                }
                leftEdge = rightEdge;
            }

            var fallback = GetLastVisibleNonFillerColumn();
            if (fallback != null)
            {
                columnIndex = fallback.Index;
                return true;
            }

            return false;
        }

        private static int HitTestFrozenColumns(double x, IEnumerable<DataGridColumn> columns, double start)
        {
            double leftEdge = start;
            foreach (var column in columns)
            {
                if (column is DataGridFillerColumn)
                {
                    leftEdge += column.ActualWidth;
                    continue;
                }

                var rightEdge = leftEdge + column.ActualWidth;
                if (x >= leftEdge && x < rightEdge)
                {
                    return column.Index;
                }
                leftEdge = rightEdge;
            }

            return -1;
        }

        private DataGridColumn GetLastVisibleNonFillerColumn()
        {
            var lastColumn = ColumnsInternal?.LastVisibleColumn;
            if (lastColumn == null)
            {
                return null;
            }

            if (lastColumn is DataGridFillerColumn)
            {
                lastColumn = ColumnsInternal.GetPreviousVisibleNonFillerColumn(lastColumn);
            }

            return lastColumn is DataGridFillerColumn ? null : lastColumn;
        }

        private void UpdateCellSelectionForDragCore(int slot, int columnIndex, KeyModifiers modifiers)
        {
            if (IsSlotOutOfBounds(slot) || DataConnection == null)
            {
                return;
            }

            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out _);

            SelectionCommitScope selectionCommit = default;
            if (HasSelectionChangingHandlers)
            {
                int targetRowIndex = RowIndexFromSlot(slot);
                List<DataGridCellInfo> proposed;
                DataGridSelectionAnchorInfo proposedAnchor = GetCurrentSelectionAnchorInfo();
                if (SelectionMode == DataGridSelectionMode.Single)
                {
                    proposed = BuildCellSelectionProposal(
                        append: false,
                        targetRowIndex,
                        targetRowIndex,
                        columnIndex,
                        columnIndex,
                        columnIndexesAreDisplayIndexes: false);
                }
                else if (_dragAnchorCell.HasValue || _cellAnchor.Slot != -1)
                {
                    DataGridCellPosition? anchorCell = _dragAnchorCell;
                    if (!anchorCell.HasValue && _cellAnchor.Slot != -1 && _cellAnchor.ColumnIndex >= 0)
                    {
                        int resolvedAnchorRowIndex = RowIndexFromSlot(_cellAnchor.Slot);
                        if (resolvedAnchorRowIndex >= 0)
                        {
                            anchorCell = new DataGridCellPosition(resolvedAnchorRowIndex, _cellAnchor.ColumnIndex);
                        }
                    }

                    proposed = BuildNormalizedCellSelectionProposal(_selectedCellsView);
                    if (anchorCell.HasValue &&
                        TryGetSelectionDisplayIndexes(anchorCell.Value.ColumnIndex, columnIndex, out int anchorDisplayIndex, out int targetDisplayIndex))
                    {
                        var selectionAnchorCell = new DataGridCellPosition(anchorCell.Value.RowIndex, anchorDisplayIndex);
                        var targetCell = new DataGridCellPosition(targetRowIndex, targetDisplayIndex);
                        DataGridCellRange range = RangeInteractionModel != null
                            ? RangeInteractionModel.BuildSelectionRange(new DataGridSelectionRangeContext(this, selectionAnchorCell, targetCell, modifiers))
                            : new DataGridCellRange(
                                Math.Min(anchorCell.Value.RowIndex, targetRowIndex),
                                Math.Max(anchorCell.Value.RowIndex, targetRowIndex),
                                Math.Min(anchorDisplayIndex, targetDisplayIndex),
                                Math.Max(anchorDisplayIndex, targetDisplayIndex));
                        if (!ctrl)
                        {
                            proposed.Clear();
                        }
                        else if (_dragLastSlot >= 0 && _dragLastColumnIndex >= 0)
                        {
                            int previousRowIndex = RowIndexFromSlot(_dragLastSlot);
                            int previousDisplayIndex = GetColumnDisplayIndex(_dragLastColumnIndex);
                            RemoveDisplayRangeFromProposal(
                                proposed,
                                anchorCell.Value.RowIndex,
                                previousRowIndex,
                                anchorDisplayIndex,
                                previousDisplayIndex);
                        }

                        List<DataGridCellInfo> rangeCells = BuildCellSelectionProposal(
                            append: false,
                            range.StartRow,
                            range.EndRow,
                            range.StartColumn,
                            range.EndColumn,
                            columnIndexesAreDisplayIndexes: true);
                        AddCellsToProposal(proposed, rangeCells);
                    }
                }
                else
                {
                    proposed = BuildCellSelectionProposal(
                        append: ctrl,
                        targetRowIndex,
                        targetRowIndex,
                        columnIndex,
                        columnIndex,
                        columnIndexesAreDisplayIndexes: false);
                }

                if (!TryPreviewCellSelection(proposed, CreateCellInfo(columnIndex, slot), proposedAnchor))
                {
                    return;
                }

                selectionCommit = BeginSelectionCommit();
            }

            using var selectionTransaction = selectionCommit;
            if (EditingRow != null && slot != EditingRow.Slot && !CommitEdit(DataGridEditingUnit.Row, true))
            {
                return;
            }

            var added = new List<DataGridCellInfo>();
            var removed = new List<DataGridCellInfo>();

            if (SelectionMode == DataGridSelectionMode.Single)
            {
                if (_selectedCellsView.Count > 0)
                {
                    removed.AddRange(_selectedCellsView);
                }

                ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                AddSingleCellSelection(columnIndex, slot, added);
            }
            else if (_dragAnchorCell.HasValue || _cellAnchor.Slot != -1)
            {
                DataGridCellPosition? anchorCell = _dragAnchorCell;
                if (!anchorCell.HasValue && _cellAnchor.Slot != -1 && _cellAnchor.ColumnIndex >= 0)
                {
                    var resolvedAnchorRowIndex = RowIndexFromSlot(_cellAnchor.Slot);
                    if (resolvedAnchorRowIndex >= 0)
                    {
                        anchorCell = new DataGridCellPosition(resolvedAnchorRowIndex, _cellAnchor.ColumnIndex);
                    }
                }

                if (!anchorCell.HasValue)
                {
                    return;
                }

                int anchorRowIndex = anchorCell.Value.RowIndex;
                int targetRowIndex = RowIndexFromSlot(slot);
                if (anchorRowIndex >= 0 &&
                    targetRowIndex >= 0 &&
                    TryGetSelectionDisplayIndexes(anchorCell.Value.ColumnIndex, columnIndex, out var anchorDisplayIndex, out var targetDisplayIndex))
                {
                    var selectionAnchorCell = new DataGridCellPosition(anchorRowIndex, anchorDisplayIndex);
                    var targetCell = new DataGridCellPosition(targetRowIndex, targetDisplayIndex);
                    var model = RangeInteractionModel;
                    var range = model != null
                        ? model.BuildSelectionRange(new DataGridSelectionRangeContext(this, selectionAnchorCell, targetCell, modifiers))
                        : new DataGridCellRange(
                            Math.Min(anchorRowIndex, targetRowIndex),
                            Math.Max(anchorRowIndex, targetRowIndex),
                            Math.Min(anchorDisplayIndex, targetDisplayIndex),
                            Math.Max(anchorDisplayIndex, targetDisplayIndex));

                    if (!ctrl)
                    {
                        removed.AddRange(_selectedCellsView);
                        ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                    }
                    else
                    {
                        RemovePreviousDragCellSelection(anchorRowIndex, anchorDisplayIndex, removed);
                    }

                    SelectCellRangeByDisplayIndexInternal(range.StartRow, range.EndRow, range.StartColumn, range.EndColumn, added);
                }
            }
            else
            {
                if (!ctrl)
                {
                    removed.AddRange(_selectedCellsView);
                    ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                }

                AddSingleCellSelection(columnIndex, slot, added);
            }

            if (added.Count > 0 || removed.Count > 0)
            {
                RaiseSelectedCellsChanged(added, removed);
            }

            _successfullyUpdatedSelection = true;
            if (CurrentSlot != slot || CurrentColumnIndex != columnIndex)
            {
                SetCurrentCellCore(columnIndex, slot, commitEdit: true, endRowEdit: false);
            }
        }

        private void RemovePreviousDragCellSelection(int anchorRowIndex, int anchorDisplayIndex, List<DataGridCellInfo> removedCollector)
        {
            if (_dragLastSlot < 0 || _dragLastColumnIndex < 0)
            {
                return;
            }

            int previousRowIndex = RowIndexFromSlot(_dragLastSlot);
            if (previousRowIndex < 0)
            {
                return;
            }

            var previousDisplayIndex = GetColumnDisplayIndex(_dragLastColumnIndex);
            if (previousDisplayIndex < 0)
            {
                previousDisplayIndex = _dragLastColumnIndex;
            }

            int startRow = Math.Min(anchorRowIndex, previousRowIndex);
            int endRow = Math.Max(anchorRowIndex, previousRowIndex);
            int startDisplay = Math.Min(anchorDisplayIndex, previousDisplayIndex);
            int endDisplay = Math.Max(anchorDisplayIndex, previousDisplayIndex);

            RemoveCellSelectionRangeByDisplayIndex(startRow, endRow, startDisplay, endDisplay, removedCollector);
        }

        private void RemoveCellSelectionRangeByDisplayIndex(int startRowIndex, int endRowIndex, int startDisplayIndex, int endDisplayIndex, List<DataGridCellInfo> removedCollector)
        {
            if (DataConnection == null || ColumnsItemsInternal == null || startRowIndex > endRowIndex)
            {
                return;
            }

            var columnsItems = ColumnsItemsInternal;
            var rowCount = DataConnection.Count;
            if (rowCount <= 0 || columnsItems.Count == 0)
            {
                return;
            }

            var columnIndexes = GetVisibleColumnIndexesInDisplayRange(startDisplayIndex, endDisplayIndex);
            if (columnIndexes.Count == 0)
            {
                return;
            }

            var firstRow = Math.Max(0, startRowIndex);
            var lastRow = Math.Min(endRowIndex, rowCount - 1);
            if (firstRow > lastRow)
            {
                return;
            }

            for (var rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
            {
                var slot = SlotFromRowIndex(rowIndex);
                if (slot < 0 || IsGroupSlot(slot))
                {
                    continue;
                }

                foreach (var columnIndex in columnIndexes)
                {
                    RemoveCellSelectionInternal(rowIndex, columnIndex, removedCollector);
                }

                if (!_selectedCells.TryGetValue(rowIndex, out var remaining) || remaining.Count == 0)
                {
                    SetRowSelection(slot, isSelected: false, setAnchorSlot: false);
                }
            }
        }

        private void UpdateDragAutoScroll(Point point)
        {
            if (!_isDraggingSelection)
            {
                return;
            }

            var model = RangeInteractionModel;
            DataGridAutoScrollDirection direction;
            if (model != null)
            {
                var presenterPoint = _rowsPresenter != null ? this.TranslatePoint(point, _rowsPresenter) : null;
                var presenterSize = _rowsPresenter?.Bounds.Size ?? default;
                var isRowSelection = _isRowSelectionDragging || _headerSelectionDragMode == HeaderSelectionDragMode.RowHeader;
                direction = model.GetAutoScrollDirection(new DataGridAutoScrollContext(
                    this,
                    point,
                    presenterPoint,
                    presenterSize,
                    ActualRowHeaderWidth,
                    CellsWidth,
                    isRowSelection));
            }
            else
            {
                int verticalDirection = 0;
                int horizontalDirection = 0;

                if (_rowsPresenter != null)
                {
                    var presenterPoint = this.TranslatePoint(point, _rowsPresenter) ?? point;
                    var presenterHeight = _rowsPresenter.Bounds.Height;
                    if (presenterPoint.Y < 0)
                    {
                        verticalDirection = -1;
                    }
                    else if (presenterPoint.Y > presenterHeight)
                    {
                        verticalDirection = 1;
                    }
                }
                else
                {
                    if (point.Y < 0)
                    {
                        verticalDirection = -1;
                    }
                    else if (point.Y > Bounds.Height)
                    {
                        verticalDirection = 1;
                    }
                }

                if (!_isRowSelectionDragging)
                {
                    var x = point.X - ActualRowHeaderWidth;
                    if (x < 0)
                    {
                        horizontalDirection = -1;
                    }
                    else if (x > CellsWidth)
                    {
                        horizontalDirection = 1;
                    }
                }

                direction = new DataGridAutoScrollDirection(horizontalDirection, verticalDirection);
            }

            if (!direction.HasScroll)
            {
                StopDragAutoScroll();
                return;
            }

            _dragAutoScrollDirectionX = direction.Horizontal;
            _dragAutoScrollDirectionY = direction.Vertical;
            StartDragAutoScroll();
        }

        private void StartDragAutoScroll()
        {
            if (_dragAutoScrollTimer == null)
            {
                _dragAutoScrollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _dragAutoScrollTimer.Tick += OnDragAutoScrollTick;
            }

            if (!_dragAutoScrollTimer.IsEnabled)
            {
                _dragAutoScrollTimer.Start();
            }
        }

        private void StopDragAutoScroll()
        {
            if (_dragAutoScrollTimer != null)
            {
                _dragAutoScrollTimer.Stop();
            }

            _dragAutoScrollDirectionX = 0;
            _dragAutoScrollDirectionY = 0;
        }

        private void DisposeDragAutoScrollTimer()
        {
            if (_dragAutoScrollTimer == null)
            {
                return;
            }

            _dragAutoScrollTimer.Stop();
            _dragAutoScrollTimer.Tick -= OnDragAutoScrollTick;
            _dragAutoScrollTimer = null;
        }

        private void OnDragAutoScrollTick(object sender, EventArgs e)
        {
            if (!_isDraggingSelection)
            {
                StopDragAutoScroll();
                return;
            }

            if (_dragAutoScrollDirectionY != 0)
            {
                ScrollVerticalForDrag(_dragAutoScrollDirectionY);
            }

            if (_dragAutoScrollDirectionX != 0)
            {
                ScrollHorizontalForDrag(_dragAutoScrollDirectionX);
            }

            if (_dragLastPoint.HasValue)
            {
                UpdateSelectionForDrag(_dragLastPoint.Value, _dragLastModifiers, force: true);
            }
        }

        private void ScrollVerticalForDrag(int direction)
        {
            var scroller = ScrollViewer;
            if (scroller != null)
            {
                if (direction < 0)
                {
                    scroller.LineUp();
                }
                else if (direction > 0)
                {
                    scroller.LineDown();
                }
                return;
            }

            if (DisplayData == null || DataConnection == null || DisplayData.NumDisplayedScrollingElements == 0)
            {
                return;
            }

            var slot = direction < 0
                ? GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot)
                : GetNextVisibleSlot(DisplayData.LastScrollingSlot);

            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return;
            }

            var item = DataConnection.GetDataItem(rowIndex);
            ScrollIntoView(item, null);
        }

        private void ScrollHorizontalForDrag(int direction)
        {
            var scroller = ScrollViewer;
            if (scroller != null)
            {
                if (direction < 0)
                {
                    scroller.LineLeft();
                }
                else if (direction > 0)
                {
                    scroller.LineRight();
                }
                return;
            }

            if (direction < 0)
            {
                var change = GetHorizontalSmallScrollDecrease();
                if (change <= 0)
                {
                    return;
                }

                UpdateHorizontalOffset(Math.Max(0, HorizontalOffset - change));
            }
            else if (direction > 0)
            {
                var change = GetHorizontalSmallScrollIncrease();
                if (change <= 0)
                {
                    return;
                }

                var widthNotVisible = Math.Max(0, ColumnsInternal.VisibleEdgedColumnsWidth - CellsWidth);
                UpdateHorizontalOffset(Math.Min(widthNotVisible, HorizontalOffset + change));
            }
        }
    }
}
