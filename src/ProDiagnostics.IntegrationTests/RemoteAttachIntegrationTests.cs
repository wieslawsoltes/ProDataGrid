using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.IntegrationTests;

[Collection("MetricsCapture")]
public class RemoteAttachIntegrationTests
{
    [AvaloniaFact]
    public async Task RemoteLoopbackSession_DenseWindow_Returns_NonEmpty_Snapshots_Across_Scopes()
    {
        var root = CreateDenseWindow(controlCount: 1000);

        await using var session = await DevToolsRemoteLoopbackSession.StartAsync(
            root,
            new DevToolsRemoteLoopbackOptions
            {
                UseDynamicPort = true,
                HostOptions = new DevToolsRemoteAttachHostOptions
                {
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
                ClientOptions = new RemoteDiagnosticsClientOptions
                {
                    ClientName = "integration-loopback-client",
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
            });

        Assert.True(session.Client.IsConnected);

        var combined = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
            IncludeSourceLocations = false,
        });
        var visual = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "visual",
            IncludeSourceLocations = false,
        });
        var logical = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "logical",
            IncludeSourceLocations = false,
        });

        Assert.NotEmpty(combined.Nodes);
        Assert.NotEmpty(visual.Nodes);
        Assert.NotEmpty(logical.Nodes);

        var target = combined.Nodes.First(node => node.Depth >= 1);
        var properties = await session.ReadOnly.GetPropertiesSnapshotAsync(new RemotePropertiesSnapshotRequest
        {
            Scope = "combined",
            NodeId = target.NodeId,
            NodePath = target.NodePath,
            IncludeClrProperties = true,
        });
        Assert.True(properties.SnapshotVersion > 0);
        Assert.NotEmpty(properties.Properties);

        var bindings = await session.ReadOnly.GetBindingsSnapshotAsync(new RemoteBindingsSnapshotRequest
        {
            Scope = "combined",
            NodeId = target.NodeId,
            NodePath = target.NodePath,
        });
        Assert.True(bindings.SnapshotVersion > 0);

        var styles = await session.ReadOnly.GetStylesSnapshotAsync(new RemoteStylesSnapshotRequest
        {
            Scope = "combined",
            NodeId = target.NodeId,
            NodePath = target.NodePath,
        });
        Assert.True(styles.SnapshotVersion > 0);
    }

    [AvaloniaFact]
    public async Task RawWebSocketProtocol_CamelCaseTreeRequest_Returns_ResponsePayload()
    {
        var port = AllocateTcpPort();
        var options = new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = port,
                Path = "/attach",
                BindingMode = HttpAttachBindingMode.Localhost,
                ReceiveTimeout = TimeSpan.FromSeconds(10),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromSeconds(30),
                    MaxFramePayloadBytes = RemoteProtocol.MaxFramePayloadBytes,
                },
            },
            RequestTimeout = TimeSpan.FromSeconds(30),
        };

        var root = CreateDenseWindow(controlCount: 800);
        await using var host = new DevToolsRemoteAttachHost(root, options);
        await host.StartAsync();

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(host.WebSocketEndpoint, CancellationToken.None);

        var sessionId = Guid.NewGuid();
        await SendAsync(
            socket,
            new RemoteHelloMessage(
                SessionId: sessionId,
                ProcessId: 101,
                ProcessName: "integration-web-client",
                ApplicationName: "integration-web-client",
                MachineName: Environment.MachineName,
                RuntimeVersion: Environment.Version.ToString(),
                ClientName: "integration-web-client",
                RequestedFeatures: new[] { "read-only", "trees" }));

        _ = Assert.IsType<RemoteHelloAckMessage>(
            await ReceiveUntilNotKeepAliveAsync(socket, TimeSpan.FromSeconds(10)));

        await SendAsync(
            socket,
            new RemoteRequestMessage(
                SessionId: sessionId,
                RequestId: 501,
                Method: RemoteReadOnlyMethods.TreeSnapshotGet,
                PayloadJson: "{\"scope\":\"combined\",\"includeSourceLocations\":false}"));

        var response = Assert.IsType<RemoteResponseMessage>(
            await ReceiveUntilNotKeepAliveAsync(socket, TimeSpan.FromSeconds(20)));
        Assert.True(response.IsSuccess);

        using var payload = JsonDocument.Parse(response.PayloadJson);
        var rootElement = payload.RootElement;
        Assert.Equal("combined", rootElement.GetProperty("scope").GetString());
        Assert.True(rootElement.GetProperty("nodes").GetArrayLength() > 0);
    }

    private static Window CreateDenseWindow(int controlCount)
    {
        var stack = new StackPanel
        {
            Name = "DenseStack",
        };

        for (var index = 0; index < controlCount; index++)
        {
            stack.Children.Add(new Border
            {
                Name = "DenseBorder" + index,
                Child = new StackPanel
                {
                    Name = "DenseInner" + index,
                    Children =
                    {
                        new TextBlock
                        {
                            Name = "DenseText" + index,
                            Text = "Row " + index,
                        },
                        new TextBox
                        {
                            Name = "DenseInput" + index,
                            Text = "Value " + index,
                        },
                    },
                },
            });
        }

        return new Window
        {
            Name = "DenseRootWindow",
            Width = 1200,
            Height = 900,
            Content = new ScrollViewer
            {
                Content = stack,
            },
        };
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
}
