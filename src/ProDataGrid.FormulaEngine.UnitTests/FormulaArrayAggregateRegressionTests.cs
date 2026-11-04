// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaArrayAggregateRegressionTests
    {
        [Theory]
        [InlineData("COUNTA(NA())", 1)]
        [InlineData("COUNTA(1/0,\"\",TRUE,0)", 4)]
        [InlineData("COUNTA(HSTACK(1,NA(),\"\",FALSE))", 4)]
        [InlineData("COUNTA(A1:A5)", 4)]
        [InlineData("COUNTA(A1,A2,A3,A4,A5)", 4)]
        [InlineData("COUNT(NA())", 0)]
        [InlineData("COUNT(1/0,\"12\",TRUE,0)", 3)]
        [InlineData("COUNT(HSTACK(1,NA(),\"12\",TRUE))", 1)]
        [InlineData("COUNT(A1:A5)", 1)]
        [InlineData("COUNT(A1,A2,A3,A4,A5)", 1)]
        [InlineData("BYROW(HSTACK(1,NA(),\"\",TRUE),LAMBDA(x,COUNTA(x)))", 4)]
        public void Count_Functions_Apply_Their_Distinct_Error_And_Reference_Rules(string formula, double expected)
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(1);
            sheet.GetCell(2, 1).Value = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            sheet.GetCell(3, 1).Value = FormulaValue.FromText("");
            sheet.GetCell(4, 1).Value = FormulaValue.FromBoolean(true);
            var context = new FormulaEvaluationContext(workbook, sheet, new FormulaCellAddress("Sheet1", 1, 3), new ExcelFunctionRegistry());
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                var value = new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver());
                if (value.Kind == FormulaValueKind.Array) value = value.AsArray()[0, 0];
                Assert.Equal(expected, value.AsNumber());
            }
        }

        [Fact]
        public void Eager_Count_Invocation_Also_Handles_Errors_Without_Propagating_Them()
        {
            var workbook = new TestWorkbook("Book1");
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaFunctionContext(new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry));
            var array = new FormulaArray(3, 1);
            array[0, 0] = FormulaValue.FromNumber(1);
            array[1, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            array[2, 0] = FormulaValue.FromText("");
            Assert.True(registry.TryGetFunction("COUNT", out var count));
            Assert.True(registry.TryGetFunction("COUNTA", out var counta));
            var args = new[] { FormulaValue.FromArray(array), FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)) };
            Assert.Equal(1, count.Invoke(context, args).AsNumber());
            Assert.Equal(4, counta.Invoke(context, args).AsNumber());
        }

        [Theory]
        [InlineData("SUM(A1:B2)", 10)]
        [InlineData("SUM(A1)", 1)]
        [InlineData("SUM(A1:A1)", 1)]
        [InlineData("A1+1", 2)]
        [InlineData("COUNT(A1:B2)", 4)]
        [InlineData("COUNTA(A1:B2)", 4)]
        [InlineData("INDEX(A1:B2,1,1)", 1)]
        [InlineData("AVERAGE(A1:B2)", 2.5)]
        public void Spill_Anchors_Are_Read_Once_As_Scalar_Cells_In_References(string formula, double expected)
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            var registry = new ExcelFunctionRegistry();
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            engine.SetCellFormula(sheet, 1, 1, "{1,2;3,4}");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            var context = new FormulaEvaluationContext(workbook, sheet, new FormulaCellAddress("Sheet1", 1, 4), registry);
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                var result = new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver());
                Assert.Equal(expected, result.AsNumber());
                Assert.Equal(FormulaValueKind.Array, sheet.GetCell(1, 1).Value.Kind);
                Assert.Equal(4, sheet.GetCell(1, 1).Value.AsArray()[1, 1].AsNumber());
            }
        }

        [Fact]
        public void ThreeDimensional_Range_Reads_Include_Each_Spilled_Anchor_Once()
        {
            var workbook = new TestWorkbook("Book1");
            var first = workbook.GetWorksheet("Sheet1");
            var second = workbook.AddWorksheet("Sheet2");
            var registry = new ExcelFunctionRegistry();
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            engine.SetCellFormula(first, 1, 1, "{1;2}");
            engine.SetCellFormula(second, 1, 1, "{3;4}");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1), new FormulaCellAddress("Sheet2", 1, 1) });
            var context = new FormulaEvaluationContext(workbook, first, new FormulaCellAddress("Sheet1", 1, 3), registry);
            var evaluator = new FormulaEvaluator();
            Assert.Equal(10, evaluator.Evaluate(Parse("SUM(Sheet1:Sheet2!A1:A2)"), context, new WorkbookValueResolver()).AsNumber());
            Assert.Equal(4, evaluator.Evaluate(Parse("SUM(Sheet1:Sheet2!A1)"), context, new WorkbookValueResolver()).AsNumber());
        }

        private static FormulaExpression Parse(string formula) => new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
    }
}
