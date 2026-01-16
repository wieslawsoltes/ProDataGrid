// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Utilities;

namespace Avalonia.Controls
{
    /// <summary>
    /// Hierarchical helpers for DataGrid.
    /// </summary>
    partial class DataGrid
    {
        internal bool TryToggleHierarchicalAtSlot(int slot, bool toggleSubtree = false)
        {
            if (!_hierarchicalRowsEnabled || _hierarchicalAdapter == null)
            {
                return false;
            }

            PrepareHierarchicalAnchor(slot);

            if (TryHandleGroupSlotAsNode(slot, GroupSlotAction.Toggle, toggleSubtree))
            {
                return true;
            }

            if (!TryGetHierarchicalIndexFromSlot(slot, out var hierarchicalIndex))
            {
                return false;
            }

            if (toggleSubtree)
            {
                var node = _hierarchicalAdapter.NodeAt(hierarchicalIndex);
                RunHierarchicalAction(() =>
                {
                    if (node.IsExpanded)
                    {
                        _hierarchicalAdapter.CollapseAll(node);
                    }
                    else
                    {
                        _hierarchicalAdapter.ExpandAll(node);
                    }
                });
                return true;
            }

            _hierarchicalAdapter.Toggle(hierarchicalIndex);
            return true;
        }

        private bool TryGetHierarchicalIndexFromSlot(int slot, out int hierarchicalIndex)
        {
            hierarchicalIndex = -1;

            if (_hierarchicalAdapter == null || slot < 0)
            {
                return false;
            }

            if (RowGroupHeadersTable != null && RowGroupHeadersTable.Contains(slot))
            {
                if (_hierarchicalModel?.Options.TreatGroupsAsNodes == true)
                {
                    var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                    var group = rowGroupInfo?.CollectionViewGroup;
                    if (group != null)
                    {
                        hierarchicalIndex = _hierarchicalAdapter.IndexOfItem(group);
                        return hierarchicalIndex >= 0;
                    }
                }

                return false;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= _hierarchicalAdapter.Count)
            {
                return false;
            }

            hierarchicalIndex = rowIndex;
            return true;
        }

        private bool TryHandleGroupSlotAsNode(int slot, GroupSlotAction action, bool subtree = false)
        {
            if (_hierarchicalModel?.Options.TreatGroupsAsNodes != true)
            {
                return false;
            }

            if (RowGroupHeadersTable == null || !RowGroupHeadersTable.Contains(slot))
            {
                return false;
            }

            var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
            if (rowGroupInfo?.CollectionViewGroup == null)
            {
                return false;
            }

            if (_hierarchicalModel?.Options.TreatGroupsAsNodes == true)
            {
                var group = rowGroupInfo.CollectionViewGroup;
                var index = _hierarchicalAdapter?.IndexOfItem(group) ?? -1;
                if (index >= 0)
                {
                    if (subtree)
                    {
                        RunHierarchicalAction(() =>
                        {
                            if (_hierarchicalAdapter.IsExpanded(index))
                            {
                                _hierarchicalAdapter.CollapseAll(_hierarchicalAdapter.NodeAt(index));
                            }
                            else
                            {
                                _hierarchicalAdapter.ExpandAll(_hierarchicalAdapter.NodeAt(index));
                            }
                        });
                    }
                    else
                    {
                        _hierarchicalAdapter.Toggle(index);
                    }

                    return true;
                }
            }

            switch (action)
            {
                case GroupSlotAction.Collapse:
                    if (!rowGroupInfo.IsVisible)
                    {
                        return false;
                    }

                    CollapseRowGroup(rowGroupInfo.CollectionViewGroup, collapseAllSubgroups: subtree);
                    break;
                case GroupSlotAction.Expand:
                    if (rowGroupInfo.IsVisible)
                    {
                        return false;
                    }

                    ExpandRowGroup(rowGroupInfo.CollectionViewGroup, expandAllSubgroups: subtree);
                    break;
                default:
                    if (rowGroupInfo.IsVisible)
                    {
                        CollapseRowGroup(rowGroupInfo.CollectionViewGroup, collapseAllSubgroups: subtree);
                    }
                    else
                    {
                        ExpandRowGroup(rowGroupInfo.CollectionViewGroup, expandAllSubgroups: subtree);
                    }
                    break;
            }

            return true;
        }

