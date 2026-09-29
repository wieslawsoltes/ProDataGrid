// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    // A private numeric snapshot: selection never reorders the host's FormulaArray.
    // Small inputs stay on the caller's stack. Larger buffers are cleared before returning.
    internal ref struct ExcelNumericSample
    {
        private Span<double> _values;
        private double[]? _rented;
        public int Count { get; private set; }
        public Span<double> Values => _values.Slice(0, Count);
        public ExcelNumericSample(Span<double> initial) { _values = initial; _rented = null; Count = 0; }

        public bool TryAdd(FormulaValue value, FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                for (var row = 0; row < array.RowCount; row++)
                    for (var column = 0; column < array.ColumnCount; column++)
                    {
                        if (!array.IsPresent(row, column)) continue;
                        var item = array[row, column];
                        if (item.Kind == FormulaValueKind.Error) { error = item.AsError(); return false; }
                        if (item.Kind == FormulaValueKind.Number && !TryAddNumber(item.AsNumber(), settings, out error)) return false;
                    }
                return true;
            }
            if (value.Kind == FormulaValueKind.Blank) return true;
            if (!FormulaCoercion.TryCoerceToNumber(value, settings, out var number, out error)) return false;
            return TryAddNumber(number, settings, out error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddNumber(double number, FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (!double.IsFinite(number)) { error = new FormulaError(FormulaErrorType.Num); return false; }
            if (Count == _values.Length && !Grow()) { error = new FormulaError(FormulaErrorType.Num); return false; }
            _values[Count++] = settings.ApplyNumberPrecision
                ? FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits) : number;
            return true;
        }

        private bool Grow()
        {
            var capacity = (int)Math.Min(Array.MaxLength, Math.Max(16L, (long)_values.Length * 2));
            if (capacity <= Count) return false;
            var next = ArrayPool<double>.Shared.Rent(capacity);
            Values.CopyTo(next);
            if (_rented != null) { Values.Clear(); ArrayPool<double>.Shared.Return(_rented); }
            _rented = next;
            _values = next;
            return true;
        }

        public void Dispose()
        {
            if (_rented != null)
            {
                Values.Clear();
                ArrayPool<double>.Shared.Return(_rented);
                _rented = null;
            }
            _values = default;
            Count = 0;
        }
    }

    internal static class ExcelOrderStatistics
    {
        public static FormulaValue Median(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var sample = new ExcelNumericSample(stackalloc double[128]);
            try
            {
                for (var i = 0; i < args.Count; i++)
                    if (!sample.TryAdd(args[i], context.EvaluationContext.Workbook.Settings, out var error)) return FormulaValue.FromError(error);
                if (sample.Count == 0) return NumError();
                var upperIndex = sample.Count / 2;
                var upper = Select(sample.Values, upperIndex);
                if ((sample.Count & 1) != 0) return ExcelFunctionUtilities.CreateNumber(context, upper);
                var lower = Maximum(sample.Values.Slice(0, upperIndex));
                return ExcelFunctionUtilities.CreateNumber(context, Interpolate(lower, upper, 0.5));
            }
            finally { sample.Dispose(); }
        }

        public static FormulaValue Ordered(FormulaCalculationSettings settings, FormulaValue data, FormulaValue rank, bool largest)
        {
            var sample = new ExcelNumericSample(stackalloc double[128]);
            try
            {
                if (!sample.TryAdd(data, settings, out var error)) return FormulaValue.FromError(error);
                if (!FormulaCoercion.TryCoerceToNumber(rank, settings, out var k, out error)) return FormulaValue.FromError(error);
                // Retain integer truncation while rejecting NaN/infinity before conversion.
                if (!double.IsFinite(k) || k <= 0 || k > sample.Count) return NumError();
                var integer = (int)Math.Truncate(k);
                if (integer == 0) return NumError();
                return ExcelFunctionUtilities.CreateNumber(settings, Select(sample.Values, largest ? sample.Count - integer : integer - 1));
            }
            finally { sample.Dispose(); }
        }

        public static FormulaValue Percentile(FormulaCalculationSettings settings, FormulaValue data, double k, bool exclusive)
        {
            if (!double.IsFinite(k) || k < 0 || k > 1 || (exclusive && (k == 0 || k == 1))) return NumError();
            var sample = new ExcelNumericSample(stackalloc double[128]);
            try
            {
                if (!sample.TryAdd(data, settings, out var error)) return FormulaValue.FromError(error);
                if (sample.Count == 0) return NumError();
                var position = exclusive ? (sample.Count + 1d) * k - 1d : (sample.Count - 1d) * k;
                if (exclusive && (k < 1d / (sample.Count + 1d) || k > sample.Count / (sample.Count + 1d))) return NumError();
                position = Math.Clamp(position, 0, sample.Count - 1d);
                var lowerIndex = (int)Math.Floor(position);
                var lower = Select(sample.Values, lowerIndex);
                var fraction = position - lowerIndex;
                if (fraction == 0) return ExcelFunctionUtilities.CreateNumber(settings, lower);
                // Selection guarantees that every remaining element is >= the selected one.
                // The next order statistic is the minimum of that suffix, not a second sort.
                var upper = Minimum(sample.Values.Slice(lowerIndex + 1));
                return ExcelFunctionUtilities.CreateNumber(settings, Interpolate(lower, upper, fraction));
            }
            finally { sample.Dispose(); }
        }

        private static FormulaValue NumError() => FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));

        private static double Interpolate(double lower, double upper, double fraction)
        {
            // Opposite-sign subtraction can overflow even when the interpolated value is finite.
            return Math.Sign(lower) != Math.Sign(upper)
                ? lower * (1 - fraction) + upper * fraction
                : lower + (upper - lower) * fraction;
        }

        private static double Minimum(ReadOnlySpan<double> values)
        {
            var value = values[0];
            for (var i = 1; i < values.Length; i++) if (values[i] < value) value = values[i];
            return value;
        }
        private static double Maximum(ReadOnlySpan<double> values)
        {
            var value = values[0];
            for (var i = 1; i < values.Length; i++) if (values[i] > value) value = values[i];
            return value;
        }

        private static double Select(Span<double> values, int target)
        {
            if (target == 0 || target == values.Length - 1)
            {
                var best = 0;
                for (var i = 1; i < values.Length; i++)
                    if (target == 0 ? values[i] < values[best] : values[i] > values[best]) best = i;
                Swap(values, best, target);
                return values[target];
            }
            var left = 0;
            var right = values.Length - 1;
            var budget = 0;
            for (var n = values.Length; n > 1; n >>= 1) budget += 2;
            while (right - left > 24 && budget-- > 0)
            {
                var a = values[left];
                var b = values[left + (right - left) / 2];
                var c = values[right];
                var pivot = a < b ? (b < c ? b : Math.Max(a, c)) : (a < c ? a : Math.Max(b, c));
                var low = left;
                var high = right;
                var current = left;
                while (current <= high)
                {
                    if (values[current] < pivot) { Swap(values, low++, current++); }
                    else if (values[current] > pivot) { Swap(values, current, high--); }
                    else current++;
                }
                if (target < low) right = low - 1;
                else if (target > high) left = high + 1;
                else return values[target];
            }
            // Bound adversarial partitioning by falling back to the runtime's introspective sort.
            values.Slice(left, right - left + 1).Sort();
            return values[target];
        }

        private static void Swap(Span<double> values, int a, int b)
        {
            var saved = values[a]; values[a] = values[b]; values[b] = saved;
        }
    }

    internal sealed class ExclusivePercentileFunction : ExcelFunctionBase
    {
        private readonly bool _quartile;
        public ExclusivePercentileFunction(bool quartile)
            : base(quartile ? "QUARTILE.EXC" : "PERCENTILE.EXC", new FormulaFunctionInfo(2, 2)) { _quartile = quartile; }
        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 2) return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            if (!ExcelFunctionUtilities.TryCoerceToNumber(context, args[1], out var k, out var error)) return FormulaValue.FromError(error);
            if (_quartile)
            {
                if (!double.IsFinite(k)) return FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));
                k = Math.Truncate(k);
                if (k < 1 || k > 3) return FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));
                k /= 4d;
            }
            return ExcelOrderStatistics.Percentile(context.EvaluationContext.Workbook.Settings, args[0], k, exclusive: true);
        }
    }
}
