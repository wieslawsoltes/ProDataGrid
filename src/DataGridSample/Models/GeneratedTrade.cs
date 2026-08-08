using System;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedTradeSchema",
    SchemaId = "sample/generated-trade/v1",
    Strict = true,
    Streaming = true)]
public sealed class GeneratedTrade
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", Order = 0, ColumnKey = "trade-id", IsReadOnly = true, Width = "70")]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Symbol", Order = 1, ColumnKey = "trade-symbol", Width = "*")]
    public string Symbol { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Desk", Order = 2, ColumnKey = "trade-desk", Width = "*")]
    public string Desk { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Price", Order = 3, ColumnKey = "trade-price", FormatString = "N2")]
    public decimal Price { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Quantity", Order = 4, ColumnKey = "trade-quantity", FormatString = "N0")]
    public int Quantity { get; init; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Timestamp", Order = 5, ColumnKey = "trade-timestamp", FormatString = "HH:mm:ss", IsReadOnly = true)]
    public DateTimeOffset Timestamp { get; init; }
}
