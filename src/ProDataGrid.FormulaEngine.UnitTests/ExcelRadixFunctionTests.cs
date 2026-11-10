// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed partial class ExcelRadixFunctionTests
    {
        [Theory]
        [InlineData("BIN2DEC(111)", "7")]
        [InlineData("BIN2DEC(\"1000000000\")", "-512")]
        [InlineData("BIN2DEC(\"1111111111\")", "-1")]
        [InlineData("BIN2HEX(11111011,4)", "00FB")]
        [InlineData("BIN2HEX(\"1111111111\")", "FFFFFFFFFF")]
        [InlineData("BIN2OCT(1001,3)", "011")]
        [InlineData("BIN2OCT(\"1111111111\")", "7777777777")]
        [InlineData("DEC2BIN(9,4)", "1001")]
        [InlineData("DEC2BIN(-100)", "1110011100")]
        [InlineData("DEC2BIN(0)", "0")]
        [InlineData("DEC2BIN(3.9)", "11")]
        [InlineData("DEC2BIN(-3.9)", "1111111101")]
        [InlineData("DEC2BIN(511)", "111111111")]
        [InlineData("DEC2BIN(-512)", "1000000000")]
        [InlineData("DEC2BIN(512)", "#NUM!")]
        [InlineData("DEC2BIN(-513)", "#NUM!")]
        [InlineData("DEC2OCT(58,3)", "072")]
        [InlineData("DEC2OCT(-1)", "7777777777")]
        [InlineData("DEC2OCT(-536870912)", "4000000000")]
        [InlineData("DEC2OCT(536870911)", "3777777777")]
        [InlineData("DEC2OCT(536870912)", "#NUM!")]
        [InlineData("DEC2HEX(100,4)", "0064")]
        [InlineData("DEC2HEX(-54)", "FFFFFFFFCA")]
        [InlineData("DEC2HEX(549755813887)", "7FFFFFFFFF")]
        [InlineData("DEC2HEX(-549755813888)", "8000000000")]
        [InlineData("DEC2HEX(549755813888)", "#NUM!")]
        [InlineData("HEX2DEC(\"ff\")", "255")]
        [InlineData("HEX2DEC(\"FFFFFFFFFF\")", "-1")]
        [InlineData("HEX2DEC(\"8000000000\")", "-549755813888")]
        [InlineData("HEX2BIN(\"1FF\")", "111111111")]
        [InlineData("HEX2BIN(\"FFFFFFFE00\")", "1000000000")]
        [InlineData("HEX2BIN(\"200\")", "#NUM!")]
        [InlineData("HEX2OCT(\"F\",3)", "017")]
        [InlineData("HEX2OCT(\"3B4E\")", "35516")]
        [InlineData("HEX2OCT(\"FFFFFFFF00\")", "7777777400")]
        [InlineData("HEX2OCT(\"FFE0000000\")", "4000000000")]
        [InlineData("HEX2OCT(\"20000000\")", "#NUM!")]
        [InlineData("OCT2DEC(\"7777777777\")", "-1")]
        [InlineData("OCT2DEC(\"4000000000\")", "-536870912")]
        [InlineData("OCT2BIN(\"7777777000\")", "1000000000")]
        [InlineData("OCT2BIN(\"777\")", "111111111")]
        [InlineData("OCT2BIN(\"1000\")", "#NUM!")]
        [InlineData("OCT2HEX(\"7777777777\")", "FFFFFFFFFF")]
        [InlineData("OCT2HEX(\"100\",4)", "0040")]
        [InlineData("DEC2HEX(1,2.9)", "01")]
        [InlineData("DEC2HEX(15,)", "F")]
        [InlineData("DEC2HEX(15,A1)", "#NUM!")]
        [InlineData("DEC2HEX(-1,1)", "FFFFFFFFFF")]
        [InlineData("DEC2HEX(-1,0)", "#NUM!")]
        [InlineData("DEC2HEX(1,0)", "#NUM!")]
        [InlineData("DEC2HEX(1,-1)", "#NUM!")]
        [InlineData("DEC2HEX(1,11)", "#NUM!")]
        [InlineData("DEC2HEX(256,1)", "#NUM!")]
        [InlineData("DEC2HEX(-1,\"bad\")", "#VALUE!")]
        [InlineData("DEC2HEX(1,NA())", "#N/A")]
        [InlineData("LAMBDA(n,p,DEC2HEX(n,p))(15,)", "F")]
        [InlineData("BIN2DEC(\"2\")", "#NUM!")]
        [InlineData("BIN2DEC(\"11111111111\")", "#NUM!")]
        [InlineData("OCT2DEC(\"8\")", "#NUM!")]
        [InlineData("HEX2DEC(\"G\")", "#NUM!")]
        [InlineData("HEX2DEC(\"FFFFFFFFFFF\")", "#NUM!")]
        [InlineData("HEX2DEC(\"0xFF\")", "#NUM!")]
        [InlineData("HEX2DEC(\" FF\")", "#NUM!")]
        [InlineData("HEX2DEC(\"-1\")", "#NUM!")]
        [InlineData("BIN2DEC(NA())", "#N/A")]
        [InlineData("BIN2DEC(\"\")", "0")]
        [InlineData("BIN2DEC(A1)", "0")]
        [InlineData("BASE(7,2)", "111")]
        [InlineData("BASE(100,16)", "64")]
        [InlineData("BASE(15,2,10)", "0000001111")]
        [InlineData("BASE(35,36)", "Z")]
        [InlineData("BASE(0,36)", "0")]
        [InlineData("BASE(0,36,3)", "000")]
        [InlineData("BASE(10.9,16.9,3.9)", "00A")]
        [InlineData("BASE(1,1)", "#NUM!")]
        [InlineData("BASE(1,37)", "#NUM!")]
        [InlineData("BASE(1,2,256)", "#NUM!")]
        [InlineData("BASE(-1,2)", "#NUM!")]
        [InlineData("BASE(9007199254740992,16)", "#NUM!")]
        [InlineData("BASE(1,2,)", "1")]
        [InlineData("BASE(\"bad\",16)", "#VALUE!")]
        [InlineData("BASE(1,NA())", "#N/A")]
        [InlineData("DECIMAL(\"FF\",16)", "255")]
        [InlineData("DECIMAL(111,2)", "7")]
        [InlineData("DECIMAL(\"zap\",36)", "45745")]
        [InlineData("DECIMAL(\"FFFFFFFFFF\",16)", "1099511627775")]
        [InlineData("DECIMAL(\"\",16)", "0")]
        [InlineData("DECIMAL(\"G\",16)", "#NUM!")]
        [InlineData("DECIMAL(\"10\",1)", "#NUM!")]
        [InlineData("DECIMAL(\"10\",37)", "#NUM!")]
        [InlineData("DECIMAL(\"-1\",16)", "#NUM!")]
        [InlineData("DECIMAL(\"0xFF\",16)", "#NUM!")]
        [InlineData("DECIMAL(\" 10\",2)", "#NUM!")]
        [InlineData("DECIMAL(NA(),16)", "#N/A")]
        [InlineData("BASE({10;11},{2,16})", "1010,A;1011,B")]
        [InlineData("DEC2HEX({15;16},{2,3})", "0F,00F;10,010")]
        [InlineData("BIN2DEC(HSTACK(\"10\",NA(),\"11\"))", "2,#N/A,3")]
        [InlineData("DECIMAL({\"A\";\"Z\"},{16,36})", "10,10;#NUM!,35")]
        [InlineData("LET(encoded,DEC2HEX({1;2;3}),SUM(HEX2DEC(encoded)))", "6")]
        public void Examples_And_Boundaries_Agree_In_Both_Evaluators(string formula, string expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var reparsed = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                Assert.Equal(expected, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                Assert.Equal(expected, Describe(evaluator.Evaluate(reparsed, context, new WorkbookValueResolver())));
            }
        }

        [Fact]
        public void All_Ten_Bit_Encodings_RoundTrip_Through_All_Legacy_Bases()
        {
            var context = Context();
            for (var bits = 0; bits < 1024; bits++)
            {
                var number = bits < 512 ? bits : bits - 1024;
                var binary = Convert.ToString(bits, 2).PadLeft(10, '0');
                Assert.Equal(number, Invoke(context, "BIN2DEC", FormulaValue.FromText(binary)).AsNumber());
                var octal = LegacyFormat(number, 8, 30);
                var hex = LegacyFormat(number, 16, 40);
                Assert.Equal(octal, Invoke(context, "BIN2OCT", FormulaValue.FromText(binary)).AsText());
                Assert.Equal(hex, Invoke(context, "BIN2HEX", FormulaValue.FromText(binary)).AsText());
                Assert.Equal(number, Invoke(context, "OCT2DEC", FormulaValue.FromText(octal)).AsNumber());
                Assert.Equal(number, Invoke(context, "HEX2DEC", FormulaValue.FromText(hex)).AsNumber());
                var minimalBinary = LegacyFormat(number, 2, 10);
                Assert.Equal(minimalBinary, Invoke(context, "OCT2BIN", FormulaValue.FromText(octal)).AsText());
                Assert.Equal(minimalBinary, Invoke(context, "HEX2BIN", FormulaValue.FromText(hex)).AsText());
                Assert.Equal(minimalBinary, Invoke(context, "DEC2BIN", FormulaValue.FromNumber(number)).AsText());
            }
        }

        [Theory]
        [InlineData(8, 30, "DEC2OCT", "OCT2DEC")]
        [InlineData(16, 40, "DEC2HEX", "HEX2DEC")]
        public void Wide_Signed_Conversions_Agree_With_Independent_Integral_Formatters(int radix, int bits, string toRadix, string toDecimal)
        {
            var context = Context();
            var random = new Random(82633 + radix);
            for (var i = 0; i < 2048; i++)
            {
                var value = random.NextInt64(-(1L << (bits - 1)), 1L << (bits - 1));
                var expected = LegacyFormat(value, radix, bits);
                Assert.Equal(expected, Invoke(context, toRadix, FormulaValue.FromNumber(value)).AsText());
                Assert.Equal(value, Invoke(context, toDecimal, FormulaValue.FromText(expected.ToLowerInvariant())).AsNumber());
            }
        }

        [Fact]
        public void All_Radices_And_Exact_Double_Integers_RoundTrip()
        {
            var context = Context();
            var random = new Random(91371);
            for (var radix = 2; radix <= 36; radix++)
                for (var i = 0; i < 300; i++)
                {
                    var value = i < 100 ? i : random.NextInt64(0, 9007199254740992L);
                    var text = BigIntegerFormat(value, radix);
                    var actual = Invoke(context, "BASE", FormulaValue.FromNumber(value), FormulaValue.FromNumber(radix));
                    Assert.Equal(text, actual.AsText());
                    Assert.Equal(value, Invoke(context, "DECIMAL", FormulaValue.FromText(text), FormulaValue.FromNumber(radix)).AsNumber());
                    Assert.Equal(text.PadLeft(64, '0'), Invoke(context, "BASE", FormulaValue.FromNumber(value),
                        FormulaValue.FromNumber(radix), FormulaValue.FromNumber(64)).AsText());
                }
        }

        // The independent oracle parses an exact BigInteger's decimal representation;
        // it does not use the engine's radix accumulator, scaling or rounding helpers.
        [Theory]
        [MemberData(nameof(RoundingCases))]
        public void Decimal_Conversion_Rounds_The_Whole_Integer_Once(string digits, int radix, long expectedBits)
        {
            var value = Invoke(Context(), "DECIMAL", FormulaValue.FromText(digits), FormulaValue.FromNumber(radix));
            if (double.IsPositiveInfinity(BitConverter.Int64BitsToDouble(expectedBits)))
                Assert.Equal(FormulaErrorType.Num, value.AsError().Type);
            else
                Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(value.AsNumber()));
        }

        [Fact]
        public void Overflow_And_Input_Length_Are_Bounded_Without_BigInteger_Workspace()
        {
            var context = Context();
            Assert.Equal(255, Invoke(context, "BASE", FormulaValue.FromNumber(1), FormulaValue.FromNumber(2), FormulaValue.FromNumber(255)).AsText().Length);
            Assert.Equal(0, Invoke(context, "DECIMAL", FormulaValue.FromText(new string('0', 255)), FormulaValue.FromNumber(36)).AsNumber());
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "DECIMAL", FormulaValue.FromText(new string('0', 256)), FormulaValue.FromNumber(36)).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "DECIMAL", FormulaValue.FromText(new string('Z', 255)), FormulaValue.FromNumber(36)).AsError().Type);
            var threshold = (BigInteger.One << 1024) - (BigInteger.One << 970);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "DECIMAL", FormulaValue.FromText(BigIntegerFormat(threshold, 36)), FormulaValue.FromNumber(36)).AsError().Type);
            Assert.Equal(double.MaxValue, Invoke(context, "DECIMAL", FormulaValue.FromText(BigIntegerFormat(threshold - 1, 36)), FormulaValue.FromNumber(36)).AsNumber());
        }

        [Fact]
        public void Positive_Padding_And_Negative_Sign_Extension_Are_Distinct()
        {
            var context = Context();
            Assert.Equal("0000000001", Invoke(context, "DEC2HEX", FormulaValue.FromNumber(1), FormulaValue.FromNumber(10)).AsText());
            Assert.Equal("FFFFFFFFFF", Invoke(context, "DEC2HEX", FormulaValue.FromNumber(-1), FormulaValue.FromNumber(1)).AsText());
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "DEC2HEX", FormulaValue.FromNumber(1), FormulaValue.Blank).AsError().Type);
            Assert.Equal("1", Invoke(context, "DEC2HEX", FormulaValue.FromNumber(1)).AsText());
            Assert.Equal("8000000000", Invoke(context, "BASE", FormulaValue.FromNumber(549755813888), FormulaValue.FromNumber(16)).AsText());
        }

        [Fact]
        public void Numeric_Results_Still_Use_Host_Significant_Digit_Settings()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            Assert.Equal(12300, Invoke(context, "DECIMAL", FormulaValue.FromText("12345"), FormulaValue.FromNumber(10)).AsNumber());
            Assert.Equal(65500, Invoke(context, "HEX2DEC", FormulaValue.FromText("FFFF")).AsNumber());
        }

        [Fact]
        public void Nonfinite_Numbers_And_Arity_Are_Rejected_Before_Integer_Casts()
        {
            var context = Context();
            foreach (var number in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "BASE", FormulaValue.FromNumber(number), FormulaValue.FromNumber(16)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "DEC2HEX", FormulaValue.FromNumber(number)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "DECIMAL", FormulaValue.FromText("FF"), FormulaValue.FromNumber(number)).AsError().Type);
            }
            foreach (var name in new[] { "BASE", "DECIMAL", "BIN2DEC", "DEC2BIN", "HEX2OCT" })
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name).AsError().Type);
        }

        [Fact]
        public void Array_Projection_Preserves_Masks_And_Checks_Output_Size()
        {
            var context = Context();
            var input = new FormulaArray(2, 1, sparse: true);
            input[0, 0] = FormulaValue.FromNumber(10);
            var bases = new FormulaArray(1, 2);
            bases[0, 0] = FormulaValue.FromNumber(2);
            bases[0, 1] = FormulaValue.FromNumber(16);
            var first = Invoke(context, "BASE", FormulaValue.FromArray(input), FormulaValue.FromArray(bases));
            Assert.Equal("1010,A;0,0", Describe(first));
            Assert.False(input.IsPresent(1, 0));
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "BASE", FormulaValue.FromArray(input), FormulaValue.FromArray(bases)).AsError().Type);
            Assert.Equal("1010,A;0,0", Describe(first));
        }

        [Fact]
        public void Text_Decimal_Parsing_Has_No_Warm_Managed_Workspace_Allocation()
        {
            var context = Context();
            Assert.True(context.FunctionRegistry.TryGetFunction("DECIMAL", out var function));
            var call = new FormulaFunctionContext(context);
            var args = new[] { FormulaValue.FromText("1000000000000000000000000000000"), FormulaValue.FromNumber(16) };
            for (var i = 0; i < 100; i++) function.Invoke(call, args);
            var before = GC.GetAllocatedBytesForCurrentThread();
            double checksum = 0;
            for (var i = 0; i < 1000; i++) checksum += function.Invoke(call, args).AsNumber();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(Math.ScaleB(1000, 120), checksum);
            Assert.True(allocated < 4096, $"Warm DECIMAL workspace allocation: {allocated}");
        }

        [Fact]
        public void Independent_Parallel_Conversions_Do_Not_Share_Digit_Buffers()
        {
            var context = Context();
            Parallel.For(0, 1024, i =>
            {
                var text = Invoke(context, "BASE", FormulaValue.FromNumber(i), FormulaValue.FromNumber(16)).AsText();
                Assert.Equal(i.ToString("X", CultureInfo.InvariantCulture), text);
                Assert.Equal(i, Invoke(context, "DECIMAL", FormulaValue.FromText(text), FormulaValue.FromNumber(16)).AsNumber());
            });
        }

        [Fact]
        public void Radix_Values_Spill_And_Recalculate_After_Input_Changes()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(15);
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(16);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "DEC2HEX(A1:A2,2)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 3) });
            Assert.Equal("10", sheet.GetCell(2, 3).Value.AsText());
            sheet.GetCell(2, 1).Value = FormulaValue.FromNumber(255);
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 2, 1) });
            Assert.Equal("FF", sheet.GetCell(2, 3).Value.AsText());
        }

        private static string LegacyFormat(long number, int radix, int bits)
        {
            var raw = number < 0 ? number + (1L << bits) : number;
            var text = Convert.ToString(raw, radix).ToUpperInvariant();
            return number < 0 ? text.PadLeft(10, '0') : text;
        }

        private static string BigIntegerFormat(BigInteger value, int radix)
        {
            if (value.IsZero) return "0";
            var reversed = new StringBuilder();
            while (value > 0)
            {
                value = BigInteger.DivRem(value, radix, out var remainder);
                reversed.Append("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[(int)remainder]);
            }
            var characters = new char[reversed.Length];
            for (var i = 0; i < characters.Length; i++) characters[i] = reversed[characters.Length - i - 1];
            return new string(characters);
        }

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
