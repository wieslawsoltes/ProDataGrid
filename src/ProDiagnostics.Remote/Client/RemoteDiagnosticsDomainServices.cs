using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Aggregates typed domain services over <see cref="IRemoteDiagnosticsClient"/>.
/// </summary>
public interface IRemoteDiagnosticsDomainServices
{
    IRemoteReadOnlyDiagnosticsDomainService ReadOnly { get; }

    IRemoteMutationDiagnosticsDomainService Mutation { get; }

    IRemoteStreamDiagnosticsDomainService Stream { get; }
}

/// <summary>
/// Read-only snapshot APIs grouped by diagnostics domain.
/// </summary>
public interface IRemoteReadOnlyDiagnosticsDomainService
{
    ValueTask<RemotePreviewCapabilitiesSnapshot> GetPreviewCapabilitiesSnapshotAsync(RemotePreviewCapabilitiesRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemotePreviewSnapshot> GetPreviewSnapshotAsync(RemotePreviewSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteTreeSnapshot> GetTreeSnapshotAsync(RemoteTreeSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteSelectionSnapshot> GetSelectionSnapshotAsync(RemoteSelectionSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemotePropertiesSnapshot> GetPropertiesSnapshotAsync(RemotePropertiesSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteElements3DSnapshot> GetElements3DSnapshotAsync(RemoteElements3DSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteOverlayOptionsSnapshot> GetOverlayOptionsSnapshotAsync(RemoteOverlayOptionsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteCodeDocumentsSnapshot> GetCodeDocumentsSnapshotAsync(RemoteCodeDocumentsRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteCodeResolveNodeSnapshot> ResolveCodeNodeAsync(RemoteCodeResolveNodeRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteBindingsSnapshot> GetBindingsSnapshotAsync(RemoteBindingsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteStylesSnapshot> GetStylesSnapshotAsync(RemoteStylesSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteResourcesSnapshot> GetResourcesSnapshotAsync(RemoteResourcesSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteAssetsSnapshot> GetAssetsSnapshotAsync(RemoteAssetsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteEventsSnapshot> GetEventsSnapshotAsync(RemoteEventsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteBreakpointsSnapshot> GetBreakpointsSnapshotAsync(RemoteBreakpointsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteLogsSnapshot> GetLogsSnapshotAsync(RemoteLogsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMetricsSnapshot> GetMetricsSnapshotAsync(RemoteMetricsSnapshotRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteProfilerSnapshot> GetProfilerSnapshotAsync(RemoteProfilerSnapshotRequest? request = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mutation/control APIs grouped by diagnostics domain.
/// </summary>
public interface IRemoteMutationDiagnosticsDomainService
{
    ValueTask<RemoteMutationResult> SetPreviewPausedAsync(RemoteSetPreviewPausedRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPreviewSettingsAsync(RemoteSetPreviewSettingsRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> InjectPreviewInputAsync(RemotePreviewInputRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> InspectHoveredAsync(RemoteInspectHoveredRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetSelectionAsync(RemoteSetSelectionRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPropertyAsync(RemoteSetPropertyRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetPseudoClassAsync(RemoteSetPseudoClassRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetElements3DRootAsync(RemoteSetElements3DRootRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ResetElements3DRootAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetElements3DFiltersAsync(RemoteSetElements3DFiltersRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetOverlayOptionsAsync(RemoteSetOverlayOptionsRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetOverlayLiveHoverAsync(RemoteSetOverlayLiveHoverRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> OpenCodeDocumentAsync(RemoteCodeDocumentOpenRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> AddPropertyBreakpointAsync(RemoteAddPropertyBreakpointRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> AddEventBreakpointAsync(RemoteAddEventBreakpointRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> RemoveBreakpointAsync(RemoteRemoveBreakpointRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ToggleBreakpointAsync(RemoteToggleBreakpointRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearBreakpointsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetBreakpointsEnabledAsync(RemoteSetBreakpointsEnabledRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetEventEnabledAsync(RemoteSetEventEnabledRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> EnableDefaultEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> DisableAllEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> ClearLogsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetLogLevelsAsync(RemoteSetLogLevelsRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetMetricsPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetMetricsSettingsAsync(RemoteSetMetricsSettingsRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetProfilerPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default);

    ValueTask<RemoteMutationResult> SetProfilerSettingsAsync(RemoteSetProfilerSettingsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stream subscriptions grouped by diagnostics topic.
/// </summary>
public interface IRemoteStreamDiagnosticsDomainService
{
    IDisposable Subscribe(Action<RemoteStreamMessage> callback);

    IDisposable Subscribe(string topic, Action<RemoteStreamMessage> callback);

    IDisposable Subscribe<TPayload>(
        string topic,
        JsonTypeInfo<TPayload> payloadTypeInfo,
        Action<RemoteTypedStreamPayload<TPayload>> callback);
}

/// <summary>
/// Default implementation of <see cref="IRemoteDiagnosticsDomainServices"/>.
/// </summary>
public sealed class RemoteDiagnosticsDomainServices : IRemoteDiagnosticsDomainServices
{
    public RemoteDiagnosticsDomainServices(IRemoteDiagnosticsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        ReadOnly = new RemoteReadOnlyDiagnosticsDomainService(client);
        Mutation = new RemoteMutationDiagnosticsDomainService(client);
        Stream = new RemoteStreamDiagnosticsDomainService(client);
    }

    public IRemoteReadOnlyDiagnosticsDomainService ReadOnly { get; }

    public IRemoteMutationDiagnosticsDomainService Mutation { get; }

    public IRemoteStreamDiagnosticsDomainService Stream { get; }
}

internal sealed class RemoteReadOnlyDiagnosticsDomainService : IRemoteReadOnlyDiagnosticsDomainService
{
    private readonly IRemoteDiagnosticsClient _client;

    public RemoteReadOnlyDiagnosticsDomainService(IRemoteDiagnosticsClient client)
    {
        _client = client;
    }

    public ValueTask<RemotePreviewCapabilitiesSnapshot> GetPreviewCapabilitiesSnapshotAsync(RemotePreviewCapabilitiesRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.PreviewCapabilitiesGet,
            request ?? new RemotePreviewCapabilitiesRequest(),
            RemoteJsonSerializerContext.Default.RemotePreviewCapabilitiesRequest,
            RemoteJsonSerializerContext.Default.RemotePreviewCapabilitiesSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemotePreviewSnapshot> GetPreviewSnapshotAsync(RemotePreviewSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.PreviewSnapshotGet,
            request ?? new RemotePreviewSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemotePreviewSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemotePreviewSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteTreeSnapshot> GetTreeSnapshotAsync(RemoteTreeSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.TreeSnapshotGet,
            request ?? new RemoteTreeSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteTreeSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteTreeSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteSelectionSnapshot> GetSelectionSnapshotAsync(RemoteSelectionSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.SelectionGet,
            request ?? new RemoteSelectionSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteSelectionSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteSelectionSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemotePropertiesSnapshot> GetPropertiesSnapshotAsync(RemotePropertiesSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.PropertiesSnapshotGet,
            request ?? new RemotePropertiesSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemotePropertiesSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemotePropertiesSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteElements3DSnapshot> GetElements3DSnapshotAsync(RemoteElements3DSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.Elements3DSnapshotGet,
            request ?? new RemoteElements3DSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteElements3DSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteElements3DSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteOverlayOptionsSnapshot> GetOverlayOptionsSnapshotAsync(RemoteOverlayOptionsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.OverlayOptionsGet,
            request ?? new RemoteOverlayOptionsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteOverlayOptionsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteOverlayOptionsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteCodeDocumentsSnapshot> GetCodeDocumentsSnapshotAsync(RemoteCodeDocumentsRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.CodeDocumentsGet,
            request ?? new RemoteCodeDocumentsRequest(),
            RemoteJsonSerializerContext.Default.RemoteCodeDocumentsRequest,
            RemoteJsonSerializerContext.Default.RemoteCodeDocumentsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteCodeResolveNodeSnapshot> ResolveCodeNodeAsync(RemoteCodeResolveNodeRequest request, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.CodeResolveNode,
            request,
            RemoteJsonSerializerContext.Default.RemoteCodeResolveNodeRequest,
            RemoteJsonSerializerContext.Default.RemoteCodeResolveNodeSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteBindingsSnapshot> GetBindingsSnapshotAsync(RemoteBindingsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.BindingsSnapshotGet,
            request ?? new RemoteBindingsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteBindingsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteBindingsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteStylesSnapshot> GetStylesSnapshotAsync(RemoteStylesSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.StylesSnapshotGet,
            request ?? new RemoteStylesSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteStylesSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteStylesSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteResourcesSnapshot> GetResourcesSnapshotAsync(RemoteResourcesSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.ResourcesSnapshotGet,
            request ?? new RemoteResourcesSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteResourcesSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteResourcesSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteAssetsSnapshot> GetAssetsSnapshotAsync(RemoteAssetsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.AssetsSnapshotGet,
            request ?? new RemoteAssetsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteAssetsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteAssetsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteEventsSnapshot> GetEventsSnapshotAsync(RemoteEventsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.EventsSnapshotGet,
            request ?? new RemoteEventsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteEventsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteEventsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteBreakpointsSnapshot> GetBreakpointsSnapshotAsync(RemoteBreakpointsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.BreakpointsSnapshotGet,
            request ?? new RemoteBreakpointsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteBreakpointsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteBreakpointsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteLogsSnapshot> GetLogsSnapshotAsync(RemoteLogsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.LogsSnapshotGet,
            request ?? new RemoteLogsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteLogsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteLogsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteMetricsSnapshot> GetMetricsSnapshotAsync(RemoteMetricsSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.MetricsSnapshotGet,
            request ?? new RemoteMetricsSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteMetricsSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteMetricsSnapshot,
            cancellationToken: cancellationToken);

    public ValueTask<RemoteProfilerSnapshot> GetProfilerSnapshotAsync(RemoteProfilerSnapshotRequest? request = null, CancellationToken cancellationToken = default) =>
        _client.RequestAsync(
            RemoteReadOnlyMethods.ProfilerSnapshotGet,
            request ?? new RemoteProfilerSnapshotRequest(),
            RemoteJsonSerializerContext.Default.RemoteProfilerSnapshotRequest,
            RemoteJsonSerializerContext.Default.RemoteProfilerSnapshot,
            cancellationToken: cancellationToken);
}

internal sealed class RemoteMutationDiagnosticsDomainService : IRemoteMutationDiagnosticsDomainService
{
    private readonly IRemoteDiagnosticsClient _client;

    public RemoteMutationDiagnosticsDomainService(IRemoteDiagnosticsClient client)
    {
        _client = client;
    }

    public ValueTask<RemoteMutationResult> SetPreviewPausedAsync(RemoteSetPreviewPausedRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.PreviewPausedSet, request, RemoteJsonSerializerContext.Default.RemoteSetPreviewPausedRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetPreviewSettingsAsync(RemoteSetPreviewSettingsRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.PreviewSettingsSet, request, RemoteJsonSerializerContext.Default.RemoteSetPreviewSettingsRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> InjectPreviewInputAsync(RemotePreviewInputRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.PreviewInputInject, request, RemoteJsonSerializerContext.Default.RemotePreviewInputRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> InspectHoveredAsync(RemoteInspectHoveredRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.InspectHovered, request ?? new RemoteInspectHoveredRequest(), RemoteJsonSerializerContext.Default.RemoteInspectHoveredRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetSelectionAsync(RemoteSetSelectionRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.SelectionSet, request, RemoteJsonSerializerContext.Default.RemoteSetSelectionRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetPropertyAsync(RemoteSetPropertyRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.PropertiesSet, request, RemoteJsonSerializerContext.Default.RemoteSetPropertyRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetPseudoClassAsync(RemoteSetPseudoClassRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.PseudoClassSet, request, RemoteJsonSerializerContext.Default.RemoteSetPseudoClassRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetElements3DRootAsync(RemoteSetElements3DRootRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.Elements3DRootSet, request, RemoteJsonSerializerContext.Default.RemoteSetElements3DRootRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> ResetElements3DRootAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.Elements3DRootReset, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetElements3DFiltersAsync(RemoteSetElements3DFiltersRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.Elements3DFiltersSet, request, RemoteJsonSerializerContext.Default.RemoteSetElements3DFiltersRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetOverlayOptionsAsync(RemoteSetOverlayOptionsRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.OverlayOptionsSet, request, RemoteJsonSerializerContext.Default.RemoteSetOverlayOptionsRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetOverlayLiveHoverAsync(RemoteSetOverlayLiveHoverRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.OverlayLiveHoverSet, request, RemoteJsonSerializerContext.Default.RemoteSetOverlayLiveHoverRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> OpenCodeDocumentAsync(RemoteCodeDocumentOpenRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.CodeDocumentOpen, request, RemoteJsonSerializerContext.Default.RemoteCodeDocumentOpenRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> AddPropertyBreakpointAsync(RemoteAddPropertyBreakpointRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsPropertyAdd, request, RemoteJsonSerializerContext.Default.RemoteAddPropertyBreakpointRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> AddEventBreakpointAsync(RemoteAddEventBreakpointRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsEventAdd, request, RemoteJsonSerializerContext.Default.RemoteAddEventBreakpointRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> RemoveBreakpointAsync(RemoteRemoveBreakpointRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsRemove, request, RemoteJsonSerializerContext.Default.RemoteRemoveBreakpointRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> ToggleBreakpointAsync(RemoteToggleBreakpointRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsToggle, request, RemoteJsonSerializerContext.Default.RemoteToggleBreakpointRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> ClearBreakpointsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsClear, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetBreakpointsEnabledAsync(RemoteSetBreakpointsEnabledRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.BreakpointsEnabledSet, request, RemoteJsonSerializerContext.Default.RemoteSetBreakpointsEnabledRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> ClearEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.EventsClear, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetEventEnabledAsync(RemoteSetEventEnabledRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.EventsNodeEnabledSet, request, RemoteJsonSerializerContext.Default.RemoteSetEventEnabledRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> EnableDefaultEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.EventsDefaultsEnable, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> DisableAllEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.EventsDisableAll, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> ClearLogsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.LogsClear, request ?? new RemoteEmptyMutationRequest(), RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetLogLevelsAsync(RemoteSetLogLevelsRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.LogsLevelsSet, request, RemoteJsonSerializerContext.Default.RemoteSetLogLevelsRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetMetricsPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.MetricsPausedSet, request, RemoteJsonSerializerContext.Default.RemoteSetPausedRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetMetricsSettingsAsync(RemoteSetMetricsSettingsRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.MetricsSettingsSet, request, RemoteJsonSerializerContext.Default.RemoteSetMetricsSettingsRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetProfilerPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.ProfilerPausedSet, request, RemoteJsonSerializerContext.Default.RemoteSetPausedRequest, cancellationToken);

    public ValueTask<RemoteMutationResult> SetProfilerSettingsAsync(RemoteSetProfilerSettingsRequest request, CancellationToken cancellationToken = default) =>
        Invoke(RemoteMutationMethods.ProfilerSettingsSet, request, RemoteJsonSerializerContext.Default.RemoteSetProfilerSettingsRequest, cancellationToken);

    private ValueTask<RemoteMutationResult> Invoke<TRequest>(
        string method,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo,
        CancellationToken cancellationToken)
    {
        return _client.RequestAsync(
            method,
            request,
            requestTypeInfo,
            RemoteJsonSerializerContext.Default.RemoteMutationResult,
            cancellationToken: cancellationToken);
    }
}

internal sealed class RemoteStreamDiagnosticsDomainService : IRemoteStreamDiagnosticsDomainService
{
    private readonly IRemoteDiagnosticsClient _client;
    private readonly object _gate = new();
    private readonly List<StreamSubscription> _subscriptions = new();

    public RemoteStreamDiagnosticsDomainService(IRemoteDiagnosticsClient client)
    {
        _client = client;
        _client.StreamReceived += ClientOnStreamReceived;
    }

    public IDisposable Subscribe(Action<RemoteStreamMessage> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Register(new StreamSubscription(
            Topic: null,
            OnMessage: callback,
            OnTypedMessage: null));
    }

    public IDisposable Subscribe(string topic, Action<RemoteStreamMessage> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(callback);
        return Register(new StreamSubscription(
            Topic: topic,
            OnMessage: callback,
            OnTypedMessage: null));
    }

    public IDisposable Subscribe<TPayload>(
        string topic,
        JsonTypeInfo<TPayload> payloadTypeInfo,
        Action<RemoteTypedStreamPayload<TPayload>> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payloadTypeInfo);
        ArgumentNullException.ThrowIfNull(callback);

        void Typed(RemoteStreamMessage message)
        {
            try
            {
                var payload = JsonSerializer.Deserialize(message.PayloadJson, payloadTypeInfo);
                callback(new RemoteTypedStreamPayload<TPayload>(message, payload, true, null));
            }
            catch (Exception ex)
            {
                callback(new RemoteTypedStreamPayload<TPayload>(message, default, false, ex.Message));
            }
        }

        return Register(new StreamSubscription(
            Topic: topic,
            OnMessage: null,
            OnTypedMessage: Typed));
    }

    private IDisposable Register(StreamSubscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        return new DelegateDisposable(() =>
        {
            lock (_gate)
            {
                _subscriptions.Remove(subscription);
            }
        });
    }

    private void ClientOnStreamReceived(object? sender, RemoteStreamReceivedEventArgs args)
    {
        StreamSubscription[] snapshot;
        lock (_gate)
        {
            if (_subscriptions.Count == 0)
            {
                return;
            }

            snapshot = _subscriptions.ToArray();
        }

        var message = args.Message;
        foreach (var subscription in snapshot)
        {
            if (subscription.Topic is not null &&
                !string.Equals(subscription.Topic, message.Topic, StringComparison.Ordinal))
            {
                continue;
            }

            subscription.OnMessage?.Invoke(message);
            subscription.OnTypedMessage?.Invoke(message);
        }
    }

    private readonly record struct StreamSubscription(
        string? Topic,
        Action<RemoteStreamMessage>? OnMessage,
        Action<RemoteStreamMessage>? OnTypedMessage);

    private sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _dispose();
        }
    }
}
