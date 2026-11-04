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
    internal enum DataGridRecycleReuseOrder
    {
        TopDown,
        BottomUp,
    }

    internal class DataGridDisplayData
    {
        private readonly Stack<DataGridRow> _recycledRows;
        private readonly Dictionary<object, Stack<DataGridRow>> _keyedRecycledRows;
        private readonly Stack<DataGridRowGroupHeader> _recycledGroupHeaders;
        private readonly Stack<DataGridRowGroupFooter> _recycledGroupFooters;
        private HashSet<Control>? _deferredHideElements;
        private readonly List<Control> _scrollingElements;
        private readonly DataGrid _owner;
        private RetargetEntry[] _retargetEntries = Array.Empty<RetargetEntry>();
        private Control[] _retargetElementOrder = Array.Empty<Control>();
        private bool _deferRecycledElementHiding;
        private DataGridRecycleReuseOrder _deferredReuseOrder;
        private int _deferredRecycleScopeDepth;
        private int _headScrollingElements;
        private int _virtualScrollingElementCount;

        public DataGridDisplayData(DataGrid owner)
        {
            _owner = owner;
            _scrollingElements = new List<Control>();
            _recycledRows = new Stack<DataGridRow>();
            _keyedRecycledRows = new Dictionary<object, Stack<DataGridRow>>();
            _recycledGroupHeaders = new Stack<DataGridRowGroupHeader>();
            _recycledGroupFooters = new Stack<DataGridRowGroupFooter>();
            ResetSlotIndexes();
            FirstDisplayedScrollingCol = -1;
            LastTotallyDisplayedScrollingCol = -1;
        }

        #region Properties

        public int FirstDisplayedScrollingCol { get; set; }

        public int FirstScrollingSlot { get; set; }

        public int LastScrollingSlot { get; set; }

        public int LastTotallyDisplayedScrollingCol { get; set; }

        public int NumDisplayedScrollingElements =>
            _virtualScrollingElementCount != 0
                ? _virtualScrollingElementCount
                : _scrollingElements.Count;

        public int NumTotallyDisplayedScrollingElements { get; set; }

        internal double PendingVerticalScrollHeight { get; set; }

        internal long RetargetedRowCount { get; private set; }

        internal bool HasVirtualScrollingElements => _virtualScrollingElementCount != 0;

        #endregion

        #region Row Recycling

        internal void RecycleRow(DataGridRow row)
        {
            Debug.Assert(row != null);
            using var recycleTimer = DataGridDiagnostics.BeginRowRecycle();
            using (DataGridDiagnostics.BeginRowRecycleCleanup())
            {
                row.RecycledDataContext = row.DataContext;
                row.RecycledIsPlaceholder = row.IsPlaceholder;
                _owner.NotifyRowRecycling(row);
            }

            using (DataGridDiagnostics.BeginRowRecycleDetach())
            {
                row.DetachFromDataGrid(true);
                HideElement(row);
            }

            using (DataGridDiagnostics.BeginRowRecyclePool())
            {
                if (_owner.UsesDefaultRealizationFactory)
                {
                    PushToRecyclePool(_recycledRows, row);
                    return;
                }

                object? key = _owner.GetRowRecyclingKey(row);
                if (key is null)
                {
                    _owner.DiscardUnkeyedRecycledRow(row);
                    return;
                }

                if (!_keyedRecycledRows.TryGetValue(key, out Stack<DataGridRow>? pool))
                {
                    pool = new Stack<DataGridRow>();
                    _keyedRecycledRows.Add(key, pool);
                }
                PushToRecyclePool(pool, row);
            }
        }

        internal DataGridRow? GetRecycledRow(object dataContext, int rowIndex, int slot)
        {
            if (_owner.UsesDefaultRealizationFactory)
            {
                return PopFromRecyclePool(_recycledRows, RestoreElementForReuse);
            }

            object? key = _owner.GetRowRecyclingKey(dataContext, rowIndex, slot);
            if (key is null || !_keyedRecycledRows.TryGetValue(key, out Stack<DataGridRow>? pool))
            {
                return null;
            }

            DataGridRow? row = PopFromRecyclePool(pool, RestoreElementForReuse);
            if (pool.Count == 0)
            {
                _keyedRecycledRows.Remove(key);
            }
            return row;
        }

        internal void TrimRecycledPools(DataGridRowsPresenter owner, int maxRecycledRows, int maxRecycledGroupHeaders, int maxRecycledGroupFooters)
        {
            while (_recycledRows.Count > maxRecycledRows)
            {
                var row = _recycledRows.Pop();
                owner.UnregisterAnchorCandidate(row);
                owner.RemoveTrackedChild(row);
            }

            int keyedRowCount = 0;
            foreach (Stack<DataGridRow> pool in _keyedRecycledRows.Values)
            {
                keyedRowCount += pool.Count;
            }
            if (keyedRowCount > maxRecycledRows)
            {
                int rowsToRemove = keyedRowCount - maxRecycledRows;
                List<object>? emptyKeys = null;
                foreach (KeyValuePair<object, Stack<DataGridRow>> entry in _keyedRecycledRows)
                {
                    Stack<DataGridRow> pool = entry.Value;
                    while (pool.Count > 0 && rowsToRemove > 0)
                    {
                        DataGridRow row = pool.Pop();
                        owner.UnregisterAnchorCandidate(row);
                        owner.RemoveTrackedChild(row);
                        rowsToRemove--;
                    }
                    if (pool.Count == 0)
                    {
                        emptyKeys ??= new List<object>();
                        emptyKeys.Add(entry.Key);
                    }
                    if (rowsToRemove == 0)
                    {
                        break;
                    }
                }
                if (emptyKeys != null)
                {
                    foreach (object key in emptyKeys)
                    {
                        _keyedRecycledRows.Remove(key);
                    }
                }
            }

            while (_recycledGroupHeaders.Count > maxRecycledGroupHeaders)
            {
                var header = _recycledGroupHeaders.Pop();
                owner.UnregisterAnchorCandidate(header);
                owner.RemoveTrackedChild(header);
            }

            while (_recycledGroupFooters.Count > maxRecycledGroupFooters)
            {
                var footer = _recycledGroupFooters.Pop();
                owner.UnregisterAnchorCandidate(footer);
                owner.RemoveTrackedChild(footer);
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
            return PopFromRecyclePool(_recycledGroupHeaders, RestoreElementForReuse);
        }

        internal void RecycleGroupFooter(DataGridRowGroupFooter groupFooter)
        {
            Debug.Assert(groupFooter != null);
            groupFooter.IsRecycled = true;
            HideElement(groupFooter);
            PushToRecyclePool(_recycledGroupFooters, groupFooter);
        }

        internal DataGridRowGroupFooter? GetRecycledGroupFooter()
        {
            return PopFromRecyclePool(_recycledGroupFooters, RestoreElementForReuse);
        }

        #endregion

        #region Element Management

        internal void ClearElements(bool recycle)
        {
            ResetSlotIndexes();
            _virtualScrollingElementCount = 0;
            
            if (recycle)
            {
                RecycleAllScrollingElements();
            }
            else
            {
                _recycledRows.Clear();
                _keyedRecycledRows.Clear();
                _recycledGroupHeaders.Clear();
                _recycledGroupFooters.Clear();
            }
            
            _scrollingElements.Clear();
            Array.Clear(_retargetEntries);
        }

        internal void SetVirtualScrollingSlots(int firstSlot, int lastSlot, int count)
        {
            Debug.Assert(_scrollingElements.Count == 0);
            Debug.Assert(count > 0);
            Debug.Assert(firstSlot >= 0);
            Debug.Assert(lastSlot >= firstSlot);

            FirstScrollingSlot = firstSlot;
            LastScrollingSlot = lastSlot;
            _virtualScrollingElementCount = count;
        }

        private void RecycleAllScrollingElements()
        {
            if (_deferRecycledElementHiding)
            {
                if (_deferredReuseOrder == DataGridRecycleReuseOrder.TopDown)
                {
                    for (int i = _scrollingElements.Count - 1; i >= 0; i--)
                    {
                        RecycleScrollingElement(GetLogicalScrollingElement(i));
                    }
                }
                else
                {
                    for (int i = 0; i < _scrollingElements.Count; i++)
                    {
                        RecycleScrollingElement(GetLogicalScrollingElement(i));
                    }
                }

                return;
            }

            foreach (Control element in _scrollingElements)
            {
                RecycleScrollingElement(element);
            }
        }

        private Control GetLogicalScrollingElement(int logicalIndex)
        {
            return _scrollingElements[(_headScrollingElements + logicalIndex) % _scrollingElements.Count];
        }

        private void RecycleScrollingElement(Control element)
        {
            switch (element)
            {
                case DataGridRow row:
                    // A row that is leaving the displayed range cannot remain pointer-over.
                    // Clear the transient state before testing recyclability so a stationary
                    // pointer does not strand an otherwise reusable offscreen row.
                    row.ClearPointerOverState();
                    if (row.IsRecyclable)
                    {
                        RecycleRow(row);
                    }
                    else
                    {
                        HideElement(row);
                        row.Clip = new RectangleGeometry();
                    }
                    break;

                case DataGridRowGroupHeader groupHeader:
                    HideElement(groupHeader);
                    groupHeader.IsRecycled = true;
                    PushToRecyclePool(_recycledGroupHeaders, groupHeader);
                    break;
                case DataGridRowGroupFooter groupFooter:
                    HideElement(groupFooter);
                    groupFooter.IsRecycled = true;
                    PushToRecyclePool(_recycledGroupFooters, groupFooter);
                    break;
            }
        }

        internal Control GetDisplayedElement(int slot)
        {
            Debug.Assert(slot >= FirstScrollingSlot);
            Debug.Assert(slot <= LastScrollingSlot);
            if (_virtualScrollingElementCount != 0)
            {
                return null;
            }
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

        internal bool TryRetargetDefaultVirtualRows(
            int firstSlot,
            int lastSlot,
            int rowCount,
            double rowHeight,
            bool slotsAreContiguous)
        {
            if (rowCount <= 0 || rowCount != _scrollingElements.Count)
            {
                return false;
            }

            if (_retargetEntries.Length < rowCount)
            {
                Array.Resize(ref _retargetEntries, rowCount);
            }

            int slot = firstSlot;
            using (DataGridDiagnostics.BeginRowsRetargetValidation())
            {
                for (int index = 0; index < rowCount; index++)
                {
                    if (GetLogicalScrollingElement(index) is not DataGridRow row)
                    {
                        return false;
                    }

                    // A row leaving the displayed range cannot remain pointer-over. The ordinary
                    // recycle pipeline clears this before checking recyclability as well.
                    row.ClearPointerOverState();
                    if (!_owner.CanRetargetDefaultVirtualRow(row, rowHeight))
                    {
                        return false;
                    }

                    int targetSlot = slotsAreContiguous ? firstSlot + index : slot;
                    int rowIndex = _owner.RowIndexFromSlot(targetSlot);
                    object item = _owner.DataConnection.GetDataItem(rowIndex);
                    if (item is DataGridRow)
                    {
                        return false;
                    }

                    _retargetEntries[index] = new RetargetEntry(row, targetSlot, rowIndex, item);

                    if (!slotsAreContiguous)
                    {
                        slot = _owner.GetNextVisibleSlot(slot);
                    }
                }
            }

            using (DataGridDiagnostics.BeginRowsRetargetBind())
            {
                using (DataGridDiagnostics.BeginRowsRetargetApply())
                {
                    for (int index = 0; index < rowCount; index++)
                    {
                        ref RetargetEntry entry = ref _retargetEntries[index];
                        _owner.RetargetDefaultVirtualRow(
                            entry.Row,
                            entry.Slot,
                            entry.RowIndex,
                            entry.Item);
                    }
                }

                DataGridDiagnostics.RecordRowsRetargeted(rowCount);
                DataGridDiagnostics.RecordRowsPrepared(rowCount);
                DataGridDiagnostics.RecordRowsRealized(
                    DataGridDiagnostics.Sources.Retargeted,
                    rowCount);

                using (DataGridDiagnostics.BeginRowsRetargetChildIndex())
                {
                    _owner.InvalidateDefaultVirtualRowsChildIndexes();
                }

                bool rowsRemainMeasureValid = true;
                bool rowsRemainArrangeValid = true;
                using (DataGridDiagnostics.BeginRowsRetargetLayoutValidity())
                {
                    for (int index = 0; index < rowCount; index++)
                    {
                        DataGridRow element = _retargetEntries[index].Row;
                        if (!element.IsMeasureValid)
                        {
                            rowsRemainMeasureValid = false;
                        }
                        if (!element.IsArrangeValid)
                        {
                            rowsRemainArrangeValid = false;
                        }
                        if (!rowsRemainMeasureValid && !rowsRemainArrangeValid)
                        {
                            break;
                        }
                    }
                }

                _owner.MarkDefaultVirtualRowsRetargeted(
                    rowCount,
                    rowHeight,
                    rowsRemainMeasureValid,
                    rowsRemainArrangeValid);
            }

            FirstScrollingSlot = firstSlot;
            LastScrollingSlot = lastSlot;
            RetargetedRowCount += rowCount;
            return true;
        }

        internal bool TryRetargetDefaultFlatRows(
            int firstSlot,
            int lastSlot,
            double rowHeight)
        {
            int rowCount = lastSlot - firstSlot + 1;
            if (rowCount <= 0 || rowCount != _scrollingElements.Count)
            {
                return false;
            }

            int oldFirstSlot = FirstScrollingSlot;
            if (_retargetElementOrder.Length < rowCount)
            {
                Array.Resize(ref _retargetElementOrder, rowCount);
            }
            int retargetCount = 0;

            try
            {
                using (DataGridDiagnostics.BeginRowsRetargetValidation())
                {
                    for (int index = 0; index < rowCount; index++)
                    {
                        if (GetLogicalScrollingElement(index) is not DataGridRow orderedRow ||
                            orderedRow.Slot != oldFirstSlot + index)
                        {
                            return false;
                        }
                    }

                    int shift = PositiveModulo(firstSlot - oldFirstSlot, rowCount);
                    for (int index = 0; index < rowCount; index++)
                    {
                        int oldLogicalIndex = index >= rowCount - shift
                            ? index - (rowCount - shift)
                            : index + shift;
                        DataGridRow row = (DataGridRow)GetLogicalScrollingElement(oldLogicalIndex);
                        _retargetElementOrder[index] = row;

                        int targetSlot = firstSlot + index;
                        if (row.Slot == targetSlot)
                        {
                            continue;
                        }

                        // A row that leaves the displayed range cannot remain pointer-over.
                        // Match the ordinary recycle path by clearing transient pointer state
                        // before checking whether the retained row can be reused in place.
                        row.ClearPointerOverState();
                        if (!_owner.CanRetargetDefaultFlatRow(row, rowHeight))
                        {
                            return false;
                        }

                        int rowIndex = _owner.RowIndexFromSlot(targetSlot);
                        object item = _owner.DataConnection.GetDataItem(rowIndex);
                        if (item is DataGridRow)
                        {
                            return false;
                        }

                        if (_retargetEntries.Length <= retargetCount)
                        {
                            Array.Resize(ref _retargetEntries, Math.Max(rowCount, retargetCount + 1));
                        }
                        _retargetEntries[retargetCount++] = new RetargetEntry(
                            row,
                            targetSlot,
                            rowIndex,
                            item);
                    }
                }

                using (DataGridDiagnostics.BeginRowsRetargetBind())
                {
                    _scrollingElements.Clear();
                    for (int index = 0; index < rowCount; index++)
                    {
                        _scrollingElements.Add(_retargetElementOrder[index]);
                    }
                    _headScrollingElements = 0;
                    FirstScrollingSlot = firstSlot;
                    LastScrollingSlot = lastSlot;
                    for (int index = 0; index < retargetCount; index++)
                    {
                        ref RetargetEntry entry = ref _retargetEntries[index];
                        entry.Row.ClearPointerOverState();
                        _owner.RetargetDefaultFlatRow(
                            entry.Row,
                            entry.Slot,
                            entry.RowIndex,
                            entry.Item);
                    }

                    if (retargetCount > 0)
                    {
                        DataGridDiagnostics.RecordRowsRetargeted(retargetCount);
                        DataGridDiagnostics.RecordRowsPrepared(retargetCount);
                        DataGridDiagnostics.RecordRowsRealized(
                            DataGridDiagnostics.Sources.Retargeted,
                            retargetCount);
                        RetargetedRowCount += retargetCount;
                    }
                }

                return true;
            }
            finally
            {
                Array.Clear(_retargetElementOrder, 0, rowCount);
            }
        }

        private readonly record struct RetargetEntry(
            DataGridRow Row,
            int Slot,
            int RowIndex,
            object Item);

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

        internal void UnloadScrollingElement(Control element, int slot, bool updateSlotInformation, bool wasDeleted)
        {
            if (element == null)
            {
                UnloadScrollingElement(slot, updateSlotInformation, wasDeleted);
                return;
            }

            int elementIndex = GetCircularListIndex(slot, wrap: false);
            if (elementIndex < 0 || elementIndex >= _scrollingElements.Count ||
                !ReferenceEquals(_scrollingElements[elementIndex], element))
            {
                elementIndex = _scrollingElements.IndexOf(element);
            }

            if (elementIndex < 0)
            {
                UnloadScrollingElement(slot, updateSlotInformation, wasDeleted);
                return;
            }

            if (elementIndex < _headScrollingElements)
            {
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

        internal void ClearRecyclePools()
        {
            _recycledRows.Clear();
            _keyedRecycledRows.Clear();
            _recycledGroupHeaders.Clear();
            _recycledGroupFooters.Clear();
        }

        internal DeferredRecycleScope BeginDeferredRecycleScope()
        {
            _deferredRecycleScopeDepth++;
            return new DeferredRecycleScope(this);
        }

        internal void ActivateDeferredRecycleHiding(DataGridRecycleReuseOrder reuseOrder)
        {
            if (_deferredRecycleScopeDepth <= 0)
            {
                return;
            }

            if (!_deferRecycledElementHiding)
            {
                _deferRecycledElementHiding = true;
                _deferredReuseOrder = reuseOrder;
            }
        }

        internal bool TryDeferElementHide(Control element)
        {
            if (!_deferRecycledElementHiding)
            {
                return false;
            }

            (_deferredHideElements ??= new HashSet<Control>()).Add(element);
            return true;
        }

        private void EndDeferredRecycleScope()
        {
            Debug.Assert(_deferredRecycleScopeDepth > 0);
            if (--_deferredRecycleScopeDepth > 0)
            {
                return;
            }

            _deferRecycledElementHiding = false;
            HashSet<Control>? deferredHideElements = _deferredHideElements;
            if (deferredHideElements == null || deferredHideElements.Count == 0)
            {
                return;
            }

            try
            {
                foreach (Control element in deferredHideElements)
                {
                    _owner.HideRecycledElement(element);
                }
            }
            finally
            {
                deferredHideElements.Clear();
            }
        }

        private void HideElement(Control element)
        {
            _owner?.HideRecycledElement(element);
        }

        private void RestoreElementForReuse(Control element)
        {
            _deferredHideElements?.Remove(element);
            if (!element.IsVisible)
            {
                element.ClearValue(Visual.IsVisibleProperty);
            }
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

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
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

        internal readonly struct DeferredRecycleScope : IDisposable
        {
            private readonly DataGridDisplayData? _owner;

            internal DeferredRecycleScope(DataGridDisplayData owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner?.EndDeferredRecycleScope();
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
