// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelPairedStatistic { PopulationCovariance, SampleCovariance, Correlation, Pearson, RSquared, Slope, Intercept, StandardError, Forecast }

    internal sealed class ExcelPairedStatisticsFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly ExcelPairedStatistic _operation;
        public ExcelPairedStatisticsFunction(string name, ExcelPairedStatistic operation)
            : base(name, new FormulaFunctionInfo(operation == ExcelPairedStatistic.Forecast ? 3 : 2,
                operation == ExcelPairedStatistic.Forecast ? 3 : 2)) { _operation = operation; }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => Evaluate(context, args, 0);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count != Info.MinArgs) return Error(FormulaErrorType.Value);
            var values = new FormulaValue[args.Count];
            var references = 0;
            for (var i = 0; i < args.Count; i++)
            {
                values[i] = evaluator.Evaluate(args[i], context.EvaluationContext, resolver);
                if (args[i].Kind is FormulaExpressionKind.Reference or FormulaExpressionKind.StructuredReference) references |= 1 << i;
            }
            return Evaluate(context, values, references);
        }

        private FormulaValue Evaluate(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args, int references)
        {
            if (args.Count != Info.MinArgs) return Error(FormulaErrorType.Value);
            var forecast = _operation == ExcelPairedStatistic.Forecast;
            var first = forecast ? 1 : 0;
            var y = new ExcelArrayOperand(args[first]);
            var x = new ExcelArrayOperand(args[first + 1]);
            if (x.IsUnresolvedReference || y.IsUnresolvedReference) return Error(FormulaErrorType.Value);
            if (x.Length != y.Length) return Error(FormulaErrorType.NA);
            var settings = context.EvaluationContext.Workbook.Settings;
            if (x.Length > settings.MaximumArrayCellCount || x.Length > Array.MaxLength) return Error(FormulaErrorType.Num);
            var capacity = (int)x.Length;
            var targets = forecast && args[0].Kind == FormulaValueKind.Array ? args[0].AsArray() : null;
            if (targets != null && ((long)targets.RowCount * targets.ColumnCount > settings.MaximumArrayCellCount ||
                targets.RowCount > 1048576 || targets.ColumnCount > 16384)) return Error(FormulaErrorType.Num);
            if (forecast && args[0].Kind == FormulaValueKind.Error) return args[0];

            double[]? rentedX = null;
            double[]? rentedY = null;
            try
            {
                Span<double> xs = capacity <= 128 ? stackalloc double[128] : (rentedX = ArrayPool<double>.Shared.Rent(capacity));
                Span<double> ys = capacity <= 128 ? stackalloc double[128] : (rentedY = ArrayPool<double>.Shared.Rent(capacity));
                var count = 0;
                var xr = 0;
                var xc = 0;
                var yReference = (references & (1 << first)) != 0 || args[first].Kind == FormulaValueKind.Array;
                var xReference = (references & (1 << (first + 1))) != 0 || args[first + 1].Kind == FormulaValueKind.Array;
                for (var row = 0; row < y.Rows; row++)
                    for (var column = 0; column < y.Columns; column++)
                    {
                        if (!TryObservation(y.Read(row, column), yReference, settings, out var vy, out var ny, out var error) ||
                            !TryObservation(x.Read(xr, xc), xReference, settings, out var vx, out var nx, out error))
                            return FormulaValue.FromError(error);
                        if (++xc == x.Columns) { xc = 0; xr++; }
                        // Drop a pair in place. Filtering the axes independently corrupts alignment.
                        if (!nx || !ny) continue;
                        xs[count] = vx;
                        ys[count++] = vy;
                    }
                if (count == 0)
                    return Error(_operation is ExcelPairedStatistic.Correlation or ExcelPairedStatistic.PopulationCovariance or
                        ExcelPairedStatistic.SampleCovariance or ExcelPairedStatistic.StandardError ? FormulaErrorType.Div0 : FormulaErrorType.NA);
                if (_operation == ExcelPairedStatistic.SampleCovariance && count < 2 ||
                    _operation == ExcelPairedStatistic.StandardError && count < 3) return Error(FormulaErrorType.Div0);
                xs = xs.Slice(0, count);
                ys = ys.Slice(0, count);
                var model = new ExcelBivariateModel(xs, ys);
                if (_operation == ExcelPairedStatistic.PopulationCovariance) return Number(settings, model.Covariance(count));
                if (_operation == ExcelPairedStatistic.SampleCovariance) return Number(settings, model.Covariance(count - 1));
                if (model.XX == 0) return Error(FormulaErrorType.Div0);
                if (_operation is ExcelPairedStatistic.Correlation or ExcelPairedStatistic.Pearson or ExcelPairedStatistic.RSquared)
                {
                    if (model.YY == 0) return Error(FormulaErrorType.Div0);
                    var coefficient = model.Correlation;
                    return Number(settings, _operation == ExcelPairedStatistic.RSquared ? coefficient * coefficient : coefficient);
                }
                if (_operation == ExcelPairedStatistic.Slope) return Number(settings, model.Slope);
                if (_operation == ExcelPairedStatistic.Intercept) return Number(settings, model.Predict(0));
                if (_operation == ExcelPairedStatistic.StandardError) return Number(settings, model.StandardError(xs, ys));
                if (targets == null) return Predict(args[0], model, settings);
                if (!ExcelArrayShapeUtilities.TryCreate(context, targets.RowCount, targets.ColumnCount, out var result, out var allocationError))
                    return FormulaValue.FromError(allocationError);
                for (var row = 0; row < result.RowCount; row++)
                    for (var column = 0; column < result.ColumnCount; column++)
                        result[row, column] = Predict(ExcelDynamicArrayUtilities.GetArrayValue(targets, row, column), model, settings);
                return FormulaValue.FromArray(result);
            }
            finally
            {
                if (rentedX != null) ArrayPool<double>.Shared.Return(rentedX, clearArray: true);
                if (rentedY != null) ArrayPool<double>.Shared.Return(rentedY, clearArray: true);
            }
        }

        private static bool TryObservation(FormulaValue value, bool reference, FormulaCalculationSettings settings,
            out double number, out bool numeric, out FormulaError error)
        {
            number = 0;
            numeric = false;
            error = default;
            if (value.Kind == FormulaValueKind.Error) { error = value.AsError(); return false; }
            if (value.Kind == FormulaValueKind.Blank || reference && value.Kind is FormulaValueKind.Text or FormulaValueKind.Boolean) return true;
            if (value.Kind == FormulaValueKind.Number)
            {
                number = value.AsNumber();
                if (settings.ApplyNumberPrecision) number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
            }
            else if (!FormulaCoercion.TryCoerceToNumber(value, settings, out number, out error)) return false;
            if (!double.IsFinite(number)) { error = new FormulaError(FormulaErrorType.Num); return false; }
            numeric = true;
            return true;
        }

        private static FormulaValue Predict(FormulaValue value, in ExcelBivariateModel model, FormulaCalculationSettings settings)
        {
            if (!FormulaCoercion.TryCoerceToNumber(value, settings, out var target, out var error)) return FormulaValue.FromError(error);
            return double.IsFinite(target) ? Number(settings, model.Predict(target)) : Error(FormulaErrorType.Num);
        }

        private static FormulaValue Number(FormulaCalculationSettings settings, double value) => ExcelDescriptiveStatistics.Number(settings, value);
        private static FormulaValue Error(FormulaErrorType type) => ExcelDescriptiveStatistics.Error(type);
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterPairedStatistics()
        {
            Register(new ExcelPairedStatisticsFunction("COVARIANCE.P", ExcelPairedStatistic.PopulationCovariance));
            Register(new ExcelPairedStatisticsFunction("COVAR", ExcelPairedStatistic.PopulationCovariance));
            Register(new ExcelPairedStatisticsFunction("COVARIANCE.S", ExcelPairedStatistic.SampleCovariance));
            Register(new ExcelPairedStatisticsFunction("CORREL", ExcelPairedStatistic.Correlation));
            Register(new ExcelPairedStatisticsFunction("PEARSON", ExcelPairedStatistic.Pearson));
            Register(new ExcelPairedStatisticsFunction("RSQ", ExcelPairedStatistic.RSquared));
            Register(new ExcelPairedStatisticsFunction("SLOPE", ExcelPairedStatistic.Slope));
            Register(new ExcelPairedStatisticsFunction("INTERCEPT", ExcelPairedStatistic.Intercept));
            Register(new ExcelPairedStatisticsFunction("STEYX", ExcelPairedStatistic.StandardError));
            Register(new ExcelPairedStatisticsFunction("FORECAST", ExcelPairedStatistic.Forecast));
            Register(new ExcelPairedStatisticsFunction("FORECAST.LINEAR", ExcelPairedStatistic.Forecast));
        }
    }
}
