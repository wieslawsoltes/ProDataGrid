# Product and paired-square aggregates

`SUMPRODUCT`, `SUMX2MY2`, `SUMX2PY2` and `SUMXMY2` operate directly on evaluated values without constructing product/square arrays. Scalar inputs are one-cell views, not allocated arrays. Numeric values honor the workbook's significant-digit setting; nonfinite inputs and intermediate overflow return `#NUM!`.

`SUMPRODUCT` accepts 1 through 255 arguments. All arrays must have identical row and column dimensions; it does not broadcast a scalar or a row into a larger array. Nonnumeric entries, including numeric text, booleans and absent positions, contribute zero. Explicit arithmetic inside an argument can coerce booleans before aggregation: `SUMPRODUCT(--(A1:A20>0),B1:B20)`. Formula errors propagate even when another factor at the same position is zero. One- and two-input loops allocate no workspace. More inputs use a pooled operand-view buffer returned with managed references cleared, including error/exception exits.

The three paired-square functions require equal numbers of cells. Inputs are paired in row-major order, including when row/column shapes differ. A text, Boolean or blank position skips its pair without shifting later observations. Numeric zeros are included. Unequal counts return `#N/A`; formula errors propagate. The operations are respectively `sum(x*x-y*y)`, `sum(x*x+y*y)` and `sum((x-y)*(x-y))`.

All four use compensated summation to reduce cancellation error. Source arrays, masks and origins remain unchanged. The algorithms are O(cells × arguments) for SUMPRODUCT and O(cells) for paired squares, with no retained per-cell storage. Eager evaluation may still allocate input ranges or arithmetic arrays before the aggregate runs; this is not a new streaming worksheet resolver or global memory/time quota.

## Verification and reproduction

The regression suite covers both evaluators, formatter round trips, precision modes, official paired examples, shape/error rules, masked holes, argument limits, source ownership, cancellation, seeded independent loops, dirty dependency recalculation, concurrent calls and warm allocation checks.

`tools/formula-aggregate-benchmarks` checks representative formulas, exports the runtime function catalog and compares `SUMPRODUCT(lhs,rhs)` with the equivalent `SUM(lhs*rhs)` on numeric inputs. Input construction/parsing are excluded; evaluation and result/workspace allocations are included. Two warm batches and seven measured batches are retained, with exact integer checksums. The CI job reverses the order of the equivalent formulas in its second process. This is a comparison of alternative formulas on one engine, not a native Excel benchmark or a claim that coercion semantics are identical for nonnumeric inputs.

```sh
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/smoke smoke
dotnet test src/ProDataGrid.FormulaEngine.UnitTests/ProDataGrid.FormulaEngine.UnitTests.csproj -c Release -p:FormulaTestFramework=net8.0
```

Binary64 intermediate multiplication/squaring may overflow even when symbolic rearrangement could yield a finite mathematical answer; this implementation returns a numerical error rather than claiming arbitrary-precision evaluation. Compensation can differ in final bits from native Excel or the engine's older sequential SUM. Native Excel differential qualification, every mixed reference-provenance/error-precedence case and full application compatibility are not claimed.

Syntax references: [SUMPRODUCT](https://support.microsoft.com/en-us/excel/functions/sumproduct-function), [SUMX2MY2](https://support.microsoft.com/en-us/excel/functions/sumx2my2-function), [SUMX2PY2](https://support.microsoft.com/en-us/excel/functions/sumx2py2-function), [SUMXMY2](https://support.microsoft.com/en-us/excel/functions/sumxmy2-function).
