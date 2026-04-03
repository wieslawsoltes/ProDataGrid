using System;
using System.Collections.Generic;
using ProCharts;
using ProDataGrid.MarketDashboardSample.Models;

namespace ProDataGrid.MarketDashboardSample.Charting;

internal sealed class MarketFinancialChartDataSource : IChartDataSource, IChartWindowInfoProvider
{
    private readonly IReadOnlyList<MarketCandle> _candles;
    private static readonly ChartSeriesStyle FastAverageStyle = new()
    {
        StrokeColor = new ChartColor(80, 189, 255),
        StrokeWidth = 1.35f
    };
    private static readonly ChartSeriesStyle SlowAverageStyle = new()
    {
        StrokeColor = new ChartColor(255, 202, 88),
        StrokeWidth = 1.15f
    };
    private static readonly ChartSeriesStyle VwapStyle = new()
    {
        StrokeColor = new ChartColor(192, 139, 255),
        StrokeWidth = 1.1f,
        DashPattern = new[] { 4f, 3f }
    };

    public MarketFinancialChartDataSource(IReadOnlyList<MarketCandle> candles, ChartSeriesKind seriesKind)
    {
        _candles = candles;
        SeriesKind = seriesKind;
    }

    public event EventHandler? DataInvalidated;

    public ChartSeriesKind SeriesKind { get; set; }

    public string SeriesName { get; set; } = "LINK / USDT";

    public bool ShowIndicators { get; set; }

    public int? GetTotalCategoryCount() => _candles.Count;

    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        var (start, count) = ResolveWindow(request, _candles.Count);
        var categories = new string?[count];
        var closeValues = new double?[count];
        double?[]? openValues = SeriesKind == ChartSeriesKind.Hlc ? null : new double?[count];
        var highValues = new double?[count];
        var lowValues = new double?[count];

        if (SeriesKind == ChartSeriesKind.HeikinAshi)
        {
            var previousHeikinOpen = 0d;
            var previousHeikinClose = 0d;

            for (var absoluteIndex = 0; absoluteIndex < start + count; absoluteIndex++)
            {
                var candle = _candles[absoluteIndex];
                var close = (candle.Open + candle.High + candle.Low + candle.Close) / 4d;
                var open = absoluteIndex == 0
                    ? (candle.Open + candle.Close) / 2d
                    : (previousHeikinOpen + previousHeikinClose) / 2d;
                var high = Math.Max(candle.High, Math.Max(open, close));
                var low = Math.Min(candle.Low, Math.Min(open, close));

                previousHeikinOpen = open;
                previousHeikinClose = close;

                if (absoluteIndex < start)
                {
                    continue;
                }

                var targetIndex = absoluteIndex - start;
                categories[targetIndex] = MarketCategoryLabelFormatter.FormatCategory(_candles, absoluteIndex, start + count - 1);
                closeValues[targetIndex] = close;
                if (openValues != null)
                {
                    openValues[targetIndex] = open;
                }

                highValues[targetIndex] = high;
                lowValues[targetIndex] = low;
            }

            return BuildSnapshotWithIndicators(categories, start, count, closeValues, openValues, highValues, lowValues);
        }

        for (var i = 0; i < count; i++)
        {
            var candle = _candles[start + i];
            categories[i] = MarketCategoryLabelFormatter.FormatCategory(_candles, start + i, start + count - 1);
            closeValues[i] = candle.Close;
            if (openValues != null)
            {
                openValues[i] = candle.Open;
            }

            highValues[i] = candle.High;
            lowValues[i] = candle.Low;
        }

