// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class FindTextFunction : ExcelElementwiseFunction
    {
        private readonly bool _wildcards;
        public FindTextFunction(bool wildcards) : base(wildcards ? "SEARCH" : "FIND", 2, 3) { _wildcards = wildcards; }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var pattern, out var error) ||
                !ExcelTextUtilities.TryText(args[1], out var text, out error)) return FormulaValue.FromError(error);
            var start = 1;
            if (!args.IsOmitted(2) && !ExcelTextUtilities.TryInteger(context, args[2], out start, out error)) return FormulaValue.FromError(error);
            var settings = context.EvaluationContext.Workbook.Settings;
            var length = ExcelTextIndexing.Length(text, settings);
            if (start < 1 || start > Math.Max(length, 1))
                return ExcelTextUtilities.ValueError();
            var pairs = ExcelTextIndexing.UsesPairs(settings);
            var offset = ExcelTextIndexing.Advance(text, 0, start - 1, pairs);
            var found = _wildcards ? ExcelWildcardSearch.Find(text, pattern, offset, pairs) : text.IndexOf(pattern, offset, StringComparison.Ordinal);
            return found < 0 ? ExcelTextUtilities.ValueError()
                : FormulaValue.FromNumber(ExcelTextIndexing.Position(text, found, pairs));
        }
    }

    internal sealed class DelimitedTextFunction : ExcelElementwiseFunction
    {
        private readonly bool _after;
        public DelimitedTextFunction(bool after) : base(after ? "TEXTAFTER" : "TEXTBEFORE", 2, 6) { _after = after; }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error) ||
                !ExcelTextUtilities.TryText(args[1], out var delimiter, out error)) return FormulaValue.FromError(error);
            var instance = 1;
            var matchMode = 0;
            var matchEnd = 0;
            if ((!args.IsOmitted(2) && !ExcelTextUtilities.TryInteger(context, args[2], out instance, out error)) ||
                (!args.IsOmitted(3) && !ExcelTextUtilities.TryInteger(context, args[3], out matchMode, out error)) ||
                (!args.IsOmitted(4) && !ExcelTextUtilities.TryInteger(context, args[4], out matchEnd, out error)))
                return FormulaValue.FromError(error);
            if (instance == 0 || matchMode < 0 || matchMode > 1 || matchEnd < 0 || matchEnd > 1)
                return ExcelTextUtilities.ValueError();
            if (text.Length == 0) return FormulaValue.FromText(string.Empty);
            var remaining = Math.Abs((long)instance);
            if (remaining > text.Length) return ExcelTextUtilities.ValueError();
            if (delimiter.Length == 0)
                return ExcelTextUtilities.Text((_after == (instance > 0)) ? text : string.Empty);
            var comparison = matchMode == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var from = instance > 0 ? 0 : text.Length;
            var index = -1;
            var delimiterLength = delimiter.Length;
            while (remaining > 0)
            {
                var found = instance > 0 ? text.AsSpan(from).IndexOf(delimiter.AsSpan(), comparison)
                    : text.AsSpan(0, from).LastIndexOf(delimiter.AsSpan(), comparison);
                if (found < 0)
                {
                    if (matchEnd == 1 && remaining == 1) { index = instance > 0 ? text.Length : 0; delimiterLength = 0; }
                    break;
                }
                index = instance > 0 ? from + found : found;
                from = instance > 0 ? index + delimiter.Length : index;
                remaining--;
                if (remaining > 0) index = -1;
            }
            if (index < 0)
                return args.IsOmitted(5) ? FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)) : args[5];
            return ExcelTextUtilities.Text(_after ? text.Substring(index + delimiterLength) : text.Substring(0, index));
        }
    }
}
