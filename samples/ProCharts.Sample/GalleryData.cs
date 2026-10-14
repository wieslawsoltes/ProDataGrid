// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Globalization;

namespace ProCharts.Sample;

/// <summary>Deterministic, synthetic chart data. No market or business data is downloaded.</summary>
public static class GalleryData
{
    public static readonly IReadOnlyList<ChartColor> Palette = Array.AsReadOnly(new[]
    {
        ChartColor.FromRgb(129,140,248), ChartColor.FromRgb(45,212,191),
        ChartColor.FromRgb(251,191,36), ChartColor.FromRgb(244,114,182),
        ChartColor.FromRgb(56,189,248), ChartColor.FromRgb(192,132,252)
    });
    public static ChartSeriesStyle Style(int index) => new()
    { StrokeColor = Palette[index % Palette.Count], FillColor = Palette[index % Palette.Count], StrokeWidth = 2.3f };

    public static IChartDataSource Create(ChartSeriesKind kind, bool overview = false, bool financial = false)
    {
        if (kind is ChartSeriesKind.Treemap or ChartSeriesKind.Sunburst)
        {
            ChartHierarchyNode root = new("root", "Product portfolio", new[]
            {
                new ChartHierarchyNode("platform", "Platform", new[] { new ChartHierarchyNode("api", "API", 42), new ChartHierarchyNode("sdk", "SDK", 23), new ChartHierarchyNode("infra", "Infrastructure", 18) }),
                new ChartHierarchyNode("apps", "Applications", new[] { new ChartHierarchyNode("desktop", "Desktop", 35), new ChartHierarchyNode("browser", "Browser", 28), new ChartHierarchyNode("mobile", "Mobile", 19) }),
                new ChartHierarchyNode("services", "Services", new[] { new ChartHierarchyNode("support", "Support", 16), new ChartHierarchyNode("consulting", "Consulting", 12) })
            });
            return new HierarchyChartDataSource(root, kind);
        }
        int count = kind switch
        {
            ChartSeriesKind.Pie or ChartSeriesKind.Donut or ChartSeriesKind.Funnel or ChartSeriesKind.Radar => 6,
            ChartSeriesKind.Gauge => 1,
            ChartSeriesKind.Heatmap => 12,
            ChartSeriesKind.Waterfall => 10,
            ChartSeriesKind.BoxWhisker => 60,
            _ => 120
        };
        if (overview) count = 48;
        string?[] labels = new string?[count];
        double[] x = new double[count];
        double?[] first = new double?[count], second = new double?[count], open = new double?[count], high = new double?[count], low = new double?[count], sizes = new double?[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = count <= 12 ? new[] { "North", "South", "East", "West", "Central", "Online", "Partner", "Direct", "Enterprise", "Other", "New", "Renewal" }[i % 12]
                : new DateTime(2026, 1, 1).AddDays(i).ToString("MMM dd", CultureInfo.InvariantCulture);
            x[i] = i * 1.0 + (i % 4) * 0.07;
            first[i] = 70 + i * 0.16 + 16 * Math.Sin(i * 0.19) + 5 * Math.Sin(i * 0.67);
            second[i] = 35 + i * 0.08 + 10 * Math.Cos(i * 0.23);
            open[i] = first[i] + Math.Sin(i * 1.63) * 7;
            high[i] = Math.Max(first[i]!.Value, open[i]!.Value) + 3 + i % 5;
            low[i] = Math.Min(first[i]!.Value, open[i]!.Value) - 3 - i % 4;
            sizes[i] = 5 + i % 15;
        }
        if (kind == ChartSeriesKind.Waterfall)
            first = new double?[] { 120, 25, -18, 32, -11, 24, -20, 18, -7, 163 };
        if (kind == ChartSeriesKind.Gauge) first[0] = 78;
        List<ChartSeriesSnapshot> series = new();
        if (overview)
        {
            series.Add(new("Organic", ChartSeriesKind.Area, first, style: Style(0)));
            series.Add(new("Direct", ChartSeriesKind.Line, second, style: Style(1)));
            series.Add(new("Campaign", ChartSeriesKind.Column, first.Select((v, i) => (double?)(12 + 9 * Math.Abs(Math.Sin(i * 0.36)))).ToArray(), style: Style(2)));
        }
        else if (kind == ChartSeriesKind.Heatmap)
        {
            for (int row = 0; row < 6; row++)
                series.Add(new($"Region {row + 1}", kind,
                    Enumerable.Range(0, count).Select(i => (double?)(Math.Sin((i + row * 2) * 0.44) * 40 + 50)).ToArray(), style: Style(row)));
        }
        else if (kind == ChartSeriesKind.RangeArea)
        {
            series.Add(ChartRangeSeries.CreateArea("Confidence interval", low, high, x, style: Style(0)));
            series.Add(new("Observations", ChartSeriesKind.Scatter, first, x, style: new ChartSeriesStyle
            { StrokeColor = Palette[1], FillColor = Palette[1], MarkerShape = ChartMarkerShape.Circle, MarkerSize = 3 }));
        }
        else
        {
            ChartSeriesSnapshot main = new("Series A", kind, first, x, sizes, style: Style(0),
                openValues: open, highValues: high, lowValues: low);
            series.Add(main);
            if (financial)
            {
                ChartSeriesSnapshot average = ChartIndicators.SimpleMovingAverage(main, 14);
                series.Add(new("SMA 14", ChartSeriesKind.Line, average.Values, style: Style(2)));
            }
            else if (kind is ChartSeriesKind.Line or ChartSeriesKind.Area or ChartSeriesKind.Column or ChartSeriesKind.Bar or
                     ChartSeriesKind.Scatter or ChartSeriesKind.Bubble or ChartSeriesKind.Radar or ChartSeriesKind.BoxWhisker or
                     ChartSeriesKind.StackedArea or ChartSeriesKind.StackedBar or ChartSeriesKind.StackedColumn or
                     ChartSeriesKind.StackedArea100 or ChartSeriesKind.StackedBar100 or ChartSeriesKind.StackedColumn100)
                series.Add(new("Series B", kind, second, x, sizes, style: Style(1)));
        }
        return new GallerySnapshotSource(new ChartDataSnapshot(labels, series.AsReadOnly()));
    }
}

