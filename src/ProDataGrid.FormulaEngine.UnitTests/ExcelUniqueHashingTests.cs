// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Globalization;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelUniqueHashingTests
    {
        [Fact]
        public void Indexed_Keys_Respect_Culture_Precision_Case_And_Error_Types()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            workbook.Settings.NumberPrecisionDigits = 10;
            var values = new[] { FormulaValue.FromNumber(1.25), FormulaValue.FromText("1,25"), FormulaValue.FromText("1,25000000001"),
                FormulaValue.FromText("word"), FormulaValue.FromText("WORD"),
                FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)),
                FormulaValue.FromError(new FormulaError(FormulaErrorType.Value)) };
            var array = new FormulaArray(values.Length, 1);
            for (var i = 0; i < values.Length; i++) array[i, 0] = values[i];
            var registry = new ExcelFunctionRegistry();
            Assert.True(registry.TryGetFunction("UNIQUE", out var function));
            var context = new FormulaFunctionContext(new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry));
            var result = function.Invoke(context, new[] { FormulaValue.FromArray(array) }).AsArray();
            Assert.Equal(4, result.RowCount);
            Assert.Equal(values[0], result[0, 0]);
            Assert.Equal(values[3], result[1, 0]);
            Assert.Equal(values[5], result[2, 0]);
            Assert.Equal(values[7], result[3, 0]);
        }

        [Fact]
        public void Nontransitive_Mixed_Coercion_Preserves_First_Representative_Behavior()
        {
            var workbook = new TestWorkbook("Book1");
            var registry = new ExcelFunctionRegistry();
            Assert.True(registry.TryGetFunction("UNIQUE", out var function));
            var context = new FormulaFunctionContext(new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry));
            var array = new FormulaArray(3, 1);
            array[0, 0] = FormulaValue.FromText("TRUE");
            array[1, 0] = FormulaValue.FromNumber(1);
            array[2, 0] = FormulaValue.FromBoolean(true);
            var result = function.Invoke(context, new[] { FormulaValue.FromArray(array), FormulaValue.FromBoolean(false), FormulaValue.FromBoolean(true) }).AsArray();
            Assert.Equal(1, result.RowCount);
            Assert.Equal(FormulaValue.FromNumber(1), result[0, 0]);
            array[0, 0] = FormulaValue.FromBoolean(true);
            array[2, 0] = FormulaValue.FromText("TRUE");
            var empty = function.Invoke(context, new[] { FormulaValue.FromArray(array), FormulaValue.FromBoolean(false), FormulaValue.FromBoolean(true) });
            Assert.Equal(FormulaErrorType.Calc, empty.AsError().Type);
        }
    }
}
