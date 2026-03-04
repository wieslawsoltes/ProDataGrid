using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Hosts remote attach sessions over named pipes.
/// </summary>
public sealed class NamedPipeAttachServer : IAttachServer
{
    private const string TransportName = "namedpipe";
    private const string LogCategory = "remote.attach.namedpipe.server";

    private readonly NamedPipeAttachServerOptions _namedPipeOptions;
    private readonly IRemoteAccessPolicy _accessPolicy;
    private readonly IRemoteProtocolMonitor _protocolMonitor;
    private readonly IRemoteDiagnosticsLogger _diagnosticsLogger;
    private readonly object _sync = new();
    private readonly List<NamedPipeAttachConnection> _connections = new();
    private readonly List<Task> _clientTasks = new();
    private CancellationTokenSource? _lifecycleCts;
    private Task? _acceptLoopTask;
    private Task? _heartbeatLoopTask;
    private long _keepAliveSequence;
    private bool _isRunning;

    public NamedPipeAttachServer(
        NamedPipeAttachServerOptions options,
        IRemoteAccessPolicy? accessPolicy = null,
        IRemoteProtocolMonitor? protocolMonitor = null,
        IRemoteDiagnosticsLogger? diagnosticsLogger = null)
    {
        _namedPipeOptions = NamedPipeAttachServerOptions.Normalize(options);
        _accessPolicy = accessPolicy ?? new DefaultRemoteAccessPolicy(_namedPipeOptions.AccessPolicy);
        _protocolMonitor = protocolMonitor ?? NoOpRemoteProtocolMonitor.Instance;
        _diagnosticsLogger = diagnosticsLogger ?? NoOpRemoteDiagnosticsLogger.Instance;
        Options = _namedPipeOptions.ServerOptions;
    }

    public event EventHandler<AttachConnectionAcceptedEventArgs>? ConnectionAccepted;

    public AttachServerOptions Options { get; }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public Exception? LastError { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!NamedPipeAttachServerOptions.IsPlatformSupported)
        {
            throw new PlatformNotSupportedException(
                "Named pipe transport is supported only on Windows, Linux, and macOS.");
        }

