using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(ProDataGrid.ExcelSample.Tests.UnitTestAppBuilder))]

namespace ProDataGrid.ExcelSample.Tests;

internal static class UnitTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var options = new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = true
        };

        return AppBuilder.Configure<UnitTestApp>()
            .UseHeadless(options)
            .UseReactiveUI(static _ => { });
    }
}

internal sealed class UnitTestApp : Application
{
}
