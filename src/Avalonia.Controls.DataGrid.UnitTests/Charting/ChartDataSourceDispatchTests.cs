// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartDataSourceDispatchTests
    {
        [AvaloniaFact]
        public void Worker_Burst_Delivers_One_Model_Snapshot_On_UI_Thread_And_Preserves_Rendered_Data()
        {
            StreamingRangeChartDataSource source = new(2048, "Dispatched live envelope");
            source.Append(new ChartRangeSample(0, 20, 40));
            using CoalescingChartDataSource adapter = ChartDataSourceDispatch.Create(source);
            using ChartModel model = new();
            using (model.DeferRefresh())
            {
                model.Request.MaxPoints = 256; model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
                model.CategoryAxis.Kind = ChartAxisKind.Value; model.DataSource = adapter;
            }
            ProChartView view = new() { ChartModel = model, Width = 800, Height = 400,
                ChartStyle = new SkiaChartStyle { ShowLegend = false, PaddingRight = 44 } };
            Window window = new() { Width = 800, Height = 400, Content = view };
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                int ui = Environment.CurrentManagedThreadId, changes = 0;
                model.SnapshotChanged += (_, _) => { Assert.Equal(ui, Environment.CurrentManagedThreadId); changes++; };
                Task producer = Task.Run(() =>
                {
                    Assert.NotEqual(ui, Environment.CurrentManagedThreadId);
                    for (int i = 1; i <= 1000; i++)
                    {
                        double center = 40 + Math.Sin(i * 0.013) * 15;
                        source.Append(new ChartRangeSample(i, i is >= 490 and < 510 ? null : center - 4, center + 4));
                    }
                });
                Assert.True(producer.Wait(TimeSpan.FromSeconds(5)));
                Assert.Equal(0, changes); Assert.True(adapter.HasPendingUpdate);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.Equal(1, changes); Assert.False(adapter.HasPendingUpdate);
                Assert.Equal(1000, model.Snapshot.Series[0].XValues![^1]); Assert.Equal(1001, source.TotalSamples);
                Assert.Same(source.BuildSnapshot(model.Request), model.Snapshot);
                byte[] png = view.ExportPng(); string svg = view.ExportSvg();
                Assert.NotEmpty(png); Assert.Contains("<svg", svg);
                bool hitFound = false;
                for (int y = 20; y < 380 && !hitFound; y += 11)
                    for (int x = 20; x < 770; x += 11)
                        if (view.HitTest(new Point(x, y)) is { SeriesKind: ChartSeriesKind.RangeArea } hit)
                        { Assert.Equal(model.Snapshot.Series[0].XValues![hit.PointIndex], hit.XValue); hitFound = true; break; }
                Assert.True(hitFound);
                string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
                if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
                {
                    string directory = Path.Combine(workspace, "artifacts", "charting", "gallery");
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, "CoalescedWorkerRange.png"), png);
                    File.WriteAllText(Path.Combine(directory, "CoalescedWorkerRange.svg"), svg);
                }
            }
            finally { window.Close(); }
        }

        [AvaloniaFact]
        public void Factory_Requires_UI_Construction_And_Disposal_Cancels_Queued_UI_Delivery()
        {
            StreamingChartDataSource source = new(8);
            Task wrongThread = Task.Run(() => Assert.Throws<InvalidOperationException>(() => ChartDataSourceDispatch.Create(source)));
            Assert.True(wrongThread.Wait(TimeSpan.FromSeconds(5)));
            CoalescingChartDataSource adapter = ChartDataSourceDispatch.Create(source, DispatcherPriority.Background);
            int events = 0; adapter.DataInvalidated += (_, _) => events++;
            source.Append(new ChartSample(0, 1)); adapter.Dispose();
            Dispatcher.UIThread.RunJobs(); Assert.Equal(0, events);
            source.Append(new ChartSample(1, 2)); Assert.Equal(2, source.Count);
        }
    }
}
