// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Text;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class ExactTextFunction : ExcelElementwiseFunction
    {
        public ExactTextFunction() : base("EXACT", 2, 2) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var left, out var error) ||
                !ExcelTextUtilities.TryText(args[1], out var right, out error)) return FormulaValue.FromError(error);
            return FormulaValue.FromBoolean(string.Equals(left, right, StringComparison.Ordinal));
        }
    }

    internal sealed class CleanTextFunction : ExcelElementwiseFunction
    {
        public CleanTextFunction() : base("CLEAN", 1, 1) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error)) return FormulaValue.FromError(error);
            var count = 0;
            for (var i = 0; i < text.Length; i++) if (text[i] >= 32) count++;
            if (count > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
            if (count == text.Length) return FormulaValue.FromText(text);
            return FormulaValue.FromText(string.Create(count, text, static (span, source) =>
            {
                var next = 0;
                for (var i = 0; i < source.Length; i++) if (source[i] >= 32) span[next++] = source[i];
            }));
        }
    }

    internal sealed class ProperTextFunction : ExcelElementwiseFunction
    {
        public ProperTextFunction() : base("PROPER", 1, 1) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error)) return FormulaValue.FromError(error);
            if (text.Length > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
            var culture = context.EvaluationContext.Workbook.Settings.Culture ?? CultureInfo.InvariantCulture;
            var lower = text.ToLower(culture);
            return FormulaValue.FromText(string.Create(lower.Length, (lower, culture), static (span, state) =>
            {
                state.lower.AsSpan().CopyTo(span);
                var first = true;
                for (var i = 0; i < span.Length; i++)
                {
                    if (char.IsHighSurrogate(span[i]) && i + 1 < span.Length && char.IsLowSurrogate(span[i + 1]))
                    {
                        var rune = new Rune(char.ConvertToUtf32(span[i], span[i + 1]));
                        var letter = Rune.IsLetter(rune);
                        if (letter && first) Rune.ToUpperInvariant(rune).EncodeToUtf16(span.Slice(i, 2));
                        first = !letter;
                        i++;
                    }
                    else
                    {
                        var letter = char.IsLetter(span[i]);
                        if (letter && first) span[i] = char.ToUpper(span[i], state.culture);
                        first = !letter;
                    }
                }
            }));
        }
    }

    internal sealed class RepeatTextFunction : ExcelElementwiseFunction
    {
        public RepeatTextFunction() : base("REPT", 2, 2) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error) ||
                !ExcelTextUtilities.TryInteger(context, args[1], out var count, out error)) return FormulaValue.FromError(error);
            if (count < 0 || (long)text.Length * count > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
            var length = text.Length * count;
            if (length == 0) return FormulaValue.FromText(string.Empty);
            if (count == 1) return FormulaValue.FromText(text);
            // Fill by doubling already-copied spans: one final string and no repeated concatenation.
            return FormulaValue.FromText(string.Create(length, text, static (span, source) =>
            {
                source.AsSpan().CopyTo(span);
                var filled = source.Length;
                while (filled < span.Length)
                {
                    var copy = Math.Min(filled, span.Length - filled);
                    span.Slice(0, copy).CopyTo(span.Slice(filled));
                    filled += copy;
                }
            }));
        }
    }

    internal sealed class ReplaceTextFunction : ExcelElementwiseFunction
    {
        public ReplaceTextFunction() : base("REPLACE", 4, 4) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error) ||
                !ExcelTextUtilities.TryInteger(context, args[1], out var start, out error) ||
                !ExcelTextUtilities.TryInteger(context, args[2], out var count, out error) ||
                !ExcelTextUtilities.TryText(args[3], out var replacement, out error)) return FormulaValue.FromError(error);
            if (start < 1 || count < 0) return ExcelTextUtilities.ValueError();
            var pairs = ExcelTextIndexing.UsesPairs(context.EvaluationContext.Workbook.Settings);
            var first = ExcelTextIndexing.Advance(text, 0, start - 1, pairs);
            var end = ExcelTextIndexing.Advance(text, first, count, pairs);
            if ((long)text.Length - (end - first) + replacement.Length > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
            return FormulaValue.FromText(string.Concat(text.AsSpan(0, first), replacement.AsSpan(), text.AsSpan(end)));
        }
    }

    internal sealed class SubstituteTextFunction : ExcelElementwiseFunction
    {
        public SubstituteTextFunction() : base("SUBSTITUTE", 3, 4) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error) ||
                !ExcelTextUtilities.TryText(args[1], out var oldText, out error) ||
                !ExcelTextUtilities.TryText(args[2], out var newText, out error)) return FormulaValue.FromError(error);
            var instance = 0;
            if (!args.IsOmitted(3))
            {
                if (!ExcelTextUtilities.TryInteger(context, args[3], out instance, out error)) return FormulaValue.FromError(error);
                if (instance < 1) return ExcelTextUtilities.ValueError();
            }
            if (oldText.Length == 0 || oldText == newText) return ExcelTextUtilities.Text(text);
            var count = 0;
            var offset = 0;
            var selected = -1;
            while (offset <= text.Length - oldText.Length)
            {
                var found = text.IndexOf(oldText, offset, StringComparison.Ordinal);
                if (found < 0) break;
                count++;
                if (instance == count) { selected = found; break; }
                offset = found + oldText.Length;
            }
            if (count == 0 || (instance > 0 && selected < 0)) return ExcelTextUtilities.Text(text);
            var replacements = instance == 0 ? count : 1;
            var outputLength = (long)text.Length + (long)replacements * (newText.Length - oldText.Length);
            if (outputLength > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
            if (instance > 0)
                return FormulaValue.FromText(string.Concat(text.AsSpan(0, selected), newText.AsSpan(), text.AsSpan(selected + oldText.Length)));
            return FormulaValue.FromText(string.Create((int)outputLength, (text, oldText, newText), static (span, state) =>
            {
                var read = 0;
                var written = 0;
                while (true)
                {
                    var found = state.text.IndexOf(state.oldText, read, StringComparison.Ordinal);
                    if (found < 0) break;
                    state.text.AsSpan(read, found - read).CopyTo(span.Slice(written));
                    written += found - read;
                    state.newText.AsSpan().CopyTo(span.Slice(written));
                    written += state.newText.Length;
                    read = found + state.oldText.Length;
                }
                state.text.AsSpan(read).CopyTo(span.Slice(written));
            }));
        }
    }
}
