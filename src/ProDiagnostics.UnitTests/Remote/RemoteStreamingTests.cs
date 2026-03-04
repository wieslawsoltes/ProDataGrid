using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

[Collection("MetricsCapture")]
public class RemoteStreamingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task RemoteStreamSessionHub_SlowConsumer_Drops_And_Reports_DroppedMessages()
    {
        await using var hub = new RemoteStreamSessionHub(new RemoteStreamSessionHubOptions(
            MaxQueueLengthPerSession: 8,
            MaxDispatchBatchSize: 4));
        var sessionId = Guid.NewGuid();
        var connection = new RecordingAttachConnection(TimeSpan.FromMilliseconds(20));
        hub.RegisterSession(sessionId, connection);

        for (var i = 0; i < 250; i++)
        {
            hub.Publish(RemoteStreamTopics.Metrics, "{\"value\":" + i + "}");
        }

        await WaitUntilAsync(
            () => connection.GetMessageCount() > 0,
            TimeSpan.FromSeconds(3));

        await Task.Delay(500);

        var stats = Assert.Single(hub.GetSessionStats());
        Assert.Equal(sessionId, stats.SessionId);
        Assert.True(stats.DroppedMessages > 0);
        Assert.Contains(connection.GetMessagesSnapshot(), message => message.DroppedMessages > 0);
    }

    [Fact]
    public async Task RemoteStreamSessionHub_BurstFanout_Delivers_To_All_Sessions_In_Order()
    {
        await using var hub = new RemoteStreamSessionHub(new RemoteStreamSessionHubOptions(
            MaxQueueLengthPerSession: 5000,
            MaxDispatchBatchSize: 512));

        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();
        var firstConnection = new RecordingAttachConnection(TimeSpan.Zero);
        var secondConnection = new RecordingAttachConnection(TimeSpan.Zero);
        hub.RegisterSession(firstSession, firstConnection);
        hub.RegisterSession(secondSession, secondConnection);

        const int burstCount = 1200;
        for (var i = 0; i < burstCount; i++)
        {
            hub.Publish(RemoteStreamTopics.Logs, "{\"index\":" + i + "}");
        }

        await WaitUntilAsync(
            () => firstConnection.GetMessageCount() >= burstCount && secondConnection.GetMessageCount() >= burstCount,
            TimeSpan.FromSeconds(5));

        var stats = hub.GetSessionStats().OrderBy(x => x.SessionId).ToArray();
        Assert.Equal(2, stats.Length);
        Assert.All(stats, x => Assert.Equal(0, x.DroppedMessages));
        Assert.All(stats, x => Assert.Equal(burstCount, x.SentMessages));

        AssertSequencesAreStrictlyIncreasing(firstConnection.GetMessagesSnapshot());
        AssertSequencesAreStrictlyIncreasing(secondConnection.GetMessagesSnapshot());
    }

    [Fact]
    public async Task RemoteStreamSessionHub_DemandFiltering_RoutesOnlyDemandedTopics()
    {
        await using var hub = new RemoteStreamSessionHub(new RemoteStreamSessionHubOptions(
            MaxQueueLengthPerSession: 128,
            MaxDispatchBatchSize: 32));

        var logsSessionId = Guid.NewGuid();
        var metricsSessionId = Guid.NewGuid();
        var logsConnection = new RecordingAttachConnection(TimeSpan.Zero);
        var metricsConnection = new RecordingAttachConnection(TimeSpan.Zero);
        hub.RegisterSession(logsSessionId, logsConnection);
        hub.RegisterSession(metricsSessionId, metricsConnection);

        Assert.True(hub.SetSessionDemand(logsSessionId, new[] { RemoteStreamTopics.Logs }));
        Assert.True(hub.SetSessionDemand(metricsSessionId, new[] { RemoteStreamTopics.Metrics }));

        for (var i = 0; i < 50; i++)
        {
            hub.Publish(RemoteStreamTopics.Logs, "{\"kind\":\"log\",\"index\":" + i + "}");
            hub.Publish(RemoteStreamTopics.Metrics, "{\"kind\":\"metric\",\"index\":" + i + "}");
        }

        await WaitUntilAsync(
            () => logsConnection.GetMessageCount() >= 50 && metricsConnection.GetMessageCount() >= 50,
            TimeSpan.FromSeconds(3));

        var logsMessages = logsConnection.GetMessagesSnapshot();
        var metricsMessages = metricsConnection.GetMessagesSnapshot();
        Assert.NotEmpty(logsMessages);
        Assert.NotEmpty(metricsMessages);
        Assert.All(logsMessages, message => Assert.Equal(RemoteStreamTopics.Logs, message.Topic));
        Assert.All(metricsMessages, message => Assert.Equal(RemoteStreamTopics.Metrics, message.Topic));

        var aggregatedDemand = hub.GetAggregatedDemand();
        Assert.True(aggregatedDemand.Logs);
        Assert.True(aggregatedDemand.Metrics);
        Assert.False(aggregatedDemand.Preview);
        Assert.False(aggregatedDemand.Profiler);
    }

    [Fact]
    public async Task RemoteStreamSessionHub_HighVolumePublish_QueueLength_RemainsBounded()
    {
        const int maxQueueLength = 32;
        await using var hub = new RemoteStreamSessionHub(new RemoteStreamSessionHubOptions(
            MaxQueueLengthPerSession: maxQueueLength,
            MaxDispatchBatchSize: 8));

        var sessionId = Guid.NewGuid();
        var connection = new RecordingAttachConnection(TimeSpan.FromMilliseconds(10));
        hub.RegisterSession(sessionId, connection);

        const int messageCount = 10_000;
        for (var i = 0; i < messageCount; i++)
        {
            hub.Publish(RemoteStreamTopics.Logs, "{\"index\":" + i + "}");
            if ((i % 500) == 0)
            {
                var stats = Assert.Single(hub.GetSessionStats());
                Assert.InRange(stats.QueueLength, 0, maxQueueLength);
            }
        }

        await WaitUntilAsync(
            () => Assert.Single(hub.GetSessionStats()).DroppedMessages > 0,
            TimeSpan.FromSeconds(5));

        var finalStats = Assert.Single(hub.GetSessionStats());
        Assert.InRange(finalStats.QueueLength, 0, maxQueueLength);
        Assert.True(finalStats.DroppedMessages > 0);
    }

    [Fact]
    public async Task RemoteStreamSessionHub_Reports_Drops_And_DispatchFailures_To_ProtocolMonitor()
    {
        var monitor = new InMemoryRemoteProtocolMonitor();
        await using var hub = new RemoteStreamSessionHub(
            new RemoteStreamSessionHubOptions(
                MaxQueueLengthPerSession: 4,
                MaxDispatchBatchSize: 2),
            protocolMonitor: monitor);

        var slowSessionId = Guid.NewGuid();
        var failingSessionId = Guid.NewGuid();
        hub.RegisterSession(slowSessionId, new RecordingAttachConnection(TimeSpan.FromMilliseconds(15)));
        hub.RegisterSession(failingSessionId, new ThrowingAttachConnection());

        for (var i = 0; i < 120; i++)
        {
            hub.Publish(RemoteStreamTopics.Metrics, "{\"value\":" + i + "}");
        }

        await WaitUntilAsync(
            () =>
            {
                var snapshot = monitor.GetSnapshot();
                return snapshot.StreamDroppedMessages > 0 && snapshot.StreamDispatchFailures > 0;
            },
            TimeSpan.FromSeconds(5));

        var monitorSnapshot = monitor.GetSnapshot();
        Assert.True(monitorSnapshot.StreamDroppedMessages > 0);
        Assert.True(monitorSnapshot.StreamDispatchFailures > 0);
    }

    [Fact]
    public async Task InProcessRemoteStreamSource_UdpFallback_Publishes_Metrics_And_Profiler_Streams()
    {
        var udpPort = AllocateUdpPort();
        var options = new InProcessRemoteStreamSourceOptions(
            EnableMetricsStream: true,
            EnableProfilerStream: true,
            EnableLogsStream: false,
            EnableEventsStream: false,
            EnableUdpTelemetryFallback: true,
            UdpPort: udpPort,
            MaxUdpSessions: 16,
            MaxRetainedMeasurements: 5000,
            MaxRetainedProfilerSamples: 2000,
            MaxSeries: 500,
            MaxSamplesPerSeries: 250,
            MaxLogStreamPerSecond: 60,
            MaxEventStreamPerSecond: 60,
            ProfilerSampleInterval: TimeSpan.FromSeconds(10));

        using var source = new InProcessRemoteStreamSource(options: options);
        var received = new ConcurrentQueue<RemoteStreamPayload>();
        using var subscription = source.Subscribe(received.Enqueue);

        await Task.Delay(100);

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var writer = new TelemetryPacketWriter();
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        var endpoint = new IPEndPoint(IPAddress.Loopback, udpPort);

        var hello = new TelemetryHello(
            SessionId: sessionId,
            Timestamp: now,
            ProcessId: 9876,
            ProcessName: "RemoteApp",
            AppName: "RemoteApp",
            MachineName: "machine",
            RuntimeVersion: "net10.0");
        var metric = new TelemetryMetric(
            SessionId: sessionId,
            Timestamp: now.AddMilliseconds(5),
            MeterName: "test-meter",
            InstrumentName: "test-counter",
            Description: "desc",
            Unit: "items",
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(42.5),
            Tags: new[] { new TelemetryTag("k", "v") });
        var activity = new TelemetryActivity(
            SessionId: sessionId,
            Timestamp: now.AddMilliseconds(10),
            SourceName: "test-source",
            Name: "test-activity",
            StartTime: now.AddMilliseconds(-50),
            Duration: TimeSpan.FromMilliseconds(25),
            Tags: new[] { new TelemetryTag("kind", "udp") });

        await SendPacketAsync(sender, endpoint, writer.Write(hello));
        await SendPacketAsync(sender, endpoint, writer.Write(metric, maxTags: 8));
        await SendPacketAsync(sender, endpoint, writer.Write(activity, maxTags: 8));

        await WaitUntilAsync(
            () => TryFindUdpMetricPayload(received, sessionId, out _) &&
                  TryFindUdpProfilerPayload(received, sessionId, out _),
            TimeSpan.FromSeconds(3));

        Assert.True(TryFindUdpMetricPayload(received, sessionId, out var metricsPayload));
        Assert.Equal(sessionId, metricsPayload!.Value.SessionId);

        Assert.True(TryFindUdpProfilerPayload(received, sessionId, out var profilerPayload));
        Assert.Equal(sessionId, profilerPayload!.Value.SessionId);
    }

    [Fact]
    public void InProcessRemoteStreamSource_MetricsSnapshot_TracksSeriesIncrementally_AfterTrim()
    {
        var options = new InProcessRemoteStreamSourceOptions(
            EnableMetricsStream: false,
            EnableProfilerStream: false,
            EnableLogsStream: false,
            EnableEventsStream: false,
            EnableUdpTelemetryFallback: false,
            UdpPort: TelemetryProtocol.DefaultPort,
            MaxUdpSessions: 4,
            MaxRetainedMeasurements: 3,
            MaxRetainedProfilerSamples: 8,
            MaxSeries: 8,
            MaxSamplesPerSeries: 8,
            MaxLogStreamPerSecond: 60,
            MaxEventStreamPerSecond: 60,
            ProfilerSampleInterval: TimeSpan.FromSeconds(1));
        using var source = new InProcessRemoteStreamSource(options: options);

        var sessionId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;
        InjectMetricSample(source, CreateMetricPayload(start.AddMilliseconds(1), sessionId, "meter", "series-a", 1));
        InjectMetricSample(source, CreateMetricPayload(start.AddMilliseconds(2), sessionId, "meter", "series-a", 2));
        InjectMetricSample(source, CreateMetricPayload(start.AddMilliseconds(3), sessionId, "meter", "series-a", 3));
        InjectMetricSample(source, CreateMetricPayload(start.AddMilliseconds(4), sessionId, "meter", "series-a", 4));
        InjectMetricSample(source, CreateMetricPayload(start.AddMilliseconds(5), sessionId, "meter", "series-a", 5));

        var snapshot = source.GetMetricsSnapshot(new RemoteMetricsSnapshotRequest
        {
            IncludeSeries = true,
            IncludeMeasurements = true,
        });

        Assert.Equal(3, snapshot.MeasurementCount);
        Assert.Equal(1, snapshot.SeriesCount);
        var series = Assert.Single(snapshot.Series);
        Assert.Equal(3, series.SampleCount);
        Assert.Equal(3d, series.MinValue);
        Assert.Equal(5d, series.MaxValue);
        Assert.Equal(4d, series.AverageValue, 6);
        Assert.Equal(5d, series.LastValue);
    }

    [Fact]
    public void InProcessRemoteStreamSource_MetricsSettings_RebuildSeries_FromRetainedHistory()
    {
        var options = new InProcessRemoteStreamSourceOptions(
            EnableMetricsStream: false,
            EnableProfilerStream: false,
            EnableLogsStream: false,
            EnableEventsStream: false,
            EnableUdpTelemetryFallback: false,
            UdpPort: TelemetryProtocol.DefaultPort,
            MaxUdpSessions: 4,
            MaxRetainedMeasurements: 20,
            MaxRetainedProfilerSamples: 8,
            MaxSeries: 1,
            MaxSamplesPerSeries: 8,
            MaxLogStreamPerSecond: 60,
            MaxEventStreamPerSecond: 60,
            ProfilerSampleInterval: TimeSpan.FromSeconds(1));
        using var source = new InProcessRemoteStreamSource(options: options);

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        InjectMetricSample(source, CreateMetricPayload(now.AddMilliseconds(1), sessionId, "meter", "series-a", 10));
        InjectMetricSample(source, CreateMetricPayload(now.AddMilliseconds(2), sessionId, "meter", "series-b", 20));

        var before = source.GetMetricsSnapshot(new RemoteMetricsSnapshotRequest { IncludeSeries = true });
        Assert.Equal(1, before.SeriesCount);

        var changed = source.ApplyMetricsSettings(new RemoteSetMetricsSettingsRequest
        {
            MaxSeries = 2,
        });
        Assert.True(changed > 0);

        var after = source.GetMetricsSnapshot(new RemoteMetricsSnapshotRequest { IncludeSeries = true });
        Assert.Equal(2, after.SeriesCount);
        Assert.Contains(after.Series, x => x.InstrumentName == "series-a");
        Assert.Contains(after.Series, x => x.InstrumentName == "series-b");
    }

    [Fact]
    public void InProcessRemoteStreamSource_ProfilerSnapshot_RecomputesPeaks_WhenTrimDropsPeak()
    {
        var options = new InProcessRemoteStreamSourceOptions(
            EnableMetricsStream: false,
            EnableProfilerStream: false,
            EnableLogsStream: false,
            EnableEventsStream: false,
            EnableUdpTelemetryFallback: false,
            UdpPort: TelemetryProtocol.DefaultPort,
            MaxUdpSessions: 4,
            MaxRetainedMeasurements: 20,
            MaxRetainedProfilerSamples: 3,
            MaxSeries: 8,
            MaxSamplesPerSeries: 8,
            MaxLogStreamPerSecond: 60,
            MaxEventStreamPerSecond: 60,
            ProfilerSampleInterval: TimeSpan.FromSeconds(1));
        using var source = new InProcessRemoteStreamSource(options: options);

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        InjectProfilerSample(source, CreateProfilerPayload(now.AddMilliseconds(1), sessionId, 10));
        InjectProfilerSample(source, CreateProfilerPayload(now.AddMilliseconds(2), sessionId, 30));
        InjectProfilerSample(source, CreateProfilerPayload(now.AddMilliseconds(3), sessionId, 20));
        InjectProfilerSample(source, CreateProfilerPayload(now.AddMilliseconds(4), sessionId, 5));
        var mid = source.GetProfilerSnapshot(new RemoteProfilerSnapshotRequest { IncludeSamples = true });
        Assert.Equal(3, mid.SampleCount);
        Assert.Equal(30d, mid.PeakCpuPercent);
        Assert.Equal(5d, mid.CurrentCpuPercent);

        InjectProfilerSample(source, CreateProfilerPayload(now.AddMilliseconds(5), sessionId, 4));
        var afterDrop = source.GetProfilerSnapshot(new RemoteProfilerSnapshotRequest { IncludeSamples = true });
        Assert.Equal(3, afterDrop.SampleCount);
        Assert.Equal(20d, afterDrop.PeakCpuPercent);
        Assert.Equal(4d, afterDrop.CurrentCpuPercent);
    }

    [Fact]
    public void InProcessRemoteStreamSource_DemandGates_MetricsAndProfilerHistory()
    {
        var options = new InProcessRemoteStreamSourceOptions(
            EnableMetricsStream: true,
            EnableProfilerStream: true,
            EnableLogsStream: false,
            EnableEventsStream: false,
            EnableUdpTelemetryFallback: false,
            UdpPort: TelemetryProtocol.DefaultPort,
            MaxUdpSessions: 4,
            MaxRetainedMeasurements: 32,
            MaxRetainedProfilerSamples: 32,
            MaxSeries: 8,
            MaxSamplesPerSeries: 8,
            MaxLogStreamPerSecond: 60,
            MaxEventStreamPerSecond: 60,
            ProfilerSampleInterval: TimeSpan.FromSeconds(5),
            MaxMetricStreamPerSecond: 60);
        using var source = new InProcessRemoteStreamSource(options: options);
        using var subscription = source.Subscribe(_ => { });

        source.SetStreamDemand(RemoteStreamDemandSnapshot.None);
        InjectUdpMetric(source, Guid.NewGuid(), DateTimeOffset.UtcNow, 10d);
        InjectUdpActivity(source, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMilliseconds(1), 5d);

        var pausedMetrics = source.GetMetricsSnapshot(new RemoteMetricsSnapshotRequest { IncludeMeasurements = true });
        var pausedProfiler = source.GetProfilerSnapshot(new RemoteProfilerSnapshotRequest { IncludeSamples = true });
        Assert.Equal(0, pausedMetrics.MeasurementCount);
        Assert.Equal(0, pausedProfiler.SampleCount);

        source.SetStreamDemand(new RemoteStreamDemandSnapshot(
            Selection: false,
            Preview: false,
            Metrics: true,
            Profiler: true,
            Logs: false,
            Events: false));

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddMilliseconds(5);
        InjectUdpMetric(source, sessionId, now, 42d);
        InjectUdpActivity(source, sessionId, now.AddMilliseconds(1), 11d);

        var activeMetrics = source.GetMetricsSnapshot(new RemoteMetricsSnapshotRequest { IncludeMeasurements = true });
        var activeProfiler = source.GetProfilerSnapshot(new RemoteProfilerSnapshotRequest { IncludeSamples = true });
        Assert.True(activeMetrics.MeasurementCount > 0);
        Assert.True(activeProfiler.SampleCount > 0);
    }

    private static async Task SendPacketAsync(UdpClient sender, IPEndPoint endpoint, ReadOnlyMemory<byte> payload)
    {
        var bytes = payload.ToArray();
        await sender.SendAsync(bytes, endpoint);
    }

    private static bool TryFindUdpMetricPayload(
        ConcurrentQueue<RemoteStreamPayload> received,
        Guid sessionId,
        out RemoteMetricStreamPayload? payload)
    {
        payload = null;
        foreach (var streamPayload in received)
        {
            if (!string.Equals(streamPayload.Topic, RemoteStreamTopics.Metrics, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = JsonSerializer.Deserialize<RemoteMetricStreamPayload>(streamPayload.PayloadJson, JsonOptions);
            if (candidate is not { Source: "udp", MeterName: "test-meter" })
            {
                continue;
            }

            if (candidate.SessionId != sessionId)
            {
                continue;
            }

            payload = candidate;
            return true;
        }

        return false;
    }

    private static bool TryFindUdpProfilerPayload(
        ConcurrentQueue<RemoteStreamPayload> received,
        Guid sessionId,
        out RemoteProfilerStreamPayload? payload)
    {
        payload = null;
        foreach (var streamPayload in received)
        {
            if (!string.Equals(streamPayload.Topic, RemoteStreamTopics.Profiler, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = JsonSerializer.Deserialize<RemoteProfilerStreamPayload>(streamPayload.PayloadJson, JsonOptions);
            if (candidate is not { Source: "udp", ActivityName: "test-activity" })
            {
                continue;
            }

            if (candidate.SessionId != sessionId)
            {
                continue;
            }

            payload = candidate;
            return true;
        }

        return false;
    }

    private static void AssertSequencesAreStrictlyIncreasing(IReadOnlyList<RemoteStreamMessage> messages)
    {
        Assert.NotEmpty(messages);
        var previous = 0L;
        for (var i = 0; i < messages.Count; i++)
        {
            var current = messages[i].Sequence;
            Assert.True(current > previous);
            previous = current;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                Assert.Fail("Condition was not met within timeout.");
            }

            await Task.Delay(20);
        }
    }

    private static int AllocateUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }

    private static void InjectMetricSample(InProcessRemoteStreamSource source, RemoteMetricStreamPayload payload)
    {
        InvokeNonPublic(
            source,
            "RecordMetricPayload",
            payload);
    }

    private static void InjectProfilerSample(InProcessRemoteStreamSource source, RemoteProfilerStreamPayload payload)
    {
        InvokeNonPublic(
            source,
            "RecordProfilerPayload",
            payload);
    }

    private static void InjectUdpMetric(
        InProcessRemoteStreamSource source,
        Guid sessionId,
        DateTimeOffset timestamp,
        double value)
    {
        var packet = new TelemetryMetric(
            SessionId: sessionId,
            Timestamp: timestamp,
            MeterName: "meter",
            InstrumentName: "counter",
            Description: "desc",
            Unit: "items",
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(value),
            Tags: Array.Empty<TelemetryTag>());
        InvokeNonPublic(
            source,
            "OnUdpPacketReceived",
            packet,
            new IPEndPoint(IPAddress.Loopback, TelemetryProtocol.DefaultPort));
    }

    private static void InjectUdpActivity(
        InProcessRemoteStreamSource source,
        Guid sessionId,
        DateTimeOffset timestamp,
        double durationMs)
    {
        var packet = new TelemetryActivity(
            SessionId: sessionId,
            Timestamp: timestamp,
            SourceName: "source",
            Name: "activity",
            StartTime: timestamp.AddMilliseconds(-durationMs),
            Duration: TimeSpan.FromMilliseconds(durationMs),
            Tags: Array.Empty<TelemetryTag>());
        InvokeNonPublic(
            source,
            "OnUdpPacketReceived",
            packet,
            new IPEndPoint(IPAddress.Loopback, TelemetryProtocol.DefaultPort));
    }

    private static void InvokeNonPublic<T>(InProcessRemoteStreamSource source, string methodName, T payload)
    {
        var method = typeof(InProcessRemoteStreamSource).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(source, new object?[] { payload! });
    }

    private static void InvokeNonPublic(InProcessRemoteStreamSource source, string methodName, params object?[] parameters)
    {
        var method = typeof(InProcessRemoteStreamSource).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(source, parameters);
    }

    private static RemoteMetricStreamPayload CreateMetricPayload(
        DateTimeOffset timestamp,
        Guid sessionId,
        string meter,
        string instrument,
        double value)
    {
        return new RemoteMetricStreamPayload(
            TimestampUtc: timestamp,
            Source: "test",
            SessionId: sessionId,
            MeterName: meter,
            InstrumentName: instrument,
            Description: "desc",
            Unit: "u",
            InstrumentType: "Counter",
            Value: value,
            Tags: Array.Empty<RemoteStreamTag>());
    }

    private static RemoteProfilerStreamPayload CreateProfilerPayload(
        DateTimeOffset timestamp,
        Guid sessionId,
        double cpuPercent)
    {
        return new RemoteProfilerStreamPayload(
            TimestampUtc: timestamp,
            Source: "test",
            SessionId: sessionId,
            Process: "proc",
            CpuPercent: cpuPercent,
            WorkingSetMb: cpuPercent,
            PrivateMemoryMb: cpuPercent,
            ManagedHeapMb: cpuPercent,
            Gen0Collections: 0,
            Gen1Collections: 0,
            Gen2Collections: 0,
            ActivitySource: string.Empty,
            ActivityName: string.Empty,
            DurationMs: cpuPercent,
            Tags: Array.Empty<RemoteStreamTag>());
    }

    private sealed class RecordingAttachConnection : IAttachConnection
    {
        private readonly TimeSpan _sendDelay;
        private readonly object _gate = new();
        private bool _isOpen = true;

        public RecordingAttachConnection(TimeSpan sendDelay)
        {
            _sendDelay = sendDelay;
        }

        public Guid ConnectionId { get; } = Guid.NewGuid();

        public string? RemoteEndpoint => "test";

        public bool IsOpen
        {
            get
            {
                lock (_gate)
                {
                    return _isOpen;
                }
            }
        }

        public List<RemoteStreamMessage> StreamMessages { get; } = new();

        public int GetMessageCount()
        {
            lock (_gate)
            {
                return StreamMessages.Count;
            }
        }

        public IReadOnlyList<RemoteStreamMessage> GetMessagesSnapshot()
        {
            lock (_gate)
            {
                return StreamMessages.ToArray();
            }
        }

        public async ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
        {
            if (_sendDelay > TimeSpan.Zero)
            {
                await Task.Delay(_sendDelay, cancellationToken);
            }

            if (message is RemoteStreamMessage streamMessage)
            {
                lock (_gate)
                {
                    if (!_isOpen)
                    {
                        throw new InvalidOperationException("Connection is closed.");
                    }

                    StreamMessages.Add(streamMessage);
                }
            }
        }

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AttachReceiveResult?>(null);

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _isOpen = false;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                _isOpen = false;
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingAttachConnection : IAttachConnection
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();

        public string? RemoteEndpoint => "throwing";

        public bool IsOpen => true;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new InvalidOperationException("send failure"));

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AttachReceiveResult?>(null);

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
