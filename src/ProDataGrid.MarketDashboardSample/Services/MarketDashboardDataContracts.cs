using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProDataGrid.MarketDashboardSample.Models;

namespace ProDataGrid.MarketDashboardSample.Services;

public sealed record MarketInstrumentDefinition(string Symbol, string DisplayName, string BaseAsset, string QuoteAsset);

public sealed record MarketWatchlistQuote(
    MarketInstrumentDefinition Instrument,
    decimal LastPrice,
    decimal ChangePercent,
    decimal QuoteVolume24h);

public sealed record MarketTradeSnapshot(
    DateTimeOffset Timestamp,
    bool IsBuy,
    decimal Quantity,
    decimal QuoteQuantity,
    decimal Price,
    long TradeId);

public sealed record MarketDashboardDataSnapshot(
    string FeedStatusText,
    MarketInstrumentDefinition SelectedInstrument,
    decimal LastPrice,
    decimal PriceChangePercent,
    decimal OpenPrice24h,
    decimal HighPrice24h,
    decimal LowPrice24h,
    decimal BidPrice,
    decimal AskPrice,
    decimal QuoteVolume24h,
    decimal BaseVolume24h,
    int TradeCount24h,
    IReadOnlyList<MarketWatchlistQuote> Watchlist,
    IReadOnlyList<MarketCandle> Candles,
    IReadOnlyList<MarketTradeSnapshot> RecentTrades,
    IReadOnlyList<SparklinePoint> NetFlowPoints,
    decimal RecentBuyVolume,
    decimal RecentSellVolume,
    int RecentBuyCount,
    int RecentSellCount,
    decimal RecentNetFlow,
    decimal RecentVolume,
    DateTimeOffset LastUpdatedUtc);

public interface IMarketDashboardDataService : IDisposable
{
    event Action<MarketDashboardDataSnapshot>? SnapshotChanged;

    IReadOnlyList<MarketInstrumentDefinition> Instruments { get; }

    MarketDashboardDataSnapshot CurrentSnapshot { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task SelectInstrumentAsync(string symbol, CancellationToken cancellationToken = default);

    Task SetChartIntervalAsync(string interval, int candleLimit, CancellationToken cancellationToken = default);
}

public sealed class BinanceMarketDataOptions
{
    public string SelectedSymbol { get; init; } = "LINKUSDT";

    public string KlineInterval { get; init; } = "1m";

    public int CandleLimit { get; init; } = 240;

    public int RecentTradesLimit { get; init; } = 18;

    public IReadOnlyList<MarketInstrumentDefinition> Instruments { get; init; } = new[]
    {
        new MarketInstrumentDefinition("LINKUSDT", "Chainlink", "LINK", "USDT"),
        new MarketInstrumentDefinition("BNBUSDT", "BNB", "BNB", "USDT"),
        new MarketInstrumentDefinition("ETHUSDT", "Ethereum", "ETH", "USDT"),
        new MarketInstrumentDefinition("SOLUSDT", "Solana", "SOL", "USDT"),
        new MarketInstrumentDefinition("XRPUSDT", "XRP", "XRP", "USDT"),
        new MarketInstrumentDefinition("ADAUSDT", "Cardano", "ADA", "USDT"),
        new MarketInstrumentDefinition("DOGEUSDT", "Dogecoin", "DOGE", "USDT"),
        new MarketInstrumentDefinition("AVAXUSDT", "Avalanche", "AVAX", "USDT"),
        new MarketInstrumentDefinition("LTCUSDT", "Litecoin", "LTC", "USDT"),
        new MarketInstrumentDefinition("DOTUSDT", "Polkadot", "DOT", "USDT"),
        new MarketInstrumentDefinition("TRXUSDT", "TRON", "TRX", "USDT"),
        new MarketInstrumentDefinition("UNIUSDT", "Uniswap", "UNI", "USDT")
    };

    public MarketInstrumentDefinition GetSelectedInstrument()
    {
        for (var i = 0; i < Instruments.Count; i++)
        {
            var instrument = Instruments[i];
            if (string.Equals(instrument.Symbol, SelectedSymbol, StringComparison.OrdinalIgnoreCase))
            {
                return instrument;
            }
        }

        return Instruments.Count > 0
            ? Instruments[0]
            : new MarketInstrumentDefinition(SelectedSymbol, SelectedSymbol, SelectedSymbol, "USDT");
    }
}
