// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.IO;
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
    public sealed class StreamingMultiSeriesDispatchTests
    {
        [AvaloniaFact]
        public void Worker_Burst_Delivers_One_Aligned_UI_Snapshot_With_Original_Hit_Identity_And_Native_Exports()
        {
            StreamingMultiSeriesChartDataSource source = new(512, new[]
            {
                new StreamingChartSeries("Reference"), new StreamingChartSeries("Tracking"),
                new StreamingChartSeries("Load", ChartSeriesKind.Area)
            });
            double?[] seed = { 80, 50, 20 };
            for (int i = 0; i < 100; i++) source.Append(i, seed);
            using CoalescingChartDataSource delivered = ChartDataSourceDispatch.Create(source);
            using ChartModel model = new();
            using (model.DeferRefresh())
            {
                model.CategoryAxis.Kind = ChartAxisKind.Value;
                model.Request.MaxPoints = 48; model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
                model.DataSource = delivered;
            }
            Assert.True(model.ShowLatest(80));
            ProChartView view = new() { ChartModel = model, Width = 840, Height = 440,
                ChartStyle = new SkiaChartStyle { ShowLegend = true, PaddingRight = 60 } };
            Window window = new() { Width = 840, Height = 440, Content = view };
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                int ui = Environment.CurrentManagedThreadId, updates = 0;
                model.SnapshotChanged += (_, _) => { Assert.Equal(ui, Environment.CurrentManagedThreadId); updates++; };
                Task producer = Task.Run(() =>
                {
                    Assert.NotEqual(ui, Environment.CurrentManagedThreadId);
                    double?[] row = new double?[3];
                    for (int i = 100; i < 1100; i++)
                    {
                        row[0] = 80 + Math.Sin(i * 0.09) * 10;
                        row[1] = i is >= 1050 and <= 1055 ? null : 50 + Math.Cos(i * 0.11) * 8;
                        row[2] = 20 + Math.Sin(i * 0.13) * 5;
                        source.Append(i, row);
                    }
                });
                Assert.True(producer.Wait(TimeSpan.FromSeconds(10))); Assert.Equal(0, updates);
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Assert.Equal(1, updates);
                StreamingMultiSeriesChartView captured = source.BuildView(model.Request);
                Assert.Same(captured.Snapshot, model.Snapshot); Assert.Equal(80, captured.WindowCount);
                Assert.Equal(1020, captured.SourceSampleIndices[0]); Assert.Equal(1099, captured.SourceSampleIndices[^1]);
                Assert.Equal(1100, captured.TotalSamples);
                Assert.Same(model.Snapshot.Series[0].XValues, model.Snapshot.Series[2].XValues);
                bool hitFound = false;
                for (int y = 20; y < 420 && !hitFound; y += 7)
                    for (int x = 20; x < 780; x += 7)
                        if (view.HitTest(new Point(x, y)) is { } hit)
                        {
                            Assert.InRange(hit.SeriesIndex, 0, 2);
                            Assert.Equal((double)captured.SourceSampleIndices[hit.PointIndex], hit.XValue);
                            Assert.Equal(captured.Snapshot.Series[hit.SeriesIndex].Values[hit.PointIndex], hit.Value);
                            hitFound = true; break;
                        }
                Assert.True(hitFound);
                byte[] png = view.ExportPng(); string svg = view.ExportSvg();
                Assert.NotEmpty(png); Assert.Contains("<svg", svg);
                string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
                if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && !string.IsNullOrEmpty(workspace))
                {
                    string directory = Path.Combine(workspace, "artifacts", "charting", "gallery");
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, "SynchronizedMultiSeries.png"), png);
                    File.WriteAllText(Path.Combine(directory, "SynchronizedMultiSeries.svg"), svg);
                }
            }
            finally { window.Close(); }
        }
    }
}
