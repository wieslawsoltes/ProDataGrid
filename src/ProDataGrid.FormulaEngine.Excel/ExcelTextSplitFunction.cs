// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class TextSplitFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public TextSplitFunction() : base("TEXTSPLIT", new FormulaFunctionInfo(2, 6)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => Split(context, args, 0);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 2 || args.Count > 6) return ExcelTextUtilities.ValueError();
            var values = new FormulaValue[args.Count];
            uint omitted = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (context.EvaluationContext.IsArgumentOmitted(args[i])) omitted |= 1u << i;
                values[i] = evaluator.Evaluate(args[i], context.EvaluationContext, resolver);
            }
            return Split(context, values, omitted);
        }

        private static FormulaValue Split(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args, uint omitted)
        {
            if (args.Count < 2 || args.Count > 6) return ExcelTextUtilities.ValueError();
            var address = context.EvaluationContext.Address;
            // Preserve the existing scalar source/options intersection contract. Delimiter
            // arrays, unlike scalar options, are consumed in full by the new matcher.
            var source = FormulaCoercion.ApplyImplicitIntersection(args[0], address);
            if (!ExcelTextUtilities.TryText(source, out var text, out var error)) return FormulaValue.FromError(error);
            var settings = context.EvaluationContext.Workbook.Settings;
            if (!ExcelSplitDelimiters.TryCreate(args[1], settings.MaximumArrayCellCount, out var columns, out error) ||
                !ExcelSplitDelimiters.TryCreate(IsOmitted(args, omitted, 2) ? FormulaValue.Blank : args[2],
                    settings.MaximumArrayCellCount, out var rows, out error)) return FormulaValue.FromError(error);
            if (columns.IsEmpty && rows.IsEmpty) return ExcelTextUtilities.ValueError();
            var ignoreEmpty = false;
            if (!IsOmitted(args, omitted, 3) && !FormulaCoercion.TryCoerceToBoolean(
                    FormulaCoercion.ApplyImplicitIntersection(args[3], address), out ignoreEmpty, out error)) return FormulaValue.FromError(error);
            var mode = 0;
            if (!IsOmitted(args, omitted, 4) && !ExcelTextUtilities.TryInteger(context,
                    FormulaCoercion.ApplyImplicitIntersection(args[4], address), out mode, out error)) return FormulaValue.FromError(error);
            if (mode != 0 && mode != 1) return ExcelTextUtilities.ValueError();
            var pad = IsOmitted(args, omitted, 5) ? FormulaValue.FromError(new FormulaError(FormulaErrorType.NA))
                : FormulaCoercion.ApplyImplicitIntersection(args[5], address);
            if (pad.Kind is FormulaValueKind.Array or FormulaValueKind.Reference) return ExcelTextUtilities.ValueError();
            var comparison = mode == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (text.Length == 0) return ExcelArrayShapeUtilities.Error(FormulaErrorType.Calc);

            // Count without constructing substrings, token lists or per-row arrays. Reject
            // an oversized rectangular result before allocating any output or token strings.
            var rowCount = 0;
            var columnCount = 0;
            var rowParts = new ExcelSplitSegments(text.AsSpan(), rows, comparison, ignoreEmpty);
            while (rowParts.MoveNext())
            {
                var count = 0;
                var columnParts = new ExcelSplitSegments(text.AsSpan(rowParts.Start, rowParts.Length), columns, comparison, ignoreEmpty);
                while (columnParts.MoveNext())
                {
                    if (columnParts.Length > ExcelTextUtilities.MaximumLength) return ExcelTextUtilities.ValueError();
                    if (++count > 16384 || count > settings.MaximumArrayCellCount) return ExcelNumericUtilities.NumError();
                }
                columnCount = Math.Max(columnCount, count);
                if (++rowCount > 1048576 || (long)rowCount * columnCount > settings.MaximumArrayCellCount)
                    return ExcelNumericUtilities.NumError();
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, rowCount, columnCount, out var result, out error))
                return FormulaValue.FromError(error);

            rowParts = new ExcelSplitSegments(text.AsSpan(), rows, comparison, ignoreEmpty);
            var row = 0;
            while (rowParts.MoveNext())
            {
                var column = 0;
                var columnParts = new ExcelSplitSegments(text.AsSpan(rowParts.Start, rowParts.Length), columns, comparison, ignoreEmpty);
                while (columnParts.MoveNext())
                    result[row, column++] = FormulaValue.FromText(text.Substring(rowParts.Start + columnParts.Start, columnParts.Length));
                while (column < columnCount) result[row, column++] = pad;
                row++;
            }
            return FormulaValue.FromArray(result);
        }

        private static bool IsOmitted(IReadOnlyList<FormulaValue> args, uint omitted, int index)
            => index >= args.Count || (omitted & (1u << index)) != 0;
    }
}
