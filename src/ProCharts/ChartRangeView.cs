// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>An owned display snapshot and its original source-point mapping.</summary>
    public sealed class ChartRangeView
    {
        internal ChartRangeView(ChartDataSnapshot snapshot, IReadOnlyList<int> sourcePointIndices,
            int sourceCount, int windowStart, int windowCount)
        {
            Snapshot = snapshot;
            SourcePointIndices = sourcePointIndices;
            SourceCount = sourceCount;
            WindowStart = windowStart;
            WindowCount = windowCount;
        }

        /// <summary>Gets the immutable display data accepted by the existing model and renderer.</summary>
        public ChartDataSnapshot Snapshot { get; }
        /// <summary>Maps each display PointIndex to the original input index at Snapshot.Version.</summary>
        public IReadOnlyList<int> SourcePointIndices { get; }
        /// <summary>Gets the total input size when this view was built.</summary>
        public int SourceCount { get; }
        /// <summary>Gets the clamped original window start.</summary>
        public int WindowStart { get; }
        /// <summary>Gets the original window size before reduction, including gaps.</summary>
        public int WindowCount { get; }
    }
}
