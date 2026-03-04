using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Default .NET remote diagnostics protocol client.
/// </summary>
public sealed class RemoteDiagnosticsClient : IRemoteDiagnosticsClient
{
    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Dictionary<long, PendingRequest> _pendingRequests = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoopTask;
    private TaskCompletionSource<RemoteHelloAckMessage>? _helloAckTcs;
    private TaskCompletionSource<RemoteHelloRejectMessage>? _helloRejectTcs;
    private RemoteDiagnosticsClientOptions _options = RemoteDiagnosticsClientOptions.Default;
    private RemoteDiagnosticsClientStatus _status = RemoteDiagnosticsClientStatus.Offline;
    private Guid _sessionId;
    private Uri? _endpoint;
    private byte _negotiatedProtocolVersion;
    private IReadOnlyList<string> _enabledFeatures = Array.Empty<string>();
    private long _nextRequestId = 1;
    private bool _isDisposed;

    private DateTimeOffset? _connectedAtUtc;
    private DateTimeOffset? _disconnectedAtUtc;
    private DateTimeOffset? _lastSendUtc;
    private DateTimeOffset? _lastReceiveUtc;
    private long _sentMessages;
    private long _receivedMessages;
    private long _sentBytes;
    private long _receivedBytes;
    private long _keepAliveCount;

    public bool IsConnected => Status == RemoteDiagnosticsClientStatus.Online;

    public RemoteDiagnosticsClientStatus Status
    {
        get
        {
            lock (_stateGate)
            {
                return _status;
            }
        }
    }

    public Guid SessionId
    {
        get
        {
            lock (_stateGate)
            {
                return _sessionId;
            }
        }
    }

    public Uri? Endpoint
    {
        get
        {
            lock (_stateGate)
            {
                return _endpoint;
            }
        }
    }

    public byte NegotiatedProtocolVersion
    {
        get
        {
            lock (_stateGate)
            {
                return _negotiatedProtocolVersion;
            }
        }
    }

    public IReadOnlyList<string> EnabledFeatures
    {
        get
        {
            lock (_stateGate)
            {
                return _enabledFeatures;
            }
        }
    }

    public event EventHandler<RemoteDiagnosticsClientStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<RemoteStreamReceivedEventArgs>? StreamReceived;

    public event EventHandler<RemoteKeepAliveMessage>? KeepAliveReceived;

    public event EventHandler<RemoteDiagnosticsClientErrorEventArgs>? Error;

    public async ValueTask ConnectAsync(
        Uri endpoint,
        RemoteDiagnosticsClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!string.Equals(endpoint.Scheme, "ws", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Remote diagnostics endpoint must use ws/wss scheme.", nameof(endpoint));
        }

        ThrowIfDisposed();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket is not null)
            {
                await DisconnectCoreAsync("Reconnect", cancellationToken).ConfigureAwait(false);
            }

            var normalizedOptions = RemoteDiagnosticsClientOptions.Normalize(options);
            var socket = new ClientWebSocket();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(normalizedOptions.ConnectTimeout);
            await socket.ConnectAsync(endpoint, connectCts.Token).ConfigureAwait(false);

