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
    private readonly int _maxLogStreamPerSecond;
    private readonly int _maxEventStreamPerSecond;
    private readonly Guid _localSessionId = Guid.NewGuid();
    private readonly string _localProcessName;
    private readonly int _localProcessId;
    private readonly Dictionary<Guid, RemoteUdpSessionInfo> _udpSessions = new();
    private readonly List<RemoteMetricStreamPayload> _metricHistory = new();
    private readonly List<RemoteProfilerStreamPayload> _profilerHistory = new();
    private readonly ProcessProfilerSampler _profilerSampler = new();

    private IDisposable? _metricsSubscription;
    private IDisposable? _logsSubscription;
    private DiagnosticsUdpReceiver? _udpReceiver;
    private Timer? _previewTimer;
    private Timer? _profilerTimer;
    private string _previewTransport = "svg";
    private int _previewTargetFps = 5;
    private int _previewMaxWidth = 1920;
    private int _previewMaxHeight = 1080;
    private double _previewScale = 1d;
    private bool _previewEnableDiff = true;
    private bool _previewIncludeFrameData = true;
    private string? _previewLastFrameHash;
    private int _previewCaptureScheduled;
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
    private long _nextLogPublishTicksUtc;
    private long _nextEventPublishTicksUtc;

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

            if (next != 0)
            {
                DisablePreviewTimer();
            }
            else
            {
                EnsurePreviewTimer();
            }

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

            if (next != 0)
            {
                DisableMetricsSubscription();
            }
            else
            {
                EnsureMetricsSubscription();
            }

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

            if (next != 0)
            {
                DisableProfilerTimer();
            }
            else
            {
                EnsureProfilerTimer();
            }

            if (changed)
            {
                _profilerGeneration++;
            }
        }

        return changed;
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
                    .Select(MapMetricMeasurement)
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

            var currentCpu = 0d;
            var peakCpu = 0d;
            var currentWorkingSet = 0d;
            var peakWorkingSet = 0d;
            var currentManagedHeap = 0d;
            var currentDuration = 0d;
            var peakDuration = 0d;
            if (_profilerHistory.Count > 0)
            {
                var last = _profilerHistory[_profilerHistory.Count - 1];
                currentCpu = last.CpuPercent;
                currentWorkingSet = last.WorkingSetMb;
                currentManagedHeap = last.ManagedHeapMb;
                currentDuration = last.DurationMs;

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
            }

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
                CurrentCpuPercent: currentCpu,
                PeakCpuPercent: peakCpu,
                CurrentWorkingSetMb: currentWorkingSet,
                PeakWorkingSetMb: peakWorkingSet,
                CurrentManagedHeapMb: currentManagedHeap,
                CurrentActivityDurationMs: currentDuration,
                PeakActivityDurationMs: peakDuration,
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
                _previewGeneration++;
            }

            if (restartTimer && _isStarted && !IsPreviewPaused)
            {
                DisablePreviewTimer();
                EnsurePreviewTimer();
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
                }
            }

            if (_metricHistory.Count > _maxRetainedMeasurements)
            {
                var removeCount = _metricHistory.Count - _maxRetainedMeasurements;
                _metricHistory.RemoveRange(0, removeCount);
                _droppedMetricMeasurements += removeCount;
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
            }

            if (restartTimer && _isStarted && _options.EnableProfilerStream && !IsProfilerPaused)
            {
                DisableProfilerTimer();
                EnsureProfilerTimer();
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
        PublishObject(RemoteStreamTopics.Selection, selection);
    }

    private void StartCore()
    {
        if (_isStarted || _isDisposed)
        {
            return;
        }

        EnsurePreviewTimer();
        EnsureMetricsSubscription();

        if (_options.EnableLogsStream)
        {
            _logsSubscription = _logCollector.Subscribe(OnLogCaptured);
        }

        if (_options.EnableEventsStream && _eventsPageViewModel is not null)
        {
            _eventsPageViewModel.RecordedEvents.CollectionChanged += OnRecordedEventsChanged;
            _eventsHooked = true;
        }

        EnsureProfilerTimer();

        if (_options.EnableUdpTelemetryFallback)
        {
            TryStartUdpReceiver();
        }

        _isStarted = true;
    }

    private void StopCore()
    {
        if (!_isStarted)
        {
            return;
        }

        DisablePreviewTimer();
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
        if (IsMetricsPaused)
        {
            return;
        }

        var tags = ConvertTags(measurement.Tags);
        PublishObject(
            RemoteStreamTopics.Metrics,
            new RemoteMetricStreamPayload(
                TimestampUtc: measurement.Timestamp,
                Source: "local",
                SessionId: _localSessionId,
                MeterName: measurement.MeterName,
                InstrumentName: measurement.InstrumentName,
                Description: measurement.Description,
                Unit: measurement.Unit,
                InstrumentType: measurement.InstrumentType,
                Value: measurement.Value,
                Tags: tags));
    }

    private void OnLogCaptured(DevToolsLogEvent logEvent)
    {
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
        if (IsPreviewPaused)
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
                        if (_isDisposed || !_isStarted || IsPreviewPaused)
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
        if (IsProfilerPaused)
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

                if (IsMetricsPaused)
                {
                    break;
                }

                PublishObject(
                    RemoteStreamTopics.Metrics,
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

                if (IsProfilerPaused)
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

    private void PublishObject<TPayload>(string topic, TPayload payload)
    {
        RecordPayloadForSnapshots(topic, payload);
        var payloadJson = JsonSerializer.Serialize(payload, s_jsonSerializerOptions);
        PublishRaw(new RemoteStreamPayload(topic, payloadJson));
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
        _metricHistory.Add(payload);
        if (_metricHistory.Count > _maxRetainedMeasurements)
        {
            var removeCount = _metricHistory.Count - _maxRetainedMeasurements;
            _metricHistory.RemoveRange(0, removeCount);
            _droppedMetricMeasurements += removeCount;
        }

        _metricsGeneration++;
    }

    private void RecordProfilerPayload(RemoteProfilerStreamPayload payload)
    {
        _totalProfilerSamples++;
        _profilerHistory.Add(payload);
        if (_profilerHistory.Count > _maxRetainedProfilerSamples)
        {
            var removeCount = _profilerHistory.Count - _maxRetainedProfilerSamples;
            _profilerHistory.RemoveRange(0, removeCount);
            _droppedProfilerSamples += removeCount;
        }

        _profilerGeneration++;
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
        if (_metricHistory.Count == 0 || _maxSeries <= 0 || _maxSamplesPerSeries <= 0)
        {
            return Array.Empty<RemoteMetricSeriesSnapshot>();
        }

        var ordered = new List<MetricSeriesAccumulator>(_maxSeries);
        var lookup = new Dictionary<string, MetricSeriesAccumulator>(StringComparer.Ordinal);
        for (var i = _metricHistory.Count - 1; i >= 0; i--)
        {
            var payload = _metricHistory[i];
            var key = BuildMetricSeriesKey(payload);
            if (!lookup.TryGetValue(key, out var accumulator))
            {
                if (lookup.Count >= _maxSeries)
                {
                    continue;
                }

                accumulator = new MetricSeriesAccumulator(payload);
                lookup[key] = accumulator;
                ordered.Add(accumulator);
            }

            if (accumulator.SampleCount >= _maxSamplesPerSeries)
            {
                continue;
            }

            accumulator.Add(payload.Value);
        }

        var result = new RemoteMetricSeriesSnapshot[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            var accumulator = ordered[i];
            var average = accumulator.SampleCount == 0
                ? 0
                : accumulator.Sum / accumulator.SampleCount;
            result[i] = new RemoteMetricSeriesSnapshot(
                Id: accumulator.Id,
                MeterName: accumulator.MeterName,
                InstrumentName: accumulator.InstrumentName,
                Description: accumulator.Description,
                Unit: accumulator.Unit,
                InstrumentType: accumulator.InstrumentType,
                LastValue: accumulator.LastValue,
                AverageValue: average,
                MinValue: accumulator.MinValue,
                MaxValue: accumulator.MaxValue,
                SampleCount: accumulator.SampleCount,
                UpdatedAt: accumulator.UpdatedAt,
                Tags: accumulator.Tags);
        }

        return result;
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

        var requestedTransport = NormalizePreviewTransport(request.Transport);
        var maxWidth = request.MaxWidth > 0 ? request.MaxWidth : _previewMaxWidth;
        var maxHeight = request.MaxHeight > 0 ? request.MaxHeight : _previewMaxHeight;
        var requestedScale = ClampPreviewScale(request.Scale <= 0 ? _previewScale : request.Scale);
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

        byte[] pngBytes;
        try
        {
            using var renderTarget = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            renderTarget.Render(topLevel);
            using var pngStream = new MemoryStream();
            renderTarget.Save(pngStream);
            pngBytes = pngStream.ToArray();
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

        var frameHash = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();
        var isDelta = enableDiff && !string.IsNullOrWhiteSpace(previousFrameHash);
        var hasChanges = !string.Equals(previousFrameHash, frameHash, StringComparison.Ordinal);
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

        string? frameData = null;
        if (includeFrameData && (!isDelta || hasChanges || !isStreaming))
        {
            if (string.Equals(requestedTransport, "png", StringComparison.Ordinal))
            {
                frameData = Convert.ToBase64String(pngBytes);
            }
            else
            {
                var pngBase64 = Convert.ToBase64String(pngBytes);
                frameData =
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\""
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
        }

        if (hasChanges)
        {
            _previewGeneration++;
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
            CapturedAtUtc: DateTimeOffset.UtcNow,
            IsPaused: IsPreviewPaused,
            IsDelta: isDelta,
            HasChanges: hasChanges,
            FrameHash: frameHash,
            PreviousFrameHash: previousFrameHash,
            DiffKind: isDelta ? "full-frame" : "none",
            ChangedRegions: changedRegions,
            FrameData: frameData);
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
        if (IsPreviewPaused || _previewTimer is not null)
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
        if (!_options.EnableMetricsStream || IsMetricsPaused || _metricsSubscription is not null)
        {
            return;
        }

        _metricsSubscription = MetricCaptureService.Subscribe(OnMetricCaptured);
    }

    private void DisableMetricsSubscription()
    {
        _metricsSubscription?.Dispose();
        _metricsSubscription = null;
    }

    private void EnsureProfilerTimer()
    {
        if (!_options.EnableProfilerStream || IsProfilerPaused || _profilerTimer is not null)
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

    private sealed class MetricSeriesAccumulator
    {
        public MetricSeriesAccumulator(RemoteMetricStreamPayload payload)
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
        }

        public string Id { get; }
        public string MeterName { get; }
        public string InstrumentName { get; }
        public string Description { get; }
        public string Unit { get; }
        public string InstrumentType { get; }
        public IReadOnlyList<RemoteStreamTag> Tags { get; }
        public double LastValue { get; }
        public DateTimeOffset UpdatedAt { get; }
        public int SampleCount { get; private set; }
        public double Sum { get; private set; }
        public double MinValue { get; private set; } = double.PositiveInfinity;
        public double MaxValue { get; private set; } = double.NegativeInfinity;

        public void Add(double value)
        {
            SampleCount++;
            Sum += value;
            if (value < MinValue)
            {
                MinValue = value;
            }

            if (value > MaxValue)
            {
                MaxValue = value;
            }
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
    TimeSpan ProfilerSampleInterval)
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
        ProfilerSampleInterval: TimeSpan.FromSeconds(1));

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
            ProfilerSampleInterval = profilerInterval
        };
    }
}
