using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Single recent protocol-monitor event entry.
/// </summary>
public readonly record struct RemoteProtocolMonitorEvent(
    DateTimeOffset TimestampUtc,
    RemoteProtocolEventType EventType,
    string TransportName,
    Guid ConnectionId,
    Guid SessionId,
    string? RemoteEndpoint,
    string? Topic,
    RemoteMessageKind? MessageKind,
    int Bytes,
    int Count,
    string? Details);
