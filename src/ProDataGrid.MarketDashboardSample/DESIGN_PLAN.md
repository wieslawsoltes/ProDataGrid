# Market Dashboard Design Plan

## Objective

Build a dedicated Avalonia sample app that mirrors the visual structure of the supplied CoinMarketCap-style screenshot while exercising the newest ProCharts financial capabilities and the dense-grid workflows that ProDataGrid is meant to demonstrate.

## Success criteria

- Add native financial chart support to `ProCharts` instead of faking candles with custom overlays.
- Use the new chart types in a dedicated sample app, not as an isolated renderer demo.
- Keep the view passive and push all state changes through a `ReactiveObject` view model.
- Reproduce the screenshot's three-zone layout: watchlist rail, main chart stack, and trading/metrics sidebar.
- Use `DataGrid` for the high-density watchlist and trade history surfaces to show virtualization and templated cells under realistic dashboard density.

## Product decisions

### 1. Extend `ProCharts` first

The screenshot’s primary interaction is a financial price chart. Building the app before the charting surface supported OHLC data would force sample-only drawing logic and violate the repository’s architecture rules. The chart layer therefore grows first:

- New `ChartSeriesKind` values: `Candlestick`, `HollowCandlestick`, `Ohlc`, `Hlc`, `HeikinAshi`, `Renko`, `Range`, `LineBreak`, `Kagi`, `PointFigure`
- New `ChartSeriesSnapshot` financial inputs: `OpenValues`, `HighValues`, `LowValues`
- Renderer support in `ProCharts.Skia` for:
  - range calculation
  - cartesian drawing
  - hit testing
  - data labels
  - legend glyphs
  - trendline exclusion for unsupported financial series

This keeps the sample app honest: the UI consumes a real reusable chart primitive.

### 2. Ship a dedicated app

The screenshot is a full-screen trading dashboard, not a single embedded sample page. A separate project gives the sample its own startup, styles, and composition root without distorting the existing `DataGridSample` catalog shell.

## Screen composition

### Left rail

- Token watchlist rendered with `DataGrid`
- Dense rows with token badge, price, change, and FDV
- Short time-range toggles above the grid

### Center stack

- Market identity header with instrument name, price, and chart-mode toggle
- Main `ProChartView` for price candles, hollow candles, Heikin-Ashi, OHLC, and HLC
- Secondary `ProChartView` for volume bars sharing the same chart window
- Bottom `DataGrid` for trade history

### Right rail

- Compact market metadata card
- Wallet/trade-entry card with segmented buy/sell controls
- Flow summary card with a sparkline area chart

## Data model plan

- `MarketCandle` is the canonical financial record.
- `MarketFinancialChartDataSource` maps candles into `ChartSeriesSnapshot` with close prices in `Values` and OHLC arrays in the new financial properties.
- `MarketVolumeChartDataSource` shares the same source candles and mirrors the active chart window.
- `MarketSparklineChartDataSource` feeds the sidebar flow chart.
- `MarketDashboardViewModel` owns all derived UI state:
  - chart mode switching
  - price and percentage summaries
  - wallet connection state
  - watchlist and trade history collections

## Interaction plan

- Default the hero chart to `Candlestick`
- Allow instant switching between candle-aligned financial modes: `Candlestick`, `HollowCandlestick`, `HeikinAshi`, `Ohlc`, and `Hlc`
- Keep price and volume charts window-synced through `ChartDataRequest.WindowStart` and `WindowCount`
- Keep the dashboard on candle-aligned chart modes so the live volume and trade-count companion strips stay synchronized with the primary chart window
- Use hit-tested tooltip formatting so the price chart can show OHLC or HLC values without custom overlay logic

## Visual system

- Dark navy frame over a blue-violet gradient stage to match the screenshot silhouette
- High-contrast accent blue for primary actions and chart-mode selection
- Green/red financial colors for bullish and bearish candles
- Rounded cards and soft inner panels to match crypto-dashboard density without default Fluent chrome

## Verification plan

- Renderer tests confirm financial hit testing returns full OHLC payloads across the supported financial renderers.
- Sample tests confirm the dashboard window attaches, loads the expected chart/grid composition, and keeps the price/volume windows synchronized.
- Build validation runs on the dedicated sample app project so XAML and project references stay green.
