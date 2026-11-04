// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateOrderStatistics(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        // The identical harness can run against the earlier baseline, which lacks the new functions.
        // Only this untimed compatibility smoke is skipped; timed baseline scenarios remain identical.
        if (!context.FunctionRegistry.TryGetFunction("PERCENTILE.EXC", out _)) return;
        var cases = new (string Formula, double Expected)[]
        {
            ("MEDIAN({9,1})", 5), ("MEDIAN({1E308,1E308})", 1e308),
            ("PERCENTILE.INC({-1E308,1E308},0.5)", 0),
            ("PERCENTILE.EXC({1,2,3,4},0.25)", 1.25),
            ("PERCENTILE.EXC({1,2,3,4},0.8)", 4),
            ("QUARTILE.EXC({1,2,3,4},3)", 3.75),
            ("LARGE({4,2,8,1},2)", 4), ("SMALL({4,2,8,1},2)", 2)
        };
        var errors = new[] { "PERCENTILE.EXC({1,2},0.1)", "QUARTILE.EXC({1,2,3},4)" };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
            foreach (var precision in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                foreach (var item in cases)
                {
                    var actual = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver).AsNumber();
                    if (!double.IsFinite(actual) || Math.Abs(actual - item.Expected) > Math.Max(1, Math.Abs(item.Expected)) * 1e-12)
                        throw new InvalidOperationException("Order statistic smoke failed: " + item.Formula);
                    assertions++;
                }
                foreach (var text in errors)
                {
                    var value = evaluator.Evaluate(parser.Parse(text, new FormulaParseOptions()), context, resolver);
                    if (value.Kind != FormulaValueKind.Error || value.AsError().Type != FormulaErrorType.Num)
                        throw new InvalidOperationException("Order statistic error smoke failed: " + text);
                    assertions++;
                }
            }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Order statistic smoke assertions=" + assertions);
    }
}
