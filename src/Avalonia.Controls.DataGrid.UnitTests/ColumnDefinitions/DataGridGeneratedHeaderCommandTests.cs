// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedHeaderCommandTests
{
    [Fact]
    public void Commands_coordinate_sort_filter_visibility_and_layout_by_stable_key()
    {
        var schema = new RowSchema();
        using var operations = new DataGridGeneratedOperationController<Row>(schema);
        var definition = new DataGridTextColumnDefinition
        {
            ColumnKey = "name", IsVisible = true, DisplayIndex = 2, Width = new DataGridLength(120)
        };
        using var layout = new DataGridGeneratedColumnLayoutController([definition]);
        using var controller = new DataGridGeneratedHeaderCommandController<Row>(RowSchema.Manifest, operations, layout);
        DataGridGeneratedHeaderCommandSet commands = controller.ForField("name");

        commands.SortDescending.Execute(null);
        SortingDescriptor sort = Assert.Single(operations.SortingModel.Descriptors);
        Assert.Equal("name", sort.ColumnId);
        Assert.Equal(System.ComponentModel.ListSortDirection.Descending, sort.Direction);
        Assert.True(commands.ClearSort.CanExecute(null));

        operations.FilteringModel.SetOrUpdate(RowSchema.Name.EqualTo("Ada"));
        Assert.True(commands.ClearFilter.CanExecute(null));
        commands.ClearFilter.Execute(null);
        Assert.Empty(operations.FilteringModel.Descriptors);

        commands.HideColumn.Execute(null);
        Assert.False(layout.IsVisible("name"));
        Assert.True(commands.ShowColumn.CanExecute(null));

        definition.DisplayIndex = 0;
        definition.Width = new DataGridLength(30);
        commands.ResetLayout.Execute(null);
        Assert.True(layout.IsVisible("name"));
        Assert.Equal(2, definition.DisplayIndex);
        Assert.Equal(new DataGridLength(120), definition.Width);
    }

    [Fact]
    public void Grid_dependent_commands_use_replaceable_interaction()
    {
        var schema = new RowSchema();
        using var operations = new DataGridGeneratedOperationController<Row>(schema);
        using var layout = new DataGridGeneratedColumnLayoutController(
            [new DataGridTextColumnDefinition { ColumnKey = "name" }]);
        var interaction = new RecordingInteraction();
        using var controller = new DataGridGeneratedHeaderCommandController<Row>(
            RowSchema.Manifest,
            operations,
            layout,
            interaction);
        DataGridGeneratedHeaderCommandSet commands = controller.ForField("name");

        Assert.True(commands.PinLeft.CanExecute(null));
        commands.PinLeft.Execute(null);
        commands.AutoSize.Execute(null);

        Assert.Equal(
            [DataGridGeneratedHeaderCommandKind.PinLeft, DataGridGeneratedHeaderCommandKind.AutoSize],
            interaction.Executed);
    }

    private sealed record Row(string Name);

    private sealed class RowSchema : IDataGridGeneratedSchema<Row>
    {
        private static readonly DataGridColumnValueAccessor<Row, string> s_nameAccessor = new(static row => row.Name);
        private static readonly DataGridGeneratedDataOperations<Row> s_operations = new(
            [new DataGridColumnAccessorRegistration("name", nameof(Row.Name), s_nameAccessor)]);

        public static DataGridGeneratedStringField<Row, string> Name { get; } =
            new(0, "name", nameof(Row.Name), s_nameAccessor, true);

        public static DataGridGeneratedSchemaManifest Manifest { get; } =
            new(1, "rows/v1", "hash", typeof(Row), [Name]);

        public DataGridColumnDefinitionList CreateColumnDefinitions() =>
            [new DataGridTextColumnDefinition { ColumnKey = "name" }];

        public IComparer<Row> CreateSortComparer(IReadOnlyList<SortingDescriptor> descriptors) =>
            s_operations.CreateSortComparer(descriptors);

        public Func<Row, bool> CreateFilterPredicate(IReadOnlyList<FilteringDescriptor> descriptors) =>
            s_operations.CreateFilterPredicate(descriptors);

        public Func<Row, bool> CreateSearchPredicate(IReadOnlyList<SearchDescriptor> descriptors) =>
            s_operations.CreateSearchPredicate(descriptors);

        public DataGridFastPathOptions CreateFastPathOptions() => new();
    }

    private sealed class RecordingInteraction : IDataGridGeneratedHeaderInteraction
    {
        public List<DataGridGeneratedHeaderCommandKind> Executed { get; } = [];

        public bool CanExecute(DataGridGeneratedHeaderCommandRequest request) => true;

        public void Execute(DataGridGeneratedHeaderCommandRequest request) => Executed.Add(request.Kind);
    }
}
