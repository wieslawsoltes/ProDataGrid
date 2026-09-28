// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class SortByFunction : ExcelFunctionBase
    {
        public SortByFunction() : base("SORTBY", new FormulaFunctionInfo(2, -1)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelArrayShapeUtilities.TryGetArray(args[0], out var source, out var error))
            {
                return FormulaValue.FromError(error);
            }
            var keyCount = args.Count / 2;
            var keys = new FormulaArray[keyCount];
            var orders = new int[keyCount];
            var byRows = true;
            for (var key = 0; key < keyCount; key++)
            {
                var argument = 1 + key * 2;
                if (!ExcelArrayShapeUtilities.TryGetArray(args[argument], out var values, out error))
                {
                    return FormulaValue.FromError(error);
                }
                if (key == 0)
                {
                    if (values.ColumnCount == 1 && values.RowCount == source.RowCount)
                    {
                        byRows = true;
                    }
                    else if (values.RowCount == 1 && values.ColumnCount == source.ColumnCount)
                    {
                        byRows = false;
                    }
                    else
                    {
                        return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
                    }
                }
                if ((byRows && (values.ColumnCount != 1 || values.RowCount != source.RowCount)) ||
                    (!byRows && (values.RowCount != 1 || values.ColumnCount != source.ColumnCount)))
                {
                    return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
                }
                keys[key] = values;
                orders[key] = 1;
                if (!ExcelArrayShapeUtilities.IsOmitted(args, argument + 1) &&
                    !ExcelArrayShapeUtilities.TryInteger(context, args[argument + 1], out orders[key], out error))
                {
                    return FormulaValue.FromError(error);
                }
                if (orders[key] != 1 && orders[key] != -1)
                {
                    return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
                }
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, source.RowCount, source.ColumnCount, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            var indices = new int[byRows ? source.RowCount : source.ColumnCount];
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }
            Array.Sort(indices, (left, right) =>
            {
                for (var key = 0; key < keys.Length; key++)
                {
                    var leftValue = ExcelDynamicArrayUtilities.GetArrayValue(keys[key], byRows ? left : 0, byRows ? 0 : left);
                    var rightValue = ExcelDynamicArrayUtilities.GetArrayValue(keys[key], byRows ? right : 0, byRows ? 0 : right);
                    var comparison = Compare(leftValue, rightValue);
                    if (comparison != 0)
                    {
                        return (comparison < 0 ? -1 : 1) * orders[key];
                    }
                }
                // Original ordinal makes multi-key ties deterministic and stable.
                return left.CompareTo(right);
            });
            for (var row = 0; row < result.RowCount; row++)
            {
                for (var column = 0; column < result.ColumnCount; column++)
                {
                    result[row, column] = ExcelArrayShapeUtilities.Read(source, byRows ? indices[row] : row,
                        byRows ? column : indices[column]);
                }
            }
            return FormulaValue.FromArray(result);
        }

        private static int Compare(FormulaValue left, FormulaValue right)
        {
            var leftRank = Rank(left.Kind);
            var rightRank = Rank(right.Kind);
            if (leftRank != rightRank)
            {
                return leftRank.CompareTo(rightRank);
            }
            return left.Kind switch
            {
                FormulaValueKind.Number => left.AsNumber().CompareTo(right.AsNumber()),
                FormulaValueKind.Text => string.Compare(left.AsText(), right.AsText(), StringComparison.OrdinalIgnoreCase),
                FormulaValueKind.Boolean => left.AsBoolean().CompareTo(right.AsBoolean()),
                FormulaValueKind.Error => left.AsError().Type.CompareTo(right.AsError().Type),
                _ => 0
            };
        }

        private static int Rank(FormulaValueKind kind) => kind switch
        {
            FormulaValueKind.Number => 0,
            FormulaValueKind.Text => 1,
            FormulaValueKind.Boolean => 2,
            FormulaValueKind.Error => 3,
            FormulaValueKind.Blank => 4,
            _ => 5
        };
    }
}
