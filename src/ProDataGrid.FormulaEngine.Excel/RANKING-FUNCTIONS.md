# Ranking and percentile ranks

`RANK.EQ`, `RANK.AVG`, and legacy `RANK` share a bounded backend. `PERCENTRANK.INC`, `PERCENTRANK.EXC`, and legacy `PERCENTRANK` add interpolated percentage positions.

```text
RANK.EQ(A1:A100,B1:B10000)
RANK.AVG(A1:A100,B1:B10000,1)
PERCENTRANK.INC(B1:B10000,A1:A100,4)
PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},5.43)
```

Zero/omitted rank order means descending; **every finite nonzero order means ascending**, without integer truncation. EQ returns the first rank of a tie and AVG averages the occupied ranks. A target absent from the numeric reference returns `#N/A`, rather than the former implementation's invented insertion rank. Those two corrections intentionally change previous behavior.

Reference data ignores nonnumeric values and sparse holes, including logical/numeric-text reference values. Present errors retain their diagnostic metadata; nonfinite numeric inputs return `#NUM!`. Source expressions are evaluated once, and no source array or mask is changed. Numeric scalar reference inputs are accepted as one observation. Source text/Boolean values are ignored even when supplied as scalar values. Target/option values use existing numeric coercion and significant-digit settings.

Percentile ranks interpolate between neighboring observations. Exact duplicates use the first occupied position; interpolation above a duplicate run uses its last occupied position. Inclusive ordinals use the 0 through n-1 scale, exclusive ordinals use 1 through n. Targets outside the observed range return `#N/A`; an empty numeric sample returns `#NUM!`. The defined singleton results are 0 inclusive and 0.5 exclusive for the matching target.

Significance defaults to three decimal places. It is truncated to an integer and must be at least one. Up to 15 places use decimal ordinal arithmetic to avoid losing a digit solely because binary multiplication placed an exact rational boundary slightly below an integer. Larger values retain available binary64 precision. Interpolated fractions still have binary64 precision. Syntactically omitted significance (including forwarding through a lambda) is distinct from an explicitly supplied blank, whose numeric zero is invalid.

## Execution and ownership

A scalar query scans the reference once, with no list, copied numeric sample, or sorting. Multiple target/option positions use one private numeric snapshot, sort it once, and perform binary lower/upper-bound queries. Target and option arrays can broadcast singleton rows/columns; the reference defines one shared distribution, not independently broadcast models. One-cell array controls retain an array result. Outputs own their storage, and target-element errors remain element errors.

Small snapshots use stack storage; larger snapshots rent private numeric arrays cleared on all exits. No cross-call cache retains mutable source data. Subsequent source changes are seen on the next invocation, and host synchronization remains necessary if arrays can be mutated concurrently.

The existing `MaximumArrayCellCount` bounds physical reference positions and output cells before allocation. This is a host resource policy, not Excel's sample limit or a whole-workbook/time quota. Batch complexity is O(n log n + q log n), using O(n) workspace and O(q) output; scalar complexity is O(n) with O(1) workspace. Pool capacity can exceed the populated length and cold pool calls may allocate. Input materialization by the evaluator is outside these function-level bounds.

## Validation and measurement

Tests cover independent counting/rational oracles, both query paths, compiled/interpreted evaluation, formatting, ties, absent targets, omissions, precision, nonfinite and subnormal values, masks, mutation, concurrency, source evaluation count, allocation bounds and dirty spill recalculation. The existing aggregate benchmark/native-smoke executable compares 128 targets sharing 20,000 observations with an equivalent MAP formula, checking every result before measurement and a full checksum during it. It also retains a prior-style numeric list/scan reference for one scalar query. These are same-engine/algorithm comparisons, not native Excel or an old-release benchmark.

The implementations are not native-Excel differential certified. Singleton/all-equal distributions, exact duplicate/interpolation boundaries, extreme significance, error precedence, every reference-provenance interaction and runtime rounding edge remain explicit qualification areas. Character/date/text formatting, multivariate regression and other missing function families are separate work.

Primary references: [RANK.EQ](https://support.microsoft.com/en-us/excel/functions/rank-eq-function), [RANK.AVG](https://support.microsoft.com/en-us/excel/functions/rank-avg-function), [PERCENTRANK.INC](https://support.microsoft.com/en-us/excel/functions/percentrank-inc-function), [PERCENTRANK.EXC](https://support.microsoft.com/en-us/excel/functions/percentrank-exc-function).
