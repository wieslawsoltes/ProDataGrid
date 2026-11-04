// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelLookupIndexRegressionTests
    {
        [Theory]
        [InlineData("XMATCH(6,{5,1,7,3},-1)", 1d)]
        [InlineData("XMATCH(6,{9,7,11,8},1)", 2d)]
        [InlineData("XMATCH(6,{5,1,5,7},-1,-1)", 3d)]
        [InlineData("XMATCH(6,{5,1,5,7},-1,1)", 1d)]
        [InlineData("XMATCH(5,{5,4,1},-1)", 1d)]
        [InlineData("XMATCH(5,{5,6,9},1)", 1d)]
        [InlineData("XMATCH(5,{5,6,5},1,-1)", 3d)]
        [InlineData("XMATCH(6,{1,3,5,7,9},-1,2)", 3d)]
        [InlineData("XMATCH(6,{1,3,5,7,9},1,2)", 4d)]
        [InlineData("XMATCH(7,{1,3,5,7,9},0,2)", 4d)]
        [InlineData("XMATCH(6,{9,7,5,3,1},-1,-2)", 3d)]
        [InlineData("XMATCH(6,{9,7,5,3,1},1,-2)", 2d)]
        [InlineData("XMATCH(7,{9,7,5,3,1},0,-2)", 2d)]
        [InlineData("XMATCH(0,{1,3,5},1,2)", 1d)]
        [InlineData("XMATCH(9,{1,3,5},-1,2)", 3d)]
        [InlineData("XMATCH(0,{5,3,1},1,-2)", 3d)]
        [InlineData("XMATCH(9,{5,3,1},-1,-2)", 1d)]
        [InlineData("XMATCH(5,{1,5,5,9},0,2)", 2d)]
        [InlineData("XMATCH(5,{9,5,5,1},0,-2)", 2d)]
        [InlineData("XMATCH(\"b*\",{\"ant\",\"bee\",\"bison\"},2,2)", 2d)]
        [InlineData("XMATCH(\"b*\",{\"ant\",\"bee\",\"bison\"},2,-2)", 3d)]
        [InlineData("XMATCH(\"bee\",{\"ant\",\"BEE\",\"dog\"},0,2)", 2d)]
        [InlineData("XLOOKUP(6,{5,1,7,3},{50,10,70,30},,-1)", 50d)]
        [InlineData("XLOOKUP(6,{9,7,11,8},{90,70,110,80},,1)", 70d)]
        [InlineData("XLOOKUP(6,{1,3,5,7},{10,30,50,70},,-1,2)", 50d)]
        [InlineData("XLOOKUP(6,{9,7,5,3},{90,70,50,30},,1,-2)", 70d)]
        [InlineData("XLOOKUP(0,{1,3,5},{10,30,50},99,-1,2)", 99d)]
        public void Lookup_Modes_Agree_In_Both_Evaluators(string formula, double expected)
        {
            var context = Context();
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            Assert.Equal(expected, evaluator.Evaluate(expression, context, resolver).AsNumber());
            context.Workbook.Settings.EnableCompiledExpressions = false;
            Assert.Equal(expected, evaluator.Evaluate(expression, context, resolver).AsNumber());
        }

        [Theory]
        [InlineData("XMATCH(0,{1,3,5},-1,2)")]
        [InlineData("XMATCH(9,{1,3,5},1,2)")]
        [InlineData("XMATCH(2,{1,3,5},0,2)")]
        [InlineData("XMATCH(0,{5,3,1},-1,-2)")]
        [InlineData("XMATCH(9,{5,3,1},1,-2)")]
        [InlineData("XMATCH(2,{5,3,1},0,-2)")]
        public void Binary_Out_Of_Range_And_Missing_Exact_Return_NA(string formula)
        {
            var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), Context(), new DictionaryValueResolver());
            Assert.Equal(FormulaErrorType.NA, result.AsError().Type);
        }

        [Fact]
        public void Binary_Modes_Match_Brute_Force_On_Seeded_Sorted_Vectors()
        {
            var random = new Random(8279);
            var context = Context();
            for (var iteration = 0; iteration < 200; iteration++)
            {
                var length = random.Next(1, 81);
                var horizontal = iteration % 2 == 0;
                foreach (var ascending in new[] { true, false })
                {
                    var array = new FormulaArray(horizontal ? 1 : length, horizontal ? length : 1);
                    for (var i = 0; i < length; i++)
                    {
                        array[horizontal ? 0 : i, horizontal ? i : 0] = FormulaValue.FromNumber((ascending ? i : length - i - 1) * 3);
                    }
                    var target = random.Next(-5, length * 3 + 5);
                    foreach (var mode in new[] { -1, 0, 1 })
                    {
                        var expected = -1;
                        var best = mode == -1 ? double.NegativeInfinity : double.PositiveInfinity;
                        for (var i = 0; i < length; i++)
                        {
                            var number = array[horizontal ? 0 : i, horizontal ? i : 0].AsNumber();
                            if (number == target) { expected = i; break; }
                            if ((mode == -1 && number < target && number > best) || (mode == 1 && number > target && number < best))
                            {
                                best = number;
                                expected = i;
                            }
                        }
                        var result = Invoke(context, "XMATCH", FormulaValue.FromNumber(target), FormulaValue.FromArray(array),
                            FormulaValue.FromNumber(mode), FormulaValue.FromNumber(ascending ? 2 : -2));
                        if (expected < 0) Assert.Equal(FormulaErrorType.NA, result.AsError().Type);
                        else Assert.Equal(expected + 1, result.AsNumber());
                    }
                }
            }
        }

        [Fact]
        public void Sparse_Lookup_Masks_Keep_Physical_Indices_And_Nearest_Semantics()
        {
            var array = new FormulaArray(5, 1, sparse: true);
            array[0, 0] = FormulaValue.FromNumber(2);
            array[2, 0] = FormulaValue.FromNumber(6);
            array[4, 0] = FormulaValue.FromNumber(10);
            foreach (var search in new[] { 1, -1, 2, -2 })
            {
                var result = Invoke(Context(), "XMATCH", FormulaValue.FromNumber(7), FormulaValue.FromArray(array),
                    FormulaValue.FromNumber(-1), FormulaValue.FromNumber(search));
                Assert.Equal(3, result.AsNumber());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Unique_Empty_Exactly_Once_Result_Returns_Calc(bool columns)
        {
            var array = new FormulaArray(columns ? 1 : 3, columns ? 3 : 1);
            for (var i = 0; i < 3; i++) array[columns ? 0 : i, columns ? i : 0] = FormulaValue.FromNumber(1);
            var result = Invoke(Context(), "UNIQUE", FormulaValue.FromArray(array), FormulaValue.FromBoolean(columns), FormulaValue.FromBoolean(true));
            Assert.Equal(FormulaErrorType.Calc, result.AsError().Type);
        }

        [Fact]
        public void Unique_Indexed_And_Fallback_Paths_Preserve_Legacy_Equality()
        {
            var random = new Random(43117);
            var context = Context();
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 10;
            var values = new[] { FormulaValue.Blank, FormulaValue.FromBoolean(true), FormulaValue.FromBoolean(false),
                FormulaValue.FromNumber(0), FormulaValue.FromNumber(1), FormulaValue.FromNumber(1.25),
                FormulaValue.FromText("1,25"), FormulaValue.FromText("TRUE"), FormulaValue.FromText(""),
                FormulaValue.FromText("word"), FormulaValue.FromText("WORD"),
                FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), FormulaValue.FromError(new FormulaError(FormulaErrorType.Value)) };
            for (var iteration = 0; iteration < 100; iteration++)
            {
                foreach (var columns in new[] { false, true })
                {
                    var count = random.Next(1, 45);
                    var width = random.Next(1, 4);
                    var array = new FormulaArray(columns ? width : count, columns ? count : width, sparse: iteration % 3 == 0);
                    for (var row = 0; row < array.RowCount; row++)
                    {
                        for (var column = 0; column < array.ColumnCount; column++)
                        {
                            var value = iteration % 2 == 0 ? values[random.Next(values.Length)] : FormulaValue.FromNumber(random.Next(5));
                            array.SetValue(row, column, value, !array.HasMask || random.Next(4) != 0);
                        }
                    }
                    foreach (var once in new[] { false, true })
                    {
                        var representatives = new List<int>();
                        var counts = new List<int>();
                        for (var i = 0; i < count; i++)
                        {
                            var found = -1;
                            for (var j = 0; j < representatives.Count; j++)
                            {
                                var equal = true;
                                for (var offset = 0; offset < width; offset++)
                                {
                                    if (!Equal(Read(array, columns, i, offset), Read(array, columns, representatives[j], offset), context.Workbook.Settings)) { equal = false; break; }
                                }
                                if (equal) { found = j; break; }
                            }
                            if (found >= 0) counts[found]++;
                            else { representatives.Add(i); counts.Add(1); }
                        }
                        var expected = new List<int>();
                        for (var i = 0; i < counts.Count; i++) if (!once || counts[i] == 1) expected.Add(representatives[i]);
                        var result = Invoke(context, "UNIQUE", FormulaValue.FromArray(array), FormulaValue.FromBoolean(columns), FormulaValue.FromBoolean(once));
                        if (expected.Count == 0) { Assert.Equal(FormulaErrorType.Calc, result.AsError().Type); continue; }
                        var actual = result.AsArray();
                        Assert.Equal(expected.Count, columns ? actual.ColumnCount : actual.RowCount);
                        for (var i = 0; i < expected.Count; i++)
                            for (var offset = 0; offset < width; offset++)
                                Assert.Equal(Read(array, columns, expected[i], offset), Read(actual, columns, i, offset));
                    }
                }
            }
        }

        [Fact]
        public void Unique_Handles_One_Hundred_Thousand_Distinct_Rows()
        {
            var array = new FormulaArray(100000, 1);
            for (var row = 0; row < array.RowCount; row++) array[row, 0] = FormulaValue.FromNumber(row);
            var result = Invoke(Context(), "UNIQUE", FormulaValue.FromArray(array)).AsArray();
            Assert.Equal(100000, result.RowCount);
            Assert.Equal(0, result[0, 0].AsNumber());
            Assert.Equal(99999, result[99999, 0].AsNumber());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Sort_Preserves_Large_Tie_Groups(bool columns)
        {
            var array = new FormulaArray(columns ? 2 : 200, columns ? 200 : 2);
            for (var index = 0; index < 200; index++)
            {
                array[columns ? 0 : index, columns ? index : 0] = FormulaValue.FromNumber(1);
                array[columns ? 1 : index, columns ? index : 1] = FormulaValue.FromNumber(index);
            }
            var result = Invoke(Context(), "SORT", FormulaValue.FromArray(array), FormulaValue.FromNumber(1),
                FormulaValue.FromNumber(1), FormulaValue.FromBoolean(columns)).AsArray();
            for (var index = 0; index < 200; index++) Assert.Equal(index, result[columns ? 1 : index, columns ? index : 1].AsNumber());
        }

        private static FormulaValue Read(FormulaArray array, bool columns, int index, int offset)
        {
            var row = columns ? offset : index;
            var column = columns ? index : offset;
            return array.HasMask && !array.IsPresent(row, column) ? FormulaValue.Blank : array[row, column];
        }

        private static bool Equal(FormulaValue left, FormulaValue right, FormulaCalculationSettings settings)
        {
            if (left.Kind == FormulaValueKind.Error || right.Kind == FormulaValueKind.Error)
                return left.Kind == right.Kind && left.AsError().Type == right.AsError().Type;
            if (FormulaCoercion.TryCoerceToNumber(left, settings, out var a, out _) && FormulaCoercion.TryCoerceToNumber(right, settings, out var b, out _))
                return a.CompareTo(b) == 0;
            return FormulaCoercion.TryCoerceToText(left, out var x, out _) && FormulaCoercion.TryCoerceToText(right, out var y, out _) &&
                string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
    }
}
