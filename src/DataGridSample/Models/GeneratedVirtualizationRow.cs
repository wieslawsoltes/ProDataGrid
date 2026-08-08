// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedVirtualizationRowSchema",
    SchemaId = "sample/generated-virtualization/v1",
    Strict = true,
    PerformanceProfile = Avalonia.Controls.DataGridGeneratedPerformanceProfile.VariableHeightEstimated)]
public sealed class GeneratedVirtualizationRow
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", Order = 0, Width = "70", IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Workload", Order = 1, Width = "*")]
    public string Workload { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Description", Order = 2, Width = "3*")]
    public string Description { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Updates/s", Order = 3, Width = "110", FormatString = "N0")]
    public double UpdatesPerSecond { get; init; }
}
