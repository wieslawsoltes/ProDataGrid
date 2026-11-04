// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingRangeChartTests
    {
        [Fact]
        public void Wraparound_Preserves_Original_Stream_Identity_And_Owned_Views()
        {
            StreamingRangeChartDataSource source = new(3, "Live");
            source.AppendRange(new[] { Sample(0), Sample(1), Sample(2) });
            StreamingRangeChartView old = source.BuildView(new());
            source.Append(Sample(3)); source.Append(Sample(4));
            StreamingRangeChartView view = source.BuildView(new());
            Assert.Equal(new long[] { 2, 3, 4 }, view.SourceSampleIndices);
            Assert.Equal(new double[] { 2, 3, 4 }, view.Snapshot.Series[0].XValues);
            Assert.Equal(new string?[] { "2", "3", "4" }, view.Snapshot.Categories);
            Assert.Equal(2, view.FirstRetainedSampleIndex); Assert.Equal(5, view.TotalSamples);
            Assert.Equal(3, source.GetTotalCategoryCount()); Assert.Equal(3, source.Capacity);
            Assert.Equal(new long[] { 0, 1, 2 }, old.SourceSampleIndices);
            Assert.Equal(new double?[] { 1, 2, 3 }, old.Snapshot.Series[0].Values);
            Assert.NotEqual(old.Snapshot.Version, view.Snapshot.Version);
            Assert.Throws<NotSupportedException>(() => ((IList<long>)view.SourceSampleIndices)[0] = 99);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)view.Snapshot.Series[0].LowValues!)[0] = 99);
            Assert.Throws<NotSupportedException>(() => ((IList<string?>)view.Snapshot.Categories)[0] = "changed");
        }

        [Fact]
        public void Oversized_Batches_Validate_Even_Discarded_Entries_And_Raise_One_Event()
        {
            StreamingRangeChartDataSource source = new(4);
            int events = 0;
            source.DataInvalidated += (_, _) => events++;
            source.AppendRange(Enumerable.Range(0, 100).Select(Sample).ToArray());
            Assert.Equal(1, events); Assert.Equal(100, source.TotalSamples);
            Assert.Equal(new long[] { 96, 97, 98, 99 }, source.BuildView(new()).SourceSampleIndices);
            StreamingRangeChartView old = source.BuildView(new());
            ChartRangeSample[] invalid = Enumerable.Range(100, 20).Select(Sample).ToArray();
            invalid[1] = new ChartRangeSample(101, 9, 2);
            Assert.Throws<ArgumentException>(() => source.AppendRange(invalid));
            Assert.Same(old, source.BuildView(new())); Assert.Equal(1, events);
            invalid[1] = new ChartRangeSample(double.NaN, 1, 2);
            Assert.Throws<ArgumentException>(() => source.AppendRange(invalid));
            Assert.Same(old, source.BuildView(new()));
            source.AppendRange(Array.Empty<ChartRangeSample>()); Assert.Equal(1, events);
            source.Append(Sample(100));
            Assert.Equal(new long[] { 97, 98, 99, 100 }, source.BuildView(new()).SourceSampleIndices);
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(10, 9)]
        [InlineData(double.NaN, 20)]
        [InlineData(11, double.PositiveInfinity)]
        public void Invalid_Coordinate_Batches_Are_Atomic(double first, double second)
        {
            StreamingRangeChartDataSource source = new(8);
            source.Append(Sample(10));
            StreamingRangeChartView old = source.BuildView(new());
            Assert.Throws<ArgumentException>(() => source.AppendRange(new[]
                { new ChartRangeSample(first, 1, 2), new ChartRangeSample(second, 1, 2) }));
            Assert.Same(old, source.BuildView(new())); Assert.Equal(1, source.TotalSamples);
        }

        [Fact]
        public void Clear_Starts_A_New_Sequence_And_Resets_X_Order_Without_Changing_Old_Views()
        {
            StreamingRangeChartDataSource source = new(3);
            source.AppendRange(new[] { Sample(10), Sample(11), Sample(12), Sample(13) });
            StreamingRangeChartView old = source.BuildView(new());
            source.Clear(); source.Clear();
            Assert.Equal(0, source.TotalSamples); Assert.Equal(0, source.FirstRetainedSampleIndex);
            Assert.Empty(source.BuildView(new()).SourceSampleIndices);
            source.Append(Sample(-5));
            Assert.Equal(new long[] { 0 }, source.BuildView(new()).SourceSampleIndices);
            Assert.Equal(new double[] { -5 }, source.BuildSnapshot(new()).Series[0].XValues);
            Assert.Equal(new long[] { 1, 2, 3 }, old.SourceSampleIndices);
            Assert.Equal(new double[] { 11, 12, 13 }, old.Snapshot.Series[0].XValues);
        }

        [Fact]
        public void Logarithmic_And_Nonfinite_Gaps_Survive_Reduction_And_Terminal_X_Is_Retained()
        {
            StreamingRangeChartDataSource source = new(40, valueAxisKind: ChartAxisKind.Logarithmic);
            ChartRangeSample[] samples = Enumerable.Range(1, 40).Select(i => new ChartRangeSample(i, 2, 8)).ToArray();
            samples[10] = new ChartRangeSample(11, -1, 8);
            samples[20] = new ChartRangeSample(21, 2, double.NaN);
            for (int i = 30; i < 40; i++) samples[i] = new ChartRangeSample(i + 1, null, 8);
            source.AppendRange(samples);
            var view = source.BuildView(new() { MaxPoints = 6 });
            Assert.Contains(10L, view.SourceSampleIndices); Assert.Contains(20L, view.SourceSampleIndices);
            Assert.Contains(30L, view.SourceSampleIndices); Assert.Equal(39L, view.SourceSampleIndices[^1]);
            Assert.Equal(40, view.Snapshot.Series[0].XValues![^1]);
            for (int i = 0; i < view.SourceSampleIndices.Count; i++)
                if (view.SourceSampleIndices[i] is 10 or 20 or >= 30)
                {
                    Assert.Null(view.Snapshot.Series[0].LowValues![i]);
                    Assert.Null(view.Snapshot.Series[0].HighValues![i]);
                    Assert.Null(view.Snapshot.Series[0].Values[i]);
                }
            Assert.Throws<ArgumentException>(() => source.Append(new ChartRangeSample(41, -1, -2)));
        }

        [Fact]
        public void All_Missing_Reduced_Window_Retains_Both_Domain_Endpoints()
        {
            StreamingRangeChartDataSource source = new(100);
            source.AppendRange(Enumerable.Range(0, 200).Select(i => new ChartRangeSample(i, null, null)).ToArray());
            var view = source.BuildView(new() { WindowStart = 17, WindowCount = 60, MaxPoints = 6 });
            Assert.Equal(new long[] { 117, 176 }, view.SourceSampleIndices);
            Assert.Equal(new double[] { 117, 176 }, view.Snapshot.Series[0].XValues);
        }

        [Theory]
        [InlineData(3, ChartAxisKind.Value)]
        [InlineData(17, ChartAxisKind.Value)]
        [InlineData(256, ChartAxisKind.Value)]
        [InlineData(127, ChartAxisKind.Logarithmic)]
        public void Seeded_Streaming_Windows_Exactly_Match_Replacement_Source_Selection(int capacity, ChartAxisKind axis)
        {
            Random random = new(45219);
            StreamingRangeChartDataSource source = new(capacity, "Range", valueAxisKind: axis);
            List<ChartRangeSample> all = new();
            for (int pass = 0; pass < 30; pass++)
            {
                int count = random.Next(1, capacity * 2 + 1);
                ChartRangeSample[] batch = new ChartRangeSample[count];
                for (int i = 0; i < count; i++)
                {
                    int index = all.Count;
                    double low = random.NextDouble() * 50 - 10;
                    batch[i] = new ChartRangeSample(100 + index * 0.25,
                        index % 13 == 5 ? null : low, low + 20, index.ToString());
                    all.Add(batch[i]);
                }
                source.AppendRange(batch);
                ChartRangeSample[] tail = all.Skip(Math.Max(0, all.Count - capacity)).ToArray();
                RangeChartDataSource replacement = new("Range", tail.Select(v => v.Lower).ToArray(), tail.Select(v => v.Upper).ToArray(),
                    tail.Select(v => v.X).ToArray(), tail.Select(v => v.Category).ToArray(), valueAxisKind: axis);
                foreach (ChartDownsampleMode mode in new[] { ChartDownsampleMode.None, ChartDownsampleMode.MinMax, ChartDownsampleMode.Adaptive })
                {
                    ChartDataRequest request = new()
                    {
                        WindowStart = random.Next(-3, capacity + 4), WindowCount = random.Next(-3, capacity + 4),
                        MaxPoints = random.Next(-1, 40), DownsampleMode = mode
                    };
                    var expected = replacement.BuildView(request); var actual = source.BuildView(request);
                    Assert.Equal(expected.Snapshot.Categories, actual.Snapshot.Categories);
                    Assert.Equal(expected.Snapshot.Series[0].XValues, actual.Snapshot.Series[0].XValues);
                    Assert.Equal(expected.Snapshot.Series[0].Values, actual.Snapshot.Series[0].Values);
                    Assert.Equal(expected.Snapshot.Series[0].LowValues, actual.Snapshot.Series[0].LowValues);
                    Assert.Equal(expected.Snapshot.Series[0].HighValues, actual.Snapshot.Series[0].HighValues);
                    Assert.Equal(expected.SourcePointIndices.Select(i => (long)all.Count - tail.Length + i), actual.SourceSampleIndices);
                }
            }
        }

        [Fact]
        public void Normalized_Requests_Reuse_Views_And_Window_Clamping_Cannot_Overflow()
        {
            StreamingRangeChartDataSource source = new(100);
            source.AppendRange(Enumerable.Range(0, 100).Select(Sample).ToArray());
            var first = source.BuildView(new() { WindowStart = -1, WindowCount = int.MaxValue, MaxPoints = 1 });
            Assert.Same(first, source.BuildView(new() { WindowStart = 0, WindowCount = 100, MaxPoints = 6, DownsampleMode = ChartDownsampleMode.MinMax }));
            Assert.Empty(source.BuildView(new() { WindowStart = int.MaxValue, WindowCount = int.MaxValue }).SourceSampleIndices);
            Assert.Empty(source.BuildView(new() { WindowStart = 2, WindowCount = -1 }).SourceSampleIndices);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildView(new() { DownsampleMode = ChartDownsampleMode.Lttb }));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildView(new() { DownsampleMode = ChartDownsampleMode.Bucket }));
            Assert.Throws<ArgumentNullException>(() => source.BuildView(null!));
        }

        [Fact]
        public void Steady_State_Appends_Batches_And_Cached_Views_Allocate_Nothing()
        {
            StreamingRangeChartDataSource source = new(256);
            for (int i = 0; i < 1000; i++) source.Append(new ChartRangeSample(i, 1, 2));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1000; i < 11000; i++) source.Append(new ChartRangeSample(i, 1, 2));
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            ChartRangeSample[] batch = new ChartRangeSample[32];
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int run = 0; run < 100; run++)
            {
                for (int i = 0; i < batch.Length; i++) batch[i] = new ChartRangeSample(11000 + run * 32 + i, 1, 2);
                source.AppendRange(batch);
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            ChartDataRequest request = new() { WindowStart = 100, WindowCount = 128, MaxPoints = 32 };
            source.BuildView(request);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) source.BuildView(request);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Notifications_Run_Outside_Lock_And_Allow_A_Separate_Reader()
        {
            StreamingRangeChartDataSource source = new(8);
            source.DataInvalidated += (_, _) =>
            {
                Task<int> reader = Task.Run(() => source.BuildView(new()).RetainedCount);
                Assert.True(reader.Wait(TimeSpan.FromSeconds(5)), "A source callback held the data lock.");
                Assert.Equal(source.Count, reader.Result);
            };
            source.Append(Sample(0)); source.AppendRange(new[] { Sample(1), Sample(2) }); source.Clear();
        }

        [Fact]
        public async Task Concurrent_Reads_Capture_One_Coherent_Stream_Version()
        {
            StreamingRangeChartDataSource source = new(256);
            Task writer = Task.Run(() => { for (int i = 0; i < 3000; i++) source.Append(new ChartRangeSample(i, i, i + 2)); });
            Task reader = Task.Run(() =>
            {
                for (int pass = 0; pass < 150; pass++)
                {
                    var view = source.BuildView(new() { MaxPoints = 32 });
                    for (int i = 0; i < view.SourceSampleIndices.Count; i++)
                    {
                        long index = view.SourceSampleIndices[i];
                        Assert.Equal((double)index, view.Snapshot.Series[0].XValues![i]);
                        Assert.Equal((double)index, view.Snapshot.Series[0].LowValues![i]);
                        Assert.Equal((double)index + 2, view.Snapshot.Series[0].HighValues![i]);
                        Assert.InRange(index, view.FirstRetainedSampleIndex, view.TotalSamples - 1);
                    }
                }
            });
            await Task.WhenAll(writer, reader);
            Assert.Equal(3000, source.TotalSamples); Assert.Equal(256, source.Count);
        }

        [Fact]
        public void Rendering_Hit_Mapping_And_Extreme_Midpoints_Use_Actual_Retained_Data()
        {
            StreamingRangeChartDataSource source = new(100);
            source.AppendRange(Enumerable.Range(0, 150).Select(i => new ChartRangeSample(i, 2, 8)).ToArray());
            var view = source.BuildView(new() { MaxPoints = 20 });
            SkiaChartStyle style = new()
            {
                CategoryAxisKind = ChartAxisKind.Value, ValueAxisMinimum = 0, ValueAxisMaximum = 10,
                ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
                PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0
            };
            Assert.NotEmpty(SkiaChartExporter.ExportPng(view.Snapshot, 600, 400, style));
            var hit = new SkiaChartRenderer().HitTest(new SKPoint(300, 200), SKRect.Create(600, 400), view.Snapshot, style);
            Assert.NotNull(hit);
            Assert.Equal((double)view.SourceSampleIndices[hit.Value.PointIndex], hit.Value.XValue);
            Assert.Equal(2, hit.Value.LowValue); Assert.Equal(8, hit.Value.HighValue);
            StreamingRangeChartDataSource extreme = new(2);
            extreme.Append(new ChartRangeSample(0, -double.MaxValue, double.MaxValue));
            extreme.Append(new ChartRangeSample(1, double.Epsilon, double.Epsilon));
            Assert.Equal(new double?[] { 0, double.Epsilon }, extreme.BuildSnapshot(new()).Series[0].Values);
        }

        [Fact]
        public void Constructor_And_Scalar_Validation_Reject_Invalid_Contracts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRangeChartDataSource(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRangeChartDataSource(8, valueAxisKind: ChartAxisKind.Category));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreamingRangeChartDataSource(8, valueAxisAssignment: (ChartValueAxisAssignment)99));
            StreamingRangeChartDataSource source = new(8, "Named", ChartValueAxisAssignment.Secondary,
                dataLabelFormatter: value => value.ToString("F1"));
            source.Append(new ChartRangeSample(1, null, double.PositiveInfinity));
            Assert.Null(source.BuildSnapshot(new()).Series[0].Values[0]);
            Assert.Throws<ArgumentException>(() => source.Append(new ChartRangeSample(1, 1, 2)));
            Assert.Throws<ArgumentException>(() => source.Append(new ChartRangeSample(2, 2, 1)));
            Assert.Throws<ArgumentException>(() => source.Append(new ChartRangeSample(double.NaN, 1, 2)));
            Assert.Equal("Named", source.BuildSnapshot(new()).Series[0].Name);
            Assert.Equal(ChartValueAxisAssignment.Secondary, source.BuildSnapshot(new()).Series[0].ValueAxisAssignment);
        }

        private static ChartRangeSample Sample(int i) => new(i, i, i + 2, i.ToString());
    }
}
