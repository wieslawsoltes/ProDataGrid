using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Threading.Tasks;
using ProCharts;
using ProDataGrid.MarketDashboardSample.Models;
using ProDataGrid.MarketDashboardSample.Services;
using ReactiveUI;

namespace ProDataGrid.MarketDashboardSample.ViewModels;

public sealed partial class MarketDashboardViewModel
{
    private MarketTerminalSection _terminalSection = MarketTerminalSection.DexScanTokens;
    private MarketWatchlistViewMode _watchlistMode = MarketWatchlistViewMode.Trending;
    private MarketWatchlistRange _watchlistRange = MarketWatchlistRange.TwentyFourHours;
    private MarketTradePanel _tradePanel = MarketTradePanel.TradeHistory;
    private MarketFlowLens _flowLens = MarketFlowLens.Net;
    private string _selectedWatchlistScope = "All";
    private string _selectedChartInterval = "1m";
    private bool _isDexModeEnabled = true;
    private bool _showIndicators;
    private bool _isLabelGuideVisible;
    private bool _isChartIntervalMenuOpen;

    public ObservableCollection<HolderBreakdownItem> HolderBreakdown { get; }

    public ObservableCollection<LiquidityVenueItem> LiquidityVenues { get; }

    public ObservableCollection<PositionSummaryItem> PositionSummaries { get; }

    public ObservableCollection<OrderLedgerItem> OrderLedger { get; }

    public IReadOnlyList<string> WatchlistScopes { get; }

    public ReactiveCommand<Unit, Unit> ToggleDexModeCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleIndicatorsCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleLabelGuideCommand { get; }

    public ReactiveCommand<Unit, Unit> UndoChartWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> RedoChartWindowCommand { get; }

    public ReactiveCommand<ChartPointerTool, Unit> SelectPointerToolCommand { get; }

    public ReactiveCommand<MarketTerminalSection, Unit> SelectTerminalSectionCommand { get; }

    public ReactiveCommand<MarketWatchlistViewMode, Unit> SelectWatchlistModeCommand { get; }

    public ReactiveCommand<MarketWatchlistRange, Unit> SelectWatchlistRangeCommand { get; }

    public ReactiveCommand<WatchlistItem, Unit> ToggleFavoriteWatchlistItemCommand { get; }

    public ReactiveCommand<MarketTradePanel, Unit> SelectTradePanelCommand { get; }

    public ReactiveCommand<MarketFlowLens, Unit> SelectFlowLensCommand { get; }

    public ReactiveCommand<string, Unit> SelectChartIntervalCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleChartIntervalMenuCommand { get; }

    public ReactiveCommand<Unit, Unit> ApplyCurrentSelectionCommand { get; }

    public bool IsDexScanTokensSectionSelected => _terminalSection == MarketTerminalSection.DexScanTokens;

    public bool IsSignalsSectionSelected => _terminalSection == MarketTerminalSection.Signals;

    public bool IsMemeExplorerSectionSelected => _terminalSection == MarketTerminalSection.MemeExplorer;

    public bool IsTopTradersSectionSelected => _terminalSection == MarketTerminalSection.TopTraders;

    public bool IsTrendingWatchlistSelected => _watchlistMode == MarketWatchlistViewMode.Trending;

    public bool IsWatchlistWatchlistSelected => _watchlistMode == MarketWatchlistViewMode.Watchlist;

    public bool IsSignalsWatchlistSelected => _watchlistMode == MarketWatchlistViewMode.Signals;

    public bool IsFiveMinuteRangeSelected => _watchlistRange == MarketWatchlistRange.FiveMinutes;

    public bool IsOneHourRangeSelected => _watchlistRange == MarketWatchlistRange.OneHour;

    public bool IsFourHourRangeSelected => _watchlistRange == MarketWatchlistRange.FourHours;

    public bool IsTwentyFourHourRangeSelected => _watchlistRange == MarketWatchlistRange.TwentyFourHours;

    public bool IsTradeHistoryPanelSelected => _tradePanel == MarketTradePanel.TradeHistory;

    public bool IsHolderPanelSelected => _tradePanel == MarketTradePanel.Holders;

