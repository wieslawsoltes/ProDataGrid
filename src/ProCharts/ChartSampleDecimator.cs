// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>Selects original samples without changing their coordinates or bridging missing data.</summary>
    public static class ChartSampleDecimator
    {
        /// <summary>
        /// Returns increasing source indices. The requested count is a soft budget: gap markers and
        /// the endpoints of every finite run take precedence over the budget. Bucket selects evenly
        /// spaced original samples; MinMax preserves bucket extrema; Adaptive uses MinMax.
        /// </summary>
        /// <remarks>Runs in O(n) time and uses O(output size) memory. Input must remain unchanged during the call.</remarks>
        public static int[] SelectIndices(IReadOnlyList<ChartSample> samples, int maxPoints,
            ChartDownsampleMode mode = ChartDownsampleMode.Adaptive)
        {
            ArgumentNullException.ThrowIfNull(samples);
            if (maxPoints < 2)
                throw new ArgumentOutOfRangeException(nameof(maxPoints), "At least two points are required.");
            if ((uint)mode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(mode));

            int count = samples.Count;
            if (mode == ChartDownsampleMode.None || count <= maxPoints)
            {
                int[] all = new int[count];
                for (int i = 0; i < count; i++) all[i] = i;
                return all;
            }

            int finiteCount = 0;
            int required = 0;
            int gapCount = 0;
            for (int i = 0; i < count;)
            {
                bool finite = IsFinite(samples[i]);
                int start = i++;
                while (i < count && IsFinite(samples[i]) == finite) i++;
                if (finite)
                {
                    finiteCount += i - start;
                    required += Math.Min(2, i - start);
                }
                else { required++; gapCount++; }
            }

            if (finiteCount == 0) return new[] { 0 };
            int extraBudget = Math.Max(0, maxPoints - required);
            int eligible = finiteCount - (required - gapCount);
            List<int> result = new(Math.Min(count, Math.Max(maxPoints, required)));
            long assignedWeight = 0;
            int assignedExtra = 0;
            for (int i = 0; i < count;)
            {
                if (!IsFinite(samples[i]))
                {
                    result.Add(i++);
                    while (i < count && !IsFinite(samples[i])) i++;
                    continue;
                }

                int start = i++;
                while (i < count && IsFinite(samples[i])) i++;
                int length = i - start;
                int minimum = Math.Min(2, length);
                assignedWeight += length - minimum;
                int cumulativeExtra = eligible == 0 ? 0 : (int)(assignedWeight * extraBudget / eligible);
                int budget = Math.Min(length, minimum + cumulativeExtra - assignedExtra);
                assignedExtra = cumulativeExtra;
                SelectRun(samples, start, length, budget, mode, result);
            }
            return result.ToArray();
        }

        private static bool IsFinite(ChartSample sample) => double.IsFinite(sample.X) &&
            sample.Value is double value && double.IsFinite(value);

        private static void SelectRun(IReadOnlyList<ChartSample> samples, int start, int length,
            int budget, ChartDownsampleMode mode, List<int> output)
        {
            if (length <= budget)
            {
                for (int i = start; i < start + length; i++) output.Add(i);
                return;
            }
            output.Add(start);
            if (budget <= 2)
            {
                if (length > 1) output.Add(start + length - 1);
                return;
            }
            if (mode == ChartDownsampleMode.Lttb || budget == 3)
                SelectTriangles(samples, start, length, budget, output);
            else if (mode == ChartDownsampleMode.Bucket)
            {
                for (int i = 1; i < budget - 1; i++)
                    output.Add(start + (int)((long)i * (length - 1) / (budget - 1)));
            }
            else
            {
                int buckets = (budget - 2) / 2;
                for (int bucket = 0; bucket < buckets; bucket++)
                {
                    int first = start + 1 + (int)((long)bucket * (length - 2) / buckets);
                    int end = start + 1 + (int)((long)(bucket + 1) * (length - 2) / buckets);
                    int min = first, max = first;
                    for (int i = first + 1; i < end; i++)
                    {
                        if (samples[i].Value!.Value < samples[min].Value!.Value) min = i;
                        if (samples[i].Value!.Value > samples[max].Value!.Value) max = i;
                    }
                    output.Add(Math.Min(min, max));
                    if (min != max) output.Add(Math.Max(min, max));
                }
            }
            output.Add(start + length - 1);
        }

        private static void SelectTriangles(IReadOnlyList<ChartSample> samples, int start,
            int length, int budget, List<int> output)
        {
            double scaleX = 1, scaleY = 1;
            for (int i = start; i < start + length; i++)
            {
                scaleX = Math.Max(scaleX, Math.Abs(samples[i].X));
                scaleY = Math.Max(scaleY, Math.Abs(samples[i].Value!.Value));
            }
            int anchor = start;
            double width = (length - 2d) / (budget - 2);
            for (int bucket = 0; bucket < budget - 2; bucket++)
            {
                int averageStart = start + 1 + (int)Math.Floor((bucket + 1) * width);
                int averageEnd = Math.Min(start + length, start + 1 + (int)Math.Floor((bucket + 2) * width));
                double averageX = 0, averageY = 0;
                for (int i = averageStart; i < averageEnd; i++)
                {
                    averageX += samples[i].X / scaleX;
                    averageY += samples[i].Value!.Value / scaleY;
                }
                int averageCount = averageEnd - averageStart;
                averageX /= averageCount;
                averageY /= averageCount;
                int first = start + 1 + (int)Math.Floor(bucket * width);
                int end = Math.Min(start + length - 1, start + 1 + (int)Math.Floor((bucket + 1) * width));
                double ax = samples[anchor].X / scaleX;
                double ay = samples[anchor].Value!.Value / scaleY;
                int best = first;
                double bestArea = -1;
                for (int i = first; i < end; i++)
                {
                    double area = Math.Abs((ax - averageX) * (samples[i].Value!.Value / scaleY - ay) -
                        (ax - samples[i].X / scaleX) * (averageY - ay));
                    if (area > bestArea) { bestArea = area; best = i; }
                }
                output.Add(best);
                anchor = best;
            }
        }
    }
}
