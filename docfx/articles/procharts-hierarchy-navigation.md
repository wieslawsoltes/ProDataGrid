# Hierarchy navigation and view hit testing

The Avalonia host now exposes the same selection semantics used by its tooltips:

```csharp
SkiaChartHitTestResult? hit = chartView.HitTest(new Avalonia.Point(x, y));
```

Coordinates are local logical units, not device pixels or screen coordinates. The method uses the displayed `ChartModel.Snapshot` and effective axes/theme. It works without category labels, including numeric scatter plots, gauges and heatmaps; those charts no longer lose their tooltips merely because their category-label list is empty. Missing data and points outside the plot still return no hit.

## Opt-in hierarchy gestures

```xml
<charts:ProChartView ChartModel="{Binding Chart}"
                     charts:ChartHierarchyNavigation.IsEnabled="True" />
```

Use the `ProCharts.Avalonia` XML namespace. The chart source must implement `IChartHierarchyNavigator` and supply a typed `ChartDataSnapshot.Hierarchy`. `HierarchyChartDataSource` implements the contract. Ordinary matrix-to-treemap projections are deliberately not treated as navigable sources.

Double-click a painted branch/header to enter it. Backspace or Alt+Left returns to the prior root; Home resets to the original root. Keyboard gestures respect `EnableKeyboardNavigation`. These gestures are disabled by default and do not replace existing Cartesian workflows. Turning them off removes their view-local handlers. Leaf and empty-space double-clicks do not navigate or reset an unrelated Cartesian window.

Programmatic commands work independently of the gesture flag:

```csharp
ChartHierarchyNavigation.TryDrillDown(chartView, pointerPosition);
ChartHierarchyNavigation.TryDrillUp(chartView);
ChartHierarchyNavigation.Reset(chartView);
```

Pointer hits are resolved to stable node IDs. Navigation checks that the displayed hierarchy root is still the source's current root. With `AutoRefresh = false`, a source can advance while the view displays an old snapshot; gestures then refuse to reinterpret those old point indices until the model is refreshed. Navigation and UI notifications should run on the UI thread. This is a UI consistency check, not concurrent navigation synchronization.

## Sample and lifecycle

The sample application's **ProCharts Hierarchy Explorer** page has a treemap/sunburst switch, breadcrumb text, Up/Root commands, and a branch selector with an Enter command for keyboard-only use. Commands and state live in its view model; the view code-behind only manages attachment/disposal. Tests cover compiled bindings, source commands, actual routed pointer/key events, disabled gestures, stale snapshots, tooltip data and detach/reattach.

`ProChartView` releases its bitmap, render-cache layers and managed interaction index on visual-tree detach and clears the interaction cache when its model changes. Reattachment rebuilds resources on demand.

This adds navigation controls, not a complete accessibility/automation peer tree or a keyboard focus target for every chart mark. Drill-down animation, history persistence and hierarchy editing are not implemented here. The renderer-independent source APIs remain usable with custom host controls.
