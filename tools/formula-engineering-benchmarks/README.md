# Engineering formula diagnostics

```sh
dotnet run -c Release --project tools/formula-engineering-benchmarks/FormulaEngineeringBenchmarks.csproj -- artifacts/engineering
dotnet run -c Release --project tools/formula-engineering-benchmarks/FormulaEngineeringBenchmarks.csproj -- artifacts/engineering-reverse reverse
# Parser/evaluator checks and function catalog only:
dotnet run -c Release --project tools/formula-engineering-benchmarks/FormulaEngineeringBenchmarks.csproj -- artifacts/engineering-smoke smoke
# Native smoke requires a supported native toolchain:
dotnet publish tools/formula-engineering-benchmarks/FormulaEngineeringBenchmarks.csproj -c Release -r linux-x64 -p:PublishAot=true -o artifacts/native-engineering
./artifacts/native-engineering/FormulaEngineeringBenchmarks artifacts/engineering-native smoke
```

The executable checks 44 parser/evaluator assertions across both modes, exports a sorted function catalog, and measures two warm plus seven sampled batches. Every operation verifies a checksum; both array formulas additionally compare every output cell to an integer oracle before timing. The second process reverses comparison order. Keep exact source revision, CPU/runtime metadata, all CSV samples and runtime-tiering settings with any reported results.

`BITAND(input,255)` and `MAP(input,LAMBDA(x,BITAND(x,255)))` produce equivalent 10,000-element results on the same engine. This is a comparison of two formulas, not old/new engine releases or native Excel. Inputs and parsing are excluded; evaluation, output allocation and a full-result checksum are included. Scalar direct cases exclude evaluator dispatch, use preallocated argument arrays and cover 53-bit and 481-bit text parsing plus BASE formatting. Precision normalization is disabled in measured workloads.

Allocation is synchronous thread-local managed allocation, not retained or peak memory, native memory, cold allocation or total workbook resource use. No performance threshold is imposed on tests and no universal speedup is implied. Native smoke executes representative functions, not the whole xUnit suite under NativeAOT.
