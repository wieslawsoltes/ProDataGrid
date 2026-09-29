// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelLambdaHelperTests
    {
        [Theory]
        [InlineData("MAP({1,2;3,4},LAMBDA(x,x*x))", "1,4;9,16")]
        [InlineData("MAP({1;2},{3;4},LAMBDA(a,b,a+b))", "4;6")]
        [InlineData("MAP({1,2},{3,4},{5,6},LAMBDA(a,b,d,a+b+d))", "9,12")]
        [InlineData("MAP(5,LAMBDA(x,x+2))", "7")]
        [InlineData("LET(offset,10,MAP({1;2},LAMBDA(x,x+offset)))", "11;12")]
        [InlineData("LET(f,LAMBDA(x,x*2),MAP({1;2},f))", "2;4")]
        [InlineData("MAP(HSTACK(1,NA(),3),LAMBDA(x,IFERROR(x,0)))", "1,0,3")]
        [InlineData("MAP({1,0,2},LAMBDA(x,1/x))", "1,#DIV/0!,0.5")]
        [InlineData("MAP(A1:A2,LAMBDA(x,ISOMITTED(x)))", "FALSE;FALSE")]
        [InlineData("MAP(A1:A2,LAMBDA(x,x))", "0;0")]
        [InlineData("MAP({1;2},LAMBDA(x,{7}))", "7;7")]
        [InlineData("MAP({1;2},LAMBDA(x,{1,2}))", "#CALC!")]
        [InlineData("MAP({1;2},LAMBDA(x,LAMBDA(y,x+y)))", "#CALC!")]
        [InlineData("MAP({1;2},LAMBDA(a,b,a+b))", "#VALUE!")]
        [InlineData("MAP({1;2},42)", "#VALUE!")]
        [InlineData("MAP({1;2},NA())", "#N/A")]
        [InlineData("MAP({1;2},{3,4},LAMBDA(a,b,a+b))", "#VALUE!")]
        [InlineData("MAKEARRAY(2,3,LAMBDA(row,col,row*col))", "1,2,3;2,4,6")]
        [InlineData("MAKEARRAY(1,1,LAMBDA(row,col,row+col))", "2")]
        [InlineData("MAKEARRAY(2,2,LAMBDA(row,col,10*row+col))", "11,12;21,22")]
        [InlineData("MAKEARRAY(2,2,LAMBDA(row,col,A1))", "0,0;0,0")]
        [InlineData("MAKEARRAY(1,2,LAMBDA(row,col,1/(col-1)))", "#DIV/0!,1")]
        [InlineData("MAKEARRAY(0,2,LAMBDA(row,col,row))", "#VALUE!")]
        [InlineData("MAKEARRAY(-1,2,LAMBDA(row,col,row))", "#VALUE!")]
        [InlineData("MAKEARRAY(1,2,LAMBDA(x,x))", "#VALUE!")]
        [InlineData("MAKEARRAY(1048577,1,LAMBDA(row,col,row))", "#NUM!")]
        [InlineData("MAKEARRAY(1,16385,LAMBDA(row,col,col))", "#NUM!")]
        [InlineData("MAKEARRAY(1,1,LAMBDA(row,col,{1,2}))", "#CALC!")]
        [InlineData("REDUCE(0,{1,2;3,4},LAMBDA(a,b,a+b))", "10")]
        [InlineData("REDUCE(1,{1,2;3,4},LAMBDA(a,b,a*b))", "24")]
        [InlineData("REDUCE(,{2,3},LAMBDA(a,b,a+b^2))", "11")]
        [InlineData("REDUCE(0,{2,3},LAMBDA(a,b,a+b^2))", "13")]
        [InlineData("REDUCE(A1,{2,3},LAMBDA(a,b,a+b^2))", "13")]
        [InlineData("REDUCE(,2,LAMBDA(a,b,1/0))", "2")]
        [InlineData("REDUCE(\"\",{1,2;3,4},LAMBDA(a,b,a&b))", "\"1234\"")]
        [InlineData("REDUCE(0,{1;2;3},LAMBDA(a,b,VSTACK(a,b)))", "0;1;2;3")]
        [InlineData("REDUCE(LAMBDA(0),{1,2,3},LAMBDA(previous,item,LAMBDA(previous()+item)))()", "6")]
        [InlineData("REDUCE(0,HSTACK(1,NA(),2),LAMBDA(a,b,a+IFERROR(b,0)))", "3")]
        [InlineData("REDUCE(0,{1;2},LAMBDA(x,x))", "#VALUE!")]
        [InlineData("LAMBDA(dummy,seed,REDUCE(seed,{2,3},LAMBDA(a,b,a+b^2)))(0,)", "11")]
        [InlineData("SCAN(0,{1,2;3,4},LAMBDA(a,b,a+b))", "1,3;6,10")]
        [InlineData("SCAN(,{1,2;3,4},LAMBDA(a,b,a+b))", "1,3;6,10")]
        [InlineData("SCAN(1,{1,2;3,4},LAMBDA(a,b,a*b))", "1,2;6,24")]
        [InlineData("SCAN(\"\",{\"a\",\"b\";\"c\",\"d\"},LAMBDA(a,b,a&b))", "\"a\",\"ab\";\"abc\",\"abcd\"")]
        [InlineData("SCAN(0,HSTACK(1,NA(),2),LAMBDA(a,b,IFERROR(a,0)+b))", "1,#N/A,2")]
        [InlineData("SCAN(0,{1,2},LAMBDA(a,b,{9}))", "9,9")]
        [InlineData("SCAN(0,{1,2},LAMBDA(a,b,{1,2}))", "#CALC!")]
        [InlineData("SCAN(0,{1,2},LAMBDA(x,x))", "#VALUE!")]
        [InlineData("BYROW({1,2;3,4},LAMBDA(values,SUM(values)))", "3;7")]
        [InlineData("BYCOL({1,2;3,4},LAMBDA(values,SUM(values)))", "4,6")]
        [InlineData("BYROW({1;2},LAMBDA(values,values))", "1;2")]
        [InlineData("BYCOL({1,2},LAMBDA(values,values))", "1,2")]
        [InlineData("BYROW({1,2;3,4},LAMBDA(values,values))", "#CALC!")]
        [InlineData("BYCOL({1,2;3,4},LAMBDA(values,values))", "#CALC!")]
        [InlineData("BYROW({1,2;3,4},LAMBDA(a,b,a+b))", "#VALUE!")]
        [InlineData("BYCOL({1,2;3,4},7)", "#VALUE!")]
        [InlineData("BYROW(A1:B2,LAMBDA(values,COUNTA(values)))", "0;0")]
        [InlineData("BYROW({\"text\",2;TRUE,3},LAMBDA(values,SUM(values)))", "2;3")]
        [InlineData("BYCOL(HSTACK({1;2},NA()),LAMBDA(values,SUM(values)))", "3,#N/A")]
        [InlineData("SUM(MAP(MAKEARRAY(2,3,LAMBDA(row,col,row*col)),LAMBDA(x,x*2)))", "36")]
        public void Helpers_Agree_In_Compiled_Interpreted_And_Formatted_Paths(string formula, string expected)
        {
            var context = Context();
            var expression = Parse(formula);
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(expected, Describe(evaluator.Evaluate(Parse(formatted), context, new WorkbookValueResolver())));
            }
        }

        [Theory]
        [InlineData("MAP({1,2,3},LAMBDA(x,TICK()))")]
        [InlineData("MAKEARRAY(2,2,LAMBDA(row,col,TICK()))")]
        [InlineData("SCAN(0,{1,2,3},LAMBDA(a,b,TICK()))")]
        [InlineData("BYROW({1;2;3},LAMBDA(values,TICK()))")]
        [InlineData("BYCOL({1,2,3},LAMBDA(values,TICK()))")]
        public void Output_Limits_Are_Checked_Before_Invoking_The_Callback(string formula)
        {
            var registry = new ExcelFunctionRegistry();
            var tick = new CountingFunction("TICK", FormulaValue.FromNumber(1));
            registry.Register(tick);
            var context = Context(registry);
            context.Workbook.Settings.MaximumArrayCellCount = 2;
            var result = new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver());
            Assert.Equal(FormulaErrorType.Num, result.AsError().Type);
            Assert.Equal(0, tick.Calls);
        }

        [Fact]
        public void Input_And_Lambda_Expressions_Are_Evaluated_Once()
        {
            var registry = new ExcelFunctionRegistry();
            var context = Context(registry);
            var array = new FormulaArray(100, 1);
            for (var i = 0; i < array.RowCount; i++) array[i, 0] = FormulaValue.FromNumber(i);
            var source = new CountingFunction("SOURCE", FormulaValue.FromArray(array));
            var factory = new CountingFunction("CALLBACK", FormulaValue.FromLambda(context.CreateLambda(new[] { "x" }, Parse("x+1"))));
            registry.Register(source);
            registry.Register(factory);
            var telemetry = new FormulaCalculationTelemetry();
            context.Workbook.Settings.CalculationObserver = telemetry;
            var result = new FormulaEvaluator().Evaluate(Parse("MAP(SOURCE(),CALLBACK())"), context, new WorkbookValueResolver()).AsArray();
            Assert.Equal(1, source.Calls);
            Assert.Equal(1, factory.Calls);
            Assert.Equal(100, result[99, 0].AsNumber());
            Assert.True(telemetry.CompileCacheHits >= 99);
            Assert.InRange(telemetry.CompiledExpressions, 1, 5);
            Assert.Equal(99, array[99, 0].AsNumber());
        }

        [Fact]
        public void Saved_Callback_Contexts_And_Row_Arrays_Are_Independent()
        {
            var registry = new ExcelFunctionRegistry();
            var save = new SavingFunction();
            registry.Register(save);
            var context = Context(registry);
            var evaluator = new FormulaEvaluator();
            evaluator.Evaluate(Parse("MAP({1;2;3},LAMBDA(x,SAVE(x)))"), context, new WorkbookValueResolver());
            for (var i = 0; i < 3; i++)
                Assert.Equal(i + 1, evaluator.Evaluate(Parse("x"), save.Contexts[i], new WorkbookValueResolver()).AsNumber());
            save.Values.Clear();
            save.Contexts.Clear();
            evaluator.Evaluate(Parse("BYROW({1,2;3,4;5,6},LAMBDA(x,SAVE(x)))"), context, new WorkbookValueResolver());
            Assert.Equal("1,2", Describe(save.Values[0]));
            Assert.Equal("3,4", Describe(save.Values[1]));
            Assert.Equal("5,6", Describe(save.Values[2]));
            Assert.NotSame(save.Values[0].AsArray(), save.Values[1].AsArray());
        }

        [Fact]
        public void Sparse_Blanks_Are_Values_Not_Omitted_Parameters()
        {
            var source = new FormulaArray(2, 2, sparse: true);
            source[0, 0] = FormulaValue.FromNumber(1);
            source[1, 0] = FormulaValue.FromText("");
            source[1, 1] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var expression = Call("MAP", new FormulaLiteralExpression(FormulaValue.FromArray(source)), Parse("LAMBDA(x,IF(ISBLANK(x),9,IFERROR(x,7)))"));
            Assert.Equal("1,9;\"\",7", Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
            var rows = Call("BYROW", new FormulaLiteralExpression(FormulaValue.FromArray(source)), Parse("LAMBDA(values,COUNTA(values))"));
            Assert.Equal("1;2", Describe(evaluator.Evaluate(rows, context, new WorkbookValueResolver())));
            Assert.False(source.IsPresent(0, 1));
        }

        [Fact]
        public void Seeded_Rectangular_Helpers_Agree_With_Independent_Loops()
        {
            var random = new Random(77029);
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var resolver = new WorkbookValueResolver();
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var source = new FormulaArray(random.Next(1, 11), random.Next(1, 11));
                for (var row = 0; row < source.RowCount; row++)
                    for (var col = 0; col < source.ColumnCount; col++) source[row, col] = FormulaValue.FromNumber(random.Next(-50, 51));
                var literal = new FormulaLiteralExpression(FormulaValue.FromArray(source));
                foreach (var compiled in new[] { false, true })
                {
                    context.Workbook.Settings.EnableCompiledExpressions = compiled;
                    var map = evaluator.Evaluate(Call("MAP", literal, literal, Parse("LAMBDA(a,b,a*3+b)")), context, resolver).AsArray();
                    var scan = evaluator.Evaluate(Call("SCAN", Parse("0"), literal, Parse("LAMBDA(a,b,a+b)")), context, resolver).AsArray();
                    var rows = evaluator.Evaluate(Call("BYROW", literal, Parse("LAMBDA(values,SUM(values))")), context, resolver).AsArray();
                    var cols = evaluator.Evaluate(Call("BYCOL", literal, Parse("LAMBDA(values,SUM(values))")), context, resolver).AsArray();
                    double total = 0;
                    var colSums = new double[source.ColumnCount];
                    for (var row = 0; row < source.RowCount; row++)
                    {
                        double rowSum = 0;
                        for (var col = 0; col < source.ColumnCount; col++)
                        {
                            var value = source[row, col].AsNumber();
                            rowSum += value;
                            colSums[col] += value;
                            total += value;
                            Assert.Equal(value * 4, map[row, col].AsNumber());
                            Assert.Equal(total, scan[row, col].AsNumber());
                        }
                        Assert.Equal(rowSum, rows[row, 0].AsNumber());
                    }
                    for (var col = 0; col < source.ColumnCount; col++) Assert.Equal(colSums[col], cols[0, col].AsNumber());
                    Assert.Equal(total, evaluator.Evaluate(Call("REDUCE", Parse("0"), literal, Parse("LAMBDA(a,b,a+b)")), context, resolver).AsNumber());
                }
            }
        }

        [Fact]
        public void Helpers_Spill_And_Recalculate_With_Captured_Dependencies()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(10);
            for (var row = 1; row <= 3; row++) sheet.GetCell(row, 2).Value = FormulaValue.FromNumber(row);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 4, "LET(offset,$A$1,MAP(B1:B3,LAMBDA(x,x+offset)))");
            engine.SetCellFormula(sheet, 1, 5, "SUM(D1:D3)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4), new FormulaCellAddress("Sheet1", 1, 5) });
            Assert.Equal(13, sheet.GetCell(3, 4).Value.AsNumber());
            Assert.Equal(36, sheet.GetCell(1, 5).Value.AsNumber());
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(20);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            Assert.Equal(23, sheet.GetCell(3, 4).Value.AsNumber());
            Assert.Equal(66, sheet.GetCell(1, 5).Value.AsNumber());
        }

        [Fact]
        public void Helpers_Can_Invoke_Recursive_Workbook_Functions()
        {
            var context = Context();
            ((TestWorkbook)context.Workbook).Names.SetExpression("Factorial", Parse("LAMBDA(n,IF(n<2,1,n*Factorial(n-1)))"));
            var result = new FormulaEvaluator().Evaluate(Parse("MAP(SEQUENCE(5),Factorial)"), context, new WorkbookValueResolver());
            Assert.Equal("1;2;6;24;120", Describe(result));
        }

        private static FormulaFunctionCallExpression Call(string name, params FormulaExpression[] args) => new FormulaFunctionCallExpression(name, args);
        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry ?? new ExcelFunctionRegistry());
        }

        private static string Describe(FormulaValue value)
        {
            if (value.Kind != FormulaValueKind.Array)
                return value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture)
                    : value.Kind == FormulaValueKind.Text ? "\"" + value.AsText() + "\"" : value.ToString();
            var array = value.AsArray();
            var text = new StringBuilder();
            for (var row = 0; row < array.RowCount; row++)
            {
                if (row > 0) text.Append(';');
                for (var col = 0; col < array.ColumnCount; col++)
                {
                    if (col > 0) text.Append(',');
                    text.Append(Describe(array[row, col]));
                }
            }
            return text.ToString();
        }

        private sealed class CountingFunction : IFormulaFunction
        {
            private readonly FormulaValue _value;
            public CountingFunction(string name, FormulaValue value) { Name = name; _value = value; }
            public int Calls { get; private set; }
            public string Name { get; }
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) { Calls++; return _value; }
        }

        private sealed class SavingFunction : IFormulaFunction
        {
            public List<FormulaValue> Values { get; } = new List<FormulaValue>();
            public List<FormulaEvaluationContext> Contexts { get; } = new List<FormulaEvaluationContext>();
            public string Name => "SAVE";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(1, 1);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            {
                Values.Add(args[0]);
                Contexts.Add(context.EvaluationContext);
                return FormulaValue.FromNumber(0);
            }
        }
    }
}
