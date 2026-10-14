using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Xunit;

[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
namespace ProCharts.Controls.Tests;

// Do not start the sample application's timer or desktop/browser composition root in unit tests.
public sealed class ControlTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://ProCharts.Controls.Tests/"))
        { Source = new Uri("avares://ProCharts.Controls/Themes/Generic.axaml") });
    }
}
