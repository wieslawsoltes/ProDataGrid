// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProCharts;
using SkiaSharp;

namespace ProCharts.Skia
{
    public sealed partial class SkiaChartRenderer
    {
        private static bool TryGetRangeAreaPoint(ChartSeriesSnapshot series, int index, ChartAxisKind axisKind,
            out double low, out double high)
        {
            low = high = 0;
            if (index < 0 || index >= series.Values.Count || series.LowValues == null || series.HighValues == null ||
                index >= series.LowValues.Count || index >= series.HighValues.Count ||
                series.LowValues[index] is not double lower || series.HighValues[index] is not double upper ||
                IsInvalidAxisValue(lower, axisKind) || IsInvalidAxisValue(upper, axisKind) || lower > upper) return false;
            low = lower;
            high = upper;
            return true;
        }

        private static bool TryProjectRangeAreaPoint(SKRect plot, ChartSeriesSnapshot series, int index,
            double minimum, double maximum, ChartAxisKind axisKind, bool numericX,
            ChartAxisKind categoryKind, double minX, double maxX, out RangeAreaPoint point)
        {
            point = default;
            if (!TryGetRangeAreaPoint(series, index, axisKind, out double low, out double high)) return false;
            double? xValue = null;
            float x;
            if (numericX)
            {
                if (series.XValues == null || index >= series.XValues.Count || IsInvalidAxisValue(series.XValues[index], categoryKind)) return false;
                xValue = series.XValues[index];
                x = (float)(plot.Left + RangeAxisRatio(xValue.Value, minX, maxX, categoryKind) * plot.Width);
            }
            else x = MapX(plot, index, series.Values.Count);
            float lower = (float)(plot.Bottom - RangeAxisRatio(low, minimum, maximum, axisKind) * plot.Height);
            float upper = (float)(plot.Bottom - RangeAxisRatio(high, minimum, maximum, axisKind) * plot.Height);
            if (!float.IsFinite(x) || !float.IsFinite(lower) || !float.IsFinite(upper)) return false;
            point = new RangeAreaPoint(index, x, lower, upper, low, high, xValue);
            return true;
        }

        // Do not clamp each endpoint to the viewport: that would bend a sloping interval at a zoom
        // boundary. Draw and hit-test the same projected segment, clipping at the plot rectangle.
        private static double RangeAxisRatio(double value, double minimum, double maximum, ChartAxisKind kind)
        {
            if (kind == ChartAxisKind.Logarithmic)
            { value = Math.Log10(value); minimum = Math.Log10(minimum); maximum = Math.Log10(maximum); }
            if (maximum <= minimum) return 0.5;
            double numerator = value - minimum, denominator = maximum - minimum;
            return double.IsFinite(numerator) && double.IsFinite(denominator)
                ? numerator / denominator : (value / 2 - minimum / 2) / (maximum / 2 - minimum / 2);
        }

        private static double RangeMidpoint(double low, double high)
        {
            double difference = high - low;
            return double.IsFinite(difference) ? Math.FusedMultiplyAdd(difference, 0.5, low) : low / 2 + high / 2;
        }

        private static void DrawRangeAreaSeries(SKCanvas canvas, SKRect plot, ChartSeriesSnapshot series,
            int seriesIndex, double minimum, double maximum, ChartAxisKind axisKind, SkiaChartStyle style,
            bool numericX, ChartAxisKind categoryKind, double minX, double maxX)
        {
            var overrides = GetSeriesStyleOverrides(style, seriesIndex);
            var theme = GetThemeSeriesStyle(style, seriesIndex);
            SKColor stroke = ResolveSeriesStrokeColor(style, seriesIndex, overrides, theme);
            SKColor fill = ResolveSeriesFillColor(stroke, overrides, theme);
            float width = ResolveSeriesStrokeWidth(overrides, theme, style.SeriesStrokeWidth);
            var gradient = ResolveSeriesGradient(overrides, theme);
            using SKShader? shader = gradient == null ? null : CreateGradientShader(plot, gradient, style.AreaFillOpacity);
            using SKPathEffect? effect = CreateLineEffect(ResolveSeriesLineStyle(overrides, theme), width,
                ResolveSeriesDashPattern(overrides, theme));
            using SKPaint fillPaint = new() { Color = ApplyOpacity(fill, style.AreaFillOpacity), IsAntialias = true, Shader = shader };
            using SKPaint strokePaint = new() { Color = stroke, StrokeWidth = width, IsAntialias = true,
                Style = SKPaintStyle.Stroke, PathEffect = effect };
            SKPath path = SkiaChartPools.RentPath();
            List<RangeAreaPoint> run = SkiaChartPools.RentList<RangeAreaPoint>();
            canvas.Save();
            try
            {
                canvas.ClipRect(plot);
                for (int i = 0; i < series.Values.Count; i++)
                {
                    if (!TryProjectRangeAreaPoint(plot, series, i, minimum, maximum, axisKind, numericX, categoryKind, minX, maxX, out var point))
                    {
                        DrawRangeAreaRun(canvas, run, path, fillPaint, strokePaint);
                        continue;
                    }
                    if (run.Count > 0 && point.X <= run[^1].X)
                        DrawRangeAreaRun(canvas, run, path, fillPaint, strokePaint);
                    run.Add(point);
                }
                DrawRangeAreaRun(canvas, run, path, fillPaint, strokePaint);
            }
            finally
            {
                canvas.Restore();
                SkiaChartPools.ReturnPath(path);
                SkiaChartPools.ReturnList(run);
            }
        }

