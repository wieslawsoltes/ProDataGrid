using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Threading;

namespace ProDiagnostics.Remote.WebClient.PerfHost;

internal static class Program
{
    public static int Main(string[] args)
    {
        BenchmarkAvaloniaBootstrap.EnsureInitialized();
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        var runTask = Task.Run(() => RunAsync(args, shutdown.Token));
        _ = runTask.ContinueWith(
            _ => shutdown.Cancel(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            Dispatcher.UIThread.MainLoop(shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // expected when host task completes or Ctrl+C is pressed
        }

        return runTask.GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken shutdownToken)
    {
        var options = PerfHostOptions.Parse(args);
        var root = await Dispatcher.UIThread.InvokeAsync(
            () => BenchmarkFixtureFactory.CreateDenseWindow(options.ControlCount),
            DispatcherPriority.Background,
            CancellationToken.None);
        var hostOptions = new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = options.Port,
                Path = options.Path,
                BindingMode = HttpAttachBindingMode.Localhost,
            },
            RequestTimeout = TimeSpan.FromSeconds(30),
            StartWithMetricsPaused = true,
            StartWithProfilerPaused = true,
            StartWithPreviewPaused = true,
        };

        var host = await Dispatcher.UIThread.InvokeAsync(
            () => new DevToolsRemoteAttachHost(root, hostOptions),
            DispatcherPriority.Background,
            shutdownToken);
        await using (host.ConfigureAwait(false))
        {
            await host.StartAsync(shutdownToken);
            Console.WriteLine("[PerfHost] READY " + host.WebSocketEndpoint);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, shutdownToken);
            }
            catch (OperationCanceledException)
            {
                // expected on Ctrl+C/teardown
            }
        }

        return 0;
    }

    private sealed record PerfHostOptions(int Port, string Path, int ControlCount)
    {
        public static PerfHostOptions Parse(string[] args)
        {
            var port = 29414;
            var path = "/attach";
            var controlCount = 1500;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out var parsedPort) && parsedPort is > 0 and <= 65535)
                    {
                        port = parsedPort;
                    }
                    continue;
                }

                if (string.Equals(arg, "--path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    path = args[++i];
                    continue;
                }

                if (string.Equals(arg, "--controls", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out var parsedCount) && parsedCount > 0)
                    {
                        controlCount = parsedCount;
                    }
                }
            }

            return new PerfHostOptions(port, path, controlCount);
        }
    }
}
