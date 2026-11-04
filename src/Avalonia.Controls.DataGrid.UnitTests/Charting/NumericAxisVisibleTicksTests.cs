// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using ProCharts;
using ProCharts.Skia;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class NumericAxisVisibleTicksTests
    {
        [Theory]
        [InlineData(ChartSeriesKind.Line)]
        [InlineData(ChartSeriesKind.Bar)]
        [InlineData(ChartSeriesKind.RangeArea)]
        public void Linear_Value_Axes_Never_Label_OutOfRange_Ticks_At_Clamped_Edges(ChartSeriesKind kind)
        {
            List<double> formatted = new();
            ChartSeriesSnapshot series = kind == ChartSeriesKind.RangeArea
                ? ChartRangeSeries.CreateArea("Range", new double?[] { 2, 2 }, new double?[] { 9, 9 })
                : new ChartSeriesSnapshot("Values", kind, new double?[] { 2, 9 });
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B" }, new[] { series });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, ValueAxisMinimum = 2, ValueAxisMaximum = 9,
                AxisLabelFormatter = value => { formatted.Add(value); return value.ToString("G17", CultureInfo.InvariantCulture); }
            };
            Assert.NotEmpty(SkiaChartExporter.ExportPng(snapshot, 640, 400, style));
            Assert.NotEmpty(formatted);
            Assert.All(formatted, value => Assert.InRange(value, 2, 9));
            Assert.Contains(8d, formatted);
            Assert.DoesNotContain(10d, formatted);
            Assert.DoesNotContain(0d, formatted);
        }

        [Fact]
        public void Numeric_Category_Axes_Only_Format_Values_Inside_Their_Explicit_Domain()
        {
            List<double> formatted = new();
            ChartDataSnapshot snapshot = new(Array.Empty<string?>(), new[]
                { ChartRangeSeries.CreateArea("Range", new double?[] { 20, 20 }, new double?[] { 60, 60 }, new double[] { 2, 9 }) });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, CategoryAxisKind = ChartAxisKind.Value,
                CategoryAxisMinimum = 2, CategoryAxisMaximum = 9,
                CategoryAxisLabelFormatter = value => { formatted.Add(value); return value.ToString("G17", CultureInfo.InvariantCulture); }
            };
            Assert.NotEmpty(SkiaChartExporter.ExportSvg(snapshot, 640, 400, style));
            Assert.NotEmpty(formatted);
            Assert.All(formatted, value => Assert.InRange(value, 2, 9));
        }

        [Fact]
        public void Automatic_Interval_Axis_Does_Not_Mislabel_The_Upper_Boundary_As_The_Next_Nice_Tick()
        {
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B", "C" }, new[]
                { ChartRangeSeries.CreateArea("Forecast", new double?[] { 10, 20, 15 }, new double?[] { 30, 45, 40 }) });
            string svg = SkiaChartExporter.ExportSvg(snapshot, 800, 480, new SkiaChartStyle { ShowLegend = false });
            string[] labels = XDocument.Parse(svg).Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value.Trim()).ToArray();
            Assert.Contains("40", labels);
            Assert.DoesNotContain("50", labels);
        }

        [Fact]
        public void Adjacent_Large_Values_And_Huge_Tick_Density_Terminate_With_Finite_InRange_Labels()
        {
            double minimum = 1e16, maximum = Math.BitIncrement(1e16);
            List<double> formatted = new();
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B" }, new[]
                { ChartRangeSeries.CreateArea("Narrow", new double?[] { minimum, minimum }, new double?[] { maximum, maximum }) });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, AxisTickCount = int.MaxValue,
                ValueAxisMinimum = minimum, ValueAxisMaximum = maximum,
                AxisLabelFormatter = value => { formatted.Add(value); return value.ToString("G17", CultureInfo.InvariantCulture); }
            };
            string svg = SkiaChartExporter.ExportSvg(snapshot, 640, 400, style);
            Assert.DoesNotContain("NaN", svg); Assert.DoesNotContain("Infinity", svg);
            Assert.InRange(formatted.Count, 2, 10000);
            Assert.All(formatted, value => Assert.InRange(value, minimum, maximum));
            Assert.Contains(minimum, formatted); Assert.Contains(maximum, formatted);
        }

        [Fact]
        public void Opposite_Extreme_Bounds_Have_Finite_Ticks_And_A_Centered_Zero_Label()
        {
            List<double> formatted = new();
            ChartDataSnapshot snapshot = new(new string?[] { "A", "B" }, new[]
                { ChartRangeSeries.CreateArea("Extreme", new double?[] { -double.MaxValue, -double.MaxValue }, new double?[] { double.MaxValue, double.MaxValue }) });
            SkiaChartStyle style = new()
            {
                ShowLegend = false, PaddingTop = 20, PaddingBottom = 20, ShowCategoryLabels = false,
                ValueAxisMinimum = -double.MaxValue, ValueAxisMaximum = double.MaxValue,
                AxisLabelFormatter = value => { formatted.Add(value); return value == 0 ? "ZERO" : value.ToString("G5", CultureInfo.InvariantCulture); }
            };
            string svg = SkiaChartExporter.ExportSvg(snapshot, 800, 400, style);
            Assert.DoesNotContain("NaN", svg); Assert.DoesNotContain("Infinity", svg);
            Assert.NotEmpty(formatted); Assert.All(formatted, value => Assert.True(double.IsFinite(value)));
            var zero = Assert.Single(XDocument.Parse(svg).Descendants(), e => e.Name.LocalName == "text" && e.Value.Trim() == "ZERO");
            double y = double.Parse(zero.Attribute("y")!.Value.Split(',')[0], CultureInfo.InvariantCulture);
            Assert.InRange(y, 185, 215);
        }
    }
}
