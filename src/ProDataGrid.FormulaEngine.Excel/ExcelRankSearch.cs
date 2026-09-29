// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal readonly struct ExcelRankPosition
    {
        public ExcelRankPosition(int count, int less, int equal, double lower, double upper)
        {
            Count = count; Less = less; Equal = equal; Lower = lower; Upper = upper;
        }
        public int Count { get; }
        public int Less { get; }
        public int Equal { get; }
        public double Lower { get; }
        public double Upper { get; }
    }

    // Both query paths use the same comparison/coercion contract. A scalar query scans once
    // with no workspace; a batch queries one private sorted snapshot with binary bounds.
    internal static class ExcelRankSearch
    {
        public static bool TryScan(in ExcelArrayOperand source, double target, FormulaCalculationSettings settings,
            out ExcelRankPosition position, out FormulaError error)
        {
            var count = 0;
            var less = 0;
            var equal = 0;
            var lower = double.NegativeInfinity;
            var upper = double.PositiveInfinity;
            position = default;
            for (var row = 0; row < source.Rows; row++)
                for (var column = 0; column < source.Columns; column++)
                {
                    if (!ExcelArrayOperand.TryNumeric(source.Read(row, column), settings, out var number, out var numeric, out error)) return false;
                    if (!numeric) continue;
                    count++;
                    if (number < target) { less++; if (number > lower) lower = number; }
                    else if (number == target) equal++;
                    else if (number < upper) upper = number;
                }
            error = default;
            position = new ExcelRankPosition(count, less, equal, lower, upper);
            return true;
        }

        public static bool TryCopy(in ExcelArrayOperand source, Span<double> values, FormulaCalculationSettings settings,
            out int count, out FormulaError error)
        {
            count = 0;
            for (var row = 0; row < source.Rows; row++)
                for (var column = 0; column < source.Columns; column++)
                {
                    if (!ExcelArrayOperand.TryNumeric(source.Read(row, column), settings, out var number, out var numeric, out error)) return false;
                    if (numeric) values[count++] = number;
                }
            error = default;
            return true;
        }

        public static ExcelRankPosition Find(ReadOnlySpan<double> sorted, double target)
        {
            var low = LowerBound(sorted, target);
            var high = low;
            if (low < sorted.Length && sorted[low] == target)
            {
                var end = sorted.Length;
                while (high < end)
                {
                    var middle = high + (end - high) / 2;
                    if (sorted[middle] <= target) high = middle + 1;
                    else end = middle;
                }
            }
            return new ExcelRankPosition(sorted.Length, low, high - low,
                low == 0 ? double.NegativeInfinity : sorted[low - 1],
                high == sorted.Length ? double.PositiveInfinity : sorted[high]);
        }

        private static int LowerBound(ReadOnlySpan<double> sorted, double target)
        {
            var low = 0;
            var high = sorted.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (sorted[middle] < target) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        public static FormulaValue Rank(in ExcelRankPosition position, bool ascending, bool average)
        {
            if (position.Equal == 0) return Error(FormulaErrorType.NA);
            var before = ascending ? position.Less : position.Count - position.Less - position.Equal;
            return FormulaValue.FromNumber(before + 1d + (average ? (position.Equal - 1d) / 2d : 0d));
        }

        public static FormulaValue Percent(in ExcelRankPosition position, double target, double significance, bool exclusive)
        {
            if (position.Count == 0) return Error(FormulaErrorType.Num);
            if (position.Equal == 0 && (position.Less == 0 || position.Less == position.Count)) return Error(FormulaErrorType.NA);
            // A singleton has only the minimum inclusive position, or the middle exclusive position.
            if (position.Count == 1) return FormulaValue.FromNumber(exclusive ? 0.5 : 0);
            var fraction = 0d;
            var ordinal = position.Less;
            if (position.Equal == 0)
            {
                ordinal--;
                var width = position.Upper - position.Lower;
                // Halve only the overflowing opposite-sign interval. Ordinary/subnormal intervals
                // keep their original scale, so tiny adjacent values are not flushed to zero.
                fraction = double.IsFinite(width) ? (target - position.Lower) / width
                    : (target * 0.5 - position.Lower * 0.5) / (position.Upper * 0.5 - position.Lower * 0.5);
                fraction = Math.Clamp(fraction, 0, 1);
            }
            if (exclusive) ordinal++;
            var denominator = exclusive ? position.Count + 1d : position.Count - 1d;
            var digits = Math.Truncate(significance);
            if (digits > 15) return FormulaValue.FromNumber((ordinal + fraction) / denominator);
            // Compute decimal-place truncation from the ordinal, not a rounded binary quotient.
            // For example 29/100 must not become .28 because .29*100 rounded just below 29.
            var scale = 1m;
            for (var i = 0; i < (int)digits; i++) scale *= 10m;
            var value = decimal.Truncate(((decimal)ordinal + (decimal)fraction) * scale / (decimal)denominator) / scale;
            return FormulaValue.FromNumber((double)value);
        }

        private static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));
    }
}
