// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Text;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelWildcardSearch
    {
        private const int Star = -1;
        private const int Any = -2;

        public static int Find(string text, string pattern, int start, bool pairs)
        {
            // The common literal search uses the runtime's optimized ordinal search and allocates nothing.
            if (pattern.AsSpan().IndexOfAny('*', '?', '~') < 0)
                return text.IndexOf(pattern, start, StringComparison.OrdinalIgnoreCase);

            int[]? rented = null;
            Span<int> tokens = pattern.Length <= 128 ? stackalloc int[128]
                : (rented = ArrayPool<int>.Shared.Rent(pattern.Length));
            try
            {
                var count = 0;
                for (var i = 0; i < pattern.Length;)
                {
                    var ch = pattern[i];
                    if (ch == '*' && (count == 0 || tokens[count - 1] != Star)) { tokens[count++] = Star; i++; }
                    else if (ch == '*') i++;
                    else if (ch == '?') { tokens[count++] = Any; i++; }
                    else
                    {
                        if (ch == '~' && i + 1 < pattern.Length && (pattern[i + 1] == '*' || pattern[i + 1] == '?' || pattern[i + 1] == '~')) i++;
                        tokens[count++] = ReadFolded(pattern, ref i, pairs);
                    }
                }

                // Ordered star-separated segments can be matched greedily: picking an earlier
                // segment never leaves less room for later segments. No recursive backtracking.
                var position = start;
                var answer = count > 0 && tokens[0] == Star ? start : -1;
                for (var token = 0; token < count;)
                {
                    if (tokens[token] == Star) { token++; continue; }
                    var end = token;
                    while (end < count && tokens[end] != Star) end++;
                    var match = FindSegment(text, position, tokens.Slice(token, end - token), pairs, out var after);
                    if (match < 0) return -1;
                    if (answer < 0) answer = match;
                    position = after;
                    token = end;
                }
                return answer < 0 ? start : answer;
            }
            finally
            {
                if (rented != null) ArrayPool<int>.Shared.Return(rented);
            }
        }

        private static int FindSegment(string text, int start, ReadOnlySpan<int> segment, bool pairs, out int after)
        {
            for (var candidate = start; candidate < text.Length; candidate = ExcelTextIndexing.Next(text, candidate, pairs))
            {
                var cursor = candidate;
                var i = 0;
                for (; i < segment.Length && cursor < text.Length; i++)
                {
                    var value = ReadFolded(text, ref cursor, pairs);
                    if (segment[i] != Any && segment[i] != value) break;
                }
                if (i == segment.Length) { after = cursor; return candidate; }
            }
            after = start;
            return -1;
        }

        private static int ReadFolded(string text, ref int index, bool pairs)
        {
            var next = ExcelTextIndexing.Next(text, index, pairs);
            if (next - index == 2)
            {
                var rune = new Rune(char.ConvertToUtf32(text[index], text[index + 1]));
                index = next;
                return Rune.ToUpperInvariant(rune).Value;
            }
            return char.ToUpperInvariant(text[index++]);
        }
    }
}
