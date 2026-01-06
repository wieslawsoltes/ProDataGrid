// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

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

        private void RequestSelectionOverlayRefresh()
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

            if (SelectionUnit == DataGridSelectionUnit.FullRow || !TryGetSelectedCellRange(out var range))
            {
                SetSelectionOverlayVisible(false);
                return;
            }

            if (!TryGetVisibleSelectionBounds(range, out var bounds, out var isFullyVisible))
            {
                SetSelectionOverlayVisible(false);
                return;
            }

            SetSelectionOverlayVisible(true, isFullyVisible);
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

        private bool TryGetVisibleSelectionBounds(DataGridCellRange range, out Rect bounds, out bool isFullyVisible)
        {
            bounds = default;
            isFullyVisible = false;

            if (_selectionOverlay == null || DisplayData == null || ColumnsItemsInternal == null)
            {
                return false;
            }

            int? startColumnIndex = null;
            int? endColumnIndex = null;
            for (var columnIndex = range.StartColumn; columnIndex <= range.EndColumn; columnIndex++)
            {
                if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
                {
                    continue;
                }

                var column = ColumnsItemsInternal[columnIndex];
                if (column == null)
                {
                    continue;
                }

                if (!column.IsVisible)
                {
                    return false;
                }

                if (startColumnIndex == null)
                {
                    startColumnIndex = columnIndex;
                }

                endColumnIndex = columnIndex;
            }

            if (startColumnIndex == null || endColumnIndex == null)
            {
                return false;
            }

            var firstSlot = DisplayData.FirstScrollingSlot;
            var lastSlot = DisplayData.LastScrollingSlot;
            if (firstSlot < 0 || lastSlot < firstSlot)
            {
                return false;
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
                if (rowIndex < range.StartRow || rowIndex > range.EndRow)
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

            var isVerticallyVisible = topRow.Index == range.StartRow && bottomRow.Index == range.EndRow;

            var startCol = startColumnIndex.Value;
            var endCol = endColumnIndex.Value;
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
    }
}
