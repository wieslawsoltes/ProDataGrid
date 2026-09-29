# Filled range areas and analytical bands

`ChartSeriesKind.RangeArea` fills the interval between aligned lower and upper boundaries. It uses the existing Cartesian renderer, axes, legends, series styles, data labels, hit testing, render-cache layers and PNG/SVG exports. It is distinct from the financial `Range` kind.

```csharp
ChartSeriesSnapshot interval = ChartRangeSeries.CreateArea(
    "Forecast interval",
    lower: new double?[] { 10, 15, null, 20, 25 },
    upper: new double?[] { 30, 35, null, 40, 50 });
var snapshot = new ChartDataSnapshot(
    new string?[] { "Mon", "Tue", "Wed", "Thu", "Fri" }, new[] { interval });
var style = new SkiaChartStyle { AreaFillOpacity = 0.35f, ShowDataLabels = true };
byte[] image = SkiaChartExporter.ExportPng(snapshot, 960, 540, style);
```

The factory copies boundaries and optional X coordinates. `Values` contains midpoints; `LowValues` and `HighValues` retain the actual bounds. Incomplete/non-finite pairs become gaps and finite inverted pairs are rejected. Optional X coordinates must be finite, strictly increasing and aligned. Numeric, date/time and logarithmic category axes position those coordinates proportionally; dates use OLE Automation coordinates.

## Analytical and grid-derived data

```csharp
ChartBandSeries bands = ChartIndicators.BollingerBands(price, period: 20, standardDeviations: 2);
ChartSeriesSnapshot filled = bands.ToRangeArea("Bollinger envelope");
var snapshot = new ChartDataSnapshot(categories, new[] { filled, bands.Middle });
```

Donchian channels use the same conversion. Original middle/boundary line series remain independently usable. Grid values can supply the input to these transforms; this does not add direct low/high selectors to the grid adapter.

Category-positioned ranges can share plots with ordinary lines/areas/columns. Numeric-X range plots can share axes with scatter/bubble data. Existing ordinary Line/Area kinds retain category-index positioning; mixing them makes the plot use category positioning. Do not combine a decimated numeric range with a category-positioned line and expect original X spacing to be retained.

## Coordinated boundary decimation

`RangeChartDataSource` adds a reusable model-compatible source for dense intervals. It owns input arrays, applies the requested source-index window before reduction, and copies only selected output channels. Lower and upper boundaries always use the **same original indices**.

```csharp
var source = new RangeChartDataSource("Envelope", bands.Lower.Values, bands.Upper.Values,
    xValues: bands.Middle.XValues);
using var model = new ChartModel { DataSource = source };
model.CategoryAxis.Kind = ChartAxisKind.Value;
model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
model.Request.MaxPoints = 2_000;
model.Request.WindowStart = 10_000;
model.Request.WindowCount = 50_000;

// Bind model to ProChartView.ChartModel. For standalone rendering and source identity:
ChartRangeView view = source.BuildView(model.Request);
byte[] png = SkiaChartExporter.ExportPng(view.Snapshot, 960, 540,
    new SkiaChartStyle { CategoryAxisKind = ChartAxisKind.Value });
// Given a hit on this exact snapshot: originalIndex = view.SourcePointIndices[hit.PointIndex].
```

When X is omitted, the source generates zero-based original indices as numeric X, before selection. Optional category labels are copied and retained at the selected indices. A numeric/date/log category axis is required for faithful spacing after selection: a category-index axis would compress omitted observations. For a logarithmic X axis, provide positive X values explicitly rather than relying on a generated zero origin.

`None` disables reduction. `MinMax` and `Adaptive` select each interior bucket's lower minimum/maximum and upper minimum/maximum, ordered by original index with duplicate candidates removed. Each finite run keeps its first/last observations; invalid runs retain a separator. Ties select the first occurrence. Positive budgets below six are promoted to six; omitted/nonpositive budgets disable reduction. Bucket/LTTB requests are rejected, not silently treated as independent line sampling.

**The budget is soft when preserving gaps requires more output.** The selector reserves up to six points per finite run and one per gap run. A gap-free window never exceeds the normalized budget. Many alternating gaps can preserve most or all input points. Duplicate extrema and rounding to four-candidate buckets can result in fewer points than requested. Selecting a displayed index never invents or averages a new value.

The source also retains the window's final original X coordinate when its last observations are missing. The final missing endpoint can add one point beyond the selector's soft budget. This preserves the same automatic X-axis extent as the full window instead of stretching surviving data into a collapsed missing tail. An all-missing window with more than one observation retains its first and last coordinates, but still paints no interval. This coordinate-domain retention is a data-source responsibility; the standalone selector keeps its single-separator policy.

The independent `ChartRangeDecimator.SelectIndices(lower, upper, maxPoints, windowStart, windowCount)` API returns full-source indices and uses O(window size) work with O(output size) auxiliary memory. It requires a budget of at least six. It treats missing, non-finite or inverted pairs as gaps; it does not inspect X coordinates or know the renderer's axis domain. Its temporary buffer is bounded by eligible selected data, so a large requested budget does not allocate a large array for an all-gap input. The data source validates inverted pairs rather than accepting them, and supplies additional ownership, coordinate and logarithmic-domain rules.

### Logarithmic value axes

