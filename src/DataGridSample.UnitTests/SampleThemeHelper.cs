using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace DataGridSample.Tests;

internal static class SampleThemeHelper
{
    private static readonly Uri DataGridThemeBaseUri = new("avares://Avalonia.Controls.DataGrid/Themes/");
    private static readonly Uri DataGridFluentThemeUri = new("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml");

    public static void ApplySampleTheme(this Window window)
    {
        window.Styles.Add(new FluentTheme());
        window.Styles.Add(new StyleInclude(DataGridThemeBaseUri)
        {
            Source = DataGridFluentThemeUri
        });
    }
}
