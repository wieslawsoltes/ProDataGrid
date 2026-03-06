using System;
using Avalonia;

namespace ProDiagnostics.DevTools;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        DevToolsStartupSettings.ApplyFromArgs(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
