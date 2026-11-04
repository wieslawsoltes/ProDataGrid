# ProDataGrid flat-layout benchmarks

This BenchmarkDotNet project compares the `Nested`, `Flat`, and `Virtualized`
layout modes with the same hierarchical data, viewport, and cell configurations.

`VirtualSurfaceSortingBenchmarks` measures accessor-backed sorting with the
250,000-row flat virtual-surface showcase workload. It guards the key-extraction
path used by header sorting independently of rendering and layout.
`VirtualSurfaceSortInteractionBenchmarks` measures the corresponding header-model
interaction in the real showcase, both with and without an active search.

`HierarchyCollapseLayoutBenchmarks` measures the pending Avalonia layout pass
after collapse. `HierarchyCollapseEndToEndBenchmarks` measures collapse plus layout
completion. Iteration setup expands and lays out the hierarchy outside the measured
operation.

## Run the suite

Build once:

```bash
dotnet build \
  tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-restore
```

Run the complete clean comparison:

```bash
DOTNET_TieredCompilation=0 dotnet run \
  --project tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-build -- \
  --filter '*HierarchyCollapse*Benchmarks*' \
  --launchCount 3 \
  --warmupCount 3 \
  --iterationCount 10 \
  --invocationCount 1 \
  --unrollFactor 1 \
  --allStats
```

Run the representative virtual-surface sorting workload:

```bash
dotnet run \
  --project tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-build -- \
  --filter '*VirtualSurfaceSortingBenchmarks*' \
  --launchCount 3 \
  --warmupCount 3 \
  --iterationCount 10 \
  --invocationCount 1 \
  --unrollFactor 1 \
  --allStats
```

Replace the filter with `*VirtualSurfaceSortInteractionBenchmarks*` to include
collection-view refresh, virtual-grid reconciliation, layout, and active-search
maintenance.

Set `PRODATAGRID_BENCHMARK_CELL_PATH` to run one cell path:

- `Standard`
- `OptimizedTheme`
- `OptimizedPresenter`
- `DirectHierarchy`
- `BuiltInDrawn`
- `CustomDrawn`

Without the environment variable, the suite runs every path.

## Profiling loops

The executable provides unmeasured repetition loops for profilers:

```bash
DOTNET_TieredCompilation=0 dotnet run \
  --project tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-build -- \
  --profile virtual 100 BuiltInDrawn inspect
```

Use `nested`, `flat`, or `virtual`. Replace `--profile` with
`--profile-end-to-end` to repeat the complete collapse and layout operation.
Profiler loops are diagnostic tools and must not be reported as clean
BenchmarkDotNet timing evidence.

## Results and artifacts

Keep `BenchmarkDotNet.Artifacts` together with the commit, dirty state, runtime,
machine metadata, exact command, and workload configuration. Upload generated
results through CI or store them under the gitignored `artifacts/performance`
directory; do not add dated result journals to this project.

See [Benchmarking layout performance](../../docfx/articles/layout-performance-benchmarking.md)
for workload equivalence, phase interpretation, comparison order, memory guidance,
and reporting requirements.
