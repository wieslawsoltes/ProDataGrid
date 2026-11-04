using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Avalonia.Diagnostics.UnitTests.TestAppBuilder))]

namespace Avalonia.Diagnostics.UnitTests;

public static class TestAppBuilder
{
    // Inspector layout depends on real text metrics, particularly where auto-sized
    // columns overflow a narrow property pane. Keep the window platform headless.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
