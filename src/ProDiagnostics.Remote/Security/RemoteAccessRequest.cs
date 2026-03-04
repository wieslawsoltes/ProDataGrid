using System.Net;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Captures transport metadata used for access-policy evaluation.
/// </summary>
public readonly record struct RemoteAccessRequest(
    string TransportName,
    string? RemoteEndpoint,
    IPAddress? RemoteAddress,
    string? AccessToken,
    bool IsNetworkTransport);
