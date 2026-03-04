using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Marker interface for remote protocol messages.
/// </summary>
public interface IRemoteMessage
{
    /// <summary>
    /// Gets message kind discriminator.
    /// </summary>
    RemoteMessageKind Kind { get; }
}

public sealed record RemoteHelloMessage(
    Guid SessionId,
    int ProcessId,
    string ProcessName,
    string ApplicationName,
    string MachineName,
    string RuntimeVersion,
    string ClientName,
    IReadOnlyList<string> RequestedFeatures)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Hello;
}

public sealed record RemoteHelloAckMessage(
    Guid SessionId,
    byte NegotiatedProtocolVersion,
    IReadOnlyList<string> EnabledFeatures)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.HelloAck;
}

public sealed record RemoteHelloRejectMessage(
    Guid SessionId,
    string Reason,
    string Details)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.HelloReject;
}

public sealed record RemoteKeepAliveMessage(
    Guid SessionId,
    long Sequence,
    DateTimeOffset TimestampUtc)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.KeepAlive;
}

public sealed record RemoteDisconnectMessage(
    Guid SessionId,
    string Reason)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Disconnect;
}

public sealed record RemoteRequestMessage(
    Guid SessionId,
    long RequestId,
    string Method,
    string PayloadJson)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Request;
}

public sealed record RemoteResponseMessage(
    Guid SessionId,
    long RequestId,
    bool IsSuccess,
    string PayloadJson,
    string ErrorCode,
    string ErrorMessage)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Response;
}

public sealed record RemoteStreamMessage(
    Guid SessionId,
    string Topic,
    long Sequence,
    int DroppedMessages,
    string PayloadJson)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Stream;
}

public sealed record RemoteErrorMessage(
    Guid SessionId,
    string ErrorCode,
    string ErrorMessage,
    long RelatedRequestId)
    : IRemoteMessage
{
    public RemoteMessageKind Kind => RemoteMessageKind.Error;
}
