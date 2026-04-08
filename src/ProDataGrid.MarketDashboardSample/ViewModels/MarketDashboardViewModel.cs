using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Threading.Tasks;
using ProCharts;
using ProCharts.Skia;
using ProDataGrid.MarketDashboardSample.Charting;
using ProDataGrid.MarketDashboardSample.Models;
using ProDataGrid.MarketDashboardSample.Services;
using ReactiveUI;
using SkiaSharp;

namespace ProDataGrid.MarketDashboardSample.ViewModels;

public sealed partial class MarketDashboardViewModel : ReactiveObject
{
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly IMarketDashboardDataService _dataService;
    private readonly ObservableCollection<MarketCandle> _candles;
    private readonly ObservableCollection<SparklinePoint> _flowPoints;
    private readonly MarketFinancialChartDataSource _priceSource;
    private readonly MarketVolumeChartDataSource _volumeSource;
    private readonly MarketTraderActivityChartDataSource _traderSource;
    private readonly MarketSparklineChartDataSource _flowSource;
    private readonly List<WatchlistItem> _allWatchlistItems = new();
    private readonly List<TradeHistoryItem> _marketTradeHistoryItems = new();
    private readonly List<LocalOrderRecord> _localOrders = new();
    private readonly Dictionary<string, PortfolioPosition> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _favoriteSymbols = new(StringComparer.OrdinalIgnoreCase);
    private MarketDashboardDataSnapshot _snapshot;
    private MarketChartMode _chartMode = MarketChartMode.Candlestick;
    private string _selectedQuoteAsset = "USDT";
    private string _watchlistFilterText = string.Empty;
    private string _orderAmountText = "0.10";
    private string _orderStatusText = "Connect wallet to place simulated orders.";
    private string _hoveredPriceSummaryText = string.Empty;
    private WatchlistItem? _selectedWatchlistItem;
    private bool _isWalletConnected;
    private bool _isOrderTicketVisible;
    private bool _isUpdatingSelectedWatchlistItem;
    private bool _orderStatusPositive;
    private bool _orderStatusNegative;
    private SkiaChartStyle _priceChartStyle = new();
    private SkiaChartStyle _volumeChartStyle = new();
    private SkiaChartStyle _traderChartStyle = new();
    private SkiaChartStyle _flowChartStyle = new();
    private MarketOrderSide _orderSide = MarketOrderSide.Buy;
    private MarketTradeHistoryView _tradeHistoryView = MarketTradeHistoryView.All;
    private int _previousCandleCount;
    private long _localOrderSequence;

    private sealed class PortfolioPosition
    {
        public decimal Quantity { get; set; }

        public decimal CostBasisUsd { get; set; }

        public decimal BoughtUsd { get; set; }

        public decimal SoldUsd { get; set; }

        public decimal RealizedPnlUsd { get; set; }

        public bool HasActivity => BoughtUsd > 0m || SoldUsd > 0m || Quantity > 0m;
    }

    private sealed class LocalOrderRecord
    {
        public LocalOrderRecord(string marketSymbol, TradeHistoryItem trade)
        {
            MarketSymbol = marketSymbol;
            Trade = trade;
        }

        public string MarketSymbol { get; }

        public TradeHistoryItem Trade { get; }
    }

    public MarketDashboardViewModel()
        : this(new SampleMarketDashboardDataService(new BinanceMarketDataOptions()))
    {
    }

    public MarketDashboardViewModel(IMarketDashboardDataService dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _snapshot = dataService.CurrentSnapshot;

        _candles = new ObservableCollection<MarketCandle>(_snapshot.Candles);
        _flowPoints = new ObservableCollection<SparklinePoint>(_snapshot.NetFlowPoints);
        Watchlist = new ObservableCollection<WatchlistItem>();
        TradeHistory = new ObservableCollection<TradeHistoryItem>();
        HolderBreakdown = new ObservableCollection<HolderBreakdownItem>();
        LiquidityVenues = new ObservableCollection<LiquidityVenueItem>();
        PositionSummaries = new ObservableCollection<PositionSummaryItem>();
        OrderLedger = new ObservableCollection<OrderLedgerItem>();
        QuoteAssets = BuildQuoteAssets(_snapshot.SelectedInstrument.QuoteAsset);
        WatchlistScopes = BuildWatchlistScopes(QuoteAssets);
        _selectedWatchlistScope = WatchlistScopes.Count > 0 ? WatchlistScopes[0] : "All";
        _selectedQuoteAsset = QuoteAssets.Count > 0 ? QuoteAssets[0] : "USDT";

        _priceSource = new MarketFinancialChartDataSource(_candles, ChartSeriesKind.Candlestick);
        _volumeSource = new MarketVolumeChartDataSource(_candles);
        _traderSource = new MarketTraderActivityChartDataSource(_candles);
        _flowSource = new MarketSparklineChartDataSource(_flowPoints);

        PriceChart = new ChartModel
        {
            DataSource = _priceSource
        };
        PriceChart.Legend.IsVisible = false;
        PriceChart.CategoryAxis.IsVisible = true;
        PriceChart.ValueAxis.IsVisible = true;
        PriceChart.Request.PropertyChanged += OnPriceChartRequestChanged;
        PriceChart.Interaction.PropertyChanged += OnPriceChartInteractionChanged;
        PriceChart.PropertyChanged += OnPriceChartPropertyChanged;

        VolumeChart = new ChartModel
        {
            DataSource = _volumeSource
        };
        VolumeChart.Legend.IsVisible = false;
        VolumeChart.CategoryAxis.IsVisible = false;
        VolumeChart.ValueAxis.IsVisible = false;
        VolumeChart.Interaction.CrosshairMode = ChartCrosshairMode.VerticalOnly;

        TraderChart = new ChartModel
        {
            DataSource = _traderSource
        };
        TraderChart.Legend.IsVisible = false;
        TraderChart.CategoryAxis.IsVisible = false;
        TraderChart.ValueAxis.IsVisible = false;
        TraderChart.Interaction.CrosshairMode = ChartCrosshairMode.VerticalOnly;
        SyncCompanionPointerTools();

        FlowChart = new ChartModel
        {
            DataSource = _flowSource
        };
        FlowChart.Legend.IsVisible = false;
        FlowChart.CategoryAxis.IsVisible = false;
        FlowChart.ValueAxis.IsVisible = false;

        ToggleOrderTicketCommand = ReactiveCommand.Create(ToggleOrderTicket);
        HideOrderTicketCommand = ReactiveCommand.Create(HideOrderTicket);
        ConnectWalletCommand = ReactiveCommand.Create(ConnectWallet);
        PrimaryOrderActionCommand = ReactiveCommand.Create(ExecutePrimaryOrderAction);
        QuickOrderAmountCommand = ReactiveCommand.Create<string>(ApplyQuickOrderAmount);
        ShowAllTradesCommand = ReactiveCommand.Create(ShowAllTrades);
        ShowMyTradesCommand = ReactiveCommand.Create(ShowMyTrades);
        ShowLatestWindowCommand = ReactiveCommand.Create(ShowLatestWindow);
        ResetChartWindowCommand = ReactiveCommand.Create(ResetChartWindow);
        ToggleFollowLatestCommand = ReactiveCommand.Create(ToggleFollowLatest);
        ToggleDexModeCommand = ReactiveCommand.Create(ToggleDexMode);
        ToggleIndicatorsCommand = ReactiveCommand.Create(ToggleIndicators);
        ToggleLabelGuideCommand = ReactiveCommand.Create(ToggleLabelGuide);
        UndoChartWindowCommand = ReactiveCommand.Create(UndoChartWindow);
        RedoChartWindowCommand = ReactiveCommand.Create(RedoChartWindow);
        SelectPointerToolCommand = ReactiveCommand.Create<ChartPointerTool>(SelectPointerTool);
        SelectTerminalSectionCommand = ReactiveCommand.Create<MarketTerminalSection>(SelectTerminalSection);
        SelectWatchlistModeCommand = ReactiveCommand.Create<MarketWatchlistViewMode>(SelectWatchlistMode);
        SelectWatchlistRangeCommand = ReactiveCommand.Create<MarketWatchlistRange>(SelectWatchlistRange);
        ToggleFavoriteWatchlistItemCommand = ReactiveCommand.Create<WatchlistItem>(ToggleFavoriteWatchlistItem);
        SelectTradePanelCommand = ReactiveCommand.Create<MarketTradePanel>(SelectTradePanel);
        SelectFlowLensCommand = ReactiveCommand.Create<MarketFlowLens>(SelectFlowLens);
        SelectChartIntervalCommand = ReactiveCommand.CreateFromTask<string>(SelectChartIntervalAsync);
        ToggleChartIntervalMenuCommand = ReactiveCommand.Create(ToggleChartIntervalMenu);
        ApplyCurrentSelectionCommand = ReactiveCommand.CreateFromTask(ApplyCurrentSelectionAsync);

        _dataService.SnapshotChanged += OnSnapshotChanged;

        ApplySnapshot(_snapshot, resetWindow: true);
        _ = _dataService.StartAsync();
    }

