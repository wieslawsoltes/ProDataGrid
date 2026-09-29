// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    // A non-owning, allocation-free view of an evaluated scalar or rectangular array.
    // Source arrays must remain stable during evaluation, as with the other array functions.
    internal readonly struct ExcelArrayOperand
    {
        private readonly FormulaValue _scalar;
        private readonly FormulaArray? _array;

        public ExcelArrayOperand(FormulaValue value)
        {
            _scalar = value;
            _array = value.Kind == FormulaValueKind.Array ? value.AsArray() : null;
        }

        public int Rows => _array?.RowCount ?? 1;
        public int Columns => _array?.ColumnCount ?? 1;
        public long Length => (long)Rows * Columns;
        public bool IsUnresolvedReference => _scalar.Kind == FormulaValueKind.Reference;

        public FormulaValue Read(int row, int column)
            => _array == null ? _scalar : ExcelDynamicArrayUtilities.GetArrayValue(_array, row, column);

        public static bool TryNumeric(FormulaValue value, FormulaCalculationSettings settings,
            out double number, out bool numeric, out FormulaError error)
        {
            number = 0;
            numeric = value.Kind == FormulaValueKind.Number;
            error = default;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            if (!numeric) return true;
            number = value.AsNumber();
            if (!double.IsFinite(number)) { error = new FormulaError(FormulaErrorType.Num); return false; }
            if (settings.ApplyNumberPrecision)
                number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
            return true;
        }
    }

    // Neumaier compensation improves cancellation without a list of intermediate terms.
    // Overflow remains an explicit numerical error, not a NaN silently stored in a cell.
    internal struct ExcelCompensatedSum
    {
        private double _sum;
        private double _correction;
        public double Total => _sum + _correction;

        public bool Add(double value)
        {
            var next = _sum + value;
            if (!double.IsFinite(next)) return false;
            _correction += Math.Abs(_sum) >= Math.Abs(value)
                ? (_sum - next) + value : (value - next) + _sum;
            _sum = next;
            return double.IsFinite(_correction);
        }
    }
}
