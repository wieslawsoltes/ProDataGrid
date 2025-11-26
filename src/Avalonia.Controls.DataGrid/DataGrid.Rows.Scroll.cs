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
        private void ScrollSlotsByHeight(double height)
        {
            Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
            Debug.Assert(!MathUtilities.IsZero(height));

            _scrollingByHeight = true;
            try
            {
                double deltaY = 0;
                int newFirstScrollingSlot = DisplayData.FirstScrollingSlot;
                double newVerticalOffset = _verticalOffset + height;
                if (height > 0)
                {
                    // Scrolling Down
                    int lastVisibleSlot = GetPreviousVisibleSlot(SlotCount);
                    if (HasLegacyVerticalScrollBar && MathUtilities.LessThanOrClose(GetLegacyVerticalScrollMaximum(), newVerticalOffset))
                    {
                        // We've scrolled to the bottom of the ScrollBar, automatically place the user at the very bottom
                        // of the DataGrid.  If this produces very odd behavior, evaluate the coping strategy used by
                        // OnRowMeasure(Size).  For most data, this should be unnoticeable.
                        ResetDisplayedRows();
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
                            if (height > 2 * CellsEstimatedHeight &&
                            (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.VisibleWhenSelected || RowDetailsTemplate == null))
                            {
                                // Very large scroll occurred. Instead of determining the exact number of scrolled off rows,
                                // let's estimate the number based on RowHeight.
                                ResetDisplayedRows();
                                
                                var estimator = RowHeightEstimator;
                                if (estimator != null)
                                {
                                    // Use the estimator's slot-at-offset calculation for better accuracy
                                    int estimatedSlot = estimator.EstimateSlotAtOffset(_verticalOffset + height, SlotCount);
                                    newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(estimatedSlot), lastVisibleSlot);
                                }
                                else
                                {
                                    double singleRowHeightEstimate = RowHeightEstimate + (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible ? RowDetailsHeightEstimate : 0);
                                    int scrolledToSlot = newFirstScrollingSlot + (int)(height / singleRowHeightEstimate);
                                    scrolledToSlot += _collapsedSlotsTable.GetIndexCount(newFirstScrollingSlot, newFirstScrollingSlot + scrolledToSlot);
                                    newFirstScrollingSlot = Math.Min(GetNextVisibleSlot(scrolledToSlot), lastVisibleSlot);
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

                                    double rowHeight = GetExactSlotElementHeight(newFirstScrollingSlot);
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

                        if (height < -2 * CellsEstimatedHeight &&
                        (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.VisibleWhenSelected || RowDetailsTemplate == null))
                        {
                            // Very large scroll occurred. Instead of determining the exact number of scrolled off rows,
                            // let's estimate the number based on RowHeight.
                            if (newVerticalOffset == 0)
                            {
                                newFirstScrollingSlot = 0;
                            }
                            else
                            {
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
                            ResetDisplayedRows();
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
                                
                                double rowHeight = GetExactSlotElementHeight(newFirstScrollingSlot);
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

                double firstRowHeight = GetExactSlotElementHeight(newFirstScrollingSlot);
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

                double firstElementHeight = GetExactSlotElementHeight(DisplayData.FirstScrollingSlot);
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
                            firstElementHeight = GetExactSlotElementHeight(firstElementSlot);
                        }
                    }
                    // We could be smarter about this, but it's not common so we wouldn't gain much from optimizing here
                    if (firstElementSlot != DisplayData.FirstScrollingSlot)
                    {
                        UpdateDisplayedRows(firstElementSlot, CellsEstimatedHeight);
                    }
                }

                Debug.Assert(DisplayData.FirstScrollingSlot >= 0);
                Debug.Assert(GetExactSlotElementHeight(DisplayData.FirstScrollingSlot) > NegVerticalOffset);

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


    }
}
