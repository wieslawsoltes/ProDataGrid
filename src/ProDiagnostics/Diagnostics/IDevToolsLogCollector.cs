using System;
using Avalonia.Logging;

namespace Avalonia.Diagnostics;

/// <summary>
/// Represents a log collector used by the DevTools Logs tab.
/// </summary>
public interface IDevToolsLogCollector
{
    /// <summary>
    /// Gets the collector display name.
    /// </summary>
    string CollectorName { get; }

    /// <summary>
    /// Subscribes to log events produced by the collector.
    /// </summary>
    /// <param name="onLogEvent">Callback invoked when a log event is captured.</param>
    /// <returns>A disposable subscription token.</returns>
    IDisposable Subscribe(Action<DevToolsLogEvent> onLogEvent);
}

/// <summary>
/// Represents a single log event captured for DevTools.
/// </summary>
/// <param name="Timestamp">Capture timestamp.</param>
/// <param name="Level">Log level.</param>
/// <param name="Area">Log area.</param>
/// <param name="Source">Log source.</param>
/// <param name="Message">Log message.</param>
public readonly record struct DevToolsLogEvent(
    DateTimeOffset Timestamp,
    LogEventLevel Level,
    string Area,
    string Source,
    string Message);
