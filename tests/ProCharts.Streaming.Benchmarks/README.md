# Persistent indicator diagnostics

```sh
dotnet run --project tests/ProCharts.Streaming.Benchmarks/ProCharts.Streaming.Benchmarks.csproj -c Release
```

The existing MACD comparison and four-indicator throughput fixture remain intact. The rolling section compares actual batch-prefix recalculation against persistent rolling calculators on identical observations, with complete-window/gap conventions checked against batch output at every point.

Rolling statistics (SMA, WMA and both population/sample deviations), Bollinger bands and Donchian channels are measured separately for periods 32 and 512. Each operation processes 2,048 seed observations and 256 subsequent updates. Initial input/calculator construction is outside timing; seed calculation, updates and matching output validation are inside timing for both paths. For combined statistics the batch side invokes four separate existing APIs while the persistent side shares one rolling state. For bands/channels one corresponding API is compared. Batch-prefix objects reuse their input storage, so no synthetic source-copy penalty is introduced.

Two paired warmups precede seven paired measurements in alternating order. Every actual update result is checked, not merely a final aggregate checksum. Shared-runner/JIT timings are diagnostic; no fixed time threshold is enforced. Managed allocations are calling-thread totals, not peak RSS or retained heap measurements.

The rolling engine deliberately trades O(period) retained summaries for avoiding subtractive eviction drift. Constructor allocation and the first full-window transfer update versus its following ordinary update are reported for periods 32, 2,048 and 16,384. Those two single-update measurements are different queue operations, not equivalent algorithms. Timing overhead affects tiny updates; medians of seven samples are not application latency percentiles. The engine provides amortized O(1) work, not worst-case constant latency.

One-off historical analysis still belongs in batch APIs. This benchmark does not establish that existing hosts previously recalculated every prefix, nor does it measure chart rendering, external feeds, UI frame scheduling, native GPU work or screen presentation. The existing read-only charting workflow includes this program and retains its raw output in `incremental-indicator-performance.txt`; no additional workflow or permissions are needed.
