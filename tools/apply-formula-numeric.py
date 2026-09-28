from pathlib import Path
file = Path('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs')
text = file.read_text(encoding='utf-8')
marker = '        private readonly ConditionalWeakTable<FormulaExpression, FormulaCompiledExpression> _compiledCache = new();'
assert text.count(marker) == 1
text = text.replace(marker, marker + '\n        private readonly object _compilationGate = new();')
start = text.index('        private FormulaCompiledExpression GetCompiledExpression(')
end = text.index('        private FormulaValue EvaluateCompiled(', start)
text = text[:start] + '''        private FormulaCompiledExpression GetCompiledExpression(
            FormulaExpression expression,
            IFormulaFunctionRegistry functionRegistry,
            IFormulaCalculationObserver? observer)
        {
            FormulaCompiledExpression compiled;
            var fromCache = false;
            var duration = TimeSpan.Zero;
            // Only cold compilation/publication is serialized. The common compatible
            // cache-hit path in Evaluate remains lock-free and allocation-free.
            lock (_compilationGate)
            {
                if (_compiledCache.TryGetValue(expression, out var existing) && existing.IsCompatibleWith(functionRegistry))
                {
                    compiled = existing;
                    fromCache = true;
                }
                else
                {
                    var compiler = new FormulaExpressionCompiler(functionRegistry);
                    var watch = observer != null ? System.Diagnostics.Stopwatch.StartNew() : null;
                    compiled = compiler.Compile(expression);
                    if (watch != null)
                    {
                        watch.Stop();
                        duration = watch.Elapsed;
                    }
                    _compiledCache.Remove(expression);
                    _compiledCache.Add(expression, compiled);
                }
            }

            // Publish before notifying host code; observers may reenter evaluation.
            // Host callbacks must not run while holding the compilation gate.
            observer?.OnExpressionCompiled(expression, compiled.Instructions.Length, duration, fromCache);
            return compiled;
        }

''' + text[end:]
file.write_text(text, encoding='utf-8')
