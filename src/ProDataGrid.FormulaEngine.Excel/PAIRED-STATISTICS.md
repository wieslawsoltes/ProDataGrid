# Covariance, correlation and linear regression

The registry includes `COVARIANCE.P`, `COVARIANCE.S`, `COVAR`, `CORREL`, `PEARSON`, `RSQ`, `SLOPE`, `INTERCEPT`, `STEYX`, `FORECAST.LINEAR` and `FORECAST`.

Regression arguments are dependent observations first, independent observations second. Forecast takes its target x value or target array first. Covariance and correlation are symmetric in their two inputs.

```text
CORREL(A1:A100,B1:B100)
COVARIANCE.S(A1:A100,B1:B100)
SLOPE(B1:B100,A1:A100)
STEYX(B1:B100,A1:A100)
FORECAST.LINEAR({101;102;103},B1:B100,A1:A100)
```

## Pairing and error contracts

Inputs must have equal physical cell counts; equal-length row/column vectors may have different orientations. Traversal pairs positions in row-major order. An array/reference pair is ignored when either value is blank, text or Boolean. Numeric zeros remain observations. Filtering never compresses the axes independently. Present formula errors propagate with their original diagnostic metadata even opposite an ignored value; masked-out cells behave as blanks.

Direct scalar numeric text/Boolean arguments use the existing number coercion policy. The lazy interface retains direct cell/structured-reference provenance so a Boolean cell is not counted as a directly typed scalar. Arbitrary named expressions and custom functions do not supply complete provenance metadata; those interactions remain a qualification boundary. Source expressions are evaluated once. Direct eager invocation receives evaluated values without syntactic provenance.

Unequal lengths return `#N/A`. Empty paired samples return `#DIV/0!` for CORREL, covariance and STEYX, and `#N/A` for PEARSON, RSQ, SLOPE, INTERCEPT and forecast. Sample covariance needs two observations and STEYX needs three. Correlation/RSQ need variation on both axes; regression needs variation in known x. Undefined results return `#DIV/0!`; nonfinite inputs and unrepresentable outputs return `#NUM!`. These documented contracts are not a claim that every undocumented native error-precedence case is qualified.

Forecast supports a rectangular target array and returns an independently owned array of the same shape. The training model is fit once for the entire array, not once per target. Target-element errors remain element errors. Known arrays are not broadcast into multiple independent models. No model is cached across calls, so changing input values changes subsequent results.

## Numerical implementation

Each paired observation is copied once into private numeric workspaces. Axes use separate exact power-of-two scaling, split origin/mean offsets, compensated recentering and compensated cross products. Correlation never materializes an overflowing covariance or variance. Forecast retains a normalized slope, so a finite prediction need not require a representable standalone slope. A scaled-product fallback handles extreme extrapolation without prematurely overflowing normalized x. STEYX sums centered residuals directly instead of subtracting nearly equal squared moments; residual scaling avoids an unnecessary squared-residual overflow/underflow.

Existing SIMD moment kernels and a compensated SIMD dot product have scalar fallbacks. Small workspaces use stack storage; larger workspaces rent private double arrays and clear them in finally. Inputs, source masks and previously returned arrays are never modified.

This is binary64 arithmetic, not arbitrary precision. Extremely wide within-axis dynamic ranges can lose tiny contributions during normalization. Nearly degenerate fits and very distant extrapolation remain sensitive to rounding. There is no condition-number estimate, multivariate LINEST/TREND model, confidence interval or native Excel differential certification in this family.

## Limits and measurement

`MaximumArrayCellCount` bounds each training array's physical positions before workspace allocation and bounds forecast output cells. Two numeric workspaces are used, approximately 16 bytes per input pair before pool-capacity overhead. This policy also counts nonnumeric/masked positions and is not an Excel sample-size restriction, peak-memory guarantee, time limit or whole-workbook quota. Input materialization by the caller/evaluator may already have occurred.

The existing `tools/formula-aggregate-benchmarks` executable checks these functions in compiled/interpreted and precision-enabled/disabled modes, including NativeAOT smoke through maintained formula CI. It compares one array forecast with an equivalent MAP formula over the same 2,048 training observations and 128 targets. Both return the same owned numerical result, checked elementwise before measurement and by a full-result checksum during measurement. Input construction/parsing is excluded; fitting, snapshots, output allocation and buffer clearing are included. Comparison order reverses across processes. Scalar direct SLOPE is separately measured. These are alternative formulas on one engine, not old/new engine revisions or Microsoft Excel performance.

## Primary references

- [COVARIANCE.P](https://support.microsoft.com/en-us/excel/functions/covariance-p-function) and [COVARIANCE.S](https://support.microsoft.com/en-us/excel/functions/covariance-s-function)
- [CORREL](https://support.microsoft.com/en-us/excel/functions/correl-function), [PEARSON](https://support.microsoft.com/en-us/excel/functions/pearson-function) and [RSQ](https://support.microsoft.com/en-us/excel/functions/rsq-function)
- [SLOPE](https://support.microsoft.com/en-us/excel/functions/slope-function), [INTERCEPT](https://support.microsoft.com/en-us/excel/functions/intercept-function) and [STEYX](https://support.microsoft.com/en-us/excel/functions/steyx-function)
- [FORECAST/FORECAST.LINEAR](https://support.microsoft.com/en-us/excel/functions/forecast-and-forecast-linear-functions)
