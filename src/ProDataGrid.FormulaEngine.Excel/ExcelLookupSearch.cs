// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static partial class ExcelLookupUtilities
    {
        public static bool TryFindXLookupIndex(
            FormulaCalculationSettings settings,
            FormulaValue lookupValue,
            FormulaArray lookupArray,
            bool isRowVector,
            int matchMode,
            int searchMode,
            out int index,
            out FormulaError error)
        {
            index = 0;
            error = default;
            var length = isRowVector ? lookupArray.ColumnCount : lookupArray.RowCount;
            if (length == 0)
            {
                return true;
            }

            // Binary modes require a sorted, dense vector. Masks and wildcard predicates
            // are not monotone, so retain the directional linear path for those inputs.
            if ((searchMode == 2 || searchMode == -2) && !lookupArray.HasMask && matchMode != 2)
            {
                return TryBinaryXLookup(settings, lookupValue, lookupArray, isRowVector,
                    matchMode, searchMode == 2, out index, out error);
            }

            var direction = searchMode < 0 ? -1 : 1;
            var start = direction == 1 ? 0 : length - 1;
            var end = direction == 1 ? length : -1;
            var bestIndex = -1;
            var bestValue = FormulaValue.Blank;
            for (var i = start; i != end; i += direction)
            {
                var row = isRowVector ? 0 : i;
                var column = isRowVector ? i : 0;
                if (lookupArray.HasMask && !lookupArray.IsPresent(row, column))
                {
                    continue;
                }

                var candidate = lookupArray[row, column];
                if (matchMode == 0 || matchMode == 2)
                {
                    if (!TryExactMatch(settings, lookupValue, candidate, matchMode == 2, out var match, out error))
                    {
                        return false;
                    }
                    if (match)
                    {
                        index = i + 1;
                        return true;
                    }
                    continue;
                }

                if (!TryCompare(settings, candidate, lookupValue, out var comparison, out error))
                {
                    return false;
                }
                // Exact matches take priority even when an approximate match is allowed.
                if (comparison == 0)
                {
                    index = i + 1;
                    return true;
                }
                if ((matchMode == -1 && comparison > 0) || (matchMode == 1 && comparison < 0))
                {
                    continue;
                }

                if (bestIndex >= 0)
                {
                    if (!TryCompare(settings, candidate, bestValue, out var bestComparison, out error))
                    {
                        return false;
                    }
                    // Keep the first encountered equal candidate, respecting search direction.
                    if ((matchMode == -1 && bestComparison <= 0) || (matchMode == 1 && bestComparison >= 0))
                    {
                        continue;
                    }
                }
                bestIndex = i;
                bestValue = candidate;
            }

            index = bestIndex + 1;
            return true;
        }

        private static bool TryBinaryXLookup(
            FormulaCalculationSettings settings,
            FormulaValue lookupValue,
            FormulaArray array,
            bool isRowVector,
            int matchMode,
            bool ascending,
            out int index,
            out FormulaError error)
        {
            index = 0;
            error = default;
            var length = isRowVector ? array.ColumnCount : array.RowCount;
            var low = 0;
            var high = length;
            // Lower bound also gives deterministic first-physical-position duplicate results.
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                var candidate = array[isRowVector ? 0 : middle, isRowVector ? middle : 0];
                if (!TryCompare(settings, candidate, lookupValue, out var comparison, out error))
                {
                    return false;
                }
                if (ascending ? comparison < 0 : comparison > 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            if (low < length)
            {
                var candidate = array[isRowVector ? 0 : low, isRowVector ? low : 0];
                if (!TryCompare(settings, candidate, lookupValue, out var comparison, out error))
                {
                    return false;
                }
                if (comparison == 0)
                {
                    index = low + 1;
                    return true;
                }
            }
            if (matchMode == 0)
            {
                return true;
            }

            var selected = ascending
                ? (matchMode == -1 ? low - 1 : low)
                : (matchMode == -1 ? low : low - 1);
            if (selected >= 0 && selected < length)
            {
                var candidate = array[isRowVector ? 0 : selected, isRowVector ? selected : 0];
                if (!TryCompare(settings, candidate, lookupValue, out _, out error))
                {
                    return false;
                }
                index = selected + 1;
            }
            return true;
        }
    }
}
