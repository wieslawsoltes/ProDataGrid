# Heatmaps, treemaps, sunbursts, and gauges

The advanced chart families use the existing `ChartSeriesKind`, `ChartDataSnapshot`, `SkiaChartRenderer`, and `SkiaChartExporter` entry points. They are not a separate demo renderer. The chart model/grid adapters can supply ordinary matrix series; an independent `HierarchyChartDataSource` supplies typed, nested trees with drill-down navigation.

## Matrix heatmap

Each series is a row; each category/value index is a column. Rows may have different lengths. Missing and non-finite values leave holes and cannot be hit. Original X values, series indices, and point indices remain available in tooltips.

```csharp
var snapshot = new ChartDataSnapshot(
    new string?[] { "Mon", "Tue", "Wed" },
    new[]
    {
        new ChartSeriesSnapshot("North", ChartSeriesKind.Heatmap, new double?[] { 12, 18, 25 }),
        new ChartSeriesSnapshot("South", ChartSeriesKind.Heatmap, new double?[] { 8, null, 31 })
    });
var style = new SkiaChartStyle
{
    ShowLegend = false,
    ShowDataLabels = true,
    Advanced = new SkiaAdvancedChartStyle
    {
        HeatmapMinimum = 0,
        HeatmapMaximum = 40,
        HeatmapCellGap = 1,
        ShowHeatmapColorScale = true
    }
};
byte[] png = SkiaChartExporter.ExportPng(snapshot, 960, 540, style);
string svg = SkiaChartExporter.ExportSvg(snapshot, 960, 540, style);
```

Heatmap bounds can be derived from the finite data or fixed for comparisons between charts. Constant data maps to the middle of the color interval. Set `HeatmapCenterColor` and optionally `HeatmapCenterValue` for a diverging scale. Non-finite or reversed configured bounds are rejected. Category and row labels are thinned according to the available space; labels that cannot fit are omitted rather than displaying truncated numbers.

## Typed hierarchies and drill-down

```csharp
var root = new ChartHierarchyNode("portfolio", "Portfolio", new[]
{
    new ChartHierarchyNode("hardware", "Hardware", new[]
    {
        new ChartHierarchyNode("laptops", "Laptops", 60),
        new ChartHierarchyNode("desktops", "Desktops", 40)
    }),
    new ChartHierarchyNode("services", "Services", 80)
});
var source = new HierarchyChartDataSource(root, ChartSeriesKind.Treemap);
var snapshot = source.BuildSnapshot(new ChartDataRequest());

// A typed hierarchy hit's PointIndex is its preorder node index.
source.TryDrillDown("hardware");
var breadcrumbs = source.GetPath();
source.TryDrillUp();
source.Kind = ChartSeriesKind.Sunburst;
```

Leaf weights must be finite and nonnegative. Branch totals are calculated from descendant leaves; children are defensively copied. A `ChartHierarchySnapshot` builds an immutable preorder index and rejects duplicate stable IDs. The maximum tree depth is 64 edges and overflowing aggregate weights are rejected. Zero-weight nodes remain in the data model but do not consume visible area.

`ChartHierarchyLayout` is UI/rendering independent. It supplies squarified treemap rectangles, weighted sunburst sectors, and geometric hit testing. With zero gaps and branch-header height, treemap leaf areas partition the rectangle proportionally to their weights. Branch headers reserve a target for drill-down. Sunburst rings correspond to hierarchy depth and use the same weighted angles for painting and pointer selection.

`HierarchyChartDataSource` caches immutable snapshots, supports point-index or stable-ID drill-down, drill-up, reset, root replacement, and owned breadcrumb paths. Notifications are synchronous on the calling thread and raised outside the state lock. UI hosts must dispatch appropriately. Point budgets and numeric index windows deliberately do not sample a hierarchy: removing a parent or child would change the represented structure and totals. Wire `TryDrillDown(hit.PointIndex)` to the host's click/selection command; the library does not silently change the application's selection behavior.

Ordinary chart snapshots also work with `Treemap` and `Sunburst`: each series is a branch and each category is a leaf. A single series projects directly to category leaves. Missing/non-finite values are omitted; negative weights are rejected. Leaf hit indices map back to the original matrix series/category. A matrix branch hit uses `PointIndex = -1` and exposes its total and label, so applications should not treat a branch header as a leaf index. Prefer typed hierarchy data for navigation. Avoid adapter downsampling for these structural chart types (`ChartDownsampleMode.None`).

## Gauges

Use `ChartSeriesKind.Gauge`. Each value receives a stable slot in a responsive grid; missing values leave empty slots. Configure the shared interval using `ValueAxisMinimum` and `ValueAxisMaximum` (defaults: 0 and 100). Values outside the interval are clamped only for painting; hit-test results retain their actual value. Gauge intervals must be finite and strictly increasing.

`GaugeStartAngle`, `GaugeSweepAngle`, and `GaugeThickness` configure radial geometry. Angles run clockwise and zero points to the right. Hit testing uses the annular sector, excluding the center hole and the unused angular span. `ShowDataLabels` displays the value and existing series/global data-label formatters are honored. `ShowCategoryLabels` displays the category or series name.

## Rendering, exports, and limits

A `SkiaChartStyle` copy retains its immutable `Advanced` options. Replace those options, for example with a record `with` expression, to change chart-family geometry. The renderer retains only managed layout data keyed by snapshot identity/version, bounds, and style; paint/path resources are scoped to a draw. Pointer heatmap lookup is constant-time after layout. Hierarchical rendering and hit testing share the same geometry. PNG and SVG exports use the same renderer.

Snapshots must have stable contents for their lifetime/version, as with the existing render cache. Create a new snapshot or advance its version when data changes. These plot families cannot be mixed with incompatible Cartesian or other advanced families in one plot; use separate chart views for a dashboard. They do not add 3D surfaces, geographic maps, Sankey/network diagrams, automatic drill-down UI controls, or a certified accessibility tree. Hierarchical geometry is cached, but this work does not claim a measured GPU/frame-rate speedup or benchmark million-cell heatmap rendering.

Regression coverage includes ownership, identifier/depth/overflow validation, seeded area partition tests, sector containment, navigation and snapshot stability, ragged/missing data, extreme bounds, original hit indices, style invalidation, and actual PNG/SVG rendering for all four families.
