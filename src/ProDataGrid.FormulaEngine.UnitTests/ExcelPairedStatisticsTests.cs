// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelPairedStatisticsTests
    {
        [Theory]
        [InlineData("COVARIANCE.P({3,2,4,5,6},{9,7,12,15,17})", 5.2)]
        [InlineData("COVARIANCE.S({2,4,8},{5,11,12})", 9.666666666666666)]
        [InlineData("COVAR({1,2,3},{2,4,6})", 1.3333333333333333)]
        [InlineData("COVARIANCE.P(2,3)", 0)]
        [InlineData("COVARIANCE.P(TRUE,\"3\")", 0)]
        [InlineData("CORREL({1,2,3},{2,4,6})", 1)]
        [InlineData("CORREL({1;2;3},{6,4,2})", -1)]
        [InlineData("CORREL({-1,0,1},{1,0,1})", 0)]
        [InlineData("PEARSON({9,7,5,3,1},{10,6,1,5,3})", 0.6993786061802354)]
        [InlineData("RSQ({1,2,3},{6,4,2})", 1)]
        [InlineData("SLOPE({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", 0.3055555555555556)]
        [InlineData("INTERCEPT({2,3,9,1,8},{6,5,11,7,5})", 0.04838709677419355)]
        [InlineData("SLOPE({3;5;7},{1,2,3})", 2)]
        [InlineData("INTERCEPT({3;5;7},{1,2,3})", 1)]
        [InlineData("STEYX({3,5,7},{1,2,3})", 0)]
        [InlineData("STEYX({1,0,1},{-1,0,1})", 0.816496580927726)]
        [InlineData("SLOPE({7,7,7},{1,2,3})", 0)]
        [InlineData("INTERCEPT({7,7,7},{1,2,3})", 7)]
        [InlineData("FORECAST.LINEAR(30,{6,7,9,15,21},{20,28,31,38,40})", 10.607253086419753)]
        [InlineData("FORECAST(4,{3,5,7},{1,2,3})", 9)]
        [InlineData("FORECAST.LINEAR(1E308,{7,7,7},{1,2,3})", 7)]
        [InlineData("SUM(FORECAST.LINEAR({4;5;6},{3,5,7},{1,2,3}))", 33)]
        [InlineData("CORREL({1,\"x\",3,4},{2,100,6,8})", 1)]
        [InlineData("COVARIANCE.P({1,\"x\",3,4},{2,100,6,8})", 3.111111111111111)]
        [InlineData("CORREL({1,TRUE,3,4},{2,100,6,8})", 1)]
        [InlineData("COVARIANCE.P({1,2,3},{0,0,0})", 0)]
        public void Examples_Compose_In_Both_Evaluators_And_Formatter(string formula, double expected)
        {
            var context = Context();
            var expression = Parse(formula);
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Near(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber());
                Near(expected, evaluator.Evaluate(Parse(formatted), context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Theory]
        [InlineData("CORREL({1,2},1)", FormulaErrorType.NA)]
        [InlineData("COVARIANCE.P({1,2},1)", FormulaErrorType.NA)]
        [InlineData("CORREL({1,1},{2,3})", FormulaErrorType.Div0)]
        [InlineData("CORREL({1,2},{3,3})", FormulaErrorType.Div0)]
        [InlineData("CORREL(1,2)", FormulaErrorType.Div0)]
        [InlineData("CORREL(A1,B1)", FormulaErrorType.Div0)]
        [InlineData("PEARSON(A1,B1)", FormulaErrorType.NA)]
        [InlineData("RSQ(A1,B1)", FormulaErrorType.NA)]
        [InlineData("SLOPE(A1,B1)", FormulaErrorType.NA)]
        [InlineData("INTERCEPT(A1,B1)", FormulaErrorType.NA)]
        [InlineData("COVARIANCE.P(A1,B1)", FormulaErrorType.Div0)]
        [InlineData("COVARIANCE.S(1,2)", FormulaErrorType.Div0)]
        [InlineData("STEYX(A1,B1)", FormulaErrorType.Div0)]
        [InlineData("STEYX({1,2},{2,3})", FormulaErrorType.Div0)]
        [InlineData("STEYX({1,2,3},{2,2,2})", FormulaErrorType.Div0)]
        [InlineData("SLOPE({1,2,3},{2,2,2})", FormulaErrorType.Div0)]
        [InlineData("FORECAST.LINEAR(1,A1,B1)", FormulaErrorType.NA)]
        [InlineData("FORECAST.LINEAR(1,{1,2},{2,2})", FormulaErrorType.Div0)]
        [InlineData("FORECAST.LINEAR(\"bad\",{1,2},{2,3})", FormulaErrorType.Value)]
        [InlineData("CORREL({1,\"x\",3},{2,#N/A,6})", FormulaErrorType.NA)]
        [InlineData("CORREL({1,#DIV/0!,3},{2,\"x\",6})", FormulaErrorType.Div0)]
        public void Error_Contracts_Are_Preserved(string formula, FormulaErrorType error)
        {
            var context = Context();
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(error, new FormulaEvaluator().Evaluate(Parse(formula), context, new WorkbookValueResolver()).AsError().Type);
            }
        }

        [Fact]
        public void Forecast_Array_Elements_And_Literal_Reference_Provenance_Are_Independent()
        {
            var context = Context();
            context.Worksheet.GetCell(1, 1).Value = FormulaValue.FromBoolean(true);
            context.Worksheet.GetCell(1, 2).Value = FormulaValue.FromNumber(3);
            var evaluator = new FormulaEvaluator();
            var resolver = new WorkbookValueResolver();
            Assert.Equal(FormulaErrorType.Div0, evaluator.Evaluate(Parse("COVARIANCE.P(A1,B1)"), context, resolver).AsError().Type);
            var array = evaluator.Evaluate(Parse("FORECAST.LINEAR({4,#N/A,\"bad\"},{3,5,7},{1,2,3})"), context, resolver).AsArray();
            Assert.Equal(9, array[0, 0].AsNumber());
            Assert.Equal(FormulaErrorType.NA, array[0, 1].AsError().Type);
            Assert.Equal(FormulaErrorType.Value, array[0, 2].AsError().Type);
        }

        [Fact]
        public void Pair_Filtering_Preserves_Indices_Masks_Errors_And_Source_Ownership()
        {
            var context = Context();
            var x = new FormulaArray(4, 1, sparse: true);
            var y = new FormulaArray(1, 4);
            x[0, 0] = FormulaValue.FromNumber(1);
            x[2, 0] = FormulaValue.FromNumber(3);
            x[3, 0] = FormulaValue.FromNumber(4);
            y[0, 0] = FormulaValue.FromNumber(3);
            y[0, 1] = FormulaValue.FromNumber(-999);
            y[0, 2] = FormulaValue.FromNumber(7);
            y[0, 3] = FormulaValue.FromNumber(9);
            var args = new[] { FormulaValue.FromArray(y), FormulaValue.FromArray(x) };
            Assert.Equal(2, Invoke(context, "SLOPE", args).AsNumber());
            Assert.False(x.IsPresent(1, 0));
            Assert.Equal(1, x[0, 0].AsNumber());
            Assert.Equal(-999, y[0, 1].AsNumber());
            var error = new FormulaError(FormulaErrorType.Value, "retain diagnostic");
            y[0, 1] = FormulaValue.FromError(error);
            Assert.Equal(error, Invoke(context, "SLOPE", args).AsError());
        }

        [Fact]
        public void Cross_Axis_Scaling_Does_Not_Require_Representable_Slope_Or_Variance()
        {
            var context = Context();
            var huge = Math.ScaleB(1d, 1000);
            var tiny = Math.ScaleB(1d, -1000);
            var x = Values(-huge, 0, huge);
            var y = Values(-tiny, 0, tiny);
            Assert.Equal(1, Invoke(context, "CORREL", x, y).AsNumber());
            Near(2d / 3, Invoke(context, "COVARIANCE.P", x, y).AsNumber());
            Assert.Equal(0, Invoke(context, "SLOPE", y, x).AsNumber());
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "SLOPE", x, y).AsError().Type);
            Assert.Equal(huge, Invoke(context, "FORECAST.LINEAR", FormulaValue.FromNumber(tiny), x, y).AsNumber());
            Assert.Equal(tiny, Invoke(context, "FORECAST.LINEAR", FormulaValue.FromNumber(huge), y, x).AsNumber());
            Assert.Equal(huge, Invoke(context, "FORECAST.LINEAR", FormulaValue.FromNumber(huge), y, y).AsNumber());
            Assert.Equal(1, Invoke(context, "CORREL", x, x).AsNumber());
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "COVARIANCE.P", x, x).AsError().Type);
            Assert.Equal(0, Invoke(context, "STEYX", x, x).AsNumber());
            var subnormal = Values(double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon);
            var doubled = Values(2 * double.Epsilon, 4 * double.Epsilon, 6 * double.Epsilon);
            Assert.Equal(2, Invoke(context, "SLOPE", doubled, subnormal).AsNumber());
            Assert.Equal(8 * double.Epsilon, Invoke(context, "FORECAST.LINEAR", FormulaValue.FromNumber(4 * double.Epsilon), doubled, subnormal).AsNumber());
        }

        [Fact]
        public void Near_Exact_Fit_Retains_Residual_Instead_Of_Subtracting_Large_Moments()
        {
            var context = Context();
            var x = new long[] { -2, -1, 0, 1, 2 };
            var y = new long[] { -4L * (1L << 30), -2L * (1L << 30) + 1, 0, 2L * (1L << 30) + 1, 4L * (1L << 30) };
            var stats = Exact(x, y);
            var expected = Math.ScaleB(Math.Sqrt(stats.Residual / 3), -30);
            var actual = Invoke(context, "STEYX", Scaled(y, -30), Scaled(x, 0)).AsNumber();
            Assert.True(actual > 0);
            Assert.True(Math.Abs((actual - expected) / expected) < 2e-6);
        }

        [Fact]
        public void Seeded_Results_Agree_With_Exact_Integer_Central_Sums_At_Large_Offsets()
        {
            var context = Context();
            var random = new Random(58219);
            for (var iteration = 0; iteration < 180; iteration++)
            {
                var n = random.Next(3, 65);
                var x = new long[n];
                var y = new long[n];
                for (var i = 0; i < n; i++)
                {
                    var value = random.Next(-200, 201);
                    x[i] = (1L << 42) + value;
                    y[i] = (1L << 40) + 3 * value + random.Next(-20, 21);
                }
                var ex = (iteration % 3 - 1) * 450;
                var ey = (iteration / 3 % 3 - 1) * 450;
                var oracle = Exact(x, y);
                var xv = Scaled(x, ex);
                var yv = Scaled(y, ey);
                Near(Math.ScaleB(oracle.Covariance, ex + ey), Invoke(context, "COVARIANCE.P", yv, xv).AsNumber());
                Near(Math.ScaleB(oracle.Covariance * n / (n - 1), ex + ey), Invoke(context, "COVARIANCE.S", yv, xv).AsNumber());
                Near(oracle.Correlation, Invoke(context, "CORREL", yv, xv).AsNumber());
                Near(oracle.Correlation * oracle.Correlation, Invoke(context, "RSQ", yv, xv).AsNumber());
                Near(Math.ScaleB(oracle.Slope, ey - ex), Invoke(context, "SLOPE", yv, xv).AsNumber());
                Near(Math.ScaleB(oracle.Intercept, ey), Invoke(context, "INTERCEPT", yv, xv).AsNumber());
                Near(Math.ScaleB(Math.Sqrt(oracle.Residual / (n - 2)), ey), Invoke(context, "STEYX", yv, xv).AsNumber());
                var target = (1L << 42) + 100;
                Near(Math.ScaleB(oracle.Forecast(target), ey), Invoke(context, "FORECAST.LINEAR", FormulaValue.FromNumber(Math.ScaleB(target, ex)), yv, xv).AsNumber());
                Array.Reverse(x);
                Array.Reverse(y);
                Near(oracle.Correlation, Invoke(context, "PEARSON", Scaled(y, ey), Scaled(x, ex)).AsNumber());
            }
        }

        [Fact]
        public void Precision_Policy_Applies_To_Observed_Values_And_Result()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            var actual = Invoke(context, "SLOPE", Values(2.0001, 4.0002, 6.0003), Values(1.0001, 2.0002, 3.0003));
            Assert.Equal(2, actual.AsNumber());
        }

        [Fact]
        public void Forecast_Fits_Once_And_Does_Not_Cache_Mutable_Input_Values()
        {
            var context = Context();
            var source = new InputFunction();
            ((ExcelFunctionRegistry)context.FunctionRegistry).Register(source);
            var evaluator = new FormulaEvaluator();
            var formula = Parse("FORECAST.LINEAR(SEQUENCE(100),OBSERVED(),{1,2,3})");
            var first = evaluator.Evaluate(formula, context, new WorkbookValueResolver()).AsArray();
            Assert.Equal(1, source.Calls);
            Assert.Equal(201, first[99, 0].AsNumber());
            source.Offset = 10;
            var second = evaluator.Evaluate(formula, context, new WorkbookValueResolver()).AsArray();
            Assert.Equal(2, source.Calls);
            Assert.Equal(211, second[99, 0].AsNumber());
            Assert.Equal(201, first[99, 0].AsNumber());
        }

        [Fact]
        public void Forecast_Spill_Recalculates_When_A_Training_Cell_Changes()
        {
            var context = Context();
            var sheet = context.Worksheet;
            for (var i = 1; i <= 3; i++)
            {
                sheet.GetCell(i, 1).Value = FormulaValue.FromNumber(i);
                sheet.GetCell(i, 2).Value = FormulaValue.FromNumber(2 * i + 1);
            }
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet, 1, 4, "FORECAST.LINEAR({4;5},B1:B3,A1:A3)");
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4) });
            Assert.Equal(11, sheet.GetCell(2, 4).Value.AsNumber());
            sheet.GetCell(3, 2).Value = FormulaValue.FromNumber(10);
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 3, 2) });
            Near(16.5, sheet.GetCell(2, 4).Value.AsNumber());
        }

        [Fact]
        public void Bounded_Pooled_Workspaces_Reject_Errors_And_Remain_Independent()
        {
            var context = Context();
            var x = new FormulaArray(4096, 1);
            var y = new FormulaArray(4096, 1);
            for (var i = 0; i < 4096; i++) { x[i, 0] = FormulaValue.FromNumber(i); y[i, 0] = FormulaValue.FromNumber(3 * i + 7); }
            var args = new[] { FormulaValue.FromArray(y), FormulaValue.FromArray(x) };
            Parallel.For(0, 64, _ => Near(3, Invoke(context, "SLOPE", args).AsNumber()));
            y[4095, 0] = FormulaValue.FromNumber(double.NaN);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "SLOPE", args).AsError().Type);
            y[4095, 0] = FormulaValue.FromNumber(3 * 4095 + 7);
            Near(3, Invoke(context, "SLOPE", args).AsNumber());
            context.Workbook.Settings.MaximumArrayCellCount = 10;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "SLOPE", args).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "FORECAST.LINEAR", FormulaValue.FromArray(x), Values(3,5,7), Values(1,2,3)).AsError().Type);
        }

        [Fact]
        public void Warm_Scalar_And_Pooled_Statistics_Do_Not_Allocate_Per_Call()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("SLOPE", out var function));
            var args = new[] { Values(3,5,7), Values(1,2,3) };
            var call = new FormulaFunctionContext(context);
            for (var i = 0; i < 100; i++) function.Invoke(call, args);
            var measurement = Measure(function, call, args);
            Assert.Equal(2000, measurement.Total);
            Assert.True(measurement.Bytes < 4096, $"Warm allocation: {measurement.Bytes}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (double Total, long Bytes) Measure(IFormulaFunction function, FormulaFunctionContext context, FormulaValue[] args)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            double total = 0;
            for (var i = 0; i < 1000; i++) total += function.Invoke(context, args).AsNumber();
            return (total, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static ExactPair Exact(long[] x, long[] y) => new ExactPair(x, y);
        private sealed class ExactPair
        {
            private readonly BigInteger _sx, _sy, _xx, _yy, _xy;
            private readonly int _n;
            public ExactPair(long[] x, long[] y)
            {
                _n = x.Length;
                BigInteger sx=0, sy=0, xx=0, yy=0, xy=0;
                for(var i=0;i<_n;i++) { BigInteger a=x[i], b=y[i]; sx+=a; sy+=b; xx+=a*a; yy+=b*b; xy+=a*b; }
                _sx=sx; _sy=sy; _xx=_n*xx-sx*sx; _yy=_n*yy-sy*sy; _xy=_n*xy-sx*sy;
            }
            public double Covariance => (double)_xy / (_n * _n);
            public double Correlation => (double)_xy / Math.Sqrt((double)_xx * (double)_yy);
            public double Slope => (double)_xy / (double)_xx;
            public double Intercept => (double)(_sy*_xx-_xy*_sx) / (double)(_n*_xx);
            public double Residual => (double)(_yy*_xx-_xy*_xy) / (double)(_n*_xx);
            public double Forecast(long x) => (double)(_sy*_xx+_xy*(_n*(BigInteger)x-_sx)) / (double)(_n*_xx);
        }

        private sealed class InputFunction : IFormulaFunction
        {
            public string Name => "OBSERVED";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0,0);
            public int Calls { get; private set; }
            public double Offset { get; set; }
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) { Calls++; return Values(3+Offset,5+Offset,7+Offset); }
        }
        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1",1,1), new ExcelFunctionRegistry());
        }
        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
        private static FormulaValue Values(params double[] values)
        {
            var array = new FormulaArray(values.Length, 1);
            for(var i=0;i<values.Length;i++) array[i,0]=FormulaValue.FromNumber(values[i]);
            return FormulaValue.FromArray(array);
        }
        private static FormulaValue Scaled(long[] values, int exponent)
        {
            var array = new FormulaArray(values.Length,1);
            for(var i=0;i<values.Length;i++) array[i,0]=FormulaValue.FromNumber(Math.ScaleB(values[i],exponent));
            return FormulaValue.FromArray(array);
        }
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name,out var function));
            return function.Invoke(new FormulaFunctionContext(context),args);
        }
        private static void Near(double expected,double actual)
        {
            Assert.True(double.IsFinite(actual));
            Assert.True(expected==0 ? Math.Abs(actual)<1e-11 : Math.Abs((actual-expected)/expected)<1e-11, $"Expected {expected:R}, actual {actual:R}");
        }
    }
}
