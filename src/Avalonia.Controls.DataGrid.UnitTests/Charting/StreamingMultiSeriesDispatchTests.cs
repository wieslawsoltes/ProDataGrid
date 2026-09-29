// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingMultiSeriesDispatchTests
    {
        [AvaloniaFact]
        public void Numeric_Scatter_Worker_Burst_Preserves_Nonuniform_X_Hits_And_Aligned_Original_Rows()
            => VerifyWorkerDelivery(numericScatter: true);

        [AvaloniaFact]
        public void Category_Line_Area_Worker_Burst_Preserves_Labels_And_Original_Row_Mapping()
            => VerifyWorkerDelivery(numericScatter: false);

        private static double XForRow(long row) => 1 + row * (double)row * 0.01;

        private static void VerifyWorkerDelivery(bool numericScatter)
        {
            // Existing Line/Area rendering is category-indexed; numeric X positioning belongs to Scatter.
            // Exercise both public contracts rather than pretending source-side X storage changes the renderer.
            StreamingMultiSeriesChartDataSource source = new(512, new[]
            {
                new StreamingChartSeries("Reference", numericScatter ? ChartSeriesKind.Scatter : ChartSeriesKind.Line),
                new StreamingChartSeries("Tracking", numericScatter ? ChartSeriesKind.Scatter : ChartSeriesKind.Line),
                new StreamingChartSeries("Load", numericScatter ? ChartSeriesKind.Scatter : ChartSeriesKind.Area)
            });
            double?[] seed = { 80, 50, 20 };
            for (int i = 0; i < 100; i++) source.Append(XForRow(i), seed, i.ToString(CultureInfo.InvariantCulture));
            using CoalescingChartDataSource delivered = ChartDataSourceDispatch.Create(source);
            using ChartModel model = new();
            using (model.DeferRefresh())
            {
                model.CategoryAxis.Kind = numericScatter ? ChartAxisKind.Value : ChartAxisKind.Category;
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
                        source.Append(XForRow(i), row, i.ToString(CultureInfo.InvariantCulture));
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
                            long original = captured.SourceSampleIndices[hit.PointIndex];
                            Assert.Equal(XForRow(original), captured.Snapshot.Series[hit.SeriesIndex].XValues![hit.PointIndex]);
                            Assert.Equal(original.ToString(CultureInfo.InvariantCulture), hit.Category);
                            Assert.Equal(captured.Snapshot.Series[hit.SeriesIndex].Values[hit.PointIndex], hit.Value);
                            if (numericScatter) Assert.Equal(XForRow(original), hit.XValue);
                            else Assert.Null(hit.XValue);
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
                    string name = numericScatter ? "SynchronizedNumericScatter" : "SynchronizedCategorySeries";
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), png);
                    File.WriteAllText(Path.Combine(directory, name + ".svg"), svg);
                }
            }
            finally { window.Close(); }
        }

        [Fact]
        public void Numeric_Scatter_Locates_Nonuniform_X_Rather_Than_The_Middle_Category_Position()
        {
            StreamingMultiSeriesChartDataSource source = new(4, new[]
                { new StreamingChartSeries("Irregular X", ChartSeriesKind.Scatter) });
            source.AppendRange(new double[] { 1, 2, 101 }, new double?[] { 50, 50, 50 });
            StreamingMultiSeriesChartView captured = source.BuildView(new() { DownsampleMode = ChartDownsampleMode.None });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
                PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
                CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 1, CategoryAxisMaximum = 101,
                ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 1
            };
            SKRect bounds = new(0, 0, 1000, 400);
            foreach (bool indexed in new[] { false, true })
            {
                SkiaChartRenderer renderer = new() { UseInteractionCache = indexed };
                var hit = renderer.HitTest(new SKPoint(10, 200), bounds, captured.Snapshot, style);
                Assert.NotNull(hit); Assert.Equal(1, hit.Value.PointIndex); Assert.Equal(2, hit.Value.XValue);
                Assert.Equal(1, captured.SourceSampleIndices[hit.Value.PointIndex]);
                Assert.Null(renderer.HitTest(new SKPoint(500, 200), bounds, captured.Snapshot, style));
            }
        }
    }
}
