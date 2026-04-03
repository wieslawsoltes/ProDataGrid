using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ProDataGrid.MarketDashboardSample.Services;
using ProDataGrid.MarketDashboardSample.ViewModels;
using System.Net.Http;

namespace ProDataGrid.MarketDashboardSample;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton(new HttpClient());
            services.AddSingleton(new BinanceMarketDataOptions());
            services.AddSingleton<IMarketDashboardDataService, BinanceMarketDashboardDataService>();
            services.AddTransient<MarketDashboardViewModel>();
            _serviceProvider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MarketDashboardViewModel>()
            };

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
