using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class CustomDrawingLiveUpdatesPageInitializationTests
{
    [AvaloniaFact]
    public void CustomDrawingLiveUpdatesPage_Attach_Initializes_ViewModel_And_Rows()
    {
        var page = new CustomDrawingLiveUpdatesPage();
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

        var viewModel = Assert.IsType<CustomDrawingLiveUpdatesViewModel>(page.DataContext);
        Assert.NotEmpty(viewModel.Rows);
        Assert.True(viewModel.FrameCount >= 0);
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
