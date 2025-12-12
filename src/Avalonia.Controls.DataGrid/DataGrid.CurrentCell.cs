// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Current cell management
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {

        // Convenient overload that commits the current edit.
        internal bool SetCurrentCellCore(int columnIndex, int slot)
        {
            return SetCurrentCellCore(columnIndex, slot, commitEdit: true, endRowEdit: true);
        }


        // columnIndex = 2, rowIndex = -1 --> current cell belongs to the 'new row'.
        // columnIndex = 2, rowIndex = 2 --> current cell is an inner cell
        // columnIndex = -1, rowIndex = -1 --> current cell is reset
        // columnIndex = -1, rowIndex = 2 --> Unexpected
        private bool SetCurrentCellCore(int columnIndex, int slot, bool commitEdit, bool endRowEdit)
        {
            Debug.Assert(columnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(slot < SlotCount);
            Debug.Assert(columnIndex == -1 || ColumnsItemsInternal[columnIndex].IsVisible);
            Debug.Assert(!(columnIndex > -1 && slot == -1));

            if (columnIndex == CurrentColumnIndex &&
                slot == CurrentSlot)
            {
                Debug.Assert(DataConnection != null);
                Debug.Assert(_editingColumnIndex == -1 || _editingColumnIndex == CurrentColumnIndex);
                Debug.Assert(EditingRow == null || EditingRow.Slot == CurrentSlot || DataConnection.CommittingEdit);
                return true;
            }

            Control oldDisplayedElement = null;
            DataGridCellCoordinates oldCurrentCell = new DataGridCellCoordinates(CurrentCellCoordinates);

            object newCurrentItem = null;
            if (!RowGroupHeadersTable.Contains(slot))
            {
                int rowIndex = RowIndexFromSlot(slot);
                if (rowIndex >= 0 && rowIndex < DataConnection.Count)
                {
                    newCurrentItem = DataConnection.GetDataItem(rowIndex);
                }
            }

            if (CurrentColumnIndex > -1)
            {
                Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
                Debug.Assert(CurrentSlot < SlotCount);

                if (!IsInnerCellOutOfBounds(oldCurrentCell.ColumnIndex, oldCurrentCell.Slot) &&
                    IsSlotVisible(oldCurrentCell.Slot))
                {
                    oldDisplayedElement = DisplayData.GetDisplayedElement(oldCurrentCell.Slot);
                }

                if (!RowGroupHeadersTable.Contains(oldCurrentCell.Slot) && !_temporarilyResetCurrentCell)
                {
                    bool keepFocus = ContainsFocus;
                    if (commitEdit)
                    {
                        if (!EndCellEdit(DataGridEditAction.Commit, exitEditingMode: true, keepFocus: keepFocus, raiseEvents: true))
                        {
                            return false;
                        }
                        // Resetting the current cell: setting it to (-1, -1) is not considered setting it out of bounds
                        if ((columnIndex != -1 && slot != -1 && IsInnerCellOutOfSelectionBounds(columnIndex, slot)) ||
                            IsInnerCellOutOfSelectionBounds(oldCurrentCell.ColumnIndex, oldCurrentCell.Slot))
                        {
                            return false;
                        }

                        if (endRowEdit && !EndRowEdit(DataGridEditAction.Commit, exitEditingMode: true, raiseEvents: true))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        CancelEdit(DataGridEditingUnit.Row, false);
                        ExitEdit(keepFocus);
                    }
                }
            }

            if (newCurrentItem != null)
            {
                slot = SlotFromRowIndex(DataConnection.IndexOf(newCurrentItem));
            }
            if (slot == -1 && columnIndex != -1)
            {
                return false;
            }
            CurrentColumnIndex = columnIndex;
            CurrentSlot = slot;

            if (_temporarilyResetCurrentCell)
            {
                if (columnIndex != -1)
                {
                    _temporarilyResetCurrentCell = false;
                }
            }
            if (!_temporarilyResetCurrentCell && _editingColumnIndex != -1)
            {
                _editingColumnIndex = columnIndex;
            }

            if (oldDisplayedElement != null)
            {
                if (oldDisplayedElement is DataGridRow row)
                {
                    // Don't reset the state of the current cell if we're editing it because that would put it in an invalid state
                    UpdateCurrentState(oldDisplayedElement, oldCurrentCell.ColumnIndex, !(_temporarilyResetCurrentCell && row.IsEditing && _editingColumnIndex == oldCurrentCell.ColumnIndex));
                }
                else
                {
                    UpdateCurrentState(oldDisplayedElement, oldCurrentCell.ColumnIndex, applyCellState: false);
                }
            }

            if (CurrentColumnIndex > -1)
            {
                Debug.Assert(CurrentSlot > -1);
                Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
                Debug.Assert(CurrentSlot < SlotCount);
                if (IsSlotVisible(CurrentSlot))
                {
                    UpdateCurrentState(DisplayData.GetDisplayedElement(CurrentSlot), CurrentColumnIndex, applyCellState: true);
                }
            }

            return true;
        }


        private bool ResetCurrentCellCore()
        {
            return (CurrentColumnIndex == -1 || SetCurrentCellCore(-1, -1));
        }


        private void FlushCurrentCellChanged()
        {
            if (_makeFirstDisplayedCellCurrentCellPending)
            {
                return;
            }
            if (SelectionHasChanged)
            {
                // selection is changing, don't raise CurrentCellChanged until it's done
                _flushCurrentCellChanged = true;
                FlushSelectionChanged();
                return;
            }

            // We don't want to expand all intermediate currency positions, so we only expand
            // the last current item before we flush the event
            if (_collapsedSlotsTable.Contains(CurrentSlot))
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(RowGroupHeadersTable.GetPreviousIndex(CurrentSlot));
                Debug.Assert(rowGroupInfo != null);
                if (rowGroupInfo != null)
                {
                    ExpandRowGroupParentChain(rowGroupInfo.Level, rowGroupInfo.Slot);
                }
            }

            if (CurrentColumn != _previousCurrentColumn
                || CurrentItem != _previousCurrentItem)
            {
                CoerceSelectedItem();
                _previousCurrentColumn = CurrentColumn;
                _previousCurrentItem = CurrentItem;

                OnCurrentCellChanged(EventArgs.Empty);
            }

            _flushCurrentCellChanged = false;
        }


        private void UpdateCurrentState(Control displayedElement, int columnIndex, bool applyCellState)
        {
            if (displayedElement is DataGridRow row)
            {
                row.UpdateCurrentPseudoClass();

                if (AreRowHeadersVisible)
                {
                    row.ApplyHeaderStatus();
                }
                DataGridCell cell = row.Cells[columnIndex];
                if (applyCellState)
                {
                    cell.UpdatePseudoClasses();
                }
            }
            else if (displayedElement is DataGridRowGroupHeader groupHeader)
            {
                groupHeader.UpdatePseudoClasses();
                if (AreRowHeadersVisible)
                {
                    groupHeader.ApplyHeaderStatus();
                }
            }
        }


        /// <summary>
        /// Raises the CurrentCellChanged event.
        /// </summary>
        protected virtual void OnCurrentCellChanged(EventArgs e)
        {
            CurrentCellChanged?.Invoke(this, e);
        }

        private int _noCurrentCellChangeCount;

        private bool _flushCurrentCellChanged;

        private bool _makeFirstDisplayedCellCurrentCellPending;

        private int _desiredCurrentColumnIndex;

        private DataGridColumn _previousCurrentColumn;

        private object _previousCurrentItem;

        private bool _temporarilyResetCurrentCell;


        /// <summary>
        /// Occurs when a different cell becomes the current cell.
        /// </summary>
        public event EventHandler<EventArgs> CurrentCellChanged;


        /// <summary>
        /// Gets or sets the column that contains the current cell.
        /// </summary>
        public DataGridColumn CurrentColumn
        {
            get
            {
                if (CurrentColumnIndex == -1)
                {
                    return null;
                }
                Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
                return ColumnsItemsInternal[CurrentColumnIndex];
            }
            set
            {
                DataGridColumn dataGridColumn = value;
                if (dataGridColumn == null)
                {
                    throw DataGridError.DataGrid.ValueCannotBeSetToNull("value", "CurrentColumn");
                }
                if (CurrentColumn != dataGridColumn)
                {
                    if (dataGridColumn.OwningGrid != this)
                    {
                        // Provided column does not belong to this DataGrid
                        throw DataGridError.DataGrid.ColumnNotInThisDataGrid();
                    }
                    if (!dataGridColumn.IsVisible)
                    {
                        // CurrentColumn cannot be set to an invisible column
                        throw DataGridError.DataGrid.ColumnCannotBeCollapsed();
                    }
                    if (CurrentSlot == -1)
                    {
                        // There is no current row so the current column cannot be set
                        throw DataGridError.DataGrid.NoCurrentRow();
                    }
                    bool beginEdit = _editingColumnIndex != -1;

                    //exitEditingMode, keepFocus, raiseEvents
                    if (!EndCellEdit(DataGridEditAction.Commit, true, ContainsFocus, true))
                    {
                        // Edited value couldn't be committed or aborted
                        return;
                    }

                    UpdateSelectionAndCurrency(dataGridColumn.Index, CurrentSlot, DataGridSelectionAction.None, false); //scrollIntoView
                    Debug.Assert(_successfullyUpdatedSelection);

                    if (beginEdit &&
                        _editingColumnIndex == -1 &&
                        CurrentSlot != -1 &&
                        CurrentColumnIndex != -1 &&
                        CurrentColumnIndex == dataGridColumn.Index &&
                        dataGridColumn.OwningGrid == this &&
                        !GetColumnEffectiveReadOnlyState(dataGridColumn))
                    {
                        // Returning to editing mode since the grid was in that mode prior to the EndCellEdit call above.
                        BeginCellEdit(new RoutedEventArgs());
                    }
                }
            }
        }


        internal int CurrentColumnIndex
        {
            get
            {
                return CurrentCellCoordinates.ColumnIndex;
            }

            private set
            {
                CurrentCellCoordinates.ColumnIndex = value;
            }
        }


        internal int CurrentSlot
        {
            get
            {
                return CurrentCellCoordinates.Slot;
            }

            private set
            {
                CurrentCellCoordinates.Slot = value;
            }
        }


        /// <summary>
        /// Gets the data item bound to the row that contains the current cell.
        /// </summary>
        protected object CurrentItem
        {
            get
            {
                if (CurrentSlot == -1 || ItemsSource == null || RowGroupHeadersTable.Contains(CurrentSlot))
                {
                    return null;
                }
                return DataConnection.GetDataItem(RowIndexFromSlot(CurrentSlot));
            }
        }


        private DataGridCellCoordinates CurrentCellCoordinates
        {
            get;
            set;
        }


        internal int NoCurrentCellChangeCount
        {
            get
            {
                return _noCurrentCellChangeCount;
            }
            set
            {
                _noCurrentCellChangeCount = value;
                if (value == 0)
                {
                    FlushCurrentCellChanged();
                }
            }
        }

    }
}
