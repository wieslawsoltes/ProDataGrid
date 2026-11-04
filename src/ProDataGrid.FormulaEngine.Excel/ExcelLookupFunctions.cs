// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class IndexFunction : ExcelFunctionBase
    {
        public IndexFunction()
            : base("INDEX", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;

            if (!ExcelLookupUtilities.TryGetArray(args[0], out var array, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var rowValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[1], address);
            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, rowValue, out var rowIndex, out error))
            {
                return FormulaValue.FromError(error);
            }

            int? columnIndex = null;
            if (args.Count > 2)
            {
                var columnValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[2], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, columnValue, out var parsedColumn, out error))
                {
                    return FormulaValue.FromError(error);
                }

                columnIndex = parsedColumn;
            }

            return ExcelLookupUtilities.Index(array, rowIndex, columnIndex);
        }
    }

    internal sealed class MatchFunction : ExcelFunctionBase
    {
        public MatchFunction()
            : base("MATCH", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var lookupValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[0], address);

            if (!ExcelLookupUtilities.TryGetVector(args[1], out var array, out var isRowVector, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var matchType = 1;
            if (args.Count > 2)
            {
                var matchValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[2], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, matchValue, out matchType, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (matchType != 0 && matchType != 1 && matchType != -1)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var length = isRowVector ? array.ColumnCount : array.RowCount;
            if (length == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            }

            var useWildcard = lookupValue.Kind == FormulaValueKind.Text &&
                ExcelCriteriaUtilities.ContainsWildcard(lookupValue.AsText());

            var bestIndex = 0;
            for (var index = 0; index < length; index++)
            {
                var row = isRowVector ? 0 : index;
                var column = isRowVector ? index : 0;
                if (array.HasMask && !array.IsPresent(row, column))
                {
                    continue;
                }

                var candidate = array[row, column];
                if (matchType == 0)
                {
                    if (!ExcelLookupUtilities.TryExactMatch(context.EvaluationContext.Workbook.Settings, lookupValue, candidate, useWildcard, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (match)
                    {
                        return ExcelFunctionUtilities.CreateNumber(context, index + 1);
                    }

                    continue;
                }

                if (!ExcelLookupUtilities.TryCompare(context.EvaluationContext.Workbook.Settings, candidate, lookupValue, out var comparison, out error))
                {
                    return FormulaValue.FromError(error);
                }

                if (matchType == 1)
                {
                    if (comparison <= 0)
                    {
                        bestIndex = index + 1;
                    }
                }
                else if (comparison >= 0)
                {
                    bestIndex = index + 1;
                }
            }

            if (bestIndex == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            }

            return ExcelFunctionUtilities.CreateNumber(context, bestIndex);
        }
    }

    internal sealed class VLookupFunction : ExcelFunctionBase
    {
        public VLookupFunction()
            : base("VLOOKUP", new FormulaFunctionInfo(3, 4))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var lookupValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[0], address);

            if (!ExcelLookupUtilities.TryGetArray(args[1], out var table, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var columnValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[2], address);
            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, columnValue, out var columnIndex, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (columnIndex < 1 || columnIndex > table.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            var approximate = true;
            if (args.Count > 3)
            {
                var rangeLookup = ExcelLookupUtilities.ApplyImplicitIntersection(args[3], address);
                if (!ExcelFunctionUtilities.TryCoerceToBoolean(rangeLookup, address, out approximate, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            var useWildcard = !approximate &&
                lookupValue.Kind == FormulaValueKind.Text &&
                ExcelCriteriaUtilities.ContainsWildcard(lookupValue.AsText());

            var rowIndex = 0;
            for (var row = 0; row < table.RowCount; row++)
            {
                if (table.HasMask && !table.IsPresent(row, 0))
                {
                    continue;
                }

                var candidate = table[row, 0];
                if (approximate)
                {
                    if (!ExcelLookupUtilities.TryCompare(context.EvaluationContext.Workbook.Settings, candidate, lookupValue, out var comparison, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (comparison <= 0)
                    {
                        rowIndex = row + 1;
                    }
                }
                else
                {
                    if (!ExcelLookupUtilities.TryExactMatch(context.EvaluationContext.Workbook.Settings, lookupValue, candidate, useWildcard, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (match)
                    {
                        rowIndex = row + 1;
                        break;
                    }
                }
            }

            if (rowIndex == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            }

            return ExcelLookupUtilities.GetValueAt(table, rowIndex - 1, columnIndex - 1);
        }
    }

    internal sealed class HLookupFunction : ExcelFunctionBase
    {
        public HLookupFunction()
            : base("HLOOKUP", new FormulaFunctionInfo(3, 4))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var lookupValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[0], address);

            if (!ExcelLookupUtilities.TryGetArray(args[1], out var table, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var rowValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[2], address);
            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, rowValue, out var rowIndex, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (rowIndex < 1 || rowIndex > table.RowCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            var approximate = true;
            if (args.Count > 3)
            {
                var rangeLookup = ExcelLookupUtilities.ApplyImplicitIntersection(args[3], address);
                if (!ExcelFunctionUtilities.TryCoerceToBoolean(rangeLookup, address, out approximate, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            var useWildcard = !approximate &&
                lookupValue.Kind == FormulaValueKind.Text &&
                ExcelCriteriaUtilities.ContainsWildcard(lookupValue.AsText());

            var columnIndex = 0;
            for (var column = 0; column < table.ColumnCount; column++)
            {
                if (table.HasMask && !table.IsPresent(0, column))
                {
                    continue;
                }

                var candidate = table[0, column];
                if (approximate)
                {
                    if (!ExcelLookupUtilities.TryCompare(context.EvaluationContext.Workbook.Settings, candidate, lookupValue, out var comparison, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (comparison <= 0)
                    {
                        columnIndex = column + 1;
                    }
                }
                else
                {
                    if (!ExcelLookupUtilities.TryExactMatch(context.EvaluationContext.Workbook.Settings, lookupValue, candidate, useWildcard, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (match)
                    {
                        columnIndex = column + 1;
                        break;
                    }
                }
            }

            if (columnIndex == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            }

            return ExcelLookupUtilities.GetValueAt(table, rowIndex - 1, columnIndex - 1);
        }
    }

    internal sealed class XMatchFunction : ExcelFunctionBase
    {
        public XMatchFunction()
            : base("XMATCH", new FormulaFunctionInfo(2, 4))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var lookupValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[0], address);

            if (!ExcelLookupUtilities.TryGetVector(args[1], out var lookupArray, out var isRowVector, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var matchMode = 0;
            if (args.Count > 2)
            {
                var matchValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[2], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, matchValue, out matchMode, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (matchMode != 0 && matchMode != -1 && matchMode != 1 && matchMode != 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var searchMode = 1;
            if (args.Count > 3)
            {
                var searchValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[3], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, searchValue, out searchMode, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (searchMode != 1 && searchMode != -1 && searchMode != 2 && searchMode != -2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelLookupUtilities.TryFindXLookupIndex(
                    context.EvaluationContext.Workbook.Settings,
                    lookupValue,
                    lookupArray,
                    isRowVector,
                    matchMode,
                    searchMode,
                    out var index,
                    out error))
            {
                return FormulaValue.FromError(error);
            }

            if (index == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            }

            return ExcelFunctionUtilities.CreateNumber(context, index);
        }
    }

    internal sealed class XLookupFunction : ExcelFunctionBase
    {
        public XLookupFunction()
            : base("XLOOKUP", new FormulaFunctionInfo(3, 6))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var lookupValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[0], address);

            if (!ExcelLookupUtilities.TryGetVector(args[1], out var lookupArray, out var isRowVector, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args[2], out var returnArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            var length = isRowVector ? lookupArray.ColumnCount : lookupArray.RowCount;
            if (isRowVector)
            {
                if (returnArray.ColumnCount != length)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
            }
            else if (returnArray.RowCount != length)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var notFound = args.Count > 3
                ? args[3]
                : FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));

            var matchMode = 0;
            if (args.Count > 4)
            {
                var matchValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[4], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, matchValue, out matchMode, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (matchMode != 0 && matchMode != -1 && matchMode != 1 && matchMode != 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var searchMode = 1;
            if (args.Count > 5)
            {
                var searchValue = ExcelLookupUtilities.ApplyImplicitIntersection(args[5], address);
                if (!ExcelFunctionUtilities.TryCoerceToInteger(context, searchValue, out searchMode, out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (searchMode != 1 && searchMode != -1 && searchMode != 2 && searchMode != -2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelLookupUtilities.TryFindXLookupIndex(
                    context.EvaluationContext.Workbook.Settings,
                    lookupValue,
                    lookupArray,
                    isRowVector,
                    matchMode,
                    searchMode,
                    out var index,
                    out error))
            {
                return FormulaValue.FromError(error);
            }

            if (index == 0)
            {
                return notFound;
            }

            return ExcelLookupUtilities.GetReturnValue(returnArray, isRowVector, index);
        }
    }

    internal static partial class ExcelLookupUtilities
    {
        public static FormulaValue ApplyImplicitIntersection(FormulaValue value, FormulaCellAddress address)
        {
            return FormulaCoercion.ApplyImplicitIntersection(value, address);
        }

        public static bool TryGetArray(FormulaValue value, out FormulaArray array, out FormulaError error)
        {
            error = default;
            switch (value.Kind)
            {
                case FormulaValueKind.Error:
                    array = null!;
                    error = value.AsError();
                    return false;
                case FormulaValueKind.Array:
                    array = value.AsArray();
                    return true;
                case FormulaValueKind.Reference:
                    array = null!;
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
                default:
                    array = new FormulaArray(1, 1);
                    array[0, 0] = value;
                    return true;
            }
        }

        public static bool TryGetVector(
            FormulaValue value,
            out FormulaArray array,
            out bool isRowVector,
            out FormulaError error)
        {
            if (!TryGetArray(value, out array, out error))
            {
                isRowVector = false;
                return false;
            }

            if (array.RowCount == 1)
            {
                isRowVector = true;
                return true;
            }

            if (array.ColumnCount == 1)
            {
                isRowVector = false;
                return true;
            }

            error = new FormulaError(FormulaErrorType.Value);
            isRowVector = false;
            return false;
        }

        public static bool TryCompare(
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

            if (FormulaCoercion.TryCoerceToNumber(left, settings, out var leftNumber, out _) &&
                FormulaCoercion.TryCoerceToNumber(right, settings, out var rightNumber, out _))
            {
                comparison = leftNumber.CompareTo(rightNumber);
                return true;
            }

            if (!FormulaCoercion.TryCoerceToText(left, out var leftText, out var leftError))
            {
                error = leftError;
                return false;
            }

            if (!FormulaCoercion.TryCoerceToText(right, out var rightText, out var rightError))
            {
                error = rightError;
                return false;
            }

            comparison = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public static bool TryExactMatch(
            FormulaCalculationSettings settings,
            FormulaValue lookupValue,
            FormulaValue candidate,
            bool useWildcard,
            out bool match,
            out FormulaError error)
        {
            match = false;
            error = default;

            if (useWildcard && lookupValue.Kind == FormulaValueKind.Text)
            {
                if (!ExcelFunctionUtilities.TryCoerceToText(candidate, out var text, out error))
                {
                    return false;
                }

                var pattern = lookupValue.AsText();
                if (ExcelCriteriaUtilities.ContainsWildcard(pattern))
                {
                    match = ExcelCriteriaUtilities.MatchesWildcard(text, pattern);
                    return true;
                }
            }

            if (!TryCompare(settings, candidate, lookupValue, out var comparison, out error))
            {
                return false;
            }

            match = comparison == 0;
            return true;
        }

        public static FormulaValue Index(FormulaArray array, int rowIndex, int? columnIndex)
        {
            if (columnIndex == null)
            {
                if (array.RowCount == 1 && array.ColumnCount > 1)
                {
                    columnIndex = rowIndex;
                    rowIndex = 1;
                }
                else
                {
                    columnIndex = 1;
                }
            }

            if (rowIndex < 0 || columnIndex < 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (rowIndex == 0 && columnIndex == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (rowIndex == 0)
            {
                return SliceColumn(array, columnIndex.Value);
            }

            if (columnIndex == 0)
            {
                return SliceRow(array, rowIndex);
            }

            if (rowIndex > array.RowCount || columnIndex > array.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            return GetValueAt(array, rowIndex - 1, columnIndex.Value - 1);
        }

        public static FormulaValue SliceRow(FormulaArray array, int rowIndex)
        {
            if (rowIndex < 1 || rowIndex > array.RowCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            var origin = array.Origin;
            if (origin != null)
            {
                origin = new FormulaCellAddress(origin.Value.SheetName, origin.Value.Row + rowIndex - 1, origin.Value.Column);
            }

            var result = new FormulaArray(1, array.ColumnCount, origin, array.HasMask);
            var row = rowIndex - 1;
            for (var column = 0; column < array.ColumnCount; column++)
            {
                if (array.HasMask && !array.IsPresent(row, column))
                {
                    result.SetValue(0, column, FormulaValue.Blank, false);
                    continue;
                }

                result[0, column] = array[row, column];
            }

            return FormulaValue.FromArray(result);
        }

        public static FormulaValue SliceColumn(FormulaArray array, int columnIndex)
        {
            if (columnIndex < 1 || columnIndex > array.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            var origin = array.Origin;
            if (origin != null)
            {
                origin = new FormulaCellAddress(origin.Value.SheetName, origin.Value.Row, origin.Value.Column + columnIndex - 1);
            }

            var result = new FormulaArray(array.RowCount, 1, origin, array.HasMask);
            var column = columnIndex - 1;
            for (var row = 0; row < array.RowCount; row++)
            {
                if (array.HasMask && !array.IsPresent(row, column))
                {
                    result.SetValue(row, 0, FormulaValue.Blank, false);
                    continue;
                }

                result[row, 0] = array[row, column];
            }

            return FormulaValue.FromArray(result);
        }

        public static FormulaValue GetValueAt(FormulaArray array, int row, int column)
        {
            if (array.HasMask && !array.IsPresent(row, column))
            {
                return FormulaValue.Blank;
            }

            return array[row, column];
        }

        public static FormulaValue GetReturnValue(FormulaArray returnArray, bool lookupIsRowVector, int index)
        {
            if (lookupIsRowVector)
            {
                if (returnArray.RowCount == 1)
                {
                    return GetValueAt(returnArray, 0, index - 1);
                }

                return SliceColumn(returnArray, index);
            }

            if (returnArray.ColumnCount == 1)
            {
                return GetValueAt(returnArray, index - 1, 0);
            }

            return SliceRow(returnArray, index);
        }
    }
}
