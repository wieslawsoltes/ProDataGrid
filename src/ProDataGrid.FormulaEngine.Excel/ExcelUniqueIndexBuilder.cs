// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelUniqueIndexBuilder
    {
        public static bool TryBuild(FormulaCalculationSettings settings, FormulaArray array,
            bool byColumn, bool exactlyOnce, out List<int> indices)
        {
            indices = null!;
            var count = byColumn ? array.ColumnCount : array.RowCount;
            var width = byColumn ? array.RowCount : array.ColumnCount;
            var hashes = new int[count];
            var hasBlankOrBoolean = false;
            var hasNonNumericText = false;
            for (var index = 0; index < count; index++)
            {
                var hash = new HashCode();
                for (var offset = 0; offset < width; offset++)
                {
                    var value = Read(array, byColumn, index, offset);
                    if (value.Kind == FormulaValueKind.Error)
                    {
                        hash.Add(2);
                        hash.Add((int)value.AsError().Type);
                        continue;
                    }
                    if (value.Kind == FormulaValueKind.Array || value.Kind == FormulaValueKind.Reference)
                    {
                        return false;
                    }
                    hasBlankOrBoolean |= value.Kind == FormulaValueKind.Blank || value.Kind == FormulaValueKind.Boolean;
                    if (FormulaCoercion.TryCoerceToNumber(value, settings, out var number, out _))
                    {
                        if (!double.IsFinite(number))
                        {
                            return false;
                        }
                        hash.Add(0);
                        hash.Add(number);
                    }
                    else if (value.Kind == FormulaValueKind.Text)
                    {
                        hasNonNumericText = true;
                        hash.Add(1);
                        hash.Add(value.AsText(), StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return false;
                    }
                }
                hashes[index] = hash.ToHashCode();
            }

            // Legacy coercion is not an equivalence relation for every mixed-type input:
            // TRUE == 1 and TRUE == "TRUE", but 1 != "TRUE". Preserve the old comparison
            // path for that domain rather than silently changing results with a hash table.
            if (hasBlankOrBoolean && hasNonNumericText)
            {
                return false;
            }

            indices = new List<int>();
            var counts = new Dictionary<int, int>(Math.Min(count, 4096),
                new IndexComparer(settings, array, byColumn, hashes));
            for (var index = 0; index < count; index++)
            {
                if (counts.TryGetValue(index, out var occurrences))
                {
                    counts[index] = occurrences + 1;
                }
                else
                {
                    counts.Add(index, 1);
                    indices.Add(index);
                }
            }
            if (exactlyOnce)
            {
                var written = 0;
                for (var i = 0; i < indices.Count; i++)
                {
                    if (counts[indices[i]] == 1)
                    {
                        indices[written++] = indices[i];
                    }
                }
                indices.RemoveRange(written, indices.Count - written);
            }
            return true;
        }

        private static FormulaValue Read(FormulaArray array, bool byColumn, int index, int offset)
            => ExcelDynamicArrayUtilities.GetArrayValue(array, byColumn ? offset : index, byColumn ? index : offset);

        private sealed class IndexComparer : IEqualityComparer<int>
        {
            private readonly FormulaCalculationSettings _settings;
            private readonly FormulaArray _array;
            private readonly bool _byColumn;
            private readonly int[] _hashes;

            public IndexComparer(FormulaCalculationSettings settings, FormulaArray array, bool byColumn, int[] hashes)
            {
                _settings = settings;
                _array = array;
                _byColumn = byColumn;
                _hashes = hashes;
            }

            public int GetHashCode(int index) => _hashes[index];

            public bool Equals(int left, int right)
            {
                if (left == right)
                {
                    return true;
                }
                var width = _byColumn ? _array.RowCount : _array.ColumnCount;
                for (var offset = 0; offset < width; offset++)
                {
                    var first = Read(_array, _byColumn, left, offset);
                    var second = Read(_array, _byColumn, right, offset);
                    if (first.Kind == FormulaValueKind.Error || second.Kind == FormulaValueKind.Error)
                    {
                        if (first.Kind != second.Kind || first.AsError().Type != second.AsError().Type)
                        {
                            return false;
                        }
                    }
                    else if (!ExcelLookupUtilities.TryCompare(_settings, first, second, out var comparison, out _) || comparison != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
