// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Utils;
using Avalonia.Utilities;
using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Navigation and scrolling
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {

        /// <summary>
        /// Scrolls the specified item or RowGroupHeader and/or column into view.
        /// If item is not null: scrolls the row representing the item into view;
        /// If column is not null: scrolls the column into view;
        /// If both item and column are null, the method returns without scrolling.
        /// </summary>
        /// <param name="item">an item from the DataGrid's items source or a CollectionViewGroup from the collection view</param>
        /// <param name="column">a column from the DataGrid's columns collection</param>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void ScrollIntoView(object item, DataGridColumn column)
        {
            if ((column == null && (item == null || FirstDisplayedNonFillerColumnIndex == -1))
                || (column != null && column.OwningGrid != this))
            {
                // no-op
                return;
            }
            if (item == null)
            {
                // scroll column into view
                ScrollSlotIntoView(
                    column.Index,
                    DisplayData.FirstScrollingSlot,
                    forCurrentCellChange: false,
                    forceHorizontalScroll: true);
            }
            else
            {
                int slot = -1;
                DataGridRowGroupInfo rowGroupInfo = null;
                if (item is DataGridCollectionViewGroup collectionViewGroup)
                {
                    rowGroupInfo = RowGroupInfoFromCollectionViewGroup(collectionViewGroup);
                    if (rowGroupInfo == null)
                    {
                        Debug.Assert(false);
                        return;
                    }
                    slot = rowGroupInfo.Slot;
                }
                else
                {
                    // the row index will be set to -1 if the item is null or not in the list
                    if (!TryGetRowIndexFromItem(item, out var rowIndex))
                    {
                        return;
                    }
                    slot = SlotFromRowIndex(rowIndex);
                }

                int columnIndex = (column == null) ? FirstDisplayedNonFillerColumnIndex : column.Index;

                if (_collapsedSlotsTable.Contains(slot))
                {
                    // We need to expand all parent RowGroups so that the slot is visible
                    if (rowGroupInfo != null)
                    {
                        ExpandRowGroupParentChain(rowGroupInfo.Level - 1, rowGroupInfo.Slot);
                    }
                    else
                    {
                        rowGroupInfo = RowGroupHeadersTable.GetValueAt(RowGroupHeadersTable.GetPreviousIndex(slot));
                        Debug.Assert(rowGroupInfo != null);
                        if (rowGroupInfo != null)
                        {
                            ExpandRowGroupParentChain(rowGroupInfo.Level, rowGroupInfo.Slot);
                        }
                    }

                    // Update Scrollbar and display information
                    NegVerticalOffset = 0;
                    SetVerticalOffset(0);
                    ResetDisplayedRows();
                    DisplayData.FirstScrollingSlot = 0;
                    ComputeScrollBarsLayout();
                }

                ScrollSlotIntoView(
                    columnIndex, slot,
                    forCurrentCellChange: true,
                    forceHorizontalScroll: true);
            }
        }

        /// <summary>
        /// Brings a row represented by an unrealized automation peer into the realized viewport.
        /// </summary>
        /// <remarks>
        /// This is deliberately vertical-only. It reuses/recycles the current row containers and
        /// does not force cell creation for horizontally virtualized columns.
        /// </remarks>
        internal bool BringRowIntoViewForAutomation(object item)
        {
            // Hierarchy expansion can invalidate and clear the realized range before the next
            // layout pass. Automation may request the newly changed item immediately, so make
            // the pending layout state usable before resolving the vertical scroll.
            if (IsAttachedToVisualTree &&
                (DisplayData.FirstScrollingSlot == -1 ||
                 MathUtilities.LessThanOrClose(CellsEstimatedHeight, 0)))
            {
                UpdateLayout();
            }

            if (item == null ||
                !TryGetRowIndexFromItem(item, out int rowIndex))
            {
                return false;
            }

            int slot = SlotFromRowIndex(rowIndex);
            if (slot < 0 || IsSlotOutOfBounds(slot) || !TryExpandCollapsedSlotForScroll(slot))
            {
                return false;
            }

            return ScrollSlotIntoView(slot, scrolledHorizontally: false);
        }


        internal bool ScrollSlotIntoView(int columnIndex, int slot, bool forCurrentCellChange, bool forceHorizontalScroll)
        {
            Debug.Assert(columnIndex >= 0 && columnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(DisplayData.FirstDisplayedScrollingCol >= -1 && DisplayData.FirstDisplayedScrollingCol < ColumnsItemsInternal.Count);
            Debug.Assert(DisplayData.LastTotallyDisplayedScrollingCol >= -1 && DisplayData.LastTotallyDisplayedScrollingCol < ColumnsItemsInternal.Count);
            Debug.Assert(!IsSlotOutOfBounds(slot));
            Debug.Assert(DisplayData.FirstScrollingSlot >= -1 && DisplayData.FirstScrollingSlot < SlotCount);
            Debug.Assert(ColumnsItemsInternal[columnIndex].IsVisible);

            if (CurrentColumnIndex >= 0 &&
                (CurrentColumnIndex != columnIndex || CurrentSlot != slot))
            {
                if (!CommitEditForOperation(columnIndex, slot, forCurrentCellChange) || IsInnerCellOutOfBounds(columnIndex, slot))
                {
                    return false;
                }
            }

            if (LayoutModel != null && _rowsPresenter != null)
            {
                return _rowsPresenter.ScrollLayoutIndexIntoView(GetLayoutIndexFromSlot(slot));
            }

            double oldHorizontalOffset = HorizontalOffset;

            //scroll horizontally unless we're on a RowGroupHeader and we're not forcing horizontal scrolling
            if ((forceHorizontalScroll || (slot != -1))
                && !ScrollColumnIntoView(columnIndex, forceHorizontalScroll))
            {
                return false;
            }

            //scroll vertically
            if (!ScrollSlotIntoView(slot, scrolledHorizontally: oldHorizontalOffset != HorizontalOffset))
            {
                return false;
            }

            return true;
        }


        internal bool UpdateHorizontalOffset(double newValue)
        {
            if (HorizontalOffset != newValue)
            {
                HorizontalOffset = newValue;

                InvalidateColumnHeadersMeasure();
                InvalidateRowsMeasure(true);
                UpdateSummaryRowLayout();
                RequestSelectionOverlayRefresh();
                return true;
            }
            return false;
        }


        private void SetVerticalOffset(double newVerticalOffset)
        {
            _verticalOffset = newVerticalOffset;
            SyncVerticalScrollBarValue(newVerticalOffset);
            RequestSelectionOverlayRefresh();
        }


        // Calculates the amount to scroll for the ScrollLeft button
        // This is a method rather than a property to emphasize a calculation
        private double GetHorizontalSmallScrollDecrease()
        {
            // If the first column is covered up, scroll to the start of it when the user clicks the left button
            if (_negHorizontalOffset > 0)
            {
                return _negHorizontalOffset;
            }
            else
            {
                if (IsColumnOutOfBounds(DisplayData.FirstDisplayedScrollingCol))
                {
                    return 0;
                }

                // The entire first column is displayed, show the entire previous column when the user clicks
                // the left button
                DataGridColumn previousColumn = ColumnsInternal.GetPreviousVisibleScrollingColumn(
                    ColumnsItemsInternal[DisplayData.FirstDisplayedScrollingCol]);
                if (previousColumn != null)
                {
                    return GetEdgedColumnWidth(previousColumn);
                }
                else
                {
                    // There's no previous column so don't move
                    return 0;
                }
            }
        }


        // Calculates the amount to scroll for the ScrollRight button
        // This is a method rather than a property to emphasize a calculation
        private double GetHorizontalSmallScrollIncrease()
        {
            if (!IsColumnOutOfBounds(DisplayData.FirstDisplayedScrollingCol))
            {
                return GetEdgedColumnWidth(ColumnsItemsInternal[DisplayData.FirstDisplayedScrollingCol]) - _negHorizontalOffset;
            }
            return 0;
        }


        // Calculates the amount the ScrollDown button should scroll
        // This is a method rather than a property to emphasize that calculations are taking place
        private double GetVerticalSmallScrollIncrease()
        {
            if (DisplayData.FirstScrollingSlot >= 0)
            {
                return GetExactSlotElementHeight(DisplayData.FirstScrollingSlot) - NegVerticalOffset;
            }
            return 0;
        }

    }
}
