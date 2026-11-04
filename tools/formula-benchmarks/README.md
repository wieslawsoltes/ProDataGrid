# Formula microbenchmarks

Run with .NET 10 from the repository root:

```sh
dotnet run -c Release --project tools/formula-benchmarks/FormulaBenchmarks.csproj -- artifacts/formula.csv current
```

The harness uses public APIs, verifies results during warmup and each measured batch, and writes all seven samples plus median time/allocation per operation. Core scenarios measure cached literals, cached 128-addition expressions with precision normalization disabled and enabled, a 10,000-element array addition, dense sorted XMATCH over 100,000 values, and UNIQUE over 5,000 distinct numeric rows.

Input array creation, registry creation, parsing and compilation are outside the timed core scenarios. Number precision normalization is disabled except for the explicitly named `cached_128_additions_precision` scenario. Output-array construction and writes are included in the array-addition scenario.

For an A/B comparison, copy the unchanged benchmark directory into a worktree at the baseline revision, build both worktrees with the same SDK/configuration, and run baseline/current/current/baseline on the same machine. Retain all CSV files, runtime and OS metadata, commit identifiers, environment settings and CPU information. Compare medians without treating hosted-runner timings as universal promises. Tiered-compilation settings materially affect results and must match for both revisions.

Allocation counts are synchronous thread-local managed allocations. They exclude native memory and worker-thread allocations; pool/cache warmup is excluded. These are focused microbenchmarks, not end-to-end workbook recalculation, rendering, UI responsiveness or cross-platform performance qualification. No timing thresholds are enforced by unit tests.

Numeric fast-path validation separately compares compiled and interpreted operations to the existing general coercion contract, including configured input/result precision, error propagation, signed zero and nonfinite values. That compatibility test is not a claim that all legacy numeric behavior is identical to Microsoft Excel.
