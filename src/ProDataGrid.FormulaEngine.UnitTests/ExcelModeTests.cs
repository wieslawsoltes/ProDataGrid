// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    [Collection(FormulaAllocationCollection.Name)]
    public sealed class ExcelModeTests
    {
        [Theory]
        [InlineData("MODE.MULT({1;2;3;4;3;2;1;2;3;5;6;1})", "1;2;3")]
        [InlineData("MODE.SNGL({5.6;4;4;3;2;4})", "4")]
        [InlineData("MODE({5.6;4;4;3;2;4})", "4")]
        [InlineData("MODE.MULT({9;3;3;9})", "9;3")]
        [InlineData("MODE.SNGL({9;3;3;9})", "9")]
        [InlineData("MODE.MULT({8,2;2,8},5,5)", "8;2;5")]
        [InlineData("MODE.MULT({1;2;2;1;2})", "2")]
        [InlineData("MODE.MULT({3;2;1})", "#N/A")]
        [InlineData("MODE.SNGL(42)", "#N/A")]
        [InlineData("MODE.MULT(42,42)", "42")]
        [InlineData("MODE.MULT(1,\"1\",2,2)", "1;2")]
        [InlineData("MODE.MULT(TRUE,1)", "1")]
        [InlineData("MODE.MULT(FALSE,0)", "0")]
        [InlineData("MODE.MULT(1,\"bad\")", "#VALUE!")]
        [InlineData("MODE.MULT({1;TRUE;1;\"1\";FALSE;0})", "1")]
        [InlineData("MODE.MULT({TRUE;FALSE;\"x\";\"1\"})", "#N/A")]
        [InlineData("MODE.SNGL({0;0;1})", "0")]
        [InlineData("MODE.MULT(1,1,NA())", "#N/A")]
        [InlineData("MODE.MULT(1,1,1/0)", "#DIV/0!")]
        [InlineData("MODE.SNGL(,)", "#N/A")]
        [InlineData("MODE.MULT(,2,2)", "2")]
        [InlineData("MODE.MULT()", "#VALUE!")]
        [InlineData("SUM(MODE.MULT({9;3;3;9}))", "12")]
        [InlineData("TRANSPOSE(MODE.MULT({9;3;3;9}))", "9,3")]
        [InlineData("LET(x,{9;3;3;9},SUM(MAP(MODE.MULT(x),LAMBDA(n,n*n))))", "90")]
        [InlineData("MODE.MULT(HSTACK({1;2},{2;1}))", "1;2")]
        public void Examples_Compose_In_Both_Evaluators_And_Formatter_RoundTrips(string formula, string expected)
        {
            var context = Context();
            var expression = Parse(formula);
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(expected, Describe(evaluator.Evaluate(Parse(formatted), context, new WorkbookValueResolver())));
            }
        }

        [Theory]
        [InlineData("MODE")]
        [InlineData("MODE.SNGL")]
        [InlineData("MODE.MULT")]
        public void References_Keep_Logical_Text_And_Blank_Cell_Provenance(string name)
        {
            var context = Context();
            context.Worksheet.GetCell(1, 1).Value = FormulaValue.FromBoolean(true);
            context.Worksheet.GetCell(2, 1).Value = FormulaValue.FromText("1");
            context.Worksheet.GetCell(3, 1).Value = FormulaValue.FromNumber(1);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                var resolver = new WorkbookValueResolver();
                foreach (var formula in new[] { $"{name}(A1:A4)", $"{name}(A1,A2,A3,A4)" })
                    Assert.Equal(FormulaErrorType.NA, evaluator.Evaluate(Parse(formula), context, resolver).AsError().Type);
                Assert.Equal("1", Describe(evaluator.Evaluate(Parse($"{name}(A1:A4,1)"), context, resolver)));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Sparse_Holes_Are_Ignored_And_No_Source_Array_Is_Mutated(bool multiple)
        {
            var array = new FormulaArray(6, 1, new FormulaCellAddress("Sheet1", 8, 2), sparse: true);
            array.SetValue(0, 0, FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref)), false);
            array[1, 0] = FormulaValue.FromNumber(0);
            array[2, 0] = FormulaValue.FromNumber(2);
            array[3, 0] = FormulaValue.FromNumber(2);
            var value = Invoke(Context(), multiple ? "MODE.MULT" : "MODE.SNGL", FormulaValue.FromArray(array));
            Assert.Equal("2", Describe(value));
            Assert.False(array.IsPresent(0, 0));
            Assert.False(array.IsPresent(4, 0));
            Assert.Equal(FormulaErrorType.Ref, array[0, 0].AsError().Type);
            Assert.Equal(0, array[1, 0].AsNumber());
            if (multiple)
            {
                Assert.Null(value.AsArray().Origin);
                value.AsArray()[0, 0] = FormulaValue.FromNumber(99);
                Assert.Equal(2, array[2, 0].AsNumber());
            }
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Nonfinite_Keys_Are_Rejected_Before_Hashing(double value)
        {
            foreach (var name in new[] { "MODE", "MODE.SNGL", "MODE.MULT" })
                Assert.Equal(FormulaErrorType.Num, Invoke(Context(), name, FormulaValue.FromNumber(value), FormulaValue.FromNumber(value)).AsError().Type);
        }

        [Fact]
        public void Signed_Zeros_Share_A_Count_And_Preserve_First_Represented_Value()
        {
            var minusZero = BitConverter.Int64BitsToDouble(long.MinValue);
            var result = Invoke(Context(), "MODE.MULT", FormulaValue.FromNumber(minusZero), FormulaValue.FromNumber(0)).AsArray();
            Assert.Equal(1, result.RowCount);
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(result[0, 0].AsNumber()));
        }

        [Fact]
        public void Precision_And_Culture_Are_Applied_Before_Grouping()
        {
            var context = Context();
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal("1.25", Describe(Invoke(context, "MODE", FormulaValue.FromText("1,25"), FormulaValue.FromNumber(1.25))));
            var input = new[] { FormulaValue.FromNumber(1.2341), FormulaValue.FromNumber(1.2344) };
            Assert.Equal(FormulaErrorType.NA, Invoke(context, "MODE", input).AsError().Type);
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 4;
            Assert.Equal("1.234", Describe(Invoke(context, "MODE.MULT", input)));
        }

        [Fact]
        public void Physical_Work_Limits_Include_Ignored_Entries_And_All_Arguments()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 4;
            var sparse = new FormulaArray(5, 1, sparse: true);
            sparse[0, 0] = sparse[1, 0] = FormulaValue.FromNumber(1);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "MODE.MULT", FormulaValue.FromArray(sparse)).AsError().Type);
            var pair = new FormulaArray(2, 1); pair[0, 0] = pair[1, 0] = FormulaValue.FromNumber(1);
            Assert.Equal("1", Describe(Invoke(context, "MODE.MULT", FormulaValue.FromArray(pair), FormulaValue.FromArray(pair))));
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "MODE", FormulaValue.FromArray(pair), FormulaValue.FromArray(pair), FormulaValue.Blank).AsError().Type);
        }

        [Fact]
        public void Arity_And_Unsupported_Nested_Values_Are_Validated()
        {
            var context = Context();
            foreach (var name in new[] { "MODE", "MODE.SNGL", "MODE.MULT" })
            {
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name).AsError().Type);
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, new FormulaValue[256]).AsError().Type);
                var accepted = Enumerable.Repeat(FormulaValue.FromNumber(7), 255).ToArray();
                Assert.Equal("7", Describe(Invoke(context, name, accepted)));
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, FormulaValue.FromReference(default)).AsError().Type);
                var nested = new FormulaArray(1, 1); nested[0, 0] = FormulaValue.FromArray(new FormulaArray(1, 1));
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, FormulaValue.FromArray(nested)).AsError().Type);
            }
        }

        [Fact]
        public void Independent_Count_Oracle_Agrees_After_Growth_And_With_Tied_Modes()
        {
            var random = new Random(293810);
            var context = Context();
            for (var iteration = 0; iteration < 800; iteration++)
            {
                var length = random.Next(1, 1200);
                var array = new FormulaArray(length, 1, sparse: iteration % 3 == 0);
                var groups = new SortedDictionary<double, (int Count, int Ordinal)>();
                for (var i = 0; i < length; i++)
                {
                    if (array.HasMask && random.Next(5) == 0) continue;
                    if (random.Next(7) == 0) { array[i, 0] = FormulaValue.FromText("ignored"); continue; }
                    var number = Math.ScaleB(random.Next(-400, 401), iteration % 40 - 20);
                    array[i, 0] = FormulaValue.FromNumber(number);
                    if (groups.TryGetValue(number, out var entry)) groups[number] = (entry.Count + 1, entry.Ordinal);
                    else groups.Add(number, (1, i));
                }
                var maximum = groups.Count == 0 ? 0 : groups.Max(x => x.Value.Count);
                var expected = groups.Where(x => x.Value.Count == maximum).OrderBy(x => x.Value.Ordinal).Select(x => x.Key).ToArray();
                foreach (var name in new[] { "MODE.MULT", "MODE.SNGL", "MODE" })
                {
                    var actual = Invoke(context, name, FormulaValue.FromArray(array));
                    if (maximum < 2) { Assert.Equal(FormulaErrorType.NA, actual.AsError().Type); continue; }
                    if (name != "MODE.MULT") Assert.Equal(expected[0], actual.AsNumber());
                    else Assert.Equal(expected, actual.AsArray().Flatten().Select(x => x.AsNumber()).ToArray());
                }
            }
        }

        [Fact]
        public void Large_Duplicate_Set_Retains_Every_Mode_In_First_Appearance_Order()
        {
            var array = new FormulaArray(20000, 1);
            for (var i = 0; i < 10000; i++)
                array[i, 0] = array[10000 + i, 0] = FormulaValue.FromNumber(9999 - i);
            var result = Invoke(Context(), "MODE.MULT", FormulaValue.FromArray(array)).AsArray();
            Assert.Equal(10000, result.RowCount);
            for (var i = 0; i < 10000; i++) Assert.Equal(9999 - i, result[i, 0].AsNumber());
        }

        [Fact]
        public void Pooled_Error_Exits_And_Parallel_Calls_Do_Not_Leak_Counts()
        {
            var context = Context();
            var diagnostic = new FormulaError(FormulaErrorType.Div0, "original diagnostic");
            Parallel.For(0, 64, iteration =>
            {
                var values = new FormulaArray(1025, 1);
                for (var i = 0; i < 1024; i++) values[i, 0] = FormulaValue.FromNumber(i);
                values[1024, 0] = FormulaValue.FromError(diagnostic);
                Assert.Equal(diagnostic, Invoke(context, "MODE.MULT", FormulaValue.FromArray(values)).AsError());
                values[1024, 0] = FormulaValue.FromNumber(iteration);
                Assert.Equal(iteration, Invoke(context, "MODE.SNGL", FormulaValue.FromArray(values)).AsNumber());
                Assert.Equal(iteration, Invoke(context, "MODE.MULT", FormulaValue.FromArray(values)).AsArray()[0, 0].AsNumber());
            });
        }

        [Fact]
        public void Warm_Scalar_Mode_Does_Not_Allocate_A_Sample_List_Or_Dictionary()
        {
            var context = Context();
            var source = new FormulaArray(10000, 1);
            for (var i = 0; i < source.RowCount; i++) source[i, 0] = FormulaValue.FromNumber(i % 1000);
            Assert.True(context.FunctionRegistry.TryGetFunction("MODE.SNGL", out var function));
            var call = new FormulaFunctionContext(context);
            var args = new[] { FormulaValue.FromArray(source) };
            for (var i = 0; i < 100; i++) function.Invoke(call, args);
            var before = GC.GetAllocatedBytesForCurrentThread();
            double sum = 0;
            for (var i = 0; i < 100; i++) sum += function.Invoke(call, args).AsNumber();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, sum);
            Assert.Equal(0, allocated);
        }

        [Fact]
        public void Mode_Multi_Spills_Recalculates_And_Clears_Obsolete_Results()
        {
            var context = Context();
            var sheet = context.Worksheet;
            var initial = new[] { 3, 2, 1, 3, 2, 1 };
            for (var i = 0; i < initial.Length; i++) sheet.GetCell(i + 1, 1).Value = FormulaValue.FromNumber(initial[i]);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet, 1, 3, "MODE.MULT(A1:A6)");
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 1, 3) });
            Assert.Equal("3;2;1", Describe(sheet.GetCell(1, 3).Value));
            Assert.Equal(1, sheet.GetCell(3, 3).Value.AsNumber());
            sheet.GetCell(6, 1).Value = FormulaValue.FromNumber(3);
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 6, 1) });
            Assert.Equal("3", Describe(sheet.GetCell(1, 3).Value));
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(2, 3).Value.Kind);
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(3, 3).Value.Kind);
            for (var i = 0; i < 6; i++) sheet.GetCell(i + 1, 1).Value = FormulaValue.FromNumber(i);
            engine.Recalculate(context.Workbook, Enumerable.Range(1, 6).Select(i => new FormulaCellAddress("Sheet1", i, 1)));
            Assert.Equal(FormulaErrorType.NA, sheet.GetCell(1, 3).Value.AsError().Type);
        }

        [Fact]
        public void Source_Expression_Is_Evaluated_Once_Without_Materializing_A_Second_Copy()
        {
            var registry = new ExcelFunctionRegistry();
            var function = new CountingFunction(); registry.Register(function);
            var context = Context(registry);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                function.Calls = 0;
                Assert.Equal("2", Describe(new FormulaEvaluator().Evaluate(Parse("MODE.MULT(SOURCE())"), context, new DictionaryValueResolver())));
                Assert.Equal(1, function.Calls);
            }
        }

        private sealed class CountingFunction : IFormulaFunction
        {
            public int Calls;
            public string Name => "SOURCE";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            {
                Calls++;
                var result = new FormulaArray(2, 1); result[0, 0] = result[1, 0] = FormulaValue.FromNumber(2);
                return FormulaValue.FromArray(result);
            }
        }

        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry = null)
        {
            var workbook = new TestWorkbook("Book1"); workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 5), registry ?? new ExcelFunctionRegistry());
        }
        private static FormulaExpression Parse(string text) => new ExcelFormulaParser().Parse(text, new FormulaParseOptions());
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
                return string.Join(";", Enumerable.Range(0, array.RowCount).Select(row =>
                    string.Join(",", Enumerable.Range(0, array.ColumnCount).Select(col => Describe(array[row, col])))));
            }
            return value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString();
        }
    }
}
