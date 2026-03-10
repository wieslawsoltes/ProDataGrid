using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents a received remote message frame.
/// </summary>
public readonly record struct AttachReceiveResult(
    IRemoteMessage Message,
    int FrameSizeBytes,
    DateTimeOffset ReceivedAtUtc);
