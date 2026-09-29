from pathlib import Path

def edit(path, old, new):
    p = Path(path)
    text = p.read_text(encoding='utf-8')
    assert text.count(old) == 1, (path, text.count(old), old[:100])
    p.write_text(text.replace(old, new), encoding='utf-8')

def method(path, start, end, replacement):
    p = Path(path)
    text = p.read_text(encoding='utf-8')
    a, b = text.index(start), text.index(end, text.index(start) + len(start))
    p.write_text(text[:a] + replacement + text[b:], encoding='utf-8')

core = 'src/ProDataGrid.FormulaEngine/'
excel = 'src/ProDataGrid.FormulaEngine.Excel/'
edit(core+'FormulaExpressions.cs', '        StructuredReference\n', '        StructuredReference,\n        Invocation,\n        Missing\n')
edit(core+'FormulaValue.cs', '        Reference\n', '        Reference,\n        Lambda\n')
edit(core+'FormulaValue.cs', 'public readonly struct FormulaValue', 'public readonly partial struct FormulaValue')
edit(core+'FormulaValue.cs', '                FormulaValueKind.Array => ReferenceEquals(_payload, other._payload),', '                FormulaValueKind.Array => ReferenceEquals(_payload, other._payload),\n                FormulaValueKind.Lambda => ReferenceEquals(_payload, other._payload),')
edit(core+'FormulaValue.cs', '                    case FormulaValueKind.Array:\n', '                    case FormulaValueKind.Lambda:\n                    case FormulaValueKind.Array:\n')
edit(core+'FormulaValue.cs', '                FormulaValueKind.Reference => AsReference().ToString(),', '                FormulaValueKind.Reference => AsReference().ToString(),\n                FormulaValueKind.Lambda => "#CALC!",')
edit(core+'FormulaWorkbook.cs', 'public sealed class FormulaEvaluationContext', 'public sealed partial class FormulaEvaluationContext')

# Syntactic omissions stay distinguishable while ordinary invocation still yields Blank.
p = Path(excel+'ExcelFormulaParser.cs')
s = p.read_text(encoding='utf-8')
assert s.count('args.Add(new FormulaLiteralExpression(FormulaValue.Blank));') == 2
s = s.replace('args.Add(new FormulaLiteralExpression(FormulaValue.Blank));', 'args.Add(new FormulaMissingExpression());')
p.write_text(s, encoding='utf-8')
edit(excel+'ExcelFormulaParser.cs', '''                while (Peek().Type == FormulaTokenType.Operator && Peek().Text == "%")
                {
                    Next();
                    expression = new FormulaUnaryExpression(FormulaUnaryOperator.Percent, expression);
                }''', '''                while (true)
                {
                    if (Match(FormulaTokenType.OpenParen))
                    {
                        expression = new FormulaInvocationExpression(expression, ParseArguments());
                    }
                    else if (Peek().Type == FormulaTokenType.Operator && Peek().Text == "%")
                    {
                        Next();
                        expression = new FormulaUnaryExpression(FormulaUnaryOperator.Percent, expression);
                    }
                    else break;
                }''')
edit(excel+'ExcelFormulaFormatter.cs', '                case FormulaExpressionKind.Literal:\n', '''                case FormulaExpressionKind.Missing:
                    return string.Empty;
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    var target = FormatExpression(invocation.Target, options, 0, false);
                    var invocationText = new StringBuilder();
                    invocationText.Append('(').Append(target).Append(")(");
                    for (var i = 0; i < invocation.Arguments.Count; i++)
                    {
                        if (i > 0) invocationText.Append(options.ArgumentSeparator);
                        invocationText.Append(FormatExpression(invocation.Arguments[i], options, 0, false));
                    }
                    return invocationText.Append(')').ToString();
                case FormulaExpressionKind.Literal:
''')

