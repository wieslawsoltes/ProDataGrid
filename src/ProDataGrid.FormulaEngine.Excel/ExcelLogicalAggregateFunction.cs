// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelLogicalAggregateOperation { Average, Minimum, Maximum }

    // A-family aggregates include logical/text cells while retaining direct scalar coercion.
    // Values are consumed once; neither a converted array nor a numeric sample is retained.
    internal sealed class ExcelLogicalAggregateFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly ExcelLogicalAggregateOperation _operation;
        public ExcelLogicalAggregateFunction(string name, ExcelLogicalAggregateOperation operation)
            : base(name, new FormulaFunctionInfo(1, 255)) => _operation = operation;

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var state = new Accumulator(_operation, _operation == ExcelLogicalAggregateOperation.Average
                ? stackalloc uint[ExcelExactMeanAccumulator.WordCount * 2] : Span<uint>.Empty);
            var settings = context.EvaluationContext.Workbook.Settings;
            var remaining = settings.MaximumArrayCellCount;
            for (var i = 0; i < args.Count; i++)
                if (!TryAppend(ref state, args[i], false, settings, ref remaining, out var error)) return FormulaValue.FromError(error);
            return state.Result(settings);
        }

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var state = new Accumulator(_operation, _operation == ExcelLogicalAggregateOperation.Average
                ? stackalloc uint[ExcelExactMeanAccumulator.WordCount * 2] : Span<uint>.Empty);
            var settings = context.EvaluationContext.Workbook.Settings;
            var remaining = settings.MaximumArrayCellCount;
            for (var i = 0; i < args.Count; i++)
            {
                var argument = args[i];
                if (context.EvaluationContext.IsArgumentOmitted(argument))
                {
                    // An explicitly missing argument is numeric zero, unlike an empty cell.
                    if (!TryAppend(ref state, FormulaValue.FromNumber(0), false, settings, ref remaining, out var error)) return FormulaValue.FromError(error);
                }
                else if (argument is FormulaReferenceExpression reference && resolver is IFormulaRangeValueResolver ranges)
                {
                    foreach (var value in ranges.EnumerateReferenceValues(context.EvaluationContext, reference.Reference))
                        if (!TryAppend(ref state, value, true, settings, ref remaining, out var error)) return FormulaValue.FromError(error);
                }
                else
                {
                    var value = evaluator.Evaluate(argument, context.EvaluationContext, resolver);
                    var fromReference = argument.Kind is FormulaExpressionKind.Reference or FormulaExpressionKind.StructuredReference;
                    if (!TryAppend(ref state, value, fromReference, settings, ref remaining, out var error)) return FormulaValue.FromError(error);
                }
            }
            return state.Result(settings);
        }

        private static bool TryAppend(ref Accumulator state, FormulaValue value, bool fromReference,
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
                        double number;
                        // Array provenance is known here: no culture/text conversion or
                        // general scalar dispatch is needed for each observation.
                        switch (item.Kind)
                        {
                            case FormulaValueKind.Blank: continue;
                            case FormulaValueKind.Number:
                                number = item.AsNumber();
                                if (precision) number = FormulaNumberUtilities.ApplyPrecision(number, digits);
                                if (!double.IsFinite(number)) { error = new FormulaError(FormulaErrorType.Num); return false; }
                                break;
                            case FormulaValueKind.Boolean: number = item.AsBoolean() ? 1 : 0; break;
                            case FormulaValueKind.Text: number = 0; break;
                            case FormulaValueKind.Error: error = item.AsError(); return false;
                            default: error = new FormulaError(FormulaErrorType.Value); return false;
                        }
                        state.Add(number);
                    }
                return true;
            }
            if (remaining == 0) { error = new FormulaError(FormulaErrorType.Num); return false; }
            remaining--;
            return TryScalar(ref state, value, fromReference, settings, out error);
        }

        private static bool TryScalar(ref Accumulator state, FormulaValue value, bool fromReference,
            FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Blank) return true;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            double number;
            switch (value.Kind)
            {
                case FormulaValueKind.Number:
                    number = value.AsNumber();
                    if (settings.ApplyNumberPrecision) number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
                    break;
                case FormulaValueKind.Boolean:
                    number = value.AsBoolean() ? 1 : 0;
                    break;
                case FormulaValueKind.Text:
                    if (fromReference || value.AsText().Length == 0) number = 0;
                    else if (!FormulaCoercion.TryCoerceToNumber(value, settings, out number, out error)) return false;
                    break;
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
            if (!double.IsFinite(number)) { error = new FormulaError(FormulaErrorType.Num); return false; }
            state.Add(number);
            return true;
        }

        private ref struct Accumulator
        {
            private readonly ExcelLogicalAggregateOperation _operation;
            private ExcelExactMeanAccumulator _mean;
            private double _extreme;
            private int _count;

            public Accumulator(ExcelLogicalAggregateOperation operation, Span<uint> workspace)
            {
                _operation = operation;
                _mean = operation == ExcelLogicalAggregateOperation.Average ? new ExcelExactMeanAccumulator(workspace) : default;
                _extreme = 0;
                _count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(double number)
            {
                if (_operation == ExcelLogicalAggregateOperation.Average) _mean.Add(number);
                else if (_count == 0 || (_operation == ExcelLogicalAggregateOperation.Minimum ? number < _extreme : number > _extreme)) _extreme = number;
                _count++;
            }

            public FormulaValue Result(FormulaCalculationSettings settings)
            {
                if (_operation != ExcelLogicalAggregateOperation.Average) return ExcelDescriptiveStatistics.Number(settings, _extreme);
                return _count == 0 ? FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0))
                    : ExcelDescriptiveStatistics.Number(settings, _mean.Mean());
            }
        }
    }
}
