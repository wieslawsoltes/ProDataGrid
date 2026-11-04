# Synchronized multi-series streaming

`StreamingMultiSeriesChartDataSource` stores a fixed set of scalar channels on one common X grid. One append commits the complete row, so a consumer never captures a partially updated set of series. Batches use flat row-major spans, emit one notification and retain only the chronological tail when capacity is exceeded.

## Define channels and append complete rows

```csharp
using ProCharts;

var source = new StreamingMultiSeriesChartDataSource(65_536, new[]
{
    new StreamingChartSeries("Reference"),
    new StreamingChartSeries("Tracking"),
    new StreamingChartSeries("Load", ChartSeriesKind.Area)
});

// Position in the span follows the fixed schema order. Null is a gap in that channel only.
source.Append(1, new double?[] { 101.5, null, 0.2 }, "First");
source.AppendRange(
    new double[] { 2, 3 },
    new double?[] { 102.0, 101.75, 0.3, 101.8, 101.9, -0.1 },
    new string?[] { "Second", "Third" });
```

For allocation-sensitive producers, reuse an input buffer or use `stackalloc` with synchronous span calls. The source copies accepted data; it does not retain caller arrays/spans. Do not mutate those buffers during the call. Each row supplies exactly one value per series. The schema itself is copied on construction and cannot be changed while streaming. Names may be null or repeated; stable series identity is its schema position, not its label.

Supported kinds are Line, Area and Scatter. Existing series styling remains configurable through `ChartSeriesStyle`. Range areas, OHLC, bubble sizes, stacked semantics, hierarchy and matrix channels need their corresponding sources; unsupported kinds are rejected instead of exposing incomplete data.

### Important renderer boundary

**The current Skia Line/Area renderer is category-indexed. It does not position those series using their numeric X arrays, and its line/area hit results have a null XValue.** Use a Category axis and labels for that presentation; recover original X from `captured.Snapshot.Series[hit.SeriesIndex].XValues[hit.PointIndex]`, or use the common row identity map. The source still preserves exact X data, but storage is not a numeric-line-rendering implementation. Reduced category points are equally spaced even when their original X intervals differ.

For true numeric/date X positioning, define the channels as `ChartSeriesKind.Scatter` and configure the model's category axis as Value or DateTime. Numeric scatter positions and hit X values are separately tested with nonuniform input X. Do not mix category-indexed line/area and numeric scatter expecting common pixel positions merely because their source X arrays match. Numeric-X line/area rendering remains separate work.

X must be finite and strictly increasing across rows, gaps, batches and history eviction. Missing/nonfinite Y values become independent gaps. A series with `valueAxisKind: ChartAxisKind.Logarithmic` also converts nonpositive Y values to gaps **before selection**; configure the assigned renderer axis to match. A logarithmic scatter X axis additionally requires positive source X at the application boundary.

## UI delivery and ownership

For the category-indexed Line/Area example above, on the Avalonia UI thread:

```csharp
using ProCharts.Avalonia;

using var delivered = ChartDataSourceDispatch.Create(source);
using var model = new ChartModel();
using (model.DeferRefresh())
{
    model.CategoryAxis.Kind = ChartAxisKind.Category;
    model.Request.MaxPoints = 500;
    model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
    model.DataSource = delivered;
}
// Bind model to ProChartView.ChartModel while this owner remains alive.
```

Keep the model and adapter in the owning component; the using declarations illustrate shutdown order, not a factory returning disposed objects. Model/request/view mutation belongs to the UI thread. A producer may append to the thread-safe source. Coalescing changes notification delivery, not accepted rows or their transaction boundaries. Stop/join application producers as needed before disposing consumers. The source owns managed storage and does not implement IDisposable; the adapter does not transfer source ownership.

Raw source events run outside its state lock, on the calling thread. Concurrent callers cannot tear rows, but the source does not reorder competing producers. Serialize timestamps before submission; a late or duplicate row is rejected. Subscriber exceptions occur **after commit** and cannot roll back accepted data. Do not retry the same row merely because an event subscriber threw. A subscriber reading after another successful producer may observe a newer complete snapshot, not necessarily the row that caused its callback.

## Common-index selection, not independent resampling

Every output series shares exactly the same owned X collection and row order. For each channel the source calls the existing `ChartSampleDecimator`, then takes the union of selected original indices. It copies every channel at those indices. There is no averaging, interpolation or fabricated value.

`MaxPoints` is a **per-series soft selection budget**, not a maximum common row count. A point important only to one channel is still retained for all of them. Finite-run endpoints and gap markers take precedence. The first and last requested rows are always retained, even when all channels are missing. Thus repeated all-missing data reduces to both X endpoints, rather than making time stop at the first gap.

