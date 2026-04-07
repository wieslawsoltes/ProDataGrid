using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Remote;

namespace Avalonia.Diagnostics;

/// <summary>
/// Options used when bootstrapping local in-process host+client loopback for DevTools.
/// </summary>
public sealed record class DevToolsRemoteLoopbackOptions
{
    /// <summary>
    /// Host options for local attach endpoint.
    /// </summary>
    public DevToolsRemoteAttachHostOptions HostOptions { get; init; } = new();

    /// <summary>
    /// Shared .NET remote client options.
    /// </summary>
    public RemoteDiagnosticsClientOptions ClientOptions { get; init; } = RemoteDiagnosticsClientOptions.Default;

    /// <summary>
    /// When true, loopback host uses an ephemeral localhost TCP port.
    /// </summary>
    public bool UseDynamicPort { get; init; } = true;
}

/// <summary>
/// Running loopback runtime containing attach host, shared client and domain service adapters.
/// </summary>
public sealed class DevToolsRemoteLoopbackSession : IAsyncDisposable
{
    private bool _isDisposed;

    private DevToolsRemoteLoopbackSession(
        DevToolsRemoteAttachHost host,
        IRemoteDiagnosticsClient client,
        IRemoteDiagnosticsDomainServices domains)
    {
        Host = host;
        Client = client;
        Domains = domains;
    }

    public DevToolsRemoteAttachHost Host { get; }

    public IRemoteDiagnosticsClient Client { get; }

    public IRemoteDiagnosticsDomainServices Domains { get; }

    public IRemoteReadOnlyDiagnosticsDomainService ReadOnly => Domains.ReadOnly;

    public IRemoteMutationDiagnosticsDomainService Mutation => Domains.Mutation;

    public IRemoteStreamDiagnosticsDomainService Stream => Domains.Stream;

    public static async ValueTask<DevToolsRemoteLoopbackSession> StartAsync(
        AvaloniaObject root,
        DevToolsRemoteLoopbackOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var normalized = options ?? new DevToolsRemoteLoopbackOptions();
        var hostOptions = normalized.HostOptions;
        if (normalized.UseDynamicPort)
        {
            hostOptions = hostOptions with
            {
                HttpOptions = hostOptions.HttpOptions with
                {
                    Port = AllocateDynamicLoopbackPort(),
                    BindingMode = HttpAttachBindingMode.Localhost,
                },
            };
        }

        var host = new DevToolsRemoteAttachHost(root, hostOptions);
        var client = (IRemoteDiagnosticsClient)new RemoteDiagnosticsClient();
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            await client.ConnectAsync(host.WebSocketEndpoint, normalized.ClientOptions, cancellationToken).ConfigureAwait(false);
            var domains = new RemoteDiagnosticsDomainServices(client);
            return new DevToolsRemoteLoopbackSession(host, client, domains);
        }
        catch
        {
            await SafeDisposeAsync(client).ConfigureAwait(false);
            await SafeDisposeAsync(host).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        await SafeDisposeAsync(Client).ConfigureAwait(false);
        await SafeDisposeAsync(Host).ConfigureAwait(false);
    }

    private static async ValueTask SafeDisposeAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort cleanup in test and tooling paths.
        }
    }

    private static int AllocateDynamicLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
