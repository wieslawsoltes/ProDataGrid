// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class MatrixMultiplyFunction : ExcelFunctionBase
    {
        public MatrixMultiplyFunction() : base("MMULT", new FormulaFunctionInfo(2, 2)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 2) return ExcelTextUtilities.ValueError();
            var left = new ExcelArrayOperand(args[0]);
            var right = new ExcelArrayOperand(args[1]);
            if (left.IsUnresolvedReference || right.IsUnresolvedReference || left.Columns != right.Rows)
                return ExcelTextUtilities.ValueError();
            var settings = context.EvaluationContext.Workbook.Settings;
            if (!ExcelMatrixUtilities.CheckCells(settings, left.Rows, left.Columns) ||
                !ExcelMatrixUtilities.CheckCells(settings, right.Rows, right.Columns) ||
                !ExcelMatrixUtilities.CheckCells(settings, left.Rows, right.Columns) ||
                !ExcelMatrixUtilities.CheckWork(settings, left.Rows, right.Columns, left.Columns))
                return ExcelNumericUtilities.NumError();

            using var a = new ExcelMatrixWorkspace((int)left.Length, stackalloc double[128]);
            using var b = new ExcelMatrixWorkspace((int)right.Length, stackalloc double[128]);
            if (!ExcelMatrixUtilities.CopyNumbers(left, a.Values, settings, transpose: false, out var error) ||
                !ExcelMatrixUtilities.CopyNumbers(right, b.Values, settings, transpose: true, out error))
                return FormulaValue.FromError(error);
            if (!ExcelArrayShapeUtilities.TryCreate(context, left.Rows, right.Columns, out var result, out error))
                return FormulaValue.FromError(error);

            // Snapshot/transpose once; each dot product then reads contiguous doubles and never
            // dispatches FormulaValue coercion or strided source-array access in its inner loop.
            for (var row = 0; row < result.RowCount; row++)
            {
                var aRow = a.Values.Slice(row * left.Columns, left.Columns);
                for (var column = 0; column < result.ColumnCount; column++)
                {
                    var bColumn = b.Values.Slice(column * left.Columns, left.Columns);
                    var sum = new ExcelCompensatedSum();
                    for (var k = 0; k < aRow.Length; k++)
                        if (!sum.Add(aRow[k] * bColumn[k])) return ExcelNumericUtilities.NumError();
                    if (!double.IsFinite(sum.Total)) return ExcelNumericUtilities.NumError();
                    result[row, column] = ExcelFunctionUtilities.CreateNumber(context, sum.Total);
                }
            }
            return FormulaValue.FromArray(result);
        }
    }

    internal sealed class MatrixFactorFunction : ExcelFunctionBase
    {
        private readonly bool _inverse;
        public MatrixFactorFunction(bool inverse) : base(inverse ? "MINVERSE" : "MDETERM", new FormulaFunctionInfo(1, 1))
        {
            _inverse = inverse;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 1) return ExcelTextUtilities.ValueError();
            var source = new ExcelArrayOperand(args[0]);
            if (source.IsUnresolvedReference || source.Rows != source.Columns) return ExcelTextUtilities.ValueError();
            var size = source.Rows;
            var settings = context.EvaluationContext.Workbook.Settings;
            if (!ExcelMatrixUtilities.CheckCells(settings, size, size) ||
                !ExcelMatrixUtilities.CheckWork(settings, size, size, (long)size * (_inverse ? 2 : 1)))
                return ExcelNumericUtilities.NumError();

            using var snapshot = new ExcelMatrixWorkspace((int)source.Length, stackalloc double[128]);
            if (!ExcelMatrixUtilities.CopyNumbers(source, snapshot.Values, settings, transpose: false, out var error))
                return FormulaValue.FromError(error);
            int[]? rentedIndices = null;
            Span<int> indices = size <= 32 ? stackalloc int[64] : (rentedIndices = ArrayPool<int>.Shared.Rent(size * 2));
            var permutation = indices.Slice(0, size);
            var exponents = indices.Slice(size, size);
            try
            {
                var status = ExcelMatrixLu.Factor(snapshot.Values, size, permutation, exponents, out var sign);
                if (status == ExcelLuStatus.NumericalFailure) return ExcelNumericUtilities.NumError();
                if (status == ExcelLuStatus.Singular)
                    return _inverse ? ExcelNumericUtilities.NumError() : FormulaValue.FromNumber(0);
                if (!_inverse)
                    return ExcelNumericUtilities.Number(context, ExcelMatrixLu.Determinant(snapshot.Values, size, exponents, sign));
                if (!ExcelArrayShapeUtilities.TryCreate(context, size, size, out var result, out error))
                    return FormulaValue.FromError(error);
                using var solution = new ExcelMatrixWorkspace(size, stackalloc double[128]);
                for (var column = 0; column < size; column++)
                {
                    if (!ExcelMatrixLu.SolveUnitColumn(snapshot.Values, size, permutation, column, solution.Values))
                        return ExcelNumericUtilities.NumError();
                    for (var row = 0; row < size; row++)
                    {
                        // If B = S*A, then inverse(A) = inverse(B)*S. Scale output columns
                        // using original row exponents, not the pivoted row ordering.
                        var value = Math.ScaleB(solution.Values[row], -exponents[column]);
                        if (!double.IsFinite(value)) return ExcelNumericUtilities.NumError();
                        result[row, column] = ExcelFunctionUtilities.CreateNumber(context, value);
                    }
                }
                return FormulaValue.FromArray(result);
            }
            finally
            {
                indices.Slice(0, size * 2).Clear();
                if (rentedIndices != null) ArrayPool<int>.Shared.Return(rentedIndices);
            }
        }
    }

    internal sealed class MatrixIdentityFunction : ExcelFunctionBase
    {
        public MatrixIdentityFunction() : base("MUNIT", new FormulaFunctionInfo(1, 1)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 1) return ExcelTextUtilities.ValueError();
            if (!ExcelArrayShapeUtilities.TryInteger(context, args[0], out var size, out var error)) return FormulaValue.FromError(error);
            if (size < 1) return ExcelTextUtilities.ValueError();
            var settings = context.EvaluationContext.Workbook.Settings;
            if (!ExcelMatrixUtilities.CheckCells(settings, size, size) || !ExcelMatrixUtilities.CheckWork(settings, size, size))
                return ExcelNumericUtilities.NumError();
            if (!ExcelArrayShapeUtilities.TryCreate(context, size, size, out var result, out error)) return FormulaValue.FromError(error);
            var zero = FormulaValue.FromNumber(0);
            var one = FormulaValue.FromNumber(1);
            for (var row = 0; row < size; row++)
                for (var column = 0; column < size; column++) result[row, column] = row == column ? one : zero;
            return FormulaValue.FromArray(result);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterMatrixFunctions()
        {
            Register(new MatrixMultiplyFunction());
            Register(new MatrixFactorFunction(inverse: false));
            Register(new MatrixFactorFunction(inverse: true));
            Register(new MatrixIdentityFunction());
        }
    }
}
