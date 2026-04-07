using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Routes mutation/control diagnostics request messages to in-process command handlers.
/// </summary>
public sealed class RemoteMutationMessageRouter : IRemoteMessageRouter
{
    private readonly IRemoteMutationDiagnosticsSource _source;

    public RemoteMutationMessageRouter(IRemoteMutationDiagnosticsSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async ValueTask<IRemoteMessage?> HandleAsync(
        IAttachConnection connection,
        IRemoteMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message is not RemoteRequestMessage requestMessage)
        {
            return null;
        }

        try
        {
            return requestMessage.Method switch
            {
                RemoteMutationMethods.PreviewPausedSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetPreviewPausedAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPreviewPausedRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.PreviewSettingsSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetPreviewSettingsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPreviewSettingsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.PreviewInputInject => BuildSuccessResponse(
                    requestMessage,
                    await _source.InjectPreviewInputAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemotePreviewInputRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.InspectHovered => BuildSuccessResponse(
                    requestMessage,
                    await _source.InspectHoveredAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteInspectHoveredRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.SelectionSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetSelectionAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetSelectionRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.PropertiesSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetPropertyAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPropertyRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.PseudoClassSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetPseudoClassAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPseudoClassRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.Elements3DRootSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetElements3DRootAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetElements3DRootRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.Elements3DRootReset => BuildSuccessResponse(
                    requestMessage,
                    await _source.ResetElements3DRootAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.Elements3DFiltersSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetElements3DFiltersAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetElements3DFiltersRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.OverlayOptionsSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetOverlayOptionsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetOverlayOptionsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.OverlayLiveHoverSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetOverlayLiveHoverAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetOverlayLiveHoverRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.CodeDocumentOpen => BuildSuccessResponse(
                    requestMessage,
                    await _source.OpenCodeDocumentAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteCodeDocumentOpenRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsPropertyAdd => BuildSuccessResponse(
                    requestMessage,
                    await _source.AddPropertyBreakpointAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteAddPropertyBreakpointRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsEventAdd => BuildSuccessResponse(
                    requestMessage,
                    await _source.AddEventBreakpointAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteAddEventBreakpointRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsRemove => BuildSuccessResponse(
                    requestMessage,
                    await _source.RemoveBreakpointAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteRemoveBreakpointRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsToggle => BuildSuccessResponse(
                    requestMessage,
                    await _source.ToggleBreakpointAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteToggleBreakpointRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsClear => BuildSuccessResponse(
                    requestMessage,
                    await _source.ClearBreakpointsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.BreakpointsEnabledSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetBreakpointsEnabledAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetBreakpointsEnabledRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.EventsClear => BuildSuccessResponse(
                    requestMessage,
                    await _source.ClearEventsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.EventsNodeEnabledSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetEventEnabledAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetEventEnabledRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.EventsDefaultsEnable => BuildSuccessResponse(
                    requestMessage,
                    await _source.EnableDefaultEventsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.EventsDisableAll => BuildSuccessResponse(
                    requestMessage,
                    await _source.DisableAllEventsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.LogsClear => BuildSuccessResponse(
                    requestMessage,
                    await _source.ClearLogsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEmptyMutationRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.LogsLevelsSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetLogLevelsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetLogLevelsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.MetricsPausedSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetMetricsPausedAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPausedRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),
                RemoteMutationMethods.MetricsSettingsSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetMetricsSettingsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetMetricsSettingsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                RemoteMutationMethods.ProfilerPausedSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetProfilerPausedAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetPausedRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),
                RemoteMutationMethods.ProfilerSettingsSet => BuildSuccessResponse(
                    requestMessage,
                    await _source.SetProfilerSettingsAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSetProfilerSettingsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMutationResult),

                _ => BuildFailureResponse(
                    requestMessage,
                    "method_not_found",
                    "Unsupported method: " + requestMessage.Method),
            };
        }
        catch (JsonException jsonException)
        {
            return BuildFailureResponse(requestMessage, "invalid_request", jsonException.Message);
        }
        catch (RemoteMutationException mutationException)
        {
            return BuildFailureResponse(requestMessage, mutationException.ErrorCode, mutationException.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BuildFailureResponse(requestMessage, "server_error", exception.Message);
        }
    }

    private static TRequest DeserializeRequest<TRequest>(string payloadJson, JsonTypeInfo<TRequest> typeInfo)
        where TRequest : class, new()
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new TRequest();
        }

        var request = JsonSerializer.Deserialize(payloadJson, typeInfo);
        return request ?? new TRequest();
    }

    private static RemoteResponseMessage BuildSuccessResponse(
        RemoteRequestMessage requestMessage,
        RemoteMutationResult payload,
        JsonTypeInfo<RemoteMutationResult> typeInfo)
    {
        return new RemoteResponseMessage(
            SessionId: requestMessage.SessionId,
            RequestId: requestMessage.RequestId,
            IsSuccess: true,
            PayloadJson: JsonSerializer.Serialize(payload, typeInfo),
            ErrorCode: string.Empty,
            ErrorMessage: string.Empty);
    }

    private static RemoteResponseMessage BuildFailureResponse(
        RemoteRequestMessage requestMessage,
        string errorCode,
        string errorMessage)
    {
        return new RemoteResponseMessage(
            SessionId: requestMessage.SessionId,
            RequestId: requestMessage.RequestId,
            IsSuccess: false,
            PayloadJson: "{}",
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}
