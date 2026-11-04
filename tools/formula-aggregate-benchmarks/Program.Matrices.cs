// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    static partial void ValidateAdditional(FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var tests = new (string Formula, double Expected)[]
        {
            ("SUM(MMULT({1,2;3,4},{5,6;7,8}))", 134),
            ("INDEX(MMULT({1,2,3},{4;5;6}),1,1)", 32),
            ("SUM(MMULT({1;2;3},{4,5}))", 54),
            ("MDETERM({1,3,8,5;1,3,6,1;1,1,1,0;7,3,10,2})", 88),
            ("MDETERM({3,6,1;1,1,0;3,10,2})", 1),
            ("MDETERM({0,2;1,3})", -2),
            ("MDETERM({1,2;2,4})", 0),
            ("MDETERM(MUNIT(12))", 1),
            ("MDETERM({1E200,0,0,0;0,1E200,0,0;0,0,1E-200,0;0,0,0,1E-200})", 1),
            ("INDEX(MINVERSE({4,7;2,6}),1,2)", -.7),
            ("INDEX(MINVERSE({0,2;1,3}),1,1)", -1.5),
            ("INDEX(MINVERSE({1E200,0;0,1E-200}),2,2)", 1e200),
            ("LET(a,{4,7;2,6},SUM(MMULT(a,MINVERSE(a))))", 2),
            ("SUM(MUNIT(4))", 4), ("SUM(MUNIT(2.9))", 2)
        };
        var errors = new (string Formula, FormulaErrorType Error)[]
        {
            ("MINVERSE({1,2;2,4})", FormulaErrorType.Num),
            ("MMULT({1,2},{3,4})", FormulaErrorType.Value),
            ("MINVERSE({1,\"2\";3,4})", FormulaErrorType.Value),
            ("MUNIT(0)", FormulaErrorType.Value),
            ("MDETERM({1,2,3;4,5,6})", FormulaErrorType.Value)
        };
        var count = 0;
        foreach (var compiled in new[] { true, false }) foreach (var precision in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            context.Workbook.Settings.ApplyNumberPrecision = precision;
            foreach (var item in tests)
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Number || !double.IsFinite(value.AsNumber()) ||
                    Math.Abs(value.AsNumber() - item.Expected) > Math.Max(1, Math.Abs(item.Expected)) * 1e-11)
                    throw new InvalidOperationException("Matrix smoke failed: " + item.Formula + " => " + value);
                count++;
            }
            foreach (var item in errors)
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Error || value.AsError().Type != item.Error)
                    throw new InvalidOperationException("Matrix error smoke failed: " + item.Formula + " => " + value);
                count++;
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Matrix smoke assertions=" + count);
    }

    static partial void MeasureAdditional(StringBuilder output, FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        const int size = 64;
        var left = new FormulaArray(size, size);
        var right = new FormulaArray(size, size);
        for (var row = 0; row < size; row++) for (var column = 0; column < size; column++)
        {
            left[row, column] = FormulaValue.FromNumber((row * 17 + column) % 13 - 6);
            right[row, column] = FormulaValue.FromNumber((row + column * 19) % 11 - 5);
        }
        var function = Function((ExcelFunctionRegistry)context.FunctionRegistry, "MMULT");
        var call = new FormulaFunctionContext(context);
        var args = new[] { FormulaValue.FromArray(left), FormulaValue.FromArray(right) };
        var oracle = ReferenceMultiply(left, right);
        var checkedResult = function.Invoke(call, args).AsArray();
        for (var row = 0; row < size; row++) for (var column = 0; column < size; column++)
            if (oracle[row, column] != checkedResult[row, column]) throw new InvalidOperationException("Matrix benchmark full-result differential failed.");
        var expected = SumCells(oracle);
        // Both paths include allocating/writing the output. The reference is a public-value
        // triple loop for valid numeric inputs; optimized snapshot validation/copy/clearing is timed.
        Measure(output, "mmult_64_reference_values", 20, () => SumCells(ReferenceMultiply(left, right)), expected);
        Measure(output, "mmult_64_contiguous_compensated", 20, () => SumCells(function.Invoke(call, args).AsArray()), expected);

        var triangular = new FormulaArray(size, size);
        for (var row = 0; row < size; row++) for (var column = 0; column < size; column++)
            triangular[row, column] = FormulaValue.FromNumber(row == column ? 1 : column == row + 1 ? -.25 : 0);
        var determinant = Function((ExcelFunctionRegistry)context.FunctionRegistry, "MDETERM");
        var inverse = Function((ExcelFunctionRegistry)context.FunctionRegistry, "MINVERSE");
        var matrixArgs = new[] { FormulaValue.FromArray(triangular) };
        var inverseCheck = inverse.Invoke(call, matrixArgs).AsArray();
        for (var row = 0; row < size; row++) for (var column = 0; column < size; column++)
        {
            var reference = column < row ? 0 : Math.Pow(.25, column - row);
            if (inverseCheck[row, column].AsNumber() != reference) throw new InvalidOperationException("Triangular inverse full-result check failed.");
        }
        Measure(output, "mdeterm_64_pooled", 20, () => determinant.Invoke(call, matrixArgs).AsNumber(), 1);
        Measure(output, "minverse_64_pooled", 20, () => Trace(inverse.Invoke(call, matrixArgs).AsArray()), size);
    }

    private static FormulaArray ReferenceMultiply(FormulaArray left, FormulaArray right)
    {
        var result = new FormulaArray(left.RowCount, right.ColumnCount);
        for (var row = 0; row < result.RowCount; row++) for (var column = 0; column < result.ColumnCount; column++)
        {
            double sum = 0;
            for (var k = 0; k < left.ColumnCount; k++) sum += left[row, k].AsNumber() * right[k, column].AsNumber();
            result[row, column] = FormulaValue.FromNumber(sum);
        }
        return result;
    }

    private static double SumCells(FormulaArray array)
    {
        double sum = 0;
        for (var row = 0; row < array.RowCount; row++) for (var column = 0; column < array.ColumnCount; column++) sum += array[row, column].AsNumber();
        return sum;
    }
    private static double Trace(FormulaArray array)
    {
        double trace = 0;
        for (var i = 0; i < array.RowCount; i++) trace += array[i, i].AsNumber();
        return trace;
    }
}
