// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelCallableArrayTests
    {
        [Theory]
        [InlineData("LET(items,MAP({1;2;3},LAMBDA(x,LAMBDA(x*x))),SUM(MAP(items,LAMBDA(f,f()))))", 14)]
        [InlineData("MAP(LAMBDA(42),LAMBDA(f,f()))", 42)]
        [InlineData("LET(items,MAKEARRAY(2,3,LAMBDA(row,col,LAMBDA(row*10+col))),SUM(MAP(items,LAMBDA(f,f()))))", 102)]
        [InlineData("LET(items,BYROW({1,2;3,4;5,6},LAMBDA(row,LAMBDA(SUM(row)))),SUM(MAP(items,LAMBDA(f,f()))))", 21)]
        [InlineData("LET(items,BYCOL({1,2;3,4;5,6},LAMBDA(col,LAMBDA(SUM(col)))),SUM(MAP(items,LAMBDA(f,f()))))", 21)]
        [InlineData("LET(items,SCAN(LAMBDA(0),{1;2;3},LAMBDA(prev,item,LAMBDA(prev()+item))),SUM(MAP(items,LAMBDA(f,f()))))", 10)]
        [InlineData("LET(items,MAP({1;2;3},LAMBDA(x,LAMBDA(y,x+y))),INDEX(items,2,1)(10))", 12)]
        [InlineData("LET(items,MAP({1;2;3},{4;5;6},{7;8;9},LAMBDA(a,b,d,LAMBDA(a+b+d))),SUM(MAP(items,LAMBDA(f,f()))))", 45)]
        public void Callable_Arrays_Compose_Without_Overwriting_Earlier_Bindings(string text, double expected)
        {
            var context = Context();
            var expression = Parse(text);
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var candidate in new[] { expression, Parse(formatted) })
                {
                    var result = new FormulaEvaluator().Evaluate(candidate, context, new WorkbookValueResolver());
                    if (result.Kind == FormulaValueKind.Array) result = result.AsArray()[0, 0];
                    Assert.Equal(expected, result.AsNumber());
                }
            }
        }

        [Theory]
        [InlineData("MAP({1;2},LAMBDA(x,LAMBDA(y,x+y)))")]
        [InlineData("MAKEARRAY(2,2,LAMBDA(row,col,LAMBDA(row+col)))")]
        [InlineData("BYROW({1,2;3,4},LAMBDA(row,LAMBDA(SUM(row))))")]
        [InlineData("BYCOL({1,2;3,4},LAMBDA(col,LAMBDA(SUM(col))))")]
        [InlineData("SCAN(LAMBDA(0),{1;2},LAMBDA(prev,item,LAMBDA(prev()+item)))")]
        public void Callable_Results_Are_Retained_Internally_But_Cannot_Spill_To_Cells(string formula)
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var internalValue = evaluator.Evaluate(Parse(formula), context, new WorkbookValueResolver());
            Assert.Equal(FormulaValueKind.Array, internalValue.Kind);
            Assert.Equal(FormulaValueKind.Lambda, internalValue.AsArray()[0, 0].Kind);
            Assert.Equal(FormulaErrorType.Calc, internalValue.ToCellResult().AsError().Type);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(context.Worksheet, 1, 1, formula);
            engine.Recalculate(context.Workbook, new[] { context.Address });
            Assert.Equal(FormulaErrorType.Calc, context.Worksheet.GetCell(1, 1).Value.AsError().Type);
            Assert.Equal(FormulaValueKind.Blank, context.Worksheet.GetCell(1, 2).Value.Kind);
            Assert.Equal(FormulaValueKind.Blank, context.Worksheet.GetCell(2, 1).Value.Kind);
        }

        [Fact]
        public void Escaped_Row_Thunks_Hold_Independent_Slices_Across_Later_Evaluations()
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var resolver = new WorkbookValueResolver();
            var result = evaluator.Evaluate(Parse("BYROW({1,2;3,4;5,6},LAMBDA(row,LAMBDA(SUM(row))))"), context, resolver).AsArray();
            evaluator.Evaluate(Parse("BYROW({90,91;92,93},LAMBDA(row,LAMBDA(SUM(row))))"), context, resolver);
            GC.Collect();
            for (var i = 0; i < 3; i++)
                Assert.Equal(3 + 4 * i, evaluator.InvokeLambda(result[i, 0].AsLambda(), Array.Empty<FormulaValue>(), context, resolver).AsNumber());
        }

        [Fact]
        public void Parallel_Helper_Calls_Have_Independent_Lexical_Frames()
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var expression = Parse("LET(items,MAP({1;2;3},LAMBDA(x,LAMBDA(x+offset))),SUM(MAP(items,LAMBDA(f,f()))))");
            Parallel.For(0, 128, i =>
                Assert.Equal(6 + 3 * i, evaluator.Evaluate(expression,
                    context.WithLocalValue("offset", FormulaValue.FromNumber(i)), new DictionaryValueResolver()).AsNumber()));
        }

        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
    }
}
