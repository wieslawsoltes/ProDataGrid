using System;
using System.Collections.Generic;
using ProDataGrid.MarketDashboardSample.Models;

namespace ProDataGrid.MarketDashboardSample.Services;

internal static class MarketDashboardSampleData
{
    public static MarketDashboardDataSnapshot CreateSnapshot(
        BinanceMarketDataOptions options,
        MarketInstrumentDefinition? selectedInstrument = null,
        string? klineInterval = null,
        int? candleLimit = null)
    {
        var instrument = selectedInstrument ?? options.GetSelectedInstrument();
        var interval = string.IsNullOrWhiteSpace(klineInterval) ? options.KlineInterval : klineInterval;
        var resolvedCandleLimit = candleLimit.GetValueOrDefault(options.CandleLimit);
        if (resolvedCandleLimit <= 0)
        {
            resolvedCandleLimit = options.CandleLimit;
        }

        var watchlist = BuildWatchlist(options.Instruments);
        var selectedQuote = FindWatchlistQuote(watchlist, instrument) ?? new MarketWatchlistQuote(
            instrument,
            GetBasePrice(instrument.BaseAsset),
            2.85m,
            421_300m);
        var lastPrice = selectedQuote.LastPrice;
        var changePercent = selectedQuote.ChangePercent;
        var candles = BuildCandles(lastPrice, interval!, resolvedCandleLimit);
        var trades = BuildTrades(instrument, lastPrice);
        var flowPoints = BuildNetFlowPoints(trades);

        ComputeTradeMetrics(
            trades,
            out var recentBuyVolume,
            out var recentSellVolume,
            out var recentBuyCount,
            out var recentSellCount,
            out var recentNetFlow,
            out var recentVolume);

        var openPrice24h = changePercent <= -99.99m
            ? lastPrice
            : decimal.Round(lastPrice / (1m + (changePercent / 100m)), 6);
        var highPrice24h = decimal.Round((decimal)FindHigh(candles), 6);
        var lowPrice24h = decimal.Round((decimal)FindLow(candles), 6);
        var quoteVolume24h = decimal.Round(selectedQuote.QuoteVolume24h, 2);
        var baseVolume24h = lastPrice > 0m
            ? decimal.Round(quoteVolume24h / lastPrice, 2)
            : 0m;
        var tradeCount24h = 14_500 + (FindInstrumentIndex(options.Instruments, instrument.Symbol) * 1_250);
        var lastUpdatedUtc = DateTimeOffset.UtcNow;

        return new MarketDashboardDataSnapshot(
            "Sample Feed",
            instrument,
            lastPrice,
            changePercent,
            openPrice24h,
            highPrice24h,
            lowPrice24h,
            decimal.Round(lastPrice * 0.9993m, 6),
            decimal.Round(lastPrice * 1.0007m, 6),
            quoteVolume24h,
            baseVolume24h,
            tradeCount24h,
            watchlist,
            candles,
            trades,
            flowPoints,
            recentBuyVolume,
            recentSellVolume,
            recentBuyCount,
            recentSellCount,
            recentNetFlow,
            recentVolume,
            lastUpdatedUtc);
    }