/// <summary>Small immutable gallery dataset with index-window support. Large streams use the real streaming sources.</summary>
public sealed class GallerySnapshotSource(ChartDataSnapshot data) : IChartDataSource, IChartWindowInfoProvider
{
    private (int Start, int Count)? _key;
    private ChartDataSnapshot? _cached;
    public event EventHandler? DataInvalidated { add { } remove { } }
    public int? GetTotalCategoryCount() => data.Categories.Count;
    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        int start = Math.Clamp(request.WindowStart ?? 0, 0, data.Categories.Count);
        int count = Math.Clamp(request.WindowCount ?? data.Categories.Count - start, 0, data.Categories.Count - start);
        if (_cached != null && _key == (start, count)) return _cached;
        if (start == 0 && count == data.Categories.Count) return data;
        T[]? Slice<T>(IReadOnlyList<T>? items)
        {
            if (items == null) return null;
            int length = Math.Min(count, Math.Max(0, items.Count - start));
            T[] result = new T[length];
            for (int i = 0; i < length; i++) result[i] = items[start + i];
            return result;
        }
        List<ChartSeriesSnapshot> series = new();
        foreach (ChartSeriesSnapshot s in data.Series)
            series.Add(new(s.Name, s.Kind, Slice(s.Values)!, Slice(s.XValues), Slice(s.SizeValues),
                s.DataLabelFormatter, s.ValueAxisAssignment, s.TrendlineType, s.TrendlinePeriod, s.ErrorBarType,
                s.ErrorBarValue, s.Style, Slice(s.OpenValues), Slice(s.HighValues), Slice(s.LowValues)));
        _key = (start, count);
        return _cached = new ChartDataSnapshot(Slice(data.Categories)!, series.AsReadOnly());
    }
}
