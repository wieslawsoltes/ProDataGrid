using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class MetricSeriesViewModel : ViewModelBase
{
    private readonly List<double> _retainedValues = new();
    private DateTimeOffset _lastTimestamp;
    private double _lastValue;
    private double _averageValue;
    private double _minValue;
    private double _maxValue;
    private int _sampleCount;
    private double _sum;
    private string _trend = "N/A";

    public MetricSeriesViewModel(
        string key,
        string meterName,
        string instrumentName,
        string description,
        string unit,
        string instrumentType,
        string category,
        string tagsSummary)
    {
        Key = key;
        MeterName = meterName;
        InstrumentName = instrumentName;
        Description = description;
        Unit = unit;
        InstrumentType = instrumentType;
        Category = category;
        TagsSummary = tagsSummary;
    }

    public string Key { get; }

    public string MeterName { get; }

    public string InstrumentName { get; }

    public string Description { get; }

    public string Unit { get; }

    public string InstrumentType { get; }

    public string Category { get; }

    public string TagsSummary { get; }

    public DateTimeOffset LastTimestamp
    {
        get => _lastTimestamp;
        private set => RaiseAndSetIfChanged(ref _lastTimestamp, value);
    }

    public double LastValue
    {
        get => _lastValue;
        private set => RaiseAndSetIfChanged(ref _lastValue, value);
    }

    public double AverageValue
    {
        get => _averageValue;
        private set => RaiseAndSetIfChanged(ref _averageValue, value);
    }

    public double MinValue
    {
        get => _minValue;
        private set => RaiseAndSetIfChanged(ref _minValue, value);
    }

    public double MaxValue
    {
        get => _maxValue;
        private set => RaiseAndSetIfChanged(ref _maxValue, value);
    }

    public int SampleCount
    {
        get => _sampleCount;
        private set => RaiseAndSetIfChanged(ref _sampleCount, value);
    }

    public string Trend
    {
        get => _trend;
        private set => RaiseAndSetIfChanged(ref _trend, value);
    }

    public void AddSample(DateTimeOffset timestamp, double value, int maxRetainedSamples)
    {
        LastTimestamp = timestamp;
        LastValue = value;

        if (SampleCount == 0)
        {
            MinValue = value;
            MaxValue = value;
        }
        else
        {
            if (value < MinValue)
            {
                MinValue = value;
            }

            if (value > MaxValue)
            {
                MaxValue = value;
            }
        }

        SampleCount++;
        _sum += value;
        AverageValue = _sum / SampleCount;

        _retainedValues.Add(value);
        var retainedLimit = maxRetainedSamples > 1 ? maxRetainedSamples : 2;
        while (_retainedValues.Count > retainedLimit)
        {
            _retainedValues.RemoveAt(0);
        }

        Trend = ComputeTrend();
    }

    private string ComputeTrend()
    {
        if (_retainedValues.Count < 2)
        {
            return "N/A";
        }

        var first = _retainedValues[0];
        var last = _retainedValues[_retainedValues.Count - 1];
        var delta = last - first;
        var epsilon = Math.Max(1e-9, (MaxValue - MinValue) * 0.01);

        if (delta > epsilon)
        {
            return "Up";
        }

        if (delta < -epsilon)
        {
            return "Down";
        }

        return "Flat";
    }
}