    public bool IsLiquidityPanelSelected => _tradePanel == MarketTradePanel.Liquidity;

    public bool IsPositionsPanelSelected => _tradePanel == MarketTradePanel.Positions;

    public bool IsOrdersPanelSelected => _tradePanel == MarketTradePanel.Orders;

    public bool IsNetFlowLensSelected => _flowLens == MarketFlowLens.Net;

    public bool IsBuyFlowLensSelected => _flowLens == MarketFlowLens.BuyVolume;

    public bool IsSellFlowLensSelected => _flowLens == MarketFlowLens.SellVolume;

    public bool IsCrosshairToolSelected => PriceChart.Interaction.PointerTool == ChartPointerTool.Crosshair;

    public bool IsPanToolSelected => PriceChart.Interaction.PointerTool == ChartPointerTool.Pan;

    public bool IsZoomToolSelected => PriceChart.Interaction.PointerTool == ChartPointerTool.Zoom;

    public bool IsMeasureToolSelected => PriceChart.Interaction.PointerTool == ChartPointerTool.Measure;

    public bool IsInterval1sSelected => string.Equals(_selectedChartInterval, "1s", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval1mSelected => string.Equals(_selectedChartInterval, "1m", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval5mSelected => string.Equals(_selectedChartInterval, "5m", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval15mSelected => string.Equals(_selectedChartInterval, "15m", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval1hSelected => string.Equals(_selectedChartInterval, "1h", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval4hSelected => string.Equals(_selectedChartInterval, "4h", StringComparison.OrdinalIgnoreCase);

    public bool IsInterval1dSelected => string.Equals(_selectedChartInterval, "1d", StringComparison.OrdinalIgnoreCase);

    public bool IsDexModeEnabled => _isDexModeEnabled;

    public bool AreIndicatorsVisible => _showIndicators;

    public bool IsLabelGuideVisible => _isLabelGuideVisible;

    public bool IsChartIntervalMenuOpen => _isChartIntervalMenuOpen;

    public bool IsChartIntervalMenuClosed => !_isChartIntervalMenuOpen;

    public bool CanUndoChartWindow => PriceChart.CanUndoWindow;

    public bool CanRedoChartWindow => PriceChart.CanRedoWindow;

    public string SelectedWatchlistScope
    {
        get => _selectedWatchlistScope;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(_selectedWatchlistScope, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedWatchlistScope, value);
            ApplyWatchlistFilter();
        }
    }

    public string IndicatorsButtonText => _showIndicators ? "Indicators On" : "Indicators";

    public string DexModeStatusText => _isDexModeEnabled ? "DEX Mode" : "Spot Mode";

    public string ActiveSectionTitleText => _terminalSection switch
    {
        MarketTerminalSection.Signals => "Live Signals",
        MarketTerminalSection.MemeExplorer => "Momentum Explorer",
        MarketTerminalSection.TopTraders => "Top Traders",
        _ => "DexScan Tokens"
    };

    public string ActiveSectionDescriptionText => _terminalSection switch
    {
        MarketTerminalSection.Signals => "Signal-ranked watchlist with indicator overlays and momentum sorting.",
        MarketTerminalSection.MemeExplorer => "High-beta movers ordered by activity and volatility.",
        MarketTerminalSection.TopTraders => "Portfolio and local order flow emphasized for active monitoring.",
        _ => "Live Binance spot stream styled like a dense trading terminal."
    };

    public string LabelGuideText => _tradePanel switch
    {
        MarketTradePanel.Holders => "Holder buckets are synthetic distribution bands derived from live turnover and price structure.",
        MarketTradePanel.Liquidity => "Venue rows summarize bid, ask, spread, and modeled depth anchored to the current live symbol.",
        MarketTradePanel.Positions => "Positions update from simulated wallet fills placed against the live Binance price stream.",
        MarketTradePanel.Orders => "Orders list executed demo orders only; there is no live exchange account integration.",
        _ => "Trade history mixes live recent trades with your local demo fills for the selected market."
    };

    private static IReadOnlyList<string> BuildWatchlistScopes(IReadOnlyList<string> quoteAssets)
    {
        var items = new List<string>(quoteAssets.Count + 1) { "All" };
        for (var i = 0; i < quoteAssets.Count; i++)
        {
            items.Add(quoteAssets[i]);
        }

        return new ReadOnlyCollection<string>(items);
    }

    private void ToggleDexMode()
    {
        _isDexModeEnabled = !_isDexModeEnabled;
        RaiseModeDependentProperties();
        SetOrderStatus(
            _isDexModeEnabled
                ? "DEX mode enabled. Terminal now emphasizes route-style pricing and wallet workflow."
                : "Spot mode enabled. Terminal now emphasizes exchange bid / ask and venue depth.",
            positive: true,
            negative: false);
    }

    private void ToggleIndicators()
    {
        _showIndicators = !_showIndicators;
        _priceSource.ShowIndicators = _showIndicators;
        _priceSource.Invalidate();
        PriceChart.Refresh();
        this.RaisePropertyChanged(nameof(AreIndicatorsVisible));
        this.RaisePropertyChanged(nameof(IndicatorsButtonText));
    }

    private void ToggleLabelGuide()
    {
        _isLabelGuideVisible = !_isLabelGuideVisible;
        this.RaisePropertyChanged(nameof(IsLabelGuideVisible));
    }

    private void UndoChartWindow()
    {
        if (PriceChart.UndoWindow())
        {
            UpdateHoveredPriceSummary();
        }
    }

    private void RedoChartWindow()
    {
        if (PriceChart.RedoWindow())
        {
            UpdateHoveredPriceSummary();
        }
    }

    private void SelectPointerTool(ChartPointerTool tool)
    {
        if (PriceChart.Interaction.PointerTool == tool)
        {
            return;
        }

        PriceChart.Interaction.PointerTool = tool;
        RaisePointerToolProperties();
        SetOrderStatus($"{tool} tool active.", positive: false, negative: false);
    }

    private void SelectTerminalSection(MarketTerminalSection section)
    {
        if (_terminalSection == section)
        {
            return;
        }

        _terminalSection = section;
        if (section == MarketTerminalSection.Signals)
        {
            _watchlistMode = MarketWatchlistViewMode.Signals;
            if (!_showIndicators)
            {
                ToggleIndicators();
            }
        }
        else if (section == MarketTerminalSection.TopTraders)
        {
            _tradePanel = MarketTradePanel.Positions;
        }

        RaiseTerminalSectionProperties();
        ApplyWatchlistFilter();
        UpdateSupplementalPanels();
    }

    private void SelectWatchlistMode(MarketWatchlistViewMode mode)
    {
        if (_watchlistMode == mode)
        {
            return;
        }

        _watchlistMode = mode;
        RaiseWatchlistModeProperties();
        ApplyWatchlistFilter();
    }

    private void SelectWatchlistRange(MarketWatchlistRange range)
    {
        if (_watchlistRange == range)
        {
            return;
        }

        _watchlistRange = range;
        RaiseWatchlistRangeProperties();
        ApplyWatchlistFilter();
    }

    private void SelectTradePanel(MarketTradePanel panel)
    {
        if (_tradePanel == panel)
        {
            return;
        }

        _tradePanel = panel;
        RaiseTradePanelProperties();
        UpdateSupplementalPanels();
    }

    private void ToggleFavoriteWatchlistItem(WatchlistItem item)
    {
        if (item is null)
        {
            return;
        }

        var wasAdded = _favoriteSymbols.Add(item.MarketSymbol);
        if (!wasAdded)
        {
            _favoriteSymbols.Remove(item.MarketSymbol);
        }

        _allWatchlistItems.Clear();
        var watchlistItems = BuildWatchlistItems(_snapshot.Watchlist);
        for (var i = 0; i < watchlistItems.Count; i++)
        {
            _allWatchlistItems.Add(watchlistItems[i]);
        }

        ApplyWatchlistFilter(_snapshot.SelectedInstrument.Symbol);
        SetOrderStatus(
            wasAdded
                ? $"{item.Symbol} added to watchlist favorites."
                : $"{item.Symbol} removed from watchlist favorites.",
            positive: wasAdded,
            negative: false);
    }

    private void SelectFlowLens(MarketFlowLens lens)
    {
        if (_flowLens == lens)
        {
            return;
        }

        _flowLens = lens;
        RaiseFlowLensProperties();
        ReplaceCollection(_flowPoints, BuildFlowPoints(_snapshot));
        _flowSource.Invalidate();
        FlowChart.Refresh();
    }

    private async Task SelectChartIntervalAsync(string interval)
    {
        if (string.IsNullOrWhiteSpace(interval))
        {
            return;
        }

        var normalizedInterval = interval.Trim().ToLowerInvariant();
        if (string.Equals(_selectedChartInterval, normalizedInterval, StringComparison.OrdinalIgnoreCase))
        {
            _isChartIntervalMenuOpen = false;
            this.RaisePropertyChanged(nameof(IsChartIntervalMenuOpen));
            this.RaisePropertyChanged(nameof(IsChartIntervalMenuClosed));
            return;
        }

        _selectedChartInterval = normalizedInterval;
        _isChartIntervalMenuOpen = false;
        RaiseChartIntervalProperties();
        SetOrderStatus($"Switching chart interval to {normalizedInterval.ToUpperInvariant()}…", positive: false, negative: false);

        try
        {
            PriceChart.ResetValueRange();
            await _dataService.SetChartIntervalAsync(normalizedInterval, ResolveCandleLimit(normalizedInterval)).ConfigureAwait(false);
        }
        catch
        {
            SetOrderStatus($"Unable to load {normalizedInterval.ToUpperInvariant()} candles.", positive: false, negative: true);
        }
    }

    private void ToggleChartIntervalMenu()
    {
        _isChartIntervalMenuOpen = !_isChartIntervalMenuOpen;
        this.RaisePropertyChanged(nameof(IsChartIntervalMenuOpen));
        this.RaisePropertyChanged(nameof(IsChartIntervalMenuClosed));
    }

    private async Task ApplyCurrentSelectionAsync()
    {
        var selectedItem = SelectedWatchlistItem;
        if (selectedItem is null)
        {
            return;
        }

        try
        {
            PriceChart.ResetValueRange();
            await _dataService.SelectInstrumentAsync(selectedItem.MarketSymbol).ConfigureAwait(false);
            await _dataService.SetChartIntervalAsync(_selectedChartInterval, ResolveCandleLimit(_selectedChartInterval)).ConfigureAwait(false);
        }
        catch
        {
            SetOrderStatus("Unable to refresh the current market selection.", positive: false, negative: true);
        }
    }

    private static int ResolveCandleLimit(string interval)
    {
        return interval switch
        {
            "1s" => 480,
            "1m" => 240,
            "5m" => 240,
            "15m" => 240,
            "1h" => 200,
            "4h" => 160,
            "1d" => 120,
            _ => 240
        };
    }

    private void RaiseTerminalSectionProperties()
    {
        this.RaisePropertyChanged(nameof(IsDexScanTokensSectionSelected));
        this.RaisePropertyChanged(nameof(IsSignalsSectionSelected));
        this.RaisePropertyChanged(nameof(IsMemeExplorerSectionSelected));
        this.RaisePropertyChanged(nameof(IsTopTradersSectionSelected));
        this.RaisePropertyChanged(nameof(ActiveSectionTitleText));
        this.RaisePropertyChanged(nameof(ActiveSectionDescriptionText));
        RaiseWatchlistModeProperties();
        RaiseTradePanelProperties();
    }

    private void RaiseWatchlistModeProperties()
    {
        this.RaisePropertyChanged(nameof(IsTrendingWatchlistSelected));
        this.RaisePropertyChanged(nameof(IsWatchlistWatchlistSelected));
        this.RaisePropertyChanged(nameof(IsSignalsWatchlistSelected));
    }

    private void RaiseWatchlistRangeProperties()
    {
        this.RaisePropertyChanged(nameof(IsFiveMinuteRangeSelected));
        this.RaisePropertyChanged(nameof(IsOneHourRangeSelected));
        this.RaisePropertyChanged(nameof(IsFourHourRangeSelected));
        this.RaisePropertyChanged(nameof(IsTwentyFourHourRangeSelected));
    }

    private void RaiseTradePanelProperties()
    {
        this.RaisePropertyChanged(nameof(IsTradeHistoryPanelSelected));
        this.RaisePropertyChanged(nameof(IsHolderPanelSelected));
        this.RaisePropertyChanged(nameof(IsLiquidityPanelSelected));
        this.RaisePropertyChanged(nameof(IsPositionsPanelSelected));
        this.RaisePropertyChanged(nameof(IsOrdersPanelSelected));
        this.RaisePropertyChanged(nameof(LabelGuideText));
    }

    private void RaiseFlowLensProperties()
    {
        this.RaisePropertyChanged(nameof(IsNetFlowLensSelected));
        this.RaisePropertyChanged(nameof(IsBuyFlowLensSelected));
        this.RaisePropertyChanged(nameof(IsSellFlowLensSelected));
    }

    private void RaisePointerToolProperties()
    {
        this.RaisePropertyChanged(nameof(IsCrosshairToolSelected));
        this.RaisePropertyChanged(nameof(IsPanToolSelected));
        this.RaisePropertyChanged(nameof(IsZoomToolSelected));
        this.RaisePropertyChanged(nameof(IsMeasureToolSelected));
    }

    private void RaiseChartIntervalProperties()
    {
        this.RaisePropertyChanged(nameof(IsInterval1sSelected));
        this.RaisePropertyChanged(nameof(IsInterval1mSelected));
        this.RaisePropertyChanged(nameof(IsInterval5mSelected));
        this.RaisePropertyChanged(nameof(IsInterval15mSelected));
        this.RaisePropertyChanged(nameof(IsInterval1hSelected));
        this.RaisePropertyChanged(nameof(IsInterval4hSelected));
        this.RaisePropertyChanged(nameof(IsInterval1dSelected));
        this.RaisePropertyChanged(nameof(IsChartIntervalMenuOpen));
        this.RaisePropertyChanged(nameof(IsChartIntervalMenuClosed));
    }

    private void RaiseModeDependentProperties()
    {
        this.RaisePropertyChanged(nameof(IsDexModeEnabled));
        this.RaisePropertyChanged(nameof(DexModeStatusText));
        this.RaisePropertyChanged(nameof(SecurityHeaderText));
        this.RaisePropertyChanged(nameof(SecurityStatusText));
        this.RaisePropertyChanged(nameof(BuyTaxText));
        this.RaisePropertyChanged(nameof(SellTaxText));
        this.RaisePropertyChanged(nameof(FeePercentText));
        this.RaisePropertyChanged(nameof(GasFeeText));
        this.RaisePropertyChanged(nameof(PairAddressText));
        this.RaisePropertyChanged(nameof(PairMetaText));
    }

    private void OnPriceChartPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartModel.CanUndoWindow))
        {
            this.RaisePropertyChanged(nameof(CanUndoChartWindow));
        }
        else if (e.PropertyName == nameof(ChartModel.CanRedoWindow))
        {
            this.RaisePropertyChanged(nameof(CanRedoChartWindow));
        }
    }

    private bool MatchesWatchlistScope(WatchlistItem item)
    {
        return string.Equals(_selectedWatchlistScope, "All", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Badges, _selectedWatchlistScope, StringComparison.OrdinalIgnoreCase);
    }

    private void SortWatchlist(List<WatchlistItem> items)
    {
        if (_watchlistMode == MarketWatchlistViewMode.Watchlist && _terminalSection == MarketTerminalSection.DexScanTokens)
        {
            return;
        }

        items.Sort((left, right) =>
        {
            var rightScore = ComputeWatchlistRank(right);
            var leftScore = ComputeWatchlistRank(left);
            var scoreComparison = rightScore.CompareTo(leftScore);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            return string.Compare(left.Symbol, right.Symbol, StringComparison.OrdinalIgnoreCase);
        });
    }

    private decimal ComputeWatchlistRank(WatchlistItem item)
    {
        var absoluteChange = decimal.Abs(item.ChangePercent);
        var rangeMultiplier = _watchlistRange switch
        {
            MarketWatchlistRange.FiveMinutes => 1.45m,
            MarketWatchlistRange.OneHour => 1.25m,
            MarketWatchlistRange.FourHours => 1.10m,
            _ => 1.00m
        };
        var volumeScore = item.QuoteVolume24h / 1_000_000m;
        decimal baseScore = _watchlistMode switch
        {
            MarketWatchlistViewMode.Signals => (item.ChangePercent >= 0m ? 40m : 0m) + (absoluteChange * 3.2m) + volumeScore,
            MarketWatchlistViewMode.Watchlist => (item.ChangePercent * 1.5m) + (volumeScore * 0.4m),
            _ => (absoluteChange * 2.6m) + (volumeScore * 0.65m)
        };

        baseScore *= rangeMultiplier;
        return _terminalSection switch
        {
            MarketTerminalSection.Signals => baseScore + (item.ChangePercent >= 0m ? 35m : -10m),
            MarketTerminalSection.MemeExplorer => baseScore + (absoluteChange * 2.1m),
            MarketTerminalSection.TopTraders => baseScore + (volumeScore * 1.4m),
            _ => baseScore
        };
    }

    private IReadOnlyList<SparklinePoint> BuildFlowPoints(MarketDashboardDataSnapshot snapshot)
    {
        if (_flowLens == MarketFlowLens.Net)
        {
            return snapshot.NetFlowPoints;
        }

        var bucketCount = Math.Min(8, Math.Max(1, snapshot.RecentTrades.Count));
        var values = new decimal[bucketCount];
        var labels = new string[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            labels[i] = $"{i + 1}";
        }

        if (snapshot.RecentTrades.Count == 0)
        {
            var empty = new List<SparklinePoint>(bucketCount);
            for (var i = 0; i < bucketCount; i++)
            {
                empty.Add(new SparklinePoint(labels[i], 0d));
            }

            return empty;
        }

        for (var tradeIndex = 0; tradeIndex < snapshot.RecentTrades.Count; tradeIndex++)
        {
            var trade = snapshot.RecentTrades[snapshot.RecentTrades.Count - tradeIndex - 1];
            var bucketIndex = (tradeIndex * bucketCount) / snapshot.RecentTrades.Count;
            if (bucketIndex < 0)
            {
                bucketIndex = 0;
            }
            else if (bucketIndex >= bucketCount)
            {
                bucketIndex = bucketCount - 1;
            }

            if (_flowLens == MarketFlowLens.BuyVolume)
            {
                if (trade.IsBuy)
                {
                    values[bucketIndex] += trade.QuoteQuantity;
                }
            }
            else if (!trade.IsBuy)
            {
                values[bucketIndex] += trade.QuoteQuantity;
            }
        }

        var points = new List<SparklinePoint>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            points.Add(new SparklinePoint(labels[i], (double)values[i]));
        }

        return points;
    }

