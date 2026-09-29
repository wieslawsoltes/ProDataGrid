// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelDescriptiveOperation
    {
        SampleVariance, PopulationVariance, SampleDeviation, PopulationDeviation,
        SquaredDeviations, AbsoluteDeviation, SampleSkew, PopulationSkew, Kurtosis,
        GeometricMean, HarmonicMean, SumSquares
    }

    // The caller owns this numeric snapshot. Scale it before centering so finite inputs
    // never overflow solely because the mean or the squared deviations are intermediate.
    internal static class ExcelDescriptiveStatistics
    {
        public static FormulaValue Evaluate(Span<double> values, FormulaCalculationSettings settings, ExcelDescriptiveOperation operation)
        {
            var count = values.Length;
            if (count == 0)
                return operation == ExcelDescriptiveOperation.SumSquares ? FormulaValue.FromNumber(0) : Error(FormulaErrorType.Div0);
            if ((operation is ExcelDescriptiveOperation.SampleVariance or ExcelDescriptiveOperation.SampleDeviation) && count < 2 ||
                (operation is ExcelDescriptiveOperation.SampleSkew or ExcelDescriptiveOperation.PopulationSkew) && count < 3 ||
                operation == ExcelDescriptiveOperation.Kurtosis && count < 4)
                return Error(FormulaErrorType.Div0);
            if (operation == ExcelDescriptiveOperation.GeometricMean || operation == ExcelDescriptiveOperation.HarmonicMean)
                return PositiveMean(values, settings, operation == ExcelDescriptiveOperation.GeometricMean);

            var maximum = 0d;
            for (var i = 0; i < count; i++) maximum = Math.Max(maximum, Math.Abs(values[i]));
            var exponent = maximum == 0 ? 0 : Math.ILogB(maximum);
            var factor = Math.ScaleB(1d, -exponent);
            // Multiplication is sufficient except when scaling subnormal inputs upward.
            // The ScaleB fallback handles that case without an infinite scale factor.
            var ordinaryScale = double.IsFinite(factor);
            var origin = ordinaryScale ? values[0] * factor : Math.ScaleB(values[0], -exponent);
            var sum = new ExcelCompensatedSum();
            for (var i = 0; i < count; i++)
            {
                var scaled = ordinaryScale ? values[i] * factor : Math.ScaleB(values[i], -exponent);
                values[i] = operation == ExcelDescriptiveOperation.SumSquares ? scaled : scaled - origin;
                sum.Add(operation == ExcelDescriptiveOperation.SumSquares ? scaled * scaled : values[i]);
            }
            if (operation == ExcelDescriptiveOperation.SumSquares)
                return Number(settings, Math.ScaleB(sum.Total, 2 * exponent));

            // Keep the origin and mean offset separate: forming origin+offset would lose
            // small but representable variation on a large common offset.
            var meanOffset = sum.Total / count;
            var residual = new ExcelCompensatedSum();
            for (var i = 0; i < count; i++)
            {
                values[i] -= meanOffset;
                residual.Add(values[i]);
            }
            var correction = residual.Total / count;
            var second = new ExcelCompensatedSum();
            var absolute = new ExcelCompensatedSum();
            for (var i = 0; i < count; i++)
            {
                var delta = values[i] - correction;
                values[i] = delta;
                second.Add(delta * delta);
                if (operation == ExcelDescriptiveOperation.AbsoluteDeviation) absolute.Add(Math.Abs(delta));
            }
            var m2 = second.Total;
            var n = (double)count;
            switch (operation)
            {
                case ExcelDescriptiveOperation.SquaredDeviations:
                    return Number(settings, Math.ScaleB(m2, 2 * exponent));
                case ExcelDescriptiveOperation.AbsoluteDeviation:
                    return Number(settings, Math.ScaleB(absolute.Total / n, exponent));
                case ExcelDescriptiveOperation.SampleVariance:
                case ExcelDescriptiveOperation.PopulationVariance:
                    return Number(settings, Math.ScaleB(m2 / (operation == ExcelDescriptiveOperation.SampleVariance ? n - 1 : n), 2 * exponent));
                case ExcelDescriptiveOperation.SampleDeviation:
                case ExcelDescriptiveOperation.PopulationDeviation:
                    // Do not compute sqrt(VAR): VAR can overflow/underflow while STDEV is finite.
                    return Number(settings, Math.ScaleB(Math.Sqrt(m2 / (operation == ExcelDescriptiveOperation.SampleDeviation ? n - 1 : n)), exponent));
            }
            if (m2 == 0) return Error(FormulaErrorType.Div0);
            var population = operation == ExcelDescriptiveOperation.PopulationSkew;
            var deviation = Math.Sqrt(m2 / (population ? n : n - 1));
            var standardized = new ExcelCompensatedSum();
            for (var i = 0; i < count; i++)
            {
                var z = values[i] / deviation;
                var square = z * z;
                standardized.Add(operation == ExcelDescriptiveOperation.Kurtosis ? square * square : square * z);
            }
            var result = operation == ExcelDescriptiveOperation.Kurtosis
                ? (n / (n - 2)) * ((n + 1) / ((n - 1) * (n - 3))) * standardized.Total - 3 * ((n - 1) / (n - 2)) * ((n - 1) / (n - 3))
                : population ? standardized.Total / n : (n / (n - 1)) * (standardized.Total / (n - 2));
            return Number(settings, result);
        }

        private static FormulaValue PositiveMean(ReadOnlySpan<double> values, FormulaCalculationSettings settings, bool geometric)
        {
            var minimum = values[0];
            var maximum = values[0];
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0) return Error(FormulaErrorType.Num);
                minimum = Math.Min(minimum, values[i]);
                maximum = Math.Max(maximum, values[i]);
            }
            if (minimum == maximum) return Number(settings, minimum);
            var sum = new ExcelCompensatedSum();
            for (var i = 0; i < values.Length; i++)
                sum.Add(geometric ? Math.Log(values[i]) : minimum / values[i]);
            // Scaled reciprocal ratios cannot overflow even for the smallest subnormal.
            var result = geometric ? Math.Exp(sum.Total / values.Length) : minimum * (values.Length / sum.Total);
            // Both means lie in this interval; clamp only the final rounding excursion.
            return Number(settings, Math.Clamp(result, minimum, maximum));
        }

        internal static FormulaValue Number(FormulaCalculationSettings settings, double value)
            => double.IsFinite(value) ? ExcelFunctionUtilities.CreateNumber(settings, value) : Error(FormulaErrorType.Num);
        internal static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));
    }
}
