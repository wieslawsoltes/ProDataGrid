// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridPivoting;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedAnalyticsTests
{
    [Fact]
    public void Typed_group_field_adapts_direct_getter_to_collection_view()
    {
        var field = new DataGridGeneratedGroupField<Row, string>(
            "desk", 1, ListSortDirection.Descending, static row => row.Desk);
        Row row = new("Rates", 10m);

        object key = field.CreateDescription().GroupKeyFromItem(row, 0, CultureInfo.InvariantCulture);

        Assert.Equal("Rates", key);
        Assert.Equal("desk", field.CreateDescription().PropertyName);
        Assert.Equal(ListSortDirection.Descending, field.Direction);
        Assert.True(field.CreateSortComparer().Compare(new Row("A", 0m), new Row("B", 0m)) < 0);
    }

    [Fact]
    public void Incremental_summary_supports_add_remove_replace_and_reset()
    {
        var summary = new DataGridGeneratedSummary<Row, decimal>(
            "amount",
            DataGridAggregateType.Average,
            DataGridSummaryScope.Both,
            static row => row.Amount,
            0m,
            static (left, right) => left + right,
            static (left, right) => left - right,
            static (sum, count) => sum / count);
        Row first = new("A", 10m);
        Row second = new("B", 20m);

        summary.Add(first);
        summary.Add(second);
        Assert.Equal(15m, summary.Value);
        summary.Remove(first);
        Assert.Equal(20m, summary.Value);
        summary.Replace(second, new Row("B", 30m));
        Assert.Equal(30m, summary.Value);
        summary.Reset([first, second]);
        Assert.Equal(15m, summary.Value);
    }

    [Fact]
    public void Summary_count_distinct_and_min_use_typed_values()
    {
        var distinct = new DataGridGeneratedSummary<Row, string>(
            "desk", DataGridAggregateType.CountDistinct, DataGridSummaryScope.Total, static row => row.Desk);
        var minimum = new DataGridGeneratedSummary<Row, decimal>(
            "amount", DataGridAggregateType.Min, DataGridSummaryScope.Total, static row => row.Amount);
        Row[] rows = [new("A", 3m), new("A", 1m), new("B", 2m)];

        distinct.Reset(rows);
        minimum.Reset(rows);

        Assert.Equal(2, distinct.Value);
        Assert.Equal(1m, minimum.Value);
    }

    [Fact]
    public void Summary_supports_null_values_and_configured_equality()
    {
        var distinct = new DataGridGeneratedSummary<NullableRow, string?>(
            "desk",
            DataGridAggregateType.CountDistinct,
            DataGridSummaryScope.Total,
            static row => row.Desk,
            equalityComparer: StringComparer.OrdinalIgnoreCase);

        distinct.Reset([new(null), new(null), new("Rates"), new("RATES")]);
        Assert.Equal(2, distinct.Value);

        distinct.Remove(new NullableRow("rates"));
        Assert.Equal(2, distinct.Value);
        distinct.Remove(new NullableRow("RATES"));
        Assert.Equal(1, distinct.Value);
    }

    [Fact]
    public void Conditional_rule_evaluates_typed_predicate_and_metadata()
    {
        var rule = new DataGridGeneratedConditionalRule<Row, decimal>(
            "large",
            "amount",
            static row => row.Amount,
            static (_, value) => value > 100m,
            "LargeCell",
            5,
            target: ConditionalFormattingTarget.Row);

        Assert.True(rule.IsMatch(new Row("A", 101m)));
        Assert.False(rule.IsMatch(new Row("A", 99m)));
        Assert.Equal("LargeCell", rule.ThemeKey);
        Assert.Equal(ConditionalFormattingTarget.Row, rule.Target);

        ConditionalFormattingDescriptor descriptor = rule.CreateDescriptor();
        Assert.Equal(ConditionalFormattingOperator.Custom, descriptor.Operator);
        Assert.Equal(ConditionalFormattingValueSource.Item, descriptor.ValueSource);
        Assert.True(descriptor.Predicate(new ConditionalFormattingContext(
            new Row("A", 101m), 0, null!, null!, null!, null!, ConditionalFormattingValueSource.Item)));

        IConditionalFormattingModel model = DataGridGeneratedConditionalFormatting.CreateModel([rule]);
        Assert.Single(model.Descriptors);
        Assert.Same(descriptor.Predicate, model.Descriptors[0].Predicate);
    }

    [Fact]
    public void Capability_metadata_adapts_direct_getters_to_pivot_fields()
    {
        var axisMetadata = new DataGridGeneratedAnalyticsField<Row, string>(
            "desk", DataGridGeneratedAnalyticsRole.PivotRow, 0, static row => row.Desk, "Desk");
        var valueMetadata = new DataGridGeneratedAnalyticsField<Row, decimal>(
            "amount",
            DataGridGeneratedAnalyticsRole.PivotValue,
            0,
            static row => row.Amount,
            "Amount",
            "N2",
            (int)PivotAggregateType.Sum,
            PivotValueDisplayMode.PercentOfGrandTotal);

        PivotAxisField axis = DataGridGeneratedPivotAdapter.CreateAxisField(axisMetadata);
        PivotValueField value = DataGridGeneratedPivotAdapter.CreateValueField(valueMetadata);
        Row row = new("Rates", 42m);

        Assert.Equal("Rates", axis.ValueSelector!(row));
        Assert.Equal(42m, value.ValueSelector!(row));
        Assert.Equal(PivotAggregateType.Sum, value.AggregateType);
        Assert.Equal(PivotValueDisplayMode.PercentOfGrandTotal, value.DisplayMode);
    }

    [Fact]
    public void Generated_pivot_factory_orders_fields_and_builds_model_without_property_paths()
    {
        IDataGridGeneratedAnalyticsField[] fields =
        [
            new DataGridGeneratedAnalyticsField<PivotSourceRow, decimal>(
                "profit", DataGridGeneratedAnalyticsRole.PivotValue, 1, static row => row.Profit,
                "Profit", "N0", (int)PivotAggregateType.Sum),
            new DataGridGeneratedAnalyticsField<PivotSourceRow, string>(
                "region", DataGridGeneratedAnalyticsRole.PivotRow, 1, static row => row.Region, "Region"),
            new DataGridGeneratedAnalyticsField<PivotSourceRow, string>(
                "period", DataGridGeneratedAnalyticsRole.PivotColumn, 0, static row => row.Period, "Period"),
            new DataGridGeneratedAnalyticsField<PivotSourceRow, string>(
                "desk", DataGridGeneratedAnalyticsRole.PivotRow, 0, static row => row.Desk, "Desk"),
            new DataGridGeneratedAnalyticsField<PivotSourceRow, decimal>(
                "revenue", DataGridGeneratedAnalyticsRole.PivotValue, 0, static row => row.Revenue,
                "Revenue", "N0", (int)PivotAggregateType.Sum)
        ];
        PivotSourceRow[] items =
        [
            new("Rates", "North", "Q1", 100m, 30m),
            new("Rates", "South", "Q1", 80m, 20m),
            new("Credit", "North", "Q2", 120m, 45m)
        ];

        using PivotTableModel model = DataGridGeneratedPivotAdapter.CreateModel(
            items,
            fields,
            static pivot => pivot.Layout.ShowRowSubtotals = false);

        Assert.Equal(new[] { "Desk", "Region" }, model.RowFields.Select(static field => field.Header));
        Assert.Equal("Period", Assert.Single(model.ColumnFields).Header);
        Assert.Equal(new[] { "Revenue", "Profit" }, model.ValueFields.Select(static field => field.Header));
        Assert.All(model.RowFields, static field => Assert.Null(field.PropertyPath));
        Assert.All(model.ValueFields, static field => Assert.Null(field.PropertyPath));
        Assert.Equal("Rates", model.RowFields[0].ValueSelector!(items[0]));
        Assert.Equal(100m, model.ValueFields[0].ValueSelector!(items[0]));
        Assert.NotEmpty(model.Rows);
        Assert.NotEmpty(model.ColumnDefinitions);
    }

    private sealed record Row(string Desk, decimal Amount);
    private sealed record NullableRow(string? Desk);
    private sealed record PivotSourceRow(string Desk, string Region, string Period, decimal Revenue, decimal Profit);
}
