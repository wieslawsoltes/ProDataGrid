# Aggregate and matrix validation

```sh
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates-reverse reverse
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/smoke smoke
```

The executable first checks product aggregates and [matrix functions](../../src/ProDataGrid.FormulaEngine.Excel/MATRIX-FUNCTIONS.md) in compiled/interpreted and precision-enabled/disabled modes. It exports the actual registered function catalog. `smoke` stops after correctness assertions. The same executable can be published with `-p:PublishAot=true` for a supported native runtime.

Measurements use two warm batches and seven measured batches. Raw samples and per-process medians are written to `measurements.csv`; every measured batch checks a deterministic result. Input creation, parsing and oracle calculations are excluded. Evaluation, numerical workspace management, output allocation and checksum reads are included. Managed allocation counters are thread-local and exclude native memory.

SUMPRODUCT and SUM(lhs*rhs) are compared on the same numeric arrays of 100,000 elements. `reverse` reverses these two formula workloads in the second process. This compares alternative formulas on one engine, not old/new revisions or native Excel. Nonnumeric coercion is not interchangeable between the formulas. Direct paired-square and product scenarios exclude parser/evaluator overhead.

The matrix multiplication reference uses a straightforward triple loop over numeric FormulaArray values; the optimized call validates and snapshots numeric inputs, transposes the right operand and uses compensated accumulation. Both write independent 64-by-64 outputs, and every cell is checked against the oracle before timing. Timed results include summing all cells. Determinant/inverse workloads use a known 64-by-64 triangular matrix; the inverse is checked against its full closed-form result before measurement, and the timed inverse checksum reads its diagonal.

The comparison does not establish universal matrix superiority: validation, compensation and copying can cost more for small matrices, and platform/JIT behavior varies. Retain CPU/runtime metadata, exact revisions, tiering configuration and every process sample. There are no wall-clock test thresholds. The maintained read-only `Formula engine validation` workflow records both runtime test suites, these samples, source hashes and NativeAOT publish/run evidence.
