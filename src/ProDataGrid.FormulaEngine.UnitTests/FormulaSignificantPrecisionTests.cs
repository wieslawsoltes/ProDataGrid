// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaSignificantPrecisionTests
    {
        [Theory]
        [InlineData(1e-12, 15, 1e-12)]
        [InlineData(-1e-12, 15, -1e-12)]
        [InlineData(1e-100, 15, 1e-100)]
        [InlineData(1e-300, 15, 1e-300)]
        [InlineData(1.23456789e-20, 5, 1.2346e-20)]
        [InlineData(-1.23456789e-20, 5, -1.2346e-20)]
        [InlineData(1.23456789e200, 5, 1.2346e200)]
        [InlineData(123456789, 5, 123460000)]
        [InlineData(123.456789, 5, 123.46)]
        [InlineData(1.25, 2, 1.3)]
        [InlineData(-1.25, 2, -1.3)]
        [InlineData(125, 2, 130)]
        [InlineData(-125, 2, -130)]
        [InlineData(0.000125, 2, 0.00013)]
        [InlineData(999.999, 3, 1000)]
        [InlineData(0.0999999, 3, 0.1)]
        [InlineData(1000001, 15, 1000001)]
        [InlineData(0.30000000000000004, 15, 0.3)]
        public void Significant_Digit_Rounding_Uses_Relative_Scale(double input, int digits, double expected)
        {
            Close(expected, FormulaNumberUtilities.ApplyPrecision(input, digits), 5e-15);
        }

        [Theory]
        [InlineData("1E-12", 1e-12)]
        [InlineData("1E-12+2E-12", 3e-12)]
        [InlineData("1/1000000000000", 1e-12)]
        [InlineData("SUM({1E-12,2E-12})", 3e-12)]
        [InlineData("AVERAGE({1E-12,3E-12})", 2e-12)]
        [InlineData("LET(x,1E-12,x+x)", 2e-12)]
        [InlineData("SUM(MAP({1E-12;2E-12},LAMBDA(x,x*2)))", 6e-12)]
        [InlineData("IF(1E-12=0,0,1)", 1)]
        [InlineData("IF(1E-12>0,1,0)", 1)]
        [InlineData("IF(-1E-12<0,1,0)", 1)]
        [InlineData("0.1+0.2", 0.3)]
        public void Both_Evaluators_And_Array_Aggregates_Preserve_Small_Nonzero_Values(string formula, double expected)
        {
            var workbook = new TestWorkbook("Book1");
            var context = new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { false, true })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                workbook.Settings.ApplyNumberPrecision = true;
                Close(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber(), 1e-14);
            }
        }

        [Fact]
        public void Finite_Extremes_Subnormals_And_Passthrough_Settings_Do_Not_Throw_Or_Become_Zero()
        {
            var values = new[] { double.Epsilon, -double.Epsilon, 1e-320, -1e-310, 1e-300,
                double.MaxValue, double.MinValue, Math.BitDecrement(1d), Math.BitIncrement(1d), -0d };
            foreach (var value in values)
            {
                for (var digits = 1; digits <= 16; digits++)
                {
                    var result = FormulaNumberUtilities.ApplyPrecision(value, digits);
                    Assert.True(double.IsFinite(result));
                    if (value != 0) Assert.NotEqual(0d, result);
                    Assert.Equal(Math.Sign(value), Math.Sign(result));
                }
                foreach (var digits in new[] { int.MinValue, 0, 17, 100, int.MaxValue })
                    Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(FormulaNumberUtilities.ApplyPrecision(value, digits)));
            }
            Assert.True(double.IsNaN(FormulaNumberUtilities.ApplyPrecision(double.NaN, 15)));
            Assert.Equal(double.PositiveInfinity, FormulaNumberUtilities.ApplyPrecision(double.PositiveInfinity, 15));
            Assert.Equal(double.NegativeInfinity, FormulaNumberUtilities.ApplyPrecision(double.NegativeInfinity, 15));
            Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(FormulaNumberUtilities.ApplyPrecision(-0d, 15)));
        }

        [Fact]
        public void Seeded_Nonmidpoint_Values_Agree_With_Independent_Decimal_Formatting_Across_600_Decades()
        {
            var random = new Random(82139);
            for (var i = 0; i < 6000; i++)
            {
                var value = (1 + random.NextDouble() * 8) * Math.Pow(10, random.Next(-300, 301));
                if ((i & 1) != 0) value = -value;
                var digits = random.Next(2, 16);
                // General formatting supplies an independent significant-digit reference. Random
                // inputs exclude deliberate ties; exact midpoint direction is tested separately.
                var reference = double.Parse(value.ToString("G" + digits, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                var actual = FormulaNumberUtilities.ApplyPrecision(value, digits);
                Close(reference, actual, 3e-14);
            }
        }

        [Fact]
        public void Precision_Does_Not_Use_Process_Culture_Or_Shared_Mutable_State()
        {
            Parallel.For(0, 256, i =>
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(i % 2 == 0 ? "pl-PL" : "tr-TR");
                Close(1.2346e-80, FormulaNumberUtilities.ApplyPrecision(1.23456789e-80, 5), 1e-14);
            });
        }

        private static void Close(double expected, double actual, double tolerance)
        {
            Assert.True(double.IsFinite(actual));
            if (expected == 0) Assert.Equal(expected, actual);
            else Assert.True(Math.Abs((actual - expected) / expected) <= tolerance,
                $"Expected {expected:R}; actual {actual:R}; relative error {Math.Abs((actual - expected) / expected):R}");
        }
    }
}
