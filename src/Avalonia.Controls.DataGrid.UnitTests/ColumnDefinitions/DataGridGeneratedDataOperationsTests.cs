// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedDataOperationsTests
{
    private static readonly DataGridColumnValueAccessor<Row, int> s_idAccessor = new(static row => row.Id);
    private static readonly DataGridColumnValueAccessor<Row, string> s_nameAccessor = new(static row => row.Name);
    private static readonly DataGridColumnValueAccessor<Row, decimal> s_amountAccessor = new(static row => row.Amount);

    private static readonly DataGridGeneratedDataOperations<Row> s_operations = new(
        new DataGridColumnAccessorRegistration[]
        {
            new("id", nameof(Row.Id), s_idAccessor),
            new("name", nameof(Row.Name), s_nameAccessor),
            new("amount", nameof(Row.Amount), s_amountAccessor),
            new("hidden", nameof(Row.Hidden), new DataGridColumnValueAccessor<Row, string>(static row => row.Hidden), isSearchable: false)
        });

    [Fact]
    public void Sort_compiler_applies_multiple_accessors_and_direction()
    {
        IComparer<Row> comparer = s_operations.CreateSortComparer(
            new[]
            {
                new SortingDescriptor("name", ListSortDirection.Ascending, nameof(Row.Name)),
                new SortingDescriptor("amount", ListSortDirection.Descending, nameof(Row.Amount))
            });
        var rows = new List<Row>
        {
            new(1, "Beta", 2m, "secret"),
            new(2, "Alpha", 1m, "secret"),
            new(3, "Alpha", 3m, "secret")
        };

        rows.Sort(comparer);

        Assert.Equal(new[] { 3, 2, 1 }, rows.ConvertAll(static row => row.Id));
    }

    [Fact]
    public void Filter_compiler_combines_descriptors_with_and_semantics()
    {
        Func<Row, bool> predicate = s_operations.CreateFilterPredicate(
            new[]
            {
                new FilteringDescriptor(
                    "name",
                    FilteringOperator.Contains,
                    nameof(Row.Name),
                    "alp",
                    stringComparison: StringComparison.OrdinalIgnoreCase),
                new FilteringDescriptor("amount", FilteringOperator.GreaterThanOrEqual, nameof(Row.Amount), 2m)
            });

        Assert.True(predicate(new Row(1, "Alpha", 3m, string.Empty)));
        Assert.False(predicate(new Row(2, "Alpha", 1m, string.Empty)));
        Assert.False(predicate(new Row(3, "Beta", 3m, string.Empty)));
    }

    [Fact]
    public void Sort_compiler_applies_custom_comparer_to_accessor_values()
    {
        IComparer<Row> comparer = s_operations.CreateSortComparer(
            new[]
            {
                new SortingDescriptor(
                    "name",
                    ListSortDirection.Ascending,
                    nameof(Row.Name),
                    comparer: StringComparer.OrdinalIgnoreCase)
            });
        var rows = new List<Row>
        {
            new(1, "beta", 2m, string.Empty),
            new(2, "Alpha", 1m, string.Empty)
        };

        rows.Sort(comparer);

        Assert.Equal(new[] { 2, 1 }, rows.ConvertAll(static row => row.Id));
    }

    [Fact]
    public void Search_compiler_combines_descriptors_with_or_semantics()
    {
        Func<Row, bool> predicate = s_operations.CreateSearchPredicate(
            new[]
            {
                new SearchDescriptor("alp", SearchMatchMode.Contains),
                new SearchDescriptor("42", SearchMatchMode.Equals)
            });

        Assert.True(predicate(new Row(1, "Alpha", 3m, string.Empty)));
        Assert.False(predicate(new Row(2, "Beta", 3m, "alp")));
    }

    [Fact]
    public void Search_compiler_honors_explicit_columns_and_wildcards()
    {
        Func<Row, bool> predicate = s_operations.CreateSearchPredicate(
            new[]
            {
                new SearchDescriptor(
                    "Al*",
                    SearchMatchMode.Wildcard,
                    SearchTermCombineMode.Any,
                    SearchScope.ExplicitColumns,
                    new object[] { "name" })
            });

        Assert.True(predicate(new Row(1, "Alpha", 3m, string.Empty)));
        Assert.False(predicate(new Row(2, "Beta", 3m, "Alpha")));
    }

    [Fact]
    public void Empty_descriptor_sets_return_no_sort_and_match_all_delegates()
    {
        IComparer<Row> comparer = s_operations.CreateSortComparer(Array.Empty<SortingDescriptor>());
        Func<Row, bool> filter = s_operations.CreateFilterPredicate(Array.Empty<FilteringDescriptor>());
        Func<Row, bool> search = s_operations.CreateSearchPredicate(Array.Empty<SearchDescriptor>());
        var row = new Row(1, "Alpha", 3m, string.Empty);

        Assert.Equal(0, comparer.Compare(row, new Row(2, "Beta", 1m, string.Empty)));
        Assert.True(filter(row));
        Assert.True(search(row));
    }

    [Fact]
    public void FastPathOptions_is_a_bindable_direct_property()
    {
        var grid = new DataGrid();
        var options = new DataGridFastPathOptions { StrictMode = true };

        grid.FastPathOptions = options;

        Assert.Same(options, grid.GetValue(DataGrid.FastPathOptionsProperty));
        Assert.Equal(Avalonia.Data.BindingMode.OneWay, DataGrid.FastPathOptionsProperty.GetMetadata<DataGrid>().DefaultBindingMode);
    }

    private sealed record Row(int Id, string Name, decimal Amount, string Hidden);
}
