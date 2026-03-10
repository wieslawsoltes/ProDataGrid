using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;

namespace ProDiagnostics.Remote.Protocol.Benchmarks;

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
            .Configure<BenchmarkApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .SetupWithoutStarting();
    }

    private sealed class BenchmarkApp : Application;
}
