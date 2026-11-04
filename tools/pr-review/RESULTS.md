# Review validation results

Original inventory: 16 open PRs and 18 open issues. No GitHub PR was merged.

## Method

Apple M3 Pro, macOS 26.6 ARM64, .NET SDK 10.0.201/runtime 10.0.5, Release, tiered compilation disabled for timing probes. Each comparison uses eight fresh processes in A-B-B-A-B-A-A-B order, six warmups and 24 measured operations per process. Tables report the mean of four process means per revision, not a confidence claim from 96 supposedly independent iterations. No local review builds or functional tests ran during these measurements. Ordinary host activity and process noise remain. Current-thread managed allocation excludes background work. The async hierarchy supplement instead uses total-process allocation.

These are headless managed/layout/model probes, not native GPU/compositor performance results. Their scope is the workload in the published harness. Positive timing changes with inconsistent block direction are inconclusive; the repeated comparisons below retain the original data rather than selecting the best run.

## Initial balanced comparisons

`420` and `421` identify issue-fix branches subsequently opened as PRs #423 and #424. `338-before` is the diagnostic integration commit before fixing virtual-surface fallback; it is not the final PR head.

| Candidate | Baseline | Workload | ms/operation: base → PR | Time change | bytes/operation: base → PR |
|---|---|---|---:|---:|---:|
| 338 | master | scroll | 7.2348 → 6.7916 | -6.1% | 1,453,506 → 1,451,364 |
| 338 | master | hierarchy | 0.5286 → 0.5637 | +6.6% | 531,176 → 535,341 |
| 338-before | master | hierarchy | 0.5351 → 23.2877 | +4251.9% | 531,176 → 9,296,756 |
| 385 | base-old | focused | 6.9418 → 6.8870 | -0.8% | 1,458,215 → 1,458,258 |
| 385 | base-old | scroll | 7.0133 → 7.0047 | -0.1% | 1,460,504 → 1,458,212 |
| 391 | base-391 | source | 108.6497 → 110.4257 | +1.6% | 36,883,129 → 36,883,076 |
| 395 | master | source | 108.3490 → 107.9108 | -0.4% | 36,882,531 → 36,882,863 |
| 395 | master | scroll | 6.7020 → 6.6775 | -0.4% | 1,451,279 → 1,449,017 |
| 398 | base-old | focused | 6.9397 → 7.2346 | +4.2% | 1,456,006 → 1,460,470 |
| 398 | base-old | scroll | 6.8059 → 6.8714 | +1.0% | 1,455,850 → 1,455,931 |
| 399 | master | source | 108.7716 → 109.3626 | +0.5% | 36,882,257 → 36,882,835 |
| 399 | master | scroll | 7.2849 → 7.2739 | -0.2% | 1,455,801 → 1,451,224 |
| 401 | master | header | 0.0863 → 0.1128 | +30.7% | 18,834 → 25,898 |
| 403 | master | groups | 303.1143 → 306.2139 | +1.0% | 6,100,611 → 6,100,606 |
| 405 | master | groups | 301.9297 → 303.1170 | +0.4% | 6,100,611 → 6,100,611 |
| 405 | master | create | 3.9746 → 4.1868 | +5.3% | 7,551,982 → 10,059,220 |
| 407 | master | idle | 2.7111 → 2.6722 | -1.4% | 2,647,367 → 2,644,519 |
| 409 | master | hierarchy | 0.5277 → 0.5146 | -2.5% | 531,176 → 531,176 |
| 411 | master | details | 1.2605 → 1.2826 | +1.7% | 352,761 → 352,762 |
| 413 | master | details | 1.4249 → 1.3182 | -7.5% | 352,762 → 353,529 |
| 415 | master | selection | 9.6392 → 9.4781 | -1.7% | 1,126,064 → 1,185,488 |
| 415 | master | source | 109.5745 → 108.4773 | -1.0% | 36,882,510 → 36,882,471 |
| 417 | 415 | selection | 10.1447 → 6.2075 | -38.8% | 1,185,303 → 983,317 |
| 417 | 415 | cells | 27.5586 → 20.5133 | -25.6% | 17,942,128 → 14,973,493 |
| 419 | 417 | selection | 6.1283 → 5.1479 | -16.0% | 983,381 → 754,098 |
| 419 | 417 | cells | 21.0750 → 17.2745 | -18.0% | 14,973,514 → 11,685,839 |
| 420 | master | clipboard | 0.5757 → 0.5635 | -2.1% | 1,206,624 → 1,078,336 |
| 421 | master | lookup | 0.6648 → 0.1148 | -82.7% | 1,962,137 → 1,984 |
| 421 | master | lookup-dense | 1.6663 → 1.6356 | -1.8% | 2,456,146 → 1,084,802 |
| 421 | master | idle | 2.7052 → 1.1595 | -57.1% | 2,647,333 → 761,661 |

