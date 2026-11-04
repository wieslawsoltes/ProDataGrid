// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public sealed class ChartCoordinateWindowSampleTests
{
    [Fact]
    public void Coordinate_Commands_Follow_Pin_And_Restore_Without_Replaying_Source_Rows()
    {
        ChartMultiSeriesStreamingViewModel model = new(); PumpContext context = new();
        model.Activate(context);
        CoordinateWindowChartDataSource window = model.WindowSource!;
        try
        {
            long initial = model.Source!.TotalSamples;
            model.FollowLatestSpan(120); context.Drain();
            var view = window.BuildView(model.Model!.Request);
            var x = view.Snapshot.Series[0].XValues!;
            Assert.Equal(initial, model.Source.TotalSamples);
            Assert.Equal(ChartCoordinateWindow.Latest(120), window.Window);
            Assert.Null(model.Model.Request.WindowCount); Assert.False(model.Model.Interaction.FollowLatest);
            Assert.InRange(x[0], x[^1] - 120, x[^1]); Assert.True(view.WindowCount < 512);
            double oldLatest = x[^1];
            model.AppendBatch(); context.Drain();
            Assert.True(model.Model.Snapshot.Series[0].XValues![^1] > oldLatest);
            Assert.True(model.PinDisplayedWindow()); context.Drain();
            ChartCoordinateWindow pinned = window.Window;
            double[] pinnedX = model.Model.Snapshot.Series[0].XValues!.ToArray();
            model.AppendBatch(); model.AppendBatch(); context.Drain();
            Assert.Equal(pinned, window.Window); Assert.Equal(pinnedX, model.Model.Snapshot.Series[0].XValues);
            model.UseLatestRows(); context.Drain();
            Assert.Equal(ChartCoordinateWindow.All, window.Window);
            Assert.Equal(initial + 3 * 128, model.Source.TotalSamples);
            Assert.Equal(512, window.BuildView(model.Model.Request).WindowCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.FollowLatestSpan(double.NaN));
            Assert.Equal(ChartCoordinateWindow.All, window.Window);
        }
        finally { model.Deactivate(); context.Drain(); }
        Assert.True(window.IsDisposed); Assert.Null(model.WindowSource);
    }

    [AvaloniaFact]
    public void Bound_Buttons_Follow_And_Pin_And_Close_Releases_The_Adapter()
    {
        ChartMultiSeriesStreamingDemo demo = new();
        Window host = new() { Width = 1100, Height = 700, Content = demo }; host.ApplySampleTheme();
        var model = Assert.IsType<ChartMultiSeriesStreamingViewModel>(demo.DataContext);
        CoordinateWindowChartDataSource? adapter = null;
        try
        {
            host.Show(); Pump(host); adapter = model.WindowSource!;
            Click(host, demo.FindControl<Button>("FollowSpanButton")!); Pump(host);
            Assert.Equal(ChartCoordinateWindow.Latest(120), adapter.Window);
            Click(host, demo.FindControl<Button>("AppendRowsButton")!); Pump(host);
            var chart = Assert.Single(demo.GetLogicalDescendants().OfType<ProChartView>());
            double latest = model.Model!.Snapshot.Series[0].XValues![^1];
            Click(host, demo.FindControl<Button>("PinWindowButton")!); Pump(host);
            Assert.Equal(ChartCoordinateWindowKind.Fixed, adapter.Window.Kind);
            Assert.Equal(latest, adapter.Window.MaximumX);
            model.AppendBatch(); Pump(host);
            Assert.Equal(latest, model.Model.Snapshot.Series[0].XValues![^1]);
            Assert.Same(adapter.BuildSnapshot(model.Model.Request), chart.ChartModel!.Snapshot);
            Save("PinnedCoordinateWindow", chart);
            Click(host, demo.FindControl<Button>("FollowSpanButton")!); Pump(host);
            Assert.True(model.Model.Snapshot.Series[0].XValues![^1] > latest);
            Save("FollowingCoordinateWindow", chart);
            Click(host, demo.FindControl<Button>("LatestRowsButton")!); Pump(host);
            Assert.Equal(ChartCoordinateWindow.All, adapter.Window);
        }
        finally { host.Close(); Dispatcher.UIThread.RunJobs(); }
        Assert.NotNull(adapter); Assert.True(adapter.IsDisposed); Assert.Null(model.WindowSource);
    }

    [AvaloniaFact]
    public void Worker_Coalescing_Delivers_One_Latest_Coordinate_Capture_On_The_Consumer_Thread()
    {
        StreamingMultiSeriesChartDataSource source = new(512, new[]
        { new StreamingChartSeries("A", ChartSeriesKind.Scatter), new StreamingChartSeries("B", ChartSeriesKind.Scatter) });
        source.Append(0, new double?[] { 0, 1 });
        using CoordinateWindowChartDataSource window = new(source, ChartCoordinateWindow.Latest(20));
        using CoalescingChartDataSource delivered = ChartDataSourceDispatch.Create(window);
        using ChartModel model = new();
        using (model.DeferRefresh()) { model.CategoryAxis.Kind = ChartAxisKind.Value; model.Request.MaxPoints = 32; model.DataSource = delivered; }
        int uiThread = Environment.CurrentManagedThreadId, changes = 0;
        model.SnapshotChanged += (_, _) => { Assert.Equal(uiThread, Environment.CurrentManagedThreadId); changes++; };
        Task producer = Task.Run(() =>
        {
            for (int i = 1; i <= 1000; i++) source.Append(i * 0.25, new double?[] { i % 13 == 3 ? null : i, -i });
        });
        Assert.True(producer.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, changes); Dispatcher.UIThread.RunJobs(); Assert.Equal(1, changes);
        var capture = window.BuildView(model.Request);
        Assert.Same(capture.Snapshot, model.Snapshot);
        Assert.Equal(250, model.Snapshot.Series[0].XValues![^1]);
        Assert.All(model.Snapshot.Series[0].XValues!, x => Assert.InRange(x, 230, 250));
        Assert.Equal(81, window.GetTotalCategoryCount());
        Assert.Equal(1000, capture.SourceSampleIndices[^1]);
    }

    private static void Pump(Control control) { Dispatcher.UIThread.RunJobs(); control.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Click(Window host, Button button)
    {
        Point position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), host)!.Value;
        host.MouseDown(position, MouseButton.Left); host.MouseUp(position, MouseButton.Left);
    }
    private static void Save(string name, ProChartView view)
    {
        byte[] png = view.ExportPng(); string svg = view.ExportSvg();
        Assert.NotEmpty(png); Assert.Contains("<svg", svg);
        string? root = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || string.IsNullOrEmpty(root)) return;
        string directory = Path.Combine(root, "artifacts", "charting", "gallery"); Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), png); File.WriteAllText(Path.Combine(directory, name + ".svg"), svg);
    }
    private sealed class PumpContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Work, object? State)> _queue = new();
        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));
        public void Drain() { while (_queue.TryDequeue(out var item)) item.Work(item.State); }
    }
}
