// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Media;
using Avalonia.Controls.Utils;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Input;
using Avalonia.Utilities;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {

        private void UpdateDisplayedRows(int newFirstDisplayedSlot, double displayHeight)
        {
            using var activity = DataGridDiagnostics.UpdateDisplayedRows();
            using var _ = DataGridDiagnostics.BeginRowsDisplayUpdate();
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayHeight, displayHeight);
            activity?.SetTag(DataGridDiagnostics.Tags.SlotCount, SlotCount);
            activity?.SetTag(DataGridDiagnostics.Tags.Columns, ColumnsItemsInternal.Count);

            int firstDisplayedScrollingSlot = NormalizeDisplayedFirstSlot(newFirstDisplayedSlot);
            int lastDisplayedScrollingSlot = -1;
            double deltaY = -NegVerticalOffset;
            int visibleScrollingRows = 0;

            if (_rowsPresenter == null)
            {
                ResetDisplayedRows();
                return;
            }

            if (MathUtilities.LessThanOrClose(displayHeight, 0) || SlotCount == 0 || ColumnsItemsInternal.Count == 0)
            {
                ResetDisplayedRows();
                return;
            }

            if (firstDisplayedScrollingSlot == -1)
            {
                ResetDisplayedRows();
                return;
            }

            if (TryUpdateLightweightVirtualRows(
                firstDisplayedScrollingSlot,
                displayHeight,
                activity))
            {
                return;
            }

            if (DisplayData.HasVirtualScrollingElements)
            {
                ResetDisplayedRows();
            }

            if (!CanRetainDisplayedRowsForScrollTarget(firstDisplayedScrollingSlot))
            {
                if (TryRetargetDefaultVirtualRows(
                    firstDisplayedScrollingSlot,
                    displayHeight,
                    out int retargetedLastSlot,
                    out int retargetedRows,
                    out double retargetedHeight))
                {
                    bool clipsLastRow =
                        MathUtilities.GreaterThan(retargetedHeight, displayHeight) ||
                        (MathUtilities.AreClose(retargetedHeight, displayHeight) &&
                         MathUtilities.GreaterThan(NegVerticalOffset, 0));
                    DisplayData.NumTotallyDisplayedScrollingElements =
                        clipsLastRow ? retargetedRows - 1 : retargetedRows;
                    AvailableSlotElementRoom = displayHeight - retargetedHeight;
                    _rowsPresenter.InvalidateArrange();
                    _rowsPresenter.InvalidateVirtualCellSurface();
                    RequestPointerOverRefresh();

                    activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, firstDisplayedScrollingSlot);
                    activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, retargetedLastSlot);
                    activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, retargetedRows);
                    return;
                }

                ResetDisplayedRows();
            }

            int slot = firstDisplayedScrollingSlot;
            while (slot < SlotCount && !MathUtilities.GreaterThanOrClose(deltaY, displayHeight))
            {
                deltaY += GetDisplayedSlotElementHeight(slot);
                visibleScrollingRows++;
                lastDisplayedScrollingSlot = slot;
                slot = GetNextVisibleSlot(slot);
            }

            while (MathUtilities.LessThan(deltaY, displayHeight) && slot >= 0)
            {
                slot = GetPreviousVisibleSlot(firstDisplayedScrollingSlot);
                if (slot >= 0)
                {
                    deltaY += GetDisplayedSlotElementHeight(slot);
                    firstDisplayedScrollingSlot = slot;
                    visibleScrollingRows++;
                }
            }
            // If we're up to the first row, and we still have room left, uncover as much of the first row as we can
            if (firstDisplayedScrollingSlot == 0 && MathUtilities.LessThan(deltaY, displayHeight))
            {
                double newNegVerticalOffset = Math.Max(0, NegVerticalOffset - displayHeight + deltaY);
                deltaY += NegVerticalOffset - newNegVerticalOffset;
                NegVerticalOffset = newNegVerticalOffset;
            }

            if (MathUtilities.GreaterThan(deltaY, displayHeight) || (MathUtilities.AreClose(deltaY, displayHeight) && MathUtilities.GreaterThan(NegVerticalOffset, 0)))
            {
                DisplayData.NumTotallyDisplayedScrollingElements = visibleScrollingRows - 1;
            }
            else
            {
                DisplayData.NumTotallyDisplayedScrollingElements = visibleScrollingRows;
            }
            if (visibleScrollingRows == 0)
            {
                firstDisplayedScrollingSlot = -1;
                Debug.Assert(lastDisplayedScrollingSlot == -1);
            }

            Debug.Assert(lastDisplayedScrollingSlot < SlotCount, "lastDisplayedScrollingRow larger than number of rows");

            RemoveNonDisplayedRows(firstDisplayedScrollingSlot, lastDisplayedScrollingSlot);

            Debug.Assert(DisplayData.NumDisplayedScrollingElements >= 0, "the number of visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0, "the number of totally visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.FirstScrollingSlot < SlotCount, "firstDisplayedScrollingRow larger than number of rows");
            Debug.Assert(DisplayData.FirstScrollingSlot == firstDisplayedScrollingSlot);
            Debug.Assert(DisplayData.LastScrollingSlot == lastDisplayedScrollingSlot);

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
        }

        private bool TryUpdateLightweightVirtualRows(
            int firstSlot,
            double displayHeight,
            Activity? activity)
        {
            if (_rowsPresenter is null ||
                !TryGetLightweightVirtualRowHeight(out double rowHeight))
            {
                return false;
            }

            return UpdateLightweightVirtualRows(
                firstSlot,
                displayHeight,
                rowHeight,
                activity,
                reuseExistingItems: false);
        }

        private bool TryUpdateLightweightVirtualRowsForScroll(
            int firstSlot,
            double displayHeight,
            double rowHeight)
        {
            using var activity = DataGridDiagnostics.UpdateDisplayedRows();
            using var _ = DataGridDiagnostics.BeginRowsDisplayUpdate();
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayHeight, displayHeight);
            activity?.SetTag(DataGridDiagnostics.Tags.SlotCount, SlotCount);
            activity?.SetTag(DataGridDiagnostics.Tags.Columns, ColumnsItemsInternal.Count);

            return UpdateLightweightVirtualRows(
                firstSlot,
                displayHeight,
                rowHeight,
                activity,
                reuseExistingItems: true);
        }

        private bool UpdateLightweightVirtualRows(
            int firstSlot,
            double displayHeight,
            double rowHeight,
            Activity? activity,
            bool reuseExistingItems)
        {
            Debug.Assert(_rowsPresenter is not null);

            if (!DisplayData.HasVirtualScrollingElements &&
                DisplayData.NumDisplayedScrollingElements != 0)
            {
                ResetDisplayedRows();
            }

            int requestedCount = Math.Max(
                1,
                (int)Math.Ceiling((displayHeight + NegVerticalOffset) / rowHeight));
            int lastSlot = Math.Min(SlotCount - 1, firstSlot + requestedCount - 1);
            int count = lastSlot - firstSlot + 1;

            if (count < requestedCount && firstSlot > 0)
            {
                firstSlot = Math.Max(0, firstSlot - (requestedCount - count));
                count = lastSlot - firstSlot + 1;
            }

            double realizedHeight = (count * rowHeight) - NegVerticalOffset;
            bool clipsLastRow =
                MathUtilities.GreaterThan(realizedHeight, displayHeight) ||
                (MathUtilities.AreClose(realizedHeight, displayHeight) &&
                 MathUtilities.GreaterThan(NegVerticalOffset, 0));

            bool updated = reuseExistingItems
                ? _rowsPresenter.TryScrollLightweightVirtualRows(
                    firstSlot,
                    lastSlot,
                    count,
                    rowHeight)
                : _rowsPresenter.TryUpdateLightweightVirtualRows(
                    firstSlot,
                    lastSlot,
                    count,
                    rowHeight);
            if (!updated)
            {
                if (DisplayData.HasVirtualScrollingElements)
                {
                    ResetDisplayedRows();
                }

                return false;
            }

            DisplayData.SetVirtualScrollingSlots(firstSlot, lastSlot, count);
            DisplayData.NumTotallyDisplayedScrollingElements =
                clipsLastRow ? Math.Max(0, count - 1) : count;
            AvailableSlotElementRoom = displayHeight - realizedHeight;
            _rowsPresenter.InvalidateArrange();
            _rowsPresenter.InvalidateVirtualCellSurface();
            RequestPointerOverRefresh();

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, firstSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, lastSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, count);
            return true;
        }

        private bool TryRetargetDefaultVirtualRows(
            int firstSlot,
            double displayHeight,
            out int lastSlot,
            out int rowCount,
            out double realizedHeight)
        {
            lastSlot = -1;
            rowCount = 0;
            realizedHeight = -NegVerticalOffset;
            double rowHeight;

            using (DataGridDiagnostics.BeginRowsRetargetEligibility())
            {
                if (!CanRetargetDefaultVirtualRowsNow() ||
                    DisplayData.NumDisplayedScrollingElements == 0)
                {
                    return false;
                }

                rowHeight = DataGridRow.GetFlatDesiredHeight(this, RowHeight);
                if (!double.IsFinite(rowHeight) || !MathUtilities.GreaterThan(rowHeight, 0))
                {
                    return false;
                }

                int slot = firstSlot;
                while (slot >= 0 && slot < SlotCount &&
                       !MathUtilities.GreaterThanOrClose(realizedHeight, displayHeight))
                {
                    if (IsGroupSlot(slot))
                    {
                        return false;
                    }

                    realizedHeight += rowHeight;
                    rowCount++;
                    lastSlot = slot;
                    slot = GetNextVisibleSlot(slot);
                }
            }

            return DisplayData.TryRetargetDefaultVirtualRows(
                firstSlot,
                lastSlot,
                rowCount,
                rowHeight,
                RowGroupHeadersTable.RangeCount == 0 &&
                RowGroupFootersTable.RangeCount == 0 &&
                _collapsedSlotsTable.IsEmpty);
        }

        private int NormalizeDisplayedFirstSlot(int slot)
        {
            if (SlotCount == 0)
            {
                return -1;
            }

            if (slot == -1)
            {
                return GetNextVisibleSlot(-1);
            }

            if (slot < 0 || slot >= SlotCount)
            {
                return GetNextVisibleSlot(-1);
            }

            if (!_collapsedSlotsTable.Contains(slot))
            {
                return slot;
            }

            int nextVisible = GetNextVisibleSlot(slot);
            if (nextVisible != -1 && nextVisible < SlotCount)
            {
                return nextVisible;
            }

            int previousVisible = GetPreviousVisibleSlot(slot + 1);
            return previousVisible >= 0 ? previousVisible : -1;
        }


        private void UpdateDisplayedRowsFromBottom(int newLastDisplayedScrollingRow)
        {
            using var activity = DataGridDiagnostics.UpdateDisplayedRows();
            using var _ = DataGridDiagnostics.BeginRowsDisplayUpdate();
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayHeight, CellsEstimatedHeight);
            activity?.SetTag(DataGridDiagnostics.Tags.SlotCount, SlotCount);
            activity?.SetTag(DataGridDiagnostics.Tags.Columns, ColumnsItemsInternal.Count);

            //Debug.Assert(!_collapsedSlotsTable.Contains(newLastDisplayedScrollingRow));

            int lastDisplayedScrollingRow = newLastDisplayedScrollingRow;
            int firstDisplayedScrollingRow = -1;
            double displayHeight = CellsEstimatedHeight;
            double deltaY = 0;
            int visibleScrollingRows = 0;

            if (_rowsPresenter == null)
            {
                ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                return;
            }

            if (MathUtilities.LessThanOrClose(displayHeight, 0) || SlotCount == 0 || ColumnsItemsInternal.Count == 0)
            {
                ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                return;
            }

            if (lastDisplayedScrollingRow == -1)
            {
                lastDisplayedScrollingRow = 0;
            }

            if (TryGetLightweightVirtualRowHeight(out double lightweightRowHeight))
            {
                int requestedCount = Math.Max(1, (int)Math.Ceiling(displayHeight / lightweightRowHeight));
                int firstSlot = Math.Max(0, lastDisplayedScrollingRow - requestedCount + 1);
                NegVerticalOffset = Math.Max(
                    0,
                    ((lastDisplayedScrollingRow - firstSlot + 1) * lightweightRowHeight) - displayHeight);
                TryUpdateLightweightVirtualRows(firstSlot, displayHeight, activity);
                return;
            }

            if (DisplayData.HasVirtualScrollingElements)
            {
                ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
            }

            if (!CanRetainDisplayedRowsForScrollTarget(lastDisplayedScrollingRow))
            {
                ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
            }

            int slot = lastDisplayedScrollingRow;
            while (MathUtilities.LessThan(deltaY, displayHeight) && slot >= 0)
            {
                deltaY += GetDisplayedSlotElementHeight(slot);
                visibleScrollingRows++;
                firstDisplayedScrollingRow = slot;
                slot = GetPreviousVisibleSlot(slot);
            }

            DisplayData.NumTotallyDisplayedScrollingElements = deltaY > displayHeight ? visibleScrollingRows - 1 : visibleScrollingRows;

            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0);
            Debug.Assert(lastDisplayedScrollingRow < SlotCount, "lastDisplayedScrollingRow larger than number of rows");

            NegVerticalOffset = Math.Max(0, deltaY - displayHeight);

            RemoveNonDisplayedRows(firstDisplayedScrollingRow, lastDisplayedScrollingRow);

            Debug.Assert(DisplayData.NumDisplayedScrollingElements >= 0, "the number of visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0, "the number of totally visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.FirstScrollingSlot < SlotCount, "firstDisplayedScrollingRow larger than number of rows");

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
        }



        private void RemoveNonDisplayedRows(int newFirstDisplayedSlot, int newLastDisplayedSlot)
        {
            while (DisplayData.FirstScrollingSlot < newFirstDisplayedSlot)
            {
                // Need to add rows above the lastDisplayedScrollingRow
                RemoveDisplayedElement(DisplayData.FirstScrollingSlot, false /*wasDeleted*/, true /*updateSlotInformation*/);
            }
            while (DisplayData.LastScrollingSlot > newLastDisplayedSlot)
            {
                // Need to remove rows below the lastDisplayedScrollingRow
                RemoveDisplayedElement(DisplayData.LastScrollingSlot, false /*wasDeleted*/, true /*updateSlotInformation*/);
            }
        }



        private void ResetDisplayedRows(DataGridRecycleReuseOrder reuseOrder = DataGridRecycleReuseOrder.TopDown)
        {
            if (HasUnloadingRowHandlers() ||
                UnloadingRowGroupEvent.HasRaisedSubscriptions ||
                HasCellClearingHandlers)
            {
                foreach (Control element in DisplayData.GetScrollingElements())
                {
                    // Raise Unloading Row for all the rows we're displaying
                    if (element is DataGridRow row)
                    {
                        if (IsRowRecyclable(row))
                        {
                            OnUnloadingRow(new DataGridRowEventArgs(row));
                        }
                    }
                    // Raise Unloading Row for all the RowGroupHeaders we're displaying
                    else if (element is DataGridRowGroupHeader groupHeader)
                    {
                        OnUnloadingRowGroup(new DataGridRowGroupHeaderEventArgs(groupHeader));
                    }
                }
            }

            DisplayData.ActivateDeferredRecycleHiding(reuseOrder);
            DisplayData.ClearElements(recycle: true);
            _rowsPresenter?.ClearLightweightVirtualRows();

            if (_rowsPresenter != null && !KeepRecycledContainersInVisualTree)
            {
                RemoveRecycledChildrenFromVisualTree();
            }
            AvailableSlotElementRoom = CellsEstimatedHeight;
        }



        /// <summary>
        /// Determines whether the row at the provided index must be displayed or not.
        /// </summary>
        private bool SlotIsDisplayed(int slot)
        {
            Debug.Assert(slot >= 0);

            if (_rowsPresenter == null)
            {
                return false;
            }

            if (slot >= DisplayData.FirstScrollingSlot &&
            slot <= DisplayData.LastScrollingSlot)
            {
                // Additional row takes the spot of a displayed row - it is necessarily displayed
                return true;
            }
            else if (DisplayData.FirstScrollingSlot == -1 &&
            CellsEstimatedHeight > 0 &&
            CellsWidth > 0)
            {
                return true;
            }
            else if (slot == GetNextVisibleSlot(DisplayData.LastScrollingSlot))
            {
                if (AvailableSlotElementRoom > 0)
                {
                    // There is room for this additional row
                    return true;
                }
            }
            return false;
        }


        private void LoadRowVisualsForDisplay(DataGridRow row)
        {
            bool usesVirtualCellSurface = UsesVirtualCellSurface;

            // Restore visibility for rows that were hidden during recycling
            row.ClearValue(Visual.IsVisibleProperty);
            row.ClearValue(Visual.ClipProperty);

            // If the row has been recycled, reapply the BackgroundBrush
            if (row.IsRecycled)
            {
                row.ApplyCellsState();
                _rowsPresenter?.InvalidateChildIndex(row);
            }
            else if (row == EditingRow)
            {
                row.ApplyCellsState();
            }

            // Set the Row's Style if we one's defined at the DataGrid level and the user didn't
            // set one at the row level
            //EnsureElementStyle(row, null, RowStyle);
            row.EnsureHeaderStyleAndVisibility(null);

            // Check to see if the row contains the CurrentCell, apply its state.
            if (!usesVirtualCellSurface &&
                CurrentColumnIndex != -1 &&
                CurrentSlot != -1 &&
                row.Index == CurrentSlot)
            {
                row.Cells[CurrentColumnIndex].UpdatePseudoClasses();
            }

            row.ApplyState();

            // Show or hide RowDetails based on DataGrid settings
            EnsureRowDetailsVisibility(row, raiseNotification: false, animate: false);

            if (_searchModel != null)
            {
                var highlightMode = _searchModel.HighlightMode;
                bool highlightMatches = highlightMode != SearchHighlightMode.None;
                bool highlightCurrent = highlightMatches && _searchModel.HighlightCurrent;
                UpdateSearchStatesForRow(row, highlightMode, highlightMatches, highlightCurrent);
            }
        }



        private void RemoveDisplayedElement(int slot, bool wasDeleted, bool updateSlotInformation)
        {
            Debug.Assert(slot >= DisplayData.FirstScrollingSlot &&
            slot <= DisplayData.LastScrollingSlot);

            RemoveDisplayedElement(DisplayData.GetDisplayedElement(slot), slot, wasDeleted, updateSlotInformation);
        }


        private void RemoveDisplayedElement(Control element, int slot, bool wasDeleted, bool updateSlotInformation)
        {
            _rowsPresenter?.UnregisterAnchorCandidate(element);

            if (element is DataGridRow dataGridRow)
            {
                if (ReferenceEquals(dataGridRow, _focusedRow)
                    || dataGridRow.IsKeyboardFocusWithin
                    || CurrentSlot == slot)
                {
                    var focusManager = FocusManager.GetFocusManager(this);
                    var focusedElement = focusManager?.GetFocusedElement() as Visual;
                    if (focusedElement == null
                        || !focusedElement.IsAttachedToVisualTree
                        || !focusedElement.IsEffectivelyVisible
                        || this.ContainsChild(focusedElement))
                    {
                        if (focusManager != null)
                        {
                            focusManager.Focus(this, NavigationMethod.Unspecified, KeyModifiers.None);
                        }
                        else
                        {
                            Focus(NavigationMethod.Unspecified, KeyModifiers.None);
                        }
                    }

                    RequestFocusAfterRowRecycle();
                }

                HideRecycledElement(dataGridRow);

                if (IsRowRecyclable(dataGridRow))
                {
                    UnloadRow(dataGridRow);
                }
                else
                {
                    dataGridRow.Clip = new RectangleGeometry();
                }
            }
            else if (element is DataGridRowGroupHeader groupHeader)
            {
                OnUnloadingRowGroup(new DataGridRowGroupHeaderEventArgs(groupHeader));
                HideRecycledElement(groupHeader);
                DisplayData.RecycleGroupHeader(groupHeader);
            }
            else if (element is DataGridRowGroupFooter groupFooter)
            {
                HideRecycledElement(groupFooter);
                DisplayData.RecycleGroupFooter(groupFooter);
            }
            else if (_rowsPresenter != null)
            {
                _rowsPresenter.RemoveTrackedChild(element);
            }

            DisplayData.UnloadScrollingElement(element, slot, updateSlotInformation, wasDeleted);
        }

        internal void RequestFocusAfterRowRecycle()
        {
            if (!IsAttachedToVisualTree || _focusRestoreScheduled)
            {
                return;
            }

            _focusRestoreScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _focusRestoreScheduled = false;
                var focusManager = FocusManager.GetFocusManager(this);
                var focusedElement = focusManager?.GetFocusedElement() as Visual;

                if (focusedElement != null && focusedElement.IsAttachedToVisualTree && focusedElement.IsEffectivelyVisible)
                {
                    return;
                }

                if (focusManager != null)
                {
                    focusManager.Focus(this, NavigationMethod.Unspecified, KeyModifiers.None);
                }
                else
                {
                    Focus(NavigationMethod.Unspecified, KeyModifiers.None);
                }

            }, DispatcherPriority.Input);
        }


        internal void HideRecycledElement(Control element)
        {
            if (element is DataGridRow row)
            {
                row.ClearPointerOverState();
            }

            if (DisplayData.TryDeferElementHide(element))
            {
                return;
            }

            if (RecycledContainerHidingMode == DataGridRecycleHidingMode.MoveOffscreen)
            {
                const double recycledElementPosition = -10000;
                var bounds = element.Bounds;
                if (bounds.X != recycledElementPosition || bounds.Y != recycledElementPosition)
                {
                    var size = bounds.Size;
                    if (size.Width <= 0 || size.Height <= 0)
                    {
                        size = element.DesiredSize;
                    }

                    // Hiding must not arrange children: auto-sized cells can recompute the viewport
                    // and recycle this same container before its removal has completed.
                    var recycledBounds = new Rect(recycledElementPosition, recycledElementPosition, size.Width, size.Height);
                    switch (element)
                    {
                        case DataGridRow recycledRow:
                            recycledRow.SetRecycledBounds(recycledBounds);
                            break;
                        case DataGridRowGroupHeader groupHeader:
                            groupHeader.SetRecycledBounds(recycledBounds);
                            break;
                        case DataGridRowGroupFooter groupFooter:
                            groupFooter.SetRecycledBounds(recycledBounds);
                            break;
                    }
                }
            }

            if (element.IsVisible)
            {
                element.SetCurrentValue(Visual.IsVisibleProperty, false);
            }
        }

        internal bool RecycleOrphanedElement(Control element)
        {
            if (element is DataGridRow row)
            {
                if (IsRowRecyclable(row) && !_loadedRows.Contains(row))
                {
                    UnloadRow(row);
                    return true;
                }

                HideRecycledElement(row);
                return false;
            }

            if (element is DataGridRowGroupHeader groupHeader)
            {
                OnUnloadingRowGroup(new DataGridRowGroupHeaderEventArgs(groupHeader));
                HideRecycledElement(groupHeader);
                DisplayData.RecycleGroupHeader(groupHeader);
                return true;
            }

            if (element is DataGridRowGroupFooter groupFooter)
            {
                HideRecycledElement(groupFooter);
                DisplayData.RecycleGroupFooter(groupFooter);
                return true;
            }

            HideRecycledElement(element);
            return false;
        }

    }
}
