using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

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
            () => received.Any(x => x.Topic == RemoteStreamTopics.Metrics) &&
                  received.Any(x => x.Topic == RemoteStreamTopics.Profiler),
            TimeSpan.FromSeconds(3));

        var metricsPayload = received
            .Where(x => x.Topic == RemoteStreamTopics.Metrics)
            .Select(x => JsonSerializer.Deserialize<RemoteMetricStreamPayload>(x.PayloadJson, JsonOptions))
            .FirstOrDefault(x => x is { Source: "udp", MeterName: "test-meter" });
        Assert.True(metricsPayload is { SessionId: var metricSessionId } && metricSessionId == sessionId);

        var profilerPayload = received
            .Where(x => x.Topic == RemoteStreamTopics.Profiler)
            .Select(x => JsonSerializer.Deserialize<RemoteProfilerStreamPayload>(x.PayloadJson, JsonOptions))
            .FirstOrDefault(x => x is { Source: "udp", ActivityName: "test-activity" });
        Assert.True(profilerPayload is { SessionId: var profilerSessionId } && profilerSessionId == sessionId);
    }

    private static async Task SendPacketAsync(UdpClient sender, IPEndPoint endpoint, ReadOnlyMemory<byte> payload)
    {
        var bytes = payload.ToArray();
        await sender.SendAsync(bytes, endpoint);
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
