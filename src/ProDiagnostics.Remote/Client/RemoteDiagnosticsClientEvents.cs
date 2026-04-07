using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Lightweight transport counters collected by <see cref="IRemoteDiagnosticsClient"/>.
/// </summary>
public readonly record struct RemoteDiagnosticsClientTransportSnapshot(
    DateTimeOffset? ConnectedAtUtc,
    DateTimeOffset? DisconnectedAtUtc,
    DateTimeOffset? LastSendUtc,
    DateTimeOffset? LastReceiveUtc,
    long SentMessages,
    long ReceivedMessages,
    long SentBytes,
    long ReceivedBytes,
    long KeepAliveCount);

/// <summary>
/// Event data for remote client lifecycle transitions.
/// </summary>
public sealed class RemoteDiagnosticsClientStatusChangedEventArgs : EventArgs
{
    public RemoteDiagnosticsClientStatusChangedEventArgs(
        RemoteDiagnosticsClientStatus status,
        Uri? endpoint,
        string? reason)
    {
        Status = status;
        Endpoint = endpoint;
        Reason = reason;
    }

    public RemoteDiagnosticsClientStatus Status { get; }

    public Uri? Endpoint { get; }

    public string? Reason { get; }
}

/// <summary>
/// Event data for stream packets emitted by <see cref="IRemoteDiagnosticsClient"/>.
/// </summary>
public sealed class RemoteStreamReceivedEventArgs : EventArgs
{
    public RemoteStreamReceivedEventArgs(RemoteStreamMessage message)
    {
        Message = message;
    }

    public RemoteStreamMessage Message { get; }
}

/// <summary>
/// Event data for client-side protocol/runtime failures.
/// </summary>
public sealed class RemoteDiagnosticsClientErrorEventArgs : EventArgs
{
    public RemoteDiagnosticsClientErrorEventArgs(string stage, Exception exception)
    {
        Stage = stage;
        Exception = exception;
    }

    public string Stage { get; }

    public Exception Exception { get; }
}

/// <summary>
/// Strongly typed stream payload envelope used by domain stream subscriptions.
/// </summary>
public readonly record struct RemoteTypedStreamPayload<TPayload>(
    RemoteStreamMessage Message,
    TPayload? Payload,
    bool IsParsed,
    string? ParseError);
