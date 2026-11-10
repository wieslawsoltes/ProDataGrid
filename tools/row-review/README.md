# Row reuse review — 2026-11-10

Scope: PRs #385 and #398, independently and together, against master
`4e869f197e304d618d6649b3b7e2078be2098ebf` (Avalonia / ProDataGrid 12.1.5).

The product fixes are complementary: cancel deferred hiding of directly reused
rows; prevent a focused/editing container from occupying two displayed slots;
correct stale row addresses and child indices.

Review refinements:
- #385 cancels only the pending hide before the existing visibility reset.
- #398 scans the existing list by reference instead of allocating an iterator.
- New hot-path tests require **exactly zero** managed allocation after warmup.

## Reproduce

Check out this report branch in a full clone, then fetch the reviewed commits:

```sh
git fetch origin pull/385/head:refs/review/385 pull/398/head:refs/review/398
python3 tools/run-comparison.py --repo . --output /absolute/fresh/perf385 \
  --base 4e869f197e304d618d6649b3b7e2078be2098ebf \
  --candidate e5b31c60e9927339f2e09cb77129dd59b468f821 \
  --scenario focused --scenario scroll
python3 tools/run-comparison.py --repo . --output /absolute/fresh/perf398 \
  --base 4e869f197e304d618d6649b3b7e2078be2098ebf \
  --candidate e261cf70 --scenario focused --scenario scroll
python3 tools/run-comparison.py --repo . --output /absolute/fresh/combined \
  --base 4e869f197e304d618d6649b3b7e2078be2098ebf \
  --candidate f636fd2c --scenario focused --scenario scroll
```

The runner builds both exact revisions before measuring and copies the identical
harness into each. Run without other builds or benchmarks. Each workload uses
10,000 rows / eight columns, 32 warmup operations and 128 measured operations per
process, eight fresh processes in ABBABAAB order, Release, tiered compilation off.
Times and current-thread managed allocations are recorded separately per operation.

The updated pump processes binding/layout jobs at Render priority and explicitly
renders one frame. It excludes the lower-priority wall-clock rendering timer,
which otherwise gives slower operations more frames and extra allocations. This
makes frame counts equal; it is a headless layout/render benchmark, not GPU or
native memory profiling. See Avalonia's [render timer implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Headless/Avalonia.Headless/HeadlessRenderTimer.cs).

The earlier local run with the old pump is preserved in
`evidence/local-contended`. Other clang/rustc jobs heavily loaded the Mac during
that run; it is unsuitable for acceptance decisions. No samples are silently
removed. The dedicated GitHub Actions run is
https://github.com/wieslawsoltes/ProDataGrid/actions/runs/42840300884.

## Functional validation

On master, the deferred-recycle and duplicate-focused-row tests fail (2 failures
out of the 3 authored PR regressions). The moving-selected-row test passes on
master. Each PR passes its regressions after the fix.

Local complete DataGrid suites: #385 3,251 passed; #398 3,252 passed.
Both revised heads pass the Windows leak tests. Additional CI results and
benchmark tables are recorded alongside this report after completion.

A finite test set cannot guarantee zero overhead or memory growth for every
possible application; results apply to the workloads and revisions recorded.
