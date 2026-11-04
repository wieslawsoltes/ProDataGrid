# Excel formula functions

`ExcelFunctionRegistry` supplies built-in functions to the reusable formula engine. Create one registry per function configuration and reuse it across evaluations; replacing a registration changes its `Version` and invalidates cached eager/lazy compilation plans. Synchronize registry changes with evaluation in the host.

## Function-family guides

- [Lexical functions](LEXICAL-FUNCTIONS.md): LET, LAMBDA, ISOMITTED, captured scopes and named recursion.
- [Higher-order arrays](LAMBDA-HELPERS.md): MAP, REDUCE, SCAN, BYROW, BYCOL, MAKEARRAY and callable composition.
- [Text and Unicode](TEXT-FUNCTIONS.md): text search/transforms, broadcasting and character-index compatibility modes.
- [Scientific math](SCIENTIFIC-FUNCTIONS.md): trigonometry, hyperbolic functions, combinatorics and integral operations.
- [Order statistics](ORDER-STATISTICS.md): inclusive/exclusive percentiles, bounded selection, numerical interpolation and workspace ownership.
- [Product aggregates](PRODUCT-AGGREGATES.md): SUMPRODUCT and paired square sums, compensated accumulation, strict pairing and allocation costs.
- [Descriptive statistics](DESCRIPTIVE-STATISTICS.md): scaled variance/deviation, central moments, positive means and compatibility aliases.
- [Paired statistics](PAIRED-STATISTICS.md): covariance, correlation, least-squares diagnostics and single-fit array forecasting.
- [Ranking](RANKING-FUNCTIONS.md): scalar scans, shared-distribution batch ranks, inclusive/exclusive percentile ranks and tie handling.
- [Matrices](MATRIX-FUNCTIONS.md): multiplication, determinants, inversion and bounded private numerical workspaces.
- [Business calendars](BUSINESS-CALENDARS.md): international weekends, indexed holidays and bounded workday arithmetic.
- [Text splitting](TEXTSPLIT.md): delimiter arrays and bounded two-pass output materialization.
- [Bitwise functions](BITWISE-FUNCTIONS.md) and [radix conversions](RADIX-FUNCTIONS.md): checked integer domains, array evaluation and exact internal integer parsing.

## Modern arrays

| Function family | Behavior |
| --- | --- |
| `HSTACK`, `VSTACK` | Concatenate arrays across columns or rows. Unequal dimensions are padded with `#N/A`. Scalar errors remain elements rather than aborting the entire stack. |
| `TAKE`, `DROP` | Keep or remove leading/trailing rows and columns. Negative counts select the trailing end. An omitted axis is unchanged; explicit zero counts produce `#CALC!`. |
| `CHOOSEROWS`, `CHOOSECOLS` | Select, reorder and repeat one-based indices. Negative indices count from the end. Index arrays are accepted and read in row-major order. |
| `EXPAND` | Enlarge dimensions with optional padding, defaulting to `#N/A`; shrinking is an error. |
| `TOCOL`, `TOROW` | Flatten in row-major or column-major order. Ignore mode 0 keeps everything, 1 excludes blanks, 2 excludes errors, and 3 excludes both. Empty text is not a blank cell. |
| `WRAPROWS`, `WRAPCOLS` | Reshape a vector with optional padding. A wrap count greater than the vector length returns a single row or column. |
| `TRANSPOSE` | Exchange rows and columns into an independent result array. |
| `SORTBY` | Sort against one or more matching row or column vectors. Orders are 1 or -1; equal keys retain their original order. |
| `RANDARRAY` | Generate a volatile rectangular array, with decimal or uniformly sampled integral values within the requested bounds. |

These functions compose with existing `SEQUENCE`, `FILTER`, `SORT`, `UNIQUE`, lookup functions, arithmetic, spill calculation and dependency recalculation. For example:

```text
HSTACK(A1:A20,A1:A20*2)
TAKE(SORTBY(A1:C20,C1:C20,-1),5)
WRAPROWS(TOROW(A1:C4),3)
CHOOSECOLS(A1:D20,4,1,4)
```

Value-producing transformations materialize blank reference cells and sparse holes as zero unless the flattening ignore mode removes them. They preserve element errors and return newly owned arrays without a source-range origin. No source array is modified.

## Allocation controls

```csharp
workbook.Settings.MaximumArrayCellCount = 1_048_576;
```

The default cap is 1,048,576 cells **per function result**. `SEQUENCE` and the modern functions above check this cap before allocating the output. Hosts can set another positive value. The row and column limits remain 1,048,576 and 16,384 respectively. Oversized results return `#NUM!`; empty results return `#CALC!` where applicable.

This is not a workbook-wide memory quota: existing range materialization, operators, custom functions, nested expressions and retained spill results can allocate additional memory. Untrusted formula workloads still need host-level resource and execution controls.

## Validation

The read-only `Formula engine validation` workflow runs the same regression sources on net8 and net10, builds both library targets, runs the reusable function/aggregate smoke executables and records NativeAOT checks, raw benchmark samples and source hashes. Default repository tests still target net10; the optional `FormulaTestFramework` property selects the alternative runtime without source edits.

```sh
dotnet test src/ProDataGrid.FormulaEngine.UnitTests/ProDataGrid.FormulaEngine.UnitTests.csproj -c Release -p:FormulaTestFramework=net8.0
dotnet test src/ProDataGrid.FormulaEngine.UnitTests/ProDataGrid.FormulaEngine.UnitTests.csproj -c Release -p:FormulaTestFramework=net10.0
dotnet run -c Release --project tools/formula-aggregate-benchmarks/FormulaAggregateBenchmarks.csproj -- artifacts/aggregates
```

## Compatibility boundaries

The regression suite checks compiled and interpreted execution, negative/repeated indices, omissions, blank/error distinctions, deterministic sort ties, allocation limits, random-array bounds, seeded reshape round trips and spill recalculation. It is not a native Microsoft Excel differential qualification.

`SORTBY` uses deterministic ordinal, case-insensitive text ordering and keeps numbers, text, booleans, errors and blanks as distinct sort categories. Locale-specific Excel collation and detailed error ordering are not certified. Random integers are restricted to the exact-integer domain of a double. The engine does not currently distinguish a syntactically omitted argument from a scalar blank in every context.

LET, LAMBDA and ISOMITTED are described in [Lexical functions](LEXICAL-FUNCTIONS.md). The complete Microsoft Excel function catalog, native-Excel differential qualification, and full application/format interoperability are not claimed by this implementation.

Function syntax references: [Microsoft array functions](https://support.microsoft.com/en-us/excel/functions/hstack-function), [TAKE](https://support.microsoft.com/en-us/excel/functions/take-function), [DROP](https://support.microsoft.com/en-us/excel/functions/drop-function), [SORTBY](https://support.microsoft.com/en-us/excel/functions/sortby-function), [RANDARRAY](https://support.microsoft.com/en-us/excel/functions/randarray-function).
