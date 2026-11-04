using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace DataGridSample.Tests;

internal static class SampleThemeHelper
{
    private static readonly Uri DataGridThemeBaseUri = new("avares://Avalonia.Controls.DataGrid/Themes/");
    private static readonly Uri DataGridFluentThemeUri = new("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml");
    private static readonly Uri DataGridOptimizedThemeUri = new("avares://Avalonia.Controls.DataGrid/Themes/Optimized.xaml");
    private static readonly Uri DataGridFlatThemeUri = new("avares://Avalonia.Controls.DataGrid/Themes/Flat.xaml");
    private static readonly Uri MarketDashboardThemeBaseUri = new("avares://ProDataGrid.MarketDashboardSample/Styles/");
    private static readonly Uri MarketDashboardThemeUri = new("avares://ProDataGrid.MarketDashboardSample/Styles/MarketDashboardStyles.axaml");

    public static void ApplySampleTheme(this Window window)
    {
        window.Styles.Add(new FluentTheme());
        window.Styles.Add(new StyleInclude(DataGridThemeBaseUri)
        {
            Source = DataGridFluentThemeUri
        });
        window.Styles.Add(new StyleInclude(DataGridThemeBaseUri)
        {
            Source = DataGridOptimizedThemeUri
        });
        window.Styles.Add(new StyleInclude(DataGridThemeBaseUri)
        {
            Source = DataGridFlatThemeUri
        });
    }

    public static void ApplyMarketDashboardTheme(this Window window)
    {
        window.ApplySampleTheme();
        window.Styles.Add(new StyleInclude(MarketDashboardThemeBaseUri)
        {
            Source = MarketDashboardThemeUri
        });
    }
}