    private static IReadOnlyList<MarketCandle> BuildCandles(decimal lastPrice, string interval, int candleCount)
    {
        candleCount = Math.Max(24, candleCount);
        var intervalSpan = ResolveInterval(interval);
        var anchors = new (int Index, double Offset)[]
        {
            (0, -0.012d),
            ((int)Math.Round((candleCount - 1) * 0.12d), 0.018d),
            ((int)Math.Round((candleCount - 1) * 0.24d), 0.008d),
            ((int)Math.Round((candleCount - 1) * 0.39d), -0.022d),
            ((int)Math.Round((candleCount - 1) * 0.56d), -0.031d),
            ((int)Math.Round((candleCount - 1) * 0.78d), 0.004d),
            (candleCount - 1, 0.000d)
        };

        var list = new List<MarketCandle>(candleCount);
        var timestamp = DateTime.UtcNow.AddTicks(-(intervalSpan.Ticks * (candleCount - 1L)));
        var basePrice = (double)Math.Max(0.0001m, lastPrice);
        var previousClose = basePrice * (1d + anchors[0].Offset);
        var intervalScale = Math.Max(0.65d, Math.Sqrt(Math.Max(intervalSpan.TotalMinutes, intervalSpan.TotalSeconds / 60d)));
        var intradayRange = Math.Max(basePrice * (0.008d + (intervalScale * 0.002d)), 0.0002d);

        for (var index = 0; index < candleCount; index++)
        {
            var trend = InterpolateAnchoredOffset(index, anchors);
            var cycle = (Math.Sin(index * 0.51d) * 0.0042d) + (Math.Cos(index * 0.19d) * 0.0021d);
            var close = Math.Max(0.0001d, basePrice * (1d + trend + cycle));
            var open = index == 0
                ? close - (intradayRange * 0.18d)
                : previousClose + (Math.Sin(index * 0.73d) * intradayRange * 0.22d);
            var high = Math.Max(open, close) + (intradayRange * (0.24d + (Math.Abs(Math.Sin(index * 0.37d)) * 0.65d)));
            var low = Math.Max(0.0001d, Math.Min(open, close) - (intradayRange * (0.22d + (Math.Abs(Math.Cos(index * 0.29d)) * 0.58d))));
            var volume = Math.Max(
                15d,
                (basePrice * 120d) +
                (Math.Abs(close - open) * 950d) +
                ((high - low) * 720d) +
                ((index % 12 == 0) ? basePrice * 26d : 0d));
            var traders = Math.Max(2d, 3d + (Math.Abs(close - open) * 14d) + ((Math.Sin(index * 0.41d) + 1d) * 1.9d));

            list.Add(new MarketCandle(
                timestamp.AddTicks(intervalSpan.Ticks * index),
                Math.Round(open, 6),
                Math.Round(high, 6),
                Math.Round(low, 6),
                Math.Round(close, 6),
                Math.Round(volume, 2),
                Math.Round(traders, 2)));

            previousClose = close;
        }

        return list;
    }

