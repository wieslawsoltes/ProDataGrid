// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;
using Avalonia.Media;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {

        private void InsertElement(int slot, Control element, bool updateVerticalScrollBarOnly, bool isCollapsed, bool isRow)
        {
            Debug.Assert(slot >= 0 && slot <= SlotCount);

            OnInsertingElement(slot, true /*firstInsertion*/, isCollapsed);   // will throw an exception if the insertion is illegal

            OnInsertedElement_Phase1(slot, element, isCollapsed, isRow);
            SlotCount++;
            if (!isCollapsed)
            {
                VisibleSlotCount++;
            }
            OnInsertedElement_Phase2(slot, updateVerticalScrollBarOnly, isCollapsed);
        }



        private void OnAddedElement_Phase1(int slot, Control element)
        {
            Debug.Assert(slot >= 0);

            // Row needs to be potentially added to the displayed rows
            if (SlotIsDisplayed(slot))
            {
                InsertDisplayedElement(slot, element, true /*wasNewlyAdded*/, true);
            }
        }



        private void OnAddedElement_Phase2(int slot, bool updateVerticalScrollBarOnly)
        {
            if (slot < DisplayData.FirstScrollingSlot - 1)
            {
                // The element was added above our viewport so it pushes the VerticalOffset down
                var estimator = RowHeightEstimator;
                double elementHeight;
                
                if (estimator != null)
                {
                    var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                    bool isHeader = rowGroupInfo != null;
                    int level = isHeader ? rowGroupInfo.Level : 0;
                    bool hasDetails = !isHeader && GetRowDetailsVisibility(slot);
                    elementHeight = estimator.GetEstimatedHeight(slot, isHeader, level, hasDetails);
                }
                else
                {
                    elementHeight = RowGroupHeadersTable.Contains(slot) ? RowGroupHeaderHeightEstimate : RowHeightEstimate;
                }

                SetVerticalOffset(_verticalOffset + elementHeight);
            }
            if (updateVerticalScrollBarOnly)
            {
                UpdateVerticalScrollBar();
            }
            else
            {
                ComputeScrollBarsLayout();
                // Reposition rows in case we use a recycled one
                InvalidateRowsArrange();
            }
        }



        private void OnElementsChanged(bool grew)
        {
            if (grew &&
            ColumnsItemsInternal.Count > 0 &&
            CurrentColumnIndex == -1)
            {
                MakeFirstDisplayedCellCurrentCell();
            }
        }



        private void OnInsertedElement_Phase1(int slot, Control element, bool isCollapsed, bool isRow)
        {
            Debug.Assert(slot >= 0);

            // Fix the Index of all following rows
            CorrectSlotsAfterInsertion(slot, isCollapsed, isRow);

            // Next, same effect as adding a row
            if (element != null)
            {
                #if DEBUG
                if (element is DataGridRow dataGridRow)
                {
                    Debug.Assert(dataGridRow.Cells.Count == ColumnsItemsInternal.Count);

                    int columnIndex = 0;
                    foreach (DataGridCell dataGridCell in dataGridRow.Cells)
                    {
                        Debug.Assert(dataGridCell.OwningRow == dataGridRow);
                        Debug.Assert(dataGridCell.OwningColumn == ColumnsItemsInternal[columnIndex]);
                        columnIndex++;
                    }
                }
                #endif
                Debug.Assert(!isCollapsed);
                OnAddedElement_Phase1(slot, element);
            }
            else if ((slot <= DisplayData.FirstScrollingSlot) || (isCollapsed && (slot <= DisplayData.LastScrollingSlot)))
            {
                DisplayData.CorrectSlotsAfterInsertion(slot, null /*row*/, isCollapsed);
            }
        }



        private void OnInsertedElement_Phase2(int slot, bool updateVerticalScrollBarOnly, bool isCollapsed)
        {
            Debug.Assert(slot >= 0);

            if (!isCollapsed)
            {
                // Same effect as adding a row
                OnAddedElement_Phase2(slot, updateVerticalScrollBarOnly);
            }
        }



        private void OnInsertingElement(int slotInserted,
        bool firstInsertion,
        bool isCollapsed)
        {
            // Reset the current cell's address if it's after the inserted row.
            if (firstInsertion)
            {
                if (CurrentSlot != -1 && slotInserted <= CurrentSlot)
                {
                    // The underlying data was already added, therefore we need to avoid accessing any back-end data since we might be off by 1 row.
                    _temporarilyResetCurrentCell = true;
                    bool success = SetCurrentCellCore(-1, -1);
                    Debug.Assert(success);
                }
            }

            _showDetailsTable.InsertIndex(slotInserted);
            // Update the slot ranges for the RowGroupHeaders before updating the _selectedItems table,
            // because it's dependent on the slots being correct with regards to grouping.
            RowGroupHeadersTable.InsertIndex(slotInserted);
            if (_selectionModelAdapter == null)
            {
                _selectedItems.InsertIndex(slotInserted);
            }

            if (isCollapsed)
            {
                _collapsedSlotsTable.InsertIndexAndValue(slotInserted, false);
            }
            else
            {
                _collapsedSlotsTable.InsertIndex(slotInserted);
            }

            // If we've inserted rows before the current selected item, update its index
            if (slotInserted <= SelectedIndex)
            {
                SetValueNoCallback(SelectedIndexProperty, SelectedIndex + 1);
            }
        }



        private void OnRemovedElement(int slotDeleted, object itemDeleted)
        {
            SlotCount--;
            bool wasCollapsed = _collapsedSlotsTable.Contains(slotDeleted);
            if (!wasCollapsed)
            {
                VisibleSlotCount--;
            }

            // If we're deleting the focused row, we need to clear the cached value
            if (_focusedRow != null && _focusedRow.Slot == slotDeleted)
            {
                ResetFocusedRow();
            }

            // The element needs to be potentially removed from the displayed elements
            Control elementDeleted = null;
            if (slotDeleted <= DisplayData.LastScrollingSlot)
            {
                if ((slotDeleted >= DisplayData.FirstScrollingSlot) && !wasCollapsed)
                {
                    elementDeleted = DisplayData.GetDisplayedElement(slotDeleted);
                    // We need to retrieve the Element before updating the tables, but we need
                    // to update the tables before updating DisplayData in RemoveDisplayedElement
                    UpdateTablesForRemoval(slotDeleted, itemDeleted);

                    // Displayed row is removed
                    RemoveDisplayedElement(elementDeleted, slotDeleted, true /*wasDeleted*/, true /*updateSlotInformation*/);
                }
                else
                {
                    UpdateTablesForRemoval(slotDeleted, itemDeleted);

                    // Removed row is not in view, just update the DisplayData
                    DisplayData.CorrectSlotsAfterDeletion(slotDeleted, wasCollapsed);
                }
            }
            else
            {
                // The element was removed beyond the viewport so we just need to update the tables
                UpdateTablesForRemoval(slotDeleted, itemDeleted);
            }

            // If a row was removed before the currently selected row, update its index
            if (slotDeleted < SelectedIndex)
            {
                SetValueNoCallback(SelectedIndexProperty, SelectedIndex - 1);
            }

            if (!wasCollapsed)
            {
                if (slotDeleted >= DisplayData.LastScrollingSlot && elementDeleted == null)
                {
                    // Deleted Row is below our Viewport, we just need to adjust the scrollbar
                    UpdateVerticalScrollBar();
                }
                else
                {
                    if (elementDeleted != null)
                    {
                        // Deleted Row is within our Viewport, update the AvailableRowRoom
                        AvailableSlotElementRoom += elementDeleted.DesiredSize.Height;
                    }
                    else
                    {
                        // Deleted Row is above our Viewport, update the vertical offset
                        SetVerticalOffset(Math.Max(0, _verticalOffset - RowHeightEstimate));
                    }

                    ComputeScrollBarsLayout();
                    // Reposition rows in case we use a recycled one
                    InvalidateRowsArrange();
                }
            }
        }



        private void OnRemovingElement(int slotDeleted)
        {
            // Note that the row needs to be deleted no matter what. The underlying data row was already deleted.

            Debug.Assert(slotDeleted >= 0 && slotDeleted < SlotCount);
            _temporarilyResetCurrentCell = false;

            // Reset the current cell's address if it's on the deleted row, or after it.
            if (CurrentSlot != -1 && slotDeleted <= CurrentSlot)
            {
                _desiredCurrentColumnIndex = CurrentColumnIndex;
                if (slotDeleted == CurrentSlot)
                {
                    // No editing is committed since the underlying entity was already deleted.
                    bool success = SetCurrentCellCore(-1, -1, false /*commitEdit*/, false /*endRowEdit*/);
                    Debug.Assert(success);
                }
                else
                {
                    // Underlying data of deleted row is gone. It cannot be accessed anymore. Skip the commit of the editing.
                    _temporarilyResetCurrentCell = true;
                    bool success = SetCurrentCellCore(-1, -1);
                    Debug.Assert(success);
                }
            }
        }



        /// <summary>
        /// Removes all of the editing elements for the row that is just leaving editing mode.
        /// </summary>
        private void RemoveEditingElements()
        {
            if (EditingRow != null && EditingRow.Cells != null)
            {
                Debug.Assert(EditingRow.Cells.Count == ColumnsItemsInternal.Count);
                foreach (DataGridColumn column in Columns)
                {
                    column.RemoveEditingElement();
                }
            }
        }



        private void RemoveElementAt(int slot, object item, bool isRow)
        {
            Debug.Assert(slot >= 0 && slot < SlotCount);

            OnRemovingElement(slot);

            CorrectSlotsAfterDeletion(slot, isRow);

            OnRemovedElement(slot, item);

            // Synchronize CurrentCellCoordinates, CurrentColumn, CurrentColumnIndex, CurrentItem
            // and CurrentSlot with the currently edited cell, since OnRemovingElement called
            // SetCurrentCellCore(-1, -1) to temporarily reset the current cell.
            if (_temporarilyResetCurrentCell &&
            _editingColumnIndex != -1 &&
            _previousCurrentItem != null &&
            EditingRow != null &&
            EditingRow.Slot != -1)
            {
                ProcessSelectionAndCurrency(
                columnIndex: _editingColumnIndex,
                item: _previousCurrentItem,
                backupSlot: this.EditingRow.Slot,
                action: DataGridSelectionAction.None,
                scrollIntoView: false);
            }
        }



        private void UnloadElements(bool recycle)
        {
            // Since we're unloading all the elements, we can't be in editing mode anymore,
            // so commit if we can, otherwise force cancel.
            if (!CommitEdit())
            {
                CancelEdit(DataGridEditingUnit.Row, false);
            }
            ResetEditingRow();

            // Make sure to clear the focused row (because it's no longer relevant).
            if (_focusedRow != null)
            {
                ResetFocusedRow();
                Focus();
            }

            if (_rowsPresenter != null)
            {
                _rowsPresenter.ClearAnchorCandidates();

                foreach (Control element in _rowsPresenter.Children)
                {
                    if (element is DataGridRow row)
                    {
                        // Raise UnloadingRow for any row that was visible
                        if (IsSlotVisible(row.Slot))
                        {
                            OnUnloadingRow(new DataGridRowEventArgs(row));
                        }
                        row.DetachFromDataGrid(recycle && row.IsRecyclable /*recycle*/);
                    }
                    else if (element is DataGridRowGroupHeader groupHeader)
                    {
                        if (IsSlotVisible(groupHeader.RowGroupInfo.Slot))
                        {
                            OnUnloadingRowGroup(new DataGridRowGroupHeaderEventArgs(groupHeader));
                        }
                    }
                }

                if (!recycle)
                {
                    _rowsPresenter.Children.Clear();
                }
            }
            DisplayData.ClearElements(recycle);

            if (recycle && UseLogicalScrollable && _rowsPresenter != null && !KeepRecycledContainersInVisualTree)
            {
                RemoveRecycledChildrenFromVisualTree();
            }

            // Update the AvailableRowRoom since we're displaying 0 rows now
            AvailableSlotElementRoom = CellsEstimatedHeight;
            VisibleSlotCount = 0;
        }



        private void UnloadRow(DataGridRow dataGridRow)
        {
            Debug.Assert(dataGridRow != null);
            Debug.Assert(_rowsPresenter != null);
            Debug.Assert(_rowsPresenter.Children.Contains(dataGridRow));

            if (_loadedRows.Contains(dataGridRow))
            {
                return; // The row is still referenced, we can't release it.
            }

            // Raise UnloadingRow regardless of whether the row will be recycled
            OnUnloadingRow(new DataGridRowEventArgs(dataGridRow));
            bool recycleRow = CurrentSlot != dataGridRow.Index;

            if (recycleRow)
            {
                DisplayData.RecycleRow(dataGridRow);
                if (UseLogicalScrollable && _rowsPresenter != null && !KeepRecycledContainersInVisualTree)
                {
                    _rowsPresenter.Children.Remove(dataGridRow);
                }
            }
            else
            {
                //
                _rowsPresenter.Children.Remove(dataGridRow);
                dataGridRow.DetachFromDataGrid(false);
            }
        }

        private void RemoveRecycledChildrenFromVisualTree()
        {
            if (_rowsPresenter == null)
            {
                return;
            }

            for (int i = _rowsPresenter.Children.Count - 1; i >= 0; i--)
            {
                if (_rowsPresenter.Children[i] is DataGridRow or DataGridRowGroupHeader)
                {
                    _rowsPresenter.Children.RemoveAt(i);
                }
            }
        }


    }
}
