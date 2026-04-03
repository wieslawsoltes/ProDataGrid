using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProDataGrid.MarketDashboardSample.Models;

namespace ProDataGrid.MarketDashboardSample.Services;

internal sealed class BinanceMarketDashboardDataService : IMarketDashboardDataService
{
    private const string RestBaseAddress = "https://api.binance.com";
    private const string StreamBaseAddress = "wss://stream.binance.com:9443/stream?streams=";
    private readonly HttpClient _httpClient;
    private readonly BinanceMarketDataOptions _options;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SemaphoreSlim _selectionGate = new(1, 1);
    private readonly Dictionary<string, TickerState> _watchlistStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MarketCandle> _candles = new();
    private readonly List<MarketTradeSnapshot> _recentTrades = new();
    private MarketInstrumentDefinition _selectedInstrument;
    private string _klineInterval;
    private int _candleLimit;
    private string _feedStatusText = "Connecting…";
    private TickerState _selectedTicker;
    private Task? _startupTask;
    private Task? _watchlistTask;
    private Task? _selectedTask;
    private CancellationTokenSource? _runtimeCts;
    private CancellationTokenSource? _selectedLoopCts;

    private sealed class TickerState
    {
        public TickerState(MarketInstrumentDefinition instrument)
        {
            Instrument = instrument;
        }

        public MarketInstrumentDefinition Instrument { get; }

        public decimal LastPrice { get; set; }

        public decimal ChangePercent { get; set; }

        public decimal OpenPrice { get; set; }

        public decimal HighPrice { get; set; }

        public decimal LowPrice { get; set; }

        public decimal BidPrice { get; set; }

        public decimal AskPrice { get; set; }

        public decimal QuoteVolume { get; set; }

        public decimal BaseVolume { get; set; }

        public int TradeCount { get; set; }

        public DateTimeOffset LastUpdatedUtc { get; set; }

        public MarketWatchlistQuote ToWatchlistQuote()
        {
            return new MarketWatchlistQuote(Instrument, LastPrice, ChangePercent, QuoteVolume);
        }
    }

    public BinanceMarketDashboardDataService(HttpClient httpClient, BinanceMarketDataOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _selectedInstrument = options.GetSelectedInstrument();
        _klineInterval = options.KlineInterval;
        _candleLimit = options.CandleLimit;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(RestBaseAddress);
        }

        CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(options, _selectedInstrument);
        _selectedTicker = new TickerState(_selectedInstrument);
        SeedFromSnapshot(CurrentSnapshot);
    }

    public event Action<MarketDashboardDataSnapshot>? SnapshotChanged;

    public IReadOnlyList<MarketInstrumentDefinition> Instruments => _options.Instruments;

    public MarketDashboardDataSnapshot CurrentSnapshot { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_startupTask is not null)
            {
                return _startupTask;
            }

            _runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            _startupTask = StartCoreAsync(_runtimeCts.Token);
            return _startupTask;
        }
    }

    public async Task SelectInstrumentAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var instrument = FindInstrument(symbol);
        if (instrument is null)
        {
            return;
        }

        await _selectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool shouldReload;
            lock (_sync)
            {
                shouldReload = !string.Equals(_selectedInstrument.Symbol, instrument.Symbol, StringComparison.OrdinalIgnoreCase) ||
                    _candles.Count == 0 ||
                    _recentTrades.Count == 0;
            }

            if (!shouldReload)
            {
                return;
            }

            await SelectInstrumentCoreAsync(instrument, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _selectionGate.Release();
        }
    }

    public async Task SetChartIntervalAsync(string interval, int candleLimit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interval))
        {
            return;
        }

        var normalizedInterval = interval.Trim();
        var normalizedCandleLimit = candleLimit > 0 ? candleLimit : _options.CandleLimit;

        await _selectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string currentInterval;
            int currentLimit;
            MarketInstrumentDefinition instrument;

            lock (_sync)
            {
                currentInterval = _klineInterval;
                currentLimit = _candleLimit;
                instrument = _selectedInstrument;
            }

            if (string.Equals(currentInterval, normalizedInterval, StringComparison.OrdinalIgnoreCase) &&
                currentLimit == normalizedCandleLimit)
            {
                return;
            }

            lock (_sync)
            {
                _feedStatusText = "Switching Interval…";
            }

            PublishSnapshot();

            var runtimeToken = _runtimeCts?.Token ?? _disposeCts.Token;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runtimeToken);
            var linkedToken = linkedCts.Token;
            var candles = await FetchKlinesAsync(instrument.Symbol, normalizedInterval, normalizedCandleLimit, linkedToken).ConfigureAwait(false);

            lock (_sync)
            {
                _klineInterval = normalizedInterval;
                _candleLimit = normalizedCandleLimit;
                _candles.Clear();
                _candles.AddRange(candles);
                _feedStatusText = "Binance Live";
            }

            PublishSnapshot();

            if (_runtimeCts is not null && !_runtimeCts.IsCancellationRequested)
            {
                await RestartSelectedLoopAsync(_runtimeCts.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            _selectionGate.Release();
        }
    }

    public void Dispose()
    {
        _disposeCts.Cancel();

        lock (_sync)
        {
            _selectedLoopCts?.Cancel();
        }

        _runtimeCts?.Dispose();
        _selectedLoopCts?.Dispose();
        _selectionGate.Dispose();
        _disposeCts.Dispose();
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await BootstrapAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            UpdateFeedStatus("Binance Retry");
            PublishSnapshot();
        }

        _watchlistTask = Task.Run(() => RunWatchlistLoopAsync(cancellationToken), cancellationToken);
        await RestartSelectedLoopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        MarketInstrumentDefinition selectedInstrument;
        lock (_sync)
        {
            selectedInstrument = _selectedInstrument;
        }

        var watchlistTask = Fetch24HourTickersAsync(_options.Instruments, cancellationToken);
        var candlesTask = FetchKlinesAsync(selectedInstrument.Symbol, _klineInterval, _candleLimit, cancellationToken);
        var tradesTask = FetchRecentTradesAsync(selectedInstrument, _options.RecentTradesLimit, cancellationToken);

        await Task.WhenAll(watchlistTask, candlesTask, tradesTask).ConfigureAwait(false);

        lock (_sync)
        {
            _watchlistStates.Clear();
            var watchlist = watchlistTask.Result;
            for (var i = 0; i < watchlist.Count; i++)
            {
                var state = watchlist[i];
                _watchlistStates[state.Instrument.Symbol] = state;
            }

            if (_watchlistStates.TryGetValue(_selectedInstrument.Symbol, out var selectedTicker))
            {
                _selectedTicker = CopyTickerState(selectedTicker);
            }

            if (string.Equals(_selectedInstrument.Symbol, selectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                _candles.Clear();
                _candles.AddRange(candlesTask.Result);

                _recentTrades.Clear();
                _recentTrades.AddRange(tradesTask.Result);
            }

            _feedStatusText = "Binance Live";
        }

        PublishSnapshot();
    }

    private async Task SelectInstrumentCoreAsync(
        MarketInstrumentDefinition instrument,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _selectedInstrument = instrument;
            if (_watchlistStates.TryGetValue(instrument.Symbol, out var ticker))
            {
                _selectedTicker = CopyTickerState(ticker);
            }
            else
            {
                _selectedTicker = new TickerState(instrument);
            }

            _feedStatusText = "Switching Symbol…";
        }

        PublishSnapshot();

        var runtimeToken = _runtimeCts?.Token ?? _disposeCts.Token;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runtimeToken);
        var linkedToken = linkedCts.Token;

        try
        {
            var tickerTask = Fetch24HourTickerAsync(instrument, linkedToken);
            var candlesTask = FetchKlinesAsync(instrument.Symbol, _klineInterval, _candleLimit, linkedToken);
            var tradesTask = FetchRecentTradesAsync(instrument, _options.RecentTradesLimit, linkedToken);

            await Task.WhenAll(tickerTask, candlesTask, tradesTask).ConfigureAwait(false);

            lock (_sync)
            {
                if (!string.Equals(_selectedInstrument.Symbol, instrument.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var selectedTicker = tickerTask.Result;
                if (selectedTicker is not null)
                {
                    _watchlistStates[instrument.Symbol] = selectedTicker;
                    _selectedTicker = CopyTickerState(selectedTicker);
                }

                _candles.Clear();
                _candles.AddRange(candlesTask.Result);

                _recentTrades.Clear();
                _recentTrades.AddRange(tradesTask.Result);

                _feedStatusText = "Binance Live";
            }

            PublishSnapshot();

            if (_runtimeCts is not null && !_runtimeCts.IsCancellationRequested)
            {
                await RestartSelectedLoopAsync(_runtimeCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            UpdateFeedStatus("Binance Retry");
            PublishSnapshot();
        }
    }

    private async Task RestartSelectedLoopAsync(CancellationToken runtimeToken)
    {
        Task? previousTask;
        CancellationTokenSource? previousCts;

        lock (_sync)
        {
            previousTask = _selectedTask;
            previousCts = _selectedLoopCts;
            _selectedTask = null;
            _selectedLoopCts = null;
        }

        previousCts?.Cancel();
        await AwaitLoopShutdownAsync(previousTask).ConfigureAwait(false);
        previousCts?.Dispose();

        string selectedSymbol;
        lock (_sync)
        {
            selectedSymbol = _selectedInstrument.Symbol;
        }

        var loopCts = CancellationTokenSource.CreateLinkedTokenSource(runtimeToken);
        var loopTask = Task.Run(() => RunSelectedLoopAsync(selectedSymbol, loopCts.Token), loopCts.Token);

        lock (_sync)
        {
            _selectedLoopCts = loopCts;
            _selectedTask = loopTask;
        }
    }

    private static async Task AwaitLoopShutdownAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task RunWatchlistLoopAsync(CancellationToken cancellationToken)
    {
        var streams = BuildWatchlistStreams(_options.Instruments);
        await RunStreamLoopAsync(
            streams,
            data =>
            {
                HandleWatchlistMessage(data);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RunSelectedLoopAsync(string expectedSymbol, CancellationToken cancellationToken)
    {
        var streams = BuildSelectedStreams(expectedSymbol, _klineInterval);
        await RunStreamLoopAsync(
            streams,
            data => HandleSelectedMessage(expectedSymbol, data),
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildWatchlistStreams(IReadOnlyList<MarketInstrumentDefinition> instruments)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < instruments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('/');
            }

            builder.Append(instruments[i].Symbol.ToLowerInvariant());
            builder.Append("@ticker");
        }

        return builder.ToString();
    }

    private static string BuildSelectedStreams(string symbol, string interval)
    {
        var normalizedSymbol = symbol.ToLowerInvariant();
        var normalizedInterval = interval.ToLowerInvariant();
        return $"{normalizedSymbol}@ticker/{normalizedSymbol}@trade/{normalizedSymbol}@kline_{normalizedInterval}";
    }

    private async Task RunStreamLoopAsync(
        string streams,
        Func<JsonElement, bool> onMessage,
        CancellationToken cancellationToken)
    {
        var streamUri = new Uri(StreamBaseAddress + streams);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(streamUri, cancellationToken).ConfigureAwait(false);

                UpdateFeedStatus("Binance Live");
                PublishSnapshot();

                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var payload = await ReceiveTextMessageAsync(socket, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        break;
                    }

                    using var document = JsonDocument.Parse(payload);
                    if (!document.RootElement.TryGetProperty("data", out var data))
                    {
                        continue;
                    }

                    if (!onMessage(data))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                UpdateFeedStatus("Binance Retry");
                PublishSnapshot();
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void HandleWatchlistMessage(JsonElement data)
    {
        var symbol = GetString(data, "s");
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        lock (_sync)
        {
            if (!_watchlistStates.TryGetValue(symbol, out var state))
            {
                return;
            }

            ApplyTickerState(data, state);
            if (string.Equals(symbol, _selectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                ApplyTickerState(data, _selectedTicker);
            }
        }

        PublishSnapshot();
    }

    private bool HandleSelectedMessage(string expectedSymbol, JsonElement data)
    {
        var symbol = GetString(data, "s");
        if (!string.Equals(symbol, expectedSymbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        lock (_sync)
        {
            if (!string.Equals(_selectedInstrument.Symbol, expectedSymbol, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var eventType = GetString(data, "e");
        if (string.Equals(eventType, "24hrTicker", StringComparison.Ordinal))
        {
            lock (_sync)
            {
                ApplyTickerState(data, _selectedTicker);
                if (_watchlistStates.TryGetValue(expectedSymbol, out var watchlistState))
                {
                    ApplyTickerState(data, watchlistState);
                }
            }

            PublishSnapshot();
            return true;
        }

        if (string.Equals(eventType, "trade", StringComparison.Ordinal))
        {
            lock (_sync)
            {
                ApplyTrade(data);
            }

            PublishSnapshot();
            return true;
        }

        if (string.Equals(eventType, "kline", StringComparison.Ordinal) &&
            data.TryGetProperty("k", out var kline))
        {
            lock (_sync)
            {
                ApplyKline(kline);
            }

            PublishSnapshot();
        }

        return true;
    }

    private void ApplyTrade(JsonElement data)
    {
        var timestamp = GetDateTimeOffset(data, "T");
        var price = GetDecimal(data, "p");
        var quantity = GetDecimal(data, "q");
        var quoteQuantity = decimal.Round(price * quantity, 8);
        var isBuy = !GetBoolean(data, "m");
        var tradeId = GetLong(data, "t");

        _recentTrades.Insert(0, new MarketTradeSnapshot(timestamp, isBuy, quantity, quoteQuantity, price, tradeId));
        if (_recentTrades.Count > _options.RecentTradesLimit)
        {
            _recentTrades.RemoveRange(_options.RecentTradesLimit, _recentTrades.Count - _options.RecentTradesLimit);
        }
    }

    private void ApplyKline(JsonElement kline)
    {
        var candle = new MarketCandle(
            GetDateTime(kline, "t"),
            GetDouble(kline, "o"),
            GetDouble(kline, "h"),
            GetDouble(kline, "l"),
            GetDouble(kline, "c"),
            GetDouble(kline, "v"),
            GetDouble(kline, "n"));

        if (_candles.Count > 0 && _candles[^1].Timestamp == candle.Timestamp)
        {
            _candles[^1] = candle;
        }
        else
        {
            _candles.Add(candle);
        }

        while (_candles.Count > _candleLimit)
        {
            _candles.RemoveAt(0);
        }
    }

    private void ApplyTickerState(JsonElement data, TickerState state)
    {
        state.LastPrice = GetDecimal(data, "c");
        state.ChangePercent = GetDecimal(data, "P");
        state.OpenPrice = GetDecimal(data, "o");
        state.HighPrice = GetDecimal(data, "h");
        state.LowPrice = GetDecimal(data, "l");
        state.BidPrice = GetDecimal(data, "b");
        state.AskPrice = GetDecimal(data, "a");
        state.BaseVolume = GetDecimal(data, "v");
        state.QuoteVolume = GetDecimal(data, "q");
        state.TradeCount = (int)Math.Max(0L, GetLong(data, "n"));
        state.LastUpdatedUtc = GetDateTimeOffset(data, "E");
    }

    private void UpdateFeedStatus(string statusText)
    {
        lock (_sync)
        {
            _feedStatusText = statusText;
        }
    }

    private void PublishSnapshot()
    {
        MarketDashboardDataSnapshot snapshot;
        lock (_sync)
        {
            snapshot = BuildSnapshotLocked();
            CurrentSnapshot = snapshot;
        }

        SnapshotChanged?.Invoke(snapshot);
    }

    private MarketDashboardDataSnapshot BuildSnapshotLocked()
    {
        var watchlist = new List<MarketWatchlistQuote>(_options.Instruments.Count);
        for (var i = 0; i < _options.Instruments.Count; i++)
        {
            var instrument = _options.Instruments[i];
            if (_watchlistStates.TryGetValue(instrument.Symbol, out var ticker))
            {
                watchlist.Add(ticker.ToWatchlistQuote());
            }
        }

        return MarketDashboardSnapshotBuilder.Build(
            _feedStatusText,
            _selectedInstrument,
            CurrentSnapshot,
            new MarketTickerSnapshot(
                _selectedTicker.LastPrice,
                _selectedTicker.ChangePercent,
                _selectedTicker.OpenPrice,
                _selectedTicker.HighPrice,
                _selectedTicker.LowPrice,
                _selectedTicker.BidPrice,
                _selectedTicker.AskPrice,
                _selectedTicker.QuoteVolume,
                _selectedTicker.BaseVolume,
                _selectedTicker.TradeCount,
                _selectedTicker.LastUpdatedUtc),
            watchlist,
            _candles.ToArray(),
            _recentTrades.ToArray(),
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<TickerState>> Fetch24HourTickersAsync(
        IReadOnlyList<MarketInstrumentDefinition> instruments,
        CancellationToken cancellationToken)
    {
        var symbols = new string[instruments.Count];
        for (var i = 0; i < instruments.Count; i++)
        {
            symbols[i] = instruments[i].Symbol;
        }

        var symbolsJson = JsonSerializer.Serialize(symbols);
        var response = await SendGetAsync(
            $"/api/v3/ticker/24hr?symbols={Uri.EscapeDataString(symbolsJson)}&type=FULL",
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        var list = new List<TickerState>(instruments.Count);
        if (root.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        for (var index = 0; index < root.GetArrayLength(); index++)
        {
            var item = root[index];
            var symbol = GetString(item, "symbol");
            var instrument = FindInstrument(symbol);
            if (instrument is null)
            {
                continue;
            }

            var state = new TickerState(instrument);
            ApplyTickerStateFromRest(item, state);
            list.Add(state);
        }

        return list;
    }

    private async Task<TickerState?> Fetch24HourTickerAsync(
        MarketInstrumentDefinition instrument,
        CancellationToken cancellationToken)
    {
        var response = await SendGetAsync(
            $"/api/v3/ticker/24hr?symbol={Uri.EscapeDataString(instrument.Symbol)}&type=FULL",
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var state = new TickerState(instrument);
        ApplyTickerStateFromRest(root, state);
        return state;
    }

    private async Task<IReadOnlyList<MarketCandle>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit,
        CancellationToken cancellationToken)
    {
        var response = await SendGetAsync(
            $"/api/v3/uiKlines?symbol={Uri.EscapeDataString(symbol)}&interval={Uri.EscapeDataString(interval)}&limit={limit.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        var list = new List<MarketCandle>(root.GetArrayLength());
        if (root.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        for (var index = 0; index < root.GetArrayLength(); index++)
        {
            var item = root[index];
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 9)
            {
                continue;
            }

            list.Add(new MarketCandle(
                DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64()).UtcDateTime,
                GetDouble(item[1]),
                GetDouble(item[2]),
                GetDouble(item[3]),
                GetDouble(item[4]),
                GetDouble(item[5]),
                GetDouble(item[8])));
        }

        return list;
    }

    private async Task<IReadOnlyList<MarketTradeSnapshot>> FetchRecentTradesAsync(
        MarketInstrumentDefinition instrument,
        int limit,
        CancellationToken cancellationToken)
    {
        var response = await SendGetAsync(
            $"/api/v3/trades?symbol={Uri.EscapeDataString(instrument.Symbol)}&limit={limit.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        var list = new List<MarketTradeSnapshot>(root.GetArrayLength());
        if (root.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        for (var index = root.GetArrayLength() - 1; index >= 0; index--)
        {
            var item = root[index];
            var isBuy = !GetBoolean(item, "isBuyerMaker");
            list.Insert(0, new MarketTradeSnapshot(
                GetDateTimeOffset(item, "time"),
                isBuy,
                GetDecimal(item, "qty"),
                GetDecimal(item, "quoteQty"),
                GetDecimal(item, "price"),
                GetLong(item, "id")));
        }

        return list;
    }

    private async Task<string> SendGetAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private void SeedFromSnapshot(MarketDashboardDataSnapshot snapshot)
    {
        for (var i = 0; i < snapshot.Watchlist.Count; i++)
        {
            var item = snapshot.Watchlist[i];
            var state = new TickerState(item.Instrument)
            {
                LastPrice = item.LastPrice,
                ChangePercent = item.ChangePercent,
                QuoteVolume = item.QuoteVolume24h,
                LastUpdatedUtc = snapshot.LastUpdatedUtc
            };
            _watchlistStates[item.Instrument.Symbol] = state;
        }

        _selectedTicker = new TickerState(snapshot.SelectedInstrument)
        {
            LastPrice = snapshot.LastPrice,
            ChangePercent = snapshot.PriceChangePercent,
            OpenPrice = snapshot.OpenPrice24h,
            HighPrice = snapshot.HighPrice24h,
            LowPrice = snapshot.LowPrice24h,
            BidPrice = snapshot.BidPrice,
            AskPrice = snapshot.AskPrice,
            QuoteVolume = snapshot.QuoteVolume24h,
            BaseVolume = snapshot.BaseVolume24h,
            TradeCount = snapshot.TradeCount24h,
            LastUpdatedUtc = snapshot.LastUpdatedUtc
        };

        _candles.AddRange(snapshot.Candles);
        _recentTrades.AddRange(snapshot.RecentTrades);
    }

    private MarketInstrumentDefinition? FindInstrument(string symbol)
    {
        for (var i = 0; i < _options.Instruments.Count; i++)
        {
            var instrument = _options.Instruments[i];
            if (string.Equals(instrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            {
                return instrument;
            }
        }

        return null;
    }

    private static TickerState CopyTickerState(TickerState source)
    {
        return new TickerState(source.Instrument)
        {
            LastPrice = source.LastPrice,
            ChangePercent = source.ChangePercent,
            OpenPrice = source.OpenPrice,
            HighPrice = source.HighPrice,
            LowPrice = source.LowPrice,
            BidPrice = source.BidPrice,
            AskPrice = source.AskPrice,
            QuoteVolume = source.QuoteVolume,
            BaseVolume = source.BaseVolume,
            TradeCount = source.TradeCount,
            LastUpdatedUtc = source.LastUpdatedUtc
        };
    }

    private static void ApplyTickerStateFromRest(JsonElement data, TickerState state)
    {
        state.LastPrice = GetDecimal(data, "lastPrice");
        state.ChangePercent = GetDecimal(data, "priceChangePercent");
        state.OpenPrice = GetDecimal(data, "openPrice");
        state.HighPrice = GetDecimal(data, "highPrice");
        state.LowPrice = GetDecimal(data, "lowPrice");
        state.BidPrice = GetDecimal(data, "bidPrice");
        state.AskPrice = GetDecimal(data, "askPrice");
        state.BaseVolume = GetDecimal(data, "volume");
        state.QuoteVolume = GetDecimal(data, "quoteVolume");
        state.TradeCount = (int)Math.Max(0L, GetLong(data, "count"));
        state.LastUpdatedUtc = GetDateTimeOffset(data, "closeTime");
    }

    private static async Task<string?> ReceiveTextMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.Count > 0)
                {
                    stream.Write(buffer, 0, result.Count);
                }

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? GetLong(value) : 0L;
    }

    private static long GetLong(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String when long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0L
        };
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => value.GetBoolean()
        };
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? GetDecimal(value) : 0m;
    }

    private static decimal GetDecimal(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0m
        };
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? GetDouble(value) : 0d;
    }

    private static double GetDouble(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
            _ => 0d
        };
    }

    private static DateTime GetDateTime(JsonElement element, string propertyName)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(GetLong(element, propertyName)).UtcDateTime;
    }

    private static DateTimeOffset GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var milliseconds = GetLong(element, propertyName);
        return milliseconds > 0L
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : DateTimeOffset.UtcNow;
    }
}
