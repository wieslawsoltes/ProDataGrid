using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

[Collection("MetricsCapture")]
public class RemoteRuntimeMetricsTests
{
    [Fact]
    public void RuntimeMetrics_Records_Requests_Transport_And_Gauges()
    {
        using var collector = new TestMeterCollector(RemoteRuntimeMetrics.RuntimeMeterName);

        RemoteRuntimeMetrics.RecordRequest(
            transport: "http",
            method: RemoteReadOnlyMethods.TreeSnapshotGet,
            domain: "tree",
            scope: "combined",
            status: "ok",
            durationMs: 12.5,
            payloadInBytes: 128,
            payloadOutBytes: 512);
        RemoteRuntimeMetrics.RecordSerializeDuration("http", "request", "ok", 0.4);
        RemoteRuntimeMetrics.RecordDeserializeDuration("http", "response", "ok", 0.3);
        RemoteRuntimeMetrics.RecordHeartbeatDuration("http", "ok", 0.2);
        RemoteRuntimeMetrics.RecordTransportFailure("http", "request", "none", "error");
        RemoteRuntimeMetrics.RecordConnectionAccepted("http");
        RemoteRuntimeMetrics.RecordConnectionClosed("http");
        RemoteRuntimeMetrics.SetActiveConnections("http", 2);
        RemoteRuntimeMetrics.SetActiveConnections("namedpipe", 1);
        RemoteRuntimeMetrics.SetActiveStreamSessions(3);
        RemoteRuntimeMetrics.SetStreamQueueDepth(9, 4);
        RemoteRuntimeMetrics.SetSnapshotCacheEntries(7);

        collector.RecordObservableInstruments();

        Assert.True(collector.GetLongTotal("remote.request.count") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.request.duration") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.serialize.duration") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.deserialize.duration") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.heartbeat.duration") >= 1);
        Assert.True(collector.GetLongTotal("remote.transport.failure.count") >= 1);
        Assert.True(collector.GetLongTotal("remote.connection.accepted.count") >= 1);
        Assert.True(collector.GetLongTotal("remote.connection.closed.count") >= 1);
        Assert.True(collector.GetLongTotal("remote.connection.active") >= 3);
        Assert.True(collector.GetLongTotal("remote.stream.session.active") >= 3);
        Assert.True(collector.GetLongTotal("remote.stream.queue.depth.max") >= 9);
        Assert.True(collector.GetLongTotal("remote.stream.queue.depth.avg") >= 4);
        Assert.True(collector.GetLongTotal("remote.snapshot.cache.entries") >= 7);
    }

    [AvaloniaFact]
    public async Task ReadOnlyRouter_TreeSnapshot_Emits_Snapshot_Runtime_Metrics()
    {
        using var collector = new TestMeterCollector(RemoteRuntimeMetrics.RuntimeMeterName);

        var window = new Window
        {
            Name = "RootWindow",
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Name = "Container", Child = new Button { Name = "ActionButton", Content = "Run" } }
                }
            }
        };

        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);

        var payloadJson = JsonSerializer.Serialize(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            },
            RemoteJsonSerializerContext.Default.RemoteTreeSnapshotRequest);

        var request = new RemoteRequestMessage(
            SessionId: Guid.NewGuid(),
            RequestId: 1,
            Method: RemoteReadOnlyMethods.TreeSnapshotGet,
            PayloadJson: payloadJson);

        var response = await router.HandleAsync(
            FakeAttachConnection.Instance,
            request,
            CancellationToken.None);

        var remoteResponse = Assert.IsType<RemoteResponseMessage>(response);
        Assert.True(remoteResponse.IsSuccess);

        collector.RecordObservableInstruments();

        Assert.True(collector.GetMeasurementCount("remote.snapshot.duration") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.ui_thread.capture.duration") >= 1);
        Assert.True(collector.GetMeasurementCount("remote.snapshot.payload.bytes") >= 1);
        Assert.True(collector.GetLongTotal("remote.snapshot.cache.entries") >= 1);
    }

    private sealed class FakeAttachConnection : IAttachConnection
    {
        public static FakeAttachConnection Instance { get; } = new();

        public Guid ConnectionId => Guid.Empty;

        public string? RemoteEndpoint => "loopback";

        public bool IsOpen => true;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
        {
            _ = message;
            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return ValueTask.FromResult<AttachReceiveResult?>(null);
        }

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
        {
            _ = reason;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
