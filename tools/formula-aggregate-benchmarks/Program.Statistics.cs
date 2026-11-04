// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateDescriptive(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        ValidatePairedStatistics(context, evaluator, resolver, parser);
        var cases = new (string Formula, double Expected)[]
        {
            ("VAR.S(1,2,3)", 1), ("VAR.P(1,2,3)", 2d / 3),
            ("STDEV.S(1,2,3)", 1), ("STDEV.P(1,2,3)", Math.Sqrt(2d / 3)),
            ("VAR(1,2,3)", 1), ("VARP(1,2,3)", 2d / 3),
            ("STDEV(1,2,3)", 1), ("STDEVP(1,2,3)", Math.Sqrt(2d / 3)),
            ("DEVSQ({4;5;8;7;11;4;3})", 48), ("AVEDEV({4;5;6;7;5;4;3})", 50d / 49),
            ("SUMSQ(3,4)", 25), ("SKEW(1,2,3)", 0), ("SKEW.P(1,2,3)", 0),
            ("KURT(1,2,3,4)", -1.2), ("GEOMEAN(1,4,16)", 4), ("HARMEAN(1,2,4)", 12d / 7),
            ("VARA({TRUE,FALSE,\"text\",2})", 11d / 12), ("VARPA({TRUE,FALSE,\"12\",2})", 11d / 16),
            ("STDEVA({TRUE,FALSE,\"text\",2})", Math.Sqrt(11d / 12)), ("STDEVPA({TRUE,FALSE,\"12\",2})", Math.Sqrt(11d / 16)),
            ("STDEV.P(1e308,-1e308)", 1e308), ("STDEV.P(1e-300,-1e-300)", 1e-300),
            ("GEOMEAN(1e308,1e-308)", 1), ("HARMEAN(1e-300,1e300)", 2e-300)
        };
        var errors = new (string Formula, FormulaErrorType Expected)[]
        {
            ("VAR.S(1)", FormulaErrorType.Div0), ("SKEW.P(1,2)", FormulaErrorType.Div0),
            ("KURT(1,1,1,1)", FormulaErrorType.Div0), ("GEOMEAN(1,0)", FormulaErrorType.Num),
            ("HARMEAN(1,-1)", FormulaErrorType.Num), ("VAR.P(1e308,-1e308)", FormulaErrorType.Num),
            ("DEVSQ(1,NA())", FormulaErrorType.NA), ("SUMSQ(\"invalid\")", FormulaErrorType.Value)
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
            foreach (var precision in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                foreach (var item in cases)
                {
                    var result = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (result.Kind != FormulaValueKind.Number || !StatisticsNear(item.Expected, result.AsNumber()))
                        throw new InvalidOperationException("Statistics smoke failed: " + item.Formula + " => " + result);
                    assertions++;
                }
                foreach (var item in errors)
                {
                    var result = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (result.Kind != FormulaValueKind.Error || result.AsError().Type != item.Expected)
                        throw new InvalidOperationException("Statistics error smoke failed: " + item.Formula + " => " + result);
                    assertions++;
                }
            }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Descriptive statistics smoke assertions=" + assertions);
    }

    private static void MeasureDescriptive(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        MeasurePairedStatistics(output, context, reverse);
        var input = new FormulaArray(100000, 1);
        for (var i = 0; i < input.RowCount; i++) input[i, 0] = FormulaValue.FromNumber((i & 1) == 0 ? 999900 : 1000100);
        var registry = (ExcelFunctionRegistry)context.FunctionRegistry;
        var variance = Function(registry, "VAR.P");
        var deviation = Function(registry, "STDEV.P");
        var call = new FormulaFunctionContext(context);
        var arguments = new[] { FormulaValue.FromArray(input) };
        // Both algorithms produce the exact population variance 10,000 for this workload.
        // Input construction is excluded; validation, snapshot/list costs and clearing are included.
        if (!StatisticsNear(ListVarianceReference(input), variance.Invoke(call, arguments).AsNumber()))
            throw new InvalidOperationException("Statistics baseline/current results disagree.");
        for (var i = 0; i < 2; i++)
        {
            var optimized = reverse ? i == 1 : i == 0;
            Measure(output, optimized ? "varp_100000_scaled_snapshot" : "varp_100000_list_reference", 20,
                optimized ? () => variance.Invoke(call, arguments).AsNumber() : () => ListVarianceReference(input), 10000);
        }
        Measure(output, "stdevp_100000_scaled_snapshot", 20, () => deviation.Invoke(call, arguments).AsNumber(), 100);
        var scalar = new[] { FormulaValue.FromNumber(1), FormulaValue.FromNumber(2), FormulaValue.FromNumber(3) };
        var sample = Function(registry, "VAR.S");
        Measure(output, "vars_3_scalars", 10000, () => sample.Invoke(call, scalar).AsNumber(), 1);
    }

    private static bool StatisticsNear(double expected, double actual)
        => double.IsFinite(actual) && (expected == 0 ? Math.Abs(actual) < 1e-12 : Math.Abs((actual - expected) / expected) < 2e-12);

    // Prior-style public-value/list algorithm, retained only as a numerical-workload baseline.
    // This is not an Excel implementation or a linked previous library revision.
    private static double ListVarianceReference(FormulaArray array)
    {
        var numbers = new List<double>();
        for (var row = 0; row < array.RowCount; row++)
            for (var column = 0; column < array.ColumnCount; column++)
                if (array.IsPresent(row, column) && array[row, column].Kind == FormulaValueKind.Number)
                    numbers.Add(array[row, column].AsNumber());
        double mean = 0;
        for (var i = 0; i < numbers.Count; i++) mean += numbers[i];
        mean /= numbers.Count;
        double sum = 0;
        for (var i = 0; i < numbers.Count; i++) { var delta = numbers[i] - mean; sum += delta * delta; }
        return sum / numbers.Count;
    }
}
