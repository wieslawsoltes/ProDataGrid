using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Hosts remote attach sessions over HTTP WebSockets.
/// </summary>
public sealed class HttpAttachServer : IAttachServer
{
    private const string TransportName = "http";
    private const string LogCategory = "remote.attach.http.server";
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaxHandshakeBytes = 16 * 1024;

    private readonly HttpAttachServerOptions _httpOptions;
    private readonly IRemoteAccessPolicy _accessPolicy;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly object _sync = new();
    private readonly List<HttpAttachConnection> _connections = new();
    private readonly List<Task> _clientTasks = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _lifecycleCts;
    private Task? _acceptLoopTask;
    private Task? _heartbeatLoopTask;
    private long _keepAliveSequence;
    private bool _isRunning;

    public HttpAttachServer(
        HttpAttachServerOptions options,
        IRemoteAccessPolicy? accessPolicy = null,
        IRemoteProtocolMonitor? protocolMonitor = null,
        IRemoteDiagnosticsLogger? diagnosticsLogger = null)
    {
        _httpOptions = HttpAttachServerOptions.Normalize(options);
        _accessPolicy = accessPolicy ?? new DefaultRemoteAccessPolicy(_httpOptions.AccessPolicy);
        _protocolMonitor = protocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = diagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
        Options = _httpOptions.ServerOptions;
    }

    public event EventHandler<AttachConnectionAcceptedEventArgs>? ConnectionAccepted;

    public AttachServerOptions Options { get; }

    public Exception? LastError { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }

            var listener = new TcpListener(GetBindAddress(), _httpOptions.Port);
            listener.Start();

