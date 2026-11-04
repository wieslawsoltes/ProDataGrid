// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    // Sort only the privately owned bin descriptors. Observations are visited once and
    // assigned by lower bound; no numeric copy or per-bin scan of the data is required.
    internal sealed class FrequencyFunction : ExcelFunctionBase
    {
        public FrequencyFunction() : base("FREQUENCY", new FormulaFunctionInfo(2, 2)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 2) return Error(FormulaErrorType.Value);
            var data = new ExcelArrayOperand(args[0]);
            var inputBins = new ExcelArrayOperand(args[1]);
            if (data.IsUnresolvedReference || inputBins.IsUnresolvedReference) return Error(FormulaErrorType.Value);
            var settings = context.EvaluationContext.Workbook.Settings;
            if (data.Length > settings.MaximumArrayCellCount || inputBins.Length > settings.MaximumArrayCellCount ||
                inputBins.Length > Array.MaxLength) return Error(FormulaErrorType.Num);

            FrequencyBin[]? rentedBins = null;
            int[]? rentedCounts = null;
            try
            {
                var capacity = (int)inputBins.Length;
                Span<FrequencyBin> bins = capacity <= 128 ? stackalloc FrequencyBin[128]
                    : (rentedBins = ArrayPool<FrequencyBin>.Shared.Rent(capacity));
                var count = 0;
                for (var row = 0; row < inputBins.Rows; row++)
                    for (var column = 0; column < inputBins.Columns; column++)
                    {
                        if (!ExcelArrayOperand.TryNumeric(inputBins.Read(row, column), settings,
                            out var number, out var numeric, out var error)) return FormulaValue.FromError(error);
                        if (numeric) bins[count] = new FrequencyBin(number, count++);
                    }
                if (count >= 1048576 || count >= settings.MaximumArrayCellCount) return Error(FormulaErrorType.Num);
                var sorted = bins.Slice(0, count);
                sorted.Sort();
                Span<int> counts = count < 128 ? stackalloc int[128]
                    : (rentedCounts = ArrayPool<int>.Shared.Rent(count + 1));
                counts = counts.Slice(0, count + 1);
                counts.Clear();

                for (var row = 0; row < data.Rows; row++)
                    for (var column = 0; column < data.Columns; column++)
                    {
                        if (!ExcelArrayOperand.TryNumeric(data.Read(row, column), settings,
                            out var number, out var numeric, out var error)) return FormulaValue.FromError(error);
                        if (!numeric) continue;
                        var bin = LowerBound(sorted, number);
                        // Equal boundaries are ordered by original position, so the first
                        // occurrence receives the count and subsequent duplicate bins stay zero.
                        counts[bin == count ? count : sorted[bin].OriginalIndex]++;
                    }

                // Allocate output only after all inputs have passed validation.
                var result = new FormulaArray(count + 1, 1);
                for (var i = 0; i < counts.Length; i++)
                    result[i, 0] = ExcelFunctionUtilities.CreateNumber(context, counts[i]);
                return FormulaValue.FromArray(result);
            }
            finally
            {
                if (rentedBins != null) ArrayPool<FrequencyBin>.Shared.Return(rentedBins, clearArray: true);
                if (rentedCounts != null) ArrayPool<int>.Shared.Return(rentedCounts, clearArray: true);
            }
        }

        private static int LowerBound(ReadOnlySpan<FrequencyBin> bins, double value)
        {
            var low = 0;
            var high = bins.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (bins[middle].Boundary < value) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));

        private readonly struct FrequencyBin : IComparable<FrequencyBin>
        {
            public FrequencyBin(double boundary, int originalIndex) { Boundary = boundary; OriginalIndex = originalIndex; }
            public double Boundary { get; }
            public int OriginalIndex { get; }
            public int CompareTo(FrequencyBin other)
            {
                var order = Boundary.CompareTo(other.Boundary);
                return order == 0 ? OriginalIndex.CompareTo(other.OriginalIndex) : order;
            }
        }
    }
}