## Repeated cost comparisons

Eight additional processes, reverse balanced order B-A-A-B-A-B-B-A, same binaries and workload. `create` is 100 grid constructions per operation; divide by 100 for per-grid costs.

| Candidate | Workload | ms/operation: base → PR | Time change | bytes/operation: base → PR |
|---|---|---:|---:|---:|
| 338 | hierarchy | 0.5199 → 0.5333 | +2.6% | 531,176 → 535,341 |
| 401 | header | 0.0887 → 0.1122 | +26.5% | 18,834 → 25,898 |
| 405 | create | 4.0218 → 4.2434 | +5.5% | 7,551,978 → 10,059,205 |
| 415 | selection | 9.7274 → 9.3308 | -4.1% | 1,126,073 → 1,185,415 |
| 409 | hierarchy-filtered | 0.1890 → 0.4818 | +154.9% | 259,360 → 263,003 |

## Additional affected paths

| Candidate | Workload | ms/operation: base → PR | Time change | bytes/operation: base → PR |
|---|---|---:|---:|---:|
| 391 | view-source | 108.9903 → 110.3481 | +1.2% | 36,291,929 → 36,292,637 |
| 409 | hierarchy-filtered | 0.1905 → 0.4672 | +145.2% | 259,360 → 263,003 |

The #409 supplemental filter accepts every node, so both revisions process equivalent visible data while exercising the filtered path. The unfiltered-index fast path on master is invalid for filters that remove nodes (covered by the PR's correctness tests); faster incorrect partial-filter behavior is not a usable alternative. The refresh cost is an explicit correctness tradeoff.

## Async hierarchy supplement for #338

Custom driver reusing `AsyncHierarchyExpansionBenchmarks`, Batched strategy, eight warmups and 32 samples, eight balanced processes. These are not BenchmarkDotNet output.

| Shape | ms/operation: master → final #338 | bytes/operation: master → final #338 |
|---|---:|---:|
| Wide2555Depth3 | 0.2213 → 0.2225 | 411,904 → 411,904 |
| Binary4094Depth11 | 1.6150 → 1.6001 | 1,855,671 → 1,855,672 |
| VeryDeep512Depth128 | 0.2625 → 0.2559 | 402,184 → 402,184 |

## #419 opt-in comparison

Same candidate revision and 16,000 selected cells, default mode versus item tracking, balanced independent processes. Performance gains do not resolve the reproduced duplicate-object correctness regression in default mode.

- cells: 17.3370 ms, 11,686,651 bytes per insert/remove pair.
- cells-tracked: 3.0612 ms, 2,848,219 bytes per insert/remove pair.

## Functional validation

Every original exact PR head passed its existing DataGrid suite. #419 also fails the added duplicate-object reproduction, which passes on master; do not merge it. Final #338 passed 3,275 tests including expanded six-column virtual-surface coverage. The local combined branch (all applicable original PRs, excluding #419, plus #423/#424) passed 3,331 DataGrid, 2,000 formula, 190 generator, 79 diagnostics, 53 Excel sample and 392 main sample tests. Its leak suite initially had one intermittent `DataGrid_Is_Freed` failure; an isolated rerun passed all 38, and four additional balanced baseline/integration runs all passed 38/38. This initial failure is retained in the record rather than described as a clean first run.

#399 additionally passed a fresh strict osx-arm64 NativeAOT publish and native execution; the enabled-reflection negative control failed with real IL2075 diagnostics as expected.

#385 was updated to current master to replace an expired, non-rerunnable historical CI run. Its DataGrid source tree is identical before/after (`c7e4e51d87ee32c4ab5387c67063d6b5505a58bd`); the old/current baseline DataGrid trees are identical as well (`3174a758706aff11efa35d69ddef306f52145f05`). Original-head timing results therefore cover the unchanged DataGrid implementation. The updated #385 head passed all 3,250 DataGrid tests.

