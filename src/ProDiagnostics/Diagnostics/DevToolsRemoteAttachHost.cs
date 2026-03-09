using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;

namespace Avalonia.Diagnostics;

/// <summary>
/// Hosts remote diagnostics attach endpoint for a local Avalonia object tree.
/// </summary>
public sealed class DevToolsRemoteAttachHost : IAsyncDisposable
{
    private static readonly HashSet<string> s_readOnlyMethods = new(StringComparer.Ordinal)
    {
        RemoteReadOnlyMethods.PreviewCapabilitiesGet,
        RemoteReadOnlyMethods.PreviewSnapshotGet,
        RemoteReadOnlyMethods.TreeSnapshotGet,
        RemoteReadOnlyMethods.SelectionGet,
        RemoteReadOnlyMethods.PropertiesSnapshotGet,
        RemoteReadOnlyMethods.Elements3DSnapshotGet,
        RemoteReadOnlyMethods.OverlayOptionsGet,
        RemoteReadOnlyMethods.CodeDocumentsGet,
        RemoteReadOnlyMethods.CodeResolveNode,
        RemoteReadOnlyMethods.BindingsSnapshotGet,
        RemoteReadOnlyMethods.StylesSnapshotGet,
        RemoteReadOnlyMethods.ResourcesSnapshotGet,
        RemoteReadOnlyMethods.AssetsSnapshotGet,
        RemoteReadOnlyMethods.EventsSnapshotGet,
        RemoteReadOnlyMethods.BreakpointsSnapshotGet,
        RemoteReadOnlyMethods.LogsSnapshotGet,
        RemoteReadOnlyMethods.MetricsSnapshotGet,
        RemoteReadOnlyMethods.ProfilerSnapshotGet,
    };

    private static readonly HashSet<string> s_mutationMethods = new(StringComparer.Ordinal)
    {
        RemoteMutationMethods.PreviewPausedSet,
        RemoteMutationMethods.PreviewSettingsSet,
        RemoteMutationMethods.PreviewInputInject,
        RemoteMutationMethods.InspectHovered,
        RemoteMutationMethods.SelectionSet,
        RemoteMutationMethods.PropertiesSet,
        RemoteMutationMethods.PseudoClassSet,
        RemoteMutationMethods.Elements3DRootSet,
        RemoteMutationMethods.Elements3DRootReset,
        RemoteMutationMethods.Elements3DFiltersSet,
        RemoteMutationMethods.OverlayOptionsSet,
        RemoteMutationMethods.OverlayLiveHoverSet,
        RemoteMutationMethods.CodeDocumentOpen,
        RemoteMutationMethods.BreakpointsPropertyAdd,
        RemoteMutationMethods.BreakpointsEventAdd,
        RemoteMutationMethods.BreakpointsRemove,
        RemoteMutationMethods.BreakpointsToggle,
        RemoteMutationMethods.BreakpointsClear,
        RemoteMutationMethods.BreakpointsEnabledSet,
        RemoteMutationMethods.EventsClear,
        RemoteMutationMethods.EventsNodeEnabledSet,
        RemoteMutationMethods.EventsDefaultsEnable,
        RemoteMutationMethods.EventsDisableAll,
        RemoteMutationMethods.LogsClear,
        RemoteMutationMethods.LogsLevelsSet,
        RemoteMutationMethods.StreamDemandSet,
        RemoteMutationMethods.MetricsPausedSet,
        RemoteMutationMethods.MetricsSettingsSet,
        RemoteMutationMethods.ProfilerPausedSet,
        RemoteMutationMethods.ProfilerSettingsSet,
    };

    private static readonly string[] s_featureOrder =
    {
        "read-only",
        "mutation",
        "streaming",
        "trees",
        "selection",
        "properties",
        "preview",
        "code",
        "bindings",
        "styles",
        "resources",
        "assets",
        "elements3d",
        "overlay",
        "breakpoints",
        "events",
        "logs",
        "metrics",
        "profiler",
    };

