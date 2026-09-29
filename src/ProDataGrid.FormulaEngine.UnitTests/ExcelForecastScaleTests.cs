// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelForecastScaleTests
    {
        [Theory]
        [InlineData(1000, -1000)]
        [InlineData(-1000, 1000)]
        [InlineData(1000, -1074)]
        [InlineData(-1074, 1000)]
        [InlineData(900, -900)]
        [InlineData(-900, 900)]
        [InlineData(-1074, -1021)]
        [InlineData(0, 53)]
        [InlineData(500, 553)]
        public void Identity_Forecasts_Preserve_Targets_Outside_The_Training_Normalization_Scale(int trainingExponent, int targetExponent)
        {
            var (function, context) = Create();
            var training = Training(trainingExponent);
            var args = new[] { FormulaValue.Blank, training, training };
            Check(function, context, args, targetExponent);
        }

        [Fact]
        public void Identity_Forecasts_Preserve_Every_Finite_Power_Of_Two_Across_Training_Scales()
        {
            var (function, context) = Create();
            foreach (var exponent in new[] { -1074, -1000, -500, 0, 500, 1000, 1023 })
            {
                var training = Training(exponent);
                var args = new[] { FormulaValue.Blank, training, training };
                for (var target = -1074; target <= 1023; target++) Check(function, context, args, target);
            }
        }

        private static void Check(IFormulaFunction function, FormulaFunctionContext context, FormulaValue[] args, int exponent)
        {
            foreach (var sign in new[] { 1d, -1d })
            {
                var target = sign * Math.ScaleB(1d, exponent);
                args[0] = FormulaValue.FromNumber(target);
                var actual = function.Invoke(context, args);
                Assert.Equal(BitConverter.DoubleToInt64Bits(target), BitConverter.DoubleToInt64Bits(actual.AsNumber()));
            }
        }

        private static FormulaValue Training(int exponent)
        {
            var array = new FormulaArray(3, 1);
            var scale = Math.ScaleB(1d, exponent);
            array[0, 0] = FormulaValue.FromNumber(-scale);
            array[1, 0] = FormulaValue.FromNumber(0);
            array[2, 0] = FormulaValue.FromNumber(scale);
            return FormulaValue.FromArray(array);
        }

        private static (IFormulaFunction Function, FormulaFunctionContext Context) Create()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaFunctionContext(new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), registry));
            Assert.True(registry.TryGetFunction("FORECAST.LINEAR", out var function));
            return (function, context);
        }
    }
}
