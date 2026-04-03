// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using SkiaSharp;

namespace ProCharts.Skia
{
    public readonly struct SkiaChartViewportInfo : IEquatable<SkiaChartViewportInfo>
    {
        public SkiaChartViewportInfo(
            SKRect plot,
            bool barOnly,
            bool hasCartesianSeries,
            double minValue,
            double maxValue)
        {
            Plot = plot;
            BarOnly = barOnly;
            HasCartesianSeries = hasCartesianSeries;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public SKRect Plot { get; }

        public bool BarOnly { get; }

        public bool HasCartesianSeries { get; }

        public double MinValue { get; }

        public double MaxValue { get; }

        public bool Equals(SkiaChartViewportInfo other)
        {
            return Plot.Equals(other.Plot) &&
                   BarOnly == other.BarOnly &&
                   HasCartesianSeries == other.HasCartesianSeries &&
                   MinValue.Equals(other.MinValue) &&
                   MaxValue.Equals(other.MaxValue);
        }

        public override bool Equals(object? obj)
        {
            return obj is SkiaChartViewportInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Plot.Left.GetHashCode();
                hash = (hash * 31) + Plot.Top.GetHashCode();
                hash = (hash * 31) + Plot.Right.GetHashCode();
                hash = (hash * 31) + Plot.Bottom.GetHashCode();
                hash = (hash * 31) + BarOnly.GetHashCode();
                hash = (hash * 31) + HasCartesianSeries.GetHashCode();
                hash = (hash * 31) + MinValue.GetHashCode();
                hash = (hash * 31) + MaxValue.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(SkiaChartViewportInfo left, SkiaChartViewportInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SkiaChartViewportInfo left, SkiaChartViewportInfo right)
        {
            return !left.Equals(right);
        }
    }
}
