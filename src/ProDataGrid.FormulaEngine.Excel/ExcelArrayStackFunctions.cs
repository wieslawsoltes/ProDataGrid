// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class StackArrayFunction : ExcelFunctionBase
    {
        private readonly bool _vertical;
        public StackArrayFunction(bool vertical)
            : base(vertical ? "VSTACK" : "HSTACK", new FormulaFunctionInfo(1, -1))
        {
            _vertical = vertical;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var sources = new FormulaArray[args.Count];
            long rows = 0;
            long columns = 0;
            for (var i = 0; i < args.Count; i++)
            {
                if (!ExcelArrayShapeUtilities.TryGetArray(args[i], out sources[i], out var error))
                {
                    return FormulaValue.FromError(error);
                }
                rows = _vertical ? rows + sources[i].RowCount : Math.Max(rows, sources[i].RowCount);
                columns = _vertical ? Math.Max(columns, sources[i].ColumnCount) : columns + sources[i].ColumnCount;
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out var allocationError))
            {
                return FormulaValue.FromError(allocationError);
            }
            var offset = 0;
            var padding = ExcelArrayShapeUtilities.Error(FormulaErrorType.NA);
            foreach (var source in sources)
            {
                var blockRows = _vertical ? source.RowCount : result.RowCount;
                var blockColumns = _vertical ? result.ColumnCount : source.ColumnCount;
                for (var row = 0; row < blockRows; row++)
                {
                    for (var column = 0; column < blockColumns; column++)
                    {
                        result[_vertical ? row + offset : row, _vertical ? column : column + offset] =
                            row < source.RowCount && column < source.ColumnCount
                                ? ExcelArrayShapeUtilities.Read(source, row, column) : padding;
                    }
                }
                offset += _vertical ? source.RowCount : source.ColumnCount;
            }
            return FormulaValue.FromArray(result);
        }
    }

    internal sealed class TransposeFunction : ExcelFunctionBase
    {
        public TransposeFunction() : base("TRANSPOSE", new FormulaFunctionInfo(1, 1)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error) ||
                !ExcelArrayShapeUtilities.TryCreate(context, source.ColumnCount, source.RowCount, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            for (var row = 0; row < result.RowCount; row++)
            {
                for (var column = 0; column < result.ColumnCount; column++)
                {
                    result[row, column] = ExcelArrayShapeUtilities.Read(source, column, row);
                }
            }
            return FormulaValue.FromArray(result);
        }
    }

    internal sealed class ExpandFunction : ExcelFunctionBase
    {
        public ExpandFunction() : base("EXPAND", new FormulaFunctionInfo(2, 4)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            var rows = source.RowCount;
            var columns = source.ColumnCount;
            if ((!ExcelArrayShapeUtilities.IsOmitted(args, 1) && !ExcelArrayShapeUtilities.TryInteger(context, args[1], out rows, out error)) ||
                (!ExcelArrayShapeUtilities.IsOmitted(args, 2) && !ExcelArrayShapeUtilities.TryInteger(context, args[2], out columns, out error)))
            {
                return FormulaValue.FromError(error);
            }
            if (rows < source.RowCount || columns < source.ColumnCount)
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var pad = ExcelArrayShapeUtilities.Pad(args, 3, context);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    result[row, column] = row < source.RowCount && column < source.ColumnCount
                        ? ExcelArrayShapeUtilities.Read(source, row, column) : pad;
                }
            }
            return FormulaValue.FromArray(result);
        }
    }
}
