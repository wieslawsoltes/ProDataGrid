# Filled range areas and analytical bands

`ChartSeriesKind.RangeArea` fills the interval between aligned lower and upper boundaries. It is a Cartesian series, so it uses the existing renderer, gridlines, axes, legends, per-series styles, render-cache layers, data labels, hit testing and PNG/SVG exports. It is distinct from the existing financial `Range` chart kind.

```csharp
ChartSeriesSnapshot interval = ChartRangeSeries.CreateArea(
    "Forecast interval",
    lower: new double?[] { 10, 15, null, 20, 25 },
    upper: new double?[] { 30, 35, null, 40, 50 });
var snapshot = new ChartDataSnapshot(
    new string?[] { "Mon", "Tue", "Wed", "Thu", "Fri" },
    new[] { interval });
var style = new SkiaChartStyle { AreaFillOpacity = 0.35f, ShowDataLabels = true };
byte[] image = SkiaChartExporter.ExportPng(snapshot, 960, 540, style);
```

The factory defensively copies boundaries and optional X coordinates. `Values` contains midpoints, while `LowValues` and `HighValues` retain actual interval boundaries. Incomplete or non-finite pairs become gaps in all three channels; finite inverted intervals are rejected. Optional X coordinates must be finite, strictly increasing and aligned with every interval. Use a numeric, date/time or logarithmic category axis for proportional X positioning. Date/time coordinates use the renderer's existing OLE Automation date convention.

## Analytical and grid-derived data

```csharp
// Input can be a value series from an existing DataGridChartModel snapshot.
ChartBandSeries bands = ChartIndicators.BollingerBands(price, period: 20, standardDeviations: 2);
ChartSeriesSnapshot filled = bands.ToRangeArea("Bollinger envelope");
var snapshot = new ChartDataSnapshot(categories, new[] { filled, bands.Middle });
```

Donchian channels use the same conversion. The original middle/boundary line series remain independently usable. Range factories do not implicitly add low/high selectors to the grid adapter: derive bands from its value series or provide aligned boundaries explicitly.

A category-axis plot can combine filled ranges with ordinary lines/areas/columns. Numeric-X plots support range areas alongside scatter/bubble series whose X channels are aligned. The existing ordinary Line/Area renderers remain category-positioned; mixing those kinds makes the plot use category positioning. This work does not silently change existing Line/Area X semantics.

## Geometry, gaps and selection

Boundaries are linearly interpolated in projected axis coordinates. Missing/invalid intervals break the polygon; logarithmic value axes require both boundaries to be positive. Raw snapshots bypassing the factory are handled defensively: ragged, non-finite and inverted pairs are gaps. Non-increasing projected X begins a new run rather than producing a self-crossing fill. Values determines the point count, but complete low/high pairs determine visible geometry even when a raw midpoint is absent.

Draw and hit testing share projection rules. Endpoints are not clamped to viewport edges before interpolation: the original sloping interval is clipped to the plot, avoiding a false bend at zoom boundaries. Non-finite projected coordinates cannot enter a native path. Very large values combined with extremely narrow explicit axes may exceed screen-coordinate precision and are omitted; this is not arbitrary-precision clipping.

Pointer hits inside the filled interval return the nearest source endpoint in X, preserving original point/series/category identity and original `LowValue`, `HighValue` and midpoint `Value`. Ties choose the earlier endpoint; no interpolated observation is invented. `Location` anchors that source interval, not the exact pointer position. The host's default tooltip displays both bounds. Boundary-column tolerance uses `HitTestRadius`; a single interval can be selected there without inventing a finite-width filled rectangle. The first matching range series takes precedence over point-only candidates in compatible mixed plots.

## Indexed interaction and ownership

Range areas with nondecreasing projected X now reuse a managed projected-interval index. A binary search skips boundaries that cannot participate in the query; candidate columns and the segment spanning the pointer are checked in their original order. Original source indices prevent missing observations from being bridged. Equal projected X values and source-order ties retain the reference behavior. A boundary beyond the hit radius is still checked when its preceding segment spans the pointer or the whole viewport.

After O(n) preparation, a query is O(log n + k) per indexed range, where k includes nearby boundary columns and a possible straddling segment. A very wide radius or dense coincident columns can still make k linear. Projected arrays retain O(n) managed storage; no native canvas, path or paint is retained by this index. Plot layout and numeric range preparation are reused as well. This does not make native drawing itself sublinear.

`SkiaChartRenderer.UseInteractionCache = false` retains the unchanged reference scan. `ClearInteractionCache()` releases all point/range/layout state and is required after in-place source edits or changes to formatter-captured state. Otherwise replace the snapshot after changing data. Bounds, relevant styles and exact nullable axis overrides invalidate the cache. Renderer instances are not thread-safe. Detached `ProChartView` instances release their renderer caches through the existing lifecycle path.

Only plots composed of Line, Area, Scatter, Bubble and RangeArea use this interaction cache, up to one million total source values. Other/mixed families and larger plots retain the full reference path. Raw range series with decreasing projected X retain their original scan rather than being sorted into a different chart; compatible plot layout can still be reused.

Reproduce cold/warm time and managed allocation diagnostics with:

```sh
dotnet run --project tests/ProCharts.Range.Benchmarks/ProCharts.Range.Benchmarks.csproj -c Release
```

The harness compares the actual public renderer APIs with caching enabled and disabled, verifies identical hits, alternates measurement order and reports cold preparation separately. Raw output is retained in the read-only charting CI artifact. It is not a GPU/frame-rate measurement.

## Styling and limits

Fill color, gradient, stroke color/width/dashes, theme overrides and `AreaFillOpacity` use existing style APIs. The legend shows an area swatch. Data labels show both bounds and honor existing numeric formatters. Paint/shader/effect resources are scoped per series and drawing geometry lists/paths use existing pools.

This adds linear range areas, not smoothed/step range interpolation, range columns/bars, interval stacking, or automatic range decimation. Preserve both aligned boundaries when preparing data; independently downsampling the two lines can misrepresent interval width. The standalone factory deliberately does not resample. No GPU/frame-rate speedup is claimed.
