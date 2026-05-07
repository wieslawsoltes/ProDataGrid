using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class DynamicDataStreamingSourceCachePageTests
{
    [AvaloniaFact]
    public void Replacement_Update_Under_Sort_Preserves_Row_Selection()
    {
        var page = new DynamicDataStreamingSourceCachePage();
        var window = CreateHostWindow(page);

        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<DynamicDataStreamingSourceCacheViewModel>(page.DataContext);

            viewModel.LoadSelectionReproCommand.Execute(null);
            PumpLayout(window);

            viewModel.SelectMiddleRowCommand.Execute(null);
            PumpLayout(window);

            Assert.Equal(2, viewModel.SelectedId);

            viewModel.ReplaceSelectedAndMoveCommand.Execute(null);
            PumpLayout(window);

            Assert.Equal(2, viewModel.ExpectedSelectedId);
            Assert.Equal(2, viewModel.SelectedId);
            Assert.Contains("Selection stayed", viewModel.SelectionStatus);
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
            Width = 1200,
            Height = 800,
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
