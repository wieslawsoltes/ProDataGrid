// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Utils;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Data;
using Avalonia.Styling;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {

        internal bool AreRowBottomGridLinesRequired
        {
            get
            {
                return (GridLinesVisibility == DataGridGridLinesVisibility.Horizontal || GridLinesVisibility == DataGridGridLinesVisibility.All) && HorizontalGridLinesBrush != null;
            }
        }

        internal int FirstVisibleSlot
        {
            get
            {
                return (SlotCount > 0) ? GetNextVisibleSlot(-1) : -1;
            }
        }

        internal int FrozenColumnCountWithFiller
        {
            get
            {
                int count = FrozenColumnCount;
                if (ColumnsInternal.RowGroupSpacerColumn.IsRepresented && (AreRowGroupHeadersFrozen || count > 0))
                {
                    // Either the RowGroupHeaders are frozen by default or the user set a frozen column count.  In both cases, we need to freeze
                    // one more column than the what the public value says
                    count++;
                }
                return count;
            }
        }

        internal int LastVisibleSlot
        {
            get
            {
                return (SlotCount > 0) ? GetPreviousVisibleSlot(SlotCount) : -1;
            }
        }

        // Cumulated height of all known rows, including the gridlines and details section.
        // This property returns an approximation of the actual total row heights and also
        // updates the RowHeightEstimate
        private double EdgedRowsHeightCalculated
        {
            get
            {
                // If we're not displaying any rows or if we have infinite space the, relative height of our rows is 0
                if (DisplayData.LastScrollingSlot == -1 || double.IsPositiveInfinity(AvailableSlotElementRoom))
                {
                    return 0;
                }
                Debug.Assert(DisplayData.LastScrollingSlot >= 0);
                Debug.Assert(_verticalOffset >= 0);
                Debug.Assert(NegVerticalOffset >= 0);

                // Height of all rows above the viewport
                double totalRowsHeight = _verticalOffset - NegVerticalOffset;

                // Add the height of all the rows currently displayed, AvailableRowRoom
                // is not always up to date enough for this
                foreach (Control element in DisplayData.GetScrollingElements())
                {
                    if (element is DataGridRow row)
                    {
                        totalRowsHeight += row.TargetHeight;
                    }
                    else
                    {
                        totalRowsHeight += element.DesiredSize.Height;
                    }
                }

                // Details up to and including viewport
                int detailsCount = GetDetailsCountInclusive(0, DisplayData.LastScrollingSlot);

                // Subtract details that were accounted for from the totalRowsHeight
                totalRowsHeight -= detailsCount * RowDetailsHeightEstimate;

                // Update the RowHeightEstimate if we have more row information
                if (DisplayData.LastScrollingSlot >= _lastEstimatedRow)
                {
                    _lastEstimatedRow = DisplayData.LastScrollingSlot;
                    RowHeightEstimate = totalRowsHeight / (_lastEstimatedRow + 1 - _collapsedSlotsTable.GetIndexCount(0, _lastEstimatedRow));
                }

                // Calculate estimates for what's beyond the viewport
                if (VisibleSlotCount > DisplayData.NumDisplayedScrollingElements)
                {
                    int remainingRowCount = (SlotCount - DisplayData.LastScrollingSlot - _collapsedSlotsTable.GetIndexCount(DisplayData.LastScrollingSlot, SlotCount - 1) - 1);

                    // Add estimation for the cell heights of all rows beyond our viewport
                    totalRowsHeight += RowHeightEstimate * remainingRowCount;

                    // Add the rest of the details beyond the viewport
                    detailsCount += GetDetailsCountInclusive(DisplayData.LastScrollingSlot + 1, SlotCount - 1);
                }

                //
                double totalDetailsHeight = detailsCount * RowDetailsHeightEstimate;

                return totalRowsHeight + totalDetailsHeight;
            }
        }

        /// <summary>
        /// Clears the entire selection except the indicated row. Displayed rows are deselected explicitly to
        /// visualize potential transition effects. The row indicated is selected if it is not already.
        /// </summary>
        internal void ClearRowSelection(int slotException, bool setAnchorSlot)
        {
            _noSelectionChangeCount++;
            try
            {
                bool exceptionAlreadySelected = false;
                if (_selectedItems.Count > 0)
                {
                    // Individually deselecting displayed rows to view potential transitions
                    for (int slot = DisplayData.FirstScrollingSlot;
                         slot > -1 && slot <= DisplayData.LastScrollingSlot;
                         slot++)
                    {
                        if (slot != slotException && _selectedItems.ContainsSlot(slot))
                        {
                            SelectSlot(slot, false);
                            SelectionHasChanged = true;
                        }
                    }
                    exceptionAlreadySelected = _selectedItems.ContainsSlot(slotException);
                    int selectedCount = _selectedItems.Count;
                    if (selectedCount > 0)
                    {
                        if (selectedCount > 1)
                        {
                            SelectionHasChanged = true;
                        }
                        else
                        {
                            int currentlySelectedSlot = _selectedItems.GetIndexes().First();
                            if (currentlySelectedSlot != slotException)
                            {
                                SelectionHasChanged = true;
                            }
                        }
                        _selectedItems.ClearRows();
                    }
                }
                if (exceptionAlreadySelected)
                {
                    // Exception row was already selected. It just needs to be marked as selected again.
                    // No transition involved.
                    _selectedItems.SelectSlot(slotException, true /*select*/);
                    if (setAnchorSlot)
                    {
                        AnchorSlot = slotException;
                    }
                }
                else
                {
                    // Exception row was not selected. It needs to be selected with potential transition
                    SetRowSelection(slotException, true /*isSelected*/, setAnchorSlot);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
        }

        /// <summary>
        /// Returns the row associated to the provided backend data item.
        /// </summary>
        /// <param name="dataItem">backend data item</param>
        /// <returns>null if the DataSource is null, the provided item in not in the source, or the item is not displayed; otherwise, the associated Row</returns>
        internal DataGridRow GetRowFromItem(object dataItem)
        {
            int rowIndex = DataConnection.IndexOf(dataItem);
            if (rowIndex < 0)
            {
                return null;
            }
            int slot = SlotFromRowIndex(rowIndex);
            return IsSlotVisible(slot) ? DisplayData.GetDisplayedElement(slot) as DataGridRow : null;
        }

        internal void InsertElementAt(int slot, int rowIndex, object item, DataGridRowGroupInfo groupInfo, bool isCollapsed)
        {
            Debug.Assert(slot >= 0 && slot <= SlotCount);

            bool isRow = rowIndex != -1;
            if (isCollapsed)
            {
                InsertElement(slot,
                    element: null,
                    updateVerticalScrollBarOnly: true,
                    isCollapsed: true,
                    isRow: isRow);
            }
            else if (SlotIsDisplayed(slot))
            {
                // Row at that index needs to be displayed
                if (isRow)
                {
                    InsertElement(slot, GenerateRow(rowIndex, slot, item), false /*updateVerticalScrollBarOnly*/, false /*isCollapsed*/, isRow);
                }
                else
                {
                    InsertElement(slot, GenerateRowGroupHeader(slot, groupInfo),
                        updateVerticalScrollBarOnly: false,
                        isCollapsed: false,
                        isRow: isRow);
                }
            }
            else
            {
                InsertElement(slot,
                    element: null,
                    updateVerticalScrollBarOnly: !HasLegacyVerticalScrollBar || IsLegacyVerticalScrollBarVisible,
                    isCollapsed: false,
                    isRow: isRow);
            }
        }

        internal void InsertRowAt(int rowIndex)
        {
            int slot = SlotFromRowIndex(rowIndex);
            object item = DataConnection.GetDataItem(rowIndex);

            // isCollapsed below is always false because we only use the method if we're not grouping
            InsertElementAt(slot, rowIndex, item, null/*DataGridRowGroupInfo*/, false /*isCollapsed*/);
        }

        internal bool IsColumnDisplayed(int columnIndex)
        {
            return columnIndex >= FirstDisplayedNonFillerColumnIndex && columnIndex <= DisplayData.LastTotallyDisplayedScrollingCol;
        }

        internal bool IsRowRecyclable(DataGridRow row)
        {
            return (row != EditingRow && row != _focusedRow);
        }

        internal void OnRowsMeasure()
        {
            if (!MathUtilities.IsZero(DisplayData.PendingVerticalScrollHeight))
            {
                ScrollSlotsByHeight(DisplayData.PendingVerticalScrollHeight);
                DisplayData.PendingVerticalScrollHeight = 0;
            }
        }

        internal void RefreshRows(bool recycleRows, bool clearRows)
        {
            if (_measured)
            {
                // _desiredCurrentColumnIndex is used in MakeFirstDisplayedCellCurrentCell to set the
                // column position back to what it was before the refresh
                _desiredCurrentColumnIndex = CurrentColumnIndex;
                double verticalOffset = _verticalOffset;
                if (DisplayData.PendingVerticalScrollHeight > 0)
                {
                    // Use the pending vertical scrollbar position if there is one, in the case that the collection
                    // has been reset multiple times in a row.
                    verticalOffset = DisplayData.PendingVerticalScrollHeight;
                }
                _verticalOffset = 0;
                NegVerticalOffset = 0;

                if (clearRows)
                {
                    ClearRows(recycleRows);
                    ClearRowGroupHeadersTable();
                    PopulateRowGroupHeadersTable();
                }

                RefreshRowGroupHeaders();

                // Update the CurrentSlot because it might have changed
                if (recycleRows && DataConnection.CollectionView != null)
                {
                    CurrentSlot = DataConnection.CollectionView.CurrentPosition == -1
                        ? -1 : SlotFromRowIndex(DataConnection.CollectionView.CurrentPosition);
                    if (CurrentSlot == -1)
                    {
                        SetCurrentCellCore(-1, -1);
                    }
                }

                if (DataConnection != null && ColumnsItemsInternal.Count > 0)
                {
                    AddSlots(DataConnection.Count);
                    AddSlots(DataConnection.Count + RowGroupHeadersTable.IndexCount);

                    InvalidateMeasure();
                }

                EnsureRowGroupSpacerColumn();

                if (HasLegacyVerticalScrollBar)
                {
                    DisplayData.PendingVerticalScrollHeight = Math.Min(verticalOffset, GetLegacyVerticalScrollMaximum());
                }
            }
            else
            {
                if (clearRows)
                {
                    ClearRows(recycleRows);
                }
                ClearRowGroupHeadersTable();
                PopulateRowGroupHeadersTable();
            }
        }

        internal void RemoveRowAt(int rowIndex, object item)
        {
            RemoveElementAt(SlotFromRowIndex(rowIndex), item, true);
        }

        internal bool ScrollSlotIntoView(int slot, bool scrolledHorizontally)
        {
            Debug.Assert(_collapsedSlotsTable.Contains(slot) || !IsSlotOutOfBounds(slot));

            if (scrolledHorizontally && DisplayData.FirstScrollingSlot <= slot && DisplayData.LastScrollingSlot >= slot)
            {
                // If the slot is displayed and we scrolled horizontally, column virtualization could cause the rows to grow.
                // As a result we need to force measure on the rows we're displaying and recalculate our First and Last slots
                // so they're accurate
                foreach (DataGridRow row in DisplayData.GetScrollingRows())
                {
                    row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                }
                UpdateDisplayedRows(DisplayData.FirstScrollingSlot, CellsEstimatedHeight);
            }

            if (DisplayData.FirstScrollingSlot < slot && (DisplayData.LastScrollingSlot > slot || DisplayData.LastScrollingSlot == -1))
            {
                // The row is already displayed in its entirety
                return true;
            }
            else if (DisplayData.FirstScrollingSlot == slot && slot != -1)
            {
                if (!MathUtilities.IsZero(NegVerticalOffset))
                {
                    // First displayed row is partially scrolled of. Let's scroll it so that NegVerticalOffset becomes 0.
                    DisplayData.PendingVerticalScrollHeight = -NegVerticalOffset;
                    InvalidateRowsMeasure(false /*invalidateIndividualRows*/);
                }
                return true;
            }

            double deltaY = 0;
            int firstFullSlot;
            if (DisplayData.FirstScrollingSlot > slot)
            {
                // Scroll up to the new row so it becomes the first displayed row
                firstFullSlot = DisplayData.FirstScrollingSlot - 1;
                if (MathUtilities.GreaterThan(NegVerticalOffset, 0))
                {
                    deltaY = -NegVerticalOffset;
                }
                deltaY -= GetSlotElementsHeight(slot, firstFullSlot);
                if (DisplayData.FirstScrollingSlot - slot > 1)
                {
                    //

                    ResetDisplayedRows();
                }
                NegVerticalOffset = 0;
                UpdateDisplayedRows(slot, CellsEstimatedHeight);
            }
            else if (DisplayData.LastScrollingSlot <= slot)
            {
                // Scroll down to the new row so it's entirely displayed.  If the height of the row
                // is greater than the height of the DataGrid, then show the top of the row at the top
                // of the grid
                firstFullSlot = DisplayData.LastScrollingSlot;
                // Figure out how much of the last row is cut off
                double rowHeight = GetExactSlotElementHeight(DisplayData.LastScrollingSlot);
                double availableHeight = AvailableSlotElementRoom + rowHeight;
                if (MathUtilities.AreClose(rowHeight, availableHeight))
                {
                    if (DisplayData.LastScrollingSlot == slot)
                    {
                        // We're already at the very bottom so we don't need to scroll down further
                        return true;
                    }
                    else
                    {
                        // We're already showing the entire last row so don't count it as part of the delta
                        firstFullSlot++;
                    }
                }
                else if (rowHeight > availableHeight)
                {
                    firstFullSlot++;
                    deltaY += rowHeight - availableHeight;
                }
                // sum up the height of the rest of the full rows
                if (slot >= firstFullSlot)
                {
                    deltaY += GetSlotElementsHeight(firstFullSlot, slot);
                }
                // If the first row we're displaying is no longer adjacent to the rows we have
                // simply discard the ones we have
                if (slot - DisplayData.LastScrollingSlot > 1)
                {
                    ResetDisplayedRows();
                }
                if (MathUtilities.GreaterThanOrClose(GetExactSlotElementHeight(slot), CellsEstimatedHeight))
                {
                    // The entire row won't fit in the DataGrid so we start showing it from the top
                    NegVerticalOffset = 0;
                    UpdateDisplayedRows(slot, CellsEstimatedHeight);
                }
                else
                {
                    UpdateDisplayedRowsFromBottom(slot);
                }
            }

            _verticalOffset += deltaY;
            if (_verticalOffset < 0 || DisplayData.FirstScrollingSlot == 0)
            {
                // We scrolled too far because a row's height was larger than its approximation
                _verticalOffset = NegVerticalOffset;
            }

            //
            Debug.Assert(MathUtilities.LessThanOrClose(NegVerticalOffset, _verticalOffset));

            SetVerticalOffset(_verticalOffset);

            InvalidateMeasure();
            InvalidateRowsMeasure(false /*invalidateIndividualRows*/);

            return true;
        }

        // For now, all scenarios are for isSelected == true.
        internal void SetRowsSelection(int startSlot, int endSlot /*, bool isSelected*/)
        {
            Debug.Assert(startSlot >= 0 && startSlot < SlotCount);
            Debug.Assert(endSlot >= 0 && endSlot < SlotCount);
            Debug.Assert(startSlot <= endSlot);

            _noSelectionChangeCount++;
            try
            {
                if (/*isSelected &&*/ !_selectedItems.ContainsAll(startSlot, endSlot))
                {
                    // At least one row gets selected
                    SelectSlots(startSlot, endSlot, true);
                    SelectionHasChanged = true;
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
        }

        private void ApplyDisplayedRowsState(int startSlot, int endSlot)
        {
            int firstSlot = Math.Max(DisplayData.FirstScrollingSlot, startSlot);
            int lastSlot = Math.Min(DisplayData.LastScrollingSlot, endSlot);

            if (firstSlot >= 0)
            {
                Debug.Assert(lastSlot >= firstSlot);
                int slot = GetNextVisibleSlot(firstSlot - 1);
                while (slot <= lastSlot)
                {
                    if (DisplayData.GetDisplayedElement(slot) is DataGridRow row)
                    {
                        row.ApplyState();
                    }
                    slot = GetNextVisibleSlot(slot);
                }
            }
        }

        private void ClearRows(bool recycle)
        {
            // Need to clean up recycled rows even if the RowCount is 0
            SetCurrentCellCore(-1, -1, commitEdit: false, endRowEdit: false);
            ClearRowSelection(resetAnchorSlot: true);
            UnloadElements(recycle);

            _showDetailsTable.Clear();
            SlotCount = 0;
            NegVerticalOffset = 0;
            SetVerticalOffset(0);
            ComputeScrollBarsLayout();
        }

        // Updates _collapsedSlotsTable and returns the number of pixels that were collapsed
        private double CollapseSlotsInTable(int startSlot, int endSlot, ref int slotsExpanded, int lastDisplayedSlot, ref double heightChangeBelowLastDisplayedSlot)
        {
            int firstSlot = startSlot;
            int lastSlot;
            double totalHeightChange = 0;
            // Figure out which slots actually need to be expanded since some might already be collapsed
            while (firstSlot <= endSlot)
            {
                firstSlot = _collapsedSlotsTable.GetNextGap(firstSlot - 1);
                int nextCollapsedSlot = _collapsedSlotsTable.GetNextIndex(firstSlot) - 1;
                lastSlot = nextCollapsedSlot == -2 ? endSlot : Math.Min(endSlot, nextCollapsedSlot);

                if (firstSlot <= lastSlot)
                {
                    double heightChange = GetHeightEstimate(firstSlot, lastSlot);
                    totalHeightChange -= heightChange;
                    slotsExpanded -= lastSlot - firstSlot + 1;

                    if (lastSlot > lastDisplayedSlot)
                    {
                        if (firstSlot > lastDisplayedSlot)
                        {
                            heightChangeBelowLastDisplayedSlot -= heightChange;
                        }
                        else
                        {
                            heightChangeBelowLastDisplayedSlot -= GetHeightEstimate(lastDisplayedSlot + 1, lastSlot);
                        }
                    }

                    firstSlot = lastSlot + 1;
                }
            }

            // Update _collapsedSlotsTable in one bulk operation
            _collapsedSlotsTable.AddValues(startSlot, endSlot - startSlot + 1, false);

            return totalHeightChange;
        }

        private static void CorrectRowAfterDeletion(DataGridRow row, bool rowDeleted)
        {
            row.Slot--;
            if (rowDeleted)
            {
                row.Index--;
            }
        }

        private static void CorrectRowAfterInsertion(DataGridRow row, bool rowInserted)
        {
            row.Slot++;
            if (rowInserted)
            {
                row.Index++;
            }
        }

        /// <summary>
        /// Adjusts the index of all displayed, loaded and edited rows after a row was deleted.
        /// Removes the deleted row from the list of loaded rows if present.
        /// </summary>
        private void CorrectSlotsAfterDeletion(int slotDeleted, bool wasRow)
        {
            Debug.Assert(slotDeleted >= 0);

            // Take care of the non-visible loaded rows
            for (int index = 0; index < _loadedRows.Count;)
            {
                DataGridRow dataGridRow = _loadedRows[index];
                if (IsSlotVisible(dataGridRow.Slot))
                {
                    index++;
                }
                else
                {
                    if (dataGridRow.Slot > slotDeleted)
                    {
                        CorrectRowAfterDeletion(dataGridRow, wasRow);
                        index++;
                    }
                    else if (dataGridRow.Slot == slotDeleted)
                    {
                        _loadedRows.RemoveAt(index);
                    }
                    else
                    {
                        index++;
                    }
                }
            }

            // Take care of the non-visible edited row
            if (EditingRow != null &&
                !IsSlotVisible(EditingRow.Slot) &&
                EditingRow.Slot > slotDeleted)
            {
                CorrectRowAfterDeletion(EditingRow, wasRow);
            }

            // Take care of the non-visible focused row
            if (_focusedRow != null &&
                _focusedRow != EditingRow &&
                !IsSlotVisible(_focusedRow.Slot) &&
                _focusedRow.Slot > slotDeleted)
            {
                CorrectRowAfterDeletion(_focusedRow, wasRow);
            }

            // Take care of the visible rows
            foreach (DataGridRow row in DisplayData.GetScrollingRows())
            {
                if (row.Slot > slotDeleted)
                {
                    CorrectRowAfterDeletion(row, wasRow);
                    _rowsPresenter?.InvalidateChildIndex(row);
                }
            }

            // Update the RowGroupHeaders
            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo.Slot > slotDeleted)
                {
                    rowGroupInfo.Slot--;
                }
                if (rowGroupInfo.LastSubItemSlot >= slotDeleted)
                {
                    rowGroupInfo.LastSubItemSlot--;
                }
            }

            // Update which row we've calculated the RowHeightEstimate up to
            if (_lastEstimatedRow >= slotDeleted)
            {
                _lastEstimatedRow--;
            }
        }

        /// <summary>
        /// Adjusts the index of all displayed, loaded and edited rows after rows were deleted.
        /// </summary>
        private void CorrectSlotsAfterInsertion(int slotInserted, bool isCollapsed, bool rowInserted)
        {
            Debug.Assert(slotInserted >= 0);

            // Take care of the non-visible loaded rows
            foreach (DataGridRow dataGridRow in _loadedRows)
            {
                if (!IsSlotVisible(dataGridRow.Slot) && dataGridRow.Slot >= slotInserted)
                {
                    DataGrid.CorrectRowAfterInsertion(dataGridRow, rowInserted);
                }
            }

            // Take care of the non-visible focused row
            if (_focusedRow != null &&
                _focusedRow != EditingRow &&
                !(IsSlotVisible(_focusedRow.Slot) || ((_focusedRow.Slot == slotInserted) && isCollapsed)) &&
                _focusedRow.Slot >= slotInserted)
            {
                DataGrid.CorrectRowAfterInsertion(_focusedRow, rowInserted);
            }

            // Take care of the visible rows
            foreach (DataGridRow row in DisplayData.GetScrollingRows())
            {
                if (row.Slot >= slotInserted)
                {
                    DataGrid.CorrectRowAfterInsertion(row, rowInserted);
                    _rowsPresenter?.InvalidateChildIndex(row);
                }
            }

            // Re-calculate the EditingRow's Slot and Index and ensure that it is still selected.
            if (EditingRow != null)
            {
                EditingRow.Index = DataConnection.IndexOf(EditingRow.DataContext);
                EditingRow.Slot = SlotFromRowIndex(EditingRow.Index);
            }

            // Update the RowGroupHeaders
            foreach (int slot in RowGroupHeadersTable.GetIndexes(slotInserted))
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo.Slot >= slotInserted)
                {
                    rowGroupInfo.Slot++;
                }

                // We are purposefully checking GT and not GTE because the equality case is handled
                // by the CorrectLastSubItemSlotsAfterInsertion method
                if (rowGroupInfo.LastSubItemSlot > slotInserted)
                {
                    rowGroupInfo.LastSubItemSlot++;
                }
            }

            // Update which row we've calculated the RowHeightEstimate up to
            if (_lastEstimatedRow >= slotInserted)
            {
                _lastEstimatedRow++;
            }
        }

        private IEnumerable<DataGridRow> GetAllRows()
        {
            if (_rowsPresenter != null)
            {
                foreach (Control element in _rowsPresenter.Children)
                {
                    if (element is DataGridRow row)
                    {
                        yield return row;
                    }
                }
            }
        }

        // Expands slots from startSlot to endSlot inclusive and adds the amount expanded in this suboperation to
        // the given totalHeightChanged of the entire operation
        private void ExpandSlots(int startSlot, int endSlot, bool isDisplayed, ref int slotsExpanded, ref double totalHeightChange)
        {
            double heightAboveStartSlot = 0;
            if (isDisplayed)
            {
                int slot = DisplayData.FirstScrollingSlot;
                while (slot < startSlot)
                {
                    heightAboveStartSlot += GetExactSlotElementHeight(slot);
                    slot = GetNextVisibleSlot(slot);
                }

                // First make the bottom rows available for recycling so we minimize element creation when expanding
                for (int i = 0; (i < endSlot - startSlot + 1) && (DisplayData.LastScrollingSlot > endSlot); i++)
                {
                    RemoveDisplayedElement(DisplayData.LastScrollingSlot, wasDeleted: false, updateSlotInformation: true);
                }
            }

            // Figure out which slots actually need to be expanded since some might already be collapsed
            double currentHeightChange = 0;
            int firstSlot = startSlot;
            int lastSlot = endSlot;
            while (firstSlot <= endSlot)
            {
                firstSlot = _collapsedSlotsTable.GetNextIndex(firstSlot - 1);
                if (firstSlot == -1)
                {
                    break;
                }
                lastSlot = Math.Min(endSlot, _collapsedSlotsTable.GetNextGap(firstSlot) - 1);

                if (firstSlot <= lastSlot)
                {
                    if (!isDisplayed)
                    {
                        // Estimate the height change if the slots aren't displayed.  If they are displayed, we can add real values
                        double rowCount = lastSlot - firstSlot - GetRowGroupHeaderCount(firstSlot, lastSlot, false, out double headerHeight) + 1;
                        double detailsCount = GetDetailsCountInclusive(firstSlot, lastSlot);
                        currentHeightChange += headerHeight + (detailsCount * RowDetailsHeightEstimate) + (rowCount * RowHeightEstimate);
                    }
                    slotsExpanded += lastSlot - firstSlot + 1;
                    firstSlot = lastSlot + 1;
                }
            }

            // Update _collapsedSlotsTable in one bulk operation
            _collapsedSlotsTable.RemoveValues(startSlot, endSlot - startSlot + 1);

            if (isDisplayed)
            {
                double availableHeight = CellsEstimatedHeight - heightAboveStartSlot;
                // Actually expand the displayed slots up to what we can display
                for (int i = startSlot; (i <= endSlot) && (currentHeightChange < availableHeight); i++)
                {
                    Control insertedElement = InsertDisplayedElement(i, updateSlotInformation: false);
                    currentHeightChange += insertedElement.DesiredSize.Height;
                    if (i > DisplayData.LastScrollingSlot)
                    {
                        DisplayData.LastScrollingSlot = i;
                    }
                }
            }

            // Update the total height for the entire Expand operation
            totalHeightChange += currentHeightChange;
        }

        /// <summary>
        /// Returns the exact row height, whether it is currently displayed or not.
        /// The row is generated and added to the displayed rows in case it is not already displayed.
        /// The horizontal gridlines thickness are added.
        /// </summary>
        private double GetExactSlotElementHeight(int slot)
        {
            Debug.Assert((slot >= 0) && slot < SlotCount);

            if (IsSlotVisible(slot))
            {
                Debug.Assert(DisplayData.GetDisplayedElement(slot) != null);
                return DisplayData.GetDisplayedElement(slot).DesiredSize.Height;
            }

            Control slotElement = InsertDisplayedElement(slot, true /*updateSlotInformation*/);
            Debug.Assert(slotElement != null);
            return slotElement.DesiredSize.Height;
        }

        // Returns an estimate for the height of the slots between fromSlot and toSlot
        private double GetHeightEstimate(int fromSlot, int toSlot)
        {
            double rowCount = toSlot - fromSlot - GetRowGroupHeaderCount(fromSlot, toSlot, true, out double headerHeight) + 1;
            double detailsCount = GetDetailsCountInclusive(fromSlot, toSlot);

            return headerHeight + (detailsCount * RowDetailsHeightEstimate) + (rowCount * RowHeightEstimate);
        }

        /// <summary>
        /// If the provided slot is displayed, returns the exact height.
        /// If the slot is not displayed, returns a default height.
        /// </summary>
        private double GetSlotElementHeight(int slot)
        {
            Debug.Assert(slot >= 0 && slot < SlotCount);
            if (IsSlotVisible(slot))
            {
                Debug.Assert(DisplayData.GetDisplayedElement(slot) != null);
                return DisplayData.GetDisplayedElement(slot).DesiredSize.Height;
            }
            else
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo != null)
                {
                    return _rowGroupHeightsByLevel[rowGroupInfo.Level];
                }

                // Assume it's a row since we're either not grouping or it wasn't a RowGroupHeader
                return RowHeightEstimate + (GetRowDetailsVisibility(slot) ? RowDetailsHeightEstimate : 0);
            }
        }

        /// <summary>
        /// Cumulates the approximate height of the rows from fromRowIndex to toRowIndex included.
        /// Including the potential gridline thickness.
        /// </summary>
        private double GetSlotElementsHeight(int fromSlot, int toSlot)
        {
            Debug.Assert(toSlot >= fromSlot);

            double height = 0;
            for (int slot = fromSlot; slot <= toSlot; slot++)
            {
                height += GetSlotElementHeight(slot);
            }
            return height;
        }

        private void InvalidateRowHeightEstimate()
        {
            // Start from scratch and assume that we haven't estimated any rows
            _lastEstimatedRow = -1;
        }

        // Makes sure the row shows the proper visuals for selection, currency, details, etc.

        // Removes an element from display either because it was deleted or it was scrolled out of view.
        // If the element was provided, it will be the element removed; otherwise, the element will be
        // retrieved from the slot information

        // Updates display information and displayed rows after scrolling the given number of pixels

        // Similar to UpdateDisplayedRows except that it starts with the LastDisplayedScrollingRow
        // and computes the FirstDisplayScrollingRow instead of doing it the other way around.  We use this
        // when scrolling down to a full row

        private void UpdateTablesForRemoval(int slotDeleted, object itemDeleted)
        {
            if (RowGroupHeadersTable.Contains(slotDeleted))
            {
                // A RowGroupHeader was removed
                RowGroupHeadersTable.RemoveIndexAndValue(slotDeleted);
                _collapsedSlotsTable.RemoveIndexAndValue(slotDeleted);
                _selectedItems.DeleteSlot(slotDeleted);
            }
            else
            {
                // Update the ranges of selected rows
                if (_selectedItems.ContainsSlot(slotDeleted))
                {
                    SelectionHasChanged = true;
                }
                _selectedItems.Delete(slotDeleted, itemDeleted);
                RowGroupHeadersTable.RemoveIndex(slotDeleted);
                _collapsedSlotsTable.RemoveIndex(slotDeleted);
            }
        }

        // This method is necessary for incrementing the LastSubItemSlot property of the group ancestors
        // because CorrectSlotsAfterInsertion only increments those that come after the specified group

        // Returns the inclusive count of expanded RowGroupHeaders from startSlot to endSlot

        // This method does not check the state of the parent RowGroupHeaders, it assumes they're ready for this newVisibility to
        // be applied this header
        // Returns the number of pixels that were expanded or (collapsed); however, if we're expanding displayed rows, we only expand up
        // to what we can display

        // Returns the number of rows with details visible between lowerBound and upperBound exclusive.
        // As of now, the caller needs to account for Collapsed slots.  This method assumes everything
        // is visible

        // detailsElement is the FrameworkElement created by the DetailsTemplate

        // detailsElement is the FrameworkElement created by the DetailsTemplate

