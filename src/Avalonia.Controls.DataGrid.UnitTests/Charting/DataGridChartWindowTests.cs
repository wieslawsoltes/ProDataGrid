// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Linq;
using ProCharts;
using ProDataGrid.Charting;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class DataGridChartWindowTests
    {
        [Theory]
        [InlineData(null, null, 0, 100)]
        [InlineData(10, 20, 10, 20)]
        [InlineData(10, null, 10, 90)]
        [InlineData(null, 20, 0, 20)]
        [InlineData(-100, 20, 0, 20)]
        [InlineData(10, -1, 10, 0)]
        [InlineData(100, 20, 100, 0)]
        [InlineData(int.MaxValue, int.MaxValue, 100, 0)]
        [InlineData(50, int.MaxValue, 50, 50)]
        [InlineData(0, 0, 0, 0)]
        public void Windows_Clamp_Safely_And_Keep_All_Channels_Aligned(int? start, int? count, int expectedStart, int expectedCount)
        {
            using DataGridChartModel model = Create(100);
            ChartDataSnapshot snapshot = model.BuildSnapshot(new ChartDataRequest
            {
                WindowStart = start, WindowCount = count, DownsampleMode = ChartDownsampleMode.None
            });
            AssertWindow(snapshot, expectedStart, expectedCount);
        }

        [Fact]
        public void Repeated_Window_Requests_Do_Not_Mutate_Previously_Returned_Data()
        {
            using DataGridChartModel model = Create(1000);
            ChartDataSnapshot full = model.BuildSnapshot(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None });
            ChartDataSnapshot first = model.BuildSnapshot(new ChartDataRequest
                { WindowStart = 100, WindowCount = 128, DownsampleMode = ChartDownsampleMode.None });
            for (int start = 0; start < 1000; start += 73)
            {
                var current = model.BuildSnapshot(new ChartDataRequest
                    { WindowStart = start, WindowCount = 128, DownsampleMode = ChartDownsampleMode.None });
                AssertWindow(current, start, Math.Min(128, 1000 - start));
            }
            AssertWindow(full, 0, 1000);
            AssertWindow(first, 100, 128);
        }

        [Fact]
        public void Small_Visible_Windows_Do_Not_Allocate_Full_Source_Channel_Copies()
        {
            using DataGridChartModel model = Create(100000);
            ChartDataRequest request = new() { WindowStart = 50000, WindowCount = 128, DownsampleMode = ChartDownsampleMode.None };
            for (int i = 0; i < 100; i++) model.BuildSnapshot(request);
            long before = GC.GetAllocatedBytesForCurrentThread();
            ChartDataSnapshot snapshot = model.BuildSnapshot(request);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(bytes, 0, 20000);
            AssertWindow(snapshot, 50000, 128);
        }

        [Fact]
        public void Window_Copy_Preserves_Absent_Optional_Channels()
        {
            using DataGridChartModel model = Create(100);
            model.Series.Clear();
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Value only", Kind = ChartSeriesKind.Line,
                ValueSelector = static item => ((Row)item).Value
            });
            ChartDataSnapshot snapshot = model.BuildSnapshot(new ChartDataRequest
                { WindowStart = 30, WindowCount = 20, DownsampleMode = ChartDownsampleMode.None });
            Assert.Equal(20, snapshot.Categories.Count);
            Assert.Null(snapshot.Series[0].XValues);
            Assert.Null(snapshot.Series[0].SizeValues);
            Assert.Equal(Enumerable.Range(30, 20).Select(i => (double?)(i * 2)), snapshot.Series[0].Values);
        }

        [Fact]
        public void Windowing_Precedes_Line_Decimation_And_Does_Not_Modify_The_Cache()
        {
            using DataGridChartModel model = Create(1000, ChartSeriesKind.Line);
            ChartDataSnapshot snapshot = model.BuildSnapshot(new ChartDataRequest
                { WindowStart = 300, WindowCount = 200, MaxPoints = 20, DownsampleMode = ChartDownsampleMode.Lttb });
            ChartSeriesSnapshot series = snapshot.Series[0];
            Assert.InRange(series.Values.Count, 2, 20);
            Assert.Equal(600, series.Values[0]);
            Assert.Equal(998, series.Values[^1]);
            Assert.All(series.Values, value => Assert.InRange(value!.Value, 600, 998));
            AssertWindow(model.BuildSnapshot(new ChartDataRequest
                { WindowStart = 300, WindowCount = 200, DownsampleMode = ChartDownsampleMode.None }), 300, 200);
        }

        [Theory]
        [InlineData(ChartSeriesKind.Heatmap)]
        [InlineData(ChartSeriesKind.Treemap)]
        [InlineData(ChartSeriesKind.Sunburst)]
        [InlineData(ChartSeriesKind.Gauge)]
        public void Structural_Chart_Types_Keep_Category_Identity_Even_With_Explicit_Decimation_Requests(ChartSeriesKind kind)
        {
            using DataGridChartModel model = Create(100, kind);
            foreach (ChartDownsampleMode mode in Enum.GetValues<ChartDownsampleMode>())
            {
                ChartDataSnapshot snapshot = model.BuildSnapshot(new ChartDataRequest
                    { WindowStart = 10, WindowCount = 61, MaxPoints = 5, DownsampleMode = mode });
                AssertWindow(snapshot, 10, 61);
                Assert.Equal(kind, snapshot.Series[0].Kind);
            }
        }

        [Fact]
        public void Multiple_Series_Keep_Independent_Missing_Value_Channels()
        {
            using DataGridChartModel model = Create(100);
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Intermittent", Kind = ChartSeriesKind.Line,
                ValueSelector = static item => ((Row)item).Index % 3 == 0 ? null : ((Row)item).Index,
                XValueSelector = static item => ((Row)item).Index * 10d
            });
            ChartDataSnapshot snapshot = model.BuildSnapshot(new ChartDataRequest
                { WindowStart = 17, WindowCount = 29, DownsampleMode = ChartDownsampleMode.None });
            Assert.Equal(2, snapshot.Series.Count);
            Assert.Equal(Enumerable.Range(17, 29).Select(i => i % 3 == 0 ? (double?)null : i), snapshot.Series[1].Values);
            Assert.Equal(Enumerable.Range(17, 29).Select(i => i * 10d), snapshot.Series[1].XValues);
            Assert.Null(snapshot.Series[1].SizeValues);
            Assert.Equal(Enumerable.Range(17, 29).Select(i => (double?)(i * 2)), snapshot.Series[0].Values);
        }

        private static DataGridChartModel Create(int count, ChartSeriesKind kind = ChartSeriesKind.Bubble)
        {
            DataGridChartModel model = new()
            {
                AutoRefresh = false, UseIncrementalUpdates = true,
                ItemsSource = Enumerable.Range(0, count).Select(i => new Row(i)).ToArray(),
                CategorySelector = static item => ((Row)item).Label,
                DownsampleMode = ChartDownsampleMode.None
            };
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Values", Kind = kind,
                ValueSelector = static item => ((Row)item).Value,
                XValueSelector = static item => ((Row)item).Index * 0.25,
                SizeSelector = static item => ((Row)item).Index % 7 + 1
            });
            return model;
        }

        private static void AssertWindow(ChartDataSnapshot snapshot, int start, int count)
        {
            Assert.Equal(count, snapshot.Categories.Count);
            ChartSeriesSnapshot series = Assert.Single(snapshot.Series);
            Assert.Equal(count, series.Values.Count);
            Assert.Equal(count, series.XValues!.Count);
            Assert.Equal(count, series.SizeValues!.Count);
            for (int i = 0; i < count; i++)
            {
                int original = start + i;
                Assert.Equal(original.ToString(CultureInfo.InvariantCulture), snapshot.Categories[i]);
                Assert.Equal(original * 2d, series.Values[i]);
                Assert.Equal(original * 0.25, series.XValues[i]);
                Assert.Equal(original % 7 + 1, series.SizeValues[i]);
            }
        }

        private sealed class Row
        {
            public Row(int index) { Index = index; Label = index.ToString(CultureInfo.InvariantCulture); }
            public int Index { get; }
            public double Value => Index * 2d;
            public string Label { get; }
        }
    }
}
