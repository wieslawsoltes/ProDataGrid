// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using SkiaSharp;

namespace ProCharts.Skia
{
    /// <summary>Immutable presentation options for matrix, hierarchical, and radial-indicator charts.</summary>
    public sealed record SkiaAdvancedChartStyle
    {
        /// <summary>Gets the heatmap color at the lower bound.</summary>
        public SKColor HeatmapLowColor { get; init; } = new(68, 1, 84);
        /// <summary>Gets the heatmap color at the upper bound.</summary>
        public SKColor HeatmapHighColor { get; init; } = new(253, 231, 37);
        /// <summary>Gets an optional center color for a diverging scale.</summary>
        public SKColor? HeatmapCenterColor { get; init; }
        /// <summary>Gets the center value for a diverging scale. Null uses the interval midpoint.</summary>
        public double? HeatmapCenterValue { get; init; }
        /// <summary>Gets an optional fixed lower heatmap bound. Null derives it from finite cells.</summary>
        public double? HeatmapMinimum { get; init; }
        /// <summary>Gets an optional fixed upper heatmap bound. Null derives it from finite cells.</summary>
        public double? HeatmapMaximum { get; init; }
        /// <summary>Gets cell spacing in logical pixels, clamped for very small cells.</summary>
        public float HeatmapCellGap { get; init; } = 1;
        /// <summary>Gets whether to show a continuous color scale with numeric endpoints.</summary>
        public bool ShowHeatmapColorScale { get; init; } = true;
        /// <summary>Gets the gap between treemap cells in logical pixels.</summary>
        public float TreemapGap { get; init; } = 2;
        /// <summary>Gets the height reserved for a treemap branch label and drill-down target.</summary>
        public float TreemapHeaderHeight { get; init; } = 18;
        /// <summary>Gets the sunburst hole radius as a fraction of the outer radius.</summary>
        public double SunburstInnerRadius { get; init; } = 0.18;
        /// <summary>Gets the gauge's clockwise starting angle in degrees, with zero pointing right.</summary>
        public float GaugeStartAngle { get; init; } = 135;
        /// <summary>Gets the gauge's clockwise angular extent, greater than zero and at most 360 degrees.</summary>
        public float GaugeSweepAngle { get; init; } = 270;
        /// <summary>Gets the gauge ring thickness as a fraction of its outer radius.</summary>
        public float GaugeThickness { get; init; } = 0.16f;

        internal void Validate()
        {
            Nonnegative(HeatmapCellGap, nameof(HeatmapCellGap));
            Nonnegative(TreemapGap, nameof(TreemapGap));
            Nonnegative(TreemapHeaderHeight, nameof(TreemapHeaderHeight));
            Finite(HeatmapMinimum, nameof(HeatmapMinimum));
            Finite(HeatmapMaximum, nameof(HeatmapMaximum));
            Finite(HeatmapCenterValue, nameof(HeatmapCenterValue));
            if (HeatmapMinimum.HasValue && HeatmapMaximum.HasValue && HeatmapMinimum > HeatmapMaximum)
                throw new ArgumentException("The heatmap minimum cannot exceed its maximum.");
            if (!double.IsFinite(SunburstInnerRadius) || SunburstInnerRadius < 0 || SunburstInnerRadius >= 1)
                throw new ArgumentOutOfRangeException(nameof(SunburstInnerRadius));
            if (!float.IsFinite(GaugeStartAngle)) throw new ArgumentOutOfRangeException(nameof(GaugeStartAngle));
            if (!float.IsFinite(GaugeSweepAngle) || GaugeSweepAngle <= 0 || GaugeSweepAngle > 360)
                throw new ArgumentOutOfRangeException(nameof(GaugeSweepAngle));
            if (!float.IsFinite(GaugeThickness) || GaugeThickness <= 0 || GaugeThickness >= 1)
                throw new ArgumentOutOfRangeException(nameof(GaugeThickness));
        }

        private static void Nonnegative(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        }
        private static void Finite(double? value, string name)
        {
            if (value.HasValue && !double.IsFinite(value.Value)) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed partial class SkiaChartStyle
    {
        /// <summary>Gets or sets immutable advanced-chart options. Replacing this value invalidates layout reuse.</summary>
        public SkiaAdvancedChartStyle Advanced { get; set; } = new();
    }
}
