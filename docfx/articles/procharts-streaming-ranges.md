# Bounded streaming range areas

`StreamingRangeChartDataSource` appends paired interval observations into a fixed-capacity ring instead of replacing a complete range dataset for each update. It implements the existing `IChartDataSource` and `IChartWindowInfoProvider` contracts and has no UI or native-rendering dependency.

```csharp
var source = new StreamingRangeChartDataSource(65_536, "Live envelope");
source.AppendRange(new[]
{
    new ChartRangeSample(1, 10, 20),
    new ChartRangeSample(2, 12, 23),
    new ChartRangeSample(3, null, null), // a real discontinuity
    new ChartRangeSample(4, 15, 27)
});
using var model = new ChartModel();
using (model.DeferRefresh())
{
    model.CategoryAxis.Kind = ChartAxisKind.Value;
    model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
    model.Request.MaxPoints = 2_000;
    model.DataSource = source;
}
// Bind model to ProChartView.ChartModel. Append subsequent batches in increasing X order.
```

## Input and batch contracts

X coordinates must be finite and strictly increase across observations, gaps, batch boundaries and history eviction. Numeric, date/time and logarithmic category axes preserve original spacing; dates use OLE Automation coordinates. For a logarithmic X axis, supply positive coordinates. The stream does not silently reorder late data or reinterpret equal timestamps. Correcting old observations requires Clear and replay, or use the replacement-oriented `RangeChartDataSource` instead.

Finite inverted lower/upper pairs are rejected. Missing or non-finite pairs normalize to gaps. For logarithmic value axes set `valueAxisKind: ChartAxisKind.Logarithmic` and configure the assigned renderer/model value axis to match; nonpositive pairs become gaps before reduction. Invalid inverted pairs still fail before domain masking.

Append validates one observation before changing state. AppendRange validates the entire batch before changing state, including any prefix that a capacity-sized retained tail will replace. A failed append leaves history, cached views, version and notifications unchanged. Empty batches are no-ops. Successful batches produce exactly one invalidation outside the state lock on the calling thread. Callback exceptions occur after committed data and are not a transaction rollback. UI hosts must marshal source/model notifications to their UI thread; do not bind a worker-mutated source to a UI model without that scheduling boundary.

Input spans and requests must remain stable during calls. Source append, clear, counters and view construction are synchronized; a subscriber is not guaranteed to observe one callback per unique version when several writers notify concurrently. Use a single producer or serialize application work when ordered notifications are required. No external feed, timer or synchronization context is created by the source.

## Bounded history and original identities

Capacity is fixed and at least two. Scalar append performs constant work and does not shift history. A batch validates every input but writes at most Capacity tail observations. TotalSamples counts all accepted observations since construction/Clear, including gaps and evictions; counter overflow fails before mutation. Clear releases retained label references, resets numbering and permits a new X origin. It does not change any already returned view.

`BuildView` returns a `StreamingRangeChartView` containing its snapshot, captured retained-history metadata and an owned `SourceSampleIndices` list of 64-bit observation indices. Given a hit on that exact snapshot, map `view.SourceSampleIndices[hit.PointIndex]` to the original accepted observation. This index is not the X coordinate or a current ring slot. Old views/maps retain their original data and version after appends/wrap/Clear. Indices restart at zero after Clear, so they are not identities across independent stream sessions.

WindowStart/WindowCount refer to currently retained history; they are clamped before reduction. None emits every selected observation. MinMax/Adaptive use the existing coordinated boundary selector; positive budgets promote to six. Run endpoints, gap separators and a missing final-window X coordinate can exceed the soft budget. Bucket/LTTB are rejected. The returned numeric/category/map arrays are owned and read-only. Style objects and formatter delegates retain shared-reference semantics.

## Performance and tradeoffs

Steady-state scalar/batch ingestion and unchanged normalized view reads allocate no managed objects in the library. Creating labels, subscribers, a newly built snapshot and the renderer can still allocate. Snapshot construction scans the requested window while holding the source lock and allocates output arrays; it is not constant-time or lock-free. The selector reads ring boundaries directly, so it need not first copy the complete retained history. Use batches and bounded visible windows rather than rebuilding the chart for every scalar observation.

Reduction approximates omitted geometric detail and is not guaranteed to enclose every omitted interval. None restores full retained resolution, but already evicted observations require an external archive. This is a single range-series source, not multi-series atomic synchronization, direct low/high grid selectors, an indicator calculator, persistent storage, late-data correction or new interpolation/stacking behavior.

## Sample and reproducible diagnostics

The **ProCharts Range Area** sample includes **Open bounded live interval demo**. The command-driven synthetic feed appends 64 observations at a time, retains 2,048, and allows gaps, reduction and reset. It has compiled bindings; commands/state live in the view model. It does not imply an external live connection or timer. Native tests route the append button and export the actual retained interval view.

```sh
dotnet test src/Avalonia.Controls.DataGrid.UnitTests/Avalonia.Controls.DataGrid.UnitTests.csproj -c Release -p:CollectCoverage=false --filter FullyQualifiedName~StreamingRangeChart
dotnet run --project tests/ProCharts.RangeStreaming.Benchmarks/ProCharts.RangeStreaming.Benchmarks.csproj -c Release
```

The paired harness compares actual replacement and append APIs on identical chronological windows, with ingestion-only and ingestion-plus-display cases. Precomputed source inputs and per-run seeding are excluded; data validation/copying and, in the second case, selection/snapshot ownership are included. Each run checks matching output, alternates measurement order and reports managed allocations. See its README and the charting workflow's raw `range-streaming-performance.txt`; no physical GPU, FPS or universal speedup is implied.