    private void UpdateSupplementalPanels()
    {
        ReplaceCollection(HolderBreakdown, BuildHolderBreakdown());
        ReplaceCollection(LiquidityVenues, BuildLiquidityVenues());
        ReplaceCollection(PositionSummaries, BuildPositionSummaries());
        ReplaceCollection(OrderLedger, BuildOrderLedger());
    }

    private IReadOnlyList<HolderBreakdownItem> BuildHolderBreakdown()
    {
        var holderBase = Math.Max(42_000, _snapshot.TradeCount24h * 13);
        var whaleShare = Math.Clamp(22m + decimal.Abs(_snapshot.PriceChangePercent * 0.6m), 16m, 42m);
        var largeShare = Math.Clamp(24m + (_snapshot.RecentBuyCount * 1.2m), 18m, 34m);
        var coreShare = Math.Clamp(28m + ((_snapshot.RecentSellCount - _snapshot.RecentBuyCount) * 0.8m), 16m, 34m);
        var retailShare = Math.Max(8m, 100m - whaleShare - largeShare - coreShare);

        return new[]
        {
            new HolderBreakdownItem("Whales", $"{holderBase / 210:N0}", $"{whaleShare:N1}%", "Aggressive accumulation"),
            new HolderBreakdownItem("Large", $"{holderBase / 36:N0}", $"{largeShare:N1}%", "Passive adds"),
            new HolderBreakdownItem("Core", $"{holderBase / 8:N0}", $"{coreShare:N1}%", "Balanced flow"),
            new HolderBreakdownItem("Retail", $"{holderBase:N0}", $"{retailShare:N1}%", "Fast turnover")
        };
    }

