// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using Avalonia.Utilities;
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
        private const int IndexedScrollMinimumSlotCount = 100_000;
        private const int IndexedScrollMinimumEstimatedRows = 1_024;

        internal long UniformScrollTargetCount { get; private set; }

        internal long LightweightVirtualScrollCount { get; private set; }

        internal long FlatRowRetargetScrollCount { get; private set; }

        private bool CanUseEstimatedScrollFastPath()
        {
            return RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.VisibleWhenSelected || RowDetailsTemplate == null;
        }

        private bool ShouldUseEstimatedScrollFastPath(double remainingHeight)
        {
            if (!CanUseEstimatedScrollFastPath() || MathUtilities.LessThanOrClose(remainingHeight, 0))
            {
                return false;
            }

            double singleRowHeightEstimate = GetCurrentSingleRowHeightEstimate();
            if (MathUtilities.LessThanOrClose(singleRowHeightEstimate, 0))
            {
                singleRowHeightEstimate = Math.Max(RowHeightEstimate, 1);
            }

            if (MathUtilities.GreaterThan(remainingHeight, 2 * CellsEstimatedHeight))
            {
                return true;
            }

            // For multi-row jumps, estimator-based repositioning is faster than per-slot recycle loops.
            double estimatedRowsToSkip = remainingHeight / Math.Max(singleRowHeightEstimate, 1);
            return MathUtilities.GreaterThan(estimatedRowsToSkip, 64);
        }

        private bool TryGetIndexedScrollTarget(double verticalOffset, int lastVisibleSlot, out int targetSlot)
        {
            targetSlot = -1;

            if (_rowsPresenter == null || !CanUseEstimatedScrollFastPath())
            {
                return false;
            }

            if (!ShouldBuildScrollHeightIndex(verticalOffset))
            {
                return false;
            }

            EnsureScrollHeightIndex();
            double totalHeight = _scrollHeightIndex.TotalHeight;
            if (totalHeight <= 0)
            {
                return false;
            }

            double boundedOffset = Math.Max(0, Math.Min(verticalOffset, totalHeight));
            targetSlot = _scrollHeightIndex.FindSlotAtOffset(boundedOffset);
            if (targetSlot < 0)
            {
                return false;
            }

            targetSlot = Math.Min(targetSlot, lastVisibleSlot);
            if (targetSlot < 0)
            {
                return false;
            }

            return true;
        }

        private bool TryGetUniformScrollTarget(
            double verticalOffset,
            int lastVisibleSlot,
            out int targetSlot,
            out double rowHeight)
        {
            targetSlot = -1;
            rowHeight = 0;

            if (!UsesDefaultVirtualRowPipeline ||
                !double.IsFinite(RowHeight) ||
                RowDetailsTemplate is not null ||
                RowGroupHeadersTable.RangeCount != 0 ||
                RowGroupFootersTable.RangeCount != 0 ||
                !_collapsedSlotsTable.IsEmpty ||
                !_showDetailsTable.IsEmpty)
            {
                return false;
            }

            rowHeight = DataGridRow.GetFlatDesiredHeight(this, RowHeight);
            if (!double.IsFinite(rowHeight) || !MathUtilities.GreaterThan(rowHeight, 0))
            {
                return false;
            }

            double boundedOffset = Math.Max(0, verticalOffset);
            double target = Math.Floor(boundedOffset / rowHeight);
            if (target > int.MaxValue)
            {
                targetSlot = lastVisibleSlot;
            }
            else
            {
                targetSlot = Math.Min((int)target, lastVisibleSlot);
            }

            if (targetSlot < 0)
            {
                return false;
            }

            UniformScrollTargetCount++;
            return true;
        }

        private bool ShouldBuildScrollHeightIndex(double verticalOffset)
        {
            double singleRowHeightEstimate = GetCurrentSingleRowHeightEstimate();
            singleRowHeightEstimate = Math.Max(singleRowHeightEstimate, 1);
            double estimatedRows = Math.Abs(verticalOffset - _verticalOffset) / singleRowHeightEstimate;
            bool hasCurrentIndex = !_scrollHeightIndexDirty && _scrollHeightIndex.Count == SlotCount;
            return ShouldBuildScrollHeightIndex(hasCurrentIndex, SlotCount, estimatedRows);
        }

        internal static bool ShouldBuildScrollHeightIndex(
            bool hasCurrentIndex,
            int slotCount,
            double estimatedRows)
        {
            return hasCurrentIndex ||
                slotCount < IndexedScrollMinimumSlotCount ||
                estimatedRows > IndexedScrollMinimumEstimatedRows;
        }

        private double GetCurrentSingleRowHeightEstimate()
        {
            var estimator = RowHeightEstimator;
            double rowHeight = estimator?.RowHeightEstimate ?? RowHeightEstimate;
            double detailsHeight = RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible
                ? estimator?.RowDetailsHeightEstimate ?? RowDetailsHeightEstimate
                : 0;
            return rowHeight + detailsHeight;
        }

        private bool CanRetainDisplayedRowsForScrollTarget(int targetSlot)
        {
            if (DisplayData.FirstScrollingSlot < 0 || DisplayData.LastScrollingSlot < 0)
            {
                return false;
            }

            int previousSlot = GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot);
            int nextSlot = GetNextVisibleSlot(DisplayData.LastScrollingSlot);
            return targetSlot >= previousSlot && (nextSlot < 0 || targetSlot <= nextSlot);
        }

        private void TrimDisplayedRowsBefore(int targetSlot)
        {
            while (DisplayData.FirstScrollingSlot >= 0 &&
                   DisplayData.FirstScrollingSlot < targetSlot)
            {
                RemoveDisplayedElement(
                    DisplayData.FirstScrollingSlot,
                    wasDeleted: false,
                    updateSlotInformation: true);
            }
        }

        private void TrimRetainedDisplayedRowsBefore(int targetSlot)
        {
            if (CanRetainDisplayedRowsForScrollTarget(targetSlot))
            {
                TrimDisplayedRowsBefore(targetSlot);
            }
        }

        private void ScrollSlotsByHeight(double height)
        {
            using var _ = DataGridDiagnostics.BeginRowsScrollSlotsByHeight();

            if (SlotCount == 0)
            {
                return;
            }

            if (TryScrollLightweightVirtualRows(height))
            {
                return;
            }

            if (TryScrollDefaultFlatRows(height))
            {
                return;
            }

            using var deferredRecycleScope = DisplayData.BeginDeferredRecycleScope();

            if (DisplayData.FirstScrollingSlot < 0)
            {
                DisplayData.FirstScrollingSlot = 0;
                NegVerticalOffset = 0;
            }

            Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
            Debug.Assert(!MathUtilities.IsZero(height));
            int previousFirstScrollingSlot = DisplayData.FirstScrollingSlot;
            double previousNegVerticalOffset = NegVerticalOffset;
            double previousVerticalOffset = _verticalOffset;

            _scrollingByHeight = true;
            try
            {
                double deltaY = 0;
                int newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                double newVerticalOffset = _verticalOffset + height;
                int lastVisibleSlot = GetPreviousVisibleSlot(SlotCount);
                bool useIndexedScrollGeometry = false;
                bool useUniformScrollGeometry = false;
                double uniformRowHeight = 0;
                if (height > 0)
                {
                    // Scrolling Down
                    if (!HasLegacyVerticalScrollBar &&
                        UseLogicalScrollable &&
                        _rowsPresenter?.IsBottomAnchorRequested == true)
                    {
                        EnsureScrollHeightIndex();
                        ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                        UpdateDisplayedRowsFromBottom(lastVisibleSlot);
                        newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                        newVerticalOffset = Math.Max(
                            0,
                            _scrollHeightIndex.TotalHeight - CellsEstimatedHeight);
                        useIndexedScrollGeometry = true;
                    }
                    else if (HasLegacyVerticalScrollBar && MathUtilities.LessThanOrClose(GetLegacyVerticalScrollMaximum(), newVerticalOffset))
                    {
                        // We've scrolled to the bottom of the ScrollBar, automatically place the user at the very bottom
                        // of the DataGrid.  If this produces very odd behavior, evaluate the coping strategy used by
                        // OnRowMeasure(Size).  For most data, this should be unnoticeable.
                        ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                        UpdateDisplayedRowsFromBottom(lastVisibleSlot);
                        newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                    }
                    else
                    {
                        deltaY = GetSlotElementHeight(newFirstScrollingSlot) - NegVerticalOffset;
                        if (MathUtilities.LessThan(height, deltaY))
                        {
                            // We've merely covered up more of the same row we're on
                            NegVerticalOffset += height;
                        }
                        else
                        {
                            // Figure out what row we've scrolled down to and update the value for NegVerticalOffset
                            NegVerticalOffset = 0;
                            //
                            var remainingScrollHeight = Math.Max(0, height - deltaY);
                            if (ShouldUseEstimatedScrollFastPath(remainingScrollHeight))
                            {
                                // Very large scroll occurred. Instead of determining the exact number of scrolled off rows,
                                // let's estimate the number based on RowHeight.
                                if (TryGetUniformScrollTarget(
                                    newVerticalOffset,
                                    lastVisibleSlot,
                                    out int uniformTargetSlot,
                                    out uniformRowHeight))
                                {
                                    useUniformScrollGeometry = true;
                                    if (uniformTargetSlot == lastVisibleSlot)
                                    {
                                        ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                                        UpdateDisplayedRowsFromBottom(lastVisibleSlot);
                                        newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                                        newVerticalOffset = Math.Max(
                                            0,
                                            (uniformRowHeight * SlotCount) - CellsEstimatedHeight);
                                    }
                                    else
                                    {
                                        newFirstScrollingSlot = uniformTargetSlot;
                                        TrimRetainedDisplayedRowsBefore(newFirstScrollingSlot);
                                    }
                                }
                                else if (TryGetIndexedScrollTarget(newVerticalOffset, lastVisibleSlot, out int indexedTargetSlot))
                                {
                                    useIndexedScrollGeometry = true;
                                    if (indexedTargetSlot == lastVisibleSlot)
                                    {
                                        // An estimated extent can grow while a bottom-anchor request realizes
                                        // variable-height rows. Anchor the visual tail explicitly so the extent
                                        // cannot keep feeding the same delta back into logical scrolling.
                                        ResetDisplayedRows(DataGridRecycleReuseOrder.BottomUp);
                                        UpdateDisplayedRowsFromBottom(lastVisibleSlot);
                                        newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                                        newVerticalOffset = Math.Max(
                                            0,
                                            _scrollHeightIndex.TotalHeight - CellsEstimatedHeight);
                                    }
                                    else
                                    {
                                        newFirstScrollingSlot = indexedTargetSlot;
                                        TrimRetainedDisplayedRowsBefore(newFirstScrollingSlot);
                                        // Keep a discontinuous window intact until UpdateDisplayedRows.
                                        // The default virtual surface can retarget those rows in place;
                                        // every other path performs the same reset there.
                                    }
                                }
                                else
                                {
                                    ResetDisplayedRows();
                                    var estimator = RowHeightEstimator;
                                    if (estimator != null)
                                    {
                                        // Use the estimator's slot-at-offset calculation for better accuracy
                                        int estimatedSlot = estimator.EstimateSlotAtOffset(_verticalOffset + height, SlotCount);
                                        newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(estimatedSlot - 1), lastVisibleSlot);
                                    }
                                    else
                                    {
                                        double singleRowHeightEstimate = RowHeightEstimate + (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible ? RowDetailsHeightEstimate : 0);
                                        int scrolledToSlot = newFirstScrollingSlot + (int)(height / singleRowHeightEstimate);
                                        scrolledToSlot += _collapsedSlotsTable.GetIndexCount(newFirstScrollingSlot, newFirstScrollingSlot + scrolledToSlot);
                                        newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(scrolledToSlot), lastVisibleSlot);
                                    }
                                }
                            }
                            else
                            {
                                while (MathUtilities.LessThanOrClose(deltaY, height))
                                {
                                    if (newFirstScrollingSlot < lastVisibleSlot)
                                    {
                                        if (IsSlotVisible(newFirstScrollingSlot))
                                        {
                                            // Make the top row available for reuse
                                            RemoveDisplayedElement(newFirstScrollingSlot, false /*wasDeleted*/, true /*updateSlotInformation*/);
                                        }
                                        newFirstScrollingSlot = GetNextVisibleSlot(newFirstScrollingSlot);
                                    }
                                    else
                                    {
                                        // We're being told to scroll beyond the last row, ignore the extra
                                        NegVerticalOffset = 0;
                                        break;
                                    }

                                    double rowHeight = GetDisplayedSlotElementHeight(newFirstScrollingSlot);
                                    double remainingHeight = height - deltaY;
                                    if (MathUtilities.LessThanOrClose(rowHeight, remainingHeight))
                                    {
                                        deltaY += rowHeight;
                                    }
                                    else
                                    {
                                        NegVerticalOffset = remainingHeight;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Scrolling Up
                    if (MathUtilities.GreaterThanOrClose(height + NegVerticalOffset, 0))
                    {
                        // We've merely exposing more of the row we're on
                        NegVerticalOffset += height;
                    }
                    else
                    {
                        // Figure out what row we've scrolled up to and update the value for NegVerticalOffset
                        deltaY = -NegVerticalOffset;
                        NegVerticalOffset = 0;
                        //

                        var remainingScrollHeight = Math.Max(0, Math.Abs(height) - Math.Abs(deltaY));
                        if (ShouldUseEstimatedScrollFastPath(remainingScrollHeight))
                        {
                            // Very large scroll occurred. Instead of determining the exact number of scrolled off rows,
                            // let's estimate the number based on RowHeight.
                            if (newVerticalOffset == 0)
                            {
                                newFirstScrollingSlot = 0;
                            }
                            else
                            {
                                if (TryGetUniformScrollTarget(
                                    newVerticalOffset,
                                    lastVisibleSlot,
                                    out int uniformTargetSlot,
                                    out uniformRowHeight))
                                {
                                    useUniformScrollGeometry = true;
                                    newFirstScrollingSlot = uniformTargetSlot;
                                    TrimRetainedDisplayedRowsBefore(newFirstScrollingSlot);
                                }
                                else if (TryGetIndexedScrollTarget(newVerticalOffset, lastVisibleSlot, out int indexedTargetSlot))
                                {
                                    useIndexedScrollGeometry = true;
                                    newFirstScrollingSlot = indexedTargetSlot;
                                    TrimRetainedDisplayedRowsBefore(newFirstScrollingSlot);
                                    // Defer discontinuous-window reset to UpdateDisplayedRows so
                                    // an eligible virtual surface can retarget rows in place.
                                }
                                else
                                {
                                    ResetDisplayedRows();
                                    var estimator = RowHeightEstimator;
                                    if (estimator != null)
                                    {
                                        // Use the estimator's slot-at-offset calculation for better accuracy
                                        int estimatedSlot = estimator.EstimateSlotAtOffset(newVerticalOffset, SlotCount);
                                        newFirstScrollingSlot = Math.Max(0, GetNextVisibleSlot(estimatedSlot - 1));
                                    }
                                    else
                                    {
                                        double singleRowHeightEstimate = RowHeightEstimate + (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible ? RowDetailsHeightEstimate : 0);
                                        int scrolledToSlot = newFirstScrollingSlot + (int)(height / singleRowHeightEstimate);
                                        scrolledToSlot -= _collapsedSlotsTable.GetIndexCount(scrolledToSlot, newFirstScrollingSlot);

                                        newFirstScrollingSlot = Math.Max(0, GetPreviousVisibleSlot(scrolledToSlot + 1));
                                    }
                                }
                            }
                        }
                        else
                        {
                            int lastScrollingSlot = DisplayData.LastScrollingSlot;
                            while (MathUtilities.GreaterThan(deltaY, height))
                            {
                                if (newFirstScrollingSlot > 0)
                                {
                                    if (IsSlotVisible(lastScrollingSlot))
                                    {
                                        // Make the bottom row available for reuse
                                        RemoveDisplayedElement(lastScrollingSlot, wasDeleted: false, updateSlotInformation: true);
                                        lastScrollingSlot = GetPreviousVisibleSlot(lastScrollingSlot);
                                    }
                                    newFirstScrollingSlot = GetPreviousVisibleSlot(newFirstScrollingSlot);
                                }
                                else
                                {
                                    NegVerticalOffset = 0;
                                    break;
                                }
                                
                                double rowHeight = GetDisplayedSlotElementHeight(newFirstScrollingSlot);
                                double remainingHeight = height - deltaY;
                                if (MathUtilities.LessThanOrClose(rowHeight + remainingHeight, 0))
                                {
                                    deltaY -= rowHeight;
                                }
                                else
                                {
                                    NegVerticalOffset = rowHeight + remainingHeight;
                                    break;
                                }
                            }
                        }
                    }
                    if (MathUtilities.GreaterThanOrClose(0, newVerticalOffset) && newFirstScrollingSlot != 0)
                    {
                        // We've scrolled to the top of the ScrollBar, automatically place the user at the very top
                        // of the DataGrid.  If this produces very odd behavior, evaluate the RowHeight estimate.
                        // strategy. For most data, this should be unnoticeable.
                        ResetDisplayedRows();
                        NegVerticalOffset = 0;
                        UpdateDisplayedRows(0, CellsEstimatedHeight);
                        newFirstScrollingSlot = 0;
                    }
                }

                double firstRowHeight = GetScrollSlotHeight(
                    newFirstScrollingSlot,
                    allowIndexBuild: useIndexedScrollGeometry);

                if (MathUtilities.LessThan(firstRowHeight, NegVerticalOffset))
                {
                    // We've scrolled off more of the first row than what's possible.  This can happen
                    // if the first row got shorter (Ex: Collapsing RowDetails) or if the user has a recycling
                    // cleanup issue.  In this case, simply try to display the next row as the first row instead
                    if (newFirstScrollingSlot < SlotCount - 1)
                    {
                        newFirstScrollingSlot = GetNextVisibleSlot(newFirstScrollingSlot);
                        Debug.Assert(newFirstScrollingSlot != -1);
                    }
                    NegVerticalOffset = 0;
                }

                UpdateDisplayedRows(newFirstScrollingSlot, CellsEstimatedHeight);

                double firstElementHeight = GetScrollSlotHeight(DisplayData.FirstScrollingSlot);
                bool atVisualTail = DisplayData.LastScrollingSlot >= 0 &&
                                    DisplayData.LastScrollingSlot >= LastVisibleSlot;
                var firstRowEstimator = RowHeightEstimator;
                if (!atVisualTail &&
                    (useUniformScrollGeometry ||
                     (firstRowEstimator != null && useIndexedScrollGeometry)))
                {
                    double baseOffset = useUniformScrollGeometry
                        ? DisplayData.FirstScrollingSlot * uniformRowHeight
                        : EstimateOffsetToVisibleSlot(
                            DisplayData.FirstScrollingSlot,
                            firstRowEstimator!,
                            useIndexedScrollGeometry);
                    if (!double.IsNaN(baseOffset) && !double.IsInfinity(baseOffset))
                    {
                        double desiredNeg = Math.Max(0, newVerticalOffset - baseOffset);
                        if (MathUtilities.GreaterThanOrClose(desiredNeg, firstElementHeight))
                        {
                            desiredNeg = Math.Max(0, firstElementHeight - MathUtilities.DoubleEpsilon);
                        }
                        NegVerticalOffset = desiredNeg;
                    }
                }

                if (MathUtilities.GreaterThan(NegVerticalOffset, firstElementHeight))
                {
                    int firstElementSlot = DisplayData.FirstScrollingSlot;
                    // We filled in some rows at the top and now we have a NegVerticalOffset that's greater than the first element
                    while (newFirstScrollingSlot > 0 && MathUtilities.GreaterThan(NegVerticalOffset, firstElementHeight))
                    {
                        int previousSlot = GetPreviousVisibleSlot(firstElementSlot);
                        if (previousSlot == -1)
                        {
                            NegVerticalOffset = 0;
                            _verticalOffset = 0;
                        }
                        else
                        {
                            NegVerticalOffset -= firstElementHeight;
                            _verticalOffset = Math.Max(0, _verticalOffset - firstElementHeight);
                            firstElementSlot = previousSlot;
                            firstElementHeight = GetScrollSlotHeight(firstElementSlot);
                        }
                    }
                    // We could be smarter about this, but it's not common so we wouldn't gain much from optimizing here
                    if (firstElementSlot != DisplayData.FirstScrollingSlot)
                    {
                        if (!CanRetainDisplayedRowsForScrollTarget(firstElementSlot))
                        {
                            ResetDisplayedRows();
                        }
                        UpdateDisplayedRows(firstElementSlot, CellsEstimatedHeight);
                    }
                }

                Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
                double safetyFirstHeight = GetScrollSlotHeight(DisplayData.FirstScrollingSlot);
                bool hasValidSafetyFirstHeight =
                    !double.IsNaN(safetyFirstHeight) &&
                    !MathUtilities.LessThanOrClose(safetyFirstHeight, 0);
                if (!hasValidSafetyFirstHeight)
                {
                    safetyFirstHeight = Math.Max(1, RowHeightEstimate);
                }
                if (MathUtilities.GreaterThanOrClose(NegVerticalOffset, safetyFirstHeight))
                {
                    NegVerticalOffset = Math.Max(0, safetyFirstHeight - 0.001);
                }
                else if (MathUtilities.LessThan(NegVerticalOffset, 0))
                {
                    NegVerticalOffset = 0;
                }
                Debug.Assert(safetyFirstHeight > NegVerticalOffset);

                if (DisplayData.FirstScrollingSlot == 0)
                {
                    _verticalOffset = NegVerticalOffset;
                }
                else if (MathUtilities.GreaterThan(NegVerticalOffset, newVerticalOffset))
                {
                    // The scrolled-in row was larger than anticipated. Adjust the DataGrid so the ScrollBar thumb
                    // can stay in the same place
                    NegVerticalOffset = newVerticalOffset;
                    _verticalOffset = newVerticalOffset;
                }
                else
                {
                    _verticalOffset = newVerticalOffset;
                }

                if (DisplayData.FirstScrollingSlot < 0)
                {
                    DisplayData.FirstScrollingSlot = 0;
                    NegVerticalOffset = 0;
                }

                double displayedFirstHeight = safetyFirstHeight;
                if (!hasValidSafetyFirstHeight)
                {
                    NegVerticalOffset = 0;
                }

                if (MathUtilities.GreaterThanOrClose(NegVerticalOffset, displayedFirstHeight))
                {
                    // Ensure the negative offset stays strictly below the realized height to avoid assertion hits
                    NegVerticalOffset = Math.Max(0, displayedFirstHeight - 0.5);
                }
                else if (MathUtilities.LessThan(NegVerticalOffset, 0))
                {
                    NegVerticalOffset = 0;
                }

                // If scrolling request resulted in no visual position change (same first slot and same
                // partial-row offset), keep the logical vertical offset stable to avoid drift at edges.
                bool noVisualPositionChange =
                    UseLogicalScrollable &&
                    DisplayData.FirstScrollingSlot == previousFirstScrollingSlot &&
                    MathUtilities.AreClose(NegVerticalOffset, previousNegVerticalOffset);

                if (noVisualPositionChange)
                {
                    _verticalOffset = previousVerticalOffset;
                }

                Debug.Assert(!(_verticalOffset == 0 && NegVerticalOffset == 0 && DisplayData.FirstScrollingSlot > 0));

                SetVerticalOffset(_verticalOffset);

                Debug.Assert(MathUtilities.GreaterThanOrClose(NegVerticalOffset, 0));
                Debug.Assert(MathUtilities.GreaterThanOrClose(_verticalOffset, NegVerticalOffset));

            }
            finally
            {
                _scrollingByHeight = false;
            }
        }

        private bool TryScrollLightweightVirtualRows(double height)
        {
            if (!DisplayData.HasVirtualScrollingElements ||
                !TryGetLightweightVirtualRowHeight(out double rowHeight) ||
                _rowsPresenter is null)
            {
                return false;
            }

            double viewportHeight = Math.Max(0, CellsEstimatedHeight);
            double maximumOffset = Math.Max(0, (SlotCount * rowHeight) - viewportHeight);
            double newVerticalOffset = Math.Clamp(_verticalOffset + height, 0, maximumOffset);
            int firstSlot = Math.Min(
                SlotCount - 1,
                (int)Math.Floor(newVerticalOffset / rowHeight));
            double negVerticalOffset = newVerticalOffset - (firstSlot * rowHeight);
            if (MathUtilities.GreaterThanOrClose(negVerticalOffset, rowHeight))
            {
                firstSlot = Math.Min(SlotCount - 1, firstSlot + 1);
                negVerticalOffset = 0;
            }

            NegVerticalOffset = Math.Max(0, negVerticalOffset);
            _verticalOffset = newVerticalOffset;
            if (!TryUpdateLightweightVirtualRowsForScroll(
                    firstSlot,
                    viewportHeight,
                    rowHeight))
            {
                // An item-owned row can invalidate lightweight eligibility while
                // entering the viewport. Complete the same scroll through the
                // retained compatibility pipeline in that uncommon case.
                UpdateDisplayedRows(firstSlot, viewportHeight);
            }
            SetVerticalOffset(_verticalOffset);
            SyncLogicalScrollableOffset();
            LightweightVirtualScrollCount++;
            return true;
        }

        private bool TryScrollDefaultFlatRows(double height)
        {
            if (!CanRetargetDefaultFlatRowsNow(out double rowHeight) ||
                DisplayData.HasVirtualScrollingElements ||
                DisplayData.NumDisplayedScrollingElements <= 0 ||
                _rowsPresenter is null)
            {
                return false;
            }

            double viewportHeight = Math.Max(0, CellsEstimatedHeight);
            double maximumOffset = Math.Max(0, (SlotCount * rowHeight) - viewportHeight);
            double newVerticalOffset = Math.Clamp(_verticalOffset + height, 0, maximumOffset);
            int firstSlot = Math.Min(
                SlotCount - 1,
                (int)Math.Floor(newVerticalOffset / rowHeight));
            double negVerticalOffset = newVerticalOffset - (firstSlot * rowHeight);
            if (MathUtilities.GreaterThanOrClose(negVerticalOffset, rowHeight))
            {
                firstSlot = Math.Min(SlotCount - 1, firstSlot + 1);
                negVerticalOffset = 0;
            }

            int rowCount = DisplayData.NumDisplayedScrollingElements;
            int requiredRows = Math.Max(
                1,
                (int)Math.Ceiling((viewportHeight + negVerticalOffset) / rowHeight));
            if (rowCount < requiredRows || firstSlot + rowCount > SlotCount)
            {
                return false;
            }

            int lastSlot = firstSlot + rowCount - 1;
            if (firstSlot != DisplayData.FirstScrollingSlot &&
                !DisplayData.TryRetargetDefaultFlatRows(firstSlot, lastSlot, rowHeight))
            {
                return false;
            }

            NegVerticalOffset = Math.Max(0, negVerticalOffset);
            _verticalOffset = newVerticalOffset;
            double realizedHeight = (rowCount * rowHeight) - NegVerticalOffset;
            bool clipsLastRow =
                MathUtilities.GreaterThan(realizedHeight, viewportHeight) ||
                (MathUtilities.AreClose(realizedHeight, viewportHeight) &&
                 MathUtilities.GreaterThan(NegVerticalOffset, 0));
            DisplayData.NumTotallyDisplayedScrollingElements =
                clipsLastRow ? Math.Max(0, rowCount - 1) : rowCount;
            AvailableSlotElementRoom = viewportHeight - realizedHeight;

            _rowsPresenter.InvalidateChildIndexes();
            _rowsPresenter.InvalidateArrange();
            RequestPointerOverRefresh();
            SetVerticalOffset(_verticalOffset);
            SyncLogicalScrollableOffset();
            FlatRowRetargetScrollCount++;
            return true;
        }

        private double EstimateOffsetToVisibleSlot(
            int slot,
            IDataGridRowHeightEstimator estimator,
            bool useIndexedScrollGeometry = false)
        {
            using var _ = DataGridDiagnostics.BeginRowsScrollEstimateOffset();

            if (slot <= 0 || estimator == null)
            {
                return 0;
            }

            if (useIndexedScrollGeometry &&
                CanUseEstimatedScrollFastPath() &&
                !_scrollHeightIndexDirty &&
                _scrollHeightIndex.Count == SlotCount)
            {
                return Math.Max(0, _scrollHeightIndex.GetOffsetToSlot(slot));
            }

            double offset = estimator.EstimateOffsetToSlot(slot);

            int collapsedSlot = _collapsedSlotsTable.GetNextIndex(-1);
            while (collapsedSlot != -1 && collapsedSlot < slot)
            {
                var rowGroupInfo = GetGroupInfoForSlot(collapsedSlot);
                bool isGroupSlot = rowGroupInfo != null;
                int level = rowGroupInfo?.Level ?? 0;

                offset -= estimator.GetEstimatedHeight(collapsedSlot, isGroupSlot, level);
                collapsedSlot = _collapsedSlotsTable.GetNextIndex(collapsedSlot);
            }

            return Math.Max(0, offset);
        }


    }
}
