// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class SliceArrayFunction : ExcelFunctionBase
    {
        private readonly bool _take;
        public SliceArrayFunction(bool take) : base(take ? "TAKE" : "DROP", new FormulaFunctionInfo(2, 3))
        {
            _take = take;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            var hasRows = !ExcelArrayShapeUtilities.IsOmitted(args, 1);
            var hasColumns = !ExcelArrayShapeUtilities.IsOmitted(args, 2);
            var rows = _take ? source.RowCount : 0;
            var columns = _take ? source.ColumnCount : 0;
            if ((hasRows && !ExcelArrayShapeUtilities.TryInteger(context, args[1], out rows, out error)) ||
                (hasColumns && !ExcelArrayShapeUtilities.TryInteger(context, args[2], out columns, out error)))
            {
                return FormulaValue.FromError(error);
            }
            if ((!hasRows && !hasColumns) || (hasRows && rows == 0) || (hasColumns && columns == 0))
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Calc);
            }
            var rowCount = GetCount(source.RowCount, rows);
            var columnCount = GetCount(source.ColumnCount, columns);
            if (!ExcelArrayShapeUtilities.TryCreate(context, rowCount, columnCount, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var rowStart = _take ? (rows < 0 ? source.RowCount - rowCount : 0) : (rows > 0 ? rows : 0);
            var columnStart = _take ? (columns < 0 ? source.ColumnCount - columnCount : 0) : (columns > 0 ? columns : 0);
            for (var row = 0; row < rowCount; row++)
            {
                for (var column = 0; column < columnCount; column++)
                {
                    result[row, column] = ExcelArrayShapeUtilities.Read(source, rowStart + row, columnStart + column);
                }
            }
            return FormulaValue.FromArray(result);
        }

        private int GetCount(int length, int count)
        {
            var magnitude = Math.Min(length, Math.Abs((long)count));
            return _take ? (int)magnitude : length - (int)magnitude;
        }
    }

    internal sealed class ChooseAxisFunction : ExcelFunctionBase
    {
        private readonly bool _rows;
        public ChooseAxisFunction(bool rows)
            : base(rows ? "CHOOSEROWS" : "CHOOSECOLS", new FormulaFunctionInfo(2, -1))
        {
            _rows = rows;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            long count = 0;
            for (var i = 1; i < args.Count; i++)
            {
                count += args[i].Kind == FormulaValueKind.Array
                    ? (long)args[i].AsArray().RowCount * args[i].AsArray().ColumnCount : 1;
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, _rows ? count : source.RowCount,
                    _rows ? source.ColumnCount : count, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var limit = _rows ? source.RowCount : source.ColumnCount;
            var position = 0;
            for (var argument = 1; argument < args.Count; argument++)
            {
                if (!ExcelArrayShapeUtilities.TryGetArray(args[argument], out var indices, out error))
                {
                    return FormulaValue.FromError(error);
                }
                for (var row = 0; row < indices.RowCount; row++)
                {
                    for (var column = 0; column < indices.ColumnCount; column++)
                    {
                        if (!ExcelArrayShapeUtilities.TryInteger(context,
                                ExcelDynamicArrayUtilities.GetArrayValue(indices, row, column), out var index, out error))
                        {
                            return FormulaValue.FromError(error);
                        }
                        if (index == 0 || Math.Abs((long)index) > limit)
                        {
                            return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
                        }
                        index = index > 0 ? index - 1 : limit + index;
                        var width = _rows ? source.ColumnCount : source.RowCount;
                        for (var other = 0; other < width; other++)
                        {
                            result[_rows ? position : other, _rows ? other : position] =
                                ExcelArrayShapeUtilities.Read(source, _rows ? index : other, _rows ? other : index);
                        }
                        position++;
                    }
                }
            }
            return FormulaValue.FromArray(result);
        }
    }
}
