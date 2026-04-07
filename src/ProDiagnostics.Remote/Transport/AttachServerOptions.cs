using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines host lifecycle and protocol limits for an attach server.
/// </summary>
public readonly record struct AttachServerOptions(
    byte ProtocolVersion,
    TimeSpan HeartbeatInterval,
    TimeSpan SessionTimeout,
    int MaxFramePayloadBytes)
{
    /// <summary>
    /// Returns protocol-safe options with clamped values.
    /// </summary>
    public static AttachServerOptions Normalize(in AttachServerOptions options)
    {
        var heartbeat = options.HeartbeatInterval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(5)
            : options.HeartbeatInterval;

        var timeout = options.SessionTimeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(30)
            : options.SessionTimeout;

        var maxFramePayloadBytes = options.MaxFramePayloadBytes <= 0
            ? RemoteProtocol.MaxFramePayloadBytes
            : options.MaxFramePayloadBytes;

        return new AttachServerOptions(
            ProtocolVersion: options.ProtocolVersion == 0 ? RemoteProtocol.Version : options.ProtocolVersion,
            HeartbeatInterval: heartbeat,
            SessionTimeout: timeout,
            MaxFramePayloadBytes: maxFramePayloadBytes);
    }
}
