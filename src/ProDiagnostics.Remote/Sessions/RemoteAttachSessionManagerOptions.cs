using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Configures remote attach session-store limits and eviction behavior.
/// </summary>
public readonly record struct RemoteAttachSessionManagerOptions(
    TimeSpan SessionTimeout,
    int MaxSessions)
{
    /// <summary>
    /// Gets default manager options.
    /// </summary>
    public static RemoteAttachSessionManagerOptions Default =>
        new(
            SessionTimeout: RemoteProtocol.DefaultServerOptions.SessionTimeout,
            MaxSessions: 512);

    /// <summary>
    /// Returns normalized options safe for runtime usage.
    /// </summary>
    public static RemoteAttachSessionManagerOptions Normalize(in RemoteAttachSessionManagerOptions options)
    {
        var timeout = options.SessionTimeout <= TimeSpan.Zero
            ? Default.SessionTimeout
            : options.SessionTimeout;
        var maxSessions = options.MaxSessions <= 0
            ? Default.MaxSessions
            : options.MaxSessions;

        return new RemoteAttachSessionManagerOptions(timeout, maxSessions);
    }
}
