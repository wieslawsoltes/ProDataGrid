// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingMultiSeriesXRangeTests
    {
        private static StreamingMultiSeriesChartDataSource Create(int capacity = 64)
            => new(capacity, new[]
            {
                new StreamingChartSeries("Signal", ChartSeriesKind.Scatter),
                new StreamingChartSeries("Independent gaps", ChartSeriesKind.Scatter)
            });

        [Theory]
        [InlineData(-20, -10, false, 0, 0)]
        [InlineData(-20, -10, true, 0, 0)]
        [InlineData(101, 200, false, 5, 0)]
        [InlineData(101, 200, true, 5, 0)]
        [InlineData(2, 9, false, 1, 0)]
        [InlineData(2, 9, true, 0, 2)]
        [InlineData(10, 10, false, 1, 1)]
        [InlineData(10, 10, true, 0, 3)]
        [InlineData(10, 30, false, 1, 3)]
        [InlineData(10, 30, true, 0, 5)]
        [InlineData(1, 1, true, 0, 2)]
        [InlineData(100, 100, true, 3, 2)]
        [InlineData(-10, 200, true, 0, 5)]
        public void Inclusive_Bounds_And_Neighbors_Are_Resolved_On_Nonuniform_X(
            double minimum, double maximum, bool neighbors, int start, int count)
        {
            StreamingMultiSeriesChartDataSource source = Create();
            double[] x = { 1, 10, 11, 30, 100 };
            source.AppendRange(x, new double?[] { 1, 10, 2, null, null, 30, 4, 40, 5, null });
            StreamingMultiSeriesChartView actual = source.BuildViewByX(minimum, maximum, includeBoundaryNeighbors: neighbors);
            Assert.Equal(start, actual.WindowStart);
            Assert.Equal(count, actual.WindowCount);
            Assert.Equal(x.Skip(start).Take(count), actual.Snapshot.Series[0].XValues);
            Assert.Equal(Enumerable.Range(start, count).Select(i => (long)i), actual.SourceSampleIndices);
            Assert.Same(actual.Snapshot.Series[0].XValues, actual.Snapshot.Series[1].XValues);
            Assert.Same(actual, source.BuildView(new() { WindowStart = start, WindowCount = count }));
            Assert.Same(actual.Snapshot, source.BuildSnapshotByX(minimum, maximum, includeBoundaryNeighbors: neighbors));
        }

        [Fact]
        public void Empty_And_Single_Row_Sources_Do_Not_Invent_Boundary_Data()
        {
            StreamingMultiSeriesChartDataSource source = Create(2);
            StreamingMultiSeriesChartView empty = source.BuildViewByX(-1, 1, includeBoundaryNeighbors: true);
            Assert.Equal(0, empty.WindowStart); Assert.Equal(0, empty.WindowCount);
            Assert.Empty(empty.SourceSampleIndices); Assert.Equal(2, empty.Snapshot.Series.Count);
            source.Append(0, new double?[] { null, null });
            StreamingMultiSeriesChartView point = source.BuildViewByX(0, 0, 1, includeBoundaryNeighbors: true);
            Assert.Single(point.SourceSampleIndices); Assert.Equal(0, point.SourceSampleIndices[0]);
            Assert.Null(point.Snapshot.Series[0].Values[0]);
            Assert.Empty(source.BuildViewByX(0.1, 1, includeBoundaryNeighbors: true).SourceSampleIndices);
        }

        [Fact]
        public void Extreme_Finite_Bounds_And_Signed_Zero_Do_Not_Overflow_Search()
        {
            StreamingMultiSeriesChartDataSource source = Create();
            source.AppendRange(new[] { -double.MaxValue, -0.0, double.Epsilon, double.MaxValue },
                new double?[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.Equal(4, source.BuildViewByX(-double.MaxValue, double.MaxValue).WindowCount);
            Assert.Equal(new long[] { 1 }, source.BuildViewByX(+0.0, -0.0).SourceSampleIndices);
            Assert.Equal(new long[] { 2 }, source.BuildViewByX(double.Epsilon, double.Epsilon).SourceSampleIndices);
            Assert.Equal(new long[] { 3 }, source.BuildViewByX(double.MaxValue, double.MaxValue).SourceSampleIndices);
        }

        [Fact]
        public void Date_Coordinates_Use_The_Same_Inclusive_Search_Without_Conversion()
        {
            StreamingMultiSeriesChartDataSource source = Create();
            DateTime day = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double[] x = { day.ToOADate(), day.AddMinutes(1).ToOADate(), day.AddHours(4).ToOADate(), day.AddDays(1).ToOADate() };
            source.AppendRange(x, new double?[] { 1, 10, 2, 20, 3, 30, 4, 40 });
            StreamingMultiSeriesChartView view = source.BuildViewByX(x[1], x[2]);
            Assert.Equal(new[] { x[1], x[2] }, view.Snapshot.Series[0].XValues);
            Assert.Equal(new long[] { 1, 2 }, view.SourceSampleIndices);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(17)]
        [InlineData(64)]
        [InlineData(257)]
        public void Wrapped_Ring_Search_Matches_A_Linear_Oracle(int capacity)
        {
            StreamingMultiSeriesChartDataSource source = Create(capacity);
            double x = -1000;
            Random random = new(711 + capacity);
            for (int i = 0; i < capacity * 3 + 1; i++)
            {
                x += 0.25 + random.Next(1, 20);
                source.Append(x, new double?[] { i % 5 == 0 ? null : i, i % 7 == 0 ? null : -i }, i.ToString());
            }
            StreamingMultiSeriesChartView full = source.BuildView(new());
            IReadOnlyList<double> coordinates = full.Snapshot.Series[0].XValues!;
            for (int query = 0; query < 400; query++)
            {
                double minimum = coordinates[0] - 20 + random.NextDouble() * (coordinates[^1] - coordinates[0] + 40);
                double maximum = minimum + random.NextDouble() * 120;
                bool neighbors = (query & 1) != 0;
                int start = 0, end = 0;
                while (start < coordinates.Count && coordinates[start] < minimum) start++;
                while (end < coordinates.Count && coordinates[end] <= maximum) end++;
                if (neighbors && maximum >= coordinates[0] && minimum <= coordinates[^1])
                {
                    if (start > 0) start--;
                    if (end < coordinates.Count) end++;
                }
                StreamingMultiSeriesChartView actual = source.BuildViewByX(minimum, maximum, includeBoundaryNeighbors: neighbors);
                Assert.Equal(start, actual.WindowStart); Assert.Equal(end - start, actual.WindowCount);
                Assert.Equal(full.FirstRetainedSampleIndex, actual.FirstRetainedSampleIndex);
                Assert.Equal(full.TotalSamples, actual.TotalSamples);
                for (int i = 0; i < actual.WindowCount; i++)
                {
                    Assert.Equal(full.SourceSampleIndices[start + i], actual.SourceSampleIndices[i]);
                    Assert.Equal(coordinates[start + i], actual.Snapshot.Series[0].XValues![i]);
                    Assert.Equal(full.Snapshot.Categories[start + i], actual.Snapshot.Categories[i]);
                    for (int s = 0; s < 2; s++)
                        Assert.Equal(full.Snapshot.Series[s].Values[start + i], actual.Snapshot.Series[s].Values[i]);
                }
            }
        }

        [Theory]
        [InlineData(ChartDownsampleMode.None)]
        [InlineData(ChartDownsampleMode.Bucket)]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Adaptive)]
        public void Coordinate_Reduction_Reuses_Ordinal_Selection_And_Common_Identities(ChartDownsampleMode mode)
        {
            StreamingMultiSeriesChartDataSource source = Create(128);
            AppendRows(source, 0, 300);
            StreamingMultiSeriesChartView expected = source.BuildView(new()
            {
                WindowStart = 20, WindowCount = 80, MaxPoints = 8, DownsampleMode = mode
            });
            double minimum = (source.FirstRetainedSampleIndex + 21) * 0.5;
            double maximum = (source.FirstRetainedSampleIndex + 98) * 0.5;
            StreamingMultiSeriesChartView actual = source.BuildViewByX(minimum, maximum, 8, mode, true);
            Assert.Same(expected, actual);
            Assert.Equal(80, actual.WindowCount);
            Assert.Same(actual.Snapshot.Series[0].XValues, actual.Snapshot.Series[1].XValues);
            for (int i = 0; i < actual.SourceSampleIndices.Count; i++)
            {
                long row = actual.SourceSampleIndices[i];
                Assert.Equal(row * 0.5, actual.Snapshot.Series[0].XValues![i]);
                Assert.Equal((double)row, actual.Snapshot.Series[0].Values[i]);
                Assert.Equal(row % 7 == 0 ? (double?)null : -row, actual.Snapshot.Series[1].Values[i]);
            }
        }

        [Theory]
        [InlineData(double.NaN, 10)]
        [InlineData(0, double.NaN)]
        [InlineData(double.NegativeInfinity, 10)]
        [InlineData(0, double.PositiveInfinity)]
        [InlineData(20, 10)]
        public void Invalid_Bounds_Do_Not_Invalidate_Cache_Or_Notify(double minimum, double maximum)
        {
            StreamingMultiSeriesChartDataSource source = Create(); AppendRows(source, 0, 20);
            StreamingMultiSeriesChartView cached = source.BuildViewByX(2, 5);
            int events = 0; source.DataInvalidated += (_, _) => events++;
            Assert.ThrowsAny<ArgumentException>(() => source.BuildViewByX(minimum, maximum));
            Assert.ThrowsAny<ArgumentException>(() => source.BuildSnapshotByX(minimum, maximum));
            Assert.Same(cached, source.BuildViewByX(2, 5));
            Assert.Equal(20, source.TotalSamples); Assert.Equal(0, events);
        }

        [Fact]
        public void Invalid_Mode_And_Rejected_Appends_Leave_The_Coordinate_Cache_Intact()
        {
            StreamingMultiSeriesChartDataSource source = Create(); AppendRows(source, 0, 20);
            StreamingMultiSeriesChartView cached = source.BuildViewByX(2, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildViewByX(2, 5, downsampleMode: (ChartDownsampleMode)99));
            Assert.Throws<ArgumentException>(() => source.Append(1, new double?[] { 1, 2 }));
            Assert.Same(cached, source.BuildViewByX(2, 5, -1));
            Assert.Same(cached.Snapshot, source.BuildSnapshotByX(2, 5, 1, ChartDownsampleMode.None));
        }

        [Fact]
        public void Views_Remain_Owned_After_Eviction_And_Clear_Requeries_The_New_Session()
        {
            StreamingMultiSeriesChartDataSource source = Create(8); AppendRows(source, 0, 20);
            StreamingMultiSeriesChartView original = source.BuildViewByX(6, 8);
            Assert.Equal(new long[] { 12, 13, 14, 15, 16 }, original.SourceSampleIndices);
            AppendRows(source, 20, 20);
            Assert.Empty(source.BuildViewByX(6, 8, includeBoundaryNeighbors: true).SourceSampleIndices);
            Assert.Equal(5, original.SourceSampleIndices.Count); Assert.Equal(20, original.TotalSamples);
            source.Clear();
            Assert.Equal(0, source.BuildViewByX(6, 8).TotalSamples);
            AppendRows(source, 0, 20);
            StreamingMultiSeriesChartView next = source.BuildViewByX(6, 8);
            Assert.NotSame(original, next); Assert.Equal(original.SourceSampleIndices, next.SourceSampleIndices);
            Assert.Equal(new double[] { 6, 6.5, 7, 7.5, 8 }, original.Snapshot.Series[0].XValues);
        }

        [Fact]
        public void Equivalent_Warm_Queries_Allocate_No_Managed_Memory()
        {
            StreamingMultiSeriesChartDataSource source = Create(8192); AppendRows(source, 0, 10000);
            StreamingMultiSeriesChartView expected = source.BuildViewByX(4500, 4520, 8);
            StreamingMultiSeriesChartView actual = expected;
            for (int i = 0; i < 200; i++) actual = source.BuildViewByX(4499.9, 4520.1, 8);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) actual = source.BuildViewByX(4500, 4520, 8);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Same(expected, actual); Assert.Equal(0, allocated);
        }

        [Fact]
        public async Task Concurrent_Eviction_Cannot_Tear_Coordinate_Window_And_Identity_Metadata()
        {
            StreamingMultiSeriesChartDataSource source = Create(256);
            AppendRows(source, 0, 256);
            using Barrier ready = new(3);
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            Task producer = Task.Run(() =>
            {
                ready.SignalAndWait(cancellation);
                for (int row = 256; row < 2000; row++)
                {
                    source.Append(row * 0.5, new double?[] { row, row % 7 == 0 ? null : -row });
                    if ((row & 15) == 0) Thread.Yield();
                }
            }, cancellation);
            Task Reader() => Task.Run(() =>
            {
                ready.SignalAndWait(cancellation);
                for (int iteration = 0; iteration < 400; iteration++)
                {
                    StreamingMultiSeriesChartView view = source.BuildViewByX(50, 850);
                    int start = (int)Math.Clamp(100 - view.FirstRetainedSampleIndex, 0, view.RetainedCount);
                    int end = (int)Math.Clamp(1701 - view.FirstRetainedSampleIndex, 0, view.RetainedCount);
                    Assert.Equal(start, view.WindowStart); Assert.Equal(end - start, view.WindowCount);
                    for (int i = 0; i < view.SourceSampleIndices.Count; i++)
                    {
                        long row = view.FirstRetainedSampleIndex + start + i;
                        Assert.Equal(row, view.SourceSampleIndices[i]);
                        Assert.Equal(row * 0.5, view.Snapshot.Series[0].XValues![i]);
                        Assert.Equal((double)row, view.Snapshot.Series[0].Values[i]);
                        Assert.Equal(row % 7 == 0 ? (double?)null : -row, view.Snapshot.Series[1].Values[i]);
                    }
                    Thread.Yield();
                }
            }, cancellation);
            await Task.WhenAll(producer, Reader(), Reader());
            Assert.Equal(2000, source.TotalSamples);
        }

        private static void AppendRows(StreamingMultiSeriesChartDataSource source, int start, int count)
        {
            double[] x = new double[count]; double?[] values = new double?[count * 2];
            for (int i = 0; i < count; i++)
            {
                int row = start + i; x[i] = row * 0.5;
                values[i * 2] = row; values[i * 2 + 1] = row % 7 == 0 ? null : -row;
            }
            source.AppendRange(x, values);
        }
    }
}
