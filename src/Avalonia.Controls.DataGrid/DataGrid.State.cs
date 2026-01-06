// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Selection;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {
        private DataGridSearchCurrentState _pendingSearchCurrentState;
        private DataGridStateOptions _pendingSearchOptions;

        public DataGridState CaptureState(DataGridStateSections sections = DataGridStateSections.All, DataGridStateOptions options = null)
        {
            var state = new DataGridState { Sections = sections };

            if (HasSection(sections, DataGridStateSections.Columns))
            {
                state.Columns = CaptureColumnLayoutState(options);
            }

            if (HasSection(sections, DataGridStateSections.Sorting))
            {
                state.Sorting = CaptureSortingState(options);
            }

            if (HasSection(sections, DataGridStateSections.Filtering))
            {
                state.Filtering = CaptureFilteringState(options);
            }

            if (HasSection(sections, DataGridStateSections.Searching))
            {
                state.Search = CaptureSearchState(options);
            }

            if (HasSection(sections, DataGridStateSections.ConditionalFormatting))
            {
                state.ConditionalFormatting = CaptureConditionalFormattingState(options);
            }

            if (HasSection(sections, DataGridStateSections.Grouping))
            {
                state.Grouping = CaptureGroupingState(options);
            }

            if (HasSection(sections, DataGridStateSections.Hierarchical))
            {
                state.Hierarchical = CaptureHierarchicalState(options);
            }

            if (HasSection(sections, DataGridStateSections.Selection))
            {
                state.Selection = CaptureSelectionState(options);
            }

            if (HasSection(sections, DataGridStateSections.Scroll))
            {
                state.Scroll = CaptureScrollState(options);
            }

            return state;
        }

        public void RestoreState(DataGridState state, DataGridStateSections sections = DataGridStateSections.All, DataGridStateOptions options = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var target = sections == DataGridStateSections.All ? state.Sections : sections;

            if (HasSection(target, DataGridStateSections.Columns))
            {
                RestoreColumnLayoutState(state.Columns, options);
            }

            if (HasSection(target, DataGridStateSections.Grouping))
            {
                RestoreGroupingState(state.Grouping, options);
            }

            if (HasSection(target, DataGridStateSections.Sorting))
            {
                RestoreSortingState(state.Sorting, options);
            }

            if (HasSection(target, DataGridStateSections.Filtering))
            {
                RestoreFilteringState(state.Filtering, options);
            }

            if (HasSection(target, DataGridStateSections.Searching))
            {
                RestoreSearchState(state.Search, options);
            }

            if (HasSection(target, DataGridStateSections.ConditionalFormatting))
            {
                RestoreConditionalFormattingState(state.ConditionalFormatting, options);
            }

            if (HasSection(target, DataGridStateSections.Hierarchical))
            {
                RestoreHierarchicalState(state.Hierarchical, options);
            }

            if (HasSection(target, DataGridStateSections.Selection))
            {
                RestoreSelectionState(state.Selection, options);
            }

            if (HasSection(target, DataGridStateSections.Scroll))
            {
                TryRestoreScrollState(state.Scroll, options);
            }
        }

        public DataGridLayoutState SaveLayout(DataGridStateOptions options = null)
        {
            return new DataGridLayoutState
            {
                Columns = CaptureColumnLayoutState(options),
                Sorting = CaptureSortingState(options),
                Filtering = CaptureFilteringState(options),
                Search = CaptureSearchState(options),
                ConditionalFormatting = CaptureConditionalFormattingState(options),
                Grouping = CaptureGroupingState(options)
            };
        }

        public void RestoreLayout(DataGridLayoutState state, DataGridStateOptions options = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            RestoreColumnLayoutState(state.Columns, options);
            RestoreGroupingState(state.Grouping, options);
            RestoreSortingState(state.Sorting, options);
            RestoreFilteringState(state.Filtering, options);
            RestoreSearchState(state.Search, options);
            RestoreConditionalFormattingState(state.ConditionalFormatting, options);
        }

        public DataGridSelectionState CaptureSelectionState(DataGridStateOptions options = null)
        {
            var selection = new DataGridSelectionState
            {
                SelectionMode = SelectionMode,
                SelectionUnit = SelectionUnit
            };

            var selectedItems = new List<object>();
            var selectedIndexes = new List<int>();

            if (_selectionModelAdapter != null)
            {
                foreach (var item in _selectionModelAdapter.SelectedItemsView.Cast<object>())
                {
                    selectedItems.Add(GetItemKey(item, options));
                    var index = GetSelectionModelIndexOfItem(item);
                    if (index >= 0)
                    {
                        selectedIndexes.Add(index);
                    }
                }
            }

            selection.SelectedItemKeys = selectedItems;
            selection.SelectedIndexes = selectedIndexes;

            var selectedCells = new List<DataGridCellState>();
            if (SelectedCells != null)
            {
                foreach (var cell in SelectedCells)
                {
                    if (!cell.IsValid || cell.Column == null)
                    {
                        continue;
                    }

                    selectedCells.Add(new DataGridCellState
                    {
                        ItemKey = GetItemKey(cell.Item, options),
                        ColumnKey = GetColumnKey(cell.Column, options),
                        RowIndex = cell.RowIndex,
                        ColumnIndex = cell.ColumnIndex
                    });
                }
            }

            selection.SelectedCells = selectedCells;

            var currentCell = CreateCurrentCellInfo();
            if (currentCell.IsValid && currentCell.Column != null)
            {
                selection.CurrentCell = new DataGridCellState
                {
                    ItemKey = GetItemKey(currentCell.Item, options),
                    ColumnKey = GetColumnKey(currentCell.Column, options),
                    RowIndex = currentCell.RowIndex,
                    ColumnIndex = currentCell.ColumnIndex
                };
            }

            return selection;
        }

        public void RestoreSelectionState(DataGridSelectionState state, DataGridStateOptions options = null)
        {
            if (state == null)
            {
                return;
            }

            SelectionMode = state.SelectionMode;
            SelectionUnit = state.SelectionUnit;

            var selectionModelAvailable = _selectionModelAdapter != null;
            if (selectionModelAvailable)
            {
                using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
                var previousSync = PushSelectionSync();
                try
                {
                    using (_selectionModelAdapter.Model.BatchUpdate())
                    {
                        _selectionModelAdapter.Clear();

                        var anySelected = false;
                        if (state.SelectedItemKeys != null)
                        {
                            foreach (var key in state.SelectedItemKeys)
                            {
                                if (!TryResolveItemKey(key, options, out var item))
                                {
                                    continue;
                                }

                                var index = GetSelectionModelIndexOfItem(item);
                                if (index >= 0)
                                {
                                    _selectionModelAdapter.Select(index);
                                    anySelected = true;
                                }
                            }
                        }

                        if (!anySelected && state.SelectedIndexes != null)
                        {
                            foreach (var index in state.SelectedIndexes)
                            {
                                if (index >= 0)
                                {
                                    _selectionModelAdapter.Select(index);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    PopSelectionSync(previousSync);
                }

                ApplySelectionFromSelectionModel();
            }

            if (state.SelectedCells != null)
            {
                RestoreSelectedCellsFromState(state.SelectedCells, options);
            }

            if (state.CurrentCell != null)
            {
                RestoreCurrentCellFromState(state.CurrentCell, options);
            }

            if (selectionModelAvailable)
            {
                UpdateSelectionSnapshot();
            }
        }

        public DataGridScrollState CaptureScrollState(DataGridStateOptions options = null)
        {
            if (_scrollStateManager is ScrollStateManager manager)
            {
                return manager.CaptureState(options);
            }

            return null;
        }

        public bool TryRestoreScrollState(DataGridScrollState state, DataGridStateOptions options = null)
        {
            if (_scrollStateManager is ScrollStateManager manager)
            {
                return manager.TryRestore(state, options);
            }

            return false;
        }

        public DataGridSortingState CaptureSortingState(DataGridStateOptions options = null)
        {
            if (SortingModel == null)
            {
                return null;
            }

            var descriptors = SortingModel.Descriptors
                .Select(d => CloneSortingDescriptor(d, options))
                .Where(d => d != null)
                .ToList();

            return new DataGridSortingState
            {
                Descriptors = descriptors,
                MultiSort = SortingModel.MultiSort,
                CycleMode = SortingModel.CycleMode,
                OwnsViewSorts = SortingModel.OwnsViewSorts
            };
        }

        public void RestoreSortingState(DataGridSortingState state, DataGridStateOptions options = null)
        {
            if (state == null || SortingModel == null)
            {
                return;
            }

            SortingModel.MultiSort = state.MultiSort;
            SortingModel.CycleMode = state.CycleMode;
            SortingModel.OwnsViewSorts = state.OwnsViewSorts;

            if (state.Descriptors == null)
            {
                return;
            }

            var descriptors = state.Descriptors
                .Select(d => ResolveSortingDescriptor(d, options))
                .Where(d => d != null)
                .ToList();

            using (SortingModel.DeferRefresh())
            {
                SortingModel.Apply(descriptors);
            }
        }

        public DataGridFilteringState CaptureFilteringState(DataGridStateOptions options = null)
        {
            if (FilteringModel == null)
            {
                return null;
            }

            var descriptors = FilteringModel.Descriptors
                .Select(d => CloneFilteringDescriptor(d, options))
                .Where(d => d != null)
                .ToList();

            return new DataGridFilteringState
            {
                Descriptors = descriptors,
                OwnsViewFilter = FilteringModel.OwnsViewFilter
            };
        }

        public void RestoreFilteringState(DataGridFilteringState state, DataGridStateOptions options = null)
        {
            if (state == null || FilteringModel == null)
            {
                return;
            }

            FilteringModel.OwnsViewFilter = state.OwnsViewFilter;

            if (state.Descriptors == null)
            {
                return;
            }

            var descriptors = state.Descriptors
                .Select(d => ResolveFilteringDescriptor(d, options))
                .Where(d => d != null)
                .ToList();

            using (FilteringModel.DeferRefresh())
            {
                FilteringModel.Apply(descriptors);
            }
        }

        public DataGridSearchState CaptureSearchState(DataGridStateOptions options = null)
        {
            if (SearchModel == null)
            {
                return null;
            }

            var descriptors = SearchModel.Descriptors
                .Select(d => CloneSearchDescriptor(d, options))
                .Where(d => d != null)
                .ToList();

            var current = SearchModel.CurrentResult;
            DataGridSearchCurrentState currentState = null;
            if (current != null)
            {
                currentState = new DataGridSearchCurrentState
                {
                    CurrentIndex = SearchModel.CurrentIndex,
                    ItemKey = GetItemKey(current.Item, options),
                    ColumnKey = GetColumnKeyFromSearch(current.ColumnId, options),
                    RowIndex = current.RowIndex,
                    ColumnIndex = current.ColumnIndex
                };
            }

            return new DataGridSearchState
            {
                Descriptors = descriptors,
                HighlightMode = SearchModel.HighlightMode,
                HighlightCurrent = SearchModel.HighlightCurrent,
                UpdateSelectionOnNavigate = SearchModel.UpdateSelectionOnNavigate,
                WrapNavigation = SearchModel.WrapNavigation,
                Current = currentState
            };
        }

        public void RestoreSearchState(DataGridSearchState state, DataGridStateOptions options = null)
        {
            if (state == null || SearchModel == null)
            {
                return;
            }

            SearchModel.HighlightMode = state.HighlightMode;
            SearchModel.HighlightCurrent = state.HighlightCurrent;
            SearchModel.UpdateSelectionOnNavigate = state.UpdateSelectionOnNavigate;
            SearchModel.WrapNavigation = state.WrapNavigation;

            if (state.Descriptors != null)
            {
                var descriptors = state.Descriptors
                    .Select(d => ResolveSearchDescriptor(d, options))
                    .Where(d => d != null)
                    .ToList();

                using (SearchModel.DeferRefresh())
                {
                    SearchModel.Apply(descriptors);
                }
            }

            _pendingSearchCurrentState = null;
            _pendingSearchOptions = null;

            if (state.Current != null)
            {
                if (!TryRestoreSearchCurrent(state.Current, options))
                {
                    if (SearchModel.Results == null || SearchModel.Results.Count == 0)
                    {
                        _pendingSearchCurrentState = state.Current;
                        _pendingSearchOptions = options;
                    }
                }
            }
        }

        public DataGridConditionalFormattingState CaptureConditionalFormattingState(DataGridStateOptions options = null)
        {
            if (ConditionalFormattingModel == null)
            {
                return null;
            }

            var descriptors = ConditionalFormattingModel.Descriptors
                ?.Where(descriptor => descriptor != null)
                .ToList();

            return new DataGridConditionalFormattingState
            {
                Descriptors = descriptors
            };
        }

        public void RestoreConditionalFormattingState(
            DataGridConditionalFormattingState state,
            DataGridStateOptions options = null)
        {
            if (state == null || ConditionalFormattingModel == null)
            {
                return;
            }

            if (state.Descriptors == null || state.Descriptors.Count == 0)
            {
                ConditionalFormattingModel.Clear();
                return;
            }

            using (ConditionalFormattingModel.DeferRefresh())
            {
                ConditionalFormattingModel.Apply(state.Descriptors);
            }
        }

        public DataGridColumnLayoutState CaptureColumnLayoutState(DataGridStateOptions options = null)
        {
            if (ColumnsInternal == null)
            {
                return null;
            }

            var columns = new List<DataGridColumnState>();
            foreach (var column in ColumnsInternal.GetDisplayedColumns())
            {
                if (column == null || column == ColumnsInternal.FillerColumn || column == ColumnsInternal.RowGroupSpacerColumn)
                {
                    continue;
                }

                columns.Add(new DataGridColumnState
                {
                    ColumnKey = GetColumnKey(column, options),
                    DisplayIndex = column.DisplayIndex,
                    IsVisible = column.IsVisible,
                    Width = column.Width,
                    MinWidth = column.MinWidth,
                    MaxWidth = column.MaxWidth
                });
            }

            return new DataGridColumnLayoutState
            {
                Columns = columns,
                FrozenColumnCount = FrozenColumnCount,
                FrozenColumnCountRight = FrozenColumnCountRight
            };
        }

        public void RestoreColumnLayoutState(DataGridColumnLayoutState state, DataGridStateOptions options = null)
        {
            if (state == null || ColumnsInternal == null)
            {
                return;
            }

            var resolved = new List<(DataGridColumn Column, DataGridColumnState State)>();
            if (state.Columns != null)
            {
                foreach (var columnState in state.Columns)
                {
                    if (columnState == null)
                    {
                        continue;
                    }

                    var column = ResolveColumnKey(columnState.ColumnKey, options, null, columnState.DisplayIndex);
                    if (column != null && column != ColumnsInternal.FillerColumn && column != ColumnsInternal.RowGroupSpacerColumn)
                    {
                        resolved.Add((column, columnState));
                    }
                }
            }

            resolved.Sort((left, right) => left.State.DisplayIndex.CompareTo(right.State.DisplayIndex));

            var ordered = new List<DataGridColumn>();
            foreach (var entry in resolved)
            {
                if (!ordered.Contains(entry.Column))
                {
                    ordered.Add(entry.Column);
                }
            }

            foreach (var column in ColumnsInternal.GetDisplayedColumns())
            {
                if (column == null || column == ColumnsInternal.FillerColumn || column == ColumnsInternal.RowGroupSpacerColumn)
                {
                    continue;
                }

                if (!ordered.Contains(column))
                {
                    ordered.Add(column);
                }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].DisplayIndex = i;
            }

            foreach (var entry in resolved)
            {
                ApplyColumnBounds(entry.Column, entry.State);
                entry.Column.IsVisible = entry.State.IsVisible;
                entry.Column.SetWidthInternalNoCallback(entry.State.Width);
                OnColumnWidthChanged(entry.Column);
            }

            FrozenColumnCount = Math.Max(0, state.FrozenColumnCount);
            FrozenColumnCountRight = Math.Max(0, state.FrozenColumnCountRight);
        }

        public DataGridGroupingState CaptureGroupingState(DataGridStateOptions options = null)
        {
            if (DataConnection?.CollectionView is not DataGridCollectionView view)
            {
                return null;
            }

            var descriptions = view.GroupDescriptions?.ToList();
            var groupStates = CaptureGroupStates();

            return new DataGridGroupingState
            {
                GroupDescriptions = descriptions,
                GroupStates = groupStates
            };
        }

        public void RestoreGroupingState(DataGridGroupingState state, DataGridStateOptions options = null)
        {
            if (state == null)
            {
                return;
            }

            if (DataConnection?.CollectionView is DataGridCollectionView view && state.GroupDescriptions != null)
            {
                var groupDescriptions = view.GroupDescriptions;
                if (groupDescriptions != null && !GroupDescriptionsMatch(groupDescriptions, state.GroupDescriptions))
                {
                    using (view.DeferRefresh())
                    {
                        groupDescriptions.Clear();
                        foreach (var description in state.GroupDescriptions)
                        {
                            groupDescriptions.Add(description);
                        }
                    }
                }
            }

            if (state.GroupStates != null && state.GroupStates.Count > 0)
            {
                RestoreGroupStates(state.GroupStates);
            }

            RefreshGroupingLayout();
            RequestGroupingIndentationRefresh();
        }

        private static bool GroupDescriptionsMatch(
            AvaloniaList<DataGridGroupDescription> current,
            IReadOnlyList<DataGridGroupDescription> desired)
        {
            if (current == null || desired == null || current.Count != desired.Count)
            {
                return false;
            }

            for (int i = 0; i < current.Count; i++)
            {
                if (!ReferenceEquals(current[i], desired[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public DataGridHierarchicalState CaptureHierarchicalState(DataGridStateOptions options = null)
        {
            if (!_hierarchicalRowsEnabled || _hierarchicalModel == null)
            {
                return null;
            }

            if (_hierarchicalModel is IHierarchicalStateProvider provider)
            {
                var expanded = provider.CaptureExpandedState()?.ToList();
                return new DataGridHierarchicalState
                {
                    ExpandedKeys = expanded,
                    KeyMode = provider.ExpandedStateKeyMode
                };
            }

            return null;
        }

        public void RestoreHierarchicalState(DataGridHierarchicalState state, DataGridStateOptions options = null)
        {
            if (state == null || _hierarchicalModel == null)
            {
                return;
            }

            var keyMode = ResolveHierarchicalKeyMode(state);

            if (_hierarchicalModel is IHierarchicalStateProviderWithKeyMode providerWithKeyMode)
            {
                providerWithKeyMode.RestoreExpandedState(state.ExpandedKeys ?? Array.Empty<object>(), keyMode);
                return;
            }

            if (_hierarchicalModel is IHierarchicalStateProvider provider)
            {
                if (keyMode != provider.ExpandedStateKeyMode)
                {
                    return;
                }

                provider.RestoreExpandedState(state.ExpandedKeys ?? Array.Empty<object>());
            }
        }

        private static bool HasSection(DataGridStateSections value, DataGridStateSections section)
        {
            return (value & section) != 0;
        }

        private void RefreshGroupingLayout()
        {
            RefreshRowGroupHeaders();
            EnsureRowGroupSpacerColumn();

            if (DataConnection?.CollectionView is IDataGridCollectionView view && view.IsGrouping)
            {
                EnsureRowGroupSpacerColumnWidth(view.GroupingDepth);
            }

            UpdateGroupingIndentation();
        }

        internal void RefreshGroupingAfterDescriptionsChange()
        {
            RefreshGroupingLayout();
            RequestGroupingIndentationRefresh();
        }

        private void UpdateGroupingIndentation()
        {
            if (RowGroupSublevelIndents == null || DisplayData == null)
            {
                _pendingGroupingIndentationReset = false;
                InvalidateRowsMeasure(invalidateIndividualElements: true);
                InvalidateRowsArrange();
                return;
            }

            if (DisplayData.FirstScrollingSlot < 0 || DisplayData.LastScrollingSlot < 0)
            {
                _pendingGroupingIndentationReset = false;
                if (SlotCount > 0 && IsAttachedToVisualTree && IsVisible)
                {
                    RequestGroupingIndentationRefresh();
                }

                InvalidateRowsMeasure(invalidateIndividualElements: true);
                InvalidateRowsArrange();
                return;
            }

            var baseIndent = RowGroupSublevelIndents.Length > 0
                ? RowGroupSublevelIndents[0]
                : DATAGRID_defaultRowGroupSublevelIndent;

            int slot = DisplayData.FirstScrollingSlot;
            int lastSlot = DisplayData.LastScrollingSlot;
            var resetDisplayedRows = false;
            while (slot >= 0 && slot <= lastSlot)
            {
                var element = DisplayData.GetDisplayedElement(slot);
                if (element is DataGridRowGroupHeader header)
                {
                    var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                    if (rowGroupInfo == null)
                    {
                        resetDisplayedRows = true;
                        break;
                    }

                    if (rowGroupInfo != null)
                    {
                        SyncRowGroupHeaderInfo(header, rowGroupInfo);
                    }

                    var level = rowGroupInfo?.Level ?? header.Level;
                    if (level <= 0 || RowGroupSublevelIndents.Length == 0)
                    {
                        header.TotalIndent = 0;
                    }
                    else
                    {
                        var index = Math.Min(level - 1, RowGroupSublevelIndents.Length - 1);
                        header.TotalIndent = RowGroupSublevelIndents[index];
                    }
                }
                else if (element is DataGridRowGroupFooter footer)
                {
                    var rowGroupInfo = RowGroupFootersTable.GetValueAt(slot);
                    if (rowGroupInfo == null)
                    {
                        resetDisplayedRows = true;
                        break;
                    }

                    if (rowGroupInfo != null)
                    {
                        if (!ReferenceEquals(footer.RowGroupInfo, rowGroupInfo))
                        {
                            footer.RowGroupInfo = rowGroupInfo;
                            footer.Group = rowGroupInfo.CollectionViewGroup;
                        }

                        if (footer.Level != rowGroupInfo.Level)
                        {
                            footer.Level = rowGroupInfo.Level;
                        }
                    }

                    footer.Margin = new Thickness(footer.Level * baseIndent, 0, 0, 0);
                }

                slot = GetNextVisibleSlot(slot);
            }

            if (resetDisplayedRows)
            {
                if (!_pendingGroupingIndentationReset)
                {
                    _pendingGroupingIndentationReset = true;
                    ResetDisplayedRows();
                }
                RequestGroupingIndentationRefresh();
                return;
            }

            _pendingGroupingIndentationReset = false;
            InvalidateRowsMeasure(invalidateIndividualElements: true);
            InvalidateRowsArrange();
        }

        private static ExpandedStateKeyMode ResolveHierarchicalKeyMode(DataGridHierarchicalState state)
        {
            if (state == null)
            {
                return ExpandedStateKeyMode.Item;
            }

            var keyMode = state.KeyMode;
            if (keyMode != ExpandedStateKeyMode.Item || state.ExpandedKeys == null)
            {
                return keyMode;
            }

            foreach (var key in state.ExpandedKeys)
            {
                if (key == null)
                {
                    continue;
                }

                var typeName = key.GetType().FullName;
                if (string.Equals(
                    typeName,
                    "Avalonia.Controls.DataGridHierarchical.HierarchicalModel+ExpandedNodePath",
                    StringComparison.Ordinal))
                {
                    return ExpandedStateKeyMode.Path;
                }
            }

            return keyMode;
        }

        private void RestoreSelectedCellsFromState(IReadOnlyList<DataGridCellState> cells, DataGridStateOptions options)
        {
            var list = new List<DataGridCellInfo>();
            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                if (!TryResolveItemKey(cell.ItemKey, options, out var item))
                {
                    item = null;
                }

                var column = ResolveColumnKey(cell.ColumnKey, options, null, cell.ColumnIndex);
                if (column == null)
                {
                    continue;
                }

                var rowIndex = cell.RowIndex;
                if (rowIndex < 0 && item != null)
                {
                    TryGetRowIndexFromItem(item, out rowIndex);
                }

                if (rowIndex < 0)
                {
                    continue;
                }

                var columnIndex = column.Index;
                list.Add(new DataGridCellInfo(item, column, rowIndex, columnIndex, true));
            }

            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
            var previousSync = _syncingSelectedCells;
            _syncingSelectedCells = true;
            try
            {
                var removed = _selectedCellsView.ToList();
                ClearCellSelectionInternal(clearRows: false, raiseEvent: false);

                var added = new List<DataGridCellInfo>();
                HashSet<int> selectedRows = null;
                foreach (var cell in list)
                {
                    if (!TryNormalizeCell(cell, out var normalized))
                    {
                        continue;
                    }

                    if (AddCellSelectionInternal(normalized, added) &&
                        SelectionUnit != DataGridSelectionUnit.FullRow)
                    {
                        selectedRows ??= new HashSet<int>();
                        selectedRows.Add(normalized.RowIndex);
                    }
                }

                if (selectedRows != null)
                {
                    foreach (var rowIndex in selectedRows)
                    {
                        int slot = SlotFromRowIndex(rowIndex);
                        if (slot >= 0)
                        {
                            SetRowSelection(slot, isSelected: true, setAnchorSlot: false);
                        }
                    }
                }

                if (_selectedCellsBinding != null &&
                    !ReferenceEquals(_selectedCellsBinding, _selectedCellsView))
                {
                    _selectedCellsBinding.Clear();
                    foreach (var cell in _selectedCellsView)
                    {
                        _selectedCellsBinding.Add(cell);
                    }
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    RaiseSelectedCellsChanged(added, removed);
                }
            }
            finally
            {
                _syncingSelectedCells = previousSync;
            }
        }

        private void RestoreCurrentCellFromState(DataGridCellState state, DataGridStateOptions options)
        {
            if (state == null)
            {
                return;
            }

            if (!TryResolveItemKey(state.ItemKey, options, out var item))
            {
                item = null;
            }

            var column = ResolveColumnKey(state.ColumnKey, options, null, state.ColumnIndex);
            if (column == null)
            {
                return;
            }

            var rowIndex = state.RowIndex;
            if (rowIndex < 0 && item != null)
            {
                TryGetRowIndexFromItem(item, out rowIndex);
            }

            if (rowIndex < 0)
            {
                return;
            }

            var cellInfo = new DataGridCellInfo(item, column, rowIndex, column.Index, true);
            try
            {
                if (TryResolveCurrentCellCoordinates(cellInfo, out var columnIndex, out var slot))
                {
                    UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.None, scrollIntoView: false);
                }
            }
            catch (InvalidOperationException)
            {
                // Column is hidden or otherwise not eligible as current.
            }
            catch (ArgumentException)
            {
                // Column does not belong to this grid.
            }
        }

        private object GetColumnKey(DataGridColumn column, DataGridStateOptions options)
        {
            if (column == null)
            {
                return null;
            }

            var selector = options?.ColumnKeySelector;
            var key = selector != null ? selector(column) : null;
            return key ?? column;
        }

        private object GetColumnKeyFromSearch(object columnId, DataGridStateOptions options)
        {
            if (columnId is DataGridColumn column)
            {
                return GetColumnKey(column, options);
            }

            return columnId;
        }

        private object GetItemKey(object item, DataGridStateOptions options)
        {
            if (item == null)
            {
                return null;
            }

            var selector = options?.ItemKeySelector;
            var key = selector != null ? selector(item) : null;
            return key ?? item;
        }

        private bool TryResolveItemKey(object key, DataGridStateOptions options, out object item)
        {
            item = null;
            if (key == null)
            {
                return false;
            }

            var resolver = options?.ItemKeyResolver;
            if (resolver != null)
            {
                item = resolver(key);
                if (item != null)
                {
                    return true;
                }
            }

            if (DataConnection != null)
            {
                var index = DataConnection.IndexOf(key);
                if (index >= 0)
                {
                    item = DataConnection.GetDataItem(index);
                    return true;
                }
            }

            if (_hierarchicalRowsEnabled && _hierarchicalModel != null)
            {
                if (key is HierarchicalNode node)
                {
                    item = node;
                    return true;
                }

                var resolved = _hierarchicalModel.FindNode(key);
                if (resolved != null)
                {
                    item = resolved;
                    return true;
                }
            }

            return false;
        }

        private DataGridColumn ResolveColumnKey(object key, DataGridStateOptions options, string propertyPath, int fallbackIndex)
        {
            if (key is DataGridColumn column)
            {
                return column;
            }

            var resolver = options?.ColumnKeyResolver;
            if (resolver != null)
            {
                column = resolver(key);
                if (column != null)
                {
                    return column;
                }
            }

            if (key is string path)
            {
                column = FindColumnByPath(path);
                if (column != null)
                {
                    return column;
                }
            }

            if (!string.IsNullOrEmpty(propertyPath))
            {
                column = FindColumnByPath(propertyPath);
                if (column != null)
                {
                    return column;
                }
            }

            if (fallbackIndex >= 0 && ColumnsInternal != null)
            {
                var fallback = ColumnsInternal.GetColumnAtDisplayIndex(fallbackIndex);
                if (fallback != null)
                {
                    return fallback;
                }
            }

            if (fallbackIndex >= 0 && ColumnsItemsInternal != null && fallbackIndex < ColumnsItemsInternal.Count)
            {
                return ColumnsItemsInternal[fallbackIndex];
            }

            return null;
        }

        private DataGridColumn FindColumnByPath(string path)
        {
            if (ColumnsItemsInternal == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (var column in ColumnsItemsInternal)
            {
                if (column == null || column == ColumnsInternal.FillerColumn || column == ColumnsInternal.RowGroupSpacerColumn)
                {
                    continue;
                }

                var sortPath = column.GetSortPropertyName();
                if (!string.IsNullOrEmpty(sortPath) && string.Equals(sortPath, path, StringComparison.Ordinal))
                {
                    return column;
                }

                if (!string.IsNullOrEmpty(column.SortMemberPath) && string.Equals(column.SortMemberPath, path, StringComparison.Ordinal))
                {
                    return column;
                }

                var searchPath = DataGridColumnSearch.GetSearchMemberPath(column);
                if (!string.IsNullOrEmpty(searchPath) && string.Equals(searchPath, path, StringComparison.Ordinal))
                {
                    return column;
                }
            }

            return null;
        }

        private SortingDescriptor CloneSortingDescriptor(SortingDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnId = descriptor.ColumnId;
            if (columnId is DataGridColumn column)
            {
                columnId = GetColumnKey(column, options);
            }

            return new SortingDescriptor(columnId, descriptor.Direction, descriptor.PropertyPath, descriptor.Comparer, descriptor.Culture);
        }

        private SortingDescriptor ResolveSortingDescriptor(SortingDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnId = descriptor.ColumnId;
            var resolved = ResolveColumnKey(columnId, options, descriptor.PropertyPath, -1);
            if (resolved != null)
            {
                columnId = resolved;
            }

            return new SortingDescriptor(columnId, descriptor.Direction, descriptor.PropertyPath, descriptor.Comparer, descriptor.Culture);
        }

        private FilteringDescriptor CloneFilteringDescriptor(FilteringDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnId = descriptor.ColumnId;
            if (columnId is DataGridColumn column)
            {
                columnId = GetColumnKey(column, options);
            }

            return new FilteringDescriptor(
                columnId,
                descriptor.Operator,
                descriptor.PropertyPath,
                descriptor.Value,
                descriptor.Values,
                descriptor.Predicate,
                descriptor.Culture,
                descriptor.StringComparisonMode);
        }

        private FilteringDescriptor ResolveFilteringDescriptor(FilteringDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnId = descriptor.ColumnId;
            var resolved = ResolveColumnKey(columnId, options, descriptor.PropertyPath, -1);
            if (resolved != null)
            {
                columnId = resolved;
            }

            return new FilteringDescriptor(
                columnId,
                descriptor.Operator,
                descriptor.PropertyPath,
                descriptor.Value,
                descriptor.Values,
                descriptor.Predicate,
                descriptor.Culture,
                descriptor.StringComparisonMode);
        }

        private SearchDescriptor CloneSearchDescriptor(SearchDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnIds = CloneSearchColumnIds(descriptor.ColumnIds, options);

            return new SearchDescriptor(
                descriptor.Query,
                descriptor.MatchMode,
                descriptor.TermMode,
                descriptor.Scope,
                columnIds,
                descriptor.Comparison,
                descriptor.Culture,
                descriptor.WholeWord,
                descriptor.NormalizeWhitespace,
                descriptor.IgnoreDiacritics,
                descriptor.AllowEmpty);
        }

        private SearchDescriptor ResolveSearchDescriptor(SearchDescriptor descriptor, DataGridStateOptions options)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columnIds = ResolveSearchColumnIds(descriptor.ColumnIds, options);

            return new SearchDescriptor(
                descriptor.Query,
                descriptor.MatchMode,
                descriptor.TermMode,
                descriptor.Scope,
                columnIds,
                descriptor.Comparison,
                descriptor.Culture,
                descriptor.WholeWord,
                descriptor.NormalizeWhitespace,
                descriptor.IgnoreDiacritics,
                descriptor.AllowEmpty);
        }

        private IReadOnlyList<object> CloneSearchColumnIds(IReadOnlyList<object> columnIds, DataGridStateOptions options)
        {
            if (columnIds == null)
            {
                return null;
            }

            var list = new List<object>(columnIds.Count);
            foreach (var id in columnIds)
            {
                if (id is DataGridColumn column)
                {
                    list.Add(GetColumnKey(column, options));
                }
                else
                {
                    list.Add(id);
                }
            }

            return list;
        }

        private IReadOnlyList<object> ResolveSearchColumnIds(IReadOnlyList<object> columnIds, DataGridStateOptions options)
        {
            if (columnIds == null)
            {
                return null;
            }

            var list = new List<object>(columnIds.Count);
            foreach (var id in columnIds)
            {
                var resolved = ResolveColumnKey(id, options, null, -1);
                list.Add(resolved ?? id);
            }

            return list;
        }

        private bool TryRestoreSearchCurrent(DataGridSearchCurrentState state, DataGridStateOptions options)
        {
            if (state == null || SearchModel == null)
            {
                return false;
            }

            if (SearchModel.Results == null || SearchModel.Results.Count == 0)
            {
                return false;
            }

            if (state.CurrentIndex >= 0 && state.CurrentIndex < SearchModel.Results.Count)
            {
                return SearchModel.MoveTo(state.CurrentIndex);
            }

            if (state.RowIndex >= 0 && state.ColumnIndex >= 0)
            {
                for (int i = 0; i < SearchModel.Results.Count; i++)
                {
                    var result = SearchModel.Results[i];
                    if (result != null && result.RowIndex == state.RowIndex && result.ColumnIndex == state.ColumnIndex)
                    {
                        return SearchModel.MoveTo(i);
                    }
                }
            }

            var resolvedColumn = ResolveColumnKey(state.ColumnKey, options, null, state.ColumnIndex);
            if (!TryResolveItemKey(state.ItemKey, options, out var resolvedItem))
            {
                resolvedItem = null;
            }

            if (resolvedItem != null && resolvedColumn != null)
            {
                for (int i = 0; i < SearchModel.Results.Count; i++)
                {
                    var result = SearchModel.Results[i];
                    if (result == null)
                    {
                        continue;
                    }

                    if (!Equals(result.Item, resolvedItem))
                    {
                        continue;
                    }

                    if (Equals(result.ColumnId, resolvedColumn))
                    {
                        return SearchModel.MoveTo(i);
                    }
                }
            }

            return false;
        }

        private void TryRestorePendingSearchCurrent()
        {
            if (_pendingSearchCurrentState == null)
            {
                return;
            }

            if (TryRestoreSearchCurrent(_pendingSearchCurrentState, _pendingSearchOptions))
            {
                _pendingSearchCurrentState = null;
                _pendingSearchOptions = null;
            }
        }

        private IReadOnlyList<DataGridGroupState> CaptureGroupStates()
        {
            if (RowGroupHeadersTable == null)
            {
                return null;
            }

            var states = new List<DataGridGroupState>();
            foreach (var slot in RowGroupHeadersTable.GetIndexes())
            {
                var info = RowGroupHeadersTable.GetValueAt(slot);
                if (info?.CollectionViewGroup == null)
                {
                    continue;
                }

                states.Add(new DataGridGroupState
                {
                    PathKeys = BuildGroupPath(info.CollectionViewGroup),
                    IsExpanded = info.IsVisible
                });
            }

            return states;
        }

        private IReadOnlyList<object> BuildGroupPath(DataGridCollectionViewGroup group)
        {
            var path = new List<object>();
            var current = group;
            while (current != null)
            {
                path.Insert(0, current.Key);
                current = current.Parent;
            }

            return path;
        }

        private void RestoreGroupStates(IReadOnlyList<DataGridGroupState> states)
        {
            if (DataConnection?.CollectionView is not IDataGridCollectionView view || view.Groups == null)
            {
                return;
            }

            var groups = new List<DataGridCollectionViewGroup>();
            CollectGroups(view.Groups, groups);

            foreach (var group in groups)
            {
                var path = BuildGroupPath(group);
                var state = FindGroupState(states, path);
                if (state == null)
                {
                    continue;
                }

                if (state.IsExpanded)
                {
                    ExpandRowGroup(group, expandAllSubgroups: false);
                }
                else
                {
                    CollapseRowGroup(group, collapseAllSubgroups: false);
                }
            }
        }

        private void CollectGroups(IEnumerable groups, List<DataGridCollectionViewGroup> output)
        {
            foreach (var entry in groups)
            {
                if (entry is DataGridCollectionViewGroup group)
                {
                    output.Add(group);
                    if (!group.IsBottomLevel)
                    {
                        CollectGroups(group.Items, output);
                    }
                }
            }
        }

        private DataGridGroupState FindGroupState(IReadOnlyList<DataGridGroupState> states, IReadOnlyList<object> path)
        {
            foreach (var state in states)
            {
                if (state == null)
                {
                    continue;
                }

                if (GroupPathsEqual(state.PathKeys, path))
                {
                    return state;
                }
            }

            return null;
        }

        private static bool GroupPathsEqual(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplyColumnBounds(DataGridColumn column, DataGridColumnState state)
        {
            if (column == null || state == null)
            {
                return;
            }

            var min = Math.Max(0, state.MinWidth);
            var max = state.MaxWidth;
            if (double.IsNaN(max) || max < 0)
            {
                max = double.PositiveInfinity;
            }

            if (max < min)
            {
                max = min;
            }

            if (min > column.MaxWidth)
            {
                column.MaxWidth = max;
                column.MinWidth = min;
                return;
            }

            if (max < column.MinWidth)
            {
                column.MinWidth = min;
                column.MaxWidth = max;
                return;
            }

            column.MinWidth = min;
            column.MaxWidth = max;
        }
    }
}
