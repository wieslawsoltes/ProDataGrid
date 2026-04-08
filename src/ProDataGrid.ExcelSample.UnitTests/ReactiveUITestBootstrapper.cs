using System.Runtime.CompilerServices;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

namespace ProDataGrid.ExcelSample.Tests;

internal static class ReactiveUITestBootstrapper
{
    [ModuleInitializer]
    public static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithMainThreadScheduler(AvaloniaScheduler.Instance, setRxApp: true)
            .WithCoreServices()
            .BuildApp();
    }
}
