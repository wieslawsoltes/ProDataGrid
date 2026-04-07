using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Avalonia.Diagnostics.UnitTests.Remote;

internal sealed class TestMeterCollector : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, long> _longTotals = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> _doubleTotals = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _measurementCounts = new(StringComparer.Ordinal);

    public TestMeterCollector(string meterName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>(OnLongMeasurement);
        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
            AddLongMeasurement(instrument.Name, measurement));
        _listener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
        _listener.SetMeasurementEventCallback<float>((instrument, measurement, tags, state) =>
            AddDoubleMeasurement(instrument.Name, measurement));
        _listener.Start();
    }

    public long GetLongTotal(string instrumentName)
    {
        return _longTotals.TryGetValue(instrumentName, out var total) ? total : 0;
    }

    public double GetDoubleTotal(string instrumentName)
    {
        return _doubleTotals.TryGetValue(instrumentName, out var total) ? total : 0;
    }

    public int GetMeasurementCount(string instrumentName)
    {
        return _measurementCounts.TryGetValue(instrumentName, out var count) ? count : 0;
    }

    public void RecordObservableInstruments()
    {
        _listener.RecordObservableInstruments();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private void OnLongMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        AddLongMeasurement(instrument.Name, measurement);
    }

    private void OnDoubleMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        AddDoubleMeasurement(instrument.Name, measurement);
    }

    private void AddLongMeasurement(string instrumentName, long value)
    {
        _longTotals.AddOrUpdate(instrumentName, value, (_, current) => current + value);
        _measurementCounts.AddOrUpdate(instrumentName, 1, static (_, current) => current + 1);
    }

    private void AddDoubleMeasurement(string instrumentName, double value)
    {
        _doubleTotals.AddOrUpdate(instrumentName, value, (_, current) => current + value);
        _measurementCounts.AddOrUpdate(instrumentName, 1, static (_, current) => current + 1);
    }
}
