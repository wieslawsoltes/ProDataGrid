using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Threading;
using ProDiagnostics.Transport;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class MetricsPageViewModel : ViewModelBase, IDisposable
{
    internal const int DefaultPendingMeasurementQueueCapacity = 4096;
    internal const int DefaultRemoteSessionCapacity = 128;
    private const int MaxMeasurementsPerFlush = 256;

    private readonly AvaloniaList<MetricSeriesViewModel> _series = new();
    private readonly Queue<MetricCaptureService.MetricMeasurementEvent> _pendingMeasurements = new();
    private readonly Dictionary<string, MetricSeriesViewModel> _seriesIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, RemoteSessionInfo> _remoteSessions = new();
    private readonly DataGridCollectionView _seriesView;
    private readonly int _localProcessId;
    private readonly int _maxPendingMeasurements;
    private readonly int _maxRemoteSessions;
    private readonly bool _isLocalCaptureEnabled;

    private DiagnosticsUdpReceiver? _remoteReceiver;
    private IDisposable? _liveMetricsSubscription;
    private int _maxSeries = 500;
    private int _maxSamplesPerSeries = 256;
    private int _remotePort;
    private bool _showCounters = true;
    private bool _showHistograms = true;
    private bool _showGauges = true;
    private bool _showObservable = true;
    private bool _showOther = true;
    private bool _isUpdatesPaused;
    private bool _isFlushScheduled;
    private bool _isRemoteStatusRefreshScheduled;
    private bool _isApplyingRemoteSettings;
    private bool _isDisposed;
    private long _totalMeasurements;
    private long _remotePacketCount;
    private long _droppedLocalPacketCount;
    private long _droppedUnknownSessionMetricCount;
    private long _droppedQueueMeasurementCount;
    private long _droppedSessionCount;
    private string _remoteStatusText = "Remote metrics listener is stopped.";
    private MetricSeriesViewModel? _selectedSeries;
    private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
    private bool _isTabActive = true;
    private bool _isAutoPausedByTabState;

    public MetricsPageViewModel()
        : this(
            subscribeToLiveMetrics: true,
            startRemoteListener: true,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: Environment.ProcessId,
            maxPendingMeasurements: DefaultPendingMeasurementQueueCapacity,
            maxRemoteSessions: DefaultRemoteSessionCapacity,
            startPaused: true)
    {
    }

    internal MetricsPageViewModel(bool subscribeToLiveMetrics)
        : this(
            subscribeToLiveMetrics,
            startRemoteListener: subscribeToLiveMetrics,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: Environment.ProcessId,
            maxPendingMeasurements: DefaultPendingMeasurementQueueCapacity,
            maxRemoteSessions: DefaultRemoteSessionCapacity,
            startPaused: false)
    {
    }

    internal MetricsPageViewModel(bool startRemoteListener, int remotePort, int localProcessId)
        : this(
            subscribeToLiveMetrics: true,
            startRemoteListener,
            remotePort,
            localProcessId,
            DefaultPendingMeasurementQueueCapacity,
            DefaultRemoteSessionCapacity,
            startPaused: false)
    {
    }

    internal MetricsPageViewModel(
        bool startRemoteListener,
        int remotePort,
        int localProcessId,
        int maxPendingMeasurements,
        int maxRemoteSessions)
        : this(
            subscribeToLiveMetrics: true,
            startRemoteListener,
            remotePort,
            localProcessId,
            maxPendingMeasurements,
            maxRemoteSessions,
            startPaused: false)
    {
    }

    internal MetricsPageViewModel(
        bool subscribeToLiveMetrics,
        bool startRemoteListener,
        int remotePort,
        int localProcessId,
        int maxPendingMeasurements,
        int maxRemoteSessions,
        bool startPaused = false)
    {
        _localProcessId = localProcessId;
        _remotePort = NormalizePort(remotePort);
        _maxPendingMeasurements = NormalizePositiveCapacity(maxPendingMeasurements, DefaultPendingMeasurementQueueCapacity);
        _maxRemoteSessions = NormalizePositiveCapacity(maxRemoteSessions, DefaultRemoteSessionCapacity);
        _isLocalCaptureEnabled = subscribeToLiveMetrics;
        _isUpdatesPaused = startPaused;

        MetricsFilter = new FilterViewModel();
        MetricsFilter.RefreshFilter += (_, _) => RefreshSeries();

        _seriesView = new DataGridCollectionView(_series)
        {
            Filter = FilterSeries
        };
        _seriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(MetricSeriesViewModel.LastTimestamp),
            ListSortDirection.Descending));

        if (_isLocalCaptureEnabled)
        {
            _liveMetricsSubscription = MetricCaptureService.Subscribe(OnLiveMeasurementCaptured);
        }

        if (startRemoteListener)
        {
            RestartRemoteListener(_remotePort);
        }
        else
        {
            UpdateRemoteStatusText();
        }
    }

    public DataGridCollectionView SeriesView => _seriesView;

    public FilterViewModel MetricsFilter { get; }

    public int SeriesCount => _series.Count;

    public int VisibleSeriesCount => _seriesView.Count;

    public long TotalMeasurements => _totalMeasurements;

    public int RemotePort => _remotePort;

    public int RemoteSessionCount => _remoteSessions.Count;

    public long RemotePacketCount => _remotePacketCount;

    public long DroppedLocalPacketCount => _droppedLocalPacketCount;

    public long DroppedUnknownSessionMetricCount => _droppedUnknownSessionMetricCount;

    public long DroppedQueueMeasurementCount => _droppedQueueMeasurementCount;

    public long DroppedSessionCount => _droppedSessionCount;

    public string RemoteStatusText
    {
        get => _remoteStatusText;
        private set => RaiseAndSetIfChanged(ref _remoteStatusText, value);
    }

    public bool IsLocalCaptureEnabled => _isLocalCaptureEnabled;

    public bool IsUpdatesPaused
    {
        get => _isUpdatesPaused;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isUpdatesPaused, value))
            {
                RaisePropertyChanged(nameof(ToggleUpdatesText));
            }
        }
    }

    public string ToggleUpdatesText => IsUpdatesPaused ? "Resume" : "Pause";

    public MetricSeriesViewModel? SelectedSeries
    {
        get => _selectedSeries;
        set => RaiseAndSetIfChanged(ref _selectedSeries, value);
    }

    public int MaxSeries
    {
        get => _maxSeries;
        set
        {
            var clamped = value > 0 ? value : 1;
            if (RaiseAndSetIfChanged(ref _maxSeries, clamped))
            {
                TrimToMaxSeries();
                RefreshSeries();
                QueueRemoteMetricsSettingsUpdate();
            }
        }
    }

    public int MaxSamplesPerSeries
    {
        get => _maxSamplesPerSeries;
        set
        {
            var clamped = value > 1 ? value : 2;
            if (RaiseAndSetIfChanged(ref _maxSamplesPerSeries, clamped))
            {
                RefreshSeries();
                QueueRemoteMetricsSettingsUpdate();
            }
        }
    }

    internal void SetRemoteMutationSource(IRemoteMutationDiagnosticsDomainService? mutation)
    {
        _remoteMutation = mutation;
    }

    internal void SetTabActive(bool isActive)
    {
        if (_isDisposed || _isTabActive == isActive)
        {
            return;
        }

        _isTabActive = isActive;
        if (!isActive)
        {
            if (IsUpdatesPaused)
            {
                _isAutoPausedByTabState = false;
                return;
            }

            if (_remoteMutation is not null)
            {
                _ = ApplyRemotePausedStateAsync(isPaused: true, pausedByTabState: true);
                return;
            }

            ApplyPausedState(isPaused: true, pausedByTabState: true);
            return;
        }

        if (!_isAutoPausedByTabState)
        {
            return;
        }

        if (_remoteMutation is not null)
        {
            _ = ApplyRemotePausedStateAsync(isPaused: false, pausedByTabState: false);
            return;
        }

        ApplyPausedState(isPaused: false, pausedByTabState: false);
    }

    public bool ShowCounters
    {
        get => _showCounters;
        set
        {
            if (RaiseAndSetIfChanged(ref _showCounters, value))
            {
                RefreshSeries();
            }
        }
    }

    public bool ShowHistograms
    {
        get => _showHistograms;
        set
        {
            if (RaiseAndSetIfChanged(ref _showHistograms, value))
            {
                RefreshSeries();
            }
        }
    }

    public bool ShowGauges
    {
        get => _showGauges;
        set
        {
            if (RaiseAndSetIfChanged(ref _showGauges, value))
            {
                RefreshSeries();
            }
        }
    }

    public bool ShowObservable
    {
        get => _showObservable;
        set
        {
            if (RaiseAndSetIfChanged(ref _showObservable, value))
            {
                RefreshSeries();
            }
        }
    }

    public bool ShowOther
    {
        get => _showOther;
        set
        {
            if (RaiseAndSetIfChanged(ref _showOther, value))
            {
                RefreshSeries();
            }
        }
    }

    public void SetOptions(DevToolsOptions options)
    {
        if (options is null)
        {
            return;
        }

        var configuredPort = NormalizePort(options.TransportPort);
        if (configuredPort != _remotePort || _remoteReceiver is null)
        {
            RestartRemoteListener(configuredPort);
        }
    }

    public void Clear()
    {
        _pendingMeasurements.Clear();
        _remoteSessions.Clear();
        _series.Clear();
        _seriesIndex.Clear();
        _totalMeasurements = 0;
        _remotePacketCount = 0;
        _droppedLocalPacketCount = 0;
        _droppedUnknownSessionMetricCount = 0;
        _droppedQueueMeasurementCount = 0;
        _droppedSessionCount = 0;
        _isRemoteStatusRefreshScheduled = false;
        SelectedSeries = null;
        RaisePropertyChanged(nameof(TotalMeasurements));
        RaisePropertyChanged(nameof(RemoteSessionCount));
        RaisePropertyChanged(nameof(RemotePacketCount));
        RaisePropertyChanged(nameof(DroppedLocalPacketCount));
        RaisePropertyChanged(nameof(DroppedUnknownSessionMetricCount));
        RaisePropertyChanged(nameof(DroppedQueueMeasurementCount));
        RaisePropertyChanged(nameof(DroppedSessionCount));
        UpdateRemoteStatusText();
        Refresh();
    }

    public bool RemoveSelectedRecord()
    {
        if (SelectedSeries is not { } selected)
        {
            return false;
        }

        if (!_series.Remove(selected))
        {
            return false;
        }

        _seriesIndex.Remove(selected.Key);
        SelectedSeries = null;
        RefreshSeries();
        return true;
    }

    public bool SelectNextMatch()
    {
        return NavigateSelection(forward: true);
    }

    public bool SelectPreviousMatch()
    {
        return NavigateSelection(forward: false);
    }

    public bool ClearSelectionOrFilter()
    {
        if (SelectedSeries is not null)
        {
            SelectedSeries = null;
            return true;
        }

        if (!string.IsNullOrEmpty(MetricsFilter.FilterString))
        {
            MetricsFilter.FilterString = string.Empty;
            return true;
        }

        return false;
    }

    public void Refresh()
    {
        RefreshSeries();
    }

    public void RefreshObservableMetrics()
    {
        RestartRemoteListener(_remotePort);
    }

    public void PauseOrResumeUpdates()
    {
        var nextPaused = !IsUpdatesPaused;
        if (_remoteMutation is not null)
        {
            _ = ApplyRemotePausedStateAsync(nextPaused, pausedByTabState: false);
            return;
        }

        ApplyPausedState(nextPaused, pausedByTabState: false);
    }

    private void ApplyPausedState(bool isPaused, bool pausedByTabState)
    {
        IsUpdatesPaused = isPaused;
        _isAutoPausedByTabState = isPaused && pausedByTabState;
        if (!isPaused && !_isDisposed && _pendingMeasurements.Count > 0 && !_isFlushScheduled)
        {
            _isFlushScheduled = true;
            Dispatcher.UIThread.Post(FlushPendingMeasurements, DispatcherPriority.Background);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _pendingMeasurements.Clear();
        _liveMetricsSubscription?.Dispose();
        _liveMetricsSubscription = null;
        StopRemoteListener();
    }

    internal void AddMeasurement(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            EnqueueMeasurement(measurement);
        }
        else
        {
            Dispatcher.UIThread.Post(() => EnqueueMeasurement(measurement), DispatcherPriority.Background);
        }
    }

    internal void HandleTelemetryPacket(TelemetryPacket packet)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            HandleTelemetryPacketCore(packet);
        }
        else
        {
            Dispatcher.UIThread.Post(() => HandleTelemetryPacketCore(packet), DispatcherPriority.Background);
        }
    }

    private void OnRemotePacketReceived(TelemetryPacket packet, System.Net.IPEndPoint endpoint)
    {
        HandleTelemetryPacket(packet);
    }

    private void OnLiveMeasurementCaptured(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        AddMeasurement(measurement);
    }

    private void HandleTelemetryPacketCore(TelemetryPacket packet)
    {
        if (_isDisposed)
        {
            return;
        }

        switch (packet)
        {
            case TelemetryHello hello:
                if (!_remoteSessions.ContainsKey(hello.SessionId) && _remoteSessions.Count >= _maxRemoteSessions)
                {
                    _droppedSessionCount++;
                    ScheduleRemoteStatusRefresh();
                    return;
                }

                _remoteSessions[hello.SessionId] = new RemoteSessionInfo(hello.ProcessId, hello.ProcessName, hello.AppName);
                ScheduleRemoteStatusRefresh();
                break;
            case TelemetryMetric metric:
                if (!TryConvertMetric(metric, out var measurement))
                {
                    return;
                }

                _remotePacketCount++;
                ScheduleRemoteStatusRefresh();
                AddMeasurement(measurement);
                break;
        }
    }

    private bool TryConvertMetric(TelemetryMetric metric, out MetricCaptureService.MetricMeasurementEvent measurement)
    {
        measurement = default;

        if (!_remoteSessions.TryGetValue(metric.SessionId, out var session))
        {
            _droppedUnknownSessionMetricCount++;
            ScheduleRemoteStatusRefresh();
            return false;
        }

        if (session.ProcessId == _localProcessId)
        {
            _droppedLocalPacketCount++;
            ScheduleRemoteStatusRefresh();
            return false;
        }

        var tags = new MetricCaptureService.MetricTag[metric.Tags.Count + 1];
        for (var i = 0; i < metric.Tags.Count; i++)
        {
            var tag = metric.Tags[i];
            tags[i] = new MetricCaptureService.MetricTag(tag.Key, tag.Value);
        }

        tags[^1] = new MetricCaptureService.MetricTag("process", session.ProcessName + "(" + session.ProcessId + ")");

        measurement = new MetricCaptureService.MetricMeasurementEvent(
            metric.Timestamp,
            metric.MeterName,
            metric.InstrumentName,
            metric.Description,
            metric.Unit,
            metric.InstrumentType,
            metric.Value.AsDouble(),
            tags);

        return true;
    }

    private void EnqueueMeasurement(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        if (_isDisposed)
        {
            return;
        }

        if (_pendingMeasurements.Count >= _maxPendingMeasurements)
        {
            _pendingMeasurements.Dequeue();
            _droppedQueueMeasurementCount++;
            ScheduleRemoteStatusRefresh();
        }

        _pendingMeasurements.Enqueue(measurement);
        if (IsUpdatesPaused)
        {
            return;
        }

        if (_isFlushScheduled)
        {
            return;
        }

        _isFlushScheduled = true;
        Dispatcher.UIThread.Post(FlushPendingMeasurements, DispatcherPriority.Background);
    }

    private void FlushPendingMeasurements()
    {
        _isFlushScheduled = false;
        if (_isDisposed || _pendingMeasurements.Count == 0 || IsUpdatesPaused)
        {
            return;
        }

        using var _ = MetricCaptureService.SuppressCapture();
        var updated = false;
        var processed = 0;
        while (_pendingMeasurements.Count > 0 && processed < MaxMeasurementsPerFlush)
        {
            var measurement = _pendingMeasurements.Dequeue();
            updated |= AddMeasurementCore(measurement);
            processed++;
        }

        if (updated)
        {
            RaisePropertyChanged(nameof(TotalMeasurements));
            RefreshSeries();
        }

        if (_pendingMeasurements.Count > 0 && !_isDisposed)
        {
            _isFlushScheduled = true;
            Dispatcher.UIThread.Post(FlushPendingMeasurements, DispatcherPriority.Background);
        }
    }

    private bool AddMeasurementCore(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        var key = BuildSeriesKey(measurement);
        if (!_seriesIndex.TryGetValue(key, out var series))
        {
            series = new MetricSeriesViewModel(
                key,
                measurement.MeterName,
                measurement.InstrumentName,
                measurement.Description,
                measurement.Unit,
                measurement.InstrumentType,
                ResolveCategory(measurement.InstrumentType),
                FormatTags(measurement.Tags));

            _seriesIndex.Add(key, series);
            _series.Add(series);
            TrimToMaxSeries();
        }

        series.AddSample(measurement.Timestamp, measurement.Value, MaxSamplesPerSeries);
        _totalMeasurements++;
        return true;
    }

    private void TrimToMaxSeries()
    {
        while (_series.Count > MaxSeries)
        {
            var removed = _series[0];
            _series.RemoveAt(0);
            _seriesIndex.Remove(removed.Key);
        }
    }

    private void RefreshSeries()
    {
        using var _ = MetricCaptureService.SuppressCapture();
        _seriesView.Refresh();
        RaisePropertyChanged(nameof(SeriesCount));
        RaisePropertyChanged(nameof(VisibleSeriesCount));
    }

    private bool FilterSeries(object item)
    {
        if (item is not MetricSeriesViewModel series)
        {
            return true;
        }

        if (!IsCategoryVisible(series.Category))
        {
            return false;
        }

        if (MetricsFilter.Filter(series.InstrumentName))
        {
            return true;
        }

        if (MetricsFilter.Filter(series.MeterName))
        {
            return true;
        }

        if (MetricsFilter.Filter(series.InstrumentType))
        {
            return true;
        }

        if (MetricsFilter.Filter(series.TagsSummary))
        {
            return true;
        }

        if (MetricsFilter.Filter(series.Description))
        {
            return true;
        }

        return MetricsFilter.Filter(series.Unit);
    }

    private bool IsCategoryVisible(string category)
    {
        return category switch
        {
            "Counter" => ShowCounters,
            "Histogram" => ShowHistograms,
            "Gauge" => ShowGauges,
            "Observable" => ShowObservable,
            _ => ShowOther
        };
    }

    private static string BuildSeriesKey(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        if (measurement.Tags.Count == 0)
        {
            return measurement.MeterName + "|" + measurement.InstrumentName;
        }

        var tags = new string[measurement.Tags.Count];
        for (var i = 0; i < measurement.Tags.Count; i++)
        {
            var tag = measurement.Tags[i];
            tags[i] = tag.Key + "=" + FormatTagValue(tag.Value);
        }

        Array.Sort(tags, StringComparer.Ordinal);
        return measurement.MeterName + "|" + measurement.InstrumentName + "|" + string.Join(";", tags);
    }

    private static string FormatTags(IReadOnlyList<MetricCaptureService.MetricTag> tags)
    {
        if (tags.Count == 0)
        {
            return "(none)";
        }

        var formatted = new string[tags.Count];
        for (var i = 0; i < tags.Count; i++)
        {
            formatted[i] = tags[i].Key + "=" + FormatTagValue(tags[i].Value);
        }

        Array.Sort(formatted, StringComparer.Ordinal);
        return string.Join(", ", formatted);
    }

    private static string FormatTagValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private static string ResolveCategory(string instrumentType)
    {
        if (instrumentType.Contains("Observable", StringComparison.Ordinal))
        {
            return "Observable";
        }

        if (instrumentType.Contains("Histogram", StringComparison.Ordinal))
        {
            return "Histogram";
        }

        if (instrumentType.Contains("Counter", StringComparison.Ordinal))
        {
            return "Counter";
        }

        if (instrumentType.Contains("Gauge", StringComparison.Ordinal))
        {
            return "Gauge";
        }

        return "Other";
    }

    private bool NavigateSelection(bool forward)
    {
        var visible = _seriesView.Cast<MetricSeriesViewModel>().ToArray();
        if (visible.Length == 0)
        {
            SelectedSeries = null;
            return false;
        }

        var currentIndex = Array.IndexOf(visible, SelectedSeries);
        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = forward ? 0 : visible.Length - 1;
        }
        else if (forward)
        {
            nextIndex = (currentIndex + 1) % visible.Length;
        }
        else
        {
            nextIndex = currentIndex == 0 ? visible.Length - 1 : currentIndex - 1;
        }

        SelectedSeries = visible[nextIndex];
        return true;
    }

    private void RestartRemoteListener(int port)
    {
        StopRemoteListener();

        _remotePort = NormalizePort(port);
        _remoteSessions.Clear();
        RaisePropertyChanged(nameof(RemotePort));
        RaisePropertyChanged(nameof(RemoteSessionCount));

        if (_isDisposed)
        {
            UpdateRemoteStatusText();
            return;
        }

        try
        {
            _remoteReceiver = new DiagnosticsUdpReceiver(_remotePort);
            _remoteReceiver.PacketReceived += OnRemotePacketReceived;
            _remoteReceiver.Start();
        }
        catch (Exception ex)
        {
            _remoteReceiver = null;
            RemoteStatusText = "Failed to bind UDP " + _remotePort + ": " + ex.Message;
            return;
        }

        UpdateRemoteStatusText();
    }

    private void StopRemoteListener()
    {
        if (_remoteReceiver is null)
        {
            return;
        }

        _remoteReceiver.PacketReceived -= OnRemotePacketReceived;
        _remoteReceiver.Dispose();
        _remoteReceiver = null;
        UpdateRemoteStatusText();
    }

    private void UpdateRemoteStatusText()
    {
        if (_remoteReceiver is null)
        {
            RemoteStatusText = "Remote metrics listener is stopped.";
            return;
        }

        RemoteStatusText =
            "Listening UDP " + _remotePort +
            " | Sessions: " + RemoteSessionCount +
            " | Accepted: " + RemotePacketCount +
            " | Dropped local: " + DroppedLocalPacketCount +
            " | Dropped unknown: " + DroppedUnknownSessionMetricCount +
            " | Dropped queue: " + DroppedQueueMeasurementCount +
            " | Dropped sessions: " + DroppedSessionCount;
    }

    private void ScheduleRemoteStatusRefresh()
    {
        if (_isDisposed || _isRemoteStatusRefreshScheduled)
        {
            return;
        }

        _isRemoteStatusRefreshScheduled = true;
        Dispatcher.UIThread.Post(FlushRemoteStatusRefresh, DispatcherPriority.Background);
    }

    private void FlushRemoteStatusRefresh()
    {
        _isRemoteStatusRefreshScheduled = false;
        if (_isDisposed)
        {
            return;
        }

        RaisePropertyChanged(nameof(RemoteSessionCount));
        RaisePropertyChanged(nameof(RemotePacketCount));
        RaisePropertyChanged(nameof(DroppedLocalPacketCount));
        RaisePropertyChanged(nameof(DroppedUnknownSessionMetricCount));
        RaisePropertyChanged(nameof(DroppedQueueMeasurementCount));
        RaisePropertyChanged(nameof(DroppedSessionCount));
        UpdateRemoteStatusText();
    }

    private static int NormalizePort(int port)
    {
        if (port < 1)
        {
            return 1;
        }

        if (port > 65535)
        {
            return 65535;
        }

        return port;
    }

    private static int NormalizePositiveCapacity(int value, int defaultValue)
    {
        if (value > 0)
        {
            return value;
        }

        return defaultValue;
    }

    private void QueueRemoteMetricsSettingsUpdate()
    {
        if (_remoteMutation is null || _isApplyingRemoteSettings)
        {
            return;
        }

        _ = ApplyRemoteMetricsSettingsAsync();
    }

    private async Task ApplyRemotePausedStateAsync(bool isPaused, bool pausedByTabState)
    {
        var mutation = _remoteMutation;
        if (mutation is null)
        {
            ApplyPausedState(isPaused, pausedByTabState);
            return;
        }

        try
        {
            _isApplyingRemoteSettings = true;
            await mutation.SetMetricsPausedAsync(new RemoteSetPausedRequest
            {
                IsPaused = isPaused,
            }).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() => ApplyPausedState(isPaused, pausedByTabState));
        }
        catch
        {
            // Keep local behavior unchanged when remote pause command fails.
        }
        finally
        {
            _isApplyingRemoteSettings = false;
        }
    }

    private async Task ApplyRemoteMetricsSettingsAsync()
    {
        var mutation = _remoteMutation;
        if (mutation is null)
        {
            return;
        }

        try
        {
            _isApplyingRemoteSettings = true;
            await mutation.SetMetricsSettingsAsync(new RemoteSetMetricsSettingsRequest
            {
                MaxSeries = MaxSeries,
                MaxSamplesPerSeries = MaxSamplesPerSeries,
            }).ConfigureAwait(false);
        }
        catch
        {
            // Keep local metrics settings active when remote settings update fails.
        }
        finally
        {
            _isApplyingRemoteSettings = false;
        }
    }

    private readonly record struct RemoteSessionInfo(int ProcessId, string ProcessName, string AppName);
}
