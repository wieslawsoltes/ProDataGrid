using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Threading;

namespace ProDiagnostics.Transport;

public sealed class DiagnosticsUdpExporter : IDisposable
{
    private readonly DiagnosticsUdpOptions _options;
    private readonly TelemetryPacketWriter _writer = new();
    private readonly object _sendLock = new();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Socket _socket;
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private bool _started;
    private bool _disposed;
    private Timer? _helloTimer;

    public DiagnosticsUdpExporter(DiagnosticsUdpOptions? options = null)
    {
        _options = options ?? new DiagnosticsUdpOptions();
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Connect(_options.Host, _options.Port);

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => WildcardMatcher.IsMatch(source.Name, _options.ActivitySourceNames),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => OnActivityStopped(activity)
        };

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (WildcardMatcher.IsMatch(instrument.Meter.Name, _options.MeterNames))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(OnMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(OnMeasurement);
    }

    public void Start()
    {
        if (_disposed || _started)
        {
            return;
        }

        _started = true;
        ActivitySource.AddActivityListener(_activityListener);
        _meterListener.Start();
        SendHello();
        _helloTimer = new Timer(_ => SendHello(), null, _options.HelloInterval, _options.HelloInterval);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _helloTimer?.Dispose();
        _activityListener.Dispose();
        _meterListener.Dispose();
        _socket.Dispose();
    }

    private void OnActivityStopped(Activity activity)
    {
        if (!_started || _disposed)
        {
            return;
        }

        var tags = _options.IncludeActivityTags
            ? CaptureTags(activity.TagObjects, _options.MaxTagsPerMessage)
            : Array.Empty<TelemetryTag>();

        var activityName = string.IsNullOrWhiteSpace(activity.DisplayName)
            ? activity.OperationName
            : activity.DisplayName;

        var packet = new TelemetryActivity(
            _sessionId,
            DateTimeOffset.UtcNow,
            activity.Source.Name,
            activityName,
            activity.StartTimeUtc,
            activity.Duration,
            tags);

        Send(packet);
    }

    private void OnMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => SendMetric(instrument, TelemetryMetricValue.FromLong(measurement), tags);

    private void OnMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => SendMetric(instrument, TelemetryMetricValue.FromDouble(measurement), tags);

    private void SendMetric(Instrument instrument, TelemetryMetricValue value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (!_started || _disposed)
        {
            return;
        }

        var metricTags = _options.IncludeMetricTags
            ? CaptureTags(tags, _options.MaxTagsPerMessage)
            : Array.Empty<TelemetryTag>();

        var packet = new TelemetryMetric(
            _sessionId,
            DateTimeOffset.UtcNow,
            instrument.Meter.Name,
            instrument.Name,
            instrument.Description ?? string.Empty,
            instrument.Unit ?? string.Empty,
            instrument.GetType().Name,
            value,
            metricTags);

        Send(packet);
    }

    private void SendHello()
    {
        if (!_started || _disposed)
        {
            return;
        }

        var process = Process.GetCurrentProcess();
        var packet = new TelemetryHello(
            _sessionId,
            DateTimeOffset.UtcNow,
            process.Id,
            process.ProcessName,
            AppDomain.CurrentDomain.FriendlyName,
            Environment.MachineName,
            Environment.Version.ToString());

        Send(packet);
    }

    private void Send(TelemetryActivity packet)
    {
        Send(_writer.WritePooled(packet, _options.MaxTagsPerMessage, TelemetryProtocol.MaxPacketBytes));
    }

    private void Send(TelemetryMetric packet)
    {
        Send(_writer.WritePooled(packet, _options.MaxTagsPerMessage, TelemetryProtocol.MaxPacketBytes));
    }

    private void Send(TelemetryHello packet)
    {
        Send(_writer.WritePooled(packet, TelemetryProtocol.MaxPacketBytes));
    }

    private void Send(TelemetryPacketWriter.PooledPayload payload)
    {
        try
        {
            SendPayload(payload.Buffer.AsSpan(0, payload.Length));
        }
        finally
        {
            payload.Return();
        }
    }

    private void SendPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0 || payload.Length > TelemetryProtocol.MaxPacketBytes)
        {
            return;
        }

        try
        {
            lock (_sendLock)
            {
                _socket.Send(payload);
            }
        }
        catch (SocketException)
        {
            // UDP can report connection refused if the receiver isn't listening yet.
        }
        catch (ObjectDisposedException)
        {
            // Shutdown raced with a send; ignore.
        }
    }

    private static IReadOnlyList<TelemetryTag> CaptureTags(IEnumerable<KeyValuePair<string, object?>> tags, int maxTags)
    {
        if (maxTags <= 0)
        {
            return Array.Empty<TelemetryTag>();
        }

        var list = new List<TelemetryTag>();
        foreach (var tag in tags)
        {
            if (list.Count >= maxTags)
            {
                break;
            }

            list.Add(new TelemetryTag(tag.Key, tag.Value));
        }

        return list.Count == 0 ? Array.Empty<TelemetryTag>() : list;
    }

    private static IReadOnlyList<TelemetryTag> CaptureTags(ReadOnlySpan<KeyValuePair<string, object?>> tags, int maxTags)
    {
        if (tags.IsEmpty || maxTags <= 0)
        {
            return Array.Empty<TelemetryTag>();
        }

        var list = new List<TelemetryTag>(Math.Min(tags.Length, maxTags));
        for (var i = 0; i < tags.Length && list.Count < maxTags; i++)
        {
            var tag = tags[i];
            list.Add(new TelemetryTag(tag.Key, tag.Value));
        }

        return list.Count == 0 ? Array.Empty<TelemetryTag>() : list;
    }
}