## Revisions

- #338: original `8ab3ddb92d4b746259e75efd8f5ec53966fc63eb`.
- #385: original `b49888cc61892131f2808de66656f12020d982db`.
- #391: original `4ffbf55c73e4db09dcc7d0f73cabaa4334d64537`.
- #395: original `a69051c9efba183dd4637cb97a3d0d5f928bbabe`.
- #398: original `a324e94f3ffb2801e0a50a764c43deedaa65f23a`.
- #399: original `596db0a6e401eb9c26b98e67de3ff6f247f0bed9`.
- #401: original `ca7e40247fe7459791e485f9e13092edc3d57eee`.
- #403: original `6641290e2e83a92a822b00201a0a984e174ab00a`.
- #405: original `81b27cc5a3b00b6f5200f76327b6dd8409dbc456`.
- #407: original `8a8538b6d1a21579a536d85027d69006661adc70`.
- #409: original `397e1c63a39680e1ba4267d9b3a4f2deaa8b7f15`.
- #411: original `bee617454da8c2aade5f55f06bf07848200b9f82`.
- #413: original `b94fa25e34f2d4428d8ea9c993a56359e765a44c`.
- #415: original `fc391733e904a939fc76db2aa62cdc1d2644475b`.
- #417: original `c3779886440358fa1783995ffcc768f97f50a0da`.
- #419: original `642d0d163965af8b8d1c28c8710ed3674297e5a1`.

