// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>Creates owned interval series and converts analytical bands to filled range areas.</summary>
    public static class ChartRangeSeries
    {
        /// <summary>Creates a linearly interpolated area between aligned lower and upper boundaries.</summary>
        /// <remarks>
        /// Collections are copied. Incomplete/non-finite boundary pairs become gaps in every channel.
        /// Finite inverted intervals are rejected. Optional X coordinates must be finite and strictly
        /// increasing; configure a numeric/date/log category axis to position them proportionally.
        /// Values contains interval midpoints; LowValues and HighValues contain the actual boundaries.
        /// Source collections must remain stable during construction.
        /// </remarks>
        public static ChartSeriesSnapshot CreateArea(string? name, IReadOnlyList<double?> lower,
            IReadOnlyList<double?> upper, IReadOnlyList<double>? xValues = null,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary,
            ChartSeriesStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(lower);
            ArgumentNullException.ThrowIfNull(upper);
            if (lower.Count != upper.Count) throw new ArgumentException("Boundary counts must match.", nameof(upper));
            if (xValues != null && xValues.Count != lower.Count) throw new ArgumentException("X coordinates must align with every interval.", nameof(xValues));
            double?[] lows = new double?[lower.Count], highs = new double?[lower.Count], values = new double?[lower.Count];
            double[]? x = xValues != null ? new double[lower.Count] : null;
            for (int i = 0; i < lower.Count; i++)
            {
                if (x != null)
                {
                    double coordinate = xValues![i];
                    if (!double.IsFinite(coordinate) || (i > 0 && coordinate <= x[i - 1]))
                        throw new ArgumentException("Range-area X coordinates must be finite and strictly increasing.", nameof(xValues));
                    x[i] = coordinate;
                }
                if (lower[i] is not double low || upper[i] is not double high || !double.IsFinite(low) || !double.IsFinite(high)) continue;
                if (low > high) throw new ArgumentException("A finite lower boundary cannot exceed its upper boundary.", nameof(lower));
                lows[i] = low;
                highs[i] = high;
                double difference = high - low;
                values[i] = double.IsFinite(difference) ? Math.FusedMultiplyAdd(difference, 0.5, low) : low / 2 + high / 2;
            }
            return new ChartSeriesSnapshot(name, ChartSeriesKind.RangeArea, Array.AsReadOnly(values),
                x == null ? null : Array.AsReadOnly(x), valueAxisAssignment: valueAxisAssignment,
                style: style, highValues: Array.AsReadOnly(highs), lowValues: Array.AsReadOnly(lows));
        }

        /// <summary>Creates a filled range from Bollinger/Donchian boundaries, preserving the middle line's X and axis assignment.</summary>
        /// <remarks>The middle line remains independently usable as an overlay. All range channels are copied.</remarks>
        public static ChartSeriesSnapshot ToRangeArea(this ChartBandSeries bands, string? name = null, ChartSeriesStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(bands);
            return CreateArea(name ?? bands.Middle.Name, bands.Lower.Values, bands.Upper.Values,
                bands.Middle.XValues, bands.Middle.ValueAxisAssignment, style);
        }
    }
}
