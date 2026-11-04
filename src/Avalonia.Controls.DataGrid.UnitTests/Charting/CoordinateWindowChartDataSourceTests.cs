// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class CoordinateWindowChartDataSourceTests
    {
        private static StreamingMultiSeriesChartDataSource Source(int capacity = 256) => new(capacity, new[]
        {
            new StreamingChartSeries("A", ChartSeriesKind.Scatter),
            new StreamingChartSeries("B", ChartSeriesKind.Scatter)
        });

        private static void Append(StreamingMultiSeriesChartDataSource source, int first, int count, double scale = 1)
        {
            double[] x = new double[count]; double?[] values = new double?[count * 2];
            for (int i = 0; i < count; i++)
            {
                int row = first + i; x[i] = row * scale;
                values[i * 2] = row % 11 == 3 ? null : row;
                values[i * 2 + 1] = row % 17 == 5 ? null : -row;
            }
            source.AppendRange(x, values);
        }

        [Fact]
        public void Policies_Are_Validated_Immutable_Values_And_Default_Selects_All()
        {
            Assert.Equal(ChartCoordinateWindow.All, default(ChartCoordinateWindow));
            Assert.Equal(ChartCoordinateWindowKind.All, ChartCoordinateWindow.All.Kind);
            Assert.Equal(ChartCoordinateWindow.Between(1, 2), ChartCoordinateWindow.Between(1, 2));
            Assert.NotEqual(ChartCoordinateWindow.Between(1, 2), ChartCoordinateWindow.Between(1, 2, true));
            Assert.Equal(ChartCoordinateWindow.Latest(0), ChartCoordinateWindow.Latest(-0d));
            Assert.NotEqual(ChartCoordinateWindow.Latest(0), ChartCoordinateWindow.All);
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinateWindow.Between(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinateWindow.Between(0, double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => ChartCoordinateWindow.Between(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinateWindow.Latest(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinateWindow.Latest(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinateWindow.Latest(double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new CoordinateWindowChartDataSource(null!));
        }

        [Theory]
        [InlineData(ChartDownsampleMode.None)]
        [InlineData(ChartDownsampleMode.Bucket)]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Adaptive)]
        public void Policies_And_Ordinal_Subwindows_Match_Existing_Source_APIs_At_Identical_Indices(ChartDownsampleMode mode)
        {
            StreamingMultiSeriesChartDataSource source = Source(137); Append(source, 0, 300, 0.25);
            using CoordinateWindowChartDataSource adapter = new(source);
            Random random = new(99327);
            foreach (ChartCoordinateWindow policy in new[]
            {
                ChartCoordinateWindow.All, ChartCoordinateWindow.Between(50, 65),
                ChartCoordinateWindow.Between(50.1, 65.1, true), ChartCoordinateWindow.Latest(17),
                ChartCoordinateWindow.Latest(0, true), ChartCoordinateWindow.Between(-10, -1, true)
            })
            {
                adapter.Window = policy;
                StreamingMultiSeriesChartView entire = Reference(source, policy, null, ChartDownsampleMode.None);
                Assert.Equal(entire.WindowCount, adapter.GetTotalCategoryCount());
                for (int pass = 0; pass < 35; pass++)
                {
                    int? offset = pass % 7 == 0 ? null : random.Next(-4, 155);
                    int? count = pass % 5 == 0 ? null : random.Next(-4, 155);
                    int skip = Math.Clamp(offset ?? 0, 0, entire.WindowCount);
                    int length = Math.Clamp(count ?? (entire.WindowCount - skip), 0, entire.WindowCount - skip);
                    ChartDataRequest request = new() { WindowStart = offset, WindowCount = count, MaxPoints = 9, DownsampleMode = mode };
                    var expected = source.BuildView(new()
                    {
                        WindowStart = entire.WindowStart + skip, WindowCount = length, MaxPoints = 9, DownsampleMode = mode
                    });
                    var actual = adapter.BuildView(request);
                    Assert.Same(expected, actual); Assert.Same(actual.Snapshot, adapter.BuildSnapshot(request));
                    Assert.Same(actual.Snapshot.Series[0].XValues, actual.Snapshot.Series[1].XValues);
                }
            }
        }

        [Fact]
        public void Neighbors_Precede_Ordinal_Windowing_And_Missing_Rows_Are_Not_Skipped()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            source.AppendRange(new double[] { 1, 3, 8, 15 }, new double?[] { 1, 2, null, null, 3, 4, 5, 6 });
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Between(4, 6, true));
            Assert.Equal(2, adapter.GetTotalCategoryCount());
            Assert.Equal(new double[] { 3, 8 }, adapter.BuildSnapshot(new()).Series[0].XValues);
            Assert.Null(adapter.BuildSnapshot(new()).Series[0].Values[0]);
            Assert.Equal(new double[] { 8 }, adapter.BuildSnapshot(new() { WindowStart = 1, WindowCount = 1 }).Series[0].XValues);
            adapter.Window = ChartCoordinateWindow.Between(20, 30, true);
            Assert.Equal(0, adapter.GetTotalCategoryCount()); Assert.Empty(adapter.BuildView(new()).SourceSampleIndices);
            adapter.Window = ChartCoordinateWindow.Latest(0);
            source.Append(20, new double?[] { null, null });
            Assert.Equal(new double[] { 20 }, adapter.BuildSnapshot(new()).Series[0].XValues);
            Assert.Null(adapter.BuildSnapshot(new()).Series[1].Values[0]);
        }

        [Fact]
        public void Trailing_Date_And_Extreme_Domains_Keep_Existing_Coordinate_Conventions()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            double origin = new DateTime(2026, 1, 1).ToOADate();
            source.AppendRange(new[] { origin, origin + 1, origin + 2, origin + 3 }, new double?[8]);
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Latest(1));
            Assert.Equal(new[] { origin + 2, origin + 3 }, adapter.BuildSnapshot(new()).Series[0].XValues);
            source.Clear(); source.Append(-double.MaxValue, new double?[] { 1, 2 });
            adapter.Window = ChartCoordinateWindow.Latest(double.MaxValue);
            Assert.Equal(-double.MaxValue, adapter.BuildSnapshot(new()).Series[0].XValues![0]);
            source.Append(double.MaxValue, new double?[] { 3, 4 });
            adapter.Window = ChartCoordinateWindow.Latest(double.Epsilon);
            Assert.Single(adapter.BuildSnapshot(new()).Series[0].Values);
            adapter.Window = ChartCoordinateWindow.Between(-double.MaxValue, double.MaxValue);
            Assert.Equal(2, adapter.GetTotalCategoryCount());
        }

        [Fact]
        public void Model_Follows_Latest_Coordinates_And_Ordinal_Navigation_Stays_Inside_The_Filter()
        {
            StreamingMultiSeriesChartDataSource source = Source(); Append(source, 0, 100);
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Latest(30));
            using ChartModel model = new() { DataSource = adapter };
            Assert.Equal(31, model.Snapshot.Categories.Count);
            Assert.True(model.ShowLatest(10));
            Assert.Equal(90, model.Snapshot.Series[0].XValues![0]);
            int changes = 0; model.SnapshotChanged += (_, _) => changes++;
            Append(source, 100, 11);
            Assert.Equal(1, changes); Assert.Equal(101, model.Snapshot.Series[0].XValues![0]);
            Assert.Equal(110, model.Snapshot.Series[0].XValues![^1]);
            adapter.Window = ChartCoordinateWindow.Between(20, 50);
            Assert.Equal(31, adapter.GetTotalCategoryCount());
            Assert.Equal(41, model.Snapshot.Series[0].XValues![0]); Assert.Equal(50, model.Snapshot.Series[0].XValues![^1]);
            Append(source, 111, 10);
            Assert.Equal(41, model.Snapshot.Series[0].XValues![0]); Assert.Equal(50, model.Snapshot.Series[0].XValues![^1]);
        }

        [Fact]
        public void Fixed_Window_Eviction_Changes_Only_Available_Rows_And_Old_Views_Remain_Owned()
        {
            StreamingMultiSeriesChartDataSource source = Source(20); Append(source, 0, 20);
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Between(5, 10));
            var original = adapter.BuildView(new());
            Append(source, 20, 30);
            Assert.Empty(adapter.BuildView(new()).SourceSampleIndices);
            Assert.Equal(Enumerable.Range(5, 6).Select(i => (long)i), original.SourceSampleIndices);
            Assert.Equal(new double[] { 5, 6, 7, 8, 9, 10 }, original.Snapshot.Series[0].XValues);
            Assert.Throws<NotSupportedException>(() => ((IList<long>)original.SourceSampleIndices)[0] = 100);
            source.Clear(); Append(source, 0, 12);
            Assert.Equal(original.SourceSampleIndices, adapter.BuildView(new()).SourceSampleIndices);
            Assert.NotEqual(original.Snapshot.Version, adapter.BuildSnapshot(new()).Version);
        }

        [Fact]
        public void Policy_Change_Is_Atomic_And_Callbacks_Run_Outside_Locks()
        {
            StreamingMultiSeriesChartDataSource source = Source(); Append(source, 0, 100);
            using CoordinateWindowChartDataSource adapter = new(source);
            int events = 0;
            adapter.DataInvalidated += (sender, _) =>
            {
                Assert.Same(adapter, sender); events++;
                Task<int?> reader = Task.Run(() => adapter.GetTotalCategoryCount());
                Assert.True(reader.Wait(TimeSpan.FromSeconds(5)), "Policy notification held a source/adapter lock.");
                Assert.NotNull(reader.Result);
            };
            adapter.Window = ChartCoordinateWindow.Latest(20); Assert.Equal(1, events);
            adapter.Window = ChartCoordinateWindow.Latest(20); Assert.Equal(1, events);
            Assert.Throws<ArgumentException>(() => adapter.Window = ChartCoordinateWindow.Between(10, -10));
            Assert.Equal(ChartCoordinateWindow.Latest(20), adapter.Window); Assert.Equal(1, events);
            Append(source, 100, 5); Assert.Equal(2, events);
            EventHandler failure = (_, _) => throw new InvalidOperationException("subscriber");
            adapter.DataInvalidated += failure;
            Assert.Throws<InvalidOperationException>(() => adapter.Window = ChartCoordinateWindow.All);
            Assert.Equal(ChartCoordinateWindow.All, adapter.Window);
            adapter.DataInvalidated -= failure;
            adapter.Window = ChartCoordinateWindow.Latest(1); Assert.Equal(2, adapter.GetTotalCategoryCount());
        }

        [Fact]
        public void Disposal_Detaches_And_Leaves_The_Underlying_Source_Usable()
        {
            StreamingMultiSeriesChartDataSource source = Source(); Append(source, 0, 5);
            CoordinateWindowChartDataSource adapter = new(source);
            int events = 0; EventHandler handler = (_, _) => events++;
            adapter.DataInvalidated += handler; adapter.Dispose(); adapter.Dispose();
            Append(source, 5, 5); Assert.Equal(0, events); Assert.True(adapter.IsDisposed); Assert.Equal(10, source.Count);
            adapter.DataInvalidated -= handler;
            Assert.Throws<ObjectDisposedException>(() => adapter.Window = ChartCoordinateWindow.All);
            Assert.Throws<ObjectDisposedException>(() => _ = adapter.Window);
            Assert.Throws<ObjectDisposedException>(() => adapter.DataInvalidated += handler);
            Assert.Throws<ObjectDisposedException>(() => adapter.BuildView(new()));
            Assert.Throws<ObjectDisposedException>(() => adapter.GetTotalCategoryCount());
        }

        [Fact]
        public void Source_Does_Not_Root_An_Abandoned_Coordinate_View()
        {
            StreamingMultiSeriesChartDataSource source = Source();
            WeakReference<CoordinateWindowChartDataSource> weak = Abandon(source);
            for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            Assert.False(weak.TryGetTarget(out _));
            source.Append(1, new double?[] { 1, 2 }); GC.KeepAlive(source);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference<CoordinateWindowChartDataSource> Abandon(StreamingMultiSeriesChartDataSource source)
        {
            CoordinateWindowChartDataSource adapter = new(source);
            adapter.DataInvalidated += static (_, _) => throw new InvalidOperationException("Abandoned view was retained.");
            return new(adapter);
        }

        [Fact]
        public void Warm_Capture_And_Count_Allocate_Nothing_And_Counting_Does_Not_Replace_The_Cache()
        {
            StreamingMultiSeriesChartDataSource source = Source(1000); Append(source, 0, 2000);
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Latest(300));
            ChartDataRequest request = new() { MaxPoints = 64 };
            var view = adapter.BuildView(request);
            Measure(adapter, request); Assert.Same(view, adapter.BuildView(request));
            long bytes = Measure(adapter, request);
            Assert.Equal(0, bytes); Assert.Same(view, adapter.BuildView(request));
            Assert.Throws<ArgumentNullException>(() => adapter.BuildView(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => adapter.BuildView(new() { DownsampleMode = (ChartDownsampleMode)99 }));
            Assert.Same(view, adapter.BuildView(request));
            Assert.Empty(adapter.BuildView(new() { WindowStart = int.MaxValue, WindowCount = int.MaxValue }).SourceSampleIndices);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Measure(CoordinateWindowChartDataSource adapter, ChartDataRequest request)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { adapter.BuildView(request); adapter.GetTotalCategoryCount(); }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public async Task Concurrent_Eviction_Preserves_One_Captured_Trailing_Domain_And_Identity_Map()
        {
            StreamingMultiSeriesChartDataSource source = Source(128); Append(source, 0, 200, 0.25);
            using CoordinateWindowChartDataSource adapter = new(source, ChartCoordinateWindow.Latest(4));
            Task writer = Task.Run(() => { for (int i = 200; i < 4000; i++) source.Append(i * 0.25, new double?[] { i, -i }); });
            for (int pass = 0; pass < 150; pass++)
            {
                var view = adapter.BuildView(new() { MaxPoints = 8 });
                var x = view.Snapshot.Series[0].XValues!;
                Assert.NotEmpty(x);
                for (int i = 0; i < x.Count; i++)
                {
                    Assert.InRange(x[i], x[^1] - 4, x[^1]);
                    Assert.Equal(view.SourceSampleIndices[i] * 0.25, x[i]);
                    Assert.InRange(view.SourceSampleIndices[i], view.FirstRetainedSampleIndex, view.TotalSamples - 1);
                }
                Assert.Equal(view.TotalSamples - 1, view.SourceSampleIndices[^1]);
            }
            await writer;
        }

        private static StreamingMultiSeriesChartView Reference(StreamingMultiSeriesChartDataSource source,
            ChartCoordinateWindow policy, int? budget, ChartDownsampleMode mode) => policy.Kind switch
        {
            ChartCoordinateWindowKind.Fixed => source.BuildViewByX(policy.MinimumX, policy.MaximumX, budget, mode, policy.IncludeBoundaryNeighbors),
            ChartCoordinateWindowKind.Latest => source.BuildLatestViewByX(policy.Span, budget, mode, policy.IncludeBoundaryNeighbors),
            _ => source.BuildView(new() { MaxPoints = budget, DownsampleMode = mode })
        };
    }
}
