// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartInteractionRenderTests
    {
        [Fact]
        public void Mixed_Indexed_Families_Render_And_Select_Identically_With_Or_Without_Caching()
        {
            ChartDataSnapshot data = new(new string?[] { "A", "B", "C" }, new[]
            {
                new ChartSeriesSnapshot("Area", ChartSeriesKind.Area, new double?[] { 10, null, 20 }),
                new ChartSeriesSnapshot("Line", ChartSeriesKind.Line, new double?[] { 20, 50, 30 }),
                new ChartSeriesSnapshot("Scatter", ChartSeriesKind.Scatter, new double?[] { 35, 45, 75 }, new double[] { 80, 10, 60 }),
                new ChartSeriesSnapshot("Bubble", ChartSeriesKind.Bubble, new double?[] { 60, 55, 25 }, new double[] { 35, 65, 45 }, new double?[] { 1, 5, 10 })
            });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, CategoryAxisKind = ChartAxisKind.Value,
                CategoryAxisMinimum = 0, CategoryAxisMaximum = 100, ValueAxisMinimum = 0, ValueAxisMaximum = 100
            };
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            SKRect bounds = SKRect.Create(320, 240);
            using SKBitmap a = new(320, 240);
            using SKBitmap b = new(320, 240);
            using SKCanvas ca = new(a);
            using SKCanvas cb = new(b);
            using SkiaChartRenderCache cache = new();
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 1) style.ValueAxisMaximum = 150;
                if (pass == 2) indexed.ClearInteractionCache();
                indexed.Render(ca, bounds, data, style, cache);
                reference.Render(cb, bounds, data, style);
                Assert.Equal(b.Bytes, a.Bytes);
                for (int y = 10; y < 240; y += 17)
                    for (int x = 10; x < 320; x += 13)
                    {
                        SKPoint point = new(x, y);
                        Assert.Equal(reference.HitTest(point, bounds, data, style), indexed.HitTest(point, bounds, data, style));
                    }
            }
        }

        [Fact]
        public void Unsupported_Mixed_Plots_Retain_Reference_Selection()
        {
            ChartDataSnapshot data = new(new string?[] { "A", "B" }, new[]
            {
                new ChartSeriesSnapshot("Line", ChartSeriesKind.Line, new double?[] { 20, 80 }),
                new ChartSeriesSnapshot("Column", ChartSeriesKind.Column, new double?[] { 40, 60 })
            });
            SkiaChartRenderer indexed = new(), reference = new() { UseInteractionCache = false };
            for (int y = 10; y < 200; y += 19)
                for (int x = 10; x < 300; x += 13)
                {
                    SKPoint point = new(x, y);
                    Assert.Equal(reference.HitTest(point, SKRect.Create(300, 200), data),
                        indexed.HitTest(point, SKRect.Create(300, 200), data));
                }
        }
    }
}
