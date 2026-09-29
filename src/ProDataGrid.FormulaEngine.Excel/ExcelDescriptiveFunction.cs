// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal class ExcelDescriptiveFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly ExcelDescriptiveOperation _operation;
        private readonly bool _includeLogical;
        public ExcelDescriptiveFunction(string name, ExcelDescriptiveOperation operation, bool includeLogical = false)
            : base(name, new FormulaFunctionInfo(1, 255)) { _operation = operation; _includeLogical = includeLogical; }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => EvaluateValues(context.EvaluationContext.Workbook.Settings, args, _operation, _includeLogical);

        internal static FormulaValue EvaluateValues(FormulaCalculationSettings settings, IReadOnlyList<FormulaValue> args,
            ExcelDescriptiveOperation operation, bool includeLogical = false)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var sample = new ExcelNumericSample(stackalloc double[128]);
            try
            {
                for (var i = 0; i < args.Count; i++)
                    if (!TryAppend(ref sample, args[i], false, includeLogical, settings, out var error)) return FormulaValue.FromError(error);
                return ExcelDescriptiveStatistics.Evaluate(sample.Values, settings, operation);
            }
            finally { sample.Dispose(); }
        }

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var settings = context.EvaluationContext.Workbook.Settings;
            var sample = new ExcelNumericSample(stackalloc double[128]);
            try
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var argument = args[i];
                    if (argument is FormulaReferenceExpression reference && resolver is IFormulaRangeValueResolver ranges)
                    {
                        // Stream each reference once, including single-cell references: logical
                        // cells must not accidentally acquire direct scalar coercion semantics.
                        foreach (var value in ranges.EnumerateReferenceValues(context.EvaluationContext, reference.Reference))
                            if (!TryAppend(ref sample, value, true, _includeLogical, settings, out var error)) return FormulaValue.FromError(error);
                    }
                    else
                    {
                        var value = evaluator.Evaluate(argument, context.EvaluationContext, resolver);
                        var fromReference = argument.Kind is FormulaExpressionKind.Reference or FormulaExpressionKind.StructuredReference;
                        if (!TryAppend(ref sample, value, fromReference, _includeLogical, settings, out var error)) return FormulaValue.FromError(error);
                    }
                }
                return ExcelDescriptiveStatistics.Evaluate(sample.Values, settings, _operation);
            }
            finally { sample.Dispose(); }
        }

        private static bool TryAppend(ref ExcelNumericSample sample, FormulaValue value, bool fromReference,
            bool includeLogical, FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                for (var row = 0; row < array.RowCount; row++)
                    for (var column = 0; column < array.ColumnCount; column++)
                    {
                        if (!array.IsPresent(row, column)) continue;
                        var item = array[row, column];
                        if (item.Kind == FormulaValueKind.Number)
                        {
                            if (sample.Count >= settings.MaximumArrayCellCount) { error = new FormulaError(FormulaErrorType.Num); return false; }
                            if (!sample.TryAddNumber(item.AsNumber(), settings, out error)) return false;
                        }
                        else if (!TryScalar(ref sample, item, true, includeLogical, settings, out error)) return false;
                    }
                return true;
            }
            return TryScalar(ref sample, value, fromReference, includeLogical, settings, out error);
        }

        private static bool TryScalar(ref ExcelNumericSample sample, FormulaValue value, bool fromReference,
            bool includeLogical, FormulaCalculationSettings settings, out FormulaError error)
        {
            error = default;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            if (value.Kind == FormulaValueKind.Blank) return true;
            if (fromReference && (value.Kind is FormulaValueKind.Text or FormulaValueKind.Boolean))
            {
                if (!includeLogical) return true;
                value = FormulaValue.FromNumber(value.Kind == FormulaValueKind.Boolean && value.AsBoolean() ? 1 : 0);
            }
            if (value.Kind is FormulaValueKind.Array or FormulaValueKind.Reference or FormulaValueKind.Lambda)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
            if (sample.Count >= settings.MaximumArrayCellCount) { error = new FormulaError(FormulaErrorType.Num); return false; }
            return value.Kind == FormulaValueKind.Number
                ? sample.TryAddNumber(value.AsNumber(), settings, out error) : sample.TryAdd(value, settings, out error);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterDescriptiveStatistics()
        {
            Register(new ExcelDescriptiveFunction("DEVSQ", ExcelDescriptiveOperation.SquaredDeviations));
            Register(new ExcelDescriptiveFunction("AVEDEV", ExcelDescriptiveOperation.AbsoluteDeviation));
            Register(new ExcelDescriptiveFunction("SKEW", ExcelDescriptiveOperation.SampleSkew));
            Register(new ExcelDescriptiveFunction("SKEW.P", ExcelDescriptiveOperation.PopulationSkew));
            Register(new ExcelDescriptiveFunction("KURT", ExcelDescriptiveOperation.Kurtosis));
            Register(new ExcelDescriptiveFunction("GEOMEAN", ExcelDescriptiveOperation.GeometricMean));
            Register(new ExcelDescriptiveFunction("HARMEAN", ExcelDescriptiveOperation.HarmonicMean));
            Register(new ExcelDescriptiveFunction("SUMSQ", ExcelDescriptiveOperation.SumSquares));
            Register(new ExcelDescriptiveFunction("VARA", ExcelDescriptiveOperation.SampleVariance, true));
            Register(new ExcelDescriptiveFunction("VARPA", ExcelDescriptiveOperation.PopulationVariance, true));
            Register(new ExcelDescriptiveFunction("STDEVA", ExcelDescriptiveOperation.SampleDeviation, true));
            Register(new ExcelDescriptiveFunction("STDEVPA", ExcelDescriptiveOperation.PopulationDeviation, true));
            Register(new ExcelDescriptiveFunction("VAR", ExcelDescriptiveOperation.SampleVariance));
            Register(new ExcelDescriptiveFunction("VARP", ExcelDescriptiveOperation.PopulationVariance));
            Register(new ExcelDescriptiveFunction("STDEV", ExcelDescriptiveOperation.SampleDeviation));
            Register(new ExcelDescriptiveFunction("STDEVP", ExcelDescriptiveOperation.PopulationDeviation));
        }
    }
}
