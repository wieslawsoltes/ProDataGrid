// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Layout;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
        partial class DataGrid
    {

#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnColumnDisplayIndexChanged(DataGridColumnEventArgs e)
        {
            e.RoutedEvent ??= ColumnDisplayIndexChangedEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

#if !DATAGRID_INTERNAL
        protected internal
#else
        internal
#endif
        virtual void OnColumnReordered(DataGridColumnEventArgs e)
        {
            EnsureVerticalGridLines();
            e.RoutedEvent ??= ColumnReorderedEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

#if !DATAGRID_INTERNAL
        protected internal
#else
        internal
#endif
        virtual void OnColumnReordering(DataGridColumnReorderingEventArgs e)
        {
            e.RoutedEvent ??= ColumnReorderingEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

#if !DATAGRID_INTERNAL
        protected internal
#else
        internal
#endif
        virtual void OnColumnSorting(DataGridColumnEventArgs e)
        {
            e.RoutedEvent ??= SortingEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

        /// <summary>
        /// Adjusts the widths of all columns with DisplayIndex >= displayIndex such that the total
        /// width is adjusted by the given amount, if possible.  If the total desired adjustment amount
        /// could not be met, the remaining amount of adjustment is returned.
        /// </summary>
        /// <param name="displayIndex">Starting column DisplayIndex.</param>
        /// <param name="amount">Adjustment amount (positive for increase, negative for decrease).</param>
        /// <param name="userInitiated">Whether or not this adjustment was initiated by a user action.</param>
        /// <returns>The remaining amount of adjustment.</returns>
        internal double AdjustColumnWidths(int displayIndex, double amount, bool userInitiated)
        {
            if (!MathUtilities.IsZero(amount))
            {
                if (amount < 0)
                {
                    amount = DecreaseColumnWidths(displayIndex, amount, userInitiated);
                }
                else
                {
                    amount = IncreaseColumnWidths(displayIndex, amount, userInitiated);
                }
            }
            return amount;
        }

        /// <summary>
        /// Grows an auto-column's width to the desired width.
        /// </summary>
        /// <param name="column">Auto-column to adjust.</param>
        /// <param name="desiredWidth">The new desired width of the column.</param>
        internal void AutoSizeColumn(DataGridColumn column, double desiredWidth)
        {
            Debug.Assert(column.Width.IsAuto || column.Width.IsSizeToCells || column.Width.IsSizeToHeader || (!UsesStarSizing && column.Width.IsStar));

            // If we're using star sizing and this is the first time we've measured this particular auto-column,
            // we want to allow all rows to get measured before we setup the star widths.  We won't know the final
            // desired value of the column until all rows have been measured.  Because of this, we wait until
            // an Arrange occurs before we adjust star widths.
            if (UsesStarSizing && !column.IsInitialDesiredWidthDetermined)
            {
                AutoSizingColumns = true;
            }

            // Update the column's DesiredValue if it needs to grow to fit the new desired value
            if (desiredWidth > column.Width.DesiredValue || double.IsNaN(column.Width.DesiredValue))
            {
                // If this auto-growth occurs after the column's initial desired width has been determined,
                // then the growth should act like a resize (squish columns to the right).  Otherwise, if
                // this column is newly added, we'll just set its display value directly.
                if (UsesStarSizing && column.IsInitialDesiredWidthDetermined)
                {
                    var oldWidth = column.Width;
                    column.Resize(oldWidth,
                        new(column.Width.Value, column.Width.UnitType, desiredWidth, desiredWidth),
                        false);
                }
                else
                {
                    column.SetWidthInternalNoCallback(new DataGridLength(column.Width.Value, column.Width.UnitType, desiredWidth, desiredWidth));
                    OnColumnWidthChanged(column);
                }
            }
        }

        internal bool ColumnRequiresRightGridLine(DataGridColumn dataGridColumn, bool includeLastRightGridLineWhenPresent)
        {
            return (GridLinesVisibility == DataGridGridLinesVisibility.Vertical || GridLinesVisibility == DataGridGridLinesVisibility.All) && VerticalGridLinesBrush != null &&
                   (dataGridColumn != ColumnsInternal.LastVisibleColumn || (includeLastRightGridLineWhenPresent && ColumnsInternal.FillerColumn.IsActive));
        }

        /// <summary>
        /// Re-applies sequential <see cref="DataGridColumn.DisplayIndex"/> values based on the current order
        /// of the <see cref="Columns"/> collection, skipping filler/spacer columns. Useful when columns have
        /// been reordered via collection operations instead of setting <see cref="DataGridColumn.DisplayIndex"/>.
        /// </summary>
        public void UpdateColumnDisplayIndexesFromCollectionOrder()
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            _areHandlersSuspended = true;
            try
            {
                var displayIndex = 0;
                foreach (var column in ColumnsInternal.ItemsInternal)
                {
                    if (column is DataGridFillerColumn)
                    {
                        continue;
                    }

                    if (column.DisplayIndex != displayIndex)
                    {
                        column.DisplayIndex = displayIndex;
                    }

                    displayIndex++;
                }
            }
            finally
            {
                _areHandlersSuspended = false;
            }
        }

        internal DataGridColumnCollection CreateColumnsInstance()
        {
            return new DataGridColumnCollection(this);
        }

        /// <summary>
        /// Decreases the widths of all columns with DisplayIndex >= displayIndex such that the total
        /// width is decreased by the given amount, if possible.  If the total desired adjustment amount
        /// could not be met, the remaining amount of adjustment is returned.
        /// </summary>
        /// <param name="displayIndex">Starting column DisplayIndex.</param>
        /// <param name="amount">Amount to decrease (in pixels).</param>
        /// <param name="userInitiated">Whether or not this adjustment was initiated by a user action.</param>
        /// <returns>The remaining amount of adjustment.</returns>
        internal double DecreaseColumnWidths(int displayIndex, double amount, bool userInitiated)
        {
            // 1. Take space from non-star columns with widths larger than desired widths (left to right).
            amount = DecreaseNonStarColumnWidths(displayIndex, c => c.Width.DesiredValue, amount, false, false);

            // 2. Take space from star columns until they reach their min.
            amount = AdjustStarColumnWidths(displayIndex, amount, userInitiated);

            // 3. Take space from non-star columns that have already been initialized, until they reach their min (right to left).
            amount = DecreaseNonStarColumnWidths(displayIndex, c => c.ActualMinWidth, amount, true, false);

            // 4. Take space from all non-star columns until they reach their min, even if they are new (right to left).
            amount = DecreaseNonStarColumnWidths(displayIndex, c => c.ActualMinWidth, amount, true, true);

            return amount;
        }

        internal bool GetColumnReadOnlyState(DataGridColumn dataGridColumn, bool isReadOnly)
        {
            Debug.Assert(dataGridColumn != null);

            if (dataGridColumn is DataGridBoundColumn dataGridBoundColumn &&
                dataGridBoundColumn.Binding is BindingBase binding)
            {
                var path = (binding as Binding)?.Path ?? (binding as CompiledBindingExtension)?.Path.ToString();

                if (string.IsNullOrWhiteSpace(path))
                {
                    return true;
                }
                else
                {
                    return DataConnection.GetPropertyIsReadOnly(path) || isReadOnly;
                }
            }

            return isReadOnly;
        }

        // Returns the column's width
        internal static double GetEdgedColumnWidth(DataGridColumn dataGridColumn)
        {
            Debug.Assert(dataGridColumn != null);
            return dataGridColumn.ActualWidth;
        }

        /// <summary>
        /// Increases the widths of all columns with DisplayIndex >= displayIndex such that the total
        /// width is increased by the given amount, if possible.  If the total desired adjustment amount
        /// could not be met, the remaining amount of adjustment is returned.
        /// </summary>
        /// <param name="displayIndex">Starting column DisplayIndex.</param>
        /// <param name="amount">Amount of increase (in pixels).</param>
        /// <param name="userInitiated">Whether or not this adjustment was initiated by a user action.</param>
        /// <returns>The remaining amount of adjustment.</returns>
        internal double IncreaseColumnWidths(int displayIndex, double amount, bool userInitiated)
        {
            // 1. Give space to non-star columns that are smaller than their desired widths (left to right).
            amount = IncreaseNonStarColumnWidths(displayIndex, c => c.Width.DesiredValue, amount, false, false);

            // 2. Give space to star columns until they reach their max.
            amount = AdjustStarColumnWidths(displayIndex, amount, userInitiated);

            // 3. Give space to non-star columns that have already been initialized, until they reach their max (right to left).
            amount = IncreaseNonStarColumnWidths(displayIndex, c => c.ActualMaxWidth, amount, true, false);

            // 4. Give space to all non-star columns until they reach their max, even if they are new (right to left).
            amount = IncreaseNonStarColumnWidths(displayIndex, c => c.ActualMaxWidth, amount, true, false);

            return amount;
        }

        internal void OnClearingColumns()
        {
            // Rows need to be cleared first. There cannot be rows without also having columns.
            ClearRows(false);

            // Removing all the column header cells
            RemoveDisplayedColumnHeaders();

            _horizontalOffset = _negHorizontalOffset = 0;
            if (_hScrollBar != null && _hScrollBar.IsVisible) // 
            {
                _hScrollBar.Value = 0;
            }
        }

        /// <summary>
        /// Invalidates the widths of all columns because the resizing behavior of an individual column has changed.
        /// </summary>
        /// <param name="column">Column with CanUserResize property that has changed.</param>
        internal void OnColumnCanUserResizeChanged(DataGridColumn column)
        {
            if (column.IsVisible)
            {
                EnsureHorizontalLayout();
            }
        }

        internal void OnColumnCollectionChanged_PostNotification(bool columnsGrew)
        {
            if (columnsGrew &&
                CurrentColumnIndex == -1)
            {
                MakeFirstDisplayedCellCurrentCell();
            }

            if (_autoGeneratingColumnOperationCount == 0)
            {
                EnsureRowsPresenterVisibility();
                InvalidateRowHeightEstimate();
            }
        }

        internal void OnColumnCollectionChanged_PreNotification(bool columnsGrew)
        {
            // dataGridColumn==null means the collection was refreshed.
            if (_totalSummaryRow != null && _totalSummaryRow.CellsPresenter != null && _totalSummaryRow.Cells.Count != ColumnsItemsInternal.Count)
            {
                if (ColumnsItemsInternal.Count == 0)
                {
                    _summaryService?.InvalidateAll();
                }

                _totalSummaryRow.EnsureCells();
            }

            if (columnsGrew && _autoGeneratingColumnOperationCount == 0 && ColumnsItemsInternal.Count == 1)
            {
                RefreshRows(false /*recycleRows*/, true /*clearRows*/);
            }
            else
            {
                InvalidateMeasure();
            }
        }

        internal void OnColumnDisplayIndexChanged(DataGridColumn dataGridColumn)
        {
            Debug.Assert(dataGridColumn != null);
            DataGridColumnEventArgs e = new DataGridColumnEventArgs(dataGridColumn);

            // Call protected method to raise event
            if (dataGridColumn != ColumnsInternal.RowGroupSpacerColumn)
            {
                OnColumnDisplayIndexChanged(e);
            }
        }

        internal void OnColumnDisplayIndexChanged_PostNotification()
        {
            // Notifications for adjusted display indexes.
            FlushDisplayIndexChanged(true /*raiseEvent*/);

            // Our displayed columns may have changed so recompute them
            UpdateDisplayedColumns();

            // Invalidate layout
            CorrectColumnFrozenStates();
            EnsureHorizontalLayout();
        }

        internal void OnColumnDisplayIndexChanging(DataGridColumn targetColumn, int newDisplayIndex)
        {
            Debug.Assert(targetColumn != null);
            Debug.Assert(newDisplayIndex != targetColumn.DisplayIndexWithFiller);

            if (InDisplayIndexAdjustments)
            {
                // We are within columns display indexes adjustments. We do not allow changing display indexes while adjusting them.
                throw DataGridError.DataGrid.CannotChangeColumnCollectionWhileAdjustingDisplayIndexes();
            }

            try
            {
                InDisplayIndexAdjustments = true;

                bool trackChange = targetColumn != ColumnsInternal.RowGroupSpacerColumn;

                DataGridColumn column;
                // Move is legal - let's adjust the affected display indexes.
                if (newDisplayIndex < targetColumn.DisplayIndexWithFiller)
                {
                    // DisplayIndex decreases. All columns with newDisplayIndex <= DisplayIndex < targetColumn.DisplayIndex
                    // get their DisplayIndex incremented.
                    for (int i = newDisplayIndex; i < targetColumn.DisplayIndexWithFiller; i++)
                    {
                        column = ColumnsInternal.GetColumnAtDisplayIndex(i);
                        column.DisplayIndexWithFiller = column.DisplayIndexWithFiller + 1;
                        if (trackChange)
                        {
                            column.DisplayIndexHasChanged = true; // OnColumnDisplayIndexChanged needs to be raised later on
                        }
                    }
                }
                else
                {
                    // DisplayIndex increases. All columns with targetColumn.DisplayIndex < DisplayIndex <= newDisplayIndex
                    // get their DisplayIndex decremented.
                    for (int i = newDisplayIndex; i > targetColumn.DisplayIndexWithFiller; i--)
                    {
                        column = ColumnsInternal.GetColumnAtDisplayIndex(i);
                        column.DisplayIndexWithFiller = column.DisplayIndexWithFiller - 1;
                        if (trackChange)
                        {
                            column.DisplayIndexHasChanged = true; // OnColumnDisplayIndexChanged needs to be raised later on
                        }
                    }
                }
                // Now let's actually change the order of the DisplayIndexMap
                if (targetColumn.DisplayIndexWithFiller != -1)
                {
                    ColumnsInternal.DisplayIndexMap.Remove(targetColumn.Index);
                }
                ColumnsInternal.DisplayIndexMap.Insert(newDisplayIndex, targetColumn.Index);
            }
            finally
            {
                InDisplayIndexAdjustments = false;
            }

            // Note that displayIndex of moved column is updated by caller.
        }

        internal void OnColumnBindingChanged(DataGridBoundColumn column)
        {
            // Update Binding in Displayed rows by regenerating the affected elements
            if (_rowsPresenter != null)
            {
                foreach (DataGridRow row in GetAllRows())
                {
                    PopulateCellContent(false /*isCellEdited*/, column, row, row.Cells[column.Index]);
                }
            }

            if (column.HasSummaries)
            {
                OnColumnSummariesChanged(column);
            }
        }

        /// <summary>
        /// Adjusts the specified column's width according to its new maximum value.
        /// </summary>
        /// <param name="column">The column to adjust.</param>
        /// <param name="oldValue">The old ActualMaxWidth of the column.</param>
        internal void OnColumnMaxWidthChanged(DataGridColumn column, double oldValue)
        {
            Debug.Assert(column != null);

            if (column.IsVisible && oldValue != column.ActualMaxWidth)
            {
                DataGridLength oldWitdh = new(oldValue, column.Width.UnitType, column.Width.DesiredValue, column.Width.DesiredValue);
                if (column.ActualMaxWidth < column.Width.DisplayValue)
                {
                    // If the maximum width has caused the column to decrease in size, try first to resize
                    // the columns to the right to make up for the difference in width, but don't limit the column's
                    // final display value to how much they could be resized.
                    AdjustColumnWidths(column.DisplayIndex + 1, column.Width.DisplayValue - column.ActualMaxWidth, false);
                    column.SetWidthDisplayValue(column.ActualMaxWidth);
                }
                else if (column.Width.DisplayValue == oldValue && column.Width.DesiredValue > column.Width.DisplayValue)
                {
                    // If the column was previously limited by its maximum value but has more room now, 
                    // attempt to resize the column to its desired width.
                    column.Resize(oldWitdh,
                        new (column.Width.Value, column.Width.UnitType, column.Width.DesiredValue, column.Width.DesiredValue),
                        false);
                }
                OnColumnWidthChanged(column);
            }
        }

        /// <summary>
        /// Adjusts the specified column's width according to its new minimum value.
        /// </summary>
        /// <param name="column">The column to adjust.</param>
        /// <param name="oldValue">The old ActualMinWidth of the column.</param>
        internal void OnColumnMinWidthChanged(DataGridColumn column, double oldValue)
        {
            Debug.Assert(column != null);

            if (column.IsVisible && oldValue != column.ActualMinWidth)
            {
                DataGridLength oldWitdh = new(oldValue, column.Width.UnitType, column.Width.DesiredValue, column.Width.DesiredValue);
                if (column.ActualMinWidth > column.Width.DisplayValue)
                {
                    // If the minimum width has caused the column to increase in size, try first to resize
                    // the columns to the right to make up for the difference in width, but don't limit the column's
                    // final display value to how much they could be resized.
                    AdjustColumnWidths(column.DisplayIndex + 1, column.Width.DisplayValue - column.ActualMinWidth, false);
                    column.SetWidthDisplayValue(column.ActualMinWidth);
                }
                else if (column.Width.DisplayValue == oldValue && column.Width.DesiredValue < column.Width.DisplayValue)
                {
                    // If the column was previously limited by its minimum value but but can be smaller now, 
                    // attempt to resize the column to its desired width.
                    column.Resize(oldWitdh,
                       new(column.Width.Value, column.Width.UnitType, column.Width.DesiredValue, column.Width.DesiredValue),
                       false);
                }
                OnColumnWidthChanged(column);
            }
        }

        internal void OnColumnReadOnlyStateChanging(DataGridColumn dataGridColumn, bool isReadOnly)
        {
            Debug.Assert(dataGridColumn != null);
            if (isReadOnly && CurrentColumnIndex == dataGridColumn.Index)
            {
                // Edited column becomes read-only. Exit editing mode.
                if (!EndCellEdit(DataGridEditAction.Commit, true /*exitEditingMode*/, ContainsFocus /*keepFocus*/, true /*raiseEvents*/))
                {
                    EndCellEdit(DataGridEditAction.Cancel, true /*exitEditingMode*/, ContainsFocus /*keepFocus*/, false /*raiseEvents*/);
                }
            }
        }

        internal void OnColumnVisibleStateChanged(DataGridColumn updatedColumn)
        {
            Debug.Assert(updatedColumn != null);

            CorrectColumnFrozenStates();
            UpdateDisplayedColumns();
            EnsureRowsPresenterVisibility();
            EnsureHorizontalLayout();
            InvalidateColumnHeadersMeasure();

            if (updatedColumn.IsVisible &&
                ColumnsInternal.VisibleColumnCount == 1 && CurrentColumnIndex == -1)
            {
                Debug.Assert(SelectedIndex == GetSelectionModelIndexOfItem(SelectedItem));
                if (SelectedIndex != -1)
                {
                    SetAndSelectCurrentCell(updatedColumn.Index, SelectedIndex, true /*forceCurrentCellSelection*/);
                }
                else
                {
                    MakeFirstDisplayedCellCurrentCell();
                }
            }

            // We need to explicitly collapse the cells of the invisible column because layout only goes through
            // visible ones
            ColumnHeaders?.InvalidateChildIndex();
            foreach (var row in GetAllRows())
            {
                row.Cells[updatedColumn.Index].IsVisible = updatedColumn.IsVisible;
                row.InvalidateCellsIndex();
            }

            UpdateSearchAdapterView();
        }

        internal void OnColumnVisibleStateChanging(DataGridColumn targetColumn, bool wasVisible, bool isVisible)
        {
            Debug.Assert(targetColumn != null);

            if (wasVisible &&
                !isVisible &&
                CurrentColumn == targetColumn)
            {
                // Column of the current cell is made invisible. Trying to move the current cell to a neighbor column. May throw an exception.
                DataGridColumn dataGridColumn = ColumnsInternal.GetNextVisibleColumn(targetColumn);
                if (dataGridColumn == null)
                {
                    dataGridColumn = ColumnsInternal.GetPreviousVisibleNonFillerColumn(targetColumn);
                }
                if (dataGridColumn == null)
                {
                    SetCurrentCellCore(-1, -1);
                }
                else
                {
                    SetCurrentCellCore(dataGridColumn.Index, CurrentSlot);
                }
            }
        }

        internal void OnColumnWidthChanged(DataGridColumn updatedColumn)
        {
            Debug.Assert(updatedColumn != null);
            if (updatedColumn.IsVisible)
            {
                EnsureHorizontalLayout();
                UpdateSummaryRowLayout();
            }
        }

        internal void OnFillerColumnWidthNeeded(double finalWidth)
        {
            DataGridFillerColumn fillerColumn = ColumnsInternal.FillerColumn;
            double totalColumnsWidth = ColumnsInternal.VisibleEdgedColumnsWidth + ActualRowHeaderWidth;
            if (finalWidth - totalColumnsWidth > LayoutHelper.LayoutEpsilon)
            {
                fillerColumn.FillerWidth = finalWidth - totalColumnsWidth;
            }
            else
            {
                fillerColumn.FillerWidth = 0;
            }
        }

        internal void OnInsertedColumn_PostNotification(DataGridCellCoordinates newCurrentCellCoordinates, int newDisplayIndex)
        {
            // Update current cell if needed
            if (newCurrentCellCoordinates.ColumnIndex != -1)
            {
                Debug.Assert(CurrentColumnIndex == -1);
                SetAndSelectCurrentCell(newCurrentCellCoordinates.ColumnIndex,
                                        newCurrentCellCoordinates.Slot,
                                        ColumnsInternal.VisibleColumnCount == 1 /*forceCurrentCellSelection*/);

                if (newDisplayIndex < FrozenColumnCountWithFiller || FrozenColumnCountRightEffective > 0)
                {
                    CorrectColumnFrozenStates();
                }
            }
        }

        internal void OnInsertedColumn_PreNotification(DataGridColumn insertedColumn)
        {
            // Fix the Index of all following columns
            CorrectColumnIndexesAfterInsertion(insertedColumn, 1);

            Debug.Assert(insertedColumn.Index >= 0);
            Debug.Assert(insertedColumn.Index < ColumnsItemsInternal.Count);
            Debug.Assert(insertedColumn.OwningGrid == this);

            CorrectColumnDisplayIndexesAfterInsertion(insertedColumn);

            InsertDisplayedColumnHeader(insertedColumn);

            // Insert the missing data cells
            if (SlotCount > 0)
            {
                int newColumnCount = ColumnsItemsInternal.Count;

                foreach (DataGridRow row in GetAllRows())
                {
                    if (row.Cells.Count < newColumnCount)
                    {
                        AddNewCellPrivate(row, insertedColumn);
                    }
                }
            }

            // Update summary row
            _totalSummaryRow?.OnColumnAdded(insertedColumn, insertedColumn.Index);
            OnGroupSummaryColumnAdded(insertedColumn, insertedColumn.Index);

            if (insertedColumn.IsVisible)
            {
                EnsureHorizontalLayout();
            }

            if (insertedColumn is DataGridBoundColumn boundColumn && !boundColumn.IsAutoGenerated)
            {
                boundColumn.SetHeaderFromBinding();
            }
        }

        internal DataGridCellCoordinates OnInsertingColumn(int columnIndexInserted, DataGridColumn insertColumn)
        {
            DataGridCellCoordinates newCurrentCellCoordinates;
            Debug.Assert(insertColumn != null);

            if (insertColumn.OwningGrid != null && insertColumn != ColumnsInternal.RowGroupSpacerColumn)
            {
                throw DataGridError.DataGrid.ColumnCannotBeReassignedToDifferentDataGrid();
            }

            // Reset current cell if there is one, no matter the relative position of the columns involved
            if (CurrentColumnIndex != -1)
            {
                _temporarilyResetCurrentCell = true;
                newCurrentCellCoordinates = new DataGridCellCoordinates(columnIndexInserted <= CurrentColumnIndex ? CurrentColumnIndex + 1 : CurrentColumnIndex,
                     CurrentSlot);
                ResetCurrentCellCore();
            }
            else
            {
                newCurrentCellCoordinates = new DataGridCellCoordinates(-1, -1);
            }
            return newCurrentCellCoordinates;
        }

        internal void OnRemovedColumn_PostNotification(DataGridCellCoordinates newCurrentCellCoordinates)
        {
            // Update current cell if needed
            if (newCurrentCellCoordinates.ColumnIndex != -1)
            {
                Debug.Assert(CurrentColumnIndex == -1);
                SetAndSelectCurrentCell(newCurrentCellCoordinates.ColumnIndex, newCurrentCellCoordinates.Slot, false /*forceCurrentCellSelection*/);
            }
        }

        internal void OnRemovedColumn_PreNotification(DataGridColumn removedColumn)
        {
            Debug.Assert(removedColumn.Index >= 0);
            Debug.Assert(removedColumn.OwningGrid == null);

            // Intentionally keep the DisplayIndex intact after detaching the column.
            CorrectColumnIndexesAfterDeletion(removedColumn);

            CorrectColumnDisplayIndexesAfterDeletion(removedColumn);

            // If the detached column was frozen, a new column needs to take its place
            if (removedColumn.IsFrozen)
            {
                removedColumn.IsFrozen = false;
                CorrectColumnFrozenStates();
            }

            UpdateDisplayedColumns();

            // Fix the existing rows by removing cells at correct index
            int newColumnCount = ColumnsItemsInternal.Count;

            if (_rowsPresenter != null)
            {
                foreach (DataGridRow row in GetAllRows())
                {
                    if (row.Cells.Count > newColumnCount)
                    {
                        row.Cells.RemoveAt(removedColumn.Index);
                    }
                }
                _rowsPresenter.InvalidateArrange();
            }

            _summaryService?.InvalidateColumn(removedColumn);

            // Update summary row
            _totalSummaryRow?.OnColumnRemoved(removedColumn);
            OnGroupSummaryColumnRemoved(removedColumn);

            RemoveDisplayedColumnHeader(removedColumn);
        }

        internal DataGridCellCoordinates OnRemovingColumn(DataGridColumn dataGridColumn)
        {
            Debug.Assert(dataGridColumn != null);
            Debug.Assert(dataGridColumn.Index >= 0 && dataGridColumn.Index < ColumnsItemsInternal.Count);

            DataGridCellCoordinates newCurrentCellCoordinates;

            _temporarilyResetCurrentCell = false;
            int columnIndex = dataGridColumn.Index;

            // Reset the current cell's address if there is one.
            if (CurrentColumnIndex != -1)
            {
                int newCurrentColumnIndex = CurrentColumnIndex;
                if (columnIndex == newCurrentColumnIndex)
                {
                    DataGridColumn dataGridColumnNext = ColumnsInternal.GetNextVisibleColumn(ColumnsItemsInternal[columnIndex]);
                    if (dataGridColumnNext != null)
                    {
                        if (dataGridColumnNext.Index > columnIndex)
                        {
                            newCurrentColumnIndex = dataGridColumnNext.Index - 1;
                        }
                        else
                        {
                            newCurrentColumnIndex = dataGridColumnNext.Index;
                        }
                    }
                    else
                    {
                        DataGridColumn dataGridColumnPrevious = ColumnsInternal.GetPreviousVisibleNonFillerColumn(ColumnsItemsInternal[columnIndex]);
                        if (dataGridColumnPrevious != null)
                        {
                            if (dataGridColumnPrevious.Index > columnIndex)
                            {
                                newCurrentColumnIndex = dataGridColumnPrevious.Index - 1;
                            }
                            else
                            {
                                newCurrentColumnIndex = dataGridColumnPrevious.Index;
                            }
                        }
                        else
                        {
                            newCurrentColumnIndex = -1;
                        }
                    }
                }
                else if (columnIndex < newCurrentColumnIndex)
                {
                    newCurrentColumnIndex--;
                }
                newCurrentCellCoordinates = new DataGridCellCoordinates(newCurrentColumnIndex, (newCurrentColumnIndex == -1) ? -1 : CurrentSlot);
                if (columnIndex == CurrentColumnIndex)
                {
                    // If the commit fails, force a cancel edit
                    if (!CommitEdit(DataGridEditingUnit.Row, false /*exitEditingMode*/))
                    {
                        CancelEdit(DataGridEditingUnit.Row, false /*raiseEvents*/);
                    }
                    bool success = SetCurrentCellCore(-1, -1);
                    Debug.Assert(success);
                }
                else
                {
                    // Underlying data of deleted column is gone. It cannot be accessed anymore.
                    // Do not end editing mode so that CellValidation doesn't get raised, since that event needs the current formatted value.
                    _temporarilyResetCurrentCell = true;
                    bool success = SetCurrentCellCore(-1, -1);
                    Debug.Assert(success);
                }
            }
            else
            {
                newCurrentCellCoordinates = new DataGridCellCoordinates(-1, -1);
            }

            // If the last column is removed, delete all the rows first.
            if (ColumnsItemsInternal.Count == 1)
            {
                ClearRows(false);
            }

            // Is deleted column scrolled off screen?
            if (dataGridColumn.IsVisible &&
                !dataGridColumn.IsFrozen &&
                DisplayData.FirstDisplayedScrollingCol >= 0)
            {
                // Deleted column is part of scrolling columns.
                if (DisplayData.FirstDisplayedScrollingCol == dataGridColumn.Index)
                {
                    // Deleted column is first scrolling column
                    _horizontalOffset -= _negHorizontalOffset;
                    _negHorizontalOffset = 0;
                }
                else if (!ColumnsInternal.DisplayInOrder(DisplayData.FirstDisplayedScrollingCol, dataGridColumn.Index))
                {
                    // Deleted column is displayed before first scrolling column
                    Debug.Assert(_horizontalOffset >= GetEdgedColumnWidth(dataGridColumn));
                    _horizontalOffset -= GetEdgedColumnWidth(dataGridColumn);
                }

                if (_hScrollBar != null && _hScrollBar.IsVisible) // 
                {
                    _hScrollBar.Value = _horizontalOffset;
                }
            }

            return newCurrentCellCoordinates;
        }

        /// <summary>
        /// Called when a column property changes, and its cells need to 
        /// adjust that that column change.
        /// </summary>
        internal void RefreshColumnElements(DataGridColumn dataGridColumn, string propertyName)
        {
            Debug.Assert(dataGridColumn != null);

            // Take care of the non-displayed loaded rows
            for (int index = 0; index < _loadedRows.Count;)
            {
                DataGridRow dataGridRow = _loadedRows[index];
                Debug.Assert(dataGridRow != null);
                if (!IsSlotVisible(dataGridRow.Slot))
                {
                    RefreshCellElement(dataGridColumn, dataGridRow, propertyName);
                }
                index++;
            }

            // Take care of the displayed rows
            if (_rowsPresenter != null)
            {
                foreach (DataGridRow row in GetAllRows())
                {
                    RefreshCellElement(dataGridColumn, row, propertyName);
                }
                // This update could change layout so we need to update our estimate and invalidate
                InvalidateRowHeightEstimate();
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Adjusts the widths of all star columns with DisplayIndex >= displayIndex such that the total
        /// width is adjusted by the given amount, if possible.  If the total desired adjustment amount
        /// could not be met, the remaining amount of adjustment is returned.
        /// </summary>
        /// <param name="displayIndex">Starting column DisplayIndex.</param>
        /// <param name="adjustment">Adjustment amount (positive for increase, negative for decrease).</param>
        /// <param name="userInitiated">Whether or not this adjustment was initiated by a user action.</param>
        /// <returns>The remaining amount of adjustment.</returns>
        private double AdjustStarColumnWidths(int displayIndex, double adjustment, bool userInitiated)
        {
            double remainingAdjustment = adjustment;
            if (MathUtilities.IsZero(remainingAdjustment))
            {
                return remainingAdjustment;
            }
            bool increase = remainingAdjustment > 0;

            // Make an initial pass through the star columns to total up some values.
            bool scaleStarWeights = false;
            double totalStarColumnsWidth = 0;
            double totalStarColumnsWidthLimit = 0;
            double totalStarWeights = 0;
            List<DataGridColumn> starColumns = new List<DataGridColumn>();
            foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns(c => c.Width.IsStar && c.IsVisible && (c.ActualCanUserResize || !userInitiated)))
            {
                if (column.DisplayIndex < displayIndex)
                {
                    scaleStarWeights = true;
                    continue;
                }
                starColumns.Add(column);
                totalStarWeights += column.Width.Value;
                totalStarColumnsWidth += column.Width.DisplayValue;
                totalStarColumnsWidthLimit += increase ? column.ActualMaxWidth : column.ActualMinWidth;
            }

            // Set the new desired widths according to how much all the star columns can be adjusted without any
            // of them being limited by their minimum or maximum widths (as that would distort their ratios).
            double adjustmentLimit = totalStarColumnsWidthLimit - totalStarColumnsWidth;
            adjustmentLimit = increase ? Math.Min(adjustmentLimit, adjustment) : Math.Max(adjustmentLimit, adjustment);
            foreach (DataGridColumn starColumn in starColumns)
            {
                starColumn.SetWidthDesiredValue((totalStarColumnsWidth + adjustmentLimit) * starColumn.Width.Value / totalStarWeights);
            }

            // Adjust the star column widths first towards their desired values, and then towards their limits.
            remainingAdjustment = AdjustStarColumnWidths(displayIndex, remainingAdjustment, userInitiated, c => c.Width.DesiredValue);
            remainingAdjustment = AdjustStarColumnWidths(displayIndex, remainingAdjustment, userInitiated, c => increase ? c.ActualMaxWidth : c.ActualMinWidth);

            // Set the new star value weights according to how much the total column widths have changed.
            // Only do this if there were other star columns to the left, though.  If there weren't any then that means
            // all the star columns were adjusted at the same time, and therefore, their ratios have not changed.
            if (scaleStarWeights)
            {
                double starRatio = (totalStarColumnsWidth + adjustment - remainingAdjustment) / totalStarColumnsWidth;
                foreach (DataGridColumn starColumn in starColumns)
                {
                    starColumn.SetWidthStarValue(Math.Min(double.MaxValue, starRatio * starColumn.Width.Value));
                }
            }

            return remainingAdjustment;
        }

        /// <summary>
        /// Adjusts the widths of all star columns with DisplayIndex >= displayIndex such that the total
        /// width is adjusted by the given amount, if possible.  If the total desired adjustment amount
        /// could not be met, the remaining amount of adjustment is returned.  The columns will stop adjusting
        /// once they hit their target widths.
        /// </summary>
        /// <param name="displayIndex">Starting column DisplayIndex.</param>
        /// <param name="remainingAdjustment">Adjustment amount (positive for increase, negative for decrease).</param>
        /// <param name="userInitiated">Whether or not this adjustment was initiated by a user action.</param>
        /// <param name="targetWidth">The target width of the column.</param>
        /// <returns>The remaining amount of adjustment.</returns>
        private double AdjustStarColumnWidths(int displayIndex, double remainingAdjustment, bool userInitiated, Func<DataGridColumn, double> targetWidth)
        {
            if (MathUtilities.IsZero(remainingAdjustment))
            {
                return remainingAdjustment;
            }
            bool increase = remainingAdjustment > 0;

            double totalStarWeights = 0;
            double totalStarColumnsWidth = 0;

            // Order the star columns according to which one will hit their target width (or min/max limit) first.
            // Each KeyValuePair represents a column (as the key) and an ordering factor (as the value).  The ordering factor
            // is computed based on the distance from each column's current display width to its target width.  Because each column
            // could have different star ratios, though, this distance is then adjusted according to its star value.  A column with
            // a larger star value, for example, will change size more rapidly than a column with a lower star value.
            List<KeyValuePair<DataGridColumn, double>> starColumnPairs = new List<KeyValuePair<DataGridColumn, double>>();
            foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns(
                c => c.Width.IsStar && c.DisplayIndex >= displayIndex && c.IsVisible && c.Width.Value > 0 && (c.ActualCanUserResize || !userInitiated)))
            {
                int insertIndex = 0;
                double distanceToTarget = Math.Min(column.ActualMaxWidth, Math.Max(targetWidth(column), column.ActualMinWidth)) - column.Width.DisplayValue;
                double factor = (increase ? Math.Max(0, distanceToTarget) : Math.Min(0, distanceToTarget)) / column.Width.Value;
                foreach (KeyValuePair<DataGridColumn, double> starColumnPair in starColumnPairs)
                {
                    if (increase ? factor <= starColumnPair.Value : factor >= starColumnPair.Value)
                    {
                        break;
                    }
                    insertIndex++;
                }
                starColumnPairs.Insert(insertIndex, new KeyValuePair<DataGridColumn, double>(column, factor));
                totalStarWeights += column.Width.Value;
                totalStarColumnsWidth += column.Width.DisplayValue;
            }

            // Adjust the column widths one at a time until they either hit their individual target width
            // or the total remaining amount to adjust has been depleted.
            foreach (KeyValuePair<DataGridColumn, double> starColumnPair in starColumnPairs)
            {
                double distanceToTarget = starColumnPair.Value * starColumnPair.Key.Width.Value;
                double distanceAvailable = (starColumnPair.Key.Width.Value * remainingAdjustment) / totalStarWeights;
                double adjustment = increase ? Math.Min(distanceToTarget, distanceAvailable) : Math.Max(distanceToTarget, distanceAvailable);

                remainingAdjustment -= adjustment;
                totalStarWeights -= starColumnPair.Key.Width.Value;
                starColumnPair.Key.SetWidthDisplayValue(Math.Max(DataGrid.DATAGRID_minimumStarColumnWidth, starColumnPair.Key.Width.DisplayValue + adjustment));
            }

            return remainingAdjustment;
        }

        private bool ComputeDisplayedColumns()
        {
            bool invalidate = false;
            int numVisibleScrollingCols = 0;
            int visibleScrollingColumnsTmp = 0;
            double frozenLeftWidth = ColumnsInternal.GetVisibleFrozenLeftEdgedColumnsWidth();
            double frozenRightWidth = ColumnsInternal.GetVisibleFrozenRightEdgedColumnsWidth();
            double displayWidth = Math.Max(0, CellsWidth - frozenRightWidth);
            double cx = 0;
            int firstDisplayedFrozenCol = -1;
            int firstDisplayedScrollingCol = DisplayData.FirstDisplayedScrollingCol;

            // the same problem with negative numbers:
            // if the width passed in is negative, then return 0
            if (displayWidth <= 0 || ColumnsInternal.VisibleColumnCount == 0)
            {
                DisplayData.FirstDisplayedScrollingCol = -1;
                DisplayData.LastTotallyDisplayedScrollingCol = -1;
                return invalidate;
            }

            foreach (DataGridColumn dataGridColumn in ColumnsInternal.GetVisibleFrozenLeftColumns())
            {
                if (firstDisplayedFrozenCol == -1)
                {
                    firstDisplayedFrozenCol = dataGridColumn.Index;
                }
                cx += GetEdgedColumnWidth(dataGridColumn);
                if (cx >= displayWidth)
                {
                    break;
                }
            }

            Debug.Assert(cx <= frozenLeftWidth);

            if (cx < displayWidth && firstDisplayedScrollingCol >= 0)
            {
                DataGridColumn dataGridColumn = ColumnsItemsInternal[firstDisplayedScrollingCol];
                if (dataGridColumn.IsFrozen)
                {
                    dataGridColumn = ColumnsInternal.FirstVisibleScrollingColumn;
                    _negHorizontalOffset = 0;
                    if (dataGridColumn == null)
                    {
                        DisplayData.FirstDisplayedScrollingCol = DisplayData.LastTotallyDisplayedScrollingCol = -1;
                        return invalidate;
                    }
                    else
                    {
                        firstDisplayedScrollingCol = dataGridColumn.Index;
                    }
                }

                cx -= _negHorizontalOffset;
                while (cx < displayWidth && dataGridColumn != null)
                {
                    cx += GetEdgedColumnWidth(dataGridColumn);
                    visibleScrollingColumnsTmp++;
                    dataGridColumn = ColumnsInternal.GetNextVisibleScrollingColumn(dataGridColumn);
                }
                numVisibleScrollingCols = visibleScrollingColumnsTmp;

                // if we inflate the data area then we paint columns to the left of firstDisplayedScrollingCol
                if (cx < displayWidth)
                {
                    Debug.Assert(firstDisplayedScrollingCol >= 0);
                    //first minimize value of _negHorizontalOffset
                    if (_negHorizontalOffset > 0)
                    {
                        invalidate = true;
                        if (displayWidth - cx > _negHorizontalOffset)
                        {
                            cx += _negHorizontalOffset;
                            _horizontalOffset -= _negHorizontalOffset;
                            if (_horizontalOffset < LayoutHelper.LayoutEpsilon)
                            {
                                // Snap to zero to avoid trying to partially scroll in first scrolled off column below
                                _horizontalOffset = 0;
                            }

                            _negHorizontalOffset = 0;
                        }
                        else
                        {
                            _horizontalOffset -= displayWidth - cx;
                            _negHorizontalOffset -= displayWidth - cx;
                            cx = displayWidth;
                        }

                        // Make sure the HorizontalAdjustment is not greater than the new HorizontalOffset
                        // since it would cause an assertion failure in DataGridCellsPresenter.ShouldDisplayCell
                        // called by DataGridCellsPresenter.MeasureOverride.
                        HorizontalAdjustment = Math.Min(HorizontalAdjustment, _horizontalOffset);
                    }
                    // second try to scroll entire columns
                    if (cx < displayWidth && _horizontalOffset > 0)
                    {
                        Debug.Assert(_negHorizontalOffset == 0);
                        dataGridColumn = ColumnsInternal.GetPreviousVisibleScrollingColumn(ColumnsItemsInternal[firstDisplayedScrollingCol]);
                        while (dataGridColumn != null && cx + GetEdgedColumnWidth(dataGridColumn) <= displayWidth)
                        {
                            cx += GetEdgedColumnWidth(dataGridColumn);
                            visibleScrollingColumnsTmp++;
                            invalidate = true;
                            firstDisplayedScrollingCol = dataGridColumn.Index;
                            _horizontalOffset -= GetEdgedColumnWidth(dataGridColumn);
                            dataGridColumn = ColumnsInternal.GetPreviousVisibleScrollingColumn(dataGridColumn);
                        }
                    }
                    // third try to partially scroll in first scrolled off column
                    if (cx < displayWidth && _horizontalOffset > 0)
                    {
                        Debug.Assert(_negHorizontalOffset == 0);
                        dataGridColumn = ColumnsInternal.GetPreviousVisibleScrollingColumn(ColumnsItemsInternal[firstDisplayedScrollingCol]);
                        Debug.Assert(dataGridColumn != null);
                        Debug.Assert(GetEdgedColumnWidth(dataGridColumn) > displayWidth - cx);
                        firstDisplayedScrollingCol = dataGridColumn.Index;
                        _negHorizontalOffset = GetEdgedColumnWidth(dataGridColumn) - displayWidth + cx;
                        _horizontalOffset -= displayWidth - cx;
                        visibleScrollingColumnsTmp++;
                        invalidate = true;
                        cx = displayWidth;
                        Debug.Assert(_negHorizontalOffset == GetNegHorizontalOffsetFromHorizontalOffset(_horizontalOffset));
                    }

                    // update the number of visible columns to the new reality
                    Debug.Assert(numVisibleScrollingCols <= visibleScrollingColumnsTmp, "the number of displayed columns can only grow");
                    numVisibleScrollingCols = visibleScrollingColumnsTmp;
                }

                int jumpFromFirstVisibleScrollingCol = numVisibleScrollingCols - 1;
                if (cx > displayWidth)
                {
                    jumpFromFirstVisibleScrollingCol--;
                }

                Debug.Assert(jumpFromFirstVisibleScrollingCol >= -1);

                if (jumpFromFirstVisibleScrollingCol < 0)
                {
                    DisplayData.LastTotallyDisplayedScrollingCol = -1; // no totally visible scrolling column at all
                }
                else
                {
                    Debug.Assert(firstDisplayedScrollingCol >= 0);
                    dataGridColumn = ColumnsItemsInternal[firstDisplayedScrollingCol];
                    for (int jump = 0; jump < jumpFromFirstVisibleScrollingCol; jump++)
                    {
                        dataGridColumn = ColumnsInternal.GetNextVisibleScrollingColumn(dataGridColumn);
                        Debug.Assert(dataGridColumn != null);
                    }
                    DisplayData.LastTotallyDisplayedScrollingCol = dataGridColumn.Index;
                }
            }
            else
            {
                DisplayData.LastTotallyDisplayedScrollingCol = -1;
            }
            DisplayData.FirstDisplayedScrollingCol = firstDisplayedScrollingCol;

            return invalidate;
        }

        private int ComputeFirstVisibleScrollingColumn()
        {
            if (ColumnsInternal.GetVisibleFrozenEdgedColumnsWidth() >= CellsWidth)
            {
                // Not enough room for scrolling columns.
                _negHorizontalOffset = 0;
                return -1;
            }

            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleScrollingColumn;

            if (_horizontalOffset == 0)
            {
                _negHorizontalOffset = 0;
                return (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            }

            double cx = 0;
            while (dataGridColumn != null)
            {
                cx += GetEdgedColumnWidth(dataGridColumn);
                if (cx > _horizontalOffset)
                {
                    break;
                }
                dataGridColumn = ColumnsInternal.GetNextVisibleScrollingColumn(dataGridColumn);
            }

            if (dataGridColumn == null)
            {
                Debug.Assert(cx <= _horizontalOffset);
                dataGridColumn = ColumnsInternal.FirstVisibleScrollingColumn;
                if (dataGridColumn == null)
                {
                    _negHorizontalOffset = 0;
                    return -1;
                }
                else
                {
                    if (_negHorizontalOffset != _horizontalOffset)
                    {
                        _negHorizontalOffset = 0;
                    }
                    return dataGridColumn.Index;
                }
            }
            else
            {
                _negHorizontalOffset = GetEdgedColumnWidth(dataGridColumn) - (cx - _horizontalOffset);
                return dataGridColumn.Index;
            }
        }

        private bool GetColumnEffectiveReadOnlyState(DataGridColumn dataGridColumn)
        {
            Debug.Assert(dataGridColumn != null);

            return IsReadOnly || dataGridColumn.IsReadOnly || dataGridColumn is DataGridFillerColumn;
        }

        /// <devdoc>
        ///      Returns the absolute coordinate of the left edge of the given column (including
        ///      the potential gridline - that is the left edge of the gridline is returned). Note that
        ///      the column does not need to be in the display area.
        /// </devdoc>
        private double GetColumnXFromIndex(int index)
        {
            Debug.Assert(index < ColumnsItemsInternal.Count);
            Debug.Assert(ColumnsItemsInternal[index].IsVisible);

            double x = 0;
            foreach (DataGridColumn column in ColumnsInternal.GetVisibleColumns())
            {
                if (index == column.Index)
                {
                    break;
                }
                x += GetEdgedColumnWidth(column);
            }
            return x;
        }

        private double GetNegHorizontalOffsetFromHorizontalOffset(double horizontalOffset)
        {
            foreach (DataGridColumn column in ColumnsInternal.GetVisibleScrollingColumns())
            {
                if (GetEdgedColumnWidth(column) > horizontalOffset)
                {
                    break;
                }
                horizontalOffset -= GetEdgedColumnWidth(column);
            }
            return horizontalOffset;
        }

        private void InsertDisplayedColumnHeader(DataGridColumn dataGridColumn)
        {
            Debug.Assert(dataGridColumn != null);
            if (_columnHeadersPresenter != null)
            {
                dataGridColumn.HeaderCell.IsVisible = dataGridColumn.IsVisible;
                Debug.Assert(!_columnHeadersPresenter.Children.Contains(dataGridColumn.HeaderCell));
                _columnHeadersPresenter.Children.Insert(dataGridColumn.DisplayIndexWithFiller, dataGridColumn.HeaderCell);
            }
        }

        private static void RefreshCellElement(DataGridColumn dataGridColumn, DataGridRow dataGridRow, string propertyName)
        {
            Debug.Assert(dataGridColumn != null);
            Debug.Assert(dataGridRow != null);

            DataGridCell dataGridCell = dataGridRow.Cells[dataGridColumn.Index];
            Debug.Assert(dataGridCell != null);
            if (dataGridCell.Content is Control element)
            {
                dataGridColumn.RefreshCellContent(element, propertyName);
            }
            dataGridColumn.RefreshCellBindings(dataGridCell, propertyName);
        }

        private void UpdateDisplayedColumns()
        {
            DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();
            ComputeDisplayedColumns();
        }

        private void OnColumnWidthChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var value = (DataGridLength)e.NewValue;

            foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns())
            {
                if (column.InheritsWidth)
                {
                    column.SetWidthInternalNoCallback(value);
                }
            }

            EnsureHorizontalLayout();
        }

        private void OnCanUserResizeColumnsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            EnsureHorizontalLayout();
        }

        private void OnMinColumnWidthChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                double oldValue = (double)e.OldValue;
                foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns())
                {
                    OnColumnMinWidthChanged(column, Math.Max(column.MinWidth, oldValue));
                }
            }
        }

        private void OnMaxColumnWidthChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                var oldValue = (double)e.OldValue;
                foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns())
                {
                    OnColumnMaxWidthChanged(column, Math.Min(column.MaxWidth, oldValue));
                }
            }
        }

        private void OnFrozenColumnCountChanged(AvaloniaPropertyChangedEventArgs e)
        {
            ProcessFrozenColumnCount();
        }

        private void OnFrozenColumnCountRightChanged(AvaloniaPropertyChangedEventArgs e)
        {
            ProcessFrozenColumnCount();
        }

        private void ProcessFrozenColumnCount()
        {
            CorrectColumnFrozenStates();
            ComputeScrollBarsLayout();

            InvalidateColumnHeadersArrange();
            InvalidateCellsArrange();
        }

    }

}
