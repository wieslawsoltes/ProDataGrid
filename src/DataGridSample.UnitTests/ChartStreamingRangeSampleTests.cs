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

public sealed class ChartStreamingRangeSampleTests
{
    [Fact]
    public void Commands_Retain_Bounded_History_And_Publish_One_Snapshot_Per_Batch()
    {
        ChartStreamingRangeViewModel model = new();
        using var chart = model.Chart;
        int changes = 0;
        chart.SnapshotChanged += (_, _) => changes++;
        var old = model.Source.BuildView(chart.Request);
        for (int i = 0; i < 40; i++) model.AppendCommand.Execute(null);
        Assert.Equal(40, changes);
        Assert.Equal(3584, model.Source.TotalSamples);
        Assert.Equal(2048, model.Source.Count);
        Assert.Equal(1536, model.Source.FirstRetainedSampleIndex);
        Assert.Equal(1024, old.TotalSamples);
        Assert.True(chart.Snapshot.Categories.Count < 2048);
        Assert.Contains(model.Source.TotalSamples.ToString("N0"), model.RenderingSummary);
        changes = 0; model.ReduceData = false;
        Assert.Equal(1, changes); Assert.Equal(2048, chart.Snapshot.Categories.Count);
        changes = 0; model.ResetCommand.Execute(null);
        Assert.Equal(1, changes); Assert.Equal(1024, model.Source.TotalSamples);
        Assert.Equal(0, model.Source.FirstRetainedSampleIndex);
    }

    [AvaloniaFact]
    public void Existing_Range_Page_Exposes_The_Bound_Live_Demo()
    {
        ChartRangePage page = new();
        Button button = Assert.Single(page.GetLogicalDescendants().OfType<Button>(),
            b => Equals(b.Content, "Open bounded live interval demo"));
        Flyout flyout = Assert.IsType<Flyout>(button.Flyout);
        var live = Assert.IsType<ChartStreamingRangePage>(flyout.Content);
        var model = Assert.IsType<ChartStreamingRangeViewModel>(live.DataContext);
        Assert.Equal(1024, model.Source.TotalSamples);
    }

    [AvaloniaFact]
    public void Bound_Live_Sample_Routes_Append_Click_And_Exports_Actual_Retained_Intervals()
    {
        ChartStreamingRangePage page = new();
        Window window = new() { Width = 960, Height = 600, Content = page };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var model = Assert.IsType<ChartStreamingRangeViewModel>(page.DataContext);
            var view = Assert.Single(page.GetLogicalDescendants().OfType<ProChartView>());
            Assert.Same(model.Chart, view.ChartModel);
            Button button = page.FindControl<Button>("AppendBatchButton")!;
            Point target = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(target, MouseButton.Left); window.MouseUp(target, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1088, model.Source.TotalSamples);
            for (int i = 0; i < 40; i++) model.AppendCommand.Execute(null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            byte[] png = view.ExportPng(); string svg = view.ExportSvg();
            Assert.NotEmpty(png); Assert.Contains("Live interval envelope", svg);
            var data = model.Source.BuildView(model.Chart.Request);
            Assert.True(data.FirstRetainedSampleIndex > 0);
            bool found = false;
            for (int y = 20; y < view.Bounds.Height && !found; y += 9)
                for (int x = 20; x < view.Bounds.Width; x += 9)
                    if (view.HitTest(new Point(x, y)) is { SeriesKind: ChartSeriesKind.RangeArea } hit)
                    {
                        Assert.Equal((double)data.SourceSampleIndices[hit.PointIndex], hit.XValue);
                        Assert.Equal(data.Snapshot.Series[0].LowValues![hit.PointIndex], hit.LowValue);
                        found = true; break;
                    }
            Assert.True(found);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "sample", "gallery");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "StreamingRange.png"), png);
                File.WriteAllText(Path.Combine(directory, "StreamingRange.svg"), svg);
            }
        }
        finally { window.Close(); }
    }
}
