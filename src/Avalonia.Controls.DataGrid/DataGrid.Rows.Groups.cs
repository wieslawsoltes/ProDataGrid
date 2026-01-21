// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Controls.Utils;
using Avalonia.Utilities;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Styling;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
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

        private void CollectionViewGroup_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.ItemsSourceChange);

            // If we receive this event when the number of GroupDescriptions is different than what we have already
            // accounted for, that means the ICollectionView is still in the process of updating its groups.  It will
            // send a reset notification when it's done, at which point we can update our visuals.

            if (_rowGroupHeightsByLevel != null &&
            DataConnection.CollectionView != null &&
            DataConnection.CollectionView.IsGrouping &&
            DataConnection.CollectionView.GroupingDepth == _rowGroupHeightsByLevel.Length)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                    CollectionViewGroup_CollectionChanged_Add(sender, e);
                    break;
                    case NotifyCollectionChangedAction.Remove:
                    CollectionViewGroup_CollectionChanged_Remove(sender, e);
                    break;
                }
            }
        }



        private void CollectionViewGroup_CollectionChanged_Add(object sender, NotifyCollectionChangedEventArgs e)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.ItemsSourceChange);

            if (e.NewItems != null && e.NewItems.Count > 0)
            {
                // We need to figure out the CollectionViewGroup that the sender belongs to.  We could cache
                // it by tagging the collections ahead of time, but I think the extra storage might not be worth
                // it since this lookup should be performant enough
                int insertSlot = -1;
                DataGridRowGroupInfo parentGroupInfo = GetParentGroupInfo(sender);
                DataGridCollectionViewGroup group = e.NewItems[0] as DataGridCollectionViewGroup;

                if (parentGroupInfo != null)
                {
                    if (group != null || parentGroupInfo.Level == -1)
                    {
                        insertSlot = parentGroupInfo.Slot + 1;
                        // For groups, we need to skip over subgroups to find the correct slot
                        DataGridRowGroupInfo groupInfo;
                        for (int i = 0; i < e.NewStartingIndex; i++)
                        {
                            do
                            {
                                insertSlot = RowGroupHeadersTable.GetNextIndex(insertSlot);
                                groupInfo = RowGroupHeadersTable.GetValueAt(insertSlot);
                            }
                            while (groupInfo != null && groupInfo.Level > parentGroupInfo.Level + 1);
                            if (groupInfo == null)
                            {
                                // We couldn't find the subchild so this should go at the end
                                insertSlot = SlotCount;
                            }
                        }

                    }
                    else
                    {
                        // For items the slot is a simple calculation
                        insertSlot = parentGroupInfo.Slot + e.NewStartingIndex + 1;
                    }
                }

                // This could not be found when new GroupDescriptions are added to the PagedCollectionView
                if (insertSlot != -1)
                {
                    bool isCollapsed = (parentGroupInfo != null) && (!parentGroupInfo.IsVisible || _collapsedSlotsTable.Contains(parentGroupInfo.Slot));
                    if (group != null)
                    {
                        if (group.Items != null)
                        {
                            group.Items.CollectionChanged += CollectionViewGroup_CollectionChanged;
                        }
                        var newGroupInfo = new DataGridRowGroupInfo(group, true, parentGroupInfo.Level + 1, insertSlot, insertSlot);
                        InsertElementAt(insertSlot,
                        rowIndex: -1,
                        item: null,
                        groupInfo: newGroupInfo,
                        isCollapsed: isCollapsed);
                        RowGroupHeadersTable.AddValue(insertSlot, newGroupInfo);
                    }
                    else
                    {
                        // Assume we're adding a new row
                        int rowIndex = DataConnection.IndexOf(e.NewItems[0]);
                        Debug.Assert(rowIndex != -1);
                        if (SlotCount == 0 && DataConnection.ShouldAutoGenerateColumns)
                        {
                            AutoGenerateColumnsPrivate();
                        }
                        InsertElementAt(insertSlot, rowIndex,
                        item: e.NewItems[0],
                        groupInfo: null,
                        isCollapsed: isCollapsed);
                    }

                    CorrectLastSubItemSlotsAfterInsertion(parentGroupInfo);
                    if (parentGroupInfo.LastSubItemSlot - parentGroupInfo.Slot == 1)
                    {
                        // We just added the first item to a RowGroup so the header should transition from Empty to either Expanded or Collapsed
                        EnsureAncestorsExpanderButtonChecked(parentGroupInfo);
                    }
                }
            }
        }



        private void CollectionViewGroup_CollectionChanged_Remove(object sender, NotifyCollectionChangedEventArgs e)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.ItemsSourceChange);

            Debug.Assert(e.OldItems.Count == 1);
            if (e.OldItems != null && e.OldItems.Count > 0)
            {
                if (e.OldItems[0] is DataGridCollectionViewGroup removedGroup)
                {
                    if (removedGroup.Items != null)
                    {
                        removedGroup.Items.CollectionChanged -= CollectionViewGroup_CollectionChanged;
                    }
                    DataGridRowGroupInfo groupInfo = RowGroupInfoFromCollectionViewGroup(removedGroup);
                    Debug.Assert(groupInfo != null);
                    if ((groupInfo.Level == _rowGroupHeightsByLevel.Length - 1) && (removedGroup.Items != null) && (removedGroup.Items.Count > 0))
                    {
                        Debug.Assert((groupInfo.LastSubItemSlot - groupInfo.Slot) == removedGroup.Items.Count);
                        // If we're removing a leaf Group then remove all of its items before removing the Group
                        for (int i = 0; i < removedGroup.Items.Count; i++)
                        {
                            RemoveElementAt(groupInfo.Slot + 1, item: removedGroup.Items[i], isRow: true);
                        }
                    }
                    RemoveElementAt(groupInfo.Slot, item: null, isRow: false);
                }
                else
                {
                    // A single item was removed from a leaf group
                    DataGridRowGroupInfo parentGroupInfo = GetParentGroupInfo(sender);
                    if (parentGroupInfo != null)
                    {
                        int slot;
                        if (parentGroupInfo.CollectionViewGroup == null && RowGroupHeadersTable.IndexCount > 0)
                        {
                            // In this case, we're removing from the root group.  If there are other groups, then this must
                            // be the new item row that doesn't belong to any group because if there are other groups then
                            // this item cannot be a child of the root group.
                            slot = SlotCount - 1;
                        }
                        else
                        {
                            slot = parentGroupInfo.Slot + e.OldStartingIndex + 1;
                        }
                        RemoveElementAt(slot, e.OldItems[0], isRow: true);
                    }
                }
            }
        }



        private void ClearRowGroupHeadersTable()
        {
            DetachRowGroupHandlers(resetTopLevelGroup: true);

            RowGroupHeadersTable.Clear();
            RowGroupFootersTable.Clear();
            // Unfortunately PagedCollectionView does not allow us to preserve expanded or collapsed states for RowGroups since
            // the CollectionViewGroups are recreated when a Reset happens.  This is true in both SL and WPF
            _collapsedSlotsTable.Clear();

            _rowGroupHeightsByLevel = null;
            RowGroupSublevelIndents = null;
        }

        private void DetachRowGroupHandlers(bool resetTopLevelGroup)
        {
            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                var groupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (groupInfo == null)
                {
                    continue;
                }

                var group = groupInfo.CollectionViewGroup;
                if (group?.Items != null)
                {
                    group.Items.CollectionChanged -= CollectionViewGroup_CollectionChanged;
                }

                if (group is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged -= CollectionViewGroup_PropertyChanged;
                }
            }

            if (_topLevelGroup != null)
            {
                // The PagedCollectionView reuses the top level group so we need to detach any existing or else we'll get duplicate handers here
                _topLevelGroup.CollectionChanged -= CollectionViewGroup_CollectionChanged;
                if (resetTopLevelGroup)
                {
                    _topLevelGroup = null;
                }
            }
        }



        private void CollectionViewGroup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ItemCount")
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupInfoFromCollectionViewGroup(sender as DataGridCollectionViewGroup);
                if (rowGroupInfo != null && IsSlotVisible(rowGroupInfo.Slot))
                {
                    if (DisplayData.GetDisplayedElement(rowGroupInfo.Slot) is DataGridRowGroupHeader rowGroupHeader)
                    {
                        rowGroupHeader.UpdateTitleElements();
                    }
                }
            }
        }


        private void CorrectLastSubItemSlotsAfterInsertion(DataGridRowGroupInfo subGroupInfo)
        {
            int subGroupSlot;
            int subGroupLevel;
            while (subGroupInfo != null)
            {
                subGroupLevel = subGroupInfo.Level;
                subGroupInfo.LastSubItemSlot++;

                while (subGroupInfo != null && subGroupInfo.Level >= subGroupLevel)
                {
                    subGroupSlot = RowGroupHeadersTable.GetPreviousIndex(subGroupInfo.Slot);
                    subGroupInfo = RowGroupHeadersTable.GetValueAt(subGroupSlot);
                }
            }
        }



        private int CountAndPopulateGroupHeaders(object group, int rootSlot, int level)
        {
            int treeCount = 1;

            if (group is DataGridCollectionViewGroup collectionViewGroup)
            {
                if (collectionViewGroup.Items != null && collectionViewGroup.Items.Count > 0)
                {
                    collectionViewGroup.Items.CollectionChanged += CollectionViewGroup_CollectionChanged;
                    if (collectionViewGroup.Items[0] is DataGridCollectionViewGroup)
                    {
                        foreach (object subGroup in collectionViewGroup.Items)
                        {
                            treeCount += CountAndPopulateGroupHeaders(subGroup, rootSlot + treeCount, level + 1);
                        }
                    }
                    else
                    {
                        // Optimization: don't walk to the bottom level nodes
                        treeCount += collectionViewGroup.Items.Count;
                    }
                }
                int footerSlot = -1;
                if (ShouldShowGroupSummaryFooters)
                {
                    footerSlot = rootSlot + treeCount;
                    treeCount++;
                }

                var groupInfo = new DataGridRowGroupInfo(collectionViewGroup, true, level, rootSlot, rootSlot + treeCount - 1);
                RowGroupHeadersTable.AddValue(rootSlot, groupInfo);
                if (footerSlot >= 0)
                {
                    RowGroupFootersTable.AddValue(footerSlot, groupInfo);
                }
            }
            return treeCount;
        }

        private bool ShouldShowGroupSummaryFooters =>
            ShowGroupSummary &&
            (GroupSummaryPosition == DataGridGroupSummaryPosition.Footer ||
             GroupSummaryPosition == DataGridGroupSummaryPosition.Both);



        private void EnsureAncestorsExpanderButtonChecked(DataGridRowGroupInfo parentGroupInfo)
        {
            if (IsSlotVisible(parentGroupInfo.Slot))
            {
                DataGridRowGroupHeader ancestorGroupHeader = DisplayData.GetDisplayedElement(parentGroupInfo.Slot) as DataGridRowGroupHeader;
                while (ancestorGroupHeader != null)
                {
                    ancestorGroupHeader.EnsureExpanderButtonIsChecked();
                    if (ancestorGroupHeader.Level > 0)
                    {
                        int slot = RowGroupHeadersTable.GetPreviousIndex(ancestorGroupHeader.RowGroupInfo.Slot);
                        if (IsSlotVisible(slot))
                        {
                            ancestorGroupHeader = DisplayData.GetDisplayedElement(slot) as DataGridRowGroupHeader;
                            continue;
                        }
                    }
                    break;
                }
            }
        }



        private void PopulateRowGroupHeadersTable()
        {
            if (DataConnection.CollectionView != null
            && DataConnection.CollectionView.CanGroup
            && DataConnection.CollectionView.Groups != null)
            {
                int totalSlots = 0;
                _topLevelGroup = (INotifyCollectionChanged)DataConnection.CollectionView.Groups;
                _topLevelGroup.CollectionChanged += CollectionViewGroup_CollectionChanged;
                foreach (object group in DataConnection.CollectionView.Groups)
                {
                    totalSlots += CountAndPopulateGroupHeaders(group, totalSlots, 0);
                }
                SyncRowGroupInfoSlots();
            }
            SlotCount = DataConnection.Count + RowGroupHeadersTable.IndexCount + RowGroupFootersTable.IndexCount;
            VisibleSlotCount = SlotCount;
            RefreshRowGroupHeaders();
        }

        private void SyncRowGroupInfoSlots()
        {
            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                var info = RowGroupHeadersTable.GetValueAt(slot);
                if (info.Slot != slot)
                {
                    info.Slot = slot;
                }
            }
        }



        private void RefreshRowGroupHeaders()
        {
            if (DataConnection.CollectionView != null
            && DataConnection.CollectionView.CanGroup
            && DataConnection.CollectionView.Groups != null
            && DataConnection.CollectionView.IsGrouping
            && DataConnection.CollectionView.GroupingDepth > 0)
            {
                // Initialize our array for the height of the RowGroupHeaders by Level.
                // If the Length is the same, we can reuse the old array
                int groupLevelCount = DataConnection.CollectionView.GroupingDepth;
                if (_rowGroupHeightsByLevel == null || _rowGroupHeightsByLevel.Length != groupLevelCount)
                {
                    _rowGroupHeightsByLevel = new double[groupLevelCount];
                    for (int i = 0; i < groupLevelCount; i++)
                    {
                        // Default height for now, the actual heights are updated as the RowGroupHeaders
                        // are added and measured
                        _rowGroupHeightsByLevel[i] = DATAGRID_defaultRowHeight;
                    }
                }
                if (RowGroupSublevelIndents == null || RowGroupSublevelIndents.Length != groupLevelCount)
                {
                    RowGroupSublevelIndents = new double[groupLevelCount];
                    double indent;
                    for (int i = 0; i < groupLevelCount; i++)
                    {
                        indent = DATAGRID_defaultRowGroupSublevelIndent;
                        RowGroupSublevelIndents[i] = indent;
                        if (i > 0)
                        {
                            RowGroupSublevelIndents[i] += RowGroupSublevelIndents[i - 1];
                        }
                    }
                }
                EnsureRowGroupSpacerColumnWidth(groupLevelCount);
                UpdateGroupingIndentation();
            }
        }



        private void EnsureRowGroupSpacerColumn()
        {
            bool spacerColumnChanged = ColumnsInternal.EnsureRowGrouping(!RowGroupHeadersTable.IsEmpty);
            if (spacerColumnChanged)
            {
                if (ColumnsInternal.RowGroupSpacerColumn.IsRepresented && CurrentColumnIndex == 0)
                {
                    CurrentColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                }

                ProcessFrozenColumnCount();
            }
        }



        private void EnsureRowGroupSpacerColumnWidth(int groupLevelCount)
        {
            if (groupLevelCount == 0)
            {
                ColumnsInternal.RowGroupSpacerColumn.Width = new DataGridLength(0);
            }
            else
            {
                ColumnsInternal.RowGroupSpacerColumn.Width = new DataGridLength(RowGroupSublevelIndents[groupLevelCount - 1]);
            }
        }



        private void EnsureRowGroupVisibility(DataGridRowGroupInfo rowGroupInfo, bool isVisible, bool setCurrent)
        {
            if (rowGroupInfo == null)
            {
                return;
            }
            if (rowGroupInfo.IsVisible != isVisible)
            {
                if (IsSlotVisible(rowGroupInfo.Slot))
                {
                    DataGridRowGroupHeader rowGroupHeader = DisplayData.GetDisplayedElement(rowGroupInfo.Slot) as DataGridRowGroupHeader;
                    Debug.Assert(rowGroupHeader != null);
                    rowGroupHeader.ToggleExpandCollapse(isVisible, setCurrent);
                }
                else
                {
                    if (_collapsedSlotsTable.Contains(rowGroupInfo.Slot))
                    {
                        // Somewhere up the parent chain, there's a collapsed header so all the slots remain the same and
                        // we just need to mark this header with the new visibility
                        rowGroupInfo.IsVisible = isVisible;
                    }
                    else
                    {
                        if (rowGroupInfo.Slot < DisplayData.FirstScrollingSlot)
                        {
                            double heightChange = UpdateRowGroupVisibility(rowGroupInfo, isVisible, isDisplayed: false);
                            // Use epsilon instead of 0 here so that in the off chance that our estimates put the vertical offset negative
                            // the user can still scroll to the top since the offset is non-zero
                            SetVerticalOffset(Math.Max(MathUtilities.DoubleEpsilon, _verticalOffset + heightChange));
                        }
                        else
                        {
                            UpdateRowGroupVisibility(rowGroupInfo, isVisible, isDisplayed: false);
                        }
                        UpdateVerticalScrollBar();
                    }
                }
            }
        }


        private int GetRowGroupHeaderCount(int startSlot, int endSlot, bool? isVisible, out double headersHeight)
        {
            int count = 0;
            headersHeight = 0;
            count += AccumulateGroupSlotCount(RowGroupHeadersTable, startSlot, endSlot, isVisible, ref headersHeight);
            count += AccumulateGroupSlotCount(RowGroupFootersTable, startSlot, endSlot, isVisible, ref headersHeight);
            return count;
        }

        private int AccumulateGroupSlotCount(IndexToValueTable<DataGridRowGroupInfo> table, int startSlot, int endSlot, bool? isVisible, ref double headersHeight)
        {
            int count = 0;
            foreach (int slot in table.GetIndexes(startSlot))
            {
                if (slot > endSlot)
                {
                    return count;
                }

                DataGridRowGroupInfo rowGroupInfo = table.GetValueAt(slot);
                if (!isVisible.HasValue ||
                    (isVisible.Value && !_collapsedSlotsTable.Contains(slot)) ||
                    (!isVisible.Value && _collapsedSlotsTable.Contains(slot)))
                {
                    count++;
                    headersHeight += _rowGroupHeightsByLevel[rowGroupInfo.Level];
                }
            }

            return count;
        }


        private double UpdateRowGroupVisibility(DataGridRowGroupInfo targetRowGroupInfo, bool newIsVisible, bool isDisplayed)
        {
            double heightChange = 0;
            int slotsExpanded = 0;
            int startSlot = targetRowGroupInfo.Slot + 1;
            int endSlot;

            targetRowGroupInfo.IsVisible = newIsVisible;
            if (newIsVisible)
            {
                // Expand
                foreach (int slot in RowGroupHeadersTable.GetIndexes(targetRowGroupInfo.Slot + 1))
                {
                    if (slot >= startSlot)
                    {
                        DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                        if (rowGroupInfo.Level <= targetRowGroupInfo.Level)
                        {
                            break;
                        }
                        if (!rowGroupInfo.IsVisible)
                        {
                            // Skip over the items in collapsed subgroups
                            endSlot = rowGroupInfo.Slot;
                            ExpandSlots(startSlot, endSlot, isDisplayed, ref slotsExpanded, ref heightChange);
                            startSlot = rowGroupInfo.LastSubItemSlot + 1;
                        }
                    }
                }
                if (targetRowGroupInfo.LastSubItemSlot >= startSlot)
                {
                    ExpandSlots(startSlot, targetRowGroupInfo.LastSubItemSlot, isDisplayed, ref slotsExpanded, ref heightChange);
                }
                if (isDisplayed)
                {
                    UpdateDisplayedRows(DisplayData.FirstScrollingSlot, CellsEstimatedHeight);
                }
            }
            else
            {
                // Collapse
                endSlot = SlotCount - 1;
                foreach (int slot in RowGroupHeadersTable.GetIndexes(targetRowGroupInfo.Slot + 1))
                {
                    DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                    if (rowGroupInfo.Level <= targetRowGroupInfo.Level)
                    {
                        endSlot = slot - 1;
                        break;
                    }
                }

                int oldLastDisplayedSlot = DisplayData.LastScrollingSlot;
                int endDisplayedSlot = Math.Min(endSlot, DisplayData.LastScrollingSlot);
                if (isDisplayed)
                {
                    // We need to remove all the displayed slots that aren't already collapsed
                    int elementsToRemove = endDisplayedSlot - startSlot + 1 - _collapsedSlotsTable.GetIndexCount(startSlot, endDisplayedSlot);

                    if (_focusedRow != null && _focusedRow.Slot >= startSlot && _focusedRow.Slot <= endSlot)
                    {
                        Debug.Assert(EditingRow == null);
                        // Don't call ResetFocusedRow here because we're already cleaning it up below, and we don't want to FullyRecycle yet
                        _focusedRow = null;
                    }

                    for (int i = 0; i < elementsToRemove; i++)
                    {
                        RemoveDisplayedElement(startSlot, wasDeleted: false , updateSlotInformation: false);
                    }
                }

                double heightChangeBelowLastDisplayedSlot = 0;
                if (DisplayData.FirstScrollingSlot >= startSlot && DisplayData.FirstScrollingSlot <= endSlot)
                {
                    int newFirstScrollingSlot = -1;
                    if (isDisplayed)
                    {
                        // Our first visible slot was collapsed, find the replacement
                        int collapsedSlotsAbove = DisplayData.FirstScrollingSlot - startSlot - _collapsedSlotsTable.GetIndexCount(startSlot, DisplayData.FirstScrollingSlot);
                        Debug.Assert(collapsedSlotsAbove >= 0);
                        newFirstScrollingSlot = GetNextVisibleSlot(DisplayData.FirstScrollingSlot);
                        while (collapsedSlotsAbove > 1 && newFirstScrollingSlot < SlotCount)
                        {
                            collapsedSlotsAbove--;
                            newFirstScrollingSlot = GetNextVisibleSlot(newFirstScrollingSlot);
                        }
                    }
                    heightChange += CollapseSlotsInTable(startSlot, endSlot, ref slotsExpanded, oldLastDisplayedSlot, ref heightChangeBelowLastDisplayedSlot);
                    if (isDisplayed)
                    {
                        if (newFirstScrollingSlot >= SlotCount)
                        {
                            // No visible slots below, look up
                            UpdateDisplayedRowsFromBottom(targetRowGroupInfo.Slot);
                        }
                        else
                        {
                            UpdateDisplayedRows(newFirstScrollingSlot, CellsEstimatedHeight);
                        }
                    }
                }
                else
                {
                    heightChange += CollapseSlotsInTable(startSlot, endSlot, ref slotsExpanded, oldLastDisplayedSlot, ref heightChangeBelowLastDisplayedSlot);
                }

                if (DisplayData.LastScrollingSlot >= startSlot && DisplayData.LastScrollingSlot <= endSlot)
                {
                    // Collapsed the last scrolling row, we need to update it
                    DisplayData.LastScrollingSlot = GetPreviousVisibleSlot(DisplayData.LastScrollingSlot);
                }

                // Collapsing could cause the vertical offset to move up if we collapsed a lot of slots
                // near the bottom of the DataGrid.  To do this, we compare the height we collapsed to
                // the distance to the last visible row and adjust the scrollbar if we collapsed more
                if (isDisplayed && _verticalOffset > 0)
                {
                    int lastVisibleSlot = GetPreviousVisibleSlot(SlotCount);
                    int slot = GetNextVisibleSlot(oldLastDisplayedSlot);
                    // AvailableSlotElementRoom ends up being the amount of the last slot that is partially scrolled off
                    // as a negative value, heightChangeBelowLastDisplayed slot is also a negative value since we're collapsing
                    double heightToLastVisibleSlot = AvailableSlotElementRoom + heightChangeBelowLastDisplayedSlot;
                    while ((heightToLastVisibleSlot > heightChange) && (slot < lastVisibleSlot))
                    {
                        heightToLastVisibleSlot -= GetSlotElementHeight(slot);
                        slot = GetNextVisibleSlot(slot);
                    }
                    if (heightToLastVisibleSlot > heightChange)
                    {
                        double newVerticalOffset = _verticalOffset + heightChange - heightToLastVisibleSlot;
                        if (newVerticalOffset > 0)
                        {
                            SetVerticalOffset(newVerticalOffset);
                        }
                        else
                        {
                            // Collapsing causes the vertical offset to go to 0 so we should go back to the first row.
                            ResetDisplayedRows();
                            NegVerticalOffset = 0;
                            SetVerticalOffset(0);
                            int firstDisplayedRow = GetNextVisibleSlot(-1);
                            UpdateDisplayedRows(firstDisplayedRow, CellsEstimatedHeight);
                        }
                    }
                }
            }

            // Update VisibleSlotCount
            VisibleSlotCount += slotsExpanded;

            return heightChange;
        }



        private DataGridRowGroupHeader GenerateRowGroupHeader(int slot, DataGridRowGroupInfo rowGroupInfo)
        {
            Debug.Assert(slot > -1);
            Debug.Assert(rowGroupInfo != null);

            DataGridRowGroupHeader groupHeader = DisplayData.GetRecycledGroupHeader() ?? new DataGridRowGroupHeader();
            groupHeader.OwningGrid = this;
            groupHeader.RowGroupInfo = rowGroupInfo;
            groupHeader.DataContext = rowGroupInfo.CollectionViewGroup;
            groupHeader.Level = rowGroupInfo.Level;
            if (RowGroupTheme is {} rowGroupTheme)
            {
                groupHeader.SetValue(ThemeProperty, rowGroupTheme, BindingPriority.Template);
            }

            // Set the RowGroupHeader's PropertyName. Unfortunately, CollectionViewGroup doesn't have this
            // so we have to set it manually
            Debug.Assert(DataConnection.CollectionView != null && groupHeader.Level < DataConnection.CollectionView.GroupingDepth);
            string propertyName = DataConnection.CollectionView.GetGroupingPropertyNameAtDepth(groupHeader.Level);

            if(string.IsNullOrWhiteSpace(propertyName))
            {
                groupHeader.PropertyName = null;
            }
            else
            {
                groupHeader.PropertyName = DataConnection.DataType?.GetDisplayName(propertyName) ?? propertyName;
            }

            if (rowGroupInfo.CollectionViewGroup is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= new PropertyChangedEventHandler(CollectionViewGroup_PropertyChanged);
                inpc.PropertyChanged += new PropertyChangedEventHandler(CollectionViewGroup_PropertyChanged);
            }
            groupHeader.UpdateTitleElements();

            OnLoadingRowGroup(new DataGridRowGroupHeaderEventArgs(groupHeader));

            return groupHeader;
        }

        private DataGridRowGroupFooter GenerateRowGroupFooter(int slot, DataGridRowGroupInfo rowGroupInfo)
        {
            Debug.Assert(slot > -1);
            Debug.Assert(rowGroupInfo != null);

            DataGridRowGroupFooter groupFooter = DisplayData.GetRecycledGroupFooter() ?? new DataGridRowGroupFooter();
            groupFooter.OwningGrid = this;
            groupFooter.RowGroupInfo = rowGroupInfo;
            groupFooter.Group = rowGroupInfo.CollectionViewGroup;
            groupFooter.Level = rowGroupInfo.Level;
            groupFooter.ApplySummaryRowTheme();
            groupFooter.UpdateSummaryRowOffset();

            return groupFooter;
        }



        private DataGridRowGroupInfo GetParentGroupInfo(object collection)
        {
            if (collection == DataConnection.CollectionView.Groups)
            {
                // If the new item is a root level element, it has no parent group, so create an empty RowGroupInfo
                return new DataGridRowGroupInfo(null, true, -1, -1, -1);
            }
            else
            {
                foreach (int slot in RowGroupHeadersTable.GetIndexes())
                {
                    DataGridRowGroupInfo groupInfo = RowGroupHeadersTable.GetValueAt(slot);
                    if (groupInfo.CollectionViewGroup.Items == collection)
                    {
                        return groupInfo;
                    }
                }
            }
            return null;
        }


        internal void OnRowGroupHeaderToggled(DataGridRowGroupHeader groupHeader, bool newIsVisible, bool setCurrent)
        {
            var collectionViewGroup = groupHeader.DataContext as DataGridCollectionViewGroup ??
                                      groupHeader.RowGroupInfo?.CollectionViewGroup;
            if (collectionViewGroup == null || collectionViewGroup.ItemCount == 0)
            {
                return;
            }

            // RowGroupInfo on the recycled header can become stale if slots shifted; always re-sync with the table
            var currentInfo = RowGroupInfoFromCollectionViewGroup(collectionViewGroup) ?? groupHeader.RowGroupInfo;
            if (currentInfo == null)
            {
                return;
            }

            SyncRowGroupHeaderInfo(groupHeader, currentInfo);

            Debug.Assert(groupHeader.RowGroupInfo.CollectionViewGroup.ItemCount > 0);

            if (WaitForLostFocus(delegate { OnRowGroupHeaderToggled(groupHeader, newIsVisible, setCurrent); }) || !CommitEdit())
            {
                return;
            }

            if (setCurrent && CurrentSlot != groupHeader.RowGroupInfo.Slot)
            {
                // Most of the time this is set by the MouseLeftButtonDown handler but validation could cause that code path to fail
                UpdateSelectionAndCurrency(CurrentColumnIndex, groupHeader.RowGroupInfo.Slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
            }

            UpdateRowGroupVisibility(groupHeader.RowGroupInfo, newIsVisible, isDisplayed: true);

            ComputeScrollBarsLayout();
            // We need force arrange since our Scrollings Rows could update without automatically triggering layout
            InvalidateRowsArrange();
        }

        internal void SyncRowGroupHeaderInfo(DataGridRowGroupHeader groupHeader, DataGridRowGroupInfo rowGroupInfo)
        {
            var infoChanged = !ReferenceEquals(groupHeader.RowGroupInfo, rowGroupInfo);
            if (infoChanged)
            {
                if (groupHeader.RowGroupInfo?.CollectionViewGroup is INotifyPropertyChanged oldInpc)
                {
                    oldInpc.PropertyChanged -= new PropertyChangedEventHandler(CollectionViewGroup_PropertyChanged);
                }

                groupHeader.RowGroupInfo = rowGroupInfo;
                groupHeader.DataContext = rowGroupInfo.CollectionViewGroup;

                if (rowGroupInfo.CollectionViewGroup is INotifyPropertyChanged newInpc)
                {
                    newInpc.PropertyChanged -= new PropertyChangedEventHandler(CollectionViewGroup_PropertyChanged);
                    newInpc.PropertyChanged += new PropertyChangedEventHandler(CollectionViewGroup_PropertyChanged);
                }
            }

            var levelChanged = groupHeader.Level != rowGroupInfo.Level;
            if (levelChanged)
            {
                groupHeader.Level = rowGroupInfo.Level;
            }

            if (infoChanged || levelChanged)
            {
                UpdateRowGroupHeaderPropertyName(groupHeader, rowGroupInfo.Level);
                groupHeader.UpdateTitleElements();
                groupHeader.EnsureExpanderButtonIsChecked();
                groupHeader.UpdateSummaryRowState();
                groupHeader.UpdatePseudoClasses();
                groupHeader.ApplyHeaderStatus();
                ApplyRowGroupHeaderIndent(groupHeader);
            }
        }

        internal void ApplyRowGroupHeaderIndent(DataGridRowGroupHeader groupHeader)
        {
            if (groupHeader == null)
            {
                return;
            }

            if (groupHeader.Level <= 0)
            {
                groupHeader.TotalIndent = 0;
                return;
            }

            if (RowGroupSublevelIndents == null || RowGroupSublevelIndents.Length == 0)
            {
                groupHeader.TotalIndent = DATAGRID_defaultRowGroupSublevelIndent * groupHeader.Level;
                return;
            }

            var index = Math.Min(groupHeader.Level - 1, RowGroupSublevelIndents.Length - 1);
            groupHeader.TotalIndent = RowGroupSublevelIndents[index];
        }

        private void UpdateRowGroupHeaderPropertyName(DataGridRowGroupHeader groupHeader, int level)
        {
            if (DataConnection?.CollectionView == null ||
                level < 0 ||
                level >= DataConnection.CollectionView.GroupingDepth)
            {
                groupHeader.PropertyName = null;
                return;
            }

            string propertyName = DataConnection.CollectionView.GetGroupingPropertyNameAtDepth(level);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                groupHeader.PropertyName = null;
            }
            else
            {
                groupHeader.PropertyName = DataConnection.DataType?.GetDisplayName(propertyName) ?? propertyName;
            }
        }



        internal void OnSublevelIndentUpdated(DataGridRowGroupHeader groupHeader, double newValue)
        {
            Debug.Assert(DataConnection.CollectionView != null);
            Debug.Assert(RowGroupSublevelIndents != null);

            int groupLevelCount = DataConnection.CollectionView.GroupingDepth;
            Debug.Assert(groupHeader.Level >= 0 && groupHeader.Level < groupLevelCount);

            double oldValue = RowGroupSublevelIndents[groupHeader.Level];
            if (groupHeader.Level > 0)
            {
                oldValue -= RowGroupSublevelIndents[groupHeader.Level - 1];
            }
            // Update the affected values in our table by the amount affected
            double change = newValue - oldValue;
            for (int i = groupHeader.Level; i < groupLevelCount; i++)
            {
                RowGroupSublevelIndents[i] += change;
                Debug.Assert(RowGroupSublevelIndents[i] >= 0);
            }

            EnsureRowGroupSpacerColumnWidth(groupLevelCount);
            UpdateGroupingIndentation();
        }



        internal DataGridRowGroupInfo RowGroupInfoFromCollectionViewGroup(DataGridCollectionViewGroup collectionViewGroup)
        {
            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                DataGridRowGroupInfo rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo.CollectionViewGroup == collectionViewGroup)
                {
                    return rowGroupInfo;
                }
            }
            return null;
        }

        /// <summary>
        /// Expands all DataGridRowGroupHeader instances in the DataGrid.
        /// </summary>
        public void ExpandAllGroups()
        {
            if (WaitForLostFocus(delegate { ExpandAllGroups(); }) || !CommitEdit())
            {
                return;
            }

            if (RowGroupHeadersTable == null || RowGroupHeadersTable.IsEmpty)
            {
                return;
            }

            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo != null)
                {
                    EnsureRowGroupVisibility(rowGroupInfo, true, setCurrent: false);
                }
            }

            ComputeScrollBarsLayout();
            InvalidateRowsArrange();
        }


        /// <summary>
        /// Collapses all DataGridRowGroupHeader instances in the DataGrid.
        /// </summary>
        public void CollapseAllGroups()
        {
            if (WaitForLostFocus(delegate { CollapseAllGroups(); }) || !CommitEdit())
            {
                return;
            }

            if (RowGroupHeadersTable == null || RowGroupHeadersTable.IsEmpty)
            {
                return;
            }

            foreach (int slot in RowGroupHeadersTable.GetIndexes())
            {
                var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                if (rowGroupInfo != null)
                {
                    EnsureRowGroupVisibility(rowGroupInfo, false, setCurrent: false);
                }
            }

            ComputeScrollBarsLayout();
            InvalidateRowsArrange();
        }


        /// <summary>
        /// Collapses the DataGridRowGroupHeader that represents a given CollectionViewGroup
        /// </summary>
        /// <param name="collectionViewGroup">CollectionViewGroup</param>
        /// <param name="collapseAllSubgroups">Set to true to collapse all Subgroups</param>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void CollapseRowGroup(DataGridCollectionViewGroup collectionViewGroup, bool collapseAllSubgroups)
        {
            if (WaitForLostFocus(delegate { CollapseRowGroup(collectionViewGroup, collapseAllSubgroups); }) ||
            collectionViewGroup == null || !CommitEdit())
            {
                return;
            }

            EnsureRowGroupVisibility(RowGroupInfoFromCollectionViewGroup(collectionViewGroup), false, true);

            if (collapseAllSubgroups)
            {
                foreach (object groupObj in collectionViewGroup.Items)
                {
                    if (groupObj is DataGridCollectionViewGroup subGroup)
                    {
                        CollapseRowGroup(subGroup, collapseAllSubgroups);
                    }
                }
            }
        }



        /// <summary>
        /// Expands the DataGridRowGroupHeader that represents a given CollectionViewGroup
        /// </summary>
        /// <param name="collectionViewGroup">CollectionViewGroup</param>
        /// <param name="expandAllSubgroups">Set to true to expand all Subgroups</param>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void ExpandRowGroup(DataGridCollectionViewGroup collectionViewGroup, bool expandAllSubgroups)
        {
            if (WaitForLostFocus(delegate { ExpandRowGroup(collectionViewGroup, expandAllSubgroups); }) ||
            collectionViewGroup == null || !CommitEdit())
            {
                return;
            }

            EnsureRowGroupVisibility(RowGroupInfoFromCollectionViewGroup(collectionViewGroup), true, true);

            if (expandAllSubgroups)
            {
                foreach (object groupObj in collectionViewGroup.Items)
                {
                    if (groupObj is DataGridCollectionViewGroup subGroup)
                    {
                        ExpandRowGroup(subGroup, expandAllSubgroups);
                    }
                }
            }
        }


    }
}
