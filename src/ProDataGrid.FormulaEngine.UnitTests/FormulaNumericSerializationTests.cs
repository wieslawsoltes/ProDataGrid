// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaNumericSerializationTests
    {
        [Theory]
        [InlineData(9007199254740991d)]
        [InlineData(9007199254740992d)]
        [InlineData(9007199254740994d)]
        [InlineData(1.2345678901234567d)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.Epsilon)]
        [InlineData(-double.Epsilon)]
        public void Numeric_Literal_Formatting_Preserves_Exact_Binary64_Value(double number)
            => RoundTrip(number, '.', ',');

        [Fact]
        public void Seeded_Finite_Bit_Patterns_And_Decimal_Separators_RoundTrip()
        {
            var random = new Random(983177);
            for (var i = 0; i < 4096; i++)
            {
                var bits = random.NextInt64();
                if ((i & 1) != 0) bits = ~bits;
                var number = BitConverter.Int64BitsToDouble(bits);
                if (!double.IsFinite(number)) continue;
                RoundTrip(number, '.', ',');
                RoundTrip(number, ',', ';');
            }
            RoundTrip(0, '.', ',');
            RoundTrip(BitConverter.Int64BitsToDouble(long.MinValue), '.', ',');
        }

        [Fact]
        public void Formatting_Does_Not_Change_Function_Domain_Boundaries()
        {
            var parser = new ExcelFormulaParser();
            var formatter = new ExcelFormulaFormatter();
            var expression = parser.Parse("BASE(9007199254740992,16)", new FormulaParseOptions());
            var text = formatter.Format(expression, new FormulaFormatOptions());
            Assert.Equal("BASE(9007199254740992,16)", text);
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var context = new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(FormulaErrorType.Num, evaluator.Evaluate(expression, context, new DictionaryValueResolver()).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, evaluator.Evaluate(parser.Parse(text, new FormulaParseOptions()), context, new DictionaryValueResolver()).AsError().Type);
            }
        }

        private static void RoundTrip(double number, char decimalSeparator, char argumentSeparator)
        {
            var expression = new FormulaLiteralExpression(FormulaValue.FromNumber(number));
            var format = new FormulaFormatOptions { DecimalSeparator = decimalSeparator, ArgumentSeparator = argumentSeparator };
            var parse = new FormulaParseOptions { DecimalSeparator = decimalSeparator, ArgumentSeparator = argumentSeparator };
            var formatter = new ExcelFormulaFormatter();
            var parser = new ExcelFormulaParser();
            var text = formatter.Format(expression, format);
            var reparsed = parser.Parse(text, parse);
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            var context = new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            foreach (var compiled in new[] { true, false })
            {
                workbook.Settings.EnableCompiledExpressions = compiled;
                var result = evaluator.Evaluate(reparsed, context, new DictionaryValueResolver()).AsNumber();
                Assert.Equal(BitConverter.DoubleToInt64Bits(number), BitConverter.DoubleToInt64Bits(result));
            }
            Assert.Equal(text, formatter.Format(reparsed, format));
        }
    }
}
