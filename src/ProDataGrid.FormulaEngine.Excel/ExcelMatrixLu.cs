// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelLuStatus { Success, Singular, NumericalFailure }

    internal static class ExcelMatrixLu
    {
        // Factor P * S * A = L * U, with power-of-two row scaling S. The scale exponents
        // stay in original row order; only the permutation changes when pivot rows swap.
        public static ExcelLuStatus Factor(Span<double> values, int size, Span<int> permutation,
            Span<int> rowExponents, out int sign)
        {
            sign = 1;
            var hasZeroRow = false;
            for (var row = 0; row < size; row++)
            {
                permutation[row] = row;
                var start = row * size;
                double maximum = 0;
                for (var column = 0; column < size; column++) maximum = Math.Max(maximum, Math.Abs(values[start + column]));
                var exponent = maximum == 0 ? 0 : Math.ILogB(maximum);
                // Equilibration is optional. If scaling would lose even a subnormal bit,
                // keep the entire row unscaled rather than rejecting an otherwise valid
                // matrix or silently changing a nonzero entry to zero.
                if (exponent != 0)
                {
                    for (var column = 0; column < size; column++)
                    {
                        var original = values[start + column];
                        var scaled = Math.ScaleB(original, -exponent);
                        if (Math.ScaleB(scaled, exponent) != original) { exponent = 0; break; }
                    }
                }
                rowExponents[row] = exponent;
                hasZeroRow |= maximum == 0;
                if (exponent != 0)
                    for (var column = 0; column < size; column++) values[start + column] = Math.ScaleB(values[start + column], -exponent);
            }
            if (hasZeroRow) return ExcelLuStatus.Singular;

            for (var column = 0; column < size; column++)
            {
                var pivotRow = column;
                var largest = Math.Abs(values[column * size + column]);
                for (var row = column + 1; row < size; row++)
                {
                    var candidate = Math.Abs(values[row * size + column]);
                    if (candidate > largest) { largest = candidate; pivotRow = row; }
                }
                if (largest == 0) return ExcelLuStatus.Singular;
                if (pivotRow != column)
                {
                    for (var j = 0; j < size; j++)
                    {
                        var saved = values[column * size + j];
                        values[column * size + j] = values[pivotRow * size + j];
                        values[pivotRow * size + j] = saved;
                    }
                    var original = permutation[column];
                    permutation[column] = permutation[pivotRow];
                    permutation[pivotRow] = original;
                    sign = -sign;
                }
                var pivot = values[column * size + column];
                for (var row = column + 1; row < size; row++)
                {
                    var start = row * size;
                    var factor = values[start + column] / pivot;
                    if (!double.IsFinite(factor)) return ExcelLuStatus.NumericalFailure;
                    values[start + column] = factor;
                    for (var j = column + 1; j < size; j++)
                    {
                        var updated = Math.FusedMultiplyAdd(-factor, values[column * size + j], values[start + j]);
                        if (!double.IsFinite(updated)) return ExcelLuStatus.NumericalFailure;
                        values[start + j] = updated;
                    }
                }
            }
            return ExcelLuStatus.Success;
        }

        public static double Determinant(ReadOnlySpan<double> lu, int size, ReadOnlySpan<int> rowExponents, int sign)
        {
            var exponent = 0;
            var mantissa = (double)sign;
            for (var row = 0; row < size; row++)
            {
                exponent += rowExponents[row];
                var diagonal = lu[row * size + row];
                if (diagonal == 0) return 0;
                var power = Math.ILogB(Math.Abs(diagonal));
                mantissa *= Math.ScaleB(diagonal, -power);
                exponent += power;
                var productPower = Math.ILogB(Math.Abs(mantissa));
                mantissa = Math.ScaleB(mantissa, -productPower);
                exponent += productPower;
            }
            // Scaling just once prevents overflowing/underflowing a partial diagonal product.
            return Math.ScaleB(mantissa, exponent);
        }

        public static bool SolveUnitColumn(ReadOnlySpan<double> lu, int size, ReadOnlySpan<int> permutation,
            int column, Span<double> solution)
        {
            for (var row = 0; row < size; row++)
            {
                var value = permutation[row] == column ? 1d : 0d;
                for (var j = 0; j < row; j++) value = Math.FusedMultiplyAdd(-lu[row * size + j], solution[j], value);
                if (!double.IsFinite(value)) return false;
                solution[row] = value;
            }
            for (var row = size - 1; row >= 0; row--)
            {
                var value = solution[row];
                for (var j = row + 1; j < size; j++) value = Math.FusedMultiplyAdd(-lu[row * size + j], solution[j], value);
                value /= lu[row * size + row];
                if (!double.IsFinite(value)) return false;
                solution[row] = value;
            }
            return true;
        }
    }
}
