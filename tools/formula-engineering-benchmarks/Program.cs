// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var outputDirectory = args.Length > 0 ? args[0] : "artifacts/formula-engineering";
            Directory.CreateDirectory(outputDirectory);
            var workbook = new BenchmarkWorkbook();
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaEvaluationContext(workbook, workbook.Worksheets[0],
                new FormulaCellAddress("Sheet1", 1, 1), registry);
            var evaluator = new FormulaEvaluator();
            var parser = new ExcelFormulaParser();
            var resolver = new EmptyResolver();
            Smoke(context, evaluator, parser, resolver);
            var catalog = new List<string>();
            foreach (var function in registry.GetAll()) catalog.Add(function.Name);
            catalog.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(outputDirectory, "function-catalog.txt"), catalog);
            if (args.Length > 1 && args[1] == "smoke") return 0;

            var numbers = new FormulaArray(10000, 1);
            double expected = 0;
            for (var i = 0; i < numbers.RowCount; i++)
            {
                numbers[i, 0] = FormulaValue.FromNumber(i);
                expected += i & 255;
            }
            context = context.WithLocalValue("input", FormulaValue.FromArray(numbers));
            var projected = parser.Parse("BITAND(input,255)", new FormulaParseOptions());
            var mapped = parser.Parse("MAP(input,LAMBDA(x,BITAND(x,255)))", new FormulaParseOptions());
            foreach (var expression in new[] { projected, mapped })
            {
                var result = evaluator.Evaluate(expression, context, resolver).AsArray();
                for (var i = 0; i < result.RowCount; i++)
                    if (result[i, 0].AsNumber() != (i & 255)) throw new InvalidOperationException("Array oracle mismatch.");
            }
            var csv = new StringBuilder();
            csv.AppendLine("# runtime=" + RuntimeInformation.FrameworkDescription);
            csv.AppendLine("# os=" + RuntimeInformation.OSDescription);
            csv.AppendLine("# architecture=" + RuntimeInformation.ProcessArchitecture);
            csv.AppendLine("# processors=" + Environment.ProcessorCount);
            csv.AppendLine("# tiered_compilation=" + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default"));
            csv.AppendLine("# precision=false; input_creation_and_parsing=excluded; result_construction_and_full_checksum=included");
            csv.AppendLine("# warm_batches=2; measured_batches=7; formula_comparison_not_old_new_engine_comparison");
            csv.AppendLine("scenario,sample,iterations,microseconds_per_operation,bytes_per_operation");
            var reverse = args.Length > 1 && args[1] == "reverse";
            if (reverse)
            {
                Measure(csv, "map_bitand_10000", 20, () => Checksum(evaluator.Evaluate(mapped, context, resolver)), expected);
                Measure(csv, "projected_bitand_10000", 20, () => Checksum(evaluator.Evaluate(projected, context, resolver)), expected);
            }
            else
            {
                Measure(csv, "projected_bitand_10000", 20, () => Checksum(evaluator.Evaluate(projected, context, resolver)), expected);
                Measure(csv, "map_bitand_10000", 20, () => Checksum(evaluator.Evaluate(mapped, context, resolver)), expected);
            }
            var call = new FormulaFunctionContext(context);
            var bitAnd = Function(registry, "BITAND");
            var bitArgs = new[] { FormulaValue.FromNumber(13), FormulaValue.FromNumber(25) };
            Measure(csv, "bitand_scalar_direct", 100000, () => bitAnd.Invoke(call, bitArgs).AsNumber(), 9);
            var decimalFunction = Function(registry, "DECIMAL");
            var shortArgs = new[] { FormulaValue.FromText("1FFFFFFFFFFFFF"), FormulaValue.FromNumber(16) };
            Measure(csv, "decimal_53bit_text_direct", 10000, () => decimalFunction.Invoke(call, shortArgs).AsNumber(), 9007199254740991d);
            var longArgs = new[] { FormulaValue.FromText("1" + new string('0', 120)), FormulaValue.FromNumber(16) };
            Measure(csv, "decimal_481bit_text_direct", 1000, () => decimalFunction.Invoke(call, longArgs).AsNumber(), Math.ScaleB(1, 480));
            var baseFunction = Function(registry, "BASE");
            var baseArgs = new[] { FormulaValue.FromNumber(9007199254740991d), FormulaValue.FromNumber(16) };
            if (baseFunction.Invoke(call, baseArgs).AsText() != "1FFFFFFFFFFFFF") throw new InvalidOperationException("BASE oracle mismatch.");
            Measure(csv, "base_53bit_direct", 10000, () => TextChecksum(baseFunction.Invoke(call, baseArgs).AsText()), TextChecksum("1FFFFFFFFFFFFF"));
            File.WriteAllText(Path.Combine(outputDirectory, "measurements.csv"), csv.ToString());
            Console.Write(csv.ToString());
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static IFormulaFunction Function(ExcelFunctionRegistry registry, string name)
        => registry.TryGetFunction(name, out var function) ? function : throw new InvalidOperationException("Missing " + name);

    private static void Smoke(FormulaEvaluationContext context, FormulaEvaluator evaluator, ExcelFormulaParser parser, IFormulaValueResolver resolver)
    {
        var cases = new (string Formula, string Expected)[]
        {
            ("BITAND(13,25)", "9"), ("BITOR(9,12)", "13"), ("BITXOR(5,6)", "3"),
            ("BITLSHIFT(4,2)", "16"), ("BITRSHIFT(13,2)", "3"),
            ("BIN2DEC(111)", "7"), ("OCT2DEC(777)", "511"), ("HEX2DEC(\"FF\")", "255"),
            ("DEC2BIN(-100)", "1110011100"), ("DEC2OCT(58,3)", "072"), ("DEC2HEX(-54)", "FFFFFFFFCA"),
            ("BIN2HEX(11111011,4)", "00FB"), ("BIN2OCT(1001,3)", "011"),
            ("OCT2BIN(777)", "111111111"), ("OCT2HEX(100,4)", "0040"),
            ("HEX2BIN(\"FFFFFFFE00\")", "1000000000"), ("HEX2OCT(\"FFFFFFFF00\")", "7777777400"),
            ("BASE(15,2,10)", "0000001111"), ("DECIMAL(\"zap\",36)", "45745"),
            ("DEC2HEX(15,)", "F"), ("BITLSHIFT(1,48)", "#NUM!"),
            ("LET(v,DEC2HEX({1;2;3}),SUM(HEX2DEC(v)))", "6")
        };
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in cases)
            {
                var result = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                var text = result.Kind == FormulaValueKind.Number
                    ? result.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : result.ToString();
                if (text != item.Expected) throw new InvalidOperationException(item.Formula + ": expected " + item.Expected + ", got " + text);
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Engineering smoke assertions passed: " + (cases.Length * 2));
    }

    private static double Checksum(FormulaValue value)
    {
        var array = value.AsArray();
        double sum = 0;
        for (var row = 0; row < array.RowCount; row++)
            for (var col = 0; col < array.ColumnCount; col++) sum += array[row, col].AsNumber();
        return sum;
    }

    private static double TextChecksum(string text)
    {
        double sum = 0;
        foreach (var ch in text) sum += ch;
        return sum;
    }

    private static void Measure(StringBuilder csv, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++)
            for (var i = 0; i < iterations; i++)
                if (operation() != expected) throw new InvalidOperationException(name + " warmup correctness failure.");
        var times = new double[7];
        var allocations = new double[7];
        for (var sample = 0; sample < 7; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            double checksum = 0;
            for (var i = 0; i < iterations; i++) checksum += operation();
            var elapsed = Stopwatch.GetTimestamp() - start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            // Compare with sequential addition, not expected*iterations: a large exact
            // scalar may round differently when aggregated by those two expressions.
            double referenceChecksum = 0;
            for (var i = 0; i < iterations; i++) referenceChecksum += expected;
            if (checksum != referenceChecksum) throw new InvalidOperationException(name + " measured checksum failure.");
            times[sample] = elapsed * 1000000d / Stopwatch.Frequency / iterations;
            allocations[sample] = allocated / (double)iterations;
            Write(csv, name, sample.ToString(CultureInfo.InvariantCulture), iterations, times[sample], allocations[sample]);
        }
        Array.Sort(times); Array.Sort(allocations);
        Write(csv, name, "median", iterations, times[3], allocations[3]);
    }

    private static void Write(StringBuilder csv, string name, string sample, int count, double time, double bytes)
        => csv.Append(name).Append(',').Append(sample).Append(',').Append(count.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(time.ToString("R", CultureInfo.InvariantCulture)).Append(',').AppendLine(bytes.ToString("R", CultureInfo.InvariantCulture));

    private sealed class BenchmarkWorkbook : IFormulaWorkbook
    {
        public BenchmarkWorkbook() { Worksheets = new IFormulaWorksheet[] { new BenchmarkWorksheet(this) }; }
        public string Name => "Benchmark";
        public IReadOnlyList<IFormulaWorksheet> Worksheets { get; }
        public FormulaCalculationSettings Settings { get; } = new FormulaCalculationSettings();
        public IFormulaWorksheet GetWorksheet(string name) => name == "Sheet1" ? Worksheets[0] : throw new ArgumentException("Unknown sheet.", nameof(name));
    }

    private sealed class BenchmarkWorksheet : IFormulaWorksheet
    {
        public BenchmarkWorksheet(IFormulaWorkbook workbook) { Workbook = workbook; }
        public string Name => "Sheet1";
        public IFormulaWorkbook Workbook { get; }
        public IFormulaCell GetCell(int row, int column) => throw new NotSupportedException("No cells are used in this benchmark.");
        public bool TryGetCell(int row, int column, out IFormulaCell cell) { cell = null!; return false; }
    }

    private sealed class EmptyResolver : IFormulaValueResolver
    {
        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value) { value = FormulaValue.Blank; return false; }
        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value) { value = FormulaValue.Blank; return false; }
    }
}
