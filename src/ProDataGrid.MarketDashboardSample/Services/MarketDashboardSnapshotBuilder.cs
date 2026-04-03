using System;
using System.Collections.Generic;
using System.Globalization;
using ProDataGrid.MarketDashboardSample.Models;

namespace ProDataGrid.MarketDashboardSample.Services;

internal sealed record MarketTickerSnapshot(
    decimal LastPrice,
    decimal ChangePercent,
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal BidPrice,
    decimal AskPrice,
    decimal QuoteVolume,
    decimal BaseVolume,
    int TradeCount,
    DateTimeOffset LastUpdatedUtc)
{
    public bool HasData => LastUpdatedUtc != default;
}

internal static class MarketDashboardSnapshotBuilder
{
    public static MarketDashboardDataSnapshot Build(
        string feedStatusText,
        MarketInstrumentDefinition selectedInstrument,
        MarketDashboardDataSnapshot fallbackSnapshot,
        MarketTickerSnapshot selectedTicker,
        IReadOnlyList<MarketWatchlistQuote> watchlist,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<MarketTradeSnapshot> recentTrades,
        DateTimeOffset nowUtc)
    {
        var effectiveWatchlist = watchlist.Count > 0 ? watchlist : fallbackSnapshot.Watchlist;
        var effectiveCandles = candles.Count > 0 ? candles : fallbackSnapshot.Candles;
        var hasRecentTrades = recentTrades.Count > 0;
        var effectiveTrades = hasRecentTrades ? recentTrades : fallbackSnapshot.RecentTrades;
        var hasLiveTicker = selectedTicker.HasData;

        decimal recentBuyVolume;
        decimal recentSellVolume;
        int recentBuyCount;
        int recentSellCount;
        decimal recentNetFlow;
        decimal recentVolume;
        IReadOnlyList<SparklinePoint> netFlowPoints;

        if (hasRecentTrades)
        {
            ComputeTradeMetrics(
                effectiveTrades,
                out recentBuyVolume,
                out recentSellVolume,
                out recentBuyCount,
                out recentSellCount,
                out recentNetFlow,
                out recentVolume);

            netFlowPoints = BuildNetFlowPoints(effectiveTrades);
        }
        else
        {
            recentBuyVolume = fallbackSnapshot.RecentBuyVolume;
            recentSellVolume = fallbackSnapshot.RecentSellVolume;
            recentBuyCount = fallbackSnapshot.RecentBuyCount;
            recentSellCount = fallbackSnapshot.RecentSellCount;
            recentNetFlow = fallbackSnapshot.RecentNetFlow;
            recentVolume = fallbackSnapshot.RecentVolume;
            netFlowPoints = fallbackSnapshot.NetFlowPoints;
        }

        return new MarketDashboardDataSnapshot(
            feedStatusText,
            selectedInstrument,
            hasLiveTicker ? selectedTicker.LastPrice : fallbackSnapshot.LastPrice,
            hasLiveTicker ? selectedTicker.ChangePercent : fallbackSnapshot.PriceChangePercent,
            hasLiveTicker ? selectedTicker.OpenPrice : fallbackSnapshot.OpenPrice24h,
            hasLiveTicker ? selectedTicker.HighPrice : fallbackSnapshot.HighPrice24h,
            hasLiveTicker ? selectedTicker.LowPrice : fallbackSnapshot.LowPrice24h,
            hasLiveTicker ? selectedTicker.BidPrice : fallbackSnapshot.BidPrice,
            hasLiveTicker ? selectedTicker.AskPrice : fallbackSnapshot.AskPrice,
            hasLiveTicker ? selectedTicker.QuoteVolume : fallbackSnapshot.QuoteVolume24h,
            hasLiveTicker ? selectedTicker.BaseVolume : fallbackSnapshot.BaseVolume24h,
            hasLiveTicker ? selectedTicker.TradeCount : fallbackSnapshot.TradeCount24h,
            effectiveWatchlist,
            effectiveCandles,
            effectiveTrades,
            netFlowPoints,
            recentBuyVolume,
            recentSellVolume,
            recentBuyCount,
            recentSellCount,
            recentNetFlow,
            recentVolume,
            hasLiveTicker
                ? selectedTicker.LastUpdatedUtc
                : fallbackSnapshot.LastUpdatedUtc != default
                    ? fallbackSnapshot.LastUpdatedUtc
                    : nowUtc);
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

        recentVolume = recentBuyVolume + recentSellVolume;
        recentNetFlow = recentBuyVolume - recentSellVolume;
    }

    private static IReadOnlyList<SparklinePoint> BuildNetFlowPoints(IReadOnlyList<MarketTradeSnapshot> trades)
    {
        var ordered = new MarketTradeSnapshot[trades.Count];
        for (var i = 0; i < trades.Count; i++)
        {
            ordered[i] = trades[trades.Count - 1 - i];
        }

        var start = Math.Max(0, ordered.Length - 12);
        var points = new List<SparklinePoint>(ordered.Length - start);
        var cumulative = 0m;

        for (var index = start; index < ordered.Length; index++)
        {
            var trade = ordered[index];
            cumulative += trade.IsBuy ? trade.QuoteQuantity : -trade.QuoteQuantity;
            points.Add(new SparklinePoint((index - start).ToString(CultureInfo.InvariantCulture), (double)cumulative));
        }

        return points;
    }
}