    private static TimeSpan ResolveInterval(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "1s" => TimeSpan.FromSeconds(1),
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "1h" => TimeSpan.FromHours(1),
            "4h" => TimeSpan.FromHours(4),
            "1d" => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private static double InterpolateAnchoredOffset(int index, IReadOnlyList<(int Index, double Offset)> anchors)
    {
        for (var i = 0; i < anchors.Count - 1; i++)
        {
            var left = anchors[i];
            var right = anchors[i + 1];
            if (index < left.Index || index > right.Index)
            {
                continue;
            }

            var span = right.Index - left.Index;
            if (span <= 0)
            {
                return right.Offset;
            }

            var ratio = (index - left.Index) / (double)span;
            return left.Offset + ((right.Offset - left.Offset) * ratio);
        }

        return anchors[^1].Offset;
    }

    private static IReadOnlyList<MarketWatchlistQuote> BuildWatchlist(IReadOnlyList<MarketInstrumentDefinition> instruments)
    {
        var list = new List<MarketWatchlistQuote>(instruments.Count);
        for (var i = 0; i < instruments.Count; i++)
        {
            var instrument = instruments[i];
            var price = GetBasePrice(instrument.BaseAsset);
            var change = decimal.Round((decimal)((Math.Sin(i * 0.79d) * 5.9d) + (Math.Cos(i * 0.31d) * 1.4d)), 2);
            var quoteVolume = decimal.Round(
                (price * (12_000m + (i * 2_350m))) +
                (250_000m * (i + 1)),
                2);

            list.Add(new MarketWatchlistQuote(
                instrument,
                decimal.Round(price, price >= 1m ? 2 : 6),
                change,
                quoteVolume));
        }

        return list;
    }

    private static MarketWatchlistQuote? FindWatchlistQuote(
        IReadOnlyList<MarketWatchlistQuote> watchlist,
        MarketInstrumentDefinition instrument)
    {
        for (var i = 0; i < watchlist.Count; i++)
        {
            var candidate = watchlist[i];
            if (string.Equals(candidate.Instrument.Symbol, instrument.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static decimal GetBasePrice(string baseAsset)
    {
        return baseAsset switch
        {
            "BNB" => 609.42m,
            "ETH" => 3211.74m,
            "SOL" => 174.22m,
            "XRP" => 2.14m,
            "ADA" => 0.68m,
            "DOGE" => 0.19m,
            "AVAX" => 38.11m,
            "LTC" => 86.57m,
            "DOT" => 7.84m,
            "TRX" => 0.12m,
            "UNI" => 11.49m,
            _ => 9.11m
        };
    }

    private static IReadOnlyList<MarketTradeSnapshot> BuildTrades(
        MarketInstrumentDefinition instrument,
        decimal lastPrice)
    {
        var notionalUsd = new[]
        {
            7.97m,
            1.02m,
            1.99m,
            46.82m,
            7.54m,
            0.65m,
            17.65m,
            7.38m,
            1.44m
        };
        var prices = new[]
        {
            lastPrice,
            lastPrice * 0.998m,
            lastPrice * 0.999m,
            lastPrice * 1.001m,
            lastPrice * 1.000m,
            lastPrice * 0.996m,
            lastPrice * 1.002m,
            lastPrice * 0.997m,
            lastPrice * 1.001m
        };
        var sides = new[] { true, false, false, true, true, false, true, false, true };
        var offsets = new[] { 2, 3, 3, 12, 18, 24, 28, 41, 55 };
        var now = DateTimeOffset.UtcNow;
        var list = new List<MarketTradeSnapshot>(notionalUsd.Length);

        for (var i = 0; i < notionalUsd.Length; i++)
        {
            var price = decimal.Round(prices[i], lastPrice >= 1m ? 2 : 6);
            var quantity = price > 0m
                ? decimal.Round(notionalUsd[i] / price, 6)
                : 0m;
            list.Add(new MarketTradeSnapshot(
                now.AddMinutes(-offsets[i]),
                sides[i],
                quantity,
                notionalUsd[i],
                price,
                (FindStableTradeId(instrument.Symbol) * 100L) + i + 1));
        }

        return list;
    }

    private static long FindStableTradeId(string symbol)
    {
        return Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(symbol));
    }

    private static IReadOnlyList<SparklinePoint> BuildNetFlowPoints(IReadOnlyList<MarketTradeSnapshot> trades)
    {
        var points = new List<SparklinePoint>(trades.Count);
        var cumulative = 0m;

        for (int index = trades.Count - 1, pointIndex = 0; index >= 0; index--, pointIndex++)
        {
            var trade = trades[index];
            cumulative += trade.IsBuy ? trade.QuoteQuantity : -trade.QuoteQuantity;
            points.Add(new SparklinePoint(pointIndex.ToString(), (double)cumulative));
        }

        return points;
    }

    private static void ComputeTradeMetrics(
        IReadOnlyList<MarketTradeSnapshot> trades,
        out decimal recentBuyVolume,
        out decimal recentSellVolume,
        out int recentBuyCount,
        out int recentSellCount,
        out decimal recentNetFlow,
        out decimal recentVolume)
    {
        recentBuyVolume = 0m;
        recentSellVolume = 0m;
        recentBuyCount = 0;
        recentSellCount = 0;

        for (var i = 0; i < trades.Count; i++)
        {
            var trade = trades[i];
            if (trade.IsBuy)
            {
                recentBuyVolume += trade.QuoteQuantity;
                recentBuyCount++;
            }
            else
            {
                recentSellVolume += trade.QuoteQuantity;
                recentSellCount++;
            }
        }

        recentNetFlow = recentBuyVolume - recentSellVolume;
        recentVolume = recentBuyVolume + recentSellVolume;
    }

    private static double FindHigh(IReadOnlyList<MarketCandle> candles)
    {
        var high = double.MinValue;
        for (var i = 0; i < candles.Count; i++)
        {
            high = Math.Max(high, candles[i].High);
        }

        return high == double.MinValue ? 0d : high;
    }

    private static double FindLow(IReadOnlyList<MarketCandle> candles)
    {
        var low = double.MaxValue;
        for (var i = 0; i < candles.Count; i++)
        {
            low = Math.Min(low, candles[i].Low);
        }

        return low == double.MaxValue ? 0d : low;
    }

    private static int FindInstrumentIndex(IReadOnlyList<MarketInstrumentDefinition> instruments, string symbol)
    {
        for (var i = 0; i < instruments.Count; i++)
        {
            if (string.Equals(instruments[i].Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }
}
