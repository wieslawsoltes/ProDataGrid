using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// No-op monitor implementation used when runtime monitoring is not required.
/// </summary>
public sealed class NoOpRemoteProtocolMonitor : IRemoteProtocolMonitor
{
    private static readonly RemoteProtocolMonitorSnapshot EmptySnapshot =
        new(
            CapturedAtUtc: DateTimeOffset.MinValue,
            ConnectionsAccepted: 0,
            ConnectionsRejected: 0,
            ConnectionsClosed: 0,
            MessagesSent: 0,
            MessagesReceived: 0,
            BytesSent: 0,
            BytesReceived: 0,
            SendFailures: 0,
            ReceiveFailures: 0,
            StreamDroppedMessages: 0,
            StreamDispatchFailures: 0,
            SentByKind: Array.Empty<RemoteMessageKindCounter>(),
            ReceivedByKind: Array.Empty<RemoteMessageKindCounter>(),
            RecentEvents: Array.Empty<RemoteProtocolMonitorEvent>());

    private NoOpRemoteProtocolMonitor()
    {
    }

    public static NoOpRemoteProtocolMonitor Instance { get; } = new();

    public void RecordConnectionAccepted(string transportName, Guid connectionId, string? remoteEndpoint)
    {
    }

    public void RecordConnectionRejected(
        string transportName,
        string? remoteEndpoint,
        RemoteAccessDecisionCode decisionCode,
        string? details)
    {
    }

    public void RecordConnectionClosed(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details)
    {
    }

    public void RecordMessageSent(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes)
    {
    }

    public void RecordMessageReceived(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes)
    {
    }

    public void RecordSendFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind? kind,
        string? details)
    {
    }

    public void RecordReceiveFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details)
    {
    }

    public void RecordStreamDropped(Guid sessionId, string topic, int droppedMessages)
    {
    }

    public void RecordStreamDispatchFailure(Guid sessionId, string topic, string? details)
    {
    }

    public RemoteProtocolMonitorSnapshot GetSnapshot() => EmptySnapshot;
}
