// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal ref struct ExcelMatrixWorkspace
    {
        private Span<double> _values;
        private double[]? _rented;

        public ExcelMatrixWorkspace(int count, Span<double> initial)
        {
            _rented = count > initial.Length ? ArrayPool<double>.Shared.Rent(count) : null;
            _values = _rented == null ? initial.Slice(0, count) : _rented.AsSpan(0, count);
        }

        public Span<double> Values => _values;

        public void Dispose()
        {
            _values.Clear();
            if (_rented != null) ArrayPool<double>.Shared.Return(_rented);
            _rented = null;
            _values = default;
        }
    }

    internal static class ExcelMatrixUtilities
    {
        public static bool CheckCells(FormulaCalculationSettings settings, int rows, int columns)
            => rows > 0 && rows <= 1048576 && columns > 0 && columns <= 16384 &&
                (long)rows * columns <= settings.MaximumArrayCellCount;

        public static bool CheckWork(FormulaCalculationSettings settings, long first, long second, long third = 1)
        {
            var limit = settings.MaximumMatrixOperationCount;
            // Division checks avoid overflowing even when a host raises limits to Int64.MaxValue.
            return first > 0 && second > 0 && third > 0 && first <= limit / second && first * second <= limit / third;
        }

        public static bool CopyNumbers(ExcelArrayOperand source, Span<double> target,
            FormulaCalculationSettings settings, bool transpose, out FormulaError error)
        {
            error = default;
            for (var row = 0; row < source.Rows; row++)
            {
                for (var column = 0; column < source.Columns; column++)
                {
                    if (!ExcelArrayOperand.TryNumeric(source.Read(row, column), settings, out var number, out var numeric, out error))
                        return false;
                    // Unlike SUMPRODUCT, numeric text, Booleans and blanks are invalid matrix cells.
                    if (!numeric) { error = new FormulaError(FormulaErrorType.Value); return false; }
                    target[transpose ? column * source.Rows + row : row * source.Columns + column] = number;
                }
            }
            return true;
        }
    }
}
