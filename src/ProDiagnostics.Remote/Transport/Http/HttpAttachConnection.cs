using System;
using System.Buffers;
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
    private readonly TaskCompletionSource<object?> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        try
        {
            payload = RemoteMessageSerializer.Serialize(message);
        }
        catch (Exception ex)
        {
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
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
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
            _sendGate.Release();
        }
    }

    public async ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOpen)
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_receiveTimeout);

        var readBuffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var aggregate = ArrayPool<byte>.Shared.Rent(Math.Min(InitialBufferSize, _maxPayloadBytes));
        var total = 0;

        try
        {
            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(readBuffer, timeoutCts.Token).ConfigureAwait(false);
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

                    aggregate = EnsureAggregateCapacity(aggregate, total, total + result.Count, _maxPayloadBytes);
                    readBuffer.AsSpan(0, result.Count).CopyTo(aggregate.AsSpan(total, result.Count));
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

            if (!RemoteMessageSerializer.TryDeserialize(aggregate.AsSpan(0, total), out var message))
            {
                return await FailReceiveAsync(
                        WebSocketCloseStatus.InvalidPayloadData,
                        "Invalid protocol frame")
                    .ConfigureAwait(false);
            }

            _protocolMonitor.RecordMessageReceived(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message!.Kind,
                total);
            return new AttachReceiveResult(message!, total, DateTimeOffset.UtcNow);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            ArrayPool<byte>.Shared.Return(aggregate);
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
        _sendGate.Dispose();
        _webSocket.Dispose();
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

    private static byte[] EnsureAggregateCapacity(
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
        return expanded;
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
