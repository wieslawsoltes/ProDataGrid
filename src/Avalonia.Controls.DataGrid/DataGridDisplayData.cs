// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Controls.Primitives;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    internal class DataGridDisplayData
    {
        private readonly Stack<DataGridRow> _recycledRows;
        private readonly Stack<DataGridRowGroupHeader> _recycledGroupHeaders;
        private readonly List<Control> _scrollingElements;
        private readonly DataGrid _owner;
        private int _headScrollingElements;

        public DataGridDisplayData(DataGrid owner)
        {
            _owner = owner;
            _scrollingElements = new List<Control>();
            _recycledRows = new Stack<DataGridRow>();
            _recycledGroupHeaders = new Stack<DataGridRowGroupHeader>();

            ResetSlotIndexes();
            FirstDisplayedScrollingCol = -1;
            LastTotallyDisplayedScrollingCol = -1;
        }

        #region Properties

        public int FirstDisplayedScrollingCol { get; set; }

        public int FirstScrollingSlot { get; set; }

        public int LastScrollingSlot { get; set; }

        public int LastTotallyDisplayedScrollingCol { get; set; }

        public int NumDisplayedScrollingElements => _scrollingElements.Count;

        public int NumTotallyDisplayedScrollingElements { get; set; }

        internal double PendingVerticalScrollHeight { get; set; }

        #endregion

        #region Row Recycling

        internal void RecycleRow(DataGridRow row)
        {
            Debug.Assert(row != null);
            row.DetachFromDataGrid(true);
            HideElement(row);
            PushToRecyclePool(_recycledRows, row);
        }

        internal DataGridRow? GetRecycledRow()
        {
            return PopFromRecyclePool(_recycledRows, RestoreElementVisibility);
        }

        internal void TrimRecycledPools(DataGridRowsPresenter owner, int maxRecycledRows, int maxRecycledGroupHeaders)
        {
            while (_recycledRows.Count > maxRecycledRows)
            {
                var row = _recycledRows.Pop();
                owner.UnregisterAnchorCandidate(row);
                owner.Children.Remove(row);
            }

            while (_recycledGroupHeaders.Count > maxRecycledGroupHeaders)
            {
                var header = _recycledGroupHeaders.Pop();
                owner.UnregisterAnchorCandidate(header);
                owner.Children.Remove(header);
            }
        }

        #endregion

        #region Group Header Recycling

        internal void RecycleGroupHeader(DataGridRowGroupHeader groupHeader)
        {
            Debug.Assert(groupHeader != null);
            groupHeader.IsRecycled = true;
            HideElement(groupHeader);
            PushToRecyclePool(_recycledGroupHeaders, groupHeader);
        }

        internal DataGridRowGroupHeader? GetRecycledGroupHeader()
        {
            return PopFromRecyclePool(_recycledGroupHeaders, RestoreElementVisibility);
        }

        #endregion

        #region Element Management

        internal void ClearElements(bool recycle)
        {
            ResetSlotIndexes();
            
            if (recycle)
            {
                RecycleAllScrollingElements();
            }
            else
            {
                _recycledRows.Clear();
                _recycledGroupHeaders.Clear();
            }
            
            _scrollingElements.Clear();
        }

        private void RecycleAllScrollingElements()
        {
            foreach (Control element in _scrollingElements)
            {
                switch (element)
                {
                    case DataGridRow row:
                        HideElement(row);
                        if (row.IsRecyclable)
                        {
                            row.DetachFromDataGrid(true);
                            PushToRecyclePool(_recycledRows, row);
                        }
                        else
                        {
                            row.Clip = new RectangleGeometry();
                        }
                        break;
                        
                    case DataGridRowGroupHeader groupHeader:
                        HideElement(groupHeader);
                        groupHeader.IsRecycled = true;
                        PushToRecyclePool(_recycledGroupHeaders, groupHeader);
                        break;
                }
            }
        }

        internal Control GetDisplayedElement(int slot)
        {
            Debug.Assert(slot >= FirstScrollingSlot);
            Debug.Assert(slot <= LastScrollingSlot);
            return _scrollingElements[GetCircularListIndex(slot, wrap: true)];
        }

        internal DataGridRow? GetDisplayedRow(int rowIndex)
        {
            return GetDisplayedElement(_owner.SlotFromRowIndex(rowIndex)) as DataGridRow;
        }

        internal IEnumerable<Control> GetScrollingElements(Predicate<object>? filter = null)
        {
            for (int i = 0; i < _scrollingElements.Count; i++)
            {
                Control element = _scrollingElements[(_headScrollingElements + i) % _scrollingElements.Count];
                if (filter == null || filter(element))
                {
                    yield return element;
                }
            }
        }

        internal IEnumerable<Control> GetScrollingRows()
        {
            return GetScrollingElements(element => element is DataGridRow);
        }

        #endregion

        #region Slot Management

        internal void LoadScrollingSlot(int slot, Control element, bool updateSlotInformation)
        {
            if (_scrollingElements.Count == 0)
            {
                SetScrollingSlots(slot);
                _scrollingElements.Add(element);
                return;
            }

            Debug.Assert(slot >= _owner.GetPreviousVisibleSlot(FirstScrollingSlot) && 
                         slot <= _owner.GetNextVisibleSlot(LastScrollingSlot));
            
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
            
            int insertIndex = GetCircularListIndex(slot, wrap: false);
            if (insertIndex > _scrollingElements.Count)
            {
                insertIndex -= _scrollingElements.Count;
                _headScrollingElements++;
            }
            _scrollingElements.Insert(insertIndex, element);
        }

        internal void UnloadScrollingElement(int slot, bool updateSlotInformation, bool wasDeleted)
        {
            Debug.Assert(_owner.IsSlotVisible(slot));
            
            int elementIndex = GetCircularListIndex(slot, wrap: false);
            if (elementIndex > _scrollingElements.Count)
            {
                elementIndex -= _scrollingElements.Count;
                _headScrollingElements--;
            }
            _scrollingElements.RemoveAt(elementIndex);

            if (updateSlotInformation)
            {
                UpdateSlotIndexesAfterUnload(slot, wasDeleted);
            }
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
                UnloadScrollingElement(slot, updateSlotInformation: true, wasDeleted: true);
            }
            
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
                FirstScrollingSlot++;
                LastScrollingSlot++;
            }
            else if (isCollapsed && slot <= LastScrollingSlot)
            {
                LastScrollingSlot++;
            }
            else if (_owner.GetPreviousVisibleSlot(slot) <= LastScrollingSlot || LastScrollingSlot == -1)
            {
                LoadScrollingSlot(slot, element, updateSlotInformation: true);
            }
        }

        #endregion

        #region Private Helpers

        private void HideElement(Control element)
        {
            _owner?.HideRecycledElement(element);
        }

        private static void RestoreElementVisibility(Control element)
        {
            element.ClearValue(Visual.IsVisibleProperty);
        }

        private static void PushToRecyclePool<T>(Stack<T> pool, T element) where T : Control
        {
            Debug.Assert(!pool.Contains(element));
            pool.Push(element);
        }

        private static T? PopFromRecyclePool<T>(Stack<T> pool, Action<T>? onPop = null) where T : Control
        {
            if (pool.Count > 0)
            {
                T element = pool.Pop();
                onPop?.Invoke(element);
                return element;
            }
            return null;
        }

        private int GetCircularListIndex(int slot, bool wrap)
        {
            int index = slot - FirstScrollingSlot - _headScrollingElements - 
                        _owner.GetCollapsedSlotCount(FirstScrollingSlot, slot);
            return wrap ? index % _scrollingElements.Count : index;
        }

        private void ResetSlotIndexes()
        {
            SetScrollingSlots(-1);
            NumTotallyDisplayedScrollingElements = 0;
            _headScrollingElements = 0;
        }

        private void SetScrollingSlots(int newValue)
        {
            FirstScrollingSlot = newValue;
            LastScrollingSlot = newValue;
        }

        private void UpdateSlotIndexesAfterUnload(int slot, bool wasDeleted)
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

        #endregion

        #region Debug

#if DEBUG
        internal void PrintDisplay()
        {
            foreach (Control element in GetScrollingElements())
            {
                switch (element)
                {
                    case DataGridRow row:
                        Debug.WriteLine($"Slot: {row.Slot} Row: {row.Index}");
                        break;
                    case DataGridRowGroupHeader groupHeader:
                        Debug.WriteLine($"Slot: {groupHeader.RowGroupInfo.Slot} GroupHeader: {groupHeader.RowGroupInfo.CollectionViewGroup.Key}");
                        break;
                }
            }
        }
#endif

        #endregion
    }
}
