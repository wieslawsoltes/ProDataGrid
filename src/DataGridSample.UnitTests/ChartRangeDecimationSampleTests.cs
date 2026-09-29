using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts;
using ProCharts.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartRangeDecimationSampleTests
{
    [Fact]
    public void Dense_Mode_Uses_Original_X_And_Toggles_Resolution_Without_Rebuilding_Input()
    {
        ChartRangeViewModel model = new();
        using var chart = model.Chart;
        Assert.Equal(96, chart.Snapshot.Categories.Count);
        model.LargeDataset = true;
        var source = Assert.IsType<RangeChartDataSource>(chart.DataSource);
        Assert.Equal(100000, source.Count); Assert.Equal(ChartAxisKind.Value, chart.CategoryAxis.Kind);
        ChartRangeView reduced = source.BuildView(chart.Request);
        Assert.Same(reduced.Snapshot, chart.Snapshot);
        Assert.InRange(reduced.SourcePointIndices.Count, 6, 800);
        for (int i = 0; i < reduced.SourcePointIndices.Count; i++)
            Assert.Equal(reduced.SourcePointIndices[i] + 1d, reduced.Snapshot.Series[0].XValues![i]);
        model.ReduceData = false;
        Assert.Same(source, chart.DataSource); Assert.Equal(100000, chart.Snapshot.Categories.Count);
        Assert.InRange(reduced.SourcePointIndices.Count, 6, 800);
        model.ReduceData = true;
        Assert.Same(source, chart.DataSource); Assert.InRange(chart.Snapshot.Categories.Count, 6, 800);
        chart.Request.WindowStart = 40000; chart.Request.WindowCount = 12000;
        var window = source.BuildView(chart.Request);
        Assert.Same(window.Snapshot, chart.Snapshot); Assert.Equal(40000, window.WindowStart); Assert.Equal(12000, window.WindowCount);
        Assert.Equal(40001, chart.Snapshot.Series[0].XValues![0]);
        model.LargeDataset = false;
        Assert.Equal(96, chart.Snapshot.Categories.Count); Assert.Equal(2, chart.Snapshot.Series.Count);
        Assert.Equal(ChartAxisKind.Category, chart.CategoryAxis.Kind);
    }

    [Fact]
    public void Gap_And_Width_Controls_Remain_Active_In_Dense_Mode()
    {
        ChartRangeViewModel model = new() { LargeDataset = true };
        using var chart = model.Chart;
        model.ReduceData = false;
        var old = chart.Snapshot;
        Assert.Null(old.Series[0].LowValues![32500]);
        model.ShowGaps = false;
        Assert.NotNull(chart.Snapshot.Series[0].LowValues![32500]); Assert.Null(old.Series[0].LowValues![32500]);
        model.BandWidth = 0;
        Assert.Equal(chart.Snapshot.Series[0].LowValues, chart.Snapshot.Series[0].HighValues);
        Assert.Contains("full-resolution", model.RenderingSummary);
    }

    [AvaloniaFact]
    public void Bound_Dense_View_Exports_Reduced_And_Full_Data_And_Maps_Pointer_Identity()
    {
        ChartRangePage page = new();
        Window window = new() { Width = 1100, Height = 720, Content = page };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var model = Assert.IsType<ChartRangeViewModel>(page.DataContext);
            model.LargeDataset = true;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var control = Assert.Single(page.GetLogicalDescendants().OfType<ProChartView>());
            var source = Assert.IsType<RangeChartDataSource>(model.Chart.DataSource);
            ChartRangeView reduced = source.BuildView(model.Chart.Request);
            Assert.Same(reduced.Snapshot, control.ChartModel!.Snapshot);
            bool found = false;
            for (int y = 15; y < control.Bounds.Height && !found; y += 3)
                for (int x = 30; x < control.Bounds.Width; x += 3)
                    if (control.HitTest(new Point(x, y)) is { } hit)
                    {
                        Assert.Equal(ChartSeriesKind.RangeArea, hit.SeriesKind);
                        Assert.Equal(reduced.SourcePointIndices[hit.PointIndex] + 1d, hit.XValue);
                        found = true; break;
                    }
            Assert.True(found);
            byte[] reducedPng = control.ExportPng(); string reducedSvg = control.ExportSvg();
            Assert.NotEmpty(reducedPng); Assert.Contains("Dense Bollinger envelope", reducedSvg);
            model.ReduceData = false;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            byte[] fullPng = control.ExportPng();
            Assert.NotEmpty(fullPng); Assert.Equal(100000, model.Chart.Snapshot.Categories.Count);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "sample", "gallery");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "DenseRangeReduced.png"), reducedPng);
                File.WriteAllBytes(Path.Combine(directory, "DenseRangeFull.png"), fullPng);
                File.WriteAllText(Path.Combine(directory, "DenseRangeReduced.svg"), reducedSvg);
            }
        }
        finally { window.Close(); }
    }
}
