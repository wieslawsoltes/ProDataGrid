using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProDiagnostics.Transport;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteStreamSource : IRemoteStreamSource, IRemoteStreamPauseController, IDisposable
{
    private const int SnapshotVersion = 1;
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = RemoteJsonSerializerContext.Default
    };

    private static readonly IReadOnlyList<RemoteStreamTag> s_emptyTags = Array.Empty<RemoteStreamTag>();

    private readonly object _gate = new();
    private readonly List<Action<RemoteStreamPayload>> _subscribers = new();
    private readonly AvaloniaObject? _root;
    private readonly IDevToolsLogCollector _logCollector;
    private readonly EventsPageViewModel? _eventsPageViewModel;
    private readonly InProcessRemoteStreamSourceOptions _options;
    private readonly int _maxMetricStreamPerSecond;
    private readonly int _maxLogStreamPerSecond;
    private readonly int _maxEventStreamPerSecond;
    private readonly Guid _localSessionId = Guid.NewGuid();
    private readonly string _localProcessName;
    private readonly int _localProcessId;
    private readonly Dictionary<Guid, RemoteUdpSessionInfo> _udpSessions = new();
    private readonly List<MetricHistoryEntry> _metricHistory = new();
    private readonly Dictionary<string, MetricSeriesState> _metricSeriesByKey = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _metricSeriesOrder = new();
    private readonly List<RemoteProfilerStreamPayload> _profilerHistory = new();
    private readonly ProcessProfilerSampler _profilerSampler = new();

    private IDisposable? _metricsSubscription;
    private IDisposable? _logsSubscription;
    private DiagnosticsUdpReceiver? _udpReceiver;
    private Timer? _previewTimer;
    private Timer? _profilerTimer;
    private Timer? _metricsCoalesceTimer;
    private string _previewTransport = "svg";
    private int _previewTargetFps = 5;
    private int _previewMaxWidth = 1920;
    private int _previewMaxHeight = 1080;
    private double _previewScale = 1d;
    private bool _previewEnableDiff = true;
    private bool _previewIncludeFrameData = true;
    private string? _previewLastFrameHash;
    private int _previewCaptureScheduled;
    private long _previewLastCaptureTicks;
    private double _previewAdaptiveScaleFactor = 1d;
    private int _previewSlowCaptureStreak;
    private int _previewFastCaptureStreak;
    private long _previewSceneInvalidationVersion = 1;
    private long _previewLastCapturedSceneVersion = -1;
    private IRenderer? _previewRenderer;
    private EventHandler<SceneInvalidatedEventArgs>? _previewSceneInvalidatedHandler;
    private PreviewSvgFrameCacheEntry? _previewSvgFrameCache;
    private PreviewPngFrameCacheEntry? _previewPngFrameCache;
    private bool _eventsHooked;
    private bool _isDisposed;
    private bool _isStarted;
    private int _maxRetainedMeasurements;
    private int _maxRetainedProfilerSamples;
    private int _maxSeries;
    private int _maxSamplesPerSeries;
    private TimeSpan _profilerSampleInterval;
    private int _previewPaused;
    private int _metricsPaused;
    private int _profilerPaused;
    private long _previewGeneration = 1;
    private long _metricsGeneration = 1;
    private long _profilerGeneration = 1;
    private long _totalMetricMeasurements;
    private long _totalProfilerSamples;
    private long _droppedMetricMeasurements;
    private long _droppedProfilerSamples;
    private long _nextMetricSequence;
    private double _profilerCurrentCpuPercent;
    private double _profilerPeakCpuPercent;
    private double _profilerCurrentWorkingSetMb;
    private double _profilerPeakWorkingSetMb;
    private double _profilerCurrentManagedHeapMb;
    private double _profilerCurrentActivityDurationMs;
    private double _profilerPeakActivityDurationMs;
    private long _nextLogPublishTicksUtc;
    private long _nextEventPublishTicksUtc;
    private long _nextMetricPublishTicksUtc;
    private RemoteMetricStreamPayload? _pendingMetricPayload;
    private RemoteStreamDemandSnapshot _streamDemand = RemoteStreamDemandSnapshot.All;

    public InProcessRemoteStreamSource(
        AvaloniaObject? root = null,
        EventsPageViewModel? eventsPageViewModel = null,
        IDevToolsLogCollector? logCollector = null,
        InProcessRemoteStreamSourceOptions? options = null)
    {
        _root = root;
        _eventsPageViewModel = eventsPageViewModel;
        _logCollector = logCollector ?? InProcessDevToolsLogCollector.Instance;
        _options = InProcessRemoteStreamSourceOptions.Normalize(options ?? InProcessRemoteStreamSourceOptions.Default);
        _localProcessId = Environment.ProcessId;
        _maxRetainedMeasurements = _options.MaxRetainedMeasurements;
        _maxRetainedProfilerSamples = _options.MaxRetainedProfilerSamples;
        _maxSeries = _options.MaxSeries;
        _maxSamplesPerSeries = _options.MaxSamplesPerSeries;
        _profilerSampleInterval = _options.ProfilerSampleInterval;
        _maxMetricStreamPerSecond = _options.MaxMetricStreamPerSecond;
        _maxLogStreamPerSecond = _options.MaxLogStreamPerSecond;
        _maxEventStreamPerSecond = _options.MaxEventStreamPerSecond;

        using var process = Process.GetCurrentProcess();
        _localProcessName = process.ProcessName + "(" + _localProcessId + ")";
    }

    public IDisposable Subscribe(Action<RemoteStreamPayload> onPayload)
    {
        if (onPayload is null)
        {
            throw new ArgumentNullException(nameof(onPayload));
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            _subscribers.Add(onPayload);
            if (_subscribers.Count == 1)
            {
                StartCore();
            }
        }

        return new Subscription(this, onPayload);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _subscribers.Clear();
            StopCore();
        }
    }

    public bool IsMetricsPaused => Volatile.Read(ref _metricsPaused) != 0;

    public bool IsProfilerPaused => Volatile.Read(ref _profilerPaused) != 0;

    public bool IsPreviewPaused => Volatile.Read(ref _previewPaused) != 0;

    public bool SetPreviewPaused(bool isPaused)
    {
        var next = isPaused ? 1 : 0;
        var previous = Interlocked.Exchange(ref _previewPaused, next);
        var changed = previous != next;
        lock (_gate)
        {
            if (_isDisposed || !_isStarted)
            {
                if (changed)
                {
                    _previewGeneration++;
                }

                return changed;
            }

            SyncPreviewProducerLocked();

            if (changed)
            {
                _previewGeneration++;
            }
        }

        return changed;
    }

    public RemotePreviewCapabilitiesSnapshot GetPreviewCapabilitiesSnapshot(RemotePreviewCapabilitiesRequest request)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var status = ResolvePreviewTopLevel() is null
                ? "Preview target is unavailable."
                : "ok";
            return new RemotePreviewCapabilitiesSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _previewGeneration,
                Status: status,
                DefaultTransport: "svg",
                SupportedTransports: new[] { "svg", "png" },
                SupportsInput: true,
                SupportsDiff: true,
                IsPaused: IsPreviewPaused,
                StreamEnabled: _isStarted,
                TargetFps: _previewTargetFps,
                MaxWidth: _previewMaxWidth,
                MaxHeight: _previewMaxHeight,
                MaxScale: 4d);
        }
    }

    public RemotePreviewSnapshot GetPreviewSnapshot(RemotePreviewSnapshotRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return CapturePreviewSnapshotCore(request, request.PreviousFrameHash, isStreaming: false);
            }
        }

        return Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    return CapturePreviewSnapshotCore(request, request.PreviousFrameHash, isStreaming: false);
                }
            },
            DispatcherPriority.Send).GetAwaiter().GetResult();
    }

    public bool SetMetricsPaused(bool isPaused)
    {
        var next = isPaused ? 1 : 0;
        var previous = Interlocked.Exchange(ref _metricsPaused, next);
        var changed = previous != next;
        lock (_gate)
        {
            if (_isDisposed || !_isStarted || !_options.EnableMetricsStream)
            {
                if (changed)
                {
                    _metricsGeneration++;
                }

                return changed;
            }

            SyncMetricsProducerLocked();

            if (changed)
            {
                _metricsGeneration++;
            }
        }

        return changed;
    }

    public bool SetProfilerPaused(bool isPaused)
    {
        var next = isPaused ? 1 : 0;
        var previous = Interlocked.Exchange(ref _profilerPaused, next);
        var changed = previous != next;
        lock (_gate)
        {
            if (_isDisposed || !_isStarted || !_options.EnableProfilerStream)
            {
                if (changed)
                {
                    _profilerGeneration++;
                }

                return changed;
            }

            SyncProfilerProducerLocked();

            if (changed)
            {
                _profilerGeneration++;
            }
        }

        return changed;
    }

    public void SetStreamDemand(RemoteStreamDemandSnapshot demand)
    {
        lock (_gate)
        {
            if (_streamDemand == demand)
            {
                return;
            }

            _streamDemand = demand;
            SyncStreamProducersLocked();
        }
    }

    public RemoteStreamDemandSnapshot GetStreamDemand()
    {
        lock (_gate)
        {
            return _streamDemand;
        }
    }

    public RemoteMetricsSnapshot GetMetricsSnapshot(RemoteMetricsSnapshotRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        lock (_gate)
        {
            ThrowIfDisposed();

            var status = _options.EnableMetricsStream
                ? "ok"
                : "Metrics stream is disabled.";
            var measurements = request.IncludeMeasurements
                ? _metricHistory
                    .Select(static entry => MapMetricMeasurement(entry.Payload))
                    .ToArray()
                : Array.Empty<RemoteMetricMeasurementSnapshot>();
            var series = request.IncludeSeries
                ? BuildMetricSeries()
                : Array.Empty<RemoteMetricSeriesSnapshot>();

            return new RemoteMetricsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _metricsGeneration,
                Status: status,
                IsPaused: IsMetricsPaused,
                MaxRetainedMeasurements: _maxRetainedMeasurements,
                MaxSeries: _maxSeries,
                MaxSamplesPerSeries: _maxSamplesPerSeries,
                TotalMeasurements: _totalMetricMeasurements,
                DroppedMeasurements: _droppedMetricMeasurements,
                MeasurementCount: _metricHistory.Count,
                SeriesCount: series.Length,
                Series: series,
                Measurements: measurements);
        }
    }

    public RemoteProfilerSnapshot GetProfilerSnapshot(RemoteProfilerSnapshotRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        lock (_gate)
        {
            ThrowIfDisposed();

            var status = _options.EnableProfilerStream
                ? "ok"
                : "Profiler stream is disabled.";
            var samples = request.IncludeSamples
                ? _profilerHistory
                    .Select(MapProfilerSample)
                    .ToArray()
                : Array.Empty<RemoteProfilerSampleSnapshot>();

            return new RemoteProfilerSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _profilerGeneration,
                Status: status,
                IsPaused: IsProfilerPaused,
                MaxRetainedSamples: _maxRetainedProfilerSamples,
                SampleIntervalMs: (int)Math.Max(1, _profilerSampleInterval.TotalMilliseconds),
                TotalSamples: _totalProfilerSamples,
                DroppedSamples: _droppedProfilerSamples,
                SampleCount: _profilerHistory.Count,
                CurrentCpuPercent: _profilerCurrentCpuPercent,
                PeakCpuPercent: _profilerPeakCpuPercent,
                CurrentWorkingSetMb: _profilerCurrentWorkingSetMb,
                PeakWorkingSetMb: _profilerPeakWorkingSetMb,
                CurrentManagedHeapMb: _profilerCurrentManagedHeapMb,
                CurrentActivityDurationMs: _profilerCurrentActivityDurationMs,
                PeakActivityDurationMs: _profilerPeakActivityDurationMs,
                Samples: samples);
        }
    }

    public int ApplyPreviewSettings(RemoteSetPreviewSettingsRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (request.Transport is null &&
                request.MaxWidth is null &&
                request.MaxHeight is null &&
                request.Scale is null &&
                request.TargetFps is null &&
                request.EnableDiff is null &&
                request.IncludeFrameData is null)
            {
                throw new RemoteMutationValidationException("At least one preview setting must be specified.");
            }

            var changed = 0;
            var restartTimer = false;

            if (request.Transport is { } transport)
            {
                var normalizedTransport = NormalizePreviewTransport(transport);
                if (!string.Equals(_previewTransport, normalizedTransport, StringComparison.Ordinal))
                {
                    _previewTransport = normalizedTransport;
                    changed++;
                }
            }

            if (request.MaxWidth is { } maxWidth)
            {
                if (maxWidth <= 0)
                {
                    throw new RemoteMutationValidationException("MaxWidth must be greater than 0.");
                }

                if (_previewMaxWidth != maxWidth)
                {
                    _previewMaxWidth = maxWidth;
                    changed++;
                }
            }

            if (request.MaxHeight is { } maxHeight)
            {
                if (maxHeight <= 0)
                {
                    throw new RemoteMutationValidationException("MaxHeight must be greater than 0.");
                }

                if (_previewMaxHeight != maxHeight)
                {
                    _previewMaxHeight = maxHeight;
                    changed++;
                }
            }

            if (request.Scale is { } scale)
            {
                if (scale <= 0)
                {
                    throw new RemoteMutationValidationException("Scale must be greater than 0.");
                }

                var clampedScale = ClampPreviewScale(scale);
                if (Math.Abs(_previewScale - clampedScale) > double.Epsilon)
                {
                    _previewScale = clampedScale;
                    changed++;
                }
            }

            if (request.TargetFps is { } targetFps)
            {
                if (targetFps <= 0)
                {
                    throw new RemoteMutationValidationException("TargetFps must be greater than 0.");
                }

                var clampedFps = Math.Clamp(targetFps, 1, 60);
                if (_previewTargetFps != clampedFps)
                {
                    _previewTargetFps = clampedFps;
                    changed++;
                    restartTimer = true;
                }
            }

            if (request.EnableDiff is { } enableDiff && _previewEnableDiff != enableDiff)
            {
                _previewEnableDiff = enableDiff;
                changed++;
            }

            if (request.IncludeFrameData is { } includeFrameData && _previewIncludeFrameData != includeFrameData)
            {
                _previewIncludeFrameData = includeFrameData;
                changed++;
            }

            if (changed > 0)
            {
                _previewLastFrameHash = null;
                _previewLastCapturedSceneVersion = -1;
                _previewAdaptiveScaleFactor = 1d;
                _previewSlowCaptureStreak = 0;
                _previewFastCaptureStreak = 0;
                ResetPreviewEncodedFrameCache();
                _previewGeneration++;
            }

            if (restartTimer && _isStarted)
            {
                DisablePreviewTimer();
                SyncPreviewProducerLocked();
            }

            return changed;
        }
    }

    public int ApplyMetricsSettings(RemoteSetMetricsSettingsRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (request.MaxRetainedMeasurements is null &&
                request.MaxSeries is null &&
                request.MaxSamplesPerSeries is null)
            {
                throw new RemoteMutationValidationException("At least one metrics setting must be specified.");
            }

            var changed = 0;
            var rebuildSeries = false;

            if (request.MaxRetainedMeasurements is { } maxRetainedMeasurements)
            {
                if (maxRetainedMeasurements <= 0)
                {
                    throw new RemoteMutationValidationException("MaxRetainedMeasurements must be greater than 0.");
                }

                if (_maxRetainedMeasurements != maxRetainedMeasurements)
                {
                    _maxRetainedMeasurements = maxRetainedMeasurements;
                    changed++;
                }
            }

            if (request.MaxSeries is { } maxSeries)
            {
                if (maxSeries <= 0)
                {
                    throw new RemoteMutationValidationException("MaxSeries must be greater than 0.");
                }

                if (_maxSeries != maxSeries)
                {
                    _maxSeries = maxSeries;
                    changed++;
                    rebuildSeries = true;
                }
            }

            if (request.MaxSamplesPerSeries is { } maxSamplesPerSeries)
            {
                if (maxSamplesPerSeries <= 0)
                {
                    throw new RemoteMutationValidationException("MaxSamplesPerSeries must be greater than 0.");
                }

                if (_maxSamplesPerSeries != maxSamplesPerSeries)
                {
                    _maxSamplesPerSeries = maxSamplesPerSeries;
                    changed++;
                    rebuildSeries = true;
                }
            }

            if (_metricHistory.Count > _maxRetainedMeasurements)
            {
                var removeCount = _metricHistory.Count - _maxRetainedMeasurements;
                for (var i = 0; i < removeCount; i++)
                {
                    RemoveMetricSeriesSample(_metricHistory[i]);
                }

                _metricHistory.RemoveRange(0, removeCount);
                _droppedMetricMeasurements += removeCount;
            }

            if (rebuildSeries)
            {
                RebuildMetricSeriesStateFromHistory();
            }
            else
            {
                PruneMetricSeriesState();
            }

            if (changed > 0)
            {
                _metricsGeneration++;
            }

            return changed;
        }
    }

    public int ApplyProfilerSettings(RemoteSetProfilerSettingsRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (request.MaxRetainedSamples is null && request.SampleIntervalMs is null)
            {
                throw new RemoteMutationValidationException("At least one profiler setting must be specified.");
            }

            var changed = 0;
            var restartTimer = false;

            if (request.MaxRetainedSamples is { } maxRetainedSamples)
            {
                if (maxRetainedSamples <= 0)
                {
                    throw new RemoteMutationValidationException("MaxRetainedSamples must be greater than 0.");
                }

                if (_maxRetainedProfilerSamples != maxRetainedSamples)
                {
                    _maxRetainedProfilerSamples = maxRetainedSamples;
                    changed++;
                }
            }

            if (request.SampleIntervalMs is { } sampleIntervalMs)
            {
                if (sampleIntervalMs <= 0)
                {
                    throw new RemoteMutationValidationException("SampleIntervalMs must be greater than 0.");
                }

                var interval = TimeSpan.FromMilliseconds(sampleIntervalMs);
                if (_profilerSampleInterval != interval)
                {
                    _profilerSampleInterval = interval;
                    changed++;
                    restartTimer = true;
                }
            }

            if (_profilerHistory.Count > _maxRetainedProfilerSamples)
            {
                var removeCount = _profilerHistory.Count - _maxRetainedProfilerSamples;
                _profilerHistory.RemoveRange(0, removeCount);
                _droppedProfilerSamples += removeCount;
                RecomputeProfilerAggregates();
            }

            if (restartTimer && _isStarted)
            {
                DisableProfilerTimer();
                SyncProfilerProducerLocked();
            }

            if (changed > 0)
            {
                _profilerGeneration++;
            }

            return changed;
        }
    }

    public void PublishSelection(RemoteSelectionSnapshot selection)
    {
        if (!IsTopicDemanded(RemoteStreamTopics.Selection))
        {
            return;
        }

        PublishObject(RemoteStreamTopics.Selection, selection);
    }

    private void StartCore()
    {
        if (_isStarted || _isDisposed)
        {
            return;
        }

        EnsurePreviewSceneSubscription();

        if (_options.EnableLogsStream)
        {
            _logsSubscription = _logCollector.Subscribe(OnLogCaptured);
        }

        if (_options.EnableEventsStream && _eventsPageViewModel is not null)
        {
            _eventsPageViewModel.RecordedEvents.CollectionChanged += OnRecordedEventsChanged;
            _eventsHooked = true;
        }

        _isStarted = true;
        SyncStreamProducersLocked();

        if (_options.EnableUdpTelemetryFallback)
        {
            TryStartUdpReceiver();
        }
    }

    private void StopCore()
    {
        if (!_isStarted)
        {
            return;
        }

        DisablePreviewTimer();
        DisablePreviewSceneSubscription();
        ResetPreviewEncodedFrameCache();
        DisableMetricsSubscription();

        _logsSubscription?.Dispose();
        _logsSubscription = null;

        if (_eventsHooked && _eventsPageViewModel is not null)
        {
            _eventsPageViewModel.RecordedEvents.CollectionChanged -= OnRecordedEventsChanged;
            _eventsHooked = false;
        }

        DisableProfilerTimer();

        if (_udpReceiver is not null)
        {
            _udpReceiver.PacketReceived -= OnUdpPacketReceived;
            _udpReceiver.Dispose();
            _udpReceiver = null;
        }

        _udpSessions.Clear();
        _isStarted = false;
    }

    private void TryStartUdpReceiver()
    {
        if (_udpReceiver is not null)
        {
            return;
        }

        try
        {
            var receiver = new DiagnosticsUdpReceiver(_options.UdpPort);
            receiver.PacketReceived += OnUdpPacketReceived;
            receiver.Start();
            _udpReceiver = receiver;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("InProcessRemoteStreamSource UDP fallback failed: " + ex.Message);
            _udpReceiver = null;
        }
    }

    private void OnMetricCaptured(MetricCaptureService.MetricMeasurementEvent measurement)
    {
        var payload = new RemoteMetricStreamPayload(
            TimestampUtc: measurement.Timestamp,
            Source: "local",
            SessionId: _localSessionId,
            MeterName: measurement.MeterName,
            InstrumentName: measurement.InstrumentName,
            Description: measurement.Description,
            Unit: measurement.Unit,
            InstrumentType: measurement.InstrumentType,
            Value: measurement.Value,
            Tags: ConvertTags(measurement.Tags));
        HandleIncomingMetricPayload(payload);
    }

    private void HandleIncomingMetricPayload(RemoteMetricStreamPayload payload)
    {
        var publishImmediately = false;
        lock (_gate)
        {
            if (_isDisposed || !_isStarted || !ShouldEmitMetricsStreamLocked())
            {
                return;
            }

            RecordMetricPayload(payload);
            publishImmediately = TryScheduleMetricsPublishLocked(payload);
        }

        if (publishImmediately)
        {
            PublishObject(RemoteStreamTopics.Metrics, payload, recordForSnapshots: false);
        }
    }

    private bool TryScheduleMetricsPublishLocked(RemoteMetricStreamPayload payload)
    {
        if (_maxMetricStreamPerSecond <= 0)
        {
            return true;
        }

        var spacingTicks = ResolveMetricSpacingTicks();
        var nowTicks = DateTime.UtcNow.Ticks;
        if (nowTicks >= _nextMetricPublishTicksUtc)
        {
            _nextMetricPublishTicksUtc = nowTicks + spacingTicks;
            return true;
        }

        _pendingMetricPayload = payload;
        EnsureMetricsCoalesceTimerLocked(spacingTicks);
        return false;
    }

    private static long ResolveMetricSpacingTicks(int maxMetricStreamPerSecond)
    {
        if (maxMetricStreamPerSecond <= 0)
        {
            return 1;
        }

        return Math.Max(1L, TimeSpan.TicksPerSecond / maxMetricStreamPerSecond);
    }

    private long ResolveMetricSpacingTicks()
    {
        return ResolveMetricSpacingTicks(_maxMetricStreamPerSecond);
    }

    private void EnsureMetricsCoalesceTimerLocked(long spacingTicks)
    {
        if (_metricsCoalesceTimer is not null)
        {
            return;
        }

        var interval = TimeSpan.FromTicks(spacingTicks);
        _metricsCoalesceTimer = new Timer(
            static state => ((InProcessRemoteStreamSource)state!).OnMetricsCoalesceTimerTick(),
            this,
            dueTime: interval,
            period: interval);
    }

    private void DisableMetricsCoalesceTimer()
    {
        _metricsCoalesceTimer?.Dispose();
        _metricsCoalesceTimer = null;
    }

    private void OnMetricsCoalesceTimerTick()
    {
        RemoteMetricStreamPayload? payload = null;
        lock (_gate)
        {
            var canEmit = ShouldEmitMetricsStreamLocked();
            if (!canEmit || _pendingMetricPayload is null)
            {
                if (!canEmit)
                {
                    _pendingMetricPayload = null;
                }

                return;
            }

            var nowTicks = DateTime.UtcNow.Ticks;
            if (_maxMetricStreamPerSecond > 0 && nowTicks < _nextMetricPublishTicksUtc)
            {
                return;
            }

            if (_maxMetricStreamPerSecond > 0)
            {
                _nextMetricPublishTicksUtc = nowTicks + ResolveMetricSpacingTicks();
            }

            payload = _pendingMetricPayload;
            _pendingMetricPayload = null;
        }

        if (payload.HasValue)
        {
            PublishObject(RemoteStreamTopics.Metrics, payload.Value, recordForSnapshots: false);
        }
    }

    private void OnLogCaptured(DevToolsLogEvent logEvent)
    {
        if (!IsTopicDemanded(RemoteStreamTopics.Logs))
        {
            return;
        }

        if (!CanPublishAtRate(ref _nextLogPublishTicksUtc, _maxLogStreamPerSecond))
        {
            return;
        }

        PublishObject(
            RemoteStreamTopics.Logs,
            new RemoteLogStreamPayload(
                TimestampUtc: logEvent.Timestamp,
                Level: logEvent.Level.ToString(),
                Area: logEvent.Area,
                Source: logEvent.Source,
                Message: logEvent.Message));
    }

    private void OnRecordedEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsTopicDemanded(RemoteStreamTopics.Events))
        {
            return;
        }

        if (e.Action is not NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            return;
        }

        for (var i = 0; i < e.NewItems.Count; i++)
        {
            if (e.NewItems[i] is not FiredEvent firedEvent)
            {
                continue;
            }

            if (!CanPublishAtRate(ref _nextEventPublishTicksUtc, _maxEventStreamPerSecond))
            {
                continue;
            }

            PublishObject(
                RemoteStreamTopics.Events,
                new RemoteEventStreamPayload(
                    TimestampUtc: new DateTimeOffset(firedEvent.TriggerTime),
                    EventName: firedEvent.Event.Name,
                    Source: DescribeTarget(firedEvent.Source),
                    Originator: firedEvent.Originator.HandlerName,
                    ObservedRoutes: firedEvent.ObservedRoutes.ToString(),
                    IsHandled: firedEvent.IsHandled,
                    HandledBy: firedEvent.HandledBy?.HandlerName ?? string.Empty,
                    ChainLength: firedEvent.EventChain.Count));
        }
    }

    private static bool CanPublishAtRate(ref long nextAllowedTicksUtc, int maxPerSecond)
    {
        if (maxPerSecond <= 0)
        {
            return true;
        }

        var spacingTicks = Math.Max(1L, TimeSpan.TicksPerSecond / maxPerSecond);
        var now = DateTime.UtcNow.Ticks;
        var next = Volatile.Read(ref nextAllowedTicksUtc);
        if (now < next)
        {
            return false;
        }

        Volatile.Write(ref nextAllowedTicksUtc, now + spacingTicks);
        return true;
    }

    private void OnPreviewTimerTick()
    {
        if (!ShouldEmitPreviewStream())
        {
            return;
        }

        if (ShouldSkipPreviewTimerTick())
        {
            return;
        }

        if (Interlocked.Exchange(ref _previewCaptureScheduled, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    lock (_gate)
                    {
                        if (!ShouldEmitPreviewStreamLocked())
                        {
                            return;
                        }

                        var request = new RemotePreviewSnapshotRequest
                        {
                            Transport = _previewTransport,
                            PreviousFrameHash = _previewLastFrameHash,
                            IncludeFrameData = _previewIncludeFrameData,
                            EnableDiff = _previewEnableDiff,
                            MaxWidth = _previewMaxWidth,
                            MaxHeight = _previewMaxHeight,
                            Scale = _previewScale,
                        };

                        var snapshot = CapturePreviewSnapshotCore(
                            request,
                            previousFrameHash: _previewLastFrameHash,
                            isStreaming: true);
                        Volatile.Write(ref _previewLastCaptureTicks, Stopwatch.GetTimestamp());
                        if (snapshot.HasChanges)
                        {
                            _previewLastFrameHash = snapshot.FrameHash;
                            PublishObject(
                                RemoteStreamTopics.Preview,
                                new RemotePreviewStreamPayload(
                                    TimestampUtc: snapshot.CapturedAtUtc,
                                    Generation: snapshot.Generation,
                                    Transport: snapshot.Transport,
                                    MimeType: snapshot.MimeType,
                                    Width: snapshot.Width,
                                    Height: snapshot.Height,
                                    Scale: snapshot.Scale,
                                    RenderScaling: snapshot.RenderScaling,
                                    IsDelta: snapshot.IsDelta,
                                    HasChanges: snapshot.HasChanges,
                                    FrameHash: snapshot.FrameHash,
                                    PreviousFrameHash: snapshot.PreviousFrameHash,
                                    DiffKind: snapshot.DiffKind,
                                    ChangedRegions: snapshot.ChangedRegions,
                                    FrameData: snapshot.FrameData));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("InProcessRemoteStreamSource preview sampling failed: " + ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _previewCaptureScheduled, 0);
                }
            },
            DispatcherPriority.Background);
    }

    private void OnProfilerTimerTick()
    {
        if (!ShouldEmitProfilerStream())
        {
            return;
        }

        try
        {
            var sample = _profilerSampler.CaptureSample();
            PublishObject(
                RemoteStreamTopics.Profiler,
                new RemoteProfilerStreamPayload(
                    TimestampUtc: sample.Timestamp,
                    Source: "local",
                    SessionId: _localSessionId,
                    Process: _localProcessName,
                    CpuPercent: sample.CpuPercent,
                    WorkingSetMb: sample.WorkingSetMb,
                    PrivateMemoryMb: sample.PrivateMemoryMb,
                    ManagedHeapMb: sample.ManagedHeapMb,
                    Gen0Collections: sample.Gen0Collections,
                    Gen1Collections: sample.Gen1Collections,
                    Gen2Collections: sample.Gen2Collections,
                    ActivitySource: string.Empty,
                    ActivityName: string.Empty,
                    DurationMs: 0,
                    Tags: s_emptyTags));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("InProcessRemoteStreamSource profiler sampling failed: " + ex.Message);
        }
    }

    private void OnUdpPacketReceived(TelemetryPacket packet, System.Net.IPEndPoint endpoint)
    {
        switch (packet)
        {
            case TelemetryHello hello:
                lock (_gate)
                {
                    if (_udpSessions.Count >= _options.MaxUdpSessions && !_udpSessions.ContainsKey(hello.SessionId))
                    {
                        return;
                    }

                    _udpSessions[hello.SessionId] = new RemoteUdpSessionInfo(
                        hello.ProcessId,
                        hello.ProcessName,
                        hello.AppName);
                }

                break;
            case TelemetryMetric metric:
                if (!_options.EnableMetricsStream)
                {
                    break;
                }

                HandleIncomingMetricPayload(
                    new RemoteMetricStreamPayload(
                        TimestampUtc: metric.Timestamp,
                        Source: "udp",
                        SessionId: metric.SessionId,
                        MeterName: metric.MeterName,
                        InstrumentName: metric.InstrumentName,
                        Description: metric.Description,
                        Unit: metric.Unit,
                        InstrumentType: metric.InstrumentType,
                        Value: metric.Value.AsDouble(),
                        Tags: ConvertTags(metric.Tags)));
                break;
            case TelemetryActivity activity:
                if (!_options.EnableProfilerStream)
                {
                    break;
                }

                if (!ShouldEmitProfilerStream())
                {
                    break;
                }

                PublishObject(
                    RemoteStreamTopics.Profiler,
                    new RemoteProfilerStreamPayload(
                        TimestampUtc: activity.Timestamp,
                        Source: "udp",
                        SessionId: activity.SessionId,
                        Process: ResolveUdpProcessName(activity.SessionId),
                        CpuPercent: 0,
                        WorkingSetMb: 0,
                        PrivateMemoryMb: 0,
                        ManagedHeapMb: 0,
                        Gen0Collections: 0,
                        Gen1Collections: 0,
                        Gen2Collections: 0,
                        ActivitySource: activity.SourceName,
                        ActivityName: activity.Name,
                        DurationMs: activity.Duration.TotalMilliseconds < 0 ? 0 : activity.Duration.TotalMilliseconds,
                        Tags: ConvertTags(activity.Tags)));
                break;
        }
    }

    private string ResolveUdpProcessName(Guid sessionId)
    {
        lock (_gate)
        {
            if (_udpSessions.TryGetValue(sessionId, out var info))
            {
                return info.ProcessName + "(" + info.ProcessId + ")";
            }
        }

        return "remote";
    }

    private void PublishObject<TPayload>(string topic, TPayload payload, bool recordForSnapshots = true)
    {
        var domain = ResolveDomainForTopic(topic);
        var source = ResolveStreamSource(payload);
        if (recordForSnapshots)
        {
            RecordPayloadForSnapshots(topic, payload);
        }

        var payloadJson = JsonSerializer.Serialize(payload, s_jsonSerializerOptions);
        PublishRaw(new RemoteStreamPayload(topic, payloadJson));
        RemoteRuntimeMetrics.RecordStreamPublish(domain, source);
    }

    private void PublishRaw(RemoteStreamPayload payload)
    {
        Action<RemoteStreamPayload>[] subscribers;
        lock (_gate)
        {
            if (_subscribers.Count == 0 || _isDisposed)
            {
                return;
            }

            subscribers = _subscribers.ToArray();
        }

        for (var i = 0; i < subscribers.Length; i++)
        {
            try
            {
                subscribers[i](payload);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InProcessRemoteStreamSource subscriber failed: " + ex);
            }
        }
    }

    private void Unsubscribe(Action<RemoteStreamPayload> callback)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _subscribers.Remove(callback);
            if (_subscribers.Count == 0)
            {
                StopCore();
            }
        }
    }

    private static IReadOnlyList<RemoteStreamTag> ConvertTags(IReadOnlyList<MetricCaptureService.MetricTag> tags)
    {
        if (tags.Count == 0)
        {
            return s_emptyTags;
        }

        var converted = new RemoteStreamTag[tags.Count];
        for (var i = 0; i < tags.Count; i++)
        {
            converted[i] = new RemoteStreamTag(tags[i].Key, FormatValue(tags[i].Value));
        }

        return converted;
    }

    private static IReadOnlyList<RemoteStreamTag> ConvertTags(IReadOnlyList<TelemetryTag> tags)
    {
        if (tags.Count == 0)
        {
            return s_emptyTags;
        }

        var converted = new RemoteStreamTag[tags.Count];
        for (var i = 0; i < tags.Count; i++)
        {
            converted[i] = new RemoteStreamTag(tags[i].Key, FormatValue(tags[i].Value));
        }

        return converted;
    }

    private static string DescribeTarget(object? source)
    {
        if (source is null)
        {
            return string.Empty;
        }

        if (source is INamed named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return source.GetType().Name + "#" + named.Name;
        }

        return source.GetType().Name;
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value.ToString() ?? string.Empty;
    }

    private void RecordPayloadForSnapshots<TPayload>(string topic, TPayload payload)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            switch (topic)
            {
                case RemoteStreamTopics.Metrics when payload is RemoteMetricStreamPayload metricPayload:
                    RecordMetricPayload(metricPayload);
                    break;
                case RemoteStreamTopics.Profiler when payload is RemoteProfilerStreamPayload profilerPayload:
                    RecordProfilerPayload(profilerPayload);
                    break;
            }
        }
    }

    private void RecordMetricPayload(RemoteMetricStreamPayload payload)
    {
        _totalMetricMeasurements++;
        var entry = new MetricHistoryEntry(Sequence: ++_nextMetricSequence, Payload: payload);
        _metricHistory.Add(entry);
        AddMetricSeriesSample(entry);
        if (_metricHistory.Count > _maxRetainedMeasurements)
        {
            var removeCount = _metricHistory.Count - _maxRetainedMeasurements;
            for (var i = 0; i < removeCount; i++)
            {
                RemoveMetricSeriesSample(_metricHistory[i]);
            }

            _metricHistory.RemoveRange(0, removeCount);
            _droppedMetricMeasurements += removeCount;
            RemoteRuntimeMetrics.RecordStreamDropped("metrics", removeCount, payload.Source);
        }

        _metricsGeneration++;
    }

    private void RecordProfilerPayload(RemoteProfilerStreamPayload payload)
    {
        _totalProfilerSamples++;
        _profilerHistory.Add(payload);
        _profilerCurrentCpuPercent = payload.CpuPercent;
        _profilerCurrentWorkingSetMb = payload.WorkingSetMb;
        _profilerCurrentManagedHeapMb = payload.ManagedHeapMb;
        _profilerCurrentActivityDurationMs = payload.DurationMs;
        if (payload.CpuPercent > _profilerPeakCpuPercent)
        {
            _profilerPeakCpuPercent = payload.CpuPercent;
        }

        if (payload.WorkingSetMb > _profilerPeakWorkingSetMb)
        {
            _profilerPeakWorkingSetMb = payload.WorkingSetMb;
        }

        if (payload.DurationMs > _profilerPeakActivityDurationMs)
        {
            _profilerPeakActivityDurationMs = payload.DurationMs;
        }

        if (_profilerHistory.Count > _maxRetainedProfilerSamples)
        {
            var removeCount = _profilerHistory.Count - _maxRetainedProfilerSamples;
            var removedPeakCandidate = false;
            for (var i = 0; i < removeCount; i++)
            {
                var removed = _profilerHistory[i];
                if (removed.CpuPercent >= _profilerPeakCpuPercent ||
                    removed.WorkingSetMb >= _profilerPeakWorkingSetMb ||
                    removed.DurationMs >= _profilerPeakActivityDurationMs)
                {
                    removedPeakCandidate = true;
                }
            }

            _profilerHistory.RemoveRange(0, removeCount);
            _droppedProfilerSamples += removeCount;
            RemoteRuntimeMetrics.RecordStreamDropped("profiler", removeCount, payload.Source);
            if (removedPeakCandidate)
            {
                RecomputeProfilerAggregates();
            }
        }

        _profilerGeneration++;
    }

    private void AddMetricSeriesSample(MetricHistoryEntry entry)
    {
        if (_maxSeries <= 0 || _maxSamplesPerSeries <= 0)
        {
            return;
        }

        var payload = entry.Payload;
        var key = BuildMetricSeriesKey(payload);
        if (!_metricSeriesByKey.TryGetValue(key, out var state))
        {
            state = new MetricSeriesState(payload);
            _metricSeriesByKey[key] = state;
            state.OrderNode = _metricSeriesOrder.AddLast(key);
        }
        else
        {
            TouchMetricSeriesOrder(state);
        }

        state.Add(entry.Sequence, payload.Value, payload.TimestampUtc, _maxSamplesPerSeries);
        if (_metricSeriesByKey.Count > _maxSeries)
        {
            TrimMetricSeriesToMaxSeries();
        }
    }

    private void RemoveMetricSeriesSample(MetricHistoryEntry entry)
    {
        var payload = entry.Payload;
        var key = BuildMetricSeriesKey(payload);
        if (!_metricSeriesByKey.TryGetValue(key, out var state))
        {
            return;
        }

        state.RemoveUpTo(entry.Sequence);
        if (state.SampleCount > 0)
        {
            return;
        }

        RemoveMetricSeriesState(key, state);
    }

    private void PruneMetricSeriesState()
    {
        if (_metricSeriesByKey.Count == 0)
        {
            return;
        }

        if (_maxSamplesPerSeries <= 0 || _maxSeries <= 0)
        {
            _metricSeriesByKey.Clear();
            _metricSeriesOrder.Clear();
            return;
        }

        var keysToRemove = new List<string>();
        foreach (var pair in _metricSeriesByKey)
        {
            var state = pair.Value;
            state.TrimToMaxSamples(_maxSamplesPerSeries);
            if (state.SampleCount == 0)
            {
                keysToRemove.Add(pair.Key);
            }
        }

        for (var i = 0; i < keysToRemove.Count; i++)
        {
            var key = keysToRemove[i];
            if (_metricSeriesByKey.TryGetValue(key, out var state))
            {
                RemoveMetricSeriesState(key, state);
            }
        }

        TrimMetricSeriesToMaxSeries();
    }

    private void RebuildMetricSeriesStateFromHistory()
    {
        _metricSeriesByKey.Clear();
        _metricSeriesOrder.Clear();
        if (_maxSeries <= 0 || _maxSamplesPerSeries <= 0 || _metricHistory.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _metricHistory.Count; i++)
        {
            AddMetricSeriesSample(_metricHistory[i]);
        }
    }

    private void TrimMetricSeriesToMaxSeries()
    {
        while (_metricSeriesByKey.Count > _maxSeries && _metricSeriesOrder.First is { } oldest)
        {
            var key = oldest.Value;
            if (_metricSeriesByKey.TryGetValue(key, out var state))
            {
                RemoveMetricSeriesState(key, state);
            }
            else
            {
                _metricSeriesOrder.RemoveFirst();
            }
        }
    }

    private void RemoveMetricSeriesState(string key, MetricSeriesState state)
    {
        if (state.OrderNode is { List: not null })
        {
            _metricSeriesOrder.Remove(state.OrderNode);
        }

        _metricSeriesByKey.Remove(key);
    }

    private void TouchMetricSeriesOrder(MetricSeriesState state)
    {
        var node = state.OrderNode;
        if (node is null || node.List is null)
        {
            return;
        }

        _metricSeriesOrder.Remove(node);
        _metricSeriesOrder.AddLast(node);
    }

    private void RecomputeProfilerAggregates()
    {
        if (_profilerHistory.Count == 0)
        {
            _profilerCurrentCpuPercent = 0d;
            _profilerPeakCpuPercent = 0d;
            _profilerCurrentWorkingSetMb = 0d;
            _profilerPeakWorkingSetMb = 0d;
            _profilerCurrentManagedHeapMb = 0d;
            _profilerCurrentActivityDurationMs = 0d;
            _profilerPeakActivityDurationMs = 0d;
            return;
        }

        var last = _profilerHistory[_profilerHistory.Count - 1];
        _profilerCurrentCpuPercent = last.CpuPercent;
        _profilerCurrentWorkingSetMb = last.WorkingSetMb;
        _profilerCurrentManagedHeapMb = last.ManagedHeapMb;
        _profilerCurrentActivityDurationMs = last.DurationMs;

        var peakCpu = 0d;
        var peakWorkingSet = 0d;
        var peakDuration = 0d;
        for (var i = 0; i < _profilerHistory.Count; i++)
        {
            var sample = _profilerHistory[i];
            if (sample.CpuPercent > peakCpu)
            {
                peakCpu = sample.CpuPercent;
            }

            if (sample.WorkingSetMb > peakWorkingSet)
            {
                peakWorkingSet = sample.WorkingSetMb;
            }

            if (sample.DurationMs > peakDuration)
            {
                peakDuration = sample.DurationMs;
            }
        }

        _profilerPeakCpuPercent = peakCpu;
        _profilerPeakWorkingSetMb = peakWorkingSet;
        _profilerPeakActivityDurationMs = peakDuration;
    }

    private static string ResolveDomainForTopic(string topic)
    {
        return topic switch
        {
            RemoteStreamTopics.Selection => "tree",
            RemoteStreamTopics.Logs => "logs",
            RemoteStreamTopics.Events => "events",
            RemoteStreamTopics.Metrics => "metrics",
            RemoteStreamTopics.Profiler => "profiler",
            RemoteStreamTopics.Preview => "preview",
            _ => "none",
        };
    }

    private static string ResolveStreamSource<TPayload>(TPayload payload)
    {
        return payload switch
        {
            RemoteMetricStreamPayload metric => metric.Source,
            RemoteProfilerStreamPayload profiler => profiler.Source,
            _ => "local",
        };
    }

    private static RemoteMetricMeasurementSnapshot MapMetricMeasurement(RemoteMetricStreamPayload payload)
    {
        return new RemoteMetricMeasurementSnapshot(
            Id: CreateSnapshotId(
                "metric-measurement",
                payload.Source,
                payload.SessionId.ToString("N"),
                payload.MeterName,
                payload.InstrumentName,
                payload.TimestampUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
                payload.Value.ToString("R", CultureInfo.InvariantCulture),
                BuildTagSignature(payload.Tags)),
            TimestampUtc: payload.TimestampUtc,
            Source: payload.Source,
            SessionId: payload.SessionId,
            MeterName: payload.MeterName,
            InstrumentName: payload.InstrumentName,
            Description: payload.Description,
            Unit: payload.Unit,
            InstrumentType: payload.InstrumentType,
            Value: payload.Value,
            Tags: payload.Tags);
    }

    private static RemoteProfilerSampleSnapshot MapProfilerSample(RemoteProfilerStreamPayload payload)
    {
        return new RemoteProfilerSampleSnapshot(
            Id: CreateSnapshotId(
                "profiler-sample",
                payload.Source,
                payload.SessionId.ToString("N"),
                payload.Process,
                payload.ActivitySource,
                payload.ActivityName,
                payload.TimestampUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
                payload.DurationMs.ToString("R", CultureInfo.InvariantCulture)),
            TimestampUtc: payload.TimestampUtc,
            Source: payload.Source,
            SessionId: payload.SessionId,
            Process: payload.Process,
            CpuPercent: payload.CpuPercent,
            WorkingSetMb: payload.WorkingSetMb,
            PrivateMemoryMb: payload.PrivateMemoryMb,
            ManagedHeapMb: payload.ManagedHeapMb,
            Gen0Collections: payload.Gen0Collections,
            Gen1Collections: payload.Gen1Collections,
            Gen2Collections: payload.Gen2Collections,
            ActivitySource: payload.ActivitySource,
            ActivityName: payload.ActivityName,
            DurationMs: payload.DurationMs,
            Tags: payload.Tags);
    }

    private RemoteMetricSeriesSnapshot[] BuildMetricSeries()
    {
        if (_metricSeriesByKey.Count == 0 || _maxSeries <= 0 || _maxSamplesPerSeries <= 0)
        {
            return Array.Empty<RemoteMetricSeriesSnapshot>();
        }

        var result = new List<RemoteMetricSeriesSnapshot>(_metricSeriesByKey.Count);
        for (var node = _metricSeriesOrder.Last; node is not null; node = node.Previous)
        {
            if (!_metricSeriesByKey.TryGetValue(node.Value, out var state))
            {
                continue;
            }

            if (state.SampleCount <= 0)
            {
                continue;
            }

            result.Add(state.ToSnapshot());
        }

        if (result.Count == 0)
        {
            return Array.Empty<RemoteMetricSeriesSnapshot>();
        }

        return result.ToArray();
    }

    private static string BuildMetricSeriesKey(RemoteMetricStreamPayload payload)
    {
        return payload.MeterName
               + "|"
               + payload.InstrumentName
               + "|"
               + payload.Description
               + "|"
               + payload.Unit
               + "|"
               + payload.InstrumentType
               + "|"
               + BuildTagSignature(payload.Tags);
    }

    private static string BuildTagSignature(IReadOnlyList<RemoteStreamTag> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(tags.Count * 16);
        for (var i = 0; i < tags.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append(tags[i].Key);
            builder.Append('=');
            builder.Append(tags[i].Value);
        }

        return builder.ToString();
    }

    private static string CreateSnapshotId(string prefix, params string[] parts)
    {
        var builder = new StringBuilder(prefix.Length + 64);
        builder.Append(prefix);
        for (var i = 0; i < parts.Length; i++)
        {
            builder.Append('|');
            builder.Append(parts[i]);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return prefix + "-" + Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }

    private RemotePreviewSnapshot CapturePreviewSnapshotCore(
        RemotePreviewSnapshotRequest request,
        string? previousFrameHash,
        bool isStreaming)
    {
        var topLevel = ResolvePreviewTopLevel();
        if (topLevel is null)
        {
            return new RemotePreviewSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _previewGeneration,
                Status: "Preview target is unavailable.",
                Transport: NormalizePreviewTransport(request.Transport),
                MimeType: string.Empty,
                Width: 0,
                Height: 0,
                Scale: request.Scale,
                RenderScaling: 1d,
                CapturedAtUtc: DateTimeOffset.UtcNow,
                IsPaused: IsPreviewPaused,
                IsDelta: false,
                HasChanges: false,
                FrameHash: string.Empty,
                PreviousFrameHash: previousFrameHash,
                DiffKind: "none",
                ChangedRegions: Array.Empty<RemotePreviewRectSnapshot>(),
                FrameData: null);
        }

        EnsurePreviewSceneSubscription(topLevel);

        var requestedTransport = NormalizePreviewTransport(request.Transport);
        var maxWidth = request.MaxWidth > 0 ? request.MaxWidth : _previewMaxWidth;
        var maxHeight = request.MaxHeight > 0 ? request.MaxHeight : _previewMaxHeight;
        var requestedScale = ClampPreviewScale(request.Scale <= 0 ? _previewScale : request.Scale);
        if (isStreaming && _previewAdaptiveScaleFactor < 1d)
        {
            requestedScale = ClampPreviewScale(requestedScale * _previewAdaptiveScaleFactor);
        }

        var enableDiff = request.EnableDiff;
        var includeFrameData = request.IncludeFrameData;

        var status = "ok";
        var renderScaling = topLevel.RenderScaling;
        var logicalWidth = Math.Max(1d, topLevel.Bounds.Width);
        var logicalHeight = Math.Max(1d, topLevel.Bounds.Height);

        var width = Math.Max(1, (int)Math.Round(logicalWidth * requestedScale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(logicalHeight * requestedScale, MidpointRounding.AwayFromZero));
        if (maxWidth > 0 && width > maxWidth)
        {
            var ratio = (double)maxWidth / width;
            width = maxWidth;
            height = Math.Max(1, (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero));
        }

        if (maxHeight > 0 && height > maxHeight)
        {
            var ratio = (double)maxHeight / height;
            height = maxHeight;
            width = Math.Max(1, (int)Math.Round(width * ratio, MidpointRounding.AwayFromZero));
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var isDelta = enableDiff && !string.IsNullOrWhiteSpace(previousFrameHash);
        var sceneVersion = Volatile.Read(ref _previewSceneInvalidationVersion);

        if (isStreaming &&
            isDelta &&
            sceneVersion == Volatile.Read(ref _previewLastCapturedSceneVersion) &&
            !string.IsNullOrWhiteSpace(previousFrameHash))
        {
            return new RemotePreviewSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _previewGeneration,
                Status: status,
                Transport: requestedTransport,
                MimeType: string.Equals(requestedTransport, "png", StringComparison.Ordinal)
                    ? "image/png"
                    : "image/svg+xml",
                Width: width,
                Height: height,
                Scale: requestedScale,
                RenderScaling: renderScaling,
                CapturedAtUtc: capturedAtUtc,
                IsPaused: IsPreviewPaused,
                IsDelta: true,
                HasChanges: false,
                FrameHash: previousFrameHash!,
                PreviousFrameHash: previousFrameHash,
                DiffKind: "full-frame",
                ChangedRegions: Array.Empty<RemotePreviewRectSnapshot>(),
                FrameData: null);
        }

        var captureStarted = Stopwatch.GetTimestamp();
        string frameHash;
        string? frameData = null;
        try
        {
            if (string.Equals(requestedTransport, "png", StringComparison.Ordinal))
            {
                var pngBytes = CapturePreviewPngBytes(topLevel, width, height);
                frameHash = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();

                if (includeFrameData)
                {
                    if (TryGetCachedPngFrameData(frameHash, width, height, out var cachedPng))
                    {
                        frameData = cachedPng;
                    }
                    else
                    {
                        frameData = Convert.ToBase64String(pngBytes);
                        StoreCachedPngFrameData(frameHash, width, height, frameData);
                    }
                }
            }
            else
            {
                frameHash = CreateSnapshotId(
                    "preview-scene",
                    sceneVersion.ToString(CultureInfo.InvariantCulture),
                    width.ToString(CultureInfo.InvariantCulture),
                    height.ToString(CultureInfo.InvariantCulture),
                    requestedScale.ToString("R", CultureInfo.InvariantCulture),
                    renderScaling.ToString("R", CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex)
        {
            status = "Preview capture failed: " + ex.Message;
            return new RemotePreviewSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _previewGeneration,
                Status: status,
                Transport: requestedTransport,
                MimeType: string.Empty,
                Width: width,
                Height: height,
                Scale: requestedScale,
                RenderScaling: renderScaling,
                CapturedAtUtc: capturedAtUtc,
                IsPaused: IsPreviewPaused,
                IsDelta: false,
                HasChanges: false,
                FrameHash: string.Empty,
                PreviousFrameHash: previousFrameHash,
                DiffKind: "none",
                ChangedRegions: Array.Empty<RemotePreviewRectSnapshot>(),
                FrameData: null);
        }

        var hasChanges = !string.Equals(previousFrameHash, frameHash, StringComparison.Ordinal);
        var shouldEmitFrameData = includeFrameData && (!isDelta || hasChanges || !isStreaming);
        if (shouldEmitFrameData &&
            string.Equals(requestedTransport, "svg", StringComparison.Ordinal))
        {
            if (TryGetCachedSvgFrameData(sceneVersion, width, height, out var cachedSvg))
            {
                frameData = cachedSvg;
            }
            else
            {
                var svg = PreviewSvgVectorExporter.Export(topLevel, width, height);
                if (!string.IsNullOrWhiteSpace(svg))
                {
                    frameData = svg;
                    StoreCachedSvgFrameData(sceneVersion, width, height, svg);
                }
                else
                {
                    var pngBytes = CapturePreviewPngBytes(topLevel, width, height);
                    frameData = WrapPngInSvg(width, height, Convert.ToBase64String(pngBytes));
                    StoreCachedSvgFrameData(sceneVersion, width, height, frameData);
                }
            }
        }

        if (!shouldEmitFrameData)
        {
            frameData = null;
        }

        var changedRegions = hasChanges
            ? new RemotePreviewRectSnapshot[]
            {
                new(
                    X: 0,
                    Y: 0,
                    Width: width,
                    Height: height)
            }
            : Array.Empty<RemotePreviewRectSnapshot>();

        if (hasChanges)
        {
            _previewGeneration++;
        }

        _previewLastCapturedSceneVersion = sceneVersion;
        if (isStreaming)
        {
            UpdatePreviewAdaptiveScale(RemoteRuntimeMetrics.ElapsedMilliseconds(captureStarted));
        }

        return new RemotePreviewSnapshot(
            SnapshotVersion: SnapshotVersion,
            Generation: _previewGeneration,
            Status: status,
            Transport: requestedTransport,
            MimeType: string.Equals(requestedTransport, "png", StringComparison.Ordinal)
                ? "image/png"
                : "image/svg+xml",
            Width: width,
            Height: height,
            Scale: requestedScale,
            RenderScaling: renderScaling,
            CapturedAtUtc: capturedAtUtc,
            IsPaused: IsPreviewPaused,
            IsDelta: isDelta,
            HasChanges: hasChanges,
            FrameHash: frameHash,
            PreviousFrameHash: previousFrameHash,
            DiffKind: isDelta ? "full-frame" : "none",
            ChangedRegions: changedRegions,
            FrameData: frameData);
    }

    private bool ShouldSkipPreviewTimerTick()
    {
        var lastCaptureTicks = Volatile.Read(ref _previewLastCaptureTicks);
        if (lastCaptureTicks <= 0)
        {
            return false;
        }

        var minTicks = Math.Max(1L, Stopwatch.Frequency / Math.Max(1, _previewTargetFps));
        var elapsed = Stopwatch.GetTimestamp() - lastCaptureTicks;
        return elapsed < minTicks;
    }

    private void EnsurePreviewSceneSubscription()
    {
        var topLevel = ResolvePreviewTopLevel();
        if (topLevel is null)
        {
            DisablePreviewSceneSubscription();
            return;
        }

        EnsurePreviewSceneSubscription(topLevel);
    }

    private void EnsurePreviewSceneSubscription(TopLevel topLevel)
    {
        var renderer = topLevel.Renderer;
        if (ReferenceEquals(_previewRenderer, renderer))
        {
            return;
        }

        DisablePreviewSceneSubscription();
        _previewRenderer = renderer;
        _previewSceneInvalidatedHandler ??= OnPreviewSceneInvalidated;
        _previewRenderer.SceneInvalidated += _previewSceneInvalidatedHandler;
        Interlocked.Increment(ref _previewSceneInvalidationVersion);
    }

    private void DisablePreviewSceneSubscription()
    {
        if (_previewRenderer is null || _previewSceneInvalidatedHandler is null)
        {
            _previewRenderer = null;
            return;
        }

        _previewRenderer.SceneInvalidated -= _previewSceneInvalidatedHandler;
        _previewRenderer = null;
    }

    private void OnPreviewSceneInvalidated(object? sender, SceneInvalidatedEventArgs e)
    {
        Interlocked.Increment(ref _previewSceneInvalidationVersion);
    }

    private static byte[] CapturePreviewPngBytes(TopLevel topLevel, int width, int height)
    {
        using var renderTarget = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        renderTarget.Render(topLevel);
        using var pngStream = new MemoryStream();
        renderTarget.Save(pngStream);
        return pngStream.ToArray();
    }

    private static string WrapPngInSvg(int width, int height, string pngBase64)
    {
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\""
               + width.ToString(CultureInfo.InvariantCulture)
               + "\" height=\""
               + height.ToString(CultureInfo.InvariantCulture)
               + "\" viewBox=\"0 0 "
               + width.ToString(CultureInfo.InvariantCulture)
               + " "
               + height.ToString(CultureInfo.InvariantCulture)
               + "\"><image width=\""
               + width.ToString(CultureInfo.InvariantCulture)
               + "\" height=\""
               + height.ToString(CultureInfo.InvariantCulture)
               + "\" href=\"data:image/png;base64,"
               + pngBase64
               + "\"/></svg>";
    }

    private bool TryGetCachedSvgFrameData(long sceneVersion, int width, int height, out string frameData)
    {
        if (_previewSvgFrameCache is
            {
                SceneVersion: var cachedSceneVersion,
                Width: var cachedWidth,
                Height: var cachedHeight,
                FrameData: var cachedFrameData
            } &&
            cachedSceneVersion == sceneVersion &&
            cachedWidth == width &&
            cachedHeight == height)
        {
            frameData = cachedFrameData;
            return true;
        }

        frameData = string.Empty;
        return false;
    }

    private void StoreCachedSvgFrameData(long sceneVersion, int width, int height, string frameData)
    {
        _previewSvgFrameCache = new PreviewSvgFrameCacheEntry(
            SceneVersion: sceneVersion,
            Width: width,
            Height: height,
            FrameData: frameData);
    }

    private bool TryGetCachedPngFrameData(string frameHash, int width, int height, out string frameData)
    {
        if (_previewPngFrameCache is
            {
                FrameHash: var cachedHash,
                Width: var cachedWidth,
                Height: var cachedHeight,
                FrameData: var cachedFrameData
            } &&
            string.Equals(cachedHash, frameHash, StringComparison.Ordinal) &&
            cachedWidth == width &&
            cachedHeight == height)
        {
            frameData = cachedFrameData;
            return true;
        }

        frameData = string.Empty;
        return false;
    }

    private void StoreCachedPngFrameData(string frameHash, int width, int height, string frameData)
    {
        _previewPngFrameCache = new PreviewPngFrameCacheEntry(
            FrameHash: frameHash,
            Width: width,
            Height: height,
            FrameData: frameData);
    }

    private void ResetPreviewEncodedFrameCache()
    {
        _previewSvgFrameCache = null;
        _previewPngFrameCache = null;
    }

    private void UpdatePreviewAdaptiveScale(double captureDurationMs)
    {
        if (!double.IsFinite(captureDurationMs) || captureDurationMs <= 0)
        {
            return;
        }

        var frameBudgetMs = 1000d / Math.Max(1, _previewTargetFps);
        if (captureDurationMs > frameBudgetMs * 1.2d)
        {
            _previewSlowCaptureStreak++;
            _previewFastCaptureStreak = 0;
            if (_previewSlowCaptureStreak >= 3)
            {
                _previewAdaptiveScaleFactor = Math.Max(0.4d, _previewAdaptiveScaleFactor * 0.9d);
                _previewSlowCaptureStreak = 0;
            }

            return;
        }

        if (captureDurationMs < frameBudgetMs * 0.55d)
        {
            _previewFastCaptureStreak++;
            _previewSlowCaptureStreak = 0;
            if (_previewFastCaptureStreak >= 5)
            {
                _previewAdaptiveScaleFactor = Math.Min(1d, _previewAdaptiveScaleFactor * 1.05d);
                _previewFastCaptureStreak = 0;
            }

            return;
        }

        _previewSlowCaptureStreak = 0;
        _previewFastCaptureStreak = 0;
    }

    private TopLevel? ResolvePreviewTopLevel()
    {
        if (_root is PopupRoot popupRoot && popupRoot.ParentTopLevel is { } popupParent)
        {
            return popupParent;
        }

        if (_root is TopLevel topLevel)
        {
            return topLevel;
        }

        if (_root is Visual visual)
        {
            return TopLevel.GetTopLevel(visual);
        }

        return null;
    }

    private static string NormalizePreviewTransport(string? transport)
    {
        if (string.Equals(transport, "png", StringComparison.OrdinalIgnoreCase))
        {
            return "png";
        }

        return "svg";
    }

    private static double ClampPreviewScale(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return 1d;
        }

        return Math.Clamp(scale, 0.1d, 4d);
    }

    private bool IsTopicDemanded(string topic)
    {
        lock (_gate)
        {
            return !_isDisposed &&
                   _isStarted &&
                   _streamDemand.IsDemanded(topic);
        }
    }

    private bool ShouldEmitPreviewStream()
    {
        lock (_gate)
        {
            return ShouldEmitPreviewStreamLocked();
        }
    }

    private bool ShouldEmitProfilerStream()
    {
        lock (_gate)
        {
            return ShouldEmitProfilerStreamLocked();
        }
    }

    private bool ShouldEmitPreviewStreamLocked()
    {
        return !_isDisposed &&
               _isStarted &&
               !IsPreviewPaused &&
               _streamDemand.Preview;
    }

    private bool ShouldEmitMetricsStreamLocked()
    {
        return !_isDisposed &&
               _isStarted &&
               _options.EnableMetricsStream &&
               !IsMetricsPaused &&
               _streamDemand.Metrics;
    }

    private bool ShouldEmitProfilerStreamLocked()
    {
        return !_isDisposed &&
               _isStarted &&
               _options.EnableProfilerStream &&
               !IsProfilerPaused &&
               _streamDemand.Profiler;
    }

    private void SyncStreamProducersLocked()
    {
        SyncPreviewProducerLocked();
        SyncMetricsProducerLocked();
        SyncProfilerProducerLocked();
    }

    private void SyncPreviewProducerLocked()
    {
        if (ShouldEmitPreviewStreamLocked())
        {
            EnsurePreviewTimer();
        }
        else
        {
            DisablePreviewTimer();
        }
    }

    private void SyncMetricsProducerLocked()
    {
        if (ShouldEmitMetricsStreamLocked())
        {
            EnsureMetricsSubscription();
        }
        else
        {
            DisableMetricsSubscription();
        }
    }

    private void SyncProfilerProducerLocked()
    {
        if (ShouldEmitProfilerStreamLocked())
        {
            EnsureProfilerTimer();
        }
        else
        {
            DisableProfilerTimer();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(InProcessRemoteStreamSource));
        }
    }

    private readonly record struct RemoteUdpSessionInfo(int ProcessId, string ProcessName, string AppName);

    private void EnsurePreviewTimer()
    {
        if (!ShouldEmitPreviewStreamLocked() || _previewTimer is not null)
        {
            return;
        }

        var intervalMs = (int)Math.Max(16, Math.Round(1000d / Math.Max(1, _previewTargetFps)));
        var interval = TimeSpan.FromMilliseconds(intervalMs);
        _previewTimer = new Timer(
            static state => ((InProcessRemoteStreamSource)state!).OnPreviewTimerTick(),
            this,
            dueTime: interval,
            period: interval);
    }

    private void DisablePreviewTimer()
    {
        _previewTimer?.Dispose();
        _previewTimer = null;
        Interlocked.Exchange(ref _previewCaptureScheduled, 0);
    }

    private void EnsureMetricsSubscription()
    {
        if (!ShouldEmitMetricsStreamLocked() || _metricsSubscription is not null)
        {
            return;
        }

        _metricsSubscription = MetricCaptureService.Subscribe(OnMetricCaptured);
    }

    private void DisableMetricsSubscription()
    {
        _metricsSubscription?.Dispose();
        _metricsSubscription = null;
        _pendingMetricPayload = null;
        _nextMetricPublishTicksUtc = 0;
        DisableMetricsCoalesceTimer();
    }

    private void EnsureProfilerTimer()
    {
        if (!ShouldEmitProfilerStreamLocked() || _profilerTimer is not null)
        {
            return;
        }

        _profilerTimer = new Timer(
            static state => ((InProcessRemoteStreamSource)state!).OnProfilerTimerTick(),
            this,
            dueTime: _profilerSampleInterval,
            period: _profilerSampleInterval);
    }

    private void DisableProfilerTimer()
    {
        _profilerTimer?.Dispose();
        _profilerTimer = null;
    }

    private sealed class Subscription : IDisposable
    {
        private InProcessRemoteStreamSource? _owner;
        private Action<RemoteStreamPayload>? _callback;

        public Subscription(InProcessRemoteStreamSource owner, Action<RemoteStreamPayload> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            var callback = Interlocked.Exchange(ref _callback, null);
            if (owner is null || callback is null)
            {
                return;
            }

            owner.Unsubscribe(callback);
        }
    }

    private readonly record struct PreviewSvgFrameCacheEntry(
        long SceneVersion,
        int Width,
        int Height,
        string FrameData);

    private readonly record struct PreviewPngFrameCacheEntry(
        string FrameHash,
        int Width,
        int Height,
        string FrameData);

    private readonly record struct MetricHistoryEntry(long Sequence, RemoteMetricStreamPayload Payload);

    private readonly record struct MetricSeriesSample(long Sequence, double Value);

    private sealed class MetricSeriesState
    {
        private readonly Queue<MetricSeriesSample> _samples = new();

        public MetricSeriesState(RemoteMetricStreamPayload payload)
        {
            Id = CreateSnapshotId(
                "metric-series",
                payload.Source,
                payload.SessionId.ToString("N"),
                payload.MeterName,
                payload.InstrumentName,
                payload.Description,
                payload.Unit,
                payload.InstrumentType,
                BuildTagSignature(payload.Tags));
            MeterName = payload.MeterName;
            InstrumentName = payload.InstrumentName;
            Description = payload.Description;
            Unit = payload.Unit;
            InstrumentType = payload.InstrumentType;
            LastValue = payload.Value;
            UpdatedAt = payload.TimestampUtc;
            Tags = payload.Tags;
            MinValue = 0d;
            MaxValue = 0d;
        }

        public LinkedListNode<string>? OrderNode { get; set; }
        public string Id { get; }
        public string MeterName { get; }
        public string InstrumentName { get; }
        public string Description { get; }
        public string Unit { get; }
        public string InstrumentType { get; }
        public IReadOnlyList<RemoteStreamTag> Tags { get; }
        public double LastValue { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public int SampleCount { get; private set; }
        public double Sum { get; private set; }
        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }

        public void Add(long sequence, double value, DateTimeOffset timestampUtc, int maxSamples)
        {
            _samples.Enqueue(new MetricSeriesSample(sequence, value));
            LastValue = value;
            UpdatedAt = timestampUtc;

            SampleCount++;
            Sum += value;
            if (SampleCount == 1)
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

            TrimToMaxSamples(maxSamples);
        }

        public void TrimToMaxSamples(int maxSamples)
        {
            if (maxSamples <= 0)
            {
                _samples.Clear();
                SampleCount = 0;
                Sum = 0d;
                MinValue = 0d;
                MaxValue = 0d;
                return;
            }

            while (_samples.Count > maxSamples)
            {
                RemoveOldestSample();
            }
        }

        public void RemoveUpTo(long sequence)
        {
            while (_samples.Count > 0 && _samples.Peek().Sequence <= sequence)
            {
                RemoveOldestSample();
                if (SampleCount == 0)
                {
                    return;
                }

                if (_samples.Count > 0 && _samples.Peek().Sequence > sequence)
                {
                    return;
                }
            }
        }

        public RemoteMetricSeriesSnapshot ToSnapshot()
        {
            var average = SampleCount == 0 ? 0d : Sum / SampleCount;
            return new RemoteMetricSeriesSnapshot(
                Id: Id,
                MeterName: MeterName,
                InstrumentName: InstrumentName,
                Description: Description,
                Unit: Unit,
                InstrumentType: InstrumentType,
                LastValue: LastValue,
                AverageValue: average,
                MinValue: MinValue,
                MaxValue: MaxValue,
                SampleCount: SampleCount,
                UpdatedAt: UpdatedAt,
                Tags: Tags);
        }

        private void RemoveOldestSample()
        {
            if (_samples.Count == 0)
            {
                return;
            }

            var removed = _samples.Dequeue();
            SampleCount--;
            Sum -= removed.Value;
            if (SampleCount <= 0)
            {
                SampleCount = 0;
                Sum = 0d;
                MinValue = 0d;
                MaxValue = 0d;
                return;
            }

            if (removed.Value <= MinValue || removed.Value >= MaxValue)
            {
                RecomputeExtrema();
            }
        }

        private void RecomputeExtrema()
        {
            if (_samples.Count == 0)
            {
                MinValue = 0d;
                MaxValue = 0d;
                return;
            }

            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            foreach (var sample in _samples)
            {
                if (sample.Value < min)
                {
                    min = sample.Value;
                }

                if (sample.Value > max)
                {
                    max = sample.Value;
                }
            }

            MinValue = min;
            MaxValue = max;
        }
    }
}