            var lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _listener = listener;
            _lifecycleCts = lifecycleCts;
            _isRunning = true;
            _acceptLoopTask = RunAcceptLoopAsync(listener, lifecycleCts.Token);
            _heartbeatLoopTask = RunHeartbeatLoopAsync(lifecycleCts.Token);
        }

        Log(
            RemoteDiagnosticsLogLevel.Information,
            "started",
            Guid.Empty,
            null,
            null,
            0,
            "Listening on " + GetBindAddress() + ":" + _httpOptions.Port + _httpOptions.Path,
            null);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        List<HttpAttachConnection> connections;
        List<Task> clientTasks;
        Task? acceptLoop;
        Task? heartbeatLoop;
        TcpListener? listener;
        CancellationTokenSource? lifecycleCts;

        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            connections = _connections.ToList();
            clientTasks = _clientTasks.ToList();
            acceptLoop = _acceptLoopTask;
            heartbeatLoop = _heartbeatLoopTask;
            listener = _listener;
            lifecycleCts = _lifecycleCts;

            _connections.Clear();
            _clientTasks.Clear();
            _acceptLoopTask = null;
            _heartbeatLoopTask = null;
            _listener = null;
            _lifecycleCts = null;
        }

        lifecycleCts?.Cancel();
        try
        {
            listener?.Stop();
        }
        catch
        {
            // no-op
        }

        if (acceptLoop is not null)
        {
            await ObserveTaskAsync(acceptLoop).ConfigureAwait(false);
        }

        if (heartbeatLoop is not null)
        {
            await ObserveTaskAsync(heartbeatLoop).ConfigureAwait(false);
        }

        if (clientTasks.Count > 0)
        {
            await Task.WhenAll(clientTasks.Select(ObserveTaskAsync)).ConfigureAwait(false);
        }

        foreach (var connection in connections)
        {
            try
            {
                await connection.CloseAsync("Server stopping", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // no-op
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        lifecycleCts?.Dispose();
        Log(
            RemoteDiagnosticsLogLevel.Information,
            "stopped",
            Guid.Empty,
            null,
            null,
            0,
            null,
            null);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RunAcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "accept-failed",
                    Guid.Empty,
                    null,
                    null,
                    0,
                    ex.Message,
                    ex);
                continue;
            }

            var task = HandleClientAsync(client, cancellationToken);
            TrackClientTask(task);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        var remoteEndpoint = client.Client.RemoteEndPoint?.ToString();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_httpOptions.ReceiveTimeout);
            var stream = client.GetStream();

            var requestText = await ReadHandshakeRequestAsync(stream, linkedCts.Token).ConfigureAwait(false);
            if (!TryParseHandshakeRequest(requestText, out var requestLine, out var headers))
            {
                _protocolMonitor.RecordReceiveFailure(TransportName, Guid.Empty, remoteEndpoint, "Invalid handshake request");
                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "invalid-handshake-request",
                    Guid.Empty,
                    remoteEndpoint,
                    null,
                    0,
                    "Invalid handshake request.",
                    null);
                await WriteHttpErrorAsync(stream, 400, "Invalid handshake request", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!IsValidWebSocketUpgrade(requestLine, headers, out var secWebSocketKey))
            {
                _protocolMonitor.RecordReceiveFailure(TransportName, Guid.Empty, remoteEndpoint, "Invalid websocket upgrade request");
                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "invalid-upgrade-request",
                    Guid.Empty,
                    remoteEndpoint,
                    null,
                    0,
                    "Invalid websocket upgrade request.",
                    null);
                await WriteHttpErrorAsync(stream, 400, "Invalid websocket upgrade request", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!_httpOptions.MatchesRequestPath(requestLine.Path))
            {
                Log(
                    RemoteDiagnosticsLogLevel.Debug,
                    "not-found",
                    Guid.Empty,
                    remoteEndpoint,
                    null,
                    0,
                    "Request path did not match configured endpoint.",
                    null);
                await WriteHttpErrorAsync(stream, 404, "Not found", cancellationToken).ConfigureAwait(false);
                return;
            }

            var remoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
            var accessDecision = await _accessPolicy.EvaluateAsync(
                    new RemoteAccessRequest(
                        TransportName: "http",
                        RemoteEndpoint: remoteEndpoint,
                        RemoteAddress: remoteAddress,
                        AccessToken: ExtractAccessToken(headers),
                        IsNetworkTransport: true),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!accessDecision.IsAllowed)
            {
                _protocolMonitor.RecordConnectionRejected(
                    TransportName,
                    remoteEndpoint,
                    accessDecision.Code,
                    accessDecision.Message);
                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "access-denied",
                    Guid.Empty,
                    remoteEndpoint,
                    null,
                    0,
                    accessDecision.Code + ": " + accessDecision.Message,
                    null);
                await WriteHttpErrorAsync(
                        stream,
                        MapHttpStatusCode(accessDecision.Code),
                        accessDecision.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var acceptKey = ComputeWebSocketAcceptKey(secWebSocketKey);
            await WriteHandshakeResponseAsync(stream, acceptKey, cancellationToken).ConfigureAwait(false);

            var webSocket = WebSocket.CreateFromStream(
                stream,
                isServer: true,
                subProtocol: null,
                keepAliveInterval: Options.HeartbeatInterval);

            var connection = new HttpAttachConnection(
                connectionId: Guid.NewGuid(),
                webSocket: webSocket,
                remoteEndpoint: remoteEndpoint,
                receiveTimeout: _httpOptions.ReceiveTimeout,
                maxPayloadBytes: Options.MaxFramePayloadBytes,
                protocolMonitor: _protocolMonitor,
                diagnosticsLogger: _diagnosticsLogger);

            lock (_sync)
            {
                _connections.Add(connection);
            }

            _protocolMonitor.RecordConnectionAccepted(TransportName, connection.ConnectionId, connection.RemoteEndpoint);
            Log(
                RemoteDiagnosticsLogLevel.Information,
                "connection-accepted",
                connection.ConnectionId,
                connection.RemoteEndpoint,
                null,
                0,
                null,
                null);

            ConnectionAccepted?.Invoke(
                this,
                new AttachConnectionAcceptedEventArgs(connection, DateTimeOffset.UtcNow));

            try
            {
                await connection.WaitForCloseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await connection.CloseAsync("Connection aborted", CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                RemoveConnection(connection);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
            _protocolMonitor.RecordReceiveFailure(TransportName, Guid.Empty, remoteEndpoint, ex.Message);
            Log(
                RemoteDiagnosticsLogLevel.Error,
                "handle-client-failed",
                Guid.Empty,
                remoteEndpoint,
                null,
                0,
                ex.Message,
                ex);
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Options.HeartbeatInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            List<HttpAttachConnection> snapshot;
            lock (_sync)
            {
                snapshot = _connections.ToList();
            }

            foreach (var connection in snapshot)
            {
                if (!connection.IsOpen)
                {
                    RemoveConnection(connection);
                    continue;
                }

                try
                {
                    var keepAlive = new RemoteKeepAliveMessage(
                        SessionId: Guid.Empty,
                        Sequence: Interlocked.Increment(ref _keepAliveSequence),
                        TimestampUtc: DateTimeOffset.UtcNow);
                    await connection.SendAsync(keepAlive, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Log(
                        RemoteDiagnosticsLogLevel.Warning,
                        "heartbeat-send-failed",
                        connection.ConnectionId,
                        connection.RemoteEndpoint,
                        RemoteMessageKind.KeepAlive,
                        0,
                        ex.Message,
                        ex);
                    try
                    {
                        await connection.CloseAsync("Heartbeat failure", cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // no-op
                    }

                    RemoveConnection(connection);
                }
            }
        }
    }

    private static async Task<string> ReadHandshakeRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var rented = ArrayPool<byte>.Shared.Rent(2048);
        var aggregate = new byte[MaxHandshakeBytes];
        var total = 0;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(rented.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new InvalidOperationException("Connection closed before handshake completed.");
                }

                if (total + read > MaxHandshakeBytes)
                {
                    throw new InvalidOperationException("Handshake header too large.");
                }

                rented.AsSpan(0, read).CopyTo(aggregate.AsSpan(total, read));
                total += read;

                if (total >= 4 &&
                    aggregate[total - 4] == '\r' &&
                    aggregate[total - 3] == '\n' &&
                    aggregate[total - 2] == '\r' &&
                    aggregate[total - 1] == '\n')
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(aggregate, 0, total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool TryParseHandshakeRequest(
        string requestText,
        out (string Method, string Path, string Protocol) requestLine,
        out Dictionary<string, string> headers)
    {
        requestLine = default;
        headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length < 2)
        {
            return false;
        }

        var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        requestLine = (parts[0], parts[1], parts[2]);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            headers[key] = value;
        }

        return true;
    }

    private static bool IsValidWebSocketUpgrade(
        (string Method, string Path, string Protocol) requestLine,
        Dictionary<string, string> headers,
        out string secWebSocketKey)
    {
        secWebSocketKey = string.Empty;
        if (!string.Equals(requestLine.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(requestLine.Protocol, "HTTP/1.1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!headers.TryGetValue("Connection", out var connectionHeader) ||
            connectionHeader.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!headers.TryGetValue("Upgrade", out var upgradeHeader) ||
            !string.Equals(upgradeHeader, "websocket", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!headers.TryGetValue("Sec-WebSocket-Version", out var versionHeader) ||
            !string.Equals(versionHeader, "13", StringComparison.Ordinal))
        {
            return false;
        }

        if (!headers.TryGetValue("Sec-WebSocket-Key", out var keyHeader) ||
            string.IsNullOrWhiteSpace(keyHeader))
        {
            return false;
        }

        secWebSocketKey = keyHeader;
        return true;
    }

    private static string ComputeWebSocketAcceptKey(string secWebSocketKey)
    {
        var keyBytes = Encoding.ASCII.GetBytes(secWebSocketKey + WebSocketGuid);
        var hashBytes = SHA1.HashData(keyBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static async Task WriteHandshakeResponseAsync(
        NetworkStream stream,
        string secWebSocketAccept,
        CancellationToken cancellationToken)
    {
        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Accept: " + secWebSocketAccept + "\r\n" +
            "\r\n";
        var bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteHttpErrorAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        CancellationToken cancellationToken)
    {
        var response =
            "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n" +
            "Content-Length: 0\r\n" +
            "Connection: close\r\n" +
            "\r\n";
        var bytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int MapHttpStatusCode(RemoteAccessDecisionCode decisionCode) =>
        decisionCode switch
        {
            RemoteAccessDecisionCode.Unauthorized => 401,
            _ => 403,
        };

    private static string? ExtractAccessToken(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue("X-ProDiagnostics-Token", out var customToken) &&
            !string.IsNullOrWhiteSpace(customToken))
        {
            return customToken.Trim();
        }

        if (headers.TryGetValue("Authorization", out var authorizationHeader) &&
            !string.IsNullOrWhiteSpace(authorizationHeader))
        {
            const string bearerPrefix = "Bearer ";
            if (authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = authorizationHeader[bearerPrefix.Length..].Trim();
                return token.Length == 0 ? null : token;
            }
        }

        return null;
    }

    private IPAddress GetBindAddress()
    {
        return _httpOptions.BindingMode switch
        {
            HttpAttachBindingMode.Localhost => IPAddress.Loopback,
            HttpAttachBindingMode.Any => IPAddress.Any,
            HttpAttachBindingMode.ExplicitAddress => _httpOptions.BindAddress ?? IPAddress.Loopback,
            _ => IPAddress.Loopback,
        };
    }

    private void TrackClientTask(Task task)
    {
        lock (_sync)
        {
            _clientTasks.Add(task);
        }

        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var @this = (HttpAttachServer)state!;
                lock (@this._sync)
                {
                    @this._clientTasks.Remove(completed);
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void RemoveConnection(HttpAttachConnection connection)
    {
        lock (_sync)
        {
            _connections.Remove(connection);
        }
    }

    private static async Task ObserveTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // no-op during shutdown
        }
    }

    private void Log(
        RemoteDiagnosticsLogLevel level,
        string eventName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind? messageKind,
        int bytes,
        string? details,
        Exception? exception)
    {
        _diagnosticsLogger.Log(
            new RemoteDiagnosticsLogEntry(
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: level,
                Category: LogCategory,
                EventName: eventName,
                TransportName: TransportName,
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                MessageKind: messageKind,
                Bytes: bytes,
                Details: details,
                ExceptionType: exception?.GetType().Name));
    }
}
