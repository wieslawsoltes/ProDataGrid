// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelRankingTests
    {
        [Theory]
        [InlineData("RANK.EQ(7,{7;3.5;3.5;1;2},1)", "5")]
        [InlineData("RANK.EQ(2,{7;3.5;3.5;1;2})", "4")]
        [InlineData("RANK.EQ(3.5,{7;3.5;3.5;1;2},1)", "3")]
        [InlineData("RANK.AVG(3.5,{7;3.5;3.5;1;2},1)", "3.5")]
        [InlineData("RANK.AVG(3.5,{7;3.5;3.5;1;2},0)", "2.5")]
        [InlineData("RANK(2,{1,2,3},-3)", "2")]
        [InlineData("RANK.EQ(1,{1,2,3},0.25)", "1")]
        [InlineData("RANK.EQ(1,{1,2,3},-0.25)", "1")]
        [InlineData("RANK.EQ(1,{1,2,3},1E100)", "1")]
        [InlineData("RANK.EQ(1,{1,2,3},)", "3")]
        [InlineData("RANK.EQ(1,{1,2,3},A1)", "3")]
        [InlineData("RANK.EQ(9,{1,2,3})", "#N/A")]
        [InlineData("RANK.AVG(0,{1,2,3})", "#N/A")]
        [InlineData("RANK.EQ(2.5,{1,2,3})", "#N/A")]
        [InlineData("RANK.EQ(2,{\"2\",TRUE,2})", "1")]
        [InlineData("RANK.EQ(2,{\"2\",TRUE})", "#N/A")]
        [InlineData("RANK.EQ(2,2)", "1")]
        [InlineData("RANK.EQ(NA(),{1,2})", "#N/A")]
        [InlineData("RANK.EQ(2,{1,#DIV/0!,2})", "#DIV/0!")]
        [InlineData("RANK.EQ(2,{1,2},\"bad\")", "#VALUE!")]
        [InlineData("RANK.EQ({1;3},{1,2,3},{0,1,-0.5})", "3,1,1;1,3,3")]
        [InlineData("RANK.AVG({1;2;3},{1,2,2,3},{0,1})", "4,1;2.5,2.5;1,4")]
        [InlineData("RANK.EQ({1;99},{1,2},{0,NA()})", "2,#N/A;#N/A,#N/A")]
        [InlineData("RANK.EQ({1;2},{1,2},{0;1;0})", "#VALUE!")]
        [InlineData("PERCENTRANK.INC({1;2;3;6;6;6;7;8;9},7)", "0.75")]
        [InlineData("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},7)", "0.7")]
        [InlineData("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},5.43)", "0.381")]
        [InlineData("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},5.43,1)", "0.3")]
        [InlineData("PERCENTRANK.INC({1;2;3;6;6;6;7;8;9},6)", "0.375")]
        [InlineData("PERCENTRANK.EXC({1;2;3;6;6;6;7;8;9},6)", "0.4")]
        [InlineData("PERCENTRANK.INC({1;2;3;6;6;6;7;8;9},6.5,4)", "0.6875")]
        [InlineData("PERCENTRANK({1,2,3},2)", "0.5")]
        [InlineData("PERCENTRANK.INC({1,2,3},1)", "0")]
        [InlineData("PERCENTRANK.INC({1,2,3},3)", "1")]
        [InlineData("PERCENTRANK.EXC({1,2,3},1)", "0.25")]
        [InlineData("PERCENTRANK.EXC({1,2,3},3)", "0.75")]
        [InlineData("PERCENTRANK.INC({1,2,3},0)", "#N/A")]
        [InlineData("PERCENTRANK.EXC({1,2,3},4)", "#N/A")]
        [InlineData("PERCENTRANK.INC({\"x\",TRUE},1)", "#NUM!")]
        [InlineData("PERCENTRANK.INC({1,2,3},2,0)", "#NUM!")]
        [InlineData("PERCENTRANK.EXC({1,2,3},2,A1)", "#NUM!")]
        [InlineData("PERCENTRANK.INC({1,2,3},2,)", "0.5")]
        [InlineData("LAMBDA(sig,unused,PERCENTRANK.INC({1,2,3},2,sig))(,1)", "0.5")]
        [InlineData("PERCENTRANK.INC({1,2,3},2,1E100)", "0.5")]
        [InlineData("PERCENTRANK.INC({1,2,3},1.58,2)", "0.29")]
        [InlineData("PERCENTRANK.INC({1,2,3},1.58,1.9)", "0.2")]
        [InlineData("PERCENTRANK.INC({1;2;3},{1;2;3},{1,3})", "0,0;0.5,0.5;1,1")]
        [InlineData("PERCENTRANK.EXC({1;2;3},{1;2;3},{1,3})", "0.2,0.25;0.5,0.5;0.7,0.75")]
        [InlineData("PERCENTRANK.INC({-1E308,1E308},0)", "0.5")]
        [InlineData("PERCENTRANK.INC({5,5,5},5)", "0")]
        [InlineData("PERCENTRANK.EXC({5,5,5},5)", "0.25")]
        [InlineData("PERCENTRANK.INC(5,5)", "0")]
        [InlineData("PERCENTRANK.EXC(5,5)", "0.5")]
        public void Examples_And_Boundaries_Agree_In_Both_Evaluators(string text, string expected)
        {
            var context = Context();
            var expression = Parse(text);
            var roundTrip = Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()));
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(expected, Describe(evaluator.Evaluate(roundTrip, context, new WorkbookValueResolver())));
            }
        }

        [Fact]
        public void Scalar_Scans_And_Batch_Binary_Bounds_Match_Independent_Counts()
        {
            var context = Context();
            var random = new Random(53863);
            for (var trial = 0; trial < 160; trial++)
            {
                var count = random.Next(1, 97);
                var data = new FormulaArray(count, 1);
                var numbers = new int[count];
                for (var i = 0; i < count; i++) data[i, 0] = FormulaValue.FromNumber(numbers[i] = random.Next(-12, 13));
                var targets = new FormulaArray(31, 1);
                for (var i = 0; i < targets.RowCount; i++) targets[i, 0] = FormulaValue.FromNumber(i - 15);
                foreach (var name in new[] { "RANK.EQ", "RANK.AVG", "RANK" })
                    foreach (var ascending in new[] { false, true })
                    {
                        var order = FormulaValue.FromNumber(ascending ? -0.5 : 0);
                        var batch = Invoke(context, name, FormulaValue.FromArray(targets), FormulaValue.FromArray(data), order).AsArray();
                        for (var i = 0; i < targets.RowCount; i++)
                        {
                            var target = i - 15;
                            var before = 0;
                            var equal = 0;
                            foreach (var number in numbers)
                            {
                                if (number == target) equal++;
                                else if (ascending ? number < target : number > target) before++;
                            }
                            var expected = equal == 0 ? FormulaValue.FromError(new FormulaError(FormulaErrorType.NA))
                                : FormulaValue.FromNumber(before + 1 + (name == "RANK.AVG" ? (equal - 1d) / 2 : 0));
                            Assert.Equal(expected, batch[i, 0]);
                            Assert.Equal(expected, Invoke(context, name, targets[i, 0], FormulaValue.FromArray(data), order));
                        }
                    }
            }
        }

        [Fact]
        public void Percentile_Ranks_Match_Exact_Rational_Ordinals_And_Interpolation()
        {
            var context = Context();
            for (var count = 2; count <= 80; count++)
            {
                var data = new FormulaArray(1, count);
                var targets = new FormulaArray(2 * count - 1, 1);
                for (var i = 0; i < count; i++) data[0, count - i - 1] = FormulaValue.FromNumber(i * 3);
                for (var i = 0; i < targets.RowCount; i++) targets[i, 0] = FormulaValue.FromNumber(i * 1.5);
                foreach (var exclusive in new[] { false, true })
                {
                    var name = exclusive ? "PERCENTRANK.EXC" : "PERCENTRANK.INC";
                    var result = Invoke(context, name, FormulaValue.FromArray(data), FormulaValue.FromArray(targets)).AsArray();
                    for (var i = 0; i < targets.RowCount; i++)
                    {
                        var numerator = i + (exclusive ? 2 : 0);
                        var denominator = 2 * (exclusive ? count + 1 : count - 1);
                        var expected = (numerator * 1000 / denominator) / 1000d;
                        Assert.Equal(expected, result[i, 0].AsNumber());
                        Assert.Equal(expected, Invoke(context, name, FormulaValue.FromArray(data), targets[i, 0]).AsNumber());
                    }
                }
            }
        }

        [Fact]
        public void Extreme_And_Subnormal_Intervals_Do_Not_Overflow_Or_Disappear()
        {
            var context = Context();
            var pairs = new[] { (double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon),
                (1d, Math.BitIncrement(1d), Math.BitIncrement(Math.BitIncrement(1d))), (-double.MaxValue, 0d, double.MaxValue) };
            foreach (var (low, middle, high) in pairs)
            {
                var source = new FormulaArray(2, 1);
                source[0, 0] = FormulaValue.FromNumber(low); source[1, 0] = FormulaValue.FromNumber(high);
                Assert.Equal(0.5, Invoke(context, "PERCENTRANK.INC", FormulaValue.FromArray(source), FormulaValue.FromNumber(middle)).AsNumber());
                Assert.Equal(0.5, Invoke(context, "PERCENTRANK.EXC", FormulaValue.FromArray(source), FormulaValue.FromNumber(middle)).AsNumber());
            }
        }

        [Fact]
        public void Nonfinite_Values_And_Options_Are_Numeric_Errors()
        {
            var context = Context();
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                foreach (var name in new[] { "RANK.EQ", "RANK.AVG", "PERCENTRANK.INC", "PERCENTRANK.EXC" })
                {
                    var bad = FormulaValue.FromNumber(invalid);
                    var one = FormulaValue.FromNumber(1);
                    Assert.Equal(FormulaErrorType.Num, Invoke(context, name, bad, one).AsError().Type);
                    Assert.Equal(FormulaErrorType.Num, Invoke(context, name, one, bad).AsError().Type);
                    Assert.Equal(FormulaErrorType.Num, Invoke(context, name, one, one, bad).AsError().Type);
                }
        }

        [Fact]
        public void Precision_Normalization_Applies_Identically_In_Both_Query_Paths()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            var source = new FormulaArray(3, 1);
            source[0, 0] = FormulaValue.FromNumber(1.2344); source[1, 0] = FormulaValue.FromNumber(1.2349); source[2, 0] = FormulaValue.FromNumber(9);
            var targets = new FormulaArray(2, 1);
            targets[0, 0] = targets[1, 0] = FormulaValue.FromNumber(1.2339);
            var batch = Invoke(context, "RANK.AVG", FormulaValue.FromArray(targets), FormulaValue.FromArray(source)).AsArray();
            Assert.Equal(2.5, batch[0, 0].AsNumber());
            Assert.Equal(batch[0, 0], Invoke(context, "RANK.AVG", targets[0, 0], FormulaValue.FromArray(source)));
        }

        [Fact]
        public void Masks_Errors_Source_Ownership_And_Later_Mutations_Are_Preserved()
        {
            var context = Context();
            var source = new FormulaArray(300, 1, sparse: true);
            for (var i = 0; i < 300; i++) source.SetValue(i, 0, FormulaValue.FromNumber(300 - i), i % 2 == 0);
            var targets = new FormulaArray(2, 1);
            targets[0, 0] = FormulaValue.FromNumber(300); targets[1, 0] = FormulaValue.FromNumber(298);
            var args = new[] { FormulaValue.FromArray(targets), FormulaValue.FromArray(source) };
            var first = Invoke(context, "RANK.EQ", args).AsArray();
            Assert.Equal(1, first[0, 0].AsNumber()); Assert.Equal(2, first[1, 0].AsNumber());
            for (var i = 0; i < 300; i++) { Assert.Equal(300 - i, source[i, 0].AsNumber()); Assert.Equal(i % 2 == 0, source.IsPresent(i, 0)); }
            var diagnostic = new FormulaError(FormulaErrorType.Ref, "retained source diagnostic");
            source[200, 0] = FormulaValue.FromError(diagnostic);
            Assert.Equal(diagnostic, Invoke(context, "RANK.EQ", args).AsError());
            source[200, 0] = FormulaValue.FromNumber(999);
            Assert.Equal(2, Invoke(context, "RANK.EQ", args).AsArray()[0, 0].AsNumber());
            Assert.Equal(1, first[0, 0].AsNumber());
            Parallel.For(0, 128, _ => Assert.Equal(2, Invoke(context, "RANK.EQ", args).AsArray()[0, 0].AsNumber()));
        }

        [Fact]
        public void Input_And_Broadcast_Output_Limits_Are_Checked()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 4;
            var data = new FormulaArray(5, 1);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "RANK.EQ", FormulaValue.FromNumber(1), FormulaValue.FromArray(data)).AsError().Type);
            var targets = new FormulaArray(3, 1);
            var options = new FormulaArray(1, 3);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "RANK.EQ", FormulaValue.FromArray(targets), FormulaValue.FromNumber(1), FormulaValue.FromArray(options)).AsError().Type);
        }

        [Fact]
        public void Sources_Are_Evaluated_Once_For_An_Array_Query()
        {
            var registry = new ExcelFunctionRegistry();
            var source = new CountingSource(); registry.Register(source);
            var context = Context(registry);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var before = source.Calls;
                var value = new FormulaEvaluator().Evaluate(Parse("RANK.EQ({1;2;3},TRAINING(),{0,1})"), context, new DictionaryValueResolver());
                Assert.Equal("3,1;2,2;1,3", Describe(value));
                Assert.Equal(before + 1, source.Calls);
            }
        }

        [Fact]
        public void Ranked_Array_Spills_And_Recalculates_After_Reference_Changes()
        {
            var context = Context(); var sheet = context.Worksheet;
            var inputs = new[] { 2, 4, 1 };
            var data = new[] { 2, 1, 4, 4 };
            for (var i = 0; i < inputs.Length; i++) sheet.GetCell(i + 1, 1).Value = FormulaValue.FromNumber(inputs[i]);
            for (var i = 0; i < data.Length; i++) sheet.GetCell(i + 1, 2).Value = FormulaValue.FromNumber(data[i]);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet, 1, 4, "RANK.EQ(A1:A3,B1:B4)");
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 1, 4) });
            Assert.Equal("3;1;4", Describe(sheet.GetCell(1, 4).Value));
            Assert.Equal(1, sheet.GetCell(2, 4).Value.AsNumber());
            sheet.GetCell(4, 2).Value = FormulaValue.FromNumber(5);
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 4, 2) });
            Assert.Equal("3;2;4", Describe(sheet.GetCell(1, 4).Value));
            Assert.Equal(2, sheet.GetCell(2, 4).Value.AsNumber());
        }

        [Fact]
        public void Scalar_Rank_Does_Not_Allocate_A_Sample_List()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("RANK.EQ", out var function));
            var source = new FormulaArray(1000, 1);
            for (var i = 0; i < 1000; i++) source[i, 0] = FormulaValue.FromNumber(i);
            var args = new[] { FormulaValue.FromNumber(500), FormulaValue.FromArray(source) };
            var call = new FormulaFunctionContext(context);
            Measure(function, call, args, 100);
            var (bytes, checksum) = Measure(function, call, args, 1000);
            Assert.Equal(500000, checksum);
            Assert.Equal(0, bytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (long Bytes, double Checksum) Measure(IFormulaFunction function, FormulaFunctionContext context, FormulaValue[] args, int count)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); double checksum = 0;
            for (var i = 0; i < count; i++) checksum += function.Invoke(context, args).AsNumber();
            return (GC.GetAllocatedBytesForCurrentThread() - before, checksum);
        }
        private sealed class CountingSource : IFormulaFunction
        {
            private readonly FormulaArray _array = new FormulaArray(3, 1);
            public CountingSource() { for (var i = 0; i < 3; i++) _array[i, 0] = FormulaValue.FromNumber(i + 1); }
            public int Calls { get; private set; }
            public string Name => "TRAINING";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) { Calls++; return FormulaValue.FromArray(_array); }
        }
        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1"); workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry ?? new ExcelFunctionRegistry());
        }
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static string Describe(FormulaValue value)
        {
            if (value.Kind != FormulaValueKind.Array) return value.Kind == FormulaValueKind.Number
                ? value.AsNumber().ToString("R", CultureInfo.InvariantCulture) : value.ToString();
            var array = value.AsArray(); var text = new StringBuilder();
            for (var row = 0; row < array.RowCount; row++)
            {
                if (row > 0) text.Append(';');
                for (var col = 0; col < array.ColumnCount; col++) { if (col > 0) text.Append(','); text.Append(Describe(array[row, col])); }
            }
            return text.ToString();
        }
    }
}
