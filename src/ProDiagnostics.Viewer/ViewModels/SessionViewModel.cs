using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using ProDiagnostics.Transport;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class SessionViewModel : ObservableObject
{
    private const int MaxActivityEvents = 600;
    private static readonly TimeSpan DefaultTrendRange = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxTrendRange = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TimelineSampleInterval = TimeSpan.FromMilliseconds(100);
    private static readonly int MaxTimelineSamples = (int)Math.Ceiling(MaxTrendRange.TotalMilliseconds / TimelineSampleInterval.TotalMilliseconds) + 1;
    private readonly Dictionary<string, MetricSeriesViewModel> _metricsIndex = new();
    private string _displayName;
    private string _subtitle;
    private MetricDetailTabViewModel? _selectedMetricTab;
    private TimeSpan _trendRange = DefaultTrendRange;
    private DateTimeOffset? _timelineAnchor;
    private long _currentBucketIndex;

    public SessionViewModel(Guid sessionId)
    {
        SessionId = sessionId;
        _displayName = sessionId.ToString();
        _subtitle = string.Empty;
        _currentBucketIndex = 0;
        Metrics = new ObservableCollection<MetricSeriesViewModel>();
        Activities = new ObservableCollection<ActivityEventViewModel>();
        MetricsView = new DataGridCollectionView(Metrics);
        ActivitiesView = new DataGridCollectionView(Activities);
        MetricTabs = new ObservableCollection<MetricDetailTabViewModel>();
        MetricTabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMetricTabs));
    }

    public Guid SessionId { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public ObservableCollection<MetricSeriesViewModel> Metrics { get; }
    public ObservableCollection<ActivityEventViewModel> Activities { get; }
    public DataGridCollectionView MetricsView { get; }
    public DataGridCollectionView ActivitiesView { get; }
    public ObservableCollection<MetricDetailTabViewModel> MetricTabs { get; }

    public MetricDetailTabViewModel? SelectedMetricTab
    {
        get => _selectedMetricTab;
        set => SetProperty(ref _selectedMetricTab, value);
    }

    public bool HasMetricTabs => MetricTabs.Count > 0;

    public TimeSpan TrendRange
    {
        get => _trendRange;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                value = DefaultTrendRange;
            }

            SetProperty(ref _trendRange, value);
        }
    }

    public void UpdateHello(TelemetryHello hello)
    {
        DisplayName = string.IsNullOrWhiteSpace(hello.AppName) ? hello.ProcessName : hello.AppName;
        Subtitle = $"{hello.ProcessName} • PID {hello.ProcessId} • {hello.MachineName}";
    }

    public ActivityEventViewModel AddActivity(TelemetryActivity activity)
    {
        var tagsSummary = FormatTags(activity.Tags);
        var entry = new ActivityEventViewModel(activity.Name, activity.SourceName, activity.StartTime, activity.Duration, tagsSummary);
        Activities.Add(entry);

        if (Activities.Count > MaxActivityEvents)
        {
            Activities.RemoveAt(0);
        }

        return entry;
    }

    public MetricSeriesViewModel AddMetric(TelemetryMetric metric)
    {
        var tagKey = BuildTagKey(metric.Tags);
        var key = $"{metric.MeterName}|{metric.InstrumentName}|{tagKey}";
        var sample = new MetricSample(metric.Timestamp, metric.Value.AsDouble());

        AdvanceTimeline(sample.Timestamp);

        if (!_metricsIndex.TryGetValue(key, out var series))
        {
            series = new MetricSeriesViewModel(
                key,
                metric.MeterName,
                metric.InstrumentName,
                metric.Description,
                metric.Unit,
                metric.InstrumentType,
                FormatTags(metric.Tags));
            series.AddSample(sample);
            series.PrefillTimelineSamples(Metrics.Count == 0 ? null : Metrics[0].TimelineSamples, sample);
            _metricsIndex.Add(key, series);
            Metrics.Add(series);
        }
        else
        {
            series.AddSample(sample);
        }

        return series;
    }

    public void OpenMetricTab(MetricSeriesViewModel series)
    {
        var existing = MetricTabs.FirstOrDefault(tab => ReferenceEquals(tab.Series, series));
        if (existing != null)
        {
            SelectedMetricTab = existing;
            return;
        }

        var tab = new MetricDetailTabViewModel(series, CloseMetricTab);
        MetricTabs.Add(tab);
        SelectedMetricTab = tab;
    }

    private void CloseMetricTab(MetricDetailTabViewModel tab)
    {
        if (!MetricTabs.Remove(tab))
        {
            return;
        }

        if (ReferenceEquals(SelectedMetricTab, tab))
        {
            SelectedMetricTab = MetricTabs.LastOrDefault();
        }
    }

    public void ApplyPreset(PresetDefinition? preset)
    {
        if (preset == null)
        {
            foreach (var series in Metrics)
            {
                series.ApplyAlias(null);
            }

            foreach (var activity in Activities)
            {
                activity.ApplyAlias(null);
            }

            return;
        }

        foreach (var series in Metrics)
        {
            preset.MetricAliases.TryGetValue(series.Name, out var alias);
            series.ApplyAlias(alias);
        }

        foreach (var activity in Activities)
        {
            preset.ActivityAliases.TryGetValue(activity.Name, out var alias);
            activity.ApplyAlias(alias);
        }
    }

    private static string BuildTagKey(IReadOnlyList<TelemetryTag> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        var parts = tags
            .Select(tag => $"{tag.Key}={tag.Value}")
            .OrderBy(part => part, StringComparer.Ordinal)
            .ToArray();
        return string.Join("|", parts);
    }

    private static string FormatTags(IReadOnlyList<TelemetryTag> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        var parts = tags
            .Select(tag => $"{tag.Key}={tag.Value}")
            .ToArray();
        return string.Join(", ", parts);
    }

    private void AdvanceTimeline(DateTimeOffset timestamp)
    {
        EnsureTimelineAnchor(timestamp);

        var bucketIndex = GetBucketIndex(timestamp);
        if (bucketIndex <= _currentBucketIndex)
        {
            return;
        }

        if (Metrics.Count == 0)
        {
            _currentBucketIndex = bucketIndex;
            return;
        }

        for (var i = _currentBucketIndex + 1; i <= bucketIndex; i++)
        {
            var bucketTimestamp = _timelineAnchor!.Value + TimeSpan.FromTicks(TimelineSampleInterval.Ticks * i);
            for (var j = 0; j < Metrics.Count; j++)
            {
                var series = Metrics[j];
                var value = series.GetIntervalValue();
                series.AddTimelineSample(new MetricSample(bucketTimestamp, value));
                series.ResetInterval();
            }
        }

        _currentBucketIndex = bucketIndex;
        TrimTimelineSamples();
    }

    private void EnsureTimelineAnchor(DateTimeOffset timestamp)
    {
        if (_timelineAnchor.HasValue)
        {
            return;
        }

        _timelineAnchor = AlignToInterval(timestamp);
        _currentBucketIndex = GetBucketIndex(timestamp);
    }

    private long GetBucketIndex(DateTimeOffset timestamp)
    {
        if (_timelineAnchor == null)
        {
            return 0;
        }

        var ticks = timestamp.Ticks - _timelineAnchor.Value.Ticks;
        return ticks <= 0 ? 0 : ticks / TimelineSampleInterval.Ticks;
    }

    private static DateTimeOffset AlignToInterval(DateTimeOffset timestamp)
    {
        var ticks = timestamp.Ticks - (timestamp.Ticks % TimelineSampleInterval.Ticks);
        return new DateTimeOffset(ticks, timestamp.Offset);
    }

    private void TrimTimelineSamples()
    {
        if (Metrics.Count == 0)
        {
            return;
        }

        var timeline = Metrics[0].TimelineSamples;
        var removeCount = timeline.Count - MaxTimelineSamples;
        if (removeCount <= 0)
        {
            return;
        }

        for (var i = 0; i < Metrics.Count; i++)
        {
            RemoveFromStart(Metrics[i].TimelineSamples, removeCount);
        }

        if (_timelineAnchor.HasValue)
        {
            _timelineAnchor = _timelineAnchor.Value + TimeSpan.FromTicks(TimelineSampleInterval.Ticks * removeCount);
        }

        _currentBucketIndex = Math.Max(0, _currentBucketIndex - removeCount);
    }

    private static void RemoveFromStart(ObservableCollection<MetricSample> samples, int count)
    {
        var remove = Math.Min(count, samples.Count);
        for (var i = 0; i < remove; i++)
        {
            samples.RemoveAt(0);
        }
    }
}
