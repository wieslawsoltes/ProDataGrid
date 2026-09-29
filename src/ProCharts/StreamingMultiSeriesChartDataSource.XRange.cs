// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    public sealed partial class StreamingMultiSeriesChartDataSource
    {
        /// <summary>Captures retained observations within inclusive source-X bounds without first copying or scanning the full history.</summary>
        /// <param name="minimumX">Finite inclusive lower bound, in the same coordinate units as Append.</param>
        /// <param name="maximumX">Finite inclusive upper bound, not less than minimumX.</param>
        /// <param name="maxPoints">Per-series soft selection budget, with the same semantics as BuildView.</param>
        /// <param name="downsampleMode">Selection mode, with the same semantics as BuildView.</param>
        /// <param name="includeBoundaryNeighbors">Retain the immediately preceding and following rows when the interval intersects the retained X extent.</param>
        /// <returns>An owned aligned view; its window metadata and original identities belong to the same capture as its values.</returns>
        /// <remarks>
        /// Binary searches read the logical ring directly in O(log retained rows) time. Search, optional neighbor expansion,
        /// selection and snapshot capture share one lock, so eviction cannot invalidate the resolved window in between.
        /// Uncached capture still costs O(window rows * series count) and holds the source lock. Equivalent normalized ordinal
        /// and coordinate windows share the existing single-entry cache; warm equivalent queries allocate no managed memory.
        /// Bounds between adjacent observations produce an empty view unless neighbors are requested, in which case both
        /// adjacent rows are included. Intervals wholly outside retained history remain empty even with neighbors enabled.
        /// Missing values do not remove source-X rows, and neighbors never interpolate, replace missing data or reach evicted rows.
        /// Reduction preserves the common identity map and the existing per-channel gap/endpoint conventions.
        /// This API does not change model requests, axis limits or renderer positioning. Date coordinates must use the same
        /// encoding as the appended X values; no calendar or timezone conversion is performed by this source.
        /// </remarks>
        public StreamingMultiSeriesChartView BuildViewByX(double minimumX, double maximumX,
            int? maxPoints = null, ChartDownsampleMode downsampleMode = ChartDownsampleMode.Adaptive,
            bool includeBoundaryNeighbors = false)
        {
            if (!double.IsFinite(minimumX))
                throw new ArgumentOutOfRangeException(nameof(minimumX), "The lower X bound must be finite.");
            if (!double.IsFinite(maximumX))
                throw new ArgumentOutOfRangeException(nameof(maximumX), "The upper X bound must be finite.");
            if (minimumX > maximumX)
                throw new ArgumentException("The upper X bound cannot precede the lower bound.", nameof(maximumX));
            if ((uint)downsampleMode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(downsampleMode), "Unknown downsampling mode.");

            lock (_gate)
            {
                int start = FindXBound(minimumX, upper: false);
                int end = FindXBound(maximumX, upper: true);
                if (includeBoundaryNeighbors && _count != 0 &&
                    maximumX >= _x[RingIndex(0)] && minimumX <= _x[RingIndex(_count - 1)])
                {
                    if (start > 0) start--;
                    if (end < _count) end++;
                }

                return BuildViewCore(start, end - start, maxPoints, downsampleMode);
            }
        }

        /// <summary>Captures the snapshot portion of an inclusive source-X query.</summary>
        /// <param name="minimumX">Finite inclusive lower bound in source coordinate units.</param>
        /// <param name="maximumX">Finite inclusive upper bound, not less than minimumX.</param>
        /// <param name="maxPoints">Per-series soft selection budget.</param>
        /// <param name="downsampleMode">Selection mode.</param>
        /// <param name="includeBoundaryNeighbors">Whether to include adjacent retained rows at intersecting boundaries.</param>
        /// <returns>The same cached snapshot that BuildViewByX would return for the equivalent query.</returns>
        /// <remarks>Use BuildViewByX when original row identities or capture-consistent history metadata are needed.</remarks>
        public ChartDataSnapshot BuildSnapshotByX(double minimumX, double maximumX,
            int? maxPoints = null, ChartDownsampleMode downsampleMode = ChartDownsampleMode.Adaptive,
            bool includeBoundaryNeighbors = false)
            => BuildViewByX(minimumX, maximumX, maxPoints, downsampleMode, includeBoundaryNeighbors).Snapshot;

        // The caller holds _gate. lower_bound is the first X >= value; upper_bound is the first X > value.
        // Midpoint and physical ring indexing avoid overflowing int even for the largest supported arrays.
        private int FindXBound(double value, bool upper)
        {
            int low = 0;
            int high = _count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                double x = _x[RingIndex(middle)];
                if (x < value || (upper && x == value)) low = middle + 1;
                else high = middle;
            }
            return low;
        }
    }
}