        private static void DrawRangeAreaRun(SKCanvas canvas, List<RangeAreaPoint> run, SKPath path, SKPaint fill, SKPaint stroke)
        {
            if (run.Count == 0) return;
            path.Reset();
            path.MoveTo(run[0].X, run[0].LowerY);
            for (int i = 1; i < run.Count; i++) path.LineTo(run[i].X, run[i].LowerY);
            for (int i = run.Count - 1; i >= 0; i--) path.LineTo(run[i].X, run[i].UpperY);
            path.Close();
            if (run.Count > 1) canvas.DrawPath(path, fill);
            if (stroke.StrokeWidth > 0) canvas.DrawPath(path, stroke);
            run.Clear();
        }

        private static SkiaChartHitTestResult? HitTestRangeArea(SKPoint pointer, SKRect plot,
            IReadOnlyList<string?> categories, ChartSeriesSnapshot series, int seriesIndex,
            double minimum, double maximum, ChartAxisKind axisKind, SkiaChartStyle style,
            bool numericX, ChartAxisKind categoryKind, double minX, double maxX)
        {
            if (!plot.Contains(pointer)) return null;
            RangeAreaPoint? previous = null;
            float radius = float.IsFinite(style.HitTestRadius) ? Math.Max(0, style.HitTestRadius) : 0;
            for (int i = 0; i < series.Values.Count; i++)
            {
                if (!TryProjectRangeAreaPoint(plot, series, i, minimum, maximum, axisKind, numericX, categoryKind, minX, maxX, out var current))
                { previous = null; continue; }
                if (Math.Abs((double)pointer.X - current.X) <= radius && pointer.Y >= current.UpperY && pointer.Y <= current.LowerY)
                    return RangeAreaHit(current, categories, series, seriesIndex);
                if (previous is RangeAreaPoint before && current.X > before.X && pointer.X >= before.X && pointer.X <= current.X)
                {
                    double fraction = ((double)pointer.X - before.X) / ((double)current.X - before.X);
                    double lowY = before.LowerY * (1 - fraction) + current.LowerY * fraction;
                    double highY = before.UpperY * (1 - fraction) + current.UpperY * fraction;
                    if (pointer.Y >= highY && pointer.Y <= lowY)
                        return RangeAreaHit(fraction <= 0.5 ? before : current, categories, series, seriesIndex);
                }
                previous = current;
            }
            return null;
        }

        private static SkiaChartHitTestResult RangeAreaHit(RangeAreaPoint point, IReadOnlyList<string?> categories,
            ChartSeriesSnapshot series, int seriesIndex) => new(seriesIndex, point.Index,
                RangeMidpoint(point.Low, point.High), point.XValue, GetCategory(categories, point.Index), series.Name,
                ChartSeriesKind.RangeArea, new SKPoint(point.X, (float)((double)point.UpperY / 2 + (double)point.LowerY / 2)),
                highValue: point.High, lowValue: point.Low);

        private static void DrawRangeAreaDataLabels(SKCanvas canvas, SKRect plot, ChartSeriesSnapshot series,
            int seriesIndex, double minimum, double maximum, ChartAxisKind axisKind, SkiaChartStyle style,
            bool numericX, ChartAxisKind categoryKind, double minX, double maxX, SKPaint textPaint,
            SKPaint backgroundPaint, List<SKRect> placed)
        {
            for (int i = 0; i < series.Values.Count; i++)
            {
                if (!TryProjectRangeAreaPoint(plot, series, i, minimum, maximum, axisKind, numericX, categoryKind, minX, maxX, out var point)) continue;
                float y = (float)((double)point.LowerY / 2 + (double)point.UpperY / 2);
                if (point.X < plot.Left || point.X > plot.Right || y < plot.Top || y > plot.Bottom) continue;
                string text = FormatDataLabel(series, seriesIndex, point.Low, style) + " – " + FormatDataLabel(series, seriesIndex, point.High, style);
                TryDrawLabelWithFallback(canvas, plot, placed, textPaint, backgroundPaint, text, point.X, y,
                    true, style.DataLabelPadding, style.DataLabelOffset);
            }
        }

        private readonly record struct RangeAreaPoint(int Index, float X, float LowerY, float UpperY,
            double Low, double High, double? XValue);
    }
}
