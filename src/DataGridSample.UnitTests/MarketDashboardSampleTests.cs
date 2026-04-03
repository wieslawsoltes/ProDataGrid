using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using ProDataGrid.MarketDashboardSample.Models;
using ProDataGrid.MarketDashboardSample.Services;
using ProDataGrid.MarketDashboardSample.ViewModels;
using SkiaSharp;
using Xunit;
using MarketDashboardWindow = ProDataGrid.MarketDashboardSample.MainWindow;

namespace DataGridSample.Tests;

public sealed class MarketDashboardSampleTests
{
    [Fact]
    public void ViewModel_Uses_Financial_Series_And_Syncs_Companion_Charts()
    {
        var viewModel = new MarketDashboardViewModel();

        var candleSnapshot = viewModel.PriceChart.DataSource!.BuildSnapshot(viewModel.PriceChart.Request);
        var candleSeries = Assert.Single(candleSnapshot.Series);
        Assert.Equal(ChartSeriesKind.Candlestick, candleSeries.Kind);
        Assert.NotNull(candleSeries.OpenValues);
        Assert.NotNull(candleSeries.HighValues);
        Assert.NotNull(candleSeries.LowValues);

        viewModel.ChartMode = MarketChartMode.HollowCandlestick;

        var hollowSnapshot = viewModel.PriceChart.DataSource.BuildSnapshot(viewModel.PriceChart.Request);
        var hollowSeries = Assert.Single(hollowSnapshot.Series);
        Assert.Equal(ChartSeriesKind.HollowCandlestick, hollowSeries.Kind);
        Assert.NotNull(hollowSeries.OpenValues);

        viewModel.ChartMode = MarketChartMode.Ohlc;

        var ohlcSnapshot = viewModel.PriceChart.DataSource.BuildSnapshot(viewModel.PriceChart.Request);
        var ohlcSeries = Assert.Single(ohlcSnapshot.Series);
        Assert.Equal(ChartSeriesKind.Ohlc, ohlcSeries.Kind);

        viewModel.ChartMode = MarketChartMode.HeikinAshi;

        var heikinSnapshot = viewModel.PriceChart.DataSource.BuildSnapshot(viewModel.PriceChart.Request);
        var heikinSeries = Assert.Single(heikinSnapshot.Series);
        Assert.Equal(ChartSeriesKind.HeikinAshi, heikinSeries.Kind);
        Assert.NotNull(heikinSeries.OpenValues);

        viewModel.ChartMode = MarketChartMode.Hlc;

        var hlcSnapshot = viewModel.PriceChart.DataSource.BuildSnapshot(viewModel.PriceChart.Request);
        var hlcSeries = Assert.Single(hlcSnapshot.Series);
        Assert.Equal(ChartSeriesKind.Hlc, hlcSeries.Kind);
        Assert.Null(hlcSeries.OpenValues);

        var volumeSnapshot = viewModel.VolumeChart.DataSource!.BuildSnapshot(viewModel.VolumeChart.Request);
        Assert.Equal(2, volumeSnapshot.Series.Count);
        Assert.All(volumeSnapshot.Series, series => Assert.Equal(ChartSeriesKind.Column, series.Kind));

        var traderSnapshot = viewModel.TraderChart.DataSource!.BuildSnapshot(viewModel.TraderChart.Request);
        var traderSeries = Assert.Single(traderSnapshot.Series);
        Assert.Equal(ChartSeriesKind.Column, traderSeries.Kind);

        viewModel.PriceChart.SetVisibleWindow(4, 12, false);

        Assert.Equal(4, viewModel.VolumeChart.Request.WindowStart);
        Assert.Equal(12, viewModel.VolumeChart.Request.WindowCount);
        Assert.Equal(4, viewModel.TraderChart.Request.WindowStart);
        Assert.Equal(12, viewModel.TraderChart.Request.WindowCount);
    }

