// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateModes(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double[] Expected)[]
        {
            ("MODE.MULT({1;2;3;4;3;2;1;2;3;5;6;1})", new double[] { 1, 2, 3 }),
            ("MODE.MULT({9;3;3;9})", new double[] { 9, 3 }),
            ("MODE.SNGL({9;3;3;9})", new double[] { 9 }),
            ("MODE({5.6;4;4;3;2;4})", new double[] { 4 }),
            ("MODE.MULT({0;0;1})", new double[] { 0 }),
            ("MODE.MULT({TRUE;1;1;\"1\";2})", new double[] { 1 }),
            ("MODE.MULT(1,\"1\",2,2)", new double[] { 1, 2 }),
            ("SUM(MODE.MULT({9;3;3;9}))", new double[] { 12 }),
            ("MODE.MULT(FALSE,0)", new double[] { 0 })
        };
        var errors = new (string Formula, FormulaErrorType Error)[]
        {
            ("MODE.MULT({1;2;3})", FormulaErrorType.NA),
            ("MODE.SNGL(42)", FormulaErrorType.NA),
            ("MODE(1,1,1/0)", FormulaErrorType.Div0),
            ("MODE.MULT(1,1,NA())", FormulaErrorType.NA),
            ("MODE.MULT(1,\"bad\")", FormulaErrorType.Value)
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in cases)
            {
                var actual = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                var array = actual.Kind == FormulaValueKind.Array ? actual.AsArray() : null;
                if ((array?.RowCount ?? 1) != item.Expected.Length || (array?.ColumnCount ?? 1) != 1)
                    throw new InvalidOperationException("Mode smoke shape failure.");
                for (var i = 0; i < item.Expected.Length; i++)
                    if ((array == null ? actual : array[i, 0]).AsNumber() != item.Expected[i])
                        throw new InvalidOperationException("Mode smoke result failure: " + item.Formula);
                assertions++;
            }
            foreach (var item in errors)
            {
                var actual = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (actual.Kind != FormulaValueKind.Error || actual.AsError().Type != item.Error)
                    throw new InvalidOperationException("Mode smoke error failure: " + item.Formula);
                assertions++;
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Mode smoke assertions=" + assertions);
    }

    private static void MeasureModes(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        var single = Function((ExcelFunctionRegistry)context.FunctionRegistry, "MODE.SNGL");
        var multiple = Function((ExcelFunctionRegistry)context.FunctionRegistry, "MODE.MULT");
        var call = new FormulaFunctionContext(context);
        foreach (var distinct in new[] { 32, 4096, 100000 })
        {
            var source = new FormulaArray(100000, 1);
            for (var i = 0; i < source.RowCount; i++) source[i, 0] = FormulaValue.FromNumber((i * 7919L % distinct) - 1000);
            var args = new[] { FormulaValue.FromArray(source) };
            foreach (var all in new[] { false, true })
            {
                var function = all ? multiple : single;
                var reference = ModeListReference(source, all);
                var actual = function.Invoke(call, args);
                CheckModes(reference, actual);
                var checksum = ModeChecksum(reference);
                for (var pass = 0; pass < 2; pass++)
                {
                    var pooled = reverse ? pass == 1 : pass == 0;
                    Measure(output, "mode_100000_" + distinct + (all ? "_multi" : "_single") + (pooled ? "_pooled" : "_list_dictionary"),
                        5, () => ModeChecksum(pooled ? function.Invoke(call, args) : ModeListReference(source, all)), checksum);
                }
            }
        }
    }

    // Prior-style materialized numeric list plus dictionary counting. This mirrors the
    // old single-mode algorithm; the multi-mode reference extends it with an owned result.
    private static FormulaValue ModeListReference(FormulaArray source, bool multiple)
    {
        var numbers = new List<double>();
        for (var row = 0; row < source.RowCount; row++) numbers.Add(source[row, 0].AsNumber());
        var counts = new Dictionary<double, int>();
        foreach (var number in numbers) { counts.TryGetValue(number, out var count); counts[number] = count + 1; }
        var peak = 1;
        var modes = 0;
        var first = 0d;
        foreach (var entry in counts)
        {
            if (entry.Value > peak) { peak = entry.Value; first = entry.Key; modes = 1; }
            else if (entry.Value == peak) modes++;
        }
        if (peak < 2) return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
        if (!multiple) return FormulaValue.FromNumber(first);
        var result = new FormulaArray(modes, 1);
        var index = 0;
        foreach (var entry in counts) if (entry.Value == peak) result[index++, 0] = FormulaValue.FromNumber(entry.Key);
        return FormulaValue.FromArray(result);
    }

    private static void CheckModes(FormulaValue expected, FormulaValue actual)
    {
        if (expected.Kind != actual.Kind) throw new InvalidOperationException("Mode comparison kind failure.");
        if (expected.Kind != FormulaValueKind.Array)
        {
            if (expected != actual) throw new InvalidOperationException("Mode comparison result failure.");
            return;
        }
        var left = expected.AsArray(); var right = actual.AsArray();
        if (left.RowCount != right.RowCount || left.ColumnCount != right.ColumnCount) throw new InvalidOperationException("Mode comparison shape failure.");
        for (var i = 0; i < left.RowCount; i++) if (left[i, 0] != right[i, 0]) throw new InvalidOperationException("Mode comparison value failure.");
    }

    private static double ModeChecksum(FormulaValue value)
    {
        if (value.Kind == FormulaValueKind.Error)
        {
            if (value.AsError().Type != FormulaErrorType.NA) throw new InvalidOperationException("Unexpected mode error.");
            return -1;
        }
        if (value.Kind == FormulaValueKind.Number) return value.AsNumber();
        var array = value.AsArray();
        double checksum = 0;
        for (var i = 0; i < array.RowCount; i++) checksum += array[i, 0].AsNumber() * (i + 1);
        return checksum;
    }
}
