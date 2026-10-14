using Avalonia;
using ReactiveUI.Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts.Avalonia;
using ProCharts.Sample;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(ProCharts.Controls.Tests.TestApplicationBuilder))]
namespace ProCharts.Controls.Tests;

public sealed class TestApplicationBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ControlTestApp>().UseSkia().WithInterFont().UseReactiveUI(static _ => { })
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class ControlTests
{
    [AvaloniaFact]
    public void Default_template_is_composed_of_native_replaceable_parts()
    {
        using ChartGalleryViewModel vm = ViewModel();
        object dataContext = new();
        ProChart chart = new() { Model = vm.Model, DataContext = dataContext, ChartStyle = vm.ChartStyle };
        using Host host = new(chart);
        Assert.Same(dataContext, chart.DataContext);
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartView>());
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartLegend>());
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartToolbar>());
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartNavigator>());
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartToolTip>());
        Assert.False(chart.GetVisualDescendants().OfType<ProChartView>().Single().ShowLegend);
        Assert.Equal(3, chart.LegendItems.Count);
        Assert.True(chart.HasData); Assert.False(chart.IsEmpty);
        Assert.All(chart.LegendItems, item => Assert.True(item.CanToggle));
    }
    [AvaloniaFact]
    public void Legend_hides_all_and_restores_without_losing_items()
    {
        using ChartGalleryViewModel vm = ViewModel();
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        foreach (ChartLegendItem item in chart.LegendItems) item.IsVisible = false;
        Assert.Empty(vm.Model.Snapshot.Series); Assert.True(chart.IsEmpty); Assert.Equal(3, chart.LegendItems.Count);
        chart.LegendItems[1].IsVisible = true;
        Assert.Single(vm.Model.Snapshot.Series); Assert.True(chart.HasData); Assert.False(chart.IsEmpty);
        Assert.False(chart.LegendItems[0].IsVisible); Assert.True(chart.LegendItems[1].IsVisible);
    }
    [AvaloniaFact]
    public void Retemplating_keeps_model_and_commands_but_replaces_surface()
    {
        using ChartGalleryViewModel vm = ViewModel();
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        ProChartView old = chart.GetVisualDescendants().OfType<ProChartView>().Single();
        chart.ZoomInCommand.Execute(null);
        vm.Model.TryGetVisibleWindow(out _, out int start, out int count);
        Assert.True(Application.Current!.Resources.TryGetResource("ProChartCompact", chart.ActualThemeVariant, out object? resource));
        chart.Theme = (ControlTheme)resource!; host.Flush();
        Assert.NotSame(old, chart.GetVisualDescendants().OfType<ProChartView>().Single());
        Assert.Same(vm.Model, chart.Model);
        vm.Model.TryGetVisibleWindow(out _, out int newStart, out int newCount);
        Assert.Equal(start, newStart); Assert.Equal(count, newCount);
        Assert.True(chart.ZoomOutCommand.CanExecute(null));
        chart.Theme = null; host.Flush();
        Assert.Single(chart.GetVisualDescendants().OfType<ProChartNavigator>());
    }
    [AvaloniaFact]
    public void Missing_optional_parts_do_not_crash_and_image_export_has_a_clear_diagnostic()
    {
        ProChart chart = new() { Template = new FuncControlTemplate<ProChart>((_, _) => new Border()) };
        using Host host = new(chart);
        Assert.Empty(chart.GetVisualDescendants().OfType<ProChartView>());
        Assert.Throws<InvalidOperationException>(() => chart.CreateExport(ChartExportFormat.Png));
        Assert.NotEmpty(chart.CreateExport(ChartExportFormat.Csv).Data);
        Assert.False(chart.ExportPngCommand.CanExecute(null));
    }
    [AvaloniaFact]
    public void Commands_zoom_reset_and_preserve_undo_redo_history()
    {
        using ChartGalleryViewModel vm = ViewModel();
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        vm.Model.TryGetVisibleWindow(out int total, out _, out int before);
        chart.ZoomInCommand.Execute(null); vm.Model.TryGetVisibleWindow(out _, out _, out int after);
        Assert.True(after < before); Assert.True(chart.UndoViewCommand.CanExecute(null));
        chart.UndoViewCommand.Execute(null); vm.Model.TryGetVisibleWindow(out _, out _, out int restored);
        Assert.Equal(before, restored); Assert.True(chart.RedoViewCommand.CanExecute(null));
        chart.RedoViewCommand.Execute(null); vm.Model.TryGetVisibleWindow(out _, out _, out int redone);
        Assert.Equal(after, redone);
        chart.ResetViewCommand.Execute(null); vm.Model.TryGetVisibleWindow(out _, out _, out int all);
        Assert.Equal(total, all);
    }
    [AvaloniaFact]
    public void Navigator_obeys_bounds_and_has_no_feedback_loop()
    {
        using ChartGalleryViewModel vm = ViewModel();
        vm.Model.SetVisibleWindow(3, 10);
        ProChartNavigator navigator = new() { Model = vm.Model };
        using Host host = new(navigator);
        Assert.Equal(38, navigator.Maximum); Assert.True(navigator.CanNavigate);
        navigator.Position = 999;
        vm.Model.TryGetVisibleWindow(out _, out int start, out int count);
        Assert.Equal(38, start); Assert.Equal(10, count);
        vm.Model.ResetWindow(); Assert.False(navigator.CanNavigate); Assert.Equal(0, navigator.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => navigator.Position = double.NaN);
    }
    [AvaloniaFact]
    public void Loading_error_and_empty_states_disable_commands()
    {
        using ChartGalleryViewModel vm = ViewModel();
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        chart.IsLoading = true; host.Flush();
        Assert.False(chart.ZoomInCommand.CanExecute(null)); Assert.False(chart.IsEmpty);
        Assert.False(chart.GetVisualDescendants().OfType<ProChartView>().Single().IsEffectivelyEnabled);
        chart.IsLoading = false; chart.ErrorMessage = "Provider error";
        host.Flush();
        Assert.True(chart.HasError); Assert.False(chart.ExportCsvCommand.CanExecute(null));
        Assert.False(chart.GetVisualDescendants().OfType<ProChartView>().Single().IsEffectivelyEnabled);
        chart.ErrorMessage = null; Assert.True(chart.ZoomInCommand.CanExecute(null));
        chart.Model = null; Assert.True(chart.IsEmpty); Assert.False(chart.HasData);
    }
    [AvaloniaFact]
    public void Model_replacement_and_detach_do_not_dispose_caller_models()
    {
        TestSource source = new();
        using ChartModel first = new() { DataSource = source };
        using ChartModel second = new();
        ProChart chart = new() { Model = first };
        using Host host = new(chart);
        chart.Model = second; source.Invalidate(); Assert.True(chart.IsEmpty);
        host.Window.Content = null;
        first.Refresh(); second.DataSource = source; second.Refresh();
        Assert.False(source.Disposed);
        host.Window.Content = chart; host.Flush(); Assert.True(chart.HasData);
    }
    [AvaloniaFact]
    public async Task Export_produces_real_images_and_errors_are_observed()
    {
        using ChartGalleryViewModel vm = ViewModel();
        ProChart chart = new() { Model = vm.Model };
        using Host host = new(chart);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, chart.CreateExport(ChartExportFormat.Png).Data.Take(8));
        Assert.Contains("<svg", System.Text.Encoding.UTF8.GetString(chart.CreateExport(ChartExportFormat.Svg).Data));
        Assert.Contains("Series,Point", System.Text.Encoding.UTF8.GetString(chart.CreateExport(ChartExportFormat.Csv).Data));
        chart.ExportService = new FailingExport();
        await chart.SaveAsync(ChartExportFormat.Csv);
        Assert.Equal("Simulated save failure", chart.ExportError); Assert.False(chart.IsExporting);
        chart.ExportService = new CancelledExport();
        await chart.SaveAsync(ChartExportFormat.Csv);
        Assert.Null(chart.ExportError); Assert.False(chart.IsExporting);
    }
    [AvaloniaFact]
    public void Control_instances_do_not_share_collections_and_validate_settings()
    {
        ProChart first = new(), second = new();
        Assert.NotSame(first.LegendItems, second.LegendItems);
        Assert.NotSame(first.ZoomInCommand, second.ZoomInCommand);
        Assert.Throws<ArgumentException>(() => first.ZoomStep = double.NaN);
        Assert.Throws<ArgumentException>(() => first.MinWindowCount = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.PointerTool = (ChartPointerTool)999);
    }
    [AvaloniaFact]
    public void Gallery_all_kinds_have_data_and_stream_retains_a_bounded_window()
    {
        using ChartGalleryViewModel vm = ViewModel();
        foreach (ChartSeriesKind kind in vm.ChartKinds)
        {
            vm.SelectedKind = kind;
            Assert.NotEmpty(vm.Model.Snapshot.Series);
            Assert.NotEmpty(vm.Model.Snapshot.Series[0].Values);
        }
        vm.SelectedExample = vm.Examples.Single(x => x.Id == "stream");
        Assert.Equal(100000, vm.ReceivedRows);
        ((System.Windows.Input.ICommand)vm.AppendCommand).Execute(null);
        Assert.Equal(100032, vm.ReceivedRows);
        Assert.Equal(100000, ((IChartWindowInfoProvider)vm.Model.DataSource!).GetTotalCategoryCount());
        Assert.All(vm.Model.Snapshot.Series, series => Assert.True(series.Values.Count <= 1200));
    }
    private static ChartGalleryViewModel ViewModel() => new(action => action(), () => { });
    private sealed class FailingExport : IChartExportService
    { public Task SaveAsync(ChartExportPayload p, CancellationToken c) => throw new IOException("Simulated save failure"); }
    private sealed class CancelledExport : IChartExportService
    { public Task SaveAsync(ChartExportPayload p, CancellationToken c) => Task.FromCanceled(new CancellationToken(true)); }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(Control content) { Window = new Window { Width = 1100, Height = 740, Content = content }; Window.Show(); Flush(); }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
