// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProCharts
{
    /// <summary>One interval observation. Streaming sources validate X and normalize incomplete bounds to a gap.</summary>
    /// <param name="X">A finite numeric or OLE Automation date coordinate, strictly increasing in the stream.</param>
    /// <param name="Lower">The lower interval boundary, or a missing observation.</param>
    /// <param name="Upper">The upper interval boundary, or a missing observation.</param>
    /// <param name="Category">An optional display label. No label is generated when omitted.</param>
    public readonly record struct ChartRangeSample(double X, double? Lower, double? Upper, string? Category = null);
}
