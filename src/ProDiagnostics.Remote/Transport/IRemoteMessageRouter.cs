using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Routes incoming remote messages to backend handlers.
/// </summary>
public interface IRemoteMessageRouter
{
    /// <summary>
    /// Handles a message and optionally returns an immediate response.
    /// </summary>
    ValueTask<IRemoteMessage?> HandleAsync(
        IAttachConnection connection,
        IRemoteMessage message,
        CancellationToken cancellationToken = default);
}
