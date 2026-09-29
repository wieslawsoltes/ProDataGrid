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
            var destination = args.Length > 0 ? args[0] : "artifacts/aggregates";
            Directory.CreateDirectory(destination);
            var workbook = new BenchmarkWorkbook();
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaEvaluationContext(workbook, workbook.Worksheets[0], new FormulaCellAddress("Sheet1", 1, 1), registry);
            var evaluator = new FormulaEvaluator();
            var resolver = new EmptyResolver();
            var parser = new ExcelFormulaParser();
            Validate(context, evaluator, resolver, parser);
            ValidateAdditional(context, evaluator, resolver, parser);
            var names = new List<string>();
            foreach (var function in registry.GetAll()) names.Add(function.Name);
            names.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(destination, "function-catalog.txt"), names);
            Console.WriteLine("Registered names=" + names.Count);
            if (args.Length > 1 && args[1] == "smoke") return 0;

            var left = new FormulaArray(100000, 1);
            var right = new FormulaArray(100000, 1);
            double dot = 0, distance = 0;
            for (var i = 0; i < left.RowCount; i++)
            {
                var x = i % 97 - 48;
                var y = i % 13 - 6;
                left[i, 0] = FormulaValue.FromNumber(x);
                right[i, 0] = FormulaValue.FromNumber(y);
                dot += x * y;
                distance += (x - y) * (x - y);
            }
            context = context.WithLocalValue("lhs", FormulaValue.FromArray(left)).WithLocalValue("rhs", FormulaValue.FromArray(right));
            var fused = parser.Parse("SUMPRODUCT(lhs,rhs)", new FormulaParseOptions());
            var materialized = parser.Parse("SUM(lhs*rhs)", new FormulaParseOptions());
            var direct = Function(registry, "SUMPRODUCT");
            var paired = Function(registry, "SUMXMY2");
            var arguments = new[] { FormulaValue.FromArray(left), FormulaValue.FromArray(right) };
            var call = new FormulaFunctionContext(context);
            var output = new StringBuilder();
            output.AppendLine("# runtime=" + RuntimeInformation.FrameworkDescription);
            output.AppendLine("# os=" + RuntimeInformation.OSDescription);
            output.AppendLine("# architecture=" + RuntimeInformation.ProcessArchitecture);
            output.AppendLine("# logical_processors=" + Environment.ProcessorCount);
            output.AppendLine("# tiered_compilation=" + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default"));
            output.AppendLine("# input_creation_and_parsing=excluded; output_and_workspace_costs=included; precision=false; warm_batches=2; measured_batches=7");
            output.AppendLine("scenario,sample,iterations,microseconds_per_operation,bytes_per_operation");
            Measure(output, "sumproduct_100000_direct", 30, () => direct.Invoke(call, arguments).AsNumber(), dot);
            Measure(output, "sumxmy2_100000_direct", 30, () => paired.Invoke(call, arguments).AsNumber(), distance);
            // Compare equivalent numeric formulas, not library revisions or Microsoft Excel.
            // Alternate which formula runs first across complete processes using the optional reverse flag.
            var reverse = args.Length > 1 && args[1] == "reverse";
            for (var i = 0; i < 2; i++)
            {
                var useFused = reverse ? i == 1 : i == 0;
                var expression = useFused ? fused : materialized;
                Measure(output, useFused ? "sumproduct_100000_formula" : "sum_multiply_100000_formula", 30,
                    () => evaluator.Evaluate(expression, context, resolver).AsNumber(), dot);
            }
            MeasureAdditional(output, context, evaluator, resolver, parser);
            File.WriteAllText(Path.Combine(destination, "measurements.csv"), output.ToString());
            Console.Write(output.ToString());
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static partial void ValidateAdditional(FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser);
    static partial void MeasureAdditional(StringBuilder output, FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser);

    private static void Validate(FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double Expected)[]
        {
            ("SUMPRODUCT({1,2,3},{4,5,6})", 32), ("SUMPRODUCT({1,2},{3,4},{5,6})", 63),
            ("SUMPRODUCT({1,\"2\",TRUE},{3,4,5})", 3), ("SUMPRODUCT({1E16,1,-1E16},{1,1,1})", 1),
            ("SUMPRODUCT(--({1;2;3}>1),{10;20;30})", 50),
            ("SUMX2MY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", -55),
            ("SUMX2PY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", 521),
            ("SUMXMY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", 79),
            ("SUMXMY2({1;2},{3,4})", 8)
        };
        var errors = new (string Formula, FormulaErrorType Error)[]
        {
            ("SUMPRODUCT({1;2},{1,2})", FormulaErrorType.Value),
            ("SUMPRODUCT(0,NA())", FormulaErrorType.NA), ("SUMPRODUCT(0,1,NA())", FormulaErrorType.NA),
            ("SUMXMY2({1;2},3)", FormulaErrorType.NA), ("SUMPRODUCT(1E308,2)", FormulaErrorType.Num)
        };
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
            foreach (var precision in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                foreach (var item in cases)
                {
                    var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (value.Kind != FormulaValueKind.Number || value.AsNumber() != item.Expected)
                        throw new InvalidOperationException("Aggregate smoke failed: " + item.Formula + " => " + value);
                    assertions++;
                }
                foreach (var item in errors)
                {
                    var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                    if (value.Kind != FormulaValueKind.Error || value.AsError().Type != item.Error)
                        throw new InvalidOperationException("Aggregate error smoke failed: " + item.Formula + " => " + value);
                    assertions++;
                }
            }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        context.Workbook.Settings.ApplyNumberPrecision = false;
        Console.WriteLine("Aggregate smoke assertions=" + assertions);
    }

    private static IFormulaFunction Function(ExcelFunctionRegistry registry, string name)
        => registry.TryGetFunction(name, out var function) ? function : throw new InvalidOperationException("Missing function " + name);

    private static void Measure(StringBuilder output, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++)
            for (var i = 0; i < iterations; i++)
                if (operation() != expected) throw new InvalidOperationException(name + " warmup checksum failed.");
        var times = new double[7];
        var allocations = new double[7];
        for (var sample = 0; sample < times.Length; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            double checksum = 0;
            for (var i = 0; i < iterations; i++) checksum += operation();
            var elapsed = Stopwatch.GetTimestamp() - start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (checksum != iterations * expected) throw new InvalidOperationException(name + " measured checksum failed.");
            times[sample] = elapsed * 1000000d / Stopwatch.Frequency / iterations;
            allocations[sample] = allocated / (double)iterations;
            Write(output, name, sample.ToString(CultureInfo.InvariantCulture), iterations, times[sample], allocations[sample]);
        }
        Array.Sort(times);
        Array.Sort(allocations);
        Write(output, name, "median", iterations, times[3], allocations[3]);
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
        public IFormulaWorksheet GetWorksheet(string name) => name == "Sheet1" ? Worksheets[0] : throw new ArgumentException("Unknown worksheet.", nameof(name));
    }
    private sealed class BenchmarkWorksheet : IFormulaWorksheet
    {
        public BenchmarkWorksheet(IFormulaWorkbook workbook) { Workbook = workbook; }
        public string Name => "Sheet1";
        public IFormulaWorkbook Workbook { get; }
        public IFormulaCell GetCell(int row, int column) => throw new NotSupportedException("Benchmark uses evaluated arrays, not worksheet materialization.");
        public bool TryGetCell(int row, int column, out IFormulaCell cell) { cell = null!; return false; }
    }
    private sealed class EmptyResolver : IFormulaValueResolver
    {
        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value) { value = FormulaValue.Blank; return false; }
        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value) { value = FormulaValue.Blank; return false; }
    }
}
