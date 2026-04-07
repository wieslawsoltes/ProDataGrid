using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents a transport server hosting incoming remote attach sessions.
/// </summary>
public interface IAttachServer : IAsyncDisposable
{
    /// <summary>
    /// Raised whenever a new connection is accepted.
    /// </summary>
    event EventHandler<AttachConnectionAcceptedEventArgs>? ConnectionAccepted;

    /// <summary>
    /// Gets normalized server options.
    /// </summary>
    AttachServerOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the server host loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the server and closes active connections.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
