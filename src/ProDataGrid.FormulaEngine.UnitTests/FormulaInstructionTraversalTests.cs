// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaInstructionTraversalTests
    {
        [Theory]
        [InlineData("SUM({1,2;3,4})", 10)]
        [InlineData("LET(step,7,LAMBDA(x,IF(x>3,x+step,x-step))(5))", 12)]
        [InlineData("INDEX(MAKEARRAY(2,3,LAMBDA(row,col,row*10+col)),2,3)", 23)]
        [InlineData("SUM(A1:B2)+boost", 17)]
        [InlineData("LET(f,LAMBDA(x,LAMBDA(y,x+y)),f(2)(3))", 5)]
        [InlineData("IFERROR(XLOOKUP(7,{1,3,5},{10,30,50}),99)", 99)]
        [InlineData("LEN(TEXTAFTER(\"a-b-c\",\"-\",-1))", 1)]
        [InlineData("SUM(CHOOSECOLS({1,2;3,4},2))", 6)]
        [InlineData("SUM(MAP({1;2;3},LAMBDA(x,x*x)))", 14)]
        public void Heterogeneous_Instruction_Operands_Survive_Repeated_Execution(string text, double expected)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(1);
            sheet.GetCell(1, 2).Value = FormulaValue.FromNumber(2);
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(3);
            sheet.GetCell(2, 2).Value = FormulaValue.FromNumber(4);
            var context = new FormulaEvaluationContext(workbook, sheet, new FormulaCellAddress("Sheet1", 1, 5), new ExcelFunctionRegistry())
                .WithLocalValue("boost", FormulaValue.FromNumber(7));
            var evaluator = new FormulaEvaluator();
            var expression = new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
            var resolver = new WorkbookValueResolver();
            for (var i = 0; i < 20; i++)
            {
                workbook.Settings.EnableCompiledExpressions = true;
                Assert.Equal(expected, evaluator.Evaluate(expression, context, resolver).AsNumber());
                workbook.Settings.EnableCompiledExpressions = false;
                Assert.Equal(expected, evaluator.Evaluate(expression, context, resolver).AsNumber());
            }
        }

        [Fact]
        public void Shared_Subtrees_Keep_Their_Values_And_Stack_Order()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var context = new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            var random = new Random(17827);
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var number = random.Next(-100, 100);
                FormulaExpression expression = new FormulaLiteralExpression(FormulaValue.FromNumber(number));
                double expected = number;
                for (var i = 0; i < 8; i++)
                {
                    var shared = new FormulaBinaryExpression(FormulaBinaryOperator.Add, expression, expression);
                    var amount = random.Next(-100, 100);
                    expression = new FormulaBinaryExpression(FormulaBinaryOperator.Subtract, shared,
                        new FormulaLiteralExpression(FormulaValue.FromNumber(amount)));
                    expected = expected * 2 - amount;
                }
                Assert.Equal(expected, evaluator.Evaluate(expression, context, new DictionaryValueResolver()).AsNumber());
                Assert.Equal(expected, evaluator.Evaluate(expression, context, new DictionaryValueResolver()).AsNumber());
            }
        }
    }
}
