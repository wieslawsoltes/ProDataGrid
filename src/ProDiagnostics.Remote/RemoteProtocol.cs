using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines constants and limits for remote attach protocol framing.
/// </summary>
public static class RemoteProtocol
{
    /// <summary>
    /// Current wire protocol version.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// Frame header size in bytes: version + kind + payload length.
    /// </summary>
    public const int HeaderSizeBytes = 6;

    /// <summary>
    /// Maximum payload size accepted for a single frame.
    /// </summary>
    public const int MaxFramePayloadBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Maximum number of string entries accepted in list payload members.
    /// </summary>
    public const int MaxListEntries = 1024;

    /// <summary>
    /// Gets default attach server options for protocol v1.
    /// </summary>
    public static AttachServerOptions DefaultServerOptions =>
        new(
            ProtocolVersion: Version,
            HeartbeatInterval: TimeSpan.FromSeconds(5),
            SessionTimeout: TimeSpan.FromSeconds(30),
            MaxFramePayloadBytes: MaxFramePayloadBytes);
}
