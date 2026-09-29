// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>An owned range snapshot and its immutable stream-identity map, captured under one source lock.</summary>
    /// <remarks>
    /// Sample indices count from zero after construction or Clear, including gaps and evicted observations.
    /// They are not retained-window positions, X coordinates or identities across separate Clear sessions.
    /// An old view remains unchanged after appends, ring wrap or Clear; map only hits on that view's snapshot.
    /// Presentation styles and formatter delegates retain their normal shared-reference semantics.
    /// </remarks>
    public sealed class StreamingRangeChartView
    {
        internal StreamingRangeChartView(ChartDataSnapshot snapshot, IReadOnlyList<long> indices,
            long firstRetained, int retainedCount, int windowStart, int windowCount)
        {
            Snapshot = snapshot; SourceSampleIndices = indices; FirstRetainedSampleIndex = firstRetained;
            RetainedCount = retainedCount; WindowStart = windowStart; WindowCount = windowCount;
        }

        /// <summary>Gets the immutable numerical/category snapshot for rendering or export.</summary>
        public ChartDataSnapshot Snapshot { get; }
        /// <summary>Gets the zero-based stream observation index for each displayed point, before reduction.</summary>
        public IReadOnlyList<long> SourceSampleIndices { get; }
        /// <summary>Gets the first retained observation's index when this view was built.</summary>
        public long FirstRetainedSampleIndex { get; }
        /// <summary>Gets the retained interval count, including gaps, when this view was built.</summary>
        public int RetainedCount { get; }
        /// <summary>Gets the total observations appended in this session when this view was built.</summary>
        public long TotalSamples => FirstRetainedSampleIndex + RetainedCount;
        /// <summary>Gets the clamped start relative to the retained history at this view's version.</summary>
        public int WindowStart { get; }
        /// <summary>Gets the clamped requested interval count before reduction.</summary>
        public int WindowCount { get; }
    }
}
