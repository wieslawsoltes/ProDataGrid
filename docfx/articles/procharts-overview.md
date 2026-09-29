# ProCharts overview

ProCharts separates chart data, rendering and UI integration into reusable libraries. It supports Cartesian, financial, statistical, matrix and hierarchical charts. Broad charting compatibility is a development goal, not a claim of complete Excel or industry-wide parity.

## Libraries

| Library | Responsibility |
| --- | --- |
| `ProCharts` | Models, snapshots, axes, formatting, bounded scalar/interval streaming, hierarchy data/layout, batch transforms, persistent incremental indicators and coordinated interval decimation. |
| `ProCharts.Skia` | Skia rendering, hit testing, interaction indexing and PNG/SVG export. |
| `ProCharts.Avalonia` | `ProChartView`, tooltips, viewport gestures and opt-in hierarchy navigation. |
| `ProDataGrid.Charting` | Grid, pivot, formula and generated-source adapters. |

## Create a live chart model

```csharp
var source = new StreamingChartDataSource(10_000, "Signal", ChartSeriesKind.Scatter);
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

The example uses Scatter with a numeric category axis for proportional X positioning. Existing Line/Area kinds use category-index positioning. RangeArea can use either category indices or aligned numeric/date/log X coordinates; mixing it with ordinary Line/Area uses category positioning for the plot.

For headless use, build a `ChartDataSnapshot` through the source or construct categories and `ChartSeriesSnapshot` objects directly, then call `SkiaChartExporter.ExportPng` or `ExportSvg`. Snapshots should have stable contents; cached render/interaction state must not be fed silently mutated collections.

## Live calculations and filled intervals

Batch `ChartIndicators` methods analyze complete datasets. Persistent `StreamingExponentialMovingAverage`, `StreamingRelativeStrengthIndex`, `StreamingMacd` and `StreamingAverageTrueRange` instead consume one observation at a time, retaining constant-size recurrence state with no managed allocations in Push/Reset. These are single-writer append-only calculators; corrections require reset/replay, and display-ring eviction does not reset calculation history.

`ChartRangeSeries.CreateArea` owns aligned lower/upper channels, and `ChartBandSeries.ToRangeArea()` turns Bollinger or Donchian bands into a filled envelope. Missing pairs break the fill. Interior tooltips retain endpoint identity and display both bounds.

`RangeChartDataSource` adds owned input, window-before-decimation preparation, atomic replacement and cached display views with original source-index maps. Paired MinMax/Adaptive selection keeps both boundaries' extrema and gap separators at shared source indices. Use a numeric/date/log X axis to retain original spacing; configure the source's logarithmic value domain when applicable. The reduced polygon approximates omitted detail rather than certifying an enclosing envelope. `None` restores every supplied interval. The **ProCharts Range Area** sample has an optional 100,000-observation mode with an 800-point reduction toggle; its original small mode retains the independent mean line.

`StreamingRangeChartDataSource` extends bounded ingestion to interval pairs: append validated batches without replacing the full retained dataset, preserve original 64-bit observation maps, and reduce ring windows through the same paired selector. It enforces increasing X, commits batches atomically and publishes owned snapshots. The Range Area page's **Open bounded live interval demo** provides a command-driven synthetic feed, not an external live connection. See [bounded streaming range areas](procharts-streaming-ranges.md) for threading, session identity, windowing and performance contracts.

## Data and interaction

Grid adapters track sorting/filtering/grouping and formula results. Cached grid windows copy only the requested category/value/X/size slices; their initial source cache still needs to be built. Bounded streaming uses fixed-capacity history. Continuous-series decimation is not a substitute for hierarchy/matrix semantics, and interval boundaries must not be sampled independently.

Repeated supported point and range-area queries can reuse indexed screen-space geometry. Index construction has an up-front time and memory cost, while warm queries avoid repeated data scans. `UseInteractionCache = false` retains the reference path, and `ClearInteractionCache` releases or invalidates managed state after in-place source changes. Ordered ranges use projected-interval binary search without bridging gaps or changing source-order selection; unordered raw ranges retain their scan. See the range-area guide for ownership, limits and separate interaction/full-versus-reduced CPU drawing diagnostics.

`ProChartView.HitTest` accepts local logical coordinates and supports label-free numeric data. Typed treemap/sunburst sources can enable branch double-click and back/root keyboard navigation through `ChartHierarchyNavigation.IsEnabled`. The sample's Hierarchy Explorer also supplies command buttons and a branch selector.

## Guides

- [Model and snapshots](procharts-chart-model.md)
- [Data sources](procharts-data-sources.md)
- [Bounded scalar streaming](procharts-streaming.md)
- [Bounded streaming range areas](procharts-streaming-ranges.md)
- [Incremental EMA, RSI, MACD and ATR](procharts-incremental-indicators.md)
- [Efficient grid windows](procharts-windowing.md)
- [Advanced chart families](procharts-advanced-charts.md)
- [Statistical and technical indicators](procharts-indicators.md)
- [Filled range areas, coordinated decimation and analytical bands](procharts-range-area.md)
- [Hierarchy navigation and public hit testing](procharts-hierarchy-navigation.md)
- [Interaction](procharts-interaction.md)
- [Export and clipboard](procharts-export-clipboard.md)

Additional diagram families, comprehensive accessibility semantics, incremental versions of the remaining batch indicators, coordinated multi-series overlay selection and physical-GPU performance qualification remain separate work. Measured preparation, calculation or CPU bitmap improvements do not imply measured UI frame-rate or GPU gains.