With multiple series, disjoint extrema and staggered gaps can make the union approach the full requested window. This is deliberate: pruning that union back to a hard cap could drop a channel's extrema or bridge its gaps. Use a bounded `WindowCount` when an upper bound on display rows is required. Budget None/null/nonpositive disables reduction; one promotes to two. Adaptive normalizes to MinMax. Bucket selects original positions rather than aggregating values. LTTB follows the existing decimator's floating-point conventions. Decimation changes represented detail and is not pixel-equivalent to full rendering.

## Stable original row identity

```csharp
StreamingMultiSeriesChartView captured = source.BuildView(model.Request);
// Render or export captured.Snapshot, then use a hit from that same snapshot.
long originalRow = captured.SourceSampleIndices[hit.PointIndex];
double originalX = captured.Snapshot.Series[hit.SeriesIndex].XValues![hit.PointIndex];
```

Use this map only with hits from that exact snapshot. Lifetime row numbers count accepted observations from zero, including gaps and evicted rows. They are not X coordinates or current retained-window positions. `FirstRetainedSampleIndex`, `RetainedCount`, `WindowStart`, `WindowCount` and `TotalSamples` are captured together with the snapshot. Separate calls to live source properties can observe different moments while producers run.

Returned numerical/category arrays and identity maps are read-only and owned; later append, wrap or Clear cannot change them. Styles and formatter delegates retain shared-reference semantics. Holding many historical views retains their memory. Clear releases source-held category references and values, resets X ordering and numbering, and starts a new session; identities are not unique across Clear sessions. Empty valid batches and repeated Clear on an empty source are no-ops. Invalid shape, nonfinite/nonincreasing X, or counter exhaustion leaves data, counters, cached view and notifications unchanged.

## Performance and verification

Storage is O(capacity × series), with X/categories stored once and a reusable O(capacity) selection bitmap. Scalar append is O(series); a batch validates O(input rows) X values and copies at most O(min(input rows, capacity) × series) cells. Oversized discarded X prefixes are still validated and counted. Clear touches retained storage. Snapshot preparation is O(window rows × series), including selection when requested, and holds the source lock. Appenders can therefore wait for large snapshot scans. Output values cost O(common output rows × series) storage; temporary independent selection arrays also allocate during uncached reduced views. Warm append/batch calls and cache-equivalent view reads allocate no managed memory in the source; callers, subscribers, dispatcher jobs and new snapshots may allocate independently.

The existing benchmark executable now includes actual independent single-series sources versus the synchronized source, not a shifting-list surrogate. Each accepts the same 512 rows in sixteen batches of 32 after seeding capacity 8,192 or 32,768, with four or eight channels. Scenarios measure ingestion alone and ingestion plus an un-reduced snapshot after every batch. Construction, input preparation and reset/seeding are excluded. Notifications, snapshots, lifetime/retained counts and every final X/value element are checked. Two paired warmups precede seven alternating-order measured pairs.

```sh
dotnet run --project tests/ProCharts.Benchmarks/ProCharts.Benchmarks.csproj -c Release
```

The MULTI-SERIES section is retained in the existing read-only workflow's `performance.txt`. Independent sources are invoked serially by that benchmark harness; it does not give their production API a cross-source transaction. Un-reduced numerical results are equivalent, but notification/snapshot object counts differ. Independent reduction is not compared as equivalent work because it does not necessarily yield a common X grid; separate UNION diagnostics report the coordinated output count and verify original values without claiming a speedup for different output geometry.

Unit tests cover schema/shape validation, rejected batches including discarded prefixes, ownership/wrap/Clear, independent/log gaps, exact reference-union selection, no bridging of missing rows, shared X and identity maps, duplicate-producer rejection, concurrent captures, callbacks outside the lock, and strict warm allocation/cache checks. Avalonia headless cases combine real worker producers, dispatcher coalescing, bounded follow-latest, native exports and original hit mapping, separately for category Line/Area and numeric Scatter. An additional nonuniform-X test distinguishes numeric placement from category placement on both indexed and reference hit paths.

This is not a multi-feed timestamp join, resampler, archive, externally transactional indicator engine, late-data editor, source-generated schema system or GPU/frame scheduler. Calculate indicators for each incoming observation before assembling its row. Indicator state and source append remain separate transactions; validate ordering upstream before advancing stateful calculators. No physical GPU, frame-rate, peak-memory, worst-case latency or complete industry-parity claim is implied.

See [update delivery](procharts-update-delivery.md), [incremental indicators](procharts-incremental-indicators.md), and [performance](procharts-performance.md).
