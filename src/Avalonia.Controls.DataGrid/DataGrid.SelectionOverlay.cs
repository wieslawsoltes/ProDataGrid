// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private bool _pendingSelectionOverlayRefresh;
        private bool _selectionOverlayLayoutHooked;
        private bool _selectionOverlayVisible;

        internal void RequestSelectionOverlayRefresh()
        {
            if (_pendingSelectionOverlayRefresh)
            {
                return;
            }

            _pendingSelectionOverlayRefresh = true;
            LayoutUpdated += DataGrid_LayoutUpdatedSelectionOverlayRefresh;
            Dispatcher.UIThread.Post(() =>
            {
                if (!_pendingSelectionOverlayRefresh)
                {
                    return;
                }

                LayoutUpdated -= DataGrid_LayoutUpdatedSelectionOverlayRefresh;
                _pendingSelectionOverlayRefresh = false;
                UpdateSelectionOverlay();
            }, DispatcherPriority.Background);
        }

        private void DataGrid_LayoutUpdatedSelectionOverlayRefresh(object? sender, EventArgs e)
        {
        }

        private void DataGrid_LayoutUpdatedSelectionOverlay(object? sender, EventArgs e)
        {
            if (_selectionOverlayVisible)
            {
                UpdateSelectionOverlay();
            }
        }

        private void UpdateSelectionOverlay()
        {
            if (_selectionOverlay == null || _selectionOutline == null || _fillHandle == null)
            {
                return;
            }

            if (SelectionUnit == DataGridSelectionUnit.FullRow ||
                !TryGetSelectedCellDisplayRange(out var displayRange, out var selectedDisplayIndexes, out var isLogicalContiguousRange))
            {
                SetSelectionOverlayVisible(false);
                return;
            }

            if (!TryGetVisibleSelectionBounds(displayRange, selectedDisplayIndexes, out var bounds, out var isFullyVisible))
            {
                SetSelectionOverlayVisible(false);
                return;
            }

            // Fill currently operates on logical-index rectangles; hide the handle when selection
            // is not representable as a logical contiguous range.
            var showFillHandle = isFullyVisible && isLogicalContiguousRange;
            SetSelectionOverlayVisible(true, showFillHandle);
            Canvas.SetLeft(_selectionOutline, bounds.X);
            Canvas.SetTop(_selectionOutline, bounds.Y);
            _selectionOutline.Width = Math.Max(0, bounds.Width);
            _selectionOutline.Height = Math.Max(0, bounds.Height);

            UpdateFillHandle(bounds);
        }

        private void UpdateFillHandle(Rect bounds)
        {
            if (_fillHandle == null)
            {
                return;
            }

            var handleSize = GetFillHandleSize();
            Canvas.SetLeft(_fillHandle, bounds.Right - (handleSize / 2));
            Canvas.SetTop(_fillHandle, bounds.Bottom - (handleSize / 2));
        }

        private double GetFillHandleSize()
        {
            if (_fillHandle == null)
            {
                return 0;
            }

            if (!double.IsNaN(_fillHandle.Width))
            {
                return _fillHandle.Width;
            }

            if (_fillHandle.Bounds.Width > 0)
            {
                return _fillHandle.Bounds.Width;
            }

            return 6;
        }

        private void SetSelectionOverlayVisible(bool isVisible, bool showFillHandle = true)
        {
            if (_selectionOutline != null)
            {
                _selectionOutline.IsVisible = isVisible;
            }

            if (_fillHandle != null)
            {
                _fillHandle.IsVisible = isVisible && showFillHandle;
            }

            if (_selectionOverlayVisible == isVisible)
            {
                return;
            }

            _selectionOverlayVisible = isVisible;
            if (_selectionOverlayVisible && !_selectionOverlayLayoutHooked)
            {
                LayoutUpdated += DataGrid_LayoutUpdatedSelectionOverlay;
                _selectionOverlayLayoutHooked = true;
            }
            else if (!_selectionOverlayVisible && _selectionOverlayLayoutHooked)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedSelectionOverlay;
                _selectionOverlayLayoutHooked = false;
            }
        }

        private bool TryGetSelectedCellRange(out DataGridCellRange range)
        {
            range = default;

            if (_selectedCellsView.Count == 0)
            {
                return false;
            }

            var minRow = int.MaxValue;
            var maxRow = int.MinValue;
            var minCol = int.MaxValue;
            var maxCol = int.MinValue;
            var found = false;
            var selectedCount = 0;

            foreach (var cell in _selectedCellsView)
            {
                if (!cell.IsValid || cell.RowIndex < 0 || cell.ColumnIndex < 0)
                {
                    continue;
                }

                minRow = Math.Min(minRow, cell.RowIndex);
                maxRow = Math.Max(maxRow, cell.RowIndex);
                minCol = Math.Min(minCol, cell.ColumnIndex);
                maxCol = Math.Max(maxCol, cell.ColumnIndex);
                found = true;
                selectedCount++;
            }

            if (!found)
            {
                return false;
            }

            var expectedCount = (long)(maxRow - minRow + 1) * (maxCol - minCol + 1);
            if (expectedCount != selectedCount)
            {
                return false;
            }

            range = new DataGridCellRange(minRow, maxRow, minCol, maxCol);
            return true;
        }

        private bool TryGetSelectedCellDisplayRange(out DataGridCellRange range, out List<int> selectedDisplayIndexes, out bool isLogicalContiguousRange)
        {
            range = default;
            selectedDisplayIndexes = new List<int>();
            isLogicalContiguousRange = false;

            if (_selectedCellsView.Count == 0 || ColumnsInternal == null || ColumnsItemsInternal == null)
            {
                return false;
            }

            var positions = new HashSet<(int RowIndex, int DisplayIndex)>();
            var minRow = int.MaxValue;
            var maxRow = int.MinValue;
            var minDisplay = int.MaxValue;
            var maxDisplay = int.MinValue;
            var minLogical = int.MaxValue;
            var maxLogical = int.MinValue;

            foreach (var cell in _selectedCellsView)
            {
                if (!cell.IsValid || cell.RowIndex < 0 || cell.ColumnIndex < 0 || cell.ColumnIndex >= ColumnsItemsInternal.Count)
                {
                    continue;
                }

                var column = ColumnsItemsInternal[cell.ColumnIndex];
                if (column == null || !column.IsVisible)
                {
                    return false;
                }

                var displayIndex = GetColumnDisplayIndex(cell.ColumnIndex);
                if (displayIndex < 0)
                {
                    return false;
                }

                if (!positions.Add((cell.RowIndex, displayIndex)))
                {
                    return false;
                }

                minRow = Math.Min(minRow, cell.RowIndex);
                maxRow = Math.Max(maxRow, cell.RowIndex);
                minDisplay = Math.Min(minDisplay, displayIndex);
                maxDisplay = Math.Max(maxDisplay, displayIndex);
                minLogical = Math.Min(minLogical, cell.ColumnIndex);
                maxLogical = Math.Max(maxLogical, cell.ColumnIndex);
            }

            if (positions.Count == 0)
            {
                return false;
            }

            for (var displayIndex = minDisplay; displayIndex <= maxDisplay; displayIndex++)
            {
                var column = ColumnsInternal.GetColumnAtDisplayIndex(displayIndex);
                if (column == null || column is DataGridFillerColumn)
                {
                    return false;
                }

                if (column.IsVisible)
                {
                    selectedDisplayIndexes.Add(displayIndex);
                }
            }

            if (selectedDisplayIndexes.Count == 0)
            {
                return false;
            }

            var expectedCount = (long)(maxRow - minRow + 1) * selectedDisplayIndexes.Count;
            if (expectedCount != positions.Count)
            {
                return false;
            }

            var logicalExpectedCount = (long)(maxRow - minRow + 1) * (maxLogical - minLogical + 1);
            if (logicalExpectedCount == positions.Count)
            {
                isLogicalContiguousRange = true;
            }

            range = new DataGridCellRange(minRow, maxRow, minDisplay, maxDisplay);
            return true;
        }

        private bool TryGetVisibleSelectionBounds(DataGridCellRange displayRange, IReadOnlyList<int> selectedDisplayIndexes, out Rect bounds, out bool isFullyVisible)
        {
            bounds = default;
            isFullyVisible = false;

            if (_selectionOverlay == null || DisplayData == null || ColumnsItemsInternal == null)
            {
                return false;
            }

            if (selectedDisplayIndexes == null || selectedDisplayIndexes.Count == 0)
            {
                return false;
            }

            var startDisplayIndex = selectedDisplayIndexes[0];
            var endDisplayIndex = selectedDisplayIndexes[selectedDisplayIndexes.Count - 1];
            var startColumn = ColumnsInternal?.GetColumnAtDisplayIndex(startDisplayIndex);
            var endColumn = ColumnsInternal?.GetColumnAtDisplayIndex(endDisplayIndex);
            if (startColumn == null || endColumn == null)
            {
                return false;
            }

            var firstSlot = DisplayData.FirstScrollingSlot;
            var lastSlot = DisplayData.LastScrollingSlot;
            if (firstSlot < 0 || lastSlot < firstSlot)
            {
                return false;
            }

            if (DisplayData.HasVirtualScrollingElements &&
                UsesVirtualCellSurface &&
                _rowsPresenter != null)
            {
                return TryGetLightweightVirtualSelectionBounds(
                    displayRange,
                    firstSlot,
                    lastSlot,
                    startColumn,
                    endColumn,
                    out bounds,
                    out isFullyVisible);
            }

            int? topSlot = null;
            int? bottomSlot = null;
            for (var slot = firstSlot; slot <= lastSlot; slot++)
            {
                if (IsGroupSlot(slot))
                {
                    continue;
                }

                var rowIndex = RowIndexFromSlot(slot);
                if (rowIndex < displayRange.StartRow || rowIndex > displayRange.EndRow)
                {
                    continue;
                }

                if (DisplayData.GetDisplayedElement(slot) is not DataGridRow)
                {
                    continue;
                }

                if (topSlot == null)
                {
                    topSlot = slot;
                }

                bottomSlot = slot;
            }

            if (topSlot == null || bottomSlot == null)
            {
                return false;
            }

            if (DisplayData.GetDisplayedElement(topSlot.Value) is not DataGridRow topRow ||
                DisplayData.GetDisplayedElement(bottomSlot.Value) is not DataGridRow bottomRow)
            {
                return false;
            }

            var isVerticallyVisible = topRow.Index == displayRange.StartRow && bottomRow.Index == displayRange.EndRow;

            var startCol = startColumn.Index;
            var endCol = endColumn.Index;
            if (UsesVirtualCellSurface && _rowsPresenter != null &&
                _rowsPresenter.TryGetVirtualCellBounds(topRow, startColumn, out Rect topLeftBounds) &&
                _rowsPresenter.TryGetVirtualCellBounds(bottomRow, endColumn, out Rect bottomRightBounds))
            {
                Point? topLeftPoint = _rowsPresenter.TranslatePoint(topLeftBounds.TopLeft, _selectionOverlay);
                Point? bottomRightPoint = _rowsPresenter.TranslatePoint(bottomRightBounds.BottomRight, _selectionOverlay);
                if (topLeftPoint == null || bottomRightPoint == null)
                {
                    return false;
                }

                bounds = new Rect(topLeftPoint.Value, bottomRightPoint.Value);
                const double virtualTolerance = 0.5;
                Rect virtualOverlayBounds = _selectionOverlay.Bounds;
                isFullyVisible = isVerticallyVisible &&
                    bounds.Left >= -virtualTolerance &&
                    bounds.Top >= -virtualTolerance &&
                    bounds.Right <= virtualOverlayBounds.Width + virtualTolerance &&
                    bounds.Bottom <= virtualOverlayBounds.Height + virtualTolerance;
                return bounds.Width > 0d && bounds.Height > 0d;
            }

            if (startCol < 0 || startCol >= topRow.Cells.Count || endCol < 0 || endCol >= bottomRow.Cells.Count)
            {
                return false;
            }

            var topLeftCell = topRow.Cells[startCol];
            var bottomRightCell = bottomRow.Cells[endCol];

            var topLeft = topLeftCell.TranslatePoint(default, _selectionOverlay);
            var bottomRight = bottomRightCell.TranslatePoint(default, _selectionOverlay);
            if (topLeft == null || bottomRight == null)
            {
                return false;
            }

            var left = topLeft.Value.X;
            var top = topLeft.Value.Y;
            var right = bottomRight.Value.X + bottomRightCell.Bounds.Width;
            var bottom = bottomRight.Value.Y + bottomRightCell.Bounds.Height;

            bounds = new Rect(new Point(left, top), new Point(right, bottom));

            const double tolerance = 0.5;
            var overlayBounds = _selectionOverlay.Bounds;
            var isWithinOverlay = left >= -tolerance
                && top >= -tolerance
                && right <= overlayBounds.Width + tolerance
                && bottom <= overlayBounds.Height + tolerance;

            isFullyVisible = isVerticallyVisible && isWithinOverlay;
            return bounds.Width > 0 && bounds.Height > 0;
        }

        private bool TryGetLightweightVirtualSelectionBounds(
            DataGridCellRange displayRange,
            int firstSlot,
            int lastSlot,
            DataGridColumn startColumn,
            DataGridColumn endColumn,
            out Rect bounds,
            out bool isFullyVisible)
        {
            bounds = default;
            isFullyVisible = false;

            int topSlot = -1;
            int bottomSlot = -1;
            int topRowIndex = -1;
            int bottomRowIndex = -1;
            for (int slot = firstSlot; slot <= lastSlot; slot++)
            {
                if (IsGroupSlot(slot))
                {
                    continue;
                }

                int rowIndex = RowIndexFromSlot(slot);
                if (rowIndex < displayRange.StartRow || rowIndex > displayRange.EndRow)
                {
                    continue;
                }

                if (topSlot == -1)
                {
                    topSlot = slot;
                    topRowIndex = rowIndex;
                }

                bottomSlot = slot;
                bottomRowIndex = rowIndex;
            }

            if (topSlot == -1 || bottomSlot == -1 ||
                !_rowsPresenter.TryGetVirtualCellBounds(topSlot, startColumn, out Rect topLeftBounds) ||
                !_rowsPresenter.TryGetVirtualCellBounds(bottomSlot, endColumn, out Rect bottomRightBounds))
            {
                return false;
            }

            Point? topLeftPoint = _rowsPresenter.TranslatePoint(topLeftBounds.TopLeft, _selectionOverlay);
            Point? bottomRightPoint = _rowsPresenter.TranslatePoint(bottomRightBounds.BottomRight, _selectionOverlay);
            if (topLeftPoint == null || bottomRightPoint == null)
            {
                return false;
            }

            bounds = new Rect(topLeftPoint.Value, bottomRightPoint.Value);
            const double tolerance = 0.5d;
            Rect overlayBounds = _selectionOverlay.Bounds;
            bool isVerticallyVisible =
                topRowIndex == displayRange.StartRow &&
                bottomRowIndex == displayRange.EndRow;
            isFullyVisible = isVerticallyVisible &&
                bounds.Left >= -tolerance &&
                bounds.Top >= -tolerance &&
                bounds.Right <= overlayBounds.Width + tolerance &&
                bounds.Bottom <= overlayBounds.Height + tolerance;
            return bounds.Width > 0d && bounds.Height > 0d;
        }
    }
}
