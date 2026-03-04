using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class HttpAttachServerTests
{
    [Fact]
    public async Task HttpAttachServer_Accepts_Disconnects_And_Reconnects()
    {
        var options = CreateOptions(receiveTimeoutMs: 1500, heartbeatMs: 500);
        await using var server = new HttpAttachServer(options);
        await server.StartAsync();

        var firstAccepted = AwaitAcceptedConnection(server);
        using var firstClient = new ClientWebSocket();
        await ConnectClientAsync(server, firstClient, options.BuildClientWebSocketUri());
        var firstConnection = await firstAccepted;
        Assert.True(firstConnection.IsOpen);

        await firstConnection.CloseAsync("test-close", CancellationToken.None);

        var closeResult = await ReceiveWebSocketMessageAsync(firstClient, TimeSpan.FromSeconds(3));
        Assert.True(closeResult.IsCloseFrame);

        var secondAccepted = AwaitAcceptedConnection(server);
        using var secondClient = new ClientWebSocket();
        await ConnectClientAsync(server, secondClient, options.BuildClientWebSocketUri());
        var secondConnection = await secondAccepted;
        Assert.True(secondConnection.IsOpen);
        Assert.NotEqual(firstConnection.ConnectionId, secondConnection.ConnectionId);

        await secondConnection.CloseAsync("done", CancellationToken.None);
    }

    [Fact]
    public async Task HttpAttachServer_Supports_ParallelSessions()
    {
        var options = CreateOptions(receiveTimeoutMs: 3000, heartbeatMs: 1000);
        await using var server = new HttpAttachServer(options);
        await server.StartAsync();

        const int clientCount = 3;
        var acceptedConnections = new List<IAttachConnection>();
        var acceptedSignal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new object();

        server.ConnectionAccepted += (_, args) =>
        {
            lock (gate)
            {
                acceptedConnections.Add(args.Connection);
                if (acceptedConnections.Count == clientCount)
                {
                    acceptedSignal.TrySetResult(null);
                }
            }
        };

        var clients = new List<ClientWebSocket>();
        for (var i = 0; i < clientCount; i++)
        {
            var client = new ClientWebSocket();
            clients.Add(client);
            await ConnectClientAsync(server, client, options.BuildClientWebSocketUri());
        }

        await acceptedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(clientCount, acceptedConnections.Count);

        var expectedRequestIds = new HashSet<long>();
        for (var i = 0; i < clients.Count; i++)
        {
            var requestId = 1000 + i;
            expectedRequestIds.Add(requestId);
            var request = new RemoteRequestMessage(
                SessionId: Guid.NewGuid(),
                RequestId: requestId,
                Method: "tree.snapshot.get",
                PayloadJson: "{\"scope\":\"combined\"}");
            await SendWebSocketMessageAsync(clients[i], request, CancellationToken.None);
        }

        var receivedRequestIds = new HashSet<long>();
        foreach (var connection in acceptedConnections)
        {
            var received = await connection.ReceiveAsync(CancellationToken.None);
            var request = Assert.IsType<RemoteRequestMessage>(received!.Value.Message);
            receivedRequestIds.Add(request.RequestId);

            var response = new RemoteResponseMessage(
                SessionId: request.SessionId,
                RequestId: request.RequestId,
                IsSuccess: true,
                PayloadJson: "{\"ok\":true}",
                ErrorCode: string.Empty,
                ErrorMessage: string.Empty);
            await connection.SendAsync(response, CancellationToken.None);
        }

        Assert.Equal(expectedRequestIds, receivedRequestIds);

        var responseIds = new HashSet<long>();
        foreach (var client in clients)
        {
            var responseMessage = await ReceiveWebSocketMessageAsync(client, TimeSpan.FromSeconds(3));
            var response = Assert.IsType<RemoteResponseMessage>(responseMessage.Message);
            responseIds.Add(response.RequestId);
        }

        Assert.Equal(expectedRequestIds, responseIds);

        foreach (var connection in acceptedConnections)
        {
            await connection.CloseAsync("done", CancellationToken.None);
            await connection.DisposeAsync();
        }

        foreach (var client in clients)
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task HttpAttachServer_Emits_Heartbeat_And_Honors_ReceiveTimeout()
    {
        var options = CreateOptions(receiveTimeoutMs: 250, heartbeatMs: 80);
        await using var server = new HttpAttachServer(options);
        await server.StartAsync();

        var acceptedConnectionTask = AwaitAcceptedConnection(server);
        using var client = new ClientWebSocket();
        await ConnectClientAsync(server, client, options.BuildClientWebSocketUri());
        var acceptedConnection = await acceptedConnectionTask;

        var heartbeat = await ReceiveWebSocketMessageAsync(client, TimeSpan.FromSeconds(3));
        Assert.IsType<RemoteKeepAliveMessage>(heartbeat.Message);

        // Do not send client data; server-side receive should timeout and close the connection.
        var timedOutReceive = await acceptedConnection.ReceiveAsync(CancellationToken.None);
        Assert.Null(timedOutReceive);
        Assert.False(acceptedConnection.IsOpen);
    }

    [Fact]
    public async Task HttpAttachServer_Returns_Unauthorized_When_AccessToken_Is_Invalid()
    {
        static ValueTask<bool> ValidateTokenAsync(
            RemoteAccessTokenValidationContext context,
            CancellationToken _)
        {
            return ValueTask.FromResult(string.Equals(context.AccessToken, "valid-token", StringComparison.Ordinal));
        }

        var options = CreateOptions(receiveTimeoutMs: 1500, heartbeatMs: 500) with
        {
            AccessPolicy = RemoteAccessPolicyOptions.Default with
            {
                AllowAnyIp = true,
                TokenValidator = ValidateTokenAsync,
            },
        };

        await using var server = new HttpAttachServer(options);
        await server.StartAsync();

        var status = await SendUpgradeRequestAsync(options.BuildClientWebSocketUri());
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task HttpAttachServer_Accepts_Connection_When_AccessToken_Is_Valid()
    {
        static ValueTask<bool> ValidateTokenAsync(
            RemoteAccessTokenValidationContext context,
            CancellationToken _)
        {
            return ValueTask.FromResult(string.Equals(context.AccessToken, "valid-token", StringComparison.Ordinal));
        }

        var options = CreateOptions(receiveTimeoutMs: 1500, heartbeatMs: 500) with
        {
            AccessPolicy = RemoteAccessPolicyOptions.Default with
            {
                AllowAnyIp = true,
                TokenValidator = ValidateTokenAsync,
            },
        };

        await using var server = new HttpAttachServer(options);
        await server.StartAsync();

        var accepted = AwaitAcceptedConnection(server);
        using var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Authorization", "Bearer valid-token");
        await ConnectClientAsync(server, client, options.BuildClientWebSocketUri());

        var connection = await accepted;
        Assert.True(connection.IsOpen);

        await connection.CloseAsync("done", CancellationToken.None);
    }

    [Fact]
    public async Task HttpAttachServer_Reports_ProtocolMonitor_And_DiagnosticsLogs()
    {
        var options = CreateOptions(receiveTimeoutMs: 3000, heartbeatMs: 10_000);
        var monitor = new InMemoryRemoteProtocolMonitor();
        var logger = new InMemoryRemoteDiagnosticsLogger();

        await using var server = new HttpAttachServer(
            options,
            protocolMonitor: monitor,
            diagnosticsLogger: logger);
        await server.StartAsync();

        var acceptedConnectionTask = AwaitAcceptedConnection(server);
        using var client = new ClientWebSocket();
        await ConnectClientAsync(server, client, options.BuildClientWebSocketUri());
        var connection = await acceptedConnectionTask;

        var request = new RemoteRequestMessage(
            SessionId: Guid.NewGuid(),
            RequestId: 42,
            Method: "tree.snapshot.get",
            PayloadJson: "{\"scope\":\"combined\"}");
        await SendWebSocketMessageAsync(client, request, CancellationToken.None);

        var received = await connection.ReceiveAsync(CancellationToken.None);
        Assert.NotNull(received);
        var requestFromClient = Assert.IsType<RemoteRequestMessage>(received.Value.Message);
        Assert.Equal(request.RequestId, requestFromClient.RequestId);

        var response = new RemoteResponseMessage(
            SessionId: requestFromClient.SessionId,
            RequestId: requestFromClient.RequestId,
            IsSuccess: true,
            PayloadJson: "{\"ok\":true}",
            ErrorCode: string.Empty,
            ErrorMessage: string.Empty);
        await connection.SendAsync(response, CancellationToken.None);

        var responseFromServer = await ReceiveWebSocketMessageAsync(client, TimeSpan.FromSeconds(3));
        Assert.IsType<RemoteResponseMessage>(responseFromServer.Message);

        await connection.CloseAsync("done", CancellationToken.None);
        await Task.Delay(100);

        var snapshot = monitor.GetSnapshot();
        Assert.Equal(1, snapshot.ConnectionsAccepted);
        Assert.True(snapshot.ConnectionsClosed >= 1);
        Assert.True(snapshot.MessagesReceived >= 1);
        Assert.True(snapshot.MessagesSent >= 1);

        var logSnapshot = logger.GetSnapshot();
        Assert.Contains(logSnapshot, entry => entry.EventName == "connection-accepted");
        Assert.Contains(logSnapshot, entry => entry.EventName == "closed");
    }

    private static HttpAttachServerOptions CreateOptions(int receiveTimeoutMs, int heartbeatMs)
    {
        var port = AllocateTcpPort();
        return HttpAttachServerOptions.Normalize(
            HttpAttachServerOptions.Default with
            {
                Port = port,
                BindingMode = HttpAttachBindingMode.Localhost,
                Path = "/attach",
                ReceiveTimeout = TimeSpan.FromMilliseconds(receiveTimeoutMs),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromMilliseconds(heartbeatMs),
                },
            });
    }

    private static Task<IAttachConnection> AwaitAcceptedConnection(HttpAttachServer server)
    {
        var tcs = new TaskCompletionSource<IAttachConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<AttachConnectionAcceptedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            server.ConnectionAccepted -= handler;
            tcs.TrySetResult(args.Connection);
        };
        server.ConnectionAccepted += handler;
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task SendWebSocketMessageAsync(
        ClientWebSocket socket,
        IRemoteMessage message,
        CancellationToken cancellationToken)
    {
        var payload = RemoteMessageSerializer.Serialize(message);
        await socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
    }

    private static async Task<(IRemoteMessage? Message, bool IsCloseFrame)> ReceiveWebSocketMessageAsync(
        ClientWebSocket socket,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var rented = ArrayPool<byte>.Shared.Rent(8 * 1024);
        var aggregate = new byte[RemoteProtocol.MaxFramePayloadBytes];
        var total = 0;
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(rented.AsMemory(), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return (null, true);
                }

                rented.AsSpan(0, result.Count).CopyTo(aggregate.AsSpan(total, result.Count));
                total += result.Count;
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            Assert.True(RemoteMessageSerializer.TryDeserialize(aggregate.AsSpan(0, total), out var message));
            return (message, false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int AllocateTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task ConnectClientAsync(HttpAttachServer server, ClientWebSocket client, Uri uri)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.ConnectAsync(uri, cts.Token);
        }
        catch (Exception ex)
        {
            var details = server.LastError?.ToString() ?? "(no server error captured)";
            var handshakeDetails = await TryCaptureHandshakeFailureAsync(uri);
            throw new InvalidOperationException(
                "Failed to connect websocket client. Server error: " + details + " Handshake: " + handshakeDetails,
                ex);
        }
    }

    private static async Task<HttpStatusCode> SendUpgradeRequestAsync(
        Uri uri,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Connection.Add("Upgrade");
        request.Headers.Upgrade.Add(new ProductHeaderValue("websocket"));
        request.Headers.Add("Sec-WebSocket-Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
        request.Headers.Add("Sec-WebSocket-Version", "13");

        if (headers is not null)
        {
            foreach (var pair in headers)
            {
                request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }
        }

        using var response = await httpClient.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<string> TryCaptureHandshakeFailureAsync(Uri uri)
    {
        try
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Connection.Add("Upgrade");
            request.Headers.Upgrade.Add(new ProductHeaderValue("websocket"));
            request.Headers.Add("Sec-WebSocket-Key", Convert.ToBase64String(Guid.NewGuid().ToByteArray()));
            request.Headers.Add("Sec-WebSocket-Version", "13");

            var response = await httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return "Status=" + (int)response.StatusCode + " Body=" + body;
        }
        catch (Exception ex)
        {
            return "Failed to capture handshake response: " + ex.GetType().Name + " " + ex.Message;
        }
    }
}
