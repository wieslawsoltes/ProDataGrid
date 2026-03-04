using System.Net;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Provides token validation context for remote attach access checks.
/// </summary>
public readonly record struct RemoteAccessTokenValidationContext(
    string TransportName,
    string? RemoteEndpoint,
    IPAddress? RemoteAddress,
    string? AccessToken);
