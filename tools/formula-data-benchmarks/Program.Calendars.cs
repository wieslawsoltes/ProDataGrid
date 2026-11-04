// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    static partial void ValidateAdditional(FormulaEvaluationContext context)
    {
        var parser = new ExcelFormulaParser();
        var evaluator = new FormulaEvaluator();
        var resolver = new EmptyResolver();
        var assertions = 0;
        foreach (var compiled in new[] { true, false })
        {
            context.Workbook.Settings.EnableCompiledExpressions = compiled;
            foreach (var item in new (string Formula, double Result)[] {
                ("WORKDAY.INTL(DATE(2012,1,1),90,11)", 41013),
                ("WORKDAY.INTL(DATE(2012,1,1),30,17)", 40944),
                ("NETWORKDAYS(DATE(2012,10,1),DATE(2013,3,1))", 110),
                ("WORKDAY(45296.9,1.9)", 45299),
                ("WORKDAY.INTL(45296,1,,{45299,45299})", 45300),
                ("WORKDAY.INTL(45296,-1)", 45295),
                ("WORKDAY.INTL(45297,0)", 45297),
                ("WORKDAY.INTL(45296,1,\"0000000\")", 45297),
                ("NETWORKDAYS.INTL(45292,45298,,{45292,45292.5,45294,45297})", 3),
                ("NETWORKDAYS.INTL(45298,45292)", -5),
                ("NETWORKDAYS.INTL(45292,45298,\"1111111\")", 0),
                ("NETWORKDAYS.INTL(45292,45298,\"0000000\")", 7),
                ("NETWORKDAYS.INTL(45292,45298,\"1010110\")", 3),
                ("SUM(WORKDAY.INTL({45292;45293},{1,2}))", 181176) })
            {
                var value = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (value.Kind != FormulaValueKind.Number || value.AsNumber() != item.Result)
                    throw new InvalidOperationException("Calendar smoke mismatch: " + item.Formula + " => " + value);
                assertions++;
            }
            foreach (var item in new (string Formula, FormulaErrorType Error)[] {
                ("WORKDAY(45292,-2147483648)", FormulaErrorType.Num),
                ("WORKDAY.INTL(2958465,1)", FormulaErrorType.Num),
                ("WORKDAY.INTL(0,-1)", FormulaErrorType.Num),
                ("WORKDAY.INTL(45292,1,0)", FormulaErrorType.Num),
                ("WORKDAY.INTL(45292,1,\"1111111\")", FormulaErrorType.Num),
                ("WORKDAY.INTL(45292,1,\"0000020\")", FormulaErrorType.Value),
                ("NETWORKDAYS.INTL(45292,45293,,NA())", FormulaErrorType.NA) })
            {
                var result = evaluator.Evaluate(parser.Parse(item.Formula, new FormulaParseOptions()), context, resolver);
                if (result.Kind != FormulaValueKind.Error || result.AsError().Type != item.Error)
                    throw new InvalidOperationException("Calendar error smoke mismatch: " + item.Formula);
                assertions++;
            }
        }
        foreach (var system in new[] { FormulaDateSystem.Windows1900, FormulaDateSystem.Mac1904 })
        {
            context.Workbook.Settings.DateSystem = system;
            var maximum = system == FormulaDateSystem.Windows1900 ? 2958465 : 2957003;
            var call = new FormulaFunctionContext(context);
            context.FunctionRegistry.TryGetFunction("WORKDAY.INTL", out var workday);
            context.FunctionRegistry.TryGetFunction("NETWORKDAYS.INTL", out var network);
            var noWeekend = FormulaValue.FromText("0000000");
            if (network.Invoke(call, new[] { FormulaValue.FromNumber(0), FormulaValue.FromNumber(maximum), noWeekend }).AsNumber() != maximum + 1)
                throw new InvalidOperationException("Full calendar count mismatch.");
            if (workday.Invoke(call, new[] { FormulaValue.FromNumber(0), FormulaValue.FromNumber(maximum), noWeekend }).AsNumber() != maximum)
                throw new InvalidOperationException("Full calendar forward offset mismatch.");
            if (workday.Invoke(call, new[] { FormulaValue.FromNumber(maximum), FormulaValue.FromNumber(-maximum), noWeekend }).AsNumber() != 0)
                throw new InvalidOperationException("Full calendar backward offset mismatch.");
            assertions += 3;
        }
        context.Workbook.Settings.DateSystem = FormulaDateSystem.Windows1900;
        context.Workbook.Settings.EnableCompiledExpressions = true;
        Console.WriteLine("Business calendar smoke assertions=" + assertions);
    }

    static partial void MeasureAdditional(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        if (!context.FunctionRegistry.TryGetFunction("WORKDAY", out var workday) ||
            !context.FunctionRegistry.TryGetFunction("NETWORKDAYS", out var network)) throw new InvalidOperationException("Missing calendar functions.");
        var call = new FormulaFunctionContext(context);
        var holidays = new FormulaArray(128, 1);
        for (var i = 0; i < 128; i++) holidays[i, 0] = FormulaValue.FromNumber(41000 + i * 37);
        var holidayValue = FormulaValue.FromArray(holidays);
        var countArgs = new[] { FormulaValue.FromNumber(40000), FormulaValue.FromNumber(1440000), holidayValue };
        var offsetArgs = new[] { FormulaValue.FromNumber(40000), FormulaValue.FromNumber(1000000), holidayValue };
        var expectedCount = DayLoopNetwork(40000, 1440000, holidays);
        var expectedDate = DayLoopOffset(40000, 1000000, holidays);
        if (network.Invoke(call, countArgs).AsNumber() != expectedCount || workday.Invoke(call, offsetArgs).AsNumber() != expectedDate)
            throw new InvalidOperationException("Calendar long-range reference mismatch.");
        for (var order = 0; order < 2; order++)
        {
            var optimized = reverse ? order == 1 : order == 0;
            Measure(output, optimized ? "networkdays_1400000_day_span" : "networkdays_day_loop_reference", 3,
                () => optimized ? network.Invoke(call, countArgs).AsNumber() : DayLoopNetwork(40000, 1440000, holidays), expectedCount);
            Measure(output, optimized ? "workday_1000000_day_offset" : "workday_day_loop_reference", 3,
                () => optimized ? workday.Invoke(call, offsetArgs).AsNumber() : DayLoopOffset(40000, 1000000, holidays), expectedDate);
        }
        var shortArgs = new[] { FormulaValue.FromNumber(45292), FormulaValue.FromNumber(45298) };
        Measure(output, "networkdays_one_week_no_holidays", 10000, () => network.Invoke(call, shortArgs).AsNumber(), 5);
        var shortOffset = new[] { FormulaValue.FromNumber(45296), FormulaValue.FromNumber(1) };
        Measure(output, "workday_one_day_no_holidays", 10000, () => workday.Invoke(call, shortOffset).AsNumber(), 45299);
    }

    // Straightforward prior-style day iteration for modern serial dates. Include building
    // the holiday HashSet inside each measured call, as the previous implementation did.
    // This is an algorithm reference, not a linked prior-release assembly.
    private static HashSet<int> ReferenceHolidays(FormulaArray values)
    {
        var result = new HashSet<int>();
        for (var row = 0; row < values.RowCount; row++) result.Add((int)values[row, 0].AsNumber());
        return result;
    }
    private static bool ReferenceWorking(int serial, HashSet<int> holidays)
        => (serial - 2) % 7 < 5 && !holidays.Contains(serial);
    private static int DayLoopNetwork(int start, int end, FormulaArray holidays)
    {
        var excluded = ReferenceHolidays(holidays);
        var count = 0;
        for (var day = start; day <= end; day++) if (ReferenceWorking(day, excluded)) count++;
        return count;
    }
    private static int DayLoopOffset(int start, int count, FormulaArray holidays)
    {
        var excluded = ReferenceHolidays(holidays);
        while (count > 0) { start++; if (ReferenceWorking(start, excluded)) count--; }
        return start;
    }
}