    private const string TransportName = "http";
    private const string LogCategory = "remote.attach.host";

    private readonly object _gate = new();
    private readonly HttpAttachServer _server;
    private readonly HttpAttachServerOptions _httpOptions;
    private readonly RemoteAttachSessionManager _sessionManager;
    private readonly RemoteReadOnlyMessageRouter _readOnlyRouter;
    private readonly InProcessRemoteMutationDiagnosticsSource? _mutationSource;
    private readonly RemoteMutationMessageRouter? _mutationRouter;
    private readonly RemoteStreamSessionHub? _streamHub;
    private readonly InProcessRemoteStreamSource? _streamSource;
    private readonly RemoteAttachStreamBridge? _streamBridge;
    private readonly InProcessRemoteSelectionState _selectionState;
    private readonly Elements3DPageViewModel _elements3DPageViewModel;
    private readonly InProcessRemoteOverlayState _overlayState;
    private readonly BreakpointService? _breakpointService;
    private readonly EventsPageViewModel? _eventsPageViewModel;
    private readonly LogsPageViewModel? _logsPageViewModel;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly bool _enableMutationApi;
    private readonly bool _enableStreamingApi;
    private readonly TimeSpan _requestTimeout;
    private readonly CancellationTokenSource _lifecycleCts = new();
    private readonly List<Task> _connectionTasks = new();
    private bool _isStarted;
    private bool _isDisposed;

    /// <summary>
    /// Creates remote attach host for the given diagnostics root.
    /// </summary>
    public DevToolsRemoteAttachHost(AvaloniaObject root, DevToolsRemoteAttachHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var normalized = DevToolsRemoteAttachHostOptions.Normalize(options);

        _httpOptions = normalized.HttpOptions;
        _enableMutationApi = normalized.EnableMutationApi;
        _enableStreamingApi = normalized.EnableStreamingApi;
        _requestTimeout = normalized.RequestTimeout;
        _protocolMonitor = normalized.ProtocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = normalized.DiagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
        _sessionManager = new RemoteAttachSessionManager(normalized.SessionManagerOptions);
        _selectionState = new InProcessRemoteSelectionState();
        _overlayState = new InProcessRemoteOverlayState();
        var nodeIdentityProvider = new InProcessRemoteNodeIdentityProvider();
        _elements3DPageViewModel = new Elements3DPageViewModel(root, selectedObjectAccessor: null);
        _breakpointService = new BreakpointService();
        _eventsPageViewModel = new EventsPageViewModel(mainViewModel: null, breakpointService: _breakpointService);
        _logsPageViewModel = new LogsPageViewModel();

        if (normalized.EnableStreamingApi)
        {
            _streamSource = new InProcessRemoteStreamSource(
                root: root,
                eventsPageViewModel: _eventsPageViewModel,
                nodeIdentityProvider: nodeIdentityProvider,
                options: InProcessRemoteStreamSourceOptions.Default with
                {
                    EnableUdpTelemetryFallback = normalized.EnableUdpTelemetryFallback,
                    UdpPort = normalized.UdpTelemetryPort,
                });

            _streamSource.SetPreviewPaused(normalized.StartWithPreviewPaused);
            _streamSource.SetMetricsPaused(normalized.StartWithMetricsPaused);
            _streamSource.SetProfilerPaused(normalized.StartWithProfilerPaused);
        }

        var readOnlySource = new InProcessRemoteReadOnlyDiagnosticsSource(
            root,
            breakpointService: _breakpointService,
            eventsPageViewModel: _eventsPageViewModel,
            logsPageViewModel: _logsPageViewModel,
            streamPauseController: _streamSource,
            elements3DPageViewModel: _elements3DPageViewModel,
            overlayState: _overlayState,
            nodeIdentityProvider: nodeIdentityProvider,
            selectionState: _selectionState);
        _readOnlyRouter = new RemoteReadOnlyMessageRouter(readOnlySource);

        if (normalized.EnableStreamingApi && _streamSource is not null)
        {
            _streamHub = new RemoteStreamSessionHub(
                normalized.StreamHubOptions,
                protocolMonitor: _protocolMonitor,
                diagnosticsLogger: _diagnosticsLogger);
            _streamBridge = new RemoteAttachStreamBridge(_streamHub, _streamSource);
            _streamSource.SetStreamDemand(_streamHub.GetAggregatedDemand());
        }

        if (normalized.EnableMutationApi)
        {
            _mutationSource = new InProcessRemoteMutationDiagnosticsSource(
                root,
                breakpointService: _breakpointService,
                eventsPageViewModel: _eventsPageViewModel,
                logsPageViewModel: _logsPageViewModel,
                streamPauseController: _streamSource,
                elements3DPageViewModel: _elements3DPageViewModel,
                overlayState: _overlayState,
                nodeIdentityProvider: nodeIdentityProvider,
                selectionState: _selectionState);
            _mutationRouter = new RemoteMutationMessageRouter(_mutationSource);
        }

        if (_streamSource is not null)
        {
            _selectionState.SelectionChanged += OnSelectionChanged;
        }

        _server = new HttpAttachServer(
            _httpOptions,
            protocolMonitor: _protocolMonitor,
            diagnosticsLogger: _diagnosticsLogger);
        _server.ConnectionAccepted += OnConnectionAccepted;
    }

