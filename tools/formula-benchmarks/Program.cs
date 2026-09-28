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

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var workbook = new BenchmarkWorkbook();
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaEvaluationContext(workbook, workbook.Worksheets[0], new FormulaCellAddress("Sheet1", 1, 1), registry);
            var functionContext = new FormulaFunctionContext(context);
            var evaluator = new FormulaEvaluator();
            var resolver = new EmptyResolver();
            var literal = new FormulaLiteralExpression(FormulaValue.FromNumber(42));
            FormulaExpression arithmetic = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            for (var i = 0; i < 128; i++)
                arithmetic = new FormulaBinaryExpression(FormulaBinaryOperator.Add, arithmetic, new FormulaLiteralExpression(FormulaValue.FromNumber(1)));

            var uniqueData = new FormulaArray(5000, 1);
            for (var i = 0; i < 5000; i++) uniqueData[i, 0] = FormulaValue.FromNumber(i);
            var lookupData = new FormulaArray(100000, 1);
            for (var i = 0; i < 100000; i++) lookupData[i, 0] = FormulaValue.FromNumber(i);
            if (!registry.TryGetFunction("UNIQUE", out var unique) || !registry.TryGetFunction("XMATCH", out var xmatch))
                throw new InvalidOperationException("Benchmark requires UNIQUE and XMATCH.");
            var uniqueArgs = new[] { FormulaValue.FromArray(uniqueData) };
            var lookupArgs = new[] { FormulaValue.FromNumber(99999), FormulaValue.FromArray(lookupData), FormulaValue.FromNumber(0), FormulaValue.FromNumber(2) };
            var output = new StringBuilder();
            output.AppendLine("# label=" + (args.Length > 1 ? args[1] : "current"));
            output.AppendLine("# runtime=" + RuntimeInformation.FrameworkDescription);
            output.AppendLine("# os=" + RuntimeInformation.OSDescription);
            output.AppendLine("# architecture=" + RuntimeInformation.ProcessArchitecture);
            output.AppendLine("# logical_processors=" + Environment.ProcessorCount);
            output.AppendLine("# number_precision=false; warm_batches=2; measured_batches=7");
            output.AppendLine("scenario,sample,iterations,microseconds_per_operation,bytes_per_operation");
            Measure(output, "cached_literal", 500000, () => evaluator.Evaluate(literal, context, resolver).AsNumber(), 42);
            Measure(output, "cached_128_additions", 5000, () => evaluator.Evaluate(arithmetic, context, resolver).AsNumber(), 129);
            Measure(output, "xmatch_binary_100000", 32, () => xmatch.Invoke(functionContext, lookupArgs).AsNumber(), 100000);
            Measure(output, "unique_distinct_5000", 1, () => unique.Invoke(functionContext, uniqueArgs).AsArray().RowCount, 5000);
            Console.Write(output.ToString());
            if (args.Length > 0)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(args[0]));
                if (directory != null) Directory.CreateDirectory(directory);
                File.WriteAllText(args[0], output.ToString());
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Measure(StringBuilder output, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++)
            for (var i = 0; i < iterations; i++)
                if (operation() != expected) throw new InvalidOperationException(name + " failed warmup correctness check.");
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
            if (checksum != expected * iterations) throw new InvalidOperationException(name + " failed measured correctness check.");
            times[sample] = elapsed * 1000000d / Stopwatch.Frequency / iterations;
            allocations[sample] = allocated / (double)iterations;
            WriteSample(output, name, sample.ToString(CultureInfo.InvariantCulture), iterations, times[sample], allocations[sample]);
        }
        Array.Sort(times);
        Array.Sort(allocations);
        WriteSample(output, name, "median", iterations, times[3], allocations[3]);
    }

    private static void WriteSample(StringBuilder output, string name, string sample, int iterations, double time, double allocation)
    {
        output.Append(name).Append(',').Append(sample).Append(',').Append(iterations.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(time.ToString("R", CultureInfo.InvariantCulture)).Append(',').AppendLine(allocation.ToString("R", CultureInfo.InvariantCulture));
    }

    private sealed class BenchmarkWorkbook : IFormulaWorkbook
    {
        public BenchmarkWorkbook() { Worksheets = new IFormulaWorksheet[] { new BenchmarkWorksheet(this) }; }
        public string Name => "Benchmark";
        public IReadOnlyList<IFormulaWorksheet> Worksheets { get; }
        public FormulaCalculationSettings Settings { get; } = new FormulaCalculationSettings();
        public IFormulaWorksheet GetWorksheet(string name) => name == "Sheet1" ? Worksheets[0] : throw new ArgumentException("Unknown worksheet.", nameof(name));
    }

    private sealed class BenchmarkWorksheet : IFormulaWorksheet
    {
        public BenchmarkWorksheet(IFormulaWorkbook workbook) { Workbook = workbook; }
        public string Name => "Sheet1";
        public IFormulaWorkbook Workbook { get; }
        public IFormulaCell GetCell(int row, int column) => throw new NotSupportedException("These microbenchmarks do not materialize worksheet cells.");
        public bool TryGetCell(int row, int column, out IFormulaCell cell) { cell = null!; return false; }
    }

    private sealed class EmptyResolver : IFormulaValueResolver
    {
        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value) { value = FormulaValue.Blank; return false; }
        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value) { value = FormulaValue.Blank; return false; }
    }
}
