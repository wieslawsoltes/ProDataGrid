// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

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
                        dataGridRow = DisplayData.GetRecycledRow() ?? new DataGridRow();
                        dataGridRow.Index = rowIndex;
                        dataGridRow.Slot = slot;
                        dataGridRow.OwningGrid = this;
                        dataGridRow.DataContext = dataContext;
                        if (RowTheme is {} rowTheme)
                        {
                            dataGridRow.SetValue(ThemeProperty, rowTheme, BindingPriority.Template);
                        }
                        CompleteCellsCollection(dataGridRow);

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
                                if (!row.IsRecycled)
                                {
                                    Debug.Assert(!_rowsPresenter.Children.Contains(element));
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

                        // Measure the element and update AvailableRowRoom
                        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        AvailableSlotElementRoom -= element.DesiredSize.Height;

                        if (groupHeader != null)
                        {
                            _rowGroupHeightsByLevel[groupHeader.Level] = groupHeader.DesiredSize.Height;
                        }

                        if (row != null && RowHeightEstimate == DataGrid.DATAGRID_defaultRowHeight && double.IsNaN(row.Height))
                        {
                            RowHeightEstimate = element.DesiredSize.Height;
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
