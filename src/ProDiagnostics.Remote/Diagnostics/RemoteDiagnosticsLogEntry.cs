using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Single structured backend diagnostics log entry.
/// </summary>
public readonly record struct RemoteDiagnosticsLogEntry(
    DateTimeOffset TimestampUtc,
    RemoteDiagnosticsLogLevel Level,
    string Category,
    string EventName,
    string TransportName,
    Guid ConnectionId,
    Guid SessionId,
    string? RemoteEndpoint,
    RemoteMessageKind? MessageKind,
    int Bytes,
    string? Details,
    string? ExceptionType);
