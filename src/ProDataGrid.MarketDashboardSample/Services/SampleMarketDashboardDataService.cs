using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProDataGrid.MarketDashboardSample.Services;

internal sealed class SampleMarketDashboardDataService : IMarketDashboardDataService
{
    private readonly BinanceMarketDataOptions _options;
    private MarketInstrumentDefinition _selectedInstrument;
    private string _klineInterval;
    private int _candleLimit;

    public SampleMarketDashboardDataService(BinanceMarketDataOptions options)
    {
        _options = options;
        _selectedInstrument = options.GetSelectedInstrument();
        _klineInterval = options.KlineInterval;
        _candleLimit = options.CandleLimit;
        CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(options, _selectedInstrument, _klineInterval, _candleLimit);
    }

    public event Action<MarketDashboardDataSnapshot>? SnapshotChanged;

    public IReadOnlyList<MarketInstrumentDefinition> Instruments => _options.Instruments;

    public MarketDashboardDataSnapshot CurrentSnapshot { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        SnapshotChanged?.Invoke(CurrentSnapshot);
        return Task.CompletedTask;
    }

    public Task SelectInstrumentAsync(string symbol, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < _options.Instruments.Count; i++)
        {
            var instrument = _options.Instruments[i];
            if (!string.Equals(instrument.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _selectedInstrument = instrument;
            CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(_options, _selectedInstrument, _klineInterval, _candleLimit);
            SnapshotChanged?.Invoke(CurrentSnapshot);
            break;
        }

        return Task.CompletedTask;
    }

    public Task SetChartIntervalAsync(string interval, int candleLimit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interval))
        {
            return Task.CompletedTask;
        }

        _klineInterval = interval;
        _candleLimit = candleLimit > 0 ? candleLimit : _options.CandleLimit;
        CurrentSnapshot = MarketDashboardSampleData.CreateSnapshot(_options, _selectedInstrument, _klineInterval, _candleLimit);
        SnapshotChanged?.Invoke(CurrentSnapshot);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
