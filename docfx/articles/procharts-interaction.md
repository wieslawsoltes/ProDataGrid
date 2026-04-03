# ProCharts Interaction

`ProChartView` provides built-in interaction for tooltips, hit testing, crosshair overlays, and category-window navigation. The interaction model is split between the Avalonia host and `ChartModel`, so views stay thin and dashboards can coordinate multiple panes.

## Fundamental parity plan

The current interaction baseline is aimed at the most important desktop trading-chart behaviors:

1. Window navigation lives in `ChartModel`, not only in the view host.
2. Wheel zoom and drag pan operate on `WindowStart` / `WindowCount`.
3. Crosshair state is model-backed so multiple panes can stay synchronized.
4. Follow-latest mode keeps streaming charts pinned to the newest candles until the user pans away.
5. Reset and latest actions are available without rebuilding the data source.
6. Tool-driven workflows such as crosshair, pan, zoom-box, and measure are selectable without rewriting the host.
7. Viewport history supports undo and redo so dashboards can expose terminal-style navigation affordances.

This does not attempt full TradingView feature parity. Drawing tools, y-axis drag scaling, saved viewport state, and advanced gesture customization remain future work.

## Tooltips and hit testing

Enable tooltips and provide a formatter:

```csharp
chartView.ShowToolTips = true;
chartView.ToolTipFormatter = hit =>
    $"{hit.SeriesName}: {hit.Value}";
```

The formatter receives `SkiaChartHitTestResult`, which includes the series name, category index, and value.

## Pan and zoom

Pan and zoom operate by updating `ChartModel.Request.WindowStart` and `WindowCount`. This keeps the interaction data-driven and works with downsampling.

```csharp
chartView.EnablePanZoom = true;
chartView.PanButton = MouseButton.Left;
chartView.PanModifiers = KeyModifiers.None;
chartView.ZoomModifiers = KeyModifiers.None;
chartView.ZoomStep = 0.2;
```

Use `MinWindowCount` to prevent zooming in too far.

For programmatic control, use the `ChartModel` APIs:

```csharp
chartModel.ShowLatest(preferredWindowCount: 120, followLatest: true);
chartModel.PanWindow(-12);
chartModel.ZoomWindow(scale: 1.25, anchorRatio: 0.5, minWindowCount: 20);
chartModel.ResetWindow();
chartModel.UndoWindow();
chartModel.RedoWindow();
```

These APIs are the preferred integration point for streaming dashboards because they keep the viewport logic inside `ProCharts`.

Use `TryGetVisibleWindow(...)` when a dashboard needs to display the active range without directly reading raw request fields:

```csharp
if (chartModel.TryGetVisibleWindow(out var total, out var start, out var count))
{
    // start/count are clamped to the current dataset size
}
```

## Pointer tools

`ChartInteractionState.PointerTool` selects the primary pointer behavior:

- `Crosshair` for hover inspection
- `Pan` for drag panning
- `Zoom` for drag-to-zoom selection
- `Measure` for range measurement overlays

This keeps dashboard toolbars declarative: the view model can switch tools by updating chart state, while the host remains passive.

## Crosshair and multi-pane sync

Crosshair state lives on `ChartModel.Interaction`. That makes it possible to render one pane from pointer input and mirror the active candle position into related panes.

```csharp
priceChart.TrackCrosshair(visibleIndex, categoryLabel, value, horizontalRatio, verticalRatio);
volumeChart.Interaction.CrosshairMode = ChartCrosshairMode.VerticalOnly;
volumeChart.TrackCrosshair(visibleIndex, categoryLabel, value: null, horizontalRatio, verticalRatio: 0.5);
```

Use `ChartCrosshairMode.VerticalOnly` for stacked companion panes such as volume or trade-count strips. Use `ClearCrosshair()` when the pointer leaves the primary chart.

## Follow-latest behavior

`ChartInteractionState.FollowLatest` keeps the visible window anchored to the newest categories as data arrives. Calling `PanWindow` or `SetVisibleWindow(..., followLatest: false)` turns it off, which matches how professional market terminals stop auto-follow when the user browses historical candles.

## Keyboard and mouse settings

`ProChartView` supports these built-in behaviors:

- wheel zoom
- drag pan
- double-click to jump back to the latest visible window
- keyboard left/right pan
- keyboard `+` / `-` zoom
- keyboard `End` to jump to latest
- keyboard `Home` to reset the full window
- keyboard `Ctrl+Z` to undo viewport changes
- keyboard `Ctrl+Shift+Z` or `Ctrl+Y` to redo viewport changes
- keyboard `Escape` to clear the crosshair

You can still customize gesture behavior with:

- `PanButton` and `PanModifiers`
- `ZoomModifiers`
- `ZoomStep`
- `MinWindowCount`
- `ShowCrosshair`
- `EnableKeyboardNavigation`
