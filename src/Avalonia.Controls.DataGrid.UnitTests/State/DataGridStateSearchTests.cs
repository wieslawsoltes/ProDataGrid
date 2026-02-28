// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.State;

public class DataGridStateSearchTests
{
    [AvaloniaFact]
    public void CaptureAndRestoreSearchState_RestoresDescriptorAndCurrent()
    {
        var items = StateTestHelper.CreateItems(20);
        var (grid, root) = StateTestHelper.CreateGrid(items);

        try
        {
            var nameColumn = grid.ColumnsInternal[1];

            grid.SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;
            grid.SearchModel.HighlightCurrent = true;
            grid.SearchModel.UpdateSelectionOnNavigate = true;
            grid.SearchModel.WrapNavigation = true;

            grid.SearchModel.Apply(new[]
            {
                new SearchDescriptor(
                    "Item 1",
                    SearchMatchMode.Contains,
                    SearchTermCombineMode.Any,
                    SearchScope.ExplicitColumns,
                    new[] { nameColumn }),
            });

            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(grid.SearchModel.Results);
            grid.SearchModel.MoveTo(0);
            Assert.Equal(0, grid.SearchModel.CurrentIndex);

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var state = grid.CaptureSearchState(options);

            Assert.NotNull(state);
            Assert.NotNull(state.Descriptors[0].ColumnIds);
            Assert.Equal("Name", state.Descriptors[0].ColumnIds[0]);

            grid.SearchModel.Clear();
            grid.SearchModel.HighlightMode = SearchHighlightMode.None;
            grid.SearchModel.HighlightCurrent = false;
            grid.SearchModel.UpdateSelectionOnNavigate = false;
            grid.SearchModel.WrapNavigation = false;

            grid.RestoreSearchState(state, options);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(SearchHighlightMode.TextAndCell, grid.SearchModel.HighlightMode);
            Assert.True(grid.SearchModel.HighlightCurrent);
            Assert.True(grid.SearchModel.UpdateSelectionOnNavigate);
            Assert.True(grid.SearchModel.WrapNavigation);

            var restored = Assert.Single(grid.SearchModel.Descriptors);
            Assert.NotNull(restored.ColumnIds);
            var restoredColumn = Assert.Single(restored.ColumnIds);
            Assert.Same(nameColumn, restoredColumn);
            Assert.Equal(0, grid.SearchModel.CurrentIndex);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CaptureAndRestoreSearchState_Resolves_Definition_Columns()
    {
        var items = StateTestHelper.CreateItems(20);
        var (grid, root, definitions) = StateTestHelper.CreateGridWithDefinitions(items);

        try
        {
            var nameDefinition = definitions[1];

            grid.SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;
            grid.SearchModel.HighlightCurrent = true;
            grid.SearchModel.UpdateSelectionOnNavigate = true;
            grid.SearchModel.WrapNavigation = true;

            grid.SearchModel.Apply(new[]
            {
                new SearchDescriptor(
                    "Item 1",
                    SearchMatchMode.Contains,
                    SearchTermCombineMode.Any,
                    SearchScope.ExplicitColumns,
                    new[] { nameDefinition }),
            });

            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(grid.SearchModel.Results);
            grid.SearchModel.MoveTo(0);
            Assert.Equal(0, grid.SearchModel.CurrentIndex);

            var options = StateTestHelper.CreateItemKeyOptions(items);
            var state = grid.CaptureSearchState(options);

            Assert.NotNull(state);
            Assert.NotNull(state.Descriptors[0].ColumnIds);
            Assert.Same(nameDefinition, state.Descriptors[0].ColumnIds[0]);
            Assert.Same("Name", state.Current.ColumnKey);

            grid.SearchModel.Clear();
            grid.SearchModel.HighlightMode = SearchHighlightMode.None;
            grid.SearchModel.HighlightCurrent = false;
            grid.SearchModel.UpdateSelectionOnNavigate = false;
            grid.SearchModel.WrapNavigation = false;

            grid.RestoreSearchState(state, options);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(SearchHighlightMode.TextAndCell, grid.SearchModel.HighlightMode);
            Assert.True(grid.SearchModel.HighlightCurrent);
            Assert.True(grid.SearchModel.UpdateSelectionOnNavigate);
            Assert.True(grid.SearchModel.WrapNavigation);

            var restored = Assert.Single(grid.SearchModel.Descriptors);
            Assert.NotNull(restored.ColumnIds);
            var restoredColumn = Assert.Single(restored.ColumnIds);
            Assert.Same(nameDefinition, restoredColumn);
            Assert.Equal(0, grid.SearchModel.CurrentIndex);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void RestoreSearchState_DefersCurrentUntilResultsAvailable()
    {
        var items = StateTestHelper.CreateItems(20);
        var (grid, root) = StateTestHelper.CreateGrid(items);

        try
        {
            var nameColumn = grid.ColumnsInternal[1];

            grid.SearchModel.Apply(new[]
            {
                new SearchDescriptor(
                    "Item 1",
                    SearchMatchMode.Contains,
                    SearchTermCombineMode.Any,
                    SearchScope.ExplicitColumns,
                    new[] { nameColumn }),
            });

            Dispatcher.UIThread.RunJobs();
            grid.SearchModel.MoveTo(0);

            var state = grid.CaptureSearchState(StateTestHelper.CreateKeyedOptions(grid, items));
            Assert.NotNull(state);

            var newItems = StateTestHelper.CreateItems(20);
            grid.ItemsSource = new List<StateTestItem>();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            grid.RestoreSearchState(state, StateTestHelper.CreateKeyedOptions(grid, newItems));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(grid.SearchModel.Results);
            Assert.True(grid.SearchModel.CurrentIndex < 0);

            grid.ItemsSource = newItems;
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.NotEmpty(grid.SearchModel.Results);
            Assert.Equal(0, grid.SearchModel.CurrentIndex);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void ClearSearchState_ClearsResultsWhenDescriptorsEmpty()
    {
        var items = StateTestHelper.CreateItems(5);
        var (grid, root) = StateTestHelper.CreateGrid(items);

        try
        {
            var column = grid.ColumnsInternal[0];
            var result = new SearchResult(
                items[0],
                rowIndex: 0,
                columnId: column,
                columnIndex: column.Index,
                text: "Item 0",
                matches: Array.Empty<SearchMatch>());

            var resultsChanged = false;
            grid.SearchModel.ResultsChanged += (_, _) => resultsChanged = true;

            grid.SearchModel.UpdateResults(new[] { result });

            Assert.NotEmpty(grid.SearchModel.Results);

            grid.SearchModel.Clear();

            Assert.Empty(grid.SearchModel.Results);
            Assert.True(grid.SearchModel.CurrentIndex < 0);
            Assert.True(resultsChanged);
        }
        finally
        {
            root.Close();
        }
    }
}
