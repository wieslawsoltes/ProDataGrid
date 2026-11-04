// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    public sealed partial class StreamingMultiSeriesChartDataSource
    {
        // Resolve coordinate filtering, then an optional ordinal subwindow, before applying common-index reduction.
        // All source operations remain under one lock; the adapter never discovers offsets from a detached snapshot.
        internal StreamingMultiSeriesChartView CaptureCoordinateWindow(ChartCoordinateWindow window, ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ChartDownsampleMode mode = request.DownsampleMode;
            if ((uint)mode > (uint)ChartDownsampleMode.Adaptive)
                throw new ArgumentOutOfRangeException(nameof(request), "Unknown downsampling mode.");
            int? requestedStart = request.WindowStart, requestedCount = request.WindowCount, budget = request.MaxPoints;
            lock (_gate)
            {
                var selected = ResolveCoordinateWindowCore(window);
                int skip = Math.Clamp(requestedStart ?? 0, 0, selected.Count);
                int count = Math.Clamp(requestedCount ?? (selected.Count - skip), 0, selected.Count - skip);
                return BuildViewCore(selected.Start + skip, count, budget, mode);
            }
        }

        internal int CountCoordinateWindow(ChartCoordinateWindow window)
        {
            lock (_gate) return ResolveCoordinateWindowCore(window).Count;
        }

        private (int Start, int Count) ResolveCoordinateWindowCore(ChartCoordinateWindow window)
        {
            if (window.Kind == ChartCoordinateWindowKind.All || _count == 0) return (0, _count);
            if (window.Kind == ChartCoordinateWindowKind.Latest)
            {
                double minimum = _x[RingIndex(_count - 1)] - window.Span;
                if (double.IsNegativeInfinity(minimum)) minimum = -double.MaxValue;
                int start = FindXBound(minimum, upper: false);
                if (window.IncludeBoundaryNeighbors && start > 0) start--;
                return (start, _count - start);
            }
            int first = FindXBound(window.MinimumX, upper: false);
            int end = FindXBound(window.MaximumX, upper: true);
            if (window.IncludeBoundaryNeighbors && window.MaximumX >= _x[RingIndex(0)] &&
                window.MinimumX <= _x[RingIndex(_count - 1)])
            {
                if (first > 0) first--;
                if (end < _count) end++;
            }
            return (first, end - first);
        }
    }
}