    /// <summary>
    /// Gets server WebSocket endpoint used by remote clients.
    /// </summary>
    public Uri WebSocketEndpoint => _httpOptions.BuildClientWebSocketUri();

    /// <summary>
    /// Gets a value indicating whether HTTP attach host is running.
    /// </summary>
    public bool IsRunning => _server.IsRunning;

    /// <summary>
    /// Starts remote attach server.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_isStarted)
            {
                return Task.CompletedTask;
            }

            _isStarted = true;
        }

        return _server.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops remote attach server and active connection loops.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task[] pendingTasks;
        lock (_gate)
        {
            if (!_isStarted)
            {
                return;
            }

            _isStarted = false;
            _lifecycleCts.Cancel();
            pendingTasks = _connectionTasks.ToArray();
            _connectionTasks.Clear();
        }

        await _server.StopAsync(cancellationToken).ConfigureAwait(false);
        if (pendingTasks.Length > 0)
        {
            await Task.WhenAll(pendingTasks.Select(ObserveTaskAsync)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        _server.ConnectionAccepted -= OnConnectionAccepted;
        if (_streamSource is not null)
        {
            _selectionState.SelectionChanged -= OnSelectionChanged;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _server.DisposeAsync().ConfigureAwait(false);
        await DisposeStreamingAsync().ConfigureAwait(false);
        _mutationSource?.Dispose();
        _eventsPageViewModel?.Dispose();
        _logsPageViewModel?.Dispose();
        _lifecycleCts.Dispose();
    }

    private void OnConnectionAccepted(object? sender, AttachConnectionAcceptedEventArgs e)
    {
        Task task;
        lock (_gate)
        {
            if (_isDisposed || !_isStarted)
            {
                return;
            }

            task = RunConnectionLoopAsync(e.Connection, _lifecycleCts.Token);
            _connectionTasks.Add(task);
        }

        _ = ObserveConnectionTaskAsync(task);
    }

    private void OnSelectionChanged(RemoteSelectionSnapshot snapshot)
    {
        _streamSource?.PublishSelection(snapshot);
    }

    private async Task ObserveConnectionTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnosticsLogger.Log(
                new RemoteDiagnosticsLogEntry(
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: RemoteDiagnosticsLogLevel.Warning,
                    Category: LogCategory,
                    EventName: "connection-loop-failed",
                    TransportName: TransportName,
                    ConnectionId: Guid.Empty,
                    SessionId: Guid.Empty,
                    RemoteEndpoint: null,
                    MessageKind: null,
                    Bytes: 0,
                    Details: ex.Message,
                    ExceptionType: ex.GetType().Name));
        }
        finally
        {
            lock (_gate)
            {
                _connectionTasks.Remove(task);
            }
        }
    }

    private async Task RunConnectionLoopAsync(IAttachConnection connection, CancellationToken cancellationToken)
    {
        Guid sessionId = Guid.Empty;
        try
        {
            while (!cancellationToken.IsCancellationRequested && connection.IsOpen)
            {
                var received = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (received is null)
                {
                    break;
                }

                var message = received.Value.Message;
                switch (message)
                {
                    case RemoteHelloMessage hello:
                        if (!await HandleHelloAsync(connection, hello, cancellationToken).ConfigureAwait(false))
                        {
                            return;
                        }

                        sessionId = hello.SessionId;
                        break;

                    case RemoteRequestMessage request:
                        var requestStarted = Stopwatch.GetTimestamp();
                        var requestStatus = "ok";
                        var payloadOutBytes = 0L;
                        try
                        {
                            IRemoteMessage? requestResponse;
                            try
                            {
                                requestResponse = await HandleRequestAsync(connection, request, sessionId, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                requestStatus = "cancelled";
                                throw;
                            }
                            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                            {
                                requestStatus = "error";
                                requestResponse = CreateFailureResponse(
                                    request,
                                    "server_error",
                                    ex.Message);
                            }

                            if (requestResponse is not null)
                            {
                                var sendResult = await TrySendRequestResponseAsync(
                                        connection,
                                        sessionId,
                                        request,
                                        requestResponse,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                                payloadOutBytes = sendResult.PayloadOutBytes;
                                requestStatus = sendResult.Status;
                                if (!sendResult.KeepConnectionOpen)
                                {
                                    if (string.Equals(requestStatus, "ok", StringComparison.Ordinal))
                                    {
                                        requestStatus = "error";
                                    }

                                    return;
                                }
                            }
                        }
                        finally
                        {
                            var requestDomain = RemoteRuntimeMetrics.ResolveDomainFromMethod(request.Method);
                            RemoteRuntimeMetrics.RecordRequest(
                                transport: TransportName,
                                method: request.Method,
                                domain: requestDomain,
                                scope: "none",
                                status: requestStatus,
                                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(requestStarted),
                                payloadInBytes: received.Value.FrameSizeBytes,
                                payloadOutBytes: payloadOutBytes);
                        }
                        break;

                    case RemoteKeepAliveMessage keepAlive:
                        if (keepAlive.SessionId != Guid.Empty)
                        {
                            _sessionManager.TryTouch(keepAlive.SessionId, DateTimeOffset.UtcNow);
                        }

                        break;

                    case RemoteDisconnectMessage disconnect:
                        var disconnectSessionId = disconnect.SessionId != Guid.Empty
                            ? disconnect.SessionId
                            : sessionId;
                        UnregisterSession(disconnectSessionId);
                        await connection.CloseAsync(disconnect.Reason, cancellationToken).ConfigureAwait(false);
                        return;
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _diagnosticsLogger.Log(
                new RemoteDiagnosticsLogEntry(
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: RemoteDiagnosticsLogLevel.Warning,
                    Category: LogCategory,
                    EventName: "connection-loop-unhandled-exception",
                    TransportName: TransportName,
                    ConnectionId: connection.ConnectionId,
                    SessionId: sessionId,
                    RemoteEndpoint: connection.RemoteEndpoint,
                    MessageKind: null,
                    Bytes: 0,
                    Details: ex.Message,
                    ExceptionType: ex.GetType().Name));
            try
            {
                await connection.CloseAsync("Connection loop failure", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best effort close when connection loop fails.
            }
        }
        finally
        {
            UnregisterSession(sessionId);
        }
    }

    private async Task<bool> HandleHelloAsync(
        IAttachConnection connection,
        RemoteHelloMessage hello,
        CancellationToken cancellationToken)
    {
        if (!_sessionManager.TryRegisterFromHello(
                connection.ConnectionId,
                TransportName,
                connection.RemoteEndpoint,
                hello,
                DateTimeOffset.UtcNow,
                out var session,
                out var rejectionReason))
        {
            await connection.SendAsync(
                    new RemoteHelloRejectMessage(
                        SessionId: hello.SessionId,
                        Reason: "session_rejected",
                        Details: rejectionReason ?? "Session rejected."),
                    cancellationToken)
                .ConfigureAwait(false);
            await connection.CloseAsync(rejectionReason ?? "Session rejected.", cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (_streamHub is not null && session is not null)
        {
            _streamHub.RegisterSession(session.SessionId, connection);
            SyncStreamDemandFromSessions();
        }

        _mutationSource?.SetActiveRemoteSessionCount(_sessionManager.SessionCount);

        var enabledFeatures = SelectEnabledFeatures(
            includeMutation: _enableMutationApi,
            includeStreaming: _enableStreamingApi,
            requestedFeatures: hello.RequestedFeatures);
        var helloAck = new RemoteHelloAckMessage(
            SessionId: hello.SessionId,
            NegotiatedProtocolVersion: RemoteProtocol.Version,
            EnabledFeatures: enabledFeatures);
        await connection.SendAsync(helloAck, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<IRemoteMessage?> HandleRequestAsync(
        IAttachConnection connection,
        RemoteRequestMessage request,
        Guid activeSessionId,
        CancellationToken cancellationToken)
    {
        if (activeSessionId == Guid.Empty)
        {
            return CreateFailureResponse(
                request,
                "handshake_required",
                "Hello handshake must be completed before requests.");
        }

        if (request.SessionId != Guid.Empty && request.SessionId != activeSessionId)
        {
            return CreateFailureResponse(
                request,
                "session_mismatch",
                "Request session does not match negotiated connection session.");
        }

        _sessionManager.TryTouch(activeSessionId, DateTimeOffset.UtcNow);

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(_requestTimeout);
        var requestCancellationToken = requestCts.Token;

        try
        {
            if (s_readOnlyMethods.Contains(request.Method))
            {
                return await _readOnlyRouter.HandleAsync(connection, request, requestCancellationToken).ConfigureAwait(false);
            }

            if (s_mutationMethods.Contains(request.Method))
            {
                if (string.Equals(request.Method, RemoteMutationMethods.StreamDemandSet, StringComparison.Ordinal))
                {
                    if (_streamHub is null || _streamSource is null)
                    {
                        return CreateFailureResponse(
                            request,
                            "streaming_disabled",
                            "Streaming API is disabled on this host.");
                    }

                    return HandleStreamDemandSet(request, activeSessionId);
                }

                if (_mutationRouter is null)
                {
                    return CreateFailureResponse(
                        request,
                        "mutation_disabled",
                        "Mutation API is disabled on this host.");
                }

                return await _mutationRouter.HandleAsync(connection, request, requestCancellationToken).ConfigureAwait(false);
            }

            return CreateFailureResponse(
                request,
                "method_not_found",
                "Unsupported method: " + request.Method);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && requestCts.IsCancellationRequested)
        {
            var details = "Request timed out: " + request.Method + " #" + request.RequestId;
            _protocolMonitor.RecordReceiveFailure(TransportName, connection.ConnectionId, connection.RemoteEndpoint, details);
            _diagnosticsLogger.Log(
                new RemoteDiagnosticsLogEntry(
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: RemoteDiagnosticsLogLevel.Warning,
                    Category: LogCategory,
                    EventName: "request-timeout",
                    TransportName: TransportName,
                    ConnectionId: connection.ConnectionId,
                    SessionId: activeSessionId,
                    RemoteEndpoint: connection.RemoteEndpoint,
                    MessageKind: request.Kind,
                    Bytes: 0,
                    Details: details,
                    ExceptionType: null));
            return CreateFailureResponse(
                request,
                "request_timeout",
                "Remote request timed out: " + request.Method);
        }
    }

    private async Task<RequestResponseSendResult> TrySendRequestResponseAsync(
        IAttachConnection connection,
        Guid sessionId,
        RemoteRequestMessage request,
        IRemoteMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(response, cancellationToken).ConfigureAwait(false);
            return new RequestResponseSendResult(
                KeepConnectionOpen: true,
                Status: ResolveResponseStatus(response),
                PayloadOutBytes: ResolveResponsePayloadBytes(response));
        }
        catch (InvalidOperationException ex) when (
            IsPayloadTooLargeException(ex) &&
            response is RemoteResponseMessage { IsSuccess: true })
        {
            var fallback = CreateFailureResponse(
                request,
                "payload_too_large",
                "Response payload exceeds max frame size. Refine request scope or filters.");
            try
            {
                await connection.SendAsync(fallback, cancellationToken).ConfigureAwait(false);
                return new RequestResponseSendResult(
                    KeepConnectionOpen: true,
                    Status: ResolveResponseStatus(fallback),
                    PayloadOutBytes: ResolveResponsePayloadBytes(fallback));
            }
            catch (Exception fallbackEx) when (!cancellationToken.IsCancellationRequested)
            {
                LogConnectionLoopSendFailure(connection, sessionId, request, fallbackEx, isFallback: true);
                return new RequestResponseSendResult(
                    KeepConnectionOpen: false,
                    Status: "error",
                    PayloadOutBytes: 0);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogConnectionLoopSendFailure(connection, sessionId, request, ex, isFallback: false);
            return new RequestResponseSendResult(
                KeepConnectionOpen: false,
                Status: "error",
                PayloadOutBytes: 0);
        }
    }

    private void LogConnectionLoopSendFailure(
        IAttachConnection connection,
        Guid sessionId,
        RemoteRequestMessage request,
        Exception exception,
        bool isFallback)
    {
        var eventName = isFallback
            ? "request-response-fallback-send-failed"
            : "request-response-send-failed";
        var details = (isFallback
            ? "Fallback response send failed for "
            : "Response send failed for ")
            + request.Method
            + " #"
            + request.RequestId
            + ": "
            + exception.Message;
        _diagnosticsLogger.Log(
            new RemoteDiagnosticsLogEntry(
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: RemoteDiagnosticsLogLevel.Warning,
                Category: LogCategory,
                EventName: eventName,
                TransportName: TransportName,
                ConnectionId: connection.ConnectionId,
                SessionId: sessionId,
                RemoteEndpoint: connection.RemoteEndpoint,
                MessageKind: request.Kind,
                Bytes: 0,
                Details: details,
                ExceptionType: exception.GetType().Name));
    }

    private static bool IsPayloadTooLargeException(InvalidOperationException exception) =>
        exception.Message.Contains("max frame size", StringComparison.OrdinalIgnoreCase);

    private static string ResolveResponseStatus(IRemoteMessage response)
    {
        return response switch
        {
            RemoteResponseMessage success when success.IsSuccess => "ok",
            RemoteResponseMessage failure => MapResponseStatus(failure.ErrorCode),
            _ => "ok",
        };
    }

    private static long ResolveResponsePayloadBytes(IRemoteMessage response)
    {
        return response is RemoteResponseMessage responseMessage
            ? RemoteRuntimeMetrics.GetUtf8ByteCount(responseMessage.PayloadJson)
            : 0;
    }

    private void UnregisterSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        _sessionManager.TryRemove(sessionId, out _);
        _streamHub?.TryUnregisterSession(sessionId);
        SyncStreamDemandFromSessions();
        _mutationSource?.SetActiveRemoteSessionCount(_sessionManager.SessionCount);
    }

    private RemoteResponseMessage HandleStreamDemandSet(RemoteRequestMessage request, Guid activeSessionId)
    {
        RemoteSetStreamDemandRequest? payload;
        try
        {
            payload = string.IsNullOrWhiteSpace(request.PayloadJson)
                ? new RemoteSetStreamDemandRequest()
                : JsonSerializer.Deserialize(
                    request.PayloadJson,
                    RemoteJsonSerializerContext.Default.RemoteSetStreamDemandRequest);
        }
        catch (JsonException ex)
        {
            return CreateFailureResponse(request, "invalid_request", ex.Message);
        }

        if (_streamHub is null || _streamSource is null)
        {
            return CreateFailureResponse(
                request,
                "streaming_disabled",
                "Streaming API is disabled on this host.");
        }

        var changed = _streamHub.SetSessionDemand(activeSessionId, payload?.Topics);
        SyncStreamDemandFromSessions();
        var result = new RemoteMutationResult(
            Operation: RemoteMutationMethods.StreamDemandSet,
            Changed: changed,
            Message: changed
                ? "Updated stream topic demand for active session."
                : "Stream topic demand unchanged.",
            AffectedCount: changed ? 1 : 0);

        return new RemoteResponseMessage(
            SessionId: request.SessionId,
            RequestId: request.RequestId,
            IsSuccess: true,
            PayloadJson: JsonSerializer.Serialize(result, RemoteJsonSerializerContext.Default.RemoteMutationResult),
            ErrorCode: string.Empty,
            ErrorMessage: string.Empty);
    }

    private void SyncStreamDemandFromSessions()
    {
        if (_streamHub is null || _streamSource is null)
        {
            return;
        }

        _streamSource.SetStreamDemand(_streamHub.GetAggregatedDemand());
    }

    private async ValueTask DisposeStreamingAsync()
    {
        if (_streamBridge is not null)
        {
            await _streamBridge.DisposeAsync().ConfigureAwait(false);
        }

        if (_streamHub is not null)
        {
            await _streamHub.DisposeAsync().ConfigureAwait(false);
        }

        _streamSource?.Dispose();
    }

    private static IReadOnlyList<string> SelectEnabledFeatures(
        bool includeMutation,
        bool includeStreaming,
        IReadOnlyList<string>? requestedFeatures)
    {
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "read-only",
            "trees",
            "selection",
            "properties",
            "preview",
            "code",
            "bindings",
            "styles",
            "resources",
            "assets",
            "elements3d",
            "overlay",
            "breakpoints",
            "events",
            "logs",
        };

        if (includeMutation)
        {
            available.Add("mutation");
            available.Add("overlay");
            available.Add("elements3d");
            available.Add("breakpoints");
            available.Add("events");
            available.Add("logs");
            available.Add("metrics");
            available.Add("profiler");
            available.Add("selection");
            available.Add("preview");
        }

        if (includeStreaming)
        {
            available.Add("streaming");
            available.Add("preview");
            available.Add("events");
            available.Add("logs");
            available.Add("metrics");
            available.Add("profiler");
            available.Add("selection");
        }

        if (requestedFeatures is null || requestedFeatures.Count == 0)
        {
            return s_featureOrder.Where(available.Contains).ToArray();
        }

        var requested = new HashSet<string>(requestedFeatures, StringComparer.OrdinalIgnoreCase);
        var negotiated = s_featureOrder
            .Where(feature => available.Contains(feature) && requested.Contains(feature))
            .ToArray();

        if (negotiated.Length > 0)
        {
            return negotiated;
        }

        return s_featureOrder
            .Where(feature => string.Equals(feature, "read-only", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static RemoteResponseMessage CreateFailureResponse(
        RemoteRequestMessage request,
        string errorCode,
        string errorMessage)
    {
        return new RemoteResponseMessage(
            SessionId: request.SessionId,
            RequestId: request.RequestId,
            IsSuccess: false,
            PayloadJson: "{}",
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    private static string MapResponseStatus(string? errorCode)
    {
        return errorCode switch
        {
            "request_timeout" => "timeout",
            "request_cancelled" => "cancelled",
            _ => "error",
        };
    }

    private readonly record struct RequestResponseSendResult(
        bool KeepConnectionOpen,
        string Status,
        long PayloadOutBytes);

    private static async Task ObserveTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // no-op
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DevToolsRemoteAttachHost));
        }
    }
}
