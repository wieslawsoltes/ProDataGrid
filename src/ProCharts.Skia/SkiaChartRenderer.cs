// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using ProCharts;
using SkiaSharp;

namespace ProCharts.Skia
{
    public enum SkiaLegendFlow
    {
        Column,
        Row
    }

    public enum SkiaPieLabelPlacement
    {
        Auto,
        Inside,
        Outside
    }

    public readonly struct SkiaChartHitTestResult : IEquatable<SkiaChartHitTestResult>
    {
        public SkiaChartHitTestResult(
            int seriesIndex,
            int pointIndex,
            double value,
            double? xValue,
            string? category,
            string? seriesName,
            ChartSeriesKind seriesKind,
            SKPoint location,
            double? openValue = null,
            double? highValue = null,
            double? lowValue = null,
            double? closeValue = null)
        {
            SeriesIndex = seriesIndex;
            PointIndex = pointIndex;
            Value = value;
            XValue = xValue;
            Category = category;
            SeriesName = seriesName;
            SeriesKind = seriesKind;
            Location = location;
            OpenValue = openValue;
            HighValue = highValue;
            LowValue = lowValue;
            CloseValue = closeValue;
        }

        public int SeriesIndex { get; }

        public int PointIndex { get; }

        public double Value { get; }

        public double? XValue { get; }

        public string? Category { get; }

        public string? SeriesName { get; }

        public ChartSeriesKind SeriesKind { get; }

        public SKPoint Location { get; }

        public double? OpenValue { get; }

        public double? HighValue { get; }

        public double? LowValue { get; }

        public double? CloseValue { get; }

        public bool Equals(SkiaChartHitTestResult other)
        {
            return SeriesIndex == other.SeriesIndex &&
                   PointIndex == other.PointIndex &&
                   Value.Equals(other.Value) &&
                   Nullable.Equals(XValue, other.XValue) &&
                   string.Equals(Category, other.Category, StringComparison.Ordinal) &&
                   string.Equals(SeriesName, other.SeriesName, StringComparison.Ordinal) &&
                   SeriesKind == other.SeriesKind &&
                   Nullable.Equals(OpenValue, other.OpenValue) &&
                   Nullable.Equals(HighValue, other.HighValue) &&
                   Nullable.Equals(LowValue, other.LowValue) &&
                   Nullable.Equals(CloseValue, other.CloseValue);
        }

        public static bool operator ==(SkiaChartHitTestResult left, SkiaChartHitTestResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SkiaChartHitTestResult left, SkiaChartHitTestResult right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object? obj)
        {
            return obj is SkiaChartHitTestResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + SeriesIndex;
                hash = (hash * 31) + PointIndex;
                hash = (hash * 31) + Value.GetHashCode();
                hash = (hash * 31) + (XValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Category?.GetHashCode() ?? 0);
                hash = (hash * 31) + (SeriesName?.GetHashCode() ?? 0);
                hash = (hash * 31) + SeriesKind.GetHashCode();
                hash = (hash * 31) + (OpenValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + (HighValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + (LowValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + (CloseValue?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    public sealed class SkiaChartStyle
    {
        public static readonly IReadOnlyList<SKColor> DefaultSeriesColors = new[]
        {
            new SKColor(33, 150, 243),
            new SKColor(244, 67, 54),
            new SKColor(76, 175, 80),
            new SKColor(255, 152, 0),
            new SKColor(156, 39, 176)
        };

        public SkiaChartStyle()
        {
        }

        public SkiaChartStyle(SkiaChartStyle source)
        {
            Background = source.Background;
            Axis = source.Axis;
            Text = source.Text;
            AxisStrokeWidth = source.AxisStrokeWidth;
            SeriesStrokeWidth = source.SeriesStrokeWidth;
            LabelSize = source.LabelSize;
            LegendTextSize = source.LegendTextSize;
            LegendPadding = source.LegendPadding;
            LegendSwatchSize = source.LegendSwatchSize;
            LegendSpacing = source.LegendSpacing;
            ShowLegend = source.ShowLegend;
            LegendPosition = source.LegendPosition;
            ShowAxisLabels = source.ShowAxisLabels;
            ShowCategoryLabels = source.ShowCategoryLabels;
            AxisTickCount = source.AxisTickCount;
            AreaFillOpacity = source.AreaFillOpacity;
            ShowGridlines = source.ShowGridlines;
            Gridline = source.Gridline;
            GridlineStrokeWidth = source.GridlineStrokeWidth;
            ShowCategoryGridlines = source.ShowCategoryGridlines;
            ShowDataLabels = source.ShowDataLabels;
            DataLabelTextSize = source.DataLabelTextSize;
            DataLabelPadding = source.DataLabelPadding;
            DataLabelOffset = source.DataLabelOffset;
            DataLabelBackground = source.DataLabelBackground;
            DataLabelText = source.DataLabelText;
            PieLabelPlacement = source.PieLabelPlacement;
            PieLabelLeaderLineLength = source.PieLabelLeaderLineLength;
            PieLabelLeaderLineOffset = source.PieLabelLeaderLineOffset;
            PieLabelLeaderLineColor = source.PieLabelLeaderLineColor;
            PieLabelLeaderLineWidth = source.PieLabelLeaderLineWidth;
            PieInnerRadiusRatio = source.PieInnerRadiusRatio;
            HitTestRadius = source.HitTestRadius;
            ShowCategoryAxisLine = source.ShowCategoryAxisLine;
            ShowValueAxisLine = source.ShowValueAxisLine;
            ShowSecondaryValueAxis = source.ShowSecondaryValueAxis;
            ShowSecondaryCategoryAxis = source.ShowSecondaryCategoryAxis;
            CategoryAxisTitle = source.CategoryAxisTitle;
            SecondaryCategoryAxisTitle = source.SecondaryCategoryAxisTitle;
            ValueAxisTitle = source.ValueAxisTitle;
            SecondaryValueAxisTitle = source.SecondaryValueAxisTitle;
            CategoryAxisKind = source.CategoryAxisKind;
            SecondaryCategoryAxisKind = source.SecondaryCategoryAxisKind;
            ValueAxisKind = source.ValueAxisKind;
            SecondaryValueAxisKind = source.SecondaryValueAxisKind;
            CategoryAxisMinimum = source.CategoryAxisMinimum;
            CategoryAxisMaximum = source.CategoryAxisMaximum;
            SecondaryCategoryAxisMinimum = source.SecondaryCategoryAxisMinimum;
            SecondaryCategoryAxisMaximum = source.SecondaryCategoryAxisMaximum;
            ValueAxisMinimum = source.ValueAxisMinimum;
            ValueAxisMaximum = source.ValueAxisMaximum;
            SecondaryValueAxisMinimum = source.SecondaryValueAxisMinimum;
            SecondaryValueAxisMaximum = source.SecondaryValueAxisMaximum;
            CategoryAxisCrossing = source.CategoryAxisCrossing;
            CategoryAxisCrossingValue = source.CategoryAxisCrossingValue;
            CategoryAxisOffset = source.CategoryAxisOffset;
            CategoryAxisMinorTickCount = source.CategoryAxisMinorTickCount;
            ShowCategoryMinorTicks = source.ShowCategoryMinorTicks;
            ShowCategoryMinorGridlines = source.ShowCategoryMinorGridlines;
            SecondaryCategoryAxisCrossing = source.SecondaryCategoryAxisCrossing;
            SecondaryCategoryAxisCrossingValue = source.SecondaryCategoryAxisCrossingValue;
            SecondaryCategoryAxisOffset = source.SecondaryCategoryAxisOffset;
            SecondaryCategoryAxisMinorTickCount = source.SecondaryCategoryAxisMinorTickCount;
            ShowSecondaryCategoryMinorTicks = source.ShowSecondaryCategoryMinorTicks;
            ShowSecondaryCategoryMinorGridlines = source.ShowSecondaryCategoryMinorGridlines;
            ValueAxisCrossing = source.ValueAxisCrossing;
            ValueAxisCrossingValue = source.ValueAxisCrossingValue;
            ValueAxisOffset = source.ValueAxisOffset;
            ValueAxisMinorTickCount = source.ValueAxisMinorTickCount;
            ShowValueMinorTicks = source.ShowValueMinorTicks;
            ShowValueMinorGridlines = source.ShowValueMinorGridlines;
            SecondaryValueAxisCrossing = source.SecondaryValueAxisCrossing;
            SecondaryValueAxisCrossingValue = source.SecondaryValueAxisCrossingValue;
            SecondaryValueAxisOffset = source.SecondaryValueAxisOffset;
            SecondaryValueAxisMinorTickCount = source.SecondaryValueAxisMinorTickCount;
            ShowSecondaryValueMinorTicks = source.ShowSecondaryValueMinorTicks;
            ShowSecondaryValueMinorGridlines = source.ShowSecondaryValueMinorGridlines;
            AxisLabelFormatter = source.AxisLabelFormatter;
            AxisValueFormat = source.AxisValueFormat;
            CategoryAxisLabelFormatter = source.CategoryAxisLabelFormatter;
            CategoryAxisValueFormat = source.CategoryAxisValueFormat;
            SecondaryAxisLabelFormatter = source.SecondaryAxisLabelFormatter;
            SecondaryAxisValueFormat = source.SecondaryAxisValueFormat;
            SecondaryCategoryAxisLabelFormatter = source.SecondaryCategoryAxisLabelFormatter;
            SecondaryCategoryAxisValueFormat = source.SecondaryCategoryAxisValueFormat;
            DataLabelFormatter = source.DataLabelFormatter;
            SeriesDataLabelFormatter = source.SeriesDataLabelFormatter;
            PaddingLeft = source.PaddingLeft;
            PaddingRight = source.PaddingRight;
            PaddingTop = source.PaddingTop;
            PaddingBottom = source.PaddingBottom;
            SeriesColors = source.SeriesColors;
            LegendFlow = source.LegendFlow;
            LegendWrap = source.LegendWrap;
            LegendMaxWidth = source.LegendMaxWidth;
            LegendGroupStackedSeries = source.LegendGroupStackedSeries;
            LegendStackedGroupTitle = source.LegendStackedGroupTitle;
            LegendStandardGroupTitle = source.LegendStandardGroupTitle;
            LegendGroupHeaderTextSize = source.LegendGroupHeaderTextSize;
            LegendGroupSpacing = source.LegendGroupSpacing;
            BubbleMinRadius = source.BubbleMinRadius;
            BubbleMaxRadius = source.BubbleMaxRadius;
            BubbleFillOpacity = source.BubbleFillOpacity;
            BubbleStrokeWidth = source.BubbleStrokeWidth;
            TrendlineStrokeWidth = source.TrendlineStrokeWidth;
            ErrorBarStrokeWidth = source.ErrorBarStrokeWidth;
            ErrorBarCapSize = source.ErrorBarCapSize;
            HistogramBinCount = source.HistogramBinCount;
            WaterfallIncreaseColor = source.WaterfallIncreaseColor;
            WaterfallDecreaseColor = source.WaterfallDecreaseColor;
            WaterfallConnectorColor = source.WaterfallConnectorColor;
            WaterfallConnectorStrokeWidth = source.WaterfallConnectorStrokeWidth;
            ShowWaterfallConnectors = source.ShowWaterfallConnectors;
            BoxWhiskerFillOpacity = source.BoxWhiskerFillOpacity;
            BoxWhiskerOutlierRadius = source.BoxWhiskerOutlierRadius;
            BoxWhiskerShowOutliers = source.BoxWhiskerShowOutliers;
            FinancialIncreaseColor = source.FinancialIncreaseColor;
            FinancialDecreaseColor = source.FinancialDecreaseColor;
            FinancialBodyFillOpacity = source.FinancialBodyFillOpacity;
            FinancialBodyWidthRatio = source.FinancialBodyWidthRatio;
            FinancialBoxWidthRatio = source.FinancialBoxWidthRatio;
            FinancialTickWidthRatio = source.FinancialTickWidthRatio;
            FinancialWickStrokeWidth = source.FinancialWickStrokeWidth;
            FinancialBodyStrokeWidth = source.FinancialBodyStrokeWidth;
            FinancialHollowBullishBodies = source.FinancialHollowBullishBodies;
            FinancialShowLastPriceLine = source.FinancialShowLastPriceLine;
            FinancialLastPriceLineColor = source.FinancialLastPriceLineColor;
            FinancialLastPriceLineWidth = source.FinancialLastPriceLineWidth;
            FinancialLastPriceLabelText = source.FinancialLastPriceLabelText;
            FinancialLastPriceLabelPadding = source.FinancialLastPriceLabelPadding;
            RadarPointRadius = source.RadarPointRadius;
            FunnelGap = source.FunnelGap;
            FunnelMinWidthRatio = source.FunnelMinWidthRatio;
            Theme = source.Theme;
            SeriesStyles = source.SeriesStyles;
            CoreTheme = source.CoreTheme;
            CoreSeriesStyles = source.CoreSeriesStyles;
        }

        public SKColor Background { get; set; } = SKColors.White;

        public SKColor Axis { get; set; } = new SKColor(48, 48, 48);

        public SKColor Text { get; set; } = new SKColor(32, 32, 32);

        public float AxisStrokeWidth { get; set; } = 1f;

        public float SeriesStrokeWidth { get; set; } = 2f;

        public float LabelSize { get; set; } = 11f;

        public float LegendTextSize { get; set; } = 11f;

        public float LegendPadding { get; set; } = 8f;

        public float LegendSwatchSize { get; set; } = 10f;

        public float LegendSpacing { get; set; } = 6f;

        public bool ShowLegend { get; set; } = true;

        public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Right;

        public SkiaLegendFlow LegendFlow { get; set; } = SkiaLegendFlow.Column;

        public bool LegendWrap { get; set; } = true;

        public float LegendMaxWidth { get; set; }

        public bool LegendGroupStackedSeries { get; set; }

        public string LegendStackedGroupTitle { get; set; } = "Stacked Series";

        public string LegendStandardGroupTitle { get; set; } = "Series";

        public float LegendGroupHeaderTextSize { get; set; } = 11f;

        public float LegendGroupSpacing { get; set; } = 6f;

        public float BubbleMinRadius { get; set; } = 6f;

        public float BubbleMaxRadius { get; set; } = 24f;

        public float BubbleFillOpacity { get; set; } = 0.65f;

        public float BubbleStrokeWidth { get; set; } = 1f;

        public float TrendlineStrokeWidth { get; set; } = 1.5f;

        public float ErrorBarStrokeWidth { get; set; } = 1f;

        public float ErrorBarCapSize { get; set; } = 4f;

        public int HistogramBinCount { get; set; }

        public SKColor WaterfallIncreaseColor { get; set; } = new SKColor(76, 175, 80);

        public SKColor WaterfallDecreaseColor { get; set; } = new SKColor(244, 67, 54);

        public SKColor WaterfallConnectorColor { get; set; } = new SKColor(120, 120, 120);

        public float WaterfallConnectorStrokeWidth { get; set; } = 1f;

        public bool ShowWaterfallConnectors { get; set; } = true;

        public float BoxWhiskerFillOpacity { get; set; } = 0.35f;

        public float BoxWhiskerOutlierRadius { get; set; } = 3f;

        public bool BoxWhiskerShowOutliers { get; set; } = true;

        public SKColor FinancialIncreaseColor { get; set; } = new SKColor(42, 214, 168);

        public SKColor FinancialDecreaseColor { get; set; } = new SKColor(255, 84, 104);

        public float FinancialBodyFillOpacity { get; set; } = 0.45f;

        public float FinancialBodyWidthRatio { get; set; } = 0.56f;

        public float FinancialBoxWidthRatio { get; set; } = 0.82f;

        public float FinancialTickWidthRatio { get; set; } = 0.22f;

        public float FinancialWickStrokeWidth { get; set; } = 1f;

        public float FinancialBodyStrokeWidth { get; set; } = 1f;

        public bool FinancialHollowBullishBodies { get; set; }

        public bool FinancialShowLastPriceLine { get; set; }

        public SKColor FinancialLastPriceLineColor { get; set; } = SKColors.Transparent;

        public float FinancialLastPriceLineWidth { get; set; } = 1f;

        public SKColor FinancialLastPriceLabelText { get; set; } = SKColors.White;

        public float FinancialLastPriceLabelPadding { get; set; } = 4f;

        public float RadarPointRadius { get; set; } = 3f;

        public float FunnelGap { get; set; } = 6f;

        public float FunnelMinWidthRatio { get; set; } = 0.15f;

        public bool ShowAxisLabels { get; set; } = true;

        public bool ShowCategoryLabels { get; set; } = true;

        public int AxisTickCount { get; set; } = 5;

        public float AreaFillOpacity { get; set; } = 0.25f;

        public bool ShowGridlines { get; set; } = true;

        public SKColor Gridline { get; set; } = new SKColor(220, 220, 220);

        public float GridlineStrokeWidth { get; set; } = 1f;

        public bool ShowCategoryGridlines { get; set; }

        public bool ShowDataLabels { get; set; }

        public float DataLabelTextSize { get; set; } = 10f;

        public float DataLabelPadding { get; set; } = 3f;

        public float DataLabelOffset { get; set; } = 6f;

        public SKColor DataLabelBackground { get; set; } = new SKColor(255, 255, 255, 200);

        public SKColor DataLabelText { get; set; } = new SKColor(32, 32, 32);

        public SkiaPieLabelPlacement PieLabelPlacement { get; set; } = SkiaPieLabelPlacement.Auto;

        public float PieLabelLeaderLineLength { get; set; } = 12f;

        public float PieLabelLeaderLineOffset { get; set; } = 4f;

        public SKColor PieLabelLeaderLineColor { get; set; } = new SKColor(120, 120, 120);

        public float PieLabelLeaderLineWidth { get; set; } = 1f;

        public float PieInnerRadiusRatio { get; set; } = 0.5f;

        public float HitTestRadius { get; set; } = 6f;

        public Func<double, string>? AxisLabelFormatter { get; set; }

        public ChartValueFormat? AxisValueFormat { get; set; }

        public Func<double, string>? CategoryAxisLabelFormatter { get; set; }

        public ChartValueFormat? CategoryAxisValueFormat { get; set; }

        public Func<double, string>? SecondaryAxisLabelFormatter { get; set; }

        public ChartValueFormat? SecondaryAxisValueFormat { get; set; }

        public Func<double, string>? SecondaryCategoryAxisLabelFormatter { get; set; }

        public ChartValueFormat? SecondaryCategoryAxisValueFormat { get; set; }

        public Func<double, string>? DataLabelFormatter { get; set; }

        public Func<int, double, string>? SeriesDataLabelFormatter { get; set; }

        public bool ShowCategoryAxisLine { get; set; } = true;

        public bool ShowValueAxisLine { get; set; } = true;

        public bool ShowSecondaryValueAxis { get; set; }

        public bool ShowSecondaryCategoryAxis { get; set; }

        public string? CategoryAxisTitle { get; set; }

        public string? SecondaryCategoryAxisTitle { get; set; }

        public string? ValueAxisTitle { get; set; }

        public string? SecondaryValueAxisTitle { get; set; }

        public ChartAxisKind CategoryAxisKind { get; set; } = ChartAxisKind.Category;

        public ChartAxisKind SecondaryCategoryAxisKind { get; set; } = ChartAxisKind.Category;

        public ChartAxisKind ValueAxisKind { get; set; } = ChartAxisKind.Value;

        public ChartAxisKind SecondaryValueAxisKind { get; set; } = ChartAxisKind.Value;

        public double? CategoryAxisMinimum { get; set; }

        public double? CategoryAxisMaximum { get; set; }

        public double? SecondaryCategoryAxisMinimum { get; set; }

        public double? SecondaryCategoryAxisMaximum { get; set; }

        public double? ValueAxisMinimum { get; set; }

        public double? ValueAxisMaximum { get; set; }

        public double? SecondaryValueAxisMinimum { get; set; }

        public double? SecondaryValueAxisMaximum { get; set; }

        public ChartAxisCrossing CategoryAxisCrossing { get; set; } = ChartAxisCrossing.Auto;

        public double? CategoryAxisCrossingValue { get; set; }

        public float CategoryAxisOffset { get; set; }

        public int CategoryAxisMinorTickCount { get; set; } = 4;

        public bool ShowCategoryMinorTicks { get; set; }

        public bool ShowCategoryMinorGridlines { get; set; }

        public ChartAxisCrossing SecondaryCategoryAxisCrossing { get; set; } = ChartAxisCrossing.Auto;

        public double? SecondaryCategoryAxisCrossingValue { get; set; }

        public float SecondaryCategoryAxisOffset { get; set; }

        public int SecondaryCategoryAxisMinorTickCount { get; set; } = 4;

        public bool ShowSecondaryCategoryMinorTicks { get; set; }

        public bool ShowSecondaryCategoryMinorGridlines { get; set; }

        public ChartAxisCrossing ValueAxisCrossing { get; set; } = ChartAxisCrossing.Auto;

        public double? ValueAxisCrossingValue { get; set; }

        public float ValueAxisOffset { get; set; }

        public int ValueAxisMinorTickCount { get; set; } = 4;

        public bool ShowValueMinorTicks { get; set; }

        public bool ShowValueMinorGridlines { get; set; }

        public ChartAxisCrossing SecondaryValueAxisCrossing { get; set; } = ChartAxisCrossing.Auto;

        public double? SecondaryValueAxisCrossingValue { get; set; }

        public float SecondaryValueAxisOffset { get; set; }

        public int SecondaryValueAxisMinorTickCount { get; set; } = 4;

        public bool ShowSecondaryValueMinorTicks { get; set; }

        public bool ShowSecondaryValueMinorGridlines { get; set; }

        public float PaddingLeft { get; set; } = 48f;

        public float PaddingRight { get; set; } = 16f;

        public float PaddingTop { get; set; } = 16f;

        public float PaddingBottom { get; set; } = 32f;

        public IReadOnlyList<SKColor> SeriesColors { get; set; } = DefaultSeriesColors;

        public SkiaChartTheme? Theme { get; set; }

        public IReadOnlyList<SkiaChartSeriesStyle>? SeriesStyles { get; set; }

        public ChartTheme? CoreTheme { get; set; }

        public IReadOnlyList<ChartSeriesStyle>? CoreSeriesStyles { get; set; }
    }

    public sealed partial class SkiaChartRenderer
    {
        private enum RenderKind
        {
            Cartesian,
            Pie,
            Radar,
            Funnel
        }

        private sealed class RenderContext
        {
            public RenderContext(
                RenderKind renderKind,
                HistogramContext? histogramContext,
                BoxWhiskerContext? boxWhiskerContext,
                IReadOnlyList<string?> renderCategories,
                bool hasCartesianSeries,
                bool barOnly,
                bool useNumericCategoryAxis,
                ChartAxisKind categoryAxisKind,
                bool hasPrimaryRange,
                bool hasSecondaryRange,
                double minValue,
                double maxValue,
                double minSecondaryValue,
                double maxSecondaryValue,
                double minCategory,
                double maxCategory,
                double minBubbleSize,
                double maxBubbleSize,
                SKRect plot,
                SKRect? legendRect)
            {
                RenderKind = renderKind;
                HistogramContext = histogramContext;
                BoxWhiskerContext = boxWhiskerContext;
                RenderCategories = renderCategories;
                HasCartesianSeries = hasCartesianSeries;
                BarOnly = barOnly;
                UseNumericCategoryAxis = useNumericCategoryAxis;
                CategoryAxisKind = categoryAxisKind;
                HasPrimaryRange = hasPrimaryRange;
                HasSecondaryRange = hasSecondaryRange;
                MinValue = minValue;
                MaxValue = maxValue;
                MinSecondaryValue = minSecondaryValue;
                MaxSecondaryValue = maxSecondaryValue;
                MinCategory = minCategory;
                MaxCategory = maxCategory;
                MinBubbleSize = minBubbleSize;
                MaxBubbleSize = maxBubbleSize;
                Plot = plot;
                LegendRect = legendRect;
            }

            public RenderKind RenderKind { get; }

            public HistogramContext? HistogramContext { get; }

            public BoxWhiskerContext? BoxWhiskerContext { get; }

            public IReadOnlyList<string?> RenderCategories { get; }

            public bool HasCartesianSeries { get; }

            public bool BarOnly { get; }

            public bool UseNumericCategoryAxis { get; }

            public ChartAxisKind CategoryAxisKind { get; }

            public bool HasPrimaryRange { get; }

            public bool HasSecondaryRange { get; }

            public double MinValue { get; }

            public double MaxValue { get; }

            public double MinSecondaryValue { get; }

            public double MaxSecondaryValue { get; }

            public double MinCategory { get; }

            public double MaxCategory { get; }

            public double MinBubbleSize { get; }

            public double MaxBubbleSize { get; }

            public SKRect Plot { get; }

            public SKRect? LegendRect { get; }
        }

        public void Render(SKCanvas canvas, SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle? style = null)
        {
            Render(canvas, bounds, snapshot, ChartDataDelta.Full, style, null);
        }

        public void Render(SKCanvas canvas, SKRect bounds, ChartDataUpdate update, SkiaChartStyle? style = null)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            Render(canvas, bounds, update.Snapshot, update.Delta, style, null);
        }

        public void Render(SKCanvas canvas, SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle? style, SkiaChartRenderCache? cache)
        {
            Render(canvas, bounds, snapshot, ChartDataDelta.Full, style, cache);
        }

        public void Render(SKCanvas canvas, SKRect bounds, ChartDataUpdate update, SkiaChartStyle? style, SkiaChartRenderCache? cache)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            Render(canvas, bounds, update.Snapshot, update.Delta, style, cache);
        }

        public bool TryGetViewportInfo(
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle? style,
            out SkiaChartViewportInfo info)
        {
            style ??= new SkiaChartStyle();
            style = ResolveStyle(style);

            if (!TryBuildRenderContext(bounds, snapshot, style, out var context))
            {
                info = default;
                return false;
            }

            info = new SkiaChartViewportInfo(
                context.Plot,
                context.BarOnly,
                context.HasCartesianSeries,
                context.MinValue,
                context.MaxValue);
            return true;
        }

        private void Render(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            ChartDataDelta delta,
            SkiaChartStyle? style,
            SkiaChartRenderCache? cache)
        {
            style ??= new SkiaChartStyle();
            style = ResolveStyle(style);

            if (cache != null)
            {
                RenderWithCache(canvas, bounds, snapshot, delta, style, cache);
                return;
            }

            RenderCore(canvas, bounds, snapshot, style);
        }

        private void RenderWithCache(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            ChartDataDelta delta,
            SkiaChartStyle style,
            SkiaChartRenderCache cache)
        {
            if (!TryBuildRenderContext(bounds, snapshot, style, out var context))
            {
                cache.Invalidate();
                canvas.Save();
                canvas.ClipRect(bounds);
                canvas.Clear(style.Background);
                canvas.Restore();
                return;
            }

            var state = BuildRenderState(context, snapshot);
            var styleHash = ComputeStyleHash(style);
            if (!cache.IsCompatible(bounds, styleHash, state))
            {
                cache.Invalidate();
            }

            var hasAxes = cache.HasAxes(bounds, styleHash, state);
            var hasAxisText = cache.HasAxisText(bounds, styleHash, state);
            var hasLegend = cache.HasLegend(bounds, styleHash, state);

            if (!hasAxes)
            {
                using var recorder = new SKPictureRecorder();
                var recordCanvas = recorder.BeginRecording(bounds);
                RenderAxesLayer(recordCanvas, bounds, snapshot, style, context);
                cache.StoreAxes(bounds, styleHash, state, recorder.EndRecording());
            }

            var dataSegments = BuildDataSegments(snapshot, context);
            var dataOrder = new List<SkiaChartDataSegmentKey>(dataSegments.Count);
            for (var i = 0; i < dataSegments.Count; i++)
            {
                dataOrder.Add(dataSegments[i].Key);
            }

            var requiresFullDataRefresh = RequiresFullDataRefresh(delta);
            if (requiresFullDataRefresh)
            {
                cache.ClearDataSegments();
            }

            for (var i = 0; i < dataSegments.Count; i++)
            {
                var segment = dataSegments[i];
                var needsUpdate = requiresFullDataRefresh || SegmentNeedsUpdate(segment, delta);
                if (!needsUpdate && cache.TryGetDataSegment(segment.Key, out _))
                {
                    continue;
                }

                using var recorder = new SKPictureRecorder();
                var recordCanvas = recorder.BeginRecording(bounds);
                RenderDataSegment(recordCanvas, bounds, snapshot, style, context, segment);
                cache.StoreDataSegment(segment.Key, recorder.EndRecording(), bounds, styleHash, state, snapshot);
            }

            if (!hasAxisText)
            {
                using var recorder = new SKPictureRecorder();
                var recordCanvas = recorder.BeginRecording(bounds);
                RenderAxisTextLayer(recordCanvas, bounds, snapshot, style, context);
                cache.StoreAxisText(bounds, styleHash, state, snapshot, recorder.EndRecording());
            }

            List<SkiaChartDataSegmentKey>? labelOrder = null;
            if (style.ShowDataLabels)
            {
                var labelSegments = BuildDataLabelSegments(snapshot, context);
                if (labelSegments.Count == 0)
                {
                    cache.ClearDataLabelSegments();
                }
                else
                {
                    labelOrder = new List<SkiaChartDataSegmentKey>(labelSegments.Count);
                    var requiresFullLabelRefresh = RequiresFullDataRefresh(delta);
                    if (requiresFullLabelRefresh)
                    {
                        cache.ClearDataLabelSegments();
                    }

                    var cascadeLabelRefresh = requiresFullLabelRefresh;
                    var placed = SkiaChartPools.RentList<SKRect>();
                    try
                    {
                        using var textPaint = CreateTextPaint(style.DataLabelText, style.DataLabelTextSize);
                        using var backgroundPaint = new SKPaint
                        {
                            Color = style.DataLabelBackground,
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill
                        };

                        for (var i = 0; i < labelSegments.Count; i++)
                        {
                            var segment = labelSegments[i];
                            labelOrder.Add(segment.Key);
                            var needsUpdate = cascadeLabelRefresh || SegmentNeedsUpdate(segment, delta);
                            if (!needsUpdate && cache.TryGetDataLabelSegment(segment.Key, out var cachedSegment))
                            {
                                var cachedPlacements = cachedSegment.Placements;
                                if (cachedPlacements.Length > 0)
                                {
                                    placed.AddRange(cachedPlacements);
                                }

                                continue;
                            }

                            using var recorder = new SKPictureRecorder();
                            var recordCanvas = recorder.BeginRecording(bounds);
                            var startCount = placed.Count;
                            RenderDataLabelSegment(recordCanvas, snapshot, style, context, segment, placed, textPaint, backgroundPaint);
                            var picture = recorder.EndRecording();
                            var segmentPlacements = ExtractPlacements(placed, startCount);
                            cache.StoreDataLabelSegment(segment.Key, picture, segmentPlacements, bounds, styleHash, state, snapshot);
                            cascadeLabelRefresh = true;
                        }
                    }
                    finally
                    {
                        SkiaChartPools.ReturnList(placed);
                    }
                }
            }
            else
            {
                cache.ClearDataLabelSegments();
            }

            if (!hasLegend)
            {
                if (state.LegendRect.HasValue)
                {
                    using var recorder = new SKPictureRecorder();
                    var recordCanvas = recorder.BeginRecording(bounds);
                    RenderLegendLayer(recordCanvas, bounds, snapshot, style, context);
                    cache.StoreLegend(bounds, styleHash, state, recorder.EndRecording());
                }
                else
                {
                    cache.ClearLegend();
                }
            }

            cache.DrawLayers(canvas, dataOrder, labelOrder);
        }

        private void RenderCore(SKCanvas canvas, SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle style)
        {
            if (!TryBuildRenderContext(bounds, snapshot, style, out var context))
            {
                canvas.Save();
                canvas.ClipRect(bounds);
                canvas.Clear(style.Background);
                canvas.Restore();
                return;
            }

            RenderAxesLayer(canvas, bounds, snapshot, style, context);
            RenderDataLayer(canvas, bounds, snapshot, style, context);
            RenderTextLayer(canvas, bounds, snapshot, style, context);
            RenderLegendLayer(canvas, bounds, snapshot, style, context);
        }

        private static bool TryBuildRenderContext(
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            out RenderContext context)
        {
            context = null!;

            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            var renderKind = GetRenderKind(snapshot);
            if (renderKind == RenderKind.Radar || renderKind == RenderKind.Funnel)
            {
                var nonCartesianPlot = CalculatePlotRect(
                    bounds,
                    snapshot,
                    snapshot.Categories,
                    style,
                    hasCartesianSeries: false,
                    barOnly: false,
                    useNumericCategoryAxis: false,
                    minValue: 0d,
                    maxValue: 1d,
                    minSecondaryValue: 0d,
                    maxSecondaryValue: 1d,
                    hasSecondaryAxis: false,
                    minCategory: 0d,
                    maxCategory: 1d,
                    out var nonCartesianLegendRect);

                if (nonCartesianPlot.Width <= 0 || nonCartesianPlot.Height <= 0)
                {
                    return false;
                }

                context = new RenderContext(
                    renderKind,
                    histogramContext: null,
                    boxWhiskerContext: null,
                    renderCategories: snapshot.Categories,
                    hasCartesianSeries: false,
                    barOnly: false,
                    useNumericCategoryAxis: false,
                    categoryAxisKind: style.CategoryAxisKind,
                    hasPrimaryRange: true,
                    hasSecondaryRange: false,
                    minValue: 0d,
                    maxValue: 1d,
                    minSecondaryValue: 0d,
                    maxSecondaryValue: 1d,
                    minCategory: 0d,
                    maxCategory: 1d,
                    minBubbleSize: 0d,
                    maxBubbleSize: 1d,
                    plot: nonCartesianPlot,
                    legendRect: nonCartesianLegendRect);
                return true;
            }

            HistogramContext? histogramContext = null;
            BoxWhiskerContext? boxWhiskerContext = null;
            if (renderKind == RenderKind.Cartesian)
            {
                if (TryBuildHistogramContext(snapshot, style, out var histogram))
                {
                    histogramContext = histogram;
                }

                if (TryBuildBoxWhiskerContext(snapshot, style.ValueAxisKind, out var boxWhisker))
                {
                    boxWhiskerContext = boxWhisker;
                }
            }

            var renderCategories = histogramContext?.Categories ?? boxWhiskerContext?.Categories ?? snapshot.Categories;
            var hasCartesianSeries = renderKind == RenderKind.Cartesian;
            var hasPrimaryRange = TryGetValueRange(
                snapshot,
                hasCartesianSeries,
                style.ValueAxisKind,
                ChartValueAxisAssignment.Primary,
                histogramContext,
                boxWhiskerContext,
                out var minValue,
                out var maxValue);
            var hasSecondaryRange = TryGetValueRange(
                snapshot,
                hasCartesianSeries,
                style.SecondaryValueAxisKind,
                ChartValueAxisAssignment.Secondary,
                histogramContext,
                boxWhiskerContext,
                out var minSecondaryValue,
                out var maxSecondaryValue);

            if (!hasPrimaryRange && !hasSecondaryRange)
            {
                return false;
            }

            var barOnly = false;
            var useNumericCategoryAxis = false;
            var categoryAxisKind = ResolveCategoryAxisScaleKind(style);
            double minCategory = 0;
            double maxCategory = 1;
            TryGetBubbleSizeRange(snapshot, out var minBubbleSize, out var maxBubbleSize);

            if (hasCartesianSeries)
            {
                if (!hasPrimaryRange)
                {
                    minValue = 0d;
                    maxValue = 1d;
                }

                ApplyValueAxisOverrides(style, ref minValue, ref maxValue);
                if (!hasPrimaryRange && (style.ValueAxisMinimum.HasValue || style.ValueAxisMaximum.HasValue))
                {
                    hasPrimaryRange = true;
                }

                EnsureAxisRange(ref minValue, ref maxValue, style.ValueAxisKind);

                if (!hasSecondaryRange)
                {
                    minSecondaryValue = 0d;
                    maxSecondaryValue = 1d;
                }

                ApplySecondaryValueAxisOverrides(style, ref minSecondaryValue, ref maxSecondaryValue);
                if (!hasSecondaryRange && (style.SecondaryValueAxisMinimum.HasValue || style.SecondaryValueAxisMaximum.HasValue))
                {
                    hasSecondaryRange = true;
                }

                EnsureAxisRange(ref minSecondaryValue, ref maxSecondaryValue, style.SecondaryValueAxisKind);

                if (!hasPrimaryRange && hasSecondaryRange)
                {
                    minValue = minSecondaryValue;
                    maxValue = maxSecondaryValue;
                }

                barOnly = IsBarOnly(snapshot);
                useNumericCategoryAxis = ShouldUseNumericCategoryAxis(snapshot, style, barOnly, categoryAxisKind);
                if (useNumericCategoryAxis)
                {
                    if (!TryGetCategoryRange(snapshot, categoryAxisKind, out minCategory, out maxCategory))
                    {
                        useNumericCategoryAxis = false;
                    }
                    else
                    {
                        ApplyCategoryAxisOverrides(style, ref minCategory, ref maxCategory);
                        EnsureAxisRange(ref minCategory, ref maxCategory, categoryAxisKind);
                    }
                }
            }
            else
            {
                if (!hasPrimaryRange)
                {
                    minValue = 0d;
                    maxValue = 1d;
                }

                if (!hasSecondaryRange)
                {
                    minSecondaryValue = 0d;
                    maxSecondaryValue = 1d;
                }
            }

            var plot = CalculatePlotRect(
                bounds,
                snapshot,
                renderCategories,
                style,
                hasCartesianSeries,
                barOnly: hasCartesianSeries && barOnly,
                useNumericCategoryAxis: hasCartesianSeries && useNumericCategoryAxis,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                hasSecondaryRange && style.ShowSecondaryValueAxis,
                minCategory,
                maxCategory,
                out var legendRect);

            if (plot.Width <= 0 || plot.Height <= 0)
            {
                return false;
            }

            context = new RenderContext(
                renderKind,
                histogramContext,
                boxWhiskerContext,
                renderCategories,
                hasCartesianSeries,
                barOnly,
                useNumericCategoryAxis,
                categoryAxisKind,
                hasPrimaryRange,
                hasSecondaryRange,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                minCategory,
                maxCategory,
                minBubbleSize,
                maxBubbleSize,
                plot,
                legendRect);

            return true;
        }

        private static SkiaChartRenderState BuildRenderState(RenderContext context, ChartDataSnapshot snapshot)
        {
            var categoriesHash = ComputeRenderCategoriesHash(context, snapshot);
            var categoryCount = ComputeRenderCategoryCount(context, snapshot);
            var legendHash = ComputeLegendHash(snapshot, categoriesHash);
            var seriesLayoutHash = ComputeSeriesLayoutHash(snapshot);
            var hasCartesianSeries = context.HasCartesianSeries;
            var useNumericCategoryAxis = hasCartesianSeries && context.UseNumericCategoryAxis;
            var hasSecondaryRange = hasCartesianSeries && context.HasSecondaryRange;
            var minValue = hasCartesianSeries ? context.MinValue : 0d;
            var maxValue = hasCartesianSeries ? context.MaxValue : 1d;
            var minSecondaryValue = hasCartesianSeries ? context.MinSecondaryValue : 0d;
            var maxSecondaryValue = hasCartesianSeries ? context.MaxSecondaryValue : 1d;
            var minCategory = useNumericCategoryAxis ? context.MinCategory : 0d;
            var maxCategory = useNumericCategoryAxis ? context.MaxCategory : 1d;

            return new SkiaChartRenderState(
                (int)context.RenderKind,
                context.BarOnly,
                useNumericCategoryAxis,
                hasSecondaryRange,
                snapshot.Series.Count,
                categoryCount,
                seriesLayoutHash,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                minCategory,
                maxCategory,
                categoriesHash,
                legendHash,
                context.Plot,
                context.LegendRect);
        }

        private static int ComputeRenderCategoriesHash(RenderContext context, ChartDataSnapshot snapshot)
        {
            var categories = context.RenderCategories;
            if (categories.Count > 0)
            {
                return ComputeCategoriesHash(categories);
            }

            if (context.RenderKind == RenderKind.Radar || UsesCategoryLegend(snapshot))
            {
                var maxCount = 0;
                foreach (var series in snapshot.Series)
                {
                    maxCount = Math.Max(maxCount, series.Values.Count);
                }

                return maxCount;
            }

            return 0;
        }

        private static int ComputeRenderCategoryCount(RenderContext context, ChartDataSnapshot snapshot)
        {
            var categories = context.RenderCategories;
            if (categories.Count > 0)
            {
                return categories.Count;
            }

            if (context.RenderKind == RenderKind.Radar || UsesCategoryLegend(snapshot))
            {
                var maxCount = 0;
                foreach (var series in snapshot.Series)
                {
                    maxCount = Math.Max(maxCount, series.Values.Count);
                }

                return maxCount;
            }

            var fallback = 0;
            foreach (var series in snapshot.Series)
            {
                fallback = Math.Max(fallback, series.Values.Count);
            }

            return fallback;
        }

        private static int ComputeSeriesLayoutHash(ChartDataSnapshot snapshot)
        {
            unchecked
            {
                var hash = snapshot.Series.Count;
                for (var i = 0; i < snapshot.Series.Count; i++)
                {
                    var series = snapshot.Series[i];
                    hash = (hash * 31) + series.Kind.GetHashCode();
                    hash = (hash * 31) + series.ValueAxisAssignment.GetHashCode();
                }

                return hash;
            }
        }

        private readonly struct DataSegment
        {
            public DataSegment(SkiaChartDataSegmentKind kind, int seriesIndex, IReadOnlyList<int>? groupIndices = null)
            {
                Kind = kind;
                SeriesIndex = seriesIndex;
                GroupIndices = groupIndices;
                Key = new SkiaChartDataSegmentKey(kind, seriesIndex);
            }

            public SkiaChartDataSegmentKind Kind { get; }

            public int SeriesIndex { get; }

            public IReadOnlyList<int>? GroupIndices { get; }

            public SkiaChartDataSegmentKey Key { get; }
        }

        private static int ComputeCategoriesHash(IReadOnlyList<string?> categories)
        {
            unchecked
            {
                var hash = categories.Count;
                for (var i = 0; i < categories.Count; i++)
                {
                    hash = (hash * 31) + (categories[i]?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }

        private static int ComputeLegendHash(ChartDataSnapshot snapshot, int categoriesHash)
        {
            unchecked
            {
                var hash = snapshot.Series.Count;
                var usesCategoryLegend = UsesCategoryLegend(snapshot);
                hash = (hash * 31) + usesCategoryLegend.GetHashCode();
                if (usesCategoryLegend)
                {
                    hash = (hash * 31) + categoriesHash;
                }

                for (var i = 0; i < snapshot.Series.Count; i++)
                {
                    var series = snapshot.Series[i];
                    hash = (hash * 31) + (series.Name?.GetHashCode() ?? 0);
                    hash = (hash * 31) + series.Kind.GetHashCode();
                }

                return hash;
            }
        }

        private static void RenderAxesLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
            canvas.Clear(style.Background);

            if (snapshot.Series.Count == 0)
            {
                canvas.Restore();
                return;
            }

            if (context.RenderKind == RenderKind.Cartesian)
            {
                if (style.ShowGridlines ||
                    style.ShowCategoryGridlines ||
                    style.ShowValueMinorGridlines ||
                    style.ShowSecondaryValueMinorGridlines ||
                    style.ShowCategoryMinorGridlines ||
                    style.ShowSecondaryCategoryMinorGridlines)
                {
                    DrawGridlines(
                        canvas,
                        context.Plot,
                        context.RenderCategories,
                        context.MinValue,
                        context.MaxValue,
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style,
                        context.BarOnly,
                        context.UseNumericCategoryAxis,
                        context.MinCategory,
                        context.MaxCategory,
                        context.HasSecondaryRange);
                }

                DrawAxes(
                    canvas,
                    context.Plot,
                    context.RenderCategories,
                    style,
                    context.BarOnly,
                    context.UseNumericCategoryAxis,
                    context.MinValue,
                    context.MaxValue,
                    context.MinSecondaryValue,
                    context.MaxSecondaryValue,
                    context.MinCategory,
                    context.MaxCategory,
                    context.HasSecondaryRange);
            }
            else if (context.RenderKind == RenderKind.Radar)
            {
                DrawRadarGridlines(canvas, context.Plot, snapshot, style);
            }

            canvas.Restore();
        }

        private static void RenderDataLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            canvas.Save();
            canvas.ClipRect(bounds);

            switch (context.RenderKind)
            {
                case RenderKind.Cartesian:
                    RenderCartesianSeries(canvas, bounds, snapshot, style, context);
                    break;
                case RenderKind.Pie:
                    DrawPieSeries(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Radar:
                    DrawRadarSeries(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Funnel:
                    DrawFunnelSeries(canvas, context.Plot, snapshot, style);
                    break;
            }

            canvas.Restore();
        }

        private static List<DataSegment> BuildDataSegments(ChartDataSnapshot snapshot, RenderContext context)
        {
            var segments = new List<DataSegment>();
            if (snapshot.Series.Count == 0)
            {
                return segments;
            }

            if (context.RenderKind != RenderKind.Cartesian)
            {
                segments.Add(new DataSegment(SkiaChartDataSegmentKind.Full, 0));
                return segments;
            }

            var stackedColumnsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Primary);
            var stackedColumnsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Secondary);
            var stackedColumns100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Primary);
            var stackedColumns100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Secondary);
            var stackedBarsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Primary);
            var stackedBarsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Secondary);
            var stackedBars100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Primary);
            var stackedBars100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Secondary);
            var stackedAreasPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Primary);
            var stackedAreasSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Secondary);
            var stackedAreas100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Primary);
            var stackedAreas100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Secondary);
            var stackedColumnsPrimaryDrawn = false;
            var stackedColumnsSecondaryDrawn = false;
            var stackedColumns100PrimaryDrawn = false;
            var stackedColumns100SecondaryDrawn = false;
            var stackedBarsPrimaryDrawn = false;
            var stackedBarsSecondaryDrawn = false;
            var stackedBars100PrimaryDrawn = false;
            var stackedBars100SecondaryDrawn = false;
            var stackedAreasPrimaryDrawn = false;
            var stackedAreasSecondaryDrawn = false;
            var stackedAreas100PrimaryDrawn = false;
            var stackedAreas100SecondaryDrawn = false;

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind == ChartSeriesKind.StackedColumn)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedColumnsSecondaryDrawn && stackedColumnsSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumnSecondary, 0, stackedColumnsSecondary));
                            stackedColumnsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumnsPrimaryDrawn && stackedColumnsPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumnPrimary, 0, stackedColumnsPrimary));
                        stackedColumnsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedColumn100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedColumns100SecondaryDrawn && stackedColumns100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumn100Secondary, 0, stackedColumns100Secondary));
                            stackedColumns100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumns100PrimaryDrawn && stackedColumns100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumn100Primary, 0, stackedColumns100Primary));
                        stackedColumns100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedBarsSecondaryDrawn && stackedBarsSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBarSecondary, 0, stackedBarsSecondary));
                            stackedBarsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBarsPrimaryDrawn && stackedBarsPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBarPrimary, 0, stackedBarsPrimary));
                        stackedBarsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedBars100SecondaryDrawn && stackedBars100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBar100Secondary, 0, stackedBars100Secondary));
                            stackedBars100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBars100PrimaryDrawn && stackedBars100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBar100Primary, 0, stackedBars100Primary));
                        stackedBars100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedAreasSecondaryDrawn && stackedAreasSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedAreaSecondary, 0, stackedAreasSecondary));
                            stackedAreasSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreasPrimaryDrawn && stackedAreasPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedAreaPrimary, 0, stackedAreasPrimary));
                        stackedAreasPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedAreas100SecondaryDrawn && stackedAreas100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedArea100Secondary, 0, stackedAreas100Secondary));
                            stackedAreas100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreas100PrimaryDrawn && stackedAreas100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedArea100Primary, 0, stackedAreas100Primary));
                        stackedAreas100PrimaryDrawn = true;
                    }

                    continue;
                }

                segments.Add(new DataSegment(SkiaChartDataSegmentKind.Series, seriesIndex));
            }

            if (HasTrendlines(snapshot))
            {
                segments.Add(new DataSegment(SkiaChartDataSegmentKind.Trendlines, 0));
            }

            if (HasErrorBars(snapshot))
            {
                segments.Add(new DataSegment(SkiaChartDataSegmentKind.ErrorBars, 0));
            }

            return segments;
        }

        private static List<DataSegment> BuildDataLabelSegments(ChartDataSnapshot snapshot, RenderContext context)
        {
            var segments = new List<DataSegment>();
            if (snapshot.Series.Count == 0)
            {
                return segments;
            }

            if (context.RenderKind != RenderKind.Cartesian)
            {
                segments.Add(new DataSegment(SkiaChartDataSegmentKind.Full, 0));
                return segments;
            }

            var stackedColumnsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Primary);
            var stackedColumnsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Secondary);
            var stackedColumns100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Primary);
            var stackedColumns100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Secondary);
            var stackedBarsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Primary);
            var stackedBarsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Secondary);
            var stackedBars100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Primary);
            var stackedBars100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Secondary);
            var stackedAreasPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Primary);
            var stackedAreasSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Secondary);
            var stackedAreas100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Primary);
            var stackedAreas100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Secondary);
            var stackedColumnsPrimaryDrawn = false;
            var stackedColumnsSecondaryDrawn = false;
            var stackedColumns100PrimaryDrawn = false;
            var stackedColumns100SecondaryDrawn = false;
            var stackedBarsPrimaryDrawn = false;
            var stackedBarsSecondaryDrawn = false;
            var stackedBars100PrimaryDrawn = false;
            var stackedBars100SecondaryDrawn = false;
            var stackedAreasPrimaryDrawn = false;
            var stackedAreasSecondaryDrawn = false;
            var stackedAreas100PrimaryDrawn = false;
            var stackedAreas100SecondaryDrawn = false;

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind == ChartSeriesKind.StackedColumn)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedColumnsSecondaryDrawn && stackedColumnsSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumnSecondary, 0, stackedColumnsSecondary));
                            stackedColumnsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumnsPrimaryDrawn && stackedColumnsPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumnPrimary, 0, stackedColumnsPrimary));
                        stackedColumnsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedColumn100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedColumns100SecondaryDrawn && stackedColumns100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumn100Secondary, 0, stackedColumns100Secondary));
                            stackedColumns100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumns100PrimaryDrawn && stackedColumns100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedColumn100Primary, 0, stackedColumns100Primary));
                        stackedColumns100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedBarsSecondaryDrawn && stackedBarsSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBarSecondary, 0, stackedBarsSecondary));
                            stackedBarsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBarsPrimaryDrawn && stackedBarsPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBarPrimary, 0, stackedBarsPrimary));
                        stackedBarsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedBars100SecondaryDrawn && stackedBars100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBar100Secondary, 0, stackedBars100Secondary));
                            stackedBars100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBars100PrimaryDrawn && stackedBars100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedBar100Primary, 0, stackedBars100Primary));
                        stackedBars100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedAreasSecondaryDrawn && stackedAreasSecondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedAreaSecondary, 0, stackedAreasSecondary));
                            stackedAreasSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreasPrimaryDrawn && stackedAreasPrimary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedAreaPrimary, 0, stackedAreasPrimary));
                        stackedAreasPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea100)
                {
                    var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                    if (isSecondary)
                    {
                        if (!stackedAreas100SecondaryDrawn && stackedAreas100Secondary.Count > 0)
                        {
                            segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedArea100Secondary, 0, stackedAreas100Secondary));
                            stackedAreas100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreas100PrimaryDrawn && stackedAreas100Primary.Count > 0)
                    {
                        segments.Add(new DataSegment(SkiaChartDataSegmentKind.StackedArea100Primary, 0, stackedAreas100Primary));
                        stackedAreas100PrimaryDrawn = true;
                    }

                    continue;
                }

                segments.Add(new DataSegment(SkiaChartDataSegmentKind.Series, seriesIndex));
            }

            return segments;
        }

        private static bool HasTrendlines(ChartDataSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                if (snapshot.Series[i].TrendlineType != ChartTrendlineType.None)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasErrorBars(ChartDataSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                if (series.ErrorBarType != ChartErrorBarType.None && series.ErrorBarValue > 0d)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequiresFullDataRefresh(ChartDataDelta delta)
        {
            if (delta == null || delta.IsFullRefresh)
            {
                return true;
            }

            return false;
        }

        private static bool SegmentNeedsUpdate(DataSegment segment, ChartDataDelta delta)
        {
            if (delta == null || delta.Kind == ChartDataDeltaKind.None)
            {
                return false;
            }

            if (segment.Kind == SkiaChartDataSegmentKind.Full ||
                segment.Kind == SkiaChartDataSegmentKind.Trendlines ||
                segment.Kind == SkiaChartDataSegmentKind.ErrorBars)
            {
                return true;
            }

            var seriesIndices = delta.SeriesIndices;
            if (seriesIndices == null || seriesIndices.Count == 0)
            {
                return true;
            }

            if (segment.Kind == SkiaChartDataSegmentKind.Series)
            {
                for (var i = 0; i < seriesIndices.Count; i++)
                {
                    if (seriesIndices[i] == segment.SeriesIndex)
                    {
                        return true;
                    }
                }

                return false;
            }

            return segment.GroupIndices != null && ContainsAny(seriesIndices, segment.GroupIndices);
        }

        private static SKRect[] ExtractPlacements(List<SKRect> placed, int startIndex)
        {
            var count = placed.Count - startIndex;
            if (count <= 0)
            {
                return Array.Empty<SKRect>();
            }

            var placements = new SKRect[count];
            placed.CopyTo(startIndex, placements, 0, count);
            return placements;
        }

        private static bool ContainsAny(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            for (var i = 0; i < left.Count; i++)
            {
                for (var j = 0; j < right.Count; j++)
                {
                    if (left[i] == right[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RenderDataSegment(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context,
            DataSegment segment)
        {
            if (segment.Kind == SkiaChartDataSegmentKind.Full)
            {
                RenderDataLayer(canvas, bounds, snapshot, style, context);
                return;
            }

            canvas.Save();
            canvas.ClipRect(bounds);

            switch (segment.Kind)
            {
                case SkiaChartDataSegmentKind.Series:
                    RenderCartesianSeriesSegment(canvas, snapshot, style, context, segment.SeriesIndex);
                    break;
                case SkiaChartDataSegmentKind.StackedColumnPrimary:
                    DrawStackedColumnSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedColumnSecondary:
                    DrawStackedColumnSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedColumn100Primary:
                    DrawStackedColumnSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.StackedColumn100Secondary:
                    DrawStackedColumnSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.StackedBarPrimary:
                    DrawStackedBarSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedBarSecondary:
                    DrawStackedBarSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedBar100Primary:
                    DrawStackedBarSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.StackedBar100Secondary:
                    DrawStackedBarSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.StackedAreaPrimary:
                    DrawStackedAreaSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedAreaSecondary:
                    DrawStackedAreaSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: false);
                    break;
                case SkiaChartDataSegmentKind.StackedArea100Primary:
                    DrawStackedAreaSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinValue,
                        context.MaxValue,
                        style.ValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.StackedArea100Secondary:
                    DrawStackedAreaSeries(
                        canvas,
                        context.Plot,
                        snapshot,
                        segment.GroupIndices ?? Array.Empty<int>(),
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style.SecondaryValueAxisKind,
                        style,
                        normalizeToPercent: true);
                    break;
                case SkiaChartDataSegmentKind.Trendlines:
                    DrawTrendlines(
                        canvas,
                        context.Plot,
                        snapshot,
                        context.RenderCategories,
                        context.MinValue,
                        context.MaxValue,
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style,
                        context.UseNumericCategoryAxis,
                        context.CategoryAxisKind,
                        context.MinCategory,
                        context.MaxCategory);
                    break;
                case SkiaChartDataSegmentKind.ErrorBars:
                    DrawErrorBars(
                        canvas,
                        context.Plot,
                        snapshot,
                        context.RenderCategories,
                        context.MinValue,
                        context.MaxValue,
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style,
                        context.UseNumericCategoryAxis,
                        context.CategoryAxisKind,
                        context.MinCategory,
                        context.MaxCategory);
                    break;
            }

            canvas.Restore();
        }

        private static void RenderCartesianSeriesSegment(
            SKCanvas canvas,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context,
            int seriesIndex)
        {
            if (seriesIndex < 0 || seriesIndex >= snapshot.Series.Count)
            {
                return;
            }

            var series = snapshot.Series[seriesIndex];
            if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
            {
                return;
            }

            if (series.Kind == ChartSeriesKind.StackedColumn ||
                series.Kind == ChartSeriesKind.StackedBar ||
                series.Kind == ChartSeriesKind.StackedArea ||
                series.Kind == ChartSeriesKind.StackedColumn100 ||
                series.Kind == ChartSeriesKind.StackedBar100 ||
                series.Kind == ChartSeriesKind.StackedArea100)
            {
                return;
            }

            var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
            var axisMin = isSecondary ? context.MinSecondaryValue : context.MinValue;
            var axisMax = isSecondary ? context.MaxSecondaryValue : context.MaxValue;
            var axisKind = isSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;
            var categoryCount = context.RenderCategories.Count;
            var seriesCount = snapshot.Series.Count;

            switch (series.Kind)
            {
                case ChartSeriesKind.Column:
                    DrawColumnSeries(canvas, context.Plot, categoryCount, series, seriesIndex, seriesCount, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Bar:
                    DrawBarSeries(canvas, context.Plot, categoryCount, series, seriesIndex, seriesCount, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Candlestick:
                case ChartSeriesKind.HollowCandlestick:
                case ChartSeriesKind.HeikinAshi:
                    if (series.Kind == ChartSeriesKind.HollowCandlestick)
                    {
                        DrawHollowCandlestickSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    }
                    else
                    {
                        DrawCandlestickSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    }
                    break;
                case ChartSeriesKind.Ohlc:
                    DrawOhlcSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Hlc:
                    DrawHlcSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Renko:
                    DrawRenkoSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Range:
                    DrawRangeSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.LineBreak:
                    DrawLineBreakSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Kagi:
                    DrawKagiSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.PointFigure:
                    DrawPointFigureSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Waterfall:
                    DrawWaterfallSeries(canvas, context.Plot, context.RenderCategories, series, axisMin, axisMax, axisKind, style);
                    break;
                case ChartSeriesKind.Histogram:
                    if (context.HistogramContext != null)
                    {
                        DrawHistogramSeries(canvas, context.Plot, context.HistogramContext, seriesIndex, axisMin, axisMax, axisKind, style);
                    }
                    break;
                case ChartSeriesKind.Pareto:
                    if (context.HistogramContext != null)
                    {
                        DrawParetoSeries(
                            canvas,
                            context.Plot,
                            context.HistogramContext,
                            seriesIndex,
                            axisMin,
                            axisMax,
                            axisKind,
                            context.MinSecondaryValue,
                            context.MaxSecondaryValue,
                            style.SecondaryValueAxisKind,
                            style);
                    }
                    break;
                case ChartSeriesKind.BoxWhisker:
                    if (context.BoxWhiskerContext != null)
                    {
                        DrawBoxWhiskerSeries(canvas, context.Plot, context.BoxWhiskerContext, seriesIndex, axisMin, axisMax, axisKind, style);
                    }
                    break;
                case ChartSeriesKind.Scatter:
                    DrawScatterSeries(
                        canvas,
                        context.Plot,
                        series,
                        seriesIndex,
                        axisMin,
                        axisMax,
                        axisKind,
                        style,
                        context.UseNumericCategoryAxis,
                        context.CategoryAxisKind,
                        context.MinCategory,
                        context.MaxCategory);
                    break;
                case ChartSeriesKind.Bubble:
                    DrawBubbleSeries(
                        canvas,
                        context.Plot,
                        series,
                        seriesIndex,
                        axisMin,
                        axisMax,
                        axisKind,
                        style,
                        context.UseNumericCategoryAxis,
                        context.CategoryAxisKind,
                        context.MinCategory,
                        context.MaxCategory,
                        context.MinBubbleSize,
                        context.MaxBubbleSize);
                    break;
                case ChartSeriesKind.Area:
                    DrawAreaSeries(canvas, context.Plot, series, seriesIndex, axisMin, axisMax, axisKind, style);
                    break;
                default:
                    DrawLineSeries(canvas, context.Plot, series, seriesIndex, axisMin, axisMax, axisKind, style);
                    break;
            }
        }

        private static void RenderTextLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            RenderAxisTextLayer(canvas, bounds, snapshot, style, context);
            RenderDataLabelsLayer(canvas, bounds, snapshot, style, context);
        }

        private static void RenderAxisTextLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            canvas.Save();
            canvas.ClipRect(bounds);
            switch (context.RenderKind)
            {
                case RenderKind.Cartesian:
                    DrawAxisLabels(
                        canvas,
                        context.Plot,
                        context.RenderCategories,
                        context.MinValue,
                        context.MaxValue,
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        context.HasSecondaryRange,
                        style,
                        context.BarOnly,
                        context.UseNumericCategoryAxis,
                        context.MinCategory,
                        context.MaxCategory);
                    DrawAxisTitles(canvas, context.Plot, style, context.BarOnly);
                    break;
                case RenderKind.Radar:
                    DrawRadarCategoryLabels(canvas, context.Plot, snapshot, style);
                    break;
            }

            canvas.Restore();
        }

        private static void RenderDataLabelsLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            canvas.Save();
            canvas.ClipRect(bounds);

            switch (context.RenderKind)
            {
                case RenderKind.Cartesian:
                    DrawDataLabels(
                        canvas,
                        context.Plot,
                        snapshot,
                        context.RenderCategories,
                        context.MinValue,
                        context.MaxValue,
                        context.MinSecondaryValue,
                        context.MaxSecondaryValue,
                        style,
                        context.UseNumericCategoryAxis,
                        context.CategoryAxisKind,
                        context.MinCategory,
                        context.MaxCategory,
                        context.HistogramContext,
                        context.BoxWhiskerContext);
                    break;
                case RenderKind.Pie:
                    DrawPieDataLabels(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Radar:
                    DrawRadarDataLabels(canvas, context.Plot, snapshot, style);
                    break;
                case RenderKind.Funnel:
                    DrawFunnelDataLabels(canvas, context.Plot, snapshot, style);
                    break;
            }

            canvas.Restore();
        }

        private static void RenderLegendLayer(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            if (!context.LegendRect.HasValue)
            {
                return;
            }

            canvas.Save();
            canvas.ClipRect(bounds);
            DrawLegend(canvas, context.LegendRect.Value, snapshot, style);
            canvas.Restore();
        }

        private static void RenderCartesianSeries(
            SKCanvas canvas,
            SKRect bounds,
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            RenderContext context)
        {
            var seriesCount = snapshot.Series.Count;
            var categoryCount = context.RenderCategories.Count;
            var stackedColumnsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Primary);
            var stackedColumnsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, ChartValueAxisAssignment.Secondary);
            var stackedColumns100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Primary);
            var stackedColumns100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, ChartValueAxisAssignment.Secondary);
            var stackedBarsPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Primary);
            var stackedBarsSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, ChartValueAxisAssignment.Secondary);
            var stackedBars100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Primary);
            var stackedBars100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, ChartValueAxisAssignment.Secondary);
            var stackedAreasPrimary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Primary);
            var stackedAreasSecondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, ChartValueAxisAssignment.Secondary);
            var stackedAreas100Primary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Primary);
            var stackedAreas100Secondary = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, ChartValueAxisAssignment.Secondary);
            var stackedColumnsPrimaryDrawn = false;
            var stackedColumnsSecondaryDrawn = false;
            var stackedColumns100PrimaryDrawn = false;
            var stackedColumns100SecondaryDrawn = false;
            var stackedBarsPrimaryDrawn = false;
            var stackedBarsSecondaryDrawn = false;
            var stackedBars100PrimaryDrawn = false;
            var stackedBars100SecondaryDrawn = false;
            var stackedAreasPrimaryDrawn = false;
            var stackedAreasSecondaryDrawn = false;
            var stackedAreas100PrimaryDrawn = false;
            var stackedAreas100SecondaryDrawn = false;

            for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                {
                    continue;
                }

                var isSecondary = series.ValueAxisAssignment == ChartValueAxisAssignment.Secondary;
                var axisMin = isSecondary ? context.MinSecondaryValue : context.MinValue;
                var axisMax = isSecondary ? context.MaxSecondaryValue : context.MaxValue;
                var axisKind = isSecondary ? style.SecondaryValueAxisKind : style.ValueAxisKind;

                if (series.Kind == ChartSeriesKind.StackedColumn)
                {
                    if (isSecondary)
                    {
                        if (!stackedColumnsSecondaryDrawn)
                        {
                            DrawStackedColumnSeries(canvas, context.Plot, snapshot, stackedColumnsSecondary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                            stackedColumnsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumnsPrimaryDrawn)
                    {
                        DrawStackedColumnSeries(canvas, context.Plot, snapshot, stackedColumnsPrimary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                        stackedColumnsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedColumn100)
                {
                    if (isSecondary)
                    {
                        if (!stackedColumns100SecondaryDrawn)
                        {
                            DrawStackedColumnSeries(canvas, context.Plot, snapshot, stackedColumns100Secondary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                            stackedColumns100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedColumns100PrimaryDrawn)
                    {
                        DrawStackedColumnSeries(canvas, context.Plot, snapshot, stackedColumns100Primary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                        stackedColumns100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar)
                {
                    if (isSecondary)
                    {
                        if (!stackedBarsSecondaryDrawn)
                        {
                            DrawStackedBarSeries(canvas, context.Plot, snapshot, stackedBarsSecondary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                            stackedBarsSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBarsPrimaryDrawn)
                    {
                        DrawStackedBarSeries(canvas, context.Plot, snapshot, stackedBarsPrimary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                        stackedBarsPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedBar100)
                {
                    if (isSecondary)
                    {
                        if (!stackedBars100SecondaryDrawn)
                        {
                            DrawStackedBarSeries(canvas, context.Plot, snapshot, stackedBars100Secondary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                            stackedBars100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedBars100PrimaryDrawn)
                    {
                        DrawStackedBarSeries(canvas, context.Plot, snapshot, stackedBars100Primary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                        stackedBars100PrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea)
                {
                    if (isSecondary)
                    {
                        if (!stackedAreasSecondaryDrawn)
                        {
                            DrawStackedAreaSeries(canvas, context.Plot, snapshot, stackedAreasSecondary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                            stackedAreasSecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreasPrimaryDrawn)
                    {
                        DrawStackedAreaSeries(canvas, context.Plot, snapshot, stackedAreasPrimary, axisMin, axisMax, axisKind, style, normalizeToPercent: false);
                        stackedAreasPrimaryDrawn = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedArea100)
                {
                    if (isSecondary)
                    {
                        if (!stackedAreas100SecondaryDrawn)
                        {
                            DrawStackedAreaSeries(canvas, context.Plot, snapshot, stackedAreas100Secondary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                            stackedAreas100SecondaryDrawn = true;
                        }
                    }
                    else if (!stackedAreas100PrimaryDrawn)
                    {
                        DrawStackedAreaSeries(canvas, context.Plot, snapshot, stackedAreas100Primary, axisMin, axisMax, axisKind, style, normalizeToPercent: true);
                        stackedAreas100PrimaryDrawn = true;
                    }

                    continue;
                }
                switch (series.Kind)
                {
                    case ChartSeriesKind.Column:
                        DrawColumnSeries(canvas, context.Plot, categoryCount, series, seriesIndex, seriesCount, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Bar:
                        DrawBarSeries(canvas, context.Plot, categoryCount, series, seriesIndex, seriesCount, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Candlestick:
                    case ChartSeriesKind.HollowCandlestick:
                    case ChartSeriesKind.HeikinAshi:
                        if (series.Kind == ChartSeriesKind.HollowCandlestick)
                        {
                            DrawHollowCandlestickSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        }
                        else
                        {
                            DrawCandlestickSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        }
                        break;
                    case ChartSeriesKind.Ohlc:
                        DrawOhlcSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Hlc:
                        DrawHlcSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Renko:
                        DrawRenkoSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Range:
                        DrawRangeSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.LineBreak:
                        DrawLineBreakSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Kagi:
                        DrawKagiSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.PointFigure:
                        DrawPointFigureSeries(canvas, context.Plot, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Waterfall:
                        DrawWaterfallSeries(canvas, context.Plot, context.RenderCategories, series, axisMin, axisMax, axisKind, style);
                        break;
                    case ChartSeriesKind.Histogram:
                        if (context.HistogramContext != null)
                        {
                            DrawHistogramSeries(canvas, context.Plot, context.HistogramContext, seriesIndex, axisMin, axisMax, axisKind, style);
                        }
                        break;
                    case ChartSeriesKind.Pareto:
                        if (context.HistogramContext != null)
                        {
                            DrawParetoSeries(
                                canvas,
                                context.Plot,
                                context.HistogramContext,
                                seriesIndex,
                                axisMin,
                                axisMax,
                                axisKind,
                                context.MinSecondaryValue,
                                context.MaxSecondaryValue,
                                style.SecondaryValueAxisKind,
                                style);
                        }
                        break;
                    case ChartSeriesKind.BoxWhisker:
                        if (context.BoxWhiskerContext != null)
                        {
                            DrawBoxWhiskerSeries(canvas, context.Plot, context.BoxWhiskerContext, seriesIndex, axisMin, axisMax, axisKind, style);
                        }
                        break;
                    case ChartSeriesKind.Scatter:
                        DrawScatterSeries(
                            canvas,
                            context.Plot,
                            series,
                            seriesIndex,
                            axisMin,
                            axisMax,
                            axisKind,
                            style,
                            context.UseNumericCategoryAxis,
                            context.CategoryAxisKind,
                            context.MinCategory,
                            context.MaxCategory);
                        break;
                    case ChartSeriesKind.Bubble:
                        DrawBubbleSeries(
                            canvas,
                            context.Plot,
                            series,
                            seriesIndex,
                            axisMin,
                            axisMax,
                            axisKind,
                            style,
                            context.UseNumericCategoryAxis,
                            context.CategoryAxisKind,
                            context.MinCategory,
                            context.MaxCategory,
                            context.MinBubbleSize,
                            context.MaxBubbleSize);
                        break;
                    case ChartSeriesKind.Area:
                        DrawAreaSeries(canvas, context.Plot, series, seriesIndex, axisMin, axisMax, axisKind, style);
                        break;
                    default:
                        DrawLineSeries(canvas, context.Plot, series, seriesIndex, axisMin, axisMax, axisKind, style);
                        break;
                }
            }

            DrawTrendlines(
                canvas,
                context.Plot,
                snapshot,
                context.RenderCategories,
                context.MinValue,
                context.MaxValue,
                context.MinSecondaryValue,
                context.MaxSecondaryValue,
                style,
                context.UseNumericCategoryAxis,
                context.CategoryAxisKind,
                context.MinCategory,
                context.MaxCategory);
            DrawErrorBars(
                canvas,
                context.Plot,
                snapshot,
                context.RenderCategories,
                context.MinValue,
                context.MaxValue,
                context.MinSecondaryValue,
                context.MaxSecondaryValue,
                style,
                context.UseNumericCategoryAxis,
                context.CategoryAxisKind,
                context.MinCategory,
                context.MaxCategory);
            DrawFinancialLastPriceOverlay(
                canvas,
                bounds,
                context.Plot,
                snapshot,
                style,
                context.MinValue,
                context.MaxValue,
                context.MinSecondaryValue,
                context.MaxSecondaryValue);
        }

        public SkiaChartHitTestResult? HitTest(SKPoint point, SKRect bounds, ChartDataSnapshot snapshot, SkiaChartStyle? style = null)
        {
            style ??= new SkiaChartStyle();
            style = ResolveStyle(style);
            if (snapshot.Series.Count == 0)
            {
                return null;
            }

            var renderKind = GetRenderKind(snapshot);
            if (renderKind == RenderKind.Radar || renderKind == RenderKind.Funnel)
            {
                var nonCartesianPlot = CalculatePlotRect(
                    bounds,
                    snapshot,
                    snapshot.Categories,
                    style,
                    hasCartesianSeries: false,
                    barOnly: false,
                    useNumericCategoryAxis: false,
                    minValue: 0d,
                    maxValue: 1d,
                    minSecondaryValue: 0d,
                    maxSecondaryValue: 1d,
                    hasSecondaryAxis: false,
                    minCategory: 0d,
                    maxCategory: 1d,
                    out _);

                if (nonCartesianPlot.Width <= 0 || nonCartesianPlot.Height <= 0 || !nonCartesianPlot.Contains(point))
                {
                    return null;
                }

                return renderKind == RenderKind.Radar
                    ? HitTestRadar(point, nonCartesianPlot, snapshot, style)
                    : HitTestFunnel(point, nonCartesianPlot, snapshot, style);
            }

            HistogramContext? histogramContext = null;
            BoxWhiskerContext? boxWhiskerContext = null;
            if (renderKind == RenderKind.Cartesian)
            {
                if (TryBuildHistogramContext(snapshot, style, out var histogram))
                {
                    histogramContext = histogram;
                }

                if (TryBuildBoxWhiskerContext(snapshot, style.ValueAxisKind, out var boxWhisker))
                {
                    boxWhiskerContext = boxWhisker;
                }
            }

            var renderCategories = histogramContext?.Categories ?? boxWhiskerContext?.Categories ?? snapshot.Categories;
            var hasCartesianSeries = renderKind == RenderKind.Cartesian;
            var hasPrimaryRange = TryGetValueRange(
                snapshot,
                hasCartesianSeries,
                style.ValueAxisKind,
                ChartValueAxisAssignment.Primary,
                histogramContext,
                boxWhiskerContext,
                out var minValue,
                out var maxValue);
            var hasSecondaryRange = TryGetValueRange(
                snapshot,
                hasCartesianSeries,
                style.SecondaryValueAxisKind,
                ChartValueAxisAssignment.Secondary,
                histogramContext,
                boxWhiskerContext,
                out var minSecondaryValue,
                out var maxSecondaryValue);

            if (!hasPrimaryRange && !hasSecondaryRange)
            {
                return null;
            }

            var barOnly = false;
            var useNumericCategoryAxis = false;
            var categoryAxisKind = ResolveCategoryAxisScaleKind(style);
            double minCategory = 0;
            double maxCategory = 1;
            TryGetBubbleSizeRange(snapshot, out var minBubbleSize, out var maxBubbleSize);
            if (hasCartesianSeries)
            {
                if (!hasPrimaryRange)
                {
                    minValue = 0d;
                    maxValue = 1d;
                }

                ApplyValueAxisOverrides(style, ref minValue, ref maxValue);
                if (!hasPrimaryRange && (style.ValueAxisMinimum.HasValue || style.ValueAxisMaximum.HasValue))
                {
                    hasPrimaryRange = true;
                }

                EnsureAxisRange(ref minValue, ref maxValue, style.ValueAxisKind);

                if (!hasSecondaryRange)
                {
                    minSecondaryValue = 0d;
                    maxSecondaryValue = 1d;
                }

                ApplySecondaryValueAxisOverrides(style, ref minSecondaryValue, ref maxSecondaryValue);
                if (!hasSecondaryRange && (style.SecondaryValueAxisMinimum.HasValue || style.SecondaryValueAxisMaximum.HasValue))
                {
                    hasSecondaryRange = true;
                }

                EnsureAxisRange(ref minSecondaryValue, ref maxSecondaryValue, style.SecondaryValueAxisKind);

                if (!hasPrimaryRange && hasSecondaryRange)
                {
                    minValue = minSecondaryValue;
                    maxValue = maxSecondaryValue;
                }

                barOnly = IsBarOnly(snapshot);
                useNumericCategoryAxis = ShouldUseNumericCategoryAxis(snapshot, style, barOnly, categoryAxisKind);
                if (useNumericCategoryAxis)
                {
                    if (!TryGetCategoryRange(snapshot, categoryAxisKind, out minCategory, out maxCategory))
                    {
                        useNumericCategoryAxis = false;
                    }
                    else
                    {
                        ApplyCategoryAxisOverrides(style, ref minCategory, ref maxCategory);
                        EnsureAxisRange(ref minCategory, ref maxCategory, categoryAxisKind);
                    }
                }
            }

            var plot = CalculatePlotRect(
                bounds,
                snapshot,
                renderCategories,
                style,
                hasCartesianSeries,
                barOnly,
                useNumericCategoryAxis,
                minValue,
                maxValue,
                minSecondaryValue,
                maxSecondaryValue,
                hasSecondaryRange && style.ShowSecondaryValueAxis,
                minCategory,
                maxCategory,
                out _);

            if (plot.Width <= 0 || plot.Height <= 0)
            {
                return null;
            }

            if (!plot.Contains(point))
            {
                return null;
            }

            if (hasCartesianSeries)
            {
                return HitTestCartesian(
                    point,
                    plot,
                    snapshot,
                    renderCategories,
                    minValue,
                    maxValue,
                    minSecondaryValue,
                    maxSecondaryValue,
                    style,
                    useNumericCategoryAxis,
                    categoryAxisKind,
                    minCategory,
                    maxCategory,
                    minBubbleSize,
                    maxBubbleSize,
                    histogramContext,
                    boxWhiskerContext);
            }

            return HitTestPie(point, plot, snapshot, style);
        }

        private static bool TryGetScatterAxisRange(
            ChartSeriesSnapshot series,
            bool useNumericCategoryAxis,
            double minCategory,
            double maxCategory,
            ChartAxisKind categoryAxisKind,
            out double minX,
            out double maxX)
        {
            minX = 0d;
            maxX = 1d;
            var xValues = series.XValues;
            if (xValues == null || xValues.Count == 0)
            {
                return false;
            }

            if (useNumericCategoryAxis)
            {
                minX = minCategory;
                maxX = maxCategory;
                return !IsInvalidAxisValue(minX, categoryAxisKind) &&
                       !IsInvalidAxisValue(maxX, categoryAxisKind) &&
                       maxX > minX;
            }

            minX = double.MaxValue;
            maxX = double.MinValue;
            var hasValue = false;
            foreach (var x in xValues)
            {
                if (IsInvalidAxisValue(x, categoryAxisKind))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                hasValue = true;
            }

            if (!hasValue || minX == double.MaxValue || maxX == double.MinValue)
            {
                minX = 0d;
                maxX = 1d;
                return false;
            }

            if (Math.Abs(maxX - minX) < double.Epsilon)
            {
                maxX = minX + 1d;
            }

            return true;
        }

        private static bool TryGetValueRange(
            ChartDataSnapshot snapshot,
            bool cartesianOnly,
            ChartAxisKind axisKind,
            ChartValueAxisAssignment axisAssignment,
            HistogramContext? histogramContext,
            BoxWhiskerContext? boxWhiskerContext,
            out double minValue,
            out double maxValue)
        {
            minValue = double.MaxValue;
            maxValue = double.MinValue;
            var hasValue = false;

            var stackedColumns = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn, axisAssignment);
            var stackedColumns100 = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedColumn100, axisAssignment);
            var stackedBars = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar, axisAssignment);
            var stackedBars100 = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedBar100, axisAssignment);
            var stackedAreas = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea, axisAssignment);
            var stackedAreas100 = CollectSeriesIndices(snapshot, ChartSeriesKind.StackedArea100, axisAssignment);

            if (stackedColumns.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedColumns, axisKind, normalizeToPercent: false, ref minValue, ref maxValue, ref hasValue);
            }

            if (stackedColumns100.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedColumns100, axisKind, normalizeToPercent: true, ref minValue, ref maxValue, ref hasValue);
            }

            if (stackedBars.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedBars, axisKind, normalizeToPercent: false, ref minValue, ref maxValue, ref hasValue);
            }

            if (stackedBars100.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedBars100, axisKind, normalizeToPercent: true, ref minValue, ref maxValue, ref hasValue);
            }

            if (stackedAreas.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedAreas, axisKind, normalizeToPercent: false, ref minValue, ref maxValue, ref hasValue);
            }

            if (stackedAreas100.Count > 0)
            {
                UpdateStackedRange(snapshot, stackedAreas100, axisKind, normalizeToPercent: true, ref minValue, ref maxValue, ref hasValue);
            }

            for (var seriesIndex = 0; seriesIndex < snapshot.Series.Count; seriesIndex++)
            {
                var series = snapshot.Series[seriesIndex];
                if (cartesianOnly && (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut))
                {
                    continue;
                }

                if (series.ValueAxisAssignment != axisAssignment)
                {
                    continue;
                }

                if (series.Kind == ChartSeriesKind.StackedColumn ||
                    series.Kind == ChartSeriesKind.StackedBar ||
                    series.Kind == ChartSeriesKind.StackedArea ||
                    series.Kind == ChartSeriesKind.StackedColumn100 ||
                    series.Kind == ChartSeriesKind.StackedBar100 ||
                    series.Kind == ChartSeriesKind.StackedArea100)
                {
                    continue;
                }

                if ((series.Kind == ChartSeriesKind.Histogram || series.Kind == ChartSeriesKind.Pareto) &&
                    histogramContext != null &&
                    histogramContext.TryGetSeries(seriesIndex, out var histogramSeries))
                {
                    foreach (var count in histogramSeries.Counts)
                    {
                        minValue = Math.Min(minValue, count);
                        maxValue = Math.Max(maxValue, count);
                        hasValue = true;
                    }

                    if (axisKind != ChartAxisKind.Logarithmic)
                    {
                        minValue = Math.Min(minValue, 0d);
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.BoxWhisker &&
                    boxWhiskerContext != null &&
                    boxWhiskerContext.TryGetSeries(seriesIndex, out var boxSeries))
                {
                    var stats = boxSeries.Stats;
                    minValue = Math.Min(minValue, stats.Min);
                    maxValue = Math.Max(maxValue, stats.Max);
                    if (stats.Outliers.Length > 0)
                    {
                        for (var i = 0; i < stats.Outliers.Length; i++)
                        {
                            var outlier = stats.Outliers[i];
                            if (IsInvalidAxisValue(outlier, axisKind))
                            {
                                continue;
                            }

                            minValue = Math.Min(minValue, outlier);
                            maxValue = Math.Max(maxValue, outlier);
                        }
                    }
                    hasValue = true;
                    continue;
                }

                if (IsFinancialSeriesKind(series.Kind))
                {
                    var count = GetFinancialPointCount(series, series.Kind);
                    for (var i = 0; i < count; i++)
                    {
                        if (!TryGetFinancialPoint(series, series.Kind, i, axisKind, out var open, out var high, out var low, out var close))
                        {
                            continue;
                        }

                        var openValue = open ?? close;
                        minValue = Math.Min(minValue, Math.Min(low, Math.Min(openValue, close)));
                        maxValue = Math.Max(maxValue, Math.Max(high, Math.Max(openValue, close)));
                        hasValue = true;
                    }

                    continue;
                }

                if (series.Kind == ChartSeriesKind.Waterfall)
                {
                    var running = 0d;
                    for (var i = 0; i < series.Values.Count; i++)
                    {
                        var value = series.Values[i];
                        if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                        {
                            continue;
                        }

                        var start = running;
                        var end = running + value.Value;
                        if (!IsInvalidAxisValue(start, axisKind))
                        {
                            minValue = Math.Min(minValue, start);
                            maxValue = Math.Max(maxValue, start);
                            hasValue = true;
                        }

                        if (!IsInvalidAxisValue(end, axisKind))
                        {
                            minValue = Math.Min(minValue, end);
                            maxValue = Math.Max(maxValue, end);
                            hasValue = true;
                        }

                        running = end;
                    }

                    continue;
                }

                var hasErrorBars = series.ErrorBarType != ChartErrorBarType.None && series.ErrorBarValue > 0d;
                var errorMagnitude = 0d;
                var errorStandardError = 0d;
                if (series.ErrorBarType == ChartErrorBarType.StandardDeviation)
                {
                    errorMagnitude = ComputeStandardDeviation(series.Values, axisKind) * series.ErrorBarValue;
                    if (errorMagnitude < 0d || double.IsNaN(errorMagnitude) || double.IsInfinity(errorMagnitude))
                    {
                        errorMagnitude = 0d;
                        hasErrorBars = false;
                    }
                }
                else if (series.ErrorBarType == ChartErrorBarType.StandardError)
                {
                    errorStandardError = ComputeStandardError(series.Values, axisKind) * series.ErrorBarValue;
                    if (errorStandardError < 0d || double.IsNaN(errorStandardError) || double.IsInfinity(errorStandardError))
                    {
                        errorStandardError = 0d;
                        hasErrorBars = false;
                    }
                }

                foreach (var value in series.Values)
                {
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                    {
                        continue;
                    }

                    minValue = Math.Min(minValue, value.Value);
                    maxValue = Math.Max(maxValue, value.Value);
                    hasValue = true;

                    if (!hasErrorBars)
                    {
                        continue;
                    }

                    var error = series.ErrorBarType switch
                    {
                        ChartErrorBarType.Fixed => series.ErrorBarValue,
                        ChartErrorBarType.Percentage => Math.Abs(value.Value) * (series.ErrorBarValue / 100d),
                        ChartErrorBarType.StandardDeviation => errorMagnitude,
                        ChartErrorBarType.StandardError => errorStandardError,
                        _ => 0d
                    };

                    if (error <= 0d || double.IsNaN(error) || double.IsInfinity(error))
                    {
                        continue;
                    }

                    var low = value.Value - error;
                    var high = value.Value + error;
                    if (!IsInvalidAxisValue(low, axisKind))
                    {
                        minValue = Math.Min(minValue, low);
                        maxValue = Math.Max(maxValue, low);
                    }

                    if (!IsInvalidAxisValue(high, axisKind))
                    {
                        minValue = Math.Min(minValue, high);
                        maxValue = Math.Max(maxValue, high);
                    }
                }
            }

            if (!hasValue || minValue == double.MaxValue || maxValue == double.MinValue)
            {
                minValue = 0;
                maxValue = 1;
                return false;
            }

            return true;
        }

        private static RenderKind GetRenderKind(ChartDataSnapshot snapshot)
        {
            if (IsPieOnly(snapshot))
            {
                return RenderKind.Pie;
            }

            if (IsRadarOnly(snapshot))
            {
                return RenderKind.Radar;
            }

            if (IsFunnelOnly(snapshot))
            {
                return RenderKind.Funnel;
            }

            return RenderKind.Cartesian;
        }

        private static bool IsPieOnly(ChartDataSnapshot snapshot)
        {
            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.Pie && series.Kind != ChartSeriesKind.Donut)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool UsesCategoryLegend(ChartDataSnapshot snapshot)
        {
            return IsPieOnly(snapshot) || IsFunnelOnly(snapshot);
        }

        private static bool IsRadarOnly(ChartDataSnapshot snapshot)
        {
            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.Radar)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFunnelOnly(ChartDataSnapshot snapshot)
        {
            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.Funnel)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHistogramOnly(ChartDataSnapshot snapshot)
        {
            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.Histogram && series.Kind != ChartSeriesKind.Pareto)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBoxWhiskerOnly(ChartDataSnapshot snapshot)
        {
            if (snapshot.Series.Count == 0)
            {
                return false;
            }

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.BoxWhisker)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBarOnly(ChartDataSnapshot snapshot)
        {
            var hasSeries = false;
            foreach (var series in snapshot.Series)
            {
                if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                {
                    continue;
                }

                hasSeries = true;
                if (series.Kind != ChartSeriesKind.Bar &&
                    series.Kind != ChartSeriesKind.StackedBar &&
                    series.Kind != ChartSeriesKind.StackedBar100)
                {
                    return false;
                }
            }

            return hasSeries;
        }

        private static bool ShouldUseNumericCategoryAxis(
            ChartDataSnapshot snapshot,
            SkiaChartStyle style,
            bool barOnly,
            ChartAxisKind categoryAxisKind)
        {
            if (barOnly)
            {
                return false;
            }

            if (!IsNumericCategoryAxisKind(categoryAxisKind))
            {
                return false;
            }

            var hasSeries = false;
            foreach (var series in snapshot.Series)
            {
                if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                {
                    continue;
                }

                if (series.Kind != ChartSeriesKind.Scatter && series.Kind != ChartSeriesKind.Bubble)
                {
                    return false;
                }

                if (series.XValues == null || series.XValues.Count != series.Values.Count)
                {
                    return false;
                }

                hasSeries = true;
            }

            return hasSeries;
        }

        private static bool TryGetCategoryRange(ChartDataSnapshot snapshot, ChartAxisKind axisKind, out double minValue, out double maxValue)
        {
            minValue = double.MaxValue;
            maxValue = double.MinValue;
            var hasValue = false;

            foreach (var series in snapshot.Series)
            {
                if ((series.Kind != ChartSeriesKind.Scatter && series.Kind != ChartSeriesKind.Bubble) || series.XValues == null)
                {
                    continue;
                }

                foreach (var xValue in series.XValues)
                {
                    if (IsInvalidAxisValue(xValue, axisKind))
                    {
                        continue;
                    }

                    minValue = Math.Min(minValue, xValue);
                    maxValue = Math.Max(maxValue, xValue);
                    hasValue = true;
                }
            }

            if (!hasValue || minValue == double.MaxValue || maxValue == double.MinValue)
            {
                minValue = 0;
                maxValue = 1;
                return false;
            }

            return true;
        }

        private static bool TryGetBubbleSizeRange(ChartDataSnapshot snapshot, out double minSize, out double maxSize)
        {
            minSize = double.MaxValue;
            maxSize = double.MinValue;
            var hasValue = false;

            foreach (var series in snapshot.Series)
            {
                if (series.Kind != ChartSeriesKind.Bubble || series.SizeValues == null)
                {
                    continue;
                }

                var count = Math.Min(series.Values.Count, series.SizeValues.Count);
                for (var i = 0; i < count; i++)
                {
                    var sizeValue = series.SizeValues[i];
                    if (!sizeValue.HasValue || IsInvalidNumber(sizeValue.Value) || sizeValue.Value <= 0)
                    {
                        continue;
                    }

                    minSize = Math.Min(minSize, sizeValue.Value);
                    maxSize = Math.Max(maxSize, sizeValue.Value);
                    hasValue = true;
                }
            }

            if (!hasValue || minSize == double.MaxValue || maxSize == double.MinValue)
            {
                minSize = 1d;
                maxSize = 1d;
                return false;
            }

            return true;
        }

        private static double ComputeStandardDeviation(IReadOnlyList<double?> values, ChartAxisKind axisKind)
        {
            var count = 0;
            var mean = 0d;
            var m2 = 0d;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                {
                    continue;
                }

                count++;
                var delta = value.Value - mean;
                mean += delta / count;
                var delta2 = value.Value - mean;
                m2 += delta * delta2;
            }

            if (count <= 1)
            {
                return 0d;
            }

            var variance = m2 / (count - 1);
            return Math.Sqrt(variance);
        }

        private static double ComputeStandardError(IReadOnlyList<double?> values, ChartAxisKind axisKind)
        {
            var count = 0;
            var mean = 0d;
            var m2 = 0d;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                {
                    continue;
                }

                count++;
                var delta = value.Value - mean;
                mean += delta / count;
                var delta2 = value.Value - mean;
                m2 += delta * delta2;
            }

            if (count <= 1)
            {
                return 0d;
            }

            var variance = m2 / (count - 1);
            var stddev = Math.Sqrt(variance);
            return stddev / Math.Sqrt(count);
        }

        private static void ApplyCategoryAxisOverrides(SkiaChartStyle style, ref double minValue, ref double maxValue)
        {
            if (style.CategoryAxisMinimum.HasValue)
            {
                minValue = style.CategoryAxisMinimum.Value;
            }
            else if (style.SecondaryCategoryAxisMinimum.HasValue)
            {
                minValue = style.SecondaryCategoryAxisMinimum.Value;
            }

            if (style.CategoryAxisMaximum.HasValue)
            {
                maxValue = style.CategoryAxisMaximum.Value;
            }
            else if (style.SecondaryCategoryAxisMaximum.HasValue)
            {
                maxValue = style.SecondaryCategoryAxisMaximum.Value;
            }
        }

        private static bool IsNumericCategoryAxisKind(ChartAxisKind axisKind)
        {
            return axisKind == ChartAxisKind.Value ||
                   axisKind == ChartAxisKind.DateTime ||
                   axisKind == ChartAxisKind.Logarithmic;
        }

        private static ChartAxisKind ResolveCategoryAxisScaleKind(SkiaChartStyle style)
        {
            if (IsNumericCategoryAxisKind(style.CategoryAxisKind))
            {
                return style.CategoryAxisKind;
            }

            if (IsNumericCategoryAxisKind(style.SecondaryCategoryAxisKind))
            {
                return style.SecondaryCategoryAxisKind;
            }

            return style.CategoryAxisKind;
        }

        private static List<int> CollectSeriesIndices(
            ChartDataSnapshot snapshot,
            ChartSeriesKind kind,
            ChartValueAxisAssignment? axisAssignment = null)
        {
            var indices = new List<int>();
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                if (series.Kind == kind && (!axisAssignment.HasValue || series.ValueAxisAssignment == axisAssignment.Value))
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private static List<PieSeriesInfo> CollectPieSeries(ChartDataSnapshot snapshot)
        {
            var result = SkiaChartPools.RentList<PieSeriesInfo>();
            for (var i = 0; i < snapshot.Series.Count; i++)
            {
                var series = snapshot.Series[i];
                if (series.Kind == ChartSeriesKind.Pie || series.Kind == ChartSeriesKind.Donut)
                {
                    result.Add(new PieSeriesInfo(i, series));
                }
            }

            return result;
        }

        private static void UpdateStackedRange(
            ChartDataSnapshot snapshot,
            IReadOnlyList<int> seriesIndices,
            ChartAxisKind axisKind,
            bool normalizeToPercent,
            ref double minValue,
            ref double maxValue,
            ref bool hasValue)
        {
            if (seriesIndices.Count == 0)
            {
                return;
            }

            var categoryCount = snapshot.Categories.Count;
            if (categoryCount == 0)
            {
                return;
            }

            var positive = new double[categoryCount];
            var negative = new double[categoryCount];
            var positiveTotals = normalizeToPercent ? new double[categoryCount] : Array.Empty<double>();
            var negativeTotals = normalizeToPercent ? new double[categoryCount] : Array.Empty<double>();
            var anyValue = false;

            if (normalizeToPercent)
            {
                foreach (var seriesIndex in seriesIndices)
                {
                    var series = snapshot.Series[seriesIndex];
                    var count = Math.Min(categoryCount, series.Values.Count);
                    for (var i = 0; i < count; i++)
                    {
                        var value = series.Values[i];
                        if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                        {
                            continue;
                        }

                        var v = value.Value;
                        if (v >= 0)
                        {
                            positiveTotals[i] += v;
                        }
                        else
                        {
                            negativeTotals[i] += -v;
                        }

                        anyValue = true;
                    }
                }
            }

            foreach (var seriesIndex in seriesIndices)
            {
                var series = snapshot.Series[seriesIndex];
                var count = Math.Min(categoryCount, series.Values.Count);
                for (var i = 0; i < count; i++)
                {
                    var value = series.Values[i];
                    if (!value.HasValue || IsInvalidAxisValue(value.Value, axisKind))
                    {
                        continue;
                    }

                    var v = value.Value;
                    if (normalizeToPercent)
                    {
                        if (v >= 0)
                        {
                            var total = positiveTotals[i];
                            v = total > 0d ? v / total : 0d;
                        }
                        else
                        {
                            var total = negativeTotals[i];
                            v = total > 0d ? v / total : 0d;
                        }
                    }

                    if (v >= 0)
                    {
                        positive[i] += v;
                    }
                    else
                    {
                        negative[i] += v;
                    }

                    anyValue = true;
                }
            }

            if (!anyValue)
            {
                return;
            }

            var hasPositiveTotals = false;
            var hasNegativeTotals = false;
            for (var i = 0; i < categoryCount; i++)
            {
                if (normalizeToPercent)
                {
                    if (positiveTotals[i] > 0d)
                    {
                        hasPositiveTotals = true;
                    }

                    if (negativeTotals[i] > 0d)
                    {
                        hasNegativeTotals = true;
                    }

                    continue;
                }

                minValue = Math.Min(minValue, negative[i]);
                maxValue = Math.Max(maxValue, positive[i]);
            }

            if (normalizeToPercent)
            {
                if (hasPositiveTotals)
                {
                    maxValue = Math.Max(maxValue, 1d);
                    hasValue = true;
                }

                if (hasNegativeTotals)
                {
                    minValue = Math.Min(minValue, -1d);
                    hasValue = true;
                }

                if (hasPositiveTotals && !hasNegativeTotals)
                {
                    minValue = Math.Min(minValue, 0d);
                    hasValue = true;
                }
                else if (hasNegativeTotals && !hasPositiveTotals)
                {
                    maxValue = Math.Max(maxValue, 0d);
                    hasValue = true;
                }
                else if (!hasPositiveTotals && !hasNegativeTotals && anyValue)
                {
                    minValue = Math.Min(minValue, 0d);
                    maxValue = Math.Max(maxValue, 1d);
                    hasValue = true;
                }
            }
            else
            {
                hasValue = true;
            }
        }

        private static SKColor GetSeriesColor(SkiaChartStyle style, int seriesIndex)
        {
            var colors = ResolveSeriesPalette(style);
            if (colors == null || colors.Count == 0)
            {
                return SKColors.Gray;
            }

            return colors[seriesIndex % colors.Count];
        }

        private static void ApplyValueAxisOverrides(SkiaChartStyle style, ref double minValue, ref double maxValue)
        {
            if (style.ValueAxisMinimum.HasValue)
            {
                minValue = style.ValueAxisMinimum.Value;
            }

            if (style.ValueAxisMaximum.HasValue)
            {
                maxValue = style.ValueAxisMaximum.Value;
            }
        }

        private static void ApplySecondaryValueAxisOverrides(SkiaChartStyle style, ref double minValue, ref double maxValue)
        {
            if (style.SecondaryValueAxisMinimum.HasValue)
            {
                minValue = style.SecondaryValueAxisMinimum.Value;
            }

            if (style.SecondaryValueAxisMaximum.HasValue)
            {
                maxValue = style.SecondaryValueAxisMaximum.Value;
            }
        }

        private static void EnsureAxisRange(ref double minValue, ref double maxValue, ChartAxisKind axisKind)
        {
            if (axisKind == ChartAxisKind.Logarithmic)
            {
                if (IsInvalidNumber(minValue) || minValue <= 0d)
                {
                    minValue = 1d;
                }

                if (IsInvalidNumber(maxValue) || maxValue <= minValue)
                {
                    maxValue = minValue * 10d;
                }

                return;
            }

            if (maxValue <= minValue || Math.Abs(maxValue - minValue) < double.Epsilon)
            {
                maxValue = minValue + 1d;
            }
        }

        private static double GetMaxSeriesValue(ChartDataSnapshot snapshot)
        {
            var maxValue = double.MinValue;
            foreach (var series in snapshot.Series)
            {
                foreach (var value in series.Values)
                {
                    if (!value.HasValue || IsInvalidNumber(value.Value))
                    {
                        continue;
                    }

                    maxValue = Math.Max(maxValue, value.Value);
                }
            }

            if (maxValue == double.MinValue || maxValue <= 0d)
            {
                return 1d;
            }

            return maxValue;
        }

    }
}
