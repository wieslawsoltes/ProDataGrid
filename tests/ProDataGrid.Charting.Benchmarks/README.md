# Grid-chart window benchmark

```sh
dotnet run --project tests/ProDataGrid.Charting.Benchmarks/ProDataGrid.Charting.Benchmarks.csproj -c Release
```

This exercises the actual `DataGridChartModel.BuildSnapshot` path, not a substitute reference implementation. Each measured operation reads twenty 128-point windows from an already cached dataset of 50,000 or 200,000 rows. Categories, numeric values, X coordinates and bubble-size channels are checked against their original source indices. Source construction and the first complete cache construction are outside the measured region; window snapshot construction and result checks are included. The program uses two warmups and reports medians of seven measurements, with managed allocations reported separately.

## Same-runner before/after evidence

The existing full-clone-and-trim path and the bounded-range copy path were built and run sequentially in [workflow run 36495543106](https://github.com/wieslawsoltes/ProDataGrid/actions/runs/36495543106), on .NET 10.0.12 / Ubuntu 24.04.5 LTS / four processors.

| Source rows / twenty window reads | Before | After | Allocations before | Allocations after |
| --- | ---: | ---: | ---: | ---: |
| 50,000 | 23.639 ms | 0.084 ms | 48,032,864 B | 136,960 B |
| 200,000 | 51.901 ms | 0.127 ms | 192,027,344 B | 136,960 B |

Baseline commit: `c2a4b3372d6877d438e5da834bfda0b26562b035`.
Optimized commit: `69127aa06fc9d9ceec8a4a39722909f2a9e8d5f9`.
The workflow artifact retains both raw outputs, commit IDs and the exact source patch. The one-shot write-enabled comparison workflow removed itself after the comparison; ongoing diagnostics use the read-only charting workflow.

The implementation now copies only the requested slice of every cached channel. Warm window-copy cost scales with visible points and series, not total retained source length. Initial source-cache construction still visits the source; this change is not lazy loading, source virtualization, or an incremental native-GPU renderer. Shared-runner elapsed times vary, so there are no timing thresholds. A focused allocation regression detects a return to full-source channel copying.
