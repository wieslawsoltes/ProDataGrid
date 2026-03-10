using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Runtime configuration of <see cref="IRemoteDiagnosticsClient"/>.
/// </summary>
public sealed record class RemoteDiagnosticsClientOptions
{
    private static readonly string[] s_defaultRequestedFeatures =
    {
        "read-only",
        "mutation",
        "streaming",
        "trees",
        "selection",
        "properties",
        "preview",
        "code",
        "bindings",
        "styles",
        "resources",
        "assets",
        "elements3d",
        "overlay",
        "breakpoints",
        "events",
        "logs",
        "metrics",
        "profiler",
    };

    /// <summary>
    /// Default client options.
    /// </summary>
    public static RemoteDiagnosticsClientOptions Default { get; } = new();

    /// <summary>
    /// Client display name sent during handshake.
    /// </summary>
    public string ClientName { get; init; } = "prodiagnostics-dotnet-client";

    /// <summary>
    /// Requested capability flags sent during handshake.
    /// </summary>
    public IReadOnlyList<string>? RequestedFeatures { get; init; }

    /// <summary>
    /// Maximum duration for WebSocket connect and handshake acknowledgement.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Default timeout used by request/response operations.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Grace timeout used when closing socket during disconnect.
    /// </summary>
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Receive chunk buffer size used while assembling incoming protocol frames.
    /// </summary>
    public int ReceiveBufferBytes { get; init; } = 8 * 1024;

    /// <summary>
    /// Maximum accepted frame payload size.
    /// </summary>
    public int MaxPayloadBytes { get; init; } = RemoteProtocol.MaxFramePayloadBytes;

    internal static RemoteDiagnosticsClientOptions Normalize(RemoteDiagnosticsClientOptions? options)
    {
        var normalized = options ?? Default;
        var clientName = string.IsNullOrWhiteSpace(normalized.ClientName)
            ? Default.ClientName
            : normalized.ClientName.Trim();
        var requestedFeatures = normalized.RequestedFeatures is { Count: > 0 }
            ? normalized.RequestedFeatures
            : s_defaultRequestedFeatures;
        var connectTimeout = normalized.ConnectTimeout <= TimeSpan.Zero
            ? Default.ConnectTimeout
            : normalized.ConnectTimeout;
        var requestTimeout = normalized.RequestTimeout <= TimeSpan.Zero
            ? Default.RequestTimeout
            : normalized.RequestTimeout;
        var closeTimeout = normalized.CloseTimeout <= TimeSpan.Zero
            ? Default.CloseTimeout
            : normalized.CloseTimeout;
        var receiveBufferBytes = normalized.ReceiveBufferBytes <= 0
            ? Default.ReceiveBufferBytes
            : normalized.ReceiveBufferBytes;
        var maxPayloadBytes = normalized.MaxPayloadBytes <= 0
            ? RemoteProtocol.MaxFramePayloadBytes
            : normalized.MaxPayloadBytes;

        return new RemoteDiagnosticsClientOptions
        {
            ClientName = clientName,
            RequestedFeatures = requestedFeatures,
            ConnectTimeout = connectTimeout,
            RequestTimeout = requestTimeout,
            CloseTimeout = closeTimeout,
            ReceiveBufferBytes = receiveBufferBytes,
            MaxPayloadBytes = maxPayloadBytes,
        };
    }
}
