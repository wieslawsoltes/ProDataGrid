// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedRecipeRowSchema",
    SchemaId = "sample/generated-recipe-row/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true)]
public sealed class GeneratedRecipeRow
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", ColumnKey = "id", Width = "68", IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(Header = "Name", ColumnKey = "name", Width = "1.5*")]
    public string Name { get; set; } = string.Empty;

    [DataGridColumn(Header = "Area", ColumnKey = "area", Width = "*")]
    public string Area { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.ProgressBar, Header = "Progress", ColumnKey = "progress", Width = "130", FormatString = "P0")]
    public double Progress { get; set; }

    [DataGridColumn(Header = "Updated", ColumnKey = "updated", Width = "1.2*", FormatString = "HH:mm:ss", IsReadOnly = true)]
    public DateTimeOffset Updated { get; init; }

    [DataGridColumn(DataGridColumnKind.CheckBox, Header = "Enabled", ColumnKey = "enabled", Width = "90")]
    public bool IsEnabled { get; set; }
}
