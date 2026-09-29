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
    public sealed class ExcelExtendedTextTests
    {
        [Theory]
        [InlineData("FIND(\"M\",\"Miriam McGovern\")", "1")]
        [InlineData("FIND(\"m\",\"Miriam McGovern\")", "6")]
        [InlineData("FIND(\"M\",\"Miriam McGovern\",3)", "8")]
        [InlineData("FIND(\"\",\"abc\",2)", "2")]
        [InlineData("FIND(\"a\",\"abc\",)", "1")]
        [InlineData("FIND(\"a\",\"abc\",A1)", "#VALUE!")]
        [InlineData("FIND(\"a\",\"abc\",0)", "#VALUE!")]
        [InlineData("FIND(\"a\",\"abc\",4)", "#VALUE!")]
        [InlineData("FIND(\"A\",\"abc\")", "#VALUE!")]
        [InlineData("FIND(\"*\",\"a*b\")", "2")]
        [InlineData("SEARCH(\"BASE\",\"database\")", "5")]
        [InlineData("SEARCH(\"b?d\",\"xxBADyy\")", "3")]
        [InlineData("SEARCH(\"b*d\",\"xxB123Dyy\")", "3")]
        [InlineData("SEARCH(\"*b*d\",\"xxB123Dyy\")", "1")]
        [InlineData("SEARCH(\"~*\",\"xx*yy\")", "3")]
        [InlineData("SEARCH(\"~?\",\"xx?yy\")", "3")]
        [InlineData("SEARCH(\"~~\",\"xx~yy\")", "3")]
        [InlineData("SEARCH(\"~x\",\"a~x\")", "2")]
        [InlineData("SEARCH(\"a\",\"banana\",4)", "4")]
        [InlineData("SEARCH(NA(),\"a\")", "#N/A")]
        [InlineData("FIND({\"a\",\"b\"},{\"abc\";\"bca\"})", "1,2;3,1")]
        [InlineData("EXACT(\"Same\",\"Same\")", "TRUE")]
        [InlineData("EXACT(\"Same\",\"same\")", "FALSE")]
        [InlineData("EXACT(1,\"1\")", "TRUE")]
        [InlineData("EXACT(A1,\"\")", "TRUE")]
        [InlineData("EXACT({\"a\";\"b\"},{\"a\",\"b\"})", "TRUE,FALSE;FALSE,TRUE")]
        [InlineData("REPT(\"ab\",3)", "ababab")]
        [InlineData("REPT(\"ab\",2.9)", "abab")]
        [InlineData("REPT(\"ab\",0)", "")]
        [InlineData("REPT(\"ab\",-1)", "#VALUE!")]
        [InlineData("REPT(\"\",2147483647)", "")]
        [InlineData("REPT(\"x\",32768)", "#VALUE!")]
        [InlineData("REPLACE(\"abcdefghijk\",6,5,\"*\")", "abcde*k")]
        [InlineData("REPLACE(\"2009\",3,2,\"10\")", "2010")]
        [InlineData("REPLACE(\"abc\",2,0,\"XYZ\")", "aXYZbc")]
        [InlineData("REPLACE(\"abc\",99,3,\"!\")", "abc!")]
        [InlineData("REPLACE(\"abc\",2,2147483647,\"!\")", "a!")]
        [InlineData("REPLACE(\"abc\",0,1,\"!\")", "#VALUE!")]
        [InlineData("REPLACE(\"abc\",1,-1,\"!\")", "#VALUE!")]
        [InlineData("SUBSTITUTE(\"a-b-a\",\"a\",\"x\")", "x-b-x")]
        [InlineData("SUBSTITUTE(\"a-b-a\",\"a\",\"x\",2)", "a-b-x")]
        [InlineData("SUBSTITUTE(\"a-b-a\",\"a\",\"x\",3)", "a-b-a")]
        [InlineData("SUBSTITUTE(\"a-b-a\",\"a\",\"x\",0)", "#VALUE!")]
        [InlineData("SUBSTITUTE(\"aaaaa\",\"aa\",\"b\")", "bba")]
        [InlineData("SUBSTITUTE(\"abc\",\"\",\"x\")", "abc")]
        [InlineData("SUBSTITUTE(\"AbA\",\"a\",\"x\")", "AbA")]
        [InlineData("PROPER(\"THIS is a TITLE\")", "This Is A Title")]
        [InlineData("PROPER(\"2-way o'NEILL\")", "2-Way O'Neill")]
        [InlineData("CLEAN(\"a\"&UNICHAR(9)&\"b\"&UNICHAR(10))", "ab")]
        [InlineData("UNICODE(\"A\")", "65")]
        [InlineData("UNICODE(\"😀abc\")", "128512")]
        [InlineData("UNICODE(\"\")", "#VALUE!")]
        [InlineData("UNICHAR(65)", "A")]
        [InlineData("UNICHAR(128512)", "😀")]
        [InlineData("UNICHAR(0)", "#VALUE!")]
        [InlineData("UNICHAR(55296)", "#N/A")]
        [InlineData("UNICHAR(1114112)", "#VALUE!")]
        [InlineData("TEXTBEFORE(\"a-b-c\",\"-\")", "a")]
        [InlineData("TEXTAFTER(\"a-b-c\",\"-\")", "b-c")]
        [InlineData("TEXTBEFORE(\"a-b-c\",\"-\",2)", "a-b")]
        [InlineData("TEXTAFTER(\"a-b-c\",\"-\",2)", "c")]
        [InlineData("TEXTBEFORE(\"a-b-c\",\"-\",-1)", "a-b")]
        [InlineData("TEXTAFTER(\"a-b-c\",\"-\",-2)", "b-c")]
        [InlineData("TEXTBEFORE(\"abc\",\"B\",,1)", "a")]
        [InlineData("TEXTAFTER(\"abc\",\"B\",,1)", "c")]
        [InlineData("TEXTBEFORE(\"abc\",\"B\")", "#N/A")]
        [InlineData("TEXTBEFORE(\"abc\",\"-\",,,1)", "abc")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",,,1)", "")]
        [InlineData("TEXTBEFORE(\"abc\",\"-\",-1,,1)", "")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",-1,,1)", "abc")]
        [InlineData("TEXTBEFORE(\"a-b\",\"-\",2,,1)", "a-b")]
        [InlineData("TEXTBEFORE(\"a-b\",\"-\",3,,1)", "#N/A")]
        [InlineData("TEXTBEFORE(\"abc\",\"\")", "")]
        [InlineData("TEXTBEFORE(\"abc\",\"\",-1)", "abc")]
        [InlineData("TEXTAFTER(\"abc\",\"\")", "abc")]
        [InlineData("TEXTAFTER(\"abc\",\"\",-1)", "")]
        [InlineData("TEXTBEFORE(\"abc\",\"-\",,,,\"missing\")", "missing")]
        [InlineData("TEXTBEFORE(\"a-b\",\"-\",,,,NA())", "a")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",,,,NA())", "#N/A")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",0)", "#VALUE!")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",4)", "#VALUE!")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",,2)", "#VALUE!")]
        [InlineData("TEXTAFTER(\"abc\",\"-\",,,2)", "#VALUE!")]
        [InlineData("TEXTAFTER({\"a-b\";\"c-d\"},\"-\")", "b;d")]
        [InlineData("MID(\"abcd\",2,2147483647)", "bcd")]
        public void Text_Functions_Agree_In_Compiled_Interpreted_And_Formatted_Execution(string formula, string expected)
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
        [InlineData("LEN(\"A😀B\")", "4", "3")]
        [InlineData("LEN(\"😀️\")", "3", "2")]
        [InlineData("LEN(\"é\")", "2", "2")]
        [InlineData("LEFT(\"😀X\",2)", "😀", "😀X")]
        [InlineData("RIGHT(\"X😀\",2)", "😀", "X😀")]
        [InlineData("MID(\"A😀B\",2,2)", "😀", "😀B")]
        [InlineData("FIND(\"B\",\"A😀B\")", "4", "3")]
        [InlineData("SEARCH(\"?B\",\"A😀B\")", "3", "2")]
        [InlineData("REPLACE(\"A😀B\",2,2,\"x\")", "AxB", "Ax")]
        public void Compatibility_Version_Changes_Character_Indices_Not_Grapheme_Clusters(string formula, string legacy, string modern)
        {
            var context = Context();
            var evaluator = new FormulaEvaluator();
            var expression = Parse(formula);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                context.Workbook.Settings.TextCompatibilityVersion = FormulaTextCompatibilityVersion.Version1;
                Assert.Equal(legacy, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
                context.Workbook.Settings.TextCompatibilityVersion = FormulaTextCompatibilityVersion.Version2;
                Assert.Equal(modern, Describe(evaluator.Evaluate(expression, context, new WorkbookValueResolver())));
            }
        }

        [Fact]
        public void Unicode_Settings_And_Nonfinite_Arguments_Are_Validated()
        {
            var context = Context();
            Assert.Equal(FormulaTextCompatibilityVersion.Version1, context.Workbook.Settings.TextCompatibilityVersion);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Workbook.Settings.TextCompatibilityVersion = (FormulaTextCompatibilityVersion)0);
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert.Equal(FormulaErrorType.Value, Invoke(context, "REPT", FormulaValue.FromText("x"), FormulaValue.FromNumber(value)).AsError().Type);
            Assert.Equal(FormulaErrorType.Value, Invoke(context, "UNICODE", FormulaValue.FromText("\ud800")).AsError().Type);
        }

        [Fact]
        public void Clean_Removes_Only_The_First_32_Ascii_Control_Characters()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < 256; i++) builder.Append((char)i);
            Assert.Equal(builder.ToString(32, 224), Invoke(Context(), "CLEAN", FormulaValue.FromText(builder.ToString())).AsText());
        }

        [Fact]
        public void Proper_Uses_Workbook_Casing_And_Does_Not_Preserve_All_Caps_Acronyms()
        {
            var context = Context();
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("İstanbul İzmir", Invoke(context, "PROPER", FormulaValue.FromText("istanbul izmir")).AsText());
            Assert.Equal("Nasa", Invoke(context, "PROPER", FormulaValue.FromText("NASA")).AsText());
        }

        [Fact]
        public void Text_Expansion_Checks_Length_Before_Allocating_The_Output()
        {
            var context = Context();
            var input = FormulaValue.FromText(new string('a', 32767));
            Assert.Equal(32767, Invoke(context, "REPT", FormulaValue.FromText("x"), FormulaValue.FromNumber(32767)).AsText().Length);
            foreach (var name in new[] { "REPT", "SUBSTITUTE", "REPLACE" })
            {
                FormulaValue[] args = name switch
                {
                    "REPT" => new[] { input, FormulaValue.FromNumber(2) },
                    "SUBSTITUTE" => new[] { input, FormulaValue.FromText("a"), FormulaValue.FromText("aa") },
                    _ => new[] { input, FormulaValue.FromNumber(1), FormulaValue.FromNumber(0), FormulaValue.FromText("x") }
                };
                Assert.Equal(FormulaErrorType.Value, Invoke(context, name, args).AsError().Type);
            }
            Assert.Equal("", Invoke(context, "SUBSTITUTE", input, FormulaValue.FromText("a"), FormulaValue.FromText("")).AsText());
        }

        [Fact]
        public void Array_Output_Broadcasting_Is_Bounded_And_Does_Not_Mutate_Sparse_Inputs()
        {
            var context = Context();
            var left = new FormulaArray(2, 1, sparse: true);
            left[0, 0] = FormulaValue.FromText("a");
            var right = new FormulaArray(1, 2);
            right[0, 0] = FormulaValue.FromText("a");
            right[0, 1] = FormulaValue.FromText("");
            Assert.Equal("TRUE,FALSE;FALSE,TRUE", Describe(Invoke(context, "EXACT", FormulaValue.FromArray(left), FormulaValue.FromArray(right))));
            Assert.False(left.IsPresent(1, 0));
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "EXACT", FormulaValue.FromArray(left), FormulaValue.FromArray(right)).AsError().Type);
        }

        [Fact]
        public void Seeded_Substitution_And_Slicing_Agree_With_Independent_Reference_Implementations()
        {
            var random = new Random(71903);
            var context = Context();
            for (var iteration = 0; iteration < 500; iteration++)
            {
                var chars = new char[random.Next(0, 40)];
                for (var i = 0; i < chars.Length; i++) chars[i] = "abAB"[random.Next(4)];
                var text = new string(chars);
                var oldText = iteration % 2 == 0 ? "ab" : "a";
                var newText = iteration % 3 == 0 ? "" : "xyz";
                var expected = text.Replace(oldText, newText, StringComparison.Ordinal);
                Assert.Equal(expected, Invoke(context, "SUBSTITUTE", FormulaValue.FromText(text), FormulaValue.FromText(oldText), FormulaValue.FromText(newText)).AsText());
                var start = random.Next(1, 50);
                var count = random.Next(0, 50);
                var index = Math.Min(start - 1, text.Length);
                expected = text.Remove(index, Math.Min(count, text.Length - index)).Insert(index, newText);
                Assert.Equal(expected, Invoke(context, "REPLACE", FormulaValue.FromText(text), FormulaValue.FromNumber(start), FormulaValue.FromNumber(count), FormulaValue.FromText(newText)).AsText());
            }
        }

        [Fact]
        public void Search_Agrees_With_Independent_Backtracking_Oracle_On_Exhaustive_Small_Inputs()
        {
            var context = Context();
            var texts = Strings("ab*?~", 3);
            var patterns = Strings("aB*?~", 4);
            var args = new[] { FormulaValue.Blank, FormulaValue.Blank, FormulaValue.FromNumber(1) };
            foreach (var text in texts)
            {
                args[1] = FormulaValue.FromText(text);
                foreach (var pattern in patterns)
                {
                    args[0] = FormulaValue.FromText(pattern);
                    for (var start = 0; start < Math.Max(1, text.Length); start++)
                    {
                        args[2] = FormulaValue.FromNumber(start + 1);
                        var expected = -1;
                        for (var candidate = start; candidate <= text.Length; candidate++)
                            if (MatchesPrefix(text, candidate, pattern, 0)) { expected = candidate + 1; break; }
                        var result = Invoke(context, "SEARCH", args);
                        if (expected < 0) Assert.Equal(FormulaErrorType.Value, result.AsError().Type);
                        else Assert.Equal(expected, result.AsNumber());
                    }
                }
            }
        }

        [Fact]
        public void Long_Wildcard_Sequences_Do_Not_Recurse_Or_Retain_Pooled_Buffers()
        {
            var context = Context();
            var pattern = new StringBuilder();
            for (var i = 0; i < 256; i++) pattern.Append("a*");
            pattern.Append('b');
            var text = new string('a', 32766);
            Parallel.For(0, 32, i =>
            {
                var value = Invoke(context, "SEARCH", FormulaValue.FromText(pattern.ToString()), FormulaValue.FromText(i % 2 == 0 ? text : text + "b"));
                if (i % 2 == 0) Assert.Equal(FormulaErrorType.Value, value.AsError().Type);
                else Assert.Equal(1, value.AsNumber());
            });
        }

        private static bool MatchesPrefix(string text, int index, string pattern, int next)
        {
            if (next == pattern.Length) return true;
            var ch = pattern[next++];
            if (ch == '*')
            {
                for (var i = index; i <= text.Length; i++) if (MatchesPrefix(text, i, pattern, next)) return true;
                return false;
            }
            var escaped = ch == '~' && next < pattern.Length && "*?~".IndexOf(pattern[next]) >= 0;
            if (escaped) ch = pattern[next++];
            return index < text.Length && ((!escaped && ch == '?') || char.ToUpperInvariant(ch) == char.ToUpperInvariant(text[index]))
                && MatchesPrefix(text, index + 1, pattern, next);
        }

        private static List<string> Strings(string alphabet, int maximum)
        {
            var values = new List<string> { "" };
            var start = 0;
            for (var length = 1; length <= maximum; length++)
            {
                var end = values.Count;
                for (var i = start; i < end; i++) foreach (var ch in alphabet) values.Add(values[i] + ch);
                start = end;
            }
            return values;
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
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
            return value.Kind == FormulaValueKind.Text ? value.AsText()
                : value.Kind == FormulaValueKind.Number ? value.AsNumber().ToString("G17", CultureInfo.InvariantCulture) : value.ToString();
        }
    }
}
