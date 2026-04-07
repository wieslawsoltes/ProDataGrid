using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
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
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private readonly TaskCompletionSource<object?> _closedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] _sendHeaderBuffer = new byte[sizeof(int)];
    private readonly byte[] _receiveHeaderBuffer = new byte[sizeof(int)];
    private byte[]? _receiveFrameBuffer;
    private long _lastActivityTicksUtc;
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
        _receiveFrameBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(8 * 1024, _maxSerializedFrameBytes));
        TouchActivity();
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

        var messageMethod = RemoteRuntimeMetrics.MapMessageKind(message.Kind);
        var serializeStarted = Stopwatch.GetTimestamp();
        ReadOnlyMemory<byte> frame;
        try
        {
            frame = RemoteMessageSerializer.Serialize(message);
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
            Log(RemoteDiagnosticsLogLevel.Warning, "serialize-failed", message.Kind, 0, ex.Message, ex);
            throw;
        }

        if (frame.Length > _maxSerializedFrameBytes)
        {
            throw new InvalidOperationException(
                "Serialized frame exceeds max allowed size of " + _maxSerializedFrameBytes + " bytes.");
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(_sendHeaderBuffer, frame.Length);
            await _pipe.WriteAsync(_sendHeaderBuffer.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
            await _pipe.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

            _protocolMonitor.RecordMessageSent(
                TransportName,
                ConnectionId,
                RemoteEndpoint,
                message.Kind,
                frame.Length);
            TouchActivity();
            RemoteRuntimeMetrics.RecordPayloadOutBytes(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                scope: "none",
                status: "ok",
                bytes: frame.Length);
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
            Log(RemoteDiagnosticsLogLevel.Warning, "send-failed", message.Kind, frame.Length, ex.Message, ex);
            await CloseInternalAsync("Send failure").ConfigureAwait(false);
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
            if (!await TryReadExactAsync(_receiveHeaderBuffer.AsMemory(0, sizeof(int)), _receiveTimeout, cancellationToken)
                    .ConfigureAwait(false))
            {
                return await FailReceiveAsync("Failed to read frame header").ConfigureAwait(false);
            }

            var frameLength = BinaryPrimitives.ReadInt32LittleEndian(_receiveHeaderBuffer);
            if (frameLength <= 0 || frameLength > _maxSerializedFrameBytes)
            {
                return await FailReceiveAsync("Invalid frame length: " + frameLength).ConfigureAwait(false);
            }

            var frame = EnsureReceiveFrameCapacity(frameLength);
            if (!await TryReadExactAsync(frame.AsMemory(0, frameLength), _receiveTimeout, cancellationToken).ConfigureAwait(false))
            {
                return await FailReceiveAsync("Failed to read frame payload").ConfigureAwait(false);
            }

            var deserializeStarted = Stopwatch.GetTimestamp();
            if (!RemoteMessageSerializer.TryDeserialize(frame.AsSpan(0, frameLength), out var message))
            {
                RemoteRuntimeMetrics.RecordDeserializeDuration(
                    transport: TransportName,
                    method: "response",
                    status: "error",
                    durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(deserializeStarted));
                return await FailReceiveAsync("Invalid protocol frame").ConfigureAwait(false);
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
                frameLength);
            TouchActivity();
            RemoteRuntimeMetrics.RecordPayloadInBytes(
                transport: TransportName,
                method: messageMethod,
                domain: "none",
                scope: "none",
                status: "ok",
                bytes: frameLength);
            return new AttachReceiveResult(message!, frameLength, DateTimeOffset.UtcNow);
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
        var receiveGateAcquired = await TryAcquireGateAsync(_receiveGate, timeout: TimeSpan.FromMilliseconds(250))
            .ConfigureAwait(false);
        try
        {
            if (receiveGateAcquired && _receiveFrameBuffer is { } receiveFrameBuffer)
            {
                _receiveFrameBuffer = null;
                ArrayPool<byte>.Shared.Return(receiveFrameBuffer);
            }
        }
        finally
        {
            if (receiveGateAcquired)
            {
                _receiveGate.Release();
            }
        }

        _pipe.Dispose();
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
        RemoteRuntimeMetrics.RecordConnectionClosed(TransportName);
        Log(RemoteDiagnosticsLogLevel.Information, "closed", null, 0, reason, null);
        _closedTcs.TrySetResult(null);
        return ValueTask.CompletedTask;
    }

    private async ValueTask<AttachReceiveResult?> FailReceiveAsync(string reason)
    {
        var status = reason.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            ? "timeout"
            : "error";
        RemoteRuntimeMetrics.RecordTransportFailure(
            transport: TransportName,
            method: "response",
            domain: "none",
            status: status);
        _protocolMonitor.RecordReceiveFailure(TransportName, ConnectionId, RemoteEndpoint, reason);
        Log(RemoteDiagnosticsLogLevel.Warning, "receive-failed", null, 0, reason, null);
        await CloseInternalAsync(reason).ConfigureAwait(false);
        return null;
    }

    private byte[] EnsureReceiveFrameCapacity(int requiredLength)
    {
        var buffer = _receiveFrameBuffer ??= ArrayPool<byte>.Shared.Rent(Math.Min(8 * 1024, _maxSerializedFrameBytes));
        if (requiredLength <= buffer.Length)
        {
            return buffer;
        }

        var targetLength = buffer.Length;
        while (targetLength < requiredLength && targetLength < _maxSerializedFrameBytes)
        {
            var doubled = targetLength * 2;
            targetLength = doubled <= 0
                ? _maxSerializedFrameBytes
                : Math.Min(doubled, _maxSerializedFrameBytes);
        }

        if (targetLength < requiredLength)
        {
            throw new InvalidOperationException("Required frame size exceeds configured max serialized frame bytes.");
        }

        var expanded = ArrayPool<byte>.Shared.Rent(targetLength);
        ArrayPool<byte>.Shared.Return(buffer);
        _receiveFrameBuffer = expanded;
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
