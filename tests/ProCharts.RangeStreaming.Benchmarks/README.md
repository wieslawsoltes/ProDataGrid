# Bounded range ingestion diagnostics

```sh
dotnet run --project tests/ProCharts.RangeStreaming.Benchmarks/ProCharts.RangeStreaming.Benchmarks.csproj -c Release
```

The actual replacement source and streaming source consume the same chronological windows. Each measured operation applies sixteen batches of 32 observations to history already seeded at capacity 4,096 or 65,536. One scenario measures ingestion alone; another builds a reduced display snapshot after every batch. The latter includes lock-held window scanning, coordinated selection and snapshot/map ownership.

All source inputs and replacement windows are precomputed. Initial construction and resetting/seeding each run are outside timing. No external producer array shifts or allocations are charged to either implementation. Replacement calls still validate/copy their complete input; streaming calls validate/append only the new batch. These are different APIs for the same resulting retained data, not a faster implementation of arbitrary replacement.

Two paired warmups precede seven paired measurements with alternating order. Each pair verifies matching checksums; selected boundary/X arrays and stream identity are compared before and after the measurements. Times and calling-thread managed allocations are diagnostic, not fixed CI thresholds. Source-label strings, UI subscribers, display rendering, GPU submission, screen presentation and frame pacing are not measured. Cache/view preparation is not constant-time.

The read-only charting workflow retains the raw output as `range-streaming-performance.txt` with its semantic tests and sample exports. Zero-allocation append and exact replacement-source equivalence are also regression tested independently.
