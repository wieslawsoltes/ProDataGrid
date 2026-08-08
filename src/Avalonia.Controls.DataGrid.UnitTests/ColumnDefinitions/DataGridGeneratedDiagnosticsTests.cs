// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedDiagnosticsTests
{
    [Fact]
    public void Manifest_exposes_schema_field_performance_and_fallback_coverage()
    {
        var field = new DataGridGeneratedDiagnosticField(
            "amount",
            typeof(decimal),
            canWrite: true,
            isSearchable: true,
            DataGridGeneratedFilterEditorKind.Range,
            DataGridGeneratedAnalyticsRole.PivotValue | DataGridGeneratedAnalyticsRole.ChartValue);
        var manifest = new DataGridGeneratedDiagnosticsManifest(
            "orders/v1",
            "hash",
            typeof(Row),
            strict: false,
            streaming: true,
            DataGridGeneratedPerformanceProfile.HighFrequencyStreaming,
            hasStableKey: true,
            [field],
            ["RuntimeCompatibility"],
            ["prodatagrid.rows.realized.count", "generated.stream.queued"]);

        Assert.Equal("orders/v1", manifest.SchemaId);
        Assert.True(manifest.Streaming);
        Assert.True(manifest.HasStableKey);
        Assert.True(manifest.HasFallbacks);
        Assert.Equal(2, manifest.MetricNames.Count);
        Assert.Contains("generated.stream.queued", manifest.MetricNames);
        Assert.Equal(DataGridGeneratedFilterEditorKind.Range, manifest.Fields[0].FilterEditor);
        Assert.True(manifest.Fields[0].AnalyticsRoles.HasFlag(DataGridGeneratedAnalyticsRole.ChartValue));
    }

    private sealed record Row(decimal Amount);
}
