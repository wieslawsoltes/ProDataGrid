# ProCharts Series Types

Series types are defined by `ChartSeriesKind`. Each series snapshot also includes optional X values, size values, financial high/low/open/close values, and per-series styles.

## Cartesian series

- `Line`, `Area`, `Column`, `Bar`
- `Scatter`, `Bubble`
- `Candlestick`, `HollowCandlestick`, `Ohlc`, `Hlc`, `HeikinAshi`, `Renko`, `Range`, `LineBreak`, `Kagi`, `PointFigure`
- `StackedColumn`, `StackedBar`, `StackedArea`
- `StackedColumn100`, `StackedBar100`, `StackedArea100`
- `Waterfall`
- `Histogram`, `Pareto`
- `Radar`
- `BoxWhisker`
- `Funnel`

## Pie and donut

- `Pie`
- `Donut`

Pie and donut series ignore category axes and render labels around the arc.

## X/Y series

`Scatter` and `Bubble` series can use explicit `XValues`. Bubble series can also use `SizeValues`.

```csharp
new ChartSeriesSnapshot(
    name: "Speed",
    kind: ChartSeriesKind.Scatter,
    values: new double?[] { 4, 6, 7, 3 },
    xValues: new double[] { 1, 2, 3, 4 })
```

## Financial series

`Candlestick`, `HollowCandlestick`, `Ohlc`, `HeikinAshi`, `Renko`, `Range`, `LineBreak`, `Kagi`, and `PointFigure` use the `Values` collection for close prices and populate `OpenValues`, `HighValues`, and `LowValues` with matching category indexes. `Hlc` omits `OpenValues` and uses `HighValues`, `LowValues`, and `Values` for high/low/close. `PointFigure` can also use `SizeValues` to carry the box count per rendered column.

```csharp
new ChartSeriesSnapshot(
    name: "LINK / USD",
    kind: ChartSeriesKind.Candlestick,
    values: new double?[] { 9.11, 9.08, 9.14 },
    openValues: new double?[] { 9.02, 9.11, 9.08 },
    highValues: new double?[] { 9.16, 9.13, 9.22 },
    lowValues: new double?[] { 8.98, 9.01, 9.04 })
```

The Skia renderer uses these arrays for value-range calculation, hit testing, legend glyphs, and data labels.

Financial styling in `SkiaChartStyle` also exposes chart-specific controls such as body width, box width, hollow bullish bodies, wick/tick widths, and a last-price overlay. The renderer now gives `HollowCandlestick` previous-close-aware color semantics, `Range` a candle-style derived bar treatment, `Kagi` a stepped line treatment, and `PointFigure` a proper X/O glyph treatment instead of reusing candlestick bodies.

## Trendlines and error bars

`ChartSeriesSnapshot` supports trendlines and error bars:

- `TrendlineType`: `Linear`, `Exponential`, `Logarithmic`, `Polynomial`, `Power`, `MovingAverage`.
- `ErrorBarType`: `Fixed`, `Percentage`, `StandardDeviation`, `StandardError`.

Use `ChartSeriesSnapshot` parameters to enable them per series.
