// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {

                private void AddSlotElement(int slot, Control element)
                {
        #if DEBUG
                    if (element is DataGridRow row)
                    {
                        Debug.Assert(row.OwningGrid == this);
                        Debug.Assert(row.Cells.Count == ColumnsItemsInternal.Count);

                        int columnIndex = 0;
                        foreach (DataGridCell dataGridCell in row.Cells)
                        {
                            Debug.Assert(dataGridCell.OwningRow == row);
                            Debug.Assert(dataGridCell.OwningColumn == ColumnsItemsInternal[columnIndex]);
                            columnIndex++;
                        }
                    }
        #endif
                    Debug.Assert(slot == SlotCount);

                    OnAddedElement_Phase1(slot, element);
                    SlotCount++;
                    VisibleSlotCount++;
                    OnAddedElement_Phase2(slot, updateVerticalScrollBarOnly: false);
                    OnElementsChanged(grew: true);
                }



                private void AddSlots(int totalSlots)
                {
                    SlotCount = 0;
                    VisibleSlotCount = 0;
                    IEnumerator<int> groupSlots = null;
                    int nextGroupSlot = -1;
                    if (RowGroupHeadersTable.RangeCount > 0)
                    {
                        groupSlots = RowGroupHeadersTable.GetIndexes().GetEnumerator();
                        if (groupSlots != null && groupSlots.MoveNext())
                        {
                            nextGroupSlot = groupSlots.Current;
                        }
                    }
                    int slot = 0;
                    int addedRows = 0;
                    while (slot < totalSlots && AvailableSlotElementRoom > 0)
                    {
                        if (slot == nextGroupSlot)
                        {
                            DataGridRowGroupInfo groupRowInfo = RowGroupHeadersTable.GetValueAt(slot);
                            AddSlotElement(slot, GenerateRowGroupHeader(slot, groupRowInfo));
                            nextGroupSlot = groupSlots.MoveNext() ? groupSlots.Current : -1;
                        }
                        else
                        {
                            AddSlotElement(slot, GenerateRow(addedRows, slot));
                            addedRows++;
                        }
                        slot++;
                    }

                    if (slot < totalSlots)
                    {
                        SlotCount += totalSlots - slot;
                        VisibleSlotCount += totalSlots - slot;
                        OnAddedElement_Phase2(0,
                            updateVerticalScrollBarOnly: !HasLegacyVerticalScrollBar || IsLegacyVerticalScrollBarVisible);
                        OnElementsChanged(grew: true);
                    }
                }



                internal int GetNextVisibleSlot(int slot)
                {
                    return _collapsedSlotsTable.GetNextGap(slot);
                }



                internal int GetPreviousVisibleSlot(int slot)
                {
                    return _collapsedSlotsTable.GetPreviousGap(slot);
                }



                internal bool IsSlotVisible(int slot)
                {
                    return slot >= DisplayData.FirstScrollingSlot
                       && slot <= DisplayData.LastScrollingSlot
                       && slot != -1
                       && !_collapsedSlotsTable.Contains(slot);
                }



                internal int RowIndexFromSlot(int slot)
                {
                    return slot - RowGroupHeadersTable.GetIndexCount(0, slot);
                }



                internal int SlotFromRowIndex(int rowIndex)
                {
                    return rowIndex + RowGroupHeadersTable.GetIndexCountBeforeGap(0, rowIndex);
                }


    }
}
