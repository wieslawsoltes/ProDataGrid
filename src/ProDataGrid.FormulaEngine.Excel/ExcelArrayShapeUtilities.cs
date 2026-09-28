// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelArrayShapeUtilities
    {
        public static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));

        public static bool TryGetArray(FormulaValue value, out FormulaArray array, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Reference)
            {
                array = null!;
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
            if (value.Kind == FormulaValueKind.Array)
            {
                array = value.AsArray();
                return true;
            }
            // Scalar errors are array elements: HSTACK(1,NA()) must not discard the 1.
            array = new FormulaArray(1, 1);
            array[0, 0] = value;
            return true;
        }

        public static bool TryCreate(FormulaFunctionContext context, long rows, long columns,
            out FormulaArray array, out FormulaError error)
        {
            array = null!;
            error = default;
            if (rows < 1 || columns < 1)
            {
                error = new FormulaError(FormulaErrorType.Calc);
                return false;
            }
            // Check dimensions before multiplication; no overflowing product or speculative allocation.
            if (rows > 1048576 || columns > 16384 ||
                rows * columns > context.EvaluationContext.Workbook.Settings.MaximumArrayCellCount)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            array = new FormulaArray((int)rows, (int)columns);
            return true;
        }

        public static bool TryInteger(FormulaFunctionContext context, FormulaValue value,
            out int result, out FormulaError error)
        {
            result = 0;
            value = FormulaCoercion.ApplyImplicitIntersection(value, context.EvaluationContext.Address);
            if (!FormulaCoercion.TryCoerceToNumber(value, context.EvaluationContext.Workbook.Settings, out var number, out error))
            {
                return false;
            }
            number = Math.Truncate(number);
            if (!double.IsFinite(number) || number < int.MinValue || number > int.MaxValue)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            result = (int)number;
            return true;
        }

        public static bool IsOmitted(IReadOnlyList<FormulaValue> args, int index)
            => index >= args.Count || args[index].Kind == FormulaValueKind.Blank;

        public static FormulaValue Read(FormulaArray array, int row, int column)
        {
            var value = ExcelDynamicArrayUtilities.GetArrayValue(array, row, column);
            // A value-producing transformation materializes blank reference cells as zero.
            return value.Kind == FormulaValueKind.Blank ? FormulaValue.FromNumber(0) : value;
        }

        public static FormulaValue Pad(IReadOnlyList<FormulaValue> args, int index, FormulaFunctionContext context)
        {
            if (index >= args.Count)
            {
                return Error(FormulaErrorType.NA);
            }
            var value = FormulaCoercion.ApplyImplicitIntersection(args[index], context.EvaluationContext.Address);
            return value.Kind == FormulaValueKind.Blank ? FormulaValue.FromNumber(0) : value;
        }
    }
}
