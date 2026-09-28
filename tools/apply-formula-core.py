from pathlib import Path

def edit(path, old, new):
    file = Path(path)
    text = file.read_text(encoding='utf-8')
    assert text.count(old) == 1, (path, text.count(old), old[:100])
    file.write_text(text.replace(old, new), encoding='utf-8')

edit('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs',
     'public sealed class ExcelFunctionRegistry : IFormulaFunctionRegistry',
     'public sealed partial class ExcelFunctionRegistry : IFormulaFunctionRegistry, IFormulaFunctionRegistryVersion')
edit('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs',
     '            RegisterDefaults();',
     '            RegisterDefaults();\n            RegisterExtendedDefaults();')
edit('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs',
     '            _functions[function.Name] = function;',
     '            _functions[function.Name] = function;\n            unchecked { Version++; }')

edit('src/ProDataGrid.FormulaEngine/FormulaCompiledExpression.cs',
     '            MaxStackDepth = maxStackDepth;',
     '            MaxStackDepth = maxStackDepth;\n            FunctionRegistryVersion = (functionRegistry as IFormulaFunctionRegistryVersion)?.Version;')
edit('src/ProDataGrid.FormulaEngine/FormulaCompiledExpression.cs',
     '        public int MaxStackDepth { get; }',
     '''        public int MaxStackDepth { get; }

        private long? FunctionRegistryVersion { get; }

        public bool IsCompatibleWith(IFormulaFunctionRegistry registry)
        {
            return ReferenceEquals(FunctionRegistry, registry) &&
                FunctionRegistryVersion == (registry as IFormulaFunctionRegistryVersion)?.Version;
        }''')
file = Path('src/ProDataGrid.FormulaEngine/FormulaCompiledExpression.cs')
text = file.read_text(encoding='utf-8')
start = text.index('        private void CompileExpression(')
end = text.index('        private void Emit(', start)
text = text[:start] + '''        private void CompileExpression(FormulaExpression expression)
        {
            // Heap-backed frames avoid overflowing the CLR stack on long generated formulas.
            // Children are pushed in reverse so evaluation remains strictly left-to-right.
            var pending = new Stack<(FormulaExpression Expression, bool Expanded)>();
            pending.Push((expression, false));
            while (pending.Count > 0)
            {
                var frame = pending.Pop();
                var current = frame.Expression;
                if (frame.Expanded)
                {
                    switch (current.Kind)
                    {
                        case FormulaExpressionKind.Unary:
                            Emit(new FormulaInstruction(FormulaInstructionKind.Unary,
                                unaryOperator: ((FormulaUnaryExpression)current).Operator), pop: 1, push: 1);
                            break;
                        case FormulaExpressionKind.Binary:
                            Emit(new FormulaInstruction(FormulaInstructionKind.Binary,
                                binaryOperator: ((FormulaBinaryExpression)current).Operator), pop: 2, push: 1);
                            break;
                        case FormulaExpressionKind.FunctionCall:
                            var call = (FormulaFunctionCallExpression)current;
                            Emit(new FormulaInstruction(FormulaInstructionKind.FunctionCall,
                                name: call.Name, argCount: call.Arguments.Count), pop: call.Arguments.Count, push: 1);
                            break;
                        case FormulaExpressionKind.ArrayLiteral:
                            var array = (FormulaArrayExpression)current;
                            Emit(new FormulaInstruction(FormulaInstructionKind.ArrayLiteral,
                                rowCount: array.RowCount, columnCount: array.ColumnCount),
                                pop: checked(array.RowCount * array.ColumnCount), push: 1);
                            break;
                    }
                    continue;
                }

                switch (current.Kind)
                {
                    case FormulaExpressionKind.Literal:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal,
                            literal: ((FormulaLiteralExpression)current).Value), push: 1);
                        break;
                    case FormulaExpressionKind.Name:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Name,
                            name: ((FormulaNameExpression)current).Name), push: 1);
                        break;
                    case FormulaExpressionKind.Reference:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Reference,
                            reference: ((FormulaReferenceExpression)current).Reference), push: 1);
                        break;
                    case FormulaExpressionKind.StructuredReference:
                        Emit(new FormulaInstruction(FormulaInstructionKind.StructuredReference,
                            structuredReference: ((FormulaStructuredReferenceExpression)current).Reference), push: 1);
                        break;
                    case FormulaExpressionKind.Unary:
                        pending.Push((current, true));
                        pending.Push((((FormulaUnaryExpression)current).Operand, false));
                        break;
                    case FormulaExpressionKind.Binary:
                        var binary = (FormulaBinaryExpression)current;
                        pending.Push((current, true));
                        pending.Push((binary.Right, false));
                        pending.Push((binary.Left, false));
                        break;
                    case FormulaExpressionKind.FunctionCall:
                        var call = (FormulaFunctionCallExpression)current;
                        if (_functionRegistry.TryGetFunction(call.Name, out var function) && function is ILazyFormulaFunction)
                        {
                            var arguments = new FormulaExpression[call.Arguments.Count];
                            for (var i = 0; i < arguments.Length; i++)
                            {
                                arguments[i] = call.Arguments[i];
                            }
                            Emit(new FormulaInstruction(FormulaInstructionKind.LazyFunctionCall,
                                name: call.Name, lazyArguments: arguments), push: 1);
                        }
                        else
                        {
                            pending.Push((current, true));
                            for (var i = call.Arguments.Count - 1; i >= 0; i--)
                            {
                                pending.Push((call.Arguments[i], false));
                            }
                        }
                        break;
                    case FormulaExpressionKind.ArrayLiteral:
                        var array = (FormulaArrayExpression)current;
                        pending.Push((current, true));
                        for (var row = array.RowCount - 1; row >= 0; row--)
                        {
                            for (var column = array.ColumnCount - 1; column >= 0; column--)
                            {
                                pending.Push((array[row, column], false));
                            }
                        }
                        break;
                    default:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal,
                            literal: FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc))), push: 1);
                        break;
                }
            }
        }

''' + text[end:]
file.write_text(text, encoding='utf-8')

