// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridReporting;
using ProDataGrid.FormulaEngine.Excel;
using ProDataGrid.SourceGeneration;

namespace ProDataGrid.SourceGeneration.AotSmoke;

[GenerateDataGridColumns(
    ProviderName = "AotTradeRowSchema",
    SchemaId = "smoke/aot-trade-row/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true,
    Streaming = true,
    DefaultPageSize = 1,
    InitialCurrency = DataGridGeneratedInitialCurrency.First,
    PreserveCurrentItemByKey = true,
    PreserveSelectionByKey = true,
    OutlineConfigureMethod = nameof(ConfigureOutline),
    MutationHandlerType = typeof(AotTradeMutationHandler),
    NewRowFactoryType = typeof(AotTradeNewRowFactory),
    FormulaFillTranslatorType = typeof(ExcelFormulaFillTranslator))]
internal sealed class AotTradeRow
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", ColumnKey = "id", Order = 0, IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Symbol", ColumnKey = "symbol", Order = 1, Width = "*")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartCategory, Order = 0)]
    public string Symbol { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Desk", ColumnKey = "desk", Order = 2, Width = "*")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartSeries, Order = 0)]
    [DataGridOutlineField(
        DataGridGeneratedAnalyticsRole.OutlineGroup,
        Order = 0,
        Name = "Desk",
        ConfigureMethod = nameof(ConfigureOutlineGroup))]
    public string Desk { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Price", ColumnKey = "price", Order = 3, FormatString = "N2")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartValue, Order = 0, Series = "Price")]
    [DataGridOutlineField(
        DataGridGeneratedAnalyticsRole.OutlineDetail,
        Order = 0,
        Name = "Price",
        Format = "N2",
        Aggregate = DataGridAggregateType.Sum,
        ConfigureMethod = nameof(ConfigureOutlineValue))]
    public decimal Price { get; init; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Timestamp", ColumnKey = "timestamp", Order = 4, FormatString = "HH:mm:ss")]
    public DateTimeOffset Timestamp { get; init; }

    public double Volume { get; set; }

    public TimeSpan? Time { get; set; }

    public static void ConfigureOutlineGroup(OutlineGroupField field) => field.ShowSubtotals = false;

    public static void ConfigureOutlineValue(OutlineValueField field) => field.NullLabel = "AOT empty";

    public static void ConfigureOutline(OutlineReportModel model) => model.Layout.ShowGrandTotal = false;
}

internal sealed class AotTradeMutationHandler : IDataGridGeneratedCollectionMutationHandler<AotTradeRow>
{
    public ValueTask AddAsync(
        int index,
        ReadOnlyMemory<AotTradeRow> items,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask RemoveAsync(
        int index,
        ReadOnlyMemory<AotTradeRow> items,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ReplaceAsync(
        int index,
        ReadOnlyMemory<AotTradeRow> oldItems,
        ReadOnlyMemory<AotTradeRow> newItems,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask MoveAsync(
        int oldIndex,
        int newIndex,
        int count,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask ResetAsync(
        ReadOnlyMemory<AotTradeRow> items,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

internal sealed class AotTradeNewRowFactory : IDataGridGeneratedNewRowFactory<AotTradeRow>
{
    public ValueTask<AotTradeRow> CreateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AotTradeRow { Id = 42, Symbol = "NEW" });
}

internal sealed class AotTradeDropHandler : IDataGridGeneratedDropHandler<int>
{
    public int ApplyCount { get; private set; }

    public ValueTask ApplyAsync(
        DataGridGeneratedDropRequest<int> request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyCount++;
        return ValueTask.CompletedTask;
    }
}
