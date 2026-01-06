// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    [Flags]
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum DataGridStateSections
    {
        None = 0,
        Columns = 1 << 0,
        Sorting = 1 << 1,
        Filtering = 1 << 2,
        Searching = 1 << 3,
        ConditionalFormatting = 1 << 4,
        Grouping = 1 << 5,
        Hierarchical = 1 << 6,
        Selection = 1 << 7,
        Scroll = 1 << 8,
        Layout = Columns | Sorting | Filtering | Searching | ConditionalFormatting | Grouping,
        View = Sorting | Filtering | Searching | ConditionalFormatting | Grouping,
        All = Layout | Hierarchical | Selection | Scroll
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridStateOptions
    {
        public Func<DataGridColumn, object> ColumnKeySelector { get; set; }

        public Func<object, DataGridColumn> ColumnKeyResolver { get; set; }

        public Func<object, object> ItemKeySelector { get; set; }

        public Func<object, object> ItemKeyResolver { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridState
    {
        public int Version { get; set; } = 1;

        public DataGridStateSections Sections { get; set; } = DataGridStateSections.All;

        public DataGridSelectionState Selection { get; set; }

        public DataGridScrollState Scroll { get; set; }

        public DataGridSortingState Sorting { get; set; }

        public DataGridFilteringState Filtering { get; set; }

        public DataGridSearchState Search { get; set; }

        public DataGridConditionalFormattingState ConditionalFormatting { get; set; }

        public DataGridColumnLayoutState Columns { get; set; }

        public DataGridGroupingState Grouping { get; set; }

        public DataGridHierarchicalState Hierarchical { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridLayoutState
    {
        public DataGridColumnLayoutState Columns { get; set; }

        public DataGridSortingState Sorting { get; set; }

        public DataGridFilteringState Filtering { get; set; }

        public DataGridSearchState Search { get; set; }

        public DataGridConditionalFormattingState ConditionalFormatting { get; set; }

        public DataGridGroupingState Grouping { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridSelectionState
    {
        public DataGridSelectionMode SelectionMode { get; set; }

        public DataGridSelectionUnit SelectionUnit { get; set; }

        public IReadOnlyList<object> SelectedItemKeys { get; set; }

        public IReadOnlyList<int> SelectedIndexes { get; set; }

        public IReadOnlyList<DataGridCellState> SelectedCells { get; set; }

        public DataGridCellState CurrentCell { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridCellState
    {
        public object ItemKey { get; set; }

        public object ColumnKey { get; set; }

        public int RowIndex { get; set; }

        public int ColumnIndex { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridScrollState
    {
        public object DataSource { get; set; }

        public int? DataSourceCount { get; set; }

        public IReadOnlyList<DataGridScrollSample> Samples { get; set; }

        public int FirstScrollingSlot { get; set; }

        public double NegVerticalOffset { get; set; }

        public double VerticalOffset { get; set; }

        public double HorizontalOffset { get; set; }

        public RowHeightEstimatorState RowHeightEstimatorState { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridScrollSample
    {
        public DataGridScrollSample(int index, object itemKey)
        {
            Index = index;
            ItemKey = itemKey;
        }

        public int Index { get; }

        public object ItemKey { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridSortingState
    {
        public IReadOnlyList<SortingDescriptor> Descriptors { get; set; }

        public bool MultiSort { get; set; }

        public SortCycleMode CycleMode { get; set; }

        public bool OwnsViewSorts { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridFilteringState
    {
        public IReadOnlyList<FilteringDescriptor> Descriptors { get; set; }

        public bool OwnsViewFilter { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridSearchState
    {
        public IReadOnlyList<SearchDescriptor> Descriptors { get; set; }

        public SearchHighlightMode HighlightMode { get; set; }

        public bool HighlightCurrent { get; set; }

        public bool UpdateSelectionOnNavigate { get; set; }

        public bool WrapNavigation { get; set; }

        public DataGridSearchCurrentState Current { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridSearchCurrentState
    {
        public int CurrentIndex { get; set; } = -1;

        public object ItemKey { get; set; }

        public object ColumnKey { get; set; }

        public int RowIndex { get; set; } = -1;

        public int ColumnIndex { get; set; } = -1;
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridConditionalFormattingState
    {
        public IReadOnlyList<ConditionalFormattingDescriptor> Descriptors { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridColumnLayoutState
    {
        public IReadOnlyList<DataGridColumnState> Columns { get; set; }

        public int FrozenColumnCount { get; set; }

        public int FrozenColumnCountRight { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridColumnState
    {
        public object ColumnKey { get; set; }

        public int DisplayIndex { get; set; }

        public bool IsVisible { get; set; }

        public DataGridLength Width { get; set; }

        public double MinWidth { get; set; }

        public double MaxWidth { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridGroupingState
    {
        public IReadOnlyList<DataGridGroupDescription> GroupDescriptions { get; set; }

        public IReadOnlyList<DataGridGroupState> GroupStates { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridGroupState
    {
        public IReadOnlyList<object> PathKeys { get; set; }

        public bool IsExpanded { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridHierarchicalState
    {
        public IReadOnlyList<object> ExpandedKeys { get; set; }

        public ExpandedStateKeyMode KeyMode { get; set; }
    }
}
