// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class MedianFunction : ExcelFunctionBase
    {
        public MedianFunction()
            : base("MEDIAN", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => ExcelOrderStatistics.Median(context, args);
    }

    internal sealed class ModeSingleFunction : ExcelModeFunction
    {
        public ModeSingleFunction() : base("MODE.SNGL", multiple: false) { }
    }

    internal sealed class StdevSFunction : ExcelDescriptiveFunction
    {
        public StdevSFunction() : base("STDEV.S", ExcelDescriptiveOperation.SampleDeviation) { }
    }

    internal sealed class StdevPFunction : ExcelDescriptiveFunction
    {
        public StdevPFunction() : base("STDEV.P", ExcelDescriptiveOperation.PopulationDeviation) { }
    }

    internal sealed class VarSFunction : ExcelDescriptiveFunction
    {
        public VarSFunction() : base("VAR.S", ExcelDescriptiveOperation.SampleVariance) { }
    }

    internal sealed class VarPFunction : ExcelDescriptiveFunction
    {
        public VarPFunction() : base("VAR.P", ExcelDescriptiveOperation.PopulationVariance) { }
    }

    internal sealed class LargeFunction : ExcelFunctionBase
    {
        public LargeFunction()
            : base("LARGE", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelStatisticalUtilities.OrderedStatistic(context.EvaluationContext.Workbook.Settings, args[0], args[1], largest: true);
        }
    }

    internal sealed class SmallFunction : ExcelFunctionBase
    {
        public SmallFunction()
            : base("SMALL", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelStatisticalUtilities.OrderedStatistic(context.EvaluationContext.Workbook.Settings, args[0], args[1], largest: false);
        }
    }

    internal sealed class PercentileIncFunction : ExcelFunctionBase
    {
        public PercentileIncFunction()
            : base("PERCENTILE.INC", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelFunctionUtilities.TryCoerceToNumber(context, args[1], out var k, out var error))
            {
                return FormulaValue.FromError(error);
            }

            return ExcelStatisticalUtilities.PercentileInc(context.EvaluationContext.Workbook.Settings, args[0], k);
        }
    }

    internal sealed class QuartileIncFunction : ExcelFunctionBase
    {
        public QuartileIncFunction()
            : base("QUARTILE.INC", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, args[1], out var quartile, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (quartile < 0 || quartile > 4)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));
            }

            var k = quartile / 4d;
            return ExcelStatisticalUtilities.PercentileInc(context.EvaluationContext.Workbook.Settings, args[0], k);
        }
    }

    internal sealed class RankEqFunction : ExcelRankingFunction
    {
        public RankEqFunction() : base("RANK.EQ", ExcelRankingOperation.EqualRank) { }
    }

    internal sealed class RankAvgFunction : ExcelRankingFunction
    {
        public RankAvgFunction() : base("RANK.AVG", ExcelRankingOperation.AverageRank) { }
    }

    internal sealed class CountBlankFunction : ExcelFunctionBase
    {
        public CountBlankFunction()
            : base("COUNTBLANK", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var count = 0;
            foreach (var value in ExcelFunctionUtilities.FlattenValues(args[0]))
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    count++;
                    continue;
                }

                if (value.Kind == FormulaValueKind.Text && value.AsText().Length == 0)
                {
                    count++;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }
    }

    internal sealed class AverageIfFunction : ExcelFunctionBase
    {
        public AverageIfFunction()
            : base("AVERAGEIF", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var criteriaValue = args[1];
            if (criteriaValue.Kind == FormulaValueKind.Array)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out var criteria, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args[0], out var rangeArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args.Count > 2 ? args[2] : args[0], out var averageArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (rangeArray.RowCount != averageArray.RowCount || rangeArray.ColumnCount != averageArray.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = 0d;
            var count = 0;
            for (var row = 0; row < rangeArray.RowCount; row++)
            {
                for (var column = 0; column < rangeArray.ColumnCount; column++)
                {
                    if (rangeArray.HasMask && !rangeArray.IsPresent(row, column))
                    {
                        continue;
                    }

                    var rangeValue = rangeArray[row, column];
                    if (!ExcelCriteriaUtilities.TryMatch(rangeValue, criteria, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (!match)
                    {
                        continue;
                    }

                    var averageValue = averageArray[row, column];
                    if (averageValue.Kind == FormulaValueKind.Error)
                    {
                        return averageValue;
                    }

                    if (averageValue.Kind != FormulaValueKind.Number)
                    {
                        continue;
                    }

                    var next = sum + averageValue.AsNumber();
                    if (settings.ApplyNumberPrecision)
                    {
                        next = FormulaNumberUtilities.ApplyPrecision(next, settings.NumberPrecisionDigits);
                    }
                    sum = next;
                    count++;
                }
            }

            if (count == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            return ExcelFunctionUtilities.CreateNumber(context, sum / count);
        }
    }

    internal sealed class SumIfFunction : ExcelFunctionBase
    {
        public SumIfFunction()
            : base("SUMIF", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var criteriaValue = args[1];
            if (criteriaValue.Kind == FormulaValueKind.Array)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out var criteria, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args[0], out var rangeArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelLookupUtilities.TryGetArray(args.Count > 2 ? args[2] : args[0], out var sumArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (rangeArray.RowCount != sumArray.RowCount || rangeArray.ColumnCount != sumArray.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = 0d;
            for (var row = 0; row < rangeArray.RowCount; row++)
            {
                for (var column = 0; column < rangeArray.ColumnCount; column++)
                {
                    if (rangeArray.HasMask && !rangeArray.IsPresent(row, column))
                    {
                        continue;
                    }

                    var rangeValue = rangeArray[row, column];
                    if (!ExcelCriteriaUtilities.TryMatch(rangeValue, criteria, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (!match)
                    {
                        continue;
                    }

                    var sumValue = sumArray[row, column];
                    if (sumValue.Kind == FormulaValueKind.Error)
                    {
                        return sumValue;
                    }

                    if (sumValue.Kind != FormulaValueKind.Number)
                    {
                        continue;
                    }

                    var next = sum + sumValue.AsNumber();
                    if (settings.ApplyNumberPrecision)
                    {
                        next = FormulaNumberUtilities.ApplyPrecision(next, settings.NumberPrecisionDigits);
                    }
                    sum = next;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, sum);
        }
    }

    internal sealed class SumIfsFunction : ExcelFunctionBase
    {
        public SumIfsFunction()
            : base("SUMIFS", new FormulaFunctionInfo(3, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if ((args.Count - 1) % 2 != 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelLookupUtilities.TryGetArray(args[0], out var sumArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var pairCount = (args.Count - 1) / 2;
            var criteria = new ExcelCriteria[pairCount];
            var ranges = new FormulaArray[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                if (!ExcelLookupUtilities.TryGetArray(args[1 + (i * 2)], out ranges[i], out error))
                {
                    return FormulaValue.FromError(error);
                }

                var criteriaValue = args[1 + (i * 2) + 1];
                if (criteriaValue.Kind == FormulaValueKind.Array)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }

                if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out criteria[i], out error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            for (var i = 0; i < pairCount; i++)
            {
                if (ranges[i].RowCount != sumArray.RowCount || ranges[i].ColumnCount != sumArray.ColumnCount)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
            }

            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = 0d;
            for (var row = 0; row < sumArray.RowCount; row++)
            {
                for (var column = 0; column < sumArray.ColumnCount; column++)
                {
                    if (sumArray.HasMask && !sumArray.IsPresent(row, column))
                    {
                        continue;
                    }

                    var matchAll = true;
                    for (var i = 0; i < pairCount; i++)
                    {
                        var rangeValue = ranges[i][row, column];
                        if (!ExcelCriteriaUtilities.TryMatch(rangeValue, criteria[i], out var match, out error))
                        {
                            return FormulaValue.FromError(error);
                        }

                        if (!match)
                        {
                            matchAll = false;
                            break;
                        }
                    }

                    if (!matchAll)
                    {
                        continue;
                    }

                    var sumValue = sumArray[row, column];
                    if (sumValue.Kind == FormulaValueKind.Error)
                    {
                        return sumValue;
                    }

                    if (sumValue.Kind != FormulaValueKind.Number)
                    {
                        continue;
                    }

                    var next = sum + sumValue.AsNumber();
                    if (settings.ApplyNumberPrecision)
                    {
                        next = FormulaNumberUtilities.ApplyPrecision(next, settings.NumberPrecisionDigits);
                    }
                    sum = next;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, sum);
        }
    }

    internal sealed class AverageIfsFunction : ExcelFunctionBase
    {
        public AverageIfsFunction()
            : base("AVERAGEIFS", new FormulaFunctionInfo(3, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if ((args.Count - 1) % 2 != 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var pairCount = (args.Count - 1) / 2;
            var criteria = new ExcelCriteria[pairCount];
            var ranges = new FormulaValue[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                ranges[i] = args[1 + (i * 2)];
                var criteriaValue = args[1 + (i * 2) + 1];
                if (criteriaValue.Kind == FormulaValueKind.Array)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }

                if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out criteria[i], out var error))
                {
                    return FormulaValue.FromError(error);
                }
            }

            if (!ExcelLookupUtilities.TryGetArray(args[0], out var averageArray, out var arrayError))
            {
                return FormulaValue.FromError(arrayError);
            }

            var rows = averageArray.RowCount;
            var columns = averageArray.ColumnCount;

            var resolvedRanges = new FormulaArray?[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                if (!ExcelLookupUtilities.TryGetArray(ranges[i], out var rangeArray, out arrayError))
                {
                    return FormulaValue.FromError(arrayError);
                }

                resolvedRanges[i] = rangeArray;
                if (rangeArray.RowCount != rows || rangeArray.ColumnCount != columns)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
            }

            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = 0d;
            var count = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var matchesAll = true;
                    for (var i = 0; i < pairCount; i++)
                    {
                        var rangeArray = resolvedRanges[i]!;
                        if (rangeArray.HasMask && !rangeArray.IsPresent(row, column))
                        {
                            matchesAll = false;
                            break;
                        }

                        var candidate = rangeArray[row, column];
                        if (!ExcelCriteriaUtilities.TryMatch(candidate, criteria[i], out var match, out var error))
                        {
                            return FormulaValue.FromError(error);
                        }

                        if (!match)
                        {
                            matchesAll = false;
                            break;
                        }
                    }

                    if (!matchesAll)
                    {
                        continue;
                    }

                    if (averageArray.HasMask && !averageArray.IsPresent(row, column))
                    {
                        continue;
                    }

                    var averageValue = averageArray[row, column];
                    if (averageValue.Kind == FormulaValueKind.Error)
                    {
                        return averageValue;
                    }

                    if (averageValue.Kind != FormulaValueKind.Number)
                    {
                        continue;
                    }

                    var next = sum + averageValue.AsNumber();
                    if (settings.ApplyNumberPrecision)
                    {
                        next = FormulaNumberUtilities.ApplyPrecision(next, settings.NumberPrecisionDigits);
                    }
                    sum = next;
                    count++;
                }
            }

            if (count == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            return ExcelFunctionUtilities.CreateNumber(context, sum / count);
        }
    }

    internal static class ExcelStatisticalUtilities
    {
        public static bool TryCollectNumbers(
            FormulaCalculationSettings settings,
            IReadOnlyList<FormulaValue> args,
            out List<double> numbers,
            out FormulaError error)
        {
            numbers = new List<double>();
            error = default;

            foreach (var value in args)
            {
                if (value.Kind == FormulaValueKind.Array)
                {
                    foreach (var inner in value.AsArray().Flatten())
                    {
                        if (inner.Kind == FormulaValueKind.Error)
                        {
                            error = inner.AsError();
                            return false;
                        }

                        if (inner.Kind == FormulaValueKind.Number)
                        {
                            var innerNumber = inner.AsNumber();
                            if (settings.ApplyNumberPrecision)
                            {
                                innerNumber = FormulaNumberUtilities.ApplyPrecision(innerNumber, settings.NumberPrecisionDigits);
                            }
                            numbers.Add(innerNumber);
                        }
                    }
                    continue;
                }

                if (value.Kind == FormulaValueKind.Error)
                {
                    error = value.AsError();
                    return false;
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                if (!ExcelFunctionUtilities.TryCoerceToNumber(settings, value, out var number, out error))
                {
                    return false;
                }

                if (settings.ApplyNumberPrecision)
                {
                    number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
                }
                numbers.Add(number);
            }

            return true;
        }

        public static FormulaValue Variance(FormulaCalculationSettings settings, IReadOnlyList<FormulaValue> args, bool sample)
            => ExcelDescriptiveFunction.EvaluateValues(settings, args,
                sample ? ExcelDescriptiveOperation.SampleVariance : ExcelDescriptiveOperation.PopulationVariance);

        public static FormulaValue StandardDeviation(FormulaCalculationSettings settings, IReadOnlyList<FormulaValue> args, bool sample)
            => ExcelDescriptiveFunction.EvaluateValues(settings, args,
                sample ? ExcelDescriptiveOperation.SampleDeviation : ExcelDescriptiveOperation.PopulationDeviation);

        public static FormulaValue OrderedStatistic(
            FormulaCalculationSettings settings, FormulaValue dataValue, FormulaValue kValue, bool largest)
            => ExcelOrderStatistics.Ordered(settings, dataValue, kValue, largest);

        public static FormulaValue PercentileInc(FormulaCalculationSettings settings, FormulaValue dataValue, double k)
            => ExcelOrderStatistics.Percentile(settings, dataValue, k, exclusive: false);

    }
}
