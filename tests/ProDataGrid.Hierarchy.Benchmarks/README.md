# ProDataGrid hierarchy benchmarks

This project owns the repeatable ProDataGrid-side hierarchy model benchmarks used by the TreeDataGrid comparison campaign. It deliberately measures hierarchy model operations separately from control creation, layout, scrolling, and rendering.

The collapse lane mirrors the **Optimized Cell Paths (Hierarchy)** sample's default
workload: 32 roots, branching factor 8, depth 4, and 149,792 materialized nodes.
Iteration setup expands the hierarchy; the measured operation contains only the
coherent `CollapseAll` dispatch.

```sh
dotnet run -c Release --project tests/ProDataGrid.Hierarchy.Benchmarks -- \
  --filter '*HierarchyCollapseBenchmarks*' \
  --job Short \
  --warmupCount 2 \
  --iterationCount 5 \
  --invocationCount 1 \
  --unrollFactor 1 \
  --allStats
```

Run a clean Release timing pass:

```sh
dotnet run -c Release --project tests/ProDataGrid.Hierarchy.Benchmarks -- \
  --filter '*HierarchyExpansionBenchmarks*' \
  --job Short \
  --allStats
```

Run intrusive diagnostics separately. For example:

```sh
dotnet run -c Release --project tests/ProDataGrid.Hierarchy.Benchmarks -- \
  --filter '*HierarchyExpansionBenchmarks*' \
  --job Dry \
  --profiler EP
```

Run the binary-tree scaling lane to verify that doubling the expanded node count
does not reintroduce superlinear work:

```sh
dotnet run -c Release --project tests/ProDataGrid.Hierarchy.Benchmarks -- \
  --filter '*HierarchyExpansionScalingBenchmarks*' \
  --job Short \
  --allStats
```

The scaling cases contain 510, 1,022, 2,046, 4,094, 8,190, and 16,382 nodes.
Retain the complete report so time and allocation ratios can be checked between
adjacent doubling steps.

Run the stack-safety check as a separate process so a regression cannot take down
the unit-test host:

```sh
dotnet run -c Release --project tests/ProDataGrid.Hierarchy.Benchmarks -- \
  --deep-stack-smoke 50000
```

Keep complete BenchmarkDotNet artifact directories with the environment, commit, dirty-state, and comparison report. Do not use a profiled run as timing evidence. The external source-linked comparison harness remains responsible for equivalent ProDataGrid/TreeDataGrid UI and native desktop workloads.