        return BuildSnapshotWithIndicators(categories, start, count, closeValues, openValues, highValues, lowValues);
    }

    public void Invalidate()
    {
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private static (int Start, int Count) ResolveWindow(ChartDataRequest request, int totalCount)
    {
        if (totalCount <= 0)
        {
            return (0, 0);
        }

        var count = request.WindowCount ?? totalCount;
        if (count <= 0 || count > totalCount)
        {
            count = totalCount;
        }

        var start = request.WindowStart ?? Math.Max(0, totalCount - count);
        if (start < 0)
        {
            start = 0;
        }

        if (start + count > totalCount)
        {
            start = Math.Max(0, totalCount - count);
        }

        return (start, count);
    }

    private ChartDataSnapshot BuildSnapshotWithIndicators(
        IReadOnlyList<string?> categories,
        int start,
        int count,
        IReadOnlyList<double?> closeValues,
        IReadOnlyList<double?>? openValues,
        IReadOnlyList<double?>? highValues,
        IReadOnlyList<double?>? lowValues)
    {
        var series = new List<ChartSeriesSnapshot>(4)
        {
            new(
                SeriesName,
                SeriesKind,
                closeValues,
                openValues: openValues,
                highValues: highValues,
                lowValues: lowValues)
        };

        if (ShowIndicators && _candles.Count > 0)
        {
            series.Add(new ChartSeriesSnapshot("EMA 9", ChartSeriesKind.Line, BuildEma(9, start, count), style: FastAverageStyle));
            series.Add(new ChartSeriesSnapshot("EMA 21", ChartSeriesKind.Line, BuildEma(21, start, count), style: SlowAverageStyle));
            series.Add(new ChartSeriesSnapshot("VWAP", ChartSeriesKind.Line, BuildVwap(start, count), style: VwapStyle));
        }

        return new ChartDataSnapshot(categories, series);
    }

    private double?[] BuildEma(int period, int start, int count)
    {
        var values = new double?[count];
        if (period <= 0 || count <= 0 || _candles.Count == 0)
        {
            return values;
        }

        var smoothing = 2d / (period + 1d);
        double? ema = null;
        for (var absoluteIndex = 0; absoluteIndex < _candles.Count; absoluteIndex++)
        {
            var close = _candles[absoluteIndex].Close;
            ema = ema.HasValue
                ? (close * smoothing) + (ema.Value * (1d - smoothing))
                : close;

            if (absoluteIndex < start || absoluteIndex >= start + count)
            {
                continue;
            }

            values[absoluteIndex - start] = Math.Round(ema.Value, 6);
        }

        return values;
    }

    private double?[] BuildVwap(int start, int count)
    {
        var values = new double?[count];
        if (count <= 0 || _candles.Count == 0)
        {
            return values;
        }

        double cumulativePriceVolume = 0d;
        double cumulativeVolume = 0d;
        for (var absoluteIndex = 0; absoluteIndex < _candles.Count; absoluteIndex++)
        {
            var candle = _candles[absoluteIndex];
            var typicalPrice = (candle.High + candle.Low + candle.Close) / 3d;
            cumulativePriceVolume += typicalPrice * candle.Volume;
            cumulativeVolume += candle.Volume;

            if (absoluteIndex < start || absoluteIndex >= start + count)
            {
                continue;
            }

            values[absoluteIndex - start] = cumulativeVolume > double.Epsilon
                ? Math.Round(cumulativePriceVolume / cumulativeVolume, 6)
                : null;
        }

        return values;
    }
}

internal sealed class MarketVolumeChartDataSource : IChartDataSource, IChartWindowInfoProvider
{
    private readonly IReadOnlyList<MarketCandle> _candles;

    public MarketVolumeChartDataSource(IReadOnlyList<MarketCandle> candles)
    {
        _candles = candles;
    }

    public event EventHandler? DataInvalidated;

    public int? GetTotalCategoryCount() => _candles.Count;

    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        var (start, count) = ResolveWindow(request, _candles.Count);
        var categories = new string?[count];
        var bullishVolumes = new double?[count];
        var bearishVolumes = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var candle = _candles[start + i];
            categories[i] = MarketCategoryLabelFormatter.FormatCategory(_candles, start + i, start + count - 1);

