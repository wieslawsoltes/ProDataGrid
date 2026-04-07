using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// WebSocket-backed attach connection for HTTP transport.
/// </summary>
public sealed class HttpAttachConnection : IAttachConnection
{
    private const string TransportName = "http";
    private const string LogCategory = "remote.attach.http.connection";
    private const int InitialBufferSize = 8 * 1024;

    private readonly WebSocket _webSocket;
    private readonly TimeSpan _receiveTimeout;
    private readonly int _maxPayloadBytes;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private readonly TaskCompletionSource<object?> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private byte[]? _receiveAggregateBuffer;
    private long _lastActivityTicksUtc;
    private int _closed;

    public HttpAttachConnection(
        Guid connectionId,
        WebSocket webSocket,
        string? remoteEndpoint,
        TimeSpan receiveTimeout,
        int maxPayloadBytes,
        IRemoteProtocolMonitor? protocolMonitor = null,
        IRemoteDiagnosticsLogger? diagnosticsLogger = null)
    {
        ConnectionId = connectionId;
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        RemoteEndpoint = remoteEndpoint;
        _receiveTimeout = receiveTimeout;
        _maxPayloadBytes = maxPayloadBytes > 0 ? maxPayloadBytes : RemoteProtocol.MaxFramePayloadBytes;
        _protocolMonitor = protocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = diagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
        _receiveAggregateBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(InitialBufferSize, _maxPayloadBytes));
        TouchActivity();
    }

    public Guid ConnectionId { get; }

    public string? RemoteEndpoint { get; }

    public bool IsOpen =>
        Interlocked.CompareExchange(ref _closed, 0, 0) == 0 &&
        _webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived;

    public async ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsOpen)
        {
            throw new InvalidOperationException("Attach connection is closed.");
        }

        ReadOnlyMemory<byte> payload;
        var messageMethod = RemoteRuntimeMetrics.MapMessageKind(message.Kind);
        var serializeStarted = Stopwatch.GetTimestamp();
        try
        {
            payload = RemoteMessageSerializer.Serialize(message);
            RemoteRuntimeMetrics.RecordSerializeDuration(
                transport: TransportName,
                method: messageMethod,
                status: "ok",
                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(serializeStarted));
        }
        catch (Exception ex)
        {
            RemoteRuntimeMetrics.RecordSerializeDuration(
                transport: TransportName,
                method: messageMethod,
                status: "error",
                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(serializeStarted));
            RemoteRuntimeMetrics.RecordTransportFailure(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                status: "error");
            _protocolMonitor.RecordSendFailure(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message.Kind,
                ex.Message);
            Log(
                RemoteDiagnosticsLogLevel.Warning,
                "serialize-failed",
                message.Kind,
                0,
                ex.Message,
                ex);
            throw;
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _webSocket.SendAsync(
                    payload,
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);

            _protocolMonitor.RecordMessageSent(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message.Kind,
                payload.Length);
            TouchActivity();
            RemoteRuntimeMetrics.RecordPayloadOutBytes(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                scope: "none",
                status: "ok",
                bytes: payload.Length);
        }
        catch (OperationCanceledException)
        {
            RemoteRuntimeMetrics.RecordTransportFailure(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                status: "cancelled");
            throw;
        }
        catch (Exception ex)
        {
            RemoteRuntimeMetrics.RecordTransportFailure(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                status: "error");
            _protocolMonitor.RecordSendFailure(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message.Kind,
                ex.Message);
            Log(
                RemoteDiagnosticsLogLevel.Warning,
                "send-failed",
                message.Kind,
                payload.Length,
                ex.Message,
                ex);
            await CloseInternalAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Send failure",
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            try
            {
                _sendGate.Release();
            }
            catch (ObjectDisposedException)
            {
                // Connection shutdown can race with in-flight send completion.
            }
        }
    }

    public async ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOpen)
        {
            return null;
        }

        var receiveLockAcquired = false;
        try
        {
            await _receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            receiveLockAcquired = true;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_receiveTimeout);

            var aggregate = _receiveAggregateBuffer ??=
                ArrayPool<byte>.Shared.Rent(Math.Min(InitialBufferSize, _maxPayloadBytes));
            var total = 0;

            while (true)
            {
                var minWritable = Math.Min(InitialBufferSize, _maxPayloadBytes - total);
                if (minWritable <= 0)
                {
                    return await FailReceiveAsync(
                            WebSocketCloseStatus.MessageTooBig,
                            "Payload too large")
                        .ConfigureAwait(false);
                }

                aggregate = EnsureReceiveAggregateCapacity(aggregate, total, total + minWritable, _maxPayloadBytes);
                var writableLength = Math.Min(minWritable, aggregate.Length - total);
                ValueWebSocketReceiveResult result;
                try
                {
                    result = await _webSocket
                        .ReceiveAsync(aggregate.AsMemory(total, writableLength), timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return await FailReceiveAsync(
                            WebSocketCloseStatus.PolicyViolation,
                            "Receive timeout")
                        .ConfigureAwait(false);
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseInternalAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Close frame received",
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    return await FailReceiveAsync(
                            WebSocketCloseStatus.InvalidMessageType,
                            "Expected binary frame")
                        .ConfigureAwait(false);
                }

                if (result.Count > 0)
                {
                    if (total + result.Count > _maxPayloadBytes)
                    {
                        return await FailReceiveAsync(
                                WebSocketCloseStatus.MessageTooBig,
                                "Payload too large")
                            .ConfigureAwait(false);
                    }

                    total += result.Count;
                }

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            if (total == 0)
            {
                return await FailReceiveAsync(
                        WebSocketCloseStatus.InvalidPayloadData,
                        "Empty payload")
                    .ConfigureAwait(false);
            }

            var deserializeStarted = Stopwatch.GetTimestamp();
            if (!RemoteMessageSerializer.TryDeserialize(aggregate.AsSpan(0, total), out var message))
            {
                RemoteRuntimeMetrics.RecordDeserializeDuration(
                    transport: TransportName,
                    method: "response",
                    status: "error",
                    durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(deserializeStarted));
                return await FailReceiveAsync(
                        WebSocketCloseStatus.InvalidPayloadData,
                        "Invalid protocol frame")
                    .ConfigureAwait(false);
            }

            var messageMethod = RemoteRuntimeMetrics.MapMessageKind(message!.Kind);
            RemoteRuntimeMetrics.RecordDeserializeDuration(
                transport: TransportName,
                method: messageMethod,
                status: "ok",
                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(deserializeStarted));
            _protocolMonitor.RecordMessageReceived(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message!.Kind,
                total);
            TouchActivity();
            RemoteRuntimeMetrics.RecordPayloadInBytes(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                scope: "none",
                status: "ok",
                bytes: total);
            return new AttachReceiveResult(message!, total, DateTimeOffset.UtcNow);
        }
        finally
        {
            if (receiveLockAcquired)
            {
                try
                {
                    _receiveGate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Connection shutdown can race with in-flight receive completion.
                }
            }
        }
    }

    public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default) =>
        CloseInternalAsync(WebSocketCloseStatus.NormalClosure, reason ?? "Closed", cancellationToken);

    internal Task WaitForCloseAsync(CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return _closedTcs.Task;
        }

        return _closedTcs.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseInternalAsync(
                WebSocketCloseStatus.NormalClosure,
                "Disposed",
                CancellationToken.None)
            .ConfigureAwait(false);
        var receiveGateAcquired = await TryAcquireGateAsync(_receiveGate, timeout: TimeSpan.FromMilliseconds(250))
            .ConfigureAwait(false);
        try
        {
            if (receiveGateAcquired && _receiveAggregateBuffer is { } receiveAggregate)
            {
                _receiveAggregateBuffer = null;
                ArrayPool<byte>.Shared.Return(receiveAggregate);
            }
        }
        finally
        {
            if (receiveGateAcquired)
            {
                _receiveGate.Release();
            }
        }
        _webSocket.Dispose();
    }

    internal bool HasRecentActivity(TimeSpan activityWindow)
    {
        if (activityWindow <= TimeSpan.Zero)
        {
            return false;
        }

        var lastActivityTicks = Interlocked.Read(ref _lastActivityTicksUtc);
        if (lastActivityTicks <= 0)
        {
            return false;
        }

        return DateTime.UtcNow.Ticks - lastActivityTicks <= activityWindow.Ticks;
    }

    private async ValueTask CloseInternalAsync(
        WebSocketCloseStatus status,
        string description,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            CancellationToken closeToken = cancellationToken;
            CancellationTokenSource? fallbackTimeoutCts = null;
            if (!closeToken.CanBeCanceled)
            {
                fallbackTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                closeToken = fallbackTimeoutCts.Token;
            }

            try
            {
                await _webSocket.CloseAsync(status, description, closeToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log(
                    RemoteDiagnosticsLogLevel.Debug,
                    "close-failed",
                    null,
                    0,
                    ex.Message,
                    ex);
            }
            finally
            {
                fallbackTimeoutCts?.Dispose();
            }
        }

        _protocolMonitor.RecordConnectionClosed(TransportName, ConnectionId, RemoteEndpoint, description);
        RemoteRuntimeMetrics.RecordConnectionClosed(TransportName);
        Log(
            RemoteDiagnosticsLogLevel.Information,
            "closed",
            null,
            0,
            description,
            null);
        _closedTcs.TrySetResult(null);
    }

    private async ValueTask<AttachReceiveResult?> FailReceiveAsync(
        WebSocketCloseStatus closeStatus,
        string reason)
    {
        var status = string.Equals(reason, "Receive timeout", StringComparison.OrdinalIgnoreCase)
            ? "timeout"
            : "error";
        RemoteRuntimeMetrics.RecordTransportFailure(
            transport: TransportName,
            method: "response",
            domain: "none",
            status: status);
        _protocolMonitor.RecordReceiveFailure(TransportName, ConnectionId, RemoteEndpoint, reason);
        Log(
            RemoteDiagnosticsLogLevel.Warning,
            "receive-failed",
            null,
            0,
            reason,
            null);
        await CloseInternalAsync(closeStatus, reason, CancellationToken.None).ConfigureAwait(false);
        return null;
    }

    private byte[] EnsureReceiveAggregateCapacity(
        byte[] aggregate,
        int existingLength,
        int requiredLength,
        int maxPayloadBytes)
    {
        if (requiredLength <= aggregate.Length)
        {
            return aggregate;
        }

        var newSize = aggregate.Length > 0 ? aggregate.Length : InitialBufferSize;
        while (newSize < requiredLength && newSize < maxPayloadBytes)
        {
            var doubled = newSize * 2;
            newSize = doubled <= 0
                ? maxPayloadBytes
                : Math.Min(doubled, maxPayloadBytes);
        }

        if (newSize < requiredLength)
        {
            throw new InvalidOperationException("Required payload size exceeds configured max payload bytes.");
        }

        var expanded = ArrayPool<byte>.Shared.Rent(newSize);
        aggregate.AsSpan(0, existingLength).CopyTo(expanded.AsSpan());
        ArrayPool<byte>.Shared.Return(aggregate);
        _receiveAggregateBuffer = expanded;
        return expanded;
    }

    private void TouchActivity()
    {
        Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
    }

    private static async Task<bool> TryAcquireGateAsync(SemaphoreSlim gate, TimeSpan timeout)
    {
        try
        {
            return await gate.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void Log(
        RemoteDiagnosticsLogLevel level,
        string eventName,
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
                ConnectionId: ConnectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: RemoteEndpoint,
                MessageKind: messageKind,
                Bytes: bytes,
                Details: details,
                ExceptionType: exception?.GetType().Name));
    }
}
