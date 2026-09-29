// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateRanking(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Text, double Expected)[]
        {
            ("RANK(2,{1,2,3})", 2), ("RANK.EQ(1,{1,2,3},0.5)", 1),
            ("RANK.AVG(2,{1,2,2,3})", 2.5), ("RANK.EQ(1,{1,2,3},-2)", 1),
            ("PERCENTRANK({1,2,3},2)", 0.5),
            ("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},5.43)", 0.381),
            ("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},5.43,1)", 0.3),
            ("PERCENTRANK.INC({1;2;3;6;6;6;7;8;9},6)", 0.375),
            ("PERCENTRANK.INC({1;2;3;6;6;6;7;8;9},6.5,4)", 0.6875),
            ("PERCENTRANK.INC({-1E308,1E308},0)", 0.5),
            ("PERCENTRANK.INC({1,2,3},1.58,2)", 0.29),
            ("SUM(RANK.EQ({1;2;3},{1,2,3},{0,1}))", 12),
            ("SUM(PERCENTRANK.EXC({1,2,3},{1;2;3},{1,3}))", 2.9),
            ("LAMBDA(sig,unused,PERCENTRANK.INC({1,2,3},2,sig))(,1)", 0.5)
        };
        var errors = new (string Text, FormulaErrorType Error)[]
        {
            ("RANK.EQ(5,{1,2,3})", FormulaErrorType.NA),
            ("RANK.AVG(2,{1,3})", FormulaErrorType.NA),
            ("PERCENTRANK.EXC({1,2,3},0)", FormulaErrorType.NA),
            ("PERCENTRANK.INC({1,2,3},4)", FormulaErrorType.NA),
            ("PERCENTRANK.INC({1,2,3},2,0)", FormulaErrorType.Num),
            ("PERCENTRANK.INC({\"a\",TRUE},2)", FormulaErrorType.Num)
        };
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in cases)
            {
                var result = evaluator.Evaluate(parser.Parse(item.Text, new FormulaParseOptions()), context, resolver);
                if (result.Kind != FormulaValueKind.Number || Math.Abs(result.AsNumber() - item.Expected) > 1e-13)
                    throw new InvalidOperationException("Ranking smoke: " + item.Text + " => " + result);
            }
            foreach (var item in errors)
            {
                var result = evaluator.Evaluate(parser.Parse(item.Text, new FormulaParseOptions()), context, resolver);
                if (result.Kind != FormulaValueKind.Error || result.AsError().Type != item.Error)
                    throw new InvalidOperationException("Ranking error smoke: " + item.Text + " => " + result);
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Ranking smoke assertions=" + 2 * (cases.Length + errors.Length));
    }

    private static void MeasureRanking(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        const int count = 20000;
        var data = new FormulaArray(count, 1);
        var targets = new FormulaArray(128, 1);
        for (var i = 0; i < count; i++) data[i, 0] = FormulaValue.FromNumber((i * 7919) % count);
        for (var i = 0; i < targets.RowCount; i++) targets[i, 0] = FormulaValue.FromNumber(i * 137);
        var scoped = context.WithLocalValue("rankData", FormulaValue.FromArray(data)).WithLocalValue("rankTargets", FormulaValue.FromArray(targets));
        var parser = new ExcelFormulaParser();
        var evaluator = new FormulaEvaluator();
        var resolver = new EmptyResolver();
        var direct = parser.Parse("RANK.EQ(rankTargets,rankData)", new FormulaParseOptions());
        var mapped = parser.Parse("MAP(rankTargets,LAMBDA(x,RANK.EQ(x,rankData)))", new FormulaParseOptions());
        var left = evaluator.Evaluate(direct, scoped, resolver).AsArray();
        var right = evaluator.Evaluate(mapped, scoped, resolver).AsArray();
        for (var i = 0; i < targets.RowCount; i++)
            if (left[i, 0] != right[i, 0] || left[i, 0].AsNumber() != count - targets[i, 0].AsNumber())
                throw new InvalidOperationException("Rank batch and reference disagree.");
        var expected = RankingChecksum(left);
        for (var pass = 0; pass < 2; pass++)
        {
            var batch = reverse ? pass == 1 : pass == 0;
            var expression = batch ? direct : mapped;
            Measure(output, batch ? "rank_20000_128_targets_batch" : "rank_20000_128_targets_map", 4,
                () => RankingChecksum(evaluator.Evaluate(expression, scoped, resolver).AsArray()), expected);
        }
        var rank = Function((ExcelFunctionRegistry)context.FunctionRegistry, "RANK.EQ");
        var call = new FormulaFunctionContext(context);
        var arguments = new[] { FormulaValue.FromNumber(10000), FormulaValue.FromArray(data) };
        Measure(output, "rank_20000_scalar_scan", 50, () => rank.Invoke(call, arguments).AsNumber(), 10000);
        Measure(output, "rank_20000_prior_list_reference", 50, () => PriorListRank(data, 10000), 10000);
        var percent = Function((ExcelFunctionRegistry)context.FunctionRegistry, "PERCENTRANK.INC");
        var percentileArguments = new[] { FormulaValue.FromArray(data), FormulaValue.FromArray(targets) };
        var percentileResult = percent.Invoke(call, percentileArguments).AsArray();
        for (var i = 0; i < targets.RowCount; i++)
        {
            var exact = ((long)(i * 137) * 1000 / (count - 1)) / 1000d;
            if (percentileResult[i, 0].AsNumber() != exact) throw new InvalidOperationException("Percentile rank oracle mismatch.");
        }
        var percentileExpected = PercentRankingChecksum(percentileResult);
        Measure(output, "percentrank_20000_128_targets_batch", 10,
            () => PercentRankingChecksum(percent.Invoke(call, percentileArguments).AsArray()), percentileExpected);
    }

    private static double RankingChecksum(FormulaArray values)
    {
        double sum = 0;
        for (var row = 0; row < values.RowCount; row++)
            for (var column = 0; column < values.ColumnCount; column++) sum += values[row, column].AsNumber();
        return sum;
    }

    private static double PercentRankingChecksum(FormulaArray values)
    {
        double sum = 0;
        for (var row = 0; row < values.RowCount; row++)
            for (var col = 0; col < values.ColumnCount; col++) sum += Math.Round(values[row, col].AsNumber() * 1000);
        return sum;
    }

    private static double PriorListRank(FormulaArray data, double target)
    {
        // Retained numeric algorithm corresponding to the former list-based rank path.
        // The input is deliberately all numeric; no external conversion/copy cost is charged.
        var values = new List<double>();
        foreach (var value in data.Flatten()) if (value.Kind == FormulaValueKind.Number) values.Add(value.AsNumber());
        var rank = 1;
        foreach (var value in values) if (value > target) rank++;
        return rank;
    }
}
