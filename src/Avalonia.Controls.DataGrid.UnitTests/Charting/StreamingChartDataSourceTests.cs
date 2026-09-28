// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingChartDataSourceTests
    {
        [Fact]
        public void Ring_Preserves_Chronological_Order_And_Old_Snapshots()
        {
            StreamingChartDataSource source = new(3, "Live");
            source.AppendRange(new[] { new ChartSample(1, 10), new ChartSample(2, 20), new ChartSample(3, 30) });
            ChartDataSnapshot original = source.BuildSnapshot(new ChartDataRequest());
            source.Append(new ChartSample(4, 40));
            ChartDataSnapshot current = source.BuildSnapshot(new ChartDataRequest());
            Assert.Equal(new double?[] { 10, 20, 30 }, original.Series[0].Values);
            Assert.Equal(new double?[] { 20, 30, 40 }, current.Series[0].Values);
            Assert.Equal(new double[] { 2, 3, 4 }, current.Series[0].XValues);
            Assert.Equal(4, source.TotalSamples);
            Assert.Equal(3, source.GetTotalCategoryCount());
            Assert.NotEqual(original.Version, current.Version);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)original.Series[0].Values)[0] = 99);
        }

        [Fact]
        public void Batch_Is_Atomic_And_Raises_One_Notification()
        {
            StreamingChartDataSource source = new(8);
            int notifications = 0;
            source.DataInvalidated += (_, _) => { notifications++; source.BuildSnapshot(new ChartDataRequest()); };
            source.AppendRange(new[] { new ChartSample(1, 1), new ChartSample(2, 2) });
            Assert.Equal(1, notifications);
            Assert.Throws<ArgumentException>(() => source.AppendRange(new[] { new ChartSample(3, 3), new ChartSample(double.NaN, 4) }));
            Assert.Equal(2, source.Count);
            Assert.Equal(1, notifications);
            source.AppendRange(Array.Empty<ChartSample>());
            Assert.Equal(1, notifications);
        }

        [Fact]
        public void Oversized_Batches_Keep_Their_Tail_And_Count_All_Input()
        {
            StreamingChartDataSource source = new(4);
            source.AppendRange(Enumerable.Range(0, 100).Select(i => new ChartSample(i, i)).ToArray());
            Assert.Equal(new double[] { 96, 97, 98, 99 }, source.BuildSnapshot(new ChartDataRequest()).Series[0].XValues);
            Assert.Equal(100, source.TotalSamples);
            source.Append(new ChartSample(100, 100));
            Assert.Equal(new double[] { 97, 98, 99, 100 }, source.BuildSnapshot(new ChartDataRequest()).Series[0].XValues);
        }

        [Fact]
        public void Windows_Are_Clamped_And_Unchanged_Requests_Reuse_Snapshots()
        {
            StreamingChartDataSource source = new(8);
            source.AppendRange(Enumerable.Range(0, 10).Select(i => new ChartSample(i, i)).ToArray());
            ChartDataRequest request = new() { WindowStart = 2, WindowCount = 3 };
            ChartDataSnapshot snapshot = source.BuildSnapshot(request);
            Assert.Equal(new double[] { 4, 5, 6 }, snapshot.Series[0].XValues);
            Assert.Same(snapshot, source.BuildSnapshot(new ChartDataRequest { WindowStart = 2, WindowCount = 3 }));
            request.WindowStart = int.MaxValue;
            request.WindowCount = int.MaxValue;
            Assert.Empty(source.BuildSnapshot(request).Categories);
            request.WindowStart = -1;
            Assert.Equal(8, source.BuildSnapshot(request).Categories.Count);
            request.WindowCount = -1;
            Assert.Empty(source.BuildSnapshot(request).Categories);
        }

        [Fact]
        public void Nonfinite_Values_Are_Gaps_And_Clear_Resets_History()
        {
            StreamingChartDataSource source = new(4);
            source.AppendRange(new[] { new ChartSample(0, double.NaN), new ChartSample(1, double.PositiveInfinity), new ChartSample(2, 2) });
            Assert.Equal(new double?[] { null, null, 2 }, source.BuildSnapshot(new ChartDataRequest()).Series[0].Values);
            source.Clear();
            Assert.Equal(0, source.Count);
            Assert.Equal(0, source.TotalSamples);
            Assert.Empty(source.BuildSnapshot(new ChartDataRequest()).Series[0].Values);
        }

        [Fact]
        public void Append_Does_Not_Allocate_After_Construction()
        {
            StreamingChartDataSource source = new(64);
            for (int i = 0; i < 1000; i++) source.Append(new ChartSample(i, i));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) source.Append(new ChartSample(i, i));
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public async Task Concurrent_Reads_And_Appends_Produce_Consistent_Snapshots()
        {
            StreamingChartDataSource source = new(256);
            Task writer = Task.Run(() => { for (int i = 0; i < 2000; i++) source.Append(new ChartSample(i, i)); });
            for (int pass = 0; pass < 100; pass++)
            {
                ChartDataSnapshot snapshot = source.BuildSnapshot(new ChartDataRequest());
                for (int i = 0; i < snapshot.Categories.Count; i++)
                    Assert.Equal(snapshot.Series[0].XValues![i], snapshot.Series[0].Values[i]);
            }
            await writer;
            Assert.Equal(2000, source.TotalSamples);
        }

        [Theory]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Adaptive)]
        public void Decimation_Preserves_Spikes_And_Original_Coordinates(ChartDownsampleMode mode)
        {
            ChartSample[] samples = Enumerable.Range(0, 1000).Select(i => new ChartSample(i * 2, i == 500 ? 1000 : 0)).ToArray();
            int[] indices = ChartSampleDecimator.SelectIndices(samples, 32, mode);
            Assert.Equal(0, indices[0]);
            Assert.Equal(999, indices[^1]);
            Assert.Contains(500, indices);
            Assert.InRange(indices.Length, 2, 32);
            Assert.True(indices.Zip(indices.Skip(1), (a, b) => a < b).All(x => x));
            StreamingChartDataSource source = new(1000);
            source.AppendRange(samples);
            ChartSeriesSnapshot series = source.BuildSnapshot(new ChartDataRequest { MaxPoints = 32, DownsampleMode = mode }).Series[0];
            Assert.Equal(indices.Select(i => samples[i].X), series.XValues!);
            Assert.Equal(indices.Select(i => samples[i].Value), series.Values);
        }

        [Theory]
        [InlineData(ChartDownsampleMode.MinMax)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData(ChartDownsampleMode.Bucket)]
        public void Gap_Topology_Takes_Precedence_Over_The_Budget(ChartDownsampleMode mode)
        {
            ChartSample[] samples = Enumerable.Range(0, 60).Select(i => new ChartSample(i, i % 3 == 1 ? null : i)).ToArray();
            int[] indices = ChartSampleDecimator.SelectIndices(samples, 4, mode);
            for (int i = 0; i < samples.Length; i++)
                if (i % 3 == 1) Assert.Contains(i, indices);
            Assert.True(indices.Length > 4);
            Assert.True(indices.Zip(indices.Skip(1), (a, b) => a < b).All(x => x));
        }

        [Fact]
        public void Degenerate_And_Extreme_Decimation_Inputs_Are_Deterministic()
        {
            Assert.Empty(ChartSampleDecimator.SelectIndices(Array.Empty<ChartSample>(), 2));
            Assert.Equal(new[] { 0 }, ChartSampleDecimator.SelectIndices(Enumerable.Range(0, 100).Select(i => new ChartSample(i, null)).ToArray(), 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartSampleDecimator.SelectIndices(Array.Empty<ChartSample>(), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartSampleDecimator.SelectIndices(Array.Empty<ChartSample>(), 2, (ChartDownsampleMode)99));
            ChartSample[] extremes = Enumerable.Range(0, 100).Select(i => new ChartSample(i % 2 == 0 ? -double.MaxValue : double.MaxValue, i % 2 == 0 ? double.MaxValue : -double.MaxValue)).ToArray();
            int[] indices = ChartSampleDecimator.SelectIndices(extremes, 10, ChartDownsampleMode.Lttb);
            Assert.Equal(10, indices.Length);
            Assert.True(indices.Zip(indices.Skip(1), (a, b) => a < b).All(x => x));
        }

        [Fact]
        public void Randomized_Decimation_Produces_Valid_Ordered_Indices()
        {
            Random random = new(7139);
            foreach (ChartDownsampleMode mode in Enum.GetValues<ChartDownsampleMode>())
            {
                for (int pass = 0; pass < 40; pass++)
                {
                    int count = random.Next(3, 500);
                    ChartSample[] samples = Enumerable.Range(0, count).Select(i => new ChartSample(i, random.NextDouble())).ToArray();
                    int budget = random.Next(2, count);
                    int[] indices = ChartSampleDecimator.SelectIndices(samples, budget, mode);
                    Assert.Equal(0, indices[0]);
                    Assert.Equal(count - 1, indices[^1]);
                    Assert.True(indices.Zip(indices.Skip(1), (a, b) => a < b).All(x => x));
                    Assert.InRange(indices.Length, 2, mode == ChartDownsampleMode.None ? count : budget);
                }
            }
        }
    }
}
