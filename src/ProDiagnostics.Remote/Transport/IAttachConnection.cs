using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents a single remote attach channel used by the server runtime.
/// </summary>
public interface IAttachConnection : IAsyncDisposable
{
    /// <summary>
    /// Gets unique connection identifier.
    /// </summary>
    Guid ConnectionId { get; }

    /// <summary>
    /// Gets remote endpoint description, if available.
    /// </summary>
    string? RemoteEndpoint { get; }

    /// <summary>
    /// Gets a value indicating whether the connection is open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Sends a single protocol message.
    /// </summary>
    ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives the next protocol message frame.
    /// </summary>
    ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connection gracefully.
    /// </summary>
    ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default);
}
