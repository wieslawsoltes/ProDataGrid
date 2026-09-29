// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelScientificFunctionTests
    {
        [Theory]
        [InlineData("SIN(0.5)", 0.479425538604203)]
        [InlineData("COS(0.5)", 0.8775825618903727)]
        [InlineData("TAN(0.5)", 0.5463024898437905)]
        [InlineData("COT(0.5)", 1.830487721712452)]
        [InlineData("SEC(0.5)", 1.139493927324549)]
        [InlineData("CSC(0.5)", 2.085829642933488)]
        [InlineData("ASIN(0.5)", 0.5235987755982989)]
        [InlineData("ACOS(0.5)", 1.047197551196598)]
        [InlineData("ATAN(0.5)", 0.4636476090008061)]
        [InlineData("ACOT(0.5)", 1.107148717794091)]
        [InlineData("ACOT(-1)", 2.356194490192345)]
        [InlineData("SINH(0.5)", 0.5210953054937474)]
        [InlineData("COSH(0.5)", 1.127625965206381)]
        [InlineData("TANH(0.5)", 0.4621171572600098)]
        [InlineData("COTH(0.5)", 2.163953413738653)]
        [InlineData("SECH(0.5)", 0.8868188839700739)]
        [InlineData("CSCH(0.5)", 1.919034751334944)]
        [InlineData("ASINH(0.5)", 0.4812118250596034)]
        [InlineData("ACOSH(2)", 1.316957896924817)]
        [InlineData("ATANH(0.5)", 0.5493061443340548)]
        [InlineData("ACOTH(2)", 0.5493061443340548)]
        [InlineData("ACOTH(-2)", -0.5493061443340548)]
        [InlineData("DEGREES(PI())", 180)]
        [InlineData("RADIANS(180)", 3.141592653589793)]
        [InlineData("SQRTPI(2)", 2.506628274631001)]
        [InlineData("ATAN2(1,1)", 0.7853981633974483)]
        [InlineData("ATAN2(-1,-1)", -2.356194490192345)]
        [InlineData("ATAN2(-1,0)", 3.141592653589793)]
        [InlineData("ATAN2(0,1)", 1.570796326794897)]
        [InlineData("ATAN2(0,-1)", -1.570796326794897)]
        [InlineData("SIN(1E-12)", 1e-12)]
        [InlineData("SINH(1E-12)", 1e-12)]
        [InlineData("ASINH(1E-12)", 1e-12)]
        [InlineData("ATANH(1E-12)", 1e-12)]
        [InlineData("CSCH(1E-12)", 1e12)]
        [InlineData("COTH(1E-12)", 1e12)]
        [InlineData("ACOTH(1E100)", 1e-100)]
        [InlineData("SECH(1000)", 0)]
        [InlineData("CSCH(-1000)", 0)]
        [InlineData("TANH(1000)", 1)]
        [InlineData("COTH(-1000)", -1)]
        [InlineData("SQRTPI(1E308)", 1.772453850905516e154)]
        [InlineData("QUOTIENT(5,2)", 2)]
        [InlineData("QUOTIENT(-10,3)", -3)]
        [InlineData("QUOTIENT(10,-3)", -3)]
        [InlineData("EVEN(1.5)", 2)]
        [InlineData("EVEN(-1.5)", -2)]
        [InlineData("EVEN(0)", 0)]
        [InlineData("ODD(1.5)", 3)]
        [InlineData("ODD(-1.5)", -3)]
        [InlineData("ODD(0)", 1)]
        [InlineData("ODD(-3)", -3)]
        [InlineData("FACT(5)", 120)]
        [InlineData("FACT(5.9)", 120)]
        [InlineData("FACT(0)", 1)]
        [InlineData("FACTDOUBLE(6)", 48)]
        [InlineData("FACTDOUBLE(7)", 105)]
        [InlineData("FACTDOUBLE(0)", 1)]
        [InlineData("COMBIN(8,2)", 28)]
        [InlineData("COMBIN(8.9,2.9)", 28)]
        [InlineData("COMBIN(0,0)", 1)]
        [InlineData("COMBIN(1000,2)", 499500)]
        [InlineData("COMBIN(1E100,1)", 1e100)]
        [InlineData("PERMUT(100,3)", 970200)]
        [InlineData("PERMUT(3,0)", 1)]
        [InlineData("PERMUTATIONA(3,2)", 9)]
        [InlineData("PERMUTATIONA(2,3)", 8)]
        [InlineData("PERMUTATIONA(0,0)", 1)]
        [InlineData("SUM(SIN({0;0;0}))", 0)]
        [InlineData("SUM(MAP({3;4;5},LAMBDA(x,FACT(x))))", 150)]
        public void Scalar_Formulas_Agree_With_Reference_Values_In_All_Evaluation_Paths(string formula, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var reparsed = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false })
                foreach (var precision in new[] { true, false })
                {
                    context.Workbook.Settings.EnableCompiledExpressions = compiled;
                    context.Workbook.Settings.ApplyNumberPrecision = precision;
                    Close(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber());
                    Close(expected, evaluator.Evaluate(reparsed, context, new WorkbookValueResolver()).AsNumber());
                }
        }

        [Theory]
        [InlineData("ASIN(1.01)", FormulaErrorType.Num)]
        [InlineData("ACOS(-1.01)", FormulaErrorType.Num)]
        [InlineData("ACOSH(0.99)", FormulaErrorType.Num)]
        [InlineData("ATANH(1)", FormulaErrorType.Num)]
        [InlineData("ATANH(-1)", FormulaErrorType.Num)]
        [InlineData("ACOTH(1)", FormulaErrorType.Num)]
        [InlineData("ACOTH(0)", FormulaErrorType.Num)]
        [InlineData("COT(0)", FormulaErrorType.Div0)]
        [InlineData("CSC(0)", FormulaErrorType.Div0)]
        [InlineData("COTH(0)", FormulaErrorType.Div0)]
        [InlineData("CSCH(0)", FormulaErrorType.Div0)]
        [InlineData("ATAN2(0,0)", FormulaErrorType.Div0)]
        [InlineData("QUOTIENT(1,0)", FormulaErrorType.Div0)]
        [InlineData("QUOTIENT(1E308,1E-308)", FormulaErrorType.Num)]
        [InlineData("COSH(1000)", FormulaErrorType.Num)]
        [InlineData("SINH(-1000)", FormulaErrorType.Num)]
        [InlineData("SQRTPI(-1)", FormulaErrorType.Num)]
        [InlineData("DEGREES(1E308)", FormulaErrorType.Num)]
        [InlineData("SIN(NA())", FormulaErrorType.NA)]
        [InlineData("COS(\"invalid\")", FormulaErrorType.Value)]
        [InlineData("SIN()", FormulaErrorType.Value)]
        [InlineData("SIN(1,2)", FormulaErrorType.Value)]
        [InlineData("ATAN2(1)", FormulaErrorType.Value)]
        [InlineData("FACT(-1)", FormulaErrorType.Num)]
        [InlineData("FACT(171)", FormulaErrorType.Num)]
        [InlineData("FACTDOUBLE(301)", FormulaErrorType.Num)]
        [InlineData("COMBIN(-1,0)", FormulaErrorType.Num)]
        [InlineData("COMBIN(3,4)", FormulaErrorType.Num)]
        [InlineData("COMBIN(1E100,1E90)", FormulaErrorType.Num)]
        [InlineData("PERMUT(0,0)", FormulaErrorType.Num)]
        [InlineData("PERMUT(200,171)", FormulaErrorType.Num)]
        [InlineData("PERMUTATIONA(0,2)", FormulaErrorType.Num)]
        [InlineData("PERMUTATIONA(1000,1000)", FormulaErrorType.Num)]
        public void Invalid_Domains_And_Overflow_Return_Formula_Errors(string formula, FormulaErrorType error)
        {
            var context = Context();
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(error, new FormulaEvaluator().Evaluate(expression, context, new WorkbookValueResolver()).AsError().Type);
            }
        }

        [Fact]
        public void Reciprocal_Trigonometric_Functions_Enforce_Documented_Angle_Bounds()
        {
            var context = Context();
            foreach (var name in new[] { "COT", "SEC", "CSC", "COTH", "SECH", "CSCH" })
                foreach (var input in new[] { 134217728d, -134217728d, double.MaxValue, double.NaN, double.PositiveInfinity })
                    Assert.Equal(FormulaErrorType.Num, Invoke(context, name, FormulaValue.FromNumber(input)).AsError().Type);
        }

        [Fact]
        public void Arrays_Broadcast_Without_Mutating_Masks_Or_Allocating_Arguments_Per_Cell()
        {
            var context = Context();
            var left = new FormulaArray(2, 1, sparse: true);
            left[0, 0] = FormulaValue.FromNumber(5);
            var right = new FormulaArray(1, 2);
            right[0, 0] = FormulaValue.FromNumber(2);
            right[0, 1] = FormulaValue.FromNumber(0);
            var result = Invoke(context, "QUOTIENT", FormulaValue.FromArray(left), FormulaValue.FromArray(right)).AsArray();
            Assert.Equal(2, result[0, 0].AsNumber());
            Assert.Equal(0, result[1, 0].AsNumber());
            Assert.Equal(FormulaErrorType.Div0, result[0, 1].AsError().Type);
            Assert.Equal(FormulaErrorType.Div0, result[1, 1].AsError().Type);
            Assert.False(left.IsPresent(1, 0));
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "ATAN2", FormulaValue.FromArray(left), FormulaValue.FromArray(right)).AsError().Type);
        }

        [Fact]
        public void Primitive_Scalar_Calls_Are_Allocation_Free_After_Warmup()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("SIN", out var function));
            Assert.False(function is ILazyFormulaFunction);
            var arguments = new[] { FormulaValue.FromNumber(0.5) };
            var functionContext = new FormulaFunctionContext(context);
            // Warm the exact measurement method, including its result reads and counter API.
            // The previous shorter, different warmup loop could include one-time work in the
            // first measured batch. Keep the per-batch bound and require all seven samples.
            for (var i = 0; i < 3; i++) MeasurePrimitiveCalls(function, functionContext, arguments, out _);
            var measured = new long[7];
            for (var sample = 0; sample < measured.Length; sample++)
            {
                measured[sample] = MeasurePrimitiveCalls(function, functionContext, arguments, out var sum);
                Close(10000 * Math.Sin(0.5), sum);
            }
            for (var sample = 0; sample < measured.Length; sample++)
                Assert.True(measured[sample] < 4096,
                    $"Primitive SIN allocation samples (10,000 calls each): {string.Join(", ", measured)} bytes");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasurePrimitiveCalls(IFormulaFunction function, FormulaFunctionContext context,
            FormulaValue[] arguments, out double sum)
        {
            var start = GC.GetAllocatedBytesForCurrentThread();
            sum = 0;
            for (var i = 0; i < 10000; i++) sum += function.Invoke(context, arguments).AsNumber();
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }

        [Fact]
        public void Seeded_Trig_And_Hyperbolic_Identities_Hold_And_Culture_Is_Respected()
        {
            var context = Context();
            var random = new Random(59917);
            for (var i = 0; i < 1000; i++)
            {
                var x = random.NextDouble() * 20 - 10;
                var value = FormulaValue.FromNumber(x);
                var sin = Invoke(context, "SIN", value).AsNumber();
                var cos = Invoke(context, "COS", value).AsNumber();
                Close(1, sin * sin + cos * cos);
                Close(x, Invoke(context, "ASINH", Invoke(context, "SINH", value)).AsNumber());
                Close(x, Invoke(context, "ATANH", Invoke(context, "TANH", FormulaValue.FromNumber(x / 20))).AsNumber() * 20);
                Close(1, Invoke(context, "SECH", value).AsNumber() * Invoke(context, "COSH", value).AsNumber());
            }
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            Close(Math.Sin(0.5), Invoke(context, "SIN", FormulaValue.FromText("0,5")).AsNumber());
        }

        internal static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
        internal static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        internal static void Close(double expected, double actual)
        {
            Assert.True(double.IsFinite(actual));
            var difference = Math.Abs(actual - expected);
            Assert.True(expected == 0 ? difference <= 1e-14 : difference <= Math.Abs(expected) * 5e-13,
                $"Expected {expected:R}, actual {actual:R}");
        }
    }
}
