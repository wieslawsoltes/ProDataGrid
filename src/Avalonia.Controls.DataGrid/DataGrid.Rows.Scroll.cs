// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using Avalonia.Utilities;
using System.Diagnostics;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {
        /// <summary>
        /// Scrolls the DataGrid vertically by the specified height.
        /// Uses a two-phase approach: calculate target first, then rebuild display.
        /// This eliminates jitter during scroll-up with variable row heights.
        /// </summary>
        /// <param name="height">The scroll delta (positive = down, negative = up).</param>
        private void ScrollSlotsByHeight(double height)
        {
            Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
            Debug.Assert(!MathUtilities.IsZero(height));

            _scrollingByHeight = true;
            try
            {
                if (height > 0)
                {
                    ScrollSlotsByHeightDown(height);
                }
                else
                {
                    ScrollSlotsByHeightUp(height);
                }
            }
            finally
            {
                _scrollingByHeight = false;
            }
        }

        /// <summary>
        /// Handles scroll-down operations with smooth height estimation.
        /// </summary>
        private void ScrollSlotsByHeightDown(double height)
        {
            Debug.Assert(height > 0);
            
            int newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
            double newVerticalOffset = _verticalOffset + height;
            int lastVisibleSlot = GetPreviousVisibleSlot(SlotCount);

            // Check if we've scrolled to the bottom of the ScrollBar
            if (HasLegacyVerticalScrollBar && MathUtilities.LessThanOrClose(GetLegacyVerticalScrollMaximum(), newVerticalOffset))
            {
                ResetDisplayedRows();
                UpdateDisplayedRowsFromBottom(lastVisibleSlot);
                newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                _verticalOffset = newVerticalOffset;
                SetVerticalOffset(_verticalOffset);
                return;
            }

            // Calculate how much of the first row remains visible
            double firstRowHeight = GetEstimatedSlotHeight(newFirstScrollingSlot);
            double firstRowRemaining = firstRowHeight - NegVerticalOffset;

            if (MathUtilities.LessThan(height, firstRowRemaining))
            {
                // Just covering more of the current first row
                NegVerticalOffset += height;
                _verticalOffset = newVerticalOffset;
                SetVerticalOffset(_verticalOffset);
                return;
            }

            // For large scrolls, use estimation
            if (height > 2 * CellsEstimatedHeight &&
                (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.VisibleWhenSelected || RowDetailsTemplate == null))
            {
                ScrollDownLargeDistance(height, newVerticalOffset, lastVisibleSlot);
                return;
            }

            // Small scroll - calculate target using estimates, then realize
            ScrollDownSmallDistance(height, newVerticalOffset, lastVisibleSlot);
        }

        /// <summary>
        /// Handles large scroll-down distances using estimation.
        /// </summary>
        private void ScrollDownLargeDistance(double height, double newVerticalOffset, int lastVisibleSlot)
        {
            ResetDisplayedRows();
            NegVerticalOffset = 0;

            var estimator = RowHeightEstimator;
            int newFirstScrollingSlot;
            
            if (estimator != null)
            {
                int estimatedSlot = estimator.EstimateSlotAtOffset(_verticalOffset + height, SlotCount);
                newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(estimatedSlot), lastVisibleSlot);
            }
            else
            {
                double singleRowHeightEstimate = RowHeightEstimate + 
                    (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible ? RowDetailsHeightEstimate : 0);
                int scrolledToSlot = DisplayData.FirstScrollingSlot + (int)(height / singleRowHeightEstimate);
                scrolledToSlot += _collapsedSlotsTable.GetIndexCount(DisplayData.FirstScrollingSlot, DisplayData.FirstScrollingSlot + scrolledToSlot);
                newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(scrolledToSlot), lastVisibleSlot);
            }

            UpdateDisplayedRows(newFirstScrollingSlot, CellsEstimatedHeight);
            FinalizeScrollPosition(newVerticalOffset);
        }

        /// <summary>
        /// Handles small scroll-down distances with precise calculation.
        /// </summary>
        private void ScrollDownSmallDistance(double height, double newVerticalOffset, int lastVisibleSlot)
        {
            int newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
            double deltaY = 0;
            double targetNegOffset = 0;

            // Phase 1: Calculate target slot using estimates (no state changes)
            double firstRowHeight = GetEstimatedSlotHeight(newFirstScrollingSlot);
            deltaY = firstRowHeight - NegVerticalOffset;
            
            if (MathUtilities.LessThan(height, deltaY))
            {
                // Just covering more of the current first row
                NegVerticalOffset += height;
                _verticalOffset = newVerticalOffset;
                SetVerticalOffset(_verticalOffset);
                return;
            }

            // Calculate which slot we'll end up on
            while (MathUtilities.LessThanOrClose(deltaY, height))
            {
                if (newFirstScrollingSlot >= lastVisibleSlot)
                {
                    targetNegOffset = 0;
                    break;
                }

                newFirstScrollingSlot = GetNextVisibleSlot(newFirstScrollingSlot);
                if (newFirstScrollingSlot < 0 || newFirstScrollingSlot > lastVisibleSlot)
                {
                    newFirstScrollingSlot = lastVisibleSlot;
                    targetNegOffset = 0;
                    break;
                }

                double rowHeight = GetEstimatedSlotHeight(newFirstScrollingSlot);
                double remainingHeight = height - deltaY;
                
                if (MathUtilities.LessThanOrClose(rowHeight, remainingHeight))
                {
                    deltaY += rowHeight;
                }
                else
                {
                    targetNegOffset = remainingHeight;
                    break;
                }
            }

            // Phase 2: Remove old rows from top and realize new slot
            while (DisplayData.FirstScrollingSlot < newFirstScrollingSlot && DisplayData.FirstScrollingSlot >= 0)
            {
                if (IsSlotVisible(DisplayData.FirstScrollingSlot))
                {
                    RemoveDisplayedElement(DisplayData.FirstScrollingSlot, false, true);
                }
                else
                {
                    break;
                }
            }

            // Phase 3: Ensure target slot is realized
            if (!IsSlotVisible(newFirstScrollingSlot))
            {
                // Need to rebuild display from target
                ResetDisplayedRows();
            }
            
            UpdateDisplayedRows(newFirstScrollingSlot, CellsEstimatedHeight);

            // Phase 4: Set NegVerticalOffset based on actual height
            double actualHeight = GetExactSlotElementHeight(DisplayData.FirstScrollingSlot);
            NegVerticalOffset = Math.Min(targetNegOffset, Math.Max(0, actualHeight - 1));

            FinalizeScrollPosition(newVerticalOffset);
        }

        /// <summary>
        /// Handles scroll-up operations with smooth height estimation.
        /// Uses two-phase approach: calculate target first, then rebuild display.
        /// </summary>
        private void ScrollSlotsByHeightUp(double height)
        {
            Debug.Assert(height < 0);
            
            double newVerticalOffset = _verticalOffset + height;

            // Check if we're just exposing more of the current first row
            if (MathUtilities.GreaterThanOrClose(height + NegVerticalOffset, 0))
            {
                NegVerticalOffset += height;
                _verticalOffset = newVerticalOffset;
                SetVerticalOffset(_verticalOffset);
                return;
            }

            // Check if we've scrolled to the top
            if (MathUtilities.LessThanOrClose(newVerticalOffset, 0))
            {
                ResetDisplayedRows();
                NegVerticalOffset = 0;
                UpdateDisplayedRows(0, CellsEstimatedHeight);
                _verticalOffset = NegVerticalOffset;
                SetVerticalOffset(_verticalOffset);
                return;
            }

            // For large scrolls, use estimation
            if (height < -2 * CellsEstimatedHeight &&
                (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.VisibleWhenSelected || RowDetailsTemplate == null))
            {
                ScrollUpLargeDistance(height, newVerticalOffset);
                return;
            }

            // Small scroll - use two-phase approach
            ScrollUpSmallDistance(height, newVerticalOffset);
        }

        /// <summary>
        /// Handles large scroll-up distances using estimation.
        /// </summary>
        private void ScrollUpLargeDistance(double height, double newVerticalOffset)
        {
            int newFirstScrollingSlot;
            
            if (newVerticalOffset <= 0)
            {
                newFirstScrollingSlot = 0;
            }
            else
            {
                var estimator = RowHeightEstimator;
                if (estimator != null)
                {
                    int estimatedSlot = estimator.EstimateSlotAtOffset(newVerticalOffset, SlotCount);
                    newFirstScrollingSlot = Math.Max(0, GetNextVisibleSlot(estimatedSlot - 1));
                }
                else
                {
                    double singleRowHeightEstimate = RowHeightEstimate + 
                        (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible ? RowDetailsHeightEstimate : 0);
                    int scrolledToSlot = DisplayData.FirstScrollingSlot + (int)(height / singleRowHeightEstimate);
                    scrolledToSlot -= _collapsedSlotsTable.GetIndexCount(scrolledToSlot, DisplayData.FirstScrollingSlot);
                    newFirstScrollingSlot = Math.Max(0, GetPreviousVisibleSlot(scrolledToSlot + 1));
                }
            }
            
            ResetDisplayedRows();
            NegVerticalOffset = 0;
            UpdateDisplayedRows(newFirstScrollingSlot, CellsEstimatedHeight);
            FinalizeScrollPosition(newVerticalOffset);
        }

        /// <summary>
        /// Handles small scroll-up distances using the two-phase approach.
        /// Phase 1: Calculate target slot using estimates (no state modifications)
        /// Phase 2: Reset display and rebuild from target
        /// </summary>
        private void ScrollUpSmallDistance(double height, double newVerticalOffset)
        {
            int currentFirstSlot = DisplayData.FirstScrollingSlot;
            double currentNegOffset = NegVerticalOffset;
            
            // Phase 1: Calculate target using estimates only
            double remainingScroll = -(height + currentNegOffset); // Make positive
            int targetSlot = currentFirstSlot;
            double targetNegOffset = 0;

            while (MathUtilities.GreaterThan(remainingScroll, 0) && targetSlot > 0)
            {
                int prevSlot = GetPreviousVisibleSlot(targetSlot);
                if (prevSlot < 0)
                {
                    targetSlot = 0;
                    targetNegOffset = 0;
                    break;
                }

                // Use estimate - no row realization here
                double prevHeight = GetEstimatedSlotHeight(prevSlot);

                if (MathUtilities.GreaterThanOrClose(prevHeight, remainingScroll))
                {
                    // This slot will be partially visible
                    targetSlot = prevSlot;
                    targetNegOffset = prevHeight - remainingScroll;
                    break;
                }

                remainingScroll -= prevHeight;
                targetSlot = prevSlot;
            }

            if (targetSlot <= 0)
            {
                targetSlot = 0;
                targetNegOffset = 0;
            }

            // Phase 2: Reset display and rebuild from target
            ResetDisplayedRows();
            UpdateDisplayedRows(targetSlot, CellsEstimatedHeight);

            // Phase 3: Set NegVerticalOffset based on actual measured height
            if (DisplayData.FirstScrollingSlot == targetSlot)
            {
                double actualHeight = GetExactSlotElementHeight(DisplayData.FirstScrollingSlot);
                NegVerticalOffset = Math.Min(targetNegOffset, Math.Max(0, actualHeight - 1));
            }
            else
            {
                // Display gave us a different slot - use 0 offset
                NegVerticalOffset = 0;
            }

            FinalizeScrollPosition(newVerticalOffset);
        }

        /// <summary>
        /// Finalizes the scroll position after display has been updated.
        /// Handles edge cases and ensures consistency.
        /// </summary>
        private void FinalizeScrollPosition(double targetVerticalOffset)
        {
            // Ensure first row height is valid
            double firstRowHeight = GetExactSlotElementHeight(DisplayData.FirstScrollingSlot);
            
            if (MathUtilities.LessThan(firstRowHeight, NegVerticalOffset))
            {
                // NegVerticalOffset is larger than the row - move to next row
                if (DisplayData.FirstScrollingSlot < SlotCount - 1)
                {
                    int nextSlot = GetNextVisibleSlot(DisplayData.FirstScrollingSlot);
                    if (nextSlot >= 0)
                    {
                        UpdateDisplayedRows(nextSlot, CellsEstimatedHeight);
                    }
                }
                NegVerticalOffset = 0;
            }

            // Handle correction for NegVerticalOffset > firstElementHeight
            double firstElementHeight = GetExactSlotElementHeight(DisplayData.FirstScrollingSlot);
            if (MathUtilities.GreaterThan(NegVerticalOffset, firstElementHeight))
            {
                int firstElementSlot = DisplayData.FirstScrollingSlot;
                while (DisplayData.FirstScrollingSlot > 0 && MathUtilities.GreaterThan(NegVerticalOffset, firstElementHeight))
                {
                    int previousSlot = GetPreviousVisibleSlot(firstElementSlot);
                    if (previousSlot == -1)
                    {
                        NegVerticalOffset = 0;
                        _verticalOffset = 0;
                        break;
                    }
                    else
                    {
                        NegVerticalOffset -= firstElementHeight;
                        _verticalOffset = Math.Max(0, _verticalOffset - firstElementHeight);
                        firstElementSlot = previousSlot;
                        firstElementHeight = GetExactSlotElementHeight(firstElementSlot);
                    }
                }
                if (firstElementSlot != DisplayData.FirstScrollingSlot)
                {
                    UpdateDisplayedRows(firstElementSlot, CellsEstimatedHeight);
                }
            }

            Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
            Debug.Assert(GetExactSlotElementHeight(DisplayData.FirstScrollingSlot) > NegVerticalOffset);

            // Calculate final vertical offset
            if (DisplayData.FirstScrollingSlot == 0)
            {
                _verticalOffset = NegVerticalOffset;
            }
            else if (MathUtilities.GreaterThan(NegVerticalOffset, targetVerticalOffset))
            {
                // The scrolled-in row was larger than anticipated
                NegVerticalOffset = targetVerticalOffset;
                _verticalOffset = targetVerticalOffset;
            }
            else
            {
                _verticalOffset = targetVerticalOffset;
            }

            Debug.Assert(!(_verticalOffset == 0 && NegVerticalOffset == 0 && DisplayData.FirstScrollingSlot > 0));

            SetVerticalOffset(_verticalOffset);

            Debug.Assert(MathUtilities.GreaterThanOrClose(NegVerticalOffset, 0));
            Debug.Assert(MathUtilities.GreaterThanOrClose(_verticalOffset, NegVerticalOffset));
        }
    }
}
