// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
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

        private void CorrectColumnDisplayIndexesAfterDeletion(DataGridColumn deletedColumn)
        {
            // Column indexes have already been adjusted.
            // This column has already been detached and has retained its old Index and DisplayIndex

            Debug.Assert(deletedColumn != null);
            Debug.Assert(deletedColumn.OwningGrid == null);
            Debug.Assert(deletedColumn.Index >= 0);
            Debug.Assert(deletedColumn.DisplayIndexWithFiller >= 0);

            try
            {
                InDisplayIndexAdjustments = true;

                // The DisplayIndex of columns greater than the deleted column need to be decremented,
                // as do the DisplayIndexMap values of modified column Indexes
                DataGridColumn column;
                ColumnsInternal.DisplayIndexMap.RemoveAt(deletedColumn.DisplayIndexWithFiller);
                for (int displayIndex = 0; displayIndex < ColumnsInternal.DisplayIndexMap.Count; displayIndex++)
                {
                    if (ColumnsInternal.DisplayIndexMap[displayIndex] > deletedColumn.Index)
                    {
                        ColumnsInternal.DisplayIndexMap[displayIndex]--;
                    }
                    if (displayIndex >= deletedColumn.DisplayIndexWithFiller)
                    {
                        column = ColumnsInternal.GetColumnAtDisplayIndex(displayIndex);
                        column.DisplayIndexWithFiller = column.DisplayIndexWithFiller - 1;
                        column.DisplayIndexHasChanged = true; // OnColumnDisplayIndexChanged needs to be raised later on
                    }
                }

                // Now raise all the OnColumnDisplayIndexChanged events
                FlushDisplayIndexChanged(true /*raiseEvent*/);
            }
            finally
            {
                InDisplayIndexAdjustments = false;
                FlushDisplayIndexChanged(false /*raiseEvent*/);
            }
        }



        private void CorrectColumnDisplayIndexesAfterInsertion(DataGridColumn insertedColumn)
        {
            Debug.Assert(insertedColumn != null);
            Debug.Assert(insertedColumn.OwningGrid == this);
            if (insertedColumn.DisplayIndexWithFiller == -1 || insertedColumn.DisplayIndexWithFiller >= ColumnsItemsInternal.Count)
            {
                // Developer did not assign a DisplayIndex or picked a large number.
                // Choose the Index as the DisplayIndex.
                insertedColumn.DisplayIndexWithFiller = insertedColumn.Index;
            }

            try
            {
                InDisplayIndexAdjustments = true;

                // The DisplayIndex of columns greater than the inserted column need to be incremented,
                // as do the DisplayIndexMap values of modified column Indexes
                DataGridColumn column;
                for (int displayIndex = 0; displayIndex < ColumnsInternal.DisplayIndexMap.Count; displayIndex++)
                {
                    if (ColumnsInternal.DisplayIndexMap[displayIndex] >= insertedColumn.Index)
                    {
                        ColumnsInternal.DisplayIndexMap[displayIndex]++;
                    }
                    if (displayIndex >= insertedColumn.DisplayIndexWithFiller)
                    {
                        column = ColumnsInternal.GetColumnAtDisplayIndex(displayIndex);
                        column.DisplayIndexWithFiller++;
                        column.DisplayIndexHasChanged = true; // OnColumnDisplayIndexChanged needs to be raised later on
                    }
                }
                ColumnsInternal.DisplayIndexMap.Insert(insertedColumn.DisplayIndexWithFiller, insertedColumn.Index);

                // Now raise all the OnColumnDisplayIndexChanged events
                FlushDisplayIndexChanged(true /*raiseEvent*/);
            }
            finally
            {
                InDisplayIndexAdjustments = false;
                FlushDisplayIndexChanged(false /*raiseEvent*/);
            }
        }



        private void CorrectColumnFrozenStates()
        {
            int index = 0;
            int totalColumns = ColumnsInternal.DisplayIndexMap.Count;
            int leftCount = FrozenColumnCountWithFiller;
            int rightCount = FrozenColumnCountRightEffective;
            int rightStartIndex = Math.Max(leftCount, totalColumns - rightCount);

            double oldLeftFrozenWidth = 0;
            double newLeftFrozenWidth = 0;

            foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns())
            {
                if (column.IsFrozenLeft)
                {
                    oldLeftFrozenWidth += column.ActualWidth;
                }

                DataGridFrozenColumnPosition frozenPosition;
                if (index < leftCount)
                {
                    frozenPosition = DataGridFrozenColumnPosition.Left;
                }
                else if (index >= rightStartIndex)
                {
                    frozenPosition = DataGridFrozenColumnPosition.Right;
                }
                else
                {
                    frozenPosition = DataGridFrozenColumnPosition.None;
                }

                if (frozenPosition == DataGridFrozenColumnPosition.Left)
                {
                    newLeftFrozenWidth += column.ActualWidth;
                }

                column.FrozenPosition = frozenPosition;
                index++;
            }

            if (HorizontalOffset > Math.Max(0, newLeftFrozenWidth - oldLeftFrozenWidth))
            {
                UpdateHorizontalOffset(HorizontalOffset - newLeftFrozenWidth + oldLeftFrozenWidth);
            }
            else
            {
                UpdateHorizontalOffset(0);
            }
        }



        private void CorrectColumnIndexesAfterDeletion(DataGridColumn deletedColumn)
        {
            Debug.Assert(deletedColumn != null);
            for (int columnIndex = deletedColumn.Index; columnIndex < ColumnsItemsInternal.Count; columnIndex++)
            {
                ColumnsItemsInternal[columnIndex].Index = ColumnsItemsInternal[columnIndex].Index - 1;
                Debug.Assert(ColumnsItemsInternal[columnIndex].Index == columnIndex);
            }
        }



        private void CorrectColumnIndexesAfterInsertion(DataGridColumn insertedColumn, int insertionCount)
        {
            Debug.Assert(insertedColumn != null);
            Debug.Assert(insertionCount > 0);
            for (int columnIndex = insertedColumn.Index + insertionCount; columnIndex < ColumnsItemsInternal.Count; columnIndex++)
            {
                ColumnsItemsInternal[columnIndex].Index = columnIndex;
            }
        }

        private void NormalizeColumnDisplayIndexesAfterDetachedMutations()
        {
            int columnCount = ColumnsItemsInternal.Count;
            if (columnCount == 0)
            {
                ColumnsInternal.DisplayIndexMap.Clear();
                return;
            }

            var previousDisplayOrder = BuildPreviousDisplayOrder(columnCount);
            ColumnsInternal.DisplayIndexMap.Clear();

            // Ensure indices match the current collection order before rebuilding the map.
            for (int index = 0; index < columnCount; index++)
            {
                ColumnsItemsInternal[index].Index = index;
            }

            var orderedColumns = new List<(DataGridColumn Column, int SortKey, int MoveDirection, int PreviousOrder, int CollectionOrder)>(columnCount);
            for (int order = 0; order < columnCount; order++)
            {
                DataGridColumn column = ColumnsItemsInternal[order];
                int previousOrder = previousDisplayOrder[column];
                int rawDisplayIndex = column.DisplayIndexWithFiller;
                int sortKey = rawDisplayIndex;
                int moveDirection = 0;
                if (sortKey < 0 || sortKey >= columnCount)
                {
                    // Detached columns can keep stale/out-of-range values after remove/add cycles.
                    // Fall back to prior display order for deterministic compaction.
                    sortKey = previousOrder;
                }
                else if (sortKey != previousOrder)
                {
                    // -1 => moved left, +1 => moved right.
                    moveDirection = sortKey < previousOrder ? -1 : 1;
                }

                orderedColumns.Add((column, sortKey, moveDirection, previousOrder, order));
            }

            orderedColumns.Sort((left, right) =>
            {
                int compare = left.SortKey.CompareTo(right.SortKey);
                if (compare != 0)
                {
                    return compare;
                }

                // For equal target indexes:
                // left movers should be inserted before stationary columns,
                // right movers should be inserted after stationary columns.
                compare = left.MoveDirection.CompareTo(right.MoveDirection);
                if (compare != 0)
                {
                    return compare;
                }

                compare = left.PreviousOrder.CompareTo(right.PreviousOrder);
                if (compare != 0)
                {
                    return compare;
                }

                return left.CollectionOrder.CompareTo(right.CollectionOrder);
            });

            for (int displayIndex = 0; displayIndex < orderedColumns.Count; displayIndex++)
            {
                DataGridColumn column = orderedColumns[displayIndex].Column;
                column.DisplayIndexWithFiller = displayIndex;
                column.DisplayIndexHasChanged = false;
                ColumnsInternal.DisplayIndexMap.Add(column.Index);
            }
        }

        private Dictionary<DataGridColumn, int> BuildPreviousDisplayOrder(int columnCount)
        {
            var displayOrder = new Dictionary<DataGridColumn, int>(columnCount);
            bool useExistingMap = ColumnsInternal.DisplayIndexMap.Count == columnCount;
            if (useExistingMap)
            {
                for (int displayIndex = 0; displayIndex < columnCount; displayIndex++)
                {
                    int columnIndex = ColumnsInternal.DisplayIndexMap[displayIndex];
                    if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
                    {
                        useExistingMap = false;
                        break;
                    }

                    DataGridColumn column = ColumnsItemsInternal[columnIndex];
                    if (!displayOrder.TryAdd(column, displayIndex))
                    {
                        useExistingMap = false;
                        break;
                    }
                }
            }

            if (!useExistingMap)
            {
                displayOrder.Clear();
                for (int index = 0; index < ColumnsItemsInternal.Count; index++)
                {
                    displayOrder[ColumnsItemsInternal[index]] = index;
                }
            }

            return displayOrder;
        }



        private void FlushDisplayIndexChanged(bool raiseEvent)
        {
            foreach (DataGridColumn column in ColumnsItemsInternal)
            {
                if (column.DisplayIndexHasChanged)
                {
                    column.DisplayIndexHasChanged = false;
                    if (raiseEvent)
                    {
                        Debug.Assert(column != ColumnsInternal.RowGroupSpacerColumn);
                        OnColumnDisplayIndexChanged(column);
                    }
                }
            }
        }


    }
}
