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

            Debug.Assert(!_collapsedSlotsTable.Contains(newFirstDisplayedSlot));
            int firstDisplayedScrollingSlot = newFirstDisplayedSlot;
            int lastDisplayedScrollingSlot = -1;
            double deltaY = -NegVerticalOffset;
            int visibleScrollingRows = 0;
            int scannedSlots = 0;
            long scanRealizedTicks = 0;

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
                // 0 is fine because the element in the first slot cannot be collapsed
                firstDisplayedScrollingSlot = 0;
            }

            bool useEstimatorFirstScan = CanUseEstimatorFirstScan();
            using (DataGridDiagnostics.BeginRowsDisplayScan())
            {
                if (TryReuseDisplayedRows(firstDisplayedScrollingSlot, displayHeight, ref scannedSlots))
                {
                    DataGridDiagnostics.RecordRowsDisplayReused();
                    DataGridDiagnostics.RecordRowsDisplayScanned(scannedSlots);
                    activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
                    activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
                    activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
                    return;
                }

                int slot;
                if (useEstimatorFirstScan &&
                    TryEstimateScanRangeFromTop(
                        firstDisplayedScrollingSlot,
                        displayHeight,
                        out int candidateFirstDisplayedSlot,
                        out int candidateLastDisplayedSlot,
                        ref scannedSlots))
                {
                    firstDisplayedScrollingSlot = candidateFirstDisplayedSlot;
                    slot = firstDisplayedScrollingSlot;

                    while (slot >= 0 &&
                        slot <= candidateLastDisplayedSlot &&
                        !MathUtilities.GreaterThanOrClose(deltaY, displayHeight))
                    {
                        scannedSlots++;
                        deltaY += GetDisplayedScanSlotHeight(slot, ref scanRealizedTicks);
                        visibleScrollingRows++;
                        lastDisplayedScrollingSlot = slot;
                        slot = GetNextVisibleSlot(slot);
                    }
                }
                else
                {
                    slot = firstDisplayedScrollingSlot;
                }

                while (slot < SlotCount && !MathUtilities.GreaterThanOrClose(deltaY, displayHeight))
                {
                    scannedSlots++;
                    deltaY += GetDisplayedScanSlotHeight(slot, ref scanRealizedTicks);
                    visibleScrollingRows++;
                    lastDisplayedScrollingSlot = slot;
                    slot = GetNextVisibleSlot(slot);
                }

                if (slot >= SlotCount)
                {
                    while (MathUtilities.LessThan(deltaY, displayHeight))
                    {
                        slot = GetPreviousVisibleSlot(firstDisplayedScrollingSlot);
                        if (slot < 0)
                        {
                            break;
                        }

                        scannedSlots++;
                        deltaY += GetDisplayedScanSlotHeight(slot, ref scanRealizedTicks);
                        firstDisplayedScrollingSlot = slot;
                        visibleScrollingRows++;
                    }
                }
            }

            DataGridDiagnostics.RecordRowsDisplayScanRealizeTime(GetElapsedMilliseconds(scanRealizedTicks));

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

            int removedElements;
            using (DataGridDiagnostics.BeginRowsDisplayTrim())
            {
                removedElements = RemoveNonDisplayedRows(firstDisplayedScrollingSlot, lastDisplayedScrollingSlot);
            }

            DataGridDiagnostics.RecordRowsDisplayScanned(scannedSlots);
            DataGridDiagnostics.RecordRowsDisplayRemoved(removedElements);

            Debug.Assert(DisplayData.NumDisplayedScrollingElements >= 0, "the number of visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0, "the number of totally visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.FirstScrollingSlot < SlotCount, "firstDisplayedScrollingRow larger than number of rows");
            Debug.Assert(DisplayData.FirstScrollingSlot == firstDisplayedScrollingSlot);
            Debug.Assert(DisplayData.LastScrollingSlot == lastDisplayedScrollingSlot);

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
        }

        private bool TryReuseDisplayedRows(int firstDisplayedScrollingSlot, double displayHeight, ref int scannedSlots)
        {
            if (firstDisplayedScrollingSlot != DisplayData.FirstScrollingSlot ||
                DisplayData.FirstScrollingSlot < 0 ||
                DisplayData.LastScrollingSlot < DisplayData.FirstScrollingSlot)
            {
                return false;
            }

            int displayedCount = DisplayData.NumDisplayedScrollingElements;
            if (displayedCount <= 0)
            {
                return false;
            }

            double deltaY = -NegVerticalOffset;
            int visibleScrollingRows = 0;
            foreach (Control element in DisplayData.GetScrollingElements())
            {
                scannedSlots++;
                deltaY += element.DesiredSize.Height;
                visibleScrollingRows++;

                if (MathUtilities.GreaterThanOrClose(deltaY, displayHeight))
                {
                    // If we hit the target height before consuming all displayed rows,
                    // the viewport needs trimming and we must recompute using exact slots.
                    if (visibleScrollingRows < displayedCount)
                    {
                        return false;
                    }

                    DisplayData.NumTotallyDisplayedScrollingElements =
                        (MathUtilities.GreaterThan(deltaY, displayHeight) ||
                         (MathUtilities.AreClose(deltaY, displayHeight) && MathUtilities.GreaterThan(NegVerticalOffset, 0)))
                            ? visibleScrollingRows - 1
                            : visibleScrollingRows;
                    return true;
                }
            }

            // If we're up to the first row, and we still have room left, uncover as much of
            // the first row as we can without forcing a full row-range recomputation.
            if (DisplayData.FirstScrollingSlot == 0 && MathUtilities.LessThan(deltaY, displayHeight))
            {
                double newNegVerticalOffset = Math.Max(0, NegVerticalOffset - displayHeight + deltaY);
                deltaY += NegVerticalOffset - newNegVerticalOffset;
                NegVerticalOffset = newNegVerticalOffset;
            }

            // We don't have enough displayed height and there are more slots below,
            // so we need the full update path to realize additional rows.
            if (MathUtilities.LessThan(deltaY, displayHeight) &&
                GetNextVisibleSlot(DisplayData.LastScrollingSlot) >= 0)
            {
                return false;
            }

            DisplayData.NumTotallyDisplayedScrollingElements =
                (MathUtilities.GreaterThan(deltaY, displayHeight) ||
                 (MathUtilities.AreClose(deltaY, displayHeight) && MathUtilities.GreaterThan(NegVerticalOffset, 0)))
                    ? visibleScrollingRows - 1
                    : visibleScrollingRows;
            return true;
        }

        private bool CanUseEstimatorFirstScan()
        {
            if (RowHeightEstimator == null)
            {
                return false;
            }

            if (RowGroupHeadersTable.IndexCount > 0 || RowGroupFootersTable.IndexCount > 0)
            {
                return false;
            }

            if (RowDetailsTemplate != null &&
                RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.Collapsed)
            {
                return false;
            }

            return IsEstimatorConfidenceHigh();
        }

        private bool IsEstimatorConfidenceHigh()
        {
            int sampleCount = 0;
            double totalHeight = 0;
            double minHeight = double.MaxValue;
            double maxHeight = 0;

            foreach (Control element in DisplayData.GetScrollingElements())
            {
                double height = element.DesiredSize.Height;
                if (MathUtilities.LessThanOrClose(height, 0))
                {
                    continue;
                }

                sampleCount++;
                totalHeight += height;
                minHeight = Math.Min(minHeight, height);
                maxHeight = Math.Max(maxHeight, height);
            }

            if (sampleCount < 3)
            {
                return false;
            }

            double averageHeight = totalHeight / sampleCount;
            double baseline = Math.Max(1, averageHeight);
            double estimatedHeight = Math.Max(1, RowHeightEstimator?.RowHeightEstimate ?? RowHeightEstimate);
            double relativeError = Math.Abs(estimatedHeight - averageHeight) / baseline;
            double relativeSpread = (maxHeight - minHeight) / baseline;

            return relativeError <= 0.35 && relativeSpread <= 1.5;
        }

        private bool TryEstimateScanRangeFromTop(
            int firstDisplayedScrollingSlot,
            double displayHeight,
            out int estimatedFirstDisplayedSlot,
            out int estimatedLastDisplayedSlot,
            ref int scannedSlots)
        {
            estimatedFirstDisplayedSlot = firstDisplayedScrollingSlot;
            estimatedLastDisplayedSlot = -1;

            if (firstDisplayedScrollingSlot < 0 || firstDisplayedScrollingSlot >= SlotCount)
            {
                return false;
            }

            double estimatedHeight = -NegVerticalOffset;
            int slot = firstDisplayedScrollingSlot;
            while (slot >= 0 &&
                slot < SlotCount &&
                !MathUtilities.GreaterThanOrClose(estimatedHeight, displayHeight))
            {
                scannedSlots++;
                estimatedHeight += GetDisplayedEstimatedScanSlotHeight(slot);
                estimatedLastDisplayedSlot = slot;
                slot = GetNextVisibleSlot(slot);
            }

            while (MathUtilities.LessThan(estimatedHeight, displayHeight))
            {
                int previousSlot = GetPreviousVisibleSlot(estimatedFirstDisplayedSlot);
                if (previousSlot < 0)
                {
                    break;
                }

                scannedSlots++;
                estimatedHeight += GetDisplayedEstimatedScanSlotHeight(previousSlot);
                estimatedFirstDisplayedSlot = previousSlot;
            }

            return estimatedLastDisplayedSlot >= estimatedFirstDisplayedSlot && estimatedLastDisplayedSlot >= 0;
        }

        private bool TryEstimateScanRangeFromBottom(
            int lastDisplayedScrollingSlot,
            double displayHeight,
            out int estimatedFirstDisplayedSlot,
            ref int scannedSlots)
        {
            estimatedFirstDisplayedSlot = -1;

            if (lastDisplayedScrollingSlot < 0 || lastDisplayedScrollingSlot >= SlotCount)
            {
                return false;
            }

            double estimatedHeight = 0;
            int slot = lastDisplayedScrollingSlot;
            while (MathUtilities.LessThan(estimatedHeight, displayHeight) && slot >= 0)
            {
                scannedSlots++;
                estimatedHeight += GetDisplayedEstimatedScanSlotHeight(slot);
                estimatedFirstDisplayedSlot = slot;
                slot = GetPreviousVisibleSlot(slot);
            }

            return estimatedFirstDisplayedSlot >= 0;
        }

        private double GetDisplayedEstimatedScanSlotHeight(int slot)
        {
            if (slot >= DisplayData.FirstScrollingSlot &&
                slot <= DisplayData.LastScrollingSlot)
            {
                return DisplayData.GetDisplayedElement(slot).DesiredSize.Height;
            }

            return GetSlotElementHeight(slot);
        }

        private double GetDisplayedScanSlotHeight(int slot, ref long scanRealizedTicks)
        {
            if (slot >= DisplayData.FirstScrollingSlot &&
                slot <= DisplayData.LastScrollingSlot)
            {
                return DisplayData.GetDisplayedElement(slot).DesiredSize.Height;
            }

            var startTimestamp = Stopwatch.GetTimestamp();
            var slotElement = InsertDisplayedElement(slot, updateSlotInformation: true);
            scanRealizedTicks += Stopwatch.GetTimestamp() - startTimestamp;
            return slotElement.DesiredSize.Height;
        }

        private static double GetElapsedMilliseconds(long timestampDelta)
        {
            return timestampDelta <= 0
                ? 0
                : timestampDelta * 1000.0 / Stopwatch.Frequency;
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
            int scannedSlots = 0;
            long scanRealizedTicks = 0;

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

            if (lastDisplayedScrollingRow == -1)
            {
                lastDisplayedScrollingRow = 0;
            }

            bool useEstimatorFirstScan = CanUseEstimatorFirstScan();
            using (DataGridDiagnostics.BeginRowsDisplayScan())
            {
                int slot;
                if (useEstimatorFirstScan &&
                    TryEstimateScanRangeFromBottom(
                        lastDisplayedScrollingRow,
                        displayHeight,
                        out int candidateFirstDisplayedSlot,
                        ref scannedSlots))
                {
                    slot = lastDisplayedScrollingRow;
                    while (MathUtilities.LessThan(deltaY, displayHeight) &&
                        slot >= candidateFirstDisplayedSlot &&
                        slot >= 0)
                    {
                        scannedSlots++;
                        deltaY += GetDisplayedScanSlotHeight(slot, ref scanRealizedTicks);
                        visibleScrollingRows++;
                        firstDisplayedScrollingRow = slot;
                        slot = GetPreviousVisibleSlot(slot);
                    }
                }
                else
                {
                    slot = lastDisplayedScrollingRow;
                }

                while (MathUtilities.LessThan(deltaY, displayHeight) && slot >= 0)
                {
                    scannedSlots++;
                    deltaY += GetDisplayedScanSlotHeight(slot, ref scanRealizedTicks);
                    visibleScrollingRows++;
                    firstDisplayedScrollingRow = slot;
                    slot = GetPreviousVisibleSlot(slot);
                }
            }

            DataGridDiagnostics.RecordRowsDisplayScanRealizeTime(GetElapsedMilliseconds(scanRealizedTicks));

            DisplayData.NumTotallyDisplayedScrollingElements = deltaY > displayHeight ? visibleScrollingRows - 1 : visibleScrollingRows;

            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0);
            Debug.Assert(lastDisplayedScrollingRow < SlotCount, "lastDisplayedScrollingRow larger than number of rows");

            NegVerticalOffset = Math.Max(0, deltaY - displayHeight);

            int removedElements;
            using (DataGridDiagnostics.BeginRowsDisplayTrim())
            {
                removedElements = RemoveNonDisplayedRows(firstDisplayedScrollingRow, lastDisplayedScrollingRow);
            }

            DataGridDiagnostics.RecordRowsDisplayScanned(scannedSlots);
            DataGridDiagnostics.RecordRowsDisplayRemoved(removedElements);

            Debug.Assert(DisplayData.NumDisplayedScrollingElements >= 0, "the number of visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.NumTotallyDisplayedScrollingElements >= 0, "the number of totally visible scrolling rows can't be negative");
            Debug.Assert(DisplayData.FirstScrollingSlot < SlotCount, "firstDisplayedScrollingRow larger than number of rows");

            activity?.SetTag(DataGridDiagnostics.Tags.FirstDisplayedSlot, DisplayData.FirstScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.LastDisplayedSlot, DisplayData.LastScrollingSlot);
            activity?.SetTag(DataGridDiagnostics.Tags.DisplayedSlots, DisplayData.NumDisplayedScrollingElements);
        }



        private int RemoveNonDisplayedRows(int newFirstDisplayedSlot, int newLastDisplayedSlot)
        {
            int removedCount = 0;
            while (DisplayData.FirstScrollingSlot < newFirstDisplayedSlot)
            {
                // Need to add rows above the lastDisplayedScrollingRow
                RemoveDisplayedElement(DisplayData.FirstScrollingSlot, false /*wasDeleted*/, true /*updateSlotInformation*/);
                removedCount++;
            }
            while (DisplayData.LastScrollingSlot > newLastDisplayedSlot)
            {
                // Need to remove rows below the lastDisplayedScrollingRow
                RemoveDisplayedElement(DisplayData.LastScrollingSlot, false /*wasDeleted*/, true /*updateSlotInformation*/);
                removedCount++;
            }

            return removedCount;
        }



        private void ResetDisplayedRows()
        {
            if (UnloadingRowEvent.HasRaisedSubscriptions || UnloadingRowGroupEvent.HasRaisedSubscriptions)
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

            DisplayData.ClearElements(recycle: true);

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
            if (CurrentColumnIndex != -1 &&
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
                _rowsPresenter.Children.Remove(element);
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
            element.SetCurrentValue(Visual.IsVisibleProperty, false);

            if (RecycledContainerHidingMode == DataGridRecycleHidingMode.MoveOffscreen)
            {
                var size = element.Bounds.Size;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    size = element.DesiredSize;
                }

                // Move hidden elements off-screen immediately to avoid stale bounds being picked up
                // by layout-sensitive logic (e.g., tests that inspect all rows).
                element.Arrange(new Rect(-10000, -10000, size.Width, size.Height));
            }

            if (element is DataGridRow row)
            {
                row.ClearPointerOverState();
            }
        }

    }
}
