// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelMatrixFunctionTests
    {
        [Theory]
        [InlineData("MMULT({1,2;3,4},{5,6;7,8})", 2, 2, new double[] { 19,22,43,50 })]
        [InlineData("MMULT({1,2,3},{4;5;6})", 1, 1, new double[] { 32 })]
        [InlineData("MMULT({1;2;3},{4,5})", 3, 2, new double[] { 4,5,8,10,12,15 })]
        [InlineData("MMULT(2,3)", 1, 1, new double[] { 6 })]
        [InlineData("MMULT({1E16,1,-1E16},{1;1;1})", 1, 1, new double[] { 1 })]
        [InlineData("MINVERSE({4,7;2,6})", 2, 2, new double[] { .6,-.7,-.2,.4 })]
        [InlineData("MINVERSE({0,2;1,3})", 2, 2, new double[] { -1.5,1,.5,0 })]
        [InlineData("MINVERSE(4)", 1, 1, new double[] { .25 })]
        [InlineData("MINVERSE({1E200,0;0,1E-200})", 2, 2, new double[] { 1e-200,0,0,1e200 })]
        [InlineData("MUNIT(3)", 3, 3, new double[] { 1,0,0,0,1,0,0,0,1 })]
        [InlineData("MUNIT(2.9)", 2, 2, new double[] { 1,0,0,1 })]
        [InlineData("MUNIT(1)", 1, 1, new double[] { 1 })]
        [InlineData("MMULT(MUNIT(2),{1,2;3,4})", 2, 2, new double[] { 1,2,3,4 })]
        [InlineData("MMULT({1,2;3,4},MUNIT(2))", 2, 2, new double[] { 1,2,3,4 })]
        public void Matrix_Results_Agree_In_All_Evaluation_And_Formatting_Paths(string formula, int rows, int columns, double[] expected)
        {
            var context = Context();
            foreach (var value in EvaluateBothModes(context, formula))
            {
                var array = value.AsArray();
                Assert.Equal(rows, array.RowCount);
                Assert.Equal(columns, array.ColumnCount);
                Assert.Null(array.Origin);
                for (var row = 0; row < rows; row++)
                    for (var col = 0; col < columns; col++)
                    {
                        Assert.Equal(FormulaValueKind.Number, array[row, col].Kind);
                        Close(expected[row * columns + col], array[row, col].AsNumber());
                    }
            }
        }

        [Theory]
        [InlineData("MDETERM({1,3,8,5;1,3,6,1;1,1,1,0;7,3,10,2})", 88)]
        [InlineData("MDETERM({3,6,1;1,1,0;3,10,2})", 1)]
        [InlineData("MDETERM({3,6;1,1})", -3)]
        [InlineData("MDETERM({0,2;1,3})", -2)]
        [InlineData("MDETERM({1,2;2,4})", 0)]
        [InlineData("MDETERM({0,0;1,2})", 0)]
        [InlineData("MDETERM(MUNIT(10))", 1)]
        [InlineData("MDETERM(7)", 7)]
        [InlineData("MDETERM({1E200,0,0,0;0,1E200,0,0;0,0,1E-200,0;0,0,0,1E-200})", 1)]
        [InlineData("MDETERM({1E-200,0,0,0;0,1E-200,0,0;0,0,1E200,0;0,0,0,1E200})", 1)]
        [InlineData("MDETERM({1E-200,0;0,1E-200})", 0)]
        [InlineData("SUM(MMULT({1,2;3,4},{5,6;7,8}))", 134)]
        [InlineData("LET(a,{4,7;2,6},SUM(MMULT(a,MINVERSE(a))))", 2)]
        public void Determinants_And_Compositions_Agree_With_Known_Results(string formula, double expected)
        {
            foreach (var result in EvaluateBothModes(Context(), formula)) Close(expected, result.AsNumber());
        }

        [Theory]
        [InlineData("MMULT({1,2},{3,4})", FormulaErrorType.Value)]
        [InlineData("MMULT({1,2;3,4},2)", FormulaErrorType.Value)]
        [InlineData("MMULT({1,\"2\"},{3;4})", FormulaErrorType.Value)]
        [InlineData("MMULT({TRUE},{2})", FormulaErrorType.Value)]
        [InlineData("MMULT(A1,2)", FormulaErrorType.Value)]
        [InlineData("MMULT(NA(),2)", FormulaErrorType.NA)]
        [InlineData("MMULT(1E308,2)", FormulaErrorType.Num)]
        [InlineData("MMULT(1)", FormulaErrorType.Value)]
        [InlineData("MINVERSE({1,2,3;4,5,6})", FormulaErrorType.Value)]
        [InlineData("MDETERM({1,2,3;4,5,6})", FormulaErrorType.Value)]
        [InlineData("MINVERSE({1,2;2,4})", FormulaErrorType.Num)]
        [InlineData("MINVERSE(0)", FormulaErrorType.Num)]
        [InlineData("MINVERSE({0,0;1,2})", FormulaErrorType.Num)]
        [InlineData("MDETERM(A1)", FormulaErrorType.Value)]
        [InlineData("MINVERSE(\"2\")", FormulaErrorType.Value)]
        [InlineData("MINVERSE(NA())", FormulaErrorType.NA)]
        [InlineData("MDETERM({1E308,0;0,1E308})", FormulaErrorType.Num)]
        [InlineData("MUNIT(0)", FormulaErrorType.Value)]
        [InlineData("MUNIT(-1)", FormulaErrorType.Value)]
        [InlineData("MUNIT(\"bad\")", FormulaErrorType.Value)]
        [InlineData("MUNIT(NA())", FormulaErrorType.NA)]
        [InlineData("MUNIT(1048577)", FormulaErrorType.Num)]
        public void Invalid_Shapes_Values_Singularities_And_Overflow_Are_Formula_Errors(string formula, FormulaErrorType expected)
        {
            foreach (var result in EvaluateBothModes(Context(), formula)) Assert.Equal(expected, result.AsError().Type);
        }

        [Fact]
        public void Matrix_Limits_Are_Validated_And_Reject_Work_Before_Allocation()
        {
            var context = Context();
            Assert.Equal(100000000L, context.Workbook.Settings.MaximumMatrixOperationCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Workbook.Settings.MaximumMatrixOperationCount = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Workbook.Settings.MaximumMatrixOperationCount = -1);
            context.Workbook.Settings.MaximumMatrixOperationCount = 7;
            foreach (var formula in new[] { "MMULT({1,2;3,4},MUNIT(2))", "MDETERM({1,2;3,4})", "MINVERSE({1,2;3,4})" })
                Assert.Equal(FormulaErrorType.Num, Evaluate(context, formula).AsError().Type);
            context.Workbook.Settings.MaximumMatrixOperationCount = 3;
            Assert.Equal(FormulaErrorType.Num, Evaluate(context, "MUNIT(2)").AsError().Type);
            context.Workbook.Settings.MaximumMatrixOperationCount = long.MaxValue;
            context.Workbook.Settings.MaximumArrayCellCount = 4;
            Assert.Equal(FormulaErrorType.Num, Evaluate(context, "MMULT({1,2,3;4,5,6},{1;2;3})").AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Evaluate(context, "MUNIT(3)").AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Evaluate(context, "MDETERM({1,0,0;0,1,0;0,0,1})").AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Evaluate(context, "MMULT({1;2;3},{4,5,6})").AsError().Type);
        }

        [Fact]
        public void Seeded_Rectangular_Products_Agree_With_Independent_Triple_Loops()
        {
            var random = new Random(839101);
            var context = Context();
            for (var trial = 0; trial < 160; trial++)
            {
                var rows = random.Next(1, 18);
                var inner = random.Next(1, 19);
                var columns = random.Next(1, 17);
                var left = new FormulaArray(rows, inner);
                var right = new FormulaArray(inner, columns);
                for (var r = 0; r < rows; r++) for (var c = 0; c < inner; c++) left[r, c] = FormulaValue.FromNumber(random.Next(-10, 11));
                for (var r = 0; r < inner; r++) for (var c = 0; c < columns; c++) right[r, c] = FormulaValue.FromNumber(random.Next(-10, 11));
                var result = Invoke(context, "MMULT", FormulaValue.FromArray(left), FormulaValue.FromArray(right)).AsArray();
                for (var r = 0; r < rows; r++) for (var c = 0; c < columns; c++)
                {
                    double expected = 0;
                    for (var k = 0; k < inner; k++) expected += left[r, k].AsNumber() * right[k, c].AsNumber();
                    Assert.Equal(expected, result[r, c].AsNumber());
                }
            }
        }

        [Fact]
        public void Determinants_Agree_With_Exact_Integer_Bareiss_Oracle()
        {
            var random = new Random(893191);
            var context = Context();
            for (var trial = 0; trial < 160; trial++)
            {
                var size = random.Next(1, 8);
                var data = new BigInteger[size, size];
                var source = new FormulaArray(size, size);
                for (var r = 0; r < size; r++) for (var c = 0; c < size; c++)
                {
                    var value = random.Next(-8, 9);
                    data[r, c] = value;
                    source[r, c] = FormulaValue.FromNumber(value);
                }
                var expected = (double)Bareiss(data);
                var actual = Invoke(context, "MDETERM", FormulaValue.FromArray(source)).AsNumber();
                Assert.True(Math.Abs(actual - expected) <= Math.Max(1, Math.Abs(expected)) * 1e-10,
                    $"Expected determinant {expected:R}, actual {actual:R}, size {size}");
            }
        }

        [Fact]
        public void Inverse_Residuals_Remain_Small_Across_Pivots_And_Extreme_Row_Scales()
        {
            var random = new Random(59981);
            var context = Context();
            for (var trial = 0; trial < 80; trial++)
            {
                var size = random.Next(2, 16);
                var source = new FormulaArray(size, size);
                var original = new double[size, size];
                var scales = new int[size];
                for (var row = 0; row < size; row++)
                {
                    double offDiagonal = 0;
                    for (var col = 0; col < size; col++)
                        if (row != col) { original[row, col] = random.Next(-5, 6); offDiagonal += Math.Abs(original[row, col]); }
                    original[row, row] = offDiagonal + random.Next(1, 5);
                    scales[row] = trial % 2 == 0 ? 0 : row % 2 == 0 ? 500 : -500;
                }
                // Reverse rows so elimination must use row permutations as well as row scales.
                for (var row = 0; row < size; row++) for (var col = 0; col < size; col++)
                    source[row, col] = FormulaValue.FromNumber(Math.ScaleB(original[size - row - 1, col], scales[row]));
                var result = Invoke(context, "MINVERSE", FormulaValue.FromArray(source)).AsArray();
                for (var row = 0; row < size; row++) for (var col = 0; col < size; col++)
                {
                    double product = 0;
                    // Undo row scaling in A and the matching column scaling in its inverse
                    // before checking residuals; an unscaled absolute residual is meaningless
                    // for cross-row ratios of 2^1000.
                    for (var k = 0; k < size; k++) product += original[size - row - 1, k] * Math.ScaleB(result[k, col].AsNumber(), scales[col]);
                    Assert.True(Math.Abs(product - (row == col ? 1 : 0)) < 2e-11, $"Scaled residual {product:R}");
                    Assert.Equal(Math.ScaleB(original[size - row - 1, col], scales[row]), source[row, col].AsNumber());
                }
            }
        }

        [Fact]
        public void Sparse_Nonnumeric_And_Nonfinite_Inputs_Do_Not_Leak_Or_Mutate_Workspace()
        {
            var context = Context();
            var source = new FormulaArray(20, 20, sparse: true);
            for (var row = 0; row < 20; row++) for (var col = 0; col < 20; col++) source[row, col] = FormulaValue.FromNumber(row == col ? 2 : 0);
            source.SetValue(19, 19, FormulaValue.FromNumber(2), present: false);
            Assert.Equal(FormulaErrorType.Value, Invoke(context, "MINVERSE", FormulaValue.FromArray(source)).AsError().Type);
            Assert.False(source.IsPresent(19, 19));
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                source[19, 19] = FormulaValue.FromNumber(value);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "MDETERM", FormulaValue.FromArray(source)).AsError().Type);
            }
            source[19, 19] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA, "last cell"));
            Assert.Equal("last cell", Invoke(context, "MINVERSE", FormulaValue.FromArray(source)).AsError().Message);
            source[19, 19] = FormulaValue.FromNumber(2);
            Parallel.For(0, 32, i =>
            {
                var inverse = Invoke(context, "MINVERSE", FormulaValue.FromArray(source)).AsArray();
                Assert.Equal(.5, inverse[19, 19].AsNumber());
                Assert.Equal(1048576, Invoke(context, "MDETERM", FormulaValue.FromArray(source)).AsNumber());
            });
        }

        [Fact]
        public void Warm_Determinants_Use_Pooled_Workspace_Without_Per_Call_Managed_Allocation()
        {
            var context = Context();
            var source = Evaluate(context, "MUNIT(40)");
            Assert.True(context.FunctionRegistry.TryGetFunction("MDETERM", out var function));
            var args = new[] { source };
            var call = new FormulaFunctionContext(context);
            for (var i = 0; i < 20; i++) function.Invoke(call, args);
            var before = GC.GetAllocatedBytesForCurrentThread();
            double checksum = 0;
            for (var i = 0; i < 30; i++) checksum += function.Invoke(call, args).AsNumber();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(30, checksum);
            Assert.True(allocated < 4096, $"Warm determinants allocated {allocated} bytes.");
        }

        [Fact]
        public void Matrix_Spills_And_Dependencies_Recalculate_Without_Changing_Source_Cells()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            for (var r = 1; r <= 2; r++) for (var c = 1; c <= 2; c++) sheet.GetCell(r, c).Value = FormulaValue.FromNumber((r - 1) * 2 + c);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 4, "MMULT(A1:B2,MUNIT(2))");
            engine.SetCellFormula(sheet, 1, 7, "SUM(D1:E2)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4), new FormulaCellAddress("Sheet1", 1, 7) });
            Assert.Equal(10, sheet.GetCell(1, 7).Value.AsNumber());
            Assert.Equal(4, sheet.GetCell(2, 5).Value.AsNumber());
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(6);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 1) });
            Assert.Equal(13, sheet.GetCell(1, 7).Value.AsNumber());
            Assert.Equal(6, sheet.GetCell(2, 4).Value.AsNumber());
            Assert.Equal(6, sheet.GetCell(2, 1).Value.AsNumber());
        }

        private static BigInteger Bareiss(BigInteger[,] source)
        {
            var a = (BigInteger[,])source.Clone();
            var size = a.GetLength(0);
            BigInteger previous = 1;
            var sign = 1;
            for (var k = 0; k < size - 1; k++)
            {
                var pivot = k;
                while (pivot < size && a[pivot, k].IsZero) pivot++;
                if (pivot == size) return 0;
                if (pivot != k)
                {
                    for (var j = 0; j < size; j++) (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]);
                    sign = -sign;
                }
                var value = a[k, k];
                for (var i = k + 1; i < size; i++)
                {
                    for (var j = k + 1; j < size; j++) a[i, j] = (a[i, j] * value - a[i, k] * a[k, j]) / previous;
                    a[i, k] = 0;
                }
                previous = value;
            }
            return sign * a[size - 1, size - 1];
        }

        private static IEnumerable<FormulaValue> EvaluateBothModes(FormulaEvaluationContext context, string text)
        {
            var parser = new ExcelFormulaParser();
            var original = parser.Parse(text, new FormulaParseOptions());
            var formatted = parser.Parse(new ExcelFormulaFormatter().Format(original, new FormulaFormatOptions()), new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false }) foreach (var precision in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.ApplyNumberPrecision = precision;
                yield return evaluator.Evaluate(original, context, new WorkbookValueResolver());
                yield return evaluator.Evaluate(formatted, context, new WorkbookValueResolver());
            }
        }
        private static FormulaValue Evaluate(FormulaEvaluationContext context, string text)
            => new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(text, new FormulaParseOptions()), context, new WorkbookValueResolver());
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
        private static void Close(double expected, double actual)
        {
            Assert.True(double.IsFinite(actual));
            Assert.True(expected == 0 ? Math.Abs(actual) < 1e-12 : Math.Abs((actual - expected) / expected) < 2e-12,
                $"Expected {expected:R}, actual {actual:R}");
        }
    }
}
