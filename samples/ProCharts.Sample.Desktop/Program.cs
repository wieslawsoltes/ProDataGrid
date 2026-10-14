using Avalonia;
using ReactiveUI.Avalonia;
namespace ProCharts.Sample.Desktop;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args) => AppBuilder.Configure<App>().UseReactiveUI(static _ => { }).UsePlatformDetect().WithInterFont().LogToTrace().StartWithClassicDesktopLifetime(args);
}
