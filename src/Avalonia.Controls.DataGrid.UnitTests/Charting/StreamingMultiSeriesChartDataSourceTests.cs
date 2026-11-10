// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingMultiSeriesChartDataSourceTests
    {
        private static StreamingMultiSeriesChartDataSource Create(int capacity = 64, int seriesCount = 3)
        {
            StreamingChartSeries[] definitions = new StreamingChartSeries[seriesCount];
            for (int s = 0; s < seriesCount; s++) definitions[s] = new StreamingChartSeries($"Channel {s}");
            return new StreamingMultiSeriesChartDataSource(capacity, definitions);
        }

        [Fact]
        public void Validates_And_Copies_Schema_Before_Allocating_History()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(1));
            Assert.Throws<ArgumentNullException>(() => new StreamingMultiSeriesChartDataSource(2, null!));
            Assert.Throws<ArgumentException>(() => Create(2, 0));
            Assert.Throws<ArgumentException>(() => new StreamingMultiSeriesChartDataSource(2, new StreamingChartSeries[] { null! }));
            StreamingChartSeries original = new("Original");
            StreamingChartSeries[] input = { original };
            StreamingMultiSeriesChartDataSource source = new(2, input);
            input[0] = new("Changed");
            Assert.Same(original, source.Series[0]); Assert.Equal(1, source.SeriesCount); Assert.Equal(2, source.Capacity);
            Assert.Throws<NotSupportedException>(() => ((IList<StreamingChartSeries>)source.Series)[0] = input[0]);
            Assert.Throws<ArgumentNullException>(() => source.BuildView(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildView(new() { DownsampleMode = (ChartDownsampleMode)99 }));
        }

        [Theory]
        [InlineData(ChartSeriesKind.Column)]
        [InlineData(ChartSeriesKind.Candlestick)]
        [InlineData(ChartSeriesKind.RangeArea)]
        [InlineData(ChartSeriesKind.StackedArea)]
        [InlineData(ChartSeriesKind.Heatmap)]
        public void Rejects_Kinds_Not_Represented_By_Scalar_Aligned_Channels(ChartSeriesKind kind)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingChartSeries(kind: kind));

        [Fact]
        public void Metadata_And_Per_Series_Logarithmic_Validity_Are_Preserved()
        {
            ChartSeriesStyle style = new(); Func<double, string> formatter = value => value.ToString("F1");
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingChartSeries(valueAxisKind: ChartAxisKind.Category));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingChartSeries(valueAxisAssignment: (ChartValueAxisAssignment)99));
            StreamingMultiSeriesChartDataSource source = new(8, new[]
            {
                new StreamingChartSeries("Linear", ChartSeriesKind.Area),
                new StreamingChartSeries("Positive", ChartSeriesKind.Scatter, ChartValueAxisAssignment.Secondary,
                    style, formatter, ChartAxisKind.Logarithmic)
            });
            source.AppendRange(new double[] { 1, 2, 3, 4 }, new double?[] { -1, -1, 0, 0, 3, 3, double.NaN, double.PositiveInfinity });
            ChartDataSnapshot snapshot = source.BuildSnapshot(new());
            Assert.Equal(new double?[] { -1, 0, 3, null }, snapshot.Series[0].Values);
            Assert.Equal(new double?[] { null, null, 3, null }, snapshot.Series[1].Values);
            Assert.Equal(ChartSeriesKind.Area, snapshot.Series[0].Kind);
            Assert.Equal(ChartSeriesKind.Scatter, snapshot.Series[1].Kind);
            Assert.Equal(ChartValueAxisAssignment.Secondary, snapshot.Series[1].ValueAxisAssignment);
            Assert.Same(style, snapshot.Series[1].Style); Assert.Same(formatter, snapshot.Series[1].DataLabelFormatter);
            Assert.Equal(ChartAxisKind.Logarithmic, source.Series[1].ValueAxisKind);
            Assert.Equal("Positive", snapshot.Series[1].Name);
        }

        [Fact]
        public void Whole_Rows_Wrap_Together_And_Oversized_Batches_Keep_Only_Their_Chronological_Tail()
        {
            StreamingMultiSeriesChartDataSource source = Create(4, 2);
            int events = 0; source.DataInvalidated += (_, _) => events++;
            source.Append(0, new double?[] { 10, 20 }, "Zero");
            source.AppendRange(new double[] { 1, 2, 3 }, new double?[] { 11, 21, 12, 22, 13, 23 });
            source.Append(4, new double?[] { 14, 24 }, "Four");
            StreamingMultiSeriesChartView first = source.BuildView(new());
            Assert.Equal(new double[] { 1, 2, 3, 4 }, first.Snapshot.Series[0].XValues);
            Assert.Equal(new double?[] { 21, 22, 23, 24 }, first.Snapshot.Series[1].Values);
            double[] x = { 5, 6, 7, 8, 9, 10 };
            double?[] values = { 15, 25, 16, 26, 17, 27, 18, 28, 19, 29, 20, 30 };
            string?[] categories = { "5", "6", "7", "8", "9", "10" };
            source.AppendRange(x, values, categories);
            x[5] = -100; values[11] = -100; categories[5] = "Changed";
            StreamingMultiSeriesChartView view = source.BuildView(new());
            Assert.Equal(4, events); Assert.Equal(11, source.TotalSamples); Assert.Equal(7, source.FirstRetainedSampleIndex);
            Assert.Equal(4, source.GetTotalCategoryCount()); Assert.Equal(11, view.TotalSamples);
            Assert.Equal(new long[] { 7, 8, 9, 10 }, view.SourceSampleIndices);
            Assert.Equal(new double?[] { 17, 18, 19, 20 }, view.Snapshot.Series[0].Values);
            Assert.Equal(new double?[] { 27, 28, 29, 30 }, view.Snapshot.Series[1].Values);
            Assert.Equal("10", view.Snapshot.Categories[^1]);
            Assert.Equal(24, first.Snapshot.Series[1].Values[^1]);
            Assert.Same(view.Snapshot.Series[0].XValues, view.Snapshot.Series[1].XValues);
            Assert.Throws<NotSupportedException>(() => ((IList<double>)view.Snapshot.Series[0].XValues!)[0] = -1);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)view.Snapshot.Series[1].Values)[0] = -1);
            Assert.Throws<NotSupportedException>(() => ((IList<long>)view.SourceSampleIndices)[0] = -1);
            Assert.Throws<NotSupportedException>(() => ((IList<string?>)view.Snapshot.Categories)[0] = "Changed");
        }

        [Fact]
        public void Rejected_And_Empty_Batches_Preserve_Data_Counters_Cache_And_Events()
        {
            StreamingMultiSeriesChartDataSource source = Create(2, 2);
            source.Append(10, new double?[] { 1, 2 });
            ChartDataRequest request = new(); StreamingMultiSeriesChartView original = source.BuildView(request);
            int events = 0; source.DataInvalidated += (_, _) => events++;
            Assert.Throws<ArgumentException>(() => source.Append(11, new double?[] { 1 }));
            Assert.Throws<ArgumentException>(() => source.Append(10, new double?[] { 3, 4 }));
            Assert.Throws<ArgumentException>(() => source.Append(double.NaN, new double?[] { 3, 4 }));
            Assert.Throws<ArgumentException>(() => source.AppendRange(new double[] { 11, 12 }, new double?[] { 1, 2 }));
            Assert.Throws<ArgumentException>(() => source.AppendRange(new double[] { 11 }, new double?[] { 1, 2 }, new string?[2]));
            Assert.Throws<ArgumentException>(() => source.AppendRange(Array.Empty<double>(), new double?[] { 1 }));
            Assert.Throws<ArgumentException>(() => source.AppendRange(Array.Empty<double>(), Array.Empty<double?>(), new string?[] { "Bad" }));
            // The invalid prefix would be discarded by a capacity-two ring, but must still reject the whole batch.
            Assert.Throws<ArgumentException>(() => source.AppendRange(new double[] { double.NegativeInfinity, 11, 12, 13 }, new double?[8]));
            Assert.Throws<ArgumentException>(() => source.AppendRange(new double[] { 11, 11, 12, 13 }, new double?[8]));
            Assert.Throws<ArgumentException>(() => source.AppendRange(new double[] { 9, 11, 12, 13 }, new double?[8]));
            source.AppendRange(Array.Empty<double>(), Array.Empty<double?>());
            Assert.Same(original, source.BuildView(request)); Assert.Equal(0, events);
            Assert.Equal(1, source.TotalSamples); Assert.Equal(1, source.Count);
            source.Append(11, new double?[] { 3, 4 }); Assert.Equal(1, events);
            Assert.NotSame(original, source.BuildView(request));
        }

        [Fact]
        public void Clear_Releases_Rows_And_Restarts_Ordering_Without_Mutating_Old_Views()
        {
            StreamingMultiSeriesChartDataSource source = Create(2, 1);
            int events = 0; source.DataInvalidated += (_, _) => events++;
            source.Clear(); Assert.Equal(0, events);
            source.Append(8, new double?[] { 3 }, "Old");
            StreamingMultiSeriesChartView old = source.BuildView(new());
            source.Clear(); source.Clear(); Assert.Equal(2, events);
            StreamingMultiSeriesChartView empty = source.BuildView(new());
            Assert.Empty(empty.SourceSampleIndices); Assert.Empty(empty.Snapshot.Categories);
            Assert.Empty(empty.Snapshot.Series[0].Values); Assert.Equal(0, empty.TotalSamples);
            source.Append(-4, new double?[] { 7 }, "New");
            Assert.Equal(new long[] { 0 }, source.BuildView(new()).SourceSampleIndices);
            Assert.Equal(8, old.Snapshot.Series[0].XValues![0]); Assert.Equal("Old", old.Snapshot.Categories[0]);
        }

        [Theory]
        [InlineData(ChartDownsampleMode.None)]
        [InlineData(ChartDownsampleMode.Bucket)]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Adaptive)]
        public void Coordinated_Selection_Equals_The_Union_Of_Independent_Selections_And_Never_Bridges_Gaps(ChartDownsampleMode mode)
        {
            const int rows = 503, channels = 4, capacity = 317, start = 11, count = 281, budget = 17;
            StreamingMultiSeriesChartDataSource source = Create(capacity, channels);
            double[] x = new double[rows]; double?[] values = new double?[rows * channels];
            string?[] categories = new string?[rows]; Random random = new(92817);
            for (int row = 0; row < rows; row++)
            {
                x[row] = row * 1.25 + 7; categories[row] = $"R{row}";
                for (int s = 0; s < channels; s++)
                    values[row * channels + s] = (row + s * 13) % 79 < 4 ? null : random.NextDouble() * 100 - 50;
            }
            source.AppendRange(x, values, categories);
            StreamingMultiSeriesChartView view = source.BuildView(new()
                { WindowStart = start, WindowCount = count, MaxPoints = budget, DownsampleMode = mode });
            SortedSet<int> expected = new() { 0, count - 1 };
            int first = rows - capacity + start;
            for (int s = 0; s < channels; s++)
            {
                ChartSample[] independent = new ChartSample[count];
                for (int i = 0; i < count; i++) independent[i] = new(x[first + i], values[(first + i) * channels + s]);
                foreach (int index in ChartSampleDecimator.SelectIndices(independent, budget, mode)) expected.Add(index);
            }
            Assert.Equal(expected.Select(index => (long)first + index), view.SourceSampleIndices);
            Assert.Equal(start, view.WindowStart); Assert.Equal(count, view.WindowCount); Assert.Equal(rows, view.TotalSamples);
            for (int i = 0; i < view.SourceSampleIndices.Count; i++)
            {
                int row = (int)view.SourceSampleIndices[i];
                Assert.Equal(x[row], view.Snapshot.Series[0].XValues![i]); Assert.Equal(categories[row], view.Snapshot.Categories[i]);
                for (int s = 0; s < channels; s++)
                {
                    Assert.Same(view.Snapshot.Series[0].XValues, view.Snapshot.Series[s].XValues);
                    Assert.Equal(values[row * channels + s], view.Snapshot.Series[s].Values[i]);
                    if (i == 0 || !view.Snapshot.Series[s].Values[i - 1].HasValue || !view.Snapshot.Series[s].Values[i].HasValue) continue;
                    int priorRow = (int)view.SourceSampleIndices[i - 1];
                    for (int omitted = priorRow + 1; omitted < row; omitted++)
                        Assert.True(values[omitted * channels + s].HasValue, $"Channel {s} bridged missing row {omitted}.");
                }
            }
        }

        [Fact]
        public void Disjoint_Extrema_Exceed_The_Per_Series_Budget_And_All_Missing_Terminal_X_Is_Retained()
        {
            StreamingMultiSeriesChartDataSource source = Create(40, 3);
            double[] x = Enumerable.Range(0, 40).Select(i => (double)i).ToArray();
            double?[] data = new double?[120];
            for (int i = 0; i < 120; i++) data[i] = 10;
            for (int s = 0; s < 3; s++) { data[(4 + 5 * s) * 3 + s] = -100; data[(5 + 5 * s) * 3 + s] = 100; }
            source.AppendRange(x, data);
            StreamingMultiSeriesChartView reduced = source.BuildView(new() { MaxPoints = 4, DownsampleMode = ChartDownsampleMode.MinMax });
            Assert.True(reduced.SourceSampleIndices.Count > 4);
            for (int s = 0; s < 3; s++)
            {
                Assert.Contains((long)(4 + 5 * s), reduced.SourceSampleIndices);
                Assert.Contains((long)(5 + 5 * s), reduced.SourceSampleIndices);
            }
            source.Clear(); source.AppendRange(x, new double?[120]);
            StreamingMultiSeriesChartView missing = source.BuildView(new() { MaxPoints = 2 });
            Assert.Equal(new long[] { 0, 39 }, missing.SourceSampleIndices);
            Assert.All(missing.Snapshot.Series, series => Assert.All(series.Values, value => Assert.Null(value)));
        }

        [Fact]
        public void Equivalent_Normalized_Requests_Reuse_The_Same_Owned_View()
        {
            StreamingMultiSeriesChartDataSource source = Create(20, 1);
            for (int i = 0; i < 20; i++) source.Append(i, new double?[] { i });
            StreamingMultiSeriesChartView a = source.BuildView(new() { WindowStart = -10, WindowCount = 99, MaxPoints = 0 });
            Assert.Same(a, source.BuildView(new() { DownsampleMode = ChartDownsampleMode.None }));
            StreamingMultiSeriesChartView b = source.BuildView(new() { MaxPoints = 4, DownsampleMode = ChartDownsampleMode.Adaptive });
            Assert.Same(b, source.BuildView(new() { MaxPoints = 4, DownsampleMode = ChartDownsampleMode.MinMax }));
            StreamingMultiSeriesChartView c = source.BuildView(new() { MaxPoints = 1 });
            Assert.Same(c, source.BuildView(new() { MaxPoints = 2 }));
            StreamingMultiSeriesChartView empty = source.BuildView(new() { WindowStart = 50 });
            Assert.Equal(20, empty.WindowStart); Assert.Equal(0, empty.WindowCount);
            Assert.Same(empty, source.BuildView(new() { WindowStart = 20, WindowCount = -4 }));
        }

        [Fact]
        public void Notifications_Observe_All_Committed_Channels_And_Run_Outside_The_State_Lock()
        {
            StreamingMultiSeriesChartDataSource source = Create(4, 2);
            int calls = 0;
            source.DataInvalidated += (sender, _) =>
            {
                Assert.Same(source, sender); calls++;
                Task<ChartDataSnapshot> reader = Task.Run(() => source.BuildSnapshot(new()));
                Assert.True(reader.Wait(TimeSpan.FromSeconds(5)), "Source lock leaked across notification.");
                Assert.Equal(reader.Result.Series[0].Values[^1] * 2, reader.Result.Series[1].Values[^1]);
            };
            source.AppendRange(new double[] { 0, 1 }, new double?[] { 1, 2, 3, 6 }); Assert.Equal(1, calls);
            EventHandler failure = (_, _) => throw new InvalidOperationException("Subscriber failed after commit.");
            source.DataInvalidated += failure;
            Assert.Throws<InvalidOperationException>(() => source.Append(2, new double?[] { 5, 10 }));
            source.DataInvalidated -= failure;
            Assert.Equal(3, source.TotalSamples); Assert.Equal(10, source.BuildSnapshot(new()).Series[1].Values[^1]);
        }

        [Fact]
        public void Concurrent_Reads_Never_Observe_Partial_Rows_Or_Inconsistent_Capture_Metadata()
        {
            StreamingMultiSeriesChartDataSource source = Create(64, 3);
            using ManualResetEventSlim start = new();
            Task writer = Task.Run(() =>
            {
                double?[] row = new double?[3]; start.Wait();
                for (int i = 0; i < 3000; i++) { row[0] = i; row[1] = i * 2; row[2] = -i; source.Append(i, row); }
            });
            Task reader = Task.Run(() =>
            {
                ChartDataRequest request = new() { DownsampleMode = ChartDownsampleMode.None }; start.Wait();
                for (int pass = 0; pass < 1000; pass++)
                {
                    StreamingMultiSeriesChartView view = source.BuildView(request);
                    Assert.Equal(view.RetainedCount, view.SourceSampleIndices.Count);
                    for (int i = 0; i < view.SourceSampleIndices.Count; i++)
                    {
                        double x = view.Snapshot.Series[0].XValues![i];
                        Assert.Equal(x, view.Snapshot.Series[0].Values[i]);
                        Assert.Equal(x * 2, view.Snapshot.Series[1].Values[i]); Assert.Equal(-x, view.Snapshot.Series[2].Values[i]);
                        Assert.Equal((long)x, view.SourceSampleIndices[i]);
                    }
                    if (view.RetainedCount != 0) Assert.Equal(view.TotalSamples - 1, view.SourceSampleIndices[^1]);
                }
            });
            start.Set(); Assert.True(Task.WaitAll(new[] { writer, reader }, TimeSpan.FromSeconds(15)));
            Assert.Equal(3000, source.TotalSamples); Assert.Equal(64, source.Count);
        }

        [Fact]
        public void Competing_Duplicate_X_Producers_Commit_Only_One_Whole_Row()
        {
            StreamingMultiSeriesChartDataSource source = Create(2, 2); int accepted = 0, rejected = 0, events = 0;
            source.DataInvalidated += (_, _) => Interlocked.Increment(ref events);
            Parallel.For(0, 8, i =>
            {
                try { source.Append(1, new double?[] { i, -i }); Interlocked.Increment(ref accepted); }
                catch (ArgumentException) { Interlocked.Increment(ref rejected); }
            });
            Assert.Equal(1, accepted); Assert.Equal(7, rejected); Assert.Equal(1, events); Assert.Equal(1, source.TotalSamples);
            ChartDataSnapshot snapshot = source.BuildSnapshot(new());
            Assert.Equal(snapshot.Series[0].Values[0], -snapshot.Series[1].Values[0]);
        }

        [Fact]
        public void Warm_Appends_Batches_And_Cache_Hits_Allocate_No_Managed_Memory()
        {
            StreamingMultiSeriesChartDataSource source = Create(128, 4);
            double?[] row = { 1, 2, 3, 4 }; double[] batchX = new double[8]; double?[] cells = new double?[32];
            MeasureAppendAllocations(source, row, batchX, cells);
            Assert.Equal(0, MeasureAppendAllocations(source, row, batchX, cells));
            ChartDataRequest request = new() { MaxPoints = 16 };
            source.BuildView(request); MeasureCacheAllocations(source, request);
            Assert.Equal(0, MeasureCacheAllocations(source, request));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureAppendAllocations(StreamingMultiSeriesChartDataSource source, double?[] row, double[] x, double?[] cells)
        {
            double next = source.TotalSamples;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                source.Append(next++, row);
                for (int j = 0; j < x.Length; j++) x[j] = next++;
                source.AppendRange(x, cells);
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureCacheAllocations(StreamingMultiSeriesChartDataSource source, ChartDataRequest request)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++) source.BuildView(request);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}