    private IReadOnlyList<LiquidityVenueItem> BuildLiquidityVenues()
    {
        var bid = _snapshot.BidPrice;
        var ask = _snapshot.AskPrice > 0m ? _snapshot.AskPrice : _snapshot.LastPrice;
        var spread = ask - bid;
        var depthBase = Math.Max(25_000m, _snapshot.RecentVolume * 14m);
        return new[]
        {
            new LiquidityVenueItem("Binance Spot", FormatPrice(bid), FormatPrice(ask), FormatPrice(spread), FormatCompactDollar(depthBase), true),
            new LiquidityVenueItem("Passive Makers", FormatPrice(bid * 0.9998m), FormatPrice(ask * 1.0002m), FormatPrice(spread * 1.15m), FormatCompactDollar(depthBase * 0.74m), false),
            new LiquidityVenueItem("Aggressive Sweep", FormatPrice(bid * 0.9995m), FormatPrice(ask * 1.0005m), FormatPrice(spread * 1.35m), FormatCompactDollar(depthBase * 0.52m), false)
        };
    }

    private IReadOnlyList<PositionSummaryItem> BuildPositionSummaries()
    {
        var items = new List<PositionSummaryItem>();
        foreach (var pair in _positions)
        {
            if (!TryGetInstrument(pair.Key, out var instrument, out var markPrice))
            {
                continue;
            }

            var position = pair.Value;
            if (!position.HasActivity)
            {
                continue;
            }

            var averageEntry = position.Quantity > 0m
                ? position.CostBasisUsd / position.Quantity
                : 0m;
            var pnl = CalculateTotalPnl(position);
            items.Add(new PositionSummaryItem(
                $"{instrument.BaseAsset}/{instrument.QuoteAsset}",
                position.Quantity > 0m ? $"{FormatAssetQuantity(position.Quantity)} {instrument.BaseAsset}" : "--",
                averageEntry > 0m ? FormatPrice(decimal.Round(averageEntry, 6)) : "--",
                FormatPrice(markPrice),
                FormatSignedDollar(pnl),
                pnl >= 0m,
                pnl < 0m));
        }

        if (items.Count == 0)
        {
            items.Add(new PositionSummaryItem($"{PairTicker}/{_snapshot.SelectedInstrument.QuoteAsset}", "--", "--", CurrentPriceText, "--", false, false));
        }

        return items;
    }

