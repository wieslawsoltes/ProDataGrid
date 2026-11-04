# Persistent indicator diagnostics

```sh
dotnet run --project tests/ProCharts.Streaming.Benchmarks/ProCharts.Streaming.Benchmarks.csproj -c Release
```

The existing MACD comparison and four-indicator throughput fixture remain intact. Two rolling sections compare actual batch APIs against persistent calculators, using independent batch reference outputs and complete-window/gap conventions. Each measured output is checked, not merely a final checksum. Two paired warmups precede seven paired measurements with alternating order; shared-runner/JIT results are diagnostics, not fixed time thresholds or UI latency percentiles.

## Full-prefix comparison

Rolling statistics (SMA, WMA and population/sample deviations), Bollinger and Donchian are measured for periods 32/512. Each operation processes 2,048 seed observations and 256 updates. Initial input/calculator construction is outside timing; seed calculation, updates and output validation are inside for both paths. All appended results are ready, not null warmup output. Combined statistics invokes four separate batch APIs versus one shared rolling state; bands/channels compare one API each. Batch-prefix wrappers reuse input storage, avoiding artificial source-copy cost.

## Current-window-only comparison

A second, stricter comparison supplies batch Bollinger/Donchian with **only the current period-sized window**, not full history. It measures 512 ready updates for periods 32/512. Initial construction and persistent reseeding are excluded for both paths; per-update calculation and output checks are included. Reused source slices change offset only between synchronous stateless calls. No source shifting/copying overhead is charged to the batch caller. The slices are not passed to renderer caches.

These two sections use different schedules, seed rules and update counts. Do not combine their medians into one ratio. Neither establishes that an existing host previously used the measured batch schedule; one-off historical analysis remains appropriate for batch APIs.

## Storage and transfer bursts

The rolling queue trades O(period) retained summaries for avoiding subtractive eviction drift. Constructor allocation and the first full-window transfer update versus its following ordinary update are reported for periods 32, 2,048 and 16,384. Those are different queue operations, not equivalent algorithms. Timing overhead affects tiny updates. Amortized O(1) work is **not** worst-case constant latency.

Managed allocations are calling-thread totals, not peak RSS or retained-heap measurements. Rendering, external feeds, native GPU work, frame scheduling and screen presentation are not measured. The existing read-only charting workflow includes this program and retains raw output in `incremental-indicator-performance.txt`; no additional workflow, permission or dependency is required.
