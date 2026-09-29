// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal readonly struct ExcelSplitDelimiters
    {
        private readonly string? _single;
        private readonly string[]? _multiple;
        private readonly int _count;

        private ExcelSplitDelimiters(string single) { _single = single; _multiple = null; _count = 1; }
        private ExcelSplitDelimiters(string[] multiple, int count) { _single = null; _multiple = multiple; _count = count; }
        public bool IsEmpty => _count == 0;

        public static bool TryCreate(FormulaValue value, int limit, out ExcelSplitDelimiters delimiters, out FormulaError error)
        {
            delimiters = default;
            error = default;
            if (value.Kind != FormulaValueKind.Array)
            {
                if (!ExcelTextUtilities.TryText(value, out var text, out error)) return false;
                if (text.Length != 0) delimiters = new ExcelSplitDelimiters(text);
                return true;
            }
            var array = value.AsArray();
            var length = (long)array.RowCount * array.ColumnCount;
            if (length > limit) { error = new FormulaError(FormulaErrorType.Num); return false; }
            var entries = new string[(int)length];
            var count = 0;
            for (var row = 0; row < array.RowCount; row++)
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    if (!ExcelTextUtilities.TryText(ExcelDynamicArrayUtilities.GetArrayValue(array, row, column), out var text, out error)) return false;
                    // Empty delimiters cannot consume input. Ignore these entries; at least
                    // one nonempty delimiter must exist across the two delimiter sets.
                    if (text.Length != 0) entries[count++] = text;
                }
            delimiters = count == 1 ? new ExcelSplitDelimiters(entries[0]) : new ExcelSplitDelimiters(entries, count);
            return true;
        }

        public int Find(ReadOnlySpan<char> text, StringComparison comparison, out int length)
        {
            length = 0;
            if (_count == 0) return -1;
            if (_single != null) { length = _single.Length; return text.IndexOf(_single.AsSpan(), comparison); }
            // Advance through the input once, checking candidates in declaration order.
            // Searching the entire remaining text separately for every delimiter on each
            // token would become quadratic when one delimiter is frequent and another absent.
            for (var position = 0; position < text.Length; position++)
            {
                var remaining = text.Slice(position);
                for (var i = 0; i < _count; i++)
                {
                    var item = _multiple![i];
                    if (remaining.StartsWith(item.AsSpan(), comparison))
                    {
                        length = item.Length;
                        return position;
                    }
                }
            }
            return -1;
        }
    }

    internal ref struct ExcelSplitSegments
    {
        private readonly ReadOnlySpan<char> _text;
        private readonly ExcelSplitDelimiters _delimiters;
        private readonly StringComparison _comparison;
        private readonly bool _ignoreEmpty;
        private int _next;
        private bool _finished;

        public ExcelSplitSegments(ReadOnlySpan<char> text, ExcelSplitDelimiters delimiters, StringComparison comparison, bool ignoreEmpty)
        {
            _text = text; _delimiters = delimiters; _comparison = comparison; _ignoreEmpty = ignoreEmpty;
            _next = 0; _finished = false; Start = 0; Length = 0;
        }
        public int Start { get; private set; }
        public int Length { get; private set; }

        public bool MoveNext()
        {
            while (!_finished)
            {
                Start = _next;
                var match = _delimiters.Find(_text.Slice(_next), _comparison, out var delimiterLength);
                if (match < 0) { Length = _text.Length - _next; _finished = true; }
                else { Length = match; _next += match + delimiterLength; }
                if (!_ignoreEmpty || Length != 0) return true;
            }
            return false;
        }
    }
}