    [AvaloniaFact]
    public void MainWindow_Attaches_With_Expected_Dashboard_Composition()
    {
        var window = new MarketDashboardWindow
        {
            DataContext = new MarketDashboardViewModel()
        };
        window.ApplyMarketDashboardTheme();

        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<MarketDashboardViewModel>(window.DataContext);
            Assert.Equal(MarketChartMode.Candlestick, viewModel.ChartMode);
            Assert.NotNull(window.Content);
            var descendants = window.GetLogicalDescendants().OfType<StyledElement>().ToArray();
            var chartViews = descendants.OfType<ProChartView>().ToArray();
            Assert.True(descendants.OfType<DataGrid>().Count() >= 2);
            Assert.Contains(descendants, element => element.Name == "MarketTerminalFrame");
            var orderTicket = Assert.IsType<Border>(descendants.Single(element => element.Name == "OrderTicket"));
            Assert.False(orderTicket.IsVisible);
            Assert.Contains(descendants, element => element.Name == "RightSearchStrip");
            Assert.Contains(descendants, element => element is GridSplitter splitter && splitter.Name == "LeftPanelSplitter");
            Assert.Contains(descendants, element => element is GridSplitter splitter && splitter.Name == "RightPanelSplitter");
            Assert.Contains(descendants, element => element is GridSplitter splitter && splitter.Name == "ChartTradeSplitter");
            var volumeChartView = Assert.Single(chartViews, chart => chart.Name == "VolumeChartView");
            var traderChartView = Assert.Single(chartViews, chart => chart.Name == "TraderChartView");
            Assert.Equal(viewModel.PriceChart, volumeChartView.ViewportChartModel);
            Assert.Equal(viewModel.PriceChart, traderChartView.ViewportChartModel);
            Assert.False(volumeChartView.EnableHoverTracking);
            Assert.False(traderChartView.EnableHoverTracking);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MainWindow_Exposes_Tooltips_On_Primary_Terminal_Controls()
    {
        var window = new MarketDashboardWindow
        {
            DataContext = new MarketDashboardViewModel()
        };
        window.ApplyMarketDashboardTheme();

        try
        {
            window.Show();
            PumpLayout(window);

            AssertControlHasToolTip<Button>(window, "CrosshairToolButton");
            AssertControlHasToolTip<Button>(window, "PanToolButton");
            AssertControlHasToolTip<Button>(window, "ZoomToolButton");
            AssertControlHasToolTip<Button>(window, "MeasureToolButton");
            AssertControlHasToolTip<Button>(window, "OrderTicketToggleButton");
            AssertControlHasToolTip<Button>(window, "UndoChartWindowButton");
            AssertControlHasToolTip<Button>(window, "RedoChartWindowButton");
            AssertControlHasToolTip<Button>(window, "ResetChartWindowIconButton");
            AssertToolRailButtonUsesVectorIcon(window, "CrosshairToolButton");
            AssertToolRailButtonUsesVectorIcon(window, "PanToolButton");
            AssertToolRailButtonUsesVectorIcon(window, "ZoomToolButton");
            AssertToolRailButtonUsesVectorIcon(window, "MeasureToolButton");
            AssertVectorIconButton(window, "UndoChartWindowButton", minimumSize: 16d);
            AssertVectorIconButton(window, "RedoChartWindowButton", minimumSize: 16d);
            AssertVectorIconButton(window, "ResetChartWindowIconButton", minimumSize: 16d);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ViewModel_Hides_Order_Ticket_By_Default_And_Places_Simulated_Order()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);

        Assert.False(viewModel.IsOrderTicketVisible);
        viewModel.ToggleOrderTicketCommand.Execute().Subscribe(_ => { });
        Assert.True(viewModel.IsOrderTicketVisible);
        Assert.False(viewModel.IsWalletConnected);

        viewModel.PrimaryOrderActionCommand.Execute().Subscribe(_ => { });
        Assert.True(viewModel.IsWalletConnected);

        viewModel.OrderAmountText = "0.50";
        viewModel.PrimaryOrderActionCommand.Execute().Subscribe(_ => { });

        Assert.NotEqual("--", viewModel.BoughtSummaryText);
        Assert.Contains(viewModel.PairTicker, viewModel.BalanceSummaryText, StringComparison.Ordinal);
        Assert.True(viewModel.OrderStatusPositive);
        Assert.NotEmpty(viewModel.TradeHistory);
        Assert.True(viewModel.TradeHistory[0].IsLocalOrder);
        Assert.Equal("Buy", viewModel.TradeHistory[0].Side);
    }

    [Fact]
    public void ViewModel_Selecting_Watchlist_Item_Requests_Symbol_Switch()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);
        var target = viewModel.Watchlist.First(item => !string.Equals(item.MarketSymbol, service.CurrentSnapshot.SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase));

