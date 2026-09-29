// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal ref struct ExcelHolidayIndex
    {
        // Supported serials fit in 22 bits. Sorting (weekday, serial) keys groups dates
        // into seven ordered ranges, so counting never scans every holiday per bisection.
        private const int WeekdayShift = 22;
        private Span<int> _keys;
        private int[]? _rented;
        private int _count;

        public ExcelHolidayIndex(int capacity, Span<int> initial)
        {
            _rented = capacity > initial.Length ? ArrayPool<int>.Shared.Rent(capacity) : null;
            _keys = _rented == null ? initial.Slice(0, capacity) : _rented.AsSpan(0, capacity);
            _count = 0;
        }

        public bool TryLoad(ExcelArrayOperand values, FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            for (var row = 0; row < values.Rows; row++) for (var col = 0; col < values.Columns; col++)
            {
                var value = values.Read(row, col);
                if (value.Kind == FormulaValueKind.Blank) continue;
                if (!ExcelBusinessCalendarMath.TryDate(value, settings, out var serial, out error)) return false;
                _keys[_count++] = (ExcelBusinessCalendarMath.Weekday(serial, settings.DateSystem) << WeekdayShift) | serial;
            }
            var keys = _keys.Slice(0, _count);
            keys.Sort();
            var unique = 0;
            for (var i = 0; i < _count; i++)
                if (unique == 0 || keys[i] != keys[unique - 1]) keys[unique++] = keys[i];
            _count = unique;
            return true;
        }

        public readonly int CountWorkingHolidays(int start, int end, int weekend)
        {
            if (_count == 0 || weekend == 127 || end < start) return 0;
            var keys = _keys.Slice(0, _count);
            var count = 0;
            for (var weekday = 0; weekday < 7; weekday++)
            {
                if ((weekend & (1 << weekday)) != 0) continue;
                var prefix = weekday << WeekdayShift;
                count += LowerBound(keys, prefix + end + 1) - LowerBound(keys, prefix + start);
            }
            return count;
        }

        private static int LowerBound(ReadOnlySpan<int> keys, int target)
        {
            var low = 0;
            var high = keys.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (keys[middle] < target) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        public void Dispose()
        {
            _keys.Clear();
            if (_rented != null) ArrayPool<int>.Shared.Return(_rented);
            _keys = default;
            _rented = null;
            _count = 0;
        }
    }
}
