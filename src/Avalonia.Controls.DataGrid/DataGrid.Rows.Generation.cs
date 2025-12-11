// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Styling;
using System.Diagnostics;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {

        /// <summary>
        /// Returns a row for the provided index. The row gets first loaded through the LoadingRow event.
        /// </summary>
        private DataGridRow GenerateRow(int rowIndex, int slot)
        {
            return GenerateRow(rowIndex, slot, DataConnection.GetDataItem(rowIndex));
        }



        /// <summary>
        /// Returns a row for the provided index. The row gets first loaded through the LoadingRow event.
        /// </summary>
        private DataGridRow GenerateRow(int rowIndex, int slot, object dataContext)
        {
            Debug.Assert(rowIndex > -1);
            DataGridRow dataGridRow = GetGeneratedRow(dataContext);
            if (dataGridRow == null)
            {
                var recycledRow = DisplayData.GetRecycledRow();
                dataGridRow = recycledRow ?? new DataGridRow();
                var previousDataContext = dataGridRow.DataContext;
                dataGridRow.Index = rowIndex;
                dataGridRow.Slot = slot;
                dataGridRow.OwningGrid = this;
                dataGridRow.DataContext = dataContext;
                if (RowTheme is {} rowTheme)
                {
                    dataGridRow.SetValue(ThemeProperty, rowTheme, BindingPriority.Template);
                }
                CompleteCellsCollection(dataGridRow);

                if (recycledRow != null &&
                    previousDataContext != dataContext &&
                    (dataContext == DataGridCollectionView.NewItemPlaceholder ||
                     previousDataContext == DataGridCollectionView.NewItemPlaceholder))
                {
                    foreach (DataGridCell cell in dataGridRow.Cells)
                    {
                        cell.Content = cell.OwningColumn.GenerateElementInternal(cell, dataContext);
                    }
                }

                OnLoadingRow(new DataGridRowEventArgs(dataGridRow));
            }
            return dataGridRow;
        }



        /// <summary>
        /// Creates all the editing elements for the current editing row, so the bindings
        /// all exist during validation.
        /// </summary>
        private void GenerateEditingElements()
        {
            if (EditingRow != null && EditingRow.Cells != null)
            {
                Debug.Assert(EditingRow.Cells.Count == ColumnsItemsInternal.Count);
                foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns(c => c.IsVisible && !c.IsReadOnly))
                {
                    column.GenerateEditingElementInternal(EditingRow.Cells[column.Index], EditingRow.DataContext);
                }
            }
        }



        /// <summary>
        /// Checks if the row for the provided dataContext has been generated and is present
        /// in either the loaded rows, pre-fetched rows, or editing row.
        /// The displayed rows are *not* searched. Returns null if the row does not belong to those 3 categories.
        /// </summary>
        private DataGridRow GetGeneratedRow(object dataContext)
        {
            // Check the list of rows being loaded via the LoadingRow event.
            DataGridRow dataGridRow = GetLoadedRow(dataContext);
            if (dataGridRow != null)
            {
                return dataGridRow;
            }

            // Check the potential editing row.
            if (EditingRow != null && dataContext == EditingRow.DataContext)
            {
                return EditingRow;
            }

            // Check the potential focused row.
            if (_focusedRow != null && dataContext == _focusedRow.DataContext)
            {
                return _focusedRow;
            }

            return null;
        }



        private DataGridRow GetLoadedRow(object dataContext)
        {
            foreach (DataGridRow dataGridRow in _loadedRows)
            {
                if (dataGridRow.DataContext == dataContext)
                {
                    return dataGridRow;
                }
            }
            return null;
        }



        private Control InsertDisplayedElement(int slot, bool updateSlotInformation)
        {
            Control slotElement;
            if (RowGroupHeadersTable.Contains(slot))
            {
                slotElement = GenerateRowGroupHeader(slot, rowGroupInfo: RowGroupHeadersTable.GetValueAt(slot));
            }
            else
            {
                // If we're grouping, the GroupLevel needs to be fixed later by methods calling this
                // which end up inserting rows. We don't do it here because elements could be inserted
                // from top to bottom or bottom to up so it's better to do in one pass
                slotElement = GenerateRow(RowIndexFromSlot(slot), slot);
            }
            InsertDisplayedElement(slot, slotElement, wasNewlyAdded: false, updateSlotInformation: updateSlotInformation);
            return slotElement;
        }



        private void InsertDisplayedElement(int slot, Control element, bool wasNewlyAdded, bool updateSlotInformation)
        {
            // We can only support creating new rows that are adjacent to the currently visible rows
            // since they need to be added to the visual tree for us to Measure them.
            Debug.Assert(DisplayData.FirstScrollingSlot == -1 || slot >= GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot) && slot <= GetNextVisibleSlot(DisplayData.LastScrollingSlot));
            Debug.Assert(element != null);

            if (_rowsPresenter != null)
            {
                DataGridRowGroupHeader groupHeader = null;
                DataGridRow row = element as DataGridRow;
                if (row != null)
                {
                    LoadRowVisualsForDisplay(row);

                    if (IsRowRecyclable(row))
                    {
                        if (_rowsPresenter != null && !_rowsPresenter.Children.Contains(row))
                        {
                            _rowsPresenter.Children.Add(row);
                        }
                    }
                    else
                    {
                        element.Clip = null;
                        Debug.Assert(row.Index == RowIndexFromSlot(slot));
                    }
                }
                else
                {
                    groupHeader = element as DataGridRowGroupHeader;
                    Debug.Assert(groupHeader != null);  // Nothing other and Rows and RowGroups now
                    if (groupHeader != null)
                    {
                        groupHeader.TotalIndent = (groupHeader.Level == 0) ? 0 : RowGroupSublevelIndents[groupHeader.Level - 1];
                        if (!groupHeader.IsRecycled)
                        {
                            _rowsPresenter.Children.Add(element);
                        }
                        groupHeader.LoadVisualsForDisplay();
                    }
                }

                if (row != null)
                {
                    _rowsPresenter.RegisterAnchorCandidate(row);
                }
                else if (groupHeader != null)
                {
                    _rowsPresenter.RegisterAnchorCandidate(groupHeader);
                }

                // Measure the element and update AvailableRowRoom
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                AvailableSlotElementRoom -= element.DesiredSize.Height;

                var estimator = RowHeightEstimator;
                
                if (groupHeader != null)
                {
                    _rowGroupHeightsByLevel[groupHeader.Level] = groupHeader.DesiredSize.Height;
                    // Record the measured group header height with the estimator
                    estimator?.RecordRowGroupHeaderHeight(slot, groupHeader.Level, element.DesiredSize.Height);
                }

                if (row != null)
                {
                    // Record the measured row height with the estimator
                    bool hasDetails = GetRowDetailsVisibility(slot);
                    // Details height is already included in element.DesiredSize.Height
                    estimator?.RecordMeasuredHeight(slot, element.DesiredSize.Height, hasDetails, 0);
                    
                    // Update the legacy estimate for backward compatibility
                    if (RowHeightEstimate == DataGrid.DATAGRID_defaultRowHeight && double.IsNaN(row.Height))
                    {
                        RowHeightEstimate = element.DesiredSize.Height;
                    }
                }
            }

            if (wasNewlyAdded)
            {
                DisplayData.CorrectSlotsAfterInsertion(slot, element, isCollapsed: false);
            }
            else
            {
                DisplayData.LoadScrollingSlot(slot, element, updateSlotInformation);
            }
        }


    }
}
