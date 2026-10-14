using Avalonia;
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

public sealed class LifecycleTests
{
    [AvaloniaFact]
    public void New_snapshot_clears_stationary_tooltip_and_reacquires_fresh_values()
    {
        MutableSource source = new();
        using ChartModel model = new() { DataSource = source };
        ProChart chart = new() { Model = model };
        using Host host = new(chart);
        host.HoverPoint();
        Assert.True(chart.IsToolTipVisible);
        source.Replace(200);
        Assert.Null(chart.HoverPoint);
        Assert.False(chart.IsToolTipVisible);
        host.Flush();
        host.HoverPoint();
        Assert.NotNull(chart.HoverPoint);
        Assert.True(chart.HoverPoint.Hit.Value >= 200);
    }

    [AvaloniaTheory]
    [InlineData("loading")]
    [InlineData("error")]
    [InlineData("disabled")]
    [InlineData("tooltip")]
    public void State_transitions_clear_hover_without_waiting_for_a_mouse_move(string state)
    {
        using ChartGalleryViewModel vm = new(action => action(), () => { });
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        host.HoverPoint();
        Assert.NotNull(chart.HoverPoint);
        switch (state)
        {
            case "loading": chart.IsLoading = true; break;
            case "error": chart.ErrorMessage = "Offline"; break;
            case "disabled": chart.IsEnabled = false; break;
            default: chart.ShowToolTips = false; break;
        }
        Assert.Null(chart.HoverPoint);
        Assert.False(chart.IsToolTipVisible);
    }

    [AvaloniaFact]
    public void Visibility_and_axis_changes_invalidate_the_hit_context()
    {
        using ChartGalleryViewModel vm = new(action => action(), () => { });
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        host.HoverPoint();
        int index = chart.HoverPoint!.Hit.SeriesIndex;
        chart.LegendItems[index].IsVisible = false;
        Assert.Null(chart.HoverPoint);
        host.Flush(); host.HoverPoint();
        vm.Model.SecondaryValueAxis.Title = "Changed axis";
        Assert.Null(chart.HoverPoint);
    }

    [AvaloniaFact]
    public void Legend_none_is_respected_without_mutating_the_models_visibility_flag()
    {
        using ChartGalleryViewModel vm = new(action => action(), () => { });
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        ChartLegendPosition original = vm.Model.Legend.Position;
        Assert.True(chart.IsLegendVisible);
        vm.Model.Legend.Position = ChartLegendPosition.None;
        Assert.False(chart.IsLegendVisible);
        Assert.True(vm.Model.Legend.IsVisible);
        vm.Model.Legend.Position = original;
        Assert.True(chart.IsLegendVisible);
    }

    [AvaloniaFact]
    public void Detaching_and_reattaching_does_not_duplicate_point_events()
    {
        using ChartGalleryViewModel vm = new(action => action(), () => { });
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        int invoked = 0;
        chart.PointInvoked += (_, _) => invoked++;
        for (int i = 0; i < 6; i++)
        {
            Point point = host.HoverPoint();
            host.Window.MouseDown(point, MouseButton.Left);
            host.Window.MouseUp(point, MouseButton.Left);
            Assert.Equal(i + 1, invoked);
            host.Window.Content = null;
            Assert.Null(chart.HoverPoint);
            host.Window.Content = chart;
            host.Flush();
        }
    }

    [AvaloniaFact]
    public async Task Concurrent_export_is_ignored_and_detach_cancels_the_pending_save()
    {
        using ChartGalleryViewModel vm = new(action => action(), () => { });
        BlockingExport service = new();
        ProChart chart = new() { Model = vm.Model, ExportService = service };
        using Host host = new(chart);
        Task saving = chart.SaveAsync(ChartExportFormat.Csv);
        Assert.True(chart.IsExporting);
        Assert.False(chart.ExportCsvCommand.CanExecute(null));
        await chart.SaveAsync(ChartExportFormat.Csv);
        Assert.Equal(1, service.Calls);
        host.Window.Content = null;
        await saving;
        Assert.True(service.Token.IsCancellationRequested);
        Assert.False(chart.IsExporting);
        Assert.Null(chart.ExportError);
    }

    private sealed class BlockingExport : IChartExportService
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public async Task SaveAsync(ChartExportPayload payload, CancellationToken cancellationToken)
        {
            Calls++; Token = cancellationToken;
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class MutableSource : IChartDataSource
    {
        public event EventHandler? DataInvalidated;
        private ChartDataSnapshot _snapshot = Build(0);
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => _snapshot;
        public void Replace(double offset)
        { _snapshot = Build(offset); DataInvalidated?.Invoke(this, EventArgs.Empty); }
        private static ChartDataSnapshot Build(double offset) => new(
            Enumerable.Range(0, 20).Select(i => (string?)i.ToString()).ToArray(),
            new[] { new ChartSeriesSnapshot("Observations", ChartSeriesKind.Line,
                Enumerable.Range(0, 20).Select(i => (double?)(offset + 10 + i)).ToArray()) });
    }

    private sealed class Host : IDisposable
    {
        private readonly ProChart _chart;
        public Window Window { get; }
        public Host(ProChart chart)
        { _chart = chart; Window = new Window { Width = 1100, Height = 740, Content = chart }; Window.Show(); Flush(); }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public Point HoverPoint()
        {
            ProChartView plot = _chart.GetVisualDescendants().OfType<ProChartView>().Single();
            Window.MouseMove(new Point(1, 1));
            for (double x = 70; x < plot.Bounds.Width - 30; x += 11)
                for (double y = 20; y < plot.Bounds.Height - 30; y += 9)
                {
                    Point local = new(x, y);
                    if (!plot.HitTest(local).HasValue) continue;
                    Point point = plot.TranslatePoint(local, Window)!.Value;
                    Window.MouseMove(point);
                    if (_chart.HoverPoint != null) return point;
                }
            throw new InvalidOperationException("No rendered chart point accepted the simulated pointer.");
        }
        public void Dispose() => Window.Close();
    }
}
