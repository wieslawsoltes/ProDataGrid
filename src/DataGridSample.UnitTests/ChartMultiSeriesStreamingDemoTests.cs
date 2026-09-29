// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using ProCharts;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartMultiSeriesStreamingDemoTests
{
    [AvaloniaFact]
    public void Sample_Tab_Is_Lazy_And_Detaches_Its_Feed_When_Another_Tab_Is_Selected()
    {
        ChartingPage page = new();
        TabControl tabs = Assert.IsType<TabControl>(page.Content);
        TabItem target = tabs.Items.OfType<TabItem>().Single(tab => Equals(tab.Header, "Synchronized streams"));
        ChartMultiSeriesStreamingDemo demo = Assert.IsType<ChartMultiSeriesStreamingDemo>(target.Content);
        Assert.Null(demo.Source); Assert.Null(demo.Model); Assert.False(demo.IsRunning);
        Window window = Host(page);
        try
        {
            window.Show(); Pump(window); Assert.Null(demo.Source);
            tabs.SelectedItem = target; Pump(window);
            Assert.NotNull(demo.Source); Assert.NotNull(demo.Model);
            Assert.Equal(ChartMultiSeriesStreamingDemo.SeedRows, demo.Source!.TotalSamples); Assert.False(demo.IsRunning);
            demo.SetRunning(true); Assert.True(demo.IsRunning);
            tabs.SelectedIndex = 0; Pump(window);
            Assert.Null(demo.Source); Assert.Null(demo.Model); Assert.False(demo.IsRunning);
            Assert.Null(demo.ChartView.ChartModel);
            tabs.SelectedItem = target; Pump(window);
            Assert.Equal(ChartMultiSeriesStreamingDemo.SeedRows, demo.Source!.TotalSamples); Assert.False(demo.IsRunning);
        }
        finally { window.Close(); }
        Assert.Null(demo.Source); Assert.Null(demo.Model); Assert.False(demo.IsRunning);
    }

    [AvaloniaFact]
    public void Batched_Feed_Coalesces_Presentation_And_Changing_Detail_Does_Not_Replay_Input()
    {
        ChartMultiSeriesStreamingDemo demo = new(); Window window = Host(demo);
        try
        {
            window.Show(); Pump(window);
            ChartModel model = demo.Model!; StreamingMultiSeriesChartDataSource source = demo.Source!;
            ChartDataSnapshot before = model.Snapshot;
            int updates = 0; model.SnapshotChanged += (_, _) => updates++;
            for (int i = 0; i < 3; i++) demo.AppendBatch();
            Assert.Equal(0, updates); Assert.Same(before, model.Snapshot);
            Pump(window); Assert.Equal(1, updates);
            long total = ChartMultiSeriesStreamingDemo.SeedRows + 3 * ChartMultiSeriesStreamingDemo.BatchRows;
            Assert.Equal(total, source.TotalSamples);
            StreamingMultiSeriesChartView captured = source.BuildView(model.Request);
            Assert.Same(captured.Snapshot, model.Snapshot);
            Assert.Equal(ChartMultiSeriesStreamingDemo.VisibleRows, captured.WindowCount);
            Assert.Equal(total - 1, captured.SourceSampleIndices[^1]);
            Assert.Equal(3, model.Snapshot.Series.Count);
            Assert.All(model.Snapshot.Series, series => Assert.Equal(ChartSeriesKind.Scatter, series.Kind));
            Assert.Same(model.Snapshot.Series[0].XValues, model.Snapshot.Series[2].XValues);
            demo.SetPointBudget(null); Pump(window);
            Assert.Equal(total, source.TotalSamples); Assert.Equal(ChartMultiSeriesStreamingDemo.VisibleRows, model.Snapshot.Categories.Count);
            demo.SetPointBudget(64); Pump(window);
            Assert.Equal(total, source.TotalSamples); Assert.Contains("64 per channel", demo.StatusText);
            Assert.Throws<ArgumentOutOfRangeException>(() => demo.SetPointBudget(0));
            Assert.NotEmpty(demo.ChartView.ExportPng()); Assert.Contains("<svg", demo.ChartView.ExportSvg());
            Assert.Equal(ChartMultiSeriesStreamingDemo.SeedRows - 1,
                source.BuildView(new ChartDataRequest { WindowStart = 0, WindowCount = ChartMultiSeriesStreamingDemo.SeedRows }).SourceSampleIndices[^1]);
            Assert.NotSame(before, model.Snapshot);
        }
        finally { window.Close(); }
        Assert.False(demo.IsRunning); Assert.Null(demo.Source);
    }

    [AvaloniaFact]
    public void Buttons_Append_And_Reset_While_Close_Suppresses_Pending_Delivery()
    {
        ChartMultiSeriesStreamingDemo demo = new(); Window window = Host(demo);
        Assert.Throws<InvalidOperationException>(() => demo.AppendBatch());
        Assert.Throws<InvalidOperationException>(() => demo.SetRunning(true));
        window.Show(); Pump(window);
        try
        {
            Button append = demo.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, "Append 128 rows"));
            append.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
            Assert.Equal(ChartMultiSeriesStreamingDemo.SeedRows + ChartMultiSeriesStreamingDemo.BatchRows, demo.Source!.TotalSamples);
            Button reset = demo.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, "Reset"));
            reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
            Assert.Equal(ChartMultiSeriesStreamingDemo.SeedRows, demo.Source.TotalSamples); Assert.False(demo.IsRunning);
            Assert.Contains("Paused", demo.StatusText);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "gallery"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "SynchronizedSample.png"), demo.ChartView.ExportPng());
                File.WriteAllText(Path.Combine(directory, "SynchronizedSample.svg"), demo.ChartView.ExportSvg());
            }
            ChartModel model = demo.Model!; int changes = 0; model.SnapshotChanged += (_, _) => changes++;
            demo.AppendBatch(); // Queued, but not serviced before owner shutdown.
            window.Close(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, changes); Assert.Null(demo.ChartView.ChartModel); Assert.Null(demo.Model); Assert.Null(demo.Source);
        }
        finally { window.Close(); }
    }

    private static Window Host(Control content)
    {
        Window window = new() { Width = 1100, Height = 700, Content = content };
        window.ApplySampleTheme(); return window;
    }

    private static void Pump(Control control) { Dispatcher.UIThread.RunJobs(); control.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
