# Indexed chart interaction benchmarks

```sh
dotnet run --project tests/ProCharts.Interaction.Benchmarks/ProCharts.Interaction.Benchmarks.csproj -c Release
```

This measures the actual renderer's public hit-testing and viewport paths, with source creation excluded. Each scenario has two warmups and seven measured repetitions; time and managed allocation medians are reported separately. The first query constructs the spatial index, while warm queries reuse it. No physical-GPU work or UI frame rate is measured.

## Paired baseline evidence

Original source commit: `c627e6ee5ce7f2b688ab3353e0da95c3f3073c68`.
The same benchmark program ran against that original implementation and the changed implementation on the same runner in [run 36529386335](https://github.com/wieslawsoltes/ProDataGrid/actions/runs/36529386335), artifact `11015697449`. The artifact includes raw before/after outputs, integration patch, and TRX (144 passed, zero failures). Its final workflow-push step failed because the Actions token could not update a workflow file; the preceding build, regression and comparison steps succeeded. Source integration was subsequently committed separately, and workflow maintenance used the repository connector.

.NET 10.0.12, Ubuntu 24.04.5 LTS, four processors.

| Scenario | Before | Indexed | Allocations before | Indexed allocations |
| --- | ---: | ---: | ---: | ---: |
| Cold first hit, 10,000 points | 0.507 ms | 2.042 ms | 2,512 B | 287,520 B |
| 128 warm hits, 10,000 points | 74.668 ms | 0.742 ms | 318,464 B | 0 B |
| 32 warm viewport queries, 10,000 points | 6.006 ms | 0.088 ms | 67,328 B | 0 B |
| Cold first hit, 100,000 points | 3.272 ms | 17.425 ms | 2,352 B | 2,851,904 B |
| 128 warm hits, 100,000 points | 256.615 ms | 1.240 ms | 265,216 B | 0 B |
| 32 warm viewport queries, 100,000 points | 30.432 ms | 0.093 ms | 59,136 B | 0 B |

These results expose the tradeoff: higher one-time preparation time/memory for much cheaper repeated queries. They are shared-runner diagnostics, not fixed timing guarantees. Clustered points or very large hit radii can still require a linear scan of candidates; this is not an O(1) worst-case guarantee.

## Cache contracts

`SkiaChartRenderer.UseInteractionCache` defaults to true. Line, area, scatter and bubble plots (including combinations of those kinds) reuse range/layout preparation and lazily build a bounded screen-space grid. Other plot kinds and plots above one million source values retain the reference path. `UseInteractionCache = false` opts out.

Snapshots and their collections must remain unchanged for their lifetime. Replace the snapshot on data changes. `ClearInteractionCache()` releases retained managed layout/index data and is required after in-place data changes or changes to state captured by formatters. Bounds, ordinary style settings and exact nullable axis overrides participate in invalidation. A renderer instance is not thread-safe.

Indices preserve source series/point identities, finite-value gaps, radius semantics and tie order. Regression tests compare indexed results against the reference implementation for ragged arrays, unsorted X, missing values, logarithmic/secondary axes, bubble sizes, off-plot centers, exact ties and style/snapshot changes. A counted-list test asserts that warm queries do not rescan source collections and allocate zero bytes. The existing bubble reference path was also corrected to use its assigned secondary value-axis kind, matching rendering.
