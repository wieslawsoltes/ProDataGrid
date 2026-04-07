using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Event payload raised when an attach server accepts a remote connection.
/// </summary>
public sealed class AttachConnectionAcceptedEventArgs : EventArgs
{
    public AttachConnectionAcceptedEventArgs(IAttachConnection connection, DateTimeOffset acceptedAtUtc)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        AcceptedAtUtc = acceptedAtUtc;
    }

    /// <summary>
    /// Gets accepted connection instance.
    /// </summary>
    public IAttachConnection Connection { get; }

    /// <summary>
    /// Gets acceptance timestamp in UTC.
    /// </summary>
    public DateTimeOffset AcceptedAtUtc { get; }
}
