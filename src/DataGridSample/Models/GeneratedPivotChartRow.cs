// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Controls.DataGridPivoting;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedPivotChartRowSchema",
    SchemaId = "sample/generated-pivot-chart-row/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true)]
public sealed class GeneratedPivotChartRow
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", ColumnKey = "id", Width = "68", IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(Header = "Period", ColumnKey = "period", Width = "*", IsReadOnly = true)]
    [DataGridGroup(Order = 0)]
    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotColumn, Order = 0, Name = "Period")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartCategory, Order = 0)]
    public string Period { get; init; } = string.Empty;

    [DataGridColumn(Header = "Region", ColumnKey = "region", Width = "*", IsReadOnly = true)]
    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotRow, Order = 0, Name = "Region")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartSeries, Order = 0, Series = "Region")]
    public string Region { get; init; } = string.Empty;

    [DataGridColumn(Header = "Channel", ColumnKey = "channel", Width = "*", IsReadOnly = true)]
    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotFilter, Order = 0, Name = "Channel")]
    public string Channel { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Revenue", ColumnKey = "revenue", Width = "*", FormatString = "C0", IsReadOnly = true)]
    [DataGridPivotValue(PivotAggregateType.Sum, Order = 0, Name = "Revenue", Format = "C0")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartValue, Order = 0, Series = "Revenue", Format = "C0", Aggregate = DataGridAggregateType.Sum)]
    public double Revenue { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Profit", ColumnKey = "profit", Width = "*", FormatString = "C0", IsReadOnly = true)]
    [DataGridPivotValue(PivotAggregateType.Sum, Order = 1, Name = "Profit", Format = "C0")]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartValue, Order = 1, Series = "Profit", Format = "C0", Aggregate = DataGridAggregateType.Sum)]
    public double Profit { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Units", ColumnKey = "units", Width = "*", IsReadOnly = true)]
    public int Units { get; init; }
}
