# ProCharts Overview

ProCharts is an Excel-quality charting library built for ProDataGrid. It provides a renderer-agnostic chart model, a SkiaSharp renderer, and an Avalonia control for interactive charts. The core goal is fidelity to Excel chart behavior while keeping the data pipeline fast and incremental.

## Key components

- `ProCharts` defines the chart model, series definitions, axes, legends, and snapshots.
- `ProCharts.Skia` provides a SkiaSharp renderer and export helpers (PNG/SVG).
- `ProCharts.Avalonia` provides `ProChartView` for interactive rendering in Avalonia.
- `ProDataGrid.Charting` bridges DataGrid data, pivots, and formulas to chart series.

## Chart model basics

A `ChartModel` describes the data and display options. A snapshot is built from that model, and renderers draw the snapshot.

```csharp
var model = new ChartModel
{
    Title = "Revenue by Quarter",
    Series = new List<ChartSeries>
    {
        ChartSeries.Line("Q1", new[] { 120d, 140d, 110d, 180d }),
        ChartSeries.Line("Q2", new[] { 160d, 130d, 150d, 210d })
    },
    XAxis = new ChartAxis { Title = "Quarter", Kind = ChartAxisKind.Category },
    YAxis = new ChartAxis { Title = "Revenue", Kind = ChartAxisKind.Value }
};
```

## Rendering pipeline

- The model is evaluated into a `ChartSnapshot` with resolved series, axes, and labels.
- Snapshots can carry `ChartDataDelta` to enable incremental updates.
- The Skia renderer caches layers (axes, legend, data, labels) for performance.
- Downsampling can be applied (bucket, LTTB, or windowed) to keep large series interactive.

## Integration with ProDataGrid

`ProDataGrid.Charting` provides models that reflect DataGrid state:

- `DataGridChartModel` builds series from DataGrid rows with sorting/filtering applied.
- `PivotChartDataSource` uses pivot tables and calculated measures.
- Formula-driven measures flow from the formula engine into chart series.

This makes charts update as the grid filters, sorts, groups, or recalculates formulas.

## Export and headless rendering

- `ProChartView` supports copy and export to PNG/SVG.
- `SkiaChartExporter` supports headless export for servers and CI.

```csharp
var png = chartView.ExportPng();
var svg = chartView.ExportSvg();
await chartView.CopyToClipboardAsync(ChartClipboardFormat.Png);
```

## Performance tips

- Use windowing (`WindowStart`/`WindowCount`) for streaming or huge series.
- Prefer aggregation or downsampling for dense line series.
- Keep legend and label density reasonable for fast layouts.

## See also

- `procharts-architecture.md`
- `procharts-chart-model.md`
- `procharts-series-types.md`
- `procharts-axes-and-scales.md`
- `procharts-data-sources.md`
- `procharts-interaction.md`
