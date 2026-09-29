// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelOrderStatisticSelectionTests
    {
        [Theory]
        [InlineData("MEDIAN({9,1})", 5)]
        [InlineData("MEDIAN({1,9})", 5)]
        [InlineData("MEDIAN({9,1,7})", 7)]
        [InlineData("MEDIAN({1E308,1E308})", 1e308)]
        [InlineData("MEDIAN({-1E308,1E308})", 0)]
        [InlineData("PERCENTILE.INC({9,1},0.25)", 3)]
        [InlineData("PERCENTILE.INC({-1E308,1E308},0.5)", 0)]
        [InlineData("PERCENTILE.INC({-1E308,1E308},0.75)", 5e307)]
        [InlineData("PERCENTILE.EXC({1,2,3,4},0.25)", 1.25)]
        [InlineData("PERCENTILE.EXC({1,2,3,4},0.5)", 2.5)]
        [InlineData("PERCENTILE.EXC({1,2,3,4},0.75)", 3.75)]
        [InlineData("PERCENTILE.EXC({1,2,3,4},0.2)", 1)]
        [InlineData("PERCENTILE.EXC({1,2,3,4},0.8)", 4)]
        [InlineData("PERCENTILE.EXC({7},0.5)", 7)]
        [InlineData("QUARTILE.EXC({1,2,3,4},1)", 1.25)]
        [InlineData("QUARTILE.EXC({1,2,3,4},2.9)", 2.5)]
        [InlineData("QUARTILE.EXC({1,2,3,4},3)", 3.75)]
        [InlineData("LARGE({1,2,3,4},2)", 3)]
        [InlineData("SMALL({4,3,2,1},2)", 2)]
        [InlineData("MEDIAN(TRUE,\"3\",{2,FALSE,\"99\"})", 2)]
        [InlineData("PERCENTILE.EXC({TRUE,\"99\",1,3},0.5)", 2)]
        public void Interpolation_And_Exclusive_Percentiles_Compose_In_Both_Evaluators(string text, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(text, new FormulaParseOptions());
            var roundTrip = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var candidate in new[] { expression, roundTrip })
                    Close(expected, new FormulaEvaluator().Evaluate(candidate, context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Theory]
        [InlineData("PERCENTILE.EXC({1,2},0)", FormulaErrorType.Num)]
        [InlineData("PERCENTILE.EXC({1,2},1)", FormulaErrorType.Num)]
        [InlineData("PERCENTILE.EXC({1,2},0.1)", FormulaErrorType.Num)]
        [InlineData("PERCENTILE.EXC({1,2},0.9)", FormulaErrorType.Num)]
        [InlineData("PERCENTILE.EXC({\"a\",TRUE},0.5)", FormulaErrorType.Num)]
        [InlineData("PERCENTILE.EXC({1,2},\"no\")", FormulaErrorType.Value)]
        [InlineData("PERCENTILE.EXC(HSTACK(1,NA()),0.5)", FormulaErrorType.NA)]
        [InlineData("QUARTILE.EXC({1,2,3},0)", FormulaErrorType.Num)]
        [InlineData("QUARTILE.EXC({1,2,3},4)", FormulaErrorType.Num)]
        [InlineData("QUARTILE.EXC({1,2},1)", FormulaErrorType.Num)]
        [InlineData("SMALL({1,2},0)", FormulaErrorType.Num)]
        [InlineData("LARGE({1,2},3)", FormulaErrorType.Num)]
        public void Undefined_Order_Statistics_Return_Errors(string text, FormulaErrorType expected)
        {
            var context = Context();
            var value = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(text, new FormulaParseOptions()), context, new WorkbookValueResolver());
            Assert.Equal(expected, value.AsError().Type);
        }

        [Fact]
        public void Selection_Agrees_With_Full_Sorting_For_Random_Duplicate_And_Adversarial_Inputs()
        {
            var random = new Random(21831);
            var context = Context();
            for (var sample = 0; sample < 120; sample++)
            {
                var n = sample < 30 ? sample + 1 : random.Next(129, 2049);
                var array = new FormulaArray(n, 1);
                var sorted = new double[n];
                var original = new double[n];
                for (var i = 0; i < n; i++)
                {
                    var value = (sample % 5) switch { 0 => i, 1 => n - i, 2 => Math.Min(i, n - 1 - i), 3 => 7, _ => random.Next(-20, 21) };
                    sorted[i] = original[i] = value;
                    array[i, 0] = FormulaValue.FromNumber(value);
                }
                Array.Sort(sorted);
                var data = FormulaValue.FromArray(array);
                var ranks = n <= 30 ? n : 30;
                for (var j = 0; j < ranks; j++)
                {
                    var k = n <= 30 ? j + 1 : random.Next(1, n + 1);
                    Close(sorted[k - 1], Invoke(context, "SMALL", data, FormulaValue.FromNumber(k)).AsNumber());
                    Close(sorted[n - k], Invoke(context, "LARGE", data, FormulaValue.FromNumber(k)).AsNumber());
                    var p = j / (double)Math.Max(1, ranks - 1);
                    var position = (n - 1) * p;
                    var lo = (int)Math.Floor(position);
                    var hi = (int)Math.Ceiling(position);
                    Close(sorted[lo] + (sorted[hi] - sorted[lo]) * (position - lo),
                        Invoke(context, "PERCENTILE.INC", data, FormulaValue.FromNumber(p)).AsNumber());
                    var q = (position + 1) / (n + 1);
                    Close(sorted[lo] + (sorted[hi] - sorted[lo]) * (position - lo),
                        Invoke(context, "PERCENTILE.EXC", data, FormulaValue.FromNumber(q)).AsNumber());
                }
                Close(n % 2 == 0 ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2 : sorted[n / 2], Invoke(context, "MEDIAN", data).AsNumber());
                for (var i = 0; i < n; i++) Assert.Equal(original[i], array[i, 0].AsNumber());
            }
        }

        [Fact]
        public void Error_Paths_Nonfinite_Inputs_And_Concurrent_Calls_Release_Private_Buffers()
        {
            var context = Context();
            var array = new FormulaArray(1000, 1, sparse: true);
            for (var i = 0; i < 999; i++) array[i, 0] = FormulaValue.FromNumber(i);
            var data = FormulaValue.FromArray(array);
            Parallel.For(0, 64, i => Assert.Equal(499, Invoke(context, "MEDIAN", data).AsNumber()));
            Assert.False(array.IsPresent(999, 0));
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "PERCENTILE.INC", data, FormulaValue.FromNumber(invalid)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "LARGE", data, FormulaValue.FromNumber(invalid)).AsError().Type);
                array[999, 0] = FormulaValue.FromNumber(invalid);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "MEDIAN", data).AsError().Type);
            }
            array[999, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            Assert.Equal(FormulaErrorType.NA, Invoke(context, "MEDIAN", data).AsError().Type);
            array[999, 0] = FormulaValue.FromNumber(999);
            Close(499.5, Invoke(context, "MEDIAN", data).AsNumber());
        }

        [Fact]
        public void Warm_Selection_Does_Not_Allocate_A_List_Or_Retain_The_Source()
        {
            var context = Context();
            var array = new FormulaArray(10000, 1);
            for (var i = 0; i < array.RowCount; i++) array[i, 0] = FormulaValue.FromNumber(i);
            var args = new[] { FormulaValue.FromArray(array), FormulaValue.FromNumber(5000) };
            Assert.True(context.FunctionRegistry.TryGetFunction("LARGE", out var function));
            var call = new FormulaFunctionContext(context);
            for (var i = 0; i < 20; i++) function.Invoke(call, args);
            var before = GC.GetAllocatedBytesForCurrentThread();
            double sum = 0;
            for (var i = 0; i < 50; i++) sum += function.Invoke(call, args).AsNumber();
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(250000, sum);
            Assert.True(bytes < 4096, $"Warm selection allocated {bytes} bytes");
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static void Close(double expected, double actual)
        {
            Assert.True(double.IsFinite(actual));
            Assert.True(Math.Abs(expected - actual) <= Math.Max(1, Math.Abs(expected)) * 1e-12, $"Expected {expected:R}, actual {actual:R}");
        }
    }
}
