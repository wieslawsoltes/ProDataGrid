using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts.Avalonia;
using SkiaSharp;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartingPageInitializationTests
{
    private static readonly SKColor DarkChartBackground = new(18, 25, 38);

    [AvaloniaFact]
    public void DefaultSampleKind_CreatesViewModel_WhenAttached()
    {
        var page = new ChartingPage();
        Assert.Null(page.DataContext);

        var window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);
        }
        finally
        {
            window.Close();
        }

        var viewModel = Assert.IsType<ChartSampleViewModel>(page.DataContext);
        Assert.Equal(ChartSampleKind.Line, viewModel.Kind);
    }

    [AvaloniaFact]
    public void SampleKindSetBeforeAttach_CreatesViewModelOnAttach_WithRequestedKind()
    {
        var page = new ChartingPage
        {
            SampleKind = ChartSampleKind.Pie
        };

        Assert.Null(page.DataContext);

        var window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);
        }
        finally
        {
            window.Close();
        }

        var viewModelAfterAttach = Assert.IsType<ChartSampleViewModel>(page.DataContext);
        Assert.Equal(ChartSampleKind.Pie, viewModelAfterAttach.Kind);
    }

    [AvaloniaFact]
    public void FinancialSampleKindSetBeforeAttach_CreatesViewModelOnAttach_WithRequestedKind()
    {
        var page = new ChartingPage
        {
            SampleKind = ChartSampleKind.Candlestick
        };

        Assert.Null(page.DataContext);

        var window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);
        }
        finally
        {
            window.Close();
        }

        var viewModelAfterAttach = Assert.IsType<ChartSampleViewModel>(page.DataContext);
        Assert.Equal(ChartSampleKind.Candlestick, viewModelAfterAttach.Kind);
    }

    [AvaloniaFact]
    public void DerivedFinancialSampleKindSetBeforeAttach_CreatesViewModelOnAttach_WithRequestedKind()
    {
        var page = new ChartingPage
        {
            SampleKind = ChartSampleKind.PointFigure
        };

        Assert.Null(page.DataContext);

        var window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);
        }
        finally
        {
            window.Close();
        }

        var viewModelAfterAttach = Assert.IsType<ChartSampleViewModel>(page.DataContext);
        Assert.Equal(ChartSampleKind.PointFigure, viewModelAfterAttach.Kind);
    }

    [AvaloniaFact]
    public void FinancialSamplePage_Uses_Dark_Chart_Surface_When_WindowTheme_Is_Dark()
    {
        var page = new ChartingPage
        {
            SampleKind = ChartSampleKind.Candlestick
        };

        var window = CreateHostWindow(page);
        window.RequestedThemeVariant = ThemeVariant.Dark;

        try
        {
            window.Show();
            PumpLayout(window);

            var chartView = window.GetLogicalDescendants().OfType<ProChartView>().First();
            var pixel = CapturePixel(chartView, 12, 12);
            Assert.Equal(DarkChartBackground, pixel);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateHostWindow(Control content)
    {
        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = content
        };
        window.ApplySampleTheme();
        return window;
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static SKColor CapturePixel(ProChartView chartView, int x, int y)
    {
        var png = chartView.ExportPng();
        Assert.NotEmpty(png);

        using var stream = new MemoryStream(png);
        using var bitmap = SKBitmap.Decode(stream);
        Assert.NotNull(bitmap);

        return bitmap!.GetPixel(x, y);
    }
}
