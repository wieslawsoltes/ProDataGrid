# Aggregate, matrix and statistical validation

```sh
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates-reverse reverse
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/smoke smoke
```

The executable first checks product aggregates, [matrix functions](../../src/ProDataGrid.FormulaEngine.Excel/MATRIX-FUNCTIONS.md), [descriptive statistics](../../src/ProDataGrid.FormulaEngine.Excel/DESCRIPTIVE-STATISTICS.md) and [paired statistics/regression](../../src/ProDataGrid.FormulaEngine.Excel/PAIRED-STATISTICS.md) in compiled/interpreted and precision-enabled/disabled modes. It exports the actual registered function catalog. `smoke` stops after correctness assertions. The same executable can be published with `-p:PublishAot=true` for a supported native runtime.

Measurements use two warm batches and seven measured batches. Raw samples and per-process medians are written to `measurements.csv`; every measured batch checks a deterministic result. Input creation, parsing and oracle calculations are excluded. Evaluation, numerical workspace management, output allocation and checksum reads are included. Managed allocation counters are thread-local and exclude native memory.

SUMPRODUCT and SUM(lhs*rhs) are compared on the same numeric arrays of 100,000 elements. `reverse` reverses these two formula workloads in the second process. This compares alternative formulas on one engine, not old/new revisions or native Excel. Nonnumeric coercion is not interchangeable between the formulas. Direct paired-square and product scenarios exclude parser/evaluator overhead.

The matrix multiplication reference uses a straightforward triple loop over numeric FormulaArray values; the optimized call validates and snapshots numeric inputs, transposes the right operand and uses compensated accumulation. Both write independent 64-by-64 outputs, and every cell is checked against the oracle before timing. Timed results include summing all cells. Determinant/inverse workloads use a known 64-by-64 triangular matrix; the inverse is checked against its full closed-form result before measurement, and the timed inverse checksum reads its diagonal.

Paired-statistics measurements compare an array forecast that fits once with an equivalent MAP formula that refits for each target. Both use 2,048 training observations and 128 targets, include owned output and all fitting/workspace costs, and check every result before timing plus a complete-result checksum during each measured call. Comparison order is reversed in the second process. Direct SLOPE throughput is measured separately. The descriptive-statistic scenarios compare the scaled private snapshot with a retained list-based numerical reference on the same 100,000 inputs, including list/snapshot creation and clearing costs.

The comparisons do not establish universal superiority: validation, compensation and copying can cost more for small problems, and platform/JIT behavior varies. Retain CPU/runtime metadata, exact revisions, tiering configuration and every process sample. There are no wall-clock test thresholds. Zero warm managed allocation does not mean zero retained workspace or zero cold allocation. The maintained read-only `Formula engine validation` workflow records both runtime test suites, scalar-fallback tests, samples, source hashes and NativeAOT publish/run evidence.
