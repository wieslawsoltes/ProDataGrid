// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelMatrixScalingTests
    {
        [Theory]
        [InlineData(1e-308)]
        [InlineData(1e-12)]
        [InlineData(1e-7)]
        public void Optional_Scaling_Does_Not_Reject_Or_Discard_Wide_Row_Dynamic_Range(double offDiagonal)
        {
            var context = Context();
            var source = new FormulaArray(2, 2);
            source[0, 0] = FormulaValue.FromNumber(1e308);
            source[0, 1] = FormulaValue.FromNumber(offDiagonal);
            source[1, 0] = FormulaValue.FromNumber(0);
            source[1, 1] = FormulaValue.FromNumber(1);
            Assert.Equal(1e308, Invoke(context, "MDETERM", source).AsNumber());
            var inverse = Invoke(context, "MINVERSE", source).AsArray();
            Assert.Equal(1e-308, inverse[0, 0].AsNumber());
            Assert.Equal(-offDiagonal / 1e308, inverse[0, 1].AsNumber());
            Assert.Equal(0, inverse[1, 0].AsNumber());
            Assert.Equal(1, inverse[1, 1].AsNumber());
            Assert.Equal(offDiagonal, source[0, 1].AsNumber());
        }

        [Fact]
        public void A_Very_Small_Nonzero_Pivot_Is_Not_Classified_By_An_Absolute_Epsilon()
        {
            var context = Context();
            var source = new FormulaArray(2, 2);
            source[0, 0] = FormulaValue.FromNumber(1e-300);
            source[0, 1] = FormulaValue.FromNumber(0);
            source[1, 0] = FormulaValue.FromNumber(0);
            source[1, 1] = FormulaValue.FromNumber(2e-300);
            var inverse = Invoke(context, "MINVERSE", source).AsArray();
            Assert.InRange(inverse[0, 0].AsNumber() / 1e300, 1 - 1e-14, 1 + 1e-14);
            Assert.InRange(inverse[1, 1].AsNumber() / 5e299, 1 - 1e-14, 1 + 1e-14);
            // The mathematical determinant is nonzero but below binary64's representable range.
            Assert.Equal(0, Invoke(context, "MDETERM", source).AsNumber());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, FormulaArray data)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), new[] { FormulaValue.FromArray(data) });
        }
        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
    }
}
