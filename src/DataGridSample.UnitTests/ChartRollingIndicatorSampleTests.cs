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

public sealed class ChartRollingIndicatorSampleTests
{
    [Fact]
    public void Calculated_Bands_Process_Every_Price_Exactly_Once_Before_Display_Reduction()
    {
        ChartStreamingRangeViewModel model = new() { IncludeGaps = false };
        using var chart = model.Chart;
        int changes = 0; chart.SnapshotChanged += (_, _) => changes++;
        model.UseCalculatedBands = true;
        Assert.Equal(1, changes); Assert.Equal(1024, model.CalculatorSamplesProcessed);
        for (int i = 0; i < 40; i++) model.AppendCommand.Execute(null);
        Assert.Equal(41, changes); Assert.Equal(3584, model.CalculatorSamplesProcessed);
        Assert.Equal(3584, model.Source.TotalSamples); Assert.Equal(2048, model.Source.Count);
        double?[] prices = Enumerable.Range(0, 3584).Select(i => (double?)(100 + Math.Sin(i * 0.018) * 12 + Math.Cos(i * 0.063) * 2)).ToArray();
        ChartBandSeries expected = ChartIndicators.BollingerBands(new ChartSeriesSnapshot("Signal", ChartSeriesKind.Line, prices), 32);
        var full = model.Source.BuildView(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None });
        for (int i = 0; i < full.SourceSampleIndices.Count; i++)
        {
            int original = (int)full.SourceSampleIndices[i];
            Near(expected.Lower.Values[original], full.Snapshot.Series[0].LowValues![i]);
            Near(expected.Upper.Values[original], full.Snapshot.Series[0].HighValues![i]);
        }
        long processed = model.CalculatorSamplesProcessed;
        model.ReduceData = false; model.ReduceData = true;
        Assert.Equal(processed, model.CalculatorSamplesProcessed);
        Near(expected.Middle.Values[^1], model.CalculatedBand.Middle);
    }

    [Fact]
    public void Gaps_Width_And_Reset_Preserve_Warmup_And_Coalesce_Model_Refreshes()
    {
        ChartStreamingRangeViewModel model = new() { UseCalculatedBands = true };
        using var chart = model.Chart;
        var data = model.Source.BuildSnapshot(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None }).Series[0];
        Assert.All(data.LowValues!.Take(31), value => Assert.Null(value));
        Assert.NotNull(data.LowValues[31]); Assert.Null(data.LowValues[532]); Assert.NotNull(data.LowValues[533]);
        model.AppendCommand.Execute(null);
        data = model.Source.BuildSnapshot(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None }).Series[0];
        Assert.Null(data.LowValues![1086]); Assert.NotNull(data.LowValues[1087]);
        int changes = 0; chart.SnapshotChanged += (_, _) => changes++;
        model.StandardDeviations = 0;
        Assert.Equal(1, changes); Assert.Equal(1024, model.CalculatorSamplesProcessed);
        data = chart.Snapshot.Series[0]; Assert.Equal(data.LowValues, data.HighValues);
        changes = 0; model.ResetCommand.Execute(null); Assert.Equal(1, changes);
        Assert.Equal(1024, model.CalculatorSamplesProcessed);
        changes = 0; model.UseCalculatedBands = false; Assert.Equal(1, changes);
        Assert.Equal(0, model.CalculatorSamplesProcessed);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.StandardDeviations = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.StandardDeviations = -1);
    }

    [AvaloniaFact]
    public void Bound_Mode_Toggle_Routes_Input_And_Exports_Computed_Bands()
    {
        ChartStreamingRangePage page = new();
        Window window = new() { Width = 960, Height = 650, Content = page };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var model = Assert.IsType<ChartStreamingRangeViewModel>(page.DataContext);
            var view = Assert.Single(page.GetLogicalDescendants().OfType<ProChartView>());
            CheckBox toggle = page.FindControl<CheckBox>("CalculatedBandsToggle")!;
            Point point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs(); Assert.True(model.UseCalculatedBands);
            Assert.Equal(1024, model.CalculatorSamplesProcessed);
            model.AppendCommand.Execute(null); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(1088, model.CalculatorSamplesProcessed);
            byte[] png = view.ExportPng(); string svg = view.ExportSvg();
            Assert.NotEmpty(png); Assert.Contains("Live interval envelope", svg);
            bool hitFound = false;
            for (int y = 10; y < view.Bounds.Height && !hitFound; y += 7)
                for (int x = 10; x < view.Bounds.Width; x += 7)
                    if (view.HitTest(new Point(x, y)) is { SeriesKind: ChartSeriesKind.RangeArea } hit)
                    {
                        Near(model.Chart.Snapshot.Series[0].LowValues![hit.PointIndex], hit.LowValue);
                        Near(model.Chart.Snapshot.Series[0].HighValues![hit.PointIndex], hit.HighValue);
                        hitFound = true; break;
                    }
            Assert.True(hitFound);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "sample", "gallery");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "StreamingBollinger.png"), png);
                File.WriteAllText(Path.Combine(directory, "StreamingBollinger.svg"), svg);
            }
        }
        finally { window.Close(); }
    }

    private static void Near(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.InRange(Math.Abs(expected.Value - actual!.Value), 0, 1e-8);
    }
}
