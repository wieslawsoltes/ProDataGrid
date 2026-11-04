// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Behaviors;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts;
using ProCharts.Avalonia;
using Xunit;
using VM = DataGridSample.ViewModels.ChartMultiSeriesStreamingViewModel;

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
        VM vm = Assert.IsType<VM>(demo.DataContext);
        Assert.Null(vm.Source); Assert.Null(vm.Model); Assert.False(vm.IsRunning);
        Window window = Host(page);
        try
        {
            window.Show(); Pump(window); Assert.Null(vm.Source);
            tabs.SelectedItem = target; Pump(window);
            Assert.NotNull(vm.Source); Assert.NotNull(vm.Model);
            Assert.Equal(VM.SeedRows, vm.Source!.TotalSamples); Assert.False(vm.IsRunning);
            vm.RunCommand.Execute(null); Assert.True(vm.IsRunning);
            tabs.SelectedIndex = 0; Pump(window);
            Assert.Null(vm.Source); Assert.Null(vm.Model); Assert.False(vm.IsRunning);
            Assert.Null(View(demo).ChartModel);
            tabs.SelectedItem = target; Pump(window);
            Assert.Equal(VM.SeedRows, vm.Source!.TotalSamples); Assert.False(vm.IsRunning);
        }
        finally { window.Close(); }
        Assert.Null(vm.Source); Assert.Null(vm.Model); Assert.False(vm.IsRunning);
    }

    [AvaloniaFact]
    public void Batched_Feed_Coalesces_Presentation_And_Changing_Detail_Does_Not_Replay_Input()
    {
        ChartMultiSeriesStreamingDemo demo = new(); Window window = Host(demo);
        VM vm = Assert.IsType<VM>(demo.DataContext);
        try
        {
            window.Show(); Pump(window);
            ChartModel model = vm.Model!; StreamingMultiSeriesChartDataSource source = vm.Source!;
            ChartDataSnapshot before = model.Snapshot;
            int updates = 0; model.SnapshotChanged += (_, _) => updates++;
            for (int i = 0; i < 3; i++) vm.AppendCommand.Execute(null);
            Assert.Equal(0, updates); Assert.Same(before, model.Snapshot);
            Pump(window); Assert.Equal(1, updates);
            long total = VM.SeedRows + 3 * VM.BatchRows;
            Assert.Equal(total, source.TotalSamples);
            StreamingMultiSeriesChartView captured = source.BuildView(model.Request);
            Assert.Same(captured.Snapshot, model.Snapshot);
            Assert.Equal(VM.VisibleRows, captured.WindowCount);
            Assert.Equal(total - 1, captured.SourceSampleIndices[^1]);
            Assert.Equal(3, model.Snapshot.Series.Count);
            Assert.All(model.Snapshot.Series, series => Assert.Equal(ChartSeriesKind.Scatter, series.Kind));
            Assert.Same(model.Snapshot.Series[0].XValues, model.Snapshot.Series[2].XValues);
            vm.FullWindowCommand.Execute(null); Pump(window);
            Assert.Equal(total, source.TotalSamples); Assert.Equal(VM.VisibleRows, model.Snapshot.Categories.Count);
            vm.SetPointBudget(64); Pump(window);
            Assert.Equal(total, source.TotalSamples); Assert.Contains("64 per channel", vm.StatusText);
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.SetPointBudget(0));
            Assert.NotEmpty(View(demo).ExportPng()); Assert.Contains("<svg", View(demo).ExportSvg());
            Assert.Equal(VM.SeedRows - 1,
                source.BuildView(new ChartDataRequest { WindowStart = 0, WindowCount = VM.SeedRows }).SourceSampleIndices[^1]);
            Assert.NotSame(before, model.Snapshot);
        }
        finally { window.Close(); }
        Assert.False(vm.IsRunning); Assert.Null(vm.Source);
    }

    [AvaloniaFact]
    public void Real_Button_Input_Appends_And_Resets_While_Close_Suppresses_Pending_Delivery()
    {
        ChartMultiSeriesStreamingDemo demo = new(); Window window = Host(demo);
        VM vm = Assert.IsType<VM>(demo.DataContext);
        Assert.Throws<InvalidOperationException>(() => vm.AppendBatch());
        Assert.Throws<InvalidOperationException>(() => vm.SetRunning(true));
        window.Show(); Pump(window);
        try
        {
            Click(window, demo.FindControl<Button>("AppendRowsButton")!); Pump(window);
            Assert.Equal(VM.SeedRows + VM.BatchRows, vm.Source!.TotalSamples);
            Click(window, demo.FindControl<Button>("ResetRowsButton")!); Pump(window);
            Assert.Equal(VM.SeedRows, vm.Source.TotalSamples); Assert.False(vm.IsRunning);
            Assert.Contains("Paused", vm.StatusText);
            string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
            {
                string directory = Path.Combine(workspace, "artifacts", "charting", "gallery"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "SynchronizedSample.png"), View(demo).ExportPng());
                File.WriteAllText(Path.Combine(directory, "SynchronizedSample.svg"), View(demo).ExportSvg());
            }
            ChartModel model = vm.Model!; int changes = 0; model.SnapshotChanged += (_, _) => changes++;
            vm.AppendBatch();
            window.Close(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, changes); Assert.Null(View(demo).ChartModel); Assert.Null(vm.Model); Assert.Null(vm.Source);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Context_Replacement_And_Disabling_Behavior_Release_Old_Sessions()
    {
        ChartMultiSeriesStreamingDemo demo = new(); Window window = Host(demo);
        VM first = Assert.IsType<VM>(demo.DataContext);
        try
        {
            window.Show(); Pump(window); first.SetRunning(true);
            VM replacement = new(); demo.DataContext = replacement; Pump(window);
            Assert.Null(first.Source); Assert.False(first.IsRunning);
            Assert.True(replacement.IsActive); Assert.False(replacement.IsRunning);
            Assert.Same(replacement.Model, View(demo).ChartModel);
            ChartMultiSeriesLifecycle.SetIsEnabled(demo, false); Pump(window);
            Assert.False(replacement.IsActive); Assert.Null(View(demo).ChartModel);
            ChartMultiSeriesLifecycle.SetIsEnabled(demo, true); Pump(window);
            Assert.True(replacement.IsActive); Assert.Equal(VM.SeedRows, replacement.Source!.TotalSamples);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void ViewModel_Commands_And_Reactivation_Run_Without_An_Avalonia_Dispatcher()
    {
        VM vm = new(); PumpContext context = new();
        Assert.False(vm.IsActive);
        Assert.Throws<ArgumentNullException>(() => vm.Activate(null!));
        vm.Activate(context);
        try
        {
            Assert.Equal(VM.SeedRows, vm.Source!.TotalSamples);
            vm.AppendCommand.Execute(null); vm.AppendCommand.Execute(null);
            Assert.Equal(1, context.Pending);
            context.Drain();
            Assert.Equal(VM.SeedRows + 2 * VM.BatchRows, vm.Source.TotalSamples);
            vm.SmallBudgetCommand.Execute(null); Assert.Equal(128, vm.Model!.Request.MaxPoints);
            vm.LargeBudgetCommand.Execute(null); Assert.Equal(512, vm.Model.Request.MaxPoints);
            vm.FullWindowCommand.Execute(null); Assert.Equal(ChartDownsampleMode.None, vm.Model.Request.DownsampleMode);
            vm.RunCommand.Execute(null); Assert.True(vm.IsRunning);
            vm.AppendCommand.Execute(null); vm.Deactivate(); context.Drain();
            Assert.False(vm.IsActive); Assert.False(vm.IsRunning);
            vm.Activate(context); Assert.Equal(VM.SeedRows, vm.Source!.TotalSamples);
            vm.ResetCommand.Execute(null); context.Drain(); Assert.Equal(VM.SeedRows, vm.Source.TotalSamples);
        }
        finally { vm.Deactivate(); }
    }

    private static ProChartView View(Control demo) => Assert.Single(demo.GetLogicalDescendants().OfType<ProChartView>());
    private static Window Host(Control content)
    {
        Window window = new() { Width = 1100, Height = 700, Content = content };
        window.ApplySampleTheme(); return window;
    }
    private static void Pump(Control control) { Dispatcher.UIThread.RunJobs(); control.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Click(Window window, Button button)
    {
        Point point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
    }
    private sealed class PumpContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _work = new();
        public int Pending => _work.Count;
        public override void Post(SendOrPostCallback d, object? state) => _work.Enqueue((d, state));
        public void Drain() { while (_work.TryDequeue(out var item)) item.Callback(item.State); }
    }
}
