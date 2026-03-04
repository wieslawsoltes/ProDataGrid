using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Collects runtime protocol counters and recent events for remote attach channels.
/// </summary>
public interface IRemoteProtocolMonitor
{
    void RecordConnectionAccepted(string transportName, Guid connectionId, string? remoteEndpoint);

    void RecordConnectionRejected(
        string transportName,
        string? remoteEndpoint,
        RemoteAccessDecisionCode decisionCode,
        string? details);

    void RecordConnectionClosed(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details);

    void RecordMessageSent(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes);

    void RecordMessageReceived(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind kind,
        int bytes);

    void RecordSendFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind? kind,
        string? details);

    void RecordReceiveFailure(
        string transportName,
        Guid connectionId,
        string? remoteEndpoint,
        string? details);

    void RecordStreamDropped(Guid sessionId, string topic, int droppedMessages);

    void RecordStreamDispatchFailure(Guid sessionId, string topic, string? details);

    RemoteProtocolMonitorSnapshot GetSnapshot();
}
