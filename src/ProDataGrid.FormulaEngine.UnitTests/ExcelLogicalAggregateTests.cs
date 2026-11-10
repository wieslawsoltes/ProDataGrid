// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    [Collection(FormulaAllocationCollection.Name)]
    public sealed class ExcelLogicalAggregateTests
    {
        [Theory]
        [InlineData("AVERAGEA({10;7;9;2;\"not available\"})", 5.6)]
        [InlineData("MINA({FALSE;0.2;0.5;0.4;0.8})", 0)]
        [InlineData("MAXA({0;0.2;0.5;0.4;TRUE})", 1)]
        [InlineData("AVERAGEA({TRUE;FALSE;4;\"6\"})", 1.25)]
        [InlineData("AVERAGEA(TRUE,FALSE,4,\"6\")", 2.75)]
        [InlineData("AVERAGEA({\"6\";2})", 1)]
        [InlineData("AVERAGEA(\"6\",2)", 4)]
        [InlineData("MINA({\"6\";2})", 0)]
        [InlineData("MINA(\"6\",2)", 2)]
        [InlineData("MAXA({\"6\";2})", 2)]
        [InlineData("MAXA(\"6\",2)", 6)]
        [InlineData("AVERAGEA(\"\",2)", 1)]
        [InlineData("AVERAGEA({\"\";2})", 1)]
        [InlineData("MINA(\"\",2)", 0)]
        [InlineData("MAXA(\"\",-2)", 0)]
        [InlineData("AVERAGEA(A1,2)", 2)]
        [InlineData("AVERAGEA(,2)", 1)]
        [InlineData("AVERAGEA(,)", 0)]
        [InlineData("MINA(,2)", 0)]
        [InlineData("MAXA(,-2)", 0)]
        [InlineData("MINA(A1:A3)", 0)]
        [InlineData("MAXA(A1:A3)", 0)]
        [InlineData("MINA({2;4;6})", 2)]
        [InlineData("MAXA({-2;-4;-6})", -2)]
        [InlineData("AVERAGEA({1;2;3;4})", 2.5)]
        [InlineData("AVERAGEA({1E308;1;-1E308})", 0.3333333333333333)]
        [InlineData("AVERAGEA({1E308;1E308})", 1E308)]
        [InlineData("MINA({1E308;-1E308})", -1E308)]
        [InlineData("MAXA({1E308;-1E308})", 1E308)]
        [InlineData("LET(v,{TRUE;FALSE;4;\"6\"},AVERAGEA(v))", 1.25)]
        [InlineData("SUM(MAP({1;2;3},LAMBDA(x,AVERAGEA(x,TRUE))))", 4.5)]
        [InlineData("LAMBDA(x,y,AVERAGEA(x,2))(,1)", 1)]
        public void Examples_Omissions_And_Array_Coercion_Agree_In_Both_Evaluators(string formula, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                AssertBits(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber());
                AssertBits(expected, evaluator.Evaluate(parser.Parse(formatted, new FormulaParseOptions()), context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Theory]
        [InlineData("AVERAGEA(A1:A4)", FormulaErrorType.Div0)]
        [InlineData("AVERAGEA()", FormulaErrorType.Value)]
        [InlineData("MINA()", FormulaErrorType.Value)]
        [InlineData("MAXA()", FormulaErrorType.Value)]
        [InlineData("AVERAGEA(\"bad\",2)", FormulaErrorType.Value)]
        [InlineData("MINA(\"bad\",2)", FormulaErrorType.Value)]
        [InlineData("MAXA(\"bad\",2)", FormulaErrorType.Value)]
        [InlineData("AVERAGEA(1,NA())", FormulaErrorType.NA)]
        [InlineData("MINA(1,1/0)", FormulaErrorType.Div0)]
        [InlineData("MAXA(HSTACK(1,NA()))", FormulaErrorType.NA)]
        public void Empty_Invalid_And_Error_Arguments_Have_Explicit_Results(string formula, FormulaErrorType expected)
        {
            var context = Context();
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var value = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new WorkbookValueResolver());
                Assert.Equal(expected, value.AsError().Type);
            }
        }

        [Fact]
        public void Cells_And_Single_Cell_References_Keep_Their_Type_Provenance()
        {
            var context = Context();
            var sheet = context.Worksheet;
            sheet.GetCell(1, 1).Value = FormulaValue.FromText("6");
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(2);
            sheet.GetCell(3, 1).Value = FormulaValue.FromBoolean(true);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
                    foreach (var arguments in new[] { "A1:A4", "A1,A2,A3,A4" })
                    {
                        var formula = name + "(" + arguments + ")";
                        var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new WorkbookValueResolver());
                        Assert.Equal(name == "AVERAGEA" ? 1 : name == "MINA" ? 0 : 2, result.AsNumber());
                    }
            }
        }

        [Fact]
        public void Random_Mixed_Arrays_Agree_With_Independent_Exact_Rational_Mean_And_Extrema()
        {
            var random = new Random(481913);
            var context = Context();
            for (var iteration = 0; iteration < 600; iteration++)
            {
                var array = new FormulaArray(random.Next(1, 90), 1, sparse: true);
                var expected = new List<double>();
                for (var row = 0; row < array.RowCount; row++)
                {
                    switch (random.Next(8))
                    {
                        case 0: continue;
                        case 1: array[row, 0] = FormulaValue.Blank; continue;
                        case 2: array[row, 0] = FormulaValue.FromText("123"); expected.Add(0); break;
                        case 3: array[row, 0] = FormulaValue.FromText(""); expected.Add(0); break;
                        case 4: array[row, 0] = FormulaValue.FromBoolean(true); expected.Add(1); break;
                        case 5: array[row, 0] = FormulaValue.FromBoolean(false); expected.Add(0); break;
                        default:
                            var bits = random.NextInt64();
                            if (((ulong)bits >> 52 & 2047) == 2047) bits ^= 1L << 52;
                            if ((row & 1) == 0) bits = unchecked((long)((ulong)bits | 1UL << 63));
                            var number = BitConverter.Int64BitsToDouble(bits);
                            array[row, 0] = FormulaValue.FromNumber(number); expected.Add(number); break;
                    }
                }
                var source = FormulaValue.FromArray(array);
                var average = Invoke(context, "AVERAGEA", source);
                if (expected.Count == 0) Assert.Equal(FormulaErrorType.Div0, average.AsError().Type);
                else AssertBits(RationalMean(expected), average.AsNumber());
                Assert.Equal(expected.Count == 0 ? 0 : expected.Min(), Invoke(context, "MINA", source).AsNumber());
                Assert.Equal(expected.Count == 0 ? 0 : expected.Max(), Invoke(context, "MAXA", source).AsNumber());
            }
        }

        [Fact]
        public void Exact_Means_Preserve_Halfway_Subnormal_And_Cancellation_Cases()
        {
            var context = Context();
            var samples = new[] {
                new[] { 1E308, 1d, -1E308 }, new[] { double.MaxValue, double.MaxValue },
                new[] { double.MinValue, double.MinValue }, new[] { double.MaxValue, double.Epsilon, double.MinValue },
                new[] { 0d, double.Epsilon }, new[] { double.Epsilon, 2*double.Epsilon },
                new[] { 0d, -double.Epsilon }, new[] { -double.Epsilon, -2*double.Epsilon },
                new[] { 1d, Math.BitIncrement(1d) }, new[] { 1E308, 1E-308, -1E308 }
            };
            foreach (var sample in samples)
            {
                var expected = RationalMean(sample);
                var args = sample.Select(FormulaValue.FromNumber).ToArray();
                AssertBits(expected, Invoke(context, "AVERAGEA", args).AsNumber());
                Array.Reverse(args);
                AssertBits(expected, Invoke(context, "AVERAGEA", args).AsNumber());
            }
        }

        [Fact]
        public void Errors_Masks_Unsupported_Payloads_And_Physical_Limits_Are_Preserved()
        {
            var context = Context();
            var diagnostic = new FormulaError(FormulaErrorType.Ref, "host detail");
            var array = new FormulaArray(4, 1, sparse: true);
            array.SetValue(0, 0, FormulaValue.FromError(diagnostic), false);
            array[1, 0] = FormulaValue.FromNumber(3);
            array[2, 0] = FormulaValue.FromText("text");
            foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
            {
                Assert.Equal(name == "AVERAGEA" ? 1.5 : name == "MINA" ? 0 : 3, Invoke(context, name, FormulaValue.FromArray(array)).AsNumber());
                Assert.False(array.IsPresent(0, 0)); Assert.Equal(3, array[1, 0].AsNumber());
                Assert.Equal(diagnostic, Invoke(context, name, FormulaValue.FromError(diagnostic)).AsError());
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, FormulaValue.FromReference(default)).AsError().Type);
                foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                    Assert.Equal(FormulaErrorType.Num, Invoke(context, name, FormulaValue.FromNumber(invalid)).AsError().Type);
                context.Workbook.Settings.MaximumArrayCellCount = 3;
                Assert.Equal(FormulaErrorType.Num, Invoke(context, name, FormulaValue.FromArray(array)).AsError().Type);
                context.Workbook.Settings.MaximumArrayCellCount = 4;
                Assert.Equal(FormulaErrorType.Num, Invoke(context, name, FormulaValue.FromArray(array), FormulaValue.Blank).AsError().Type);
            }
        }

        [Fact]
        public void Culture_Precision_And_Argument_Limits_Are_Explicit()
        {
            var context = Context();
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(1.25, Invoke(context, "AVERAGEA", FormulaValue.FromText("1,25")).AsNumber());
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
            {
                Assert.Equal(1.23, Invoke(context, name, FormulaValue.FromNumber(1.23456)).AsNumber());
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, new FormulaValue[256]).AsError().Type);
                Assert.Equal(2, Invoke(context, name, Enumerable.Repeat(FormulaValue.FromNumber(2), 255).ToArray()).AsNumber());
            }
        }

        [Fact]
        public void Source_Expressions_Are_Evaluated_Once_And_Concurrent_Calls_Are_Independent()
        {
            var context = Context();
            var source = new CountingSource(); ((ExcelFunctionRegistry)context.FunctionRegistry).Register(source);
            foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
            {
                foreach (var compiled in new[] { true, false })
                {
                    context.Workbook.Settings.EnableCompiledExpressions = compiled;
                    source.Calls = 0;
                    var formula = name + "(SOURCE())";
                    var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new DictionaryValueResolver());
                    Assert.Equal(name == "AVERAGEA" ? 1 : name == "MINA" ? 0 : 2, result.AsNumber());
                    Assert.Equal(1, source.Calls);
                }
                Parallel.For(0, 64, i => Assert.Equal(i, Invoke(context, name, FormulaValue.FromNumber(i), FormulaValue.FromNumber(i)).AsNumber()));
            }
        }

        [Fact]
        public void Exact_Mean_And_Extrema_Allocate_No_Managed_Workspace()
        {
            var context = Context(); var source = new FormulaArray(10000, 1);
            for (var i = 0; i < source.RowCount; i++) source[i, 0] = i % 2 == 0 ? FormulaValue.FromNumber(2) : FormulaValue.FromText("zero");
            var args = new[] { FormulaValue.FromArray(source) };
            var call = new FormulaFunctionContext(context);
            foreach (var name in new[] { "AVERAGEA", "MINA", "MAXA" })
            {
                Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
                for (var i = 0; i < 100; i++) function.Invoke(call, args);
                var before = GC.GetAllocatedBytesForCurrentThread();
                double checksum = 0;
                for (var i = 0; i < 100; i++) checksum += function.Invoke(call, args).AsNumber();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(name == "AVERAGEA" ? 100 : name == "MINA" ? 0 : 200, checksum);
                Assert.Equal(0, allocated);
            }
        }

        [Fact]
        public void Dirty_Cell_Recalculation_Includes_Type_Changes()
        {
            var context = Context(); var sheet = context.Worksheet;
            sheet.GetCell(1, 1).Value = FormulaValue.FromText("ignored as zero");
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(2);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet, 1, 3, "AVERAGEA(A1:A2)");
            engine.SetCellFormula(sheet, 2, 3, "MINA(A1:A2)");
            engine.SetCellFormula(sheet, 3, 3, "MAXA(A1:A2)");
            engine.Recalculate(context.Workbook, Enumerable.Range(1, 3).Select(row => new FormulaCellAddress("Sheet1", row, 3)));
            Assert.Equal(1, sheet.GetCell(1, 3).Value.AsNumber()); Assert.Equal(0, sheet.GetCell(2, 3).Value.AsNumber()); Assert.Equal(2, sheet.GetCell(3, 3).Value.AsNumber());
            sheet.GetCell(1, 1).Value = FormulaValue.Blank;
            engine.Recalculate(context.Workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            for (var row = 1; row <= 3; row++) Assert.Equal(2, sheet.GetCell(row, 3).Value.AsNumber());
        }

        // Independent exact rational oracle: search adjacent representable values, then
        // compare distances with BigInteger. No production accumulation/rounding is used.
        private static double RationalMean(IReadOnlyList<double> values)
        {
            BigInteger sum = 0;
            foreach (var value in values) sum += Units(value);
            if (sum.IsZero) return 0;
            var negative = sum.Sign < 0; sum = BigInteger.Abs(sum);
            ulong low = 0, high = 0x7fefffffffffffffUL;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                if (Units(BitConverter.Int64BitsToDouble((long)middle)) * values.Count <= sum) low = middle; else high = middle - 1;
            }
            if (low < 0x7fefffffffffffffUL)
            {
                var below = sum - Units(BitConverter.Int64BitsToDouble((long)low)) * values.Count;
                var above = Units(BitConverter.Int64BitsToDouble((long)(low + 1))) * values.Count - sum;
                if (above < below || above == below && (low & 1) != 0) low++;
            }
            if (negative) low |= 1UL << 63;
            return BitConverter.Int64BitsToDouble(unchecked((long)low));
        }
        private static BigInteger Units(double number)
        {
            var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(number));
            var exponent = (int)(bits >> 52 & 2047); var mantissa = bits & 0xfffffffffffffUL;
            if (exponent != 0) mantissa |= 1UL << 52;
            var units = new BigInteger(mantissa) << Math.Max(0, exponent - 1);
            return bits >> 63 == 0 ? units : -units;
        }
        private sealed class CountingSource : IFormulaFunction
        {
            public int Calls; public string Name => "SOURCE"; public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            {
                Calls++; var array = new FormulaArray(3, 1); array[0, 0] = FormulaValue.FromNumber(2);
                array[1, 0] = FormulaValue.FromBoolean(true); array[2, 0] = FormulaValue.FromText("3"); return FormulaValue.FromArray(array);
            }
        }
        private static void AssertBits(double expected, double actual) => Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1"); workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 5), new ExcelFunctionRegistry());
        }
        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function)); return function.Invoke(new FormulaFunctionContext(context), args);
        }
    }
}
