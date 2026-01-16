// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Media;
using Avalonia.Controls.Utils;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Utilities;
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

            int slot = firstDisplayedScrollingSlot;
            while (slot < SlotCount && !MathUtilities.GreaterThanOrClose(deltaY, displayHeight))
            {
                deltaY += GetExactSlotElementHeight(slot);
                visibleScrollingRows++;
                lastDisplayedScrollingSlot = slot;
                slot = GetNextVisibleSlot(slot);
            }

            while (MathUtilities.LessThan(deltaY, displayHeight) && slot >= 0)
            {
                slot = GetPreviousVisibleSlot(firstDisplayedScrollingSlot);
                if (slot >= 0)
                {
                    deltaY += GetExactSlotElementHeight(slot);
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

            int slot = lastDisplayedScrollingRow;
            while (MathUtilities.LessThan(deltaY, displayHeight) && slot >= 0)
            {
                deltaY += GetExactSlotElementHeight(slot);
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
