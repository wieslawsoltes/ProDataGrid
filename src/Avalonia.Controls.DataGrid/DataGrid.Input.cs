// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Keyboard and pointer input handling
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {

        //TODO TabStop
        //TODO FlowDirection
        private bool ProcessDataGridKey(KeyEventArgs e)
        {
            bool focusDataGrid = false;
            switch (e.Key)
            {
                case Key.Tab:
                    return ProcessTabKey(e);

                case Key.Up:
                    focusDataGrid = ProcessUpKey(e);
                    break;

                case Key.Down:
                    focusDataGrid = ProcessDownKey(e);
                    break;

                case Key.PageDown:
                    focusDataGrid = ProcessNextKey(e);
                    break;

                case Key.PageUp:
                    focusDataGrid = ProcessPriorKey(e);
                    break;

                case Key.Left:
                    focusDataGrid = ProcessLeftKey(e);
                    break;

                case Key.Right:
                    focusDataGrid = ProcessRightKey(e);
                    break;

                case Key.F2:
                    return ProcessF2Key(e);

                case Key.Home:
                    focusDataGrid = ProcessHomeKey(e);
                    break;

                case Key.End:
                    focusDataGrid = ProcessEndKey(e);
                    break;

                case Key.Enter:
                    focusDataGrid = ProcessEnterKey(e);
                    break;

                case Key.Escape:
                    return ProcessEscapeKey();

                case Key.A:
                    return ProcessAKey(e);

                case Key.C:
                    return ProcessCopyKey(e.KeyModifiers);

                case Key.Insert:
                    return ProcessCopyKey(e.KeyModifiers);
            }
            if (focusDataGrid)
            {
                Focus();
            }
            return focusDataGrid;
        }






        private bool ProcessTabKey(KeyEventArgs e, bool shift, bool ctrl)
        {
            if (ctrl || _editingColumnIndex == -1 || IsReadOnly)
            {
                //Go to the next/previous control on the page when
                // - Ctrl key is used
                // - Potential current cell is not edited, or the datagrid is read-only.
                return false;
            }

            // Try to locate a writable cell before/after the current cell
            Debug.Assert(CurrentColumnIndex != -1);
            Debug.Assert(CurrentSlot != -1);

            int neighborVisibleWritableColumnIndex, neighborSlot;
            DataGridColumn dataGridColumn;
            if (shift)
            {
                dataGridColumn = ColumnsInternal.GetPreviousVisibleWritableColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                neighborSlot = GetPreviousVisibleSlot(CurrentSlot);
                if (EditingRow != null)
                {
                    while (neighborSlot != -1 && RowGroupHeadersTable.Contains(neighborSlot))
                    {
                        neighborSlot = GetPreviousVisibleSlot(neighborSlot);
                    }
                }
            }
            else
            {
                dataGridColumn = ColumnsInternal.GetNextVisibleWritableColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                neighborSlot = GetNextVisibleSlot(CurrentSlot);
                if (EditingRow != null)
                {
                    while (neighborSlot < SlotCount && RowGroupHeadersTable.Contains(neighborSlot))
                    {
                        neighborSlot = GetNextVisibleSlot(neighborSlot);
                    }
                }
            }
            neighborVisibleWritableColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;

            if (neighborVisibleWritableColumnIndex == -1 && (neighborSlot == -1 || neighborSlot >= SlotCount))
            {
                // There is no previous/next row and no previous/next writable cell on the current row
                return false;
            }

            if (WaitForLostFocus(() => ProcessTabKey(e, shift, ctrl)))
            {
                return true;
            }

            int targetSlot = -1, targetColumnIndex = -1;

            _noSelectionChangeCount++;
            try
            {
                if (neighborVisibleWritableColumnIndex == -1)
                {
                    targetSlot = neighborSlot;
                    if (shift)
                    {
                        Debug.Assert(ColumnsInternal.LastVisibleWritableColumn != null);
                        targetColumnIndex = ColumnsInternal.LastVisibleWritableColumn.Index;
                    }
                    else
                    {
                        Debug.Assert(ColumnsInternal.FirstVisibleWritableColumn != null);
                        targetColumnIndex = ColumnsInternal.FirstVisibleWritableColumn.Index;
                    }
                }
                else
                {
                    targetSlot = CurrentSlot;
                    targetColumnIndex = neighborVisibleWritableColumnIndex;
                }

                DataGridSelectionAction action;
                if (targetSlot != CurrentSlot || (SelectionMode == DataGridSelectionMode.Extended))
                {
                    if (IsSlotOutOfBounds(targetSlot))
                    {
                        return true;
                    }
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    action = DataGridSelectionAction.None;
                }

                UpdateSelectionAndCurrency(targetColumnIndex, targetSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            if (_successfullyUpdatedSelection && !RowGroupHeadersTable.Contains(targetSlot))
            {
                BeginCellEdit(e);
            }

            // Return true to say we handled the key event even if the operation was unsuccessful. If we don't
            // say we handled this event, the framework will continue to process the tab key and change focus.
            return true;
        }








        private bool ProcessUpKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessUpKey(shift, ctrl)))
            {
                return true;
            }

            int previousVisibleSlot = (CurrentSlot != -1) ? GetPreviousVisibleSlot(CurrentSlot) : -1;

            _noSelectionChangeCount++;

            try
            {
                int slot;
                int columnIndex;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    slot = firstVisibleSlot;
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else if (ctrl)
                {
                    if (shift)
                    {
                        // Both Ctrl and Shift
                        slot = firstVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = (SelectionMode == DataGridSelectionMode.Extended)
                            ? DataGridSelectionAction.SelectFromAnchorToCurrent
                            : DataGridSelectionAction.SelectCurrent;
                    }
                    else
                    {
                        // Ctrl without Shift
                        slot = firstVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }
                else
                {
                    if (previousVisibleSlot == -1)
                    {
                        return true;
                    }
                    if (shift)
                    {
                        // Shift without Ctrl
                        slot = previousVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectFromAnchorToCurrent;
                    }
                    else
                    {
                        // Neither Shift nor Ctrl
                        slot = previousVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }
                UpdateSelectionAndCurrency(columnIndex, slot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessLeftKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessLeftKey(shift, ctrl)))
            {
                return true;
            }

            int previousVisibleColumnIndex = -1;
            if (CurrentColumnIndex != -1)
            {
                dataGridColumn = ColumnsInternal.GetPreviousVisibleNonFillerColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                if (dataGridColumn != null)
                {
                    previousVisibleColumnIndex = dataGridColumn.Index;
                }
            }

            _noSelectionChangeCount++;
            try
            {
                if (ctrl)
                {
                    return ProcessLeftMost(firstVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    if (RowGroupHeadersTable.Contains(CurrentSlot))
                    {
                        CollapseRowGroup(RowGroupHeadersTable.GetValueAt(CurrentSlot).CollectionViewGroup, collapseAllSubgroups: false);
                    }
                    else if (CurrentColumnIndex == -1)
                    {
                        UpdateSelectionAndCurrency(
                            firstVisibleColumnIndex,
                            firstVisibleSlot,
                            DataGridSelectionAction.SelectCurrent,
                            scrollIntoView: true);
                    }
                    else
                    {
                        if (previousVisibleColumnIndex == -1)
                        {
                            return true;
                        }

                        UpdateSelectionAndCurrency(
                            previousVisibleColumnIndex,
                            CurrentSlot,
                            DataGridSelectionAction.None,
                            scrollIntoView: true);
                    }
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessRightKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.LastVisibleColumn;
            int lastVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (lastVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(delegate { ProcessRightKey(shift, ctrl); }))
            {
                return true;
            }

            int nextVisibleColumnIndex = -1;
            if (CurrentColumnIndex != -1)
            {
                dataGridColumn = ColumnsInternal.GetNextVisibleColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                if (dataGridColumn != null)
                {
                    nextVisibleColumnIndex = dataGridColumn.Index;
                }
            }
            _noSelectionChangeCount++;
            try
            {
                if (ctrl)
                {
                    return ProcessRightMost(lastVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    if (RowGroupHeadersTable.Contains(CurrentSlot))
                    {
                        ExpandRowGroup(RowGroupHeadersTable.GetValueAt(CurrentSlot).CollectionViewGroup, expandAllSubgroups: false);
                    }
                    else if (CurrentColumnIndex == -1)
                    {
                        int firstVisibleColumnIndex = ColumnsInternal.FirstVisibleColumn == null ? -1 : ColumnsInternal.FirstVisibleColumn.Index;

                        UpdateSelectionAndCurrency(
                            firstVisibleColumnIndex,
                            firstVisibleSlot,
                            DataGridSelectionAction.SelectCurrent,
                            scrollIntoView: true);
                    }
                    else
                    {
                        if (nextVisibleColumnIndex == -1)
                        {
                            return true;
                        }

                        UpdateSelectionAndCurrency(
                            nextVisibleColumnIndex,
                            CurrentSlot,
                            DataGridSelectionAction.None,
                            scrollIntoView: true);
                    }
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessHomeKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessHomeKey(shift, ctrl)))
            {
                return true;
            }

            _noSelectionChangeCount++;
            try
            {
                if (!ctrl)
                {
                    return ProcessLeftMost(firstVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    DataGridSelectionAction action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;

                    UpdateSelectionAndCurrency(firstVisibleColumnIndex, firstVisibleSlot, action, scrollIntoView: true);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessEndKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.LastVisibleColumn;
            int lastVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            int lastVisibleSlot = LastVisibleSlot;
            if (lastVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessEndKey(shift, ctrl)))
            {
                return true;
            }

            _noSelectionChangeCount++;
            try
            {
                if (!ctrl)
                {
                    return ProcessRightMost(lastVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    DataGridSelectionAction action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;

                    UpdateSelectionAndCurrency(lastVisibleColumnIndex, lastVisibleSlot, action, scrollIntoView: true);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessEnterKey(bool shift, bool ctrl)
        {
            int oldCurrentSlot = CurrentSlot;

            if (!ctrl)
            {
                // If Enter was used by a TextBox, we shouldn't handle the key
                if (FocusManager.GetFocusManager(this)?.GetFocusedElement() is TextBox focusedTextBox
                    && focusedTextBox.AcceptsReturn)
                {
                    return false;
                }

                if (WaitForLostFocus(() => ProcessEnterKey(shift, ctrl)))
                {
                    return true;
                }

                // Enter behaves like down arrow - it commits the potential editing and goes down one cell.
                if (!ProcessDownKeyInternal(false, ctrl))
                {
                    return false;
                }
            }
            else if (WaitForLostFocus(() => ProcessEnterKey(shift, ctrl)))
            {
                return true;
            }

            // Try to commit the potential editing
            if (oldCurrentSlot == CurrentSlot &&
                EndCellEdit(DataGridEditAction.Commit, exitEditingMode: true, keepFocus: true, raiseEvents: true) &&
                EditingRow != null)
            {
                EndRowEdit(DataGridEditAction.Commit, exitEditingMode: true, raiseEvents: true);
                ScrollIntoView(CurrentItem, CurrentColumn);
            }

            return true;
        }






        private bool ProcessNextKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            if (firstVisibleColumnIndex == -1 || DisplayData.FirstScrollingSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessNextKey(shift, ctrl)))
            {
                return true;
            }

            int nextPageSlot = CurrentSlot == -1 ? DisplayData.FirstScrollingSlot : CurrentSlot;
            Debug.Assert(nextPageSlot != -1);
            int slot = GetNextVisibleSlot(nextPageSlot);

            int scrollCount = DisplayData.NumTotallyDisplayedScrollingElements;
            while (scrollCount > 0 && slot < SlotCount)
            {
                nextPageSlot = slot;
                scrollCount--;
                slot = GetNextVisibleSlot(slot);
            }

            _noSelectionChangeCount++;
            try
            {
                DataGridSelectionAction action;
                int columnIndex;
                if (CurrentColumnIndex == -1)
                {
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    columnIndex = CurrentColumnIndex;
                    action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? action = DataGridSelectionAction.SelectFromAnchorToCurrent
                        : action = DataGridSelectionAction.SelectCurrent;
                }

                UpdateSelectionAndCurrency(columnIndex, nextPageSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessPriorKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            if (firstVisibleColumnIndex == -1 || DisplayData.FirstScrollingSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessPriorKey(shift, ctrl)))
            {
                return true;
            }

            int previousPageSlot = (CurrentSlot == -1) ? DisplayData.FirstScrollingSlot : CurrentSlot;
            Debug.Assert(previousPageSlot != -1);

            int scrollCount = DisplayData.NumTotallyDisplayedScrollingElements;
            int slot = GetPreviousVisibleSlot(previousPageSlot);
            while (scrollCount > 0 && slot != -1)
            {
                previousPageSlot = slot;
                scrollCount--;
                slot = GetPreviousVisibleSlot(slot);
            }
            Debug.Assert(previousPageSlot != -1);

            _noSelectionChangeCount++;
            try
            {
                int columnIndex;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    columnIndex = CurrentColumnIndex;
                    action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;
                }

                UpdateSelectionAndCurrency(columnIndex, previousPageSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }








        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = ProcessDataGridKey(e);
            }
        }


        private void DataGrid_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && CurrentColumnIndex != -1 && e.Source == this)
            {
                bool success =
                    ScrollSlotIntoView(
                        CurrentColumnIndex, CurrentSlot,
                        forCurrentCellChange: false,
                        forceHorizontalScroll: true);
                Debug.Assert(success);
                if (CurrentColumnIndex != -1 && SelectedItem == null)
                {
                    SetRowSelection(CurrentSlot, isSelected: true, setAnchorSlot: true);
                }
            }
        }







        //TODO: Ensure left button is checked for
        private bool UpdateStateOnMouseLeftButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit, bool shift, bool ctrl)
        {
            bool beginEdit;

            Debug.Assert(slot >= 0);

            // Before changing selection, check if the current cell needs to be committed, and
            // check if the current row needs to be committed. If any of those two operations are required and fail,
            // do not change selection, and do not change current cell.

            bool wasInEdit = EditingColumnIndex != -1;

            if (IsSlotOutOfBounds(slot))
            {
                return true;
            }

            if (wasInEdit && (columnIndex != EditingColumnIndex || slot != CurrentSlot) &&
                WaitForLostFocus(() => UpdateStateOnMouseLeftButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl)))
            {
                return true;
            }

            try
            {
                _noSelectionChangeCount++;

                beginEdit = allowEdit &&
                            CurrentSlot == slot &&
                            columnIndex != -1 &&
                            (wasInEdit || CurrentColumnIndex == columnIndex) &&
                            !GetColumnEffectiveReadOnlyState(ColumnsItemsInternal[columnIndex]);

                DataGridSelectionAction action;
                if (SelectionMode == DataGridSelectionMode.Extended && shift)
                {
                    // Shift select multiple rows
                    action = DataGridSelectionAction.SelectFromAnchorToCurrent;
                }
                else if (GetRowSelection(slot))  // Unselecting single row or Selecting a previously multi-selected row
                {
                    if (!ctrl && SelectionMode == DataGridSelectionMode.Extended && _selectedItems.Count != 0)
                    {
                        // Unselect everything except the row that was clicked on
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                    else if (ctrl && EditingRow == null)
                    {
                        action = DataGridSelectionAction.RemoveCurrentFromSelection;
                    }
                    else
                    {
                        action = DataGridSelectionAction.None;
                    }
                }
                else // Selecting a single row or multi-selecting with Ctrl
                {
                    if (SelectionMode == DataGridSelectionMode.Single || !ctrl)
                    {
                        // Unselect the currently selected rows except the new selected row
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                    else
                    {
                        action = DataGridSelectionAction.AddCurrentToSelection;
                    }
                }

                UpdateSelectionAndCurrency(columnIndex, slot, action, scrollIntoView: false);
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            if (_successfullyUpdatedSelection && beginEdit && BeginCellEdit(pointerPressedEventArgs))
            {
                FocusEditingCell(setFocus: true);
            }

            return true;
        }




        //TODO: Ensure right button is checked for
        private bool UpdateStateOnMouseRightButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit, bool shift, bool ctrl)
        {
            Debug.Assert(slot >= 0);

            if (shift || ctrl)
            {
                return true;
            }
            if (IsSlotOutOfBounds(slot))
            {
                return true;
            }
            if (GetRowSelection(slot))
            {
                return true;
            }
            // Unselect everything except the row that was clicked on
            _noSelectionChangeCount++;
            try
            {
                UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return true;
        }





        private Control _clickedElement;


        /// <summary>
        /// Occurs when cell is mouse-pressed.
        /// </summary>
        public event EventHandler<DataGridCellPointerPressedEventArgs> CellPointerPressed;

    }
}
