// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelRadixUInt64RoundingTests
    {
        [Theory]
        [InlineData("1Y2P0IJ32ECCH", 4890909195324358659L)]
        [InlineData("1Y2P0IJ32EH33", 4890909195324358661L)]
        public void HighBit_Conversion_Preserves_Values_Adjacent_To_Halfway(string text, long bits)
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("DECIMAL", out var function));
            var value = function.Invoke(new FormulaFunctionContext(context),
                new[] { FormulaValue.FromText(text), FormulaValue.FromNumber(36) });
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(value.AsNumber()));
        }

        [Fact]
        public void UInt64_Boundary_Neighborhoods_Agree_With_Independent_Decimal_Parsing()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("DECIMAL", out var function));
            var call = new FormulaFunctionContext(context);
            var args = new[] { FormulaValue.Blank, FormulaValue.FromNumber(16) };
            foreach (var center in new[] { 1UL << 53, 1UL << 54, 1UL << 63, ulong.MaxValue - 4096 })
            {
                for (var offset = -4096; offset <= 4096; offset++)
                {
                    var number = offset < 0 ? center - (ulong)(-offset) : center + (ulong)offset;
                    args[0] = FormulaValue.FromText(number.ToString("X", CultureInfo.InvariantCulture));
                    var expected = double.Parse(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    var actual = function.Invoke(call, args).AsNumber();
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
                }
            }
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
    }
}
