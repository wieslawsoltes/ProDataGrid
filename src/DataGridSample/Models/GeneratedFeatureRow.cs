using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedFeatureRowSchema",
    SchemaId = "sample/generated-feature-row/v2",
    StateVersion = 2,
    Strict = true,
    Streaming = true,
    PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
public sealed class GeneratedFeatureRow
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", ColumnKey = "id", IsReadOnly = true, Width = "70")]
    public int Id { get; init; }

    [Required, StringLength(12, MinimumLength = 2)]
    [DataGridColumn(Header = "Symbol", ColumnKey = "symbol", PreviousColumnKeys = ["ticker"], Width = "*")]
    [DataGridBand("Identity", Order = 0)]
    public string Symbol { get; set; } = string.Empty;

    [DataGridColumn(Header = "Desk", ColumnKey = "desk", Width = "*")]
    [DataGridGroup(Order = 0)]
    [DataGridBand("Identity", Order = 1)]
    public string Desk { get; set; } = string.Empty;

    [DataGridColumn(
        DataGridColumnKind.Numeric,
        Header = "Amount",
        ColumnKey = "amount",
        FormatString = "N2",
        ValidatorMethod = nameof(ValidateAmount),
        AsyncValidatorMethod = nameof(ValidateAmountAsync),
        CoerceMethod = nameof(CoerceAmount),
        CanEditMethod = nameof(CanEditAmount))]
    [DataGridSummary(DataGridAggregateType.Sum, Scope = DataGridSummaryScope.Both, Format = "N2")]
    [DataGridSummary(DataGridAggregateType.Average, Scope = DataGridSummaryScope.Total, Format = "N2")]
    [DataGridConditionalFormat(DataGridCondition.GreaterThan, Operand = "100000", CellThemeKey = "LargeAmountCell")]
    [DataGridBand("Trading/Risk", Order = 0)]
    public decimal Amount { get; set; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Timestamp", ColumnKey = "timestamp", IsReadOnly = true)]
    [DataGridBand("Trading/Audit", Order = 0)]
    public DateTimeOffset Timestamp { get; init; }

    public static string? ValidateAmount(GeneratedFeatureRow item, decimal value) =>
        value < 0m ? "Amount cannot be negative." : null;

    public static ValueTask<string?> ValidateAmountAsync(
        GeneratedFeatureRow item,
        decimal value,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<string?>(value > 1_000_000m ? "Amount exceeds the sample limit." : null);

    public static decimal CoerceAmount(GeneratedFeatureRow item, decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static bool CanEditAmount(GeneratedFeatureRow item) => item.Id != 1;
}
