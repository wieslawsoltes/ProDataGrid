// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelLambdaTests
    {
        [Theory]
        [InlineData("LET(x,5,x+1)", "6")]
        [InlineData("LET(x,5,y,x*2,x+y)", "15")]
        [InlineData("LET(x,5,LET(x,7,x)+x)", "12")]
        [InlineData("LET(x,1/0,42)", "42")]
        [InlineData("LET(x,2,x,7,x)", "7")]
        [InlineData("LET(x,1,f,LAMBDA(y,x+y),x,100,f(2))", "3")]
        [InlineData("LET(rate.value,3,rate.value+2)", "5")]
        [InlineData("LET(x,5,X)", "5")]
        [InlineData("LAMBDA(x,x+1)(5)", "6")]
        [InlineData("(LAMBDA(x,x*2))(3)", "6")]
        [InlineData("-LAMBDA(x,x+1)(5)", "-6")]
        [InlineData("LAMBDA(x,x)(25)%", "0.25")]
        [InlineData("LAMBDA(42)()", "42")]
        [InlineData("LAMBDA(x,LAMBDA(y,x+y))(3)(4)", "7")]
        [InlineData("LET(f,LAMBDA(x,x*x),f(5)+f(6))", "61")]
        [InlineData("LAMBDA(f,x,f(x))(LAMBDA(y,y+10),2)", "12")]
        [InlineData("LET(f,IF(TRUE,LAMBDA(x,x+1),LAMBDA(x,x+2)),f(7))", "8")]
        [InlineData("IF(TRUE,LAMBDA(x,x*2),LAMBDA(x,x*3))(4)", "8")]
        [InlineData("LET(ABS,LAMBDA(x,x+10),ABS(3))", "13")]
        [InlineData("LET(SUM,LAMBDA(x,x+10),SUM(3))", "13")]
        [InlineData("LET(ABS,5,ABS(3))", "#VALUE!")]
        [InlineData("LAMBDA(x,y,IF(ISOMITTED(y),x,x+y))(2,)", "2")]
        [InlineData("LAMBDA(x,y,ISOMITTED(y))(2,0)", "FALSE")]
        [InlineData("LAMBDA(x,y,ISOMITTED(y))(2,\"\")", "FALSE")]
        [InlineData("LAMBDA(x,y,ISOMITTED(y))(2,A1)", "FALSE")]
        [InlineData("LAMBDA(x,y,LAMBDA(a,b,ISOMITTED(b))(x,y))(2,)", "TRUE")]
        [InlineData("LAMBDA(x,y,LET(saved,y,ISOMITTED(saved)))(2,)", "TRUE")]
        [InlineData("LAMBDA(x,y,IFERROR(y,17))(2,1/0)", "17")]
        [InlineData("ISOMITTED(A1)", "FALSE")]
        [InlineData("LAMBDA(x,x)(1,2)", "#VALUE!")]
        [InlineData("LAMBDA(x,x)()", "#VALUE!")]
        [InlineData("LAMBDA(x,X,x)(1,2)", "#VALUE!")]
        [InlineData("LAMBDA(A1,A1)(2)", "#VALUE!")]
        [InlineData("LAMBDA(a.b,a.b)(2)", "#VALUE!")]
        [InlineData("LET(c,1,c)", "#VALUE!")]
        [InlineData("LET(A1,1,A1)", "#VALUE!")]
        [InlineData("LET(x,1,y,2)", "#VALUE!")]
        [InlineData("LET(x,1)", "#VALUE!")]
        [InlineData("(42)(1)", "#VALUE!")]
        [InlineData("UNKNOWN(1)", "#NAME?")]
        public void Lexical_Functions_And_Formatting_Agree_In_Both_Evaluation_Modes(string formula, string expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            var reparsed = parser.Parse(formatted, new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                var resolver = new WorkbookValueResolver(parser);
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, resolver)));
                Assert.Equal(expected, Describe(evaluator.Evaluate(reparsed, context, resolver)));
            }
        }

        [Fact]
        public void Let_Computes_A_Binding_Once_Per_Evaluation_Not_Per_Reference()
        {
            var registry = new ExcelFunctionRegistry();
            var tick = new TickFunction();
            registry.Register(tick);
            var context = Context(registry);
            var expression = Parse("LET(x,TICK(),x+x+x)");
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var before = tick.Count;
                Assert.Equal(3 * (before + 1), evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber());
                Assert.Equal(before + 1, tick.Count);
            }
        }

        [Fact]
        public void Bare_Lambdas_Are_Callable_Values_But_Not_Worksheet_Results()
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var value = evaluator.Evaluate(Parse("LAMBDA(x,x+1)"), context, new WorkbookValueResolver());
            Assert.Equal(FormulaValueKind.Lambda, value.Kind);
            Assert.Equal(FormulaErrorType.Calc, value.ToCellResult().AsError().Type);
            Assert.Equal(3, evaluator.InvokeLambda(value.AsLambda(), new[] { FormulaValue.FromNumber(2) }, context, new WorkbookValueResolver()).AsNumber());
            Assert.Equal(value, FormulaValue.FromLambda(value.AsLambda()));
            Assert.Equal(value.GetHashCode(), FormulaValue.FromLambda(value.AsLambda()).GetHashCode());
            Assert.Throws<InvalidOperationException>(() => FormulaValue.Blank.AsLambda());
            Assert.Throws<ArgumentNullException>(() => FormulaValue.FromLambda(null!));
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), (ExcelFunctionRegistry)context.FunctionRegistry);
            engine.SetCellFormula(context.Worksheet, 1, 1, "LAMBDA(x,x)");
            engine.Recalculate(context.Workbook, new[] { context.Address });
            Assert.Equal(FormulaErrorType.Calc, context.Worksheet.GetCell(1, 1).Value.AsError().Type);
        }

        [Fact]
        public void Escaping_Closures_Snapshot_Reused_Argument_Buffers()
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var resolver = new WorkbookValueResolver();
            var lambda = evaluator.Evaluate(Parse("LAMBDA(a,b,d,LAMBDA(x,a+b+d+x))"), context, resolver).AsLambda();
            var values = new[] { FormulaValue.FromNumber(1), FormulaValue.FromNumber(2), FormulaValue.FromNumber(3) };
            var first = evaluator.InvokeLambda(lambda, values, context, resolver).AsLambda();
            values[0] = FormulaValue.FromNumber(100);
            values[2] = FormulaValue.FromNumber(300);
            var second = evaluator.InvokeLambda(lambda, values, context, resolver).AsLambda();
            Assert.Equal(10, evaluator.InvokeLambda(first, new[] { FormulaValue.FromNumber(4) }, context, resolver).AsNumber());
            Assert.Equal(406, evaluator.InvokeLambda(second, new[] { FormulaValue.FromNumber(4) }, context, resolver).AsNumber());
        }

        [Fact]
        public void Named_Lambda_Recursion_And_Recursion_Limits_Are_Independent_Of_The_Name_Cycle_Guard()
        {
            var context = Context();
            var workbook = (TestWorkbook)context.Workbook;
            workbook.Names.SetExpression("Factorial", Parse("LAMBDA(n,IF(n<2,1,n*Factorial(n-1)))"));
            workbook.Names.SetExpression("Forever", Parse("LAMBDA(n,Forever(n+1))"));
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(3628800, evaluator.Evaluate(Parse("Factorial(10)"), context, new WorkbookValueResolver()).AsNumber());
                workbook.Settings.MaximumLambdaDepth = 12;
                Assert.Equal(FormulaErrorType.Num, evaluator.Evaluate(Parse("Forever(1)"), context, new WorkbookValueResolver()).AsError().Type);
                Assert.Equal(6, evaluator.Evaluate(Parse("Factorial(3)"), context, new WorkbookValueResolver()).AsNumber());
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => workbook.Settings.MaximumLambdaDepth = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => workbook.Settings.MaximumLambdaDepth = 257);
        }

        [Fact]
        public void Global_Names_Do_Not_Capture_An_Unrelated_Callers_Locals()
        {
            var context = Context();
            var workbook = (TestWorkbook)context.Workbook;
            workbook.Names.SetValue("x", FormulaValue.FromNumber(10));
            workbook.Names.SetExpression("GlobalValue", Parse("x"));
            workbook.Names.SetExpression("GlobalFunction", Parse("LAMBDA(y,x+y)"));
            var evaluator = new FormulaEvaluator();
            Assert.Equal(10, evaluator.Evaluate(Parse("LET(x,99,GlobalValue)"), context, new WorkbookValueResolver()).AsNumber());
            Assert.Equal(12, evaluator.Evaluate(Parse("LET(x,99,GlobalFunction(2))"), context, new WorkbookValueResolver()).AsNumber());
        }

        [Fact]
        public void Named_Call_Dependencies_Recalculate_And_Update_When_The_Name_Changes()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(3);
            sheet.GetCell(1, 2).Value = FormulaValue.FromNumber(4);
            workbook.Names.SetExpression("Scale", Parse("LAMBDA(amount,amount*$A$1)"));
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "Scale(B1)");
            var target = new FormulaCellAddress("Sheet1", 1, 3);
            Assert.Contains(new FormulaCellAddress("Sheet1", 1, 1), engine.DependencyGraph.GetDependencies(target));
            Assert.Contains(new FormulaCellAddress("Sheet1", 1, 2), engine.DependencyGraph.GetDependencies(target));
            engine.Recalculate(workbook, new[] { target });
            Assert.Equal(12, sheet.GetCell(1, 3).Value.AsNumber());
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(5);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            Assert.Equal(20, sheet.GetCell(1, 3).Value.AsNumber());
            using var tracking = engine.TrackNameChanges(workbook);
            workbook.Names.SetExpression("Scale", Parse("LAMBDA(amount,amount*2)"));
            Assert.DoesNotContain(new FormulaCellAddress("Sheet1", 1, 1), engine.DependencyGraph.GetDependencies(target));
        }

        [Fact]
        public void Lexical_Declarations_Do_Not_Create_False_Workbook_Name_Cycles()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            workbook.Names.SetExpression("x", Parse("C1"));
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "LET(x,2,LAMBDA(x,x+1)(x))");
            var target = new FormulaCellAddress("Sheet1", 1, 3);
            Assert.Empty(engine.DependencyGraph.GetDependencies(target));
            var result = engine.Recalculate(workbook, new[] { target });
            Assert.False(result.HasCycle);
            Assert.Equal(3, sheet.GetCell(1, 3).Value.AsNumber());
        }

        [Fact]
        public void Named_Callable_Bodies_Participate_In_Volatility_Analysis()
        {
            var workbook = new TestWorkbook("Book1");
            var registry = new ExcelFunctionRegistry();
            var tick = new TickFunction();
            registry.Register(tick);
            workbook.Names.SetExpression("ClockValue", Parse("LAMBDA(TICK())"));
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            var sheet = workbook.GetWorksheet("Sheet1");
            engine.SetCellFormula(sheet, 1, 1, "ClockValue()");
            engine.Recalculate(workbook, Array.Empty<FormulaCellAddress>());
            Assert.Equal(1, sheet.GetCell(1, 1).Value.AsNumber());
            engine.Recalculate(workbook, Array.Empty<FormulaCellAddress>());
            Assert.Equal(2, sheet.GetCell(1, 1).Value.AsNumber());
        }

        [Fact]
        public void Copy_And_Insert_Rewrite_Invocation_Targets_And_Nonadjacent_Arguments()
        {
            var expression = Parse("LAMBDA(x,x+A1)(B2)");
            var translated = FormulaReferenceTranslator.TranslateForCopy(expression, 2, 1);
            var text = new ExcelFormulaFormatter().Format(translated, new FormulaFormatOptions());
            Assert.Contains("B3", text);
            Assert.Contains("C4", text);
            var workbook = new TestWorkbook("Book1");
            var sheet = (TestWorksheet)workbook.GetWorksheet("Sheet1");
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "SUM(A2,5,A3)");
            engine.SetCellFormula(sheet, 1, 4, "LAMBDA(a,b,d,a+b+d)(A2,5,A3)");
            sheet.InsertRows(2, 1);
            engine.InsertRows(workbook, "Sheet1", 2, 1, new ExcelFormulaFormatter());
            Assert.Equal("SUM(A3,5,A4)", sheet.GetCell(1, 3).Formula);
            Assert.Contains("(A3,5,A4)", sheet.GetCell(1, 4).Formula);
        }

        [Fact]
        public void Lambda_Bodies_Reuse_Compiled_Plans_Across_Invocations()
        {
            var context = Context();
            var telemetry = new FormulaCalculationTelemetry();
            context.Workbook.Settings.CalculationObserver = telemetry;
            var lambda = context.CreateLambda(new[] { "x" }, Parse("x*x"));
            var evaluator = new FormulaEvaluator();
            var args = new FormulaValue[1];
            for (var i = 0; i < 100; i++)
            {
                args[0] = FormulaValue.FromNumber(i);
                Assert.Equal(i * i, evaluator.InvokeLambda(lambda, args, context, new WorkbookValueResolver()).AsNumber());
            }
            Assert.Equal(1, telemetry.CompiledExpressions);
            Assert.Equal(99, telemetry.CompileCacheHits);
        }

        [Fact]
        public void Concurrent_Closures_Do_Not_Share_Mutable_Parameter_Bindings()
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var lambda = context.CreateLambda(new[] { "x" }, Parse("LAMBDA(y,x+y)(5)"));
            Parallel.For(0, 256, i => Assert.Equal(i + 5,
                evaluator.InvokeLambda(lambda, new[] { FormulaValue.FromNumber(i) }, context, new DictionaryValueResolver()).AsNumber()));
        }

        [Theory]
        [InlineData(126, false)]
        [InlineData(127, true)]
        public void Let_Binding_Limit_Is_Enforced(int count, bool error)
        {
            var text = new StringBuilder("LET(");
            for (var i = 0; i < count; i++) text.Append("_p").Append(i).Append(",1,");
            text.Append("7)");
            var result = new FormulaEvaluator().Evaluate(Parse(text.ToString()), Context(), new DictionaryValueResolver());
            if (error) Assert.Equal(FormulaErrorType.Value, result.AsError().Type);
            else Assert.Equal(7, result.AsNumber());
        }

        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());

        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry ?? new ExcelFunctionRegistry());
        }

        private static string Describe(FormulaValue value) => value.Kind == FormulaValueKind.Number
            ? value.AsNumber().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) : value.ToString();

        private sealed class TickFunction : IFormulaFunction
        {
            public int Count { get; private set; }
            public string Name => "TICK";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0, isVolatile: true);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) => FormulaValue.FromNumber(++Count);
        }
    }
}
