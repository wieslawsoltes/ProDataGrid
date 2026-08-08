// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedRemoteOrderSchema",
    SchemaId = "sample/generated-remote-order/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true)]
public sealed class GeneratedRemoteOrder
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Order", Order = 0, ColumnKey = "order-id", IsReadOnly = true, Width = "80")]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Customer", Order = 1, ColumnKey = "order-customer", Width = "*")]
    public string Customer { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Region", Order = 2, ColumnKey = "order-region", Width = "120")]
    public string Region { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Status", Order = 3, ColumnKey = "order-status", Width = "110")]
    public string OrderStatus { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Total", Order = 4, ColumnKey = "order-total", FormatString = "C2", Width = "120")]
    public decimal Total { get; init; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Updated", Order = 5, ColumnKey = "order-updated", FormatString = "yyyy-MM-dd HH:mm", IsReadOnly = true, Width = "150")]
    public DateTimeOffset UpdatedAt { get; init; }
}