            var sessionId = Guid.NewGuid();
            var connectionCts = new CancellationTokenSource();
            var helloAckTcs = new TaskCompletionSource<RemoteHelloAckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var helloRejectTcs = new TaskCompletionSource<RemoteHelloRejectMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateGate)
            {
                _socket = socket;
                _connectionCts = connectionCts;
                _options = normalizedOptions;
                _sessionId = sessionId;
                _endpoint = endpoint;
                _negotiatedProtocolVersion = 0;
                _enabledFeatures = Array.Empty<string>();
                _status = RemoteDiagnosticsClientStatus.Connecting;
                _helloAckTcs = helloAckTcs;
                _helloRejectTcs = helloRejectTcs;
                ResetTransportStats(DateTimeOffset.UtcNow);
            }

            RaiseStatusChanged(RemoteDiagnosticsClientStatus.Connecting, endpoint, null);
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(socket, connectionCts.Token));

            await SendCoreAsync(
                    socket,
                    new RemoteHelloMessage(
                        SessionId: sessionId,
                        ProcessId: Environment.ProcessId,
                        ProcessName: Process.GetCurrentProcess().ProcessName,
                        ApplicationName: AppDomain.CurrentDomain.FriendlyName,
                        MachineName: Environment.MachineName,
                        RuntimeVersion: Environment.Version.ToString(),
                        ClientName: normalizedOptions.ClientName,
                        RequestedFeatures: normalizedOptions.RequestedFeatures ?? Array.Empty<string>()),
                    cancellationToken)
                .ConfigureAwait(false);

            var helloAck = await WaitForHandshakeAsync(
                    helloAckTcs.Task,
                    helloRejectTcs.Task,
                    normalizedOptions.ConnectTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            lock (_stateGate)
            {
                _negotiatedProtocolVersion = helloAck.NegotiatedProtocolVersion;
                _enabledFeatures = helloAck.EnabledFeatures;
                _helloAckTcs = null;
                _helloRejectTcs = null;
                _status = RemoteDiagnosticsClientStatus.Online;
            }

            RaiseStatusChanged(RemoteDiagnosticsClientStatus.Online, endpoint, null);
        }
        catch
        {
            await DisconnectCoreAsync("Connect failed", CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAsync(
        string reason = "Client disconnect",
        CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(reason, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<TResponse> RequestAsync<TRequest, TResponse>(
        string method,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(requestTypeInfo);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);
        ThrowIfDisposed();

        PendingRequest pending;
        long requestId;
        Guid sessionId;
        ClientWebSocket socket;
        TimeSpan effectiveTimeout;

        lock (_stateGate)
        {
            if (_status != RemoteDiagnosticsClientStatus.Online ||
                _socket is null ||
                _socket.State is not WebSocketState.Open and not WebSocketState.CloseReceived)
            {
                throw new InvalidOperationException("Remote diagnostics client is not connected.");
            }

            requestId = _nextRequestId++;
            sessionId = _sessionId;
            socket = _socket;
            effectiveTimeout = timeout is { } explicitTimeout && explicitTimeout > TimeSpan.Zero
                ? explicitTimeout
                : _options.RequestTimeout;
            pending = new PendingRequest(method);
            _pendingRequests[requestId] = pending;
        }

        var payloadJson = JsonSerializer.Serialize(request, requestTypeInfo);
        var message = new RemoteRequestMessage(
            SessionId: sessionId,
            RequestId: requestId,
            Method: method,
            PayloadJson: payloadJson);

        try
        {
            await SendCoreAsync(socket, message, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            RemovePendingRequest(requestId);
            throw;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(effectiveTimeout);

        RemoteResponseMessage response;
        try
        {
            response = await pending.Completion.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RemovePendingRequest(requestId);
            throw new TimeoutException("Remote request timed out: " + method);
        }

        if (!response.IsSuccess)
        {
            throw new RemoteDiagnosticsRequestException(
                method,
                requestId,
                response.ErrorCode,
                response.ErrorMessage);
        }

        var payload = JsonSerializer.Deserialize(response.PayloadJson, responseTypeInfo);
        if (payload is null)
        {
            throw new InvalidOperationException(
                "Remote response payload could not be deserialized for method " + method + ".");
        }

        return payload;
    }

    public RemoteDiagnosticsClientTransportSnapshot GetTransportSnapshot()
    {
        lock (_stateGate)
        {
            return new RemoteDiagnosticsClientTransportSnapshot(
                _connectedAtUtc,
                _disconnectedAtUtc,
                _lastSendUtc,
                _lastReceiveUtc,
                _sentMessages,
                _receivedMessages,
                _sentBytes,
                _receivedBytes,
                _keepAliveCount);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            await DisconnectCoreAsync("Disposed", CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
            _sendGate.Dispose();
        }
    }

    private async Task<RemoteHelloAckMessage> WaitForHandshakeAsync(
        Task<RemoteHelloAckMessage> helloAckTask,
        Task<RemoteHelloRejectMessage> helloRejectTask,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);
        var completed = await Task.WhenAny(helloAckTask, helloRejectTask, timeoutTask).ConfigureAwait(false);

        if (completed == helloAckTask)
        {
            return await helloAckTask.ConfigureAwait(false);
        }

        if (completed == helloRejectTask)
        {
            var reject = await helloRejectTask.ConfigureAwait(false);
            throw new InvalidOperationException(
                "Remote attach rejected: " + reject.Reason + " " + reject.Details);
        }

        throw new TimeoutException("Remote attach handshake timed out.");
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        string disconnectReason = "Remote connection closed.";
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await ReceiveCoreAsync(socket, cancellationToken).ConfigureAwait(false);
                if (received.Message is null)
                {
                    disconnectReason = received.CloseDescription ?? disconnectReason;
                    break;
                }

                RecordReceived(received.Message.Kind, received.ByteCount);

                switch (received.Message)
                {
                    case RemoteHelloAckMessage helloAck:
                        _helloAckTcs?.TrySetResult(helloAck);
                        break;
                    case RemoteHelloRejectMessage helloReject:
                        _helloRejectTcs?.TrySetResult(helloReject);
                        break;
                    case RemoteKeepAliveMessage keepAlive:
                        IncrementKeepAlive();
                        KeepAliveReceived?.Invoke(this, keepAlive);
                        break;
                    case RemoteResponseMessage response:
                        CompletePendingRequest(response.RequestId, response);
                        break;
                    case RemoteErrorMessage error:
                        CompletePendingRequestWithException(
                            error.RelatedRequestId,
                            new RemoteDiagnosticsRequestException(
                                method: "unknown",
                                requestId: error.RelatedRequestId,
                                errorCode: error.ErrorCode,
                                errorMessage: error.ErrorMessage));
                        break;
                    case RemoteStreamMessage stream:
                        StreamReceived?.Invoke(this, new RemoteStreamReceivedEventArgs(stream));
                        break;
                    case RemoteDisconnectMessage disconnect:
                        disconnectReason = string.IsNullOrWhiteSpace(disconnect.Reason)
                            ? "Remote disconnected."
                            : disconnect.Reason;
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            disconnectReason = "Disconnected.";
        }
        catch (Exception ex)
        {
            disconnectReason = ex.Message;
            Error?.Invoke(this, new RemoteDiagnosticsClientErrorEventArgs("receive", ex));
        }
        finally
        {
            FailAllPendingRequests(new InvalidOperationException("Disconnected: " + disconnectReason));
            SetOffline(disconnectReason);
            await DisposeSocketIfOwnedAsync(socket).ConfigureAwait(false);
        }
    }

    private async ValueTask DisconnectCoreAsync(string reason, CancellationToken cancellationToken)
    {
        ClientWebSocket? socket;
        CancellationTokenSource? connectionCts;
        Task? receiveLoopTask;
        Guid sessionId;
        Uri? endpoint;
        bool wasConnected;

        lock (_stateGate)
        {
            socket = _socket;
            connectionCts = _connectionCts;
            receiveLoopTask = _receiveLoopTask;
            sessionId = _sessionId;
            endpoint = _endpoint;
            wasConnected = socket is not null;
            _socket = null;
            _connectionCts = null;
            _receiveLoopTask = null;
            _helloAckTcs = null;
            _helloRejectTcs = null;
            if (_status != RemoteDiagnosticsClientStatus.Offline)
            {
                _status = RemoteDiagnosticsClientStatus.Disconnecting;
            }
        }

        if (!wasConnected || socket is null)
        {
            SetOffline(reason);
            return;
        }

        RaiseStatusChanged(RemoteDiagnosticsClientStatus.Disconnecting, endpoint, reason);

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    var disconnect = new RemoteDisconnectMessage(sessionId, reason);
                    await SendCoreAsync(socket, disconnect, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best effort disconnect signal.
                }
            }
        }
        finally
        {
            connectionCts?.Cancel();
            connectionCts?.Dispose();
        }

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                closeCts.CancelAfter(_options.CloseTimeout);
                await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        reason,
                        closeCts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore close failures.
        }

        if (receiveLoopTask is not null)
        {
            try
            {
                using var joinCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                joinCts.CancelAfter(_options.CloseTimeout);
                await receiveLoopTask.WaitAsync(joinCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Ignore loop join failures; we still force local cleanup.
            }
        }

        FailAllPendingRequests(new InvalidOperationException("Disconnected: " + reason));
        SetOffline(reason);
        await DisposeSocketIfOwnedAsync(socket).ConfigureAwait(false);
    }

    private async Task SendCoreAsync(ClientWebSocket socket, IRemoteMessage message, CancellationToken cancellationToken)
    {
        var frame = RemoteMessageSerializer.Serialize(message);
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(
                    frame,
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);
            RecordSent(message.Kind, frame.Length);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async ValueTask<(IRemoteMessage? Message, int ByteCount, string? CloseDescription)> ReceiveCoreAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var chunk = ArrayPool<byte>.Shared.Rent(_options.ReceiveBufferBytes);
        var aggregate = ArrayPool<byte>.Shared.Rent(Math.Min(_options.ReceiveBufferBytes, _options.MaxPayloadBytes));
        var total = 0;

        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return (null, 0, socket.CloseStatusDescription ?? "Closed by remote endpoint.");
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    throw new InvalidOperationException("Expected binary protocol frame.");
                }

                if (result.Count > 0)
                {
                    if (total + result.Count > _options.MaxPayloadBytes)
                    {
                        throw new InvalidOperationException("Remote frame exceeds max payload size.");
                    }

                    if (total + result.Count > aggregate.Length)
                    {
                        var target = Math.Max(total + result.Count, aggregate.Length * 2);
                        target = Math.Min(target, _options.MaxPayloadBytes);
                        var resized = ArrayPool<byte>.Shared.Rent(target);
                        aggregate.AsSpan(0, total).CopyTo(resized);
                        ArrayPool<byte>.Shared.Return(aggregate);
                        aggregate = resized;
                    }

                    chunk.AsSpan(0, result.Count).CopyTo(aggregate.AsSpan(total, result.Count));
                    total += result.Count;
                }

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            if (total == 0)
            {
                throw new InvalidOperationException("Received empty remote frame.");
            }

            if (!RemoteMessageSerializer.TryDeserialize(aggregate.AsSpan(0, total), out var message) || message is null)
            {
                throw new InvalidOperationException("Failed to deserialize remote frame.");
            }

            return (message, total, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
            ArrayPool<byte>.Shared.Return(aggregate);
        }
    }

    private void CompletePendingRequest(long requestId, RemoteResponseMessage response)
    {
        PendingRequest? pending;
        lock (_stateGate)
        {
            _pendingRequests.Remove(requestId, out pending);
        }

        pending?.Completion.TrySetResult(response);
    }

    private void CompletePendingRequestWithException(long requestId, Exception exception)
    {
        PendingRequest? pending;
        lock (_stateGate)
        {
            _pendingRequests.Remove(requestId, out pending);
        }

        pending?.Completion.TrySetException(exception);
    }

    private void RemovePendingRequest(long requestId)
    {
        lock (_stateGate)
        {
            _pendingRequests.Remove(requestId);
        }
    }

    private void FailAllPendingRequests(Exception exception)
    {
        PendingRequest[] pending;
        lock (_stateGate)
        {
            if (_pendingRequests.Count == 0)
            {
                return;
            }

            pending = new PendingRequest[_pendingRequests.Count];
            _pendingRequests.Values.CopyTo(pending, 0);
            _pendingRequests.Clear();
        }

        for (var i = 0; i < pending.Length; i++)
        {
            pending[i].Completion.TrySetException(exception);
        }
    }

    private async Task DisposeSocketIfOwnedAsync(ClientWebSocket socket)
    {
        try
        {
            socket.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch
        {
            // Ignore dispose failures.
        }
    }

    private void RecordSent(RemoteMessageKind kind, int bytes)
    {
        lock (_stateGate)
        {
            _sentMessages++;
            _sentBytes += bytes;
            _lastSendUtc = DateTimeOffset.UtcNow;
        }
    }

    private void RecordReceived(RemoteMessageKind kind, int bytes)
    {
        lock (_stateGate)
        {
            _receivedMessages++;
            _receivedBytes += bytes;
            _lastReceiveUtc = DateTimeOffset.UtcNow;
        }
    }

    private void IncrementKeepAlive()
    {
        lock (_stateGate)
        {
            _keepAliveCount++;
        }
    }

    private void ResetTransportStats(DateTimeOffset connectedAtUtc)
    {
        _connectedAtUtc = connectedAtUtc;
        _disconnectedAtUtc = null;
        _lastSendUtc = null;
        _lastReceiveUtc = null;
        _sentMessages = 0;
        _receivedMessages = 0;
        _sentBytes = 0;
        _receivedBytes = 0;
        _keepAliveCount = 0;
    }

    private void SetOffline(string? reason)
    {
        Uri? endpoint;
        var shouldNotify = false;
        lock (_stateGate)
        {
            endpoint = _endpoint;
            _sessionId = Guid.Empty;
            _negotiatedProtocolVersion = 0;
            _enabledFeatures = Array.Empty<string>();
            _endpoint = null;
            _helloAckTcs = null;
            _helloRejectTcs = null;
            if (_status != RemoteDiagnosticsClientStatus.Offline)
            {
                _status = RemoteDiagnosticsClientStatus.Offline;
                _disconnectedAtUtc = DateTimeOffset.UtcNow;
                shouldNotify = true;
            }
        }

        if (shouldNotify)
        {
            RaiseStatusChanged(RemoteDiagnosticsClientStatus.Offline, endpoint, reason);
        }
    }

    private void RaiseStatusChanged(RemoteDiagnosticsClientStatus status, Uri? endpoint, string? reason)
    {
        StatusChanged?.Invoke(this, new RemoteDiagnosticsClientStatusChangedEventArgs(status, endpoint, reason));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(RemoteDiagnosticsClient));
        }
    }

    private sealed class PendingRequest
    {
        public PendingRequest(string method)
        {
            Method = method;
            Completion = new TaskCompletionSource<RemoteResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string Method { get; }

        public TaskCompletionSource<RemoteResponseMessage> Completion { get; }
    }
}
