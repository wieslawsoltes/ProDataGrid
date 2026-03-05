using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Routes read-only diagnostics request messages to snapshot source handlers.
/// </summary>
public sealed class RemoteReadOnlyMessageRouter : IRemoteMessageRouter
{
    private readonly IRemoteReadOnlyDiagnosticsSource _source;
    private readonly object _treeSnapshotCacheGate = new();
    private string _treeSnapshotCacheRequestPayload = string.Empty;
    private string _treeSnapshotCacheScope = string.Empty;
    private long _treeSnapshotCacheGeneration = long.MinValue;
    private int _treeSnapshotCacheVersion = int.MinValue;
    private string? _treeSnapshotCachePayloadJson;

    public RemoteReadOnlyMessageRouter(IRemoteReadOnlyDiagnosticsSource source)
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
                RemoteReadOnlyMethods.PreviewCapabilitiesGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetPreviewCapabilitiesSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemotePreviewCapabilitiesRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemotePreviewCapabilitiesSnapshot),
                RemoteReadOnlyMethods.PreviewSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetPreviewSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemotePreviewSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemotePreviewSnapshot),
                RemoteReadOnlyMethods.TreeSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetTreeSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteTreeSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    requestMessage.PayloadJson),
                RemoteReadOnlyMethods.SelectionGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetSelectionSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteSelectionSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteSelectionSnapshot),
                RemoteReadOnlyMethods.PropertiesSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetPropertiesSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemotePropertiesSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemotePropertiesSnapshot),
                RemoteReadOnlyMethods.Elements3DSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetElements3DSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteElements3DSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteElements3DSnapshot),
                RemoteReadOnlyMethods.OverlayOptionsGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetOverlayOptionsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteOverlayOptionsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteOverlayOptionsSnapshot),
                RemoteReadOnlyMethods.CodeDocumentsGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetCodeDocumentsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteCodeDocumentsRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteCodeDocumentsSnapshot),
                RemoteReadOnlyMethods.CodeResolveNode => BuildSuccessResponse(
                    requestMessage,
                    await _source.ResolveCodeNodeAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteCodeResolveNodeRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteCodeResolveNodeSnapshot),
                RemoteReadOnlyMethods.BindingsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetBindingsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteBindingsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteBindingsSnapshot),
                RemoteReadOnlyMethods.StylesSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetStylesSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteStylesSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteStylesSnapshot),
                RemoteReadOnlyMethods.ResourcesSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetResourcesSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteResourcesSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteResourcesSnapshot),
                RemoteReadOnlyMethods.AssetsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetAssetsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteAssetsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteAssetsSnapshot),
                RemoteReadOnlyMethods.EventsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetEventsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteEventsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteEventsSnapshot),
                RemoteReadOnlyMethods.BreakpointsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetBreakpointsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteBreakpointsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteBreakpointsSnapshot),
                RemoteReadOnlyMethods.LogsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetLogsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteLogsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteLogsSnapshot),
                RemoteReadOnlyMethods.MetricsSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetMetricsSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteMetricsSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteMetricsSnapshot),
                RemoteReadOnlyMethods.ProfilerSnapshotGet => BuildSuccessResponse(
                    requestMessage,
                    await _source.GetProfilerSnapshotAsync(
                            DeserializeRequest(
                                requestMessage.PayloadJson,
                                RemoteJsonSerializerContext.Default.RemoteProfilerSnapshotRequest),
                            cancellationToken)
                        .ConfigureAwait(false),
                    RemoteJsonSerializerContext.Default.RemoteProfilerSnapshot),
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return BuildFailureResponse(requestMessage, "server_error", exception.Message);
        }
    }

    private string GetTreeSnapshotPayloadJson(RemoteTreeSnapshot snapshot, string requestPayloadJson)
    {
        lock (_treeSnapshotCacheGate)
        {
            if (_treeSnapshotCachePayloadJson is not null &&
                _treeSnapshotCacheGeneration == snapshot.Generation &&
                _treeSnapshotCacheVersion == snapshot.SnapshotVersion &&
                string.Equals(_treeSnapshotCacheScope, snapshot.Scope, StringComparison.Ordinal) &&
                string.Equals(_treeSnapshotCacheRequestPayload, requestPayloadJson, StringComparison.Ordinal))
            {
                return _treeSnapshotCachePayloadJson;
            }
        }

        var payloadJson = JsonSerializer.Serialize(snapshot, RemoteJsonSerializerContext.Default.RemoteTreeSnapshot);

        lock (_treeSnapshotCacheGate)
        {
            _treeSnapshotCacheRequestPayload = requestPayloadJson;
            _treeSnapshotCacheScope = snapshot.Scope;
            _treeSnapshotCacheGeneration = snapshot.Generation;
            _treeSnapshotCacheVersion = snapshot.SnapshotVersion;
            _treeSnapshotCachePayloadJson = payloadJson;
        }

        return payloadJson;
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

    private static RemoteResponseMessage BuildSuccessResponse<TPayload>(
        RemoteRequestMessage requestMessage,
        TPayload payload,
        JsonTypeInfo<TPayload> typeInfo)
    {
        var payloadJson = JsonSerializer.Serialize(payload, typeInfo);
        var scope = ResolveScope(payload);
        RecordSnapshotPayloadBytesIfEnabled(requestMessage.Method, scope, payloadJson);

        return BuildSuccessResponse(requestMessage, payloadJson);
    }

    private RemoteResponseMessage BuildSuccessResponse(
        RemoteRequestMessage requestMessage,
        RemoteTreeSnapshot snapshot,
        string requestPayloadJson)
    {
        var payloadJson = GetTreeSnapshotPayloadJson(snapshot, requestPayloadJson);
        RecordSnapshotPayloadBytesIfEnabled(requestMessage.Method, snapshot.Scope, payloadJson);
        return BuildSuccessResponse(requestMessage, payloadJson);
    }

    private static void RecordSnapshotPayloadBytesIfEnabled(
        string method,
        string scope,
        string payloadJson)
    {
        if (RemoteRuntimeMetrics.IsSnapshotPayloadMetricsEnabled)
        {
            var domain = RemoteRuntimeMetrics.ResolveDomainFromMethod(method);
            RemoteRuntimeMetrics.RecordSnapshotPayloadBytes(
                domain: domain,
                scope: scope,
                bytes: RemoteRuntimeMetrics.GetUtf8ByteCount(payloadJson),
                cache: "bypass");
        }
    }

    private static RemoteResponseMessage BuildSuccessResponse(
        RemoteRequestMessage requestMessage,
        string payloadJson)
    {
        return new RemoteResponseMessage(
            SessionId: requestMessage.SessionId,
            RequestId: requestMessage.RequestId,
            IsSuccess: true,
            PayloadJson: payloadJson,
            ErrorCode: string.Empty,
            ErrorMessage: string.Empty);
    }

    private static string ResolveScope<TPayload>(TPayload payload)
    {
        return payload switch
        {
            RemoteTreeSnapshot tree => tree.Scope,
            RemoteSelectionSnapshot selection => selection.Scope,
            RemotePropertiesSnapshot properties => properties.Scope,
            RemoteCodeDocumentsSnapshot codeDocuments => codeDocuments.Scope,
            RemoteCodeResolveNodeSnapshot codeResolve => codeResolve.Scope,
            RemoteBindingsSnapshot bindings => bindings.Scope,
            RemoteStylesSnapshot styles => styles.Scope,
            RemoteEventsSnapshot eventsSnapshot => eventsSnapshot.Scope,
            RemoteBreakpointsSnapshot breakpoints => breakpoints.Scope,
            _ => "none",
        };
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
