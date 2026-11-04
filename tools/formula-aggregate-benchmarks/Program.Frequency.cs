// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateFrequency(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double[] Expected)[]
        {
            ("FREQUENCY({79;85;78;85;50;81;95;88;97},{70;79;89})", new double[] { 1,2,4,2 }),
            ("FREQUENCY({1;2;2;3},{2;1;2})", new double[] { 2,1,0,1 }),
            ("FREQUENCY({1;2;3},{\"none\";TRUE})", new double[] { 3 }),
            ("FREQUENCY({\"none\";FALSE},{1;2})", new double[] { 0,0,0 }),
            ("FREQUENCY({-1E308;0;1E308},{-1E308;1E308})", new double[] { 1,2,0 }),
            ("FREQUENCY({1,2;3,4},{3,2})", new double[] { 1,2,1 })
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in cases)
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Array) throw new InvalidOperationException("Frequency smoke type failure: " + item.Formula);
                var array = value.AsArray();
                if (array.RowCount != item.Expected.Length || array.ColumnCount != 1) throw new InvalidOperationException("Frequency smoke shape failure.");
                for (var i = 0; i < item.Expected.Length; i++)
                    if (array[i, 0].AsNumber() != item.Expected[i]) throw new InvalidOperationException("Frequency smoke result failure.");
                assertions++;
            }
            foreach (var formula in new[] { "FREQUENCY(NA(),1)", "FREQUENCY(1,NA())" })
            {
                var value = evaluator.Evaluate(parser.Parse(formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Error || value.AsError().Type != FormulaErrorType.NA)
                    throw new InvalidOperationException("Frequency smoke error propagation failure.");
                assertions++;
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Frequency smoke assertions=" + assertions);
    }

    private static void MeasureFrequency(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        var function = Function((ExcelFunctionRegistry)context.FunctionRegistry, "FREQUENCY");
        var call = new FormulaFunctionContext(context);
        var data = new FormulaArray(100000, 1);
        for (var i = 0; i < data.RowCount; i++) data[i, 0] = FormulaValue.FromNumber((i * 7919L % 104729) - 1000);
        foreach (var binCount in new[] { 4, 128, 512 })
        {
            var bins = new FormulaArray(binCount, 1);
            for (var i = 0; i < binCount; i++)
                bins[i, 0] = FormulaValue.FromNumber(((i * 3) % binCount) * 100000d / binCount);
            var arguments = new[] { FormulaValue.FromArray(data), FormulaValue.FromArray(bins) };
            var reference = FrequencyLinearReference(data, bins);
            var actual = function.Invoke(call, arguments).AsArray();
            for (var i = 0; i < reference.RowCount; i++)
                if (reference[i, 0] != actual[i, 0]) throw new InvalidOperationException("Frequency full-result comparison failed.");
            var expected = FrequencyChecksum(reference);
            for (var pass = 0; pass < 2; pass++)
            {
                var indexed = reverse ? pass == 1 : pass == 0;
                Measure(output, "frequency_100000_" + binCount + (indexed ? "_indexed" : "_linear"), 3,
                    () => FrequencyChecksum(indexed ? function.Invoke(call, arguments).AsArray() : FrequencyLinearReference(data, bins)), expected);
            }
        }
    }

    // Independent O(n*b) assignment over evaluated numeric arrays. It owns the same
    // vertical output but needs no sort/index. Input preparation is excluded for both.
    private static FormulaArray FrequencyLinearReference(FormulaArray data, FormulaArray bins)
    {
        var result = new FormulaArray(bins.RowCount + 1, 1);
        for (var i = 0; i < result.RowCount; i++) result[i, 0] = FormulaValue.FromNumber(0);
        for (var row = 0; row < data.RowCount; row++)
        {
            var value = data[row, 0].AsNumber();
            var selected = bins.RowCount;
            var selectedBoundary = double.PositiveInfinity;
            for (var i = 0; i < bins.RowCount; i++)
            {
                var boundary = bins[i, 0].AsNumber();
                if (value <= boundary && (selected == bins.RowCount || boundary < selectedBoundary))
                {
                    selected = i;
                    selectedBoundary = boundary;
                }
            }
            result[selected, 0] = FormulaValue.FromNumber(result[selected, 0].AsNumber() + 1);
        }
        return result;
    }

    private static double FrequencyChecksum(FormulaArray array)
    {
        double sum = 0;
        for (var i = 0; i < array.RowCount; i++) sum += array[i, 0].AsNumber() * (i + 1d);
        return sum;
    }
}
