// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class TrimMeanFunction : ExcelFunctionBase
    {
        public TrimMeanFunction() : base("TRIMMEAN", new FormulaFunctionInfo(2, 2)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 2) return Error(FormulaErrorType.Value);
            var source = new ExcelArrayOperand(args[0]);
            var percentages = new ExcelArrayOperand(args[1]);
            var settings = context.EvaluationContext.Workbook.Settings;
            if (source.IsUnresolvedReference || percentages.IsUnresolvedReference) return Error(FormulaErrorType.Value);
            if (source.Length > settings.MaximumArrayCellCount || source.Length > Array.MaxLength ||
                percentages.Length > settings.MaximumArrayCellCount || percentages.Length > Array.MaxLength ||
                percentages.Rows > 1048576 || percentages.Columns > 16384) return Error(FormulaErrorType.Num);
            double[]? rentedValues = null;
            TrimQuery[]? rentedQueries = null;
            try
            {
                var capacity = (int)source.Length;
                Span<double> workspace = capacity <= 128 ? stackalloc double[128]
                    : (rentedValues = ArrayPool<double>.Shared.Rent(capacity));
                if (!ExcelRankSearch.TryCopy(source, workspace, settings, out var count, out var error)) return FormulaValue.FromError(error);
                var values = workspace.Slice(0, count);
                if (percentages.Length == 1)
                {
                    var value = TryTail(percentages.Read(0, 0), count, settings, out var tail, out error)
                        ? Scalar(values, tail, settings) : FormulaValue.FromError(error);
                    if (args[1].Kind != FormulaValueKind.Array) return value;
                    var singleton = new FormulaArray(1, 1);
                    singleton[0, 0] = value;
                    return FormulaValue.FromArray(singleton);
                }

                // Order queries from the smallest retained core to the largest. One inward-to-
                // outward sweep then answers all percentages, without refitting each interval.
                if (!ExcelArrayShapeUtilities.TryCreate(context, percentages.Rows, percentages.Columns, out var result, out error)) return FormulaValue.FromError(error);
                var queryCapacity = (int)percentages.Length;
                Span<TrimQuery> queries = queryCapacity <= 128 ? stackalloc TrimQuery[128]
                    : (rentedQueries = ArrayPool<TrimQuery>.Shared.Rent(queryCapacity));
                var queryCount = 0;
                for (var row = 0; row < percentages.Rows; row++)
                    for (var column = 0; column < percentages.Columns; column++)
                    {
                        if (TryTail(percentages.Read(row, column), count, settings, out var tail, out error))
                            queries[queryCount++] = new TrimQuery(tail, row * percentages.Columns + column);
                        else result[row, column] = FormulaValue.FromError(error);
                    }
                if (queryCount == 0) return FormulaValue.FromArray(result);
                values.Sort();
                queries = queries.Slice(0, queryCount);
                queries.Sort();
                var accumulator = new ExcelExactMeanAccumulator(stackalloc uint[ExcelExactMeanAccumulator.WordCount * 2]);
                var left = count / 2;
                var right = left;
                if ((count & 1) != 0) { accumulator.Add(values[right]); right++; }
                var previousTail = -1;
                var previousValue = FormulaValue.Blank;
                foreach (var query in queries)
                {
                    while (left > query.Tail)
                    {
                        accumulator.Add(values[--left]);
                        accumulator.Add(values[right++]);
                    }
                    if (query.Tail != previousTail)
                    {
                        previousValue = ExcelDescriptiveStatistics.Number(settings, accumulator.Mean());
                        previousTail = query.Tail;
                    }
                    result[query.Index / percentages.Columns, query.Index % percentages.Columns] = previousValue;
                }
                return FormulaValue.FromArray(result);
            }
            finally
            {
                if (rentedValues != null) ArrayPool<double>.Shared.Return(rentedValues, clearArray: true);
                if (rentedQueries != null) ArrayPool<TrimQuery>.Shared.Return(rentedQueries, clearArray: true);
            }
        }

        private static FormulaValue Scalar(Span<double> values, int tail, FormulaCalculationSettings settings)
        {
            var retained = values.Length - 2 * tail;
            if (tail != 0)
            {
                // Partition only the required boundaries. Selection has an introspective
                // sort fallback; the resulting interior need not be sorted to average it.
                ExcelOrderStatistics.Select(values, tail);
                ExcelOrderStatistics.Select(values.Slice(tail), retained - 1);
            }
            var accumulator = new ExcelExactMeanAccumulator(stackalloc uint[ExcelExactMeanAccumulator.WordCount * 2]);
            for (var i = tail; i < tail + retained; i++) accumulator.Add(values[i]);
            return ExcelDescriptiveStatistics.Number(settings, accumulator.Mean());
        }

        private static bool TryTail(FormulaValue percent, int count, FormulaCalculationSettings settings, out int tail, out FormulaError error)
        {
            tail = 0;
            if (!FormulaCoercion.TryCoerceToNumber(percent, settings, out var fraction, out error)) return false;
            if (!double.IsFinite(fraction) || fraction < 0 || fraction > 1 || count == 0)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            tail = (int)Math.Floor(count * fraction / 2);
            if (count - 2 * tail == 0)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            return true;
        }

        private static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));
        private readonly struct TrimQuery : IComparable<TrimQuery>
        {
            public TrimQuery(int tail, int index) { Tail = tail; Index = index; }
            public int Tail { get; }
            public int Index { get; }
            public int CompareTo(TrimQuery other) => other.Tail.CompareTo(Tail);
        }
    }
}
