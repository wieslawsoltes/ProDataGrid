// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class SequenceFunction : ExcelFunctionBase
    {
        public SequenceFunction()
            : base("SEQUENCE", new FormulaFunctionInfo(1, 4))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var rowsValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[0], address);
            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, rowsValue, out var rows, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var columns = 1;
            if (args.Count > 1)
            {
                var columnsValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[1], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, columnsValue, out columns, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (rows < 1 || columns < 1)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var start = 1d;
            if (args.Count > 2)
            {
                var startValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[2], address);
                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, startValue, out start, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            var step = 1d;
            if (args.Count > 3)
            {
                var stepValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[3], address);
                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, stepValue, out step, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var array, out error))
            {
                return FormulaValue.FromError(error);
            }
            var index = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var value = start + (step * index++);
                    array[row, column] = ExcelFunctionUtilities.CreateNumber(context, value);
                }
            }

            return FormulaValue.FromArray(array);
        }
    }

    internal sealed class FilterFunction : ExcelFunctionBase
    {
        public FilterFunction()
            : base("FILTER", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelLookupUtilities.TryGetArray(args[0], out var sourceArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args[1], out var includeArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            var filterRows = false;
            if (includeArray.RowCount == sourceArray.RowCount && includeArray.ColumnCount == 1)
            {
                filterRows = true;
            }
            else if (includeArray.ColumnCount == sourceArray.ColumnCount && includeArray.RowCount == 1)
            {
                filterRows = false;
            }
            else
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var indices = new List<int>();
            if (filterRows)
            {
                for (var row = 0; row < sourceArray.RowCount; row++)
                {
                    var includeValue = ExcelDynamicArrayUtilities.GetArrayValue(includeArray, row, 0);
                    if (!ExcelDynamicArrayUtilities.TryCoerceToBoolean(includeValue, out var include, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (include)
                    {
                        indices.Add(row);
                    }
                }

                if (indices.Count == 0)
                {
                    return args.Count > 2
                        ? args[2]
                        : FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                }

                var result = new FormulaArray(indices.Count, sourceArray.ColumnCount, origin: null, sparse: sourceArray.HasMask);
                for (var rowIndex = 0; rowIndex < indices.Count; rowIndex++)
                {
                    var sourceRow = indices[rowIndex];
                    for (var column = 0; column < sourceArray.ColumnCount; column++)
                    {
                        ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, sourceRow, column, result, rowIndex, column);
                    }
                }

                return FormulaValue.FromArray(result);
            }

            for (var column = 0; column < sourceArray.ColumnCount; column++)
            {
                var includeValue = ExcelDynamicArrayUtilities.GetArrayValue(includeArray, 0, column);
                if (!ExcelDynamicArrayUtilities.TryCoerceToBoolean(includeValue, out var include, out error))
                {
                    return FormulaValue.FromError(error);
                }

                if (include)
                {
                    indices.Add(column);
                }
            }

            if (indices.Count == 0)
            {
                return args.Count > 2
                    ? args[2]
                    : FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            }

            var columnResult = new FormulaArray(sourceArray.RowCount, indices.Count, origin: null, sparse: sourceArray.HasMask);
            for (var columnIndex = 0; columnIndex < indices.Count; columnIndex++)
            {
                var sourceColumn = indices[columnIndex];
                for (var row = 0; row < sourceArray.RowCount; row++)
                {
                    ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, row, sourceColumn, columnResult, row, columnIndex);
                }
            }

            return FormulaValue.FromArray(columnResult);
        }
    }

    internal sealed class SortFunction : ExcelFunctionBase
    {
        public SortFunction()
            : base("SORT", new FormulaFunctionInfo(1, 4))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelLookupUtilities.TryGetArray(args[0], out var sourceArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var maxIndex = sourceArray.ColumnCount;
            var byColumn = false;
            if (args.Count > 3)
            {
                var address = context.EvaluationContext.Address;
                var byColumnValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[3], address);
                if (!ExcelFunctionUtilities.TryCoerceToBoolean(byColumnValue, address, out byColumn, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (byColumn)
            {
                maxIndex = sourceArray.RowCount;
            }
            else if (sourceArray.RowCount == 1 && sourceArray.ColumnCount > 1)
            {
                byColumn = true;
                maxIndex = sourceArray.RowCount;
            }

            var sortIndexValue = args.Count > 1 ? args[1] : FormulaValue.FromNumber(1);
            if (!ExcelDynamicArrayUtilities.TryGetSortIndices(context, sortIndexValue, maxIndex, out var sortIndices, out error))
            {
                return FormulaValue.FromError(error);
            }

            var sortOrderValue = args.Count > 2 ? args[2] : FormulaValue.FromNumber(1);
            if (!ExcelDynamicArrayUtilities.TryGetSortOrders(context, sortOrderValue, sortIndices.Length, out var sortOrders, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (byColumn)
            {
                var indices = new int[sourceArray.ColumnCount];
                for (var index = 0; index < indices.Length; index++)
                {
                    indices[index] = index;
                }

                if (!ExcelDynamicArrayUtilities.TrySortColumns(context.EvaluationContext.Workbook.Settings, sourceArray, indices, sortIndices, sortOrders, out error))
                {
                    return FormulaValue.FromError(error);
                }

                var result = new FormulaArray(sourceArray.RowCount, sourceArray.ColumnCount, origin: null, sparse: sourceArray.HasMask);
                for (var column = 0; column < indices.Length; column++)
                {
                    var sourceColumn = indices[column];
                    for (var row = 0; row < sourceArray.RowCount; row++)
                    {
                        ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, row, sourceColumn, result, row, column);
                    }
                }

                return FormulaValue.FromArray(result);
            }

            var rowIndices = new int[sourceArray.RowCount];
            for (var index = 0; index < rowIndices.Length; index++)
            {
                rowIndices[index] = index;
            }

            if (!ExcelDynamicArrayUtilities.TrySortRows(context.EvaluationContext.Workbook.Settings, sourceArray, rowIndices, sortIndices, sortOrders, out error))
            {
                return FormulaValue.FromError(error);
            }

            var rowResult = new FormulaArray(sourceArray.RowCount, sourceArray.ColumnCount, origin: null, sparse: sourceArray.HasMask);
            for (var row = 0; row < rowIndices.Length; row++)
            {
                var sourceRow = rowIndices[row];
                for (var column = 0; column < sourceArray.ColumnCount; column++)
                {
                    ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, sourceRow, column, rowResult, row, column);
                }
            }

            return FormulaValue.FromArray(rowResult);
        }
    }

    internal sealed class UniqueFunction : ExcelFunctionBase
    {
        public UniqueFunction()
            : base("UNIQUE", new FormulaFunctionInfo(1, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelLookupUtilities.TryGetArray(args[0], out var sourceArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var address = context.EvaluationContext.Address;
            var byColumn = false;
            if (args.Count > 1)
            {
                var byColumnValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[1], address);
                if (!ExcelFunctionUtilities.TryCoerceToBoolean(byColumnValue, address, out byColumn, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            var exactlyOnce = false;
            if (args.Count > 2)
            {
                var exactlyOnceValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[2], address);
                if (!ExcelFunctionUtilities.TryCoerceToBoolean(exactlyOnceValue, address, out exactlyOnce, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (byColumn)
            {
                if (!ExcelDynamicArrayUtilities.TryBuildUniqueColumns(
                        context.EvaluationContext.Workbook.Settings,
                        sourceArray,
                        exactlyOnce,
                        out var columnIndices,
                        out error))
                {
                    return FormulaValue.FromError(error);
                }

                var result = new FormulaArray(sourceArray.RowCount, columnIndices.Count, origin: null, sparse: sourceArray.HasMask);
                for (var column = 0; column < columnIndices.Count; column++)
                {
                    var sourceColumn = columnIndices[column];
                    for (var row = 0; row < sourceArray.RowCount; row++)
                    {
                        ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, row, sourceColumn, result, row, column);
                    }
                }

                return FormulaValue.FromArray(result);
            }

            if (!ExcelDynamicArrayUtilities.TryBuildUniqueRows(
                    context.EvaluationContext.Workbook.Settings,
                    sourceArray,
                    exactlyOnce,
                    out var rowIndices,
                    out error))
            {
                return FormulaValue.FromError(error);
            }

            var rowResult = new FormulaArray(rowIndices.Count, sourceArray.ColumnCount, origin: null, sparse: sourceArray.HasMask);
            for (var row = 0; row < rowIndices.Count; row++)
            {
                var sourceRow = rowIndices[row];
                for (var column = 0; column < sourceArray.ColumnCount; column++)
                {
                    ExcelDynamicArrayUtilities.CopyArrayValue(sourceArray, sourceRow, column, rowResult, row, column);
                }
            }

            return FormulaValue.FromArray(rowResult);
        }
    }

    internal static class ExcelDynamicArrayUtilities
    {
        public static FormulaValue GetArrayValue(FormulaArray array, int row, int column)
        {
            if (array.HasMask && !array.IsPresent(row, column))
            {
                return FormulaValue.Blank;
            }

            return array[row, column];
        }

        public static void CopyArrayValue(
            FormulaArray source,
            int sourceRow,
            int sourceColumn,
            FormulaArray destination,
            int destinationRow,
            int destinationColumn)
        {
            if (source.HasMask && !source.IsPresent(sourceRow, sourceColumn))
            {
                destination.SetValue(destinationRow, destinationColumn, FormulaValue.Blank, false);
                return;
            }

            destination[destinationRow, destinationColumn] = source[sourceRow, sourceColumn];
        }

        public static bool TryCoerceToBoolean(FormulaValue value, out bool result, out FormulaError error)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                result = false;
                error = value.AsError();
                return false;
            }

            return FormulaCoercion.TryCoerceToBoolean(value, out result, out error);
        }

        public static bool TryGetSortIndices(
            FormulaFunctionContext context,
            FormulaValue value,
            int maxIndex,
            out int[] indices,
            out FormulaError error)
        {
            indices = Array.Empty<int>();
            error = default;

            var list = new List<int>();
            if (value.Kind == FormulaValueKind.Array)
            {
                foreach (var item in value.AsArray().Flatten())
                {
                    if (item.Kind == FormulaValueKind.Blank)
                    {
                        continue;
                    }

                    if (!ExcelFunctionUtilities.TryCoerceToInteger(context, item, out var parsed, out error))
                    {
                        return false;
                    }

                    if (parsed < 1 || parsed > maxIndex)
                    {
                        error = new FormulaError(FormulaErrorType.Value);
                        return false;
                    }

                    list.Add(parsed);
                }
            }
            else
            {
                if (value.Kind == FormulaValueKind.Blank)
                {
                    list.Add(1);
                }
                else
                {
                    if (!ExcelFunctionUtilities.TryCoerceToInteger(context, value, out var parsed, out error))
                    {
                        return false;
                    }

                    if (parsed < 1 || parsed > maxIndex)
                    {
                        error = new FormulaError(FormulaErrorType.Value);
                        return false;
                    }

                    list.Add(parsed);
                }
            }

            if (list.Count == 0)
            {
                list.Add(1);
            }

            indices = list.ToArray();
            return true;
        }

        public static bool TryGetSortOrders(
            FormulaFunctionContext context,
            FormulaValue value,
            int count,
            out int[] orders,
            out FormulaError error)
        {
            orders = Array.Empty<int>();
            error = default;

            var list = new List<int>();
            if (value.Kind == FormulaValueKind.Array)
            {
                foreach (var item in value.AsArray().Flatten())
                {
                    if (item.Kind == FormulaValueKind.Blank)
                    {
                        continue;
                    }

                    if (!ExcelFunctionUtilities.TryCoerceToInteger(context, item, out var parsed, out error))
                    {
                        return false;
                    }

                    if (parsed != 1 && parsed != -1)
                    {
                        error = new FormulaError(FormulaErrorType.Value);
                        return false;
                    }

                    list.Add(parsed);
                }
            }
            else
            {
                if (value.Kind == FormulaValueKind.Blank)
                {
                    list.Add(1);
                }
                else
                {
                    if (!ExcelFunctionUtilities.TryCoerceToInteger(context, value, out var parsed, out error))
                    {
                        return false;
                    }

                    if (parsed != 1 && parsed != -1)
                    {
                        error = new FormulaError(FormulaErrorType.Value);
                        return false;
                    }

                    list.Add(parsed);
                }
            }

            if (list.Count == 0)
            {
                list.Add(1);
            }

            if (list.Count == 1 && count > 1)
            {
                var fill = list[0];
                list.Clear();
                for (var i = 0; i < count; i++)
                {
                    list.Add(fill);
                }
            }
            else if (list.Count != count)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }

            orders = list.ToArray();
            return true;
        }

        public static bool TrySortRows(
            FormulaCalculationSettings settings,
            FormulaArray array,
            int[] rowIndices,
            int[] sortIndices,
            int[] sortOrders,
            out FormulaError error)
        {
            error = default;
            var compareError = default(FormulaError);
            var hasError = false;

            Array.Sort(rowIndices, (left, right) =>
            {
                if (hasError)
                {
                    return 0;
                }

                for (var i = 0; i < sortIndices.Length; i++)
                {
                    var sortIndex = sortIndices[i] - 1;
                    var leftValue = GetArrayValue(array, left, sortIndex);
                    var rightValue = GetArrayValue(array, right, sortIndex);
                    if (!TryCompareForSort(settings, leftValue, rightValue, out var comparison, out compareError))
                    {
                        hasError = true;
                        return 0;
                    }

                    if (comparison != 0)
                    {
                        return comparison * sortOrders[i];
                    }
                }

                // Preserve source order for equal sort keys.
                return left.CompareTo(right);
            });

            if (hasError)
            {
                error = compareError;
                return false;
            }

            return true;
        }

        public static bool TrySortColumns(
            FormulaCalculationSettings settings,
            FormulaArray array,
            int[] columnIndices,
            int[] sortIndices,
            int[] sortOrders,
            out FormulaError error)
        {
            error = default;
            var compareError = default(FormulaError);
            var hasError = false;

            Array.Sort(columnIndices, (left, right) =>
            {
                if (hasError)
                {
                    return 0;
                }

                for (var i = 0; i < sortIndices.Length; i++)
                {
                    var sortIndex = sortIndices[i] - 1;
                    var leftValue = GetArrayValue(array, sortIndex, left);
                    var rightValue = GetArrayValue(array, sortIndex, right);
                    if (!TryCompareForSort(settings, leftValue, rightValue, out var comparison, out compareError))
                    {
                        hasError = true;
                        return 0;
                    }

                    if (comparison != 0)
                    {
                        return comparison * sortOrders[i];
                    }
                }

                // Preserve source order for equal sort keys.
                return left.CompareTo(right);
            });

            if (hasError)
            {
                error = compareError;
                return false;
            }

            return true;
        }

        private static bool TryCompareForSort(
            FormulaCalculationSettings settings,
            FormulaValue left,
            FormulaValue right,
            out int comparison,
            out FormulaError error)
        {
            comparison = 0;
            error = default;

            if (left.Kind == FormulaValueKind.Error)
            {
                error = left.AsError();
                return false;
            }

            if (right.Kind == FormulaValueKind.Error)
            {
                error = right.AsError();
                return false;
            }

            return ExcelLookupUtilities.TryCompare(settings, left, right, out comparison, out error);
        }

        public static bool TryBuildUniqueRows(
            FormulaCalculationSettings settings,
            FormulaArray array,
            bool exactlyOnce,
            out List<int> uniqueRows,
            out FormulaError error)
        {
            if (ExcelUniqueIndexBuilder.TryBuild(settings, array, false, exactlyOnce, out uniqueRows))
            {
                error = uniqueRows.Count == 0 ? new FormulaError(FormulaErrorType.Calc) : default;
                return uniqueRows.Count > 0;
            }

            error = default;
            uniqueRows = new List<int>();
            var counts = new List<int>();

            for (var row = 0; row < array.RowCount; row++)
            {
                var foundIndex = -1;
                for (var i = 0; i < uniqueRows.Count; i++)
                {
                    if (!TryRowsEqual(settings, array, row, uniqueRows[i], out var equals, out error))
                    {
                        return false;
                    }

                    if (equals)
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (foundIndex >= 0)
                {
                    counts[foundIndex] = counts[foundIndex] + 1;
                }
                else
                {
                    uniqueRows.Add(row);
                    counts.Add(1);
                }
            }

            if (exactlyOnce)
            {
                var filtered = new List<int>();
                for (var i = 0; i < uniqueRows.Count; i++)
                {
                    if (counts[i] == 1)
                    {
                        filtered.Add(uniqueRows[i]);
                    }
                }

                uniqueRows = filtered;
            }

            if (uniqueRows.Count == 0)
            {
                error = new FormulaError(FormulaErrorType.Calc);
                return false;
            }

            return true;
        }

        public static bool TryBuildUniqueColumns(
            FormulaCalculationSettings settings,
            FormulaArray array,
            bool exactlyOnce,
            out List<int> uniqueColumns,
            out FormulaError error)
        {
            if (ExcelUniqueIndexBuilder.TryBuild(settings, array, true, exactlyOnce, out uniqueColumns))
            {
                error = uniqueColumns.Count == 0 ? new FormulaError(FormulaErrorType.Calc) : default;
                return uniqueColumns.Count > 0;
            }

            error = default;
            uniqueColumns = new List<int>();
            var counts = new List<int>();

            for (var column = 0; column < array.ColumnCount; column++)
            {
                var foundIndex = -1;
                for (var i = 0; i < uniqueColumns.Count; i++)
                {
                    if (!TryColumnsEqual(settings, array, column, uniqueColumns[i], out var equals, out error))
                    {
                        return false;
                    }

                    if (equals)
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (foundIndex >= 0)
                {
                    counts[foundIndex] = counts[foundIndex] + 1;
                }
                else
                {
                    uniqueColumns.Add(column);
                    counts.Add(1);
                }
            }

            if (exactlyOnce)
            {
                var filtered = new List<int>();
                for (var i = 0; i < uniqueColumns.Count; i++)
                {
                    if (counts[i] == 1)
                    {
                        filtered.Add(uniqueColumns[i]);
                    }
                }

                uniqueColumns = filtered;
            }

            if (uniqueColumns.Count == 0)
            {
                error = new FormulaError(FormulaErrorType.Calc);
                return false;
            }

            return true;
        }

        private static bool TryRowsEqual(
            FormulaCalculationSettings settings,
            FormulaArray array,
            int leftRow,
            int rightRow,
            out bool equals,
            out FormulaError error)
        {
            equals = true;
            error = default;

            for (var column = 0; column < array.ColumnCount; column++)
            {
                var leftValue = GetArrayValue(array, leftRow, column);
                var rightValue = GetArrayValue(array, rightRow, column);
                if (!TryValuesEqual(settings, leftValue, rightValue, out var valueEqual, out error))
                {
                    equals = false;
                    return false;
                }

                if (!valueEqual)
                {
                    equals = false;
                    return true;
                }
            }

            return true;
        }

        private static bool TryColumnsEqual(
            FormulaCalculationSettings settings,
            FormulaArray array,
            int leftColumn,
            int rightColumn,
            out bool equals,
            out FormulaError error)
        {
            equals = true;
            error = default;

            for (var row = 0; row < array.RowCount; row++)
            {
                var leftValue = GetArrayValue(array, row, leftColumn);
                var rightValue = GetArrayValue(array, row, rightColumn);
                if (!TryValuesEqual(settings, leftValue, rightValue, out var valueEqual, out error))
                {
                    equals = false;
                    return false;
                }

                if (!valueEqual)
                {
                    equals = false;
                    return true;
                }
            }

            return true;
        }

        private static bool TryValuesEqual(
            FormulaCalculationSettings settings,
            FormulaValue left,
            FormulaValue right,
            out bool equals,
            out FormulaError error)
        {
            equals = false;
            error = default;

            if (left.Kind == FormulaValueKind.Error || right.Kind == FormulaValueKind.Error)
            {
                if (left.Kind == FormulaValueKind.Error && right.Kind == FormulaValueKind.Error)
                {
                    equals = left.AsError().Type == right.AsError().Type;
                    return true;
                }

                equals = false;
                return true;
            }

            if (!ExcelLookupUtilities.TryCompare(settings, left, right, out var comparison, out error))
            {
                equals = false;
                return false;
            }

            equals = comparison == 0;
            return true;
        }
    }
}
