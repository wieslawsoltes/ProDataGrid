# Lexical formulas

`LET`, `LAMBDA` and `ISOMITTED` are implemented in the reusable engine, with matching compiled and interpreted paths, named definitions, dependency tracking, volatility analysis, formatting, fill translation and structural reference editing.

```text
LET(x,SUM(A1:A100),x+x)
LAMBDA(x,x*x)(5)
LET(offset,10,add,LAMBDA(x,x+offset),add(7))
LAMBDA(x,LAMBDA(y,x+y))(3)(4)
LAMBDA(x,y,IF(ISOMITTED(y),x,x+y))(2,)
```

`LET` supports up to 126 bindings. Each binding is evaluated once, in order, into a new immutable lexical frame; references do not repeat its calculation. Nested bindings shadow outer ones, and closures retain the bindings in force at their definition. Unused bindings are still evaluated: this implementation does not promise Excel's optimization of every unused expression or side effect.

`LAMBDA` supports zero through 253 parameters. Parameters must be distinct valid names, case-insensitively; periods and reference-like names are rejected. Arguments are evaluated once before binding. Explicitly empty argument positions are retained separately from blank cells, zero and empty text for `ISOMITTED`. Argument forwarding and local aliases preserve that omission flag. Ordinary functions continue to receive ordinary blank values for omitted arguments.

The parser preserves `FormulaLiteralExpression` for blank argument positions and adds `IsOmitted` metadata. Its existing constructor remains available. Returned-function calls use `FormulaInvocationExpression`; consumers with exhaustive AST visitors need to handle this additive expression kind. `FormulaValueKind.Lambda` is appended without renumbering previous kinds, and retains the compact tagged storage representation.

## Named and recursive functions

Store a parsed definition in the host's existing name provider, for example:

```text
Factorial := LAMBDA(n,IF(n<2,1,n*Factorial(n-1)))
Factorial(10)
```

Workbook/worksheet definitions are resolved outside the caller's unrelated local scope. Local callable bindings override registered function names; otherwise registered functions take precedence over defined names. Named function bodies participate in dependency and volatility analysis, including updates through `TrackNameChanges`. Analysis conservatively visits lambda bodies, even before an eventual invocation.

`FormulaCalculationEngine` constructs its dependency graph with the same registry as evaluation. Standalone consumers should use `new FormulaDependencyGraph(registry)` for exact registered-function precedence. The parameterless graph conservatively treats callable names as possible defined names. `IFormulaLexicalFunction` supplies optional binding metadata, so custom replacements are not incorrectly interpreted as declaration syntax. After mutating function registrations, hosts must refresh dependency/volatility analysis as well as recalculating; compiled plans already use the registry revision contract.

## Host APIs and limits

Use `context.CreateLambda(parameters, body)` and `evaluator.InvokeLambda(lambda, arguments, context, resolver)` to create/invoke closures without reparsing their bodies. Calls reuse compiled body plans. Bindings snapshot caller argument buffers; one- and two-parameter frames store values inline, while additional parameter values are copied. Captured arrays retain normal `FormulaArray` reference semantics; hosts must not mutate shared arrays concurrently.

The low-level evaluator can return a callable for composition. `FormulaValue.ToCellResult()` converts an uncalled lambda, or an array containing callable cells, to `#CALC!`. The workbook engine applies this at cell publication. Custom worksheet hosts should do the same; do not apply it to intermediate function arguments.

`workbook.Settings.MaximumLambdaDepth` defaults to 128 and accepts 1 through 256. Exceeding the recursive call limit returns `#NUM!`. This protects recursive lambda calls, not every parser/dependency traversal, elapsed execution time, or total workbook memory. The existing `MaximumArrayCellCount` also remains a per-result limit rather than a workbook quota.

## Qualification boundaries

Regression tests cover closures, recursion, shadowing, omissions, invalid arities, limits, reference edits, dependency invalidation and concurrent independent bindings. They are not a differential run against native Microsoft Excel. Locale-specific name rules, cross-workbook native formats, all reference-valued lambda semantics, and general tail-call/stackless evaluation are not certified. The existing eager/lazy custom-function contract remains host-synchronized.

Syntax references: [Microsoft LET](https://support.microsoft.com/en-us/excel/functions/let-function), [LAMBDA](https://support.microsoft.com/en-us/excel/functions/lambda-function), [ISOMITTED](https://support.microsoft.com/en-us/excel/functions/isomitted-function).
