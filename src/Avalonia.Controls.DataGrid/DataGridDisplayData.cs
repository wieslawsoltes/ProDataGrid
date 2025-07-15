// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Media;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    internal class DataGridDisplayData
    {
        private Stack<DataGridRow> _fullyRecycledRows; // list of Rows that have been fully recycled (Collapsed)
        private DataGrid _owner;
        private Stack<DataGridRow> _recyclableRows; // list of Rows which have not been fully recycled (avoids Measure in several cases)
        private RealizedStackElements _realizedElements = new();
        private Stack<DataGridRowGroupHeader> _fullyRecycledGroupHeaders; // list of GroupHeaders that have been fully recycled (Collapsed)
        private Stack<DataGridRowGroupHeader> _recyclableGroupHeaders; // list of GroupHeaders which have not been fully recycled (avoids Measure in several cases)
        private readonly Action<Control> _recycleElement;
        private readonly Action<Control, int, int> _updateElementIndex;

        public DataGridDisplayData(DataGrid owner)
        {
            _owner = owner;

            ResetSlotIndexes();
            FirstDisplayedScrollingCol = -1;
            LastTotallyDisplayedScrollingCol = -1;

            _recyclableRows = new Stack<DataGridRow>();
            _fullyRecycledRows = new Stack<DataGridRow>();
            _recyclableGroupHeaders = new Stack<DataGridRowGroupHeader>();
            _fullyRecycledGroupHeaders = new Stack<DataGridRowGroupHeader>();

            _recycleElement = RecycleElement;
            _updateElementIndex = UpdateElementIndex;
        }

        public int FirstDisplayedScrollingCol
        {
            get;
            set;
        }

        public int FirstScrollingSlot
        {
            get;
            set;
        }

        public int LastScrollingSlot
        {
            get;
            set;
        }

        public int LastTotallyDisplayedScrollingCol
        {
            get;
            set;
        }

        public int NumDisplayedScrollingElements
        {
            get => _realizedElements.Count;
        }

        public int NumTotallyDisplayedScrollingElements
        {
            get;
            set;
        }

        internal double PendingVerticalScrollHeight
        {
            get;
            set;
        }

        internal void AddRecyclableRow(DataGridRow row)
        {
            if (_recyclableRows.Contains(row))
                return;
            row.DetachFromDataGrid(true);
            _recyclableRows.Push(row);
        }

        internal DataGridRowGroupHeader? GetUsedGroupHeader()
        {
            if (_recyclableGroupHeaders.Count > 0)
            {
                return _recyclableGroupHeaders.Pop();
            }
            else if (_fullyRecycledGroupHeaders.Count > 0)
            {
                // For fully recycled rows, we need to set the Visibility back to Visible
                DataGridRowGroupHeader groupHeader = _fullyRecycledGroupHeaders.Pop();
                groupHeader.IsVisible = true;
                return groupHeader;
            }
            return null;
        }

        internal void AddRecylableRowGroupHeader(DataGridRowGroupHeader groupHeader)
        {
            if (_recyclableGroupHeaders.Contains(groupHeader))
                return;
            groupHeader.IsRecycled = true;
            _recyclableGroupHeaders.Push(groupHeader);
        }

        internal void ClearElements(bool recycle)
        {
            ResetSlotIndexes();
            if (recycle)
            {
                foreach (Control? element in _realizedElements.Elements)
                {
                    if (element is DataGridRow row)
                    {
                        if (row.IsRecyclable)
                            AddRecyclableRow(row);
                        else
                            row.Clip = new RectangleGeometry();
                    }
                    else if (element is DataGridRowGroupHeader groupHeader)
                    {
                        AddRecylableRowGroupHeader(groupHeader);
                    }
                }
                _realizedElements.ItemsReset(_recycleElement);
            }
            else
            {
                _recyclableRows.Clear();
                _fullyRecycledRows.Clear();
                _recyclableGroupHeaders.Clear();
                _fullyRecycledGroupHeaders.Clear();
            }
            _realizedElements.ResetForReuse();
        }

        internal void CorrectSlotsAfterDeletion(int slot, bool wasCollapsed)
        {
            if (wasCollapsed)
            {
                if (slot > FirstScrollingSlot)
                {
                    LastScrollingSlot--;
                }
            }
            else if (_owner.IsSlotVisible(slot))
            {
                UnloadScrollingElement(slot, true /*updateSlotInformation*/, true /*wasDeleted*/);
                _realizedElements.ItemsRemoved(slot, 1, _updateElementIndex, e => _recycleElement(e));
            }
            // This cannot be an else condition because if there are 2 rows left, and you delete the first one
            // then these indexes need to be updated as well
            if (slot < FirstScrollingSlot)
            {
                FirstScrollingSlot--;
                LastScrollingSlot--;
            }
        }

        internal void CorrectSlotsAfterInsertion(int slot, Control element, bool isCollapsed)
        {
            if (slot < FirstScrollingSlot)
            {
                // The row was inserted above our viewport, just update our indexes
                FirstScrollingSlot++;
                LastScrollingSlot++;
            }
            else if (isCollapsed && (slot <= LastScrollingSlot))
            {
                LastScrollingSlot++;
            }
            else if ((_owner.GetPreviousVisibleSlot(slot) <= LastScrollingSlot) || (LastScrollingSlot == -1))
            {
                _realizedElements.ItemsInserted(slot, 1, _updateElementIndex);

                // The row was inserted in our viewport, replace the placeholder with the element
                _realizedElements.SetElement(slot, element, element.DesiredSize.Height);

                // Track as scrolling element
                LoadScrollingSlot(slot, element, true /*updateSlotInformation*/);
                return;
            }

            _realizedElements.ItemsInserted(slot, 1, _updateElementIndex);
        }


        internal void FullyRecycleElements()
        {
            // Fully recycle Recyclable rows and transfer them to Recycled rows
            while (_recyclableRows.Count > 0)
            {
                DataGridRow row = _recyclableRows.Pop();
                row.IsVisible = false;
                Debug.Assert(!_fullyRecycledRows.Contains(row));
                _fullyRecycledRows.Push(row);
            }
            // Fully recycle Recyclable GroupHeaders and transfer them to Recycled GroupHeaders
            while (_recyclableGroupHeaders.Count > 0)
            {
                DataGridRowGroupHeader groupHeader = _recyclableGroupHeaders.Pop();
                groupHeader.IsVisible = false;
                Debug.Assert(!_fullyRecycledGroupHeaders.Contains(groupHeader));
                _fullyRecycledGroupHeaders.Push(groupHeader);
            }

            _realizedElements.RecycleAllElements((e, _) => _recycleElement(e));
        }

        internal Control GetDisplayedElement(int slot)
        {
            Debug.Assert(slot >= FirstScrollingSlot);
            Debug.Assert(slot <= LastScrollingSlot);

            return _realizedElements.GetElement(slot)!;
        }

        internal DataGridRow? GetDisplayedRow(int rowIndex)
        {

            return GetDisplayedElement(_owner.SlotFromRowIndex(rowIndex)) as DataGridRow;
        }

        // Returns an enumeration of the displayed scrolling rows in order starting with the FirstDisplayedScrollingRow
        internal IEnumerable<Control> GetScrollingElements()
        {
            return GetScrollingElements(null);
        }

        internal IEnumerable<Control> GetScrollingElements(Predicate<object>? filter)
        {
            foreach (var element in _realizedElements.Elements)
            {
                if (element is { } e && (filter is null || filter(e)))
                    yield return e;
            }
        }

        internal IEnumerable<Control> GetScrollingRows()
        {
            return GetScrollingElements(element => element is DataGridRow);
        }

        internal DataGridRow? GetUsedRow()
        {
            if (_recyclableRows.Count > 0)
            {
                return _recyclableRows.Pop();
            }
            else if (_fullyRecycledRows.Count > 0)
            {
                // For fully recycled rows, we need to set the Visibility back to Visible
                DataGridRow row = _fullyRecycledRows.Pop();
                row.IsVisible = true;
                return row;
            }
            return null;
        }

        // Tracks the row at index rowIndex as a scrolling row
        internal void LoadScrollingSlot(int slot, Control element, bool updateSlotInformation)
        {
            if (_realizedElements.Count == 0)
            {
                SetScrollingSlots(slot);
                _realizedElements.Add(slot, element, 0, element.DesiredSize.Height);
            }
            else
            {
                // The slot should be adjacent to the other slots being displayed
                Debug.Assert(slot >= _owner.GetPreviousVisibleSlot(FirstScrollingSlot) && slot <= _owner.GetNextVisibleSlot(LastScrollingSlot));

                if (updateSlotInformation)
                {
                    if (slot < FirstScrollingSlot)
                    {
                        FirstScrollingSlot = slot;
                    }
                    else
                    {
                        LastScrollingSlot = _owner.GetNextVisibleSlot(LastScrollingSlot);
                    }
                }

                if (_realizedElements.GetElement(slot) is null)
                    _realizedElements.SetElement(slot, element, element.DesiredSize.Height);
            }
        }

        private void ResetSlotIndexes()
        {
            SetScrollingSlots(-1);
            NumTotallyDisplayedScrollingElements = 0;
            _realizedElements.ResetForReuse();
        }

        private void SetScrollingSlots(int newValue)
        {
            FirstScrollingSlot = newValue;
            LastScrollingSlot = newValue;
        }

        // Stops tracking the element at the given slot as a scrolling element
        internal void UnloadScrollingElement(int slot, bool updateSlotInformation, bool wasDeleted)
        {
            Debug.Assert(_owner.IsSlotVisible(slot));

            if (slot == FirstScrollingSlot)
            {
                _realizedElements.RecycleElementsBefore(slot + 1, (e, _) => _recycleElement(e));
            }
            else if (slot == LastScrollingSlot)
            {
                _realizedElements.RecycleElementsAfter(slot - 1, (e, _) => _recycleElement(e));
            }
            else
            {
                _realizedElements.ItemsReplaced(slot, 1, e => _recycleElement(e));
            }

            if (updateSlotInformation)
            {
                if (slot == FirstScrollingSlot && !wasDeleted)
                {
                    FirstScrollingSlot = _owner.GetNextVisibleSlot(FirstScrollingSlot);
                }
                else
                {
                    LastScrollingSlot = _owner.GetPreviousVisibleSlot(LastScrollingSlot);
                }
                if (LastScrollingSlot < FirstScrollingSlot)
                {
                    ResetSlotIndexes();
                }
            }
        }

#if DEBUG
        internal void PrintDisplay()
        {
            foreach (Control element in GetScrollingElements())
            {
                if (element is DataGridRow row)
                {
                    Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Slot: {0} Row: {1} ", row.Slot, row.Index));
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Slot: {0} GroupHeader: {1}", groupHeader.RowGroupInfo.Slot, groupHeader.RowGroupInfo.CollectionViewGroup.Key));
                }
            }
        }
#endif
        private void RecycleElement(Control element)
        {
            if (element is DataGridRow row)
                AddRecyclableRow(row);
            else if (element is DataGridRowGroupHeader groupHeader)
                AddRecylableRowGroupHeader(groupHeader);
        }

        private void UpdateElementIndex(Control element, int oldIndex, int newIndex)
        {
            switch (element)
            {
                case DataGridRow row:
                    row.Slot = newIndex;
                    break;
                case DataGridRowGroupHeader groupHeader:
                    if (groupHeader.RowGroupInfo != null)
                        groupHeader.RowGroupInfo.Slot = newIndex;
                    break;
            }
        }
    }
}
