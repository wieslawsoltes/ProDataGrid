using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

[Collection("RemoteAttachServers")]
public class NamedPipeAttachServerTests
{
    [Fact]
    public void NamedPipeAttachServerOptions_InvalidPipeName_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => NamedPipeAttachServerOptions.Normalize(
                NamedPipeAttachServerOptions.Default with { PipeName = "bad/name" }));
    }

    [Fact]
    public void NamedPipeAttachServerOptions_TooLongPipeNameOnUnix_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var tooLong = new string('a', 128);
        Assert.Throws<ArgumentException>(
            () => NamedPipeAttachServerOptions.Normalize(
                NamedPipeAttachServerOptions.Default with { PipeName = tooLong }));
    }

    [Fact]
    public async Task NamedPipeAttachServer_PlatformGuardrail_Works()
    {
        await using var server = new NamedPipeAttachServer(CreateOptions());
        if (NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            await server.StartAsync();
            await server.StopAsync();
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => server.StartAsync());
    }

    [Fact]
    public async Task NamedPipeAttachServer_Accepts_Disconnects_And_Reconnects()
    {
        if (!NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            return;
        }

        var options = CreateOptions(receiveTimeoutMs: 1500, heartbeatMs: 500);
        await using var server = new NamedPipeAttachServer(options);
        await server.StartAsync();

        await using var firstClient = CreateClient(options.PipeName);
        await using var secondClient = CreateClient(options.PipeName);
        IAttachConnection? firstConnection = null;
        IAttachConnection? secondConnection = null;

        try
        {
            var firstAccepted = AwaitAcceptedConnection(server);
            await ConnectClientAsync(firstClient);
            firstConnection = await firstAccepted;
            Assert.True(firstConnection.IsOpen);

            await firstConnection.CloseAsync("test-close", CancellationToken.None);
            var closeResult = await ReceivePipeMessageAsync(firstClient, TimeSpan.FromSeconds(3));
            Assert.True(closeResult.IsClosed);

            var secondAccepted = AwaitAcceptedConnection(server);
            await ConnectClientAsync(secondClient);
            secondConnection = await secondAccepted;
            Assert.True(secondConnection.IsOpen);
            Assert.NotEqual(firstConnection.ConnectionId, secondConnection.ConnectionId);

            await secondConnection.CloseAsync("done", CancellationToken.None);
        }
        finally
        {
            if (firstConnection is not null)
            {
                await firstConnection.DisposeAsync();
            }

            if (secondConnection is not null)
            {
                await secondConnection.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task NamedPipeAttachServer_Supports_ParallelSessions()
    {
        if (!NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            return;
        }

        var options = CreateOptions(receiveTimeoutMs: 3000, heartbeatMs: 1000);
        await using var server = new NamedPipeAttachServer(options);
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

        var clients = new List<NamedPipeClientStream>();
        try
        {
            for (var i = 0; i < clientCount; i++)
            {
                var client = CreateClient(options.PipeName);
                clients.Add(client);
                await ConnectClientAsync(client);
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
                await SendPipeMessageAsync(clients[i], request, CancellationToken.None);
            }

            var receivedRequestIds = new HashSet<long>();
            foreach (var connection in acceptedConnections)
            {
                using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var received = await connection.ReceiveAsync(receiveCts.Token);
                Assert.NotNull(received);
                var request = Assert.IsType<RemoteRequestMessage>(received.Value.Message);
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
                var responseMessage = await ReceivePipeMessageAsync(client, TimeSpan.FromSeconds(3));
                var response = Assert.IsType<RemoteResponseMessage>(responseMessage.Message);
                responseIds.Add(response.RequestId);
            }

            Assert.Equal(expectedRequestIds, responseIds);
        }
        finally
        {
            foreach (var connection in acceptedConnections)
            {
                try
                {
                    if (connection.IsOpen)
                    {
                        await connection.CloseAsync("done", CancellationToken.None);
                    }
                }
                catch
                {
                    // Ignore cleanup errors in test teardown.
                }

                await connection.DisposeAsync();
            }

            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task NamedPipeAttachServer_Emits_Heartbeat_And_Honors_ReceiveTimeout()
    {
        if (!NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            return;
        }

        var options = CreateOptions(receiveTimeoutMs: 250, heartbeatMs: 80);
        await using var server = new NamedPipeAttachServer(options);
        await server.StartAsync();

        var acceptedConnectionTask = AwaitAcceptedConnection(server);
        await using var client = CreateClient(options.PipeName);
        await ConnectClientAsync(client);
        var acceptedConnection = await acceptedConnectionTask;

        try
        {
            var heartbeat = await ReceivePipeMessageAsync(client, TimeSpan.FromSeconds(3));
            Assert.IsType<RemoteKeepAliveMessage>(heartbeat.Message);

            using var receiveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var timedOutReceive = await acceptedConnection.ReceiveAsync(receiveCts.Token);
            Assert.Null(timedOutReceive);
            Assert.False(acceptedConnection.IsOpen);
        }
        finally
        {
            await acceptedConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task NamedPipeAttachServer_Denies_Connection_When_Policy_Rejects()
    {
        if (!NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            return;
        }

        static ValueTask<bool> RejectAllTokens(
            RemoteAccessTokenValidationContext _,
            CancellationToken __)
        {
            return ValueTask.FromResult(false);
        }

        var options = CreateOptions(receiveTimeoutMs: 3000, heartbeatMs: 500) with
        {
            AccessPolicy = RemoteAccessPolicyOptions.Default with
            {
                TokenValidator = RejectAllTokens,
            },
        };

        await using var server = new NamedPipeAttachServer(options);
        await server.StartAsync();

        var accepted = false;
        server.ConnectionAccepted += (_, _) => accepted = true;

        await using var client = CreateClient(options.PipeName);
        await ConnectClientAsync(client);
        await Task.Delay(150);

        Assert.False(accepted);
    }

    private static NamedPipeAttachServerOptions CreateOptions(
        int receiveTimeoutMs = 5000,
        int heartbeatMs = 500)
    {
        var uniquePipeName = "pdg." + Guid.NewGuid().ToString("N")[..10];
        return NamedPipeAttachServerOptions.Normalize(
            NamedPipeAttachServerOptions.Default with
            {
                PipeName = uniquePipeName,
                ReceiveTimeout = TimeSpan.FromMilliseconds(receiveTimeoutMs),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromMilliseconds(heartbeatMs),
                },
            });
    }

    private static NamedPipeClientStream CreateClient(string pipeName) =>
        new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

    private static async Task ConnectClientAsync(NamedPipeClientStream client)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);
    }

    private static Task<IAttachConnection> AwaitAcceptedConnection(NamedPipeAttachServer server)
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

    private static async Task SendPipeMessageAsync(
        NamedPipeClientStream stream,
        IRemoteMessage message,
        CancellationToken cancellationToken)
    {
        var frame = RemoteMessageSerializer.Serialize(message);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, frame.Length);

        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<(IRemoteMessage? Message, bool IsClosed)> ReceivePipeMessageAsync(
        NamedPipeClientStream stream,
        TimeSpan timeout)
    {
        var header = new byte[sizeof(int)];
        if (!await TryReadExactAsync(stream, header, timeout))
        {
            return (null, true);
        }

        var frameLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (frameLength <= 0 || frameLength > RemoteProtocol.MaxFramePayloadBytes + RemoteProtocol.HeaderSizeBytes)
        {
            return (null, true);
        }

        var frame = new byte[frameLength];
        if (!await TryReadExactAsync(stream, frame, timeout))
        {
            return (null, true);
        }

        Assert.True(RemoteMessageSerializer.TryDeserialize(frame, out var message));
        return (message, false);
    }

    private static async Task<bool> TryReadExactAsync(
        NamedPipeClientStream stream,
        byte[] buffer,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var totalRead = 0;
        try
        {
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead),
                    cts.Token);
                if (read <= 0)
                {
                    return false;
                }

                totalRead += read;
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }

        return true;
    }
}
