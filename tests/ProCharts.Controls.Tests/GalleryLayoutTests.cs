using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts.Avalonia;
using ProCharts.Sample;
using Xunit;

namespace ProCharts.Controls.Tests;

public sealed class GalleryLayoutTests
{
    [AvaloniaFact]
    public void Derived_gallery_view_hides_sidebar_at_narrow_widths_and_restores_it()
    {
        using var vm = new ChartGalleryViewModel(action => action(), () => { });
        var view = new MainView { DataContext = vm };
        var window = new Window { Width = 1440, Height = 1000, Content = view };
        try
        {
            window.Show();
            void Layout() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
            Layout();
            var sidebar = view.FindControl<Border>("Sidebar")!;
            Assert.True(sidebar.IsVisible);
            foreach (int width in new[] { 800, 420 })
            {
                window.Width = width;
                Layout();
                Assert.Contains("narrow", view.Classes);
                Assert.False(sidebar.IsVisible);
                var plot = view.GetVisualDescendants().OfType<ProChartView>().Single();
                Assert.True(plot.Bounds.Width > 200);
                Assert.True(plot.Bounds.Height >= 120);
                var position = plot.TranslatePoint(default, view)!.Value;
                Assert.True(position.X + plot.Bounds.Width <= view.Bounds.Width + 1);
            }
            window.Width = 1440;
            Layout();
            Assert.DoesNotContain("narrow", view.Classes);
            Assert.True(sidebar.IsVisible);
        }
        finally { window.Close(); }
    }
}
