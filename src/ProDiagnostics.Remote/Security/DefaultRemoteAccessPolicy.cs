using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Implements default transport access checks for remote attach connections.
/// </summary>
public sealed class DefaultRemoteAccessPolicy : IRemoteAccessPolicy
{
    private readonly RemoteAccessPolicyOptions _options;
    private readonly HashSet<IPAddress> _allowedRemoteAddresses;

    public DefaultRemoteAccessPolicy(RemoteAccessPolicyOptions options)
    {
        _options = RemoteAccessPolicyOptions.Normalize(options);
        _allowedRemoteAddresses = new HashSet<IPAddress>();
        foreach (var candidate in _options.AllowedRemoteAddresses ?? Array.Empty<string>())
        {
            if (IPAddress.TryParse(candidate, out var parsed))
            {
                _allowedRemoteAddresses.Add(parsed);
            }
        }
    }

    public async ValueTask<RemoteAccessDecision> EvaluateAsync(
        RemoteAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.IsNetworkTransport && !IsAllowedNetworkAddress(request.RemoteAddress))
        {
            return RemoteAccessDecision.Forbid("Remote access denied by network policy.");
        }

        if (_options.TokenValidator is not null)
        {
            var validationContext = new RemoteAccessTokenValidationContext(
                request.TransportName,
                request.RemoteEndpoint,
                request.RemoteAddress,
                request.AccessToken);

            var tokenIsValid = await _options.TokenValidator(validationContext, cancellationToken).ConfigureAwait(false);
            if (!tokenIsValid)
            {
                return RemoteAccessDecision.Unauthorized("Remote access token is invalid.");
            }
        }

        return RemoteAccessDecision.Allow();
    }

    private bool IsAllowedNetworkAddress(IPAddress? remoteAddress)
    {
        if (_options.AllowAnyIp)
        {
            return true;
        }

        if (remoteAddress is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteAddress))
        {
            return true;
        }

        return _allowedRemoteAddresses.Contains(remoteAddress);
    }
}
