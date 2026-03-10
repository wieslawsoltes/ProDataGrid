using System.Threading;
using Avalonia;
using Avalonia.Headless;

namespace ProDiagnostics.Remote.HostLoad;

internal static class BenchmarkAvaloniaBootstrap
{
    private static int s_initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref s_initialized, 1) == 1)
        {
            return;
        }

        AppBuilder
            .Configure<HeadlessBenchmarkApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }

    private sealed class HeadlessBenchmarkApp : Application;
}
