using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Evaluates whether an incoming attach connection is allowed.
/// </summary>
public interface IRemoteAccessPolicy
{
    /// <summary>
    /// Evaluates access for the provided request context.
    /// </summary>
    ValueTask<RemoteAccessDecision> EvaluateAsync(
        RemoteAccessRequest request,
        CancellationToken cancellationToken = default);
}
