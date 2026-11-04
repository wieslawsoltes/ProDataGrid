// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelTextIndexing
    {
        public static bool UsesPairs(FormulaCalculationSettings settings)
            => settings.TextCompatibilityVersion == FormulaTextCompatibilityVersion.Version2;

        public static int Length(string text, FormulaCalculationSettings settings)
        {
            if (!UsesPairs(settings)) return text.Length;
            var count = 0;
            for (var i = 0; i < text.Length; i = Next(text, i, true)) count++;
            return count;
        }

        public static int Next(string text, int index, bool pairs)
            => index + (pairs && char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1);

        // Clamp before arithmetic. Very large counts must never overflow into a negative index.
        public static int Advance(string text, int index, int count, bool pairs)
        {
            if (!pairs) return index + Math.Min(count, text.Length - index);
            while (count > 0 && index < text.Length) { index = Next(text, index, true); count--; }
            return index;
        }

        public static int Position(string text, int offset, bool pairs)
        {
            if (!pairs) return offset + 1;
            var position = 1;
            for (var i = 0; i < offset; i = Next(text, i, true)) position++;
            return position;
        }

        public static string Prefix(string text, int count, FormulaCalculationSettings settings)
            => text.Substring(0, Advance(text, 0, count, UsesPairs(settings)));

        public static string Suffix(string text, int count, FormulaCalculationSettings settings)
        {
            if (!UsesPairs(settings)) return text.Substring(text.Length - Math.Min(count, text.Length));
            var index = text.Length;
            while (count > 0 && index > 0)
            {
                index--;
                if (char.IsLowSurrogate(text[index]) && index > 0 && char.IsHighSurrogate(text[index - 1])) index--;
                count--;
            }
            return text.Substring(index);
        }

        public static string Middle(string text, int start, int count, FormulaCalculationSettings settings)
        {
            var pairs = UsesPairs(settings);
            var first = Advance(text, 0, start - 1, pairs);
            var end = Advance(text, first, count, pairs);
            return text.Substring(first, end - first);
        }
    }
}
