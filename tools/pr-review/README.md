# PR review validation — November 2026

These are review artifacts, outside the product projects. They are not included in production builds.

## Reproduce a headless timing/allocation case

Use isolated worktrees for the exact baseline and PR commits recorded in the results. Copy `ReviewPerformanceTests.cs` to `src/Avalonia.Controls.DataGrid.UnitTests/ReviewPerformanceTests.cs` in each worktree, then run:

```sh
dotnet build src/Avalonia.Controls.DataGrid.UnitTests -c Release -p:CollectCoverage=false
REVIEW_SCENARIO=scroll REVIEW_OUTPUT=/absolute/path/results.jsonl DOTNET_TieredCompilation=0 dotnet src/Avalonia.Controls.DataGrid.UnitTests/bin/Release/net10.0/Avalonia.Controls.DataGrid.UnitTests.dll -class '*ReviewPerformanceTests' -parallel none
```

For the `cells-tracked` scenario on #419, build with `-p:DefineConstants=REVIEW_TRACKED_CELLS`. Otherwise use the same harness and build settings on both revisions. See the switch in the source for the other scenarios. `SupplementalReviewPerformanceTests.cs` adds `view-source` and `hierarchy-filtered`; copy that file separately and select `-class '*SupplementalReviewPerformanceTests'`.

Each process performs six warmups and 24 measured operations. We ran eight independent processes in A-B-B-A-B-A-A-B order (four per revision), with no review builds or functional tests running concurrently. Compare process means, not individual samples as independent observations. Raw times are milliseconds and allocations are bytes per operation on the UI thread. Model-only cases use the same accounting on the calling thread. These measurements cover headless managed work; they do not establish native GPU/compositor performance, other operating systems, or every workload.

`ReviewDuplicateCellSelectionTests.cs` is an intentional failing regression reproduction for #419. Copy it into the test project separately to reproduce the reported default-mode duplicate-object selection bug. It passes on master and fails on the reviewed #419 head. It is not part of the performance harness.

The hierarchy async supplement reuses the repository's `AsyncHierarchyExpansionBenchmarks` workloads through a small custom driver, with eight warmups and 32 samples. Its results are not BenchmarkDotNet reports. It measures total-process allocation because work may cross async continuations.
