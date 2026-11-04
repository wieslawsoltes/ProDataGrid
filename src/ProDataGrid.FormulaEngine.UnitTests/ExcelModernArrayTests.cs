// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Text;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelModernArrayTests
    {
        [Theory]
        [InlineData("HSTACK({1;2},{3;4})", "1,3;2,4")]
        [InlineData("HSTACK({1;2},3)", "1,3;2,#N/A")]
        [InlineData("HSTACK(1,NA())", "1,#N/A")]
        [InlineData("VSTACK({1,2},{3,4})", "1,2;3,4")]
        [InlineData("VSTACK({1,2},3)", "1,2;3,#N/A")]
        [InlineData("VSTACK(NA(),{2,3})", "#N/A,#N/A;2,3")]
        [InlineData("TAKE({1,2;3,4;5,6},2)", "1,2;3,4")]
        [InlineData("TAKE({1,2;3,4;5,6},-2)", "3,4;5,6")]
        [InlineData("TAKE({1,2;3,4},,1)", "1;3")]
        [InlineData("TAKE({1,2;3,4},,-1)", "2;4")]
        [InlineData("TAKE({1,2;3,4},1,-1)", "2")]
        [InlineData("TAKE({1,2;3,4},999,999)", "1,2;3,4")]
        [InlineData("TAKE({1,2;3,4},-2147483648)", "1,2;3,4")]
        [InlineData("TAKE({1;2},0)", "#CALC!")]
        [InlineData("TAKE({1;2},1e100)", "#NUM!")]
        [InlineData("TAKE({1;2},\"bad\")", "#VALUE!")]
        [InlineData("TAKE({1;2},NA())", "#N/A")]
        [InlineData("DROP({1,2;3,4;5,6},1)", "3,4;5,6")]
        [InlineData("DROP({1,2;3,4;5,6},-1)", "1,2;3,4")]
        [InlineData("DROP({1,2;3,4},,1)", "2;4")]
        [InlineData("DROP({1,2;3,4},,-1)", "1;3")]
        [InlineData("DROP({1,2;3,4},1,1)", "4")]
        [InlineData("DROP({1,2;3,4},99)", "#CALC!")]
        [InlineData("DROP({1,2;3,4},-2147483648)", "#CALC!")]
        [InlineData("DROP({1;2},0)", "#CALC!")]
        [InlineData("CHOOSECOLS({1,2,3;4,5,6},3,1,3)", "3,1,3;6,4,6")]
        [InlineData("CHOOSECOLS({1,2,3;4,5,6},{-1,1})", "3,1;6,4")]
        [InlineData("CHOOSEROWS({1,2;3,4;5,6},-1,1,1)", "5,6;1,2;1,2")]
        [InlineData("CHOOSEROWS({1,2;3,4;5,6},{3;2})", "5,6;3,4")]
        [InlineData("CHOOSECOLS({1,2},0)", "#VALUE!")]
        [InlineData("CHOOSECOLS({1,2},3)", "#VALUE!")]
        [InlineData("CHOOSEROWS({1;2},-3)", "#VALUE!")]
        [InlineData("CHOOSECOLS({1,2},-2147483648)", "#VALUE!")]
        [InlineData("CHOOSECOLS({1,2},2.9)", "2")]
        [InlineData("CHOOSECOLS({1,2},NA())", "#N/A")]
        [InlineData("EXPAND({1,2;3,4},3,3)", "1,2,#N/A;3,4,#N/A;#N/A,#N/A,#N/A")]
        [InlineData("EXPAND({1;2},,2,9)", "1,9;2,9")]
        [InlineData("EXPAND({1,2},2,,0)", "1,2;0,0")]
        [InlineData("EXPAND({1;2},1)", "#VALUE!")]
        [InlineData("EXPAND(1,1048577)", "#NUM!")]
        [InlineData("EXPAND(1,1,16385)", "#NUM!")]
        [InlineData("TOROW({1,2;3,4})", "1,2,3,4")]
        [InlineData("TOROW({1,2;3,4},,TRUE)", "1,3,2,4")]
        [InlineData("TOCOL({1,2;3,4})", "1;2;3;4")]
        [InlineData("TOCOL({1,2;3,4},,TRUE)", "1;3;2;4")]
        [InlineData("TOROW(HSTACK(1,NA(),2),2)", "1,2")]
        [InlineData("TOCOL(NA(),2)", "#CALC!")]
        [InlineData("TOROW({1,2},4)", "#VALUE!")]
        [InlineData("TOROW({1,2},-1)", "#VALUE!")]
        [InlineData("WRAPROWS({1,2,3,4,5},2)", "1,2;3,4;5,#N/A")]
        [InlineData("WRAPROWS({1;2;3;4;5},2,0)", "1,2;3,4;5,0")]
        [InlineData("WRAPCOLS({1,2,3,4,5},2)", "1,3,5;2,4,#N/A")]
        [InlineData("WRAPCOLS({1;2;3;4;5},2,0)", "1,3,5;2,4,0")]
        [InlineData("WRAPROWS({1;2;3},999)", "1,2,3")]
        [InlineData("WRAPCOLS({1,2,3},999)", "1;2;3")]
        [InlineData("WRAPROWS({1,2;3,4},2)", "#VALUE!")]
        [InlineData("WRAPROWS({1,2},0)", "#NUM!")]
        [InlineData("WRAPROWS({1,2},-1)", "#NUM!")]
        [InlineData("TRANSPOSE({1,2,3;4,5,6})", "1,4;2,5;3,6")]
        [InlineData("TRANSPOSE(42)", "42")]
        [InlineData("SORTBY({10;20;30},{2;3;1})", "30;10;20")]
        [InlineData("SORTBY({10;20;30},{2;3;1},-1)", "20;10;30")]
        [InlineData("SORTBY({10,20,30},{2,3,1})", "30,10,20")]
        [InlineData("SORTBY({10;20;30},{1;1;1},1,{1;3;2},-1)", "20;30;10")]
        [InlineData("SORTBY({10;20;30},{1;1;1})", "10;20;30")]
        [InlineData("SORTBY({10;20},{1;2},0)", "#VALUE!")]
        [InlineData("SORTBY({10;20},{1,2})", "#VALUE!")]
        [InlineData("SORTBY({10;20;30},{1;2})", "#VALUE!")]
        [InlineData("RANDARRAY(1,1,3,2)", "#VALUE!")]
        [InlineData("RANDARRAY(0)", "#VALUE!")]
        [InlineData("RANDARRAY(1048577)", "#NUM!")]
        [InlineData("SUM(TOCOL(HSTACK({1;2},{3;4})))", "10")]
        public void Functions_Agree_In_Compiled_And_Interpreted_Evaluation(string formula, string expected)
        {
            var context = Context();
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, resolver)));
            context.Workbook.Settings.EnableCompiledExpressions = false;
            Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, resolver)));
        }

        [Theory]
        [InlineData(0, "1,0;#N/A,\"\"")]
        [InlineData(1, "1,#N/A,\"\"")]
        [InlineData(2, "1,0,\"\"")]
        [InlineData(3, "1,\"\"")]
        public void Flatten_Distinguishes_Absent_Blank_Empty_Text_And_Error(int ignore, string expected)
        {
            var array = new FormulaArray(2, 2, sparse: true);
            array[0, 0] = FormulaValue.FromNumber(1);
            array[1, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            array[1, 1] = FormulaValue.FromText("");
            var context = Context();
            var result = Invoke(context, "TOROW", FormulaValue.FromArray(array), FormulaValue.FromNumber(ignore));
            // Ignoring nothing flattens to one row as well.
            Assert.Equal(expected.Replace(';', ','), Describe(result));
            Assert.False(array.IsPresent(0, 1));
        }

        [Fact]
        public void Transpose_And_Wrap_RoundTrip_Seeded_Rectangular_Arrays()
        {
            var random = new Random(2791);
            var context = Context();
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var array = new FormulaArray(random.Next(1, 16), random.Next(1, 16));
                for (var row = 0; row < array.RowCount; row++)
                {
                    for (var column = 0; column < array.ColumnCount; column++)
                    {
                        array[row, column] = FormulaValue.FromNumber(random.Next(-1000, 1000));
                    }
                }
                var value = FormulaValue.FromArray(array);
                var transposed = Invoke(context, "TRANSPOSE", Invoke(context, "TRANSPOSE", value));
                var wrapped = Invoke(context, "WRAPROWS", Invoke(context, "TOROW", value), FormulaValue.FromNumber(array.ColumnCount));
                Assert.Equal(Describe(value), Describe(transposed));
                Assert.Equal(Describe(value), Describe(wrapped));
                Assert.NotSame(array, transposed.AsArray());
                Assert.NotSame(array, wrapped.AsArray());
            }
        }

        [Theory]
        [InlineData("SEQUENCE(3,3)")]
        [InlineData("EXPAND(1,3,3)")]
        [InlineData("HSTACK({1,2;3,4},{5;6})")]
        [InlineData("VSTACK({1,2;3,4},{5,6})")]
        [InlineData("CHOOSECOLS({1,2;3,4},1,1,1)")]
        [InlineData("WRAPROWS({1,2,3,4},3)")]
        [InlineData("RANDARRAY(3,3)")]
        public void Configured_Allocation_Limit_Is_Checked_Before_Allocating(string formula)
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 4;
            var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new DictionaryValueResolver());
            Assert.Equal(FormulaErrorType.Num, result.AsError().Type);
        }

        [Fact]
        public void Allocation_Limit_Is_Validated_And_Can_Be_Increased()
        {
            var context = Context();
            Assert.Equal(1048576, context.Workbook.Settings.MaximumArrayCellCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Workbook.Settings.MaximumArrayCellCount = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Workbook.Settings.MaximumArrayCellCount = -1);
            context.Workbook.Settings.MaximumArrayCellCount = 9;
            Assert.Equal("1,2,3;4,5,6;7,8,9", Describe(new FormulaEvaluator().Evaluate(
                new ExcelFormulaParser().Parse("SEQUENCE(3,3)", new FormulaParseOptions()), context, new DictionaryValueResolver())));
        }

        [Fact]
        public void Array_Transformations_Spill_And_Recalculate_From_References()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(5);
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(7);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "HSTACK(A1:A2,A1:A2*2)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1), new FormulaCellAddress("Sheet1", 1, 3) });
            Assert.Equal("5,10;7,14", Describe(sheet.GetCell(1, 3).Value));
            Assert.Equal(14, sheet.GetCell(2, 4).Value.AsNumber());
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(11);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 1) });
            Assert.Equal("5,10;11,22", Describe(sheet.GetCell(1, 3).Value));
            Assert.Equal(22, sheet.GetCell(2, 4).Value.AsNumber());
        }

        [Fact]
        public void RandArray_Has_Dimensions_Bounds_Integral_Output_And_Volatility()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("RANDARRAY", out var function));
            Assert.True(function.Info.IsVolatile);
            foreach (var whole in new[] { false, true })
            {
                var result = Invoke(context, "RANDARRAY", FormulaValue.FromNumber(32), FormulaValue.FromNumber(4),
                    FormulaValue.FromNumber(-3), FormulaValue.FromNumber(7), FormulaValue.FromBoolean(whole)).AsArray();
                Assert.Equal(32, result.RowCount);
                Assert.Equal(4, result.ColumnCount);
                foreach (var value in result.Flatten())
                {
                    Assert.InRange(value.AsNumber(), -3d, 7d);
                    if (whole) Assert.Equal(Math.Truncate(value.AsNumber()), value.AsNumber());
                }
            }
        }

        [Fact]
        public void SortBy_Preserves_Order_Of_Large_Tie_Groups()
        {
            var source = new FormulaArray(200, 1);
            var key = new FormulaArray(200, 1);
            for (var row = 0; row < source.RowCount; row++)
            {
                source[row, 0] = FormulaValue.FromNumber(row);
                key[row, 0] = FormulaValue.FromText("same");
            }
            var result = Invoke(Context(), "SORTBY", FormulaValue.FromArray(source), FormulaValue.FromArray(key));
            Assert.Equal(Describe(FormulaValue.FromArray(source)), Describe(result));
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }

        private static string Describe(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                var builder = new StringBuilder();
                for (var row = 0; row < array.RowCount; row++)
                {
                    if (row > 0) builder.Append(';');
                    for (var column = 0; column < array.ColumnCount; column++)
                    {
                        if (column > 0) builder.Append(',');
                        builder.Append(Describe(array[row, column]));
                    }
                }
                return builder.ToString();
            }
            return value.Kind switch
            {
                FormulaValueKind.Number => value.AsNumber().ToString("G17", CultureInfo.InvariantCulture),
                FormulaValueKind.Text => "\"" + value.AsText() + "\"",
                FormulaValueKind.Blank => "<blank>",
                _ => value.ToString()
            };
        }
    }
}
