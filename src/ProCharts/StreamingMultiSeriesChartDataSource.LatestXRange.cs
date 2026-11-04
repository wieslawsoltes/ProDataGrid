// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    public sealed partial class StreamingMultiSeriesChartDataSource
    {
        /// <summary>Captures the latest inclusive coordinate span, anchoring its end to the newest retained row.</summary>
        /// <param name="span">Finite nonnegative span in the source's X units. Zero selects the newest row.</param>
        /// <param name="maxPoints">Per-series soft selection budget, with the same semantics as BuildView.</param>
        /// <param name="downsampleMode">Selection mode, with the same semantics as BuildView.</param>
        /// <param name="includeBoundaryNeighbors">Include the immediately preceding retained row when available.</param>
        /// <returns>An owned aligned view whose newest coordinate, window and identities belong to one capture.</returns>
        /// <remarks>
        /// Newest-coordinate lookup, lower-bound search, optional neighbor expansion and snapshot capture share the
        /// source lock. Callers do not need to read a latest X value separately and race an append or eviction before
        /// capture. Missing values in the newest row still advance the coordinate window; they are not skipped.
        /// A zero span with neighbors enabled includes the preceding row as well. Empty history produces an empty view.
        /// Coordinates have the units supplied to Append: for OLE Automation date coordinates, spans are measured in
        /// days, not seconds. No calendar/timezone conversion occurs. Subtracting the span uses double arithmetic;
        /// overflow below the finite domain includes all earlier retained rows, and sub-ULP spans can round to zero width.
        /// This is an explicit query, not an automatic model viewport or a timer. Each invocation re-anchors to its own
        /// captured latest row. Reduction, gaps, cache ownership and complexity follow BuildViewByX.
        /// </remarks>
        public StreamingMultiSeriesChartView BuildLatestViewByX(double span, int? maxPoints = null,
            ChartDownsampleMode downsampleMode = ChartDownsampleMode.Adaptive, bool includeBoundaryNeighbors = false)
        {
            if (!double.IsFinite(span) || span < 0)
                throw new ArgumentOutOfRangeException(nameof(span), "The coordinate span must be finite and nonnegative.");
            if ((uint)downsampleMode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(downsampleMode), "Unknown downsampling mode.");

            lock (_gate)
            {
                if (_count == 0) return BuildViewCore(0, 0, maxPoints, downsampleMode);
                double latestX = _x[RingIndex(_count - 1)];
                double minimumX = latestX - span;
                // All stored coordinates are finite, so saturation here loses no eligible observation.
                if (double.IsNegativeInfinity(minimumX)) minimumX = -double.MaxValue;
                int start = FindXBound(minimumX, upper: false);
                if (includeBoundaryNeighbors && start > 0) start--;
                return BuildViewCore(start, _count - start, maxPoints, downsampleMode);
            }
        }

        /// <summary>Captures the snapshot portion of a trailing coordinate-span query.</summary>
        /// <param name="span">Finite nonnegative span in source X units, measured back from the newest retained row.</param>
        /// <param name="maxPoints">Per-series soft selection budget.</param>
        /// <param name="downsampleMode">Selection mode.</param>
        /// <param name="includeBoundaryNeighbors">Whether to include the immediately preceding retained row.</param>
        /// <returns>The same cached snapshot that an equivalent BuildLatestViewByX call would return.</returns>
        /// <remarks>Use BuildLatestViewByX when the snapshot's original row identities are needed.</remarks>
        public ChartDataSnapshot BuildLatestSnapshotByX(double span, int? maxPoints = null,
            ChartDownsampleMode downsampleMode = ChartDownsampleMode.Adaptive, bool includeBoundaryNeighbors = false)
            => BuildLatestViewByX(span, maxPoints, downsampleMode, includeBoundaryNeighbors).Snapshot;
    }
}
