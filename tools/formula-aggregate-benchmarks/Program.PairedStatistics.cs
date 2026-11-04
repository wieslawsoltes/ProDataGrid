// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidatePairedStatistics(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double Expected)[]
        {
            ("COVARIANCE.P({3,2,4,5,6},{9,7,12,15,17})", 5.2),
            ("COVARIANCE.S({2,4,8},{5,11,12})", 29d / 3),
            ("COVAR({1,2,3},{2,4,6})", 4d / 3),
            ("CORREL({1,2,3},{2,4,6})", 1), ("PEARSON({1,2,3},{6,4,2})", -1),
            ("RSQ({1,2,3},{6,4,2})", 1), ("SLOPE({3,5,7},{1,2,3})", 2),
            ("INTERCEPT({3,5,7},{1,2,3})", 1), ("STEYX({3,5,7},{1,2,3})", 0),
            ("STEYX({1,0,1},{-1,0,1})", Math.Sqrt(2d / 3)),
            ("FORECAST(4,{3,5,7},{1,2,3})", 9),
            ("FORECAST.LINEAR(30,{6,7,9,15,21},{20,28,31,38,40})", 13747d / 1296),
            ("SUM(FORECAST.LINEAR({4;5;6},{3,5,7},{1,2,3}))", 33),
            ("SLOPE({7,7,7},{1,2,3})", 0), ("FORECAST.LINEAR(1E308,{7,7,7},{1,2,3})", 7),
            ("CORREL({1,\"x\",3},{2,100,6})", 1),
            ("FORECAST.LINEAR(1E-300,{-1E300,0,1E300},{-1E300,0,1E300})", 1e-300),
            ("FORECAST.LINEAR(1E300,{-1E-300,0,1E-300},{-1E-300,0,1E-300})", 1e300),
            ("CORREL({-1E300,0,1E300},{-1E-300,0,1E-300})", 1),
            ("FORECAST.LINEAR(9007199254740992,{-1,0,1},{-1,0,1})", 9007199254740992d)
        };
        var errors = new (string Formula, FormulaErrorType Error)[]
        {
            ("CORREL({1,2},1)", FormulaErrorType.NA), ("CORREL({1,1},{2,3})", FormulaErrorType.Div0),
            ("COVARIANCE.S(1,2)", FormulaErrorType.Div0), ("STEYX({1,2},{2,3})", FormulaErrorType.Div0),
            ("FORECAST.LINEAR(4,{1,2},{2,2})", FormulaErrorType.Div0),
            ("FORECAST.LINEAR(\"bad\",{1,2},{2,3})", FormulaErrorType.Value),
            ("CORREL({1,\"x\",3},{2,#N/A,6})", FormulaErrorType.NA)
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
            foreach (var precision in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                foreach (var item in cases)
                {
                    var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (value.Kind != FormulaValueKind.Number || !StatisticsNear(item.Expected, value.AsNumber()))
                        throw new InvalidOperationException("Paired-statistics smoke failed: " + item.Formula + " => " + value);
                    assertions++;
                }
                foreach (var item in errors)
                {
                    var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (value.Kind != FormulaValueKind.Error || value.AsError().Type != item.Error)
                        throw new InvalidOperationException("Paired-statistics error smoke failed: " + item.Formula + " => " + value);
                    assertions++;
                }
            }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Paired statistics smoke assertions=" + assertions);
    }

    private static void MeasurePairedStatistics(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        var x = new FormulaArray(2048, 1);
        var y = new FormulaArray(2048, 1);
        var targets = new FormulaArray(128, 1);
        for (var i = 0; i < x.RowCount; i++)
        {
            x[i, 0] = FormulaValue.FromNumber(i);
            y[i, 0] = FormulaValue.FromNumber(2 * i + 1);
        }
        double expected = 0;
        for (var i = 0; i < targets.RowCount; i++)
        {
            targets[i, 0] = FormulaValue.FromNumber(2048 + i);
            expected += 2 * (2048 + i) + 1;
        }
        var scoped = context.WithLocalValue("trainx", FormulaValue.FromArray(x)).WithLocalValue("trainy", FormulaValue.FromArray(y))
            .WithLocalValue("targets", FormulaValue.FromArray(targets));
        var parser = new ExcelFormulaParser();
        var fitted = parser.Parse("FORECAST.LINEAR(targets,trainy,trainx)", new FormulaParseOptions());
        var repeated = parser.Parse("MAP(targets,LAMBDA(t,FORECAST.LINEAR(t,trainy,trainx)))", new FormulaParseOptions());
        var evaluator = new FormulaEvaluator();
        var resolver = new EmptyResolver();
        var first = evaluator.Evaluate(fitted, scoped, resolver).AsArray();
        var second = evaluator.Evaluate(repeated, scoped, resolver).AsArray();
        for (var i = 0; i < targets.RowCount; i++)
            if (!StatisticsNear(2 * (2048 + i) + 1, first[i, 0].AsNumber()) || !StatisticsNear(first[i, 0].AsNumber(), second[i, 0].AsNumber()))
                throw new InvalidOperationException("Forecast result arrays differ.");
        for (var i = 0; i < 2; i++)
        {
            var singleFit = reverse ? i == 1 : i == 0;
            var expression = singleFit ? fitted : repeated;
            Measure(output, singleFit ? "forecast_2048_training_128_targets_single_fit" : "forecast_2048_training_128_targets_map", 5,
                () => PairedChecksum(evaluator.Evaluate(expression, scoped, resolver).AsArray()), expected);
        }
        var call = new FormulaFunctionContext(context);
        var registry = (ExcelFunctionRegistry)context.FunctionRegistry;
        var slope = Function(registry, "SLOPE");
        var args = new[] { FormulaValue.FromArray(y), FormulaValue.FromArray(x) };
        Measure(output, "slope_2048_direct", 1000, () => slope.Invoke(call, args).AsNumber(), 2);
    }

    private static double PairedChecksum(FormulaArray result)
    {
        double sum = 0;
        for (var row = 0; row < result.RowCount; row++)
            for (var column = 0; column < result.ColumnCount; column++) sum += result[row, column].AsNumber();
        return sum;
    }
}
