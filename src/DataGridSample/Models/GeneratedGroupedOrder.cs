// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedGroupedOrderSchema",
    SchemaId = "sample/generated-grouped-order/v1",
    Strict = true,
    Streaming = true)]
public sealed class GeneratedGroupedOrder
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Order", ColumnKey = "order-id", Width = "102", IsReadOnly = true)]
    [DataGridSummary(DataGridAggregateType.Count, Scope = DataGridSummaryScope.Both, Title = "Orders: ")]
    public int OrderId { get; init; }

    [DataGridColumn(Header = "Region", ColumnKey = "region", Width = "110", IsReadOnly = true)]
    [DataGridGroup(Order = 0)]
    public string Region { get; init; } = string.Empty;

    [DataGridColumn(Header = "Category", ColumnKey = "category", Width = "120", IsReadOnly = true)]
    [DataGridGroup(Order = 1)]
    public string Category { get; init; } = string.Empty;

    [DataGridColumn(Header = "Customer", ColumnKey = "customer", Width = "*")]
    [DataGridSummary(DataGridAggregateType.CountDistinct, Scope = DataGridSummaryScope.Both, Title = "Customers: ")]
    public string Customer { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Qty", ColumnKey = "quantity", Width = "92", IsReadOnly = true)]
    [DataGridSummary(DataGridAggregateType.Sum, Scope = DataGridSummaryScope.Both, Format = "N0", Title = "Qty: ")]
    public int Quantity { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Unit price", ColumnKey = "unit-price", Width = "132", FormatString = "C2", IsReadOnly = true)]
    [DataGridSummary(DataGridAggregateType.Average, Scope = DataGridSummaryScope.Total, Format = "C2", Title = "Avg: ")]
    public decimal UnitPrice { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Revenue", ColumnKey = "revenue", Width = "182", FormatString = "C2", IsReadOnly = true)]
    [DataGridSummary(DataGridAggregateType.Sum, Scope = DataGridSummaryScope.Both, Format = "C2", Title = "Revenue: ")]
    public decimal Revenue => Quantity * UnitPrice;
}
