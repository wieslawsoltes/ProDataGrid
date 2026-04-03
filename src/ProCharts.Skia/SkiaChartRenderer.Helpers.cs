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
    public sealed partial class SkiaChartRenderer
    {
        private const float DefaultLineMarkerSize = 2.5f;
        private const float DefaultScatterMarkerSize = 3f;
        private const float DefaultMarkerStrokeWidth = 0f;

        private static byte ToAlpha(float opacity)
        {
            var clamped = Clamp(opacity, 0f, 1f);
            return (byte)(clamped * 255f);
        }

        private static int ComputeStyleHash(SkiaChartStyle style)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + style.Background.GetHashCode();
                hash = (hash * 31) + style.Axis.GetHashCode();
                hash = (hash * 31) + style.Text.GetHashCode();
                hash = (hash * 31) + style.AxisStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.SeriesStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.LabelSize.GetHashCode();
                hash = (hash * 31) + style.LegendTextSize.GetHashCode();
                hash = (hash * 31) + style.LegendPadding.GetHashCode();
                hash = (hash * 31) + style.LegendSwatchSize.GetHashCode();
                hash = (hash * 31) + style.LegendSpacing.GetHashCode();
                hash = (hash * 31) + style.ShowLegend.GetHashCode();
                hash = (hash * 31) + style.LegendPosition.GetHashCode();
                hash = (hash * 31) + style.ShowAxisLabels.GetHashCode();
                hash = (hash * 31) + style.ShowCategoryLabels.GetHashCode();
                hash = (hash * 31) + style.AxisTickCount.GetHashCode();
                hash = (hash * 31) + style.AreaFillOpacity.GetHashCode();
                hash = (hash * 31) + style.ShowGridlines.GetHashCode();
                hash = (hash * 31) + style.Gridline.GetHashCode();
                hash = (hash * 31) + style.GridlineStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.ShowCategoryGridlines.GetHashCode();
                hash = (hash * 31) + style.ShowDataLabels.GetHashCode();
                hash = (hash * 31) + style.DataLabelTextSize.GetHashCode();
                hash = (hash * 31) + style.DataLabelPadding.GetHashCode();
                hash = (hash * 31) + style.DataLabelOffset.GetHashCode();
                hash = (hash * 31) + style.DataLabelBackground.GetHashCode();
                hash = (hash * 31) + style.DataLabelText.GetHashCode();
                hash = (hash * 31) + style.PieLabelPlacement.GetHashCode();
                hash = (hash * 31) + style.PieLabelLeaderLineLength.GetHashCode();
                hash = (hash * 31) + style.PieLabelLeaderLineOffset.GetHashCode();
                hash = (hash * 31) + style.PieLabelLeaderLineColor.GetHashCode();
                hash = (hash * 31) + style.PieLabelLeaderLineWidth.GetHashCode();
                hash = (hash * 31) + style.PieInnerRadiusRatio.GetHashCode();
                hash = (hash * 31) + style.HitTestRadius.GetHashCode();
                hash = (hash * 31) + style.ShowCategoryAxisLine.GetHashCode();
                hash = (hash * 31) + style.ShowValueAxisLine.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryValueAxis.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryCategoryAxis.GetHashCode();
                hash = (hash * 31) + (style.CategoryAxisTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryCategoryAxisTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.ValueAxisTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryValueAxisTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.CategoryAxisKind.GetHashCode();
                hash = (hash * 31) + style.SecondaryCategoryAxisKind.GetHashCode();
                hash = (hash * 31) + style.ValueAxisKind.GetHashCode();
                hash = (hash * 31) + style.SecondaryValueAxisKind.GetHashCode();
                hash = (hash * 31) + (style.CategoryAxisMinimum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.CategoryAxisMaximum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryCategoryAxisMinimum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryCategoryAxisMaximum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.ValueAxisMinimum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.ValueAxisMaximum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryValueAxisMinimum?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryValueAxisMaximum?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.CategoryAxisCrossing.GetHashCode();
                hash = (hash * 31) + (style.CategoryAxisCrossingValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.CategoryAxisOffset.GetHashCode();
                hash = (hash * 31) + style.CategoryAxisMinorTickCount.GetHashCode();
                hash = (hash * 31) + style.ShowCategoryMinorTicks.GetHashCode();
                hash = (hash * 31) + style.ShowCategoryMinorGridlines.GetHashCode();
                hash = (hash * 31) + style.SecondaryCategoryAxisCrossing.GetHashCode();
                hash = (hash * 31) + (style.SecondaryCategoryAxisCrossingValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.SecondaryCategoryAxisOffset.GetHashCode();
                hash = (hash * 31) + style.SecondaryCategoryAxisMinorTickCount.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryCategoryMinorTicks.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryCategoryMinorGridlines.GetHashCode();
                hash = (hash * 31) + style.ValueAxisCrossing.GetHashCode();
                hash = (hash * 31) + (style.ValueAxisCrossingValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.ValueAxisOffset.GetHashCode();
                hash = (hash * 31) + style.ValueAxisMinorTickCount.GetHashCode();
                hash = (hash * 31) + style.ShowValueMinorTicks.GetHashCode();
                hash = (hash * 31) + style.ShowValueMinorGridlines.GetHashCode();
                hash = (hash * 31) + style.SecondaryValueAxisCrossing.GetHashCode();
                hash = (hash * 31) + (style.SecondaryValueAxisCrossingValue?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.SecondaryValueAxisOffset.GetHashCode();
                hash = (hash * 31) + style.SecondaryValueAxisMinorTickCount.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryValueMinorTicks.GetHashCode();
                hash = (hash * 31) + style.ShowSecondaryValueMinorGridlines.GetHashCode();
                hash = (hash * 31) + (style.AxisLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.AxisValueFormat?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.CategoryAxisLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.CategoryAxisValueFormat?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryAxisLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryAxisValueFormat?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryCategoryAxisLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SecondaryCategoryAxisValueFormat?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.DataLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.SeriesDataLabelFormatter?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.PaddingLeft.GetHashCode();
                hash = (hash * 31) + style.PaddingRight.GetHashCode();
                hash = (hash * 31) + style.PaddingTop.GetHashCode();
                hash = (hash * 31) + style.PaddingBottom.GetHashCode();
                hash = (hash * 31) + style.LegendFlow.GetHashCode();
                hash = (hash * 31) + style.LegendWrap.GetHashCode();
                hash = (hash * 31) + style.LegendMaxWidth.GetHashCode();
                hash = (hash * 31) + style.LegendGroupStackedSeries.GetHashCode();
                hash = (hash * 31) + (style.LegendStackedGroupTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.LegendStandardGroupTitle?.GetHashCode() ?? 0);
                hash = (hash * 31) + style.LegendGroupHeaderTextSize.GetHashCode();
                hash = (hash * 31) + style.LegendGroupSpacing.GetHashCode();
                hash = (hash * 31) + style.BubbleMinRadius.GetHashCode();
                hash = (hash * 31) + style.BubbleMaxRadius.GetHashCode();
                hash = (hash * 31) + style.BubbleFillOpacity.GetHashCode();
                hash = (hash * 31) + style.BubbleStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.TrendlineStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.ErrorBarStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.ErrorBarCapSize.GetHashCode();
                hash = (hash * 31) + style.HistogramBinCount.GetHashCode();
                hash = (hash * 31) + style.WaterfallIncreaseColor.GetHashCode();
                hash = (hash * 31) + style.WaterfallDecreaseColor.GetHashCode();
                hash = (hash * 31) + style.WaterfallConnectorColor.GetHashCode();
                hash = (hash * 31) + style.WaterfallConnectorStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.ShowWaterfallConnectors.GetHashCode();
                hash = (hash * 31) + style.BoxWhiskerFillOpacity.GetHashCode();
                hash = (hash * 31) + style.BoxWhiskerOutlierRadius.GetHashCode();
                hash = (hash * 31) + style.BoxWhiskerShowOutliers.GetHashCode();
                hash = (hash * 31) + style.FinancialIncreaseColor.GetHashCode();
                hash = (hash * 31) + style.FinancialDecreaseColor.GetHashCode();
                hash = (hash * 31) + style.FinancialBodyFillOpacity.GetHashCode();
                hash = (hash * 31) + style.FinancialBodyWidthRatio.GetHashCode();
                hash = (hash * 31) + style.FinancialBoxWidthRatio.GetHashCode();
                hash = (hash * 31) + style.FinancialTickWidthRatio.GetHashCode();
                hash = (hash * 31) + style.FinancialWickStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.FinancialBodyStrokeWidth.GetHashCode();
                hash = (hash * 31) + style.FinancialHollowBullishBodies.GetHashCode();
                hash = (hash * 31) + style.FinancialShowLastPriceLine.GetHashCode();
                hash = (hash * 31) + style.FinancialLastPriceLineColor.GetHashCode();
                hash = (hash * 31) + style.FinancialLastPriceLineWidth.GetHashCode();
                hash = (hash * 31) + style.FinancialLastPriceLabelText.GetHashCode();
                hash = (hash * 31) + style.FinancialLastPriceLabelPadding.GetHashCode();
                hash = (hash * 31) + style.RadarPointRadius.GetHashCode();
                hash = (hash * 31) + style.FunnelGap.GetHashCode();
                hash = (hash * 31) + style.FunnelMinWidthRatio.GetHashCode();

                var colors = ResolveSeriesPalette(style);
                hash = (hash * 31) + (colors?.Count ?? 0);
                if (colors != null)
                {
                    for (var i = 0; i < colors.Count; i++)
                    {
                        hash = (hash * 31) + colors[i].GetHashCode();
                    }
                }

                hash = (hash * 31) + HashSeriesStyles(style.Theme?.SeriesStyles);
                hash = (hash * 31) + HashSeriesStyles(style.SeriesStyles);
                hash = (hash * 31) + HashChartTheme(style.CoreTheme);
                hash = (hash * 31) + HashChartSeriesStyles(style.CoreSeriesStyles);

                return hash;
            }
        }

        private static int HashSeriesStyles(IReadOnlyList<SkiaChartSeriesStyle>? styles)
        {
            unchecked
            {
                if (styles == null)
                {
                    return 0;
                }

                var hash = styles.Count;
                for (var i = 0; i < styles.Count; i++)
                {
                    hash = (hash * 31) + HashSeriesStyle(styles[i]);
                }

                return hash;
            }
        }

        private static int HashSeriesStyle(SkiaChartSeriesStyle? style)
        {
            unchecked
            {
                if (style == null)
                {
                    return 0;
                }

                var hash = 17;
                hash = (hash * 31) + (style.StrokeColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.FillColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.StrokeWidth?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.LineStyle.HasValue ? ((int)style.LineStyle.Value + 1) : 0);
                hash = (hash * 31) + (style.LineInterpolation.HasValue ? ((int)style.LineInterpolation.Value + 1) : 0);
                hash = (hash * 31) + HashFloatArray(style.DashPattern);
                hash = (hash * 31) + (style.MarkerShape.HasValue ? ((int)style.MarkerShape.Value + 1) : 0);
                hash = (hash * 31) + (style.MarkerSize?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerFillColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerStrokeColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerStrokeWidth?.GetHashCode() ?? 0);
                hash = (hash * 31) + HashGradient(style.FillGradient);
                return hash;
            }
        }

        private static int HashChartTheme(ChartTheme? theme)
        {
            unchecked
            {
                if (theme == null)
                {
                    return 0;
                }

                var hash = 17;
                hash = (hash * 31) + (theme.Background?.GetHashCode() ?? 0);
                hash = (hash * 31) + (theme.Axis?.GetHashCode() ?? 0);
                hash = (hash * 31) + (theme.Text?.GetHashCode() ?? 0);
                hash = (hash * 31) + (theme.Gridline?.GetHashCode() ?? 0);
                hash = (hash * 31) + (theme.DataLabelBackground?.GetHashCode() ?? 0);
                hash = (hash * 31) + (theme.DataLabelText?.GetHashCode() ?? 0);
                hash = (hash * 31) + HashChartColorList(theme.SeriesColors);
                hash = (hash * 31) + HashChartSeriesStyles(theme.SeriesStyles);
                return hash;
            }
        }

        private static int HashChartSeriesStyles(IReadOnlyList<ChartSeriesStyle>? styles)
        {
            unchecked
            {
                if (styles == null)
                {
                    return 0;
                }

                var hash = styles.Count;
                for (var i = 0; i < styles.Count; i++)
                {
                    hash = (hash * 31) + HashChartSeriesStyle(styles[i]);
                }

                return hash;
            }
        }

        private static int HashChartSeriesStyle(ChartSeriesStyle? style)
        {
            unchecked
            {
                if (style == null)
                {
                    return 0;
                }

                var hash = 17;
                hash = (hash * 31) + (style.StrokeColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.FillColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.StrokeWidth?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.LineStyle.HasValue ? ((int)style.LineStyle.Value + 1) : 0);
                hash = (hash * 31) + (style.LineInterpolation.HasValue ? ((int)style.LineInterpolation.Value + 1) : 0);
                hash = (hash * 31) + HashFloatArray(style.DashPattern);
                hash = (hash * 31) + (style.MarkerShape.HasValue ? ((int)style.MarkerShape.Value + 1) : 0);
                hash = (hash * 31) + (style.MarkerSize?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerFillColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerStrokeColor?.GetHashCode() ?? 0);
                hash = (hash * 31) + (style.MarkerStrokeWidth?.GetHashCode() ?? 0);
                hash = (hash * 31) + HashChartGradient(style.FillGradient);
                return hash;
            }
        }

        private static int HashChartGradient(ChartGradient? gradient)
        {
            unchecked
            {
                if (gradient == null)
                {
                    return 0;
                }

                var hash = 17;
                hash = (hash * 31) + gradient.Direction.GetHashCode();
                hash = (hash * 31) + HashChartColorList(gradient.Colors);
                hash = (hash * 31) + HashFloatArray(gradient.Stops);
                return hash;
            }
        }

        private static int HashChartColorList(IReadOnlyList<ChartColor>? colors)
        {
            unchecked
            {
                if (colors == null)
                {
                    return 0;
                }

                var hash = colors.Count;
                for (var i = 0; i < colors.Count; i++)
                {
                    hash = (hash * 31) + colors[i].GetHashCode();
                }

                return hash;
            }
        }

        private static int HashGradient(SkiaChartGradient? gradient)
        {
            unchecked
            {
                if (gradient == null)
                {
                    return 0;
                }

                var hash = 17;
                hash = (hash * 31) + gradient.Direction.GetHashCode();
                hash = (hash * 31) + HashColorList(gradient.Colors);
                hash = (hash * 31) + HashFloatArray(gradient.Stops);
                return hash;
            }
        }

        private static int HashColorList(IReadOnlyList<SKColor>? colors)
        {
            unchecked
            {
                if (colors == null)
                {
                    return 0;
                }

                var hash = colors.Count;
                for (var i = 0; i < colors.Count; i++)
                {
                    hash = (hash * 31) + colors[i].GetHashCode();
                }

                return hash;
            }
        }

        private static int HashFloatArray(IReadOnlyList<float>? values)
        {
            unchecked
            {
                if (values == null)
                {
                    return 0;
                }

                var hash = values.Count;
                for (var i = 0; i < values.Count; i++)
                {
                    hash = (hash * 31) + values[i].GetHashCode();
                }

                return hash;
            }
        }

        private static SkiaChartStyle ResolveStyle(SkiaChartStyle style)
        {
            var theme = style.Theme;
            var coreTheme = style.CoreTheme;
            if (theme == null && coreTheme == null)
            {
                return style;
            }

            var resolved = new SkiaChartStyle(style);
            if (coreTheme != null)
            {
                ApplyCoreThemeOverrides(resolved, coreTheme);
            }

            if (theme != null)
            {
                ApplyThemeOverrides(resolved, theme);
            }

            return resolved;
        }

        private static void ApplyThemeOverrides(SkiaChartStyle style, SkiaChartTheme theme)
        {
            if (theme.Background.HasValue)
            {
                style.Background = theme.Background.Value;
            }

            if (theme.Axis.HasValue)
            {
                style.Axis = theme.Axis.Value;
            }

            if (theme.Text.HasValue)
            {
                style.Text = theme.Text.Value;
            }

            if (theme.Gridline.HasValue)
            {
                style.Gridline = theme.Gridline.Value;
            }

            if (theme.DataLabelBackground.HasValue)
            {
                style.DataLabelBackground = theme.DataLabelBackground.Value;
            }

            if (theme.DataLabelText.HasValue)
            {
                style.DataLabelText = theme.DataLabelText.Value;
            }
        }

        private static void ApplyCoreThemeOverrides(SkiaChartStyle style, ChartTheme theme)
        {
            if (theme.Background.HasValue)
            {
                style.Background = ConvertColor(theme.Background.Value);
            }

            if (theme.Axis.HasValue)
            {
                style.Axis = ConvertColor(theme.Axis.Value);
            }

            if (theme.Text.HasValue)
            {
                style.Text = ConvertColor(theme.Text.Value);
            }

            if (theme.Gridline.HasValue)
            {
                style.Gridline = ConvertColor(theme.Gridline.Value);
            }

            if (theme.DataLabelBackground.HasValue)
            {
                style.DataLabelBackground = ConvertColor(theme.DataLabelBackground.Value);
            }

            if (theme.DataLabelText.HasValue)
            {
                style.DataLabelText = ConvertColor(theme.DataLabelText.Value);
            }
        }

        private static IReadOnlyList<SKColor> ResolveSeriesPalette(SkiaChartStyle style)
        {
            var explicitColors = style.SeriesColors;
            var isDefault = ReferenceEquals(explicitColors, SkiaChartStyle.DefaultSeriesColors);
            if (!isDefault && explicitColors != null && explicitColors.Count > 0)
            {
                return explicitColors;
            }

            var themeColors = style.Theme?.SeriesColors;
            if (themeColors != null && themeColors.Count > 0)
            {
                return themeColors;
            }

            var coreThemeColors = style.CoreTheme?.SeriesColors;
            if (coreThemeColors != null && coreThemeColors.Count > 0)
            {
                return ConvertColors(coreThemeColors);
            }

            if (explicitColors != null && explicitColors.Count > 0)
            {
                return explicitColors;
            }

            return SkiaChartStyle.DefaultSeriesColors;
        }

        private static SkiaChartSeriesStyle? GetSkiaSeriesStyle(IReadOnlyList<SkiaChartSeriesStyle>? styles, int seriesIndex)
        {
            if (styles != null && seriesIndex >= 0 && seriesIndex < styles.Count)
            {
                return styles[seriesIndex];
            }

            return null;
        }

        private static ChartSeriesStyle? GetCoreSeriesStyle(IReadOnlyList<ChartSeriesStyle>? styles, int seriesIndex)
        {
            if (styles != null && seriesIndex >= 0 && seriesIndex < styles.Count)
            {
                return styles[seriesIndex];
            }

            return null;
        }

        private static SkiaChartSeriesStyle? MergeSeriesStyles(
            SkiaChartSeriesStyle? baseStyle,
            SkiaChartSeriesStyle? overrides)
        {
            if (baseStyle == null)
            {
                return overrides;
            }

            if (overrides == null)
            {
                return baseStyle;
            }

            return new SkiaChartSeriesStyle
            {
                StrokeColor = overrides.StrokeColor ?? baseStyle.StrokeColor,
                FillColor = overrides.FillColor ?? baseStyle.FillColor,
                StrokeWidth = overrides.StrokeWidth ?? baseStyle.StrokeWidth,
                LineStyle = overrides.LineStyle ?? baseStyle.LineStyle,
                LineInterpolation = overrides.LineInterpolation ?? baseStyle.LineInterpolation,
                DashPattern = overrides.DashPattern ?? baseStyle.DashPattern,
                MarkerShape = overrides.MarkerShape ?? baseStyle.MarkerShape,
                MarkerSize = overrides.MarkerSize ?? baseStyle.MarkerSize,
                MarkerFillColor = overrides.MarkerFillColor ?? baseStyle.MarkerFillColor,
                MarkerStrokeColor = overrides.MarkerStrokeColor ?? baseStyle.MarkerStrokeColor,
                MarkerStrokeWidth = overrides.MarkerStrokeWidth ?? baseStyle.MarkerStrokeWidth,
                FillGradient = overrides.FillGradient ?? baseStyle.FillGradient
            };
        }

        private static SkiaChartSeriesStyle? ConvertSeriesStyle(ChartSeriesStyle? style)
        {
            if (style == null)
            {
                return null;
            }

            return new SkiaChartSeriesStyle
            {
                StrokeColor = style.StrokeColor.HasValue ? ConvertColor(style.StrokeColor.Value) : null,
                FillColor = style.FillColor.HasValue ? ConvertColor(style.FillColor.Value) : null,
                StrokeWidth = style.StrokeWidth,
                LineStyle = style.LineStyle.HasValue ? ConvertLineStyle(style.LineStyle.Value) : null,
                LineInterpolation = style.LineInterpolation.HasValue ? ConvertLineInterpolation(style.LineInterpolation.Value) : null,
                DashPattern = style.DashPattern,
                MarkerShape = style.MarkerShape.HasValue ? ConvertMarkerShape(style.MarkerShape.Value) : null,
                MarkerSize = style.MarkerSize,
                MarkerFillColor = style.MarkerFillColor.HasValue ? ConvertColor(style.MarkerFillColor.Value) : null,
                MarkerStrokeColor = style.MarkerStrokeColor.HasValue ? ConvertColor(style.MarkerStrokeColor.Value) : null,
                MarkerStrokeWidth = style.MarkerStrokeWidth,
                FillGradient = ConvertGradient(style.FillGradient)
            };
        }

        private static SkiaChartGradient? ConvertGradient(ChartGradient? gradient)
        {
            if (gradient == null)
            {
                return null;
            }

            return new SkiaChartGradient
            {
                Direction = ConvertGradientDirection(gradient.Direction),
                Colors = ConvertColors(gradient.Colors),
                Stops = gradient.Stops
            };
        }

        private static SKColor ConvertColor(ChartColor color)
        {
            return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        }

        private static IReadOnlyList<SKColor> ConvertColors(IReadOnlyList<ChartColor> colors)
        {
            var converted = new SKColor[colors.Count];
            for (var i = 0; i < colors.Count; i++)
            {
                converted[i] = ConvertColor(colors[i]);
            }

            return converted;
        }

        private static SkiaLineStyle ConvertLineStyle(ChartLineStyle lineStyle)
        {
            return lineStyle switch
            {
                ChartLineStyle.Dashed => SkiaLineStyle.Dashed,
                ChartLineStyle.Dotted => SkiaLineStyle.Dotted,
                ChartLineStyle.DashDot => SkiaLineStyle.DashDot,
                ChartLineStyle.DashDotDot => SkiaLineStyle.DashDotDot,
                _ => SkiaLineStyle.Solid
            };
        }

        private static SkiaLineInterpolation ConvertLineInterpolation(ChartLineInterpolation interpolation)
        {
            return interpolation switch
            {
                ChartLineInterpolation.Smooth => SkiaLineInterpolation.Smooth,
                ChartLineInterpolation.Step => SkiaLineInterpolation.Step,
                _ => SkiaLineInterpolation.Linear
            };
        }

        private static SkiaMarkerShape ConvertMarkerShape(ChartMarkerShape shape)
        {
            return shape switch
            {
                ChartMarkerShape.Circle => SkiaMarkerShape.Circle,
                ChartMarkerShape.Square => SkiaMarkerShape.Square,
                ChartMarkerShape.Diamond => SkiaMarkerShape.Diamond,
                ChartMarkerShape.Triangle => SkiaMarkerShape.Triangle,
                ChartMarkerShape.TriangleDown => SkiaMarkerShape.TriangleDown,
                ChartMarkerShape.Cross => SkiaMarkerShape.Cross,
                ChartMarkerShape.X => SkiaMarkerShape.X,
                ChartMarkerShape.Plus => SkiaMarkerShape.Plus,
                _ => SkiaMarkerShape.None
            };
        }

        private static SkiaGradientDirection ConvertGradientDirection(ChartGradientDirection direction)
        {
            return direction switch
            {
                ChartGradientDirection.Horizontal => SkiaGradientDirection.Horizontal,
                ChartGradientDirection.DiagonalDown => SkiaGradientDirection.DiagonalDown,
                ChartGradientDirection.DiagonalUp => SkiaGradientDirection.DiagonalUp,
                _ => SkiaGradientDirection.Vertical
            };
        }

        private static SkiaChartSeriesStyle? GetSeriesStyleOverrides(SkiaChartStyle style, int seriesIndex)
        {
            var coreStyle = GetCoreSeriesStyle(style.CoreSeriesStyles, seriesIndex);
            var coreConverted = ConvertSeriesStyle(coreStyle);
            var skiaStyle = GetSkiaSeriesStyle(style.SeriesStyles, seriesIndex);
            return MergeSeriesStyles(coreConverted, skiaStyle);
        }

        private static SkiaChartSeriesStyle? GetThemeSeriesStyle(SkiaChartStyle style, int seriesIndex)
        {
            var coreStyle = GetCoreSeriesStyle(style.CoreTheme?.SeriesStyles, seriesIndex);
            var coreConverted = ConvertSeriesStyle(coreStyle);
            var skiaThemeStyle = GetSkiaSeriesStyle(style.Theme?.SeriesStyles, seriesIndex);
            return MergeSeriesStyles(coreConverted, skiaThemeStyle);
        }

        private static SKColor ResolveSeriesStrokeColor(
            SkiaChartStyle style,
            int seriesIndex,
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            var palette = ResolveSeriesPalette(style);
            var baseColor = palette.Count > 0 ? palette[seriesIndex % palette.Count] : SKColors.Gray;
            return overrides?.StrokeColor ?? themeStyle?.StrokeColor ?? baseColor;
        }

        private static SKColor ResolveSeriesFillColor(
            SKColor strokeColor,
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.FillColor ?? themeStyle?.FillColor ?? strokeColor;
        }

        private static SkiaChartGradient? ResolveSeriesGradient(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.FillGradient ?? themeStyle?.FillGradient;
        }

        private static float ResolveSeriesStrokeWidth(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle,
            float fallback)
        {
            return overrides?.StrokeWidth ?? themeStyle?.StrokeWidth ?? fallback;
        }

        private static SkiaLineStyle ResolveSeriesLineStyle(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.LineStyle ?? themeStyle?.LineStyle ?? SkiaLineStyle.Solid;
        }

        private static SkiaLineInterpolation ResolveSeriesLineInterpolation(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.LineInterpolation ?? themeStyle?.LineInterpolation ?? SkiaLineInterpolation.Linear;
        }

        private static float[]? ResolveSeriesDashPattern(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.DashPattern ?? themeStyle?.DashPattern;
        }

        private static SkiaMarkerShape ResolveMarkerShape(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle,
            SkiaMarkerShape fallback)
        {
            return overrides?.MarkerShape ?? themeStyle?.MarkerShape ?? fallback;
        }

        private static float ResolveMarkerSize(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle,
            float fallback)
        {
            return overrides?.MarkerSize ?? themeStyle?.MarkerSize ?? fallback;
        }

        private static SKColor ResolveMarkerFillColor(
            SKColor baseColor,
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.MarkerFillColor ?? themeStyle?.MarkerFillColor ?? baseColor;
        }

        private static SKColor ResolveMarkerStrokeColor(
            SKColor baseColor,
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.MarkerStrokeColor ?? themeStyle?.MarkerStrokeColor ?? baseColor;
        }

        private static float ResolveMarkerStrokeWidth(
            SkiaChartSeriesStyle? overrides,
            SkiaChartSeriesStyle? themeStyle)
        {
            return overrides?.MarkerStrokeWidth ?? themeStyle?.MarkerStrokeWidth ?? DefaultMarkerStrokeWidth;
        }

        private static SKPathEffect? CreateLineEffect(SkiaLineStyle lineStyle, float strokeWidth, float[]? dashPattern)
        {
            if (dashPattern != null && dashPattern.Length >= 2)
            {
                return SKPathEffect.CreateDash(dashPattern, 0f);
            }

            var unit = Math.Max(1f, strokeWidth);
            return lineStyle switch
            {
                SkiaLineStyle.Dashed => SKPathEffect.CreateDash(new[] { unit * 4f, unit * 3f }, 0f),
                SkiaLineStyle.Dotted => SKPathEffect.CreateDash(new[] { unit, unit * 2f }, 0f),
                SkiaLineStyle.DashDot => SKPathEffect.CreateDash(new[] { unit * 4f, unit * 2f, unit, unit * 2f }, 0f),
                SkiaLineStyle.DashDotDot => SKPathEffect.CreateDash(new[] { unit * 4f, unit * 2f, unit, unit * 2f, unit, unit * 2f }, 0f),
                _ => null
            };
        }

        private static SKShader? CreateGradientShader(SKRect bounds, SkiaChartGradient gradient, float opacity)
        {
            var colors = gradient.Colors;
            if (colors == null || colors.Count < 2)
            {
                return null;
            }

            var resolvedColors = new SKColor[colors.Count];
            for (var i = 0; i < colors.Count; i++)
            {
                resolvedColors[i] = ApplyOpacity(colors[i], opacity);
            }

            float[]? stopArray = null;
            var stops = gradient.Stops;
            if (stops != null && stops.Count == colors.Count)
            {
                if (stops is float[] stopsArray)
                {
                    stopArray = stopsArray;
                }
                else
                {
                    stopArray = new float[stops.Count];
                    for (var i = 0; i < stops.Count; i++)
                    {
                        stopArray[i] = stops[i];
                    }
                }
            }

            var (start, end) = ResolveGradientPoints(bounds, gradient.Direction);
            return SKShader.CreateLinearGradient(start, end, resolvedColors, stopArray, SKShaderTileMode.Clamp);
        }

        private static (SKPoint start, SKPoint end) ResolveGradientPoints(SKRect bounds, SkiaGradientDirection direction)
        {
            return direction switch
            {
                SkiaGradientDirection.Horizontal => (new SKPoint(bounds.Left, bounds.MidY), new SKPoint(bounds.Right, bounds.MidY)),
                SkiaGradientDirection.DiagonalDown => (new SKPoint(bounds.Left, bounds.Top), new SKPoint(bounds.Right, bounds.Bottom)),
                SkiaGradientDirection.DiagonalUp => (new SKPoint(bounds.Left, bounds.Bottom), new SKPoint(bounds.Right, bounds.Top)),
                _ => (new SKPoint(bounds.MidX, bounds.Top), new SKPoint(bounds.MidX, bounds.Bottom))
            };
        }

        private static SKColor ApplyOpacity(SKColor color, float opacity)
        {
            if (opacity >= 1f)
            {
                return color;
            }

            var alpha = Clamp(opacity, 0f, 1f);
            var scaled = (byte)Math.Round(color.Alpha * alpha, MidpointRounding.AwayFromZero);
            return color.WithAlpha(scaled);
        }

        private static void DrawMarker(
            SKCanvas canvas,
            SKPoint center,
            float size,
            SkiaMarkerShape shape,
            SKPaint fillPaint,
            SKPaint? strokePaint)
        {
            if (shape == SkiaMarkerShape.None || size <= 0f)
            {
                return;
            }

            var half = size;
            switch (shape)
            {
                case SkiaMarkerShape.Circle:
                    canvas.DrawCircle(center, half, fillPaint);
                    if (strokePaint != null)
                    {
                        canvas.DrawCircle(center, half, strokePaint);
                    }
                    break;
                case SkiaMarkerShape.Square:
                    {
                        var rect = new SKRect(center.X - half, center.Y - half, center.X + half, center.Y + half);
                        canvas.DrawRect(rect, fillPaint);
                        if (strokePaint != null)
                        {
                            canvas.DrawRect(rect, strokePaint);
                        }
                        break;
                    }
                case SkiaMarkerShape.Diamond:
                    {
                        var path = SkiaChartPools.RentPath();
                        try
                        {
                            path.MoveTo(center.X, center.Y - half);
                            path.LineTo(center.X + half, center.Y);
                            path.LineTo(center.X, center.Y + half);
                            path.LineTo(center.X - half, center.Y);
                            path.Close();
                            canvas.DrawPath(path, fillPaint);
                            if (strokePaint != null)
                            {
                                canvas.DrawPath(path, strokePaint);
                            }
                        }
                        finally
                        {
                            SkiaChartPools.ReturnPath(path);
                        }
                        break;
                    }
                case SkiaMarkerShape.Triangle:
                case SkiaMarkerShape.TriangleDown:
                    {
                        var path = SkiaChartPools.RentPath();
                        try
                        {
                            var direction = shape == SkiaMarkerShape.Triangle ? 1f : -1f;
                            path.MoveTo(center.X, center.Y + (direction * -half));
                            path.LineTo(center.X + half, center.Y + (direction * half));
                            path.LineTo(center.X - half, center.Y + (direction * half));
                            path.Close();
                            canvas.DrawPath(path, fillPaint);
                            if (strokePaint != null)
                            {
                                canvas.DrawPath(path, strokePaint);
                            }
                        }
                        finally
                        {
                            SkiaChartPools.ReturnPath(path);
                        }
                        break;
                    }
                case SkiaMarkerShape.Cross:
                case SkiaMarkerShape.Plus:
                case SkiaMarkerShape.X:
                    {
                        var paint = strokePaint ?? fillPaint;
                        if (paint == null)
                        {
                            return;
                        }

                        if (shape == SkiaMarkerShape.X)
                        {
                            canvas.DrawLine(center.X - half, center.Y - half, center.X + half, center.Y + half, paint);
                            canvas.DrawLine(center.X + half, center.Y - half, center.X - half, center.Y + half, paint);
                        }
                        else
                        {
                            canvas.DrawLine(center.X - half, center.Y, center.X + half, center.Y, paint);
                            canvas.DrawLine(center.X, center.Y - half, center.X, center.Y + half, paint);
                        }
                        break;
                    }
            }
        }

        private static float GetPieInnerRadius(
            ChartSeriesSnapshot series,
            int seriesCount,
            float outerRadius,
            float ringThickness,
            SkiaChartStyle style)
        {
            if (seriesCount > 1)
            {
                return Math.Max(0f, outerRadius - ringThickness);
            }

            if (series.Kind == ChartSeriesKind.Donut)
            {
                var ratio = Clamp(style.PieInnerRadiusRatio, 0f, 0.9f);
                return Math.Max(0f, outerRadius * ratio);
            }

            return 0f;
        }

        private static bool IsFinancialSeriesKind(ChartSeriesKind kind)
        {
            return kind == ChartSeriesKind.Candlestick ||
                   kind == ChartSeriesKind.HollowCandlestick ||
                   kind == ChartSeriesKind.Ohlc ||
                   kind == ChartSeriesKind.Hlc ||
                   kind == ChartSeriesKind.HeikinAshi ||
                   kind == ChartSeriesKind.Renko ||
                   kind == ChartSeriesKind.Range ||
                   kind == ChartSeriesKind.LineBreak ||
                   kind == ChartSeriesKind.Kagi ||
                   kind == ChartSeriesKind.PointFigure;
        }

        private static int GetFinancialPointCount(ChartSeriesSnapshot series, ChartSeriesKind kind)
        {
            if (kind == ChartSeriesKind.Hlc)
            {
                return GetHighLowClosePointCount(series);
            }

            if (series.OpenValues == null || series.HighValues == null || series.LowValues == null)
            {
                return 0;
            }

            return Math.Min(
                series.Values.Count,
                Math.Min(
                    series.OpenValues.Count,
                    Math.Min(series.HighValues.Count, series.LowValues.Count)));
        }

        private static int GetHighLowClosePointCount(ChartSeriesSnapshot series)
        {
            if (series.HighValues == null || series.LowValues == null)
            {
                return 0;
            }

            return Math.Min(series.Values.Count, Math.Min(series.HighValues.Count, series.LowValues.Count));
        }

        private static bool TryGetFinancialPoint(
            ChartSeriesSnapshot series,
            ChartSeriesKind kind,
            int index,
            ChartAxisKind valueAxisKind,
            out double? open,
            out double high,
            out double low,
            out double close)
        {
            open = null;
            high = 0d;
            low = 0d;
            close = 0d;

            if (kind == ChartSeriesKind.Hlc)
            {
                return TryGetHighLowClosePoint(series, index, valueAxisKind, out high, out low, out close);
            }

            if (index < 0 || index >= GetFinancialPointCount(series, kind))
            {
                return false;
            }

            var openValue = series.OpenValues![index];
            var highValue = series.HighValues![index];
            var lowValue = series.LowValues![index];
            var closeValue = series.Values[index];
            if (!openValue.HasValue ||
                !highValue.HasValue ||
                !lowValue.HasValue ||
                !closeValue.HasValue)
            {
                return false;
            }

            open = openValue.Value;
            high = highValue.Value;
            low = lowValue.Value;
            close = closeValue.Value;

            if (IsInvalidAxisValue(open.Value, valueAxisKind) ||
                IsInvalidAxisValue(high, valueAxisKind) ||
                IsInvalidAxisValue(low, valueAxisKind) ||
                IsInvalidAxisValue(close, valueAxisKind))
            {
                return false;
            }

            var normalizedHigh = Math.Max(high, Math.Max(open.Value, close));
            var normalizedLow = Math.Min(low, Math.Min(open.Value, close));
            high = normalizedHigh;
            low = normalizedLow;
            return true;
        }

        private static bool TryGetHighLowClosePoint(
            ChartSeriesSnapshot series,
            int index,
            ChartAxisKind valueAxisKind,
            out double high,
            out double low,
            out double close)
        {
            high = 0d;
            low = 0d;
            close = 0d;

            if (index < 0 || index >= GetHighLowClosePointCount(series))
            {
                return false;
            }

            var highValue = series.HighValues![index];
            var lowValue = series.LowValues![index];
            var closeValue = series.Values[index];
            if (!highValue.HasValue || !lowValue.HasValue || !closeValue.HasValue)
            {
                return false;
            }

            high = highValue.Value;
            low = lowValue.Value;
            close = closeValue.Value;

            if (IsInvalidAxisValue(high, valueAxisKind) ||
                IsInvalidAxisValue(low, valueAxisKind) ||
                IsInvalidAxisValue(close, valueAxisKind))
            {
                return false;
            }

            if (high < close)
            {
                high = close;
            }

            if (low > close)
            {
                low = close;
            }

            return true;
        }

        private static float GetFinancialCategorySpan(SKRect plot, int count)
        {
            if (count <= 1)
            {
                return plot.Width;
            }

            return plot.Width / (count - 1);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
