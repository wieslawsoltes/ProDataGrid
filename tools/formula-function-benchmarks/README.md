# Formula function benchmarks and native smoke checks

```sh
dotnet run -c Release --project tools/formula-function-benchmarks/FormulaFunctionBenchmarks.csproj -- artifacts/formula-functions
# Smoke assertions and catalog only:
dotnet run -c Release --project tools/formula-function-benchmarks/FormulaFunctionBenchmarks.csproj -- artifacts/formula-functions-smoke smoke
# NativeAOT example (requires a supported native toolchain):
dotnet publish tools/formula-function-benchmarks/FormulaFunctionBenchmarks.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/native
./artifacts/native/FormulaFunctionBenchmarks artifacts/formula-functions-native smoke
```

The executable first checks representative lexical, higher-order, callable-array, text and Unicode formulas in both evaluation modes and exports the registered function catalog. Runtime measurements use two warm batches and seven measured batches; every batch verifies a deterministic checksum. Raw samples and per-process medians are written to `measurements.csv` with runtime/OS/architecture metadata.

Higher-order cases operate on 10,000 elements (BYROW uses a 1,000-by-10 matrix), comparing compiled and interpreted execution. The two repeated-expression cases have the same result: one repeats array arithmetic three times; the other uses LET to compute the array once and reuse it. This compares alternative formulas on the same engine, not old/new library revisions and not Microsoft Excel performance.

Text scenarios exercise 10,000-cell EXACT, a 20,000-character REPT result, 10,000 SUBSTITUTE matches, and literal/wildcard SEARCH. `direct` means invocation through the registry's eager API with preallocated arguments, excluding parser/evaluator call overhead. All input creation and parsing are excluded from timed regions; output allocations, lambda frames and callback evaluation are included. Precision normalization is disabled. Thread-local managed-allocation counters exclude native memory and worker-thread allocations.

Run the whole executable repeatedly on the same machine; retain runtime metadata, CPU details, tiered-compilation settings and exact source revision. These are warm microbenchmarks rather than workbook-recalculation, cold-start, memory-peak, UI or cross-platform performance qualification. No timing thresholds are imposed on unit tests. Native smoke is a small functional check, not the entire xUnit suite running as NativeAOT.
