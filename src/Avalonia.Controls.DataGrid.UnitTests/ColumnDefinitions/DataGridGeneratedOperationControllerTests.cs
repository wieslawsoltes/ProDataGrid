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

public sealed class DataGridGeneratedOperationControllerTests
{
    [Fact]
    public void External_controller_compiles_model_changes_and_disables_view_ownership()
    {
        using var controller = new DataGridGeneratedOperationController<Row>(
            new RowSchema(),
            DataGridOperationExecution.ExternalPipeline);
        var changes = new List<DataGridGeneratedOperationChange>();
        controller.OperationsChanged += (_, args) => changes.Add(args.Change);

        controller.SetSorting(new[] { RowSchema.Amount.Descending() });
        controller.SetFiltering(new[] { RowSchema.Amount.GreaterThanOrEqual(10m) });
        controller.SetSearching(new[] { RowSchema.Name.Search("alp") });

        Assert.False(controller.SortingModel.OwnsViewSorts);
        Assert.False(controller.FilteringModel.OwnsViewFilter);
        Assert.True(controller.FilterPredicate(new Row(1, "Alpha", 20m)));
        Assert.False(controller.FilterPredicate(new Row(2, "Alpha", 5m)));
        Assert.True(controller.SearchPredicate(new Row(3, "Alpha", 1m)));
        Assert.False(controller.SearchPredicate(new Row(4, "Beta", 1m)));
        Assert.Equal(-1, controller.SortComparer.Compare(new Row(1, "A", 20m), new Row(2, "B", 10m)));
        Assert.Equal(
            new[]
            {
                DataGridGeneratedOperationChange.Sorting,
                DataGridGeneratedOperationChange.Filtering,
                DataGridGeneratedOperationChange.Searching
            },
            changes);
        Assert.Equal(3, controller.Version);
    }

    [Fact]
    public void Dispose_detaches_models_and_rejects_controller_mutation()
    {
        var controller = new DataGridGeneratedOperationController<Row>(new RowSchema());
        long version = controller.Version;

        controller.Dispose();
        controller.SortingModel.SetOrUpdate(RowSchema.Amount.Ascending());

        Assert.Equal(version, controller.Version);
        Assert.Throws<ObjectDisposedException>(() =>
            controller.SetSorting(Array.Empty<SortingDescriptor>()));
    }

    [Fact]
    public void Disabled_operations_do_not_subscribe_or_claim_view_ownership()
    {
        using var controller = new DataGridGeneratedOperationController<Row>(
            new RowSchema(),
            DataGridOperationExecution.View,
            DataGridGeneratedFeatures.Columns | DataGridGeneratedFeatures.Searching);

        controller.SortingModel.SetOrUpdate(RowSchema.Amount.Ascending());
        controller.FilteringModel.SetOrUpdate(RowSchema.Amount.GreaterThan(0m));

        Assert.False(controller.SortingModel.OwnsViewSorts);
        Assert.False(controller.FilteringModel.OwnsViewFilter);
        Assert.Equal(0, controller.Version);
        Assert.Throws<InvalidOperationException>(() =>
            controller.SetSorting(Array.Empty<SortingDescriptor>()));

        controller.SetSearching(new[] { RowSchema.Name.Search("alpha") });
        Assert.Equal(1, controller.Version);
    }

    [Fact]
    public void Preset_is_copied_and_applied_as_one_combined_revision()
    {
        var sourceSorts = new List<SortingDescriptor> { RowSchema.Amount.Descending() };
        var preset = new DataGridGeneratedOperationPreset(
            "expensive alpha",
            sourceSorts,
            new[] { RowSchema.Amount.GreaterThanOrEqual(10m) },
            new[] { RowSchema.Name.Search("alpha") });
        sourceSorts.Clear();

        using var controller = new DataGridGeneratedOperationController<Row>(new RowSchema());
        var changes = new List<DataGridGeneratedOperationChange>();
        controller.OperationsChanged += (_, args) => changes.Add(args.Change);

        controller.ApplyPreset(preset);

        Assert.Single(preset.Sorting);
        Assert.Equal(1, controller.Version);
        Assert.Equal(
            DataGridGeneratedOperationChange.Sorting |
            DataGridGeneratedOperationChange.Filtering |
            DataGridGeneratedOperationChange.Searching,
            Assert.Single(changes));
        Assert.True(controller.FilterPredicate(new Row(1, "Alpha", 20m)));
        Assert.True(controller.SearchPredicate(new Row(1, "Alpha", 20m)));
    }

    private sealed record Row(int Id, string Name, decimal Amount);

    private sealed class RowSchema : IDataGridGeneratedSchema<Row>
    {
        private static readonly DataGridColumnValueAccessor<Row, string> s_nameAccessor = new(static row => row.Name);
        private static readonly DataGridColumnValueAccessor<Row, decimal> s_amountAccessor = new(static row => row.Amount);
        private static readonly DataGridGeneratedDataOperations<Row> s_operations = new(
            new DataGridColumnAccessorRegistration[]
            {
                new("name", nameof(Row.Name), s_nameAccessor),
                new("amount", nameof(Row.Amount), s_amountAccessor)
            });

        public static DataGridGeneratedStringField<Row, string> Name { get; } =
            new(0, "name", nameof(Row.Name), s_nameAccessor, true);

        public static DataGridGeneratedComparableField<Row, decimal> Amount { get; } =
            new(1, "amount", nameof(Row.Amount), s_amountAccessor, true);

        public DataGridColumnDefinitionList CreateColumnDefinitions() => new();

        public IComparer<Row> CreateSortComparer(IReadOnlyList<SortingDescriptor> descriptors) =>
            s_operations.CreateSortComparer(descriptors);

        public Func<Row, bool> CreateFilterPredicate(IReadOnlyList<FilteringDescriptor> descriptors) =>
            s_operations.CreateFilterPredicate(descriptors);

        public Func<Row, bool> CreateSearchPredicate(IReadOnlyList<SearchDescriptor> descriptors) =>
            s_operations.CreateSearchPredicate(descriptors);

        public DataGridFastPathOptions CreateFastPathOptions() => new();
    }
}
