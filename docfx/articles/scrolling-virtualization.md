# Scrolling and Virtualization

The v2 templates switch ProDataGrid to a ScrollViewer-based layout that implements `ILogicalScrollable`. This improves scroll chaining, inertia, and template interoperability.

## ScrollViewer-Based Implementation (v2)

ProDataGrid ships a ScrollViewer-based template that implements `ILogicalScrollable` on `DataGridRowsPresenter`. This removes the custom `PART_VerticalScrollbar`/`PART_HorizontalScrollbar` pair and lets Avalonia handle scroll bars, scroll chaining, and inertia.

Add the v2 theme to opt into the new template (enables `UseLogicalScrollable` by default):

```xml
<!-- Fluent -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />

<!-- or Simple -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
```

If you use a custom control template, wrap `DataGridRowsPresenter` in a `ScrollViewer` named `PART_ScrollViewer` and set `UseLogicalScrollable="True"`. Keep the column headers in a separate row so they stay fixed while rows scroll.

## Logical Scrolling

```xml
<DataGrid UseLogicalScrollable="True" />
```

## Scroll Snap Points and Anchoring

The v2 templates expose the underlying `ScrollViewer`, so you can enable snap points for row-aligned scrolling. Apply snap points after the template is applied and keep them in sync with user settings.

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

dataGrid.TemplateApplied += (_, __) =>
{
    var scrollViewer = dataGrid.FindDescendantOfType<ScrollViewer>();
    if (scrollViewer != null)
    {
        scrollViewer.VerticalSnapPointsType = SnapPointsType.MandatorySingle;
        scrollViewer.VerticalSnapPointsAlignment = SnapPointsAlignment.Near;
    }
};
```

See the `Scroll Interactions` sample page for snap points, scroll anchoring, and row details with variable heights.

## Recycle Pool Controls

Row recycling after resize/shrink is governed by three styled properties:

- `TrimRecycledContainers` (default `False`): trims the recycled row/group-header pool to a small buffer when the viewport contracts (e.g., maximize then restore). Set to `True` to bound the recycle pool; leave `False` for a larger cache.
- `KeepRecycledContainersInVisualTree` (default `True`): when `True`, recycled rows stay as hidden children; set to `False` to remove recycled containers from the presenter.
- `RecycledContainerHidingMode` (default `MoveOffscreen`): choose how recycled containers are hidden when `KeepRecycledContainersInVisualTree=True` - either move far offscreen or just set `IsVisible=False` (TreeDataGrid-style).

Typical usage:

```xml
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="False"
          KeepRecycledContainersInVisualTree="True"
          RecycledContainerHidingMode="MoveOffscreen" />

<!-- Bounded recycle pool, removed from tree -->
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="True"
          KeepRecycledContainersInVisualTree="False" />

<!-- TreeDataGrid-style visibility hiding -->
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="False"
          KeepRecycledContainersInVisualTree="True"
          RecycledContainerHidingMode="SetIsVisibleOnly" />
```

A sample toggle UI is available on the "Resize recycling diagnostics" page in the sample app.

## Row Height Estimators

Scrolling with variable row heights is driven by pluggable estimators via `RowHeightEstimator`:

- `AdvancedRowHeightEstimator` (default): regional averages + Fenwick tree for accurate offsets.
- `CachingRowHeightEstimator`: caches per-row heights for predictable datasets.
- `DefaultRowHeightEstimator`: average-based for uniform rows.

Override the estimator per grid:

```xml
<DataGrid UseLogicalScrollable="True">
  <DataGrid.RowHeightEstimator>
    <CachingRowHeightEstimator />
  </DataGrid.RowHeightEstimator>
</DataGrid>
```

## Large Data Sets

For very large sources, keep `UseLogicalScrollable="True"` and prefer uniform row heights where possible. The sample app includes `LargeUniform` and `LargeVariableHeight` pages that stress-test scrolling with hundreds of thousands of rows.

## Migrating Existing Usage

- Prefer the v2 theme or update your template to use the ScrollViewer pattern; legacy scroll bars remain available when `UseLogicalScrollable="False"`.
- Replace direct access to template scroll bars with `ScrollViewer` APIs (`ScrollChanged`, `Offset`, `Extent`, `Viewport`).
- When handling wheel/gesture input, rely on the built-in logic (it routes through `UpdateScroll` when `UseLogicalScrollable` is true).
- For theme v2, ensure frozen columns and header separators are kept in sync with horizontal offset (the supplied templates already do this).
- If you depend on stable scroll positioning with dynamic row heights, choose the estimator that matches your data set and reset it after data source changes if needed.
