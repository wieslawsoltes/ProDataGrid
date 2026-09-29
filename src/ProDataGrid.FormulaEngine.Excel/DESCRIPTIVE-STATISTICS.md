# Descriptive statistics

The Excel registry supports `DEVSQ`, `AVEDEV`, `SKEW`, `SKEW.P`, `KURT`, `GEOMEAN`, `HARMEAN` and `SUMSQ`, as well as `VARA`, `VARPA`, `STDEVA` and `STDEVPA`. Legacy `VAR`, `VARP`, `STDEV` and `STDEVP` use the same backend as `VAR.S`, `VAR.P`, `STDEV.S` and `STDEV.P`; no obsolete implementation is shadowed by duplicate registrations.

## Values, references and errors

Scalar numeric text and logical arguments follow numeric coercion. For ordinary variants, text and logical values in arrays or direct worksheet references are ignored. The A variants count logical values as one/zero and reference/array text, including empty or numeric text, as zero. Blank cells and absent sparse positions are ignored. Input errors retain their type and diagnostic message. Unresolved references, nested arrays and callable scalar values are not numeric observations.

Direct reference arguments use the caller's range enumerator when available, so a single-cell text/logical reference has the same reference-origin semantics as a range. Other argument expressions are evaluated once. Named definitions and arbitrary custom expressions that resolve to scalar values do not carry full Excel reference provenance; native qualification of those interactions remains separate.

`VAR.S`/`STDEV.S` require two numbers, population variants require one, both skewness functions require three, and `KURT` requires four. Constant samples have undefined skewness/kurtosis (`#DIV/0!`). Geometric and harmonic means require strictly positive numbers (`#NUM!` for zero/negative input). An empty `SUMSQ` sample returns zero; other empty samples return `#DIV/0!` under this implementation's explicit contract. Nonfinite inputs and unrepresentable outputs return `#NUM!` rather than host exceptions or infinite results.

## Numerical method and ownership

Each invocation collects one private numeric snapshot. Up to 128 values use stack storage; larger snapshots rent numeric arrays. Populated rented regions are cleared before returning them, including error paths. Input values, masks and host arrays are never reordered or changed. Captured/custom source expressions are not reevaluated for each numerical pass.

Centered statistics use power-of-two normalization, a separate first-value origin and compensated mean offsets. A residual recentering pass avoids forming an imprecise large absolute mean. Moments are accumulated with compensated summation. Large samples use portable SIMD lanes with compensation in each lane and a compensated lane reduction; small inputs and runtimes without hardware acceleration retain the scalar path. Skewness and excess sample kurtosis use standardized deviations; population skewness uses its population denominator.

Standard deviation is evaluated in the scaled domain and rescaled directly, **not** as the square root of a materialized variance. Consequently `STDEV.P(1E308,-1E308)` can return a finite result even though the corresponding variance cannot be represented. Tiny finite standard deviations are likewise preserved when their variance underflows. Geometric means use logarithms; harmonic means use ratios to the smallest sample value instead of overflowing reciprocals. Both positive means are clamped to their mathematically valid sample interval only after final rounding.

`MaximumArrayCellCount` bounds accepted numeric observations before adding another to the snapshot. This extends the existing host policy to these statistics, including the pre-existing variance/deviation functions. It is not an Excel sample-size limit, global memory quota, iteration/time limit or peak-memory guarantee. Pool capacity may exceed populated length. Ignored text/blank inputs and source evaluation still cost work; hosts must keep sources stable during evaluation. Normal engine input and final-result significant-digit settings apply.

## Verification and reproduction

Tests exercise compiled/interpreted and formatter paths, aliases, logical/text/reference distinctions, sample limits, extreme/subnormal values, captured-source evaluation counts, dirty recalculation, immutable inputs and concurrent independent calls. Seeded central moments are compared with exact integer power sums before transformation by large offsets and powers of two. These are not differential executions against native Microsoft Excel or an arbitrary-precision implementation.

The retained `tools/formula-aggregate-benchmarks` executable includes parser/native smoke checks and a matched 100,000-value comparison with the prior-style list-based mean/squared-deviation algorithm. It verifies matching results before timing, reverses comparison order between processes, and retains all seven measured samples. Inputs are precreated; collection, numerical passes and clearing are included. Zero warm managed allocation is not zero workspace or zero cold allocation, and narrower allocation does not guarantee lower latency for every sample size.

Primary syntax references: [VAR.S](https://support.microsoft.com/en-us/excel/functions/var-s-function), [STDEV.S](https://support.microsoft.com/en-us/excel/functions/stdev-s-function), [DEVSQ](https://support.microsoft.com/en-us/excel/functions/devsq-function), [AVEDEV](https://support.microsoft.com/en-us/excel/functions/avedev-function), [SKEW](https://support.microsoft.com/en-us/excel/functions/skew-function), [SKEW.P](https://support.microsoft.com/en-us/excel/functions/skew-p-function), [KURT](https://support.microsoft.com/en-us/excel/functions/kurt-function), [GEOMEAN](https://support.microsoft.com/en-us/excel/functions/geomean-function), [HARMEAN](https://support.microsoft.com/en-us/excel/functions/harmean-function), [SUMSQ](https://support.microsoft.com/en-us/excel/functions/sumsq-function).
