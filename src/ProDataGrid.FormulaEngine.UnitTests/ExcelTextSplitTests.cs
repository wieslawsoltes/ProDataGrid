// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelTextSplitTests
    {
        [Theory]
        [InlineData("TEXTSPLIT(\"Dakota Lennon Sanchez\",\" \")", 1, 3, "Dakota|Lennon|Sanchez")]
        [InlineData("TEXTSPLIT(\"1,2,3;4,5,6\",\",\",\";\")", 2, 3, "1|2|3/4|5|6")]
        [InlineData("TEXTSPLIT(\"a;b;c\",,\";\")", 3, 1, "a/b/c")]
        [InlineData("TEXTSPLIT(\"a.b-c\",,{\".\",\"-\"})", 3, 1, "a/b/c")]
        [InlineData("TEXTSPLIT(\"a.b-c\",{\".\",\"-\"})", 1, 3, "a|b|c")]
        [InlineData("TEXTSPLIT(\"a,b;c\",\",\",\";\")", 2, 2, "a|b/c|#N/A")]
        [InlineData("TEXTSPLIT(\"a,b;c\",\",\",\";\",,,0)", 2, 2, "a|b/c|0")]
        [InlineData("TEXTSPLIT(\"a,b;c\",\",\",\";\",,,\"missing\")", 2, 2, "a|b/c|missing")]
        [InlineData("TEXTSPLIT(\"a,b;c\",\",\",\";\",,,1/0)", 2, 2, "a|b/c|#DIV/0!")]
        [InlineData("TEXTSPLIT(\"a,b\",\",\",,,,1/0)", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT(\",a,,b,\",\",\")", 1, 5, "|a||b|")]
        [InlineData("TEXTSPLIT(\",a,,b,\",\",\",,TRUE)", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT(\";a;;b;\",,\";\")", 5, 1, "/a//b/")]
        [InlineData("TEXTSPLIT(\";a;;b;\",,\";\",TRUE)", 2, 1, "a/b")]
        [InlineData("TEXTSPLIT(\"a,,b;c,;d,e,f\",\",\",\";\",TRUE)", 3, 3, "a|b|#N/A/c|#N/A|#N/A/d|e|f")]
        [InlineData("TEXTSPLIT(\"aXXbxxc\",\"xx\")", 1, 2, "aXXb|c")]
        [InlineData("TEXTSPLIT(\"aXXbxxc\",\"xx\",,,1)", 1, 3, "a|b|c")]
        [InlineData("TEXTSPLIT(\"aXXbYYc\",{\"xx\",\"yy\"},,,1)", 1, 3, "a|b|c")]
        [InlineData("TEXTSPLIT(\"a😀b😀c\",\"😀\")", 1, 3, "a|b|c")]
        [InlineData("TEXTSPLIT(\"a*b?c\",{\"*\",\"?\"})", 1, 3, "a|b|c")]
        [InlineData("TEXTSPLIT(\"abc\",\",\")", 1, 1, "abc")]
        [InlineData("TEXTSPLIT(12345,3)", 1, 2, "12|45")]
        [InlineData("TEXTSPLIT({\"a,b\"},\",\")", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT({\"a,b\";\"c,d\"},\",\")", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT(\"a,b;c\",\",\",\";\",,,{1,2})", 2, 2, "a|b/c|1")]
        [InlineData("TEXTSPLIT(\"a,b\",{\"\",\",\",\",\"})", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT(\"a--b\",{\"-\",\"--\"})", 1, 3, "a||b")]
        [InlineData("TEXTSPLIT(\"a--b\",{\"--\",\"-\"})", 1, 2, "a|b")]
        [InlineData("TEXTSPLIT(\"a,,b\",\",\",,)", 1, 3, "a||b")]
        [InlineData("TEXTSPLIT(\"a,,b\",\",\",,A1)", 1, 3, "a||b")]
        [InlineData("LAMBDA(x,y,TEXTSPLIT(x,\",\",,y))(\"a,,b\",)", 1, 3, "a||b")]
        [InlineData("LET(parts,TEXTSPLIT(\"1,2;3,4\",\",\",\";\"),TRANSPOSE(parts))", 2, 2, "1|3/2|4")]
        public void Splitting_Composes_In_Both_Evaluators_And_Formatter_RoundTrips(string formula, int rows, int columns, string expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var candidate in new[] { expression, formatted })
                {
                    var result = new FormulaEvaluator().Evaluate(candidate, context, new WorkbookValueResolver()).AsArray();
                    Assert.Equal(rows, result.RowCount);
                    Assert.Equal(columns, result.ColumnCount);
                    Assert.Null(result.Origin);
                    Assert.Equal(expected, Describe(result));
                }
            }
        }

        [Theory]
        [InlineData("TEXTSPLIT(\"x\")", FormulaErrorType.Value)]
        [InlineData("TEXTSPLIT(\"x\",\"\")", FormulaErrorType.Value)]
        [InlineData("TEXTSPLIT(\"x\",,)", FormulaErrorType.Value)]
        [InlineData("TEXTSPLIT(\"\",\",\")", FormulaErrorType.Calc)]
        [InlineData("TEXTSPLIT(\",,,\",\",\",,TRUE)", FormulaErrorType.Calc)]
        [InlineData("TEXTSPLIT(NA(),\",\")", FormulaErrorType.NA)]
        [InlineData("TEXTSPLIT(\"x\",NA())", FormulaErrorType.NA)]
        [InlineData("TEXTSPLIT(\"x\",\",\",NA())", FormulaErrorType.NA)]
        [InlineData("TEXTSPLIT(\"x\",\",\",,NA())", FormulaErrorType.NA)]
        [InlineData("TEXTSPLIT(\"x\",\",\",,,NA())", FormulaErrorType.NA)]
        [InlineData("TEXTSPLIT(\"x\",\",\",,,2)", FormulaErrorType.Value)]
        [InlineData("TEXTSPLIT(\"x\",\",\",,,-1)", FormulaErrorType.Value)]
        public void Invalid_Inputs_Return_Formula_Errors(string formula, FormulaErrorType expected)
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
        public void Seeded_Rectangular_Results_Agree_With_Independent_String_Splitting()
        {
            var random = new Random(892061);
            var context = Context();
            for (var trial = 0; trial < 400; trial++)
            {
                var chars = new char[random.Next(1, 200)];
                for (var i = 0; i < chars.Length; i++) chars[i] = "abc,;"[random.Next(5)];
                var text = new string(chars);
                foreach (var ignore in new[] { true, false })
                {
                    var options = ignore ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
                    var inputRows = text.Split(';', options);
                    var cells = new List<string[]>();
                    var width = 0;
                    foreach (var item in inputRows) { var parts = item.Split(',', options); cells.Add(parts); width = Math.Max(width, parts.Length); }
                    var value = Invoke(context, FormulaValue.FromText(text), FormulaValue.FromText(","), FormulaValue.FromText(";"), FormulaValue.FromBoolean(ignore));
                    if (cells.Count == 0 || width == 0) { Assert.Equal(FormulaErrorType.Calc, value.AsError().Type); continue; }
                    var array = value.AsArray();
                    Assert.Equal(cells.Count, array.RowCount);
                    Assert.Equal(width, array.ColumnCount);
                    for (var row = 0; row < cells.Count; row++) for (var col = 0; col < width; col++)
                    {
                        if (col >= cells[row].Length) Assert.Equal(FormulaErrorType.NA, array[row, col].AsError().Type);
                        else Assert.Equal(cells[row][col], array[row, col].AsText());
                    }
                }
            }
        }

        [Fact]
        public void Delimiter_Masks_And_Source_Values_Are_Not_Modified()
        {
            var context = Context();
            var delimiters = new FormulaArray(3, 1, sparse: true);
            delimiters[0, 0] = FormulaValue.FromText(",");
            delimiters.SetValue(1, 0, FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), false);
            delimiters[2, 0] = FormulaValue.FromText(";");
            var source = FormulaValue.FromText("a,b;c");
            var result = Invoke(context, source, FormulaValue.FromArray(delimiters)).AsArray();
            Assert.Equal("a|b|c", Describe(result));
            result[0, 0] = FormulaValue.FromText("changed");
            Assert.Equal("a,b;c", source.AsText());
            Assert.False(delimiters.IsPresent(1, 0));
            Assert.Equal(FormulaValueKind.Error, delimiters[1, 0].Kind);
            Parallel.For(0, 64, i => Assert.Equal("a|b|c", Describe(Invoke(context, source, FormulaValue.FromArray(delimiters)).AsArray())));
        }

        [Fact]
        public void Cell_Limits_Include_Padding_And_Reject_Oversized_Delimiter_Lists()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 5;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromText("a,b,c;d"), FormulaValue.FromText(","), FormulaValue.FromText(";")).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, FormulaValue.FromText("x"), FormulaValue.FromArray(new FormulaArray(6, 1))).AsError().Type);
            context.Workbook.Settings.MaximumArrayCellCount = 6;
            Assert.Equal("a|b|c/d|#N/A|#N/A", Describe(Invoke(context, FormulaValue.FromText("a,b,c;d"), FormulaValue.FromText(","), FormulaValue.FromText(";")).AsArray()));
            Assert.Equal(FormulaErrorType.Num, Invoke(Context(), FormulaValue.FromText(new string(',', 16384)), FormulaValue.FromText(",")).AsError().Type);
            Assert.Equal(FormulaErrorType.Value, Invoke(Context(), FormulaValue.FromText(new string('a', 32768)), FormulaValue.FromText(",")).AsError().Type);
        }

        [Fact]
        public async Task Rejected_Output_Does_Not_Allocate_Token_Strings_Or_Row_Arrays()
        {
            var context = Context();
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            var source = FormulaValue.FromText(string.Join(",", new string[10000]).Replace(",", "value,"));
            var args = new[] { source, FormulaValue.FromText(",") };
            Assert.True(context.FunctionRegistry.TryGetFunction("TEXTSPLIT", out var function));
            var call = new FormulaFunctionContext(context);
            foreach (var sample in await FormulaAllocationMeasurements.RunAsync(() => (double)function.Invoke(call, args).AsError().Type, 50))
            {
                Assert.Equal(50 * (double)FormulaErrorType.Num, sample.Checksum);
                Assert.True(sample.Bytes < 4096, $"Rejected split allocated {sample.Bytes} bytes.");
            }
        }

        [Fact]
        public void Formula_Spills_Resize_And_Recalculate_Dependent_References()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromText("a,b;c");
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "TEXTSPLIT(A1,\",\",\";\",,,\"\")");
            engine.SetCellFormula(sheet, 1, 7, "COUNTA(C1:D2)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 3), new FormulaCellAddress("Sheet1", 1, 7) });
            Assert.Equal("a|b/c|", Describe(sheet.GetCell(1, 3).Value.AsArray()));
            Assert.Equal(4, sheet.GetCell(1, 7).Value.AsNumber());
            sheet.GetCell(1, 1).Value = FormulaValue.FromText("x,y");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            Assert.Equal("x|y", Describe(sheet.GetCell(1, 3).Value.AsArray()));
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(2, 3).Value.Kind);
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(2, 4).Value.Kind);
            Assert.Equal(2, sheet.GetCell(1, 7).Value.AsNumber());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction("TEXTSPLIT", out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
        private static string Describe(FormulaArray array)
        {
            var result = new StringBuilder();
            for (var row = 0; row < array.RowCount; row++)
            {
                if (row > 0) result.Append('/');
                for (var col = 0; col < array.ColumnCount; col++)
                {
                    if (col > 0) result.Append('|');
                    var value = array[row, col];
                    result.Append(value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString());
                }
            }
            return result.ToString();
        }
    }
}