    public ObservableCollection<WatchlistItem> Watchlist { get; }

    public ObservableCollection<TradeHistoryItem> TradeHistory { get; }

    public IReadOnlyList<string> QuoteAssets { get; }

    public ChartModel PriceChart { get; }

    public ChartModel VolumeChart { get; }

    public ChartModel TraderChart { get; }

    public ChartModel FlowChart { get; }

    public SkiaChartStyle PriceChartStyle
    {
        get => _priceChartStyle;
        private set => this.RaiseAndSetIfChanged(ref _priceChartStyle, value);
    }

    public SkiaChartStyle VolumeChartStyle
    {
        get => _volumeChartStyle;
        private set => this.RaiseAndSetIfChanged(ref _volumeChartStyle, value);
    }

    public SkiaChartStyle TraderChartStyle
    {
        get => _traderChartStyle;
        private set => this.RaiseAndSetIfChanged(ref _traderChartStyle, value);
    }

    public SkiaChartStyle FlowChartStyle
    {
        get => _flowChartStyle;
        private set => this.RaiseAndSetIfChanged(ref _flowChartStyle, value);
    }

    public Func<SkiaChartHitTestResult, string> PriceToolTipFormatter => FormatPriceToolTip;

    public Func<SkiaChartHitTestResult, string> FlowToolTipFormatter => FormatFlowToolTip;

    public ReactiveCommand<Unit, Unit> ToggleOrderTicketCommand { get; }

    public ReactiveCommand<Unit, Unit> HideOrderTicketCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectWalletCommand { get; }

    public ReactiveCommand<Unit, Unit> PrimaryOrderActionCommand { get; }

    public ReactiveCommand<string, Unit> QuickOrderAmountCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowAllTradesCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowMyTradesCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowLatestWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> ResetChartWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleFollowLatestCommand { get; }

    public MarketChartMode ChartMode
    {
        get => _chartMode;
        set
        {
            if (_chartMode == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _chartMode, value);
            _priceSource.SeriesKind = MapChartMode(value);
            _priceSource.Invalidate();
            PriceChart.Refresh();
            this.RaisePropertyChanged(nameof(IsCandlestickSelected));
            this.RaisePropertyChanged(nameof(IsHollowCandlestickSelected));
            this.RaisePropertyChanged(nameof(IsHeikinAshiSelected));
            this.RaisePropertyChanged(nameof(IsOhlcSelected));
            this.RaisePropertyChanged(nameof(IsHlcSelected));
        }
    }

    public bool IsCandlestickSelected
    {
        get => ChartMode == MarketChartMode.Candlestick;
        set
        {
            if (value)
            {
                ChartMode = MarketChartMode.Candlestick;
            }
        }
    }

    public bool IsHollowCandlestickSelected
    {
        get => ChartMode == MarketChartMode.HollowCandlestick;
        set
        {
            if (value)
            {
                ChartMode = MarketChartMode.HollowCandlestick;
            }
        }
    }

    public bool IsOhlcSelected
    {
        get => ChartMode == MarketChartMode.Ohlc;
        set
        {
            if (value)
            {
                ChartMode = MarketChartMode.Ohlc;
            }
        }
    }

    public bool IsHeikinAshiSelected
    {
        get => ChartMode == MarketChartMode.HeikinAshi;
        set
        {
            if (value)
            {
                ChartMode = MarketChartMode.HeikinAshi;
            }
        }
    }

    public bool IsHlcSelected
    {
        get => ChartMode == MarketChartMode.Hlc;
        set
        {
            if (value)
            {
                ChartMode = MarketChartMode.Hlc;
            }
        }
    }

    public bool IsOrderTicketVisible
    {
        get => _isOrderTicketVisible;
        private set
        {
            if (_isOrderTicketVisible != value)
            {
                this.RaiseAndSetIfChanged(ref _isOrderTicketVisible, value);
                this.RaisePropertyChanged(nameof(OrderTicketToggleText));
            }
        }
    }

    public WatchlistItem? SelectedWatchlistItem
    {
        get => _selectedWatchlistItem;
        set
        {
            if (ReferenceEquals(_selectedWatchlistItem, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedWatchlistItem, value);
            if (!_isUpdatingSelectedWatchlistItem && value is not null)
            {
                _ = SelectInstrumentAsync(value);
            }
        }
    }

    public string WatchlistFilterText
    {
        get => _watchlistFilterText;
        set
        {
            if (!string.Equals(_watchlistFilterText, value, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _watchlistFilterText, value);
                ApplyWatchlistFilter();
            }
        }
    }

    public string DashboardDateText => _snapshot.LastUpdatedUtc.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);

    public string PairTicker => _snapshot.SelectedInstrument.BaseAsset;

    public string PairName => _snapshot.SelectedInstrument.DisplayName;

    public string PairAddressText => _isDexModeEnabled
        ? $"{_snapshot.SelectedInstrument.Symbol} • Route via {SelectedQuoteAsset}"
        : $"{_snapshot.SelectedInstrument.Symbol} • Binance Spot";

    public string PairMetaText => $"{_snapshot.FeedStatusText} • {_selectedChartInterval.ToUpperInvariant()} • {_snapshot.LastUpdatedUtc.ToLocalTime():HH:mm:ss}";

    public string HoveredPriceSummaryText => _hoveredPriceSummaryText;

    public string CurrentPriceText => FormatPrice(_snapshot.LastPrice);

    public string PriceChangeText => $"{(_snapshot.PriceChangePercent >= 0m ? "+" : string.Empty)}{_snapshot.PriceChangePercent:N2}%";

    public bool PriceChangePositive => _snapshot.PriceChangePercent >= 0m;

    public bool PriceChangeNegative => _snapshot.PriceChangePercent < 0m;

    public string SecurityHeaderText => _isDexModeEnabled ? "Security Scan" : "Market Feed";

    public string SecurityStatusText => _isDexModeEnabled ? "Safe" : _snapshot.FeedStatusText;

    public string BuyTaxText => _isDexModeEnabled ? "0.00%" : FormatPrice(_snapshot.BidPrice);

    public string SellTaxText => _isDexModeEnabled ? "0.00%" : FormatPrice(_snapshot.AskPrice);

