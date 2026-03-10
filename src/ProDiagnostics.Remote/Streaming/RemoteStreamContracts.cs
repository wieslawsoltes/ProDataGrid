using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Serialized stream payload emitted by diagnostics stream sources.
/// </summary>
public readonly record struct RemoteStreamPayload(string Topic, string PayloadJson);

/// <summary>
/// Producer contract for diagnostics stream payloads.
/// </summary>
public interface IRemoteStreamSource
{
    /// <summary>
    /// Subscribes to stream payloads.
    /// </summary>
    /// <param name="onPayload">Callback invoked for each payload.</param>
    /// <returns>A disposable subscription token.</returns>
    IDisposable Subscribe(Action<RemoteStreamPayload> onPayload);
}

/// <summary>
/// Per-session streaming diagnostics snapshot.
/// </summary>
public readonly record struct RemoteStreamSessionStats(
    Guid SessionId,
    int QueueLength,
    long SentMessages,
    long DroppedMessages,
    bool IsConnectionOpen);
