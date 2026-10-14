// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Globalization;
using Avalonia.Interactivity;
using ProCharts.Skia;

namespace ProCharts.Controls;

/// <summary>Strongly typed data context for custom tooltip templates and point activation events.</summary>
public sealed class ChartPointInfo
{
    /// <summary>Constructs point information using the assigned series' value axis and its formatter.</summary>
    public ChartPointInfo(SkiaChartHitTestResult hit, ChartModel? model)
    {
        Hit = hit;
        SeriesName = hit.SeriesName ?? $"Series {hit.SeriesIndex + 1}";
        ChartSeriesSnapshot? series = model != null && (uint)hit.SeriesIndex < (uint)model.Snapshot.Series.Count
            ? model.Snapshot.Series[hit.SeriesIndex] : null;
        ChartAxisDefinition? axis = series?.ValueAxisAssignment == ChartValueAxisAssignment.Secondary
            ? model?.SecondaryValueAxis : model?.ValueAxis;
        string Format(double value) => axis?.LabelFormatter?.Invoke(value)
            ?? ChartValueFormatter.Format(value, axis?.ValueFormat, CultureInfo.CurrentCulture);
        Category = hit.Category ?? (hit.XValue is double x
            ? model?.CategoryAxis.LabelFormatter?.Invoke(x)
              ?? ChartValueFormatter.Format(x, model?.CategoryAxis.ValueFormat, CultureInfo.CurrentCulture)
            : $"Point {hit.PointIndex + 1}");
        ValueText = Format(hit.Value);
        Detail = hit.LowValue is double low && hit.HighValue is double high
            ? $"Low {Format(low)}  ·  High {Format(high)}" : string.Empty;
        if (hit.OpenValue is double open && hit.CloseValue is double close)
            Detail = $"Open {Format(open)}  ·  Close {Format(close)}\n{Detail}";
    }
    public SkiaChartHitTestResult Hit { get; }
    public string SeriesName { get; }
    public string Category { get; }
    public string ValueText { get; }
    public string Detail { get; }
}

/// <summary>A point activated by a click/tap. Indices are into the currently displayed snapshot.</summary>
public sealed class ChartPointInvokedEventArgs(RoutedEvent routedEvent, ChartPointInfo point) : RoutedEventArgs(routedEvent)
{
    public ChartPointInfo Point { get; } = point;
}