internal readonly record struct InProcessRemoteStreamSourceOptions(
    bool EnableMetricsStream,
    bool EnableProfilerStream,
    bool EnableLogsStream,
    bool EnableEventsStream,
    bool EnableUdpTelemetryFallback,
    int UdpPort,
    int MaxUdpSessions,
    int MaxRetainedMeasurements,
    int MaxRetainedProfilerSamples,
    int MaxSeries,
    int MaxSamplesPerSeries,
    int MaxLogStreamPerSecond,
    int MaxEventStreamPerSecond,
    TimeSpan ProfilerSampleInterval,
    int MaxMetricStreamPerSecond = 60)
{
    public static InProcessRemoteStreamSourceOptions Default => new(
        EnableMetricsStream: true,
        EnableProfilerStream: true,
        EnableLogsStream: true,
        EnableEventsStream: true,
        EnableUdpTelemetryFallback: true,
        UdpPort: TelemetryProtocol.DefaultPort,
        MaxUdpSessions: 256,
        MaxRetainedMeasurements: 5000,
        MaxRetainedProfilerSamples: 2000,
        MaxSeries: 500,
        MaxSamplesPerSeries: 250,
        MaxLogStreamPerSecond: 60,
        MaxEventStreamPerSecond: 60,
        ProfilerSampleInterval: TimeSpan.FromSeconds(1),
        MaxMetricStreamPerSecond: 60);

    public static InProcessRemoteStreamSourceOptions Normalize(InProcessRemoteStreamSourceOptions options)
    {
        var udpPort = options.UdpPort;
        if (udpPort < 1)
        {
            udpPort = 1;
        }
        else if (udpPort > 65535)
        {
            udpPort = 65535;
        }

        var maxUdpSessions = options.MaxUdpSessions <= 0 ? 1 : options.MaxUdpSessions;
        var maxRetainedMeasurements = options.MaxRetainedMeasurements <= 0
            ? Default.MaxRetainedMeasurements
            : options.MaxRetainedMeasurements;
        var maxRetainedProfilerSamples = options.MaxRetainedProfilerSamples <= 0
            ? Default.MaxRetainedProfilerSamples
            : options.MaxRetainedProfilerSamples;
        var maxSeries = options.MaxSeries <= 0
            ? Default.MaxSeries
            : options.MaxSeries;
        var maxSamplesPerSeries = options.MaxSamplesPerSeries <= 0
            ? Default.MaxSamplesPerSeries
            : options.MaxSamplesPerSeries;
        var maxLogStreamPerSecond = options.MaxLogStreamPerSecond <= 0
            ? Default.MaxLogStreamPerSecond
            : options.MaxLogStreamPerSecond;
        var maxEventStreamPerSecond = options.MaxEventStreamPerSecond <= 0
            ? Default.MaxEventStreamPerSecond
            : options.MaxEventStreamPerSecond;
        var maxMetricStreamPerSecond = options.MaxMetricStreamPerSecond <= 0
            ? Default.MaxMetricStreamPerSecond
            : options.MaxMetricStreamPerSecond;
        var profilerInterval = options.ProfilerSampleInterval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : options.ProfilerSampleInterval;

        return options with
        {
            UdpPort = udpPort,
            MaxUdpSessions = maxUdpSessions,
            MaxRetainedMeasurements = maxRetainedMeasurements,
            MaxRetainedProfilerSamples = maxRetainedProfilerSamples,
            MaxSeries = maxSeries,
            MaxSamplesPerSeries = maxSamplesPerSeries,
            MaxLogStreamPerSecond = maxLogStreamPerSecond,
            MaxEventStreamPerSecond = maxEventStreamPerSecond,
            ProfilerSampleInterval = profilerInterval,
            MaxMetricStreamPerSecond = maxMetricStreamPerSecond
        };
    }
}
