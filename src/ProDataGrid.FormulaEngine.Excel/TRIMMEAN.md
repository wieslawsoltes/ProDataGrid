# Trimmed means

`TRIMMEAN(array,percent)` excludes the same number of numeric observations from each tail and returns the mean of the retained values. The removed total is the specified proportion of the numeric count, rounded down to an even number.

```text
TRIMMEAN({1;2;3;4;100},0.4)    // 3
TRIMMEAN({1;2;3;4;100},0)      // 22
TRIMMEAN(A1:A20000,{0;0.1;0.2;0.5})
```

The sample is one shared distribution, flattened row-major. Blank, text and logical elements, including masked holes, are ignored; numeric zero is included. Formula errors retain their metadata. Nonfinite inputs, invalid percentages or an empty retained distribution return `#NUM!`. Percentages use ordinary numeric coercion; a blank is zero. A percentage array produces an independently owned result of the same shape, including per-element percentage errors. A scalar percentage produces a scalar result. One-cell arrays retain their array shape.

The implementation accepts percentages in [0,1]. At 1, the even-removal rule leaves one central observation for an odd numeric count and no observations for an even count; the latter returns `#NUM!`. This endpoint and unusual coercion/error-precedence behavior are explicit implementation contracts, not native Excel differential certification.

## Numerical behavior

After existing workbook precision normalization, each retained finite binary64 value is accumulated exactly as an integer multiple of the smallest positive subnormal. Positive and negative sums use fixed-size private stack limbs, not BigInteger objects, decimal strings, reflection or global caches. Division by the retained count preserves the remainder and rounds the final mean to binary64, nearest with ties to even. Existing workbook result-precision normalization is then applied.

This avoids overflowing an intermediate sum or losing a small residual between cancelling large terms. For example, with precision normalization disabled, `TRIMMEAN({1E308;1;-1E308},0)` returns the rounded value of one third. A mean of repeated `1E308` values remains finite; subnormal means and halfway cases are handled without scaling them away. Exact zero returns positive zero; a negative nonzero result that underflows can retain negative zero.

Exact accumulation applies to the represented, already-coerced input values. It does not restore decimal information lost before evaluation, turn the workbook into arbitrary-precision arithmetic, or promise bit-for-bit native Excel output.

## Scalar and batch execution

A scalar nonzero trim request uses two existing bounded order-statistic selections; the retained interior need not be sorted. A zero-trim scalar skips selection. The selection backend retains its introspective sorting fallback, so adversarial work is bounded by O(n log n), not promised worst-case linear.

For multiple percentages, sample values are privately sorted once. Queries are ordered from the smallest retained core to the largest; each value is added at most once as the core expands. Identical trim counts reuse a result. The accumulator can be read without destroying its sum, avoiding unstable subtraction of large prefix sums. Work is O(n log n + q log q + n + q) with a fixed-width numerical factor; workspace is O(n+q) plus fixed stack limbs and O(q) output. This is not a persistent cross-call cache.

All input expressions are evaluated once by the normal eager evaluator. Host arrays/masks are never partitioned or sorted in place. Small workspaces use stack storage; larger numeric/query buffers are cleared before return to the pool on every exit. `MaximumArrayCellCount` bounds physical sample positions and percentage/output cells. Caller-provided input arrays must remain stable during invocation. These are per-call policies, not whole-workbook memory, elapsed-time or concurrency guarantees.

## Validation and diagnostics

Tests compare both scalar and batch paths against exact BigInteger rational sums followed by an independent search over representable doubles. They cover exponent extremes, cancellation, subnormals, halfway ties, permutations, masks, errors, source ownership, limits, concurrent pool use and real spill recalculation. The production library has no BigInteger dependency or managed accumulator allocation.

The retained aggregate harness compares equivalent array-target and MAP formulas and a full-sort scalar reference. It validates complete results before timing, includes output/workspace clearing in the measurements and retains raw samples. Such measurements are workload-specific, not comparisons with a native Excel process.

Primary syntax/removal rule: [Microsoft TRIMMEAN](https://support.microsoft.com/en-us/excel/functions/trimmean-function) and [WorksheetFunction.TrimMean](https://learn.microsoft.com/en-us/office/vba/api/excel.worksheetfunction.trimmean).
