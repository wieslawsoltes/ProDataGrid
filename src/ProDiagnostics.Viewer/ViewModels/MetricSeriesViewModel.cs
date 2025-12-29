using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class MetricSeriesViewModel : ObservableObject
{
    private const int MaxSampleCount = 600;
    private readonly ObservableCollection<MetricSample> _samples = new();
    private readonly ObservableCollection<MetricSample> _timelineSamples = new();
    private double _lastValue;
    private double _minValue;
    private double _maxValue;
    private double _average;
    private int _sampleCount;
    private double _sum;
    private double _intervalSum;
    private int _intervalCount;
    private string _displayName;

    public MetricSeriesViewModel(
        string key,
        string meterName,
        string name,
        string description,
        string unit,
        string instrumentType,
        string tagsSummary)
    {
        Key = key;
        MeterName = meterName;
        Name = name;
        Description = description;
        Unit = unit;
        InstrumentType = instrumentType;
        TagsSummary = tagsSummary;
        _displayName = name;
        AccentBrush = new SolidColorBrush(CreateAccentColor(key));
    }

    public string Key { get; }
    public string MeterName { get; }
    public string Name { get; }
    public string Description { get; }
    public string Unit { get; }
    public string InstrumentType { get; }
    public string TagsSummary { get; }
    public IBrush AccentBrush { get; }
    public ObservableCollection<MetricSample> Samples => _samples;
    public ObservableCollection<MetricSample> TimelineSamples => _timelineSamples;

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public double LastValue
    {
        get => _lastValue;
        private set => SetProperty(ref _lastValue, value);
    }

    public double MinValue
    {
        get => _minValue;
        private set => SetProperty(ref _minValue, value);
    }

    public double MaxValue
    {
        get => _maxValue;
        private set => SetProperty(ref _maxValue, value);
    }

    public double Average
    {
        get => _average;
        private set => SetProperty(ref _average, value);
    }

    public int SampleCount
    {
        get => _sampleCount;
        private set => SetProperty(ref _sampleCount, value);
    }

    public void ApplyAlias(string? alias)
        => DisplayName = string.IsNullOrWhiteSpace(alias) ? Name : alias;

    public void AddSample(MetricSample sample)
    {
        _samples.Add(sample);
        if (_samples.Count > MaxSampleCount)
        {
            _samples.RemoveAt(0);
        }

        SampleCount++;
        LastValue = sample.Value;
        _intervalSum += sample.Value;
        _intervalCount++;

        if (SampleCount == 1)
        {
            MinValue = sample.Value;
            MaxValue = sample.Value;
        }
        else
        {
            MinValue = Math.Min(MinValue, sample.Value);
            MaxValue = Math.Max(MaxValue, sample.Value);
        }

        _sum += sample.Value;
        Average = _sum / SampleCount;
    }

    public void PrefillTimelineSamples(IReadOnlyList<MetricSample>? templateSamples, MetricSample sample)
    {
        if (templateSamples == null || templateSamples.Count == 0)
        {
            return;
        }

        for (var i = 0; i < templateSamples.Count; i++)
        {
            _timelineSamples.Add(new MetricSample(templateSamples[i].Timestamp, sample.Value));
        }

    }

    public void AddTimelineSample(MetricSample sample)
    {
        _timelineSamples.Add(sample);
    }

    public double GetIntervalValue()
    {
        if (_intervalCount > 0)
        {
            return _intervalSum / _intervalCount;
        }

        return LastValue;
    }

    public void ResetInterval()
    {
        _intervalSum = 0;
        _intervalCount = 0;
    }

    private static Color CreateAccentColor(string seed)
    {
        var hash = seed.GetHashCode();
        var hue = Math.Abs(hash % 360) / 360.0;
        var saturation = 0.55;
        var lightness = 0.55;
        return HslToColor(hue, saturation, lightness);
    }

    private static Color HslToColor(double h, double s, double l)
    {
        double r;
        double g;
        double b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}
