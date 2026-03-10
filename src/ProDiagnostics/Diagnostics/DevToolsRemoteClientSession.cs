using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Diagnostics.Remote;

namespace Avalonia.Diagnostics;

/// <summary>
/// Running remote client session connected to an external diagnostics attach endpoint.
/// </summary>
internal sealed class DevToolsRemoteClientSession : IAsyncDisposable
{
    private bool _isDisposed;

    private DevToolsRemoteClientSession(
        Uri endpoint,
        IRemoteDiagnosticsClient client,
        IRemoteDiagnosticsDomainServices domains)
    {
        Endpoint = endpoint;
        Client = client;
        Domains = domains;
    }

    /// <summary>
    /// Connected remote attach endpoint.
    /// </summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// Shared remote client connection.
    /// </summary>
    public IRemoteDiagnosticsClient Client { get; }

    /// <summary>
    /// Domain services backed by <see cref="Client"/>.
    /// </summary>
    public IRemoteDiagnosticsDomainServices Domains { get; }

    /// <summary>
    /// Connects to an external attach endpoint and initializes domain services.
    /// </summary>
    public static async ValueTask<DevToolsRemoteClientSession> ConnectAsync(
        Uri endpoint,
        RemoteDiagnosticsClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var client = (IRemoteDiagnosticsClient)new RemoteDiagnosticsClient();
        try
        {
            await client.ConnectAsync(endpoint, options ?? RemoteDiagnosticsClientOptions.Default, cancellationToken)
                .ConfigureAwait(false);
            var domains = new RemoteDiagnosticsDomainServices(client);
            return new DevToolsRemoteClientSession(endpoint, client, domains);
        }
        catch
        {
            await SafeDisposeAsync(client).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await SafeDisposeAsync(Client).ConfigureAwait(false);
    }

    private static async ValueTask SafeDisposeAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort cleanup for tooling scenarios.
        }
    }
}
