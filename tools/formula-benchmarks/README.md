# Formula microbenchmarks

Run with .NET 10 from the repository root:

```sh
dotnet run -c Release --project tools/formula-benchmarks/FormulaBenchmarks.csproj -- artifacts/formula.csv current
```

The harness uses public APIs, verifies results during warmup and each measured batch, and writes all seven samples plus median time/allocation per operation. It measures cached literals, a cached 128-addition expression, dense sorted XMATCH over 100,000 values, and UNIQUE over 5,000 distinct numeric rows. Array creation, registry creation, parsing and compilation are outside the timed regions. Number precision normalization is disabled to isolate evaluation and indexing costs.

For an A/B comparison, copy the unchanged benchmark directory into a worktree at the baseline revision, build both worktrees with the same SDK/configuration, and run baseline/current/current/baseline on the same machine. Retain the CSV files, runtime and OS metadata, commit identifiers, environment settings and CPU information. Compare medians without treating hosted-runner timings as universal promises.

Allocation counts are synchronous thread-local managed allocations. They exclude native memory and worker-thread allocations; pool/cache warmup is excluded. These are focused microbenchmarks, not end-to-end workbook recalculation, rendering, UI responsiveness or cross-platform performance qualification. No timing thresholds are enforced by unit tests.
