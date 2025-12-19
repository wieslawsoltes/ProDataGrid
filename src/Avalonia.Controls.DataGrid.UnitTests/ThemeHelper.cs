using System;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;

namespace Avalonia.Controls.DataGridTests;

/// <summary>
/// Enum representing the available DataGrid themes for testing.
/// </summary>
public enum DataGridTheme
{
    /// <summary>Simple theme (v1) - uses Simple.xaml</summary>
    Simple,
    /// <summary>Simple theme v2 - uses Simple.v2.xaml with ScrollViewer-based template</summary>
    SimpleV2,
    /// <summary>Fluent theme (v1) - uses Fluent.xaml</summary>
    Fluent,
    /// <summary>Fluent theme v2 - uses Fluent.v2.xaml with ScrollViewer-based template (same as sample app)</summary>
    FluentV2
}

public static class ThemeHelper
{
    public static Styles GetThemeStyles(DataGridTheme theme)
    {
        var styles = new Styles();

        var baseUri = new Uri("avares://Avalonia.Controls.DataGrid/Themes/");

        switch (theme)
        {
            case DataGridTheme.Simple:
                styles.Add(new SimpleTheme());
                styles.Add(new StyleInclude(baseUri)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                });
                break;
            case DataGridTheme.SimpleV2:
                styles.Add(new SimpleTheme());
                styles.Add(new StyleInclude(baseUri)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml")
                });
                break;
            case DataGridTheme.Fluent:
                styles.Add(new FluentTheme());
                styles.Add(new StyleInclude(baseUri)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
                });
                break;
            case DataGridTheme.FluentV2:
                styles.Add(new FluentTheme());
                styles.Add(new StyleInclude(baseUri)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml")
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
        }

        return styles;
    }

    public static void SetThemeStyles(this Window window, DataGridTheme theme = DataGridTheme.Simple)
    {
        foreach (var style in GetThemeStyles(theme))
        {
            window.Styles.Add(style);
        }
    }
}
