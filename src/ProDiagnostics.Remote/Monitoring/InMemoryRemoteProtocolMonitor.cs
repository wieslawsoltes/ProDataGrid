using System;
using System.Collections.Generic;
using System.Threading;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// In-memory protocol monitor with bounded recent-event buffer.
/// </summary>
public sealed class InMemoryRemoteProtocolMonitor : IRemoteProtocolMonitor
{
    private readonly object _eventGate = new();
    private readonly RemoteProtocolMonitorEvent[] _events;
    private readonly long[] _sentByKind;
    private readonly long[] _receivedByKind;
    private int _nextEventIndex;
    private int _eventCount;

    private long _connectionsAccepted;
    private long _connectionsRejected;
    private long _connectionsClosed;
    private long _messagesSent;
    private long _messagesReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _sendFailures;
    private long _receiveFailures;
    private long _streamDroppedMessages;
    private long _streamDispatchFailures;

    public InMemoryRemoteProtocolMonitor(int maxRecentEvents = 512)
    {
        var capacity = maxRecentEvents <= 0 ? 512 : maxRecentEvents;
        _events = new RemoteProtocolMonitorEvent[capacity];
        var kindLength = Enum.GetValues<RemoteMessageKind>().Length + 1;
        _sentByKind = new long[kindLength];
        _receivedByKind = new long[kindLength];
    }

