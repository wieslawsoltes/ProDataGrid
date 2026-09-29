// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelPairedWorkspaceTests
    {
        [Theory]
        [InlineData("SLOPE", 3d)]
        [InlineData("CORREL", 1d)]
        [InlineData("COVARIANCE.P", 4194303.75d)]
        public void Warm_Large_Sample_Calls_Return_Their_Pooled_Workspaces_Without_Managed_Allocation(string name, double expected)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var registry = new ExcelFunctionRegistry();
            var context = new FormulaFunctionContext(new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), registry));
            var x = new FormulaArray(4096, 1);
            var y = new FormulaArray(4096, 1);
            for (var i = 0; i < x.RowCount; i++)
            {
                x[i, 0] = FormulaValue.FromNumber(i);
                y[i, 0] = FormulaValue.FromNumber(3 * i + 7);
            }
            var args = new[] { FormulaValue.FromArray(y), FormulaValue.FromArray(x) };
            Assert.True(registry.TryGetFunction(name, out var function));
            for (var i = 0; i < 64; i++) function.Invoke(context, args);
            var measured = Measure(function, context, args);
            Assert.True(Math.Abs(measured.Total - expected * 100) <= Math.Abs(expected) * 1e-10);
            Assert.Equal(0, measured.Bytes);
            Assert.Equal(0, x[0, 0].AsNumber());
            Assert.Equal(3 * 4095 + 7, y[4095, 0].AsNumber());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (double Total, long Bytes) Measure(IFormulaFunction function, FormulaFunctionContext context, FormulaValue[] args)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            double total = 0;
            for (var i = 0; i < 100; i++) total += function.Invoke(context, args).AsNumber();
            return (total, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
