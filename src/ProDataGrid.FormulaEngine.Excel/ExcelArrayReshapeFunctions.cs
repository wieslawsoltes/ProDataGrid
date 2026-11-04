// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class FlattenArrayFunction : ExcelFunctionBase
    {
        private readonly bool _column;
        public FlattenArrayFunction(bool column)
            : base(column ? "TOCOL" : "TOROW", new FormulaFunctionInfo(1, 3))
        {
            _column = column;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            var ignore = 0;
            if (!ExcelArrayShapeUtilities.IsOmitted(args, 1) && !ExcelArrayShapeUtilities.TryInteger(context, args[1], out ignore, out error))
            {
                return FormulaValue.FromError(error);
            }
            if (ignore < 0 || ignore > 3)
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
            }
            var byColumn = false;
            if (!ExcelArrayShapeUtilities.IsOmitted(args, 2) &&
                !FormulaCoercion.TryCoerceToBoolean(FormulaCoercion.ApplyImplicitIntersection(args[2], context.EvaluationContext.Address), out byColumn, out error))
            {
                return FormulaValue.FromError(error);
            }
            // Count then fill: no list containing a second copy of every FormulaValue.
            long count = 0;
            for (var row = 0; row < source.RowCount; row++)
            {
                for (var column = 0; column < source.ColumnCount; column++)
                {
                    if (!ShouldIgnore(ExcelDynamicArrayUtilities.GetArrayValue(source, row, column), ignore))
                    {
                        count++;
                    }
                }
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, _column ? count : 1, _column ? 1 : count, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var outerCount = byColumn ? source.ColumnCount : source.RowCount;
            var innerCount = byColumn ? source.RowCount : source.ColumnCount;
            var index = 0;
            for (var outer = 0; outer < outerCount; outer++)
            {
                for (var inner = 0; inner < innerCount; inner++)
                {
                    var row = byColumn ? inner : outer;
                    var column = byColumn ? outer : inner;
                    if (!ShouldIgnore(ExcelDynamicArrayUtilities.GetArrayValue(source, row, column), ignore))
                    {
                        result[_column ? index : 0, _column ? 0 : index] = ExcelArrayShapeUtilities.Read(source, row, column);
                        index++;
                    }
                }
            }
            return FormulaValue.FromArray(result);
        }

        private static bool ShouldIgnore(FormulaValue value, int mode)
            => (value.Kind == FormulaValueKind.Blank && (mode & 1) != 0) ||
               (value.Kind == FormulaValueKind.Error && (mode & 2) != 0);
    }

    internal sealed class WrapArrayFunction : ExcelFunctionBase
    {
        private readonly bool _columns;
        public WrapArrayFunction(bool columns)
            : base(columns ? "WRAPCOLS" : "WRAPROWS", new FormulaFunctionInfo(2, 3))
        {
            _columns = columns;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            if (source.RowCount != 1 && source.ColumnCount != 1)
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
            }
            if (!ExcelArrayShapeUtilities.TryInteger(context, args[1], out var wrap, out error))
            {
                return FormulaValue.FromError(error);
            }
            if (wrap < 1)
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Num);
            }
            var length = Math.Max(source.RowCount, source.ColumnCount);
            wrap = Math.Min(wrap, length);
            var groups = ((long)length + wrap - 1) / wrap;
            if (!ExcelArrayShapeUtilities.TryCreate(context, _columns ? wrap : groups,
                    _columns ? groups : wrap, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var pad = ExcelArrayShapeUtilities.Pad(args, 2, context);
            for (var row = 0; row < result.RowCount; row++)
            {
                for (var column = 0; column < result.ColumnCount; column++)
                {
                    var index = _columns ? column * wrap + row : row * wrap + column;
                    result[row, column] = index < length
                        ? ExcelArrayShapeUtilities.Read(source, source.ColumnCount == 1 ? index : 0, source.RowCount == 1 ? index : 0)
                        : pad;
                }
            }
            return FormulaValue.FromArray(result);
        }
    }
}
