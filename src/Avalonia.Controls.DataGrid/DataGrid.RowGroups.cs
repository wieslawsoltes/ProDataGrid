// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.Diagnostics;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
    /// <summary>
    /// Row grouping functionality
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="LoadingRowGroup"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowGroupHeaderEventArgs> LoadingRowGroupEvent =
            RoutedEvent.Register<DataGrid, DataGridRowGroupHeaderEventArgs>(nameof(LoadingRowGroup), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="UnloadingRowGroup"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowGroupHeaderEventArgs> UnloadingRowGroupEvent =
            RoutedEvent.Register<DataGrid, DataGridRowGroupHeaderEventArgs>(nameof(UnloadingRowGroup), RoutingStrategies.Bubble);

        /// <summary>
        /// Returns the Group at the indicated level or null if the item is not in the ItemsSource
        /// </summary>
        /// <param name="item">item</param>
        /// <param name="groupLevel">groupLevel</param>
        /// <returns>The group the given item falls under or null if the item is not in the ItemsSource</returns>
        public DataGridCollectionViewGroup GetGroupFromItem(object item, int groupLevel)
        {
            int itemIndex = DataConnection.IndexOf(item);
            if (itemIndex == -1)
            {
                return null;
            }
            int groupHeaderSlot = RowGroupHeadersTable.GetPreviousIndex(SlotFromRowIndex(itemIndex));
            DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(groupHeaderSlot);
            while (rowGroupInfo != null && rowGroupInfo.Level != groupLevel)
            {
                groupHeaderSlot = RowGroupHeadersTable.GetPreviousIndex(rowGroupInfo.Slot);
                rowGroupInfo = RowGroupHeadersTable.GetValueAt(groupHeaderSlot);
            }
            return rowGroupInfo?.CollectionViewGroup;
        }


        /// <summary>
        /// Raises the LoadingRowGroup event
        /// </summary>
        /// <param name="e">EventArgs</param>
        protected virtual void OnLoadingRowGroup(DataGridRowGroupHeaderEventArgs e)
        {
            LoadingOrUnloadingRow = true;
            e.RoutedEvent ??= LoadingRowGroupEvent;
            e.Source ??= this;
            RaiseEvent(e);
            LoadingOrUnloadingRow = false;
        }


        /// <summary>
        /// Raises the UnLoadingRowGroup event
        /// </summary>
        /// <param name="e">EventArgs</param>
        protected virtual void OnUnloadingRowGroup(DataGridRowGroupHeaderEventArgs e)
        {
            LoadingOrUnloadingRow = true;
            e.RoutedEvent ??= UnloadingRowGroupEvent;
            e.Source ??= this;
            RaiseEvent(e);
            LoadingOrUnloadingRow = false;
        }


        // Recursively expands parent RowGroupHeaders from the top down
        private void ExpandRowGroupParentChain(int level, int slot)
        {
            if (level < 0)
            {
                return;
            }
            int previousHeaderSlot = RowGroupHeadersTable.GetPreviousIndex(slot + 1);
            DataGridRowGroupInfo rowGroupInfo = null;
            while (previousHeaderSlot >= 0)
            {
                rowGroupInfo = RowGroupHeadersTable.GetValueAt(previousHeaderSlot);
                Debug.Assert(rowGroupInfo != null);
                if (level == rowGroupInfo.Level)
                {
                    if (_collapsedSlotsTable.Contains(rowGroupInfo.Slot))
                    {
                        // Keep going up the chain
                        ExpandRowGroupParentChain(level - 1, rowGroupInfo.Slot - 1);
                    }
                    if (!rowGroupInfo.IsVisible)
                    {
                        EnsureRowGroupVisibility(rowGroupInfo, true, false);
                    }
                    return;
                }
                else
                {
                    previousHeaderSlot = RowGroupHeadersTable.GetPreviousIndex(previousHeaderSlot);
                }
            }
        }


        /// <summary>
        /// Occurs before a DataGridRowGroupHeader header is used.
        /// </summary>
        public event EventHandler<DataGridRowGroupHeaderEventArgs> LoadingRowGroup
        {
            add => AddHandler(LoadingRowGroupEvent, value);
            remove => RemoveHandler(LoadingRowGroupEvent, value);
        }


        /// <summary>
        /// Occurs when the DataGridRowGroupHeader is available for reuse.
        /// </summary>
        public event EventHandler<DataGridRowGroupHeaderEventArgs> UnloadingRowGroup
        {
            add => AddHandler(UnloadingRowGroupEvent, value);
            remove => RemoveHandler(UnloadingRowGroupEvent, value);
        }

    }
}
