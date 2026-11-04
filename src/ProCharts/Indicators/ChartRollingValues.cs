// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProCharts
{
    /// <summary>Aligned statistics for one complete rolling window. Missing results are null, never infinity.</summary>
    /// <param name="Mean">Arithmetic mean, or warmup/gap.</param>
    /// <param name="WeightedMean">Linear weights from one for the oldest to Period for the newest observation.</param>
    /// <param name="PopulationStandardDeviation">Deviation with divisor Period.</param>
    /// <param name="SampleStandardDeviation">Deviation with divisor Period minus one; null for Period one.</param>
    public readonly record struct ChartRollingStatisticsValue(double? Mean, double? WeightedMean,
        double? PopulationStandardDeviation, double? SampleStandardDeviation);

    /// <summary>An aligned lower/middle/upper result for a streaming band or channel calculator.</summary>
    /// <param name="Lower">The lower boundary, or warmup/gap/unrepresentable output.</param>
    /// <param name="Middle">The center line, or warmup/gap.</param>
    /// <param name="Upper">The upper boundary, or warmup/gap/unrepresentable output.</param>
    public readonly record struct ChartBandValue(double? Lower, double? Middle, double? Upper);
}
