// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>An owned, aligned multi-series snapshot and common observation identity map.</summary>
    /// <remarks>
    /// Captured atomically under the source lock. Identities are zero-based appended-row numbers since construction
    /// or Clear, including gaps and evicted rows. They are not X coordinates or identities across Clear sessions.
    /// Map a hit only against the view whose snapshot produced it. Later appends and Clear do not change this view.
    /// Styles and formatter delegates remain shared presentation references.
    /// </remarks>
    public sealed class StreamingMultiSeriesChartView
    {
        internal StreamingMultiSeriesChartView(ChartDataSnapshot snapshot, IReadOnlyList<long> indices,
            long firstRetained, int retainedCount, int windowStart, int windowCount)
        {
            Snapshot = snapshot; SourceSampleIndices = indices; FirstRetainedSampleIndex = firstRetained;
            RetainedCount = retainedCount; WindowStart = windowStart; WindowCount = windowCount;
        }

        /// <summary>Gets the numerical/category snapshot. Every series shares the same owned X collection.</summary>
        public ChartDataSnapshot Snapshot { get; }
        /// <summary>Gets the original row number for each output point, shared by every series.</summary>
        public IReadOnlyList<long> SourceSampleIndices { get; }
        /// <summary>Gets the first retained row number at capture time.</summary>
        public long FirstRetainedSampleIndex { get; }
        /// <summary>Gets retained rows before windowing/reduction, including gaps.</summary>
        public int RetainedCount { get; }
        /// <summary>Gets accepted rows since construction or Clear at capture time.</summary>
        public long TotalSamples => FirstRetainedSampleIndex + RetainedCount;
        /// <summary>Gets the clamped window offset relative to retained history.</summary>
        public int WindowStart { get; }
        /// <summary>Gets the clamped window row count before reduction.</summary>
        public int WindowCount { get; }
    }
}
