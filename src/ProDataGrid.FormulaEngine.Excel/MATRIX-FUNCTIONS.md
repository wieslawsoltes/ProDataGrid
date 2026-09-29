# Matrix functions

The registry provides `MMULT`, `MDETERM`, `MINVERSE` and `MUNIT`, with dynamic-array results, dependency tracking, compiled/interpreted evaluation and the existing worksheet spill engine.

```text
MMULT({1,2;3,4},{5,6;7,8})
MDETERM({1,3,8,5;1,3,6,1;1,1,1,0;7,3,10,2})
MINVERSE({4,7;2,6})
LET(a,{4,7;2,6},MMULT(a,MINVERSE(a)))
MUNIT(4)
```

Matrices must contain numeric values. Unlike SUMPRODUCT, blank cells, sparse holes, text (including numeric text), Boolean and callable entries return `#VALUE!`; element errors preserve their error value. Nonfinite numeric inputs and overflowing results return `#NUM!`. Scalars are treated as one-by-one matrices. MMULT requires matching inner dimensions. Determinants and inverses require square inputs. A detected singular matrix has determinant zero; its inverse returns `#NUM!`. MUNIT truncates a numeric dimension to an integer, requires a positive result and writes explicit numeric zeros off the diagonal.

## Numerical implementation

MMULT validates and snapshots both inputs once, transposing the right operand so each inner dot product reads contiguous doubles. Compensated accumulation reduces cancellation, without per-term FormulaValue dispatch. The newly owned output has no source origin. The original input arrays, masks and values are never overwritten.

MDETERM and MINVERSE share an in-place LU factorization of a private snapshot. Power-of-two row equilibration reduces avoidable scale imbalance; partial pivoting selects the largest available pivot in the active column. Permutation and original scaling metadata are retained separately. The inverse solves for each unit column and reverses row scaling on the corresponding output column. Determinants accumulate diagonal mantissas and exponents separately, preventing a partial diagonal product from overflowing/underflowing when the final value remains representable.

Small numeric workspaces use caller stack storage; larger buffers are pooled. Numeric spans and pivot metadata are cleared before return, including error exits. Warm determinant calls do not need per-call managed allocations, but still require O(n squared) workspace. Matrix result arrays remain owned allocations. No reflection, native linear-algebra dependency, unsafe reference overlay or mutable global cache is introduced.

## Host resource limits

```csharp
workbook.Settings.MaximumArrayCellCount = 1_048_576;
workbook.Settings.MaximumMatrixOperationCount = 100_000_000;
```

Each input snapshot and output must fit the existing cell cap and Excel row/column ceilings. Before matrix workspace is allocated, an overflow-safe work check estimates `rows * inner * columns` for MMULT, `n^3` for MDETERM, `2*n^3` for MINVERSE, and `n^2` for MUNIT. Excess work returns `#NUM!`. The positive host-configurable estimate is not an Excel size restriction, exact CPU instruction count, time limit or total-workbook memory quota. Raising it enables larger calculations but does not eliminate their cost. Inputs may already have been materialized by the evaluator before these checks run.

## Qualification boundaries

The implementation uses binary64 runtime math, not arbitrary precision. LU detects exact zero pivots, without an absolute epsilon that would incorrectly reject very small invertible diagonals; nearly singular matrices may therefore produce finite but inaccurate inverses. No condition-number estimate, rank-revealing factorization, iterative refinement or blanket accuracy guarantee is supplied. Excess within-row dynamic range can underflow during equilibration; that condition returns a numerical error rather than silently dropping a nonzero entry. Other intermediate dot-product/elimination overflows also return numerical errors. True final determinant underflow can produce zero even for a mathematically nonsingular matrix. Precision normalization applies to inputs and outputs, not every inner LU operation. Bit-for-bit native Excel parity is not certified.

Regression tests cover official determinant examples, exact-integer Bareiss comparisons, independently computed rectangular products, pivot signs, singularities, finite-extreme determinants, scale-normalized inverse residuals, source ownership, error paths, concurrency, dirty spill recalculation and configured limits. The aggregate benchmark executable adds full-result multiplication/inverse checks, timed 64-by-64 public-value versus contiguous multiplication, pooled determinants/inverses and native parser smoke assertions. These are focused measurements, not Microsoft Excel, GPU, end-to-end workbook or all-platform numerical qualification.

References: [MMULT](https://support.microsoft.com/en-us/excel/functions/mmult-function), [MDETERM](https://support.microsoft.com/en-us/excel/functions/mdeterm-function), [MINVERSE](https://support.microsoft.com/en-us/excel/functions/minverse-function), [MUNIT](https://support.microsoft.com/en-us/excel/functions/munit-function).
