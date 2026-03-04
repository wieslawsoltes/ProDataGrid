using System;
using Avalonia.Diagnostics.Remote;

namespace Avalonia.Diagnostics;

/// <summary>
/// Configures the in-process remote attach host used by application startup.
/// </summary>
public sealed record DevToolsRemoteAttachHostOptions
{
    /// <summary>
    /// Gets or sets HTTP transport options.
    /// </summary>
    public HttpAttachServerOptions HttpOptions { get; init; } = HttpAttachServerOptions.Default;

    /// <summary>
    /// Gets or sets logical session manager limits.
    /// </summary>
    public RemoteAttachSessionManagerOptions SessionManagerOptions { get; init; } = RemoteAttachSessionManagerOptions.Default;

    /// <summary>
    /// Gets or sets stream hub queue and batching options.
    /// </summary>
    public RemoteStreamSessionHubOptions StreamHubOptions { get; init; } = RemoteStreamSessionHubOptions.Default;

    /// <summary>
    /// Gets or sets a value indicating whether mutation methods are enabled.
    /// </summary>
    public bool EnableMutationApi { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stream topics are enabled.
    /// </summary>
    public bool EnableStreamingApi { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether preview stream starts paused.
    /// </summary>
    public bool StartWithPreviewPaused { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics stream starts paused.
    /// </summary>
    public bool StartWithMetricsPaused { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether profiler stream starts paused.
    /// </summary>
    public bool StartWithProfilerPaused { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether UDP telemetry fallback ingestion is enabled.
    /// </summary>
    public bool EnableUdpTelemetryFallback { get; init; } = true;

    /// <summary>
    /// Gets or sets UDP telemetry fallback port.
    /// </summary>
    public int UdpTelemetryPort { get; init; } = 54831;

    /// <summary>
    /// Gets or sets optional protocol monitor sink.
    /// </summary>
    public IRemoteProtocolMonitor? ProtocolMonitor { get; init; }

    /// <summary>
    /// Gets or sets optional backend diagnostics logger sink.
    /// </summary>
    public IRemoteDiagnosticsLogger? DiagnosticsLogger { get; init; }

    /// <summary>
    /// Gets or sets per-request processing timeout for read-only and mutation handlers.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    internal static DevToolsRemoteAttachHostOptions Normalize(DevToolsRemoteAttachHostOptions? options)
    {
        var normalized = options ?? new DevToolsRemoteAttachHostOptions();
        var port = normalized.UdpTelemetryPort is > 0 and <= 65535
            ? normalized.UdpTelemetryPort
            : 54831;

        return normalized with
        {
            HttpOptions = HttpAttachServerOptions.Normalize(normalized.HttpOptions),
            SessionManagerOptions = RemoteAttachSessionManagerOptions.Normalize(normalized.SessionManagerOptions),
            StreamHubOptions = RemoteStreamSessionHubOptions.Normalize(normalized.StreamHubOptions),
            UdpTelemetryPort = port,
            RequestTimeout = normalized.RequestTimeout <= TimeSpan.Zero
                ? TimeSpan.FromSeconds(10)
                : normalized.RequestTimeout,
        };
    }
}
