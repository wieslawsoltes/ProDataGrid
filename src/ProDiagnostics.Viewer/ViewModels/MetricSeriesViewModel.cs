using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using ProDataGrid.SourceGeneration;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.ViewModels;

[GenerateDataGridColumns(
    ProviderName = "MetricSeriesGridSchema",
    SchemaId = "prodiagnostics/viewer/metric/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true,
    Streaming = true,
    PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
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
    [DataGridColumn(DataGridColumnKind.Text, Header = "Meter", ColumnKey = "meter", Order = 2, Width = "*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string MeterName { get; }
    public string Name { get; }
    [DataGridColumn(DataGridColumnKind.Text, Header = "Description", ColumnKey = "description", Order = 1, Width = "3*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string Description { get; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Unit", ColumnKey = "unit", Order = 3, Width = "80", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string Unit { get; }
    public string InstrumentType { get; }
    [DataGridColumn(DataGridColumnKind.Text, Header = "Tags", ColumnKey = "tags", Order = 10, Width = "2*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string TagsSummary { get; }
    public IBrush AccentBrush { get; }
    public ObservableCollection<MetricSample> Samples => _samples;
    public ObservableCollection<MetricSample> TimelineSamples => _timelineSamples;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Metric", ColumnKey = "metric", Order = 0, Width = "2*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Last", ColumnKey = "last", Order = 4, Width = "80", FormatString = "0.###", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public double LastValue
    {
        get => _lastValue;
        private set => SetProperty(ref _lastValue, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Min", ColumnKey = "min", Order = 6, Width = "80", FormatString = "0.###", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public double MinValue
    {
        get => _minValue;
        private set => SetProperty(ref _minValue, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Max", ColumnKey = "max", Order = 7, Width = "80", FormatString = "0.###", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public double MaxValue
    {
        get => _maxValue;
        private set => SetProperty(ref _maxValue, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Avg", ColumnKey = "avg", Order = 5, Width = "80", FormatString = "0.###", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public double Average
    {
        get => _average;
        private set => SetProperty(ref _average, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Samples", ColumnKey = "samples", Order = 8, Width = "80", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public int SampleCount
    {
        get => _sampleCount;
        private set => SetProperty(ref _sampleCount, value);
    }

    [DataGridColumn(DataGridColumnKind.Template, Header = "Trend", ColumnKey = "trend", Order = 9, Width = "120", TemplateKey = "MetricTrendCellTemplate", IsReadOnly = true, CanUserHide = true)]
    public MetricSeriesViewModel Trend => this;

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
