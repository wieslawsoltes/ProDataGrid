using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using ProCharts.Sample;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace ProCharts.Controls.Tests;

public sealed class PresentationTests
{
    [AvaloniaFact]
    public void Snapshot_colors_reach_the_raster_and_stay_stable_when_a_neighbor_is_hidden()
    {
        using var vm = new ChartGalleryViewModel(action => action(), () => { });
        var chart = new ProChart { Model = vm.Model, ChartStyle = vm.ChartStyle };
        var window = new Window { Width = 1000, Height = 700, Content = chart };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            SKColor purple = new(129, 140, 248), teal = new(45, 212, 191);
            Assert.Equal(purple, chart.EffectiveChartStyle.SeriesStyles![0].StrokeColor);
            Assert.Equal(teal, chart.EffectiveChartStyle.SeriesStyles![1].StrokeColor);
            Assert.Null(vm.Model.SeriesStyles);
            Assert.Null(vm.ChartStyle.SeriesStyles);
            using var image = SKBitmap.Decode(chart.CreateExport(ChartExportFormat.Png).Data);
            Assert.Contains(image.Pixels, pixel => pixel == purple);
            Assert.Contains(image.Pixels, pixel => pixel == teal);
            chart.LegendItems[0].IsVisible = false;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(teal, chart.EffectiveChartStyle.SeriesStyles![0].StrokeColor);
            var swatch = Assert.IsAssignableFrom<ISolidColorBrush>(chart.LegendItems[1].Swatch);
            Assert.Equal((uint)teal, swatch.Color.ToUInt32());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Explicit_style_overrides_do_not_mutate_snapshot_styles()
    {
        using var vm = new ChartGalleryViewModel(action => action(), () => { });
        var explicitStyle = new SkiaChartStyle
        { SeriesStyles = new[] { new SkiaChartSeriesStyle { StrokeColor = SKColors.Orange, StrokeWidth = 7 } } };
        var chart = new ProChart { Model = vm.Model, ChartStyle = explicitStyle };
        Assert.Equal(SKColors.Orange, chart.EffectiveChartStyle.SeriesStyles![0].StrokeColor);
        Assert.Equal(7, chart.EffectiveChartStyle.SeriesStyles![0].StrokeWidth);
        Assert.Equal(ChartColor.FromRgb(129, 140, 248), vm.Model.Snapshot.Series[0].Style!.StrokeColor);
        Assert.Null(explicitStyle.SeriesStyles![0].FillColor);
    }
}
