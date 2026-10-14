// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using ProCharts.Skia;
using SkiaSharp;

namespace ProCharts.Controls;

public partial class ProChart
{
    private SkiaChartStyle _effectiveChartStyle = new();
    private ChartDataSnapshot? _presentationSnapshot;
    private SkiaChartStyle? _presentationStyle;
    private ChartTheme? _presentationTheme;
    private IReadOnlyList<ChartSeriesStyle>? _presentationOverrides;

    /// <summary>Renderer-ready copy of ChartStyle with snapshot styles and stable original series identities.</summary>
    public static readonly DirectProperty<ProChart, SkiaChartStyle> EffectiveChartStyleProperty =
        AvaloniaProperty.RegisterDirect<ProChart, SkiaChartStyle>(nameof(EffectiveChartStyle), x => x.EffectiveChartStyle);
    /// <summary>Bind PART_Plot.ChartStyle to this property in a replacement template. Caller-owned styles remain unchanged.</summary>
    public SkiaChartStyle EffectiveChartStyle => _effectiveChartStyle;

    private void UpdatePresentation(bool force)
    {
        ChartDataSnapshot snapshot = Model?.Snapshot ?? ChartDataSnapshot.Empty;
        if (!force && ReferenceEquals(snapshot, _presentationSnapshot) && ReferenceEquals(ChartStyle, _presentationStyle) &&
            ReferenceEquals(Model?.Theme, _presentationTheme) && ReferenceEquals(Model?.SeriesStyles, _presentationOverrides)) return;
        _presentationSnapshot = snapshot;
        _presentationStyle = ChartStyle;
        _presentationTheme = Model?.Theme;
        _presentationOverrides = Model?.SeriesStyles;
        var resolved = new SkiaChartSeriesStyle[snapshot.Series.Count];
        var filter = Model?.DataSource as SeriesVisibilityChartDataSource;
        for (int i = 0; i < resolved.Length; i++)
        {
            int original = filter != null && i < filter.OriginalSeriesIndices.Count ? filter.OriginalSeriesIndices[i] : i;
            resolved[i] = ResolvePresentation(original, snapshot.Series[i].Style);
        }
        SkiaChartStyle style = ChartStyle == null ? new() : new(ChartStyle);
        style.SeriesStyles = resolved;
        SetAndRaise(EffectiveChartStyleProperty, ref _effectiveChartStyle, style);
    }

    private SKColor GetLegendColor(int original, ChartSeriesSnapshot series)
    {
        SkiaChartSeriesStyle style = ResolvePresentation(original, series.Style);
        return series.Kind is ChartSeriesKind.Column or ChartSeriesKind.Bar or ChartSeriesKind.Area
            ? style.FillColor ?? style.StrokeColor!.Value : style.StrokeColor!.Value;
    }

    private SkiaChartSeriesStyle ResolvePresentation(int original, ChartSeriesStyle? snapshotStyle)
    {
        SkiaChartSeriesStyle result = Merge(Convert(At(Model?.Theme?.SeriesStyles, original)), At(ChartStyle?.Theme?.SeriesStyles, original));
        result = Merge(result, Convert(snapshotStyle));
        result = Merge(result, Convert(At(Model?.SeriesStyles, original)));
        result = Merge(result, At(ChartStyle?.SeriesStyles, original));
        IReadOnlyList<SKColor>? palette = ChartStyle?.SeriesColors;
        if (palette == null || palette.Count == 0 || ReferenceEquals(palette, SkiaChartStyle.DefaultSeriesColors))
        {
            palette = ChartStyle?.Theme?.SeriesColors;
            if (palette == null || palette.Count == 0)
                palette = Model?.Theme?.SeriesColors?.Select(ToSkiaColor).ToArray();
        }
        if (palette == null || palette.Count == 0) palette = SkiaChartStyle.DefaultSeriesColors;
        result.StrokeColor ??= palette[original % palette.Count];
        return result;
    }
    private static T? At<T>(IReadOnlyList<T>? list, int index) where T : class =>
        list != null && (uint)index < (uint)list.Count ? list[index] : null;
    private static SKColor ToSkiaColor(ChartColor c) => new(c.Red, c.Green, c.Blue, c.Alpha);
    private static SKColor? ToSkiaColor(ChartColor? c) => c.HasValue ? ToSkiaColor(c.Value) : null;
    private static SkiaChartSeriesStyle Convert(ChartSeriesStyle? s) => s == null ? new() : new()
    {
        StrokeColor = ToSkiaColor(s.StrokeColor), FillColor = ToSkiaColor(s.FillColor), StrokeWidth = s.StrokeWidth,
        LineStyle = s.LineStyle.HasValue ? (SkiaLineStyle)s.LineStyle.Value : null,
        LineInterpolation = s.LineInterpolation.HasValue ? (SkiaLineInterpolation)s.LineInterpolation.Value : null,
        DashPattern = s.DashPattern, MarkerShape = s.MarkerShape.HasValue ? (SkiaMarkerShape)s.MarkerShape.Value : null,
        MarkerSize = s.MarkerSize, MarkerFillColor = ToSkiaColor(s.MarkerFillColor), MarkerStrokeColor = ToSkiaColor(s.MarkerStrokeColor),
        MarkerStrokeWidth = s.MarkerStrokeWidth,
        FillGradient = s.FillGradient is { } g ? new SkiaChartGradient
        { Direction = (SkiaGradientDirection)g.Direction, Colors = g.Colors.Select(ToSkiaColor).ToArray(), Stops = g.Stops } : null
    };
    private static SkiaChartSeriesStyle Merge(SkiaChartSeriesStyle? basis, SkiaChartSeriesStyle? over) => new()
    {
        StrokeColor = over?.StrokeColor ?? basis?.StrokeColor, FillColor = over?.FillColor ?? basis?.FillColor,
        StrokeWidth = over?.StrokeWidth ?? basis?.StrokeWidth, LineStyle = over?.LineStyle ?? basis?.LineStyle,
        LineInterpolation = over?.LineInterpolation ?? basis?.LineInterpolation, DashPattern = over?.DashPattern ?? basis?.DashPattern,
        MarkerShape = over?.MarkerShape ?? basis?.MarkerShape, MarkerSize = over?.MarkerSize ?? basis?.MarkerSize,
        MarkerFillColor = over?.MarkerFillColor ?? basis?.MarkerFillColor, MarkerStrokeColor = over?.MarkerStrokeColor ?? basis?.MarkerStrokeColor,
        MarkerStrokeWidth = over?.MarkerStrokeWidth ?? basis?.MarkerStrokeWidth, FillGradient = over?.FillGradient ?? basis?.FillGradient
    };
}
