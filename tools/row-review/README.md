# Row disappearance PR review — 2026-11-10

## Decision: hold merging and release

Both PRs fix real correctness problems and all eight standard CI checks pass on
the final heads. The combined DataGrid suite passes all **3,257 tests**. However,
the requested zero-performance/zero-memory-increase acceptance gate is not
established. Positive timing and small allocation estimates remain in some
workloads; unchanged-code controls also vary. These are **inconclusive estimates,
not proof of a causal regression**. A successful benchmark workflow means its
workloads executed successfully, not that its performance passed the gate.

Neither PR has been merged. No release branch/tag or package has been published.
Version 12.1.6 was prepared and built locally only (11 packages and 10 symbol
packages); publication remains gated. Version 12.1.5 remains the baseline.

## Revisions and fixes

| Revision | Meaning |
|---|---|
| `4e869f197e304d618d6649b3b7e2078be2098ebf` | Current master / ProDataGrid and Avalonia 12.1.5 |
| `fd9af0b3bbcb5e9e87c35c02aaf28e824f679a69` | Reviewed #385 product and tests |
| `c2a32559e01a4a92677998382a0ef2d5df1942df` | Final #385 head; empty CI-trigger commit, identical tree to fd9af0b3 |
| `d3b9fcb92e005ab278aca0a74d666cc18a3cb500` | Final #398 head, including #385 and preserving all regression tests |

- #385 cancels a pending deferred hide when a row is reused directly.
- #398 prevents a focused/editing container from representing two displayed slots
  and corrects stale row addresses / child indices.
- Review fix: replace #398's allocating displayed-row iterator with a direct
  reference-membership scan.
- Review fix: cancel deferred hiding without a redundant visibility check.
- Review fix: restore recycled elements with a direct call instead of allocating
  an instance-method delegate for every pool lookup.

New tests require exactly zero allocation for warmed deferred-hide cancellation,
row membership lookups, and empty recycle-pool lookups. The last test fails on
master with **64,000 bytes per 1,000 lookups**, and passes with **0 bytes** after
the fix. This isolated result must not be substituted for whole-grid measurements.

## Functional and release validation

The authored deferred-hide and duplicate-focused-row regressions fail on master
and pass after their respective fixes. The moving-selected-row test passes on
master too. Added focused-cell/frozen-column smoke tests pass under Simple v2 and
Fluent v2; these also pass on master and are compatibility checks, not additional
bug reproductions.

Final combined DataGrid suite: 3,257 passed, zero failed. Both latest PR heads:
build, Linux/macOS/Windows tests, NativeAOT, package build, Windows native source
comparison, and Windows leak tests all pass. An earlier superseded #398 Windows
job hit the unchanged formula allocation-test concurrency sensitivity; all final
checks pass without changing formula production code or increasing its budget.

## Actual recycling benchmark results

The following explicit Nested-layout scenarios assert that row retargeting stays
disabled. The focused workload reapplies and asserts focus before every scroll,
including measured operations. Each row is the average of four independent
process means per side, 128 measured operations per process.

Source run: https://github.com/wieslawsoltes/ProDataGrid/actions/runs/42846185398

| Comparison | Workload | Base ms | Candidate ms | Time delta | Base bytes/op | Candidate bytes/op | Byte delta |
|---|---|---:|---:|---:|---:|---:|---:|
| #385 vs master | recycling-focused | 14.94981 | 14.78577 | -1.097% | 2,574,626.766 | 2,574,613.062 | -13.703 |
| #385 vs master | recycling-scroll | 8.73558 | 8.80065 | +0.745% | 1,617,367.750 | 1,617,367.750 | +0.000 |
| #398 incremental vs #385 | recycling-focused | 14.83725 | 14.84551 | +0.056% | 2,574,626.781 | 2,574,628.328 | +1.547 |
| #398 incremental vs #385 | recycling-scroll | 8.81703 | 8.69493 | -1.385% | 1,617,367.750 | 1,617,367.750 | +0.000 |
| Both vs master | recycling-focused | 14.69868 | 14.80019 | +0.691% | 2,574,610.328 | 2,574,623.734 | +13.406 |
| Both vs master | recycling-scroll | 8.87398 | 8.54085 | -3.754% | 1,617,367.750 | 1,617,367.750 | +0.000 |
| Unchanged master vs itself | recycling-focused | 13.91870 | 14.03376 | +0.827% | 2,574,611.250 | 2,574,621.328 | +10.078 |
| Unchanged master vs itself | recycling-scroll | 9.16357 | 9.19815 | +0.377% | 1,617,367.750 | 1,617,367.750 | +0.000 |

Do not offset a worse workload with gains elsewhere. In particular, the focused
incremental and combined point estimates are positive. The control also shows
positive time/allocation differences for identical code, so attribution is
uncertain. The strict gate remains unresolved.

