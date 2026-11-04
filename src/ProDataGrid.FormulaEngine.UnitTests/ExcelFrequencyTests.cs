// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    [Collection(FormulaAllocationCollection.Name)]
    public sealed class ExcelFrequencyTests
    {
        [Theory]
        [InlineData("FREQUENCY({79;85;78;85;50;81;95;88;97},{70;79;89})", "1;2;4;2")]
        [InlineData("FREQUENCY({79;85;78;85;50;81;95;88;97},{89;70;79})", "4;1;2;2")]
        [InlineData("FREQUENCY({1;2;2;3},{2;1;2})", "2;1;0;1")]
        [InlineData("FREQUENCY({1,2;3,4},{2,3})", "2;1;1")]
        [InlineData("FREQUENCY({1;2;3;4},{1;\"ignore\";3})", "1;2;1")]
        [InlineData("FREQUENCY({1;2;3},{\"a\";TRUE})", "3")]
        [InlineData("FREQUENCY({\"a\";TRUE},{1;2;3})", "0;0;0;0")]
        [InlineData("FREQUENCY({0;\"0\";FALSE},0)", "1;0")]
        [InlineData("FREQUENCY(A1:A3,{1;2})", "0;0;0")]
        [InlineData("FREQUENCY({1;2},A1:A3)", "2")]
        [InlineData("FREQUENCY(A1,A2)", "0")]
        [InlineData("FREQUENCY(1,1)", "1;0")]
        [InlineData("FREQUENCY(2,1)", "0;1")]
        [InlineData("FREQUENCY(-1,1)", "1;0")]
        [InlineData("FREQUENCY({-3;-1;0;1;3},{-1;0;2})", "2;1;1;1")]
        [InlineData("FREQUENCY({-1E308;0;1E308},{-1E308;1E308})", "1;2;0")]
        [InlineData("FREQUENCY({-0;0;1},{0;-0})", "2;0;1")]
        [InlineData("FREQUENCY({1;2;3},{3;2;1})", "1;1;1;0")]
        [InlineData("FREQUENCY({1;1;1},{1;1;1})", "3;0;0;0")]
        [InlineData("FREQUENCY(NA(),1)", "#N/A")]
        [InlineData("FREQUENCY(1,NA())", "#N/A")]
        [InlineData("FREQUENCY(HSTACK(1,NA()),1)", "#N/A")]
        [InlineData("FREQUENCY(1,HSTACK(1,NA()))", "#N/A")]
        [InlineData("FREQUENCY(1)", "#VALUE!")]
        [InlineData("FREQUENCY(1,2,3)", "#VALUE!")]
        [InlineData("SUM(FREQUENCY({1;2;3;4},{1;2;3}))", "4")]
        [InlineData("SUM(FREQUENCY({1;2;3;4},A1))", "4")]
        public void Boundaries_Errors_And_Composition_RoundTrip(string formula, string expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(expected, Describe(evaluator.Evaluate(parser.Parse(formatted, new FormulaParseOptions()), context, new WorkbookValueResolver())));
            }
        }

        [Fact]
        public void Seeded_Assignments_Match_Independent_Linear_Bin_Search()
        {
            var random = new Random(41903);
            var context = Context();
            for (var attempt = 0; attempt < 600; attempt++)
            {
                var data = new FormulaArray(random.Next(1, 15), random.Next(1, 12), sparse: attempt % 3 == 0);
                var bins = new FormulaArray(random.Next(1, 8), random.Next(1, 6), sparse: attempt % 4 == 0);
                Fill(data, random); Fill(bins, random);
                var dataBefore = Describe(FormulaValue.FromArray(data));
                var binsBefore = Describe(FormulaValue.FromArray(bins));
                var expected = LinearOracle(data, bins);
                var result = Invoke(context, FormulaValue.FromArray(data), FormulaValue.FromArray(bins)).AsArray();
                Assert.Equal(expected.Length, result.RowCount);
                Assert.Equal(1, result.ColumnCount);
                Assert.Null(result.Origin);
                for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i, 0].AsNumber());
                Assert.Equal(dataBefore, Describe(FormulaValue.FromArray(data)));
                Assert.Equal(binsBefore, Describe(FormulaValue.FromArray(bins)));
            }
        }

        [Fact]
        public void Masks_Blanks_Error_Metadata_And_Numeric_Precision_Are_Preserved()
        {
            var context = Context();
            var data = new FormulaArray(3, 1, sparse: true);
            var failure = new FormulaError(FormulaErrorType.Ref, "frequency input failure");
            data.SetValue(0, 0, FormulaValue.FromError(failure), present: false);
            data[1, 0] = FormulaValue.FromNumber(1.2344);
            data[2, 0] = FormulaValue.FromNumber(1.2354);
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            Assert.Equal("1;1", Describe(Invoke(context, FormulaValue.FromArray(data), FormulaValue.FromNumber(1.234))));
            Assert.False(data.IsPresent(0, 0));
            data.SetValue(0, 0, FormulaValue.FromError(failure), present: true);
            Assert.Equal(failure, Invoke(context, FormulaValue.FromArray(data), FormulaValue.FromNumber(1)).AsError());
            Assert.Equal(failure, Invoke(context, FormulaValue.FromNumber(1), FormulaValue.FromError(failure)).AsError());
        }

        [Fact]
        public void Nonfinite_Values_Are_Rejected_Before_Ordering()
        {
            var context = Context();
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromNumber(invalid), FormulaValue.FromNumber(1)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromNumber(1), FormulaValue.FromNumber(invalid)).AsError().Type);
            }
        }

        [Fact]
        public void Limits_Include_Overflow_Row_And_Physical_Ignored_Positions()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            var bins = new FormulaArray(3, 1);
            for (var i = 0; i < 3; i++) bins[i, 0] = FormulaValue.FromNumber(i);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromNumber(1), FormulaValue.FromArray(bins)).AsError().Type);
            var ignored = new FormulaArray(4, 1, sparse: true);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromArray(ignored), FormulaValue.FromNumber(1)).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromNumber(1), FormulaValue.FromArray(ignored)).AsError().Type);
            context.Workbook.Settings.MaximumArrayCellCount = 4;
            Assert.Equal("0;1;0;0", Describe(Invoke(context, FormulaValue.FromNumber(1), FormulaValue.FromArray(bins))));
        }

        [Fact]
        public void Concurrent_Pooled_Workspaces_And_Error_Exits_Do_Not_Contaminate_Results()
        {
            var context = Context();
            var bins = new FormulaArray(257, 1);
            var data = new FormulaArray(400, 1);
            for (var i = 0; i < bins.RowCount; i++) bins[i, 0] = FormulaValue.FromNumber(i);
            for (var i = 0; i < data.RowCount; i++) data[i, 0] = FormulaValue.FromNumber(i);
            var expected = LinearOracle(data, bins);
            Parallel.For(0, 64, i =>
            {
                var bad = Invoke(context, FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), FormulaValue.FromArray(bins));
                Assert.Equal(FormulaErrorType.NA, bad.AsError().Type);
                var result = Invoke(context, FormulaValue.FromArray(data), FormulaValue.FromArray(bins)).AsArray();
                for (var row = 0; row < expected.Length; row++) Assert.Equal(expected[row], result[row, 0].AsNumber());
            });
        }

        [Fact]
        public void Input_Expressions_Are_Evaluated_Once()
        {
            var registry = new ExcelFunctionRegistry();
            var function = new CountCallsFunction();
            registry.Register(function);
            var context = Context(registry);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                function.Calls = 0;
                var expression = new ExcelFormulaParser().Parse("FREQUENCY(SOURCE(),SOURCE())", new FormulaParseOptions());
                Assert.Equal("1;0", Describe(new FormulaEvaluator().Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(2, function.Calls);
            }
        }

        [Fact]
        public void Bin_Changes_Resize_The_Spill_And_Clear_Obsolete_Cells()
        {
            var context = Context();
            var sheet = context.Worksheet;
            for (var i = 1; i <= 4; i++) sheet.GetCell(i, 1).Value = FormulaValue.FromNumber(i);
            sheet.GetCell(1, 2).Value = FormulaValue.FromNumber(2);
            sheet.GetCell(2, 2).Value = FormulaValue.FromNumber(3);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet, 1, 4, "FREQUENCY(A1:A4,B1:B2)");
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4) });
            Assert.Equal("2;1;1", Describe(sheet.GetCell(1, 4).Value));
            Assert.Equal(1, sheet.GetCell(3, 4).Value.AsNumber());
            sheet.GetCell(2, 2).Value = FormulaValue.Blank;
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 2, 2) });
            Assert.Equal("2;2", Describe(sheet.GetCell(1, 4).Value));
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(3, 4).Value.Kind);
            sheet.GetCell(4, 1).Value = FormulaValue.FromNumber(1);
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 4, 1) });
            Assert.Equal("3;1", Describe(sheet.GetCell(1, 4).Value));
        }

        [Fact]
        public void Warm_Workspace_Allocation_Is_Only_The_Owned_Result()
        {
            var context = Context();
            var bins = new FormulaArray(256, 1);
            for (var i = 0; i < bins.RowCount; i++) bins[i, 0] = FormulaValue.FromNumber(i);
            Assert.True(context.FunctionRegistry.TryGetFunction("FREQUENCY", out var function));
            var call = new FormulaFunctionContext(context);
            var args = new[] { FormulaValue.FromNumber(100), FormulaValue.FromArray(bins) };
            var operation = new Func<FormulaValue>(() => function.Invoke(call, args));
            var reference = new Func<FormulaValue>(() => FormulaValue.FromArray(new FormulaArray(257, 1)));
            for (var i = 0; i < 100; i++) { operation(); reference(); }
            var expected = MeasureAllocation(reference, 100);
            var actual = MeasureAllocation(operation, 100);
            Assert.Equal(expected, actual);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureAllocation(Func<FormulaValue> operation, int iterations)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
            {
                var result = operation();
                if (result.Kind != FormulaValueKind.Array) throw new InvalidOperationException("Expected array");
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static void Fill(FormulaArray array, Random random)
        {
            for (var row = 0; row < array.RowCount; row++)
                for (var col = 0; col < array.ColumnCount; col++)
                {
                    var value = random.Next(6) switch
                    {
                        0 => FormulaValue.Blank, 1 => FormulaValue.FromText("ignored"), 2 => FormulaValue.FromBoolean(true),
                        _ => FormulaValue.FromNumber(random.Next(-10, 11))
                    };
                    array.SetValue(row, col, value, !array.HasMask || random.Next(4) != 0);
                }
        }

        private static int[] LinearOracle(FormulaArray data, FormulaArray bins)
        {
            var boundaries = new List<double>();
            foreach (var value in bins.Flatten()) if (value.Kind == FormulaValueKind.Number) boundaries.Add(value.AsNumber());
            var counts = new int[boundaries.Count + 1];
            foreach (var value in data.Flatten())
            {
                if (value.Kind != FormulaValueKind.Number) continue;
                var selected = boundaries.Count;
                for (var i = 0; i < boundaries.Count; i++)
                    if (value.AsNumber() <= boundaries[i] && (selected == boundaries.Count || boundaries[i] < boundaries[selected])) selected = i;
                counts[selected]++;
            }
            return counts;
        }

        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry ?? new ExcelFunctionRegistry());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction("FREQUENCY", out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }

        private static string Describe(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Array)
            {
                var result = new StringBuilder();
                var array = value.AsArray();
                for (var row = 0; row < array.RowCount; row++)
                {
                    if (row > 0) result.Append(';');
                    for (var col = 0; col < array.ColumnCount; col++)
                    {
                        if (col > 0) result.Append(',');
                        result.Append(Describe(array[row, col]));
                    }
                }
                return result.ToString();
            }
            return value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString();
        }

        private sealed class CountCallsFunction : IFormulaFunction
        {
            public int Calls;
            public string Name => "SOURCE";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            {
                Calls++;
                return FormulaValue.FromNumber(1);
            }
        }
    }
}