    public string SocialMentions => _snapshot.TradeCount24h.ToString("N0", CultureInfo.InvariantCulture);

    public string MarketCapText => FormatPrice(_snapshot.OpenPrice24h);

    public string FullyDilutedValueText => FormatPrice(_snapshot.HighPrice24h);

    public string LiquidityText => FormatPrice(_snapshot.LowPrice24h);

    public string TwentyFourHourVolumeText => FormatCompactDollar(_snapshot.QuoteVolume24h);

    public string HoneypotStatusText => FormatPrice(_snapshot.BidPrice);

    public string VerifiedStatusText => FormatPrice(_snapshot.AskPrice);

    public string RugPullStatusText => FormatCompactNumber(_snapshot.BaseVolume24h);

    public string FakeTokenStatusText => _snapshot.TradeCount24h.ToString("N0", CultureInfo.InvariantCulture);

    public string UniqueTraderText => _snapshot.RecentTrades.Count.ToString(CultureInfo.InvariantCulture);

    public string BuyerCountText => _snapshot.RecentBuyCount.ToString(CultureInfo.InvariantCulture);

    public string SellerCountText => _snapshot.RecentSellCount.ToString(CultureInfo.InvariantCulture);

    public string NetFlowText => FormatSignedDollar(_snapshot.RecentNetFlow);

    public string VolumeBreakdownText => FormatCompactDollar(_snapshot.RecentVolume);

    public string VolumePaneValueText
    {
        get
        {
            return TryGetActiveCandle(out var candle, out _, out _)
                ? FormatCompactNumber((decimal)candle.Volume)
                : "--";
        }
    }

    public string TraderPaneSummaryText
    {
        get
        {
            return TryGetActiveCandle(out var candle, out _, out _)
                ? $"Unique Traders {Math.Max(1, (int)Math.Round(candle.UniqueTraders))}"
                : "Unique Traders --";
        }
    }

    public string BoughtSummaryText
    {
        get
        {
            var position = GetCurrentPosition();
            return position is not null && position.BoughtUsd > 0m
                ? FormatCompactDollar(position.BoughtUsd)
                : "--";
        }
    }

    public string SoldSummaryText
    {
        get
        {
            var position = GetCurrentPosition();
            return position is not null && position.SoldUsd > 0m
                ? FormatCompactDollar(position.SoldUsd)
                : "--";
        }
    }

    public string BalanceSummaryText
    {
        get
        {
            var position = GetCurrentPosition();
            return position is not null && position.Quantity > 0m
                ? $"{FormatAssetQuantity(position.Quantity)} {PairTicker}"
                : "--";
        }
    }

    public string TpnlSummaryText
    {
        get
        {
            var position = GetCurrentPosition();
            if (position is null || !position.HasActivity)
            {
                return "--";
            }

            return FormatSignedDollar(CalculateTotalPnl(position));
        }
    }

    public string FeePercentText => _isDexModeEnabled ? "0.60%" : "0.10%";

    public string GasFeeText => _isDexModeEnabled ? "0.06452 Gwei" : "Spot Fee";

    public bool IsWalletConnected
    {
        get => _isWalletConnected;
        private set
        {
            if (_isWalletConnected != value)
            {
                this.RaiseAndSetIfChanged(ref _isWalletConnected, value);
                this.RaisePropertyChanged(nameof(WalletStatusText));
                this.RaisePropertyChanged(nameof(PrimaryOrderActionText));
                this.RaisePropertyChanged(nameof(WalletButtonText));
            }
        }
    }

    public string WalletStatusText => IsWalletConnected ? "Demo wallet connected" : "Wallet disconnected";

    public MarketOrderSide OrderSide
    {
        get => _orderSide;
        set
        {
            if (_orderSide != value)
            {
                this.RaiseAndSetIfChanged(ref _orderSide, value);
                this.RaisePropertyChanged(nameof(IsBuyOrderSelected));
                this.RaisePropertyChanged(nameof(IsSellOrderSelected));
                this.RaisePropertyChanged(nameof(PrimaryOrderActionText));
                this.RaisePropertyChanged(nameof(WalletButtonText));
                this.RaisePropertyChanged(nameof(OrderPreviewText));
            }
        }
    }

    public bool IsBuyOrderSelected
    {
        get => OrderSide == MarketOrderSide.Buy;
        set
        {
            if (value)
            {
                OrderSide = MarketOrderSide.Buy;
            }
        }
    }

    public bool IsSellOrderSelected
    {
        get => OrderSide == MarketOrderSide.Sell;
        set
        {
            if (value)
            {
                OrderSide = MarketOrderSide.Sell;
            }
        }
    }