## Hierarchical viewport validation

The hierarchy samples from run42846185398 are **invalid**: the viewport was not
moving. Their sub-microsecond timings and percentage deltas must not be used.
They are retained and marked invalid rather than silently discarded.

The corrected harness uses the Simple v2 theme and `model.Flattened`, drives the
ScrollViewer offset, and asserts 1,100 grid slots plus an actual viewport move
past slot 900 and back to slot 0 on every operation. The corrected matched run is
https://github.com/wieslawsoltes/ProDataGrid/actions/runs/42849074900.

| Comparison | Base ms | Candidate ms | Time delta | Base bytes/op | Candidate bytes/op |
|---|---:|---:|---:|---:|---:|
| #385 vs master | 2.48063 | 2.47466 | -0.241% | 307,612.6875 | 307,612.6875 |
| #398 incremental vs #385 | 2.50150 | 2.45618 | -1.812% | 307,612.6875 | 307,612.6875 |
| Both vs master | 2.45409 | 2.45072 | -0.138% | 307,612.6875 | 307,612.6875 |
| Unchanged master vs itself | 2.45513 | 2.44683 | -0.338% | 307,612.6875 | 307,612.6875 |

The corrected hierarchy workload shows no increase in these measured means and exactly equal managed allocations. This does not resolve the other workloads or establish a universal performance bound.

## Reproduce all current workloads

Use a full clone and the harness from this report branch:

```sh
git fetch origin codex/row-review-report-nov10
git checkout codex/row-review-report-nov10
git fetch origin pull/385/head:refs/review/385 pull/398/head:refs/review/398
python3 tools/run-comparison.py --repo . --output /absolute/fresh/perf385 \
  --base 4e869f197e304d618d6649b3b7e2078be2098ebf \
  --candidate fd9af0b3bbcb5e9e87c35c02aaf28e824f679a69 \
  --scenario recycling-focused --scenario recycling-scroll --scenario hierarchy-scroll
python3 tools/run-comparison.py --repo . --output /absolute/fresh/perf398 \
  --base fd9af0b3bbcb5e9e87c35c02aaf28e824f679a69 \
  --candidate d3b9fcb92e005ab278aca0a74d666cc18a3cb500 \
  --scenario recycling-focused --scenario recycling-scroll --scenario hierarchy-scroll
python3 tools/run-comparison.py --repo . --output /absolute/fresh/combined \
  --base 4e869f197e304d618d6649b3b7e2078be2098ebf \
  --candidate d3b9fcb92e005ab278aca0a74d666cc18a3cb500 \
  --scenario recycling-focused --scenario recycling-scroll --scenario hierarchy-scroll
```

For an unchanged-code control, pass master as both base and candidate. To reverse
order, add `--order BAABABBA`. Each output directory must be new. Both revisions
are built before timing and receive the identical harness. Do not run other
builds/benchmarks concurrently. Full command logs, runtime information, exact
revisions, per-operation samples and process means are retained in the output.

Release .NET SDK 10.0.201, tiered compilation off, 32 warmups and 128 measured
operations per process, eight fresh processes in ABBABAAB order. Flat workloads
use 10,000 rows/eight columns. Hierarchy uses 100 roots / 10 children each and one
hierarchical column. The pump processes binding/layout at Render priority and
explicitly renders one frame, excluding the wall-clock UiThreadRender timer.
See the [Avalonia timer implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Headless/Avalonia.Headless/HeadlessRenderTimer.cs).

## Evidence and limits

All raw samples are under `evidence/`. No unfavorable sample is silently removed.
- `ci-first`, `ci-reverse`, `ci-final-fastpath`: earlier default-layout probes.
  Their initial focus could move during warmup and their rows could take the
  retargeting fast path. They are smoke comparisons, not sufficient evidence for
  the changed recycling paths. Reverse and final runs include unchanged controls.
- `local-contended`: invalid for timing because unrelated compilers heavily
  loaded the Mac.
- `local-final-fastpath`: another ARM64 run; external builds resumed and the
  identical-code control differed by up to 21.85%, so it cannot establish the gate.
- `ci-recycling`: valid explicit focused/ordinary recycling, plus clearly marked
  invalid hierarchy samples.
- `ci-hierarchy-corrected`: valid hierarchy viewport scrolling with motion assertions.

Current-thread managed allocation and headless wall time are measured; peak,
native, GPU and retained memory are not quantified by this harness. Windows leak
tests provide separate lifetime coverage. Hierarchy collection moves and duplicate
insertions have correctness tests, but these scroll benchmarks do not quantify
all costs of those mutation paths. No universal zero-overhead guarantee follows
from a finite set of tests. Controlled timing/allocation evidence covering the
changed paths is still required before the requested merge/release gate can pass.
