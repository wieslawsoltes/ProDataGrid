// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelProductAggregateTests
    {
        [Theory]
        [InlineData("SUMPRODUCT({1,2,3},{4,5,6})", 32)]
        [InlineData("SUMPRODUCT({1,2;3,4},{5,6;7,8})", 70)]
        [InlineData("SUMPRODUCT({1,2;3,4})", 10)]
        [InlineData("SUMPRODUCT({1,2},{3,4},{5,6})", 63)]
        [InlineData("SUMPRODUCT(2,3,4)", 24)]
        [InlineData("SUMPRODUCT(\"2\",3)", 0)]
        [InlineData("SUMPRODUCT(TRUE,3)", 0)]
        [InlineData("SUMPRODUCT({1,\"2\",TRUE},{3,4,5})", 3)]
        [InlineData("SUMPRODUCT(--({1;2;3}>1),{10;20;30})", 50)]
        [InlineData("SUMPRODUCT(({1;2;3}>1)*{10;20;30})", 50)]
        [InlineData("LET(x,{1;2;3},SUMPRODUCT(x,x))", 14)]
        [InlineData("SUMPRODUCT({1E-12,2E-12},{2,3})", 8e-12)]
        [InlineData("SUMPRODUCT({1E16,1,-1E16},{1,1,1})", 1)]
        [InlineData("SUMX2MY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", -55)]
        [InlineData("SUMX2PY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", 521)]
        [InlineData("SUMXMY2({2,3,9,1,8,7,5},{6,5,11,7,5,4,4})", 79)]
        [InlineData("SUMX2MY2({1;2},{3,4})", -20)]
        [InlineData("SUMX2PY2({1;2},{3,4})", 30)]
        [InlineData("SUMXMY2({1;2},{3,4})", 8)]
        [InlineData("SUMX2MY2({1,\"2\",3},{4,5,TRUE})", -15)]
        [InlineData("SUMX2PY2({1,\"2\",3},{4,5,TRUE})", 17)]
        [InlineData("SUMXMY2({1,\"2\",3},{4,5,TRUE})", 9)]
        [InlineData("SUMXMY2({1,2,3},{1,2,3})", 0)]
        [InlineData("SUMX2PY2(3,4)", 25)]
        [InlineData("SUMPRODUCT(A1:B2,A1:B2)", 0)]
        public void Scalar_Results_Compose_In_Both_Evaluators_And_Formatter_RoundTrips(string formula, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var roundTrip = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
                foreach (var precision in new[] { true, false })
                {
                    context.Workbook.Settings.EnableCompiledExpressions = compiled;
                    context.Workbook.Settings.ApplyNumberPrecision = precision;
                    foreach (var candidate in new[] { expression, roundTrip })
                    {
                        var result = new FormulaEvaluator().Evaluate(candidate, context, new WorkbookValueResolver()).AsNumber();
                        Assert.True(Math.Abs(result - expected) <= Math.Max(1e-300, Math.Abs(expected)) * 2e-14);
                    }
                }
        }

        [Theory]
        [InlineData("SUMPRODUCT()", FormulaErrorType.Value)]
        [InlineData("SUMPRODUCT({1;2},{1,2})", FormulaErrorType.Value)]
        [InlineData("SUMPRODUCT({1;2},3)", FormulaErrorType.Value)]
        [InlineData("SUMPRODUCT(0,NA())", FormulaErrorType.NA)]
        [InlineData("SUMPRODUCT(0,1,NA())", FormulaErrorType.NA)]
        [InlineData("SUMPRODUCT(HSTACK(0,NA()),{0,0})", FormulaErrorType.NA)]
        [InlineData("SUMPRODUCT(1E308,2)", FormulaErrorType.Num)]
        [InlineData("SUMXMY2({1;2},3)", FormulaErrorType.NA)]
        [InlineData("SUMX2MY2(1)", FormulaErrorType.Value)]
        [InlineData("SUMX2PY2(NA(),0)", FormulaErrorType.NA)]
        [InlineData("SUMXMY2(\"text\",NA())", FormulaErrorType.NA)]
        [InlineData("SUMX2PY2(1E308,0)", FormulaErrorType.Num)]
        public void Invalid_Arguments_And_Numerical_Overflow_Return_Formula_Errors(string formula, FormulaErrorType expected)
        {
            var context = Context();
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(expected, new FormulaEvaluator().Evaluate(expression, context, new WorkbookValueResolver()).AsError().Type);
            }
        }

        [Fact]
        public void Sparse_Holes_And_Nonnumeric_Pairs_Preserve_Physical_Alignment()
        {
            var context = Context();
            var left = new FormulaArray(4, 1, sparse: true);
            var right = new FormulaArray(4, 1);
            for (var i = 0; i < 4; i++) right[i, 0] = FormulaValue.FromNumber(i + 4);
            left[0, 0] = FormulaValue.FromNumber(1);
            left.SetValue(1, 0, FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), present: false);
            left[2, 0] = FormulaValue.FromNumber(3);
            left[3, 0] = FormulaValue.FromText("4");
            Assert.Equal(22, Invoke(context, "SUMPRODUCT", left, right).AsNumber());
            Assert.Equal(18, Invoke(context, "SUMXMY2", left, right).AsNumber());
            Assert.False(left.IsPresent(1, 0));
            Assert.Equal(FormulaValueKind.Error, left[1, 0].Kind);
        }

        [Fact]
        public void Seeded_Paired_Aggregates_Agree_With_Independent_Integer_Loops()
        {
            var context = Context();
            var random = new Random(994017);
            for (var trial = 0; trial < 200; trial++)
            {
                var rows = random.Next(1, 20);
                var columns = random.Next(1, 9);
                var left = new FormulaArray(rows, columns, sparse: true);
                var right = new FormulaArray(rows, columns, sparse: true);
                double product = 0, difference = 0, squares = 0, distance = 0;
                for (var row = 0; row < rows; row++)
                    for (var column = 0; column < columns; column++)
                    {
                        var x = random.Next(-100, 101);
                        var y = random.Next(-100, 101);
                        var xNumeric = random.Next(5) != 0;
                        var yNumeric = random.Next(5) != 0;
                        left.SetValue(row, column, xNumeric ? FormulaValue.FromNumber(x) : FormulaValue.FromText("123"), xNumeric || random.Next(2) == 0);
                        right.SetValue(row, column, yNumeric ? FormulaValue.FromNumber(y) : FormulaValue.FromBoolean(true), yNumeric || random.Next(2) == 0);
                        if (xNumeric && yNumeric)
                        {
                            product += x * y;
                            difference += x * x - y * y;
                            squares += x * x + y * y;
                            distance += (x - y) * (x - y);
                        }
                    }
                Assert.Equal(product, Invoke(context, "SUMPRODUCT", left, right).AsNumber());
                Assert.Equal(difference, Invoke(context, "SUMX2MY2", left, right).AsNumber());
                Assert.Equal(squares, Invoke(context, "SUMX2PY2", left, right).AsNumber());
                Assert.Equal(distance, Invoke(context, "SUMXMY2", left, right).AsNumber());
            }
        }

        [Fact]
        public void Parameter_Bounds_Pooled_Error_Paths_And_Concurrent_Use_Are_Safe()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("SUMPRODUCT", out var function));
            var call = new FormulaFunctionContext(context);
            var arguments = new FormulaValue[255];
            Array.Fill(arguments, FormulaValue.FromNumber(1));
            Assert.Equal(1, function.Invoke(call, arguments).AsNumber());
            Assert.Equal(FormulaErrorType.Value, function.Invoke(call, new FormulaValue[256]).AsError().Type);
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                arguments[254] = FormulaValue.FromNumber(bad);
                Assert.Equal(FormulaErrorType.Num, function.Invoke(call, arguments).AsError().Type);
            }
            arguments[254] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA, "source error"));
            Assert.Equal("source error", function.Invoke(call, arguments).AsError().Message);
            arguments[254] = FormulaValue.FromNumber(2);
            Parallel.For(0, 64, i => Assert.Equal(2, function.Invoke(call, arguments).AsNumber()));
        }

        [Fact]
        public void Warm_Common_Calls_Allocate_No_Product_Array_Or_Cell_Arguments()
        {
            var context = Context();
            var data = new FormulaArray(10000, 1);
            for (var i = 0; i < data.RowCount; i++) data[i, 0] = FormulaValue.FromNumber(1);
            var arguments = new[] { FormulaValue.FromArray(data), FormulaValue.FromArray(data) };
            var call = new FormulaFunctionContext(context);
            foreach (var name in new[] { "SUMPRODUCT", "SUMXMY2", "SUMX2PY2", "SUMX2MY2" })
            {
                Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
                for (var i = 0; i < 20; i++) function.Invoke(call, arguments);
                var before = GC.GetAllocatedBytesForCurrentThread();
                double checksum = 0;
                for (var i = 0; i < 50; i++) checksum += function.Invoke(call, arguments).AsNumber();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(name == "SUMPRODUCT" ? 500000d : name == "SUMX2PY2" ? 1000000d : 0, checksum);
                Assert.True(allocated < 4096, $"{name} allocated {allocated} bytes over 50 calls.");
            }
        }

        [Fact]
        public void Workbook_References_And_Dirty_Dependencies_Recalculate()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            for (var i = 1; i <= 3; i++)
            {
                sheet.GetCell(i, 1).Value = FormulaValue.FromNumber(i);
                sheet.GetCell(i, 2).Value = FormulaValue.FromNumber(i * 2);
            }
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 4, "SUMPRODUCT(A1:A3,B1:B3)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4) });
            Assert.Equal(28, sheet.GetCell(1, 4).Value.AsNumber());
            sheet.GetCell(2, 2).Value = FormulaValue.FromNumber(10);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 2) });
            Assert.Equal(40, sheet.GetCell(1, 4).Value.AsNumber());
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, FormulaArray left, FormulaArray right)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), new[] { FormulaValue.FromArray(left), FormulaValue.FromArray(right) });
        }
    }
}
