// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelBitwiseFunctionTests
    {
        [Theory]
        [InlineData("BITAND(13,25)", "9")]
        [InlineData("BITOR(9,12)", "13")]
        [InlineData("BITXOR(5,6)", "3")]
        [InlineData("BITLSHIFT(4,2)", "16")]
        [InlineData("BITRSHIFT(13,2)", "3")]
        [InlineData("BITLSHIFT(63,-2)", "15")]
        [InlineData("BITRSHIFT(63,-2)", "252")]
        [InlineData("BITLSHIFT(1,47)", "140737488355328")]
        [InlineData("BITLSHIFT(1,48)", "#NUM!")]
        [InlineData("BITLSHIFT(0,53)", "0")]
        [InlineData("BITLSHIFT(0,-53)", "0")]
        [InlineData("BITLSHIFT(0,54)", "#NUM!")]
        [InlineData("BITRSHIFT(281474976710655,53)", "0")]
        [InlineData("BITLSHIFT(281474976710655,0)", "281474976710655")]
        [InlineData("BITLSHIFT(281474976710655,1)", "#NUM!")]
        [InlineData("BITRSHIFT(1,-48)", "#NUM!")]
        [InlineData("BITAND(281474976710655,1)", "1")]
        [InlineData("BITOR(281474976710655,0)", "281474976710655")]
        [InlineData("BITXOR(281474976710655,281474976710655)", "0")]
        [InlineData("BITAND(281474976710656,0)", "#NUM!")]
        [InlineData("BITAND(-1,0)", "#NUM!")]
        [InlineData("BITAND(1.5,1)", "#NUM!")]
        [InlineData("BITOR(0,1.5)", "#NUM!")]
        [InlineData("BITXOR(1,-1)", "#NUM!")]
        [InlineData("BITLSHIFT(2,1.5)", "#NUM!")]
        [InlineData("BITRSHIFT(2,-1.5)", "#NUM!")]
        [InlineData("BITLSHIFT(2,2147483647)", "#NUM!")]
        [InlineData("BITRSHIFT(2,-2147483648)", "#NUM!")]
        [InlineData("BITXOR(NA(),1)", "#N/A")]
        [InlineData("BITLSHIFT(1,1/0)", "#DIV/0!")]
        [InlineData("BITAND(\"bad\",1)", "#VALUE!")]
        [InlineData("BITOR(1,\"bad\")", "#VALUE!")]
        [InlineData("BITAND(\"13\",25)", "9")]
        [InlineData("BITAND(A1,1)", "0")]
        [InlineData("BITAND(1)", "#VALUE!")]
        [InlineData("BITOR(1,2,3)", "#VALUE!")]
        [InlineData("BITAND({1;2;3},{1,2})", "1,0;0,2;1,2")]
        [InlineData("BITLSHIFT({1;2},{0,1,2})", "1,2,4;2,4,8")]
        [InlineData("BITAND(HSTACK(1,NA(),2),1)", "1,#N/A,0")]
        [InlineData("BITOR({1;2},{1;2;3})", "#VALUE!")]
        [InlineData("LET(mask,3,SUM(BITAND({1;2;4},mask)))", "3")]
        public void Formula_And_Formatter_Paths_Agree(string formula, string expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var reparsed = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var precision in new[] { true, false })
                {
                    context.Workbook.Settings.ApplyNumberPrecision = precision;
                    var evaluator = new FormulaEvaluator();
                    Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                    Assert.Equal(expected, Describe(evaluator.Evaluate(reparsed, context, new WorkbookValueResolver())));
                }
            }
        }

        [Fact]
        public void All_Byte_Pairs_Agree_With_Independent_Bit_Expansion()
        {
            var context = Context();
            var functions = new[] { Function(context, "BITAND"), Function(context, "BITOR"), Function(context, "BITXOR") };
            var args = new FormulaValue[2];
            var invocation = new FormulaFunctionContext(context);
            for (var a = 0; a <= 255; a++)
                for (var b = 0; b <= 255; b++)
                {
                    args[0] = FormulaValue.FromNumber(a);
                    args[1] = FormulaValue.FromNumber(b);
                    var both = 0; var either = 0; var different = 0;
                    for (var weight = 1; weight <= 128; weight *= 2)
                    {
                        var x = a / weight % 2 != 0;
                        var y = b / weight % 2 != 0;
                        if (x && y) both += weight;
                        if (x || y) either += weight;
                        if (x != y) different += weight;
                    }
                    Assert.Equal(both, functions[0].Invoke(invocation, args).AsNumber());
                    Assert.Equal(either, functions[1].Invoke(invocation, args).AsNumber());
                    Assert.Equal(different, functions[2].Invoke(invocation, args).AsNumber());
                }
        }

        [Fact]
        public void Every_Supported_Shift_Agrees_With_Exact_Arithmetic_On_Seeded_Inputs()
        {
            var context = Context();
            var left = Function(context, "BITLSHIFT");
            var right = Function(context, "BITRSHIFT");
            var call = new FormulaFunctionContext(context);
            var random = new Random(283741);
            var args = new FormulaValue[2];
            const long maximum = 281474976710655L;
            for (var i = 0; i < 256; i++)
            {
                var value = i < 128 ? i : random.NextInt64(0, maximum + 1);
                args[0] = FormulaValue.FromNumber(value);
                for (var amount = -53; amount <= 53; amount++)
                {
                    args[1] = FormulaValue.FromNumber(amount);
                    var factor = System.Numerics.BigInteger.One << Math.Abs(amount);
                    var expectedLeft = amount < 0 ? value / factor : value * factor;
                    var expectedRight = amount < 0 ? value * factor : value / factor;
                    Check(expectedLeft, left.Invoke(call, args));
                    Check(expectedRight, right.Invoke(call, args));
                }
            }
            static void Check(System.Numerics.BigInteger expected, FormulaValue result)
            {
                if (expected > 281474976710655L) Assert.Equal(FormulaErrorType.Num, result.AsError().Type);
                else Assert.Equal((double)expected, result.AsNumber());
            }
        }

        [Theory]
        [InlineData("BITAND")]
        [InlineData("BITOR")]
        [InlineData("BITXOR")]
        [InlineData("BITLSHIFT")]
        [InlineData("BITRSHIFT")]
        public void Nonfinite_Values_And_Eager_Arity_Are_Rejected(string name)
        {
            var context = Context();
            var function = Function(context, name);
            Assert.False(function is ILazyFormulaFunction);
            Assert.Equal(FormulaErrorType.Value, function.Invoke(new FormulaFunctionContext(context), Array.Empty<FormulaValue>()).AsError().Type);
            foreach (var number in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                foreach (var first in new[] { true, false })
                {
                    var values = new[] { FormulaValue.FromNumber(first ? number : 1), FormulaValue.FromNumber(first ? 1 : number) };
                    Assert.Equal(FormulaErrorType.Num, function.Invoke(new FormulaFunctionContext(context), values).AsError().Type);
                }
        }

        [Fact]
        public void Sparse_Broadcasting_Preserves_Inputs_And_Enforces_Output_Bounds()
        {
            var context = Context();
            var source = new FormulaArray(2, 1, sparse: true);
            source[0, 0] = FormulaValue.FromNumber(3);
            var masks = new FormulaArray(1, 2);
            masks[0, 0] = FormulaValue.FromNumber(1);
            masks[0, 1] = FormulaValue.FromNumber(2);
            var function = Function(context, "BITAND");
            var args = new[] { FormulaValue.FromArray(source), FormulaValue.FromArray(masks) };
            Assert.Equal("1,2;0,0", Describe(function.Invoke(new FormulaFunctionContext(context), args)));
            Assert.False(source.IsPresent(1, 0));
            Assert.Equal(3, source[0, 0].AsNumber());
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, function.Invoke(new FormulaFunctionContext(context), args).AsError().Type);
        }

        [Fact]
        public void Warm_Scalar_Invocation_Does_Not_Allocate_Per_Call()
        {
            var context = Context();
            var function = Function(context, "BITXOR");
            var call = new FormulaFunctionContext(context);
            var args = new[] { FormulaValue.FromNumber(13), FormulaValue.FromNumber(25) };
            for (var i = 0; i < 100; i++) function.Invoke(call, args);
            var before = GC.GetAllocatedBytesForCurrentThread();
            double checksum = 0;
            for (var i = 0; i < 1000; i++) checksum += function.Invoke(call, args).AsNumber();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(20000, checksum);
            Assert.True(allocated < 4096, $"Warm scalar allocation: {allocated}");
        }

        [Fact]
        public void Shared_Functions_Are_Safe_For_Independent_Concurrent_Calls()
        {
            var context = Context();
            var function = Function(context, "BITXOR");
            var call = new FormulaFunctionContext(context);
            Parallel.For(0, 1024, i => Assert.Equal(i ^ 511,
                function.Invoke(call, new[] { FormulaValue.FromNumber(i), FormulaValue.FromNumber(511) }).AsNumber()));
        }

        [Fact]
        public void Bitwise_Array_Formulas_Spill_And_Recalculate_Through_Existing_Dependencies()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(3);
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(7);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "BITAND(A1:A2,3)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 3) });
            Assert.Equal(3, sheet.GetCell(2, 3).Value.AsNumber());
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(4);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 1) });
            Assert.Equal(0, sheet.GetCell(2, 3).Value.AsNumber());
        }

        private static IFormulaFunction Function(FormulaEvaluationContext context, string name)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function;
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }

        private static string Describe(FormulaValue value)
        {
            if (value.Kind != FormulaValueKind.Array)
                return value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString();
            var array = value.AsArray();
            var text = new StringBuilder();
            for (var row = 0; row < array.RowCount; row++)
            {
                if (row > 0) text.Append(';');
                for (var col = 0; col < array.ColumnCount; col++)
                {
                    if (col > 0) text.Append(',');
                    text.Append(Describe(array[row, col]));
                }
            }
            return text.ToString();
        }
    }
}
