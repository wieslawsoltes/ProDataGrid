// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelBusinessCalendarMath
    {
        public static int MaximumDate(FormulaDateSystem system) => system == FormulaDateSystem.Windows1900 ? 2958465 : 2957003;

        // Monday = 0. Keep Excel's uninterrupted serial weekday cycle, including serial
        // 60 (the fictitious 1900 leap day), instead of mixing it with Gregorian dates.
        public static int Weekday(int serial, FormulaDateSystem system)
            => (serial + (system == FormulaDateSystem.Windows1900 ? 5 : 4)) % 7;

        public static bool TryDate(FormulaValue value, FormulaCalculationSettings settings, out int day, out FormulaError error)
        {
            day = 0;
            if (value.Kind is FormulaValueKind.Array or FormulaValueKind.Reference or FormulaValueKind.Lambda)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
            if (!ExcelDateFunctionHelpers.TryGetDateSerial(value, settings, out var number, out error)) return false;
            number = Math.Truncate(number);
            if (!double.IsFinite(number) || number < 0 || number > MaximumDate(settings.DateSystem))
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            day = (int)number;
            return true;
        }

        public static bool TryOffset(FormulaValue value, FormulaCalculationSettings settings, out int days, out FormulaError error)
        {
            days = 0;
            if (!FormulaCoercion.TryCoerceToNumber(value, settings, out var number, out error)) return false;
            number = Math.Truncate(number);
            // A larger offset cannot fit in either supported calendar even with no weekends.
            // Validate before integer conversion or Abs, including Int32.MinValue and infinities.
            if (!double.IsFinite(number) || Math.Abs(number) > MaximumDate(settings.DateSystem))
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            days = (int)number;
            return true;
        }

        public static bool TryWeekend(FormulaValue value, FormulaCalculationSettings settings, out int mask, out FormulaError error)
        {
            mask = 0;
            error = default;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            if (value.Kind == FormulaValueKind.Text)
            {
                var text = value.AsText();
                if (text.Length != 7) { error = new FormulaError(FormulaErrorType.Value); return false; }
                for (var i = 0; i < 7; i++)
                {
                    if (text[i] != '0' && text[i] != '1') { error = new FormulaError(FormulaErrorType.Value); return false; }
                    if (text[i] == '1') mask |= 1 << i;
                }
                return true;
            }
            if (!FormulaCoercion.TryCoerceToNumber(value, settings, out var code, out error)) return false;
            if (double.IsFinite(code) && code == Math.Truncate(code))
            {
                if (code >= 1 && code <= 7)
                {
                    var first = ((int)code + 4) % 7;
                    mask = (1 << first) | (1 << ((first + 1) % 7));
                    return true;
                }
                if (code >= 11 && code <= 17)
                {
                    mask = 1 << (((int)code - 5) % 7);
                    return true;
                }
            }
            error = new FormulaError(FormulaErrorType.Num);
            return false;
        }

        public static int Count(int start, int end, int weekend, FormulaDateSystem system, in ExcelHolidayIndex holidays)
        {
            if (end < start || weekend == 127) return 0;
            var length = end - start + 1;
            var weekdays = 7 - BitOperations.PopCount((uint)weekend);
            var count = length / 7 * weekdays;
            var first = Weekday(start, system);
            for (var i = 0; i < length % 7; i++)
                if ((weekend & (1 << ((first + i) % 7))) == 0) count++;
            return count - holidays.CountWorkingHolidays(start, end, weekend);
        }

        public static bool TryOffsetDate(int start, int days, int weekend, FormulaDateSystem system,
            in ExcelHolidayIndex holidays, out int result)
        {
            result = start;
            if (days == 0) return true;
            var forward = days > 0;
            var wanted = Math.Abs(days);
            var lower = forward ? start + 1 : 0;
            var upper = forward ? MaximumDate(system) : start - 1;
            if (Count(lower, upper, weekend, system, holidays) < wanted) return false;
            // At most 22 bisections across the entire supported date domain. Counting uses
            // full weeks and a bounded remainder, with binary-search holiday ranks.
            while (lower < upper)
            {
                if (forward)
                {
                    var middle = lower + (upper - lower) / 2;
                    if (Count(start + 1, middle, weekend, system, holidays) >= wanted) upper = middle;
                    else lower = middle + 1;
                }
                else
                {
                    var middle = lower + (upper - lower + 1) / 2;
                    if (Count(middle, start - 1, weekend, system, holidays) >= wanted) lower = middle;
                    else upper = middle - 1;
                }
            }
            result = lower;
            return true;
        }
    }
}
