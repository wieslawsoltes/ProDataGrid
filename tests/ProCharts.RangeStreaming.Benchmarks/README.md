# Bounded range ingestion and delivery diagnostics

```sh
dotnet run --project tests/ProCharts.RangeStreaming.Benchmarks/ProCharts.RangeStreaming.Benchmarks.csproj -c Release
```

## Replacement versus append

The actual replacement source and streaming source consume the same chronological windows. Each measured operation applies sixteen batches of 32 observations to history already seeded at capacity 4,096 or 65,536. One scenario measures ingestion alone; another builds a reduced display snapshot after every batch. The latter includes lock-held window scanning, coordinated selection and snapshot/map ownership.

All source inputs and replacement windows are precomputed. Initial construction and resetting/seeding each run are outside timing. No external producer array shifts or allocations are charged to either implementation. Replacement calls still validate/copy their complete input; streaming calls validate/append only the new batch. These are different APIs for the same resulting retained data, not a faster implementation of arbitrary replacement.

Two paired warmups precede seven paired measurements with alternating order. Each pair verifies matching checksums; selected boundary/X arrays and stream identity are compared before and after the measurements. Times and calling-thread managed allocations are diagnostic, not fixed CI thresholds. Source-label strings, UI subscribers, display rendering, GPU submission, screen presentation and frame pacing are not measured. Cache/view preparation is not constant-time.

## Immediate, coalesced and source-batched model delivery

The DELIVERY section uses actual `ChartModel` refreshes and `StreamingRangeChartDataSource` data. It accepts 1,024 observations in eight bursts of 128 at retained capacities 1,024 and 8,192. Compare:

- Scalar appends with immediate model notifications: 1,024 snapshots.
- The same scalar appends through `CoalescingChartDataSource`, serviced after each burst: eight snapshots and eight posted callbacks.
- The existing producer-side AppendRange alternative: eight snapshots with no posted callback.

All paths retain the same final numerical data; their intermediate snapshots and source version increments deliberately differ. Final X/low/high/midpoint arrays and delivery counts are checked after every measured round. Input/model/source construction and per-run seeding are excluded. Ingestion, notification callbacks, model refresh, reduction and every resulting owned snapshot are included. Two warmups precede seven measurements with rotating order among the three paths.

This uses a deterministic same-thread synchronization-context pump to isolate delivery policy. It does not time a physical UI queue, cross-thread scheduling, rendering, vsync or end-to-end latency. Real worker/UI affinity is separately exercised by Avalonia headless tests. An actual dispatcher can allocate callback jobs and choose different service intervals. Coalescing skips redundant presentation work rather than dropping accepted source observations or accelerating individual snapshots. Source batching remains the simpler alternative when producers control their own batches; it is included rather than hidden.

The existing read-only charting workflow retains both diagnostic sections in `range-streaming-performance.txt` with its tests and exports. Zero-allocation adapter invalidation is a property of the adapter with a reusable queue, not a promise that arbitrary schedulers, subscribers or renderers allocate nothing.
