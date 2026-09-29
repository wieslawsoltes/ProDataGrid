// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var directory = args.Length > 0 ? args[0] : "artifacts/data-formulas";
            Directory.CreateDirectory(directory);
            var workbook = new BenchmarkWorkbook();
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaEvaluationContext(workbook, workbook.Worksheets[0], new FormulaCellAddress("Sheet1", 1, 1), registry);
            ValidateText(context);
            ValidateAdditional(context);
            var names = new List<string>();
            foreach (var function in registry.GetAll()) names.Add(function.Name);
            names.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(directory, "function-catalog.txt"), names);
            Console.WriteLine("Registered names=" + names.Count);
            if (args.Length > 1 && args[1] == "smoke") return 0;
            var output = new StringBuilder();
            output.AppendLine("# runtime=" + RuntimeInformation.FrameworkDescription);
            output.AppendLine("# os=" + RuntimeInformation.OSDescription);
            output.AppendLine("# architecture=" + RuntimeInformation.ProcessArchitecture);
            output.AppendLine("# tiered_compilation=" + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default"));
            output.AppendLine("# warm_batches=2; measured_batches=7; input_creation_and_parsing=excluded; output_and_workspace=included; precision=false");
            output.AppendLine("scenario,sample,iterations,microseconds_per_operation,bytes_per_operation");
            var reverse = args.Length > 1 && args[1] == "reverse";
            MeasureText(output, context, reverse);
            MeasureAdditional(output, context, reverse);
            File.WriteAllText(Path.Combine(directory, "measurements.csv"), output.ToString());
            Console.Write(output.ToString());
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static partial void ValidateAdditional(FormulaEvaluationContext context);
    static partial void MeasureAdditional(StringBuilder output, FormulaEvaluationContext context, bool reverse);

    private static void ValidateText(FormulaEvaluationContext context)
    {
        var parser = new ExcelFormulaParser();
        var evaluator = new FormulaEvaluator();
        var resolver = new EmptyResolver();
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in new (string Formula, int Rows, int Columns, string Fields)[] {
                ("TEXTSPLIT(\"a,b;c,d\",\",\",\";\")", 2, 2, "a|b|c|d"),
                ("TEXTSPLIT(\"a.b-c\",{\".\",\"-\"})", 1, 3, "a|b|c"),
                ("TEXTSPLIT(\"a;b;c\",,\";\")", 3, 1, "a|b|c"),
                ("TEXTSPLIT(\",a,,b,\",\",\",,TRUE)", 1, 2, "a|b"),
                ("TEXTSPLIT(\"aXbxc\",\"x\",,,1)", 1, 3, "a|b|c"),
                ("TEXTSPLIT(\"a,b;c\",\",\",\";\",,,\"pad\")", 2, 2, "a|b|c|pad"),
                ("TEXTSPLIT(\"a😀b\",\"😀\")", 1, 2, "a|b") })
            {
                var array = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver).AsArray();
                var fields = new List<string>();
                foreach (var value in array.Flatten()) fields.Add(value.AsText());
                if (array.RowCount != item.Rows || array.ColumnCount != item.Columns || string.Join("|", fields) != item.Fields)
                    throw new InvalidOperationException("TEXTSPLIT smoke mismatch: " + item.Formula);
                assertions++;
            }
            foreach (var item in new (string Formula, FormulaErrorType Error)[] {
                ("TEXTSPLIT(NA(),\",\")", FormulaErrorType.NA),
                ("TEXTSPLIT(\"x\",\",\",,,2)", FormulaErrorType.Value),
                ("TEXTSPLIT(\"\",\",\")", FormulaErrorType.Calc),
                ("TEXTSPLIT(\",,,\",\",\",,TRUE)", FormulaErrorType.Calc) })
            {
                var result = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (result.Kind != FormulaValueKind.Error || result.AsError().Type != item.Error)
                    throw new InvalidOperationException("TEXTSPLIT error mismatch: " + item.Formula);
                assertions++;
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Text split smoke assertions=" + assertions);
    }

    private static void MeasureText(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        if (!context.FunctionRegistry.TryGetFunction("TEXTSPLIT", out var function)) throw new InvalidOperationException("Missing TEXTSPLIT.");
        var call = new FormulaFunctionContext(context);
        var input = new StringBuilder();
        for (var row = 0; row < 1000; row++)
        {
            if (row > 0) input.Append(';');
            for (var column = 0; column <= row % 10; column++) { if (column > 0) input.Append(','); input.Append("value"); }
        }
        var text = input.ToString();
        var arguments = new[] { FormulaValue.FromText(text), FormulaValue.FromText(","), FormulaValue.FromText(";"),
            FormulaValue.FromBoolean(false), FormulaValue.FromNumber(0), FormulaValue.FromText("") };
        var reference = ReferenceSplit(text);
        var checkedArray = function.Invoke(call, arguments).AsArray();
        if (checkedArray.RowCount != reference.RowCount || checkedArray.ColumnCount != reference.ColumnCount)
            throw new InvalidOperationException("Split benchmark dimensions mismatch.");
        for (var row = 0; row < reference.RowCount; row++) for (var col = 0; col < reference.ColumnCount; col++)
            if (checkedArray[row, col] != reference[row, col]) throw new InvalidOperationException("Split benchmark full-result mismatch.");
        var expected = TextLength(reference);
        for (var order = 0; order < 2; order++)
        {
            var optimized = reverse ? order == 1 : order == 0;
            Measure(output, optimized ? "textsplit_ragged_1000_rows" : "string_split_reference_1000_rows", 30,
                () => TextLength(optimized ? function.Invoke(call, arguments).AsArray() : ReferenceSplit(text)), expected);
        }
        var many = new StringBuilder();
        for (var i = 0; i < 5000; i++) { if (i > 0) many.Append(','); many.Append("value"); }
        var delimiters = new FormulaArray(2, 1);
        delimiters[0, 0] = FormulaValue.FromText(",");
        delimiters[1, 0] = FormulaValue.FromText("absent delimiter");
        var multiArgs = new[] { FormulaValue.FromText(many.ToString()), FormulaValue.FromArray(delimiters) };
        Measure(output, "textsplit_5000_fields_frequent_and_absent_delimiters", 30,
            () => TextLength(function.Invoke(call, multiArgs).AsArray()), 25000);
    }

    private static FormulaArray ReferenceSplit(string source)
    {
        var rows = source.Split(';');
        var fields = new string[rows.Length][];
        var width = 0;
        for (var i = 0; i < rows.Length; i++) { fields[i] = rows[i].Split(','); width = Math.Max(width, fields[i].Length); }
        var result = new FormulaArray(rows.Length, width);
        for (var row = 0; row < rows.Length; row++) for (var col = 0; col < width; col++)
            result[row, col] = FormulaValue.FromText(col < fields[row].Length ? fields[row][col] : "");
        return result;
    }
    private static double TextLength(FormulaArray values)
    {
        var total = 0;
        for (var row = 0; row < values.RowCount; row++) for (var col = 0; col < values.ColumnCount; col++) total += values[row, col].AsText().Length;
        return total;
    }

    private static void Measure(StringBuilder output, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++) for (var i = 0; i < iterations; i++)
            if (operation() != expected) throw new InvalidOperationException(name + " warmup mismatch.");
        var times = new double[7];
        var bytes = new double[7];
        for (var sample = 0; sample < 7; sample++)
        {
            double checksum = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++) checksum += operation();
            var elapsed = Stopwatch.GetTimestamp() - start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (checksum != expected * iterations) throw new InvalidOperationException(name + " measured mismatch.");
            times[sample] = elapsed * 1000000d / Stopwatch.Frequency / iterations;
            bytes[sample] = allocated / (double)iterations;
            Write(output, name, sample.ToString(CultureInfo.InvariantCulture), iterations, times[sample], bytes[sample]);
        }
        Array.Sort(times); Array.Sort(bytes);
        Write(output, name, "median", iterations, times[3], bytes[3]);
    }
    private static void Write(StringBuilder output, string name, string sample, int iterations, double time, double bytes)
        => output.Append(name).Append(',').Append(sample).Append(',').Append(iterations.ToString(CultureInfo.InvariantCulture)).Append(',')
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
        public IFormulaCell GetCell(int row, int column) => throw new NotSupportedException("This benchmark uses evaluated values.");
        public bool TryGetCell(int row, int column, out IFormulaCell cell) { cell = null!; return false; }
    }
    private sealed class EmptyResolver : IFormulaValueResolver
    {
        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value) { value = FormulaValue.Blank; return false; }
        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value) { value = FormulaValue.Blank; return false; }
    }
}
