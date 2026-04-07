using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class DefaultRemoteAccessPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_Denies_NonLoopback_Network_Request_ByDefault()
    {
        var policy = new DefaultRemoteAccessPolicy(RemoteAccessPolicyOptions.Default);

        var decision = await policy.EvaluateAsync(
            new RemoteAccessRequest(
                TransportName: "http",
                RemoteEndpoint: "10.10.0.5:1234",
                RemoteAddress: IPAddress.Parse("10.10.0.5"),
                AccessToken: null,
                IsNetworkTransport: true),
            CancellationToken.None);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RemoteAccessDecisionCode.Forbidden, decision.Code);
    }

    [Fact]
    public async Task EvaluateAsync_Allows_Network_Request_When_AllowAnyIp_Is_Enabled()
    {
        var options = RemoteAccessPolicyOptions.Default with { AllowAnyIp = true };
        var policy = new DefaultRemoteAccessPolicy(options);

        var decision = await policy.EvaluateAsync(
            new RemoteAccessRequest(
                TransportName: "http",
                RemoteEndpoint: "10.10.0.5:1234",
                RemoteAddress: IPAddress.Parse("10.10.0.5"),
                AccessToken: null,
                IsNetworkTransport: true),
            CancellationToken.None);

        Assert.True(decision.IsAllowed);
        Assert.Equal(RemoteAccessDecisionCode.Allowed, decision.Code);
    }

    [Fact]
    public async Task EvaluateAsync_Allows_Allowlisted_Network_Address()
    {
        var options = RemoteAccessPolicyOptions.Default with
        {
            AllowedRemoteAddresses = new[] { "10.10.0.5" },
        };
        var policy = new DefaultRemoteAccessPolicy(options);

        var decision = await policy.EvaluateAsync(
            new RemoteAccessRequest(
                TransportName: "http",
                RemoteEndpoint: "10.10.0.5:1234",
                RemoteAddress: IPAddress.Parse("10.10.0.5"),
                AccessToken: null,
                IsNetworkTransport: true),
            CancellationToken.None);

        Assert.True(decision.IsAllowed);
        Assert.Equal(RemoteAccessDecisionCode.Allowed, decision.Code);
    }

    [Fact]
    public async Task EvaluateAsync_Returns_Unauthorized_When_TokenValidation_Fails()
    {
        static ValueTask<bool> ValidateTokenAsync(
            RemoteAccessTokenValidationContext context,
            CancellationToken _)
        {
            return ValueTask.FromResult(string.Equals(context.AccessToken, "valid-token", StringComparison.Ordinal));
        }

        var options = RemoteAccessPolicyOptions.Default with
        {
            TokenValidator = ValidateTokenAsync,
        };
        var policy = new DefaultRemoteAccessPolicy(options);

        var decision = await policy.EvaluateAsync(
            new RemoteAccessRequest(
                TransportName: "http",
                RemoteEndpoint: "127.0.0.1:1234",
                RemoteAddress: IPAddress.Loopback,
                AccessToken: "invalid",
                IsNetworkTransport: true),
            CancellationToken.None);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RemoteAccessDecisionCode.Unauthorized, decision.Code);
    }
}