#if DEBUG
        internal void PrintRowGroupInfo()
        {
            Debug.WriteLine("-----------------------------------------------RowGroupHeaders");
            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                DataGridRowGroupInfo info = RowGroupHeadersTable.GetValueAt(slot);
                Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} Slot:{2} Last:{3} Level:{4}", info.CollectionViewGroup.Key, info.IsVisible.ToString(), slot, info.LastSubItemSlot, info.Level));
            }
            Debug.WriteLine("-----------------------------------------------CollapsedSlots");
            _collapsedSlotsTable.PrintIndexes();
        }
#endif
        internal void RemoveReferenceFromCollectionViewGroup(DataGridCollectionViewGroup rowGroupInfo)
        {
            if (rowGroupInfo is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= CollectionViewGroup_PropertyChanged;
            }
        }

        private void OnRowHeightChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                InvalidateRowHeightEstimate();
                // Re-measure all the rows due to the Height change
                InvalidateRowsMeasure(invalidateIndividualElements: true);
                // DataGrid needs to update the layout information and the ScrollBars
                InvalidateMeasure();
            }
        }

        private void OnRowHeaderWidthChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                EnsureRowHeaderWidth();
            }
        }

        private void OnAreRowGroupHeadersFrozenChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var value = (bool)e.NewValue;
            ProcessFrozenColumnCount();

            // Update elements in the RowGroupHeader that were previously frozen
            if (value)
            {
                if (_rowsPresenter != null)
                {
                    foreach (Control element in _rowsPresenter.Children)
                    {
                        if (element is DataGridRowGroupHeader groupHeader)
                        {
                            groupHeader.ClearFrozenStates();
                        }
                    }
                }
            }
        }

        protected virtual void OnLoadingRow(DataGridRowEventArgs e)
        {
            EventHandler<DataGridRowEventArgs> handler = LoadingRow;
            if (handler != null)
            {
                Debug.Assert(!_loadedRows.Contains(e.Row));
                _loadedRows.Add(e.Row);
                LoadingOrUnloadingRow = true;
                handler(this, e);
                LoadingOrUnloadingRow = false;
                Debug.Assert(_loadedRows.Contains(e.Row));
                _loadedRows.Remove(e.Row);
            }
        }

        protected virtual void OnUnloadingRow(DataGridRowEventArgs e)
        {
            EventHandler<DataGridRowEventArgs> handler = UnloadingRow;
            if (handler != null)
            {
                LoadingOrUnloadingRow = true;
                handler(this, e);
                LoadingOrUnloadingRow = false;
            }
        }

    }
}