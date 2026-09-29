using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts;
using ProCharts.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartRangeSampleTests
{
    [Fact]
    public void Band_Width_Gaps_And_Windowing_Update_Aligned_Intervals()
    {
        ChartRangeViewModel model = new();
        using var chart = model.Chart;
        Assert.Equal(ChartSeriesKind.RangeArea, chart.Snapshot.Series[0].Kind);
        Assert.Null(chart.Snapshot.Series[0].LowValues![32]);
        var old = chart.Snapshot;
        model.ShowGaps = false;
        Assert.NotNull(chart.Snapshot.Series[0].LowValues![32]);
        Assert.Null(old.Series[0].LowValues![32]);
        model.BandWidth = 0;
        Assert.Equal(chart.Snapshot.Series[0].LowValues, chart.Snapshot.Series[0].HighValues);
        chart.Request.WindowStart = 20; chart.Request.WindowCount = 12;
        Assert.Equal(12, chart.Snapshot.Categories.Count);
        Assert.Equal("21", chart.Snapshot.Categories[0]);
        Assert.Equal(12, chart.Snapshot.Series[0].LowValues!.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.BandWidth = double.NaN);
    }

    [AvaloniaFact]
    public void Bound_Sample_Exports_Filled_Bands_And_Host_Tooltip_Shows_Both_Boundaries()
    {
        ChartRangePage page = new();
        Window window = new() { Width = 960, Height = 600, Content = page };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var model = Assert.IsType<ChartRangeViewModel>(page.DataContext);
            var view = Assert.Single(page.GetLogicalDescendants().OfType<ProChartView>());
            Assert.Same(model.Chart, view.ChartModel);
            Assert.True(view.EnableHoverTracking);
            Assert.True(view.ShowToolTips);
            byte[] png = view.ExportPng(); string svg = view.ExportSvg();
            Assert.NotEmpty(png); Assert.Contains("Bollinger envelope", svg);
            Point? target = null;
            for (int y = 20; y < view.Bounds.Height && !target.HasValue; y += 7)
                for (int x = 20; x < view.Bounds.Width; x += 7)
                    if (view.HitTest(new Point(x, y)) is { SeriesKind: ChartSeriesKind.RangeArea })
                    { target = new Point(x, y); break; }
            Assert.NotNull(target);
            // PointerEventArgs stores presentation-root coordinates, not the nested chart's local
            // coordinates. Check the round trip before testing the control's routed handler.
            Point? rootPoint = view.TranslatePoint(target!.Value, window);
            Assert.NotNull(rootPoint);
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var moved = new PointerEventArgs(InputElement.PointerMovedEvent, view, pointer, window,
                rootPoint!.Value, 1, new PointerPointProperties(), KeyModifiers.None);
            Point local = moved.GetPosition(view);
            Assert.InRange(Math.Abs(local.X - target.Value.X), 0, 1e-6);
            Assert.InRange(Math.Abs(local.Y - target.Value.Y), 0, 1e-6);
            view.RaiseEvent(moved);
            string tooltip = Assert.IsType<string>(ToolTip.GetTip(view));
            Assert.Contains("Low", tooltip); Assert.Contains("High", tooltip);
            // Exercise the platform input route as well, with normal crosshair tracking enabled.
            window.MouseMove(rootPoint.Value);
            string routedTooltip = Assert.IsType<string>(ToolTip.GetTip(view));
            Assert.Contains("Low", routedTooltip); Assert.Contains("High", routedTooltip);
            Assert.True(model.Chart.Interaction.IsCrosshairVisible);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "sample", "gallery"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "BollingerRangeArea.png"), png);
                File.WriteAllText(Path.Combine(directory, "BollingerRangeArea.svg"), svg);
            }
        }
        finally { window.Close(); }
    }
}
