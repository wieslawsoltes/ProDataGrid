using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace ProDataGrid.ExcelSample;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI(static _ => { })
            .LogToTrace();
}
