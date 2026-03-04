using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Bounded, per-session stream fan-out hub for remote diagnostics messages.
/// </summary>
public sealed class RemoteStreamSessionHub : IAsyncDisposable
{
    private const string LogCategory = "remote.attach.stream.hub";

    private readonly object _sync = new();
    private readonly Dictionary<Guid, SessionChannel> _sessions = new();
    private readonly RemoteStreamSessionHubOptions _options;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _isDisposed;

    public RemoteStreamSessionHub(
        RemoteStreamSessionHubOptions options,
        IRemoteProtocolMonitor? protocolMonitor = null,
        IRemoteDiagnosticsLogger? diagnosticsLogger = null)
    {
        _options = RemoteStreamSessionHubOptions.Normalize(options);
        _protocolMonitor = protocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = diagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
    }

    public RemoteStreamSessionHubOptions Options => _options;

    public int SessionCount
    {
        get
        {
            lock (_sync)
            {
                return _sessions.Count;
            }
        }
    }

    public void RegisterSession(Guid sessionId, IAttachConnection connection)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(connection);

        SessionChannel? previous = null;
        SessionChannel created;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_sessions.TryGetValue(sessionId, out previous))
            {
                _sessions.Remove(sessionId);
            }

            created = new SessionChannel(sessionId, connection);
            created.StartPump(() => RunPumpAsync(created));
            _sessions.Add(sessionId, created);
        }

        if (previous is not null)
        {
            previous.Cancel();
            Log(
                RemoteDiagnosticsLogLevel.Debug,
                "session-replaced",
                previous.Connection.ConnectionId,
                previous.Connection.RemoteEndpoint,
                previous.SessionId,
                null,
                null);
        }
    }

    public bool TryUnregisterSession(Guid sessionId)
    {
        SessionChannel? removed;
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out removed))
            {
                return false;
            }

            _sessions.Remove(sessionId);
        }

        removed.Cancel();
        Log(
            RemoteDiagnosticsLogLevel.Debug,
            "session-unregistered",
            removed.Connection.ConnectionId,
            removed.Connection.RemoteEndpoint,
            removed.SessionId,
            null,
            null);
        return true;
    }

    public void Publish(string topic, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Topic is required.", nameof(topic));
        }

        if (payloadJson is null)
        {
            throw new ArgumentNullException(nameof(payloadJson));
        }

        SessionChannel[] targets;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_sessions.Count == 0)
            {
                return;
            }

            targets = _sessions.Values.ToArray();
        }

        for (var i = 0; i < targets.Length; i++)
        {
            var dropped = targets[i].Enqueue(topic, payloadJson, _options.MaxQueueLengthPerSession);
            if (dropped > 0)
            {
                _protocolMonitor.RecordStreamDropped(targets[i].SessionId, topic, dropped);
                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "stream-dropped",
                    targets[i].Connection.ConnectionId,
                    targets[i].Connection.RemoteEndpoint,
                    targets[i].SessionId,
                    topic,
                    "Dropped messages: " + dropped);
            }
        }
    }

    public void Publish(RemoteStreamPayload payload)
    {
        Publish(payload.Topic, payload.PayloadJson);
    }

    public IReadOnlyList<RemoteStreamSessionStats> GetSessionStats()
    {
        SessionChannel[] snapshot;
        lock (_sync)
        {
            snapshot = _sessions.Values.ToArray();
        }

        if (snapshot.Length == 0)
        {
            return Array.Empty<RemoteStreamSessionStats>();
        }

        var result = new RemoteStreamSessionStats[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            result[i] = snapshot[i].GetStats();
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        SessionChannel[] sessions;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }

        _shutdown.Cancel();

        for (var i = 0; i < sessions.Length; i++)
        {
            sessions[i].Cancel();
        }

        for (var i = 0; i < sessions.Length; i++)
        {
            await sessions[i].AwaitPumpAsync().ConfigureAwait(false);
            sessions[i].Dispose();
        }

        _shutdown.Dispose();
    }

    private async Task RunPumpAsync(SessionChannel session)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, session.Cancellation.Token);
        var cancellationToken = linkedCts.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await session.Signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var dispatched = 0;
            while (dispatched < _options.MaxDispatchBatchSize && !cancellationToken.IsCancellationRequested)
            {
                if (!session.TryDequeue(out var pending, out var droppedBefore, out var sequence))
                {
                    break;
                }

                try
                {
                    await session.Connection.SendAsync(
                            new RemoteStreamMessage(
                                SessionId: session.SessionId,
                                Topic: pending.Topic,
                                Sequence: sequence,
                                DroppedMessages: droppedBefore,
                                PayloadJson: pending.PayloadJson),
                            cancellationToken)
                        .ConfigureAwait(false);
                    session.MarkSent();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _protocolMonitor.RecordStreamDispatchFailure(session.SessionId, pending.Topic, ex.Message);
                    Log(
                        RemoteDiagnosticsLogLevel.Warning,
                        "stream-dispatch-failed",
                        session.Connection.ConnectionId,
                        session.Connection.RemoteEndpoint,
                        session.SessionId,
                        pending.Topic,
                        ex.Message,
                        ex);
                    TryUnregisterSession(session.SessionId);
                    return;
                }

                dispatched++;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(RemoteStreamSessionHub));
        }
    }

    private readonly record struct PendingStreamItem(string Topic, string PayloadJson);

    private sealed class SessionChannel : IDisposable
    {
        private readonly object _gate = new();
        private readonly Queue<PendingStreamItem> _queue = new();
        private readonly CancellationTokenSource _cancellation = new();
        private long _sequence;
        private long _sent;
        private long _dropped;
        private int _pendingDroppedForNextMessage;
        private Task? _pumpTask;

        public SessionChannel(Guid sessionId, IAttachConnection connection)
        {
            SessionId = sessionId;
            Connection = connection;
        }

        public Guid SessionId { get; }

        public IAttachConnection Connection { get; }

        public CancellationTokenSource Cancellation => _cancellation;

        public SemaphoreSlim Signal { get; } = new(0);

        public int Enqueue(string topic, string payloadJson, int maxQueueLength)
        {
            var dropped = 0;
            lock (_gate)
            {
                if (_queue.Count >= maxQueueLength)
                {
                    _queue.Dequeue();
                    _pendingDroppedForNextMessage++;
                    _dropped++;
                    dropped = 1;
                }

                _queue.Enqueue(new PendingStreamItem(topic, payloadJson));
            }

            Signal.Release();
            return dropped;
        }

        public bool TryDequeue(out PendingStreamItem item, out int droppedBefore, out long sequence)
        {
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    item = default;
                    droppedBefore = 0;
                    sequence = 0;
                    return false;
                }

                item = _queue.Dequeue();
                droppedBefore = _pendingDroppedForNextMessage;
                _pendingDroppedForNextMessage = 0;
                sequence = ++_sequence;
                return true;
            }
        }

        public void StartPump(Func<Task> pumpFactory)
        {
            _pumpTask = Task.Run(pumpFactory);
        }

        public Task AwaitPumpAsync()
        {
            return _pumpTask ?? Task.CompletedTask;
        }

        public void Cancel()
        {
            _cancellation.Cancel();
            Signal.Release();
        }

        public void MarkSent()
        {
            Interlocked.Increment(ref _sent);
        }

        public RemoteStreamSessionStats GetStats()
        {
            int queueLength;
            lock (_gate)
            {
                queueLength = _queue.Count;
            }

            return new RemoteStreamSessionStats(
                SessionId: SessionId,
                QueueLength: queueLength,
                SentMessages: Interlocked.Read(ref _sent),
                DroppedMessages: Interlocked.Read(ref _dropped),
                IsConnectionOpen: Connection.IsOpen);
        }

        public void Dispose()
        {
            _cancellation.Dispose();
            Signal.Dispose();
        }
    }

    private void Log(
        RemoteDiagnosticsLogLevel level,
        string eventName,
        Guid connectionId,
        string? remoteEndpoint,
        Guid sessionId,
        string? topic,
        string? details,
        Exception? exception = null)
    {
        var detailsWithTopic = string.IsNullOrWhiteSpace(topic)
            ? details
            : (string.IsNullOrWhiteSpace(details)
                ? "topic=" + topic
                : "topic=" + topic + "; " + details);

        _diagnosticsLogger.Log(
            new RemoteDiagnosticsLogEntry(
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: level,
                Category: LogCategory,
                EventName: eventName,
                TransportName: "stream",
                ConnectionId: connectionId,
                SessionId: sessionId,
                RemoteEndpoint: remoteEndpoint,
                MessageKind: RemoteMessageKind.Stream,
                Bytes: 0,
                Details: detailsWithTopic,
                ExceptionType: exception?.GetType().Name));
    }
}