Set `valueAxisKind: ChartAxisKind.Logarithmic` on the source and configure the assigned renderer/model value axis to match. The source normalizes nonpositive intervals to gaps **before reduction**, preventing an omitted invalid interval from reconnecting visible neighbors. Finite inverted pairs still fail validation before filtering. The default source domain is linear and retains finite negative values. Recreate the source when changing its validity domain. A standalone decimator caller must perform its own logarithmic validity normalization; the default finite-pair predicate alone is not sufficient.

### Ownership, replacement and mapping

`ReplaceData` copies and validates all input before replacing state, then raises one invalidation outside the lock on the calling thread. Validation failure preserves the old source and emits no event. UI hosts must marshal background replacements appropriately. Source collections and requests must not be modified concurrently while a call reads them.

`BuildView` returns a `ChartRangeView` containing the snapshot, source count, original window and an owned `SourcePointIndices` map. `BuildSnapshot` returns that same view's snapshot through the standard interface. An unchanged normalized request reuses the last view without managed allocation; this is a single-entry cache, not an unbounded cache of every viewport. Old views and maps remain unchanged after replacement and refer to their own snapshot version. Map a renderer hit against the view that produced it, not a later source replacement. Styles and formatter delegates are forwarded by reference, following existing chart presentation contracts; numerical/category arrays and index maps are owned.

Initial construction/replacement still reads and copies the complete source. A new window scans only that window; retained output memory scales with selected points. Snapshot construction holds the source lock during selection. The source supplies one range series, not an automatically co-decimated collection of independent overlays. Consumers may reuse the index map for an aligned overlay, but that does not guarantee preservation of that overlay's separate extrema or gaps.

**Reduced geometry is approximate.** Connecting selected original intervals can omit features between them; the resulting polygon is not guaranteed to enclose every omitted interval. Use `None` for full-resolution analysis or an export that must retain every supplied sample. Selection does not claim pixel equality with full geometry, arbitrary-precision clipping or a mathematically certified enclosing envelope.

The existing **ProCharts Range Area** sample now has a 100,000-observation mode and an 800-point display-budget toggle. Turning reduction off reuses the owned input and restores every interval without recomputing the indicators. Mode changes defer refresh until data, axes and the display policy are coherent, avoiding transient full-size snapshots. The small example retains its independent mean line; dense mode demonstrates the numeric-X envelope alone. Band width, missing-data controls and windowing remain active.

## Geometry and selection

Boundaries interpolate linearly in projected axis coordinates. Missing/invalid pairs break polygons; logarithmic value axes require positive bounds. Raw snapshots are handled defensively: ragged, non-finite and inverted pairs are gaps. Non-increasing projected X starts a new run. Complete low/high pairs determine geometry even when a raw midpoint is absent.

Projection preserves original slopes when clipping to the plot instead of clamping endpoints and bending segments at zoom boundaries. Non-finite screen coordinates cannot enter native paths. Extreme values with very narrow axes can exceed float screen precision and be omitted; clipping is not arbitrary precision.

Interior hits return the nearest original endpoint within the **displayed snapshot**, with actual `LowValue`, `HighValue`, midpoint, X and label. Ties favor the earlier endpoint. With a reduced view, use its index map to recover the full-source endpoint. `Location` anchors the selected interval, not an invented interpolated observation. Boundary-column tolerance uses `HitTestRadius`; one isolated interval can be selected there without drawing a finite-width rectangle. The first matching range series takes precedence over point-only candidates in compatible mixed plots.

## Indexed interaction and rendering

Ordered ranges reuse a managed projected-interval index. Binary search skips irrelevant columns; nearby columns and a straddling segment are examined in source order. Original adjacency preserves gaps. After O(n) preparation, queries cost O(log n + k), with k potentially linear for coincident columns or wide tolerances. This retains O(n) projected data but no native canvas, path or paint. Drawing itself still visits the supplied display geometry; the source's coordinated reduction is what lowers the vertex count.

`UseInteractionCache = false` retains the original scan. `ClearInteractionCache()` releases point/range/layout state after in-place edits or formatter-captured-state changes; otherwise replace changed snapshots. Relevant styles, bounds and exact nullable axis overrides invalidate cached geometry. Renderer instances are not thread-safe. Detached views release caches through their existing lifecycle.

Only compatible Line/Area/Scatter/Bubble/RangeArea plots up to one million supplied values use this cache. Unsupported mixed plots and larger snapshots retain the full reference path. Raw decreasing X retains reference selection without reordering, while compatible layout can still be reused.

Reproduce interaction and full/reduced CPU-drawing diagnostics:

```sh
dotnet run --project tests/ProCharts.Range.Benchmarks/ProCharts.Range.Benchmarks.csproj -c Release
dotnet run --project tests/ProCharts.Decimation.Benchmarks/ProCharts.Decimation.Benchmarks.csproj -c Release
```

The latter compares approximate reduced display geometry with full geometry, not equivalent outputs. It separately records selection/copying and native CPU bitmap drawing with prepared snapshots. Raw runtime/platform, timings and managed allocations are retained by the read-only charting workflow. Neither benchmark measures physical-GPU execution, screen presentation or FPS.

## Styling and remaining scope

Fill/gradient/stroke/dashes/theme overrides and opacity use existing APIs; legends show area swatches and labels show both formatted bounds. Scoped native resources and geometry pools remain in use. Smoothed/step interpolation, range bars/columns, interval stacking, coordinated multi-series overlay selection and incremental range ingestion remain separate work. No universal application or GPU/frame-rate speedup is claimed.
