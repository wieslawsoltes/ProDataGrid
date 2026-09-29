from pathlib import Path

def edit(path, old, new):
    p=Path(path); s=p.read_text(encoding='utf-8')
    assert s.count(old)==1,(path,s.count(old),old[:80])
    p.write_text(s.replace(old,new),encoding='utf-8')
core='src/ProDataGrid.FormulaEngine/'
excel='src/ProDataGrid.FormulaEngine.Excel/'
edit(core+'FormulaDependencyGraph.cs', '        private readonly Dictionary<FormulaCellAddress, HashSet<FormulaCellAddress>> _dependencies', '''        private readonly IFormulaFunctionRegistry? _functionRegistry;

        /// <summary>Creates a graph with conventional LET/LAMBDA binding analysis.</summary>
        /// <remarks>Without a registry, callable names are conservatively treated as possible defined names.
        /// Use the registry constructor to match registered-function precedence and custom binding metadata.</remarks>
        public FormulaDependencyGraph() { }

        /// <summary>Creates a graph that uses the same function precedence and binding metadata as evaluation.</summary>
        public FormulaDependencyGraph(IFormulaFunctionRegistry functionRegistry)
        {
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        private readonly Dictionary<FormulaCellAddress, HashSet<FormulaCellAddress>> _dependencies''')
edit(core+'FormulaDependencyGraph.cs', 'private static void CollectDependencies(', 'private void CollectDependencies(')
edit(core+'FormulaDependencyGraph.cs', 'private static void CollectReferences(', 'private void CollectReferences(')
edit(core+'FormulaDependencyGraph.cs', '''                    Visit(new FormulaNameExpression(call.Name), locals);
                    FormulaBindingTraversal.VisitArguments(call, locals, (arg, scope) => { Visit(arg, scope); return false; });''', '''                    if (_functionRegistry == null || !_functionRegistry.TryGetFunction(call.Name, out _))
                        Visit(new FormulaNameExpression(call.Name), locals);
                    FormulaBindingTraversal.VisitArguments(call, locals,
                        FormulaBindingTraversal.GetBindingKind(call, locals, _functionRegistry),
                        (arg, scope) => { Visit(arg, scope); return false; });''')
edit(core+'FormulaCalculationEngine.cs', 'private readonly FormulaDependencyGraph _dependencyGraph = new FormulaDependencyGraph();', 'private readonly FormulaDependencyGraph _dependencyGraph;')
edit(core+'FormulaCalculationEngine.cs', '_functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));', '_functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));\n            _dependencyGraph = new FormulaDependencyGraph(functionRegistry);')
edit(core+'FormulaCalculationEngine.cs', 'return FormulaBindingTraversal.VisitArguments(call, locals, Visit);', 'return FormulaBindingTraversal.VisitArguments(call, locals,\n                        FormulaBindingTraversal.GetBindingKind(call, locals, _functionRegistry), Visit);')
edit(core+'FormulaBindingTraversal.cs', '''            Func<FormulaExpression, HashSet<string>?, bool> visit)''', '''            FormulaLexicalBindingKind bindingKind, Func<FormulaExpression, HashSet<string>?, bool> visit)''')
edit(core+'FormulaBindingTraversal.cs', 'string.Equals(call.Name, "LAMBDA", StringComparison.OrdinalIgnoreCase)', 'bindingKind == FormulaLexicalBindingKind.Lambda')
edit(core+'FormulaBindingTraversal.cs', 'string.Equals(call.Name, "LET", StringComparison.OrdinalIgnoreCase)', 'bindingKind == FormulaLexicalBindingKind.Let')
edit(core+'FormulaBindingTraversal.cs', '        private static HashSet<string> Copy(', '''        public static FormulaLexicalBindingKind GetBindingKind(FormulaFunctionCallExpression call,
            HashSet<string>? locals, IFormulaFunctionRegistry? registry)
        {
            if (locals != null && locals.Contains(call.Name)) return FormulaLexicalBindingKind.None;
            if (registry != null)
                return registry.TryGetFunction(call.Name, out var function) && function is IFormulaLexicalFunction lexical
                    ? lexical.BindingKind : FormulaLexicalBindingKind.None;
            if (string.Equals(call.Name, "LET", StringComparison.OrdinalIgnoreCase)) return FormulaLexicalBindingKind.Let;
            if (string.Equals(call.Name, "LAMBDA", StringComparison.OrdinalIgnoreCase)) return FormulaLexicalBindingKind.Lambda;
            return FormulaLexicalBindingKind.None;
        }

        private static HashSet<string> Copy(''')
edit(excel+'ExcelLexicalFunctions.cs', 'class LetFunction : ExcelFunctionBase, ILazyFormulaFunction', 'class LetFunction : ExcelFunctionBase, ILazyFormulaFunction, IFormulaLexicalFunction')
edit(excel+'ExcelLexicalFunctions.cs', 'public LetFunction() : base("LET", new FormulaFunctionInfo(3, 253)) { }', 'public LetFunction() : base("LET", new FormulaFunctionInfo(3, 253)) { }\n\n        public FormulaLexicalBindingKind BindingKind => FormulaLexicalBindingKind.Let;')
edit(excel+'ExcelLexicalFunctions.cs', 'class LambdaFunction : ExcelFunctionBase, ILazyFormulaFunction', 'class LambdaFunction : ExcelFunctionBase, ILazyFormulaFunction, IFormulaLexicalFunction')
edit(excel+'ExcelLexicalFunctions.cs', 'public LambdaFunction() : base("LAMBDA", new FormulaFunctionInfo(1, 254)) { }', 'public LambdaFunction() : base("LAMBDA", new FormulaFunctionInfo(1, 254)) { }\n\n        public FormulaLexicalBindingKind BindingKind => FormulaLexicalBindingKind.Lambda;')
edit(core+'FormulaValue.Lambda.cs', '''        public FormulaValue ToCellResult()
            => Kind == FormulaValueKind.Lambda
                ? FromError(new FormulaError(FormulaErrorType.Calc, "A lambda must be invoked to produce a cell result."))
                : this;''', '''        public FormulaValue ToCellResult()
        {
            if (Kind == FormulaValueKind.Lambda)
                return FromError(new FormulaError(FormulaErrorType.Calc, "A lambda must be invoked to produce a cell result."));
            if (Kind == FormulaValueKind.Array)
            {
                var array = AsArray();
                for (var row = 0; row < array.RowCount; row++)
                    for (var column = 0; column < array.ColumnCount; column++)
                        if (array.IsPresent(row, column) && array[row, column].Kind == FormulaValueKind.Lambda)
                            return FromError(new FormulaError(FormulaErrorType.Calc, "An array cannot spill callable values."));
            }
            return this;
        }''')
edit(excel+'README.md', 'The complete Microsoft Excel function catalog, LET/LAMBDA closures and higher-order functions, and full application/format interoperability are not claimed by this implementation.', 'LET, LAMBDA and ISOMITTED are described in [Lexical functions](LEXICAL-FUNCTIONS.md). The complete Microsoft Excel function catalog, native-Excel differential qualification, and full application/format interoperability are not claimed by this implementation.')