    private IReadOnlyList<OrderLedgerItem> BuildOrderLedger()
    {
        var items = new List<OrderLedgerItem>(_localOrders.Count);
        for (var i = 0; i < _localOrders.Count; i++)
        {
            var order = _localOrders[i];
            if (!string.Equals(order.MarketSymbol, _snapshot.SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new OrderLedgerItem(
                order.Trade.RelativeTime,
                $"{order.Trade.Asset}/{order.Trade.QuoteAsset}",
                order.Trade.Side,
                order.Trade.AmountText,
                order.Trade.QuoteAmountText,
                "Filled"));
        }

        if (items.Count == 0)
        {
            items.Add(new OrderLedgerItem("now", $"{PairTicker}/{_snapshot.SelectedInstrument.QuoteAsset}", "Buy", "--", "--", "No demo fills yet"));
        }

        return items;
    }

    private bool TryGetInstrument(string symbol, out MarketInstrumentDefinition instrument, out decimal markPrice)
    {
        if (string.Equals(_snapshot.SelectedInstrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            instrument = _snapshot.SelectedInstrument;
            markPrice = _snapshot.LastPrice;
            return true;
        }

        for (var i = 0; i < _snapshot.Watchlist.Count; i++)
        {
            var candidate = _snapshot.Watchlist[i];
            if (string.Equals(candidate.Instrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            {
                instrument = candidate.Instrument;
                markPrice = candidate.LastPrice;
                return true;
            }
        }

        instrument = _snapshot.SelectedInstrument;
        markPrice = _snapshot.LastPrice;
        return false;
    }
}
