// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal class ExcelModeFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly bool _multiple;
        public ExcelModeFunction(string name, bool multiple) : base(name, new FormulaFunctionInfo(1, 255))
            => _multiple = multiple;

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var settings = context.EvaluationContext.Workbook.Settings;
            var remaining = settings.MaximumArrayCellCount;
            var counter = new ExcelModeCounter(stackalloc ExcelModeCounter.ModeEntry[128]);
            try
            {
                for (var i = 0; i < args.Count; i++)
                    if (!TryAppend(ref counter, args[i], false, settings, ref remaining, out var error))
                        return FormulaValue.FromError(error);
                return counter.Result(context, _multiple);
            }
            finally { counter.Dispose(); }
        }

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var settings = context.EvaluationContext.Workbook.Settings;
            var remaining = settings.MaximumArrayCellCount;
            var counter = new ExcelModeCounter(stackalloc ExcelModeCounter.ModeEntry[128]);
            try
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var argument = args[i];
                    if (argument is FormulaReferenceExpression reference && resolver is IFormulaRangeValueResolver ranges)
                    {
                        // Stream direct references once and retain cell provenance even for
                        // scalar references; logical/text cells are not literal arguments.
                        foreach (var value in ranges.EnumerateReferenceValues(context.EvaluationContext, reference.Reference))
                            if (!TryAppend(ref counter, value, true, settings, ref remaining, out var error))
                                return FormulaValue.FromError(error);
                    }
                    else
                    {
                        var value = evaluator.Evaluate(argument, context.EvaluationContext, resolver);
                        var fromReference = argument.Kind is FormulaExpressionKind.Reference or FormulaExpressionKind.StructuredReference;
                        if (!TryAppend(ref counter, value, fromReference, settings, ref remaining, out var error))
                            return FormulaValue.FromError(error);
                    }
                }
                return counter.Result(context, _multiple);
            }
            finally { counter.Dispose(); }
        }

        private static bool TryAppend(ref ExcelModeCounter counter, FormulaValue value, bool fromReference,
            FormulaCalculationSettings settings, ref int remaining, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                var length = (long)array.RowCount * array.ColumnCount;
                if (length > remaining) { error = new FormulaError(FormulaErrorType.Num); return false; }
                remaining -= (int)length;
                var rows = array.RowCount;
                var columns = array.ColumnCount;
                var precision = settings.ApplyNumberPrecision;
                var digits = settings.NumberPrecisionDigits;
                for (var row = 0; row < rows; row++)
                    for (var column = 0; column < columns; column++)
                    {
                        if (!array.IsPresent(row, column)) continue;
                        var item = array[row, column];
                        if (item.Kind == FormulaValueKind.Number)
                        {
                            // Avoid the general scalar-coercion dispatch for the common
                            // numeric array path; the precision/error contract is identical.
                            var number = item.AsNumber();
                            if (precision) number = FormulaNumberUtilities.ApplyPrecision(number, digits);
                            if (!double.IsFinite(number) || !counter.TryAdd(number))
                            { error = new FormulaError(FormulaErrorType.Num); return false; }
                        }
                        else if (!TryScalar(ref counter, item, true, settings, out error)) return false;
                    }
                return true;
            }
            if (remaining == 0) { error = new FormulaError(FormulaErrorType.Num); return false; }
            remaining--;
            return TryScalar(ref counter, value, fromReference, settings, out error);
        }

        private static bool TryScalar(ref ExcelModeCounter counter, FormulaValue value, bool fromReference,
            FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            if (value.Kind == FormulaValueKind.Blank || (fromReference && value.Kind is FormulaValueKind.Text or FormulaValueKind.Boolean)) return true;
            if (value.Kind is FormulaValueKind.Array or FormulaValueKind.Reference or FormulaValueKind.Lambda)
            { error = new FormulaError(FormulaErrorType.Value); return false; }
            double number;
            if (value.Kind == FormulaValueKind.Number)
            {
                number = value.AsNumber();
                if (settings.ApplyNumberPrecision)
                    number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
            }
            else if (!FormulaCoercion.TryCoerceToNumber(value, settings, out number, out error)) return false;
            if (!double.IsFinite(number) || !counter.TryAdd(number))
            { error = new FormulaError(FormulaErrorType.Num); return false; }
            return true;
        }
    }
}
