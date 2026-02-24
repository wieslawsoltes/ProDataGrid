// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.State;

public class DataGridStatePersistenceTests
{
    [AvaloniaFact]
    public void SerializeAndRestoreState_RoundTrips_WithBuiltInSerializer()
    {
        var items = StateTestHelper.CreateItems(40);
        var (grid, root) = StateTestHelper.CreateGrid(items, width: 360, height: 140);

        try
        {
            var idColumn = grid.ColumnsInternal[0];
            var nameColumn = grid.ColumnsInternal[1];
            var categoryColumn = grid.ColumnsInternal[2];

            nameColumn.DisplayIndex = 0;
            idColumn.DisplayIndex = 1;
            categoryColumn.IsVisible = false;
            nameColumn.Width = new DataGridLength(180);
            grid.FrozenColumnCount = 1;

            grid.SortingModel.Apply(new[]
            {
                new SortingDescriptor(nameColumn, ListSortDirection.Ascending, nameof(StateTestItem.Name)),
            });

            grid.FilteringModel.Apply(new[]
            {
                new Avalonia.Controls.DataGridFiltering.FilteringDescriptor(
                    categoryColumn,
                    Avalonia.Controls.DataGridFiltering.FilteringOperator.Equals,
                    nameof(StateTestItem.Category),
                    "A"),
            });

            grid.SearchModel.Apply(new[]
            {
                new Avalonia.Controls.DataGridSearching.SearchDescriptor(
                    "Item 1",
                    Avalonia.Controls.DataGridSearching.SearchMatchMode.Contains,
                    Avalonia.Controls.DataGridSearching.SearchTermCombineMode.Any,
                    Avalonia.Controls.DataGridSearching.SearchScope.AllColumns),
            });

            Dispatcher.UIThread.RunJobs();
            grid.SearchModel.MoveTo(0);

            grid.Selection.Select(2);
            grid.Selection.Select(3);

            grid.ScrollIntoView(items[20], nameColumn);
            grid.UpdateLayout();
            grid.UpdateHorizontalOffset(80);

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var payload = DataGridStatePersistence.SerializeStateToString(
                grid,
                DataGridStateSections.All,
                options);

            Assert.False(string.IsNullOrWhiteSpace(payload));

            grid.SortingModel.Clear();
            grid.FilteringModel.Clear();
            grid.SearchModel.Clear();
            grid.SelectedItems.Clear();
            grid.SelectedCells.Clear();
            idColumn.DisplayIndex = 0;
            nameColumn.DisplayIndex = 1;
            categoryColumn.IsVisible = true;
            grid.FrozenColumnCount = 0;
            grid.ScrollIntoView(items[0], nameColumn);
            grid.UpdateHorizontalOffset(0);
            grid.UpdateLayout();

            DataGridStatePersistence.RestoreStateFromString(
                grid,
                payload,
                DataGridStateSections.All,
                options);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, grid.FrozenColumnCount);
            Assert.False(categoryColumn.IsVisible);
            Assert.Equal(0, nameColumn.DisplayIndex);
            Assert.Single(grid.SortingModel.Descriptors);
            Assert.Single(grid.FilteringModel.Descriptors);
            Assert.Single(grid.SearchModel.Descriptors);

            var selectedIds = grid.SelectedItems.Cast<StateTestItem>()
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArray();

            Assert.Equal(new[] { 12, 14 }, selectedIds);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void SerializeAndRestoreState_RoundTrips_Filtering_ColumnDefinition()
    {
        var items = StateTestHelper.CreateItems(40);
        var (grid, root, definitions) = StateTestHelper.CreateGridWithDefinitions(items);

        try
        {
            var idColumn = definitions[0];

            grid.FilteringModel.Apply(new[]
            {
                new FilteringDescriptor(idColumn, FilteringOperator.Equals, nameof(StateTestItem.Id), 1),
            });

            Dispatcher.UIThread.RunJobs();

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var payload = DataGridStatePersistence.SerializeStateToString(
                grid,
                DataGridStateSections.Filtering,
                options);

            Assert.False(string.IsNullOrWhiteSpace(payload));

            grid.FilteringModel.Clear();

            DataGridStatePersistence.RestoreStateFromString(
                grid,
                payload,
                DataGridStateSections.All,
                options);

            Dispatcher.UIThread.RunJobs();

            Assert.Single(grid.FilteringModel.Descriptors);
            Assert.IsType<DataGridColumnDefinition>(grid.FilteringModel.Descriptors[0].ColumnId, exactMatch: false);
            Assert.Equal("Id", ((DataGridColumnDefinition)grid.FilteringModel.Descriptors[0].ColumnId).ColumnKey);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void SerializeAndRestoreState_RoundTrips_Filtering_MaterializedColumn()
    {
        var items = StateTestHelper.CreateItems(40);
        var (grid, root) = StateTestHelper.CreateGrid(items);

        try
        {
            var idColumn = grid.ColumnsInternal[0];
            grid.FilteringModel.Apply(new[]
            {
                new FilteringDescriptor(idColumn, FilteringOperator.Equals, nameof(StateTestItem.Id), 1),
            });

            Dispatcher.UIThread.RunJobs();

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var payload = DataGridStatePersistence.SerializeStateToString(
                grid,
                DataGridStateSections.Filtering,
                options);

            Assert.False(string.IsNullOrWhiteSpace(payload));

            grid.FilteringModel.Clear();

            DataGridStatePersistence.RestoreStateFromString(
                grid,
                payload,
                DataGridStateSections.All,
                options);

            Dispatcher.UIThread.RunJobs();

            Assert.Single(grid.FilteringModel.Descriptors);
            Assert.IsType<DataGridColumn>(grid.FilteringModel.Descriptors[0].ColumnId, exactMatch: false);
            Assert.Equal("Id", ((DataGridColumn)grid.FilteringModel.Descriptors[0].ColumnId).ColumnKey);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void SerializeState_Throws_ForUnsupportedRuntimeMembers_ByDefault()
    {
        var items = StateTestHelper.CreateItems(20);
        var (grid, root) = StateTestHelper.CreateGrid(items, width: 320, height: 140);

        try
        {
            var nameColumn = grid.ColumnsInternal[1];
            grid.FilteringModel.Apply(new[]
            {
                new Avalonia.Controls.DataGridFiltering.FilteringDescriptor(
                    nameColumn,
                    Avalonia.Controls.DataGridFiltering.FilteringOperator.Custom,
                    nameof(StateTestItem.Name),
                    predicate: _ => true),
            });

            var options = StateTestHelper.CreateKeyedOptions(grid, items);

            Assert.Throws<DataGridStatePersistenceException>(() =>
                DataGridStatePersistence.SerializeState(
                    grid,
                    DataGridStateSections.Filtering,
                    options));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CaptureState_SkipsUnsupportedMembers_WhenConfigured()
    {
        var items = StateTestHelper.CreateItems(20);
        var (grid, root) = StateTestHelper.CreateGrid(items, width: 320, height: 140);

        try
        {
            var nameColumn = grid.ColumnsInternal[1];
            grid.FilteringModel.Apply(new[]
            {
                new Avalonia.Controls.DataGridFiltering.FilteringDescriptor(
                    nameColumn,
                    Avalonia.Controls.DataGridFiltering.FilteringOperator.Custom,
                    nameof(StateTestItem.Name),
                    predicate: _ => true),
            });

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var persistenceOptions = new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            };

            var persisted = DataGridStatePersistence.CaptureState(
                grid,
                DataGridStateSections.Filtering,
                options,
                persistenceOptions);

            Assert.NotNull(persisted.Filtering);
            Assert.Empty(persisted.Filtering.Descriptors ?? []);
        }
        finally
        {
            root.Close();
        }
    }

    [Fact]
    public void SerializeAndDeserialize_UsesProvidedSerializer()
    {
        var tracking = new TrackingSerializer();
        var state = new DataGridPersistedState
        {
            Version = 7,
            Sections = DataGridStateSections.Selection
        };

        var bytes = DataGridStatePersistence.Serialize(state, tracking);
        var roundTrip = DataGridStatePersistence.Deserialize(bytes, tracking);
        var text = DataGridStatePersistence.SerializeToString(state, tracking);
        var roundTripFromText = DataGridStatePersistence.Deserialize(text, tracking);

        Assert.NotNull(roundTrip);
        Assert.NotNull(roundTripFromText);
        Assert.True(tracking.SerializeBytesCalled);
        Assert.True(tracking.DeserializeBytesCalled);
        Assert.True(tracking.SerializeStringCalled);
        Assert.True(tracking.DeserializeStringCalled);
    }

    [Fact]
    public void Mapper_RoundTrips_SortingComparerToken()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var registry = new TokenRegistry
        {
            SortingComparer = comparer
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Sorting,
            Sorting = new DataGridSortingState
            {
                Descriptors = new[]
                {
                    new SortingDescriptor("Name", ListSortDirection.Ascending, nameof(StateTestItem.Name), comparer)
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescending,
                OwnsViewSorts = true
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("sorting:comparer", persisted.Sorting.Descriptors[0].ComparerToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        Assert.Single(restored.Sorting.Descriptors);
        Assert.Same(comparer, restored.Sorting.Descriptors[0].Comparer);
    }

    [Fact]
    public void Mapper_RoundTrips_FilteringPredicateToken()
    {
        Func<object, bool> predicate = _ => true;
        var registry = new TokenRegistry
        {
            FilteringPredicate = predicate
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Filtering,
            Filtering = new DataGridFilteringState
            {
                Descriptors = new[]
                {
                    new FilteringDescriptor(
                        "Name",
                        FilteringOperator.Custom,
                        nameof(StateTestItem.Name),
                        predicate: predicate)
                },
                OwnsViewFilter = true
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("filtering:predicate", persisted.Filtering.Descriptors[0].PredicateToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        Assert.Single(restored.Filtering.Descriptors);
        Assert.Equal(FilteringOperator.Custom, restored.Filtering.Descriptors[0].Operator);
        Assert.Same(predicate, restored.Filtering.Descriptors[0].Predicate);
    }

    [Fact]
    public void Mapper_RoundTrips_FilteringValueToken()
    {
        Func<object, bool> predicate = _ => true;
        object value = "filtering:value0";
        var registry = new TokenRegistry
        {
            FilteringPredicate = predicate,
            FilteringValues = [ value ]
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Filtering,
            Filtering = new DataGridFilteringState
            {
                Descriptors = new[]
                {
                    new FilteringDescriptor(
                        "Name",
                        FilteringOperator.Custom,
                        nameof(StateTestItem.Name),
                        predicate: predicate,
                        value: value)
                },
                OwnsViewFilter = true
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("filtering:predicate", persisted.Filtering.Descriptors[0].PredicateToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        Assert.Single(restored.Filtering.Descriptors);
        Assert.Equal(FilteringOperator.Custom, restored.Filtering.Descriptors[0].Operator);
        Assert.Same(value, restored.Filtering.Descriptors[0].Value);
    }

    [Fact]
    public void Mapper_RoundTrips_FilteringValuesToken()
    {
        Func<object, bool> predicate = _ => true;
        object[] values = [ "filtering:value1", "filtering:value2"];
        var registry = new TokenRegistry
        {
            FilteringPredicate = predicate,
            FilteringValues = values
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Filtering,
            Filtering = new DataGridFilteringState
            {
                Descriptors = new[]
                {
                    new FilteringDescriptor(
                        "Name",
                        FilteringOperator.Custom,
                        nameof(StateTestItem.Name),
                        predicate: predicate,
                        values: values)
                },
                OwnsViewFilter = true
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("filtering:predicate", persisted.Filtering.Descriptors[0].PredicateToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        Assert.Single(restored.Filtering.Descriptors);
        Assert.Equal(FilteringOperator.Custom, restored.Filtering.Descriptors[0].Operator);
        Assert.Same(values[0], restored.Filtering.Descriptors[0].Values[0]);
        Assert.Same(values[1], restored.Filtering.Descriptors[0].Values[1]);
    }

    [Fact]
    public void Mapper_RoundTrips_ConditionalFormattingPredicateAndThemeTokens()
    {
        Func<ConditionalFormattingContext, bool> predicate = _ => true;
        var theme = new ControlTheme(typeof(DataGridCell));
        var registry = new TokenRegistry
        {
            ConditionalFormattingPredicate = predicate,
            ConditionalFormattingTheme = theme
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.ConditionalFormatting,
            ConditionalFormatting = new DataGridConditionalFormattingState
            {
                Descriptors = new[]
                {
                    new ConditionalFormattingDescriptor(
                        ruleId: "rule-1",
                        @operator: ConditionalFormattingOperator.Custom,
                        columnId: "Category",
                        propertyPath: nameof(StateTestItem.Category),
                        predicate: predicate,
                        theme: theme,
                        themeKey: "theme-key")
                }
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("conditional:predicate", persisted.ConditionalFormatting.Descriptors[0].PredicateToken);
        Assert.Equal("conditional:theme", persisted.ConditionalFormatting.Descriptors[0].ThemeToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        Assert.Single(restored.ConditionalFormatting.Descriptors);
        Assert.Equal(ConditionalFormattingOperator.Custom, restored.ConditionalFormatting.Descriptors[0].Operator);
        Assert.Same(predicate, restored.ConditionalFormatting.Descriptors[0].Predicate);
        Assert.Same(theme, restored.ConditionalFormatting.Descriptors[0].Theme);
    }

    [Fact]
    public void Mapper_RoundTrips_GroupingValueConverterToken()
    {
        var converter = new PassThroughValueConverter();
        var groupDescription = new DataGridPathGroupDescription(nameof(StateTestItem.Group))
        {
            ValueConverter = converter
        };
        groupDescription.GroupKeys.Add("G1");

        var registry = new TokenRegistry
        {
            GroupingValueConverter = converter
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Grouping,
            Grouping = new DataGridGroupingState
            {
                GroupDescriptions = new DataGridGroupDescription[]
                {
                    groupDescription
                },
                GroupStates = new[]
                {
                    new DataGridGroupState
                    {
                        PathKeys = new object[] { "G1" },
                        IsExpanded = true
                    }
                }
            }
        };

        var options = new DataGridStatePersistenceOptions
        {
            TokenProvider = registry,
            TokenResolver = registry
        };

        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions: null, options);
        Assert.Equal("grouping:converter", persisted.Grouping.GroupDescriptions[0].ValueConverterToken);

        var restored = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            stateOptions: null,
            options);

        var restoredDescription = Assert.IsType<DataGridPathGroupDescription>(restored.Grouping.GroupDescriptions[0]);
        Assert.Same(converter, restoredDescription.ValueConverter);
        Assert.Single(restoredDescription.GroupKeys);
        Assert.Equal("G1", restoredDescription.GroupKeys[0]);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Columns()
    {
        var runtime = new DataGridState
        {
            Version = 3,
            Sections = DataGridStateSections.Columns,
            Columns = new DataGridColumnLayoutState
            {
                FrozenColumnCount = 1,
                FrozenColumnCountRight = 2,
                Columns = new DataGridColumnState[]
                {
                    new()
                    {
                        ColumnKey = "Id",
                        DisplayIndex = 1,
                        IsVisible = true,
                        Width = new DataGridLength(120, DataGridLengthUnitType.Pixel),
                        MinWidth = 30,
                        MaxWidth = 500
                    },
                    new()
                    {
                        ColumnKey = "Name",
                        DisplayIndex = 0,
                        IsVisible = false,
                        Width = new DataGridLength(2, DataGridLengthUnitType.Star),
                        MinWidth = 40,
                        MaxWidth = 600
                    }
                }
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(3, restored.Version);
        Assert.Equal(DataGridStateSections.Columns, restored.Sections);
        Assert.NotNull(restored.Columns);
        Assert.Equal(2, restored.Columns.Columns.Count);
        Assert.Equal("Id", restored.Columns.Columns[0].ColumnKey);
        Assert.Equal(DataGridLengthUnitType.Pixel, restored.Columns.Columns[0].Width.UnitType);
        Assert.Equal("Name", restored.Columns.Columns[1].ColumnKey);
        Assert.Equal(DataGridLengthUnitType.Star, restored.Columns.Columns[1].Width.UnitType);
        Assert.Equal(1, restored.Columns.FrozenColumnCount);
        Assert.Equal(2, restored.Columns.FrozenColumnCountRight);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Sorting()
    {
        var runtime = new DataGridState
        {
            Version = 4,
            Sections = DataGridStateSections.Sorting,
            Sorting = new DataGridSortingState
            {
                Descriptors = new[]
                {
                    new SortingDescriptor(
                        "Name",
                        ListSortDirection.Descending,
                        nameof(StateTestItem.Name),
                        comparer: null,
                        culture: CultureInfo.GetCultureInfo("en-US")),
                    new SortingDescriptor(
                        "Amount",
                        ListSortDirection.Ascending,
                        nameof(StateTestItem.Id))
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescendingNone,
                OwnsViewSorts = false
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Sorting, restored.Sections);
        Assert.NotNull(restored.Sorting);
        Assert.Equal(2, restored.Sorting.Descriptors.Count);
        Assert.Equal("Name", restored.Sorting.Descriptors[0].ColumnId);
        Assert.Equal(ListSortDirection.Descending, restored.Sorting.Descriptors[0].Direction);
        Assert.Equal("en-US", restored.Sorting.Descriptors[0].Culture.Name);
        Assert.Equal(SortCycleMode.AscendingDescendingNone, restored.Sorting.CycleMode);
        Assert.False(restored.Sorting.OwnsViewSorts);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Filtering()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Filtering,
            Filtering = new DataGridFilteringState
            {
                Descriptors = new[]
                {
                    new FilteringDescriptor(
                        "Category",
                        FilteringOperator.Equals,
                        nameof(StateTestItem.Category),
                        "A",
                        stringComparison: StringComparison.OrdinalIgnoreCase),
                    new FilteringDescriptor(
                        "Category",
                        FilteringOperator.In,
                        nameof(StateTestItem.Category),
                        values: new object[] { "A", "B" })
                },
                OwnsViewFilter = false
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Filtering, restored.Sections);
        Assert.NotNull(restored.Filtering);
        Assert.Equal(2, restored.Filtering.Descriptors.Count);
        Assert.Equal(FilteringOperator.Equals, restored.Filtering.Descriptors[0].Operator);
        Assert.Equal("A", restored.Filtering.Descriptors[0].Value);
        Assert.Equal(FilteringOperator.In, restored.Filtering.Descriptors[1].Operator);
        Assert.Equal(2, restored.Filtering.Descriptors[1].Values.Count);
        Assert.False(restored.Filtering.OwnsViewFilter);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Search()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Searching,
            Search = new DataGridSearchState
            {
                Descriptors = new[]
                {
                    new SearchDescriptor(
                        "Item",
                        SearchMatchMode.Contains,
                        SearchTermCombineMode.Any,
                        SearchScope.AllColumns,
                        columnIds: new object[] { "Name", "Category" },
                        comparison: StringComparison.OrdinalIgnoreCase,
                        culture: CultureInfo.GetCultureInfo("en-US"),
                        wholeWord: true,
                        normalizeWhitespace: true,
                        ignoreDiacritics: true,
                        allowEmpty: false)
                },
                HighlightMode = SearchHighlightMode.TextAndCell,
                HighlightCurrent = true,
                UpdateSelectionOnNavigate = true,
                WrapNavigation = true,
                Current = new DataGridSearchCurrentState
                {
                    CurrentIndex = 1,
                    ItemKey = 42,
                    ColumnKey = "Name",
                    RowIndex = 4,
                    ColumnIndex = 1
                }
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Searching, restored.Sections);
        Assert.NotNull(restored.Search);
        Assert.Single(restored.Search.Descriptors);
        Assert.Equal("Item", restored.Search.Descriptors[0].Query);
        Assert.Equal(2, restored.Search.Descriptors[0].ColumnIds.Count);
        Assert.Equal("en-US", restored.Search.Descriptors[0].Culture.Name);
        Assert.NotNull(restored.Search.Current);
        Assert.Equal(42, restored.Search.Current.ItemKey);
        Assert.Equal("Name", restored.Search.Current.ColumnKey);
        Assert.Equal(4, restored.Search.Current.RowIndex);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Selection()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Selection,
            Selection = new DataGridSelectionState
            {
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
                SelectedItemKeys = new object[] { 10, 20 },
                SelectedIndexes = new[] { 1, 4 },
                SelectedCells = new[]
                {
                    new DataGridCellState
                    {
                        ItemKey = 20,
                        ColumnKey = "Name",
                        RowIndex = 4,
                        ColumnIndex = 1
                    }
                },
                CurrentCell = new DataGridCellState
                {
                    ItemKey = 10,
                    ColumnKey = "Id",
                    RowIndex = 1,
                    ColumnIndex = 0
                }
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Selection, restored.Sections);
        Assert.NotNull(restored.Selection);
        Assert.Equal(DataGridSelectionMode.Extended, restored.Selection.SelectionMode);
        Assert.Equal(DataGridSelectionUnit.CellOrRowHeader, restored.Selection.SelectionUnit);
        Assert.Equal(2, restored.Selection.SelectedItemKeys.Count);
        Assert.Equal(2, restored.Selection.SelectedIndexes.Count);
        Assert.Single(restored.Selection.SelectedCells);
        Assert.NotNull(restored.Selection.CurrentCell);
        Assert.Equal(1, restored.Selection.CurrentCell.RowIndex);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Scroll()
    {
        var stateOptions = new DataGridStateOptions
        {
            ItemKeySelector = static item => item,
            ItemKeyResolver = static key => key
        };

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Scroll,
            Scroll = new DataGridScrollState
            {
                DataSource = null,
                DataSourceCount = 100,
                Samples = new[]
                {
                    new DataGridScrollSample(10, 10),
                    new DataGridScrollSample(11, 11)
                },
                FirstScrollingSlot = 10,
                NegVerticalOffset = 0.5,
                VerticalOffset = 22.75,
                HorizontalOffset = 16.25,
                RowHeightEstimatorState = null
            }
        };

        var restored = RoundTripRuntimeState(runtime, stateOptions);

        Assert.Equal(DataGridStateSections.Scroll, restored.Sections);
        Assert.NotNull(restored.Scroll);
        Assert.Equal(100, restored.Scroll.DataSourceCount);
        Assert.Equal(2, restored.Scroll.Samples.Count);
        Assert.Equal(10, restored.Scroll.Samples[0].ItemKey);
        Assert.Equal(10, restored.Scroll.FirstScrollingSlot);
        Assert.Equal(16.25, restored.Scroll.HorizontalOffset);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Grouping()
    {
        var description = new DataGridPathGroupDescription(nameof(StateTestItem.Category));
        description.GroupKeys.Add("A");
        description.GroupKeys.Add("B");

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Grouping,
            Grouping = new DataGridGroupingState
            {
                GroupDescriptions = new DataGridGroupDescription[] { description },
                GroupStates = new[]
                {
                    new DataGridGroupState
                    {
                        PathKeys = new object[] { "A" },
                        IsExpanded = true
                    }
                }
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Grouping, restored.Sections);
        Assert.NotNull(restored.Grouping);
        Assert.Single(restored.Grouping.GroupDescriptions);
        var restoredDescription = Assert.IsType<DataGridPathGroupDescription>(restored.Grouping.GroupDescriptions[0]);
        Assert.Equal(nameof(StateTestItem.Category), restoredDescription.PropertyName);
        Assert.Equal(2, restoredDescription.GroupKeys.Count);
        Assert.Single(restored.Grouping.GroupStates);
        Assert.True(restored.Grouping.GroupStates[0].IsExpanded);
    }

    [Fact]
    public void PerSectionPersistenceRoundTrip_Hierarchical()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Hierarchical,
            Hierarchical = new DataGridHierarchicalState
            {
                ExpandedKeys = new object[] { 1, 2, 3 },
                KeyMode = ExpandedStateKeyMode.Path
            }
        };

        var restored = RoundTripRuntimeState(runtime);

        Assert.Equal(DataGridStateSections.Hierarchical, restored.Sections);
        Assert.NotNull(restored.Hierarchical);
        Assert.Equal(ExpandedStateKeyMode.Path, restored.Hierarchical.KeyMode);
        Assert.Equal(3, restored.Hierarchical.ExpandedKeys.Count);
    }

    [AvaloniaFact]
    public void SerializeAndRestoreState_RoundTrips_WithBuiltInByteSerializer()
    {
        var items = StateTestHelper.CreateItems(40);
        var (grid, root) = StateTestHelper.CreateGrid(items, width: 360, height: 140);

        try
        {
            var idColumn = grid.ColumnsInternal[0];
            var nameColumn = grid.ColumnsInternal[1];
            var categoryColumn = grid.ColumnsInternal[2];

            nameColumn.DisplayIndex = 0;
            idColumn.DisplayIndex = 1;
            categoryColumn.IsVisible = false;
            grid.FrozenColumnCount = 1;

            grid.SortingModel.Apply(new[]
            {
                new SortingDescriptor(nameColumn, ListSortDirection.Ascending, nameof(StateTestItem.Name))
            });

            grid.FilteringModel.Apply(new[]
            {
                new FilteringDescriptor(
                    categoryColumn,
                    FilteringOperator.Equals,
                    nameof(StateTestItem.Category),
                    "A")
            });

            grid.Selection.Select(2);
            grid.Selection.Select(3);
            grid.ScrollIntoView(items[20], nameColumn);
            grid.UpdateLayout();
            grid.UpdateHorizontalOffset(64);

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var payload = DataGridStatePersistence.SerializeState(
                grid,
                DataGridStateSections.All,
                options);

            Assert.NotEmpty(payload);

            grid.SortingModel.Clear();
            grid.FilteringModel.Clear();
            grid.SelectedItems.Clear();
            idColumn.DisplayIndex = 0;
            nameColumn.DisplayIndex = 1;
            categoryColumn.IsVisible = true;
            grid.FrozenColumnCount = 0;
            grid.ScrollIntoView(items[0], nameColumn);
            grid.UpdateHorizontalOffset(0);
            grid.UpdateLayout();

            DataGridStatePersistence.RestoreState(
                grid,
                payload,
                DataGridStateSections.All,
                options);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, grid.FrozenColumnCount);
            Assert.False(categoryColumn.IsVisible);
            Assert.Equal(0, nameColumn.DisplayIndex);
            Assert.Single(grid.SortingModel.Descriptors);
            Assert.Single(grid.FilteringModel.Descriptors);

            var selectedIds = grid.SelectedItems.Cast<StateTestItem>()
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArray();

            Assert.Equal(new[] { 12, 14 }, selectedIds);
        }
        finally
        {
            root.Close();
        }
    }

    [Fact]
    public void VersionCompatibility_AcceptsFutureVersionAndUnknownFields()
    {
        var persisted = new DataGridPersistedState
        {
            Version = 999,
            Sections = DataGridStateSections.Sorting,
            Sorting = new DataGridPersistedState.SortingState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.SortingDescriptorState
                    {
                        ColumnId = PersistedString("Name"),
                        Direction = ListSortDirection.Ascending,
                        PropertyPath = nameof(StateTestItem.Name)
                    }
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescending,
                OwnsViewSorts = true
            }
        };

        var json = DataGridStatePersistence.SerializeToString(persisted);
        var withUnknown = json[..^1] + ",\"futureMetadata\":{\"enabled\":true}}";

        var deserialized = DataGridStatePersistence.Deserialize(withUnknown);
        var runtime = DataGridStatePersistenceMapper.ToRuntime(deserialized, DataGridStateSections.All, null, null);

        Assert.Equal(999, deserialized.Version);
        Assert.Equal(999, runtime.Version);
        Assert.Equal(DataGridStateSections.Sorting, runtime.Sections);
        Assert.Single(runtime.Sorting.Descriptors);
    }

    [Fact]
    public void VersionCompatibility_AcceptsLegacyVersionPayload()
    {
        var persisted = new DataGridPersistedState
        {
            Version = 0,
            Sections = DataGridStateSections.Filtering,
            Filtering = new DataGridPersistedState.FilteringState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.FilteringDescriptorState
                    {
                        ColumnId = PersistedString("Category"),
                        Operator = FilteringOperator.Equals,
                        PropertyPath = nameof(StateTestItem.Category),
                        Value = PersistedString("A"),
                        StringComparisonMode = StringComparison.Ordinal
                    }
                },
                OwnsViewFilter = true
            }
        };

        var payload = DataGridStatePersistence.Serialize(persisted);
        var deserialized = DataGridStatePersistence.Deserialize(payload);
        var runtime = DataGridStatePersistenceMapper.ToRuntime(deserialized, DataGridStateSections.All, null, null);

        Assert.Equal(0, deserialized.Version);
        Assert.Equal(0, runtime.Version);
        Assert.Equal(DataGridStateSections.Filtering, runtime.Sections);
        Assert.Single(runtime.Filtering.Descriptors);
        Assert.Equal(FilteringOperator.Equals, runtime.Filtering.Descriptors[0].Operator);
    }

    [Fact]
    public void VersionCompatibility_InfersSections_WhenMissingInPayload()
    {
        var persisted = new DataGridPersistedState
        {
            Version = 1,
            Sections = DataGridStateSections.None,
            Sorting = new DataGridPersistedState.SortingState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.SortingDescriptorState
                    {
                        ColumnId = PersistedString("Name"),
                        Direction = ListSortDirection.Ascending,
                        PropertyPath = nameof(StateTestItem.Name)
                    }
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescending,
                OwnsViewSorts = true
            }
        };

        var payload = DataGridStatePersistence.SerializeToString(persisted);
        var runtime = DataGridStatePersistenceMapper.ToRuntime(
            DataGridStatePersistence.Deserialize(payload),
            DataGridStateSections.All,
            null,
            null);

        Assert.Equal(DataGridStateSections.Sorting, runtime.Sections);
        Assert.NotNull(runtime.Sorting);
        Assert.Single(runtime.Sorting.Descriptors);
    }

    [Fact]
    public void UnsupportedMatrix_Comparer_ThrowAndSkip()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Sorting,
            Sorting = new DataGridSortingState
            {
                Descriptors = new[]
                {
                    new SortingDescriptor(
                        "Name",
                        ListSortDirection.Ascending,
                        nameof(StateTestItem.Name),
                        StringComparer.Ordinal)
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescending,
                OwnsViewSorts = true
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToPersisted(runtime, null, null));

        var skipped = DataGridStatePersistenceMapper.ToPersisted(
            runtime,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(skipped.Sorting);
        Assert.Empty(skipped.Sorting.Descriptors);
    }

    [Fact]
    public void UnsupportedMatrix_Theme_ThrowAndSkip()
    {
        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.ConditionalFormatting,
            ConditionalFormatting = new DataGridConditionalFormattingState
            {
                Descriptors = new[]
                {
                    new ConditionalFormattingDescriptor(
                        ruleId: "rule-1",
                        @operator: ConditionalFormattingOperator.Equals,
                        columnId: "Category",
                        propertyPath: nameof(StateTestItem.Category),
                        value: "A",
                        theme: new ControlTheme(typeof(DataGridCell)),
                        themeKey: "theme-a")
                }
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToPersisted(runtime, null, null));

        var skipped = DataGridStatePersistenceMapper.ToPersisted(
            runtime,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(skipped.ConditionalFormatting);
        Assert.Empty(skipped.ConditionalFormatting.Descriptors);
    }

    [Fact]
    public void UnsupportedMatrix_Converter_ThrowAndSkip()
    {
        var groupDescription = new DataGridPathGroupDescription(nameof(StateTestItem.Category))
        {
            ValueConverter = new PassThroughValueConverter()
        };
        groupDescription.GroupKeys.Add("A");

        var runtime = new DataGridState
        {
            Sections = DataGridStateSections.Grouping,
            Grouping = new DataGridGroupingState
            {
                GroupDescriptions = new DataGridGroupDescription[] { groupDescription },
                GroupStates = Array.Empty<DataGridGroupState>()
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToPersisted(runtime, null, null));

        var skipped = DataGridStatePersistenceMapper.ToPersisted(
            runtime,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(skipped.Grouping);
        Assert.Empty(skipped.Grouping.GroupDescriptions);
    }

    [Fact]
    public void UnsupportedMatrix_UnresolvedComparerToken_ThrowAndSkip()
    {
        var persisted = new DataGridPersistedState
        {
            Sections = DataGridStateSections.Sorting,
            Sorting = new DataGridPersistedState.SortingState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.SortingDescriptorState
                    {
                        ColumnId = PersistedString("Name"),
                        Direction = ListSortDirection.Ascending,
                        PropertyPath = nameof(StateTestItem.Name),
                        ComparerToken = "missing-comparer"
                    }
                },
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescending,
                OwnsViewSorts = true
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToRuntime(persisted, DataGridStateSections.All, null, null));

        var runtime = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(runtime.Sorting);
        Assert.Empty(runtime.Sorting.Descriptors);
    }

    [Fact]
    public void UnsupportedMatrix_UnresolvedThemeToken_ThrowAndSkip()
    {
        var persisted = new DataGridPersistedState
        {
            Sections = DataGridStateSections.ConditionalFormatting,
            ConditionalFormatting = new DataGridPersistedState.ConditionalFormattingState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.ConditionalFormattingDescriptorState
                    {
                        RuleId = PersistedString("rule-1"),
                        ColumnId = PersistedString("Category"),
                        PropertyPath = nameof(StateTestItem.Category),
                        Operator = ConditionalFormattingOperator.Equals,
                        Value = PersistedString("A"),
                        ThemeKey = PersistedString("theme-a"),
                        Target = ConditionalFormattingTarget.Cell,
                        ValueSource = ConditionalFormattingValueSource.Cell,
                        StopIfTrue = true,
                        Priority = 0,
                        ThemeToken = "missing-theme"
                    }
                }
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToRuntime(persisted, DataGridStateSections.All, null, null));

        var runtime = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(runtime.ConditionalFormatting);
        Assert.Empty(runtime.ConditionalFormatting.Descriptors);
    }

    [Fact]
    public void UnsupportedMatrix_UnresolvedConverterToken_ThrowAndSkip()
    {
        var persisted = new DataGridPersistedState
        {
            Sections = DataGridStateSections.Grouping,
            Grouping = new DataGridPersistedState.GroupingState
            {
                GroupDescriptions = new[]
                {
                    new DataGridPersistedState.GroupDescriptionState
                    {
                        Kind = "Path",
                        PropertyPath = nameof(StateTestItem.Category),
                        GroupKeys = new[] { PersistedString("A") },
                        ValueConverterToken = "missing-converter"
                    }
                },
                GroupStates = Array.Empty<DataGridPersistedState.GroupState>()
            }
        };

        Assert.Throws<DataGridStatePersistenceException>(() =>
            DataGridStatePersistenceMapper.ToRuntime(persisted, DataGridStateSections.All, null, null));

        var runtime = DataGridStatePersistenceMapper.ToRuntime(
            persisted,
            DataGridStateSections.All,
            null,
            new DataGridStatePersistenceOptions
            {
                UnsupportedBehavior = DataGridStatePersistenceUnsupportedBehavior.Skip
            });

        Assert.NotNull(runtime.Grouping);
        Assert.Empty(runtime.Grouping.GroupDescriptions);
    }

    private static DataGridState RoundTripRuntimeState(
        DataGridState runtime,
        DataGridStateOptions stateOptions = null,
        DataGridStatePersistenceOptions persistenceOptions = null)
    {
        var persisted = DataGridStatePersistenceMapper.ToPersisted(runtime, stateOptions, persistenceOptions);
        var payload = DataGridStatePersistence.Serialize(persisted);
        var deserialized = DataGridStatePersistence.Deserialize(payload);
        return DataGridStatePersistenceMapper.ToRuntime(
            deserialized,
            DataGridStateSections.All,
            stateOptions,
            persistenceOptions);
    }

    private static DataGridPersistedState.PersistedValue PersistedString(string value)
    {
        return new DataGridPersistedState.PersistedValue
        {
            Type = "string",
            Value = value
        };
    }

    private sealed class TokenRegistry : IDataGridStatePersistenceTokenProvider, IDataGridStatePersistenceTokenResolver
    {
        public IComparer SortingComparer { get; set; }

        public Func<object, bool> FilteringPredicate { get; set; }

        public object[] FilteringValues { get; set; }

        public Func<ConditionalFormattingContext, bool> ConditionalFormattingPredicate { get; set; }

        public ControlTheme ConditionalFormattingTheme { get; set; }

        public IValueConverter GroupingValueConverter { get; set; }

        public bool TryGetSortingComparerToken(SortingDescriptor descriptor, out string token)
        {
            if (ReferenceEquals(descriptor.Comparer, SortingComparer))
            {
                token = "sorting:comparer";
                return true;
            }

            token = null;
            return false;
        }

        public bool TryGetFilteringPredicateToken(FilteringDescriptor descriptor, out string token)
        {
            if (ReferenceEquals(descriptor.Predicate, FilteringPredicate))
            {
                token = "filtering:predicate";
                return true;
            }

            token = null;
            return false;
        }

        public bool TryGetFilteringValueToken(FilteringDescriptor descriptor, object value, out string token)
        {
            var idx = FilteringValues.IndexOf(value);
            if (idx != -1)
            {
                token = value.ToString()!;
                return true;
            }

            token = null;
            return false;
        }

        public bool TryGetConditionalFormattingPredicateToken(
            ConditionalFormattingDescriptor descriptor,
            out string token)
        {
            if (ReferenceEquals(descriptor.Predicate, ConditionalFormattingPredicate))
            {
                token = "conditional:predicate";
                return true;
            }

            token = null;
            return false;
        }

        public bool TryGetConditionalFormattingThemeToken(ConditionalFormattingDescriptor descriptor, out string token)
        {
            if (ReferenceEquals(descriptor.Theme, ConditionalFormattingTheme))
            {
                token = "conditional:theme";
                return true;
            }

            token = null;
            return false;
        }

        public bool TryGetGroupingValueConverterToken(IValueConverter converter, out string token)
        {
            if (ReferenceEquals(converter, GroupingValueConverter))
            {
                token = "grouping:converter";
                return true;
            }

            token = null;
            return false;
        }

        public bool TryResolveSortingComparer(string token, out IComparer comparer)
        {
            if (string.Equals(token, "sorting:comparer", StringComparison.Ordinal))
            {
                comparer = SortingComparer;
                return comparer != null;
            }

            comparer = null;
            return false;
        }

        public bool TryResolveFilteringPredicate(
            string token,
            object value,
            List<object> values,
            out Func<object, bool> predicate)
        {
            if (string.Equals(token, "filtering:predicate", StringComparison.Ordinal))
            {
                predicate = FilteringPredicate;
                return predicate != null;
            }

            predicate = null;
            return false;
        }

        public bool TryResolveFilteringValue(string token, out object value)
        {
            if (token.StartsWith("filtering:value", StringComparison.Ordinal))
            {
                value = token;
                return value != null;
            }

            value = null;
            return false;
        }

        public bool TryResolveConditionalFormattingPredicate(
            string token,
            out Func<ConditionalFormattingContext, bool> predicate)
        {
            if (string.Equals(token, "conditional:predicate", StringComparison.Ordinal))
            {
                predicate = ConditionalFormattingPredicate;
                return predicate != null;
            }

            predicate = null;
            return false;
        }

        public bool TryResolveConditionalFormattingTheme(string token, out ControlTheme theme)
        {
            if (string.Equals(token, "conditional:theme", StringComparison.Ordinal))
            {
                theme = ConditionalFormattingTheme;
                return theme != null;
            }

            theme = null;
            return false;
        }

        public bool TryResolveGroupingValueConverter(string token, out IValueConverter converter)
        {
            if (string.Equals(token, "grouping:converter", StringComparison.Ordinal))
            {
                converter = GroupingValueConverter;
                return converter != null;
            }

            converter = null;
            return false;
        }
    }

    private sealed class PassThroughValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    private sealed class TrackingSerializer : IDataGridStateSerializer
    {
        private readonly SystemTextJsonDataGridStateSerializer _inner = new();

        public bool SerializeBytesCalled { get; private set; }

        public bool DeserializeBytesCalled { get; private set; }

        public bool SerializeStringCalled { get; private set; }

        public bool DeserializeStringCalled { get; private set; }

        public string FormatId => "tracking";

        public byte[] Serialize(DataGridPersistedState state)
        {
            SerializeBytesCalled = true;
            return _inner.Serialize(state);
        }

        public DataGridPersistedState Deserialize(ReadOnlySpan<byte> payload)
        {
            DeserializeBytesCalled = true;
            return _inner.Deserialize(payload);
        }

        public string SerializeToString(DataGridPersistedState state)
        {
            SerializeStringCalled = true;
            return _inner.SerializeToString(state);
        }

        public DataGridPersistedState Deserialize(string payload)
        {
            DeserializeStringCalled = true;
            return _inner.Deserialize(payload);
        }
    }
}
