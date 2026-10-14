using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using ProCharts.Controls;

namespace ProCharts.Sample;

/// <summary>The shared composition root; platform hosts only provide their lifetime and export destination.</summary>
public sealed partial class App : Application
{
    public ChartGalleryViewModel? Gallery { get; private set; }
    public MainView? View { get; private set; }
    public IChartExportService? ExportService { get; set; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        Gallery = new ChartGalleryViewModel(action => Dispatcher.UIThread.Post(action),
            () => RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark);
        View = new MainView { DataContext = Gallery };
        View.FindControl<ProChart>("Chart")!.ExportService = ExportService;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window { Title = "ProCharts — Avalonia Charting Studio", Width = 1440, Height = 960,
                MinWidth = 420, MinHeight = 600, Content = View };
            desktop.Exit += (_, _) => Gallery.Dispose();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single) single.MainView = View;
        base.OnFrameworkInitializationCompleted();
    }
}
