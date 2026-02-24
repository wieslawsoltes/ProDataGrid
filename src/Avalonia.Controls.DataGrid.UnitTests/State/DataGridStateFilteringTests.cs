// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.State;

public class DataGridStateFilteringTests
{
    [AvaloniaFact]
    public void CaptureAndRestoreFilteringState_ResolvesColumns()
    {
        var items = StateTestHelper.CreateItems(10);
        var (grid, root) = StateTestHelper.CreateGrid(items);

        try
        {
            var nameColumn = grid.ColumnsInternal[1];

            grid.FilteringModel.OwnsViewFilter = true;
            grid.FilteringModel.Apply(new[]
            {
                new FilteringDescriptor(
                    nameColumn,
                    FilteringOperator.Contains,
                    nameof(StateTestItem.Name),
                    "Item 1",
                    stringComparison: StringComparison.Ordinal),
            });

            var options = StateTestHelper.CreateKeyedOptions(grid, items);
            var state = grid.CaptureFilteringState(options);

            Assert.NotNull(state);
            Assert.Equal("Name", state.Descriptors[0].ColumnId);

            grid.FilteringModel.Clear();
            grid.FilteringModel.OwnsViewFilter = false;

            grid.RestoreFilteringState(state, options);

            Assert.True(grid.FilteringModel.OwnsViewFilter);
            var restored = Assert.Single(grid.FilteringModel.Descriptors);
            Assert.Same(nameColumn, restored.ColumnId);
            Assert.Equal(FilteringOperator.Contains, restored.Operator);
            Assert.Equal("Item 1", restored.Value);
            Assert.Equal("Name", ((DataGridColumn)restored.ColumnId).ColumnKey);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CaptureAndRestoreFilteringState_Resolves_Definition_Columns()
    {
        var items = StateTestHelper.CreateItems(10);
        var (grid, root, definitions) = StateTestHelper.CreateGridWithDefinitions(items);

        try
        {
            var nameDefinition = definitions[1];

            grid.FilteringModel.OwnsViewFilter = true;
            grid.FilteringModel.Apply(new[]
            {
                new FilteringDescriptor(
                    nameDefinition,
                    FilteringOperator.Contains,
                    nameof(StateTestItem.Name),
                    "Item 1",
                    stringComparison: StringComparison.Ordinal),
            });

            var options = StateTestHelper.CreateItemKeyOptions(items);
            var state = grid.CaptureFilteringState(options);

            Assert.NotNull(state);
            Assert.Equal("Name", state.Descriptors[0].ColumnId);

            grid.FilteringModel.Clear();
            grid.FilteringModel.OwnsViewFilter = false;

            grid.RestoreFilteringState(state, options);

            Assert.True(grid.FilteringModel.OwnsViewFilter);
            var restored = Assert.Single(grid.FilteringModel.Descriptors);
            Assert.Same(nameDefinition, restored.ColumnId);
            Assert.Equal(FilteringOperator.Contains, restored.Operator);
            Assert.Equal("Item 1", restored.Value);
            Assert.IsType<DataGridColumnDefinition>(restored.ColumnId, exactMatch: false);
            Assert.Equal("Name", ((DataGridColumnDefinition)restored.ColumnId).ColumnKey);
        }
        finally
        {
            root.Close();
        }
    }
}
