using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Threading;

namespace ProDiagnostics.Remote.HostLoad;

internal static class Program
{
    public static int Main(string[] args)
    {
        BenchmarkAvaloniaBootstrap.EnsureInitialized();
        using var mainLoopCts = new CancellationTokenSource();
        var runTask = Task.Run(() => RunAsync(args));
        _ = runTask.ContinueWith(
            _ => mainLoopCts.Cancel(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            Dispatcher.UIThread.MainLoop(mainLoopCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation once workload task completes.
        }

        return runTask.GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var options = HostLoadOptions.Parse(args);
        var startedAt = DateTimeOffset.UtcNow;
        var profile = options.Profile;
        var runId = startedAt.ToString("yyyyMMdd-HHmmss");

        var root = await Dispatcher.UIThread.InvokeAsync(
            () => BenchmarkFixtureFactory.CreateDenseWindow(profile.ControlCount),
            DispatcherPriority.Background,
            CancellationToken.None);
        using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var startupCancellationToken = startupCts.Token;
        DevToolsRemoteLoopbackSession? session = null;
        try
        {
            session = await WithTimeoutAsync(
                timedCancellationToken =>
                {
                    var startupTcs = new TaskCompletionSource<DevToolsRemoteLoopbackSession>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _ = Dispatcher.UIThread.InvokeAsync(
                        async () =>
                        {
                            try
                            {
                                var startedSession = await DevToolsRemoteLoopbackSession.StartAsync(
                                    root,
                                    new DevToolsRemoteLoopbackOptions
                                    {
                                        UseDynamicPort = true,
                                        HostOptions = new DevToolsRemoteAttachHostOptions
                                        {
                                            RequestTimeout = TimeSpan.FromSeconds(5),
                                        },
                                        ClientOptions = new RemoteDiagnosticsClientOptions
                                        {
                                            ClientName = "remote-hostload-runner",
                                            ConnectTimeout = TimeSpan.FromSeconds(10),
                                            RequestTimeout = TimeSpan.FromSeconds(5),
                                        },
                                    },
                                    timedCancellationToken);
                                startupTcs.TrySetResult(startedSession);
                            }
                            catch (OperationCanceledException oce)
                            {
                                startupTcs.TrySetCanceled(oce.CancellationToken);
                            }
                            catch (Exception ex)
                            {
                                startupTcs.TrySetException(ex);
                            }
                        },
                        DispatcherPriority.Background,
                        timedCancellationToken);
                    return startupTcs.Task;
                },
                TimeSpan.FromSeconds(15),
                startupCancellationToken);
        }
        catch
        {
            var startupFailed = new HostLoadRunReport(
                RunId: runId,
                Profile: profile.Name,
                StartedAtUtc: startedAt,
                FinishedAtUtc: DateTimeOffset.UtcNow,
                RootNodeCount: profile.ControlCount,
                Scenarios: new[]
                {
                    new HostLoadScenarioResult("startup", 0, 1, 0, 0, 0),
                },
                MetricTotals: new Dictionary<string, double>(StringComparer.Ordinal));
            await WriteReportAsync(startupFailed, runId);
            Console.WriteLine(JsonSerializer.Serialize(startupFailed, HostLoadJsonContext.Default.HostLoadRunReport));
            return 1;
        }

        var collector = new RuntimeMetricCollector(RemoteRuntimeMetrics.RuntimeMeterName);
        try
        {
            var scenarioTimeout = TimeSpan.FromSeconds(Math.Max(20, (int)Math.Ceiling(options.Duration.TotalSeconds * 4)));
            var scenarios = new List<HostLoadScenarioResult>
            {
                await RunWithTimeoutAsync(
                    "snapshot-storm",
                    cancellationToken => RunSnapshotStormAsync(session, options.Duration, cancellationToken),
                    scenarioTimeout),
                await RunWithTimeoutAsync(
                    "mixed-workload",
                    cancellationToken => RunMixedWorkloadAsync(session, options.Duration, cancellationToken),
                    scenarioTimeout),
                await RunWithTimeoutAsync(
                    "stream-fanout",
                    cancellationToken => RunStreamFanoutAsync(profile, cancellationToken),
                    scenarioTimeout),
            };

            collector.RecordObservableInstruments();

            var report = new HostLoadRunReport(
                RunId: runId,
                Profile: profile.Name,
                StartedAtUtc: startedAt,
                FinishedAtUtc: DateTimeOffset.UtcNow,
                RootNodeCount: profile.ControlCount,
                Scenarios: scenarios,
                MetricTotals: collector.ToDictionary());

            await WriteReportAsync(report, runId);
            Console.WriteLine(JsonSerializer.Serialize(report, HostLoadJsonContext.Default.HostLoadRunReport));
            return 0;
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            collector.Dispose();
        }
    }

    private static async Task<HostLoadScenarioResult> RunWithTimeoutAsync(
        string name,
        Func<CancellationToken, Task<HostLoadScenarioResult>> run,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        var runTask = run(cts.Token);
        var completionTask = await Task.WhenAny(runTask, Task.Delay(timeout));
        if (completionTask != runTask)
        {
            cts.Cancel();
            _ = runTask.ContinueWith(
                static task =>
                {
                    _ = task.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return CreateTimedOutScenario(name);
        }

        try
        {
            return await runTask;
        }
        catch (OperationCanceledException)
        {
            return CreateTimedOutScenario(name);
        }
        catch (TimeoutException)
        {
            return CreateTimedOutScenario(name);
        }
    }

    private static HostLoadScenarioResult CreateTimedOutScenario(string name)
    {
        return new HostLoadScenarioResult(
            Name: name,
            Samples: 0,
            Errors: 1,
            AvgMs: 0,
            P95Ms: 0,
            MaxMs: 0);
    }

    private static async Task<HostLoadScenarioResult> RunSnapshotStormAsync(
        DevToolsRemoteLoopbackSession session,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var requestTimeout = TimeSpan.FromMilliseconds(250);
        RemoteTreeSnapshot treeSnapshot;
        try
        {
            treeSnapshot = await WithTimeoutAsync(
                timedCancellationToken => session.ReadOnly.GetTreeSnapshotAsync(
                        new RemoteTreeSnapshotRequest
                        {
                            Scope = "combined",
                            IncludeSourceLocations = false,
                            IncludeVisualDetails = false,
                        },
                        timedCancellationToken).AsTask(),
                requestTimeout,
                cancellationToken);
        }
        catch
        {
            return new HostLoadScenarioResult("snapshot-storm", 0, 1, 0, 0, 0);
        }

        if (treeSnapshot.Nodes.Count == 0)
        {
            return new HostLoadScenarioResult("snapshot-storm", 0, 1, 0, 0, 0);
        }

        var target = treeSnapshot.Nodes.FirstOrDefault(x => x.Depth > 1) ?? treeSnapshot.Nodes.Last();
        var latencies = new ConcurrentQueue<double>();
        var errors = 0;
        using var durationCts = new CancellationTokenSource(duration);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(durationCts.Token, cancellationToken);
        var iterations = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * 2));

        for (var i = 0; i < iterations && !cts.IsCancellationRequested; i++)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetTreeSnapshotAsync(
                            new RemoteTreeSnapshotRequest
                            {
                                Scope = "combined",
                                IncludeSourceLocations = false,
                                IncludeVisualDetails = false,
                            },
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);

                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetPropertiesSnapshotAsync(
                            new RemotePropertiesSnapshotRequest
                            {
                                Scope = "combined",
                                NodeId = target.NodeId,
                                NodePath = target.NodePath,
                                IncludeClrProperties = true,
                            },
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
            finally
            {
                latencies.Enqueue(RemoteRuntimeMetrics.ElapsedMilliseconds(started));
            }
        }

        return HostLoadScenarioResult.FromSamples("snapshot-storm", latencies, errors);
    }

    private static async Task<HostLoadScenarioResult> RunMixedWorkloadAsync(
        DevToolsRemoteLoopbackSession session,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var requestTimeout = TimeSpan.FromMilliseconds(250);
        RemoteTreeSnapshot treeSnapshot;
        try
        {
            treeSnapshot = await WithTimeoutAsync(
                timedCancellationToken => session.ReadOnly.GetTreeSnapshotAsync(
                        new RemoteTreeSnapshotRequest
                        {
                            Scope = "combined",
                            IncludeSourceLocations = false,
                            IncludeVisualDetails = false,
                        },
                        timedCancellationToken).AsTask(),
                requestTimeout,
                cancellationToken);
        }
        catch
        {
            return new HostLoadScenarioResult("mixed-workload", 0, 1, 0, 0, 0);
        }

        if (treeSnapshot.Nodes.Count == 0)
        {
            return new HostLoadScenarioResult("mixed-workload", 0, 1, 0, 0, 0);
        }

        var target = treeSnapshot.Nodes.FirstOrDefault(x => x.Depth > 1) ?? treeSnapshot.Nodes.Last();
        var latencies = new ConcurrentQueue<double>();
        var errors = 0;
        using var durationCts = new CancellationTokenSource(duration);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(durationCts.Token, cancellationToken);
        var iterations = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * 2));

        for (var i = 0; i < iterations && !cts.IsCancellationRequested; i++)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetStylesSnapshotAsync(
                            new RemoteStylesSnapshotRequest
                            {
                                Scope = "combined",
                                NodeId = target.NodeId,
                                NodePath = target.NodePath,
                            },
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
            finally
            {
                latencies.Enqueue(RemoteRuntimeMetrics.ElapsedMilliseconds(started));
            }

            started = Stopwatch.GetTimestamp();
            try
            {
                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetResourcesSnapshotAsync(
                            new RemoteResourcesSnapshotRequest
                            {
                                IncludeEntries = true,
                            },
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);
                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetAssetsSnapshotAsync(
                            new RemoteAssetsSnapshotRequest(),
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);
                _ = await WithTimeoutAsync(
                    timedCancellationToken => session.ReadOnly.GetLogsSnapshotAsync(
                            new RemoteLogsSnapshotRequest(),
                            timedCancellationToken).AsTask(),
                    requestTimeout,
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
            finally
            {
                latencies.Enqueue(RemoteRuntimeMetrics.ElapsedMilliseconds(started));
            }
        }

        return HostLoadScenarioResult.FromSamples("mixed-workload", latencies, errors);
    }

    private static async Task<HostLoadScenarioResult> RunStreamFanoutAsync(
        HostLoadProfile profile,
        CancellationToken cancellationToken)
    {
        var options = new RemoteStreamSessionHubOptions
        {
            MaxQueueLengthPerSession = 2048,
            MaxDispatchBatchSize = 512,
        };
        await using var hub = new RemoteStreamSessionHub(options);
        var connections = new List<NoOpAttachConnection>(profile.StreamSessionCount);

        for (var i = 0; i < profile.StreamSessionCount; i++)
        {
            var connection = new NoOpAttachConnection("hostload-" + i);
            connections.Add(connection);
            hub.RegisterSession(Guid.NewGuid(), connection);
        }

        var latencies = new ConcurrentQueue<double>();
        var errors = 0;
        for (var i = 0; i < profile.StreamMessages; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                hub.Publish(
                    RemoteStreamTopics.Logs,
                    "{\"timestampUtc\":\"2026-03-04T00:00:00Z\",\"level\":\"Info\",\"message\":\"hostload\"}");
            }
            catch
            {
                errors++;
            }
            finally
            {
                latencies.Enqueue(RemoteRuntimeMetrics.ElapsedMilliseconds(started));
            }
        }

        return HostLoadScenarioResult.FromSamples("stream-fanout", latencies, errors);
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var task = operation(timeoutCts.Token);
        var completed = await Task.WhenAny(task, Task.Delay(timeout, CancellationToken.None));
        if (completed == task)
        {
            return await task;
        }

        timeoutCts.Cancel();
        _ = task.ContinueWith(
            static timedOutTask =>
            {
                _ = timedOutTask.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        throw new TimeoutException();
    }

    private static async Task WriteReportAsync(HostLoadRunReport report, string runId)
    {
        var dir = Path.Combine("artifacts", "remote-hostload");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, runId + ".json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(report, HostLoadJsonContext.Default.HostLoadRunReport));
    }

    private sealed class NoOpAttachConnection : IAttachConnection
    {
        public NoOpAttachConnection(string endpoint)
        {
            ConnectionId = Guid.NewGuid();
            RemoteEndpoint = endpoint;
        }

        public Guid ConnectionId { get; }

        public string? RemoteEndpoint { get; }

        public bool IsOpen => true;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return ValueTask.FromResult<AttachReceiveResult?>(null);
        }

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
        {
            _ = reason;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RuntimeMetricCollector : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly ConcurrentDictionary<string, double> _totals = new(StringComparer.Ordinal);

        public RuntimeMetricCollector(string meterName)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
                _totals.AddOrUpdate(instrument.Name, measurement, (_, current) => current + measurement));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
                _totals.AddOrUpdate(instrument.Name, measurement, (_, current) => current + measurement));
            _listener.Start();
        }

        public void RecordObservableInstruments() => _listener.RecordObservableInstruments();

        public IReadOnlyDictionary<string, double> ToDictionary() => new Dictionary<string, double>(_totals);

        public void Dispose() => _listener.Dispose();
    }
}

[JsonSerializable(typeof(HostLoadRunReport))]
internal partial class HostLoadJsonContext : JsonSerializerContext;
