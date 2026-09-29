// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateNumerics(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var tests = new (string Formula, double Expected)[]
        {
            ("SIN(0.5)", Math.Sin(0.5)), ("COS(0.5)", Math.Cos(0.5)), ("TAN(0.5)", Math.Tan(0.5)),
            ("COT(0.5)", 1 / Math.Tan(0.5)), ("SEC(0.5)", 1 / Math.Cos(0.5)), ("CSC(0.5)", 1 / Math.Sin(0.5)),
            ("ASIN(0.5)", Math.Asin(0.5)), ("ACOS(0.5)", Math.Acos(0.5)), ("ATAN(0.5)", Math.Atan(0.5)),
            ("ACOT(-1)", 3 * Math.PI / 4), ("ATAN2(-1,-1)", -3 * Math.PI / 4),
            ("SINH(0.5)", Math.Sinh(0.5)), ("COSH(0.5)", Math.Cosh(0.5)), ("TANH(0.5)", Math.Tanh(0.5)),
            ("COTH(0.5)", 1 / Math.Tanh(0.5)), ("SECH(0.5)", 1 / Math.Cosh(0.5)), ("CSCH(0.5)", 1 / Math.Sinh(0.5)),
            ("ASINH(0.5)", Math.Asinh(0.5)), ("ACOSH(2)", Math.Acosh(2)), ("ATANH(0.5)", Math.Atanh(0.5)),
            ("ACOTH(2)", Math.Atanh(0.5)), ("DEGREES(PI())", 180), ("RADIANS(180)", Math.PI),
            ("SQRTPI(2)", Math.Sqrt(2 * Math.PI)), ("FACT(5)", 120), ("FACTDOUBLE(7)", 105),
            ("COMBIN(8,2)", 28), ("PERMUT(100,3)", 970200), ("PERMUTATIONA(2,3)", 8),
            ("QUOTIENT(-10,3)", -3), ("EVEN(-1.5)", -2), ("ODD(0)", 1),
            ("SIN(1E-12)", 1e-12), ("SUM(MAP({3;4;5},LAMBDA(x,FACT(x))))", 150)
        };
        var checks = 0;
        foreach (var compiled in new[] { true, false })
            foreach (var precision in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                foreach (var test in tests)
                {
                    var actual = evaluator.Evaluate(parser.Parse(test.Formula, new FormulaParseOptions()), context, resolver).AsNumber();
                    if (!double.IsFinite(actual) || Math.Abs(actual - test.Expected) > Math.Abs(test.Expected) * 5e-13)
                        throw new InvalidOperationException("Scientific smoke failed: " + test.Formula);
                    checks++;
                }
            }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Scientific smoke assertions=" + checks);
    }

    private static void MeasureNumerics(StringBuilder output, FormulaEvaluationContext context,
        FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var formula = parser.Parse("SIN(input)", new FormulaParseOptions());
        var mapped = parser.Parse("MAP(input,LAMBDA(x,SIN(x)))", new FormulaParseOptions());
        // A rounded integer checksum is exactly summable, avoiding checksum-roundoff false alarms.
        var expected = Math.Round(Math.Sin(9999) * 1e12);
        Measure(output, "sin_array_10000", 20, () => Math.Round(LastNumber(evaluator.Evaluate(formula, context, resolver)) * 1e12), expected);
        Measure(output, "sin_map_10000", 20, () => Math.Round(LastNumber(evaluator.Evaluate(mapped, context, resolver)) * 1e12), expected);

        var data = new FormulaArray(100000, 1);
        var sorted = new double[data.RowCount];
        var random = new Random(77419);
        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i] = random.Next(0, 100000);
            data[i, 0] = FormulaValue.FromNumber(sorted[i]);
        }
        Array.Sort(sorted); // Oracle outside the timed regions; input data remains unsorted.
        var registry = (ExcelFunctionRegistry)context.FunctionRegistry;
        var call = new FormulaFunctionContext(context);
        var median = Function(registry, "MEDIAN");
        var large = Function(registry, "LARGE");
        var percentile = Function(registry, "PERCENTILE.INC");
        var medianArgs = new[] { FormulaValue.FromArray(data) };
        var largeArgs = new[] { FormulaValue.FromArray(data), FormulaValue.FromNumber(100) };
        var percentileArgs = new[] { FormulaValue.FromArray(data), FormulaValue.FromNumber(0.25) };
        Measure(output, "median_100000", 10, () => median.Invoke(call, medianArgs).AsNumber() * 4, (sorted[49999] + sorted[50000]) * 2);
        Measure(output, "large_100000_k100", 10, () => large.Invoke(call, largeArgs).AsNumber(), sorted[99900]);
        Measure(output, "percentile_100000_q25", 10, () => percentile.Invoke(call, percentileArgs).AsNumber() * 4, sorted[24999] + 3 * sorted[25000]);
    }
}
