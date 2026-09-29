# Coordinated interval display diagnostics

```sh
dotnet run --project tests/ProCharts.Decimation.Benchmarks/ProCharts.Decimation.Benchmarks.csproj -c Release
```

The experiment compares **full-resolution geometry with an explicitly approximate reduced display** using the same source data and existing public source/renderer APIs. It is not an equivalent-output algorithm comparison. Selection keeps both boundaries' extrema, aligned original X/source indices and gap separators; interpolation between retained samples can omit other details and is not guaranteed to enclose every original interval.

Fixtures have 10,000 and 100,000 observations, two injected extrema and a real missing-data run. The requested point budget is 2,000; actual selected counts are printed. Source ownership/construction is outside measured regions. Separate scenarios measure two uncached alternating windows (including selection/copying), 10,000 cached-view reads, and repeated 1200x600 CPU bitmap raster drawing of already prepared snapshots with warm renderer layout. No SKPicture/render-cache replay is passed to the draw operation. Native drawing is included; physical-GPU execution, screen presentation and frame scheduling are not.

Two paired warmups precede seven paired measured repetitions, alternating full/reduced execution order with garbage collection before each operation. The report includes median elapsed time and calling-thread managed allocations. Input/selected-channel alignment, gap separator and injected-extremum checks run before timing; window/cache contract checks remain in their relevant timed operations. Full and reduced pixel output is not asserted identical because geometry is intentionally reduced. Native sample exports are retained separately by chart sample tests.

The read-only charting workflow retains the raw runtime/platform metadata and `range-decimation-performance.txt`. No elapsed-time thresholds or universal speedup claim are imposed on shared runners. Raw full-resolution data remains available through `ChartDownsampleMode.None`.
