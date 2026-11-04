// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>How a coordinate window selects retained observations.</summary>
    public enum ChartCoordinateWindowKind
    {
        /// <summary>Use all retained observations before optional ordinal windowing.</summary>
        All,
        /// <summary>Use an inclusive, fixed coordinate interval.</summary>
        Fixed,
        /// <summary>Measure a trailing span from the newest retained observation.</summary>
        Latest
    }

    /// <summary>An immutable, validated coordinate-selection policy. The default value selects all retained rows.</summary>
    /// <remarks>
    /// Bounds and spans use the original X units: OLE Automation dates use days, not seconds.
    /// No clock, timezone conversion, interpolation or renderer axis settings are implied.
    /// </remarks>
    public readonly record struct ChartCoordinateWindow
    {
        private ChartCoordinateWindow(ChartCoordinateWindowKind kind, double minimum, double maximum,
            double span, bool neighbors)
        { Kind = kind; MinimumX = minimum; MaximumX = maximum; Span = span; IncludeBoundaryNeighbors = neighbors; }

        /// <summary>Gets the selection policy.</summary>
        public ChartCoordinateWindowKind Kind { get; }
        /// <summary>Gets the inclusive lower coordinate for Fixed; zero otherwise.</summary>
        public double MinimumX { get; }
        /// <summary>Gets the inclusive upper coordinate for Fixed; zero otherwise.</summary>
        public double MaximumX { get; }
        /// <summary>Gets the trailing coordinate span for Latest; zero otherwise.</summary>
        public double Span { get; }
        /// <summary>Gets whether intersecting coordinate windows include immediate boundary neighbors.</summary>
        public bool IncludeBoundaryNeighbors { get; }
        /// <summary>Gets a policy selecting all retained rows.</summary>
        public static ChartCoordinateWindow All => default;

        /// <summary>Creates an inclusive fixed interval. Equal endpoints are permitted; both must be finite.</summary>
        /// <remarks>Intervals wholly outside retained X history remain empty even when neighbors are requested.</remarks>
        public static ChartCoordinateWindow Between(double minimumX, double maximumX, bool includeBoundaryNeighbors = false)
        {
            if (!double.IsFinite(minimumX)) throw new ArgumentOutOfRangeException(nameof(minimumX));
            if (!double.IsFinite(maximumX)) throw new ArgumentOutOfRangeException(nameof(maximumX));
            if (minimumX > maximumX) throw new ArgumentException("The upper bound cannot precede the lower bound.", nameof(maximumX));
            return new ChartCoordinateWindow(ChartCoordinateWindowKind.Fixed, minimumX, maximumX, 0, includeBoundaryNeighbors);
        }

        /// <summary>Creates a finite, nonnegative span re-anchored to the newest retained X on each capture.</summary>
        /// <remarks>
        /// Zero selects the newest row; neighbors additionally include its predecessor. Missing newest values still
        /// advance the window. Subtraction may round a tiny span to zero or saturate below the finite coordinate domain.
        /// </remarks>
        public static ChartCoordinateWindow Latest(double span, bool includeBoundaryNeighbors = false)
        {
            if (!double.IsFinite(span) || span < 0) throw new ArgumentOutOfRangeException(nameof(span));
            return new ChartCoordinateWindow(ChartCoordinateWindowKind.Latest, 0, 0, span, includeBoundaryNeighbors);
        }
    }
}
