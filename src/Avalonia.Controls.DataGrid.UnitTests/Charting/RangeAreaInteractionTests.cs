// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class RangeAreaInteractionTests
    {
        private static readonly SKRect Bounds = new(17, 23, 617, 423);

        private static SkiaChartStyle Style(ChartAxisKind xKind = ChartAxisKind.Value) => new()
        {
            ShowLegend = false, ShowAxisLabels = false, ShowCategoryLabels = false, ShowDataLabels = false,
            PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0,
            CategoryAxisKind = xKind, CategoryAxisMinimum = 1, CategoryAxisMaximum = 101,
            ValueAxisMinimum = 1, ValueAxisMaximum = 100,
            SecondaryValueAxisMinimum = 1, SecondaryValueAxisMaximum = 1000,
            SecondaryValueAxisKind = ChartAxisKind.Logarithmic
        };

        [Theory]
        [InlineData(ChartAxisKind.Category, 0)]
        [InlineData(ChartAxisKind.Value, 0)]
        [InlineData(ChartAxisKind.Value, 6)]
        [InlineData(ChartAxisKind.DateTime, 9)]
        [InlineData(ChartAxisKind.Logarithmic, 4)]
        [InlineData(ChartAxisKind.Value, -4)]
        [InlineData(ChartAxisKind.Value, 1000)]
        public void Seeded_Queries_Match_The_Unchanged_Scan_With_Gaps_And_Mixed_Axes(ChartAxisKind kind, float radius)
        {
            Random random = new(29147);
            double?[] low = new double?[420], high = new double?[420];
            double[] x = new double[420];
            double coordinate = 1;
            for (int i = 0; i < low.Length; i++)
            {
                coordinate += random.NextDouble() + 0.03;
                x[i] = coordinate;
                low[i] = random.NextDouble() * 50 + 5;
                high[i] = low[i] + random.NextDouble() * 35;
                if (i % 19 == 3 || i % 29 == 7) low[i] = null;
            }
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                ChartRangeSeries.CreateArea("First", low, high, x),
                ChartRangeSeries.CreateArea("Secondary", low.Select(v => v * 7).ToArray(), high.Select(v => v * 7).ToArray(), x,
                    ChartValueAxisAssignment.Secondary)
            });
            SkiaChartStyle style = Style(kind);
            style.HitTestRadius = radius;
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            for (int i = 0; i < 800; i++)
            {
                SKPoint point = new((float)(Bounds.Left - 5 + random.NextDouble() * (Bounds.Width + 10)),
                    (float)(Bounds.Top - 5 + random.NextDouble() * (Bounds.Height + 10)));
                EqualHit(reference.HitTest(point, Bounds, snapshot, style), indexed.HitTest(point, Bounds, snapshot, style));
            }
        }

        [Theory]
        [InlineData(ChartSeriesKind.Line)]
        [InlineData(ChartSeriesKind.Area)]
        [InlineData(ChartSeriesKind.Scatter)]
        [InlineData(ChartSeriesKind.Bubble)]
        [InlineData(ChartSeriesKind.Column)]
        public void Mixed_Plots_Preserve_First_Range_Precedence_And_Point_Only_Fallback(ChartSeriesKind kind)
        {
            ChartSeriesSnapshot range = ChartRangeSeries.CreateArea("Band", new double?[] { 20, 20, null, 20, 20 },
                new double?[] { 60, 60, null, 60, 60 }, new double[] { 1, 26, 51, 76, 101 });
            ChartSeriesSnapshot points = new("Overlay", kind, new double?[] { 40, 40, 40, 40, 40 },
                new double[] { 1, 26, 51, 76, 101 }, sizeValues: new double?[] { 1, 2, 3, 4, 5 });
            foreach (bool reverse in new[] { false, true })
            {
                ChartDataSnapshot snapshot = new(new string?[] { "A", "B", "C", "D", "E" },
                    reverse ? new[] { range, points } : new[] { points, range });
                SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
                SkiaChartStyle style = Style();
                for (int y = 25; y < 420; y += 13)
                    for (int x = 18; x < 615; x += 11)
                    {
                        SKPoint point = new(x, y);
                        EqualHit(reference.HitTest(point, Bounds, snapshot, style), indexed.HitTest(point, Bounds, snapshot, style));
                    }
            }
        }

        [Fact]
        public void A_Segment_Spanning_The_Viewport_Is_Found_Beyond_The_Column_Radius()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                ChartRangeSeries.CreateArea("Wide", new double?[] { 10, 50 }, new double?[] { 40, 90 }, new double[] { -100, 200 })
            });
            SkiaChartStyle style = Style();
            style.HitTestRadius = 0;
            SKPoint center = new(Bounds.MidX, Bounds.MidY);
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            Assert.NotNull(reference.HitTest(center, Bounds, snapshot, style));
            EqualHit(reference.HitTest(center, Bounds, snapshot, style), indexed.HitTest(center, Bounds, snapshot, style));
        }

        [Fact]
        public void Gaps_Do_Not_Create_Segments_And_Midpoints_Do_Not_Create_Invisible_Markers()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[] { 40, 40, 40 },
                    new double[] { 1, 51, 101 }, lowValues: new double?[] { 20, null, 20 }, highValues: new double?[] { 60, null, 60 })
            });
            SkiaChartStyle style = Style();
            style.HitTestRadius = 0;
            Assert.Null(new SkiaChartRenderer().HitTest(new SKPoint(Bounds.MidX, Bounds.MidY), Bounds, snapshot, style));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Raw_Duplicate_Or_Decreasing_X_Preserves_Source_Order_Not_Sorted_Order(bool decreasing)
        {
            double[] x = decreasing ? new double[] { 1, 80, 30, 60, 101 } : new double[] { 1, 51, 51, 51, 101 };
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[5], x,
                    lowValues: new double?[] { 10, 15, null, 25, 35 }, highValues: new double?[] { 20, 55, null, 75, 85 })
            });
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            SkiaChartStyle style = Style();
            for (int xPosition = 18; xPosition < 617; xPosition += 4)
                for (int yPosition = 24; yPosition < 423; yPosition += 9)
                {
                    SKPoint point = new(xPosition, yPosition);
                    EqualHit(reference.HitTest(point, Bounds, snapshot, style), indexed.HitTest(point, Bounds, snapshot, style));
                }
        }

        [Fact]
        public void Invalid_Ragged_And_Float_Collapsed_Intervals_Match_The_Scan()
        {
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[8],
                    new double[] { 1, 1.000000000001, 1.000000000002, 25, 51, 76, 101, double.NaN },
                    lowValues: new double?[] { 20, 30, 40, 60, double.NaN, 15, double.NegativeInfinity },
                    highValues: new double?[] { 60, 70, 80, 20, 90, 80, 90, 100 })
            });
            SkiaChartStyle style = Style();
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            foreach (float radius in new[] { 0f, 5f, float.NaN, float.PositiveInfinity })
            {
                style.HitTestRadius = radius;
                for (int y = 24; y < 423; y += 23)
                    for (int x = 17; x < 617; x += 10)
                        EqualHit(reference.HitTest(new SKPoint(x, y), Bounds, snapshot, style),
                            indexed.HitTest(new SKPoint(x, y), Bounds, snapshot, style));
            }
        }

        [Fact]
        public void Warm_Queries_Read_No_Boundary_Or_X_Elements_And_Allocate_Nothing()
        {
            const int count = 20000;
            Counted<double?> low = new(Enumerable.Repeat<double?>(20, count).ToArray());
            Counted<double?> high = new(Enumerable.Repeat<double?>(60, count).ToArray());
            Counted<double> x = new(Enumerable.Range(0, count).Select(i => 1 + i * 100d / (count - 1)).ToArray());
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
            {
                new ChartSeriesSnapshot("Counted", ChartSeriesKind.RangeArea, new double?[count], x, lowValues: low, highValues: high)
            });
            SkiaChartRenderer renderer = new();
            SkiaChartStyle style = Style();
            SKPoint point = new(Bounds.MidX, Bounds.MidY);
            for (int i = 0; i < 1000; i++) renderer.HitTest(point, Bounds, snapshot, style);
            low.Reads = high.Reads = x.Reads = 0;
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) renderer.HitTest(point, Bounds, snapshot, style);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - bytes);
            Assert.Equal(0, low.Reads + high.Reads + x.Reads);
            renderer.ClearInteractionCache();
            Assert.NotNull(renderer.HitTest(point, Bounds, snapshot, style));
            Assert.True(low.Reads > 0 && high.Reads > 0 && x.Reads > 0);
        }

        [Fact]
        public void Snapshot_Style_Bounds_And_Explicit_Clearing_Invalidate_Range_Geometry()
        {
            double?[] low = { 20, 20 }, high = { 60, 60 };
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
                { new ChartSeriesSnapshot("Raw", ChartSeriesKind.RangeArea, new double?[2], new double[] { 1, 101 }, lowValues: low, highValues: high) });
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            SkiaChartStyle style = Style();
            SKPoint point = new(Bounds.MidX, Bounds.MidY);
            Assert.NotNull(indexed.HitTest(point, Bounds, snapshot, style));
            low[0] = low[1] = 80; high[0] = high[1] = 90;
            indexed.ClearInteractionCache();
            Assert.Null(indexed.HitTest(point, Bounds, snapshot, style));
            foreach (double? minimum in new double?[] { null, 0, 50, -100, 1 })
            {
                style.ValueAxisMinimum = minimum;
                foreach (SKRect bounds in new[] { Bounds, new SKRect(0, 0, 320, 240) })
                {
                    SKPoint p = new(bounds.MidX, bounds.MidY);
                    EqualHit(reference.HitTest(p, bounds, snapshot, style), indexed.HitTest(p, bounds, snapshot, style));
                    Assert.Equal(reference.TryGetViewportInfo(bounds, snapshot, style, out var expected),
                        indexed.TryGetViewportInfo(bounds, snapshot, style, out var actual));
                    Assert.Equal(expected.MinValue, actual.MinValue); Assert.Equal(expected.MaxValue, actual.MaxValue);
                }
            }
            ChartDataSnapshot next = new(snapshot.Categories, new[] { ChartRangeSeries.CreateArea("Next", new double?[] { 10, 10 }, new double?[] { 95, 95 }) });
            EqualHit(reference.HitTest(point, Bounds, next, style), indexed.HitTest(point, Bounds, next, style));
            indexed.UseInteractionCache = false;
            EqualHit(reference.HitTest(point, Bounds, snapshot, style), indexed.HitTest(point, Bounds, snapshot, style));
            indexed.UseInteractionCache = true;
            EqualHit(reference.HitTest(point, Bounds, next, style), indexed.HitTest(point, Bounds, next, style));
        }

        [Fact]
        public void Layout_Reuse_Does_Not_Change_Raster_Output_Or_Leak_A_Native_Canvas()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B", "C", "D" }, new[]
            {
                ChartRangeSeries.CreateArea("Band", new double?[] { 10, 20, null, 30 }, new double?[] { 50, 75, null, 60 }),
                new ChartSeriesSnapshot("Mean", ChartSeriesKind.Line, new double?[] { 30, 45, null, 50 })
            });
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            using SKBitmap actual = new(640, 440), expected = new(640, 440);
            using SKCanvas a = new(actual), b = new(expected);
            SkiaChartStyle style = Style(ChartAxisKind.Category);
            indexed.HitTest(new SKPoint(100, 200), Bounds, snapshot, style);
            indexed.Render(a, Bounds, snapshot, style);
            reference.Render(b, Bounds, snapshot, style);
            Assert.Equal(expected.Bytes, actual.Bytes);
        }

        private static void EqualHit(SkiaChartHitTestResult? expected, SkiaChartHitTestResult? actual)
        {
            Assert.Equal(expected.HasValue, actual.HasValue);
            if (!expected.HasValue) return;
            var a = expected.Value; var b = actual!.Value;
            Assert.Equal(a.SeriesIndex, b.SeriesIndex); Assert.Equal(a.PointIndex, b.PointIndex);
            Assert.Equal(a.Value, b.Value); Assert.Equal(a.LowValue, b.LowValue); Assert.Equal(a.HighValue, b.HighValue);
            Assert.Equal(a.XValue, b.XValue); Assert.Equal(a.Category, b.Category); Assert.Equal(a.SeriesName, b.SeriesName);
            Assert.Equal(a.SeriesKind, b.SeriesKind); Assert.Equal(a.Location, b.Location);
        }

        private sealed class Counted<T> : IReadOnlyList<T>
        {
            private readonly T[] _values;
            public Counted(T[] values) { _values = values; }
            public long Reads { get; set; }
            public int Count => _values.Length;
            public T this[int index] { get { Reads++; return _values[index]; } }
            public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
