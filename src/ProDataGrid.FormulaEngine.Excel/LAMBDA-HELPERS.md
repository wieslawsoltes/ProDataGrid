# Higher-order array functions

The registry includes `MAP`, `REDUCE`, `SCAN`, `BYROW`, `BYCOL` and `MAKEARRAY`, building on [LET, LAMBDA and ISOMITTED](LEXICAL-FUNCTIONS.md). Helpers use the caller's evaluator, resolver and lexical context. Source and callback expressions are evaluated once; callback bodies reuse compiled plans without parsing or AST substitution in the element loop.

| Function | Callback and result |
| --- | --- |
| `MAP(array1, [array2, ...], lambda)` | One parameter per input array, matching input dimensions, one output value per position. Scalars are one-cell inputs. |
| `REDUCE([initial], array, lambda)` | Two parameters, accumulator and element, visited row-major. Returns the final accumulator, which can be an array or closure. An omitted seed takes the first input element and starts callbacks at the second. |
| `SCAN([initial], array, lambda)` | Two parameters as above, but returns each intermediate value in the input shape. An omitted seed starts at numeric zero. |
| `BYROW(array, lambda)` / `BYCOL(array, lambda)` | One row/column array per call, producing an output column/row. Each slice has independent storage. |
| `MAKEARRAY(rows, columns, lambda)` | Two one-based parameters, row and column. Positive dimensions and output size are checked before element callbacks. |

```text
LET(offset,10,MAP(A1:A20,LAMBDA(x,x+offset)))
REDUCE(1,A1:A20,LAMBDA(total,item,total*item))
SCAN(0,A1:A20,LAMBDA(total,item,total+item))
BYROW(A1:D20,LAMBDA(row,SUM(row)))
MAKEARRAY(3,4,LAMBDA(row,col,row*col))
```

Scalar errors remain input values so callbacks can handle them with `IFERROR`. Blank cells and masked-out positions are passed as blanks, not omitted parameters. Blank scalar callback results materialize as zero. A one-cell array result is unwrapped; larger directly nested arrays return `#CALC!`. Wrong callback arity or a non-callable callback returns `#VALUE!`. Matching MAP input dimensions are required; this implementation does not silently broadcast inputs.

## Closures and deferred values

A callable is a scalar intermediate payload. It can wrap an array or retain a calculation for later invocation, avoiding directly nested array results:

```text
LET(items,MAP({1;2;3},LAMBDA(x,LAMBDA(x*x))),
    SUM(MAP(items,LAMBDA(f,f()))))
LET(items,BYROW({1,2;3,4},LAMBDA(row,LAMBDA(SUM(row)))),
    MAP(items,LAMBDA(f,f())))
```

Captured parameter frames are immutable snapshots even though the iteration argument buffer is reused. BYROW/BYCOL allocate a separate slice per callback because custom functions or returned closures may retain it. Neither slices nor lexical frames are pooled or overwritten between calls. Nested user arrays retain the usual shared `FormulaArray` reference semantics, not deep-copy semantics.

The low-level evaluator may return callable arrays for composition; `ToCellResult()` and the built-in workbook engine reject uncalled closures at worksheet publication with `#CALC!`. An array of closures cannot spill as worksheet cell values.

## Resource and compatibility contracts

The existing `MaximumArrayCellCount` bounds helper output arrays and BYROW/BYCOL slices before allocation. It is not a total iteration, callback, retained-closure or workbook memory quota. REDUCE can carry an array accumulator; arbitrary host functions and previously allocated input arrays are not covered by a global memory limit. `MaximumLambdaDepth` bounds recursive/nested invocation, not the number of sequential iterations.

The eager-only `IFormulaFunction.Invoke` interface cannot supply the caller's evaluator/resolver to these helpers. Invoke them through `FormulaEvaluator`, or through `ILazyFormulaFunction.InvokeLazy` with the appropriate expression arguments and context. Do not replace the resolver implicitly in host adapters.

Regression coverage exercises both evaluation modes, formatter round trips, omissions, errors, seeded independent-loop comparisons, dependency/spill recalculation, retained contexts/slices, callable-array composition, allocation rejection and body-plan reuse. This is not native Microsoft Excel differential qualification. Locale-specific coercion, every shape/error interaction and native-format interchange remain separate qualification work.

References: [MAP](https://support.microsoft.com/en-us/excel/functions/map-function), [REDUCE](https://support.microsoft.com/en-us/excel/functions/reduce-function), [SCAN](https://support.microsoft.com/en-us/excel/functions/scan-function), [BYROW](https://support.microsoft.com/en-us/excel/functions/byrow-function), [BYCOL](https://support.microsoft.com/en-us/excel/functions/bycol-function), [MAKEARRAY](https://support.microsoft.com/en-us/excel/functions/makearray-function).
