# Bounded streaming charts and decimation

`StreamingChartDataSource` supplies a single numeric series through the same `IChartDataSource` contract used by the grid adapters. It does not depend on Avalonia, SkiaSharp, reflection, or an application-specific dispatcher.

```csharp
var source = new StreamingChartDataSource(capacity: 100_000, name: "Sensor");
source.AppendRange(new[]
{
    new ChartSample(0.0, 12.0),
    new ChartSample(0.1, 12.4),
    new ChartSample(0.2, null), // a real discontinuity
    new ChartSample(0.3, 13.2)
});
var snapshot = source.BuildSnapshot(new ChartDataRequest
{
    WindowStart = 0,
    WindowCount = 20_000,
    MaxPoints = 2_000,
    DownsampleMode = ChartDownsampleMode.MinMax
});
```

Assign the source to a `ChartModel` as with any other data source. Configure the category axis as numeric (`ChartAxisKind.Value`) or date/time when using X coordinates. Category labels are optional and remain null unless supplied, avoiding a string allocation for every ingested sample. Date/time X coordinates use the renderer's existing OLE Automation date convention.

## Ownership and scheduling

The capacity is fixed. Appending replaces the oldest retained sample in constant time, rather than shifting an array. A batch validates every X coordinate before changing history and emits one invalidation. A batch larger than capacity retains only its tail, while `TotalSamples` counts every input sample. Non-finite X coordinates reject the append; non-finite Y values become gaps.

Append, clear, counters, and snapshot construction are protected by a lock. Notifications occur **outside** that lock, synchronously on the calling thread. Applications that ingest on a worker thread must marshal chart/model notifications to the UI thread. The library intentionally does not select a UI dispatcher. Building a large snapshot holds the lock while selecting samples, so the intended high-throughput pattern is batched ingestion and bounded display windows, not a snapshot per individual append.

Snapshots own read-only arrays. An already returned snapshot remains unchanged after appends, wraparound, and clear. Repeating an unchanged request reuses the snapshot. Request windows are indexed within currently retained history, not lifetime sample numbers. `Clear` releases retained category references and resets the lifetime counter.

## Selection policies

`ChartSampleDecimator.SelectIndices` can also be used independently. It returns increasing original source indices; it never manufactures X coordinates or averages away a discontinuity.

| Mode | Policy |
| --- | --- |
| None | Keep every sample. |
| Bucket | Select representative original samples at regular index intervals. |
| MinMax | Retain each bucket's extrema in their original order. |
| Lttb | Select points by largest-triangle area relative to the next bucket. |
| Adaptive | Use MinMax for this streaming source. |

A budget of three within a finite run uses triangle selection to retain one interior representative. A budget of one in a streaming request is promoted to two. Null or nonpositive `MaxPoints` disables selection. Independent `SelectIndices` calls require at least two points as the budget.

**The point budget is soft when preserving gaps requires more output.** Every finite run keeps its endpoints and every missing-data run keeps a separator. Alternating finite/missing data can therefore remain larger than `MaxPoints`; silently connecting disconnected runs would misrepresent the data. For gap-free input, output is bounded by the requested count. MinMax can return fewer points when extrema coincide or the budget is odd.

The source supplies value/X/category channels. It does not synthesize OHLC or multi-channel financial data, aggregate histogram distributions, or synchronize multiple independent series. Existing grid adapters retain their own downsampling semantics; this new selector is not silently substituted into them.

## Reproducible performance checks

```bash
dotnet test src/Avalonia.Controls.DataGrid.UnitTests/Avalonia.Controls.DataGrid.UnitTests.csproj -c Release -p:CollectCoverage=false --filter FullyQualifiedName~Charting
dotnet run --project tests/ProCharts.Benchmarks/ProCharts.Benchmarks.csproj -c Release
```

The benchmark records runtime/OS, two warmups, and medians of seven measured repetitions. It compares 100,000 appends into a 4,096-sample ring with a deliberately shifting `List<T>` reference implementation, checks retained-tail equivalence, measures cached window reads, and selects one million samples down to a 2,000-point budget. Managed allocations are reported separately. Timing thresholds are not imposed on shared CI machines; semantic and allocation regressions are covered by tests.

This reference comparison is **not** a before/after measurement of the previous grid adapter, nor a GPU rendering or UI frame-rate claim. The `Charting regression and performance` workflow publishes the actual run output as an artifact alongside test results. Oversized grid/Skia list pools also now discard large buffers rather than allocating smaller replacement arrays merely to return them to the pool.
