// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Manages virtualization state for the DataGrid, including row recycling,
    /// element realization, and size estimation.
    /// This class extracts virtualization logic to prepare for ILogicalScrollable migration.
    /// </summary>
    internal class DataGridVirtualizationController
    {
        private readonly DataGrid _owner;
        
        // Recycling pools for rows
        private readonly Stack<DataGridRow> _recycledRows;
        private readonly Stack<DataGridRow> _recyclableRows;
        
        // Recycling pools for group headers
        private readonly Stack<DataGridRowGroupHeader> _recycledGroupHeaders;
        private readonly Stack<DataGridRowGroupHeader> _recyclableGroupHeaders;
        
        // Currently realized (visible) elements
        private readonly List<Control> _realizedElements;
        private int _headElementIndex;
        
        // Realized slot range
        private int _firstRealizedSlot;
        private int _lastRealizedSlot;
        
        // Size estimation
        private double _estimatedRowHeight;
        private double _estimatedRowDetailsHeight;
        private int _lastMeasuredRowIndex;
        
        // Pending scroll state
        private double _pendingVerticalScrollHeight;
        
        // Default row height constant (same as in DataGrid)
        private const double DefaultRowHeight = 22.0;

        /// <summary>
        /// Initializes a new instance of the DataGridVirtualizationController.
        /// </summary>
        /// <param name="owner">The owning DataGrid.</param>
        public DataGridVirtualizationController(DataGrid owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            
            _recycledRows = new Stack<DataGridRow>();
            _recyclableRows = new Stack<DataGridRow>();
            _recycledGroupHeaders = new Stack<DataGridRowGroupHeader>();
            _recyclableGroupHeaders = new Stack<DataGridRowGroupHeader>();
            _realizedElements = new List<Control>();
            
            ResetState();
            
            _estimatedRowHeight = DefaultRowHeight;
            _estimatedRowDetailsHeight = 0;
            _lastMeasuredRowIndex = -1;
        }

        #region Properties

        /// <summary>
        /// Gets the first realized (visible) slot index.
        /// </summary>
        public int FirstRealizedSlot => _firstRealizedSlot;

        /// <summary>
        /// Gets the last realized (visible) slot index.
        /// </summary>
        public int LastRealizedSlot => _lastRealizedSlot;

        /// <summary>
        /// Gets the number of currently realized elements.
        /// </summary>
        public int RealizedElementCount => _realizedElements.Count;

        /// <summary>
        /// Gets or sets the number of totally displayed elements (not clipped).
        /// </summary>
        public int TotallyDisplayedElementCount { get; set; }

        /// <summary>
        /// Gets or sets the first displayed scrolling column index.
        /// </summary>
        public int FirstDisplayedScrollingColumn { get; set; } = -1;

        /// <summary>
        /// Gets or sets the last totally displayed scrolling column index.
        /// </summary>
        public int LastTotallyDisplayedScrollingColumn { get; set; } = -1;

        /// <summary>
        /// Gets or sets the pending vertical scroll height.
        /// Used to defer scroll operations until the next measure pass.
        /// </summary>
        public double PendingVerticalScrollHeight
        {
            get => _pendingVerticalScrollHeight;
            set => _pendingVerticalScrollHeight = value;
        }

        /// <summary>
        /// Gets or sets the estimated row height used for scroll calculations.
        /// </summary>
        public double EstimatedRowHeight
        {
            get => _estimatedRowHeight;
            set
            {
                if (value > 0)
                {
                    _estimatedRowHeight = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the estimated row details height.
        /// </summary>
        public double EstimatedRowDetailsHeight
        {
            get => _estimatedRowDetailsHeight;
            set
            {
                if (value >= 0)
                {
                    _estimatedRowDetailsHeight = value;
                }
            }
        }

        /// <summary>
        /// Gets the index of the last row used to calculate the row height estimate.
        /// </summary>
        public int LastMeasuredRowIndex
        {
            get => _lastMeasuredRowIndex;
            set => _lastMeasuredRowIndex = value;
        }

        /// <summary>
        /// Gets the number of recyclable rows available.
        /// </summary>
        public int RecyclableRowCount => _recyclableRows.Count;

        /// <summary>
        /// Gets the number of fully recycled rows available.
        /// </summary>
        public int RecycledRowCount => _recycledRows.Count;

        /// <summary>
        /// Gets the number of recyclable group headers available.
        /// </summary>
        public int RecyclableGroupHeaderCount => _recyclableGroupHeaders.Count;

        /// <summary>
        /// Gets the number of fully recycled group headers available.
        /// </summary>
        public int RecycledGroupHeaderCount => _recycledGroupHeaders.Count;

        #endregion

        #region Row Recycling

        /// <summary>
        /// Adds a row to the recyclable pool.
        /// </summary>
        /// <param name="row">The row to recycle.</param>
        public void AddRecyclableRow(DataGridRow row)
        {
            Debug.Assert(row != null);
            Debug.Assert(!_recyclableRows.Contains(row));
            
            row.DetachFromDataGrid(true);
            _recyclableRows.Push(row);
        }

        /// <summary>
        /// Gets a row from the recycling pool, or null if none available.
        /// </summary>
        /// <returns>A recycled row, or null.</returns>
        public DataGridRow? GetRecycledRow()
        {
            if (_recyclableRows.Count > 0)
            {
                return _recyclableRows.Pop();
            }
            
            if (_recycledRows.Count > 0)
            {
                var row = _recycledRows.Pop();
                row.ClearValue(Visual.IsVisibleProperty);
                return row;
            }
            
            return null;
        }

        /// <summary>
        /// Transfers all recyclable rows to the fully recycled pool.
        /// </summary>
        public void FullyRecycleRows()
        {
            while (_recyclableRows.Count > 0)
            {
                var row = _recyclableRows.Pop();
                row.SetCurrentValue(Visual.IsVisibleProperty, false);
                Debug.Assert(!_recycledRows.Contains(row));
                _recycledRows.Push(row);
            }
        }

        /// <summary>
        /// Clears all recycled rows from the pools.
        /// </summary>
        public void ClearRecycledRows()
        {
            _recyclableRows.Clear();
            _recycledRows.Clear();
        }

        #endregion

        #region Group Header Recycling

        /// <summary>
        /// Adds a group header to the recyclable pool.
        /// </summary>
        /// <param name="groupHeader">The group header to recycle.</param>
        public void AddRecyclableGroupHeader(DataGridRowGroupHeader groupHeader)
        {
            Debug.Assert(groupHeader != null);
            Debug.Assert(!_recyclableGroupHeaders.Contains(groupHeader));
            
            groupHeader.IsRecycled = true;
            _recyclableGroupHeaders.Push(groupHeader);
        }

        /// <summary>
        /// Gets a group header from the recycling pool, or null if none available.
        /// </summary>
        /// <returns>A recycled group header, or null.</returns>
        public DataGridRowGroupHeader? GetRecycledGroupHeader()
        {
            if (_recyclableGroupHeaders.Count > 0)
            {
                return _recyclableGroupHeaders.Pop();
            }
            
            if (_recycledGroupHeaders.Count > 0)
            {
                var groupHeader = _recycledGroupHeaders.Pop();
                groupHeader.ClearValue(Visual.IsVisibleProperty);
                return groupHeader;
            }
            
            return null;
        }

        /// <summary>
        /// Transfers all recyclable group headers to the fully recycled pool.
        /// </summary>
        public void FullyRecycleGroupHeaders()
        {
            while (_recyclableGroupHeaders.Count > 0)
            {
                var groupHeader = _recyclableGroupHeaders.Pop();
                groupHeader.SetCurrentValue(Visual.IsVisibleProperty, false);
                Debug.Assert(!_recycledGroupHeaders.Contains(groupHeader));
                _recycledGroupHeaders.Push(groupHeader);
            }
        }

        /// <summary>
        /// Clears all recycled group headers from the pools.
        /// </summary>
        public void ClearRecycledGroupHeaders()
        {
            _recyclableGroupHeaders.Clear();
            _recycledGroupHeaders.Clear();
        }

        #endregion

        #region Element Realization

        /// <summary>
        /// Realizes (loads) an element at the specified slot.
        /// </summary>
        /// <param name="slot">The slot index.</param>
        /// <param name="element">The element to realize.</param>
        /// <param name="updateSlotInformation">Whether to update slot tracking.</param>
        public void RealizeElement(int slot, Control element, bool updateSlotInformation)
        {
            if (_realizedElements.Count == 0)
            {
                SetRealizedSlotRange(slot, slot);
                _realizedElements.Add(element);
            }
            else
            {
                Debug.Assert(slot >= _owner.GetPreviousVisibleSlot(_firstRealizedSlot) && 
                             slot <= _owner.GetNextVisibleSlot(_lastRealizedSlot));
                
                if (updateSlotInformation)
                {
                    if (slot < _firstRealizedSlot)
                    {
                        _firstRealizedSlot = slot;
                    }
                    else
                    {
                        _lastRealizedSlot = _owner.GetNextVisibleSlot(_lastRealizedSlot);
                    }
                }
                
                int insertIndex = GetCircularIndex(slot, wrap: false);
                if (insertIndex > _realizedElements.Count)
                {
                    insertIndex -= _realizedElements.Count;
                    _headElementIndex++;
                }
                
                _realizedElements.Insert(insertIndex, element);
            }
        }

        /// <summary>
        /// Unrealizes (unloads) the element at the specified slot.
        /// </summary>
        /// <param name="slot">The slot index.</param>
        /// <param name="updateSlotInformation">Whether to update slot tracking.</param>
        /// <param name="wasDeleted">Whether the element was deleted from the data source.</param>
        public void UnrealizeElement(int slot, bool updateSlotInformation, bool wasDeleted)
        {
            Debug.Assert(_owner.IsSlotVisible(slot));
            
            int elementIndex = GetCircularIndex(slot, wrap: false);
            if (elementIndex > _realizedElements.Count)
            {
                elementIndex -= _realizedElements.Count;
                _headElementIndex--;
            }
            
            _realizedElements.RemoveAt(elementIndex);

            if (updateSlotInformation)
            {
                if (slot == _firstRealizedSlot && !wasDeleted)
                {
                    _firstRealizedSlot = _owner.GetNextVisibleSlot(_firstRealizedSlot);
                }
                else
                {
                    _lastRealizedSlot = _owner.GetPreviousVisibleSlot(_lastRealizedSlot);
                }
                
                if (_lastRealizedSlot < _firstRealizedSlot)
                {
                    ResetState();
                }
            }
        }

        /// <summary>
        /// Gets the realized element at the specified slot.
        /// </summary>
        /// <param name="slot">The slot index.</param>
        /// <returns>The element at the slot.</returns>
        public Control GetRealizedElement(int slot)
        {
            Debug.Assert(slot >= _firstRealizedSlot);
            Debug.Assert(slot <= _lastRealizedSlot);
            
            return _realizedElements[GetCircularIndex(slot, wrap: true)];
        }

        /// <summary>
        /// Gets the realized row at the specified row index.
        /// </summary>
        /// <param name="rowIndex">The row index.</param>
        /// <returns>The row at the index, or null if not a row.</returns>
        public DataGridRow? GetRealizedRow(int rowIndex)
        {
            return GetRealizedElement(_owner.SlotFromRowIndex(rowIndex)) as DataGridRow;
        }

        /// <summary>
        /// Enumerates all realized elements in display order.
        /// </summary>
        /// <returns>An enumeration of realized elements.</returns>
        public IEnumerable<Control> GetRealizedElements()
        {
            return GetRealizedElements(filter: null);
        }

        /// <summary>
        /// Enumerates realized elements matching the filter in display order.
        /// </summary>
        /// <param name="filter">Optional filter predicate.</param>
        /// <returns>An enumeration of matching realized elements.</returns>
        public IEnumerable<Control> GetRealizedElements(Predicate<object>? filter)
        {
            for (int i = 0; i < _realizedElements.Count; i++)
            {
                var element = _realizedElements[(_headElementIndex + i) % _realizedElements.Count];
                if (filter == null || filter(element))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Enumerates all realized rows in display order.
        /// </summary>
        /// <returns>An enumeration of realized rows.</returns>
        public IEnumerable<Control> GetRealizedRows()
        {
            return GetRealizedElements(element => element is DataGridRow);
        }

        /// <summary>
        /// Clears all realized elements, optionally recycling them.
        /// </summary>
        /// <param name="recycle">Whether to add elements to recycling pools.</param>
        public void ClearRealizedElements(bool recycle)
        {
            ResetState();
            
            if (recycle)
            {
                foreach (var element in _realizedElements)
                {
                    if (element is DataGridRow row)
                    {
                        if (row.IsRecyclable)
                        {
                            AddRecyclableRow(row);
                        }
                        else
                        {
                            row.Clip = new RectangleGeometry();
                        }
                    }
                    else if (element is DataGridRowGroupHeader groupHeader)
                    {
                        AddRecyclableGroupHeader(groupHeader);
                    }
                }
            }
            else
            {
                ClearRecycledRows();
                ClearRecycledGroupHeaders();
            }
            
            _realizedElements.Clear();
        }

        /// <summary>
        /// Fully recycles all recyclable elements.
        /// </summary>
        public void FullyRecycleAllElements()
        {
            FullyRecycleRows();
            FullyRecycleGroupHeaders();
        }

        #endregion

        #region Slot Correction

        /// <summary>
        /// Corrects slot indexes after a deletion.
        /// </summary>
        /// <param name="slot">The deleted slot.</param>
        /// <param name="wasCollapsed">Whether the slot was collapsed.</param>
        public void CorrectSlotsAfterDeletion(int slot, bool wasCollapsed)
        {
            if (wasCollapsed)
            {
                if (slot > _firstRealizedSlot)
                {
                    _lastRealizedSlot--;
                }
            }
            else if (_owner.IsSlotVisible(slot))
            {
                UnrealizeElement(slot, updateSlotInformation: true, wasDeleted: true);
            }
            
            if (slot < _firstRealizedSlot)
            {
                _firstRealizedSlot--;
                _lastRealizedSlot--;
            }
        }

        /// <summary>
        /// Corrects slot indexes after an insertion.
        /// </summary>
        /// <param name="slot">The inserted slot.</param>
        /// <param name="element">The inserted element.</param>
        /// <param name="isCollapsed">Whether the slot is collapsed.</param>
        public void CorrectSlotsAfterInsertion(int slot, Control element, bool isCollapsed)
        {
            if (slot < _firstRealizedSlot)
            {
                _firstRealizedSlot++;
                _lastRealizedSlot++;
            }
            else if (isCollapsed && slot <= _lastRealizedSlot)
            {
                _lastRealizedSlot++;
            }
            else if (_owner.GetPreviousVisibleSlot(slot) <= _lastRealizedSlot || _lastRealizedSlot == -1)
            {
                RealizeElement(slot, element, updateSlotInformation: true);
            }
        }

        #endregion

        #region Size Estimation

        /// <summary>
        /// Calculates the total estimated extent (size of all content).
        /// </summary>
        /// <param name="visibleSlotCount">The number of visible slots.</param>
        /// <param name="detailsCount">The number of visible details sections.</param>
        /// <returns>The estimated total height.</returns>
        public double CalculateEstimatedExtent(int visibleSlotCount, int detailsCount)
        {
            double totalHeight = visibleSlotCount * _estimatedRowHeight;
            totalHeight += detailsCount * _estimatedRowDetailsHeight;
            return totalHeight;
        }

        /// <summary>
        /// Invalidates the size estimates, requiring recalculation.
        /// </summary>
        public void InvalidateSizeEstimates()
        {
            _lastMeasuredRowIndex = -1;
        }

        #endregion

        #region Private Helpers

        private void ResetState()
        {
            _firstRealizedSlot = -1;
            _lastRealizedSlot = -1;
            TotallyDisplayedElementCount = 0;
            _headElementIndex = 0;
        }

        private void SetRealizedSlotRange(int first, int last)
        {
            _firstRealizedSlot = first;
            _lastRealizedSlot = last;
        }

        private int GetCircularIndex(int slot, bool wrap)
        {
            int index = slot - _firstRealizedSlot - _headElementIndex - 
                        _owner.GetCollapsedSlotCount(_firstRealizedSlot, slot);
            return wrap ? index % _realizedElements.Count : index;
        }

        #endregion

        #region Debug

#if DEBUG
        /// <summary>
        /// Prints debug information about realized elements.
        /// </summary>
        public void PrintRealizedElements()
        {
            Debug.WriteLine($"Virtualization State: First={_firstRealizedSlot}, Last={_lastRealizedSlot}, Count={_realizedElements.Count}");
            Debug.WriteLine($"Recycling: Rows={_recyclableRows.Count}/{_recycledRows.Count}, Headers={_recyclableGroupHeaders.Count}/{_recycledGroupHeaders.Count}");
            
            foreach (var element in GetRealizedElements())
            {
                if (element is DataGridRow row)
                {
                    Debug.WriteLine($"  Slot: {row.Slot} Row: {row.Index}");
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    Debug.WriteLine($"  Slot: {groupHeader.RowGroupInfo.Slot} GroupHeader: {groupHeader.RowGroupInfo.CollectionViewGroup.Key}");
                }
            }
        }
#endif

        #endregion
    }
}