        viewModel.SelectedWatchlistItem = target;

        Assert.True(
            SpinWait.SpinUntil(
                () => string.Equals(service.LastSelectedSymbol, target.MarketSymbol, StringComparison.OrdinalIgnoreCase),
                TimeSpan.FromSeconds(1)));
        Assert.Equal(target.MarketSymbol, service.LastSelectedSymbol);
    }

    [Fact]
    public void ViewModel_Synchronizes_Companion_Crosshair_From_PriceChart()
    {
        var viewModel = new MarketDashboardViewModel(new StubMarketDashboardDataService());

        viewModel.PriceChart.TrackCrosshair(3, "15:45", 9.11d, 0.35d, 0.42d);

        Assert.True(viewModel.VolumeChart.Interaction.IsCrosshairVisible);
        Assert.True(viewModel.TraderChart.Interaction.IsCrosshairVisible);
        Assert.Equal(ChartCrosshairMode.VerticalOnly, viewModel.VolumeChart.Interaction.CrosshairMode);
        Assert.Equal(ChartCrosshairMode.VerticalOnly, viewModel.TraderChart.Interaction.CrosshairMode);
        Assert.Equal(3, viewModel.VolumeChart.Interaction.CrosshairCategoryIndex);
        Assert.Equal(3, viewModel.TraderChart.Interaction.CrosshairCategoryIndex);
        Assert.Equal(viewModel.PriceChart.Interaction.CrosshairHorizontalRatio, viewModel.VolumeChart.Interaction.CrosshairHorizontalRatio);
        Assert.Contains(" O ", viewModel.HoveredPriceSummaryText, StringComparison.Ordinal);

        viewModel.PriceChart.ClearCrosshair();

        Assert.False(viewModel.VolumeChart.Interaction.IsCrosshairVisible);
        Assert.False(viewModel.TraderChart.Interaction.IsCrosshairVisible);
    }

    [Fact]
    public void ViewModel_Wires_Intervals_Tools_And_Supplemental_Panels()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);

        viewModel.SelectTerminalSectionCommand.Execute(MarketTerminalSection.Signals).Subscribe(_ => { });
        viewModel.SelectPointerToolCommand.Execute(ChartPointerTool.Measure).Subscribe(_ => { });
        viewModel.SelectChartIntervalCommand.Execute("5m").Subscribe(_ => { });
        viewModel.SelectTradePanelCommand.Execute(MarketTradePanel.Liquidity).Subscribe(_ => { });
        viewModel.SelectFlowLensCommand.Execute(MarketFlowLens.BuyVolume).Subscribe(_ => { });

        Assert.True(SpinWait.SpinUntil(
            () => string.Equals(service.LastSelectedInterval, "5m", StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(1)));
        Assert.True(viewModel.AreIndicatorsVisible);
        Assert.True(viewModel.IsSignalsSectionSelected);
        Assert.Equal(ChartPointerTool.Measure, viewModel.PriceChart.Interaction.PointerTool);
        Assert.Equal(ChartPointerTool.Measure, viewModel.VolumeChart.Interaction.PointerTool);
        Assert.Equal(ChartPointerTool.Measure, viewModel.TraderChart.Interaction.PointerTool);
        Assert.True(viewModel.IsLiquidityPanelSelected);
        Assert.True(viewModel.IsBuyFlowLensSelected);
        Assert.NotEmpty(viewModel.LiquidityVenues);
        Assert.NotEmpty(viewModel.HolderBreakdown);
        Assert.True(service.LastCandleLimit > 0);
    }

    [Fact]
    public void ViewModel_Toggles_Favorites_And_Updates_Live_Pane_Readouts()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);
        var target = viewModel.Watchlist[1];

        viewModel.ToggleFavoriteWatchlistItemCommand.Execute(target).Subscribe(_ => { });
        viewModel.SelectWatchlistModeCommand.Execute(MarketWatchlistViewMode.Watchlist).Subscribe(_ => { });

        Assert.Single(viewModel.Watchlist);
        Assert.True(viewModel.Watchlist[0].IsFavorite);
        Assert.Equal(target.MarketSymbol, viewModel.Watchlist[0].MarketSymbol);

        viewModel.PriceChart.SetVisibleWindow(0, 10, false);

        Assert.NotEqual("--", viewModel.VolumePaneValueText);
        Assert.Contains("Unique Traders", viewModel.TraderPaneSummaryText, StringComparison.Ordinal);
        Assert.Contains("bars", viewModel.HoveredPriceSummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModel_Preserves_Manual_Window_On_Live_Snapshot_Update()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);

        viewModel.PriceChart.SetVisibleWindow(4, 12, false);
        service.PublishSnapshot(candleLimit: 220);

        Assert.True(SpinWait.SpinUntil(
            () => viewModel.PriceChart.Request.WindowStart == 4 && viewModel.PriceChart.Request.WindowCount == 12,
            TimeSpan.FromSeconds(1)));
        Assert.False(viewModel.PriceChart.Interaction.FollowLatest);
    }

    [Fact]
    public void ViewModel_FollowLatest_Preserves_Current_Zoom_Count_On_Live_Snapshot_Update()
    {
        var service = new StubMarketDashboardDataService();
        var viewModel = new MarketDashboardViewModel(service);

        viewModel.PriceChart.SetVisibleWindow(20, 12, false);
        viewModel.ToggleFollowLatestCommand.Execute().Subscribe(_ => { });

        var expectedWindowCount = viewModel.PriceChart.Request.WindowCount;
        service.PublishSnapshot(candleLimit: 220);

        Assert.True(SpinWait.SpinUntil(
            () => viewModel.PriceChart.Interaction.FollowLatest && viewModel.PriceChart.Request.WindowCount == expectedWindowCount,
            TimeSpan.FromSeconds(1)));
        Assert.Equal(expectedWindowCount, viewModel.PriceChart.Request.WindowCount);
    }

    [Fact]
    public void ViewModel_Uses_Configured_ValueFormat_For_Chart_Price_Rounding()
    {
        var viewModel = new MarketDashboardViewModel(new StubMarketDashboardDataService());
        var format = Assert.IsType<ChartValueFormat>(viewModel.PriceChart.ValueAxis.ValueFormat);

        Assert.Equal(2, format.MaximumFractionDigits);
        Assert.Equal(0, format.MinimumFractionDigits);

        var tooltip = viewModel.PriceToolTipFormatter(
            new SkiaChartHitTestResult(
                0,
                0,
                79.14999999999999d,
                null,
                "15:45",
                "Price",
                ChartSeriesKind.Candlestick,
                SKPoint.Empty,
                openValue: 79.14999999999999d,
                highValue: 79.18999999999998d,
                lowValue: 79.101d,
                closeValue: 79.145d));

        Assert.Contains("79.15", tooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("79.149999", tooltip, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void MainWindow_LeftMouseDrag_Pans_Shared_Time_Window_And_PriceScale()
    {
        var window = new MarketDashboardWindow
        {
            DataContext = new MarketDashboardViewModel(new StubMarketDashboardDataService())
        };
        window.ApplyMarketDashboardTheme();

        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<MarketDashboardViewModel>(window.DataContext);
            var descendants = window.GetVisualDescendants().OfType<Control>().ToArray();
            var priceChartView = Assert.IsType<ProChartView>(descendants.Single(control => control.Name == "PriceChartView"));
            var volumeChartView = Assert.IsType<ProChartView>(descendants.Single(control => control.Name == "VolumeChartView"));

            viewModel.PriceChart.SetVisibleWindow(18, 12, false);
            PumpLayout(window);

            var startWindow = viewModel.PriceChart.Request.WindowStart;
            Assert.Equal(18, startWindow);
            DragLeftToRight(priceChartView);
            PumpLayout(window);

            Assert.NotNull(viewModel.PriceChart.Request.WindowStart);
            Assert.True(viewModel.PriceChart.Request.WindowStart < startWindow);

            viewModel.PriceChart.SetVisibleWindow(18, 12, false);
            PumpLayout(window);

            startWindow = viewModel.PriceChart.Request.WindowStart;
            Assert.Equal(18, startWindow);
            DragLeftToRight(volumeChartView);
            PumpLayout(window);

            Assert.NotNull(viewModel.PriceChart.Request.WindowStart);
            Assert.True(viewModel.PriceChart.Request.WindowStart < startWindow);
            Assert.Equal(viewModel.PriceChart.Request.WindowStart, viewModel.VolumeChart.Request.WindowStart);
            Assert.Equal(viewModel.PriceChart.Request.WindowStart, viewModel.TraderChart.Request.WindowStart);

            var valueAxisMinimum = viewModel.PriceChart.ValueAxis.Minimum;
            var valueAxisMaximum = viewModel.PriceChart.ValueAxis.Maximum;

            DragBottomToTop(priceChartView);
            PumpLayout(window);

            Assert.True(viewModel.PriceChart.ValueAxis.Minimum.HasValue);
            Assert.True(viewModel.PriceChart.ValueAxis.Maximum.HasValue);
            Assert.NotEqual(valueAxisMinimum, viewModel.PriceChart.ValueAxis.Minimum);
            Assert.NotEqual(valueAxisMaximum, viewModel.PriceChart.ValueAxis.Maximum);
            Assert.True(viewModel.PriceChart.ValueAxis.Maximum.Value > viewModel.PriceChart.ValueAxis.Minimum.Value);
        }
        finally
        {
            window.Close();
        }
    }

    private static void PumpLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertControlHasToolTip<TControl>(Window window, string name)
        where TControl : Control
    {
        var control = Assert.IsType<TControl>(window.GetVisualDescendants().Single(descendant => descendant is TControl typed && typed.Name == name));
        var tip = ToolTip.GetTip(control) as string;
        Assert.False(string.IsNullOrWhiteSpace(tip));
    }

    private static void AssertToolRailButtonUsesVectorIcon(Window window, string name)
    {
        AssertVectorIconButton(window, name, minimumSize: 22d);
    }

    private static void AssertVectorIconButton(Window window, string name, double minimumSize)
    {
        var button = Assert.IsType<Button>(window.GetVisualDescendants().Single(descendant => descendant is Button typed && typed.Name == name));
        var viewbox = Assert.IsType<Viewbox>(button.Content);
        Assert.True(viewbox.Width >= minimumSize);
        Assert.True(viewbox.Height >= minimumSize);
        Assert.NotEqual(HorizontalAlignment.Stretch, button.HorizontalContentAlignment);
    }

    private static void DragLeftToRight(ProChartView chartView)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var start = new Point(chartView.Bounds.Width * 0.42d, chartView.Bounds.Height * 0.5d);
        var move = new Point(start.X + 96d, start.Y);

        chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, start));
        chartView.RaiseEvent(CreatePointerMovedArgs(chartView, chartView, pointer, move));
        chartView.RaiseEvent(CreatePointerReleasedArgs(chartView, chartView, pointer, move));
    }

    private static void DragBottomToTop(ProChartView chartView)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var start = new Point(chartView.Bounds.Width * 0.5d, chartView.Bounds.Height * 0.74d);
        var move = new Point(start.X, chartView.Bounds.Height * 0.34d);

        chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, start));
        chartView.RaiseEvent(CreatePointerMovedArgs(chartView, chartView, pointer, move));
        chartView.RaiseEvent(CreatePointerReleasedArgs(chartView, chartView, pointer, move));
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(
        Control source,
        Visual root,
        IPointer pointer,
        Point position,
        int clickCount = 1)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, clickCount);
    }

    private static PointerEventArgs CreatePointerMovedArgs(
        Control source,
        Visual root,
        IPointer pointer,
        Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, KeyModifiers.None);
    }

    private static PointerReleasedEventArgs CreatePointerReleasedArgs(
        Control source,
        Visual root,
        IPointer pointer,
        Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        return new PointerReleasedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, MouseButton.Left);
    }

    private sealed class StubMarketDashboardDataService : IMarketDashboardDataService
    {
        private readonly BinanceMarketDataOptions _options = new()
        {
            Instruments = new[]
            {
                new MarketInstrumentDefinition("LINKUSDT", "Chainlink", "LINK", "USDT"),
                new MarketInstrumentDefinition("ETHUSDT", "Ethereum", "ETH", "USDT")
            }
        };

        public StubMarketDashboardDataService()
        {
            CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(_options);
        }

        public event Action<MarketDashboardDataSnapshot>? SnapshotChanged;

        public IReadOnlyList<MarketInstrumentDefinition> Instruments => _options.Instruments;

        public MarketDashboardDataSnapshot CurrentSnapshot { get; private set; }

        public string? LastSelectedSymbol { get; private set; }

        public string? LastSelectedInterval { get; private set; }

        public int LastCandleLimit { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            SnapshotChanged?.Invoke(CurrentSnapshot);
            return Task.CompletedTask;
        }

        public Task SelectInstrumentAsync(string symbol, CancellationToken cancellationToken = default)
        {
            LastSelectedSymbol = symbol;
            MarketInstrumentDefinition? selectedInstrument = null;
            for (var i = 0; i < _options.Instruments.Count; i++)
            {
                var instrument = _options.Instruments[i];
                if (string.Equals(instrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                {
                    selectedInstrument = instrument;
                    break;
                }
            }

            if (selectedInstrument is not null)
            {
                CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(
                    _options,
                    selectedInstrument,
                    LastSelectedInterval ?? _options.KlineInterval,
                    LastCandleLimit > 0 ? LastCandleLimit : _options.CandleLimit);
                SnapshotChanged?.Invoke(CurrentSnapshot);
            }

            return Task.CompletedTask;
        }

        public Task SetChartIntervalAsync(string interval, int candleLimit, CancellationToken cancellationToken = default)
        {
            LastSelectedInterval = interval;
            LastCandleLimit = candleLimit;
            CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(_options, CurrentSnapshot.SelectedInstrument, interval, candleLimit);
            SnapshotChanged?.Invoke(CurrentSnapshot);
            return Task.CompletedTask;
        }

        public void PublishSnapshot(int candleLimit)
        {
            LastCandleLimit = candleLimit;
            CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(
                _options,
                CurrentSnapshot.SelectedInstrument,
                LastSelectedInterval ?? _options.KlineInterval,
                candleLimit);
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }

        public void Dispose()
        {
        }
    }
}
