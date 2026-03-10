using System;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Shared .NET client used by DevTools frontends to consume remote diagnostics protocol.
/// </summary>
public interface IRemoteDiagnosticsClient : IAsyncDisposable
{
    bool IsConnected { get; }

    RemoteDiagnosticsClientStatus Status { get; }

    Guid SessionId { get; }

    Uri? Endpoint { get; }

    byte NegotiatedProtocolVersion { get; }

    IReadOnlyList<string> EnabledFeatures { get; }

    event EventHandler<RemoteDiagnosticsClientStatusChangedEventArgs>? StatusChanged;

    event EventHandler<RemoteStreamReceivedEventArgs>? StreamReceived;

    event EventHandler<RemoteKeepAliveMessage>? KeepAliveReceived;

    event EventHandler<RemoteDiagnosticsClientErrorEventArgs>? Error;

    ValueTask ConnectAsync(
        Uri endpoint,
        RemoteDiagnosticsClientOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask DisconnectAsync(
        string reason = "Client disconnect",
        CancellationToken cancellationToken = default);

    ValueTask<TResponse> RequestAsync<TRequest, TResponse>(
        string method,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    RemoteDiagnosticsClientTransportSnapshot GetTransportSnapshot();
}