            if (candle.Close >= candle.Open)
            {
                bullishVolumes[i] = candle.Volume;
            }
            else
            {
                bearishVolumes[i] = candle.Volume;
            }
        }

        return new ChartDataSnapshot(
            categories,
            new[]
            {
                new ChartSeriesSnapshot("Buy Volume", ChartSeriesKind.Column, bullishVolumes),
                new ChartSeriesSnapshot("Sell Volume", ChartSeriesKind.Column, bearishVolumes)
            });
    }

    public void Invalidate()
    {
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private static (int Start, int Count) ResolveWindow(ChartDataRequest request, int totalCount)
    {
        if (totalCount <= 0)
        {
            return (0, 0);
        }

        var count = request.WindowCount ?? totalCount;
        if (count <= 0 || count > totalCount)
        {
            count = totalCount;
        }

        var start = request.WindowStart ?? Math.Max(0, totalCount - count);
        if (start < 0)
        {
            start = 0;
        }

        if (start + count > totalCount)
        {
            start = Math.Max(0, totalCount - count);
        }

        return (start, count);
    }
}

internal sealed class MarketTraderActivityChartDataSource : IChartDataSource, IChartWindowInfoProvider
{
    private readonly IReadOnlyList<MarketCandle> _candles;

    public MarketTraderActivityChartDataSource(IReadOnlyList<MarketCandle> candles)
    {
        _candles = candles;
    }

    public event EventHandler? DataInvalidated;

    public int? GetTotalCategoryCount() => _candles.Count;

    public string SeriesName { get; set; } = "Trades / Candle";

    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        var (start, count) = ResolveWindow(request, _candles.Count);
        var categories = new string?[count];
        var traders = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var candle = _candles[start + i];
            categories[i] = MarketCategoryLabelFormatter.FormatCategory(_candles, start + i, start + count - 1);
            traders[i] = candle.UniqueTraders;
        }

        return new ChartDataSnapshot(
            categories,
            new[]
            {
                new ChartSeriesSnapshot(SeriesName, ChartSeriesKind.Column, traders)
            });
    }

    public void Invalidate()
    {
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private static (int Start, int Count) ResolveWindow(ChartDataRequest request, int totalCount)
    {
        if (totalCount <= 0)
        {
            return (0, 0);
        }

        var count = request.WindowCount ?? totalCount;
        if (count <= 0 || count > totalCount)
        {
            count = totalCount;
        }

        var start = request.WindowStart ?? Math.Max(0, totalCount - count);
        if (start < 0)
        {
            start = 0;
        }

        if (start + count > totalCount)
        {
            start = Math.Max(0, totalCount - count);
        }

        return (start, count);
    }
}

internal sealed class MarketSparklineChartDataSource : IChartDataSource
{
    private readonly IReadOnlyList<SparklinePoint> _points;

    public MarketSparklineChartDataSource(IReadOnlyList<SparklinePoint> points)
    {
        _points = points;
    }

    public event EventHandler? DataInvalidated;

    public string SeriesName { get; set; } = "Net Flow";

    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        var categories = new string?[_points.Count];
        var values = new double?[_points.Count];

        for (var i = 0; i < _points.Count; i++)
        {
            categories[i] = _points[i].Label;
            values[i] = _points[i].Value;
        }

        return new ChartDataSnapshot(
            categories,
            new[]
            {
                new ChartSeriesSnapshot(SeriesName, ChartSeriesKind.Area, values)
            });
    }

    public void Invalidate()
    {
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }
}

internal static class MarketCategoryLabelFormatter
{
    public static string? FormatCategory(IReadOnlyList<MarketCandle> candles, int absoluteIndex, int lastAbsoluteIndex)
    {
        var candle = candles[absoluteIndex];

        if (absoluteIndex == lastAbsoluteIndex)
        {
            return candle.Timestamp.ToString("HH:mm");
        }

        if (absoluteIndex == 0 || candle.Timestamp.Hour == 0)
        {
            return candle.Timestamp.ToString("d");
        }

        return string.Empty;
    }
}
