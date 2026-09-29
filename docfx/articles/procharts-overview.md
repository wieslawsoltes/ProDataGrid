# ProCharts overview

ProCharts separates chart data, rendering and UI integration into reusable libraries. It supports Cartesian, financial, statistical, matrix and hierarchical charts. Broad charting compatibility is a development goal, not a claim of complete Excel or industry-wide parity.

## Libraries

| Library | Responsibility |
| --- | --- |
| `ProCharts` | Models, snapshots, axes, formatting, bounded streaming, hierarchy data/layout and analytical transforms. |
| `ProCharts.Skia` | Skia rendering, hit testing, interaction indexing and PNG/SVG export. |
| `ProCharts.Avalonia` | `ProChartView`, tooltips, viewport gestures and opt-in hierarchy navigation. |
| `ProDataGrid.Charting` | Grid, pivot, formula and generated-source adapters. |

## Create a live chart model

```csharp
var source = new StreamingChartDataSource(10_000, "Signal");
source.AppendRange(new[]
{
    new ChartSample(0, 12),
    new ChartSample(1, 18),
    new ChartSample(2, null),
    new ChartSample(3, 15)
});
using var model = new ChartModel { DataSource = source };
model.CategoryAxis.Kind = ChartAxisKind.Value;
model.Request.MaxPoints = 2_000;
model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
```

Bind the model to `ProChartView.ChartModel`. Keep it alive while its view is in use and dispose it when the owning application/view-model is finished. Data-source notifications run on their caller's thread; marshal worker ingestion appropriately before updating UI-bound models.

For headless use, build a `ChartDataSnapshot` through the source or construct categories and `ChartSeriesSnapshot` objects directly, then call `SkiaChartExporter.ExportPng` or `ExportSvg`. Snapshots should have stable contents; cached render/interaction state must not be fed silently mutated collections.

## Data and interaction

Grid adapters track sorting/filtering/grouping and formula results. Cached grid windows copy only the requested category/value/X/size slices; their initial source cache still needs to be built. Bounded streaming uses fixed-capacity history. Point decimation is suitable for dense continuous series, not a substitute for hierarchy/matrix semantics.

Repeated point queries can reuse indexed screen-space geometry. Index construction has an up-front time and memory cost, while warm queries avoid repeated data scans. `UseInteractionCache = false` retains the reference path, and `ClearInteractionCache` explicitly releases/invalidate managed state after in-place source changes.

`ProChartView.HitTest` accepts local logical coordinates and supports label-free numeric data. Typed treemap/sunburst sources can enable branch double-click and back/root keyboard navigation through `ChartHierarchyNavigation.IsEnabled`. The sample's Hierarchy Explorer also supplies command buttons and a branch selector.

## Guides

- [Model and snapshots](procharts-chart-model.md)
- [Data sources](procharts-data-sources.md)
- [Bounded streaming](procharts-streaming.md)
- [Efficient grid windows](procharts-windowing.md)
- [Advanced chart families](procharts-advanced-charts.md)
- [Statistical and technical indicators](procharts-indicators.md)
- [Hierarchy navigation and public hit testing](procharts-hierarchy-navigation.md)
- [Interaction](procharts-interaction.md)
- [Export and clipboard](procharts-export-clipboard.md)

Additional diagram families, comprehensive accessibility semantics and physical-GPU performance qualification remain separate work. Batch indicators do not yet provide persistent incremental indicator state, and three-line bands are not a filled inter-series range renderer.

## Filled intervals

[Range areas and analytical bands](procharts-range-area.md) adds owned interval factories, filled envelopes, paired-bound tooltips and the ProCharts Range Area sample.
