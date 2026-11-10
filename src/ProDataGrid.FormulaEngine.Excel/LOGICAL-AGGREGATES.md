# Logical-value-aware aggregates

`AVERAGEA`, `MINA` and `MAXA` include logical and text values in arrays/references. TRUE contributes one; FALSE and text (including numeric-looking and empty text) contribute zero. Empty cells and sparse holes are ignored. Direct scalar numeric text uses workbook-culture numerical coercion; unconvertible nonempty scalar text returns `#VALUE!`. An explicit missing argument contributes zero, unlike a referenced empty cell. Empty scalar text contributes zero. Original error metadata propagates.

```text
AVERAGEA({10;7;9;2;"not available"})
MINA({FALSE;0.2;0.5;0.4;0.8})
MAXA({0;0.2;0.5;0.4;TRUE})
```

With no included values, `AVERAGEA` returns `#DIV/0!` and the two extrema return zero. All accept 1 through 255 arguments. Direct nested arrays, callable payloads, unresolved references and nonfinite numeric values are rejected. The usual precision policy is applied to numeric inputs and the result; mean accumulation itself does not repeatedly round partial sums.

## Streaming and numerical behavior

Each source expression is evaluated once. Direct references use the caller's range enumerator, retaining provenance for single-cell references. Evaluated arrays are traversed directly without mutation or a converted copy. Extrema use scalar state. The mean reuses the fixed-workspace exact binary64 accumulator introduced for TRIMMEAN: represented finite inputs are summed exactly, and the quotient is rounded once using its remainder. It avoids intermediate sum overflow, destructive cancellation and loss of subnormal residuals. This does not restore decimal precision lost before evaluation or change SUM/AVERAGE behavior.

Work is O(n), workspace O(1), and there is no managed numerical workspace allocation, even for cold calls. Eager evaluator/reference materialization, text coercion, registry setup and user callbacks can allocate separately. `MaximumArrayCellCount` bounds visited scalar/physical array entries across arguments, including ignored entries and sparse holes. Range enumeration is bounded by the entries supplied by the host resolver. This is a per-call policy, not an Excel data-size limit or a whole-workbook memory/time quota.

## Validation and boundaries

Tests distinguish direct scalar arguments from arrays and direct cell/range references, omissions from empty cells, and empty text from absent values. They use an independent BigInteger rational-mean oracle across mixed arrays and extreme exponents, plus masks, error recovery, precision/culture, concurrent calls, zero-allocation checks and actual dirty-cell type-change recalculation. Explicit concurrency tests remain active inside the allocation-sensitive collection. The retained aggregate executable includes native smoke and reversed-order measurements against a converted-list reference on well-conditioned inputs, not an older library or Microsoft Excel.

Native Excel differential qualification is not claimed. In particular, complete named-expression reference provenance, undocumented missing/empty/error precedence and every locale conversion are not certified. The Microsoft support pages contain both a text-as-zero rule and a conflicting generic text-ignore sentence; the concrete array/reference contract above follows the A-family text-as-zero rule and is explicitly tested. Exact numerical summation can differ from native Excel's internal summation order on extreme cases.

Primary references: [AVERAGEA](https://support.microsoft.com/en-us/excel/functions/averagea-function), [MINA](https://support.microsoft.com/en-us/excel/functions/mina-function), [MAXA](https://support.microsoft.com/en-us/excel/functions/maxa-function).
