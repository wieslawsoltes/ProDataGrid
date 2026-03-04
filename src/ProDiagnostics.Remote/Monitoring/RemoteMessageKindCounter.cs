namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents per-message-kind counter values captured by protocol monitor.
/// </summary>
public readonly record struct RemoteMessageKindCounter(
    RemoteMessageKind Kind,
    long Count);
