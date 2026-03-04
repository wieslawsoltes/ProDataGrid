namespace Avalonia.Diagnostics.Services;

using Avalonia.Diagnostics.Remote;

internal interface IRemoteStreamPauseController
{
    bool IsPreviewPaused { get; }

    bool IsMetricsPaused { get; }

    bool IsProfilerPaused { get; }

    bool SetPreviewPaused(bool isPaused);

    bool SetMetricsPaused(bool isPaused);

    bool SetProfilerPaused(bool isPaused);

    RemotePreviewCapabilitiesSnapshot GetPreviewCapabilitiesSnapshot(RemotePreviewCapabilitiesRequest request);

    RemotePreviewSnapshot GetPreviewSnapshot(RemotePreviewSnapshotRequest request);

    int ApplyPreviewSettings(RemoteSetPreviewSettingsRequest request);

    RemoteMetricsSnapshot GetMetricsSnapshot(RemoteMetricsSnapshotRequest request);

    RemoteProfilerSnapshot GetProfilerSnapshot(RemoteProfilerSnapshotRequest request);

    int ApplyMetricsSettings(RemoteSetMetricsSettingsRequest request);

    int ApplyProfilerSettings(RemoteSetProfilerSettingsRequest request);
}
