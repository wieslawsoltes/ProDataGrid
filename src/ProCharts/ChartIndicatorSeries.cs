// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProCharts
{
    /// <summary>Three aligned, independently renderable series describing a lower, middle and upper band.</summary>
    public sealed class ChartBandSeries
    {
        internal ChartBandSeries(ChartSeriesSnapshot lower, ChartSeriesSnapshot middle, ChartSeriesSnapshot upper)
        { Lower = lower; Middle = middle; Upper = upper; }

        /// <summary>Gets the lower boundary.</summary>
        public ChartSeriesSnapshot Lower { get; }
        /// <summary>Gets the center line.</summary>
        public ChartSeriesSnapshot Middle { get; }
        /// <summary>Gets the upper boundary.</summary>
        public ChartSeriesSnapshot Upper { get; }
    }

    /// <summary>Aligned moving-average convergence/divergence line, signal and histogram series.</summary>
    public sealed class ChartMacdSeries
    {
        internal ChartMacdSeries(ChartSeriesSnapshot line, ChartSeriesSnapshot signal, ChartSeriesSnapshot histogram)
        { Line = line; Signal = signal; Histogram = histogram; }

        /// <summary>Gets the fast EMA minus the slow EMA.</summary>
        public ChartSeriesSnapshot Line { get; }
        /// <summary>Gets the EMA of the MACD line.</summary>
        public ChartSeriesSnapshot Signal { get; }
        /// <summary>Gets the MACD line minus the signal, suitable for column rendering.</summary>
        public ChartSeriesSnapshot Histogram { get; }
    }
}
