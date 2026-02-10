using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class FilteringModelSamplePageTests
{
    [AvaloniaFact]
    public void ApplyingCustomerFilter_ReducesVisibleRows()
    {
        var page = new FilteringModelSamplePage();
        var viewModel = Assert.IsType<FilteringModelSampleViewModel>(page.DataContext);
        var window = CreateHostWindow(page);

        try
        {
            window.Show();
            PumpLayout(window);

            var beforeCount = viewModel.View.Count;
            viewModel.CustomerFilter.Text = "Contoso";
            viewModel.CustomerFilter.ApplyCommand.Execute(null);
            PumpLayout(window);

            Assert.Single(viewModel.FilteringModel.Descriptors);
            Assert.Equal(nameof(FilteringModelSampleViewModel.Order.Customer), viewModel.FilteringModel.Descriptors[0].PropertyPath);
            Assert.True(viewModel.View.Count < beforeCount);
            Assert.True(viewModel.View.Count > 0);
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
}
