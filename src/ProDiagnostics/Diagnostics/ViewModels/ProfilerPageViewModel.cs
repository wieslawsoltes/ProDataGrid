using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Threading;
using ProDiagnostics.Transport;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class ProfilerPageViewModel : ViewModelBase, IDisposable
{
    private const int MaxPendingRemoteSamples = 2048;
    private const int MaxRemoteSamplesPerFlush = 256;
    private const int MaxRemoteSessions = 128;

    private readonly AvaloniaList<ProfilerSampleViewModel> _samples = new();
    private readonly Queue<ProfilerSampleViewModel> _pendingRemoteSamples = new();
    private readonly Dictionary<Guid, RemoteSessionInfo> _remoteSessions = new();
    private readonly DataGridCollectionView _samplesView;
    private readonly IProfilerSampler _sampler;
    private readonly DispatcherTimer _timer;
    private readonly int _localProcessId;
    private readonly string _localProcessName;

    private DiagnosticsUdpReceiver? _remoteReceiver;
    private int _maxSamples = 300;
    private bool _isSampling;
    private bool _isRemoteMode;
    private bool _isDisposed;
    private bool _isRemoteFlushScheduled;
    private bool _isRemoteStatusRefreshScheduled;
    private int _remotePort;
    private long _remotePacketCount;
    private long _droppedLocalPacketCount;
    private long _droppedUnknownSessionActivityCount;
    private long _droppedQueueSampleCount;
    private long _droppedSessionCount;
    private string _remoteStatusText = "Remote profiler listener is stopped.";
    private double _currentCpuPercent;
    private double _peakCpuPercent;
    private double _currentWorkingSetMb;
    private double _peakWorkingSetMb;
    private double _currentManagedHeapMb;
    private double _currentActivityDurationMs;
    private double _peakActivityDurationMs;
    private ProfilerSampleViewModel? _selectedSample;

    public ProfilerPageViewModel()
        : this(
            new ProcessProfilerSampler(),
            startSampling: false,
            startRemoteListener: true,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: Environment.ProcessId)
    {
    }

    internal ProfilerPageViewModel(IProfilerSampler sampler, bool startSampling)
        : this(
            sampler,
            startSampling,
            startRemoteListener: false,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: Environment.ProcessId)
    {
    }

    internal ProfilerPageViewModel(
        IProfilerSampler sampler,
        bool startSampling,
        bool startRemoteListener,
        int remotePort,
        int localProcessId)
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _localProcessId = localProcessId;
        _remotePort = NormalizePort(remotePort);
        using var currentProcess = Process.GetCurrentProcess();
        _localProcessName = currentProcess.ProcessName;
        _samplesView = new DataGridCollectionView(_samples);
        _samplesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(ProfilerSampleViewModel.Timestamp),
            ListSortDirection.Descending));

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;

        _isSampling = startSampling;
        if (startSampling)
        {
            _timer.Start();
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

    public DataGridCollectionView SamplesView => _samplesView;

    public int SampleCount => _samples.Count;

    public bool IsRemoteMode
    {
        get => _isRemoteMode;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isRemoteMode, value))
            {
                RaisePropertyChanged(nameof(ToggleSamplingText));
            }
        }
    }

    public int RemotePort => _remotePort;

    public int RemoteSessionCount => _remoteSessions.Count;

    public long RemotePacketCount => _remotePacketCount;

    public long DroppedLocalPacketCount => _droppedLocalPacketCount;

    public long DroppedUnknownSessionActivityCount => _droppedUnknownSessionActivityCount;

    public long DroppedQueueSampleCount => _droppedQueueSampleCount;

    public long DroppedSessionCount => _droppedSessionCount;

    public string RemoteStatusText
    {
        get => _remoteStatusText;
        private set => RaiseAndSetIfChanged(ref _remoteStatusText, value);
    }

    public ProfilerSampleViewModel? SelectedSample
    {
        get => _selectedSample;
        set => RaiseAndSetIfChanged(ref _selectedSample, value);
    }

    public int MaxSamples
    {
        get => _maxSamples;
        set
        {
            var clamped = value > 0 ? value : 1;
            if (RaiseAndSetIfChanged(ref _maxSamples, clamped))
            {
                TrimToMaxSamples();
                RaisePropertyChanged(nameof(SampleCount));
            }
        }
    }

    public bool IsSampling
    {
        get => _isSampling;
        private set
        {
            if (RaiseAndSetIfChanged(ref _isSampling, value))
            {
                RaisePropertyChanged(nameof(ToggleSamplingText));
            }
        }
    }

    public string ToggleSamplingText
        => IsRemoteMode ? "Remote" : IsSampling ? "Pause" : "Resume";

    public double CurrentCpuPercent
    {
        get => _currentCpuPercent;
        private set => RaiseAndSetIfChanged(ref _currentCpuPercent, value);
    }

    public double PeakCpuPercent
    {
        get => _peakCpuPercent;
        private set => RaiseAndSetIfChanged(ref _peakCpuPercent, value);
    }

    public double CurrentWorkingSetMb
    {
        get => _currentWorkingSetMb;
        private set => RaiseAndSetIfChanged(ref _currentWorkingSetMb, value);
    }

    public double PeakWorkingSetMb
    {
        get => _peakWorkingSetMb;
        private set => RaiseAndSetIfChanged(ref _peakWorkingSetMb, value);
    }

    public double CurrentManagedHeapMb
    {
        get => _currentManagedHeapMb;
        private set => RaiseAndSetIfChanged(ref _currentManagedHeapMb, value);
    }

    public double CurrentActivityDurationMs
    {
        get => _currentActivityDurationMs;
        private set => RaiseAndSetIfChanged(ref _currentActivityDurationMs, value);
    }

    public double PeakActivityDurationMs
    {
        get => _peakActivityDurationMs;
        private set => RaiseAndSetIfChanged(ref _peakActivityDurationMs, value);
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

    public void PauseOrResumeSampling()
    {
        if (IsRemoteMode)
        {
            return;
        }

        IsSampling = !IsSampling;
        if (IsSampling)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void RefreshNow()
    {
        if (IsRemoteMode)
        {
            return;
        }

        AddSample(_sampler.CaptureSample());
    }

    public void Refresh()
    {
        if (IsRemoteMode)
        {
            _samplesView.Refresh();
            return;
        }

        RefreshNow();
    }

    public void RefreshRemoteActivities()
    {
        RestartRemoteListener(_remotePort);
    }

    public void Clear()
    {
        _samples.Clear();
        _pendingRemoteSamples.Clear();
        _remoteSessions.Clear();
        _remotePacketCount = 0;
        _droppedLocalPacketCount = 0;
        _droppedUnknownSessionActivityCount = 0;
        _droppedQueueSampleCount = 0;
        _droppedSessionCount = 0;
        _isRemoteStatusRefreshScheduled = false;
        _isRemoteFlushScheduled = false;
        SelectedSample = null;
        CurrentCpuPercent = 0;
        PeakCpuPercent = 0;
        CurrentWorkingSetMb = 0;
        PeakWorkingSetMb = 0;
        CurrentManagedHeapMb = 0;
        CurrentActivityDurationMs = 0;
        PeakActivityDurationMs = 0;
        RaisePropertyChanged(nameof(SampleCount));
        RaisePropertyChanged(nameof(RemoteSessionCount));
        RaisePropertyChanged(nameof(RemotePacketCount));
        RaisePropertyChanged(nameof(DroppedLocalPacketCount));
        RaisePropertyChanged(nameof(DroppedUnknownSessionActivityCount));
        RaisePropertyChanged(nameof(DroppedQueueSampleCount));
        RaisePropertyChanged(nameof(DroppedSessionCount));
        _samplesView.Refresh();
        UpdateRemoteStatusText();
    }

    public bool RemoveSelectedRecord()
    {
        if (SelectedSample is not { } selected)
        {
            return false;
        }

        if (!_samples.Remove(selected))
        {
            return false;
        }

        SelectedSample = null;
        RecalculateAggregates();
        _samplesView.Refresh();
        RaisePropertyChanged(nameof(SampleCount));
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
        if (SelectedSample is null)
        {
            return false;
        }

        SelectedSample = null;
        return true;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _pendingRemoteSamples.Clear();
        StopRemoteListener();

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsSampling || IsRemoteMode)
        {
            return;
        }

        AddSample(_sampler.CaptureSample());
    }

    private void AddSample(ProfilerSampleSnapshot snapshot)
    {
        var sample = new ProfilerSampleViewModel(
            snapshot.Timestamp,
            snapshot.CpuPercent,
            snapshot.WorkingSetMb,
            snapshot.PrivateMemoryMb,
            snapshot.ManagedHeapMb,
            snapshot.Gen0Collections,
            snapshot.Gen1Collections,
            snapshot.Gen2Collections,
            process: _localProcessName + "(" + _localProcessId + ")");
        AddSampleCore(sample);
        _samplesView.Refresh();
    }

    private void AddSampleCore(ProfilerSampleViewModel sample)
    {
        _samples.Add(sample);
        TrimToMaxSamples();

        CurrentCpuPercent = sample.CpuPercent;
        CurrentWorkingSetMb = sample.WorkingSetMb;
        CurrentManagedHeapMb = sample.ManagedHeapMb;
        CurrentActivityDurationMs = sample.DurationMs;

        if (sample.CpuPercent > PeakCpuPercent)
        {
            PeakCpuPercent = sample.CpuPercent;
        }

        if (sample.WorkingSetMb > PeakWorkingSetMb)
        {
            PeakWorkingSetMb = sample.WorkingSetMb;
        }

        if (sample.DurationMs > PeakActivityDurationMs)
        {
            PeakActivityDurationMs = sample.DurationMs;
        }

        RaisePropertyChanged(nameof(SampleCount));
    }

    private void TrimToMaxSamples()
    {
        while (_samples.Count > MaxSamples)
        {
            if (ReferenceEquals(_samples[0], SelectedSample))
            {
                SelectedSample = null;
            }

            _samples.RemoveAt(0);
        }
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
                if (!_remoteSessions.ContainsKey(hello.SessionId) && _remoteSessions.Count >= MaxRemoteSessions)
                {
                    _droppedSessionCount++;
                    ScheduleRemoteStatusRefresh();
                    return;
                }

                _remoteSessions[hello.SessionId] = new RemoteSessionInfo(hello.ProcessId, hello.ProcessName, hello.AppName);
                ScheduleRemoteStatusRefresh();
                break;
            case TelemetryActivity activity:
                if (!TryConvertActivity(activity, out var sample))
                {
                    return;
                }

                _remotePacketCount++;
                ScheduleRemoteStatusRefresh();
                SwitchToRemoteModeIfNeeded();
                EnqueueRemoteSample(sample);
                break;
        }
    }

    private bool TryConvertActivity(TelemetryActivity activity, out ProfilerSampleViewModel sample)
    {
        sample = default!;

        if (!_remoteSessions.TryGetValue(activity.SessionId, out var session))
        {
            _droppedUnknownSessionActivityCount++;
            ScheduleRemoteStatusRefresh();
            return false;
        }

        if (session.ProcessId == _localProcessId)
        {
            _droppedLocalPacketCount++;
            ScheduleRemoteStatusRefresh();
            return false;
        }

        var durationMs = activity.Duration.TotalMilliseconds;
        if (double.IsNaN(durationMs) || double.IsInfinity(durationMs) || durationMs < 0)
        {
            durationMs = 0;
        }

        sample = new ProfilerSampleViewModel(
            activity.Timestamp,
            cpuPercent: 0,
            workingSetMb: 0,
            privateMemoryMb: 0,
            managedHeapMb: 0,
            gen0Collections: 0,
            gen1Collections: 0,
            gen2Collections: 0,
            sourceName: activity.SourceName,
            activityName: activity.Name,
            durationMs: durationMs,
            process: session.ProcessName + "(" + session.ProcessId + ")");

        return true;
    }

    private void EnqueueRemoteSample(ProfilerSampleViewModel sample)
    {
        if (_isDisposed)
        {
            return;
        }

        if (_pendingRemoteSamples.Count >= MaxPendingRemoteSamples)
        {
            _pendingRemoteSamples.Dequeue();
            _droppedQueueSampleCount++;
            ScheduleRemoteStatusRefresh();
        }

        _pendingRemoteSamples.Enqueue(sample);
        if (_isRemoteFlushScheduled)
        {
            return;
        }

        _isRemoteFlushScheduled = true;
        Dispatcher.UIThread.Post(FlushPendingRemoteSamples, DispatcherPriority.Background);
    }

    private void FlushPendingRemoteSamples()
    {
        _isRemoteFlushScheduled = false;
        if (_isDisposed || _pendingRemoteSamples.Count == 0)
        {
            return;
        }

        var updated = false;
        var processed = 0;
        while (_pendingRemoteSamples.Count > 0 && processed < MaxRemoteSamplesPerFlush)
        {
            AddSampleCore(_pendingRemoteSamples.Dequeue());
            updated = true;
            processed++;
        }

        if (updated)
        {
            _samplesView.Refresh();
        }

        if (_pendingRemoteSamples.Count > 0 && !_isDisposed)
        {
            _isRemoteFlushScheduled = true;
            Dispatcher.UIThread.Post(FlushPendingRemoteSamples, DispatcherPriority.Background);
        }
    }

    private void SwitchToRemoteModeIfNeeded()
    {
        if (IsRemoteMode)
        {
            return;
        }

        IsRemoteMode = true;
        IsSampling = false;
        _timer.Stop();
        _samples.Clear();
        SelectedSample = null;
        CurrentCpuPercent = 0;
        PeakCpuPercent = 0;
        CurrentWorkingSetMb = 0;
        PeakWorkingSetMb = 0;
        CurrentManagedHeapMb = 0;
        CurrentActivityDurationMs = 0;
        PeakActivityDurationMs = 0;
        RaisePropertyChanged(nameof(SampleCount));
        _samplesView.Refresh();
    }

    private void OnRemotePacketReceived(TelemetryPacket packet, System.Net.IPEndPoint endpoint)
    {
        HandleTelemetryPacket(packet);
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
        RaisePropertyChanged(nameof(DroppedUnknownSessionActivityCount));
        RaisePropertyChanged(nameof(DroppedQueueSampleCount));
        RaisePropertyChanged(nameof(DroppedSessionCount));
        UpdateRemoteStatusText();
    }

    private void UpdateRemoteStatusText()
    {
        if (_remoteReceiver is null)
        {
            RemoteStatusText = "Remote profiler listener is stopped.";
            return;
        }

        RemoteStatusText =
            "Listening UDP " + _remotePort +
            " | Sessions: " + RemoteSessionCount +
            " | Accepted: " + RemotePacketCount +
            " | Dropped local: " + DroppedLocalPacketCount +
            " | Dropped unknown: " + DroppedUnknownSessionActivityCount +
            " | Dropped queue: " + DroppedQueueSampleCount +
            " | Dropped sessions: " + DroppedSessionCount;
    }

    private bool NavigateSelection(bool forward)
    {
        var visible = _samplesView.Cast<ProfilerSampleViewModel>().ToArray();
        if (visible.Length == 0)
        {
            SelectedSample = null;
            return false;
        }

        var currentIndex = Array.IndexOf(visible, SelectedSample);
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

        SelectedSample = visible[nextIndex];
        return true;
    }

    private void RecalculateAggregates()
    {
        if (_samples.Count == 0)
        {
            CurrentCpuPercent = 0;
            PeakCpuPercent = 0;
            CurrentWorkingSetMb = 0;
            PeakWorkingSetMb = 0;
            CurrentManagedHeapMb = 0;
            CurrentActivityDurationMs = 0;
            PeakActivityDurationMs = 0;
            return;
        }

        var latest = _samples[_samples.Count - 1];
        CurrentCpuPercent = latest.CpuPercent;
        CurrentWorkingSetMb = latest.WorkingSetMb;
        CurrentManagedHeapMb = latest.ManagedHeapMb;
        CurrentActivityDurationMs = latest.DurationMs;

        var peakCpu = 0d;
        var peakWorkingSet = 0d;
        var peakDuration = 0d;
        for (var i = 0; i < _samples.Count; i++)
        {
            var sample = _samples[i];
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

        PeakCpuPercent = peakCpu;
        PeakWorkingSetMb = peakWorkingSet;
        PeakActivityDurationMs = peakDuration;
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

    private readonly record struct RemoteSessionInfo(int ProcessId, string ProcessName, string AppName);
}

internal interface IProfilerSampler
{
    ProfilerSampleSnapshot CaptureSample();
}

internal readonly record struct ProfilerSampleSnapshot(
    DateTimeOffset Timestamp,
    double CpuPercent,
    double WorkingSetMb,
    double PrivateMemoryMb,
    double ManagedHeapMb,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);

internal sealed class ProcessProfilerSampler : IProfilerSampler
{
    private const double BytesToMb = 1d / (1024d * 1024d);

    private readonly Process _process;
    private DateTimeOffset _lastCpuTimestamp;
    private TimeSpan _lastTotalProcessorTime;
    private bool _hasCpuBaseline;
    private int _lastGen0Collections;
    private int _lastGen1Collections;
    private int _lastGen2Collections;
    private bool _hasGcBaseline;

    public ProcessProfilerSampler()
    {
        _process = Process.GetCurrentProcess();
    }

    public ProfilerSampleSnapshot CaptureSample()
    {
        var timestamp = DateTimeOffset.UtcNow;
        _process.Refresh();

        var totalProcessorTime = _process.TotalProcessorTime;
        var cpuPercent = 0d;
        if (_hasCpuBaseline)
        {
            var wallTimeMs = (timestamp - _lastCpuTimestamp).TotalMilliseconds;
            if (wallTimeMs > 0)
            {
                var cpuTimeMs = (totalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                cpuPercent = cpuTimeMs / wallTimeMs / Environment.ProcessorCount * 100d;
                if (cpuPercent < 0)
                {
                    cpuPercent = 0;
                }
            }
        }

        _lastCpuTimestamp = timestamp;
        _lastTotalProcessorTime = totalProcessorTime;
        _hasCpuBaseline = true;

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        var gen0Delta = _hasGcBaseline ? Math.Max(0, gen0 - _lastGen0Collections) : 0;
        var gen1Delta = _hasGcBaseline ? Math.Max(0, gen1 - _lastGen1Collections) : 0;
        var gen2Delta = _hasGcBaseline ? Math.Max(0, gen2 - _lastGen2Collections) : 0;

        _lastGen0Collections = gen0;
        _lastGen1Collections = gen1;
        _lastGen2Collections = gen2;
        _hasGcBaseline = true;

        return new ProfilerSampleSnapshot(
            timestamp,
            cpuPercent,
            _process.WorkingSet64 * BytesToMb,
            _process.PrivateMemorySize64 * BytesToMb,
            GC.GetTotalMemory(forceFullCollection: false) * BytesToMb,
            gen0Delta,
            gen1Delta,
            gen2Delta);
    }
}
