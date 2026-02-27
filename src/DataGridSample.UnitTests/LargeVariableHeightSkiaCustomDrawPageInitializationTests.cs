using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class LargeVariableHeightSkiaCustomDrawPageInitializationTests
{
    [AvaloniaFact]
    public void LargeVariableHeightSkiaCustomDrawPage_GeneratesItems_WhenAttached()
    {
        var page = new global::DataGridSample.LargeVariableHeightSkiaCustomDrawPage();
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

        var viewModel = Assert.IsType<LargeVariableHeightViewModel>(page.DataContext);
        Assert.NotEmpty(viewModel.Items);
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
}
