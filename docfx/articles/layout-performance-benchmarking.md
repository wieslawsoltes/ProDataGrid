# Benchmarking layout performance

Use this guide to compare ProDataGrid visual layout modes or validate a layout
optimization. Keep benchmark results, traces, and investigation notes in benchmark
artifacts or reports; keep product documentation focused on repeatable methodology.

## Choose the right benchmark

ProDataGrid uses complementary benchmark types because no single measurement can
describe the complete UI pipeline.

| Benchmark type | Use it to measure | Do not use it to claim |
| --- | --- | --- |
| Model microbenchmark | Hierarchy projection, sorting, filtering, or another model operation | Scrolling or frame-time improvement |
| Headless component benchmark | Realization, layout, allocation, and visual-tree structure | Native compositor or display performance |
| Native application benchmark | Complete interaction, layout, render scheduling, frame pacing, and tails | Exact method ownership without diagnostics |
| Diagnostic trace | CPU, allocation, dispatcher, renderer, compositor, or scheduler ownership | Clean timing numbers |
| Memory stress test | Managed retention, allocation traffic, process footprint, and cache behavior | A managed leak from RSS alone |

Use a focused benchmark to identify the affected component and a native application
benchmark to confirm the user-visible result.

## Define an equivalent workload

Before comparing two modes or commits, fix and record:

- source commit and dirty state;
- `Release` configuration, target framework, SDK, and runtime;
- operating system, architecture, hardware, and power mode;
- window size, render scale, display refresh rate, and renderer;
- data shape and item count;
- column count, types, widths, and display modes;
- row height, viewport size, and frozen-column configuration;
- warm-up and measurement counts; and
- the exact input sequence.

For scrolling, name the pattern explicitly:

- **discontinuous** jumps between distant ranges and exercises complete projection
  replacement;
- **line** advances one fixed-height row and exercises an overlapping viewport;
- **fractional** changes the offset within the current row and exercises geometry
  updates without changing the visible slot range.

Do not pool results from different patterns. They represent different workloads.

## Validate behavior before timing

A faster result is invalid if the candidate skipped required work. Validate at
least:

- item and row counts;
- extent and viewport geometry;
- realized row, cell, control, and visual counts;
- selection, current cell, editing, and hierarchy behavior used by the scenario;
- expected fallback or surface activation; and
- cleanup after recycling, mode changes, and host closure.

An eligible `Virtualized` surface run should have no retained display cells in its
steady state. If retained fallback is expected, record that as a separate mode.

## Interpret the native phases

The native hierarchy harness records a timed operation as:

```text
total wall time = mutation + explicit layout + frame wait
```

- **mutation** includes the requested scroll, expand, or collapse and synchronous
  grid work caused by it;
- **explicit layout** is the measured `UpdateLayout()` boundary;
- **frame wait** is the harness completion wait across animation callbacks.

Frame wait can include render recording, composition submission, render-thread
work, scheduler delay, and display-clock pacing. It is not CPU time and it is not a
GPU presentation fence. Report it as user-observable latency, but do not attribute
it to DataGrid layout without supporting trace or meter evidence.

When a separate diagnostic run records Avalonia render and compositor stages, an
ownership-oriented comparison can use:

```text
active components = mutation + explicit layout + UI render recording
                  + compositor update + compositor render
```

This sum excludes the deliberate animation-clock wait. It is an attribution score,
not literal single-thread CPU time, because UI and render-thread work can overlap.
Always report its component values and keep the full wall time visible.

## Run the headless component benchmark

The flat-layout BenchmarkDotNet project compares `Nested`, `Flat`, and
`Virtualized` hierarchy collapse paths with matched data and viewport settings.

Build it first:

```bash
dotnet build \
  tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-restore
```

Run the clean comparison:

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

