// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelBusinessCalendarTests
    {
        [Theory]
        [InlineData("WORKDAY.INTL(DATE(2012,1,1),90,11)", 41013)]
        [InlineData("WORKDAY.INTL(DATE(2012,1,1),30,17)", 40944)]
        [InlineData("NETWORKDAYS(DATE(2012,10,1),DATE(2013,3,1))", 110)]
        [InlineData("NETWORKDAYS(DATE(2012,10,1),DATE(2013,3,1),VSTACK(DATE(2012,11,22),DATE(2012,12,4),DATE(2013,1,21)))", 107)]
        [InlineData("WORKDAY(45296,1)", 45299)]
        [InlineData("WORKDAY.INTL(45296,1)", 45299)]
        [InlineData("WORKDAY.INTL(45296,-1)", 45295)]
        [InlineData("WORKDAY(45296.9,1.9)", 45299)]
        [InlineData("WORKDAY.INTL(45296.9,-1.9)", 45295)]
        [InlineData("WORKDAY.INTL(45297.8,0)", 45297)]
        [InlineData("WORKDAY.INTL(45296,1,\"0000000\")", 45297)]
        [InlineData("WORKDAY.INTL(45296,1,1,{45299;45299.5})", 45300)]
        [InlineData("WORKDAY(45296,1,{45299;45299.5})", 45300)]
        [InlineData("NETWORKDAYS.INTL(45292,45298)", 5)]
        [InlineData("NETWORKDAYS.INTL(45298,45292)", -5)]
        [InlineData("NETWORKDAYS.INTL(45292,45298,11)", 6)]
        [InlineData("NETWORKDAYS.INTL(45292,45298,\"0000000\")", 7)]
        [InlineData("NETWORKDAYS.INTL(45292,45298,\"1111111\")", 0)]
        [InlineData("NETWORKDAYS.INTL(45292,45298,\"1010110\")", 3)]
        [InlineData("NETWORKDAYS.INTL(45292,45298,,{45292,45292.8,45294,45297,45298})", 3)]
        [InlineData("NETWORKDAYS(45292,45298,{45292,45292.8,45294,45297,45298})", 3)]
        [InlineData("NETWORKDAYS(45292.9,45292.1)", 1)]
        [InlineData("NETWORKDAYS.INTL(45297,45298)", 0)]
        [InlineData("NETWORKDAYS.INTL(45292,45292)", 1)]
        [InlineData("NETWORKDAYS.INTL(45292,45292,,45292)", 0)]
        [InlineData("LAMBDA(start,weekend,WORKDAY.INTL(start,1,weekend))(45296,)", 45299)]
        [InlineData("NETWORKDAYS(6,7)", 1)]
        [InlineData("WORKDAY(5,1)", 6)]
        public void Calendar_Formulas_Agree_In_Both_Evaluators_And_Formatter_RoundTrips(string formula, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = parser.Parse(new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions()), new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                foreach (var candidate in new[] { expression, formatted })
                    Assert.Equal(expected, new FormulaEvaluator().Evaluate(candidate, context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Theory]
        [InlineData("WORKDAY.INTL(45292,1,0)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,8)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,18)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,1.5)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,\"1111111\")", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,\"000001\")", FormulaErrorType.Value)]
        [InlineData("NETWORKDAYS.INTL(45292,45293,\"00000000\")", FormulaErrorType.Value)]
        [InlineData("NETWORKDAYS.INTL(45292,45293,\"0000020\")", FormulaErrorType.Value)]
        [InlineData("WORKDAY.INTL(45292,1,A1)", FormulaErrorType.Num)]
        [InlineData("WORKDAY(45292,-2147483648)", FormulaErrorType.Num)]
        [InlineData("WORKDAY(45292,2147483647)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(2958465,1)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(0,-1)", FormulaErrorType.Num)]
        [InlineData("NETWORKDAYS.INTL(-1,45292)", FormulaErrorType.Num)]
        [InlineData("NETWORKDAYS(0,2958466)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(45292,1,,2958466)", FormulaErrorType.Num)]
        [InlineData("NETWORKDAYS.INTL(45292,45293,,-1)", FormulaErrorType.Num)]
        [InlineData("WORKDAY.INTL(NA(),1)", FormulaErrorType.NA)]
        [InlineData("WORKDAY.INTL(45292,NA())", FormulaErrorType.NA)]
        [InlineData("WORKDAY.INTL(45292,1,NA())", FormulaErrorType.NA)]
        [InlineData("NETWORKDAYS.INTL(45292,45293,,NA())", FormulaErrorType.NA)]
        [InlineData("WORKDAY.INTL(\"not a date\",1)", FormulaErrorType.Value)]
        public void Invalid_Dates_Offsets_And_Weekend_Inputs_Are_Bounded_Formula_Errors(string formula, FormulaErrorType expected)
        {
            var context = Context();
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal(expected, new FormulaEvaluator().Evaluate(expression, context, new WorkbookValueResolver()).AsError().Type);
            }
        }

        [Theory]
        [InlineData(FormulaDateSystem.Windows1900, 2958465)]
        [InlineData(FormulaDateSystem.Mac1904, 2957003)]
        public void Full_Calendar_Bounds_Do_Not_Require_Day_By_Day_Iteration(FormulaDateSystem system, int maximum)
        {
            var context = Context(system);
            var zero = FormulaValue.FromNumber(0);
            var max = FormulaValue.FromNumber(maximum);
            var noWeekend = FormulaValue.FromText("0000000");
            Assert.Equal(maximum + 1, Invoke(context, "NETWORKDAYS.INTL", zero, max, noWeekend).AsNumber());
            Assert.Equal(-maximum - 1, Invoke(context, "NETWORKDAYS.INTL", max, zero, noWeekend).AsNumber());
            Assert.Equal(maximum, Invoke(context, "WORKDAY.INTL", zero, max, noWeekend).AsNumber());
            Assert.Equal(0, Invoke(context, "WORKDAY.INTL", max, FormulaValue.FromNumber(-maximum), noWeekend).AsNumber());
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1e100 })
            {
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "WORKDAY", FormulaValue.FromNumber(bad), zero).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "WORKDAY", zero, FormulaValue.FromNumber(bad)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, "NETWORKDAYS", zero, FormulaValue.FromNumber(bad)).AsError().Type);
            }
        }

        [Theory]
        [InlineData(FormulaDateSystem.Windows1900)]
        [InlineData(FormulaDateSystem.Mac1904)]
        public void Every_Weekend_Mask_Agrees_With_Independent_Gregorian_Loops(FormulaDateSystem system)
        {
            var context = Context(system);
            var epoch = system == FormulaDateSystem.Windows1900 ? new DateTime(1899, 12, 30) : new DateTime(1904, 1, 1);
            var random = new Random(9192026);
            for (var mask = 0; mask < 128; mask++)
            {
                var weekendText = new char[7];
                for (var i = 0; i < 7; i++) weekendText[i] = (mask & (1 << i)) == 0 ? '0' : '1';
                var weekend = FormulaValue.FromText(new string(weekendText));
                for (var trial = 0; trial < 16; trial++)
                {
                    var start = 40000 + random.Next(1000);
                    var end = start + random.Next(-100, 101);
                    var holidays = new HashSet<int>();
                    var holidayValues = new FormulaArray(24, 1);
                    for (var i = 0; i < 24; i++)
                    {
                        var date = start + random.Next(-120, 121);
                        holidays.Add(date);
                        holidayValues[i, 0] = FormulaValue.FromNumber(date + 0.25);
                    }
                    bool Working(int serial)
                    {
                        var weekday = ((int)epoch.AddDays(serial).DayOfWeek + 6) % 7;
                        return (mask & (1 << weekday)) == 0 && !holidays.Contains(serial);
                    }
                    var count = 0;
                    for (var day = Math.Min(start, end); day <= Math.Max(start, end); day++) if (Working(day)) count++;
                    if (end < start) count = -count;
                    Assert.Equal(count, Invoke(context, "NETWORKDAYS.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(end), weekend, FormulaValue.FromArray(holidayValues)).AsNumber());
                    var days = random.Next(-30, 31);
                    var result = Invoke(context, "WORKDAY.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(days), weekend, FormulaValue.FromArray(holidayValues));
                    if (mask == 127) { Assert.Equal(FormulaErrorType.Num, result.AsError().Type); continue; }
                    var expected = start;
                    var remaining = Math.Abs(days);
                    while (remaining > 0) { expected += days > 0 ? 1 : -1; if (Working(expected)) remaining--; }
                    Assert.Equal(expected, result.AsNumber());
                }
            }
        }

        [Fact]
        public void Numeric_Weekend_Codes_Match_Their_Day_Masks_For_All_Start_Weekdays()
        {
            var masks = new[] { "0000011", "1000001", "1100000", "0110000", "0011000", "0001100", "0000110",
                "0000001", "1000000", "0100000", "0010000", "0001000", "0000100", "0000010" };
            var context = Context();
            for (var i = 0; i < masks.Length; i++) for (var start = 45292; start < 45299; start++)
            {
                var code = FormulaValue.FromNumber(i < 7 ? i + 1 : i + 4);
                var mask = FormulaValue.FromText(masks[i]);
                foreach (var days in new[] { -20, -1, 0, 1, 20 })
                    Assert.Equal(Invoke(context, "WORKDAY.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(days), mask),
                        Invoke(context, "WORKDAY.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(days), code));
                Assert.Equal(Invoke(context, "NETWORKDAYS.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(start + 20), mask),
                    Invoke(context, "NETWORKDAYS.INTL", FormulaValue.FromNumber(start), FormulaValue.FromNumber(start + 20), code));
            }
        }

        [Fact]
        public void Array_Controls_Broadcast_Without_Broadcasting_Holiday_Lists()
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var result = new FormulaEvaluator().Evaluate(parser.Parse("WORKDAY.INTL({45292;45293},{1,2})", new FormulaParseOptions()), context, new WorkbookValueResolver()).AsArray();
            Assert.Equal(2, result.RowCount); Assert.Equal(2, result.ColumnCount);
            Assert.Equal(45293, result[0, 0].AsNumber()); Assert.Equal(45294, result[0, 1].AsNumber());
            Assert.Equal(45294, result[1, 0].AsNumber()); Assert.Equal(45295, result[1, 1].AsNumber());
            var errors = new FormulaEvaluator().Evaluate(parser.Parse("WORKDAY.INTL({45292;-1},1)", new FormulaParseOptions()), context, new WorkbookValueResolver()).AsArray();
            Assert.Equal(45293, errors[0, 0].AsNumber()); Assert.Equal(FormulaErrorType.Num, errors[1, 0].AsError().Type);
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, new FormulaEvaluator().Evaluate(parser.Parse("WORKDAY.INTL({45292;45293},{1,2})", new FormulaParseOptions()), context, new WorkbookValueResolver()).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, "WORKDAY", FormulaValue.FromNumber(45292), FormulaValue.FromNumber(1), FormulaValue.FromArray(new FormulaArray(4, 1))).AsError().Type);
        }

        [Fact]
        public void Holiday_Workspaces_Are_Private_Mask_Aware_And_Released_After_Errors()
        {
            var context = Context();
            var dates = new FormulaArray(1024, 1, sparse: true);
            for (var i = 0; i < dates.RowCount; i++) dates[i, 0] = FormulaValue.FromNumber(45292);
            dates.SetValue(20, 0, FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), false);
            var args = new[] { FormulaValue.FromNumber(45292), FormulaValue.FromNumber(45298), FormulaValue.FromNumber(1), FormulaValue.FromArray(dates) };
            Parallel.For(0, 64, i => Assert.Equal(4, Invoke(context, "NETWORKDAYS.INTL", args).AsNumber()));
            Assert.Equal(45292, dates[0, 0].AsNumber()); Assert.False(dates.IsPresent(20, 0));
            dates[1023, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            Assert.Equal(FormulaErrorType.NA, Invoke(context, "NETWORKDAYS.INTL", args).AsError().Type);
            dates[1023, 0] = FormulaValue.FromNumber(45294);
            Assert.Equal(3, Invoke(context, "NETWORKDAYS.INTL", args).AsNumber());
        }

        [Fact]
        public async Task Warm_Scalar_Calendar_Calls_Do_Not_Allocate_A_Day_List_Or_Holiday_Set()
        {
            var context = Context();
            var call = new FormulaFunctionContext(context);
            Assert.True(context.FunctionRegistry.TryGetFunction("NETWORKDAYS.INTL", out var function));
            var args = new[] { FormulaValue.FromNumber(0), FormulaValue.FromNumber(2958465), FormulaValue.FromText("0000000") };
            foreach (var sample in await FormulaAllocationMeasurements.RunAsync(() => function.Invoke(call, args).AsNumber(), 1000))
            {
                Assert.Equal(2958466000d, sample.Checksum);
                Assert.True(sample.Bytes < 4096, $"Calendar allocated {sample.Bytes} bytes.");
            }
        }

        [Fact]
        public void Calendar_Spills_Recalculate_When_A_Holiday_Changes()
        {
            var workbook = new TestWorkbook("Book1");
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(45299);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "WORKDAY.INTL(45296,{1;2},,A1)");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 3) });
            Assert.Equal(45300, sheet.GetCell(1, 3).Value.AsArray()[0, 0].AsNumber());
            Assert.Equal(45301, sheet.GetCell(2, 3).Value.AsNumber());
            sheet.GetCell(1, 1).Value = FormulaValue.Blank;
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            Assert.Equal(45299, sheet.GetCell(1, 3).Value.AsArray()[0, 0].AsNumber());
            Assert.Equal(45300, sheet.GetCell(2, 3).Value.AsNumber());
        }

        private static FormulaValue Invoke(FormulaEvaluationContext context, string name, params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction(name, out var function));
            return function.Invoke(new FormulaFunctionContext(context), args);
        }
        private static FormulaEvaluationContext Context(FormulaDateSystem system = FormulaDateSystem.Windows1900)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            workbook.Settings.DateSystem = system;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }
    }
}
