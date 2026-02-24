// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace Avalonia.Controls
{
    internal static class DataGridStatePersistenceMapper
    {
        public static DataGridPersistedState ToPersisted(
            DataGridState state,
            DataGridStateOptions stateOptions,
            DataGridStatePersistenceOptions persistenceOptions)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var context = new ConversionContext(persistenceOptions);
            var result = new DataGridPersistedState
            {
                Version = state.Version
            };

            var persistedSections = DataGridStateSections.None;

            if (HasSection(state.Sections, DataGridStateSections.Columns)
                && TryMapColumnsToPersisted(state.Columns, context, out var columns))
            {
                result.Columns = columns;
                persistedSections |= DataGridStateSections.Columns;
            }

            if (HasSection(state.Sections, DataGridStateSections.Sorting)
                && TryMapSortingToPersisted(state.Sorting, context, out var sorting))
            {
                result.Sorting = sorting;
                persistedSections |= DataGridStateSections.Sorting;
            }

            if (HasSection(state.Sections, DataGridStateSections.Filtering)
                && TryMapFilteringToPersisted(state.Filtering, context, out var filtering))
            {
                result.Filtering = filtering;
                persistedSections |= DataGridStateSections.Filtering;
            }

            if (HasSection(state.Sections, DataGridStateSections.Searching)
                && TryMapSearchingToPersisted(state.Search, context, out var search))
            {
                result.Search = search;
                persistedSections |= DataGridStateSections.Searching;
            }

            if (HasSection(state.Sections, DataGridStateSections.ConditionalFormatting)
                && TryMapConditionalFormattingToPersisted(state.ConditionalFormatting, context, out var conditionalFormatting))
            {
                result.ConditionalFormatting = conditionalFormatting;
                persistedSections |= DataGridStateSections.ConditionalFormatting;
            }

            if (HasSection(state.Sections, DataGridStateSections.Grouping)
                && TryMapGroupingToPersisted(state.Grouping, context, out var grouping))
            {
                result.Grouping = grouping;
                persistedSections |= DataGridStateSections.Grouping;
            }

            if (HasSection(state.Sections, DataGridStateSections.Hierarchical)
                && TryMapHierarchicalToPersisted(state.Hierarchical, context, out var hierarchical))
            {
                result.Hierarchical = hierarchical;
                persistedSections |= DataGridStateSections.Hierarchical;
            }

            if (HasSection(state.Sections, DataGridStateSections.Selection)
                && TryMapSelectionToPersisted(state.Selection, context, out var selection))
            {
                result.Selection = selection;
                persistedSections |= DataGridStateSections.Selection;
            }

            if (HasSection(state.Sections, DataGridStateSections.Scroll)
                && TryMapScrollToPersisted(state.Scroll, stateOptions, context, out var scroll))
            {
                result.Scroll = scroll;
                persistedSections |= DataGridStateSections.Scroll;
            }

            result.Sections = persistedSections;
            return result;
        }

        public static DataGridState ToRuntime(
            DataGridPersistedState persisted,
            DataGridStateSections requestedSections,
            DataGridStateOptions stateOptions,
            DataGridStatePersistenceOptions persistenceOptions)
        {
            if (persisted == null)
            {
                throw new ArgumentNullException(nameof(persisted));
            }

            var context = new ConversionContext(persistenceOptions);
            var state = new DataGridState
            {
                Version = persisted.Version
            };

            var targetSections = ResolveTargetSections(persisted, requestedSections);

            var mappedSections = DataGridStateSections.None;

            if (HasSection(targetSections, DataGridStateSections.Columns)
                && TryMapColumnsToRuntime(persisted.Columns, context, out var columns))
            {
                state.Columns = columns;
                mappedSections |= DataGridStateSections.Columns;
            }

            if (HasSection(targetSections, DataGridStateSections.Sorting)
                && TryMapSortingToRuntime(persisted.Sorting, context, out var sorting))
            {
                state.Sorting = sorting;
                mappedSections |= DataGridStateSections.Sorting;
            }

            if (HasSection(targetSections, DataGridStateSections.Filtering)
                && TryMapFilteringToRuntime(persisted.Filtering, context, out var filtering))
            {
                state.Filtering = filtering;
                mappedSections |= DataGridStateSections.Filtering;
            }

            if (HasSection(targetSections, DataGridStateSections.Searching)
                && TryMapSearchingToRuntime(persisted.Search, context, out var search))
            {
                state.Search = search;
                mappedSections |= DataGridStateSections.Searching;
            }

            if (HasSection(targetSections, DataGridStateSections.ConditionalFormatting)
                && TryMapConditionalFormattingToRuntime(persisted.ConditionalFormatting, context, out var conditionalFormatting))
            {
                state.ConditionalFormatting = conditionalFormatting;
                mappedSections |= DataGridStateSections.ConditionalFormatting;
            }

            if (HasSection(targetSections, DataGridStateSections.Grouping)
                && TryMapGroupingToRuntime(persisted.Grouping, context, out var grouping))
            {
                state.Grouping = grouping;
                mappedSections |= DataGridStateSections.Grouping;
            }

            if (HasSection(targetSections, DataGridStateSections.Hierarchical)
                && TryMapHierarchicalToRuntime(persisted.Hierarchical, context, out var hierarchical))
            {
                state.Hierarchical = hierarchical;
                mappedSections |= DataGridStateSections.Hierarchical;
            }

            if (HasSection(targetSections, DataGridStateSections.Selection)
                && TryMapSelectionToRuntime(persisted.Selection, context, out var selection))
            {
                state.Selection = selection;
                mappedSections |= DataGridStateSections.Selection;
            }

            if (HasSection(targetSections, DataGridStateSections.Scroll)
                && TryMapScrollToRuntime(persisted.Scroll, stateOptions, context, out var scroll))
            {
                state.Scroll = scroll;
                mappedSections |= DataGridStateSections.Scroll;
            }

            state.Sections = mappedSections;
            return state;
        }

        private static bool TryMapSelectionToPersisted(
            DataGridSelectionState state,
            ConversionContext context,
            out DataGridPersistedState.SelectionState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            if (!TryMapValuesToPersisted(state.SelectedItemKeys, "Selection.SelectedItemKeys", context, out var selectedItemKeys))
            {
                return false;
            }

            var selectedCells = new List<DataGridPersistedState.CellState>();
            if (state.SelectedCells != null)
            {
                for (var i = 0; i < state.SelectedCells.Count; i++)
                {
                    if (!TryMapCellToPersisted(state.SelectedCells[i], $"Selection.SelectedCells[{i}]", context, out var cell))
                    {
                        continue;
                    }

                    selectedCells.Add(cell);
                }
            }

            DataGridPersistedState.CellState currentCell = null;
            if (state.CurrentCell != null
                && !TryMapCellToPersisted(state.CurrentCell, "Selection.CurrentCell", context, out currentCell))
            {
                currentCell = null;
            }

            persisted = new DataGridPersistedState.SelectionState
            {
                SelectionMode = state.SelectionMode,
                SelectionUnit = state.SelectionUnit,
                SelectedItemKeys = selectedItemKeys,
                SelectedIndexes = state.SelectedIndexes?.ToList(),
                SelectedCells = selectedCells,
                CurrentCell = currentCell
            };

            return true;
        }

        private static bool TryMapSelectionToRuntime(
            DataGridPersistedState.SelectionState persisted,
            ConversionContext context,
            out DataGridSelectionState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            if (!TryMapValuesToRuntime(persisted.SelectedItemKeys, "Selection.SelectedItemKeys", context, out var selectedItemKeys))
            {
                return false;
            }

            var selectedCells = new List<DataGridCellState>();
            if (persisted.SelectedCells != null)
            {
                for (var i = 0; i < persisted.SelectedCells.Count; i++)
                {
                    if (!TryMapCellToRuntime(persisted.SelectedCells[i], $"Selection.SelectedCells[{i}]", context, out var cell))
                    {
                        continue;
                    }

                    selectedCells.Add(cell);
                }
            }

            DataGridCellState currentCell = null;
            if (persisted.CurrentCell != null
                && !TryMapCellToRuntime(persisted.CurrentCell, "Selection.CurrentCell", context, out currentCell))
            {
                currentCell = null;
            }

            state = new DataGridSelectionState
            {
                SelectionMode = persisted.SelectionMode,
                SelectionUnit = persisted.SelectionUnit,
                SelectedItemKeys = selectedItemKeys,
                SelectedIndexes = persisted.SelectedIndexes?.ToList(),
                SelectedCells = selectedCells,
                CurrentCell = currentCell
            };

            return true;
        }

        private static bool TryMapScrollToPersisted(
            DataGridScrollState state,
            DataGridStateOptions stateOptions,
            ConversionContext context,
            out DataGridPersistedState.ScrollState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            if (stateOptions?.ItemKeySelector == null)
            {
                return context.Unsupported(
                    "Scroll",
                    "Scroll persistence requires DataGridStateOptions.ItemKeySelector because runtime data source references are not persisted.");
            }

            var samples = new List<DataGridPersistedState.ScrollSample>();
            if (state.Samples != null)
            {
                for (var i = 0; i < state.Samples.Count; i++)
                {
                    var sample = state.Samples[i];
                    if (sample == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(sample.ItemKey, $"Scroll.Samples[{i}].ItemKey", context, out var itemKey))
                    {
                        continue;
                    }

                    samples.Add(new DataGridPersistedState.ScrollSample
                    {
                        Index = sample.Index,
                        ItemKey = itemKey
                    });
                }
            }

            persisted = new DataGridPersistedState.ScrollState
            {
                DataSourceCount = state.DataSourceCount,
                Samples = samples,
                FirstScrollingSlot = state.FirstScrollingSlot,
                NegVerticalOffset = state.NegVerticalOffset,
                VerticalOffset = state.VerticalOffset,
                HorizontalOffset = state.HorizontalOffset
            };

            return true;
        }

        private static bool TryMapScrollToRuntime(
            DataGridPersistedState.ScrollState persisted,
            DataGridStateOptions stateOptions,
            ConversionContext context,
            out DataGridScrollState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            if (stateOptions?.ItemKeySelector == null)
            {
                return context.Unsupported(
                    "Scroll",
                    "Restoring persisted scroll state requires DataGridStateOptions.ItemKeySelector.");
            }

            var samples = new List<DataGridScrollSample>();
            if (persisted.Samples != null)
            {
                for (var i = 0; i < persisted.Samples.Count; i++)
                {
                    var sample = persisted.Samples[i];
                    if (sample == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(sample.ItemKey, $"Scroll.Samples[{i}].ItemKey", context, out var itemKey))
                    {
                        continue;
                    }

                    samples.Add(new DataGridScrollSample(sample.Index, itemKey));
                }
            }

            state = new DataGridScrollState
            {
                DataSource = null,
                DataSourceCount = persisted.DataSourceCount,
                Samples = samples,
                FirstScrollingSlot = persisted.FirstScrollingSlot,
                NegVerticalOffset = persisted.NegVerticalOffset,
                VerticalOffset = persisted.VerticalOffset,
                HorizontalOffset = persisted.HorizontalOffset,
                RowHeightEstimatorState = null
            };

            return true;
        }

        private static bool TryMapSortingToPersisted(
            DataGridSortingState state,
            ConversionContext context,
            out DataGridPersistedState.SortingState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var descriptors = new List<DataGridPersistedState.SortingDescriptorState>();
            if (state.Descriptors != null)
            {
                for (var i = 0; i < state.Descriptors.Count; i++)
                {
                    var descriptor = state.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    string comparerToken = null;
                    if (descriptor.Comparer != null
                        && !context.TryGetSortingComparerToken(
                            descriptor,
                            $"Sorting.Descriptors[{i}].Comparer",
                            out comparerToken))
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(descriptor.ColumnId, $"Sorting.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    descriptors.Add(new DataGridPersistedState.SortingDescriptorState
                    {
                        ColumnId = columnId,
                        Direction = descriptor.Direction,
                        PropertyPath = descriptor.PropertyPath,
                        CultureName = descriptor.Culture?.Name,
                        ComparerToken = comparerToken
                    });
                }
            }

            persisted = new DataGridPersistedState.SortingState
            {
                Descriptors = descriptors,
                MultiSort = state.MultiSort,
                CycleMode = state.CycleMode,
                OwnsViewSorts = state.OwnsViewSorts
            };

            return true;
        }

        private static bool TryMapSortingToRuntime(
            DataGridPersistedState.SortingState persisted,
            ConversionContext context,
            out DataGridSortingState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var descriptors = new List<SortingDescriptor>();
            if (persisted.Descriptors != null)
            {
                for (var i = 0; i < persisted.Descriptors.Count; i++)
                {
                    var descriptor = persisted.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(descriptor.ColumnId, $"Sorting.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    if (columnId == null)
                    {
                        if (!context.Unsupported($"Sorting.Descriptors[{i}].ColumnId", "Column id cannot be null."))
                        {
                            continue;
                        }
                    }

                    if (!TryParseCulture(descriptor.CultureName, $"Sorting.Descriptors[{i}].CultureName", context, out var culture))
                    {
                        continue;
                    }

                    if (!context.TryResolveSortingComparer(
                            descriptor.ComparerToken,
                            $"Sorting.Descriptors[{i}].ComparerToken",
                            out var comparer))
                    {
                        continue;
                    }

                    descriptors.Add(new SortingDescriptor(
                        columnId,
                        descriptor.Direction,
                        descriptor.PropertyPath,
                        comparer: comparer,
                        culture: culture));
                }
            }

            state = new DataGridSortingState
            {
                Descriptors = descriptors,
                MultiSort = persisted.MultiSort,
                CycleMode = persisted.CycleMode,
                OwnsViewSorts = persisted.OwnsViewSorts
            };

            return true;
        }

        private static bool TryMapFilteringToPersisted(
            DataGridFilteringState state,
            ConversionContext context,
            out DataGridPersistedState.FilteringState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var descriptors = new List<DataGridPersistedState.FilteringDescriptorState>();
            if (state.Descriptors != null)
            {
                for (var i = 0; i < state.Descriptors.Count; i++)
                {
                    var descriptor = state.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    string predicateToken = null;
                    if (descriptor.Predicate != null
                        && !context.TryGetFilteringPredicateToken(
                            descriptor,
                            $"Filtering.Descriptors[{i}].Predicate",
                            out predicateToken))
                    {
                        continue;
                    }

                    if (descriptor.Operator == FilteringOperator.Custom
                        && string.IsNullOrEmpty(predicateToken))
                    {
                        if (!context.Unsupported(
                                $"Filtering.Descriptors[{i}].Operator",
                                "Custom filtering operators require a predicate token."))
                        {
                            continue;
                        }
                    }

                    DataGridPersistedState.PersistedValue value = null;
                    IReadOnlyList<DataGridPersistedState.PersistedValue> values = null;

                    if (!TryMapValueToPersisted(descriptor.ColumnId, $"Filtering.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    if (!context.TryGetFilteringValueToken(descriptor, out var valueToken) && !TryMapValueToPersisted(descriptor.Value, $"Filtering.Descriptors[{i}].Value", context, out value))
                    {
                        continue;
                    }

                    if (!context.TryGetFilteringValuesTokens(descriptor, out var valuesTokens) && !TryMapValuesToPersisted(descriptor.Values, $"Filtering.Descriptors[{i}].Values", context, out values))
                    {
                        continue;
                    }

                    descriptors.Add(new DataGridPersistedState.FilteringDescriptorState
                    {
                        ColumnId = columnId,
                        Operator = descriptor.Operator,
                        PropertyPath = descriptor.PropertyPath,
                        Value = value,
                        ValueToken = valueToken,
                        Values = values,
                        ValuesTokens = valuesTokens,
                        CultureName = descriptor.Culture?.Name,
                        StringComparisonMode = descriptor.StringComparisonMode,
                        PredicateToken = predicateToken,
                        ColumnIdIsColumnDefinition = descriptor.ColumnIdIsColumnDefinition
                    });
                }
            }

            persisted = new DataGridPersistedState.FilteringState
            {
                Descriptors = descriptors,
                OwnsViewFilter = state.OwnsViewFilter
            };

            return true;
        }

        private static bool TryMapFilteringToRuntime(
            DataGridPersistedState.FilteringState persisted,
            ConversionContext context,
            out DataGridFilteringState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var descriptors = new List<FilteringDescriptor>();
            if (persisted.Descriptors != null)
            {
                for (var i = 0; i < persisted.Descriptors.Count; i++)
                {
                    var descriptor = persisted.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(descriptor.ColumnId, $"Filtering.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    if (columnId == null)
                    {
                        if (!context.Unsupported($"Filtering.Descriptors[{i}].ColumnId", "Column id cannot be null."))
                        {
                            continue;
                        }
                    }

                    List<object> values = null;
                    if (!context.TryResolveFilteringValue(descriptor.ValueToken, out var value) && !DataGridStatePersistenceValueConverter.TryReadValue(descriptor.Value, out value, out _))
                    {
                        if (!context.TryResolveFilteringValues(descriptor.ValuesTokens, out values) && !TryMapValuesToRuntime(descriptor.Values, $"Filtering.Descriptors[{i}].Values", context, out values))
                        {
                            continue;
                        }
                    }

                    if (!context.TryResolveFilteringPredicate(
                            descriptor.PredicateToken,
                            $"Filtering.Descriptors[{i}].PredicateToken",
                            value,
                            values,
                            out var predicate))
                    {
                        continue;
                    }

                    if (descriptor.Operator == FilteringOperator.Custom
                        && predicate == null)
                    {
                        if (!context.Unsupported(
                                $"Filtering.Descriptors[{i}].Operator",
                                "Custom filtering operators require a predicate token resolver."))
                        {
                            continue;
                        }
                    }

                    if (!TryParseCulture(descriptor.CultureName, $"Filtering.Descriptors[{i}].CultureName", context, out var culture))
                    {
                        continue;
                    }

                    descriptors.Add(new FilteringDescriptor(
                        columnId,
                        descriptor.Operator,
                        descriptor.PropertyPath,
                        value,
                        values,
                        predicate: predicate,
                        culture: culture,
                        stringComparison: descriptor.StringComparisonMode)
                    {
                        ColumnIdIsColumnDefinition = descriptor.ColumnIdIsColumnDefinition
                    });
                }
            }

            state = new DataGridFilteringState
            {
                Descriptors = descriptors,
                OwnsViewFilter = persisted.OwnsViewFilter
            };

            return true;
        }

        private static bool TryMapSearchingToPersisted(
            DataGridSearchState state,
            ConversionContext context,
            out DataGridPersistedState.SearchState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var descriptors = new List<DataGridPersistedState.SearchDescriptorState>();
            if (state.Descriptors != null)
            {
                for (var i = 0; i < state.Descriptors.Count; i++)
                {
                    var descriptor = state.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    if (!TryMapValuesToPersisted(descriptor.ColumnIds, $"Search.Descriptors[{i}].ColumnIds", context, out var columnIds))
                    {
                        continue;
                    }

                    descriptors.Add(new DataGridPersistedState.SearchDescriptorState
                    {
                        Query = descriptor.Query,
                        MatchMode = descriptor.MatchMode,
                        TermMode = descriptor.TermMode,
                        Scope = descriptor.Scope,
                        ColumnIds = columnIds,
                        Comparison = descriptor.Comparison,
                        CultureName = descriptor.Culture?.Name,
                        WholeWord = descriptor.WholeWord,
                        NormalizeWhitespace = descriptor.NormalizeWhitespace,
                        IgnoreDiacritics = descriptor.IgnoreDiacritics,
                        AllowEmpty = descriptor.AllowEmpty
                    });
                }
            }

            DataGridPersistedState.SearchCurrentState current = null;
            if (state.Current != null)
            {
                if (TryMapValueToPersisted(state.Current.ItemKey, "Search.Current.ItemKey", context, out var itemKey)
                    && TryMapValueToPersisted(state.Current.ColumnKey, "Search.Current.ColumnKey", context, out var columnKey))
                {
                    current = new DataGridPersistedState.SearchCurrentState
                    {
                        CurrentIndex = state.Current.CurrentIndex,
                        ItemKey = itemKey,
                        ColumnKey = columnKey,
                        RowIndex = state.Current.RowIndex,
                        ColumnIndex = state.Current.ColumnIndex
                    };
                }
            }

            persisted = new DataGridPersistedState.SearchState
            {
                Descriptors = descriptors,
                HighlightMode = state.HighlightMode,
                HighlightCurrent = state.HighlightCurrent,
                UpdateSelectionOnNavigate = state.UpdateSelectionOnNavigate,
                WrapNavigation = state.WrapNavigation,
                Current = current
            };

            return true;
        }

        private static bool TryMapSearchingToRuntime(
            DataGridPersistedState.SearchState persisted,
            ConversionContext context,
            out DataGridSearchState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var descriptors = new List<SearchDescriptor>();
            if (persisted.Descriptors != null)
            {
                for (var i = 0; i < persisted.Descriptors.Count; i++)
                {
                    var descriptor = persisted.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    if (!TryMapValuesToRuntime(descriptor.ColumnIds, $"Search.Descriptors[{i}].ColumnIds", context, out var columnIds))
                    {
                        continue;
                    }

                    if (!TryParseCulture(descriptor.CultureName, $"Search.Descriptors[{i}].CultureName", context, out var culture))
                    {
                        continue;
                    }

                    descriptors.Add(new SearchDescriptor(
                        descriptor.Query,
                        descriptor.MatchMode,
                        descriptor.TermMode,
                        descriptor.Scope,
                        columnIds,
                        descriptor.Comparison,
                        culture,
                        descriptor.WholeWord,
                        descriptor.NormalizeWhitespace,
                        descriptor.IgnoreDiacritics,
                        descriptor.AllowEmpty));
                }
            }

            DataGridSearchCurrentState current = null;
            if (persisted.Current != null)
            {
                if (TryMapValueToRuntime(persisted.Current.ItemKey, "Search.Current.ItemKey", context, out var itemKey)
                    && TryMapValueToRuntime(persisted.Current.ColumnKey, "Search.Current.ColumnKey", context, out var columnKey))
                {
                    current = new DataGridSearchCurrentState
                    {
                        CurrentIndex = persisted.Current.CurrentIndex,
                        ItemKey = itemKey,
                        ColumnKey = columnKey,
                        RowIndex = persisted.Current.RowIndex,
                        ColumnIndex = persisted.Current.ColumnIndex
                    };
                }
            }

            state = new DataGridSearchState
            {
                Descriptors = descriptors,
                HighlightMode = persisted.HighlightMode,
                HighlightCurrent = persisted.HighlightCurrent,
                UpdateSelectionOnNavigate = persisted.UpdateSelectionOnNavigate,
                WrapNavigation = persisted.WrapNavigation,
                Current = current
            };

            return true;
        }

        private static bool TryMapConditionalFormattingToPersisted(
            DataGridConditionalFormattingState state,
            ConversionContext context,
            out DataGridPersistedState.ConditionalFormattingState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var descriptors = new List<DataGridPersistedState.ConditionalFormattingDescriptorState>();
            if (state.Descriptors != null)
            {
                for (var i = 0; i < state.Descriptors.Count; i++)
                {
                    var descriptor = state.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    string predicateToken = null;
                    if (descriptor.Predicate != null
                        && !context.TryGetConditionalFormattingPredicateToken(
                            descriptor,
                            $"ConditionalFormatting.Descriptors[{i}].Predicate",
                            out predicateToken))
                    {
                        continue;
                    }

                    string themeToken = null;
                    if (descriptor.Theme != null
                        && !context.TryGetConditionalFormattingThemeToken(
                            descriptor,
                            $"ConditionalFormatting.Descriptors[{i}].Theme",
                            out themeToken))
                    {
                        continue;
                    }

                    if (descriptor.Operator == ConditionalFormattingOperator.Custom
                        && string.IsNullOrEmpty(predicateToken))
                    {
                        if (!context.Unsupported(
                                $"ConditionalFormatting.Descriptors[{i}].Operator",
                                "Custom conditional formatting operators require a predicate token."))
                        {
                            continue;
                        }
                    }

                    if (!TryMapValueToPersisted(descriptor.RuleId, $"ConditionalFormatting.Descriptors[{i}].RuleId", context, out var ruleId))
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(descriptor.ColumnId, $"ConditionalFormatting.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(descriptor.Value, $"ConditionalFormatting.Descriptors[{i}].Value", context, out var value))
                    {
                        continue;
                    }

                    if (!TryMapValuesToPersisted(descriptor.Values, $"ConditionalFormatting.Descriptors[{i}].Values", context, out var values))
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(descriptor.ThemeKey, $"ConditionalFormatting.Descriptors[{i}].ThemeKey", context, out var themeKey))
                    {
                        continue;
                    }

                    descriptors.Add(new DataGridPersistedState.ConditionalFormattingDescriptorState
                    {
                        RuleId = ruleId,
                        ColumnId = columnId,
                        PropertyPath = descriptor.PropertyPath,
                        Operator = descriptor.Operator,
                        Value = value,
                        Values = values,
                        ThemeKey = themeKey,
                        Target = descriptor.Target,
                        ValueSource = descriptor.ValueSource,
                        StopIfTrue = descriptor.StopIfTrue,
                        Priority = descriptor.Priority,
                        CultureName = descriptor.Culture?.Name,
                        StringComparisonMode = descriptor.StringComparisonMode,
                        PredicateToken = predicateToken,
                        ThemeToken = themeToken
                    });
                }
            }

            persisted = new DataGridPersistedState.ConditionalFormattingState
            {
                Descriptors = descriptors
            };

            return true;
        }

        private static bool TryMapConditionalFormattingToRuntime(
            DataGridPersistedState.ConditionalFormattingState persisted,
            ConversionContext context,
            out DataGridConditionalFormattingState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var descriptors = new List<ConditionalFormattingDescriptor>();
            if (persisted.Descriptors != null)
            {
                for (var i = 0; i < persisted.Descriptors.Count; i++)
                {
                    var descriptor = persisted.Descriptors[i];
                    if (descriptor == null)
                    {
                        continue;
                    }

                    if (!context.TryResolveConditionalFormattingPredicate(
                            descriptor.PredicateToken,
                            $"ConditionalFormatting.Descriptors[{i}].PredicateToken",
                            out var predicate))
                    {
                        continue;
                    }

                    if (!context.TryResolveConditionalFormattingTheme(
                            descriptor.ThemeToken,
                            $"ConditionalFormatting.Descriptors[{i}].ThemeToken",
                            out var theme))
                    {
                        continue;
                    }

                    if (descriptor.Operator == ConditionalFormattingOperator.Custom
                        && predicate == null)
                    {
                        if (!context.Unsupported(
                                $"ConditionalFormatting.Descriptors[{i}].Operator",
                                "Custom conditional formatting operators require a predicate token resolver."))
                        {
                            continue;
                        }
                    }

                    if (!TryMapValueToRuntime(descriptor.RuleId, $"ConditionalFormatting.Descriptors[{i}].RuleId", context, out var ruleId))
                    {
                        continue;
                    }

                    if (ruleId == null)
                    {
                        if (!context.Unsupported($"ConditionalFormatting.Descriptors[{i}].RuleId", "Rule id cannot be null."))
                        {
                            continue;
                        }
                    }

                    if (!TryMapValueToRuntime(descriptor.ColumnId, $"ConditionalFormatting.Descriptors[{i}].ColumnId", context, out var columnId))
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(descriptor.Value, $"ConditionalFormatting.Descriptors[{i}].Value", context, out var value))
                    {
                        continue;
                    }

                    if (!TryMapValuesToRuntime(descriptor.Values, $"ConditionalFormatting.Descriptors[{i}].Values", context, out var values))
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(descriptor.ThemeKey, $"ConditionalFormatting.Descriptors[{i}].ThemeKey", context, out var themeKey))
                    {
                        continue;
                    }

                    if (!TryParseCulture(descriptor.CultureName, $"ConditionalFormatting.Descriptors[{i}].CultureName", context, out var culture))
                    {
                        continue;
                    }

                    descriptors.Add(new ConditionalFormattingDescriptor(
                        ruleId,
                        descriptor.Operator,
                        columnId,
                        descriptor.PropertyPath,
                        value,
                        values,
                        predicate: predicate,
                        theme: theme,
                        themeKey: themeKey,
                        target: descriptor.Target,
                        valueSource: descriptor.ValueSource,
                        stopIfTrue: descriptor.StopIfTrue,
                        priority: descriptor.Priority,
                        culture: culture,
                        stringComparison: descriptor.StringComparisonMode));
                }
            }

            state = new DataGridConditionalFormattingState
            {
                Descriptors = descriptors
            };

            return true;
        }

        private static bool TryMapColumnsToPersisted(
            DataGridColumnLayoutState state,
            ConversionContext context,
            out DataGridPersistedState.ColumnLayoutState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var columns = new List<DataGridPersistedState.ColumnState>();
            if (state.Columns != null)
            {
                for (var i = 0; i < state.Columns.Count; i++)
                {
                    var column = state.Columns[i];
                    if (column == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToPersisted(column.ColumnKey, $"Columns.Columns[{i}].ColumnKey", context, out var columnKey))
                    {
                        continue;
                    }

                    columns.Add(new DataGridPersistedState.ColumnState
                    {
                        ColumnKey = columnKey,
                        DisplayIndex = column.DisplayIndex,
                        IsVisible = column.IsVisible,
                        Width = new DataGridPersistedState.DataGridLengthValue
                        {
                            Value = column.Width.Value,
                            UnitType = column.Width.UnitType
                        },
                        MinWidth = column.MinWidth,
                        MaxWidth = column.MaxWidth
                    });
                }
            }

            persisted = new DataGridPersistedState.ColumnLayoutState
            {
                Columns = columns,
                FrozenColumnCount = state.FrozenColumnCount,
                FrozenColumnCountRight = state.FrozenColumnCountRight
            };

            return true;
        }

        private static bool TryMapColumnsToRuntime(
            DataGridPersistedState.ColumnLayoutState persisted,
            ConversionContext context,
            out DataGridColumnLayoutState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var columns = new List<DataGridColumnState>();
            if (persisted.Columns != null)
            {
                for (var i = 0; i < persisted.Columns.Count; i++)
                {
                    var column = persisted.Columns[i];
                    if (column == null)
                    {
                        continue;
                    }

                    if (!TryMapValueToRuntime(column.ColumnKey, $"Columns.Columns[{i}].ColumnKey", context, out var columnKey))
                    {
                        continue;
                    }

                    if (column.Width == null)
                    {
                        if (!context.Unsupported($"Columns.Columns[{i}].Width", "Column width payload cannot be null."))
                        {
                            continue;
                        }
                    }

                    columns.Add(new DataGridColumnState
                    {
                        ColumnKey = columnKey,
                        DisplayIndex = column.DisplayIndex,
                        IsVisible = column.IsVisible,
                        Width = new DataGridLength(column.Width.Value, column.Width.UnitType),
                        MinWidth = column.MinWidth,
                        MaxWidth = column.MaxWidth
                    });
                }
            }

            state = new DataGridColumnLayoutState
            {
                Columns = columns,
                FrozenColumnCount = persisted.FrozenColumnCount,
                FrozenColumnCountRight = persisted.FrozenColumnCountRight
            };

            return true;
        }

        private static bool TryMapGroupingToPersisted(
            DataGridGroupingState state,
            ConversionContext context,
            out DataGridPersistedState.GroupingState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            var descriptions = new List<DataGridPersistedState.GroupDescriptionState>();
            if (state.GroupDescriptions != null)
            {
                for (var i = 0; i < state.GroupDescriptions.Count; i++)
                {
                    var description = state.GroupDescriptions[i];
                    if (description == null)
                    {
                        continue;
                    }

                    if (description is not DataGridPathGroupDescription pathDescription)
                    {
                        if (!context.Unsupported(
                                $"Grouping.GroupDescriptions[{i}]",
                                $"Unsupported group description type '{description.GetType().FullName}'."))
                        {
                            continue;
                        }

                        return false;
                    }

                    if (pathDescription.ValueConverter != null)
                    {
                        if (!context.TryGetGroupingValueConverterToken(
                                pathDescription.ValueConverter,
                                $"Grouping.GroupDescriptions[{i}].ValueConverter",
                                out var valueConverterToken))
                        {
                            continue;
                        }

                        if (!TryMapValuesToPersisted(pathDescription.GroupKeys, $"Grouping.GroupDescriptions[{i}].GroupKeys", context, out var descriptorGroupKeys))
                        {
                            continue;
                        }

                        descriptions.Add(new DataGridPersistedState.GroupDescriptionState
                        {
                            Kind = "Path",
                            PropertyPath = pathDescription.PropertyName,
                            GroupKeys = descriptorGroupKeys,
                            ValueConverterToken = valueConverterToken
                        });

                        continue;
                    }

                    if (!TryMapValuesToPersisted(pathDescription.GroupKeys, $"Grouping.GroupDescriptions[{i}].GroupKeys", context, out var groupKeys))
                    {
                        continue;
                    }

                    descriptions.Add(new DataGridPersistedState.GroupDescriptionState
                    {
                        Kind = "Path",
                        PropertyPath = pathDescription.PropertyName,
                        GroupKeys = groupKeys,
                        ValueConverterToken = null
                    });
                }
            }

            var groupStates = new List<DataGridPersistedState.GroupState>();
            if (state.GroupStates != null)
            {
                for (var i = 0; i < state.GroupStates.Count; i++)
                {
                    var groupState = state.GroupStates[i];
                    if (groupState == null)
                    {
                        continue;
                    }

                    if (!TryMapValuesToPersisted(groupState.PathKeys, $"Grouping.GroupStates[{i}].PathKeys", context, out var pathKeys))
                    {
                        continue;
                    }

                    groupStates.Add(new DataGridPersistedState.GroupState
                    {
                        PathKeys = pathKeys,
                        IsExpanded = groupState.IsExpanded
                    });
                }
            }

            persisted = new DataGridPersistedState.GroupingState
            {
                GroupDescriptions = descriptions,
                GroupStates = groupStates
            };

            return true;
        }

        private static bool TryMapGroupingToRuntime(
            DataGridPersistedState.GroupingState persisted,
            ConversionContext context,
            out DataGridGroupingState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            var descriptions = new List<DataGridGroupDescription>();
            if (persisted.GroupDescriptions != null)
            {
                for (var i = 0; i < persisted.GroupDescriptions.Count; i++)
                {
                    var description = persisted.GroupDescriptions[i];
                    if (description == null)
                    {
                        continue;
                    }

                    if (!string.Equals(description.Kind, "Path", StringComparison.Ordinal))
                    {
                        if (!context.Unsupported(
                                $"Grouping.GroupDescriptions[{i}].Kind",
                                $"Unknown group description kind '{description.Kind}'."))
                        {
                            continue;
                        }
                    }

                    var runtimeDescription = new DataGridPathGroupDescription(description.PropertyPath);
                    if (!context.TryResolveGroupingValueConverter(
                            description.ValueConverterToken,
                            $"Grouping.GroupDescriptions[{i}].ValueConverterToken",
                            out var valueConverter))
                    {
                        continue;
                    }

                    if (!TryMapValuesToRuntime(description.GroupKeys, $"Grouping.GroupDescriptions[{i}].GroupKeys", context, out var groupKeys))
                    {
                        continue;
                    }

                    runtimeDescription.ValueConverter = valueConverter;
                    foreach (var key in groupKeys)
                    {
                        runtimeDescription.GroupKeys.Add(key);
                    }

                    descriptions.Add(runtimeDescription);
                }
            }

            var groupStates = new List<DataGridGroupState>();
            if (persisted.GroupStates != null)
            {
                for (var i = 0; i < persisted.GroupStates.Count; i++)
                {
                    var groupState = persisted.GroupStates[i];
                    if (groupState == null)
                    {
                        continue;
                    }

                    if (!TryMapValuesToRuntime(groupState.PathKeys, $"Grouping.GroupStates[{i}].PathKeys", context, out var pathKeys))
                    {
                        continue;
                    }

                    groupStates.Add(new DataGridGroupState
                    {
                        PathKeys = pathKeys,
                        IsExpanded = groupState.IsExpanded
                    });
                }
            }

            state = new DataGridGroupingState
            {
                GroupDescriptions = descriptions,
                GroupStates = groupStates
            };

            return true;
        }

        private static bool TryMapHierarchicalToPersisted(
            DataGridHierarchicalState state,
            ConversionContext context,
            out DataGridPersistedState.HierarchicalState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            if (!TryMapValuesToPersisted(state.ExpandedKeys, "Hierarchical.ExpandedKeys", context, out var expandedKeys))
            {
                return false;
            }

            persisted = new DataGridPersistedState.HierarchicalState
            {
                ExpandedKeys = expandedKeys,
                KeyMode = state.KeyMode
            };

            return true;
        }

        private static bool TryMapHierarchicalToRuntime(
            DataGridPersistedState.HierarchicalState persisted,
            ConversionContext context,
            out DataGridHierarchicalState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            if (!TryMapValuesToRuntime(persisted.ExpandedKeys, "Hierarchical.ExpandedKeys", context, out var expandedKeys))
            {
                return false;
            }

            state = new DataGridHierarchicalState
            {
                ExpandedKeys = expandedKeys,
                KeyMode = persisted.KeyMode
            };

            return true;
        }

        private static bool TryMapCellToPersisted(
            DataGridCellState state,
            string path,
            ConversionContext context,
            out DataGridPersistedState.CellState persisted)
        {
            persisted = null;
            if (state == null)
            {
                return false;
            }

            if (!TryMapValueToPersisted(state.ItemKey, $"{path}.ItemKey", context, out var itemKey))
            {
                return false;
            }

            if (!TryMapValueToPersisted(state.ColumnKey, $"{path}.ColumnKey", context, out var columnKey))
            {
                return false;
            }

            persisted = new DataGridPersistedState.CellState
            {
                ItemKey = itemKey,
                ColumnKey = columnKey,
                RowIndex = state.RowIndex,
                ColumnIndex = state.ColumnIndex
            };

            return true;
        }

        private static bool TryMapCellToRuntime(
            DataGridPersistedState.CellState persisted,
            string path,
            ConversionContext context,
            out DataGridCellState state)
        {
            state = null;
            if (persisted == null)
            {
                return false;
            }

            if (!TryMapValueToRuntime(persisted.ItemKey, $"{path}.ItemKey", context, out var itemKey))
            {
                return false;
            }

            if (!TryMapValueToRuntime(persisted.ColumnKey, $"{path}.ColumnKey", context, out var columnKey))
            {
                return false;
            }

            state = new DataGridCellState
            {
                ItemKey = itemKey,
                ColumnKey = columnKey,
                RowIndex = persisted.RowIndex,
                ColumnIndex = persisted.ColumnIndex
            };

            return true;
        }

        private static bool TryMapValuesToPersisted(
            IReadOnlyList<object> values,
            string path,
            ConversionContext context,
            out IReadOnlyList<DataGridPersistedState.PersistedValue> persistedValues)
        {
            persistedValues = null;
            if (values == null)
            {
                return true;
            }

            var list = new List<DataGridPersistedState.PersistedValue>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                if (!TryMapValueToPersisted(values[i], $"{path}[{i}]", context, out var value))
                {
                    return false;
                }

                list.Add(value);
            }

            persistedValues = list;
            return true;
        }

        private static bool TryMapValuesToPersisted(
            AvaloniaList<object> values,
            string path,
            ConversionContext context,
            out IReadOnlyList<DataGridPersistedState.PersistedValue> persistedValues)
        {
            persistedValues = null;
            if (values == null)
            {
                return true;
            }

            var list = new List<DataGridPersistedState.PersistedValue>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                if (!TryMapValueToPersisted(values[i], $"{path}[{i}]", context, out var value))
                {
                    return false;
                }

                list.Add(value);
            }

            persistedValues = list;
            return true;
        }

        private static bool TryMapValuesToRuntime(
            IReadOnlyList<DataGridPersistedState.PersistedValue> persistedValues,
            string path,
            ConversionContext context,
            out List<object> values)
        {
            values = null;
            if (persistedValues == null)
            {
                return true;
            }

            var list = new List<object>(persistedValues.Count);
            for (var i = 0; i < persistedValues.Count; i++)
            {
                if (!TryMapValueToRuntime(persistedValues[i], $"{path}[{i}]", context, out var value))
                {
                    return false;
                }

                list.Add(value);
            }

            values = list;
            return true;
        }

        private static bool TryMapValueToPersisted(
            object value,
            string path,
            ConversionContext context,
            out DataGridPersistedState.PersistedValue persisted)
        {
            if (DataGridStatePersistenceValueConverter.TryWriteValue(value, out persisted, out var reason))
            {
                return true;
            }

            return context.Unsupported(path, reason);
        }

        private static bool TryMapValueToRuntime(
            DataGridPersistedState.PersistedValue persisted,
            string path,
            ConversionContext context,
            out object value)
        {
            if (DataGridStatePersistenceValueConverter.TryReadValue(persisted, out value, out var reason))
            {
                return true;
            }

            return context.Unsupported(path, reason);
        }

        private static bool TryParseCulture(
            string cultureName,
            string path,
            ConversionContext context,
            out CultureInfo culture)
        {
            culture = null;
            if (cultureName == null)
            {
                return true;
            }

            if (cultureName.Length == 0)
            {
                culture = CultureInfo.InvariantCulture;
                return true;
            }

            try
            {
                culture = new CultureInfo(cultureName);
                return true;
            }
            catch (CultureNotFoundException ex)
            {
                return context.Unsupported(path, ex.Message);
            }
        }

        private static bool HasSection(DataGridStateSections sections, DataGridStateSections section)
        {
            return (sections & section) != 0;
        }

        private static DataGridStateSections ResolveTargetSections(
            DataGridPersistedState persisted,
            DataGridStateSections requestedSections)
        {
            if (requestedSections != DataGridStateSections.All)
            {
                return requestedSections;
            }

            if (persisted.Sections != DataGridStateSections.None)
            {
                return persisted.Sections;
            }

            return InferSectionsFromPayload(persisted);
        }

        private static DataGridStateSections InferSectionsFromPayload(DataGridPersistedState persisted)
        {
            var sections = DataGridStateSections.None;

            if (persisted.Columns != null)
            {
                sections |= DataGridStateSections.Columns;
            }

            if (persisted.Sorting != null)
            {
                sections |= DataGridStateSections.Sorting;
            }

            if (persisted.Filtering != null)
            {
                sections |= DataGridStateSections.Filtering;
            }

            if (persisted.Search != null)
            {
                sections |= DataGridStateSections.Searching;
            }

            if (persisted.ConditionalFormatting != null)
            {
                sections |= DataGridStateSections.ConditionalFormatting;
            }

            if (persisted.Grouping != null)
            {
                sections |= DataGridStateSections.Grouping;
            }

            if (persisted.Hierarchical != null)
            {
                sections |= DataGridStateSections.Hierarchical;
            }

            if (persisted.Selection != null)
            {
                sections |= DataGridStateSections.Selection;
            }

            if (persisted.Scroll != null)
            {
                sections |= DataGridStateSections.Scroll;
            }

            return sections;
        }

        private sealed class ConversionContext
        {
            private readonly DataGridStatePersistenceUnsupportedBehavior _behavior;
            private readonly IDataGridStatePersistenceTokenProvider _tokenProvider;
            private readonly IDataGridStatePersistenceTokenResolver _tokenResolver;

            public ConversionContext(DataGridStatePersistenceOptions options)
            {
                _behavior = options?.UnsupportedBehavior ?? DataGridStatePersistenceUnsupportedBehavior.Throw;
                _tokenProvider = options?.TokenProvider;
                _tokenResolver = options?.TokenResolver;
            }

            public bool Unsupported(string path, string reason)
            {
                if (_behavior == DataGridStatePersistenceUnsupportedBehavior.Throw)
                {
                    throw new DataGridStatePersistenceException($"{path}: {reason}");
                }

                return false;
            }

            public bool TryGetSortingComparerToken(SortingDescriptor descriptor, string path, out string token)
            {
                token = null;
                if (descriptor.Comparer == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetSortingComparerToken(descriptor, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return Unsupported(path, "Sorting comparer is runtime-only and no token provider mapping exists.");
            }

            public bool TryGetFilteringPredicateToken(FilteringDescriptor descriptor, string path, out string token)
            {
                token = null;
                if (descriptor.Predicate == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetFilteringPredicateToken(descriptor, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return Unsupported(path, "Filtering predicate is runtime-only and no token provider mapping exists.");
            }

            public bool TryGetFilteringValueToken(FilteringDescriptor descriptor, out string token)
            {
                token = null;
                if (descriptor.Value == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetFilteringValueToken(descriptor, descriptor.Value, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return false;
            }

            public bool TryGetFilteringValuesTokens(FilteringDescriptor descriptor, out string[] tokens)
            {
                tokens = null;
                if (descriptor.Values == null || descriptor.Values.Count == 0)
                {
                    return true;
                }

                if (_tokenProvider == null)
                {
                    return false;
                }

                var ret = new string[descriptor.Values.Count];

                for (var x = 0;x < ret.Length;x++)
                {
                    if (!_tokenProvider.TryGetFilteringValueToken(descriptor, descriptor.Values[x], out ret[x]))
                    {
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(ret[x]))
                    {
                        return false;
                    }
                }

                tokens = ret;

                return true;
            }

            public bool TryGetConditionalFormattingPredicateToken(
                ConditionalFormattingDescriptor descriptor,
                string path,
                out string token)
            {
                token = null;
                if (descriptor.Predicate == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetConditionalFormattingPredicateToken(descriptor, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return Unsupported(path, "Conditional formatting predicate is runtime-only and no token provider mapping exists.");
            }

            public bool TryGetConditionalFormattingThemeToken(ConditionalFormattingDescriptor descriptor, string path, out string token)
            {
                token = null;
                if (descriptor.Theme == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetConditionalFormattingThemeToken(descriptor, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return Unsupported(path, "Conditional formatting theme is runtime-only and no token provider mapping exists.");
            }

            public bool TryGetGroupingValueConverterToken(IValueConverter valueConverter, string path, out string token)
            {
                token = null;
                if (valueConverter == null)
                {
                    return true;
                }

                if (_tokenProvider != null
                    && _tokenProvider.TryGetGroupingValueConverterToken(valueConverter, out token)
                    && !string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                return Unsupported(path, "Grouping value converter is runtime-only and no token provider mapping exists.");
            }

            public bool TryResolveSortingComparer(string token, string path, out IComparer comparer)
            {
                comparer = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveSortingComparer(token, out comparer)
                    && comparer != null)
                {
                    return true;
                }

                return Unsupported(path, $"Cannot resolve sorting comparer token '{token}'.");
            }

            public bool TryResolveFilteringPredicate(string token, string path, object value, List<object> values, out Func<object, bool> predicate)
            {
                predicate = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveFilteringPredicate(token, value, values, out predicate)
                    && predicate != null)
                {
                    return true;
                }

                return Unsupported(path, $"Cannot resolve filtering predicate token '{token}'.");
            }

            public bool TryResolveFilteringValue(string token, out object value)
            {
                value = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveFilteringValue(token, out value)
                    && value != null)
                {
                    return true;
                }

                return false;
            }

            public bool TryResolveFilteringValues(string[] tokens, out List<object> values)
            {
                values = null;

                if (_tokenResolver == null)
                {
                    return false;
                }

                if (tokens == null || tokens.Length == 0)
                {
                    values = [];
                    return true;
                }

                var ret = new List<object>(tokens.Length);

                for (var x = 0;x < tokens.Length; x++)
                {
                    if (string.IsNullOrWhiteSpace(tokens[x]))
                    {
                        return false;
                    }

                    if (!_tokenResolver.TryResolveFilteringValue(tokens[x], out var value) || value == null)
                    {
                        return false;
                    }

                    ret.Add(value);
                }

                values = ret;

                return true;
            }

            public bool TryResolveConditionalFormattingPredicate(
                string token,
                string path,
                out Func<ConditionalFormattingContext, bool> predicate)
            {
                predicate = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveConditionalFormattingPredicate(token, out predicate)
                    && predicate != null)
                {
                    return true;
                }

                return Unsupported(path, $"Cannot resolve conditional formatting predicate token '{token}'.");
            }

            public bool TryResolveConditionalFormattingTheme(string token, string path, out ControlTheme theme)
            {
                theme = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveConditionalFormattingTheme(token, out theme)
                    && theme != null)
                {
                    return true;
                }

                return Unsupported(path, $"Cannot resolve conditional formatting theme token '{token}'.");
            }

            public bool TryResolveGroupingValueConverter(string token, string path, out IValueConverter valueConverter)
            {
                valueConverter = null;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }

                if (_tokenResolver != null
                    && _tokenResolver.TryResolveGroupingValueConverter(token, out valueConverter)
                    && valueConverter != null)
                {
                    return true;
                }

                return Unsupported(path, $"Cannot resolve grouping value converter token '{token}'.");
            }
        }
    }
}
