using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Named-pipe-backed attach connection.
/// </summary>
public sealed class NamedPipeAttachConnection : IAttachConnection
{
    private const string TransportName = "namedpipe";
    private const string LogCategory = "remote.attach.namedpipe.connection";

    private readonly NamedPipeServerStream _pipe;
    private readonly TimeSpan _receiveTimeout;
    private readonly int _maxSerializedFrameBytes;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly TaskCompletionSource<object?> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _closed;

    public NamedPipeAttachConnection(
        Guid connectionId,
        NamedPipeServerStream pipe,
        string? remoteEndpoint,
        TimeSpan receiveTimeout,
        int maxPayloadBytes,
        IRemoteProtocolMonitor? protocolMonitor = null,
        IRemoteDiagnosticsLogger? diagnosticsLogger = null)
    {
        ConnectionId = connectionId;
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
        RemoteEndpoint = remoteEndpoint;
        _receiveTimeout = receiveTimeout;
        var effectiveMaxPayload = maxPayloadBytes > 0 ? maxPayloadBytes : RemoteProtocol.MaxFramePayloadBytes;
        _maxSerializedFrameBytes = effectiveMaxPayload + RemoteProtocol.HeaderSizeBytes;
        _protocolMonitor = protocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = diagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
    }

    public Guid ConnectionId { get; }

    public string? RemoteEndpoint { get; }

    public bool IsOpen =>
        Interlocked.CompareExchange(ref _closed, 0, 0) == 0 &&
        _pipe.IsConnected;

    public async ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsOpen)
        {
            throw new InvalidOperationException("Attach connection is closed.");
        }

        var frame = RemoteMessageSerializer.Serialize(message);
        if (frame.Length > _maxSerializedFrameBytes)
        {
            throw new InvalidOperationException(
                "Serialized frame exceeds max allowed size of " + _maxSerializedFrameBytes + " bytes.");
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var header = ArrayPool<byte>.Shared.Rent(sizeof(int));
            try
            {
                BinaryPrimitives.WriteInt32LittleEndian(header, frame.Length);
                await _pipe.WriteAsync(header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
                await _pipe.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }

            _protocolMonitor.RecordMessageSent(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message.Kind,
                frame.Length);
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
            Log(RemoteDiagnosticsLogLevel.Warning, "send-failed", message.Kind, frame.Length, ex.Message, ex);
            await CloseInternalAsync("Send failure").ConfigureAwait(false);
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

        var header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            if (!await TryReadExactAsync(header.AsMemory(0, sizeof(int)), _receiveTimeout, cancellationToken).ConfigureAwait(false))
            {
                return await FailReceiveAsync("Failed to read frame header").ConfigureAwait(false);
            }

            var frameLength = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (frameLength <= 0 || frameLength > _maxSerializedFrameBytes)
            {
                return await FailReceiveAsync("Invalid frame length: " + frameLength).ConfigureAwait(false);
            }

            var frame = ArrayPool<byte>.Shared.Rent(frameLength);
            try
            {
                if (!await TryReadExactAsync(frame.AsMemory(0, frameLength), _receiveTimeout, cancellationToken).ConfigureAwait(false))
                {
                    return await FailReceiveAsync("Failed to read frame payload").ConfigureAwait(false);
                }

                if (!RemoteMessageSerializer.TryDeserialize(frame.AsSpan(0, frameLength), out var message))
                {
                    return await FailReceiveAsync("Invalid protocol frame").ConfigureAwait(false);
                }

                _protocolMonitor.RecordMessageReceived(
                    TransportName,
                    ConnectionId,
                    RemoteEndpoint,
                    message!.Kind,
                    frameLength);
                return new AttachReceiveResult(message!, frameLength, DateTimeOffset.UtcNow);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(frame);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default) =>
        CloseInternalAsync(reason ?? "Closed");

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
        await CloseInternalAsync("Disposed").ConfigureAwait(false);
        _sendGate.Dispose();
        _pipe.Dispose();
    }

    private async ValueTask<bool> TryReadExactAsync(
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        var totalRead = 0;
        try
        {
            while (totalRead < buffer.Length)
            {
                var read = await _pipe.ReadAsync(
                        buffer.Slice(totalRead, buffer.Length - totalRead),
                        linkedCts.Token)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                totalRead += read;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private ValueTask CloseInternalAsync(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            if (_pipe.IsConnected)
            {
                _pipe.Disconnect();
            }
        }
        catch
        {
            // no-op
        }

        try
        {
            _pipe.Dispose();
        }
        catch
        {
            // no-op
        }

        _protocolMonitor.RecordConnectionClosed(TransportName, ConnectionId, RemoteEndpoint, reason);
        Log(RemoteDiagnosticsLogLevel.Information, "closed", null, 0, reason, null);
        _closedTcs.TrySetResult(null);
        return ValueTask.CompletedTask;
    }

    private async ValueTask<AttachReceiveResult?> FailReceiveAsync(string reason)
    {
        _protocolMonitor.RecordReceiveFailure(TransportName, ConnectionId, RemoteEndpoint, reason);
        Log(RemoteDiagnosticsLogLevel.Warning, "receive-failed", null, 0, reason, null);
        await CloseInternalAsync(reason).ConfigureAwait(false);
        return null;
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
