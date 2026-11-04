// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelDescriptiveStatisticsTests
    {
        [Theory]
        [InlineData("DEVSQ({4;5;8;7;11;4;3})", 48)]
        [InlineData("AVEDEV({4;5;6;7;5;4;3})", 1.0204081632653061)]
        [InlineData("SKEW({3;4;5;2;3;4;5;6;4;7})", 0.3595430714067974)]
        [InlineData("SKEW.P({3;4;5;2;3;4;5;6;4;7})", 0.303193339354144)]
        [InlineData("KURT({3;4;5;2;3;4;5;6;4;7})", -0.151799637208416)]
        [InlineData("GEOMEAN(1,4,16)", 4)]
        [InlineData("HARMEAN(1,2,4)", 12d / 7)]
        [InlineData("SUMSQ(3,4)", 25)]
        [InlineData("SUMSQ({3,4;5,6})", 86)]
        [InlineData("SUMSQ(A1:A3)", 0)]
        [InlineData("VAR.S(1,2,3)", 1)]
        [InlineData("VAR.P(1,2,3)", 2d / 3)]
        [InlineData("STDEV.S(1,2,3)", 1)]
        [InlineData("STDEV.P(1,2,3)", 0.816496580927726)]
        [InlineData("VAR(1,2,3)", 1)]
        [InlineData("VARP(1,2,3)", 2d / 3)]
        [InlineData("STDEV(1,2,3)", 1)]
        [InlineData("STDEVP(1,2,3)", 0.816496580927726)]
        [InlineData("VARA({TRUE,FALSE,\"text\",2})", 11d / 12)]
        [InlineData("VARPA({TRUE,FALSE,\"12\",2})", 11d / 16)]
        [InlineData("STDEVA({TRUE,FALSE,\"text\",2})", 0.9574271077563381)]
        [InlineData("STDEVPA({TRUE,FALSE,\"12\",2})", 0.82915619758885)]
        [InlineData("SUMSQ(TRUE,\"3\",FALSE)", 10)]
        [InlineData("SUMSQ({TRUE,\"3\",FALSE,2})", 4)]
        [InlineData("VAR.S(1,\"3\",TRUE)", 4d / 3)]
        [InlineData("VAR.S({1,\"3\",TRUE,3})", 2)]
        [InlineData("DEVSQ(7)", 0)]
        [InlineData("AVEDEV(7)", 0)]
        [InlineData("GEOMEAN(7)", 7)]
        [InlineData("HARMEAN(7)", 7)]
        [InlineData("SKEW({1;2;3})", 0)]
        [InlineData("SKEW.P({1;2;3})", 0)]
        [InlineData("KURT({1;2;3;4})", -1.2)]
        [InlineData("LET(v,{1;2;3},DEVSQ(v)+SUMSQ(v))", 16)]
        [InlineData("SUM(BYROW({1,2,3;2,4,6},LAMBDA(row,VAR.S(row))))", 5)]
        public void Scalar_Array_And_Formatter_Paths_Agree(string formula, double expected)
        {
            var context = Context();
            var expression = Parse(formula);
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Close(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber(), 2e-12);
                Close(expected, evaluator.Evaluate(Parse(formatted), context, new WorkbookValueResolver()).AsNumber(), 2e-12);
            }
        }

        [Theory]
        [InlineData("VAR.S(1)", FormulaErrorType.Div0)]
        [InlineData("STDEV.S(1)", FormulaErrorType.Div0)]
        [InlineData("VAR.P(A1:A2)", FormulaErrorType.Div0)]
        [InlineData("SKEW(1,2)", FormulaErrorType.Div0)]
        [InlineData("SKEW.P(1,2)", FormulaErrorType.Div0)]
        [InlineData("SKEW(1,1,1)", FormulaErrorType.Div0)]
        [InlineData("SKEW.P(1,1,1)", FormulaErrorType.Div0)]
        [InlineData("KURT(1,2,3)", FormulaErrorType.Div0)]
        [InlineData("KURT(1,1,1,1)", FormulaErrorType.Div0)]
        [InlineData("GEOMEAN(1,0)", FormulaErrorType.Num)]
        [InlineData("GEOMEAN(1,-1)", FormulaErrorType.Num)]
        [InlineData("HARMEAN(1,0)", FormulaErrorType.Num)]
        [InlineData("HARMEAN(1,-1)", FormulaErrorType.Num)]
        [InlineData("SUMSQ(\"bad\")", FormulaErrorType.Value)]
        [InlineData("DEVSQ(1,\"bad\")", FormulaErrorType.Value)]
        [InlineData("VAR.S(1,2,NA())", FormulaErrorType.NA)]
        [InlineData("DEVSQ(HSTACK(1,2,NA()))", FormulaErrorType.NA)]
        [InlineData("VARA(1,2,NA())", FormulaErrorType.NA)]
        [InlineData("SUMSQ(LAMBDA(1))", FormulaErrorType.Value)]
        [InlineData("VAR.P(1e308,-1e308)", FormulaErrorType.Num)]
        [InlineData("SUMSQ(1e308)", FormulaErrorType.Num)]
        public void Invalid_Domains_And_Unrepresentable_Results_Return_Formula_Errors(string formula, FormulaErrorType expected)
        {
            var context = Context();
            foreach (var compiled in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(expected, new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver()).AsError().Type);
            }
        }

        [Theory]
        [InlineData("VAR.S(1e308,1e308)", 0)]
        [InlineData("STDEV.P(1e308,-1e308)", 1e308)]
        [InlineData("STDEV.S(1e308,-1e308)", 1.4142135623730951e308)]
        [InlineData("STDEV.P(1e-300,-1e-300)", 1e-300)]
        [InlineData("VAR.P(1e-300,-1e-300)", 0)]
        [InlineData("AVEDEV(1e308,-1e308)", 1e308)]
        [InlineData("KURT(1e308,1e308,-1e308,-1e308)", -6)]
        [InlineData("GEOMEAN(1e308,1e-308)", 1)]
        [InlineData("GEOMEAN(1e308,1e308)", 1e308)]
        [InlineData("HARMEAN(1e308,1e308)", 1e308)]
        [InlineData("HARMEAN(1e-300,1e300)", 2e-300)]
        public void Finite_Extreme_Statistics_Do_Not_Overflow_Intermediate_Values(string formula, double expected)
        {
            var context = Context();
            foreach (var compiled in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Close(expected, new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver()).AsNumber(), 3e-13);
            }
        }

        [Fact]
        public void Neighboring_Maximum_And_Subnormal_Values_Retain_Their_Representable_Spread()
        {
            var context = Context();
            var large = Math.ScaleB(1d, 1023);
            var next = double.BitIncrement(large);
            Close((next - large) / 2, Invoke(context, "STDEV.P", Array(large, next)).AsNumber());
            var small = double.Epsilon;
            Assert.Equal(small, Invoke(context, "STDEV.P", Array(small, 3 * small)).AsNumber());
            Assert.Equal(small, Invoke(context, "HARMEAN", Array(small, small)).AsNumber());
            Assert.Equal(small, Invoke(context, "GEOMEAN", Array(small, small)).AsNumber());
        }

        [Fact]
        public void Single_Cell_References_Keep_Their_Reference_Coercion_In_Both_Modes()
        {
            var context = Context();
            var sheet = context.Worksheet;
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(1);
            sheet.GetCell(2, 1).Value = FormulaValue.FromText("3");
            sheet.GetCell(3, 1).Value = FormulaValue.FromBoolean(true);
            sheet.GetCell(4, 1).Value = FormulaValue.FromNumber(3);
            foreach (var compiled in new[] { false, true })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var formula in new[] { "VAR.S(A1:A4)", "VAR.S(A1,A2,A3,A4)" })
                    Assert.Equal(2, new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver()).AsNumber());
                foreach (var formula in new[] { "VARA(A1:A4)", "VARA(A1,A2,A3,A4)" })
                    Close(19d / 12, new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Fact]
        public void Sources_Are_Evaluated_Once_And_Input_Arrays_Are_Not_Overwritten()
        {
            var registry = new ExcelFunctionRegistry();
            var source = Array(1, 2, 3, 4).AsArray();
            var tick = new SourceFunction(FormulaValue.FromArray(source));
            registry.Register(tick);
            var context = Context(registry);
            foreach (var name in Names)
            {
                var before = tick.Calls;
                new FormulaEvaluator().Evaluate(Parse(name + "(SOURCE())"), context, new WorkbookValueResolver());
                Assert.Equal(before + 1, tick.Calls);
                for (var i = 0; i < 4; i++) Assert.Equal(i + 1, source[i, 0].AsNumber());
            }
        }

        [Fact]
        public void Sparse_Text_Logical_And_Error_Inputs_Keep_Distinct_Meanings()
        {
            var context = Context();
            var source = new FormulaArray(4, 1, sparse: true);
            source[0, 0] = FormulaValue.FromNumber(1);
            source[2, 0] = FormulaValue.FromText("");
            source[3, 0] = FormulaValue.FromBoolean(true);
            Assert.Equal(0, Invoke(context, "VAR.P", FormulaValue.FromArray(source)).AsNumber());
            Close(1d / 3, Invoke(context, "VARA", FormulaValue.FromArray(source)).AsNumber());
            Assert.False(source.IsPresent(1, 0));
            source[3, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "original detail"));
            var result = Invoke(context, "STDEV.P", FormulaValue.FromArray(source));
            Assert.Equal("original detail", result.AsError().Message);
        }

        [Fact]
        public void Exact_Integer_Central_Moment_Oracle_Covers_Offset_Scale_And_Permutation()
        {
            var random = new Random(78291);
            var context = Context();
            for (var iteration = 0; iteration < 120; iteration++)
            {
                var integers = new int[random.Next(4, 100)];
                for (var i = 0; i < integers.Length; i++) integers[i] = random.Next(-100, 101);
                var expected = Oracle(integers);
                foreach (var exponent in new[] { -450, 0, 450 })
                {
                    var values = new double[integers.Length];
                    for (var i = 0; i < values.Length; i++) values[i] = Math.ScaleB(1099511627776d + integers[i], exponent);
                    var argument = Array(values);
                    Close(Math.ScaleB(expected.M2 / values.Length, 2 * exponent), Invoke(context, "VAR.P", argument).AsNumber(), 3e-13);
                    Close(Math.ScaleB(Math.Sqrt(expected.M2 / (values.Length - 1)), exponent), Invoke(context, "STDEV.S", argument).AsNumber(), 3e-13);
                    Close(expected.Skew, Invoke(context, "SKEW", argument).AsNumber(), 5e-12);
                    Close(expected.Kurt, Invoke(context, "KURT", argument).AsNumber(), 5e-12);
                    System.Array.Reverse(values);
                    Close(expected.Skew, Invoke(context, "SKEW", Array(values)).AsNumber(), 5e-12);
                }
            }
        }

        [Fact]
        public void Limits_Nonfinite_Input_And_Error_Paths_Do_Not_Corrupt_Pooled_Workspaces()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            foreach (var name in Names)
                Assert.Equal(FormulaErrorType.Num, Invoke(context, name, Array(1, 2, 3, 4)).AsError().Type);
            context.Workbook.Settings.MaximumArrayCellCount = 10000;
            var array = new FormulaArray(1000, 1);
            for (var i = 0; i < 999; i++) array[i, 0] = FormulaValue.FromNumber(i + 1);
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                array[999, 0] = FormulaValue.FromNumber(bad);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "STDEV.S", FormulaValue.FromArray(array)).AsError().Type);
                Assert.Equal(1, Invoke(context, "VAR.S", Array(1, 2, 3)).AsNumber());
            }
        }

        [Fact]
        public void Precision_Settings_Are_Applied_To_Inputs_And_Final_Output()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            Assert.Equal(0, Invoke(context, "VAR.S", Array(1.001, 1.002)).AsNumber());
            context.Workbook.Settings.ApplyNumberPrecision = false;
            Close(0.0000005, Invoke(context, "VAR.S", Array(1.001, 1.002)).AsNumber(), 1e-10);
        }

        [Fact]
        public void Independent_Parallel_Invocations_Do_Not_Share_Working_Values()
        {
            var context = Context();
            var values = new double[1000];
            for (var i = 0; i < values.Length; i++) values[i] = i + 1;
            var input = Array(values);
            Parallel.For(0, 64, _ => Close((1000d * 1000 - 1) / 12, Invoke(context, "VAR.P", input).AsNumber()));
        }

        [Fact]
        public void Warm_Direct_Statistics_Do_Not_Allocate_Per_Value_Or_Per_Call()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("VAR.P", out var function));
            var call = new FormulaFunctionContext(context);
            var values = new double[10000];
            for (var i = 0; i < values.Length; i++) values[i] = i;
            var arguments = new[] { Array(values) };
            for (var i = 0; i < 10; i++) function.Invoke(call, arguments);
            var start = GC.GetAllocatedBytesForCurrentThread();
            double checksum = 0;
            for (var i = 0; i < 20; i++) checksum += function.Invoke(call, arguments).AsNumber();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.True(checksum > 0);
            Assert.True(allocated < 4096, $"Warm statistics allocated {allocated} bytes.");
        }

        [Fact]
        public void Descriptive_Functions_Participate_In_Reference_Dirty_Recalculation()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            for (var row = 1; row <= 3; row++) sheet.GetCell(row, 1).Value = FormulaValue.FromNumber(row);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "DEVSQ(A1:A3)");
            var target = new FormulaCellAddress("Sheet1", 1, 3);
            engine.Recalculate(workbook, new[] { target });
            Assert.Equal(2, sheet.GetCell(1, 3).Value.AsNumber());
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(5);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 1) });
            Assert.Equal(8, sheet.GetCell(1, 3).Value.AsNumber());
        }

        private static readonly string[] Names = { "VAR.S", "VAR.P", "STDEV.S", "STDEV.P", "DEVSQ", "AVEDEV", "SKEW", "SKEW.P", "KURT", "SUMSQ", "GEOMEAN", "HARMEAN", "VARA", "VARPA", "STDEVA", "STDEVPA" };
        private static FormulaValue Array(params double[] values)
        {
            var result = new FormulaArray(values.Length, 1);
            for (var i = 0; i < values.Length; i++) result[i, 0] = FormulaValue.FromNumber(values[i]);
            return FormulaValue.FromArray(result);
        }
        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 8), registry ?? new ExcelFunctionRegistry());
        }
        private static void Close(double expected, double actual, double tolerance = 2e-13)
        {
            Assert.True(double.IsFinite(actual));
            if (expected == 0) Assert.True(Math.Abs(actual) < 1e-12, $"Expected zero, got {actual:R}");
            else Assert.True(Math.Abs((actual - expected) / expected) <= tolerance, $"Expected {expected:R}, got {actual:R}");
        }
        private static (double M2, double Skew, double Kurt) Oracle(int[] values)
        {
            BigInteger s1 = 0, s2 = 0, s3 = 0, s4 = 0;
            foreach (var value in values) { var x = new BigInteger(value); s1 += x; s2 += x * x; s3 += x * x * x; s4 += x * x * x * x; }
            var n = new BigInteger(values.Length);
            var m2 = (double)(n * s2 - s1 * s1) / (double)n;
            var m3 = (double)(n * n * s3 - 3 * n * s1 * s2 + 2 * s1 * s1 * s1) / (double)(n * n);
            var m4 = (double)(n * n * n * s4 - 4 * n * n * s1 * s3 + 6 * n * s1 * s1 * s2 - 3 * s1 * s1 * s1 * s1) / (double)(n * n * n);
            var count = (double)values.Length;
            var variance = m2 / (count - 1);
            return (m2, count * m3 / ((count - 1) * (count - 2) * Math.Pow(variance, 1.5)),
                count * (count + 1) * m4 / ((count - 1) * (count - 2) * (count - 3) * variance * variance) - 3 * (count - 1) * (count - 1) / ((count - 2) * (count - 3)));
        }
        private sealed class SourceFunction : IFormulaFunction
        {
            private readonly FormulaValue _value;
            public SourceFunction(FormulaValue value) { _value = value; }
            public int Calls { get; private set; }
            public string Name => "SOURCE";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) { Calls++; return _value; }
        }
    }
}