edit('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs',
     'using System;\n', 'using System;\nusing System.Buffers;\n')
edit('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs',
     '''            if (context.Workbook.Settings.EnableCompiledExpressions && !ContainsReferenceOperators(expression))
            {
                var compiled = GetCompiledExpression(expression, context.FunctionRegistry, observer);
                return EvaluateCompiled(compiled, context, resolver);
            }''',
     '''            if (context.Workbook.Settings.EnableCompiledExpressions)
            {
                // The cached plan already passed the reference-operator eligibility check.
                // Do not walk the AST (or allocate a traversal stack) on every cache hit.
                if (_compiledCache.TryGetValue(expression, out var cached) && cached.IsCompatibleWith(context.FunctionRegistry))
                {
                    observer?.OnExpressionCompiled(expression, cached.Instructions.Length, TimeSpan.Zero, fromCache: true);
                    return EvaluateCompiled(cached, context, resolver);
                }

                if (!ContainsReferenceOperators(expression))
                {
                    var compiled = GetCompiledExpression(expression, context.FunctionRegistry, observer);
                    return EvaluateCompiled(compiled, context, resolver);
                }
            }''')
edit('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs',
     'if (ReferenceEquals(compiled.FunctionRegistry, functionRegistry))',
     'if (compiled.IsCompatibleWith(functionRegistry))')
file = Path('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs')
text = file.read_text(encoding='utf-8')
start = text.index('        private FormulaValue EvaluateCompiled(')
end = text.index('        private FormulaValue EvaluateCore(', start)
block = text[start:end]
assert block.count('? new FormulaValue[compiled.MaxStackDepth]') == 1
block = block.replace('? new FormulaValue[compiled.MaxStackDepth]', '? ArrayPool<FormulaValue>.Shared.Rent(compiled.MaxStackDepth)')
block = block.replace('var args = new FormulaValue[instruction.ArgCount];',
                      'var args = instruction.ArgCount == 0 ? Array.Empty<FormulaValue>() : new FormulaValue[instruction.ArgCount];')
body_start = block.index('            var sp = 0;')
assert block.endswith('        }\n\n')
body = block[body_start:-len('        }\n\n')]
body = ''.join(('    ' + line if line.strip() else line) for line in body.splitlines(keepends=True))
block = block[:body_start] + '            try\n            {\n' + body + '''            }
            finally
            {
                if (stack.Length > 0)
                {
                    // FormulaValue can retain strings, arrays and workbook references.
                    // Each invocation rents independently, including reentrant UDF calls.
                    ArrayPool<FormulaValue>.Shared.Return(stack, clearArray: true);
                }
            }
        }

'''
text = text[:start] + block + text[end:]
file.write_text(text, encoding='utf-8')