        lock (_sync)
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }

            var lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _lifecycleCts = lifecycleCts;
            _isRunning = true;
            _acceptLoopTask = RunAcceptLoopAsync(lifecycleCts.Token);
            _heartbeatLoopTask = RunHeartbeatLoopAsync(lifecycleCts.Token);
        }

        Log(
            RemoteDiagnosticsLogLevel.Information,
            "started",
            Guid.Empty,
            "pipe:" + _namedPipeOptions.PipeName,
            null,
            0,
            null,
            null);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        List<NamedPipeAttachConnection> connections;
        List<Task> clientTasks;
        Task? acceptLoop;
        Task? heartbeatLoop;
        CancellationTokenSource? lifecycleCts;

        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            connections = _connections.ToList();
            clientTasks = _clientTasks.ToList();
            acceptLoop = _acceptLoopTask;
            heartbeatLoop = _heartbeatLoopTask;
            lifecycleCts = _lifecycleCts;

            _connections.Clear();
            RemoteRuntimeMetrics.SetActiveConnections(TransportName, 0);
            _clientTasks.Clear();
            _acceptLoopTask = null;
            _heartbeatLoopTask = null;
            _lifecycleCts = null;
        }

        lifecycleCts?.Cancel();

        if (acceptLoop is not null)
        {
            await ObserveTaskAsync(acceptLoop).ConfigureAwait(false);
        }

        if (heartbeatLoop is not null)
        {
            await ObserveTaskAsync(heartbeatLoop).ConfigureAwait(false);
        }

        if (clientTasks.Count > 0)
        {
            await Task.WhenAll(clientTasks.Select(ObserveTaskAsync)).ConfigureAwait(false);
        }

        foreach (var connection in connections)
        {
            try
            {
                await connection.CloseAsync("Server stopping", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // no-op
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        lifecycleCts?.Dispose();
        Log(
            RemoteDiagnosticsLogLevel.Information,
            "stopped",
            Guid.Empty,
            "pipe:" + _namedPipeOptions.PipeName,
            null,
            0,
            null,
            null);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = CreateServerStream();
                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var task = HandleConnectedStreamAsync(serverStream, cancellationToken);
                TrackClientTask(task);
            }
            catch (OperationCanceledException)
            {
                serverStream?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                LastError = ex;
                Log(
                    RemoteDiagnosticsLogLevel.Warning,
                    "accept-failed",
                    Guid.Empty,
                    "pipe:" + _namedPipeOptions.PipeName,
                    null,
                    0,
                    ex.Message,
                    ex);
                serverStream?.Dispose();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleConnectedStreamAsync(
        NamedPipeServerStream serverStream,
        CancellationToken cancellationToken)
    {
        var remoteEndpoint = "pipe:" + _namedPipeOptions.PipeName;
        var accessDecision = await _accessPolicy.EvaluateAsync(
                new RemoteAccessRequest(
                    TransportName: "namedpipe",
                    RemoteEndpoint: remoteEndpoint,
                    RemoteAddress: null,
                    AccessToken: null,
                    IsNetworkTransport: false),
                cancellationToken)
            .ConfigureAwait(false);
        if (!accessDecision.IsAllowed)
        {
            _protocolMonitor.RecordConnectionRejected(
                TransportName,
                remoteEndpoint,
                accessDecision.Code,
                accessDecision.Message);
            Log(
                RemoteDiagnosticsLogLevel.Warning,
                "access-denied",
                Guid.Empty,
                remoteEndpoint,
                null,
                0,
                accessDecision.Code + ": " + accessDecision.Message,
                null);
            serverStream.Dispose();
            return;
        }

        var connection = new NamedPipeAttachConnection(
            connectionId: Guid.NewGuid(),
            pipe: serverStream,
            remoteEndpoint: remoteEndpoint,
            receiveTimeout: _namedPipeOptions.ReceiveTimeout,
            maxPayloadBytes: Options.MaxFramePayloadBytes,
            protocolMonitor: _protocolMonitor,
            diagnosticsLogger: _diagnosticsLogger);

        lock (_sync)
        {
            _connections.Add(connection);
            RemoteRuntimeMetrics.SetActiveConnections(TransportName, _connections.Count);
        }

        _protocolMonitor.RecordConnectionAccepted(TransportName, connection.ConnectionId, connection.RemoteEndpoint);
        RemoteRuntimeMetrics.RecordConnectionAccepted(TransportName);
        Log(
            RemoteDiagnosticsLogLevel.Information,
            "connection-accepted",
            connection.ConnectionId,
            connection.RemoteEndpoint,
            null,
            0,
            null,
            null);

        ConnectionAccepted?.Invoke(
            this,
            new AttachConnectionAcceptedEventArgs(connection, DateTimeOffset.UtcNow));

        try
        {
            await connection.WaitForCloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await connection.CloseAsync("Connection aborted", CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            RemoveConnection(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        var delay = Options.HeartbeatInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            List<NamedPipeAttachConnection> snapshot;
            lock (_sync)
            {
                snapshot = _connections.ToList();
            }

            var sentHeartbeats = 0;
            var skippedBecauseActive = 0;
            foreach (var connection in snapshot)
            {
                if (!connection.IsOpen)
                {
                    RemoveConnection(connection);
                    continue;
                }

                if (connection.HasRecentActivity(Options.HeartbeatInterval))
                {
                    skippedBecauseActive++;
                    continue;
                }

                var heartbeatStarted = Stopwatch.GetTimestamp();
                try
                {
                    var keepAlive = new RemoteKeepAliveMessage(
                        SessionId: Guid.Empty,
                        Sequence: Interlocked.Increment(ref _keepAliveSequence),
                        TimestampUtc: DateTimeOffset.UtcNow);
                    await connection.SendAsync(keepAlive, cancellationToken).ConfigureAwait(false);
                    sentHeartbeats++;
                    RemoteRuntimeMetrics.RecordHeartbeatDuration(
                        transport: TransportName,
                        status: "ok",
                        durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(heartbeatStarted));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    RemoteRuntimeMetrics.RecordHeartbeatDuration(
                        transport: TransportName,
                        status: "error",
                        durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(heartbeatStarted));
                    RemoteRuntimeMetrics.RecordTransportFailure(
                        transport: TransportName,
                        method: "keepAlive",
                        domain: "none",
                        status: "error");
                    Log(
                        RemoteDiagnosticsLogLevel.Warning,
                        "heartbeat-send-failed",
                        connection.ConnectionId,
                        connection.RemoteEndpoint,
                        RemoteMessageKind.KeepAlive,
                        0,
                        ex.Message,
                        ex);
                    try
                    {
                        await connection.CloseAsync("Heartbeat failure", cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // no-op
                    }

                    RemoveConnection(connection);
                }
            }

            if (snapshot.Count > 0 && sentHeartbeats == 0 && skippedBecauseActive > 0)
            {
                var doubled = Options.HeartbeatInterval.Ticks * 2;
                var capped = Math.Min(doubled, TimeSpan.FromSeconds(30).Ticks);
                delay = TimeSpan.FromTicks(capped);
            }
            else
            {
                delay = Options.HeartbeatInterval;
            }
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        var options = PipeOptions.Asynchronous;
        if (_namedPipeOptions.CurrentUserOnly &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            options |= PipeOptions.CurrentUserOnly;
        }

        return new NamedPipeServerStream(
            pipeName: _namedPipeOptions.PipeName,
            direction: PipeDirection.InOut,
            maxNumberOfServerInstances: _namedPipeOptions.MaxServerInstances,
            transmissionMode: PipeTransmissionMode.Byte,
            options: options);
    }

    private void TrackClientTask(Task task)
    {
        lock (_sync)
        {
            _clientTasks.Add(task);
        }

        _ = task.ContinueWith(
            static (completed, state) =>
            {
                var @this = (NamedPipeAttachServer)state!;
                lock (@this._sync)
                {
                    @this._clientTasks.Remove(completed);
                }
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void RemoveConnection(NamedPipeAttachConnection connection)
    {
        lock (_sync)
        {
            _connections.Remove(connection);
            RemoteRuntimeMetrics.SetActiveConnections(TransportName, _connections.Count);
        }
    }

    private static async Task ObserveTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // no-op during shutdown
        }
    }

    private void Log(
        RemoteDiagnosticsLogLevel level,
        string eventName,
        Guid connectionId,
        string? remoteEndpoint,
        RemoteMessageKind? messageKind,
        int bytes,
        string? details,
        Exception? exception)
    {
        _diagnosticsLogger.Log(
            new RemoteDiagnosticsLogEntry(
                TimestampUtc: DateTimeOffset.UtcNow,
                Level: level,
                Category: LogCategory,
                EventName: eventName,
                TransportName: TransportName,
                ConnectionId: connectionId,
                SessionId: Guid.Empty,
                RemoteEndpoint: remoteEndpoint,
                MessageKind: messageKind,
                Bytes: bytes,
                Details: details,
                ExceptionType: exception?.GetType().Name));
    }
}
