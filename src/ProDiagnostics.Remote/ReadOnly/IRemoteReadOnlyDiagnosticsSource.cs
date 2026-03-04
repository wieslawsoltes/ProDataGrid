using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Provides read-only diagnostics snapshots consumed by remote API handlers.
/// </summary>
public interface IRemoteReadOnlyDiagnosticsSource
{
    ValueTask<RemotePreviewCapabilitiesSnapshot> GetPreviewCapabilitiesSnapshotAsync(
        RemotePreviewCapabilitiesRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemotePreviewSnapshot> GetPreviewSnapshotAsync(
        RemotePreviewSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteTreeSnapshot> GetTreeSnapshotAsync(
        RemoteTreeSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteSelectionSnapshot> GetSelectionSnapshotAsync(
        RemoteSelectionSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemotePropertiesSnapshot> GetPropertiesSnapshotAsync(
        RemotePropertiesSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteElements3DSnapshot> GetElements3DSnapshotAsync(
        RemoteElements3DSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteOverlayOptionsSnapshot> GetOverlayOptionsSnapshotAsync(
        RemoteOverlayOptionsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteCodeDocumentsSnapshot> GetCodeDocumentsSnapshotAsync(
        RemoteCodeDocumentsRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteCodeResolveNodeSnapshot> ResolveCodeNodeAsync(
        RemoteCodeResolveNodeRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteBindingsSnapshot> GetBindingsSnapshotAsync(
        RemoteBindingsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteStylesSnapshot> GetStylesSnapshotAsync(
        RemoteStylesSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteResourcesSnapshot> GetResourcesSnapshotAsync(
        RemoteResourcesSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteAssetsSnapshot> GetAssetsSnapshotAsync(
        RemoteAssetsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteEventsSnapshot> GetEventsSnapshotAsync(
        RemoteEventsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteBreakpointsSnapshot> GetBreakpointsSnapshotAsync(
        RemoteBreakpointsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteLogsSnapshot> GetLogsSnapshotAsync(
        RemoteLogsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMetricsSnapshot> GetMetricsSnapshotAsync(
        RemoteMetricsSnapshotRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteProfilerSnapshot> GetProfilerSnapshotAsync(
        RemoteProfilerSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
