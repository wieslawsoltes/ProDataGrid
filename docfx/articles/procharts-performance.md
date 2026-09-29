# ProCharts Performance

ProCharts is designed for high-volume data while keeping interaction responsive.

## Windowed rendering

Use `ChartDataRequest.WindowStart` and `WindowCount` to render a slice of the data. This is ideal for streaming or timeline charts.

```csharp
model.Request.WindowStart = 5000;
model.Request.WindowCount = 1000;
```

## Downsampling

`ChartDataRequest.MaxPoints` and `DownsampleMode` control downsampling:

- `Bucket`: simple aggregation per bucket.
- `MinMax`: preserves extrema within buckets.
- `Lttb`: Largest Triangle Three Buckets.
- `Adaptive`: selects a mode based on data density.

```csharp
model.Request.MaxPoints = 2000;
model.Request.DownsampleMode = ChartDownsampleMode.Lttb;
```

Available modes and exact selection semantics depend on the data source. Range sources require coordinated lower/upper selection and reject Bucket/LTTB rather than independently sampling the boundaries; see [range areas](procharts-range-area.md). Reduction changes the represented geometry and is not a pixel-equivalent rendering optimization.

## Synchronized multi-series streams

`StreamingMultiSeriesChartDataSource` commits all scalar channels at each X coordinate as one atomic row. It stores X/categories once, emits one notification per row/batch and returns owned aligned snapshots. Reduction takes the union of each series' original selected indices, preserving independent gaps and a common row identity map. MaxPoints is a per-series soft budget; the common union can be larger. Snapshot preparation still scans under the source lock. Unreduced windows use at most two contiguous span copies per ring channel rather than copying every cell through an interleaved per-row loop.

The actual-source benchmark compares independent single-series streams with synchronized storage, including identical final numerical arrays and explicit notification/snapshot counts. It separately reports coordinated reduction output sizes rather than claiming independent and coordinated reduction do equivalent work. Current Skia Line/Area presentation is category-indexed; use Scatter for numeric X positioning. See [synchronized multi-series streaming](procharts-multi-series-streaming.md) for API examples, renderer boundaries, ownership and measurement details.

In the sample application, open any ordinary ProCharts chart page and select **Synchronized streams**. The lazy tab initializes three numeric Scatter channels, starts paused, and offers Run/Pause, atomic 128-row batches, Reset, latest-512-row navigation, per-channel detail budgets and a full-window mode. Its status distinguishes accepted rows, retained history, requested window and common selected output. Switching tabs or closing the page stops its timer, detaches notifications and disposes the model; reopening starts a fresh paused session. The feed is a synthetic UI-thread timer, not a worker/GPU/frame-rate benchmark. Separate headless tests exercise real worker-to-UI delivery.

## Incremental updates

If your data source implements `IChartIncrementalDataSource`, it can return `ChartDataUpdate` with a `ChartDataDelta` so the renderer avoids full rebuilds when the delta is supported. Sources must describe changes relative to the supplied previous snapshot or return false for a full rebuild.

## Notification delivery and producer bursts

Avoid rebuilding an owned snapshot for every scalar observation when only the latest presentation state is needed. Use source `AppendRange` batches where possible. The reusable `CoalescingChartDataSource` combines queued invalidations on an explicit consumer synchronization context, while `ChartDataSourceDispatch.Create` provides an Avalonia UI-dispatcher adapter. All accepted source observations remain in the source according to its retention policy; intermediate notifications/snapshots deliberately differ.

This schedules events only, not data mutations or reads. The source must support the actual concurrent access pattern. It is not a timer, fixed-rate throttle or frame scheduler. Models and adapters have separate lifetimes; see [coalesced updates and worker-to-UI delivery](procharts-update-delivery.md) for complete setup, ownership, error and scheduling contracts.

The range-streaming benchmark compares immediate scalar model refreshes, coalesced scalar delivery and producer batching, including all resulting snapshots and identical final-data checks. Its deterministic pump isolates delivery policy; headless tests separately check actual UI-thread affinity. Neither throughput measurements nor reduced callback counts imply physical-GPU/FPS or UI-latency improvements.

## DataGrid-specific tuning

`DataGridChartModel` supports aggregation and group-aware series. For large datasets:

- Use aggregation (`Sum`, `Average`, `Min`, `Max`, `Count`).
- Configure `DownsampleMode` and `DownsampleAggregation` on the model.
- Combine grouping with windowing for interactive pivots.