# Compile invocation nodes and retain argument syntax for local function shadowing.
edit(core+'FormulaCompiledExpression.cs', '        ArrayLiteral\n', '        ArrayLiteral,\n        Invocation\n')
edit(core+'FormulaCompiledExpression.cs', '            FormulaExpression[]? lazyArguments = null)', '            FormulaExpression[]? lazyArguments = null,\n            FormulaExpression? expression = null)')
edit(core+'FormulaCompiledExpression.cs', '            LazyArguments = lazyArguments;', '            LazyArguments = lazyArguments;\n            Expression = expression;')
edit(core+'FormulaCompiledExpression.cs', '        public FormulaExpression[]? LazyArguments { get; }', '        public FormulaExpression[]? LazyArguments { get; }\n\n        public FormulaExpression? Expression { get; }')
edit(core+'FormulaCompiledExpression.cs', '                                name: call.Name, argCount: call.Arguments.Count),', '                                name: call.Name, argCount: call.Arguments.Count, lazyArguments: new List<FormulaExpression>(call.Arguments).ToArray()),')
edit(core+'FormulaCompiledExpression.cs', '                    case FormulaExpressionKind.Literal:\n', '''                    case FormulaExpressionKind.Invocation:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Invocation, expression: current), push: 1);
                        break;
                    case FormulaExpressionKind.Missing:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal, literal: FormulaValue.Blank), push: 1);
                        break;
                    case FormulaExpressionKind.Literal:
''')
edit(core+'FormulaCompiledExpression.cs', 'if (_functionRegistry.TryGetFunction(call.Name, out var function) && function is ILazyFormulaFunction)', 'if (!_functionRegistry.TryGetFunction(call.Name, out var function) || function is ILazyFormulaFunction)')

edit(core+'FormulaEvaluation.cs', '                    case FormulaExpressionKind.Binary:\n', '''                    case FormulaExpressionKind.Invocation:
                        var invocation = (FormulaInvocationExpression)current;
                        stack.Push(invocation.Target);
                        foreach (var argument in invocation.Arguments) stack.Push(argument);
                        break;
                    case FormulaExpressionKind.Binary:
''')
edit(core+'FormulaEvaluation.cs', '                        case FormulaInstructionKind.Literal:\n', '''                        case FormulaInstructionKind.Invocation:
                            stack[sp++] = EvaluateInvocation((FormulaInvocationExpression)instruction.Expression!, context, resolver);
                            break;
                        case FormulaInstructionKind.Literal:
''')
edit(core+'FormulaEvaluation.cs', 'InvokeFunction(instruction.Name ?? string.Empty, args, context)', 'InvokeFunction(instruction.Name ?? string.Empty, args, context, resolver, instruction.LazyArguments)')
edit(core+'FormulaEvaluation.cs', '                case FormulaExpressionKind.Literal:\n', '''                case FormulaExpressionKind.Invocation:
                    return EvaluateInvocation((FormulaInvocationExpression)expression, context, resolver);
                case FormulaExpressionKind.Missing:
                    return FormulaValue.Blank;
                case FormulaExpressionKind.Literal:
''')
edit(core+'FormulaEvaluation.cs', '            if (resolver.TryResolveName(context, name, out var value))', '            if (context.LocalScope != null && context.LocalScope.TryGet(name, out var local, out _)) return local;\n            if (resolver.TryResolveName(context, name, out var value))')
method(core+'FormulaEvaluation.cs', '        private FormulaValue EvaluateFunctionCall(', '        private static bool ValidateArguments(', '''        private FormulaValue EvaluateFunctionCall(
            FormulaFunctionCallExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
            => InvokeLazyFunction(expression.Name, expression.Arguments, context, resolver);

        private FormulaValue InvokeFunction(string name, IReadOnlyList<FormulaValue> args,
            FormulaEvaluationContext context, IFormulaValueResolver resolver,
            IReadOnlyList<FormulaExpression>? expressions = null)
        {
            if (context.LocalScope != null && context.LocalScope.TryGet(name, out var local, out _))
            {
                if (local.Kind == FormulaValueKind.Error) return local;
                return local.Kind == FormulaValueKind.Lambda
                    ? InvokeLambda(local.AsLambda(), args, context, resolver, GetOmissions(expressions, context))
                    : FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }
            if (!context.FunctionRegistry.TryGetFunction(name, out var function))
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            if (!ValidateArguments(function.Info, args.Count))
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }

        private FormulaValue InvokeLazyFunction(string name, IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluationContext context, IFormulaValueResolver resolver)
        {
            if (context.LocalScope != null && context.LocalScope.TryGet(name, out var local, out _))
                return InvokeLambdaExpressions(local, arguments, context, resolver);
            if (!context.FunctionRegistry.TryGetFunction(name, out var function))
            {
                return resolver.TryResolveName(context, name, out var named)
                    ? InvokeLambdaExpressions(named, arguments, context, resolver)
                    : FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            }
            if (!ValidateArguments(function.Info, arguments.Count))
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            if (function is ILazyFormulaFunction lazy)
                return lazy.InvokeLazy(new FormulaFunctionContext(context), arguments, this, resolver);
            var values = arguments.Count == 0 ? Array.Empty<FormulaValue>() : new FormulaValue[arguments.Count];
            for (var i = 0; i < arguments.Count; i++) values[i] = EvaluateCore(arguments[i], context, resolver);
            return InvokeFunction(name, values, context, resolver, arguments);
        }

''')
edit(core+'FormulaCalculationEngine.cs', 'var value = _evaluator.Evaluate(cell.Expression, context, resolver);', 'var value = _evaluator.Evaluate(cell.Expression, context, resolver).ToCellResult();')
edit(core+'WorkbookValueResolver.cs', 'value = _evaluator.Evaluate(expression, context, this);', 'value = _evaluator.Evaluate(expression, context.WithoutLocals(), this);')
edit(core+'WorkbookValueResolver.cs', 'var value = _evaluator.Evaluate(cell.Expression, cellContext, this);', 'var value = _evaluator.Evaluate(cell.Expression, cellContext, this).ToCellResult();')

