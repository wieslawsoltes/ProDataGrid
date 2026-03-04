using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Provides mutable diagnostics operations consumed by remote API handlers.
/// </summary>
public interface IRemoteMutationDiagnosticsSource
{
    ValueTask<RemoteMutationResult> SetPreviewPausedAsync(
        RemoteSetPreviewPausedRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPreviewSettingsAsync(
        RemoteSetPreviewSettingsRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> InjectPreviewInputAsync(
        RemotePreviewInputRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> InspectHoveredAsync(
        RemoteInspectHoveredRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetSelectionAsync(
        RemoteSetSelectionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPropertyAsync(
        RemoteSetPropertyRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPseudoClassAsync(
        RemoteSetPseudoClassRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetElements3DRootAsync(
        RemoteSetElements3DRootRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ResetElements3DRootAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetElements3DFiltersAsync(
        RemoteSetElements3DFiltersRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetOverlayOptionsAsync(
        RemoteSetOverlayOptionsRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetOverlayLiveHoverAsync(
        RemoteSetOverlayLiveHoverRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> OpenCodeDocumentAsync(
        RemoteCodeDocumentOpenRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> AddPropertyBreakpointAsync(
        RemoteAddPropertyBreakpointRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> AddEventBreakpointAsync(
        RemoteAddEventBreakpointRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> RemoveBreakpointAsync(
        RemoteRemoveBreakpointRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ToggleBreakpointAsync(
        RemoteToggleBreakpointRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearBreakpointsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetBreakpointsEnabledAsync(
        RemoteSetBreakpointsEnabledRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetEventEnabledAsync(
        RemoteSetEventEnabledRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> EnableDefaultEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> DisableAllEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearLogsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetLogLevelsAsync(
        RemoteSetLogLevelsRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetMetricsPausedAsync(
        RemoteSetPausedRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetMetricsSettingsAsync(
        RemoteSetMetricsSettingsRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetProfilerPausedAsync(
        RemoteSetPausedRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetProfilerSettingsAsync(
        RemoteSetProfilerSettingsRequest request,
        CancellationToken cancellationToken = default);
}
