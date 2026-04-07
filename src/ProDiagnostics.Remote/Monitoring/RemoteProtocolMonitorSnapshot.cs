using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Immutable snapshot of protocol monitor counters and recent events.
/// </summary>
public sealed record RemoteProtocolMonitorSnapshot(
    DateTimeOffset CapturedAtUtc,
    long ConnectionsAccepted,
    long ConnectionsRejected,
    long ConnectionsClosed,
    long MessagesSent,
    long MessagesReceived,
    long BytesSent,
    long BytesReceived,
    long SendFailures,
    long ReceiveFailures,
    long StreamDroppedMessages,
    long StreamDispatchFailures,
    IReadOnlyList<RemoteMessageKindCounter> SentByKind,
    IReadOnlyList<RemoteMessageKindCounter> ReceivedByKind,
    IReadOnlyList<RemoteProtocolMonitorEvent> RecentEvents);