        private enum GroupSlotAction
        {
            Toggle,
            Expand,
            Collapse
        }

        private void RefreshHierarchicalIndentation()
        {
            if (!_hierarchicalRowsEnabled || DisplayData == null)
            {
                return;
            }

            var needsReset = false;
            var requestPointerOverRefresh = false;
            var slot = DisplayData.FirstScrollingSlot;
            while (slot >= 0 && slot <= DisplayData.LastScrollingSlot)
            {
                var element = DisplayData.GetDisplayedElement(slot);
                if (element is DataGridRow row)
                {
                    var stateChanged = false;
                    if (!row.IsVisible)
                    {
                        row.ClearValue(Visual.IsVisibleProperty);
                        stateChanged = true;
                    }
                    if (row.Slot != slot)
                    {
                        row.Slot = slot;
                        requestPointerOverRefresh = true;
                        stateChanged = true;
                    }

                    var dataItemChanged = false;
                    var rowIndex = RowIndexFromSlot(slot);

                    object dataItem = null;
                    if (DataConnection != null && rowIndex >= 0 && rowIndex < DataConnection.Count)
                    {
                        dataItem = DataConnection.GetDataItem(rowIndex);
                        if (!ReferenceEquals(row.DataContext, dataItem))
                        {
                            row.DataContext = dataItem;
                            dataItemChanged = true;
                            requestPointerOverRefresh = true;
                            stateChanged = true;
                        }

                        var isPlaceholder = ReferenceEquals(dataItem, DataGridCollectionView.NewItemPlaceholder);
                        if (row.IsPlaceholder != isPlaceholder)
                        {
                            row.IsPlaceholder = isPlaceholder;
                            stateChanged = true;
                        }
                    }
                    else if (rowIndex >= 0)
                    {
                        needsReset = true;
                    }
                    if (row.Index != rowIndex)
                    {
                        row.Index = rowIndex;
                        requestPointerOverRefresh = true;
                        stateChanged = true;
                    }
                    foreach (var cell in row.Cells)
                    {
                        if (cell is not DataGridCell dataGridCell)
                        {
                            continue;
                        }

                        if (dataItemChanged && dataGridCell.OwningColumn is DataGridHierarchicalColumn)
                        {
                            dataGridCell.Content = dataGridCell.OwningColumn.GenerateElementInternal(dataGridCell, row.DataContext);
                        }

                        if (dataGridCell.Content is DataGridHierarchicalPresenter presenter)
                        {
                            BindingOperations
                                .GetBindingExpressionBase(presenter, DataGridHierarchicalPresenter.LevelProperty)
                                ?.UpdateTarget();
                            BindingOperations
                                .GetBindingExpressionBase(presenter, DataGridHierarchicalPresenter.IsExpandedProperty)
                                ?.UpdateTarget();
                            BindingOperations
                                .GetBindingExpressionBase(presenter, DataGridHierarchicalPresenter.IsExpandableProperty)
                                ?.UpdateTarget();
                        }
                    }

                    if (stateChanged)
                    {
                        row.ApplyState();
                    }
                }

                slot = GetNextVisibleSlot(slot);
            }

            if (_lastPointerPosition != null || _mouseOverRowIndex != null)
            {
                RefreshPointerOverRowStates();
                if (requestPointerOverRefresh)
                {
                    RequestPointerOverRefresh();
                }
            }

            if (needsReset)
            {
                ResetDisplayedRows();
                var displayHeight = CellsEstimatedHeight;
                var firstSlot = DisplayData.FirstScrollingSlot;
                var lastVisibleSlot = LastVisibleSlot;
                if (firstSlot < 0 || firstSlot > lastVisibleSlot)
                {
                    firstSlot = FirstVisibleSlot;
                }
                if (firstSlot >= 0 && MathUtilities.GreaterThan(displayHeight, 0))
                {
                    UpdateDisplayedRows(firstSlot, displayHeight);
                }
            }
        }
    }
}
