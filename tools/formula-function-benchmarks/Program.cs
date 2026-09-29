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
            var destination = args.Length > 0 ? args[0] : "artifacts/formula-functions";
            Directory.CreateDirectory(destination);
            var workbook = new BenchmarkWorkbook();
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaEvaluationContext(workbook, workbook.Worksheets[0], new FormulaCellAddress("Sheet1", 1, 1), registry);
            var evaluator = new FormulaEvaluator();
            var resolver = new EmptyResolver();
            var parser = new ExcelFormulaParser();
            Validate(context, evaluator, resolver, parser);
            ValidateNumerics(context, evaluator, resolver, parser);
            var names = new List<string>();
            foreach (var function in registry.GetAll()) names.Add(function.Name);
            names.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(destination, "function-catalog.txt"), names);
            Console.WriteLine("Parser/function smoke passed in both modes; registered names=" + names.Count);
            if (args.Length > 1 && args[1] == "smoke") return 0;

            var numbers = new FormulaArray(10000, 1);
            var matrix = new FormulaArray(1000, 10);
            var textArray = new FormulaArray(10000, 1);
            for (var i = 0; i < 10000; i++)
            {
                numbers[i, 0] = FormulaValue.FromNumber(i);
                matrix[i / 10, i % 10] = FormulaValue.FromNumber(i);
                textArray[i, 0] = FormulaValue.FromText("a");
            }
            context = context.WithLocalValue("input", FormulaValue.FromArray(numbers)).WithLocalValue("matrix", FormulaValue.FromArray(matrix));
            var cases = new (string Name, string Formula, double Expected, int Iterations)[]
            {
                ("map_10000", "MAP(input,LAMBDA(x,x*2+1))", 19999, 20),
                ("reduce_10000", "REDUCE(0,input,LAMBDA(acc,x,acc+x))", 49995000, 20),
                ("scan_10000", "SCAN(0,input,LAMBDA(acc,x,acc+x))", 49995000, 20),
                ("byrow_1000x10", "BYROW(matrix,LAMBDA(row,SUM(row)))", 99945, 20),
                ("makearray_100x100", "MAKEARRAY(100,100,LAMBDA(row,col,row+col))", 200, 20),
                ("repeated_array_expression", "SUM(input*2+1)+SUM(input*2+1)+SUM(input*2+1)", 300000000, 20),
                ("let_reused_array_expression", "LET(v,input*2+1,SUM(v)+SUM(v)+SUM(v))", 300000000, 20)
            };
            var output = new StringBuilder();
            output.AppendLine("# runtime=" + RuntimeInformation.FrameworkDescription);
            output.AppendLine("# os=" + RuntimeInformation.OSDescription);
            output.AppendLine("# architecture=" + RuntimeInformation.ProcessArchitecture);
            output.AppendLine("# logical_processors=" + Environment.ProcessorCount);
            output.AppendLine("# tiered_compilation=" + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default"));
            output.AppendLine("# precision=false; input_creation_and_parsing=excluded; output_allocation=included; warm_batches=2; measured_batches=7");
            output.AppendLine("scenario,sample,iterations,microseconds_per_operation,bytes_per_operation");
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var item in cases)
                {
                    var expression = parser.Parse(item.Formula, new FormulaParseOptions());
                    Measure(output, item.Name + (compiled ? "_compiled" : "_interpreted"), item.Iterations,
                        () => LastNumber(evaluator.Evaluate(expression, context, resolver)), item.Expected);
                }
            }
            workbook.Settings.EnableCompiledExpressions = true;
            var functionContext = new FormulaFunctionContext(context);
            var exact = Function(registry, "EXACT");
            var exactArgs = new[] { FormulaValue.FromArray(textArray), FormulaValue.FromText("a") };
            Measure(output, "exact_10000_direct", 100, () => exact.Invoke(functionContext, exactArgs).AsArray()[9999, 0].AsBoolean() ? 1 : 0, 1);
            var repeat = Function(registry, "REPT");
            var repeatArgs = new[] { FormulaValue.FromText("ab"), FormulaValue.FromNumber(10000) };
            Measure(output, "rept_20000_chars_direct", 1000, () => repeat.Invoke(functionContext, repeatArgs).AsText().Length, 20000);
            var substitute = Function(registry, "SUBSTITUTE");
            var substituteArgs = new[] { FormulaValue.FromText(new string('a', 10000)), FormulaValue.FromText("a"), FormulaValue.FromText("bb") };
            Measure(output, "substitute_10000_matches_direct", 50, () => substitute.Invoke(functionContext, substituteArgs).AsText().Length, 20000);
            var search = Function(registry, "SEARCH");
            var searchArgs = new[] { FormulaValue.FromText("needle"), FormulaValue.FromText(new string('x', 10000) + "NEEDLE") };
            Measure(output, "search_literal_10006_chars_direct", 10000, () => search.Invoke(functionContext, searchArgs).AsNumber(), 10001);
            var pattern = new StringBuilder();
            for (var i = 0; i < 64; i++) pattern.Append("a*");
            pattern.Append('b');
            var wildcardArgs = new[] { FormulaValue.FromText(pattern.ToString()), FormulaValue.FromText(new string('a', 10000) + "b") };
            Measure(output, "search_64_stars_10001_chars_direct", 100, () => search.Invoke(functionContext, wildcardArgs).AsNumber(), 1);
            MeasureNumerics(output, context, evaluator, resolver, parser);
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

    private static IFormulaFunction Function(ExcelFunctionRegistry registry, string name)
        => registry.TryGetFunction(name, out var function) ? function : throw new InvalidOperationException("Missing " + name);

    private static double LastNumber(FormulaValue value)
    {
        if (value.Kind == FormulaValueKind.Array)
        {
            var array = value.AsArray();
            value = array[array.RowCount - 1, array.ColumnCount - 1];
        }
        return value.AsNumber();
    }

    private static void Validate(FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var checks = new (string Formula, string Expected)[]
        {
            ("LET(x,3,x*x)", "9"), ("LAMBDA(x,x+1)(4)", "5"), ("LAMBDA(x,ISOMITTED(x))()", "#VALUE!"),
            ("LAMBDA(x,y,ISOMITTED(x))(,1)", "TRUE"),
            ("SUM(MAP({1;2;3},LAMBDA(x,x*x)))", "14"),
            ("REDUCE(1,{2;3;4},LAMBDA(acc,x,acc*x))", "24"),
            ("SUM(SCAN(0,{1;2;3},LAMBDA(acc,x,acc+x)))", "10"),
            ("SUM(BYROW({1,2;3,4},LAMBDA(row,SUM(row))))", "10"),
            ("SUM(BYCOL({1,2;3,4},LAMBDA(col,SUM(col))))", "10"),
            ("SUM(MAKEARRAY(2,2,LAMBDA(row,col,row*col)))", "9"),
            ("LET(items,MAP({1;2;3},LAMBDA(x,LAMBDA(x*x))),SUM(MAP(items,LAMBDA(f,f()))))", "14"),
            ("FIND(\"b\",\"abc\")", "2"), ("SEARCH(\"b*d\",\"xxB12D\")", "3"),
            ("TEXTBEFORE(\"a-b-c\",\"-\",-1)", "a-b"), ("TEXTAFTER(\"a-b-c\",\"-\",-1)", "c"),
            ("EXACT(\"a\",\"A\")", "FALSE"), ("CLEAN(\"a\"&UNICHAR(9)&\"b\")", "ab"),
            ("PROPER(\"NASA words\")", "Nasa Words"), ("REPT(\"ab\",3)", "ababab"),
            ("REPLACE(\"abcd\",2,2,\"x\")", "axd"), ("SUBSTITUTE(\"a-b-a\",\"a\",\"x\",2)", "a-b-x"),
            ("UNICODE(\"😀\")", "128512"), ("UNICHAR(128512)", "😀"),
            ("MID(\"abcd\",2,2147483647)", "bcd"), ("REPT(\"x\",32768)", "#VALUE!")
        };
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var check in checks)
            {
                var value = evaluator.Evaluate(parser.Parse(check.Formula, new FormulaParseOptions()), context, resolver);
                var actual = value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString();
                if (actual != check.Expected) throw new InvalidOperationException(check.Formula + " expected " + check.Expected + " got " + actual);
            }
            foreach (var version in new[] { FormulaTextCompatibilityVersion.Version1, FormulaTextCompatibilityVersion.Version2 })
            {
                context.Workbook.Settings.TextCompatibilityVersion = version;
                var actual = evaluator.Evaluate(parser.Parse("LEN(\"A😀B\")", new FormulaParseOptions()), context, resolver).AsNumber();
                if (actual != (version == FormulaTextCompatibilityVersion.Version1 ? 4 : 3)) throw new InvalidOperationException("Unicode indexing smoke failure.");
            }
            context.Workbook.Settings.TextCompatibilityVersion = FormulaTextCompatibilityVersion.Version1;
        }
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Smoke assertions=" + (2 * (checks.Length + 2)));
    }

    private static void Measure(StringBuilder output, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++)
            for (var i = 0; i < iterations; i++)
                if (operation() != expected) throw new InvalidOperationException(name + " correctness failure.");
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
            if (checksum != expected * iterations) throw new InvalidOperationException(name + " measured correctness failure.");
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
        public IFormulaWorksheet GetWorksheet(string name) => name == "Sheet1" ? Worksheets[0] : throw new ArgumentException("Unknown sheet.", nameof(name));
    }

    private sealed class BenchmarkWorksheet : IFormulaWorksheet
    {
        public BenchmarkWorksheet(IFormulaWorkbook workbook) { Workbook = workbook; }
        public string Name => "Sheet1";
        public IFormulaWorkbook Workbook { get; }
        public IFormulaCell GetCell(int row, int column) => throw new NotSupportedException("The benchmark does not access worksheet cells.");
        public bool TryGetCell(int row, int column, out IFormulaCell cell) { cell = null!; return false; }
    }

    private sealed class EmptyResolver : IFormulaValueResolver
    {
        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value) { value = FormulaValue.Blank; return false; }
        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value) { value = FormulaValue.Blank; return false; }
    }
}
