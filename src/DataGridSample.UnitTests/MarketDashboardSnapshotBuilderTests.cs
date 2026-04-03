using System;
using ProDataGrid.MarketDashboardSample.Services;
using Xunit;

namespace DataGridSample.Tests;

public sealed class MarketDashboardSnapshotBuilderTests
{
    [Fact]
    public void Build_Preserves_Live_Ticker_Zero_Change_Percent()
    {
        var options = new BinanceMarketDataOptions();
        var fallback = MarketDashboardSampleData.CreateSnapshot(options);
        var now = DateTimeOffset.UtcNow;

        var snapshot = MarketDashboardSnapshotBuilder.Build(
            "Binance Live",
            fallback.SelectedInstrument,
            fallback,
            new MarketTickerSnapshot(
                8.46m,
                0m,
                8.93m,
                9.14m,
                8.42m,
                8.46m,
                8.47m,
                29518649.44m,
                3369961.06m,
                86249,
                now),
            fallback.Watchlist,
            fallback.Candles,
            fallback.RecentTrades,
            now);

        Assert.Equal(0m, snapshot.PriceChangePercent);
        Assert.Equal(8.46m, snapshot.LastPrice);
        Assert.Equal(now, snapshot.LastUpdatedUtc);
    }

    [Fact]
    public void Build_Uses_Zero_Buy_Aggregates_For_All_Sell_Live_Trades()
    {
        var options = new BinanceMarketDataOptions();
        var fallback = MarketDashboardSampleData.CreateSnapshot(options);
        var now = DateTimeOffset.UtcNow;
        var recentTrades = new[]
        {
            new MarketTradeSnapshot(now.AddSeconds(-12), false, 1m, 8.46m, 8.46m, 101),
            new MarketTradeSnapshot(now.AddSeconds(-3), false, 2m, 16.92m, 8.46m, 102)
        };

        var snapshot = MarketDashboardSnapshotBuilder.Build(
            "Binance Live",
            fallback.SelectedInstrument,
            fallback,
            new MarketTickerSnapshot(
                8.46m,
                -5.26m,
                8.93m,
                9.14m,
                8.42m,
                8.46m,
                8.47m,
                29518649.44m,
                3369961.06m,
                86249,
                now),
            fallback.Watchlist,
            fallback.Candles,
            recentTrades,
            now);

        Assert.Equal(0m, snapshot.RecentBuyVolume);
        Assert.Equal(0, snapshot.RecentBuyCount);
        Assert.Equal(25.38m, snapshot.RecentSellVolume);
        Assert.Equal(2, snapshot.RecentSellCount);
        Assert.Equal(-25.38m, snapshot.RecentNetFlow);
        Assert.Equal(25.38m, snapshot.RecentVolume);
        Assert.Equal(2, snapshot.NetFlowPoints.Count);
        Assert.Equal(-25.38d, snapshot.NetFlowPoints[^1].Value, 3);
    }
}
