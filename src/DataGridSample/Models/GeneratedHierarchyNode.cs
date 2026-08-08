// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedHierarchyNodeSchema",
    SchemaId = "sample/generated-hierarchy-node/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true,
    Streaming = true,
    HierarchicalRows = true)]
public sealed class GeneratedHierarchyNode
{
    [DataGridKey]
    public int Id { get; init; }

    [DataGridParentKey]
    public int? ParentId { get; init; }

    [DataGridColumn(
        DataGridColumnKind.Hierarchical,
        Header = "Hierarchy",
        Order = 0,
        ColumnKey = "node-hierarchy",
        TemplateKey = "GeneratedHierarchyNodeTemplate",
        SortMemberPath = nameof(Name),
        CanUserSort = false,
        IsReadOnly = true,
        Width = "120")]
    public GeneratedHierarchyNode Item => this;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Name", Order = 1, ColumnKey = "node-name", Width = "*")]
    public string Name { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Desk", Order = 2, ColumnKey = "node-desk", Width = "120")]
    public string Desk { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Price", Order = 3, ColumnKey = "node-price", FormatString = "N2", Width = "100")]
    public decimal Price { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Quantity", Order = 4, ColumnKey = "node-quantity", FormatString = "N0", Width = "100")]
    public int Quantity { get; init; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Updated", Order = 5, ColumnKey = "node-updated", FormatString = "HH:mm:ss", IsReadOnly = true, Width = "120")]
    public DateTimeOffset UpdatedAt { get; init; }

    [DataGridChildren]
    public ObservableCollection<GeneratedHierarchyNode> Children { get; } = new();

    [DataGridExpanded]
    public bool IsExpanded { get; set; }

    public string Kind => ParentId.HasValue ? "Child" : "Root";
}
