using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts.Avalonia;
using ProCharts.Sample;
using Xunit;

namespace ProCharts.Controls.Tests;

public sealed class SmallViewportTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Small_logical_viewport_keeps_export_toolbar_separate_from_plot(bool compact)
    {
        using var vm = new ChartGalleryViewModel(action => action(), () => { });
        var view = new MainView { DataContext = vm };
        var window = new Window { Width = 400, Height = 550, Content = view };
        try
        {
            window.Show(); Flush(window);
            vm.IsCompact = compact; Flush(window);
            var chart = view.FindControl<ProChart>("Chart")!;
            var plot = chart.GetVisualDescendants().OfType<ProChartView>().Single();
            var toolbar = chart.GetVisualDescendants().OfType<ProChartToolbar>().Single();
            var plotRect = RectOf(plot, view);
            Assert.All(toolbar.GetVisualDescendants().OfType<Button>(), button =>
                Assert.False(RectOf(button, view).Intersects(plotRect), "Toolbar hit target overlaps plot"));
            var png = toolbar.GetVisualDescendants().OfType<Button>().Single(x => AutomationProperties.GetAutomationId(x) == "chart-png");
            png.BringIntoView(); Flush(window);
            Point center = png.TranslatePoint(new Point(png.Bounds.Width / 2, png.Bounds.Height / 2), window)!.Value;
            Assert.InRange(center.X, 0, window.ClientSize.Width);
            Assert.InRange(center.Y, 0, window.ClientSize.Height);
            var service = new RecordingExport(); chart.ExportService = service;
            window.MouseDown(center, MouseButton.Left); window.MouseUp(center, MouseButton.Left);
            Assert.Equal(1, service.Calls);
            Assert.True(service.Bytes > 1000);
            Assert.Null(chart.ExportError);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Constrained_chart_clips_overflow_instead_of_putting_plot_over_toolbar()
    {
        using var vm = new ChartGalleryViewModel(action => action(), () => { });
        var chart = new ProChart { Model = vm.Model, Title = "A long chart heading", Width = 360, Height = 300 };
        var window = new Window { Width = 360, Height = 300, Content = chart };
        try
        {
            window.Show(); Flush(window);
            var plot = chart.GetVisualDescendants().OfType<ProChartView>().Single();
            var toolbar = chart.GetVisualDescendants().OfType<ProChartToolbar>().Single();
            Assert.All(toolbar.GetVisualDescendants().OfType<Button>(), button =>
                Assert.False(RectOf(button, chart).Intersects(RectOf(plot, chart))));
        }
        finally { window.Close(); }
    }

    private static Rect RectOf(Control control, Visual relativeTo) => new(control.TranslatePoint(default, relativeTo)!.Value, control.Bounds.Size);
    private static void Flush(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class RecordingExport : IChartExportService
    {
        public int Calls { get; private set; }
        public int Bytes { get; private set; }
        public Task SaveAsync(ChartExportPayload payload, CancellationToken cancellationToken)
        { Calls++; Bytes = payload.Data.Length; return Task.CompletedTask; }
    }
}