    public void RecordConnectionAccepted(string transportName, Guid connectionId, string? remoteEndpoint)
    {
        Interlocked.Increment(ref _connectionsAccepted);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.ConnectionAccepted,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: null,
                Bytes: 0,
                Count: 0,
                Details: null));
    }

    public void RecordConnectionRejected(
        string transportName,
        string? remoteEndpoint,
        RemoteAccessDecisionCode decisionCode,
        string? details)
    {
        Interlocked.Increment(ref _connectionsRejected);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.ConnectionRejected,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: Guid.Empty,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: null,
                Bytes: 0,
                Count: 0,
                Details: string.IsNullOrWhiteSpace(details)
                    ? decisionCode.ToString()
                    : decisionCode + ": " + details));
    }

    public void RecordConnectionClosed(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details)
    {
        Interlocked.Increment(ref _connectionsClosed);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.ConnectionClosed,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: null,
                Bytes: 0,
                Count: 0,
                Details: details));
    }

    public void RecordMessageSent(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes)
    {
        var normalizedBytes = bytes < 0 ? 0 : bytes;
        Interlocked.Increment(ref _messagesSent);
        Interlocked.Add(ref _bytesSent, normalizedBytes);
        Interlocked.Increment(ref _sentByKind[GetKindIndex(kind)]);

        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.MessageSent,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: kind,
                Bytes: normalizedBytes,
                Count: 1,
                Details: null));
    }

    public void RecordMessageReceived(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes)
    {
        var normalizedBytes = bytes < 0 ? 0 : bytes;
        Interlocked.Increment(ref _messagesReceived);
        Interlocked.Add(ref _bytesReceived, normalizedBytes);
        Interlocked.Increment(ref _receivedByKind[GetKindIndex(kind)]);

        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.MessageReceived,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: kind,
                Bytes: normalizedBytes,
                Count: 1,
                Details: null));
    }

    public void RecordSendFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind? kind,
        string? details)
    {
        Interlocked.Increment(ref _sendFailures);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.SendFailure,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: kind,
                Bytes: 0,
                Count: 0,
                Details: details));
    }

    public void RecordReceiveFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details)
    {
        Interlocked.Increment(ref _receiveFailures);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.ReceiveFailure,
                TransportName: NormalizeTransport(transportName),
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                Topic: null,
                MessageKind: null,
                Bytes: 0,
                Count: 0,
                Details: details));
    }

    public void RecordStreamDropped(Guid sessionId, string topic, int droppedMessages)
    {
        var dropped = droppedMessages <= 0 ? 0 : droppedMessages;
        if (dropped == 0)
        {
            return;
        }

        Interlocked.Add(ref _streamDroppedMessages, dropped);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.StreamDropped,
                TransportName: "stream",
                ConnectionId: Guid.Empty,
                SessionId: sessionId,
                RemoteEndpoint: null,
                Topic: topic,
                MessageKind: RemoteMessageKind.Stream,
                Bytes: 0,
                Count: dropped,
                Details: null));
    }

    public void RecordStreamDispatchFailure(Guid sessionId, string topic, string? details)
    {
        Interlocked.Increment(ref _streamDispatchFailures);
        AddEvent(
            new RemoteProtocolMonitorEvent(
                TimestampUtc: DateTimeOffset.UtcNow,
                EventType: RemoteProtocolEventType.StreamDispatchFailure,
                TransportName: "stream",
                ConnectionId: Guid.Empty,
                SessionId: sessionId,
                RemoteEndpoint: null,
                Topic: topic,
                MessageKind: RemoteMessageKind.Stream,
                Bytes: 0,
                Count: 0,
                Details: details));
    }

    public RemoteProtocolMonitorSnapshot GetSnapshot()
    {
        var sent = CaptureKindCounters(_sentByKind);
        var received = CaptureKindCounters(_receivedByKind);
        var events = CaptureEvents();

        return new RemoteProtocolMonitorSnapshot(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            ConnectionsAccepted: Interlocked.Read(ref _connectionsAccepted),
            ConnectionsRejected: Interlocked.Read(ref _connectionsRejected),
            ConnectionsClosed: Interlocked.Read(ref _connectionsClosed),
            MessagesSent: Interlocked.Read(ref _messagesSent),
            MessagesReceived: Interlocked.Read(ref _messagesReceived),
            BytesSent: Interlocked.Read(ref _bytesSent),
            BytesReceived: Interlocked.Read(ref _bytesReceived),
            SendFailures: Interlocked.Read(ref _sendFailures),
            ReceiveFailures: Interlocked.Read(ref _receiveFailures),
            StreamDroppedMessages: Interlocked.Read(ref _streamDroppedMessages),
            StreamDispatchFailures: Interlocked.Read(ref _streamDispatchFailures),
            SentByKind: sent,
            ReceivedByKind: received,
            RecentEvents: events);
    }

    private static string NormalizeTransport(string transportName)
    {
        return string.IsNullOrWhiteSpace(transportName) ? "unknown" : transportName;
    }

    private static IReadOnlyList<RemoteMessageKindCounter> CaptureKindCounters(long[] counters)
    {
        var values = Enum.GetValues<RemoteMessageKind>();
        var result = new RemoteMessageKindCounter[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var kind = values[i];
            var index = (int)kind;
            var count = index >= 0 && index < counters.Length
                ? Interlocked.Read(ref counters[index])
                : 0;
            result[i] = new RemoteMessageKindCounter(kind, count);
        }

        return result;
    }

    private int GetKindIndex(RemoteMessageKind kind)
    {
        var index = (int)kind;
        return index < 0 || index >= _sentByKind.Length ? 0 : index;
    }

    private IReadOnlyList<RemoteProtocolMonitorEvent> CaptureEvents()
    {
        lock (_eventGate)
        {
            if (_eventCount == 0)
            {
                return Array.Empty<RemoteProtocolMonitorEvent>();
            }

            var result = new RemoteProtocolMonitorEvent[_eventCount];
            var start = _eventCount == _events.Length ? _nextEventIndex : 0;
            for (var i = 0; i < _eventCount; i++)
            {
                var index = (start + i) % _events.Length;
                result[i] = _events[index];
            }

            return result;
        }
    }

    private void AddEvent(in RemoteProtocolMonitorEvent entry)
    {
        lock (_eventGate)
        {
            _events[_nextEventIndex] = entry;
            _nextEventIndex++;
            if (_nextEventIndex == _events.Length)
            {
                _nextEventIndex = 0;
            }

            if (_eventCount < _events.Length)
            {
                _eventCount++;
            }
        }
    }
}
