# Efficient grid-chart windows

`DataGridChartModel.BuildSnapshot` accepts `WindowStart` and `WindowCount` through `ChartDataRequest`. Requests now copy only the selected slice of cached categories and value/X/size channels rather than copying the complete cache and trimming it afterward.

```csharp
var visible = model.BuildSnapshot(new ChartDataRequest
{
    WindowStart = 50_000,
    WindowCount = 128,
    DownsampleMode = ChartDownsampleMode.None
});
```

Start and count are safely clamped without adding potentially overflowing integers. A negative start is treated as zero; a start past the end produces an empty window; negative counts produce an empty window; an omitted count includes the remaining source. Every optional channel retains its original alignment. Missing optional channels remain absent. Existing full-cache clone callers retain their behavior, and subsequent window requests do not modify previously returned snapshots.

The optimization applies after the source cache exists. Building or rebuilding that cache still visits the source data. Warm visible-window copy work is proportional to the requested window and number of channels, not the full retained dataset. Windowing continues to occur before compatible line/scatter downsampling.

Heatmap cells, treemap/sunburst weights and gauge slots represent category identity rather than interchangeable samples of a continuous line. The grid adapter therefore disables point decimation for those chart kinds even when a caller explicitly requests Bucket, MinMax or LTTB. Explicit windows are still honored. This preserves the requested matrix cells and structural weights instead of silently averaging or dropping them. Existing Cartesian sampling policies are unchanged.

Run the actual adapter benchmark with:

```sh
dotnet run --project tests/ProDataGrid.Charting.Benchmarks/ProDataGrid.Charting.Benchmarks.csproj -c Release
```

The benchmark's README records same-runner before/after measurements and exact commit IDs. The charting workflow retains ongoing window timings and allocations beside its core benchmarks, native chart previews and regression TRX. These measurements concern data preparation, not UI frame rate or GPU throughput.
