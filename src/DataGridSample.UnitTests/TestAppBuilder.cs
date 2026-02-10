using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(DataGridSample.Tests.UnitTestAppBuilder))]

namespace DataGridSample.Tests;

internal static class UnitTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var options = new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = true
        };

        return AppBuilder.Configure<UnitTestApp>()
            .UseHeadless(options);
    }
}

internal sealed class UnitTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml")
        });
    }
}
