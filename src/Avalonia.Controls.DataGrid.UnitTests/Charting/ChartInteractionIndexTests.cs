// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartInteractionIndexTests
    {
        private static SkiaChartStyle Style() => new()
        {
            ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false,
            PaddingLeft = 0, PaddingRight = 0, PaddingTop = 0, PaddingBottom = 0,
            CategoryAxisKind = ChartAxisKind.Value, CategoryAxisMinimum = 0, CategoryAxisMaximum = 100,
            ValueAxisMinimum = 0, ValueAxisMaximum = 100, HitTestRadius = 12
        };

        [Theory]
        [InlineData(ChartSeriesKind.Line)]
        [InlineData(ChartSeriesKind.Area)]
        [InlineData(ChartSeriesKind.Scatter)]
        [InlineData(ChartSeriesKind.Bubble)]
        public void Indexed_Queries_Match_Reference_For_Ragged_Gaps_Unsorted_X_And_Multiple_Axes(ChartSeriesKind kind)
        {
            Random random = new(4107);
            for (int pass = 0; pass < 6; pass++)
            {
                ChartSeriesSnapshot[] series = new ChartSeriesSnapshot[3];
                for (int s = 0; s < series.Length; s++)
                {
                    int count = 201 + s * 23;
                    double?[] values = new double?[count], sizes = new double?[count];
                    double[] x = new double[count];
                    for (int p = 0; p < count; p++)
                    {
                        values[p] = p % 11 == 0 ? null : random.NextDouble() * 120 - 10;
                        x[p] = p % 17 == 0 ? double.NaN : random.NextDouble() * 120 - 10;
                        sizes[p] = p % 19 == 0 ? null : random.NextDouble() * 10;
                    }
                    series[s] = new ChartSeriesSnapshot("S" + s, kind, values,
                        pass == 0 ? null : x, pass == 1 ? null : sizes,
                        valueAxisAssignment: s == 1 ? ChartValueAxisAssignment.Secondary : ChartValueAxisAssignment.Primary);
                }
                ChartDataSnapshot data = new(new string?[] { "First", "Second" }, series);
                SkiaChartStyle style = Style();
                style.SecondaryValueAxisMinimum = 1;
                style.SecondaryValueAxisMaximum = 100;
                style.SecondaryValueAxisKind = pass % 2 == 0 ? ChartAxisKind.Logarithmic : ChartAxisKind.Value;
                SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
                SKRect bounds = new(13, 29, 913, 629);
                for (int p = 0; p < 200; p++)
                {
                    SKPoint pointer = new((float)(random.NextDouble() * 950), (float)(random.NextDouble() * 650));
                    Assert.Equal(reference.HitTest(pointer, bounds, data, style), indexed.HitTest(pointer, bounds, data, style));
                }
            }
        }

        [Fact]
        public void Warm_Queries_And_Viewport_Reads_Do_Not_Rescan_Values_Or_Allocate()
        {
            double?[] values = new double?[10000];
            double[] xs = new double[values.Length];
            for (int i = 0; i < values.Length; i++) { values[i] = i % 100; xs[i] = i % 100; }
            CountingList<double?> y = new(values);
            CountingList<double> x = new(xs);
            ChartDataSnapshot data = new(Array.Empty<string?>(), new[] { new ChartSeriesSnapshot("Points", ChartSeriesKind.Scatter, y, x) });
            SkiaChartRenderer renderer = new();
            SkiaChartStyle style = Style();
            SKRect bounds = SKRect.Create(1000, 600);
            SKPoint pointer = new(500, 300);
            for (int i = 0; i < 50; i++) renderer.HitTest(pointer, bounds, data, style);
            y.Reads = x.Reads = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++)
            {
                renderer.HitTest(pointer, bounds, data, style);
                renderer.TryGetViewportInfo(bounds, data, style, out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(y.Reads, 0, 50);
            Assert.InRange(x.Reads, 0, 50);
            Assert.Equal(0, allocated);
        }

        [Fact]
        public void Snapshot_Bounds_Style_Nullable_Overrides_And_Explicit_Clear_Invalidate()
        {
            double?[] values = { 20, 40, 60 };
            ChartDataSnapshot data = new(Array.Empty<string?>(), new[] { new ChartSeriesSnapshot("Line", ChartSeriesKind.Line, values) });
            SkiaChartStyle style = Style();
            SkiaChartRenderer renderer = new(), reference = new() { UseInteractionCache = false };
            SKRect bounds = SKRect.Create(200, 200);
            for (int iteration = 0; iteration < 6; iteration++)
            {
                if (iteration == 1) { style.ValueAxisMinimum = null; style.ValueAxisMaximum = null; }
                if (iteration == 2) style.ValueAxisMinimum = 0;
                if (iteration == 3) { bounds = SKRect.Create(400, 300); style.HitTestRadius = 30; }
                if (iteration == 4) { values[1] = 90; renderer.ClearInteractionCache(); }
                if (iteration == 5) data = new ChartDataSnapshot(data.Categories, data.Series, 1);
                for (int y = 0; y < bounds.Height; y += 11)
                    for (int x = 0; x < bounds.Width; x += 13)
                        Assert.Equal(reference.HitTest(new SKPoint(x, y), bounds, data, style),
                            renderer.HitTest(new SKPoint(x, y), bounds, data, style));
            }
            renderer.UseInteractionCache = false;
            Assert.Equal(reference.HitTest(new SKPoint(200, 100), bounds, data, style),
                renderer.HitTest(new SKPoint(200, 100), bounds, data, style));
        }

        [Fact]
        public void Exact_Ties_Preserve_First_Series_Then_First_Source_Point()
        {
            ChartSeriesSnapshot first = new("First", ChartSeriesKind.Scatter, new double?[] { 50, 50 }, new double[] { 50, 50 });
            ChartSeriesSnapshot second = new("Second", ChartSeriesKind.Scatter, new double?[] { 50 }, new double[] { 50 });
            var hit = new SkiaChartRenderer().HitTest(new SKPoint(100, 100), SKRect.Create(200, 200),
                new ChartDataSnapshot(Array.Empty<string?>(), new[] { first, second }), Style());
            Assert.NotNull(hit);
            Assert.Equal(0, hit.Value.SeriesIndex);
            Assert.Equal(0, hit.Value.PointIndex);
        }

        [Fact]
        public void Bubble_Hit_Uses_Its_Secondary_Logarithmic_Scale_And_Actual_Radius()
        {
            ChartDataSnapshot data = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Bubble", ChartSeriesKind.Bubble, new double?[] { 10 }, new double[] { 50 },
                    new double?[] { 1 }, valueAxisAssignment: ChartValueAxisAssignment.Secondary)
            });
            SkiaChartStyle style = Style();
            style.SecondaryValueAxisKind = ChartAxisKind.Logarithmic;
            style.SecondaryValueAxisMinimum = 1;
            style.SecondaryValueAxisMaximum = 100;
            style.BubbleMinRadius = style.BubbleMaxRadius = 30;
            style.HitTestRadius = 1;
            foreach (bool cache in new[] { false, true })
            {
                SkiaChartRenderer renderer = new() { UseInteractionCache = cache };
                Assert.NotNull(renderer.HitTest(new SKPoint(120, 100), SKRect.Create(200, 200), data, style));
                Assert.Null(renderer.HitTest(new SKPoint(100, 182), SKRect.Create(200, 200), data, style));
            }
        }

        [Fact]
        public void Bubble_Center_Outside_Plot_Can_Be_Selected_At_Visible_Edge()
        {
            ChartDataSnapshot data = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Edge", ChartSeriesKind.Bubble, new double?[] { 50 }, new double[] { -5 }, new double?[] { 1 }) });
            SkiaChartStyle style = Style();
            style.BubbleMinRadius = style.BubbleMaxRadius = 30;
            Assert.NotNull(new SkiaChartRenderer().HitTest(new SKPoint(1, 100), SKRect.Create(200, 200), data, style));
        }

        [Fact]
        public void Nonfinite_Pointers_Empty_Data_And_Nonpositive_Bounds_Do_Not_Hit()
        {
            SkiaChartRenderer renderer = new();
            ChartDataSnapshot data = new(Array.Empty<string?>(), new[] { new ChartSeriesSnapshot("Line", ChartSeriesKind.Line, new double?[] { 1 }) });
            Assert.Null(renderer.HitTest(new SKPoint(float.NaN, 1), SKRect.Create(200, 200), data));
            Assert.Null(renderer.HitTest(new SKPoint(1, float.PositiveInfinity), SKRect.Create(200, 200), data));
            Assert.Null(renderer.HitTest(new SKPoint(1, 1), SKRect.Create(0, 200), data));
            Assert.Null(renderer.HitTest(new SKPoint(1, 1), SKRect.Create(200, 200), ChartDataSnapshot.Empty));
        }

        private sealed class CountingList<T> : IReadOnlyList<T>
        {
            private readonly T[] _values;
            public CountingList(T[] values) { _values = values; }
            public int Reads { get; set; }
            public int Count => _values.Length;
            public T this[int index] { get { Reads++; return _values[index]; } }
            public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
