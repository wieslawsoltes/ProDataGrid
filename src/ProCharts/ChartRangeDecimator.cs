// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>Selects one shared set of original indices for both boundaries of an interval chart.</summary>
    public static class ChartRangeDecimator
    {
        /// <summary>
        /// Selects increasing original indices from a clamped window, retaining both boundaries'
        /// bucket minima/maxima, finite-run endpoints, and a separator for every invalid run.
        /// </summary>
        /// <remarks>
        /// MaxPoints must be at least six. The budget is soft when gap topology requires more:
        /// reserve up to six points per finite run and one point per invalid run. For one finite
        /// run the output never exceeds the budget. Minima/maxima ties use their first occurrence.
        /// Null, non-finite, or inverted pairs are gaps. No values or coordinates are averaged.
        /// Selected indices refer to the full input, not the window. Source lists must remain stable.
        /// Work is O(window size), with O(output size) auxiliary memory. This is a visual reduction,
        /// not a guarantee that every omitted interval lies inside the interpolated reduced envelope.
        /// </remarks>
        public static int[] SelectIndices(IReadOnlyList<double?> lower, IReadOnlyList<double?> upper,
            int maxPoints, int windowStart = 0, int? windowCount = null)
        {
            ArgumentNullException.ThrowIfNull(lower);
            ArgumentNullException.ThrowIfNull(upper);
            if (lower.Count != upper.Count) throw new ArgumentException("Boundary counts must match.", nameof(upper));
            if (maxPoints < 6) throw new ArgumentOutOfRangeException(nameof(maxPoints), "Paired extrema require a budget of at least six.");
            int start = Math.Clamp(windowStart, 0, lower.Count);
            int count = Math.Clamp(windowCount ?? (lower.Count - start), 0, lower.Count - start);
            int end = start + count;
            if (count <= maxPoints)
            {
                int[] all = new int[count];
                for (int i = 0; i < count; i++) all[i] = start + i;
                return all;
            }

            int required = 0, eligible = 0;
            for (int i = start; i < end;)
            {
                bool finite = IsFinite(lower, upper, i);
                int first = i++;
                while (i < end && IsFinite(lower, upper, i) == finite) i++;
                int minimum = finite ? Math.Min(6, i - first) : 1;
                required += minimum;
                if (finite) eligible += i - first - minimum;
            }

            List<int> result = new(Math.Min(count, Math.Max(required, maxPoints)));
            int extra = Math.Max(0, maxPoints - required), assignedExtra = 0;
            long assignedWeight = 0;
            for (int i = start; i < end;)
            {
                if (!IsFinite(lower, upper, i))
                {
                    result.Add(i++);
                    while (i < end && !IsFinite(lower, upper, i)) i++;
                    continue;
                }
                int first = i++;
                while (i < end && IsFinite(lower, upper, i)) i++;
                int length = i - first, minimum = Math.Min(6, length);
                assignedWeight += length - minimum;
                int cumulative = eligible == 0 ? 0 : (int)(assignedWeight * extra / eligible);
                int budget = Math.Min(length, minimum + cumulative - assignedExtra);
                assignedExtra = cumulative;
                SelectRun(lower, upper, first, length, budget, result);
            }
            return result.ToArray();
        }

        private static bool IsFinite(IReadOnlyList<double?> lower, IReadOnlyList<double?> upper, int index) =>
            lower[index] is double low && upper[index] is double high &&
            double.IsFinite(low) && double.IsFinite(high) && low <= high;

        private static void SelectRun(IReadOnlyList<double?> lower, IReadOnlyList<double?> upper,
            int start, int length, int budget, List<int> output)
        {
            if (length <= budget)
            {
                for (int i = start; i < start + length; i++) output.Add(i);
                return;
            }
            output.Add(start);
            int buckets = (budget - 2) / 4;
            Span<int> extrema = stackalloc int[4];
            for (int bucket = 0; bucket < buckets; bucket++)
            {
                int first = start + 1 + (int)((long)bucket * (length - 2) / buckets);
                int end = start + 1 + (int)((long)(bucket + 1) * (length - 2) / buckets);
                int lowMin = first, lowMax = first, highMin = first, highMax = first;
                double minLow = lower[first]!.Value, maxLow = minLow;
                double minHigh = upper[first]!.Value, maxHigh = minHigh;
                for (int i = first + 1; i < end; i++)
                {
                    double low = lower[i]!.Value, high = upper[i]!.Value;
                    if (low < minLow) { minLow = low; lowMin = i; }
                    if (low > maxLow) { maxLow = low; lowMax = i; }
                    if (high < minHigh) { minHigh = high; highMin = i; }
                    if (high > maxHigh) { maxHigh = high; highMax = i; }
                }
                extrema[0] = lowMin; extrema[1] = lowMax; extrema[2] = highMin; extrema[3] = highMax;
                // Four candidates: stable insertion sort avoids sorting data or allocating per bucket.
                for (int i = 1; i < extrema.Length; i++)
                {
                    int value = extrema[i], j = i - 1;
                    while (j >= 0 && extrema[j] > value) { extrema[j + 1] = extrema[j]; j--; }
                    extrema[j + 1] = value;
                }
                for (int i = 0; i < extrema.Length; i++)
                    if (i == 0 || extrema[i] != extrema[i - 1]) output.Add(extrema[i]);
            }
            output.Add(start + length - 1);
        }
    }
}
