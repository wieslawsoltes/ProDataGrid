// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace Avalonia.Controls
{
    /// <summary>
    /// Defines behavior for unsupported runtime members during persistence mapping.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum DataGridStatePersistenceUnsupportedBehavior
    {
        /// <summary>
        /// Throws a <see cref="DataGridStatePersistenceException"/> when an unsupported member is encountered.
        /// </summary>
        Throw,

        /// <summary>
        /// Skips unsupported members and continues conversion when possible.
        /// </summary>
        Skip
    }

    /// <summary>
    /// Configures DataGrid state persistence behavior.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridStatePersistenceOptions
    {
        /// <summary>
        /// Gets or sets how unsupported runtime members are handled during conversion.
        /// </summary>
        public DataGridStatePersistenceUnsupportedBehavior UnsupportedBehavior { get; set; } =
            DataGridStatePersistenceUnsupportedBehavior.Throw;

        /// <summary>
        /// Gets or sets an optional token provider used to persist runtime-only members.
        /// </summary>
        public IDataGridStatePersistenceTokenProvider TokenProvider { get; set; }

        /// <summary>
        /// Gets or sets an optional token resolver used to restore runtime-only members.
        /// </summary>
        public IDataGridStatePersistenceTokenResolver TokenResolver { get; set; }
    }

    /// <summary>
    /// Represents persistence errors for DataGrid state conversion and serialization.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridStatePersistenceException : InvalidOperationException
    {
        public DataGridStatePersistenceException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Defines a pluggable serializer for <see cref="DataGridPersistedState"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridStateSerializer
    {
        /// <summary>
        /// Gets the stable serializer format identifier.
        /// </summary>
        string FormatId { get; }

        /// <summary>
        /// Serializes state to a binary payload.
        /// </summary>
        byte[] Serialize(DataGridPersistedState state);

        /// <summary>
        /// Deserializes state from a binary payload.
        /// </summary>
        DataGridPersistedState Deserialize(ReadOnlySpan<byte> payload);

        /// <summary>
        /// Serializes state to a string payload.
        /// </summary>
        string SerializeToString(DataGridPersistedState state);

        /// <summary>
        /// Deserializes state from a string payload.
        /// </summary>
        DataGridPersistedState Deserialize(string payload);
    }

    /// <summary>
    /// Provides tokens for runtime-only members so they can be represented in persisted state.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridStatePersistenceTokenProvider
    {
        bool TryGetSortingComparerToken(SortingDescriptor descriptor, out string token);

        bool TryGetFilteringPredicateToken(FilteringDescriptor descriptor, out string token);

        bool TryGetFilteringValueToken(FilteringDescriptor descriptor, object value, out string token);

        bool TryGetConditionalFormattingPredicateToken(ConditionalFormattingDescriptor descriptor, out string token);

        bool TryGetConditionalFormattingThemeToken(ConditionalFormattingDescriptor descriptor, out string token);

        bool TryGetGroupingValueConverterToken(IValueConverter converter, out string token);
    }

    /// <summary>
    /// Resolves persisted tokens back into runtime-only members.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridStatePersistenceTokenResolver
    {
        bool TryResolveSortingComparer(string token, out IComparer comparer);

        bool TryResolveFilteringPredicate(string token, object value, List<object> values, out Func<object, bool> predicate);

        bool TryResolveFilteringValue(string token, out object value);

        bool TryResolveConditionalFormattingPredicate(string token, out Func<ConditionalFormattingContext, bool> predicate);

        bool TryResolveConditionalFormattingTheme(string token, out ControlTheme theme);

        bool TryResolveGroupingValueConverter(string token, out IValueConverter converter);
    }

    /// <summary>
    /// Persistable DataGrid state model independent from runtime-only state objects.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridPersistedState
    {
        public int Version { get; set; } = 1;

        public DataGridStateSections Sections { get; set; } = DataGridStateSections.None;

        public SelectionState Selection { get; set; }

        public ScrollState Scroll { get; set; }

        public SortingState Sorting { get; set; }

        public FilteringState Filtering { get; set; }

        public SearchState Search { get; set; }

        public ConditionalFormattingState ConditionalFormatting { get; set; }

        public ColumnLayoutState Columns { get; set; }

        public GroupingState Grouping { get; set; }

        public HierarchicalState Hierarchical { get; set; }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class PersistedValue
        {
            public string Type { get; set; }

            public string Value { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class DataGridLengthValue
        {
            public double Value { get; set; }

            public DataGridLengthUnitType UnitType { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class CellState
        {
            public PersistedValue ItemKey { get; set; }

            public PersistedValue ColumnKey { get; set; }

            public int RowIndex { get; set; }

            public int ColumnIndex { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SelectionState
        {
            public DataGridSelectionMode SelectionMode { get; set; }

            public DataGridSelectionUnit SelectionUnit { get; set; }

            public IReadOnlyList<PersistedValue> SelectedItemKeys { get; set; }

            public IReadOnlyList<int> SelectedIndexes { get; set; }

            public IReadOnlyList<CellState> SelectedCells { get; set; }

            public CellState CurrentCell { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ScrollSample
        {
            public int Index { get; set; }

            public PersistedValue ItemKey { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ScrollState
        {
            public int? DataSourceCount { get; set; }

            public IReadOnlyList<ScrollSample> Samples { get; set; }

            public int FirstScrollingSlot { get; set; }

            public double NegVerticalOffset { get; set; }

            public double VerticalOffset { get; set; }

            public double HorizontalOffset { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SortingDescriptorState
        {
            public PersistedValue ColumnId { get; set; }

            public string PropertyPath { get; set; }

            public ListSortDirection Direction { get; set; }

            public string CultureName { get; set; }

            public string ComparerToken { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SortingState
        {
            public IReadOnlyList<SortingDescriptorState> Descriptors { get; set; }

            public bool MultiSort { get; set; }

            public SortCycleMode CycleMode { get; set; }

            public bool OwnsViewSorts { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class FilteringDescriptorState
        {
            public PersistedValue ColumnId { get; set; }

            public FilteringOperator Operator { get; set; }

            public string PropertyPath { get; set; }

            public PersistedValue Value { get; set; }
            public string ValueToken { get; set; }

            public IReadOnlyList<PersistedValue> Values { get; set; }

            public string[] ValuesTokens { get; set; }

            public string CultureName { get; set; }

            public StringComparison? StringComparisonMode { get; set; }

            public string PredicateToken { get; set; }

            public bool? ColumnIdIsColumnDefinition { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class FilteringState
        {
            public IReadOnlyList<FilteringDescriptorState> Descriptors { get; set; }

            public bool OwnsViewFilter { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SearchDescriptorState
        {
            public string Query { get; set; }

            public SearchMatchMode MatchMode { get; set; }

            public SearchTermCombineMode TermMode { get; set; }

            public SearchScope Scope { get; set; }

            public IReadOnlyList<PersistedValue> ColumnIds { get; set; }

            public StringComparison? Comparison { get; set; }

            public string CultureName { get; set; }

            public bool WholeWord { get; set; }

            public bool NormalizeWhitespace { get; set; }

            public bool IgnoreDiacritics { get; set; }

            public bool AllowEmpty { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SearchCurrentState
        {
            public int CurrentIndex { get; set; } = -1;

            public PersistedValue ItemKey { get; set; }

            public PersistedValue ColumnKey { get; set; }

            public int RowIndex { get; set; } = -1;

            public int ColumnIndex { get; set; } = -1;
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class SearchState
        {
            public IReadOnlyList<SearchDescriptorState> Descriptors { get; set; }

            public SearchHighlightMode HighlightMode { get; set; }

            public bool HighlightCurrent { get; set; }

            public bool UpdateSelectionOnNavigate { get; set; }

            public bool WrapNavigation { get; set; }

            public SearchCurrentState Current { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ConditionalFormattingDescriptorState
        {
            public PersistedValue RuleId { get; set; }

            public PersistedValue ColumnId { get; set; }

            public string PropertyPath { get; set; }

            public ConditionalFormattingOperator Operator { get; set; }

            public PersistedValue Value { get; set; }

            public IReadOnlyList<PersistedValue> Values { get; set; }

            public PersistedValue ThemeKey { get; set; }

            public ConditionalFormattingTarget Target { get; set; }

            public ConditionalFormattingValueSource ValueSource { get; set; }

            public bool StopIfTrue { get; set; }

            public int Priority { get; set; }

            public string CultureName { get; set; }

            public StringComparison? StringComparisonMode { get; set; }

            public string PredicateToken { get; set; }

            public string ThemeToken { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ConditionalFormattingState
        {
            public IReadOnlyList<ConditionalFormattingDescriptorState> Descriptors { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ColumnState
        {
            public PersistedValue ColumnKey { get; set; }

            public int DisplayIndex { get; set; }

            public bool IsVisible { get; set; }

            public DataGridLengthValue Width { get; set; }

            public double MinWidth { get; set; }

            public double MaxWidth { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class ColumnLayoutState
        {
            public IReadOnlyList<ColumnState> Columns { get; set; }

            public int FrozenColumnCount { get; set; }

            public int FrozenColumnCountRight { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class GroupDescriptionState
        {
            public string Kind { get; set; }

            public string PropertyPath { get; set; }

            public IReadOnlyList<PersistedValue> GroupKeys { get; set; }

            public string ValueConverterToken { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class GroupState
        {
            public IReadOnlyList<PersistedValue> PathKeys { get; set; }

            public bool IsExpanded { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class GroupingState
        {
            public IReadOnlyList<GroupDescriptionState> GroupDescriptions { get; set; }

            public IReadOnlyList<GroupState> GroupStates { get; set; }
        }

        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        sealed class HierarchicalState
        {
            public IReadOnlyList<PersistedValue> ExpandedKeys { get; set; }

            public ExpandedStateKeyMode KeyMode { get; set; }
        }
    }
}