- Final #338: `77e77d02a8a0b37a54ff38b37ce37d4d24c5a96a`.
- Final #385: `9ca78eb8636ee300aa3664ac4e5e9dcdb155509e`.
- #423 / issue #420: `1fcb6906699a839d64920cd13954586528697865`.
- #424 / issue #421: `9da7c14e994b03ac402cf6e4b8d09c2d7a9f8205`.
- Master: `f92035975ac5ab6c0cdede8228952bcd8f618a2e`.
- Old baseline (#385/#398): `2027658ef06f`.
- #391 baseline: `a3f142f42dd4`.
- Diagnostic #338 before fix: `57bd5922`.
- #417 measured against #415; #419 against #417.

## Review decisions and issue triage

The original PR review verdicts and performance costs have been posted to the authors. The following are code/validation recommendations; current CI is checked separately before final approval.

| PR | DataGrid tests passing | Recommendation |
|---|---:|---|
| #338 | 3,275 at fixed head | Applicable after CI; virtual-surface regression fixed directly. |
| #385 | 3,250 at updated head | Applicable after CI; retain its deferred-hide test alongside #398's two tests. |
| #391 | 3,179 | Applicable. |
| #395 | 3,250 | Applicable; fixes the open-tooltip reproduction of #390. |
| #398 | 3,149 | Applicable; complementary to #385. |
| #399 | 3,265 | Applicable; strict NativeAOT smoke also passed. |
| #401 | 3,251 | Applicable; accepted capture/release overhead reported. |
| #403 | 3,253 | Applicable. |
| #405 | 3,252 | Applicable; accepted per-grid estimator allocation reported. |
| #407 | 3,252 | Applicable. |
| #409 | 3,251 | Applicable; accepted filtered-refresh cost reported. |
| #411 | 3,253 | Applicable. |
| #413 | 3,255 | Applicable. |
| #415 | 3,250 | Applicable; modest selection allocation increase reported. |
| #417 | 3,251 | Applicable after #415; lower selection latency/allocation. |
| #419 | 3,259 existing tests; added reproduction fails | Do not merge. Default-mode duplicate-object cell selection aliases distinct rows. |

All 18 original open issues were triaged and commented: #337, #344, #347, #390, #396, #400, #402, #404, #406, #408, #410, #412, #414, #416, #418, #420, #421 and #422. Bug/enhancement/missing-reproduction labels were added where needed. Issues remain open pending merge/release; #337/#422 remain scoped enhancement proposals, #344 needs a third-party-theme minimal reproduction, and #418 remains blocked by #419's semantics.

New fixes: #423 for #420 (clipboard quoting), #424 for #421 (sparse reference-index allocation). #425 adds five existing nonparallel-collection annotations to formula allocation tests after the unchanged product test intermittently failed in macOS CI for #338 and #417. All 2,000 formula tests pass locally with the isolation change. It does not change production code or allocation budgets, and its PR explicitly does not claim the exact runtime allocation source is proven.

## Final status — 2026-11-04 23:17 UTC

All 19 expected current heads were rechecked: all 155 GitHub checks are completed successfully, all PRs remain open and individually mergeable, and no external head change occurred. The 15 applicable original PRs have formal approvals; #419 retains a changes-requested review despite green existing CI. All three new fixes have final validation comments. No PR was merged, and master remains `f92035975ac5ab6c0cdede8228952bcd8f618a2e`.

| PR | Current head | Successful checks | Final recommendation |
|---|---|---:|---|
| [#338](https://github.com/wieslawsoltes/ProDataGrid/pull/338) | `77e77d02a8a0` | 8 | Approved, applicable to merge |
| [#385](https://github.com/wieslawsoltes/ProDataGrid/pull/385) | `9ca78eb8636e` | 8 | Approved, applicable to merge |
| [#391](https://github.com/wieslawsoltes/ProDataGrid/pull/391) | `4ffbf55c73e4` | 8 | Approved, applicable to merge |
| [#395](https://github.com/wieslawsoltes/ProDataGrid/pull/395) | `a69051c9efba` | 8 | Approved, applicable to merge |
| [#398](https://github.com/wieslawsoltes/ProDataGrid/pull/398) | `a324e94f3ffb` | 8 | Approved, applicable to merge |
| [#399](https://github.com/wieslawsoltes/ProDataGrid/pull/399) | `596db0a6e401` | 10 | Approved, applicable to merge |
| [#401](https://github.com/wieslawsoltes/ProDataGrid/pull/401) | `ca7e40247fe7` | 8 | Approved, applicable to merge |
| [#403](https://github.com/wieslawsoltes/ProDataGrid/pull/403) | `6641290e2e83` | 8 | Approved, applicable to merge |
| [#405](https://github.com/wieslawsoltes/ProDataGrid/pull/405) | `81b27cc5a3b0` | 8 | Approved, applicable to merge |
| [#407](https://github.com/wieslawsoltes/ProDataGrid/pull/407) | `8a8538b6d1a2` | 8 | Approved, applicable to merge |
| [#409](https://github.com/wieslawsoltes/ProDataGrid/pull/409) | `397e1c63a396` | 8 | Approved, applicable to merge |
| [#411](https://github.com/wieslawsoltes/ProDataGrid/pull/411) | `bee617454da8` | 8 | Approved, applicable to merge |
| [#413](https://github.com/wieslawsoltes/ProDataGrid/pull/413) | `b94fa25e34f2` | 8 | Approved, applicable to merge |
| [#415](https://github.com/wieslawsoltes/ProDataGrid/pull/415) | `fc391733e904` | 8 | Approved, applicable to merge |
| [#417](https://github.com/wieslawsoltes/ProDataGrid/pull/417) | `c37798864403` | 8 | Approved, applicable to merge after #415 |
| [#419](https://github.com/wieslawsoltes/ProDataGrid/pull/419) | `642d0d163965` | 8 | Do not merge: default-mode duplicate-object selection regression |
| [#423](https://github.com/wieslawsoltes/ProDataGrid/pull/423) | `1fcb6906699a` | 8 | Validated, applicable to merge |
| [#424](https://github.com/wieslawsoltes/ProDataGrid/pull/424) | `9da7c14e994b` | 8 | Validated, applicable to merge |
| [#425](https://github.com/wieslawsoltes/ProDataGrid/pull/425) | `f66f933cbc37` | 9 | Validated, applicable to merge |

The initial macOS CI attempts for #338 and #417 failed unchanged formula allocation tests (7,912 bytes over 50 calls in different paired-square operations). Their targeted job reruns passed without product changes. #425 separately places the five allocation-helper test classes in the existing nonparallel collection; its own complete CI also passed, including macOS, with all budgets and positive controls unchanged. This is documented test isolation hardening, not proof of the precise runtime allocation source. The earlier transient local leak failure and its successful repeats remain documented above.

The full final CI snapshot, check URLs, exact heads and GitHub review decisions are in [results/final-pr-status.json](results/final-pr-status.json). Existing CI passing on #419 does not override the additional failing review reproduction.