    public string SelectedQuoteAsset
    {
        get => _selectedQuoteAsset;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!string.Equals(_selectedQuoteAsset, value, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _selectedQuoteAsset, value);
                this.RaisePropertyChanged(nameof(OrderPreviewText));
            }
        }
    }

    public string OrderAmountText
    {
        get => _orderAmountText;
        set
        {
            if (!string.Equals(_orderAmountText, value, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _orderAmountText, value);
                this.RaisePropertyChanged(nameof(OrderPreviewText));
            }
        }
    }

    public string OrderPreviewText
    {
        get
        {
            if (!TryParseOrderAmount(out var amount) || amount <= 0m)
            {
                return $"Enter {SelectedQuoteAsset} amount";
            }

            if (!TryGetQuoteAssetUsdPrice(SelectedQuoteAsset, out var usdRate))
            {
                return $"No live {SelectedQuoteAsset} conversion";
            }

            if (_snapshot.LastPrice <= 0m)
            {
                return $"Waiting for {PairTicker} price";
            }

            var baseQuantity = decimal.Round((amount * usdRate) / _snapshot.LastPrice, 6);
            return $"≈ {FormatAssetQuantity(baseQuantity)} {PairTicker}";
        }
    }

    public string PrimaryOrderActionText => IsWalletConnected
        ? OrderSide == MarketOrderSide.Buy
            ? "Place Buy Order"
            : "Place Sell Order"
        : "Connect Wallet";

    public string WalletButtonText => PrimaryOrderActionText;

    public string OrderTicketToggleText => IsOrderTicketVisible ? "Hide Trade" : "Open Trade";

    public string FollowLatestButtonText => PriceChart.Interaction.FollowLatest ? "Follow On" : "Follow Off";

    public string OrderStatusText => _orderStatusText;

    public bool OrderStatusPositive => _orderStatusPositive;

    public bool OrderStatusNegative => _orderStatusNegative;

    public bool OrderStatusNeutral => !_orderStatusPositive && !_orderStatusNegative;

    public string OrderTicketTitleText => $"{PairTicker} Quick Trade";

    public bool IsAllTradesSelected
    {
        get => _tradeHistoryView == MarketTradeHistoryView.All;
        set
        {
            if (value)
            {
                ShowAllTrades();
            }
        }
    }

    public bool IsMyTradesSelected
    {
        get => _tradeHistoryView == MarketTradeHistoryView.Mine;
        set
        {
            if (value)
            {
                ShowMyTrades();
            }
        }
    }

    private static IReadOnlyList<string> BuildQuoteAssets(string defaultQuoteAsset)
    {
        var items = new List<string>(4);
        AddQuoteAsset(items, defaultQuoteAsset);
        AddQuoteAsset(items, "USDT");
        AddQuoteAsset(items, "BNB");
        AddQuoteAsset(items, "ETH");
        return new ReadOnlyCollection<string>(items);
    }

    private static void AddQuoteAsset(ICollection<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.ToUpperInvariant();
        foreach (var item in target)
        {
            if (string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        target.Add(normalized);
    }

    private void ToggleOrderTicket()
    {
        IsOrderTicketVisible = !IsOrderTicketVisible;
        if (IsOrderTicketVisible)
        {
            SetOrderStatus("Order ticket ready. Connect wallet to trade.", positive: false, negative: false);
        }
    }

    private void HideOrderTicket()
    {
        IsOrderTicketVisible = false;
    }

    private void ConnectWallet()
    {
        IsWalletConnected = true;
        SetOrderStatus("Demo wallet connected. Orders now update your portfolio.", positive: true, negative: false);
    }

    private void ExecutePrimaryOrderAction()
    {
        if (!IsWalletConnected)
        {
            ConnectWallet();
            return;
        }

        PlaceSimulatedOrder();
    }

    private void ApplyQuickOrderAmount(string amountText)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return;
        }

        OrderAmountText = amountText;
    }

    private void ShowAllTrades()
    {
        if (_tradePanel != MarketTradePanel.TradeHistory)
        {
            _tradePanel = MarketTradePanel.TradeHistory;
            RaiseTradePanelProperties();
        }

        if (_tradeHistoryView == MarketTradeHistoryView.All)
        {
            return;
        }

        _tradeHistoryView = MarketTradeHistoryView.All;
        ApplyTradeHistoryFilter();
        this.RaisePropertyChanged(nameof(IsAllTradesSelected));
        this.RaisePropertyChanged(nameof(IsMyTradesSelected));
    }

    private void ShowMyTrades()
    {
        if (_tradePanel != MarketTradePanel.TradeHistory)
        {
            _tradePanel = MarketTradePanel.TradeHistory;
            RaiseTradePanelProperties();
        }

        if (_tradeHistoryView == MarketTradeHistoryView.Mine)
        {
            return;
        }

        _tradeHistoryView = MarketTradeHistoryView.Mine;
        ApplyTradeHistoryFilter();
        this.RaisePropertyChanged(nameof(IsAllTradesSelected));
        this.RaisePropertyChanged(nameof(IsMyTradesSelected));
    }

    private void ShowLatestWindow()
    {
        var preferredCount = ResolvePreferredLatestWindowCount(_candles.Count, PriceChart.Request.WindowCount);
        PriceChart.ShowLatest(preferredCount, true);
        UpdateHoveredPriceSummary();
        this.RaisePropertyChanged(nameof(FollowLatestButtonText));
    }

    private void ResetChartWindow()
    {
        PriceChart.ResetValueRange();
        PriceChart.ResetWindow(false);
        UpdateHoveredPriceSummary();
        this.RaisePropertyChanged(nameof(FollowLatestButtonText));
    }

    private void ToggleFollowLatest()
    {
        if (PriceChart.Interaction.FollowLatest)
        {
            PriceChart.Interaction.FollowLatest = false;
        }
        else
        {
            ShowLatestWindow();
        }

        this.RaisePropertyChanged(nameof(FollowLatestButtonText));
    }

    private async Task SelectInstrumentAsync(WatchlistItem item)
    {
        if (string.Equals(item.MarketSymbol, _snapshot.SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetOrderStatus($"Loading {item.Symbol} market…", positive: false, negative: false);

        try
        {
            PriceChart.ResetValueRange();
            await _dataService.SelectInstrumentAsync(item.MarketSymbol).ConfigureAwait(false);
        }
        catch
        {
            SetOrderStatus($"Unable to load {item.Symbol} market.", positive: false, negative: true);
        }
    }

    private void PlaceSimulatedOrder()
    {
        if (!TryParseOrderAmount(out var quoteAmount) || quoteAmount <= 0m)
        {
            SetOrderStatus($"Enter a valid {SelectedQuoteAsset} amount.", positive: false, negative: true);
            return;
        }

        if (!TryGetQuoteAssetUsdPrice(SelectedQuoteAsset, out var quoteUsdRate))
        {
            SetOrderStatus($"No live conversion rate for {SelectedQuoteAsset}.", positive: false, negative: true);
            return;
        }

        if (_snapshot.LastPrice <= 0m)
        {
            SetOrderStatus($"Waiting for {PairTicker} price feed.", positive: false, negative: true);
            return;
        }

        var usdNotional = decimal.Round(quoteAmount * quoteUsdRate, 4);
        var baseQuantity = decimal.Round(usdNotional / _snapshot.LastPrice, 6);
        if (baseQuantity <= 0m)
        {
            SetOrderStatus("Order amount is too small for the current market price.", positive: false, negative: true);
            return;
        }

        var symbol = _snapshot.SelectedInstrument.Symbol;
        var position = GetOrCreatePosition(symbol);
        if (OrderSide == MarketOrderSide.Sell && position.Quantity < baseQuantity)
        {
            SetOrderStatus($"Insufficient {PairTicker} balance for this sell order.", positive: false, negative: true);
            return;
        }

        if (OrderSide == MarketOrderSide.Buy)
        {
            position.Quantity += baseQuantity;
            position.CostBasisUsd += usdNotional;
            position.BoughtUsd += usdNotional;
        }
        else
        {
            var sellRatio = position.Quantity <= 0m ? 0m : baseQuantity / position.Quantity;
            var realizedCost = decimal.Round(position.CostBasisUsd * sellRatio, 4);
            position.Quantity = Math.Max(0m, position.Quantity - baseQuantity);
            position.CostBasisUsd = Math.Max(0m, position.CostBasisUsd - realizedCost);
            position.SoldUsd += usdNotional;
            position.RealizedPnlUsd += usdNotional - realizedCost;
        }

        var trade = new TradeHistoryItem(
            "now",
            OrderSide == MarketOrderSide.Buy ? "Buy" : "Sell",
            OrderSide == MarketOrderSide.Buy ? baseQuantity : -baseQuantity,
            PairTicker,
            quoteAmount,
            SelectedQuoteAsset,
            _snapshot.LastPrice,
            usdNotional,
            "Demo Wallet",
            $"MY-{++_localOrderSequence}",
            isLocalOrder: true,
            makerBadgeText: "YOU");

        _localOrders.Insert(0, new LocalOrderRecord(symbol, trade));
        ApplyTradeHistoryFilter();
        RaisePortfolioProperties();
        UpdateSupplementalPanels();
        SetOrderStatus(
            $"{(OrderSide == MarketOrderSide.Buy ? "Bought" : "Sold")} {FormatAssetQuantity(baseQuantity)} {PairTicker} using {quoteAmount:N4} {SelectedQuoteAsset}.",
            positive: OrderSide == MarketOrderSide.Buy,
            negative: OrderSide == MarketOrderSide.Sell);
    }

    private bool TryParseOrderAmount(out decimal amount)
    {
        var text = OrderAmountText?.Trim();
        if (!string.IsNullOrWhiteSpace(text) &&
            decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(text) &&
            decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out amount))
        {
            return true;
        }

        amount = 0m;
        return false;
    }

    private bool TryGetQuoteAssetUsdPrice(string asset, out decimal usdPrice)
    {
        if (string.IsNullOrWhiteSpace(asset))
        {
            usdPrice = 0m;
            return false;
        }

        if (string.Equals(asset, "USD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asset, "USDT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asset, "USDC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asset, "FDUSD", StringComparison.OrdinalIgnoreCase))
        {
            usdPrice = 1m;
            return true;
        }

        if (string.Equals(asset, _snapshot.SelectedInstrument.BaseAsset, StringComparison.OrdinalIgnoreCase))
        {
            usdPrice = _snapshot.LastPrice;
            return true;
        }

        for (var i = 0; i < _snapshot.Watchlist.Count; i++)
        {
            var item = _snapshot.Watchlist[i];
            if (string.Equals(item.Instrument.BaseAsset, asset, StringComparison.OrdinalIgnoreCase))
            {
                usdPrice = item.LastPrice;
                return true;
            }
        }

        usdPrice = 0m;
        return false;
    }

    private PortfolioPosition GetOrCreatePosition(string symbol)
    {
        if (!_positions.TryGetValue(symbol, out var position))
        {
            position = new PortfolioPosition();
            _positions[symbol] = position;
        }

        return position;
    }

    private PortfolioPosition? GetCurrentPosition()
    {
        return _positions.TryGetValue(_snapshot.SelectedInstrument.Symbol, out var position)
            ? position
            : null;
    }

    private decimal CalculateTotalPnl(PortfolioPosition position)
    {
        var unrealizedPnl = (_snapshot.LastPrice * position.Quantity) - position.CostBasisUsd;
        return position.RealizedPnlUsd + unrealizedPnl;
    }

    private void SetOrderStatus(string text, bool positive, bool negative)
    {
        _orderStatusText = text;
        _orderStatusPositive = positive;
        _orderStatusNegative = negative;
        this.RaisePropertyChanged(nameof(OrderStatusText));
        this.RaisePropertyChanged(nameof(OrderStatusPositive));
        this.RaisePropertyChanged(nameof(OrderStatusNegative));
        this.RaisePropertyChanged(nameof(OrderStatusNeutral));
    }

    private void OnSnapshotChanged(MarketDashboardDataSnapshot snapshot)
    {
        ReactiveUI.RxSchedulers.MainThreadScheduler.Schedule(
            snapshot,
            (_, state) =>
            {
                ApplySnapshot(state, resetWindow: false);
                return System.Reactive.Disposables.Disposable.Empty;
            });
    }

    private void OnPriceChartInteractionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartInteractionState.CrosshairCategoryIndex) ||
            e.PropertyName == nameof(ChartInteractionState.CrosshairCategoryLabel) ||
            e.PropertyName == nameof(ChartInteractionState.CrosshairValue) ||
            e.PropertyName == nameof(ChartInteractionState.IsCrosshairVisible) ||
            e.PropertyName == nameof(ChartInteractionState.CrosshairHorizontalRatio))
        {
            UpdateHoveredPriceSummary();
            SyncCompanionCrosshair();
        }

        if (e.PropertyName == nameof(ChartInteractionState.FollowLatest))
        {
            this.RaisePropertyChanged(nameof(FollowLatestButtonText));
        }

        if (e.PropertyName == nameof(ChartInteractionState.PointerTool))
        {
            SyncCompanionPointerTools();
            RaisePointerToolProperties();
        }
    }

    private void ApplySnapshot(MarketDashboardDataSnapshot snapshot, bool resetWindow)
    {
        _snapshot = snapshot;

        _allWatchlistItems.Clear();
        var watchlistItems = BuildWatchlistItems(snapshot.Watchlist);
        for (var i = 0; i < watchlistItems.Count; i++)
        {
            _allWatchlistItems.Add(watchlistItems[i]);
        }

        _marketTradeHistoryItems.Clear();
        var tradeItems = BuildTradeHistoryItems(snapshot);
        for (var i = 0; i < tradeItems.Count; i++)
        {
            _marketTradeHistoryItems.Add(tradeItems[i]);
        }

        ApplyWatchlistFilter(snapshot.SelectedInstrument.Symbol);
        ApplyTradeHistoryFilter();

        var candleCountBefore = _candles.Count;
        ReplaceCollection(_candles, snapshot.Candles);
        ReplaceCollection(_flowPoints, BuildFlowPoints(snapshot));

        _priceSource.SeriesName = $"{snapshot.SelectedInstrument.BaseAsset} / {snapshot.SelectedInstrument.QuoteAsset}";
        _priceSource.ShowIndicators = _showIndicators;
        _traderSource.SeriesName = "Trades / Candle";
        _flowSource.SeriesName = _flowLens switch
        {
            MarketFlowLens.BuyVolume => "Buy Flow",
            MarketFlowLens.SellVolume => "Sell Flow",
            _ => "Net Flow"
        };

        ApplyChartValueFormats(snapshot);
        PriceChartStyle = CreatePriceChartStyle(_candles);
        VolumeChartStyle = CreateVolumeChartStyle(_candles);
        TraderChartStyle = CreateTraderChartStyle(_candles);
        FlowChartStyle = CreateFlowChartStyle(_flowPoints);

        UpdateWindowRequest(candleCountBefore, resetWindow);

        _priceSource.Invalidate();
        _volumeSource.Invalidate();
        _traderSource.Invalidate();
        _flowSource.Invalidate();

        PriceChart.Refresh();
        VolumeChart.Refresh();
        TraderChart.Refresh();
        FlowChart.Refresh();

        UpdateHoveredPriceSummary();
        UpdateSupplementalPanels();
        RaiseDashboardProperties();
        this.RaisePropertyChanged(nameof(OrderPreviewText));
        this.RaisePropertyChanged(nameof(OrderTicketTitleText));
    }

    private void ApplyWatchlistFilter(string? preferredSymbol = null)
    {
        var filter = WatchlistFilterText.Trim();
        var selectedSymbol = preferredSymbol ?? _snapshot.SelectedInstrument.Symbol;
        var filtered = new List<WatchlistItem>(_allWatchlistItems.Count);

        for (var i = 0; i < _allWatchlistItems.Count; i++)
        {
            var item = _allWatchlistItems[i];
            if (!MatchesWatchlistFilter(item, filter) || !MatchesWatchlistScope(item))
            {
                continue;
            }

            if (_watchlistMode == MarketWatchlistViewMode.Watchlist && !item.IsFavorite)
            {
                continue;
            }

            filtered.Add(item);
        }

        SortWatchlist(filtered);
        ReplaceCollection(Watchlist, filtered);

        WatchlistItem? selectedItem = null;
        for (var i = 0; i < Watchlist.Count; i++)
        {
            var item = Watchlist[i];
            if (!string.Equals(item.MarketSymbol, selectedSymbol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            selectedItem = item;
            break;
        }

        _isUpdatingSelectedWatchlistItem = true;
        try
        {
            SelectedWatchlistItem = selectedItem;
        }
        finally
        {
            _isUpdatingSelectedWatchlistItem = false;
        }
    }

    private static bool MatchesWatchlistFilter(WatchlistItem item, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return item.Symbol.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.Badges.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.MarketSymbol.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTradeHistoryFilter()
    {
        var items = new List<TradeHistoryItem>();
        for (var i = 0; i < _localOrders.Count; i++)
        {
            var localOrder = _localOrders[i];
            if (string.Equals(localOrder.MarketSymbol, _snapshot.SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(localOrder.Trade);
            }
        }

        if (_tradeHistoryView == MarketTradeHistoryView.All)
        {
            for (var i = 0; i < _marketTradeHistoryItems.Count; i++)
            {
                items.Add(_marketTradeHistoryItems[i]);
            }
        }

        ReplaceCollection(TradeHistory, items);
    }

    private void UpdateWindowRequest(int previousCandleCount, bool resetWindow)
    {
        var newCount = _candles.Count;
        if (newCount <= 0)
        {
            return;
        }

        if (resetWindow || previousCandleCount == 0)
        {
            PriceChart.ShowLatest(ResolvePreferredLatestWindowCount(newCount), true);
            _previousCandleCount = newCount;
            return;
        }

        if (PriceChart.Interaction.FollowLatest)
        {
            PriceChart.ShowLatest(ResolvePreferredLatestWindowCount(newCount, PriceChart.Request.WindowCount), true);
            _previousCandleCount = newCount;
            return;
        }

        if (PriceChart.Request.WindowStart is null && PriceChart.Request.WindowCount is null)
        {
            _previousCandleCount = newCount;
            return;
        }

        if (PriceChart.TryGetVisibleWindow(out _, out var currentStart, out var currentWindowCount))
        {
            var boundedWindowCount = Math.Min(currentWindowCount, newCount);
            var boundedStart = Math.Clamp(currentStart, 0, Math.Max(0, newCount - boundedWindowCount));
            PriceChart.SetVisibleWindow(boundedStart, boundedWindowCount, false);
        }
        else
        {
            PriceChart.ShowLatest(ResolvePreferredLatestWindowCount(newCount, PriceChart.Request.WindowCount), false);
        }

        _previousCandleCount = newCount;
    }

    private static int ResolvePreferredLatestWindowCount(int totalCount, int? explicitWindowCount = null)
    {
        if (totalCount <= 0)
        {
            return 1;
        }

        var preferredCount = explicitWindowCount ?? Math.Min(60, totalCount);
        return Math.Max(1, Math.Min(preferredCount, totalCount));
    }

    private void OnPriceChartRequestChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartDataRequest.WindowStart))
        {
            VolumeChart.Request.WindowStart = PriceChart.Request.WindowStart;
            TraderChart.Request.WindowStart = PriceChart.Request.WindowStart;
        }
        else if (e.PropertyName == nameof(ChartDataRequest.WindowCount))
        {
            VolumeChart.Request.WindowCount = PriceChart.Request.WindowCount;
            TraderChart.Request.WindowCount = PriceChart.Request.WindowCount;
        }

        UpdateHoveredPriceSummary();
        this.RaisePropertyChanged(nameof(VolumePaneValueText));
        this.RaisePropertyChanged(nameof(TraderPaneSummaryText));
    }

    private void RaiseDashboardProperties()
    {
        this.RaisePropertyChanged(nameof(DashboardDateText));
        this.RaisePropertyChanged(nameof(PairTicker));
        this.RaisePropertyChanged(nameof(PairName));
        this.RaisePropertyChanged(nameof(PairAddressText));
        this.RaisePropertyChanged(nameof(PairMetaText));
        this.RaisePropertyChanged(nameof(CurrentPriceText));
        this.RaisePropertyChanged(nameof(PriceChangeText));
        this.RaisePropertyChanged(nameof(PriceChangePositive));
        this.RaisePropertyChanged(nameof(PriceChangeNegative));
        this.RaisePropertyChanged(nameof(SecurityHeaderText));
        this.RaisePropertyChanged(nameof(SecurityStatusText));
        this.RaisePropertyChanged(nameof(BuyTaxText));
        this.RaisePropertyChanged(nameof(SellTaxText));
        this.RaisePropertyChanged(nameof(SocialMentions));
        this.RaisePropertyChanged(nameof(MarketCapText));
        this.RaisePropertyChanged(nameof(FullyDilutedValueText));
        this.RaisePropertyChanged(nameof(LiquidityText));
        this.RaisePropertyChanged(nameof(TwentyFourHourVolumeText));
        this.RaisePropertyChanged(nameof(HoneypotStatusText));
        this.RaisePropertyChanged(nameof(VerifiedStatusText));
        this.RaisePropertyChanged(nameof(RugPullStatusText));
        this.RaisePropertyChanged(nameof(FakeTokenStatusText));
        this.RaisePropertyChanged(nameof(UniqueTraderText));
        this.RaisePropertyChanged(nameof(BuyerCountText));
        this.RaisePropertyChanged(nameof(SellerCountText));
        this.RaisePropertyChanged(nameof(NetFlowText));
        this.RaisePropertyChanged(nameof(VolumeBreakdownText));
        this.RaisePropertyChanged(nameof(VolumePaneValueText));
        this.RaisePropertyChanged(nameof(TraderPaneSummaryText));
        this.RaisePropertyChanged(nameof(HoveredPriceSummaryText));
        this.RaisePropertyChanged(nameof(FollowLatestButtonText));
        this.RaisePropertyChanged(nameof(CanUndoChartWindow));
        this.RaisePropertyChanged(nameof(CanRedoChartWindow));
        this.RaisePropertyChanged(nameof(SelectedWatchlistScope));
        this.RaisePropertyChanged(nameof(AreIndicatorsVisible));
        this.RaisePropertyChanged(nameof(IndicatorsButtonText));
        this.RaisePropertyChanged(nameof(IsLabelGuideVisible));
        this.RaisePropertyChanged(nameof(LabelGuideText));
        this.RaisePropertyChanged(nameof(ActiveSectionTitleText));
        this.RaisePropertyChanged(nameof(ActiveSectionDescriptionText));
        RaiseTerminalSectionProperties();
        RaiseWatchlistRangeProperties();
        RaiseFlowLensProperties();
        RaisePointerToolProperties();
        RaiseChartIntervalProperties();
        RaiseModeDependentProperties();
        RaisePortfolioProperties();
    }

    private void UpdateHoveredPriceSummary()
    {
        if (!TryGetActiveCandle(out var candle, out _, out var label))
        {
            _hoveredPriceSummaryText = $"Live {PairTicker}: {CurrentPriceText} ({PriceChangeText})";
            this.RaisePropertyChanged(nameof(HoveredPriceSummaryText));
            return;
        }

        var windowSummary = BuildVisibleWindowSummary();
        if (!PriceChart.Interaction.IsCrosshairVisible || !PriceChart.Interaction.CrosshairCategoryIndex.HasValue)
        {
            _hoveredPriceSummaryText = $"Live {PairTicker}: {CurrentPriceText} ({PriceChangeText})";
            if (!string.IsNullOrWhiteSpace(windowSummary))
            {
                _hoveredPriceSummaryText += $" | {windowSummary}";
            }

            this.RaisePropertyChanged(nameof(HoveredPriceSummaryText));
            return;
        }

        _hoveredPriceSummaryText =
            $"{label}  O {FormatChartPrice(candle.Open)}  H {FormatChartPrice(candle.High)}  L {FormatChartPrice(candle.Low)}  C {FormatChartPrice(candle.Close)}";
        if (!string.IsNullOrWhiteSpace(windowSummary))
        {
            _hoveredPriceSummaryText += $" | {windowSummary}";
        }

        this.RaisePropertyChanged(nameof(HoveredPriceSummaryText));
    }

    private void SyncCompanionCrosshair()
    {
        if (!PriceChart.Interaction.IsCrosshairVisible || !PriceChart.Interaction.CrosshairCategoryIndex.HasValue)
        {
            VolumeChart.ClearCrosshair();
            TraderChart.ClearCrosshair();
            return;
        }

        var categoryIndex = PriceChart.Interaction.CrosshairCategoryIndex;
        var categoryLabel = PriceChart.Interaction.CrosshairCategoryLabel;
        var horizontalRatio = PriceChart.Interaction.CrosshairHorizontalRatio;

        VolumeChart.TrackCrosshair(categoryIndex, categoryLabel, null, horizontalRatio, 0.5d);
        TraderChart.TrackCrosshair(categoryIndex, categoryLabel, null, horizontalRatio, 0.5d);
    }

    private void SyncCompanionPointerTools()
    {
        var pointerTool = PriceChart.Interaction.PointerTool;
        VolumeChart.Interaction.PointerTool = pointerTool;
        TraderChart.Interaction.PointerTool = pointerTool;
    }

    private void RaisePortfolioProperties()
    {
        this.RaisePropertyChanged(nameof(BoughtSummaryText));
        this.RaisePropertyChanged(nameof(SoldSummaryText));
        this.RaisePropertyChanged(nameof(BalanceSummaryText));
        this.RaisePropertyChanged(nameof(TpnlSummaryText));
        this.RaisePropertyChanged(nameof(WalletStatusText));
    }

    private IReadOnlyList<WatchlistItem> BuildWatchlistItems(IReadOnlyList<MarketWatchlistQuote> quotes)
    {
        var items = new List<WatchlistItem>(quotes.Count);
        for (var i = 0; i < quotes.Count; i++)
        {
            var quote = quotes[i];
            items.Add(new WatchlistItem(
                quote.Instrument.Symbol,
                quote.Instrument.BaseAsset,
                quote.Instrument.DisplayName,
                BuildIconText(quote.Instrument.BaseAsset),
                BuildIconBackground(quote.Instrument.Symbol),
                quote.LastPrice,
                quote.ChangePercent,
                quote.QuoteVolume24h,
                $"24h {FormatCompactDollar(quote.QuoteVolume24h)}",
                quote.Instrument.QuoteAsset,
                _favoriteSymbols.Contains(quote.Instrument.Symbol)));
        }

        return items;
    }

    private bool TryGetActiveCandle(out MarketCandle candle, out int absoluteIndex, out string label)
    {
        candle = null!;
        absoluteIndex = -1;
        label = string.Empty;

        if (_candles.Count == 0)
        {
            return false;
        }

        if (!PriceChart.TryGetVisibleWindow(out _, out var windowStart, out var windowCount))
        {
            absoluteIndex = _candles.Count - 1;
        }
        else if (PriceChart.Interaction.IsCrosshairVisible && PriceChart.Interaction.CrosshairCategoryIndex.HasValue)
        {
            var visibleIndex = Math.Clamp(PriceChart.Interaction.CrosshairCategoryIndex.Value, 0, Math.Max(0, windowCount - 1));
            absoluteIndex = Math.Clamp(windowStart + visibleIndex, 0, _candles.Count - 1);
        }
        else
        {
            absoluteIndex = Math.Clamp(windowStart + Math.Max(0, windowCount - 1), 0, _candles.Count - 1);
        }

        candle = _candles[absoluteIndex];
        label = PriceChart.Interaction.CrosshairCategoryLabel ?? candle.Timestamp.ToString("HH:mm", CultureInfo.CurrentCulture);
        return true;
    }

    private string BuildVisibleWindowSummary()
    {
        if (_candles.Count == 0 || !PriceChart.TryGetVisibleWindow(out _, out var windowStart, out var windowCount))
        {
            return string.Empty;
        }

        var firstIndex = Math.Clamp(windowStart, 0, _candles.Count - 1);
        var lastIndex = Math.Clamp(windowStart + Math.Max(0, windowCount - 1), 0, _candles.Count - 1);
        var first = _candles[firstIndex].Timestamp;
        var last = _candles[lastIndex].Timestamp;
        return $"{first:HH:mm} - {last:HH:mm} | {windowCount} bars";
    }

    private IReadOnlyList<TradeHistoryItem> BuildTradeHistoryItems(MarketDashboardDataSnapshot snapshot)
    {
        var items = new List<TradeHistoryItem>(snapshot.RecentTrades.Count);
        for (var i = 0; i < snapshot.RecentTrades.Count; i++)
        {
            var trade = snapshot.RecentTrades[i];
            var signedQuantity = trade.IsBuy ? trade.Quantity : -trade.Quantity;
            items.Add(new TradeHistoryItem(
                FormatRelativeTime(trade.Timestamp),
                trade.IsBuy ? "Buy" : "Sell",
                signedQuantity,
                snapshot.SelectedInstrument.BaseAsset,
                trade.QuoteQuantity,
                snapshot.SelectedInstrument.QuoteAsset,
                trade.Price,
                trade.QuoteQuantity,
                trade.IsBuy ? "Agg. Buy" : "Agg. Sell",
                trade.TradeId.ToString(CultureInfo.InvariantCulture)));
        }

        return items;
    }

    private static SkiaChartStyle CreatePriceChartStyle(IReadOnlyList<MarketCandle> candles)
    {
        return new SkiaChartStyle
        {
            Background = SKColors.Transparent,
            Axis = new SKColor(72, 82, 110),
            Text = new SKColor(173, 184, 211),
            Gridline = new SKColor(26, 34, 52),
            ShowLegend = false,
            ShowAxisLabels = true,
            ShowCategoryLabels = true,
            ShowGridlines = true,
            ShowCategoryGridlines = true,
            AxisTickCount = 7,
            PaddingLeft = 8,
            PaddingRight = 22,
            PaddingTop = 10,
            PaddingBottom = 10,
            FinancialIncreaseColor = new SKColor(30, 212, 171),
            FinancialDecreaseColor = new SKColor(255, 89, 111),
            FinancialBodyFillOpacity = 0.30f,
            FinancialBodyWidthRatio = 0.58f,
            FinancialTickWidthRatio = 0.22f,
            FinancialWickStrokeWidth = 1.15f,
            FinancialBodyStrokeWidth = 1f,
            FinancialHollowBullishBodies = true,
            FinancialShowLastPriceLine = true,
            FinancialLastPriceLineColor = SKColors.Transparent,
            FinancialLastPriceLineWidth = 1.15f,
            FinancialLastPriceLabelText = new SKColor(8, 17, 26)
        };
    }

    private static SkiaChartStyle CreateVolumeChartStyle(IReadOnlyList<MarketCandle> candles)
    {
        return new SkiaChartStyle
        {
            Background = SKColors.Transparent,
            Axis = new SKColor(44, 54, 78),
            Text = new SKColor(112, 125, 155),
            Gridline = new SKColor(20, 28, 43),
            ShowLegend = false,
            ShowAxisLabels = false,
            ShowCategoryLabels = false,
            ShowGridlines = false,
            ShowCategoryGridlines = false,
            PaddingLeft = 8,
            PaddingRight = 18,
            PaddingTop = 2,
            PaddingBottom = 2,
            ValueAxisMinimum = 0,
            SeriesStyles = new[]
            {
                new SkiaChartSeriesStyle
                {
                    FillColor = new SKColor(39, 203, 160, 140),
                    StrokeColor = new SKColor(39, 203, 160, 196),
                    StrokeWidth = 0f
                },
                new SkiaChartSeriesStyle
                {
                    FillColor = new SKColor(255, 85, 106, 140),
                    StrokeColor = new SKColor(255, 85, 106, 196),
                    StrokeWidth = 0f
                }
            }
        };
    }

    private static SkiaChartStyle CreateTraderChartStyle(IReadOnlyList<MarketCandle> candles)
    {
        return new SkiaChartStyle
        {
            Background = SKColors.Transparent,
            Axis = new SKColor(44, 54, 78),
            Text = new SKColor(112, 125, 155),
            Gridline = new SKColor(20, 28, 43),
            ShowLegend = false,
            ShowAxisLabels = false,
            ShowCategoryLabels = false,
            ShowGridlines = false,
            ShowCategoryGridlines = false,
            PaddingLeft = 8,
            PaddingRight = 18,
            PaddingTop = 1,
            PaddingBottom = 2,
            ValueAxisMinimum = 0,
            SeriesStyles = new[]
            {
                new SkiaChartSeriesStyle
                {
                    FillColor = new SKColor(81, 118, 255, 170),
                    StrokeColor = new SKColor(81, 118, 255, 210),
                    StrokeWidth = 0f
                }
            }
        };
    }

    private static SkiaChartStyle CreateFlowChartStyle(IReadOnlyList<SparklinePoint> points)
    {
        var minValue = 0d;
        var maxValue = 1d;
        if (points.Count > 0)
        {
            minValue = double.MaxValue;
            maxValue = double.MinValue;
            for (var i = 0; i < points.Count; i++)
            {
                minValue = Math.Min(minValue, points[i].Value);
                maxValue = Math.Max(maxValue, points[i].Value);
            }

            if (Math.Abs(maxValue - minValue) < double.Epsilon)
            {
                maxValue = minValue + 1d;
            }
        }

        var padding = Math.Max(1d, (maxValue - minValue) * 0.12d);
        return new SkiaChartStyle
        {
            Background = SKColors.Transparent,
            Axis = new SKColor(58, 71, 101),
            Text = new SKColor(121, 138, 173),
            Gridline = new SKColor(26, 36, 58),
            ShowLegend = false,
            ShowAxisLabels = false,
            ShowCategoryLabels = false,
            ShowGridlines = false,
            ShowCategoryGridlines = false,
            PaddingLeft = 6,
            PaddingRight = 6,
            PaddingTop = 4,
            PaddingBottom = 6,
            ValueAxisMinimum = minValue - padding,
            ValueAxisMaximum = maxValue + padding,
            AreaFillOpacity = 0.22f,
            SeriesStyles = new[]
            {
                new SkiaChartSeriesStyle
                {
                    StrokeColor = new SKColor(18, 241, 175),
                    FillColor = new SKColor(18, 241, 175, 138),
                    StrokeWidth = 2.1f,
                    LineInterpolation = SkiaLineInterpolation.Smooth
                }
            }
        };
    }

    private string FormatPriceToolTip(SkiaChartHitTestResult hit)
    {
        if ((hit.SeriesKind == ChartSeriesKind.Candlestick ||
             hit.SeriesKind == ChartSeriesKind.HollowCandlestick ||
             hit.SeriesKind == ChartSeriesKind.Ohlc ||
             hit.SeriesKind == ChartSeriesKind.HeikinAshi) &&
            hit.OpenValue.HasValue &&
            hit.HighValue.HasValue &&
            hit.LowValue.HasValue &&
            hit.CloseValue.HasValue)
        {
            return $"{hit.Category}: O {FormatChartPrice(hit.OpenValue.Value)}, H {FormatChartPrice(hit.HighValue.Value)}, L {FormatChartPrice(hit.LowValue.Value)}, C {FormatChartPrice(hit.CloseValue.Value)}";
        }

        if (hit.SeriesKind == ChartSeriesKind.Hlc &&
            hit.HighValue.HasValue &&
            hit.LowValue.HasValue &&
            hit.CloseValue.HasValue)
        {
            return $"{hit.Category}: H {FormatChartPrice(hit.HighValue.Value)}, L {FormatChartPrice(hit.LowValue.Value)}, C {FormatChartPrice(hit.CloseValue.Value)}";
        }

        return $"{hit.Category}: {FormatChartPrice(hit.Value)}";
    }

    private string FormatFlowToolTip(SkiaChartHitTestResult hit)
    {
        return $"{hit.Category}: {FormatFlowValue(hit.Value)}";
    }

    private string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var delta = DateTimeOffset.UtcNow - timestamp;
        if (delta.TotalMinutes < 1d)
        {
            return "now";
        }

        if (delta.TotalHours < 1d)
        {
            var minutes = Math.Max(1, (int)Math.Floor(delta.TotalMinutes));
            return $"{minutes}m ago";
        }

        var hours = Math.Max(1, (int)Math.Floor(delta.TotalHours));
        return $"{hours}h ago";
    }

    private string FormatPrice(decimal value)
    {
        return value >= 1m
            ? $"${value:N2}"
            : $"${value:N6}";
    }

    private string FormatChartPrice(double value)
    {
        return ChartValueFormatter.Format(value, PriceChart.ValueAxis.ValueFormat, _culture);
    }

    private string FormatFlowValue(double value)
    {
        var prefix = value >= 0d ? "+$" : "-$";
        return prefix + ChartValueFormatter.Format(Math.Abs(value), FlowChart.ValueAxis.ValueFormat, _culture);
    }

    private string FormatCompactDollar(decimal value)
    {
        return $"${FormatCompactNumber(value)}";
    }

    private string FormatSignedDollar(decimal value)
    {
        return $"{(value >= 0m ? "+" : "-")}${FormatCompactNumber(decimal.Abs(value))}";
    }

    private string FormatCompactNumber(decimal value)
    {
        var absolute = decimal.Abs(value);
        if (absolute >= 1_000_000_000m)
        {
            return $"{value / 1_000_000_000m:N2}B";
        }

        if (absolute >= 1_000_000m)
        {
            return $"{value / 1_000_000m:N2}M";
        }

        if (absolute >= 1_000m)
        {
            return $"{value / 1_000m:N2}K";
        }

        return value.ToString("N2", _culture);
    }

    private static string FormatAssetQuantity(decimal value)
    {
        return value >= 1m ? value.ToString("N4", CultureInfo.InvariantCulture) : value.ToString("N6", CultureInfo.InvariantCulture);
    }

    private static string BuildIconText(string symbol)
    {
        return string.IsNullOrWhiteSpace(symbol)
            ? "?"
            : symbol.Substring(0, 1).ToUpperInvariant();
    }

    private static string BuildIconBackground(string symbol)
    {
        var palette = new[]
        {
            "#4C88FF",
            "#F4C95D",
            "#30C67C",
            "#F56565",
            "#A78BFA",
            "#22D3EE",
            "#FB7185",
            "#F59E0B",
            "#60A5FA",
            "#34D399"
        };

        var hash = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(symbol));
        return palette[hash % palette.Length];
    }

    private void ApplyChartValueFormats(MarketDashboardDataSnapshot snapshot)
    {
        var priceMaximumFractionDigits = snapshot.LastPrice >= 1m ? 2 : 6;
        var priceFormat = new ChartValueFormat
        {
            MinimumFractionDigits = 0,
            MaximumFractionDigits = priceMaximumFractionDigits,
            RoundingMode = MidpointRounding.AwayFromZero,
            UseGrouping = false,
            Culture = _culture
        };

        PriceChart.ValueAxis.ValueFormat = priceFormat;
        PriceChart.SecondaryValueAxis.ValueFormat = priceFormat;
        VolumeChart.ValueAxis.ValueFormat = new ChartValueFormat
        {
            MinimumFractionDigits = 0,
            MaximumFractionDigits = 2,
            RoundingMode = MidpointRounding.AwayFromZero,
            UseGrouping = true,
            Culture = _culture
        };
        TraderChart.ValueAxis.ValueFormat = new ChartValueFormat
        {
            MinimumFractionDigits = 0,
            MaximumFractionDigits = 0,
            RoundingMode = MidpointRounding.AwayFromZero,
            UseGrouping = true,
            Culture = _culture
        };
        FlowChart.ValueAxis.ValueFormat = new ChartValueFormat
        {
            MinimumFractionDigits = 0,
            MaximumFractionDigits = 2,
            RoundingMode = MidpointRounding.AwayFromZero,
            UseGrouping = true,
            Culture = _culture
        };
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            target.Add(source[i]);
        }
    }

    private static ChartSeriesKind MapChartMode(MarketChartMode mode)
    {
        return mode switch
        {
            MarketChartMode.Candlestick => ChartSeriesKind.Candlestick,
            MarketChartMode.HollowCandlestick => ChartSeriesKind.HollowCandlestick,
            MarketChartMode.HeikinAshi => ChartSeriesKind.HeikinAshi,
            MarketChartMode.Ohlc => ChartSeriesKind.Ohlc,
            MarketChartMode.Hlc => ChartSeriesKind.Hlc,
            _ => ChartSeriesKind.Candlestick
        };
    }
}
