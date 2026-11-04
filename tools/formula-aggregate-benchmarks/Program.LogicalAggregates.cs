// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateLogicalAggregates(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double Expected)[]
        {
            ("AVERAGEA({10;7;9;2;\"not available\"})", 5.6),
            ("AVERAGEA({TRUE;FALSE;4;\"6\"})", 1.25),
            ("AVERAGEA(TRUE,FALSE,4,\"6\")", 2.75),
            ("AVERAGEA(,2)", 1), ("AVERAGEA(\"\",2)", 1),
            ("AVERAGEA({1E308;1;-1E308})", 1d/3), ("AVERAGEA({1E308;1E308})", 1E308),
            ("MINA({FALSE;0.2;0.5;0.4;0.8})", 0),
            ("MAXA({0;0.2;0.5;0.4;TRUE})", 1),
            ("MINA(\"6\",2)", 2), ("MAXA(\"6\",2)", 6),
            ("MINA({\"6\";2})", 0), ("MAXA({\"6\";2})", 2),
            ("MINA({2;4;6})", 2), ("MAXA({-2;-4;-6})", -2),
            ("LAMBDA(x,y,AVERAGEA(x,2))(,1)", 1)
        };
        var errors = new (string Formula, FormulaErrorType Expected)[]
        {
            ("AVERAGEA()", FormulaErrorType.Value), ("MINA()", FormulaErrorType.Value),
            ("MAXA()", FormulaErrorType.Value), ("AVERAGEA(\"bad\",2)", FormulaErrorType.Value),
            ("MINA(1,1/0)", FormulaErrorType.Div0), ("MAXA(HSTACK(1,NA()))", FormulaErrorType.NA)
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in cases)
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Number || value.AsNumber() != item.Expected)
                    throw new InvalidOperationException("Logical aggregate smoke failure: " + item.Formula + " => " + value);
                assertions++;
            }
            foreach (var item in errors)
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Error || value.AsError().Type != item.Expected)
                    throw new InvalidOperationException("Logical aggregate error smoke failure: " + item.Formula + " => " + value);
                assertions++;
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Logical aggregate smoke assertions=" + assertions);
    }

    private static void MeasureLogicalAggregates(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        var call = new FormulaFunctionContext(context);
        foreach (var mixed in new[] { false, true })
        {
            var source = new FormulaArray(100000, 1);
            long total = 0;
            var count = 0;
            var min = double.PositiveInfinity; var max = double.NegativeInfinity;
            for (var i = 0; i < source.RowCount; i++)
            {
                var number = i % 1024 - 512;
                var value = FormulaValue.FromNumber(number);
                if (mixed)
                {
                    switch (i % 8)
                    {
                        case 0: source[i, 0] = FormulaValue.Blank; continue;
                        case 1: value = FormulaValue.FromBoolean(true); number = 1; break;
                        case 2: value = FormulaValue.FromBoolean(false); number = 0; break;
                        case 3: value = FormulaValue.FromText("literal zero"); number = 0; break;
                        case 4: value = FormulaValue.FromText("999"); number = 0; break;
                    }
                }
                source[i, 0] = value; total += number; count++; min = Math.Min(min, number); max = Math.Max(max, number);
            }
            var args = new[] { FormulaValue.FromArray(source) };
            foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
            {
                var function = Function((ExcelFunctionRegistry)context.FunctionRegistry, name);
                var expected = name == "AVERAGEA" ? total / (double)count : name == "MINA" ? min : max;
                if (function.Invoke(call, args).AsNumber() != expected || LogicalListReference(source, name) != expected)
                    throw new InvalidOperationException("Logical aggregate benchmark validation failed.");
                for (var pass = 0; pass < 2; pass++)
                {
                    var stream = reverse ? pass == 1 : pass == 0;
                    Measure(output, name.ToLowerInvariant() + "_100000_" + (mixed ? "mixed" : "numeric") + (stream ? "_stream" : "_list"),
                        10, () => LogicalChecksum(stream ? function.Invoke(call, args).AsNumber() : LogicalListReference(source, name)), LogicalChecksum(expected));
                }
            }
        }
    }

    // Folding both binary64 words produces an exactly summable integer checksum;
    // repeated addition of a nonintegral mean is not a reliable timing-loop assertion.
    private static double LogicalChecksum(double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        return (uint)bits ^ (uint)(bits >> 32);
    }

    // Independent converted-list reference on exact small-integer inputs. This is an
    // alternative algorithm, not an older binary or an extreme-input accuracy oracle.
    private static double LogicalListReference(FormulaArray source, string name)
    {
        var numbers = new List<double>();
        for (var row = 0; row < source.RowCount; row++)
        {
            var value = source[row, 0];
            if (value.Kind == FormulaValueKind.Blank) continue;
            numbers.Add(value.Kind == FormulaValueKind.Number ? value.AsNumber()
                : value.Kind == FormulaValueKind.Boolean && value.AsBoolean() ? 1 : 0);
        }
        double result = name == "MINA" ? double.PositiveInfinity : name == "MAXA" ? double.NegativeInfinity : 0;
        foreach (var number in numbers)
            result = name == "MINA" ? Math.Min(result, number) : name == "MAXA" ? Math.Max(result, number) : result + number;
        return name == "AVERAGEA" ? result / numbers.Count : result;
    }
}
