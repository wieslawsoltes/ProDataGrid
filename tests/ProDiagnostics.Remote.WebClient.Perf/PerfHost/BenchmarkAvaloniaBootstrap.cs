using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using System.Threading;

namespace ProDiagnostics.Remote.WebClient.PerfHost;

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
            .UseHeadless(
                new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true,
                })
            .SetupWithoutStarting();
    }

    private sealed class BenchmarkApp : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow ??= new Avalonia.Controls.Window();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
