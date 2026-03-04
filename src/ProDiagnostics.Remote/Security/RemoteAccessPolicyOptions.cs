using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Configures access checks for incoming remote attach connections.
/// </summary>
public readonly record struct RemoteAccessPolicyOptions(
    bool AllowAnyIp,
    IReadOnlyList<string>? AllowedRemoteAddresses,
    Func<RemoteAccessTokenValidationContext, CancellationToken, ValueTask<bool>>? TokenValidator)
{
    /// <summary>
    /// Gets default policy options (localhost-only, no token validator).
    /// </summary>
    public static RemoteAccessPolicyOptions Default =>
        new(
            AllowAnyIp: false,
            AllowedRemoteAddresses: Array.Empty<string>(),
            TokenValidator: null);

    /// <summary>
    /// Returns normalized options safe for runtime usage.
    /// </summary>
    public static RemoteAccessPolicyOptions Normalize(in RemoteAccessPolicyOptions options)
    {
        IReadOnlyList<string> addresses;
        if (options.AllowedRemoteAddresses is null || options.AllowedRemoteAddresses.Count == 0)
        {
            addresses = Array.Empty<string>();
        }
        else
        {
            addresses = options.AllowedRemoteAddresses
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Select(static entry => entry.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return new RemoteAccessPolicyOptions(
            AllowAnyIp: options.AllowAnyIp,
            AllowedRemoteAddresses: addresses,
            TokenValidator: options.TokenValidator);
    }
}
