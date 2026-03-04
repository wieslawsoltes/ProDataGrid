using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

[Collection("MetricsCapture")]
public class DevToolsRemoteAttachHostTests
{
    [AvaloniaFact]
    public async Task DevToolsRemoteAttachHost_Hello_And_TreeSnapshot_Request_Work_EndToEnd()
    {
        var port = AllocateTcpPort();
        var options = new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = port,
                Path = "/attach",
                BindingMode = HttpAttachBindingMode.Localhost,
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromSeconds(30),
                },
            },
            EnableMutationApi = false,
            EnableStreamingApi = false,
        };

        var root = new Window
        {
            Content = new Grid()
        };

        await using var host = new DevToolsRemoteAttachHost(root, options);
        await host.StartAsync();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(options.HttpOptions.BuildClientWebSocketUri(), CancellationToken.None);

        var sessionId = Guid.NewGuid();
        await SendAsync(
            client,
            new RemoteHelloMessage(
                SessionId: sessionId,
                ProcessId: 123,
                ProcessName: "test-client",
                ApplicationName: "test-client",
                MachineName: Environment.MachineName,
                RuntimeVersion: Environment.Version.ToString(),
                ClientName: "test-suite",
                RequestedFeatures: new[] { "trees", "properties", "code" }));

        var helloAck = Assert.IsType<RemoteHelloAckMessage>(
            await ReceiveUntilNotKeepAliveAsync(client, TimeSpan.FromSeconds(5)));
        Assert.Equal(sessionId, helloAck.SessionId);
        Assert.Equal(RemoteProtocol.Version, helloAck.NegotiatedProtocolVersion);
        Assert.True(helloAck.EnabledFeatures.Contains("trees", StringComparer.OrdinalIgnoreCase));
        Assert.True(helloAck.EnabledFeatures.Contains("properties", StringComparer.OrdinalIgnoreCase));
        Assert.True(helloAck.EnabledFeatures.Contains("code", StringComparer.OrdinalIgnoreCase));
        Assert.False(helloAck.EnabledFeatures.Contains("mutation", StringComparer.OrdinalIgnoreCase));
        Assert.False(helloAck.EnabledFeatures.Contains("streaming", StringComparer.OrdinalIgnoreCase));

        var requestId = 42L;
        await SendAsync(
            client,
            new RemoteRequestMessage(
                SessionId: sessionId,
                RequestId: requestId,
                Method: RemoteReadOnlyMethods.TreeSnapshotGet,
                PayloadJson: "{\"scope\":\"combined\"}"));

        var response = Assert.IsType<RemoteResponseMessage>(
            await ReceiveUntilNotKeepAliveAsync(client, TimeSpan.FromSeconds(5)));
        Assert.True(response.IsSuccess);
        Assert.Equal(requestId, response.RequestId);
        Assert.Contains("\"nodes\":", response.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task DevToolsRemoteAttachHost_RequestTimeout_Returns_TimeoutResponse_And_ProtocolFailure()
    {
        var port = AllocateTcpPort();
        var monitor = new InMemoryRemoteProtocolMonitor();
        var options = new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = port,
                Path = "/attach",
                BindingMode = HttpAttachBindingMode.Localhost,
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromSeconds(30),
                },
            },
            EnableMutationApi = false,
            EnableStreamingApi = false,
            RequestTimeout = TimeSpan.FromMilliseconds(10),
            ProtocolMonitor = monitor,
        };

        var root = CreateLargeTreeWindow(nodeCount: 5000);
        await using var host = new DevToolsRemoteAttachHost(root, options);
        await host.StartAsync();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(options.HttpOptions.BuildClientWebSocketUri(), CancellationToken.None);

        var sessionId = Guid.NewGuid();
        await SendAsync(
            client,
            new RemoteHelloMessage(
                SessionId: sessionId,
                ProcessId: 999,
                ProcessName: "timeout-test",
                ApplicationName: "timeout-test",
                MachineName: Environment.MachineName,
                RuntimeVersion: Environment.Version.ToString(),
                ClientName: "timeout-suite",
                RequestedFeatures: new[] { "trees" }));

        _ = Assert.IsType<RemoteHelloAckMessage>(
            await ReceiveUntilNotKeepAliveAsync(client, TimeSpan.FromSeconds(5)));

        // Force the request handler to queue behind a short UI-thread stall, guaranteeing timeout deterministically.
        Dispatcher.UIThread.Post(
            static () => Thread.Sleep(150),
            DispatcherPriority.Send);

        await SendAsync(
            client,
            new RemoteRequestMessage(
                SessionId: sessionId,
                RequestId: 1001,
                Method: RemoteReadOnlyMethods.TreeSnapshotGet,
                PayloadJson: "{\"scope\":\"combined\",\"includeSourceLocations\":true}"));

        var response = Assert.IsType<RemoteResponseMessage>(
            await ReceiveUntilNotKeepAliveAsync(client, TimeSpan.FromSeconds(5)));
        Assert.False(response.IsSuccess);
        Assert.Equal("request_timeout", response.ErrorCode);
        Assert.Contains(RemoteReadOnlyMethods.TreeSnapshotGet, response.ErrorMessage, StringComparison.Ordinal);

        var snapshot = monitor.GetSnapshot();
        Assert.True(snapshot.ReceiveFailures > 0);
        Assert.Contains(
            snapshot.RecentEvents,
            entry => entry.EventType == RemoteProtocolEventType.ReceiveFailure &&
                     (entry.Details?.Contains(RemoteReadOnlyMethods.TreeSnapshotGet, StringComparison.Ordinal) ?? false));
    }

    private static async Task SendAsync(ClientWebSocket socket, IRemoteMessage message)
    {
        var payload = RemoteMessageSerializer.Serialize(message);
        await socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
    }

    private static async Task<IRemoteMessage> ReceiveUntilNotKeepAliveAsync(ClientWebSocket socket, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (true)
        {
            var elapsed = DateTime.UtcNow - start;
            if (elapsed >= timeout)
            {
                throw new TimeoutException("Timed out waiting for protocol message.");
            }

            var message = await ReceiveAsync(socket, timeout - elapsed);
            if (message.Kind != RemoteMessageKind.KeepAlive)
            {
                return message;
            }
        }
    }

    private static async Task<IRemoteMessage> ReceiveAsync(ClientWebSocket socket, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var rented = ArrayPool<byte>.Shared.Rent(RemoteProtocol.MaxFramePayloadBytes);
        try
        {
            var result = await socket.ReceiveAsync(rented.AsMemory(), cts.Token);
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
            Assert.True(result.EndOfMessage);
            Assert.True(RemoteMessageSerializer.TryDeserialize(rented.AsSpan(0, result.Count), out var message));
            Assert.NotNull(message);
            return message!;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int AllocateTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static Window CreateLargeTreeWindow(int nodeCount)
    {
        var panel = new StackPanel();
        for (var i = 0; i < nodeCount; i++)
        {
            panel.Children.Add(new Border
            {
                Name = "Node" + i.ToString(),
                Child = new TextBlock
                {
                    Text = "Item " + i.ToString(),
                },
            });
        }

        return new Window
        {
            Name = "RootWindow",
            Content = panel,
        };
    }
}
