// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingMultiSeriesLatestXRangeTests
    {
        private static StreamingMultiSeriesChartDataSource Source(int capacity = 64) => new(capacity, new[]
        {
            new StreamingChartSeries("First", ChartSeriesKind.Scatter),
            new StreamingChartSeries("Second", ChartSeriesKind.Scatter)
        });

        [Theory]
        [InlineData(0, false, 4, 1)]
        [InlineData(0, true, 3, 2)]
        [InlineData(1, false, 4, 1)]
        [InlineData(1, true, 3, 2)]
        [InlineData(10, false, 3, 2)]
        [InlineData(10, true, 2, 3)]
        [InlineData(100, false, 0, 5)]
        public void Latest_Window_Uses_Inclusive_X_Distance_Not_Row_Count(double span, bool neighbors, int start, int count)
        {
            StreamingMultiSeriesChartDataSource source = Source();
            source.AppendRange(new double[] { 2, 3, 7, 11, 21 }, new double?[] { 1, 2, 3, 4, 5, 6, 7, 8, null, null });
            var view = source.BuildLatestViewByX(span, includeBoundaryNeighbors: neighbors);
            Assert.Equal(start, view.WindowStart); Assert.Equal(count, view.WindowCount);
            Assert.Equal(Enumerable.Range(start, count).Select(i => (long)i), view.SourceSampleIndices);
            Assert.Equal(21, view.Snapshot.Series[0].XValues![^1]);
            Assert.Null(view.Snapshot.Series[0].Values[^1]);
            Assert.Same(view.Snapshot, source.BuildLatestSnapshotByX(span, includeBoundaryNeighbors: neighbors));
        }

        [Theory]
        [InlineData(ChartDownsampleMode.None)]
        [InlineData(ChartDownsampleMode.Bucket)]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Adaptive)]
        public void Seeded_Wrapped_Queries_Match_Independent_Linear_Window_Resolution(ChartDownsampleMode mode)
        {
            Random random = new(67019);
            StreamingMultiSeriesChartDataSource source = Source(127);
            double[] coordinates = new double[700];
            double?[] values = new double?[2];
            double x = -170;
            for (int i = 0; i < coordinates.Length; i++)
            {
                x += 0.05 + random.NextDouble() * 3;
                coordinates[i] = x;
                values[0] = i % 31 < 3 ? null : Math.Sin(i * 0.13);
                values[1] = i % 47 < 4 ? null : Math.Cos(i * 0.21);
                source.Append(x, values);
            }
            double[] retained = coordinates.Skip(coordinates.Length - source.Capacity).ToArray();
            for (int pass = 0; pass < 100; pass++)
            {
                double span = random.NextDouble() * 260;
                bool neighbors = pass % 2 == 0;
                double minimum = retained[^1] - span;
                int start = 0;
                while (start < retained.Length && retained[start] < minimum) start++;
                if (neighbors && start > 0) start--;
                ChartDataRequest request = new() { WindowStart = start, WindowCount = retained.Length - start,
                    MaxPoints = 12, DownsampleMode = mode };
                var expected = source.BuildView(request);
                var actual = source.BuildLatestViewByX(span, 12, mode, neighbors);
                // Equivalent normalized requests must share the exact view, including the common gap-aware union.
                Assert.Same(expected, actual);
                Assert.Equal(699L, actual.SourceSampleIndices[^1]);
                Assert.Equal(retained[^1], actual.Snapshot.Series[0].XValues![^1]);
            }
        }

        [Fact]
        public void Empty_And_Single_Row_History_Honor_Validation_And_Ownership()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            Assert.Empty(source.BuildLatestViewByX(0).SourceSampleIndices);
            Assert.Empty(source.BuildLatestSnapshotByX(10).Categories);
            source.Append(10, new double?[] { 2, 3 });
            var old = source.BuildLatestViewByX(0, includeBoundaryNeighbors: true);
            Assert.Equal(new long[] { 0 }, old.SourceSampleIndices);
            source.Clear(); source.Append(-5, new double?[] { 4, 5 });
            var next = source.BuildLatestViewByX(0);
            Assert.Equal(10, old.Snapshot.Series[0].XValues![0]);
            Assert.Equal(-5, next.Snapshot.Series[0].XValues![0]);
            Assert.NotEqual(old.Snapshot.Version, next.Snapshot.Version);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Invalid_Spans_Do_Not_Change_Data_Events_Or_Cached_View(double span)
        {
            StreamingMultiSeriesChartDataSource source = Source();
            source.Append(0, new double?[] { 1, 2 });
            var before = source.BuildLatestViewByX(10);
            int events = 0; source.DataInvalidated += (_, _) => events++;
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildLatestViewByX(span));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildLatestSnapshotByX(span));
            Assert.Same(before, source.BuildLatestViewByX(10));
            Assert.Equal(0, events); Assert.Equal(1, source.TotalSamples);
        }

        [Fact]
        public void Invalid_Mode_Is_Rejected_Even_For_Empty_History()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildLatestViewByX(1, downsampleMode: (ChartDownsampleMode)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildLatestViewByX(1, downsampleMode: (ChartDownsampleMode)(-1)));
        }

        [Fact]
        public void Overflowing_Lower_Bound_Includes_All_Eligible_Finite_Data_And_SubUlp_Spans_Are_Explicit()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            source.Append(-double.MaxValue, new double?[] { 1, 2 });
            source.Append(-double.MaxValue / 2, new double?[] { 2, 3 });
            Assert.Equal(2, source.BuildLatestViewByX(double.MaxValue).WindowCount);
            Assert.Equal(1, source.BuildLatestViewByX(double.Epsilon).WindowCount);
            source.Clear();
            source.AppendRange(new double[] { -double.MaxValue, -1, 0, double.MaxValue },
                new double?[] { 1, 1, 2, 2, 3, 3, 4, 4 });
            Assert.Equal(new long[] { 2, 3 }, source.BuildLatestViewByX(double.MaxValue).SourceSampleIndices);
            Assert.Equal(new long[] { 1, 2, 3 }, source.BuildLatestViewByX(double.MaxValue, includeBoundaryNeighbors: true).SourceSampleIndices);
        }

        [Fact]
        public void Encoded_Date_Window_Uses_Explicit_Day_Units()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            double start = new DateTime(2026, 9, 1).ToOADate();
            source.AppendRange(new[] { start, start + 0.25, start + 1, start + 1.5, start + 2 },
                new double?[] { 1, 2, 2, 3, 3, 4, 4, 5, 5, 6 });
            var actual = source.BuildLatestViewByX(0.5);
            Assert.Equal(new long[] { 3, 4 }, actual.SourceSampleIndices);
            Assert.Same(source.BuildViewByX(start + 1.5, start + 2), actual);
        }

        [Fact]
        public async Task Latest_Anchor_And_Rows_Are_Captured_Atomically_During_Eviction()
        {
            StreamingMultiSeriesChartDataSource source = Source(128);
            source.Append(0, new double?[] { 0, 1 });
            Task writer = Task.Run(() =>
            {
                double?[] row = new double?[2];
                for (int i = 1; i < 5000; i++)
                {
                    row[0] = i; row[1] = i + 1;
                    source.Append(i * 0.5, row);
                }
            });
            for (int pass = 0; pass < 500; pass++)
            {
                var view = source.BuildLatestViewByX(16, downsampleMode: ChartDownsampleMode.None);
                long last = view.TotalSamples - 1;
                long first = Math.Max(view.FirstRetainedSampleIndex, last - 32);
                Assert.Equal(last, view.SourceSampleIndices[^1]);
                Assert.Equal(first, view.SourceSampleIndices[0]);
                Assert.Equal(last - first + 1, view.WindowCount);
                for (int i = 0; i < view.SourceSampleIndices.Count; i++)
                {
                    long original = view.SourceSampleIndices[i];
                    Assert.Equal(original * 0.5, view.Snapshot.Series[0].XValues![i]);
                    Assert.Equal((double)original, view.Snapshot.Series[0].Values[i]);
                    Assert.Equal((double)original + 1, view.Snapshot.Series[1].Values[i]);
                }
            }
            await writer;
            Assert.Equal(4999L, source.BuildLatestViewByX(16).SourceSampleIndices[^1]);
        }

        [Fact]
        public void Warm_Latest_Queries_Reuse_View_Without_Allocation_And_Appends_Reanchor_Them()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            double?[] values = new double?[2];
            for (int i = 0; i < 100; i++) source.Append(i, values);
            var before = source.BuildLatestViewByX(16, 12);
            MeasureQueries(source, out _);
            long bytes = MeasureQueries(source, out long count);
            Assert.Equal(0, bytes); Assert.Equal(before.TotalSamples * 1000, count);
            source.Append(110, values);
            var after = source.BuildLatestViewByX(16, 12);
            Assert.NotSame(before, after);
            Assert.Equal(99, before.Snapshot.Series[0].XValues![^1]);
            Assert.Equal(110, after.Snapshot.Series[0].XValues![^1]);
            Assert.Same(after, source.BuildViewByX(94, 110, 12));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureQueries(StreamingMultiSeriesChartDataSource source, out long count)
        {
            count = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) count += source.BuildLatestViewByX(16, 12).TotalSamples;
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