The suite's
[README](https://github.com/wieslawsoltes/ProDataGrid/blob/master/tests/ProDataGrid.FlatLayout.Benchmarks/README.md)
documents its modes, optional filters, and profiling entry points. Use the
repository's pinned BenchmarkDotNet options; check the generated `--help` output
before changing the command.

## Run the native application benchmark

Build the ProDataGrid native harness:

```bash
dotnet build \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Pro/Native.Pro.csproj \
  -c Release
```

Run one clean process for a virtual line-scroll workload:

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
  --output artifacts/performance/virtual-line.json
```

Change `GRID_BENCH_PRO_MODE` and the scroll pattern to create matched comparison
processes. The native harness
[README](https://github.com/wieslawsoltes/ProDataGrid/blob/master/tests/ProDataGrid.Hierarchy.NativeBenchmarks/README.md)
lists supported modes and TreeDataGrid source-comparison setup.

Run diagnostic instrumentation separately from clean timing. The
`--avalonia-diagnostics` option records Avalonia render/compositor stages, and
`--prodatagrid-diagnostics` records ProDataGrid component counters. Both add
measurement overhead.

## Profiling and tracing

This section is intended for contributors investigating ownership inside the
layout and rendering pipeline. Keep the user-facing layout articles focused on
configuration and behavior.

### Keep clean timing and diagnostics separate

Use at least two processes for an investigation:

1. a clean process with no meters, trace providers, debugger, or profiler attached;
2. an instrumented process with the same mode and workload for ownership evidence.

Never use the instrumented process as the performance gate. Meter listeners,
EventPipe providers, sampled stacks, allocation profilers, and graphics captures
all add work. The diagnostic run explains the clean result; it does not replace it.

Record the tool versions, exact command, commit, dirty state, runtime, architecture,
and environment variables with every diagnostic artifact. Query installed help
before reusing a command because diagnostic-tool syntax and built-in profiles can
change between releases.

### Frame alignment and the callback barrier

The native harness controls animation-clock phase before every measured sample:

1. it completes a full managed collection outside the timed operation;
2. it waits through an unmeasured three-callback alignment barrier;
3. it arms the timed two-callback barrier; and
4. it performs mutation and `UpdateLayout()` while the UI thread is still inside
   the timed operation.

Three alignment callbacks are required. Avalonia invokes an animation callback
before completing the remainder of that media pass. Starting after only two
callbacks can leave an unrelated composition batch pending and move the next sample
into a different refresh band. The diagnostic-only `--alignment-callbacks` option
can test this assumption, but baseline and candidate timing must use the same
alignment count.

The timed barrier is armed before mutation. Callback 1 therefore cannot execute
until synchronous mutation and layout yield the UI thread. Callback 1 schedules
callback 2, which runs on a later animation pulse because Avalonia swaps the current
and next callback queues before invoking them.

This is a conservative UI-completion convention, not a presentation fence.
Avalonia can release the second callback after a composition batch is marked
`Processed`; that state is earlier than `Rendered` and does not prove GPU scanout.
At a 60 Hz refresh rate, crossing a frame cutoff can move wall time by about
16.7 ms even when synchronous work changes only slightly.

The JSON separates:

- layout completion to callback 1, called **frame pickup**;
- callback 1 to callback 2; and
- the animation-clock interval reported to those callbacks.

When frame pickup is small and the callback interval follows the animation-clock
interval, the dominant portion is refresh pacing. Keep it in full wall time, but do
not relabel it as DataGrid CPU work. A row-realization or layout optimization cannot
be expected to halve that idle interval.

### Attribute Avalonia rendering stages

Run a separate process with Avalonia diagnostics enabled:

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
  --avalonia-diagnostics \
  --output artifacts/performance/virtual-line-avalonia-diagnostics.json
```

The harness listens to `Avalonia.Diagnostic.Meter` and records the maximum
instrumented pass associated with each sample:

- `avalonia.ui.render.time` for UI render recording;
- `avalonia.comp.update.time` for compositor update; and
- `avalonia.comp.render.time` for compositor rendering.

The harness waits outside the timed interval so late compositor measurements can
arrive before it snapshots the meters. These values support the active-components
attribution described above. They are not added to frame wait, and their maxima are
not literal additive CPU time because UI and render-thread stages may overlap.

Always inspect UI render and compositor render together. A reduction in scene
recording is not a complete win if equivalent work moved to the compositor. If CPU
and compositor stages finish early but the callback interval remains long, inspect
scheduler, compositor, vsync, or display pacing rather than continuing to optimize
DataGrid layout.

### Attribute ProDataGrid components

Add `--prodatagrid-diagnostics` to a ProDataGrid-only diagnostic process. It enables
`ProDataGrid.Diagnostic.Meter` and records raw per-jump samples plus aggregate values:

```bash
DOTNET_TieredCompilation=0 \
GRID_BENCH_PRO_MODE=virtual \
dotnet \
  tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Pro/bin/Release/net8.0/Native.Pro.dll \
  --scroll-only \
  --scroll-pattern discontinuous \
  --scroll-jumps 32 \
  --warmup 2 \
  --iterations 10 \
  --avalonia-diagnostics \
  --prodatagrid-diagnostics \
  --output artifacts/performance/virtual-discontinuous-components.json
```

TreeDataGrid does not expose these ProDataGrid meters, so its executable rejects the
switch. Compare clean application-level phases across controls; use component meters
only to explain ownership inside ProDataGrid.

The most useful meter groups are:

| Area | Timers and counters |
| --- | --- |
| Scroll entry and projection | `prodatagrid.rows.scroll.slots.by.height.time`, `prodatagrid.rows.scroll.estimate.offset.time`, `prodatagrid.rows.display.update.time`, and rows-presenter viewport time |
| Row generation | `prodatagrid.rows.generate.time` with acquire, bind, and prepare timers |
| Row recycling | `prodatagrid.rows.recycle.time` with cleanup, detach, and pool timers |
| Element insertion | display-element insert, attach, measure, height-record, and load timers |
| Retargeting | eligibility, validation, bind, apply, child-index, and layout-validity timers; realized, recycled, retargeted, measure-reused, and arrange-reused counts |
| Layout | DataGrid, rows-presenter, row, and cells-presenter measure and arrange timers and counts |
| Virtual surface | `prodatagrid.virtual.surface.render.time`, rendered-row/cell counts, clips, grid lines, hierarchy expanders, text operations, glyph runs, and text/value-cache hits and misses |

Several component timers are nested. In particular,
`prodatagrid.rows.retarget.apply.time`,
`prodatagrid.rows.retarget.child-index.time`, and
`prodatagrid.rows.retarget.layout-validity.time` are inside the enclosing retarget
bind phase. Do not add them to bind time. Similarly, virtual-surface render time is
inside the UI rendering pipeline and must not be added to the active-components
score. Use component timers to decompose an owner, not to manufacture a larger
total.

Counters prove that the expected path executed:

- compare realized, recycled, and retargeted counts with the requested scroll
  pattern;
- compare retarget measure/arrange reuse with the realized-row count before claiming
  geometry reuse;
- require zero realized rows and display cells for an eligible rowless surface run;
- require cache misses when attributing cost to value resolution or text shaping;
- require vertical-grid-line operations before evaluating grid-line batching; and
- use text scene-operation and glyph-run counts to prove that text batching was
  active.

For fixed-height scrolling, compare
`prodatagrid.rows.scroll.slots.by.height.time` with mutation plus layout and the
complete active-components score. A faster slot lookup is useful only when the
application-level components improve. Keep full frame wait as a separate pacing
diagnostic.

For virtual-surface work, compare discontinuous, line, and fractional processes
separately. Report `prodatagrid.virtual.surface.render.time`, UI render recording,
compositor render, cache behavior, and structural counts together. A faster inner
surface timer does not by itself prove a whole-frame improvement.

### Capture managed CPU traces

Check the installed tool before collecting:

```bash
dotnet-trace --version
dotnet-trace collect --help
```

Current standard `dotnet-trace collect` uses
`dotnet-common,dotnet-sampled-thread-time` for runtime events and sampled managed
stacks. The historical standard `cpu-sampling` profile was removed; it remains a
different Linux `collect-linux` workflow and must not be copied into this command.

Create the artifact directory, then launch the native harness through the collector:

```bash
mkdir -p artifacts/performance/traces

GRID_BENCH_PRO_MODE=virtual \
dotnet-trace collect \
  --profile dotnet-common,dotnet-sampled-thread-time \
  --output artifacts/performance/traces/virtual-collapse.nettrace \
  --show-child-io \
  -- dotnet \
    tests/ProDataGrid.Hierarchy.NativeBenchmarks/Native.Pro/bin/Release/net8.0/Native.Pro.dll \
    --collapse-only \
    --warmup 6 \
    --iterations 20 \
    --output artifacts/performance/traces/virtual-collapse.json
```

The extra warmups and iterations make the intended workload dominate process
startup in the trace; they do not turn the trace into clean timing evidence.
Preserve the raw `.nettrace` because derived stack views do not retain every runtime
event.

Generate inclusive and exclusive summaries when a quick first pass is useful:

```bash
dotnet-trace report \
  artifacts/performance/traces/virtual-collapse.nettrace \
  topN -n 100 --inclusive \
  > artifacts/performance/traces/virtual-collapse-inclusive.txt

dotnet-trace report \
  artifacts/performance/traces/virtual-collapse.nettrace \
  topN -n 100 \
  > artifacts/performance/traces/virtual-collapse-exclusive.txt
```

Analyze the deterministic workload interval, inclusive costs first, then callers,
callees, exclusive costs, allocation/runtime activity, and thread ownership.
Separate application code, Avalonia, CoreCLR/JIT/GC, P/Invoke, native graphics, and
scheduler frames. Sampled stacks identify where CPU was observed; they do not give
exact call counts or exact per-call duration.

On Linux and macOS, an attaching diagnostic tool must share `TMPDIR` with the target
process. Launch-through-collector avoids selecting the wrong short-lived `dotnet`
child and captures startup; attach by PID when only a warmed interval is wanted.

### Use focused profiler loops

The BenchmarkDotNet executable exposes repetition loops with stable event markers
for CPU or allocation profilers:

```bash
DOTNET_TieredCompilation=0 dotnet run \
  --project tests/ProDataGrid.FlatLayout.Benchmarks/ProDataGrid.FlatLayout.Benchmarks.csproj \
  -c Release --no-build -- \
  --profile virtual 100 BuiltInDrawn inspect
```

Use `nested`, `flat`, or `virtual`. Replace `--profile` with
`--profile-end-to-end` to repeat collapse plus layout. The `inspect` option prints
realized visual and layout-validity statistics. These loops isolate a component for
profiling and are never reported as BenchmarkDotNet timing results.

### Cross managed, native, and graphics boundaries

If sampled managed stacks do not explain the clean wall time, continue across the
ownership boundary:

- use a platform-native CPU and scheduler trace for native library, driver,
  compositor, wakeup, and off-CPU ownership;
- use a graphics timeline or capture when GPU execution, resource upload, queueing,
  or presentation is plausible;
- keep window size, render scale, display, refresh rate, renderer, adapter, power
  mode, and capture configuration identical; and
- correlate the native timeline with harness phases and managed trace timestamps.

High render-thread CPU does not prove GPU saturation. GPU timestamps measure queue
execution, not UI command generation, driver submission, compositor latency, or
display scanout. If CPU and GPU finish early but presentation is late, investigate
present mode, compositor/vsync pacing, drawable starvation, and scheduling.

### Preserve diagnostic artifacts safely

Store raw JSON, `.nettrace`, native trace bundles, stack reports, captures, tool
versions, and exact commands under `artifacts/performance/<investigation>` or in CI
artifacts. Keep generated artifacts and dated investigation journals out of the
product documentation. Traces, dumps, and captures can contain source paths, user
data, or process contents; handle them as sensitive artifacts.

## Compare baseline and candidate

For a focused change:

1. build clean baseline and candidate worktrees or published binaries;
2. keep the runtime, machine, workload, and configuration identical;
3. warm both variants before measurement;
4. alternate process order, for example `B A`, `A B`, `B A`;
5. retain every raw sample;
6. compare process-level medians or means with a spread or confidence interval;
7. inspect p95/p99 or hitch counts for interaction workloads; and
8. compare allocation and memory behavior as well as elapsed time.

Do not select the fastest process. Exclude a run only by a rule declared before
examining the result. Treat small changes on shared runners as noise unless repeated
evidence shows practical significance.

The repository's native source-comparison CI uses a stronger fixed protocol: every
mode runs in an independent process, each process uses two warmups and ten measured
iterations, complete mode order is reversed in alternating processes, and four
process means are aggregated with a Student-t 95% confidence interval. Clean timing,
Avalonia diagnostics, ProDataGrid component diagnostics, and sampled traces are
separate runs. Raw JSON and `aggregate.json` remain CI artifacts. Preserve that
separation when extending the workflow.

## Allocation and memory

`GC.GetTotalAllocatedBytes` and BenchmarkDotNet's memory diagnoser report managed
allocation traffic during the measured operation. They do not measure retained
heap, native allocations, Skia resources, compositor resources, GPU memory, or
process footprint.

For cache or pooling changes, add a separate stress run and compare equivalent
lifecycle points:

- managed live and committed heap;
- total allocated bytes;
- process working set or RSS and private memory;
- cache count, capacity, and eviction behavior; and
- state after detaching the grid or closing the host.

Stable managed live bytes with higher RSS is not sufficient evidence of a managed
leak.

## Report results

A useful report includes:

1. the question and acceptance threshold;
2. the exact workload and semantic checks;
3. environment, commit IDs, and dirty state;
4. exact commands and mode flags;
5. warm-up, run order, sample count, and exclusions;
6. component times, wall time, tails, and allocation;
7. structure and fallback validation;
8. diagnostic evidence used for ownership; and
9. limitations and unresolved regressions.

Keep raw JSON, BenchmarkDotNet output, traces, and dumps under
`artifacts/performance` or upload them as CI artifacts. Avoid adding generated
artifacts or dated investigation logs to the user documentation.

## Related articles

- [Visual layout modes](flat-row-cell-layout.md)
- [Virtualized cell surface](virtual-surface-architecture.md)
- [Scrolling and virtualization](scrolling-virtualization.md)
