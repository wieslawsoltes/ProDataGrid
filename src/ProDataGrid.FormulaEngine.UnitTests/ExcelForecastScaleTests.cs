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
        [InlineData(1000,-1000)]
        [InlineData(-1000,1000)]
        [InlineData(1000,-1074)]
        [InlineData(-1074,1000)]
        [InlineData(900,-900)]
        [InlineData(-900,900)]
        public void Identity_Forecasts_Preserve_Targets_Outside_The_Training_Normalization_Scale(int trainingExponent,int targetExponent)
        {
            var workbook=new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision=false;
            var registry=new ExcelFunctionRegistry();
            var context=new FormulaFunctionContext(new FormulaEvaluationContext(workbook,workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1",1,1),registry));
            var training=new FormulaArray(3,1);
            var scale=Math.ScaleB(1d,trainingExponent);
            training[0,0]=FormulaValue.FromNumber(-scale);
            training[1,0]=FormulaValue.FromNumber(0);
            training[2,0]=FormulaValue.FromNumber(scale);
            Assert.True(registry.TryGetFunction("FORECAST.LINEAR",out var function));
            foreach(var sign in new[]{1d,-1d})
            {
                var target=sign*Math.ScaleB(1d,targetExponent);
                var actual=function.Invoke(context,new[]{FormulaValue.FromNumber(target),FormulaValue.FromArray(training),FormulaValue.FromArray(training)});
                Assert.Equal(BitConverter.DoubleToInt64Bits(target),BitConverter.DoubleToInt64Bits(actual.AsNumber()));
            }
        }
    }
}
