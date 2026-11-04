// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {

        private void AddSlotElement(int slot, Control element)
        {
            #if DEBUG
            if (element is DataGridRow row)
            {
                Debug.Assert(row.OwningGrid == this);
                int expectedCellCount = UsesVirtualCellSurface && !IsVirtualCompatibilityRow(row)
                    ? 0
                    : ColumnsItemsInternal.Count;
                Debug.Assert(row.Cells.Count == expectedCellCount);

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
            if (UsesLightweightVirtualRows)
            {
                SlotCount = totalSlots;
                VisibleSlotCount = totalSlots;
                if (totalSlots > 0)
                {
                    OnAddedElement_Phase2(
                        0,
                        updateVerticalScrollBarOnly: !HasLegacyVerticalScrollBar || IsLegacyVerticalScrollBarVisible);
                    OnElementsChanged(grew: true);
                }

                return;
            }

            IEnumerator<int> headerSlots = null;
            IEnumerator<int> footerSlots = null;
            int nextHeaderSlot = -1;
            int nextFooterSlot = -1;
            if (RowGroupHeadersTable.RangeCount > 0)
            {
                headerSlots = RowGroupHeadersTable.GetIndexes().GetEnumerator();
                if (headerSlots != null && headerSlots.MoveNext())
                {
                    nextHeaderSlot = headerSlots.Current;
                }
            }
            if (RowGroupFootersTable.RangeCount > 0)
            {
                footerSlots = RowGroupFootersTable.GetIndexes().GetEnumerator();
                if (footerSlots != null && footerSlots.MoveNext())
                {
                    nextFooterSlot = footerSlots.Current;
                }
            }
            int slot = 0;
            int addedRows = 0;
            while (slot < totalSlots && AvailableSlotElementRoom > 0)
            {
                if (slot == nextHeaderSlot)
                {
                    DataGridRowGroupInfo groupRowInfo = RowGroupHeadersTable.GetValueAt(slot);
                    AddSlotElement(slot, GenerateRowGroupHeader(slot, groupRowInfo));
                    nextHeaderSlot = headerSlots.MoveNext() ? headerSlots.Current : -1;
                }
                else if (slot == nextFooterSlot)
                {
                    DataGridRowGroupInfo groupRowInfo = RowGroupFootersTable.GetValueAt(slot);
                    AddSlotElement(slot, GenerateRowGroupFooter(slot, groupRowInfo));
                    nextFooterSlot = footerSlots.MoveNext() ? footerSlots.Current : -1;
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
            return slot - GetGroupSlotCountBefore(slot);
        }



        internal int SlotFromRowIndex(int rowIndex)
        {
            return rowIndex + GetGroupSlotCountBeforeGap(rowIndex);
        }

        internal bool IsGroupHeaderSlot(int slot)
        {
            return RowGroupHeadersTable.Contains(slot);
        }

        internal bool IsGroupFooterSlot(int slot)
        {
            return RowGroupFootersTable.Contains(slot);
        }

        internal bool IsGroupSlot(int slot)
        {
            return IsGroupHeaderSlot(slot) || IsGroupFooterSlot(slot);
        }

        internal DataGridRowGroupInfo GetGroupInfoForSlot(int slot)
        {
            return RowGroupHeadersTable.GetValueAt(slot) ?? RowGroupFootersTable.GetValueAt(slot);
        }

        private int GetGroupSlotCountBefore(int slot)
        {
            return RowGroupHeadersTable.GetIndexCount(0, slot)
                + RowGroupFootersTable.GetIndexCount(0, slot);
        }

        private int GetGroupSlotCountBeforeGap(int rowIndex)
        {
            return RowGroupHeadersTable.GetIndexCountBeforeGap(0, rowIndex)
                + RowGroupFootersTable.GetIndexCountBeforeGap(0, rowIndex);
        }


    }
}
