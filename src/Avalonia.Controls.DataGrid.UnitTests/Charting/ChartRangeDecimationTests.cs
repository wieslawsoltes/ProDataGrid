// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartRangeDecimationTests
    {
        [Fact]
        public void Both_Boundary_Extrema_Are_Kept_At_Their_Original_Shared_Indices()
        {
            double?[] low = Enumerable.Repeat<double?>(0, 1000).ToArray();
            double?[] high = Enumerable.Repeat<double?>(10, 1000).ToArray();
            low[123] = -50; low[321] = 9; high[456] = 1; high[789] = 75;
            Assert.Equal(new[] { 0, 123, 321, 456, 789, 999 }, ChartRangeDecimator.SelectIndices(low, high, 6));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(63)]
        [InlineData(200)]
        public void Finite_Buckets_Match_Independent_Extrema_Scans_And_Respect_The_Budget(int budget)
        {
            Random random = new(76419);
            for (int pass = 0; pass < 20; pass++)
            {
                int count = random.Next(budget + 1, budget + 900);
                double?[] low = new double?[count], high = new double?[count];
                for (int i = 0; i < count; i++) { low[i] = random.Next(-20, 20); high[i] = low[i] + random.Next(1, 50); }
                SortedSet<int> expected = new() { 0, count - 1 };
                int buckets = (budget - 2) / 4;
                for (int b = 0; b < buckets; b++)
                {
                    int start = 1 + b * (count - 2) / buckets;
                    int end = 1 + (b + 1) * (count - 2) / buckets;
                    int[] indices = Enumerable.Range(start, end - start).ToArray();
                    expected.Add(indices.OrderBy(i => low[i]).First());
                    expected.Add(indices.OrderByDescending(i => low[i]).First());
                    expected.Add(indices.OrderBy(i => high[i]).First());
                    expected.Add(indices.OrderByDescending(i => high[i]).First());
                }
                int[] actual = ChartRangeDecimator.SelectIndices(low, high, budget);
                Assert.Equal(expected, actual);
                Assert.InRange(actual.Length, 2, budget);
                Assert.True(actual.Zip(actual.Skip(1), (a, b) => a < b).All(v => v));
            }
        }

        [Fact]
        public void Missing_Nonfinite_And_Inverted_Runs_Are_Not_Bridged()
        {
            double?[] low = Enumerable.Repeat<double?>(0, 100).ToArray();
            double?[] high = Enumerable.Repeat<double?>(10, 100).ToArray();
            low[20] = low[21] = null; high[45] = double.PositiveInfinity; low[70] = 11;
            int[] selected = ChartRangeDecimator.SelectIndices(low, high, 6);
            foreach (int index in new[] { 0, 19, 20, 22, 44, 45, 46, 69, 70, 71, 99 }) Assert.Contains(index, selected);
            Assert.DoesNotContain(21, selected);
            Assert.True(selected.Length > 6);
            for (int i = 1; i < selected.Length; i++)
            {
                int a = selected[i - 1], b = selected[i];
                if (Valid(low, high, a) && Valid(low, high, b))
                    Assert.All(Enumerable.Range(a, b - a + 1), j => Assert.True(Valid(low, high, j)));
            }
        }

        [Fact]
        public void Alternating_Gaps_Honor_Topology_Even_When_It_Exceeds_The_Budget()
        {
            double?[] low = Enumerable.Range(0, 100).Select(i => i % 2 == 0 ? (double?)1 : null).ToArray();
            double?[] high = Enumerable.Repeat<double?>(2, 100).ToArray();
            Assert.Equal(Enumerable.Range(0, 100), ChartRangeDecimator.SelectIndices(low, high, 6));
        }

        [Fact]
        public void Extrema_Do_Not_Require_Overflowing_Arithmetic_Or_Change_Input()
        {
            double?[] low = Enumerable.Repeat<double?>(-1, 100).ToArray();
            double?[] high = Enumerable.Repeat<double?>(1, 100).ToArray();
            low[31] = -double.MaxValue; high[61] = double.MaxValue;
            double?[] originalLow = (double?[])low.Clone(), originalHigh = (double?[])high.Clone();
            int[] indices = ChartRangeDecimator.SelectIndices(low, high, 6);
            Assert.Contains(31, indices); Assert.Contains(61, indices);
            Assert.Equal(originalLow, low); Assert.Equal(originalHigh, high);
            Assert.Equal(indices, ChartRangeDecimator.SelectIndices(low, high, 6));
        }

        [Fact]
        public void Window_Selection_Reads_Only_That_Window_And_Uses_Full_Source_Indices()
        {
            WindowOnly low = new(100000, 50000, 100, 0), high = new(100000, 50000, 100, 10);
            int[] indices = ChartRangeDecimator.SelectIndices(low, high, 12, 50000, 100);
            Assert.Equal(50000, indices[0]); Assert.Equal(50099, indices[^1]);
            Assert.All(indices, i => Assert.InRange(i, 50000, 50099));
            Assert.InRange(low.Reads + high.Reads, 1, 2000);
        }

        [Fact]
        public void Empty_AllMissing_Clamped_And_Invalid_Requests_Have_Explicit_Results()
        {
            Assert.Empty(ChartRangeDecimator.SelectIndices(Array.Empty<double?>(), Array.Empty<double?>(), 6));
            Assert.Equal(new[] { 0 }, ChartRangeDecimator.SelectIndices(new double?[100], new double?[100], 6));
            double?[] low = Enumerable.Repeat<double?>(0, 20).ToArray(), high = Enumerable.Repeat<double?>(1, 20).ToArray();
            Assert.Equal(new[] { 0, 1, 2 }, ChartRangeDecimator.SelectIndices(low, high, 6, -1, 3));
            Assert.Empty(ChartRangeDecimator.SelectIndices(low, high, 6, int.MaxValue, int.MaxValue));
            Assert.Empty(ChartRangeDecimator.SelectIndices(low, high, 6, 10, -1));
            Assert.Equal(Enumerable.Range(17, 3), ChartRangeDecimator.SelectIndices(low, high, 6, 17, int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChartRangeDecimator.SelectIndices(low, high, 5));
            Assert.Throws<ArgumentException>(() => ChartRangeDecimator.SelectIndices(low, new double?[19], 6));
            Assert.Throws<ArgumentNullException>(() => ChartRangeDecimator.SelectIndices(null!, high, 6));
        }

        [Fact]
        public void View_Preserves_Original_Nonuniform_X_Labels_Bounds_And_Metadata()
        {
            double?[] low = Enumerable.Range(0, 1000).Select(i => (double?)(Math.Sin(i) * 10)).ToArray();
            double?[] high = low.Select(v => v + 20).ToArray();
            double[] x = Enumerable.Range(0, 1000).Select(i => 100 + i * i * 0.5).ToArray();
            string?[] labels = Enumerable.Range(0, 1000).Select(i => (string?)($"row-{i}")).ToArray();
            ChartSeriesStyle style = new() { StrokeWidth = 2 };
            Func<double, string> formatter = v => v.ToString("F1");
            RangeChartDataSource source = new("Range", low, high, x, labels, ChartValueAxisAssignment.Secondary, style, formatter);
            ChartRangeView view = source.BuildView(new ChartDataRequest { WindowStart = 200, WindowCount = 500, MaxPoints = 50 });
            Assert.Equal(200, view.WindowStart); Assert.Equal(500, view.WindowCount); Assert.Equal(1000, view.SourceCount);
            Assert.InRange(view.SourcePointIndices.Count, 2, 50);
            ChartSeriesSnapshot series = Assert.Single(view.Snapshot.Series);
            Assert.Equal(ChartSeriesKind.RangeArea, series.Kind); Assert.Same(style, series.Style);
            Assert.Same(formatter, series.DataLabelFormatter); Assert.Equal(ChartValueAxisAssignment.Secondary, series.ValueAxisAssignment);
            for (int i = 0; i < view.SourcePointIndices.Count; i++)
            {
                int original = view.SourcePointIndices[i];
                Assert.Equal(x[original], series.XValues![i]); Assert.Equal(labels[original], view.Snapshot.Categories[i]);
                Assert.Equal(low[original], series.LowValues![i]); Assert.Equal(high[original], series.HighValues![i]);
                Assert.Equal(low[original] + 10, series.Values[i]);
            }
        }

        [Fact]
        public void Views_Own_Data_And_Mapping_After_Source_Array_Mutation_And_Replacement()
        {
            double?[] low = { 10, 20, 30 }, high = { 20, 30, 40 };
            double[] x = { 1, 4, 9 }; string?[] labels = { "A", "B", "C" };
            RangeChartDataSource source = new("Owned", low, high, x, labels);
            low[0] = 999; high[1] = 999; x[0] = 999; labels[0] = "Changed";
            ChartRangeView original = source.BuildView(new ChartDataRequest());
            Assert.Equal(10, original.Snapshot.Series[0].LowValues![0]); Assert.Equal(1, original.Snapshot.Series[0].XValues![0]);
            Assert.Equal("A", original.Snapshot.Categories[0]);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)original.SourcePointIndices)[0] = 9);
            Assert.Throws<NotSupportedException>(() => ((IList<double?>)original.Snapshot.Series[0].HighValues!)[0] = 9);
            source.ReplaceData(new double?[] { 1, 2 }, new double?[] { 3, 4 });
            ChartRangeView next = source.BuildView(new ChartDataRequest());
            Assert.NotEqual(original.Snapshot.Version, next.Snapshot.Version);
            Assert.Equal(3, original.SourceCount); Assert.Equal(2, next.SourceCount);
            Assert.Equal(new double[] { 0, 1 }, next.Snapshot.Series[0].XValues);
            Assert.Equal(new double[] { 1, 4, 9 }, original.Snapshot.Series[0].XValues);
        }

        [Fact]
        public void Invalid_Replacement_Is_Atomic_And_Success_Notifies_Once_Outside_Lock()
        {
            RangeChartDataSource source = new("Values", new double?[] { 1, 2 }, new double?[] { 3, 4 });
            ChartDataSnapshot original = source.BuildSnapshot(new ChartDataRequest());
            int notifications = 0;
            source.DataInvalidated += (_, _) => { notifications++; Assert.Equal(3, source.BuildSnapshot(new ChartDataRequest()).Categories.Count); };
            Assert.Throws<ArgumentException>(() => source.ReplaceData(new double?[] { 5, 2 }, new double?[] { 3, 4 }));
            Assert.Throws<ArgumentException>(() => source.ReplaceData(new double?[2], new double?[2], new double[] { 1, 1 }));
            Assert.Throws<ArgumentException>(() => source.ReplaceData(new double?[2], new double?[2], categories: new string?[1]));
            Assert.Same(original, source.BuildSnapshot(new ChartDataRequest())); Assert.Equal(0, notifications);
            source.ReplaceData(new double?[] { 1, 2, 3 }, new double?[] { 4, 5, 6 });
            Assert.Equal(1, notifications); Assert.Equal(3, source.GetTotalCategoryCount());
        }

        [Fact]
        public void Normalized_Cached_Requests_Allocate_Nothing_And_None_Is_Exact()
        {
            double?[] low = Enumerable.Repeat<double?>(0, 10000).ToArray(), high = Enumerable.Repeat<double?>(10, 10000).ToArray();
            RangeChartDataSource source = new(null, low, high);
            ChartDataRequest request = new() { WindowStart = 3000, WindowCount = 5000, MaxPoints = 64 };
            ChartRangeView view = source.BuildView(request);
            Assert.Same(view.Snapshot, source.BuildSnapshot(request));
            Assert.Same(view, source.BuildView(new ChartDataRequest { WindowStart = 3000, WindowCount = 5000, MaxPoints = 64, DownsampleMode = ChartDownsampleMode.MinMax }));
            for (int i = 0; i < 1000; i++) source.BuildView(request);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) source.BuildView(request);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            request.DownsampleMode = ChartDownsampleMode.None;
            Assert.Equal(Enumerable.Range(3000, 5000), source.BuildView(request).SourcePointIndices);
        }

        [Theory]
        [InlineData(null, 100)]
        [InlineData(-1, 100)]
        [InlineData(0, 100)]
        [InlineData(1, 3)]
        [InlineData(6, 3)]
        [InlineData(100, 100)]
        public void Point_Budget_Normalization_Is_Explicit(int? budget, int expected)
        {
            RangeChartDataSource source = new(null, Enumerable.Repeat<double?>(0, 100).ToArray(), Enumerable.Repeat<double?>(1, 100).ToArray());
            Assert.Equal(expected, source.BuildView(new ChartDataRequest { MaxPoints = budget }).SourcePointIndices.Count);
        }

        [Theory]
        [InlineData(ChartDownsampleMode.Bucket)]
        [InlineData(ChartDownsampleMode.Lttb)]
        [InlineData((ChartDownsampleMode)99)]
        public void Independent_Line_Sampling_Modes_Are_Not_Silently_Applied_To_Pairs(ChartDownsampleMode mode)
        {
            RangeChartDataSource source = new(null, new double?[2], new double?[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.BuildSnapshot(new ChartDataRequest { DownsampleMode = mode }));
        }

        [Fact]
        public async Task Concurrent_Replacements_And_Views_Do_Not_Mix_Input_Versions()
        {
            RangeChartDataSource source = new(null, new double?[] { 0 }, new double?[] { 1 });
            Task writer = Task.Run(() =>
            {
                for (int pass = 1; pass < 40; pass++)
                    source.ReplaceData(Enumerable.Repeat<double?>(pass, 100).ToArray(), Enumerable.Repeat<double?>(pass + 1, 100).ToArray());
            });
            for (int pass = 0; pass < 100; pass++)
            {
                ChartSeriesSnapshot series = source.BuildSnapshot(new ChartDataRequest { MaxPoints = 16 }).Series[0];
                Assert.All(series.LowValues!, v => Assert.Equal(series.LowValues![0], v));
                Assert.All(series.HighValues!, v => Assert.Equal(series.LowValues![0] + 1, v));
            }
            await writer;
        }

        [Fact]
        public void Reduced_Rendering_And_Hits_Keep_The_Gap_At_Its_Original_Numeric_Position()
        {
            double?[] low = Enumerable.Repeat<double?>(20, 1000).ToArray(), high = Enumerable.Repeat<double?>(80, 1000).ToArray();
            for (int i = 400; i <= 600; i++) low[i] = null;
            RangeChartDataSource source = new("Band", low, high);
            ChartRangeView view = source.BuildView(new ChartDataRequest { MaxPoints = 16 });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
                PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
                CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 0, CategoryAxisMaximum = 999,
                ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 0
            };
            using SKBitmap bitmap = SKBitmap.Decode(SkiaChartExporter.ExportPng(view.Snapshot, 600, 300, style));
            Assert.Equal(style.Background, bitmap.GetPixel(300, 150));
            Assert.NotEqual(style.Background, bitmap.GetPixel(100, 150));
            SkiaChartRenderer renderer = new();
            Assert.Null(renderer.HitTest(new SKPoint(300, 150), SKRect.Create(600, 300), view.Snapshot, style));
            var hit = renderer.HitTest(new SKPoint(100, 150), SKRect.Create(600, 300), view.Snapshot, style);
            Assert.NotNull(hit);
            int original = view.SourcePointIndices[hit.Value.PointIndex];
            Assert.Equal((double)original, hit.Value.XValue);
            Assert.Equal(low[original], hit.Value.LowValue); Assert.Equal(high[original], hit.Value.HighValue);
            Assert.Contains("<svg", SkiaChartExporter.ExportSvg(view.Snapshot, 600, 300, style));
        }

        private static bool Valid(double?[] low, double?[] high, int i) => low[i] is double a && high[i] is double b && double.IsFinite(a) && double.IsFinite(b) && a <= b;

        private sealed class WindowOnly : IReadOnlyList<double?>
        {
            private readonly int _start, _end;
            private readonly double _value;
            public WindowOnly(int total, int start, int count, double value) { Count = total; _start = start; _end = start + count; _value = value; }
            public int Count { get; }
            public int Reads { get; private set; }
            public double? this[int index]
            {
                get { Assert.InRange(index, _start, _end - 1); Reads++; return _value; }
            }
            public IEnumerator<double?> GetEnumerator() => throw new NotSupportedException();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