# Scope-aware dependencies: traverse named callable bodies, but not parameter declarations.
method(core+'FormulaDependencyGraph.cs', '        private static void CollectReferences(', '        private static bool TryExpandReference(', '''        private static void CollectReferences(
            FormulaExpression expression, List<FormulaReference> references,
            List<FormulaStructuredReference> structuredReferences, HashSet<string> nameDependencies,
            IFormulaNameProvider? worksheetNames, IFormulaNameProvider? workbookNames,
            string? sheetName, HashSet<string> nameStack, HashSet<string>? locals = null)
        {
            void Visit(FormulaExpression item, HashSet<string>? scope)
                => CollectReferences(item, references, structuredReferences, nameDependencies,
                    worksheetNames, workbookNames, sheetName, nameStack, scope);
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Reference:
                    references.Add(((FormulaReferenceExpression)expression).Reference);
                    return;
                case FormulaExpressionKind.StructuredReference:
                    structuredReferences.Add(((FormulaStructuredReferenceExpression)expression).Reference);
                    return;
                case FormulaExpressionKind.Unary:
                    Visit(((FormulaUnaryExpression)expression).Operand, locals);
                    return;
                case FormulaExpressionKind.Binary:
                    var binary = (FormulaBinaryExpression)expression;
                    Visit(binary.Left, locals);
                    Visit(binary.Right, locals);
                    return;
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    Visit(invocation.Target, locals);
                    foreach (var arg in invocation.Arguments) Visit(arg, locals);
                    return;
                case FormulaExpressionKind.FunctionCall:
                    var call = (FormulaFunctionCallExpression)expression;
                    // Also register unresolved callable names so later definitions invalidate dependencies.
                    Visit(new FormulaNameExpression(call.Name), locals);
                    FormulaBindingTraversal.VisitArguments(call, locals, (arg, scope) => { Visit(arg, scope); return false; });
                    return;
                case FormulaExpressionKind.Name:
                    var name = ((FormulaNameExpression)expression).Name;
                    if (locals != null && locals.Contains(name)) return;
                    if (!TryGetNameExpression(worksheetNames, workbookNames, name, sheetName, out var resolved, out var scopeKey))
                    {
                        RegisterUnresolvedName(nameDependencies, name, sheetName, worksheetNames, workbookNames);
                        return;
                    }
                    nameDependencies.Add(scopeKey);
                    if (!nameStack.Add(scopeKey)) return;
                    try { Visit(resolved, null); }
                    finally { nameStack.Remove(scopeKey); }
                    return;
                case FormulaExpressionKind.ArrayLiteral:
                    var array = (FormulaArrayExpression)expression;
                    for (var row = 0; row < array.RowCount; row++)
                        for (var column = 0; column < array.ColumnCount; column++) Visit(array[row, column], locals);
                    return;
            }
        }

''')
method(core+'FormulaCalculationEngine.cs', '        private bool IsVolatileExpression(', '        private static string CreateNameScopeKey(', '''        private bool IsVolatileExpression(FormulaExpression expression, IFormulaWorkbook workbook,
            IFormulaNameProvider? worksheetNames, IFormulaNameProvider? workbookNames,
            string? sheetName, HashSet<string> nameStack, HashSet<string>? locals = null)
        {
            bool Visit(FormulaExpression item, HashSet<string>? scope)
                => IsVolatileExpression(item, workbook, worksheetNames, workbookNames, sheetName, nameStack, scope);
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Unary:
                    return Visit(((FormulaUnaryExpression)expression).Operand, locals);
                case FormulaExpressionKind.Binary:
                    var binary = (FormulaBinaryExpression)expression;
                    return Visit(binary.Left, locals) || Visit(binary.Right, locals);
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    if (Visit(invocation.Target, locals)) return true;
                    foreach (var argument in invocation.Arguments) if (Visit(argument, locals)) return true;
                    return false;
                case FormulaExpressionKind.ArrayLiteral:
                    var array = (FormulaArrayExpression)expression;
                    for (var row = 0; row < array.RowCount; row++)
                        for (var column = 0; column < array.ColumnCount; column++)
                            if (Visit(array[row, column], locals)) return true;
                    return false;
                case FormulaExpressionKind.FunctionCall:
                    var call = (FormulaFunctionCallExpression)expression;
                    if (locals == null || !locals.Contains(call.Name))
                    {
                        if (_functionRegistry.TryGetFunction(call.Name, out var function))
                        {
                            if (function.Info.IsVolatile) return true;
                        }
                        else if (Visit(new FormulaNameExpression(call.Name), locals)) return true;
                    }
                    return FormulaBindingTraversal.VisitArguments(call, locals, Visit);
                case FormulaExpressionKind.Name:
                    var name = ((FormulaNameExpression)expression).Name;
                    if (locals != null && locals.Contains(name)) return false;
                    var scopeKey = CreateNameScopeKey(sheetName, name);
                    if (!nameStack.Add(scopeKey)) return false;
                    try
                    {
                        if (worksheetNames != null && worksheetNames.TryGetName(name, out var sheetExpression))
                            return Visit(sheetExpression, null);
                        if (workbookNames != null && workbookNames.TryGetName(name, out var workbookExpression))
                            return Visit(workbookExpression, null);
                    }
                    finally { nameStack.Remove(scopeKey); }
                    return false;
                default:
                    return false;
            }
        }

''')

