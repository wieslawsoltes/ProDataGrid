# Range-area interaction diagnostics

```sh
dotnet run --project tests/ProCharts.Range.Benchmarks/ProCharts.Range.Benchmarks.csproj -c Release
```

This compares the actual retained reference renderer (`UseInteractionCache = false`) against the indexed path on identical snapshots, axes and pointer coordinates. It uses two paired warmups and seven paired measurements, alternating which implementation runs first. Source construction is excluded. Cold first-query measurements include renderer/index preparation; warm tests reuse an already prepared renderer. Every query is compared before timing, and every measured repetition compares its result checksum.

The fixtures contain 10,000 and 100,000 aligned intervals with nonconstant boundaries and periodic gaps. A query batch contains 128 deterministic hits/misses across the viewport. A viewport batch contains 32 calls. Managed allocation is measured on the calling thread; elapsed-time thresholds are intentionally not enforced on shared CI runners.

The original projection and scan methods remain unchanged and are exercised by the reference path. Report cold cost as well as warm-query savings: the index retains O(n) projected managed data and is rebuilt after snapshot, relevant style or viewport changes. Dense projected X columns or a very wide hit radius can still require a linear candidate scan. Unordered raw X data retains reference selection rather than being sorted into a different chart. More than one million values and unsupported mixed plot kinds retain the existing full reference path.

These measurements do not represent native rasterization, GPU throughput, frame pacing or end-to-end UI latency. The existing read-only charting workflow retains `range-interaction-performance.txt` alongside its test results and other benchmarks.
