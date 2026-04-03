using System;

namespace ProDataGrid.MarketDashboardSample.Models;

public enum MarketChartMode
{
    Candlestick,
    HollowCandlestick,
    HeikinAshi,
    Ohlc,
    Hlc
}

public enum MarketOrderSide
{
    Buy,
    Sell
}

public enum MarketTradeHistoryView
{
    All,
    Mine
}

public enum MarketTerminalSection
{
    DexScanTokens,
    Signals,
    MemeExplorer,
    TopTraders
}

public enum MarketWatchlistViewMode
{
    Trending,
    Watchlist,
    Signals
}

public enum MarketWatchlistRange
{
    FiveMinutes,
    OneHour,
    FourHours,
    TwentyFourHours
}

public enum MarketTradePanel
{
    TradeHistory,
    Holders,
    Liquidity,
    Positions,
    Orders
}

public enum MarketFlowLens
{
    Net,
    BuyVolume,
    SellVolume
}

public sealed class MarketCandle
{
    public MarketCandle(DateTime timestamp, double open, double high, double low, double close, double volume, double uniqueTraders)
    {
        Timestamp = timestamp;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        UniqueTraders = uniqueTraders;
    }

    public DateTime Timestamp { get; }

    public double Open { get; }

    public double High { get; }

    public double Low { get; }

    public double Close { get; }

    public double Volume { get; }

    public double UniqueTraders { get; }
}

public sealed class WatchlistItem
{
    public WatchlistItem(
        string marketSymbol,
        string symbol,
        string name,
        string iconText,
        string iconBackground,
        decimal price,
        decimal changePercent,
        decimal quoteVolume24h,
        string marketCap,
        string badges,
        bool isFavorite)
    {
        MarketSymbol = marketSymbol;
        Symbol = symbol;
        Name = name;
        IconText = iconText;
        IconBackground = iconBackground;
        Price = price;
        ChangePercent = changePercent;
        QuoteVolume24h = quoteVolume24h;
        MarketCap = marketCap;
        Badges = badges;
        IsFavorite = isFavorite;
    }

    public string MarketSymbol { get; }

    public string Symbol { get; }

    public string Name { get; }

    public string IconText { get; }

    public string IconBackground { get; }

    public decimal Price { get; }

    public decimal ChangePercent { get; }

    public decimal QuoteVolume24h { get; }

    public string MarketCap { get; }

    public string Badges { get; }

    public bool IsFavorite { get; }

    public bool IsPositive => ChangePercent >= 0m;

    public bool IsNegative => ChangePercent < 0m;

    public string PriceText => Price >= 1m ? $"${Price:N2}" : $"${Price:N6}";

    public string ChangeText => $"{(ChangePercent >= 0m ? "+" : string.Empty)}{ChangePercent:N2}%";
}

public sealed class TradeHistoryItem
{
    public TradeHistoryItem(
        string relativeTime,
        string side,
        decimal amount,
        string asset,
        decimal quoteAmount,
        string quoteAsset,
        decimal price,
        decimal total,
        string maker,
        string transactionId,
        bool isLocalOrder = false,
        string? makerBadgeText = null)
    {
        RelativeTime = relativeTime;
        Side = side;
        Amount = amount;
        Asset = asset;
        QuoteAmount = quoteAmount;
        QuoteAsset = quoteAsset;
        Price = price;
        Total = total;
        Maker = maker;
        TransactionId = transactionId;
        IsLocalOrder = isLocalOrder;
        MakerBadgeText = string.IsNullOrWhiteSpace(makerBadgeText)
            ? isLocalOrder
                ? "YOU"
                : "999+"
            : makerBadgeText;
    }

    public string RelativeTime { get; }

    public string Side { get; }

    public decimal Amount { get; }

    public string Asset { get; }

    public decimal QuoteAmount { get; }

    public string QuoteAsset { get; }

    public decimal Price { get; }

    public decimal Total { get; }

    public string Maker { get; }

    public string TransactionId { get; }

    public bool IsLocalOrder { get; }

    public string MakerBadgeText { get; }

    public bool IsBuy => string.Equals(Side, "Buy", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => string.Equals(Side, "Sell", StringComparison.OrdinalIgnoreCase);

    public string AmountText => $"{(Amount >= 0m ? "+" : string.Empty)}{Amount:N4} {Asset}";

    public string QuoteAmountText => $"{QuoteAmount:N4} {QuoteAsset}";

    public string PriceText => $"${Price:N2}";

    public string TotalText => $"${Total:N2}";

    public string MakerText => Maker;
}

public sealed class SparklinePoint
{
    public SparklinePoint(string label, double value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public double Value { get; }
}

public sealed class HolderBreakdownItem
{
    public HolderBreakdownItem(string segment, string walletsText, string allocationText, string activityText)
    {
        Segment = segment;
        WalletsText = walletsText;
        AllocationText = allocationText;
        ActivityText = activityText;
    }

    public string Segment { get; }

    public string WalletsText { get; }

    public string AllocationText { get; }

    public string ActivityText { get; }
}

public sealed class LiquidityVenueItem
{
    public LiquidityVenueItem(string venue, string bidText, string askText, string spreadText, string depthText, bool isPrimary)
    {
        Venue = venue;
        BidText = bidText;
        AskText = askText;
        SpreadText = spreadText;
        DepthText = depthText;
        IsPrimary = isPrimary;
    }

    public string Venue { get; }

    public string BidText { get; }

    public string AskText { get; }

    public string SpreadText { get; }

    public string DepthText { get; }

    public bool IsPrimary { get; }
}

public sealed class PositionSummaryItem
{
    public PositionSummaryItem(string marketText, string balanceText, string averageEntryText, string markText, string pnlText, bool isPositive, bool isNegative)
    {
        MarketText = marketText;
        BalanceText = balanceText;
        AverageEntryText = averageEntryText;
        MarkText = markText;
        PnlText = pnlText;
        IsPositive = isPositive;
        IsNegative = isNegative;
    }

    public string MarketText { get; }

    public string BalanceText { get; }

    public string AverageEntryText { get; }

    public string MarkText { get; }

    public string PnlText { get; }

    public bool IsPositive { get; }

    public bool IsNegative { get; }

    public bool IsNeutral => !IsPositive && !IsNegative;
}

public sealed class OrderLedgerItem
{
    public OrderLedgerItem(string timeText, string marketText, string side, string amountText, string quoteText, string statusText)
    {
        TimeText = timeText;
        MarketText = marketText;
        Side = side;
        AmountText = amountText;
        QuoteText = quoteText;
        StatusText = statusText;
    }

    public string TimeText { get; }

    public string MarketText { get; }

    public string Side { get; }

    public string AmountText { get; }

    public string QuoteText { get; }

    public string StatusText { get; }

    public bool IsBuy => string.Equals(Side, "Buy", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => string.Equals(Side, "Sell", StringComparison.OrdinalIgnoreCase);

    public bool IsNeutral => !IsBuy && !IsSell;
}
