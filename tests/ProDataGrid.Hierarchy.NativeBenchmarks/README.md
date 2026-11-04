# Native hierarchy source comparison

This harness compares ProDataGrid layout modes with Wieslaw's open-source
TreeDataGrid in independent native desktop processes. Both applications use the
same generated models, five data columns, window and row dimensions, scroll
patterns, warm-up policy, and completion convention.

The harness measures:

- first render of a 4,094-node hierarchy;
- expand and collapse operations;
- deterministic discontinuous, line, or fractional scrolling; and
- managed allocation traffic during each measured operation.

The output includes structural validation so a missing cell, incorrect extent, or
unexpected Virtualized fallback fails the run instead of being reported as a
performance improvement.

## Build

Build the ProDataGrid application:

```bash
dotnet build \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Pro/Native.Pro.csproj \
  -c Release
```

To compare TreeDataGrid from source, clone it separately and provide its absolute
path:

```bash
dotnet build \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Tree/Native.Tree.csproj \
  -c Release \
  -p:TreeDataGridSourceRoot=/absolute/path/to/Avalonia.Controls.TreeDataGrid
```

## Run ProDataGrid

Select a ProDataGrid path with `GRID_BENCH_PRO_MODE`:

- `standard`, `optimized`, `direct`, or `direct-cell` for retained nested modes;
- `drawn` for drawn cells in the nested layout;
- `flat-direct-cell` or `flat-drawn` for the Flat layout; and
- `virtual`, `virtual-checkbox`, `virtual-date`, `virtual-time`,
  `virtual-masked`, `virtual-autocomplete`, `virtual-slider-text`, or
  `virtual-combobox-text` for the virtual surface.

Example:

```bash
DOTNET_TieredCompilation=0 \
GRID_BENCH_PRO_MODE=virtual \
dotnet \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Pro/bin/Release/net8.0/Native.Pro.dll \
  --scroll-only \
  --scroll-pattern line \
  --scroll-jumps 32 \
  --warmup 2 \
  --iterations 10 \
  --output artifacts/performance/pro-virtual-line.json
```

Run the TreeDataGrid application with the same workload options:

```bash
DOTNET_TieredCompilation=0 dotnet \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Tree/bin/Release/net8.0/Native.Tree.dll \
  --scroll-only \
  --scroll-pattern line \
  --scroll-jumps 32 \
  --warmup 2 \
  --iterations 10 \
  --output artifacts/performance/tree-line.json
```

Supported scenario switches are `--first-render-only`, `--collapse-only`, and
`--scroll-only`. Scroll patterns are `discontinuous`, `line`, and `fractional`.

## Diagnostic runs

Add `--avalonia-diagnostics` to record Avalonia render and compositor stages. Add
`--prodatagrid-diagnostics` to a ProDataGrid process to record grid component
counters. These options add overhead and must be run separately from clean timing.

The frame wait crosses animation callbacks. It can include renderer, compositor,
scheduler, and display-clock pacing and must not be labeled as DataGrid CPU work.
See [Benchmarking layout performance](../../docfx/articles/layout-performance-benchmarking.md)
for the phase model and interpretation rules.

## Comparison protocol

Run each mode in an independent process, warm it before measurement, alternate the
complete baseline/candidate order, and retain every raw JSON sample. Keep runtime,
hardware, window, data, columns, and scroll pattern identical.

Store generated JSON, traces, and summaries under the gitignored
`artifacts/performance` directory or upload them as CI artifacts. Do not add dated
result journals to the repository.