# Copy/fill and structural edits must rewrite both the target and arguments of invocations.
edit(core+'FormulaFillTranslation.cs', '                case FormulaExpressionKind.Reference:\n', '''                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    var target = Rewrite(invocation.Target, rowOffset, columnOffset);
                    var args = new FormulaExpression[invocation.Arguments.Count];
                    var changed = !ReferenceEquals(target, invocation.Target);
                    for (var i = 0; i < args.Length; i++)
                    {
                        args[i] = Rewrite(invocation.Arguments[i], rowOffset, columnOffset);
                        changed |= !ReferenceEquals(args[i], invocation.Arguments[i]);
                    }
                    return changed ? new FormulaInvocationExpression(target, args) : expression;
                case FormulaExpressionKind.Reference:
''')
edit(core+'FormulaReferenceUpdater.cs', '                    case FormulaExpressionKind.Reference:\n', '''                    case FormulaExpressionKind.Invocation:
                        var invocation = (FormulaInvocationExpression)expression;
                        var target = Rewrite(invocation.Target);
                        var args = new FormulaExpression[invocation.Arguments.Count];
                        var changed = !ReferenceEquals(target, invocation.Target);
                        for (var i = 0; i < args.Length; i++)
                        {
                            args[i] = Rewrite(invocation.Arguments[i]);
                            changed |= !ReferenceEquals(args[i], invocation.Arguments[i]);
                        }
                        if (!changed) return expression;
                        _changed = true;
                        return new FormulaInvocationExpression(target, args);
                    case FormulaExpressionKind.Reference:
''')
method(core+'FormulaReferenceUpdater.cs', '            private FormulaExpression RewriteFunctionCall(', '            private FormulaExpression RewriteArrayLiteral(', '''            private FormulaExpression RewriteFunctionCall(FormulaFunctionCallExpression expression)
            {
                FormulaExpression[]? updated = null;
                for (var i = 0; i < expression.Arguments.Count; i++)
                {
                    var argument = expression.Arguments[i];
                    var next = Rewrite(argument);
                    if (updated == null && !ReferenceEquals(next, argument))
                    {
                        updated = new FormulaExpression[expression.Arguments.Count];
                        for (var j = 0; j < i; j++) updated[j] = expression.Arguments[j];
                    }
                    if (updated != null) updated[i] = next;
                }
                if (updated == null) return expression;
                _changed = true;
                return new FormulaFunctionCallExpression(expression.Name, updated);
            }

''')
