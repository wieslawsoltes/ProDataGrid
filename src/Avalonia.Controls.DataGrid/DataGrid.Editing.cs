// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Cell and row editing functionality
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="BeginningEdit"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridBeginningEditEventArgs> BeginningEditEvent =
            RoutedEvent.Register<DataGrid, DataGridBeginningEditEventArgs>(nameof(BeginningEdit), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="CellEditEnded"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridCellEditEndedEventArgs> CellEditEndedEvent =
            RoutedEvent.Register<DataGrid, DataGridCellEditEndedEventArgs>(nameof(CellEditEnded), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="CellEditEnding"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridCellEditEndingEventArgs> CellEditEndingEvent =
            RoutedEvent.Register<DataGrid, DataGridCellEditEndingEventArgs>(nameof(CellEditEnding), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="PreparingCellForEdit"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridPreparingCellForEditEventArgs> PreparingCellForEditEvent =
            RoutedEvent.Register<DataGrid, DataGridPreparingCellForEditEventArgs>(nameof(PreparingCellForEdit), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="RowEditEnded"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridRowEditEndedEventArgs> RowEditEndedEvent =
            RoutedEvent.Register<DataGrid, DataGridRowEditEndedEventArgs>(nameof(RowEditEnded), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="RowEditEnding"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridRowEditEndingEventArgs> RowEditEndingEvent =
            RoutedEvent.Register<DataGrid, DataGridRowEditEndingEventArgs>(nameof(RowEditEnding), RoutingStrategies.Bubble);

        /// <summary>
        /// Checks if the specified slot is the placeholder row for adding new items.
        /// </summary>
        /// <param name="slot">The slot to check.</param>
        /// <returns>True if the slot is the placeholder row; otherwise, false.</returns>
        private bool IsSlotPlaceholderRow(int slot)
        {
            if (slot < 0 || IsGroupSlot(slot))
            {
                return false;
            }
            var rowIndex = RowIndexFromSlot(slot);
            var dataItem = DataConnection.GetDataItem(rowIndex);
            return ReferenceEquals(dataItem, DataGridCollectionView.NewItemPlaceholder);
        }

        /// <summary>
        /// Checks if the specified slot can be edited. Returns true if the row is selected
        /// or if it's the placeholder row (for adding new items).
        /// </summary>
        /// <param name="slot">The slot to check.</param>
        /// <returns>True if the slot can be edited; otherwise, false.</returns>
        private bool CanEditSlot(int slot)
        {
            return GetRowSelection(slot) || IsSlotPlaceholderRow(slot);
        }

        /// <summary>
        /// Enters editing mode for the current cell and current row (if they're not already in editing mode).
        /// </summary>
        /// <returns>True if operation was successful. False otherwise.</returns>
        public bool BeginEdit()
        {
            return BeginEdit(null);
        }


        /// <summary>
        /// Enters editing mode for the current cell and current row (if they're not already in editing mode).
        /// </summary>
        /// <param name="editingEventArgs">Provides information about the user gesture that caused the call to BeginEdit. Can be null.</param>
        /// <returns>True if operation was successful. False otherwise.</returns>
        public bool BeginEdit(RoutedEventArgs editingEventArgs)
        {
            if (CurrentColumnIndex == -1 || !CanEditSlot(CurrentSlot))
            {
                return false;
            }

            Debug.Assert(CurrentColumnIndex >= 0);
            Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(CurrentSlot >= -1);
            Debug.Assert(CurrentSlot < SlotCount);
            Debug.Assert(EditingRow == null || EditingRow.Slot == CurrentSlot);

            if (GetColumnEffectiveReadOnlyState(CurrentColumn))
            {
                // Current column is read-only
                return false;
            }
            return BeginCellEdit(editingEventArgs);
        }


        /// <summary>
        /// Cancels editing mode and restores the original value.
        /// </summary>
        /// <returns>True if operation was successful. False otherwise.</returns>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        bool CancelEdit()
        {
            return CancelEdit(DataGridEditingUnit.Row);
        }


        /// <summary>
        /// Cancels editing mode for the specified DataGridEditingUnit and restores its original value.
        /// </summary>
        /// <param name="editingUnit">Specifies whether to cancel edit for a Cell or Row.</param>
        /// <returns>True if operation was successful. False otherwise.</returns>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        bool CancelEdit(DataGridEditingUnit editingUnit)
        {
            return CancelEdit(editingUnit, raiseEvents: true);
        }


        /// <summary>
        /// Cancels editing mode for the specified DataGridEditingUnit and restores its original value.
        /// </summary>
        /// <param name="editingUnit">Specifies whether to cancel edit for a Cell or Row.</param>
        /// <param name="raiseEvents">Specifies whether or not to raise editing events</param>
        /// <returns>True if operation was successful. False otherwise.</returns>
        internal bool CancelEdit(DataGridEditingUnit editingUnit, bool raiseEvents)
        {
            if (!EndCellEdit(
                    DataGridEditAction.Cancel,
                    exitEditingMode: true,
                    keepFocus: ContainsFocus,
                    raiseEvents: raiseEvents))
            {
                return false;
            }

            if (editingUnit == DataGridEditingUnit.Row)
            {
                return EndRowEdit(DataGridEditAction.Cancel, true, raiseEvents);
            }

            return true;
        }


        /// <summary>
        /// Commits editing mode and pushes changes to the backend.
        /// </summary>
        /// <returns>True if operation was successful. False otherwise.</returns>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        bool CommitEdit()
        {
            return CommitEdit(DataGridEditingUnit.Row, true);
        }


        /// <summary>
        /// Commits editing mode for the specified DataGridEditingUnit and pushes changes to the backend.
        /// </summary>
        /// <param name="editingUnit">Specifies whether to commit edit for a Cell or Row.</param>
        /// <param name="exitEditingMode">Editing mode is left if True.</param>
        /// <returns>True if operation was successful. False otherwise.</returns>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        bool CommitEdit(DataGridEditingUnit editingUnit, bool exitEditingMode)
        {
            if (!EndCellEdit(
                    editAction: DataGridEditAction.Commit,
                    exitEditingMode: editingUnit == DataGridEditingUnit.Cell ? exitEditingMode : true,
                    keepFocus: ContainsFocus,
                    raiseEvents: true))
            {
                return false;
            }
            if (editingUnit == DataGridEditingUnit.Row)
            {
                return EndRowEdit(DataGridEditAction.Commit, exitEditingMode, raiseEvents: true);
            }
            return true;
        }


        private bool BeginCellEdit(RoutedEventArgs editingEventArgs)
        {
            if (CurrentColumnIndex == -1 || !CanEditSlot(CurrentSlot))
            {
                return false;
            }

            Debug.Assert(CurrentColumnIndex >= 0);
            Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(CurrentSlot >= -1);
            Debug.Assert(CurrentSlot < SlotCount);
            Debug.Assert(EditingRow == null || EditingRow.Slot == CurrentSlot);
            Debug.Assert(!GetColumnEffectiveReadOnlyState(CurrentColumn));
            Debug.Assert(CurrentColumn.IsVisible);

            if (_editingColumnIndex != -1)
            {
                // Current cell is already in edit mode
                return true;
            }

            // Get or generate the editing row if it doesn't exist
            DataGridRow dataGridRow = EditingRow;
            if (dataGridRow == null)
            {
                if (IsSlotVisible(CurrentSlot))
                {
                    dataGridRow = DisplayData.GetDisplayedElement(CurrentSlot) as DataGridRow;
                    Debug.Assert(dataGridRow != null);
                }
                else
                {
                    dataGridRow = GenerateRow(RowIndexFromSlot(CurrentSlot), CurrentSlot);
                }
            }
            Debug.Assert(dataGridRow != null);

            // Cache these to see if they change later
            int currentRowIndex = CurrentSlot;
            int currentColumnIndex = CurrentColumnIndex;

            // Raise the BeginningEdit event
            DataGridCell dataGridCell = TryGetCell(dataGridRow, CurrentColumnIndex);
            if (dataGridCell == null)
            {
                return false;
            }
            DataGridBeginningEditEventArgs e = new DataGridBeginningEditEventArgs(CurrentColumn, dataGridRow, editingEventArgs);
            OnBeginningEdit(e);
            if (e.Cancel
                || currentRowIndex != CurrentSlot
                || currentColumnIndex != CurrentColumnIndex
                || !CanEditSlot(CurrentSlot)
                || (EditingRow == null && !BeginRowEdit(dataGridRow)))
            {
                // If either BeginningEdit was canceled, currency/selection was changed in the event handler,
                // or we failed opening the row for edit, then we can no longer continue BeginCellEdit
                return false;
            }
            if (EditingRow == null || EditingRow.Slot != CurrentSlot)
            {
                return false;
            }

            // Finally, we can prepare the cell for editing
            _editingCellValidationSnapshot = CellValidationSnapshot.Capture(dataGridCell);
            // Hide existing cell errors while editing to avoid duplicate validation visuals.
            DataValidationErrors.ClearErrors(dataGridCell);
            _editingColumnIndex = CurrentColumnIndex;
            _editingEventArgs = editingEventArgs;
            dataGridCell.UpdatePseudoClasses();
            PopulateCellContent(
                isCellEdited: true,
                dataGridColumn: CurrentColumn,
                dataGridRow: dataGridRow,
                dataGridCell: dataGridCell);
            return true;
        }


        //TODO Validation
        private bool BeginRowEdit(DataGridRow dataGridRow)
        {
            Debug.Assert(EditingRow == null);
            Debug.Assert(dataGridRow != null);

            Debug.Assert(CurrentSlot >= -1);
            Debug.Assert(CurrentSlot < SlotCount);

            if (DataConnection.BeginEdit(dataGridRow.DataContext))
            {
                EditingRow = dataGridRow;
                CaptureRowValidationSnapshot(dataGridRow);
                GenerateEditingElements();
                return true;
            }
            return false;
        }


        //TODO Validation
        //TODO Binding
        //TODO TabStop
        private bool EndCellEdit(DataGridEditAction editAction, bool exitEditingMode, bool keepFocus, bool raiseEvents)
        {
            if (_editingColumnIndex == -1)
            {
                return true;
            }

            var editingRow = EditingRow;
            if (editingRow is null)
            {
                return true;
            }

            Debug.Assert(_editingColumnIndex >= 0);
            Debug.Assert(_editingColumnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(_editingColumnIndex == CurrentColumnIndex);

            // Cache these to see if they change later
            int currentSlot = CurrentSlot;
            int currentColumnIndex = CurrentColumnIndex;

            // We're ready to start ending, so raise the event
            DataGridCell editingCell = TryGetCell(editingRow, _editingColumnIndex);
            if (editingCell == null)
            {
                _editingColumnIndex = -1;
                ResetEditingRow();
                return true;
            }
            var editingElement = editingCell.Content as Control;
            if (editingElement == null)
            {
                return false;
            }
            if (raiseEvents)
            {
                DataGridCellEditEndingEventArgs e = new DataGridCellEditEndingEventArgs(CurrentColumn, editingRow, editingElement, editAction);
                OnCellEditEnding(e);
                if (e.Cancel)
                {
                    // CellEditEnding has been cancelled
                    return false;
                }

                // Ensure that the current cell wasn't changed in the user's CellEditEnding handler
                if (_editingColumnIndex == -1 ||
                    currentSlot != CurrentSlot ||
                    currentColumnIndex != CurrentColumnIndex)
                {
                    return true;
                }
                Debug.Assert(EditingRow != null);
                Debug.Assert(EditingRow.Slot == currentSlot);
                Debug.Assert(_editingColumnIndex != -1);
                Debug.Assert(_editingColumnIndex == CurrentColumnIndex);
            }

            // If we're canceling, let the editing column repopulate its old value if it wants
            if (editAction == DataGridEditAction.Cancel)
            {
                CurrentColumn.CancelCellEditInternal(editingElement, _uneditedValue);

                // Ensure that the current cell wasn't changed in the user column's CancelCellEdit
                if (_editingColumnIndex == -1 ||
                    currentSlot != CurrentSlot ||
                    currentColumnIndex != CurrentColumnIndex)
                {
                    return true;
                }
                Debug.Assert(EditingRow != null);
                Debug.Assert(EditingRow.Slot == currentSlot);
                Debug.Assert(_editingColumnIndex != -1);
                Debug.Assert(_editingColumnIndex == CurrentColumnIndex);
            }

            // If we're committing, explicitly update the source but watch out for any validation errors
            if (editAction == DataGridEditAction.Commit)
            {
                void SetValidationStatus(ICellEditBinding binding)
                {
                    var severity = ValidationUtil.GetValidationSeverity(binding.ValidationErrors);
                    if (severity == DataGridValidationSeverity.None)
                    {
                        ResetValidationStatus(editingCell);
                        if (editingElement != null)
                        {
                            DataValidationErrors.ClearErrors(editingElement);
                        }
                    }
                    else
                    {
                        if (editingRow != null)
                        {
                            editingCell.IsValid = severity != DataGridValidationSeverity.Error;
                            editingCell.ValidationSeverity = severity;
                            editingCell.UpdatePseudoClasses();
                            UpdateRowValidationStateFromCells(editingRow);
                        }
                        UpdateGridValidationState();

                        if (editingElement != null)
                        {
                            DataValidationErrors.ClearErrors(editingElement);
                        }

                        if (editingCell != null)
                        {
                            DataValidationErrors.SetError(editingCell,
                                new AggregateException(binding.ValidationErrors));
                        }
                    }
                }

                var editBinding = CurrentColumn?.CellEditBinding;
                if (editBinding != null)
                {
                    editBinding.CommitEdit();
                    var severity = ValidationUtil.GetValidationSeverity(editBinding.ValidationErrors);
                    if (severity != DataGridValidationSeverity.None)
                    {
                        SetValidationStatus(editBinding);

                        if (severity == DataGridValidationSeverity.Error)
                        {
                            _validationSubscription?.Dispose();
                            _validationSubscription = editBinding.ValidationChanged.Subscribe(v => SetValidationStatus(editBinding));

                            ScrollSlotIntoView(CurrentColumnIndex, CurrentSlot, forCurrentCellChange: false, forceHorizontalScroll: true);
                            return false;
                        }

                        _validationSubscription?.Dispose();
                        _validationSubscription = null;
                    }
                    else
                    {
                        ResetValidationStatus(editingCell);
                    }
                }
                else
                {
                    ResetValidationStatus(editingCell);
                }
            }

            if (editAction != DataGridEditAction.Commit)
            {
                ResetValidationStatus(editingCell);
                if (editingElement != null)
                {
                    DataValidationErrors.ClearErrors(editingElement);
                }
                RestoreRowValidationState(editingRow, editingRow.DataContext, clearIfNoIndei: false);
                if (editingRow?.DataContext is not INotifyDataErrorInfo)
                {
                    RestoreEditingCellValidationSnapshot(editingCell);
                    UpdateRowValidationStateFromCells(editingRow);
                    UpdateGridValidationState();
                }
                else if (IsColumnBindingPathEmpty(editingCell?.OwningColumn))
                {
                    RestoreEditingCellValidationSnapshot(editingCell);
                    UpdateRowValidationStateFromCells(editingRow);
                    UpdateGridValidationState();
                }
            }
            _editingCellValidationSnapshot = null;

            if (exitEditingMode)
            {
                CurrentColumn.EndCellEditInternal();
                _editingColumnIndex = -1;
                editingCell.UpdatePseudoClasses();

                //IsTabStop = true;
                if (keepFocus && editingElement.ContainsFocusedElement())
                {
                    Focus();
                }

                PopulateCellContent(
                    isCellEdited: !exitEditingMode,
                    dataGridColumn: CurrentColumn,
                    dataGridRow: editingRow,
                    dataGridCell: editingCell);

                if (editAction == DataGridEditAction.Commit && HasAnySummaries())
                {
                    InvalidateSummaries();
                }

                editingRow.InvalidateDesiredHeight();
                var column = editingCell.OwningColumn;
                if (column.Width.IsSizeToCells || column.Width.IsAuto)
                {// Invalidate desired width and force recalculation
                    column.SetWidthDesiredValue(0);
                    editingRow.OwningGrid.AutoSizeColumn(column, editingCell.DesiredSize.Width);
                }
            }

            // We're done, so raise the CellEditEnded event
            if (raiseEvents)
            {
                OnCellEditEnded(new DataGridCellEditEndedEventArgs(CurrentColumn, editingRow, editAction));
            }

            // There's a chance that somebody reopened this cell for edit within the CellEditEnded handler,
            // so we should return false if we were supposed to exit editing mode, but we didn't
            return !(exitEditingMode && currentColumnIndex == _editingColumnIndex);
        }


        //TODO Validation
        private bool EndRowEdit(DataGridEditAction editAction, bool exitEditingMode, bool raiseEvents)
        {
            if (EditingRow == null || DataConnection.CommittingEdit)
            {
                return true;
            }
            if (_editingColumnIndex != -1 || (editAction == DataGridEditAction.Cancel && raiseEvents &&
                !((DataConnection.EditableCollectionView != null && (DataConnection.EditableCollectionView.CanCancelEdit || DataConnection.EditableCollectionView.IsAddingNew)) || (EditingRow.DataContext is IEditableObject))))
            {
                // Ending the row edit will fail immediately under the following conditions:
                // 1. We haven't ended the cell edit yet.
                // 2. We're trying to cancel edit when the underlying DataType is not an IEditableObject,
                //    because we have no way to properly restore the old value.  We will only allow this to occur
                //    if raiseEvents == false, which means we're internally forcing a cancel.
                return false;
            }
            DataGridRow editingRow = EditingRow;

            if (raiseEvents)
            {
                DataGridRowEditEndingEventArgs e = new DataGridRowEditEndingEventArgs(EditingRow, editAction);
                OnRowEditEnding(e);
                if (e.Cancel)
                {
                    // RowEditEnding has been cancelled
                    return false;
                }

                // Editing states might have been changed in the RowEditEnding handlers
                if (_editingColumnIndex != -1)
                {
                    return false;
                }
                if (editingRow != EditingRow)
                {
                    return true;
                }
            }

            // Call the appropriate commit or cancel methods
            if (editAction == DataGridEditAction.Commit)
            {
                if (!CommitRowEdit(exitEditingMode))
                {
                    return false;
                }
            }
            else
            {
                if (!CancelRowEdit(exitEditingMode) && raiseEvents)
                {
                    // We failed to cancel edit so we should abort unless we're forcing a cancel
                    return false;
                }
            }
            if (editAction != DataGridEditAction.Commit)
            {
                ResetValidationStatus();
                RestoreRowValidationState(editingRow, editingRow.DataContext, clearIfNoIndei: false);
                if (editingRow?.DataContext is not INotifyDataErrorInfo)
                {
                    RestoreRowValidationSnapshot(editingRow);
                    UpdateRowValidationStateFromCells(editingRow);
                    UpdateGridValidationState();
                }
                else
                {
                    RestoreTemplateColumnValidationSnapshots(editingRow);
                    UpdateRowValidationStateFromCells(editingRow);
                    UpdateGridValidationState();
                }
            }
            if (!exitEditingMode && editingRow == EditingRow)
            {
                CaptureRowValidationSnapshot(editingRow);
            }

            // Update the previously edited row's state
            if (exitEditingMode && editingRow == EditingRow)
            {
                RemoveEditingElements();
                ResetEditingRow();
            }

            // Raise the RowEditEnded event
            if (raiseEvents)
            {
                OnRowEditEnded(new DataGridRowEditEndedEventArgs(editingRow, editAction));
            }

            return true;
        }


        private bool CancelRowEdit(bool exitEditingMode)
        {
            if (EditingRow == null)
            {
                return true;
            }
            Debug.Assert(EditingRow != null && EditingRow.Index >= -1);
            Debug.Assert(EditingRow.Slot < SlotCount);
            Debug.Assert(CurrentColumn != null);

            object dataItem = EditingRow.DataContext;
            if (!DataConnection.CancelEdit(dataItem))
            {
                return false;
            }
            foreach (DataGridColumn column in Columns)
            {
                if (!exitEditingMode && column.Index == _editingColumnIndex && column is DataGridBoundColumn)
                {
                    continue;
                }
                PopulateCellContent(
                    isCellEdited: !exitEditingMode && column.Index == _editingColumnIndex,
                    dataGridColumn: column,
                    dataGridRow: EditingRow,
                    dataGridCell: EditingRow.Cells[column.Index]);
            }
            return true;
        }


        //TODO Validation
        private bool CommitRowEdit(bool exitEditingMode)
        {
            if (EditingRow == null)
            {
                return true;
            }
            Debug.Assert(EditingRow != null && EditingRow.Index >= -1);
            Debug.Assert(EditingRow.Slot < SlotCount);

            //if (!ValidateEditingRow(scrollIntoView: true, wireEvents: false))
            if (!EditingRow.IsValid)
            {
                return false;
            }

            DataConnection.EndEdit(EditingRow.DataContext);

            if (!exitEditingMode)
            {
                DataConnection.BeginEdit(EditingRow.DataContext);
            }
            return true;
        }


        private bool CommitEditForOperation(int columnIndex, int slot, bool forCurrentCellChange)
        {
            if (forCurrentCellChange)
            {
                if (!EndCellEdit(DataGridEditAction.Commit, exitEditingMode: true, keepFocus: true, raiseEvents: true))
                {
                    return false;
                }
                if (CurrentSlot != slot &&
                    !EndRowEdit(DataGridEditAction.Commit, exitEditingMode: true, raiseEvents: true))
                {
                    return false;
                }
            }

            if (IsColumnOutOfBounds(columnIndex))
            {
                return false;
            }
            if (slot >= SlotCount)
            {
                // Current cell was reset because the commit deleted row(s).
                // Since the user wants to change the current cell, we don't
                // want to end up with no current cell. We pick the last row
                // in the grid which may be the 'new row'.
                int lastSlot = LastVisibleSlot;
                if (forCurrentCellChange &&
                    CurrentColumnIndex == -1 &&
                    lastSlot != -1)
                {
                    SetAndSelectCurrentCell(columnIndex, lastSlot, forceCurrentCellSelection: false);
                }
                // Interrupt operation because it has become invalid.
                return false;
            }
            return true;
        }


        /// <summary>
        /// Exits editing mode without trying to commit or revert the editing, and
        /// without repopulating the edited row's cell.
        /// </summary>
        //TODO TabStop
        private void ExitEdit(bool keepFocus)
        {
            if (EditingRow == null || DataConnection.CommittingEdit)
            {
                Debug.Assert(_editingColumnIndex == -1);
                return;
            }

            if (_editingColumnIndex != -1)
            {
                Debug.Assert(_editingColumnIndex >= 0);
                Debug.Assert(_editingColumnIndex < ColumnsItemsInternal.Count);
                Debug.Assert(_editingColumnIndex == CurrentColumnIndex);
                Debug.Assert(EditingRow != null && EditingRow.Slot == CurrentSlot);

                _editingColumnIndex = -1;
                TryGetCell(EditingRow, CurrentColumnIndex)?.UpdatePseudoClasses();
            }
            //IsTabStop = true;
            if (IsSlotVisible(EditingRow.Slot))
            {
                EditingRow.ApplyState();
            }
            ResetEditingRow();
            if (keepFocus)
            {
                Focus();
            }
        }

        private static DataGridCell TryGetCell(DataGridRow dataGridRow, int columnIndex)
        {
            if (dataGridRow == null)
            {
                return null;
            }

            if (columnIndex < 0 || columnIndex >= dataGridRow.Cells.Count)
            {
                return null;
            }

            return dataGridRow.Cells[columnIndex];
        }

        private void CaptureRowValidationSnapshot(DataGridRow row)
        {
            if (row == null)
            {
                _editingRowValidationSnapshot = null;
                return;
            }

            var snapshots = new List<CellValidationSnapshot>(row.Cells.Count);
            foreach (DataGridCell cell in row.Cells)
            {
                if (cell != null)
                {
                    snapshots.Add(CellValidationSnapshot.Capture(cell));
                }
            }

            _editingRowValidationSnapshot = snapshots;
        }

        private void RestoreRowValidationSnapshot(DataGridRow row)
        {
            if (_editingRowValidationSnapshot == null || row == null)
            {
                return;
            }

            foreach (var snapshot in _editingRowValidationSnapshot)
            {
                if (snapshot.Cell?.OwningRow == row)
                {
                    snapshot.Restore();
                }
            }
        }

        private void RestoreCellValidationSnapshot(DataGridCell cell)
        {
            if (_editingRowValidationSnapshot == null || cell == null)
            {
                return;
            }

            foreach (var snapshot in _editingRowValidationSnapshot)
            {
                if (ReferenceEquals(snapshot.Cell, cell))
                {
                    snapshot.Restore();
                    break;
                }
            }
        }

        private void RestoreEditingCellValidationSnapshot(DataGridCell cell)
        {
            if (_editingCellValidationSnapshot == null || cell == null)
            {
                return;
            }

            if (ReferenceEquals(_editingCellValidationSnapshot.Cell, cell))
            {
                _editingCellValidationSnapshot.Restore();
            }
        }

        private void RestoreTemplateColumnValidationSnapshots(DataGridRow row)
        {
            if (_editingRowValidationSnapshot == null || row == null)
            {
                return;
            }

            foreach (var snapshot in _editingRowValidationSnapshot)
            {
                var cell = snapshot.Cell;
                if (cell?.OwningRow != row)
                {
                    continue;
                }

                if (IsColumnBindingPathEmpty(cell.OwningColumn))
                {
                    snapshot.Restore();
                }
            }
        }

        private static bool IsColumnBindingPathEmpty(DataGridColumn column)
        {
            return column == null || string.IsNullOrWhiteSpace(GetColumnBindingPath(column));
        }

        private sealed class CellValidationSnapshot
        {
            public CellValidationSnapshot(DataGridCell cell, bool isValid, DataGridValidationSeverity severity, object[] errors)
            {
                Cell = cell;
                IsValid = isValid;
                Severity = severity;
                Errors = errors;
            }

            public DataGridCell Cell { get; }

            public bool IsValid { get; }

            public DataGridValidationSeverity Severity { get; }

            public object[] Errors { get; }

            public static CellValidationSnapshot Capture(DataGridCell cell)
            {
                return new CellValidationSnapshot(
                    cell,
                    cell.IsValid,
                    cell.ValidationSeverity,
                    CreateErrorsSnapshot(cell));
            }

            public void Restore()
            {
                Cell.IsValid = IsValid;
                Cell.ValidationSeverity = Severity;
                Cell.UpdatePseudoClasses();

                if (Errors == null || Errors.Length == 0)
                {
                    DataValidationErrors.ClearErrors(Cell);
                }
                else
                {
                    DataValidationErrors.SetErrors(Cell, Errors);
                }
            }

            private static object[] CreateErrorsSnapshot(DataGridCell cell)
            {
                var errors = DataValidationErrors.GetErrors(cell);
                if (errors == null)
                {
                    return null;
                }

                var list = new List<object>();
                foreach (var error in errors)
                {
                    if (error != null)
                    {
                        list.Add(error);
                    }
                }

                return list.Count == 0 ? Array.Empty<object>() : list.ToArray();
            }
        }


        private void ResetEditingRow()
        {
            if (EditingRow != null
                && EditingRow != _focusedRow
                && !IsSlotVisible(EditingRow.Slot))
            {
                // Unload the old editing row if it's off screen
                EditingRow.Clip = null;
                UnloadRow(EditingRow);
            }
            EditingRow = null;
            _editingRowValidationSnapshot = null;
            _editingCellValidationSnapshot = null;
        }


        private void PreparingCellForEditPrivate(Control editingElement)
        {
            if (_editingColumnIndex == -1 ||
                CurrentColumnIndex == -1 ||
                TryGetCell(EditingRow, CurrentColumnIndex)?.Content != editingElement)
            {
                // The current cell has changed since the call to BeginCellEdit, so the fact
                // that this element has loaded is no longer relevant
                return;
            }

            Debug.Assert(EditingRow != null);
            Debug.Assert(_editingColumnIndex >= 0);
            Debug.Assert(_editingColumnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(_editingColumnIndex == CurrentColumnIndex);
            Debug.Assert(EditingRow != null && EditingRow.Slot == CurrentSlot);

            FocusEditingCell(setFocus: ContainsFocus || _focusEditingControl);

            // Prepare the cell for editing and raise the PreparingCellForEdit event for all columns
            DataGridColumn dataGridColumn = CurrentColumn;
            _uneditedValue = dataGridColumn.PrepareCellForEditInternal(editingElement, _editingEventArgs);
            OnPreparingCellForEdit(new DataGridPreparingCellForEditEventArgs(dataGridColumn, EditingRow, _editingEventArgs, editingElement));
            ApplyPendingTextInput(editingElement);
        }

        private void ApplyPendingTextInput(Control editingElement)
        {
            if (string.IsNullOrEmpty(_pendingTextInput))
            {
                return;
            }

            var text = _pendingTextInput;
            _pendingTextInput = null;

            if (TryApplyTextInput(editingElement, text))
            {
                return;
            }

            if (editingElement is TemplatedControl templated)
            {
                void Handler(object sender, TemplateAppliedEventArgs e)
                {
                    templated.TemplateApplied -= Handler;
                    TryApplyTextInput(templated, text);
                }

                templated.TemplateApplied += Handler;
            }
        }

        private bool TryApplyTextInput(Control editingElement, string text)
        {
            var model = EditingInteractionModel;
            if (model != null)
            {
                return model.TryApplyTextInput(new DataGridTextInputApplyContext(editingElement, text));
            }

            if (editingElement is TextBox textBox)
            {
                textBox.Text = text;
                textBox.CaretIndex = text.Length;
                textBox.SelectionStart = text.Length;
                textBox.SelectionEnd = text.Length;
                return true;
            }

            var nestedTextBox = editingElement.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (nestedTextBox != null)
            {
                nestedTextBox.Text = text;
                nestedTextBox.CaretIndex = text.Length;
                nestedTextBox.SelectionStart = text.Length;
                nestedTextBox.SelectionEnd = text.Length;
                return true;
            }

            return false;
        }


        private void PopulateCellContent(bool isCellEdited,
                                         DataGridColumn dataGridColumn,
                                         DataGridRow dataGridRow,
                                         DataGridCell dataGridCell)
        {
            Debug.Assert(dataGridColumn != null);
            Debug.Assert(dataGridRow != null);
            Debug.Assert(dataGridCell != null);

            Control element = null;
            DataGridBoundColumn dataGridBoundColumn = dataGridColumn as DataGridBoundColumn;
            if (isCellEdited)
            {
                // Generate EditingElement and apply column style if available
                element = dataGridColumn.GenerateEditingElementInternal(dataGridCell, dataGridRow.DataContext);
                if (element != null)
                {

                    dataGridCell.Content = element;
                    if (element.IsInitialized)
                    {
                        PreparingCellForEditPrivate(element as Control);
                    }
                    else
                    {
                        // Subscribe to the new element's events
                        element.Initialized += EditingElement_Initialized;
                    }
                }
            }
            else
            {
                // Generate Element and apply column style if available
                element = dataGridColumn.GenerateElementInternal(dataGridCell, dataGridRow.DataContext);
                dataGridCell.Content = element;
            }
            dataGridColumn.ApplyCellBindings(dataGridCell);

        }


        //TODO TabStop
        private bool FocusEditingCell(bool setFocus)
        {
            if (_editingColumnIndex == -1 || EditingRow == null)
            {
                return false;
            }

            Debug.Assert(CurrentColumnIndex >= 0);
            Debug.Assert(CurrentColumnIndex < ColumnsItemsInternal.Count);
            Debug.Assert(CurrentSlot >= -1);
            Debug.Assert(CurrentSlot < SlotCount);
            Debug.Assert(EditingRow.Slot == CurrentSlot);
            Debug.Assert(_editingColumnIndex != -1);

            //IsTabStop = false;
            _focusEditingControl = false;

            bool success = false;
            DataGridCell dataGridCell = TryGetCell(EditingRow, _editingColumnIndex);
            if (dataGridCell == null)
            {
                return false;
            }
            if (setFocus)
            {
                if (dataGridCell.ContainsFocusedElement())
                {
                    success = true;
                }
                else
                {
                    dataGridCell.Focus();
                    success = dataGridCell.ContainsFocusedElement();
                }

                _focusEditingControl = !success;
            }
            return success;
        }


        /// <summary>
        /// If the editing element has focus, this method will set focus to the DataGrid itself
        /// in order to force the element to lose focus.  It will then wait for the editing element's
        /// LostFocus event, at which point it will perform the specified action.
        ///
        /// NOTE: It is important to understand that the specified action will be performed when the editing
        /// element loses focus only if this method returns true.  If it returns false, then the action
        /// will not be performed later on, and should instead be performed by the caller, if necessary.
        /// </summary>
        /// <param name="action">Action to perform after the editing element loses focus</param>
        /// <returns>True if the editing element had focus and the action was cached away; false otherwise</returns>
        //TODO TabStop
        internal bool WaitForLostFocus(Action action)
        {
            if (EditingRow != null && EditingColumnIndex != -1 && !_executingLostFocusActions)
            {
                DataGridColumn editingColumn = ColumnsItemsInternal[EditingColumnIndex];
                if (editingColumn?.CellEditBinding is ExplicitCellEditBinding)
                {
                    return false;
                }
                Control editingElement = editingColumn.GetCellContent(EditingRow);
                if (editingElement != null && editingElement.ContainsChild(_focusedObject))
                {
                    Debug.Assert(_lostFocusActions != null);
                    _lostFocusActions.Enqueue(action);
                    editingElement.LostFocus += EditingElement_LostFocus;
                    //IsTabStop = true;
                    Focus();
                    return true;
                }
            }
            return false;
        }


        private void EditingElement_Initialized(object sender, EventArgs e)
        {
            var element = sender as Control;
            if (element != null)
            {
                element.Initialized -= EditingElement_Initialized;
            }
            PreparingCellForEditPrivate(element);
        }


        /// <summary>
        /// Handles the current editing element's LostFocus event by performing any actions that
        /// were cached by the WaitForLostFocus method.
        /// </summary>
        /// <param name="sender">Editing element</param>
        /// <param name="e">RoutedEventArgs</param>
        private void EditingElement_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control editingElement)
            {
                editingElement.LostFocus -= EditingElement_LostFocus;
                if (EditingRow != null && _editingColumnIndex != -1)
                {
                    FocusEditingCell(true);
                }
                Debug.Assert(_lostFocusActions != null);
                try
                {
                    _executingLostFocusActions = true;
                    while (_lostFocusActions.Count > 0)
                    {
                        _lostFocusActions.Dequeue()();
                    }
                }
                finally
                {
                    _executingLostFocusActions = false;
                }
            }
        }


        /// <summary>
        /// Raises the BeginningEdit event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnBeginningEdit(DataGridBeginningEditEventArgs e)
        {
            e.RoutedEvent ??= BeginningEditEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Raises the CellEditEnded event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnCellEditEnded(DataGridCellEditEndedEventArgs e)
        {
            e.RoutedEvent ??= CellEditEndedEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Raises the CellEditEnding event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnCellEditEnding(DataGridCellEditEndingEventArgs e)
        {
            e.RoutedEvent ??= CellEditEndingEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Raises the PreparingCellForEdit event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnPreparingCellForEdit(DataGridPreparingCellForEditEventArgs e)
        {
            e.RoutedEvent ??= PreparingCellForEditEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Raises the RowEditEnded event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnRowEditEnded(DataGridRowEditEndedEventArgs e)
        {
            e.RoutedEvent ??= RowEditEndedEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Raises the RowEditEnding event.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnRowEditEnding(DataGridRowEditEndingEventArgs e)
        {
            e.RoutedEvent ??= RowEditEndingEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

        private int _editingColumnIndex;

        private object _uneditedValue; // Represents the original current cell value at the time it enters editing mode.

        private RoutedEventArgs _editingEventArgs;

        private bool _focusEditingControl;
        private List<CellValidationSnapshot> _editingRowValidationSnapshot;
        private CellValidationSnapshot _editingCellValidationSnapshot;


        /// <summary>
        /// Occurs before a cell or row enters editing mode.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridBeginningEditEventArgs> BeginningEdit
        {
            add => AddHandler(BeginningEditEvent, value);
            remove => RemoveHandler(BeginningEditEvent, value);
        }


        /// <summary>
        /// Occurs after cell editing has ended.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellEditEndedEventArgs> CellEditEnded
        {
            add => AddHandler(CellEditEndedEvent, value);
            remove => RemoveHandler(CellEditEndedEvent, value);
        }


        /// <summary>
        /// Occurs immediately before cell editing has ended.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellEditEndingEventArgs> CellEditEnding
        {
            add => AddHandler(CellEditEndingEvent, value);
            remove => RemoveHandler(CellEditEndingEvent, value);
        }


        /// <summary>
        /// Occurs when a cell in a <see cref="T:Avalonia.Controls.DataGridTemplateColumn" /> enters editing mode.
        ///
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridPreparingCellForEditEventArgs> PreparingCellForEdit
        {
            add => AddHandler(PreparingCellForEditEvent, value);
            remove => RemoveHandler(PreparingCellForEditEvent, value);
        }


        /// <summary>
        /// Occurs when the row has been successfully committed or cancelled.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridRowEditEndedEventArgs> RowEditEnded
        {
            add => AddHandler(RowEditEndedEvent, value);
            remove => RemoveHandler(RowEditEndedEvent, value);
        }


        /// <summary>
        /// Occurs immediately before the row has been successfully committed or cancelled.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridRowEditEndingEventArgs> RowEditEnding
        {
            add => AddHandler(RowEditEndingEvent, value);
            remove => RemoveHandler(RowEditEndingEvent, value);
        }


        internal DataGridRow EditingRow
        {
            get;
            private set;
        }


        internal int EditingColumnIndex
            => _editingColumnIndex;


        private void OnIsReadOnlyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                var value = (bool)e.NewValue;
                if (value && !CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
                {
                    CancelEdit(DataGridEditingUnit.Row, raiseEvents: false);
                }

                RefreshRowsAndColumns(clearRows: false);
                UpdatePseudoClasses();
            }
        }

        private void OnAutoGenerateColumnsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var value = (bool)e.NewValue;
            if (value)
            {
                InitializeElements(recycleRows: false);
            }
            else
            {
                RemoveAutoGeneratedColumns();
            }
        }

    }
}
