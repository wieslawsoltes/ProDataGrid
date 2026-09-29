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
