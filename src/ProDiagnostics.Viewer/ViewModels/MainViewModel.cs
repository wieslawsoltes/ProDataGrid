using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ProDiagnostics.Transport;
using ProDiagnostics.Viewer.Models;
using ProDiagnostics.Viewer.Services;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, ColumnVisibilityOption> _metricColumnsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ColumnVisibilityOption> _activityColumnsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, SessionViewModel> _sessionsById = new();
    private DiagnosticsUdpReceiver? _receiver;
    private SessionViewModel? _selectedSession;
    private PresetDefinition? _selectedPreset;
    private TrendRangeOption? _selectedTrendRange;
    private string _metricsFilter = string.Empty;
    private string _activitiesFilter = string.Empty;
    private int _port = TelemetryProtocol.DefaultPort;
    private bool _isListening;
    private string _statusText = string.Empty;

    public MainViewModel()
        : this(startListening: true)
    {
    }

    public MainViewModel(bool startListening)
    {
        Sessions = new ObservableCollection<SessionViewModel>();
        Presets = new ObservableCollection<PresetDefinition>(PresetStore.LoadPresets());
        MetricColumns = new ObservableCollection<ColumnVisibilityOption>();
        ActivityColumns = new ObservableCollection<ColumnVisibilityOption>();
        TrendRanges = new ObservableCollection<TrendRangeOption>();
        InitializeColumnOptions();
        InitializeTrendRanges();
        SelectedPreset = Presets.FirstOrDefault();
        ToggleListeningCommand = new RelayCommand(ToggleListening);
        ReloadPresetsCommand = new RelayCommand(ReloadPresets);
        if (startListening)
        {
            StartListening();
        }
    }

    public ObservableCollection<SessionViewModel> Sessions { get; }
    public ObservableCollection<PresetDefinition> Presets { get; }
    public ObservableCollection<ColumnVisibilityOption> MetricColumns { get; }
    public ObservableCollection<ColumnVisibilityOption> ActivityColumns { get; }
    public ObservableCollection<TrendRangeOption> TrendRanges { get; }

    public RelayCommand ToggleListeningCommand { get; }
    public RelayCommand ReloadPresetsCommand { get; }

    public SessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                ApplyFilters();
            }
        }
    }

    public PresetDefinition? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                ApplyPreset();
                ApplyFilters();
            }
        }
    }

    public TrendRangeOption? SelectedTrendRange
    {
        get => _selectedTrendRange;
        set
        {
            if (SetProperty(ref _selectedTrendRange, value))
            {
                ApplyTrendRange();
            }
        }
    }

    public string MetricsFilter
    {
        get => _metricsFilter;
        set
        {
            if (SetProperty(ref _metricsFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string ActivitiesFilter
    {
        get => _activitiesFilter;
        set
        {
            if (SetProperty(ref _activitiesFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (SetProperty(ref _isListening, value))
            {
                ToggleListeningCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ListeningButtonText));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ListeningButtonText
        => IsListening ? "Pause" : "Listen";

    public string PresetFolder
        => PresetStore.GetUserPresetFolder();

    public void OpenMetricTab(MetricSeriesViewModel series)
    {
        SelectedSession?.OpenMetricTab(series);
    }

    public ColumnVisibilityOption? GetMetricColumn(string key)
        => _metricColumnsByKey.TryGetValue(key, out var option) ? option : null;

    public ColumnVisibilityOption? GetActivityColumn(string key)
        => _activityColumnsByKey.TryGetValue(key, out var option) ? option : null;

    public void Dispose()
    {
        StopListening();
    }

    private void StartListening()
    {
        StopListening();

        try
        {
            _receiver = new DiagnosticsUdpReceiver(Port);
            _receiver.PacketReceived += OnPacketReceived;
            _receiver.Start();
            IsListening = true;
            StatusText = $"Listening on UDP {Port}";
        }
        catch (Exception ex)
        {
            _receiver = null;
            IsListening = false;
            StatusText = $"Failed to bind UDP {Port}: {ex.Message}";
        }
    }

    private void StopListening()
    {
        if (_receiver == null)
        {
            return;
        }

        _receiver.PacketReceived -= OnPacketReceived;
        _receiver.Dispose();
        _receiver = null;
        IsListening = false;
        StatusText = "Not listening";
    }

    private void ToggleListening()
    {
        if (IsListening)
        {
            StopListening();
        }
        else
        {
            StartListening();
        }
    }

    private void ReloadPresets()
    {
        Presets.Clear();
        foreach (var preset in PresetStore.LoadPresets())
        {
            Presets.Add(preset);
        }

        SelectedPreset = Presets.FirstOrDefault();
    }

    private void OnPacketReceived(TelemetryPacket packet, System.Net.IPEndPoint endpoint)
    {
        Dispatcher.UIThread.Post(() => HandlePacket(packet));
    }

    private void HandlePacket(TelemetryPacket packet)
    {
        var session = EnsureSession(packet.SessionId);

        switch (packet)
        {
            case TelemetryHello hello:
                session.UpdateHello(hello);
                break;
            case TelemetryActivity activity:
                var activityEntry = session.AddActivity(activity);
                activityEntry.ApplyAlias(GetActivityAlias(activityEntry.Name));
                break;
            case TelemetryMetric metric:
                var series = session.AddMetric(metric);
                series.ApplyAlias(GetMetricAlias(series.Name));
                break;
        }
    }

    private SessionViewModel EnsureSession(Guid sessionId)
    {
        if (_sessionsById.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        var session = new SessionViewModel(sessionId);
        if (SelectedTrendRange != null)
        {
            session.TrendRange = SelectedTrendRange.Range;
        }
        _sessionsById.Add(sessionId, session);
        Sessions.Add(session);
        SelectedSession ??= session;
        return session;
    }

    private void ApplyPreset()
    {
        foreach (var session in Sessions)
        {
            session.ApplyPreset(SelectedPreset);
        }
    }

    private void ApplyFilters()
    {
        if (SelectedSession == null)
        {
            return;
        }

        SelectedSession.MetricsView.Filter = FilterMetric;
        SelectedSession.ActivitiesView.Filter = FilterActivity;
        SelectedSession.MetricsView.Refresh();
        SelectedSession.ActivitiesView.Refresh();
    }

    private void ApplyTrendRange()
    {
        if (SelectedTrendRange == null)
        {
            return;
        }

        foreach (var session in Sessions)
        {
            session.TrendRange = SelectedTrendRange.Range;
        }
    }

    private bool FilterMetric(object item)
    {
        if (item is not MetricSeriesViewModel metric)
        {
            return false;
        }

        if (SelectedPreset != null && SelectedPreset.IncludeMetrics.Length > 0)
        {
            if (!WildcardMatcher.IsMatch(metric.Name, SelectedPreset.IncludeMetrics))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(MetricsFilter) &&
            !metric.DisplayName.Contains(MetricsFilter, StringComparison.OrdinalIgnoreCase) &&
            !metric.Name.Contains(MetricsFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool FilterActivity(object item)
    {
        if (item is not ActivityEventViewModel activity)
        {
            return false;
        }

        if (SelectedPreset != null && SelectedPreset.IncludeActivities.Length > 0)
        {
            if (!WildcardMatcher.IsMatch(activity.Name, SelectedPreset.IncludeActivities))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(ActivitiesFilter) &&
            !activity.DisplayName.Contains(ActivitiesFilter, StringComparison.OrdinalIgnoreCase) &&
            !activity.Name.Contains(ActivitiesFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private string? GetMetricAlias(string name)
    {
        if (SelectedPreset == null)
        {
            return null;
        }

        return SelectedPreset.MetricAliases.TryGetValue(name, out var alias) ? alias : null;
    }

    private string? GetActivityAlias(string name)
    {
        if (SelectedPreset == null)
        {
            return null;
        }

        return SelectedPreset.ActivityAliases.TryGetValue(name, out var alias) ? alias : null;
    }

    private void InitializeColumnOptions()
    {
        AddMetricColumn("metric", "Metric", isVisible: true);
        AddMetricColumn("description", "Description", isVisible: false);
        AddMetricColumn("meter", "Meter", isVisible: false);
        AddMetricColumn("unit", "Unit", isVisible: true);
        AddMetricColumn("last", "Last", isVisible: true);
        AddMetricColumn("avg", "Avg", isVisible: true);
        AddMetricColumn("min", "Min", isVisible: true);
        AddMetricColumn("max", "Max", isVisible: true);
        AddMetricColumn("samples", "Samples", isVisible: true);
        AddMetricColumn("trend", "Trend", isVisible: true);
        AddMetricColumn("tags", "Tags", isVisible: false);

        AddActivityColumn("started", "Started", isVisible: true);
        AddActivityColumn("activity", "Activity", isVisible: true);
        AddActivityColumn("duration", "Duration (ms)", isVisible: true);
        AddActivityColumn("source", "Source", isVisible: true);
        AddActivityColumn("tags", "Tags", isVisible: true);
    }

    private void InitializeTrendRanges()
    {
        AddTrendRange("5 sec", TimeSpan.FromSeconds(5));
        AddTrendRange("10 sec", TimeSpan.FromSeconds(10));
        AddTrendRange("15 sec", TimeSpan.FromSeconds(15));
        AddTrendRange("30 sec", TimeSpan.FromSeconds(30));
        AddTrendRange("1 min", TimeSpan.FromMinutes(1));
        AddTrendRange("3 min", TimeSpan.FromMinutes(3));
        AddTrendRange("5 min", TimeSpan.FromMinutes(5));
        AddTrendRange("15 min", TimeSpan.FromMinutes(15));

        SelectedTrendRange = TrendRanges.FirstOrDefault(option => option.Range == TimeSpan.FromSeconds(5))
            ?? TrendRanges.FirstOrDefault();
    }

    private void AddMetricColumn(string key, string title, bool isVisible)
    {
        var option = new ColumnVisibilityOption(key, title, isVisible);
        _metricColumnsByKey[key] = option;
        MetricColumns.Add(option);
    }

    private void AddActivityColumn(string key, string title, bool isVisible)
    {
        var option = new ColumnVisibilityOption(key, title, isVisible);
        _activityColumnsByKey[key] = option;
        ActivityColumns.Add(option);
    }

    private void AddTrendRange(string title, TimeSpan range)
    {
        TrendRanges.Add(new TrendRangeOption(title, range));
    }
}
