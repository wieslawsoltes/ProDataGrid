using System;
using System.Threading;
using ProDiagnostics.Transport;

namespace Avalonia.Diagnostics.Services;

internal sealed class UdpTransportExportService : ITransportExportService
{
    internal const string UdpProtocolName = "udp";

    private DiagnosticsUdpExporter? _exporter;
    private Timer? _statsTimer;
    private bool _isRunning;
    private string _statusText = "Not running";
    private long _sentPacketCount;
    private long _failedPacketCount;

    public event EventHandler? StateChanged;

    public string ProtocolName => UdpProtocolName;

    public bool IsRunning => _isRunning;

    public string StatusText => _statusText;

    public long SentPacketCount => _sentPacketCount;

    public long FailedPacketCount => _failedPacketCount;

    public TransportExportSettings CurrentSettings { get; private set; } = TransportExportSettings.Default;

    public void Start(TransportExportSettings settings)
    {
        CurrentSettings = settings;
        RestartOrStart();
    }

    public void Stop()
    {
        if (!_isRunning && _exporter is null)
        {
            return;
        }

        _statsTimer?.Dispose();
        _statsTimer = null;
        _exporter?.Dispose();
        _exporter = null;
        _isRunning = false;
        _statusText = "Stopped (" + UdpProtocolName + ")";
        RaiseStateChanged();
    }

    public void ApplySettings(TransportExportSettings settings)
    {
        var wasRunning = _isRunning;
        CurrentSettings = settings;

        if (wasRunning)
        {
            RestartOrStart();
            return;
        }

        _statusText = "Configured for " + UdpProtocolName + " " + settings.Host + ":" + settings.Port;
        RaiseStateChanged();
    }

    public void Dispose()
    {
        Stop();
    }

    private void RestartOrStart()
    {
        _statsTimer?.Dispose();
        _statsTimer = null;
        _exporter?.Dispose();
        _exporter = null;
        _isRunning = false;

        try
        {
            _exporter = new DiagnosticsUdpExporter(ToUdpOptions(CurrentSettings));
            _exporter.Start();
            _isRunning = true;
            _statusText = "Exporting to " + UdpProtocolName + " " + CurrentSettings.Host + ":" + CurrentSettings.Port;
            SyncRuntimeCounters();
            _statsTimer = new Timer(static state => ((UdpTransportExportService)state!).OnStatsTimer(), this, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }
        catch (Exception ex)
        {
            SyncRuntimeCounters();
            _exporter?.Dispose();
            _exporter = null;
            _isRunning = false;
            _statusText = "Export failed (" + UdpProtocolName + "): " + ex.Message;
        }

        RaiseStateChanged();
    }

    private void OnStatsTimer()
    {
        if (SyncRuntimeCounters())
        {
            RaiseStateChanged();
        }
    }

    private bool SyncRuntimeCounters()
    {
        var sent = _exporter?.SentPacketCount ?? 0;
        var failed = _exporter?.FailedPacketCount ?? 0;

        if (sent == _sentPacketCount && failed == _failedPacketCount)
        {
            return false;
        }

        _sentPacketCount = sent;
        _failedPacketCount = failed;
        return true;
    }

    private static DiagnosticsUdpOptions ToUdpOptions(TransportExportSettings settings)
    {
        return new DiagnosticsUdpOptions
        {
            Host = settings.Host,
            Port = settings.Port,
            MaxTagsPerMessage = settings.MaxTagsPerMessage,
            HelloInterval = settings.HelloInterval,
            IncludeActivityTags = settings.IncludeActivityTags,
            IncludeMetricTags = settings.IncludeMetricTags,
            ActivitySourceNames = settings.ActivitySourceNames,
            MeterNames = settings.MeterNames
        };
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
