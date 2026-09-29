// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts.Skia
{
    public sealed partial class SkiaChartRenderer
    {
        private static void FillVisibleNumericTicks(List<double> ticks, double minimum, double maximum, int desiredTicks)
        {
            ticks.Clear();
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) return;
            if (maximum < minimum) (minimum, maximum) = (maximum, minimum);
            if (maximum == minimum) { ticks.Add(minimum); return; }

            // AxisTickCount is a density hint, not permission to allocate or iterate without bound.
            // Retain the existing 1/2/5 nice-step policy for ordinary ranges, but never emit values
            // outside the actual domain: clamping such ticks to the plot edge mislabels the axis.
            int count = Math.Clamp(desiredTicks, 2, 1024);
            double span = maximum - minimum;
            double niceRange = double.IsFinite(span) ? NiceNumber(span, false) : double.PositiveInfinity;
            double rawStep = double.IsFinite(niceRange) && niceRange > 0
                ? niceRange / (count - 1)
                : double.IsFinite(span) ? span / (count - 1) : ((maximum / 2 - minimum / 2) / (count - 1)) * 2;
            if (rawStep == 0) rawStep = double.Epsilon;
            double step = NiceNumber(rawStep, true);
            if (!double.IsFinite(step) || step <= 0) step = rawStep;
            if (!double.IsFinite(step) || step <= 0)
            {
                ticks.Add(minimum); ticks.Add(maximum);
                return;
            }

            // A sub-ULP step at a large common offset cannot advance a repeated-addition loop.
            // Respect representable spacing, compute each candidate directly, and bound iterations.
            double resolution = Math.Max(Math.BitIncrement(minimum) - minimum, maximum - Math.BitDecrement(maximum));
            if (double.IsFinite(resolution)) step = Math.Max(step, resolution);
            double first = Math.Ceiling(minimum / step) * step;
            double previous = double.NegativeInfinity;
            for (int i = 0; i < 4096; i++)
            {
                double value = Math.FusedMultiplyAdd(i, step, first);
                if (!double.IsFinite(value) || value > maximum) break;
                if (value < minimum || value <= previous) continue;
                ticks.Add(value);
                previous = value;
            }
            if (ticks.Count < 2)
            {
                ticks.Clear(); ticks.Add(minimum); ticks.Add(maximum);
            }
        }

        private static double NormalizeFiniteAxisRatio(double value, double minimum, double maximum)
        {
            if (maximum <= minimum || value <= minimum) return 0;
            if (value >= maximum) return 1;
            double numerator = value - minimum, denominator = maximum - minimum;
            // Finite opposite-sign endpoints can have an infinite difference. Scale only that
            // case so ordinary ranges retain their existing arithmetic and cached hit positions.
            return double.IsFinite(numerator) && double.IsFinite(denominator)
                ? numerator / denominator : (value / 2 - minimum / 2) / (maximum / 2 - minimum / 2);
        }
    }
}
