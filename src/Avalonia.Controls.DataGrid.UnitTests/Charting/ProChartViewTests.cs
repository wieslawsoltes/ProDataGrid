// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ProChartViewTests
    {
        [AvaloniaFact]
        public void ProChartView_Shows_ToolTip_From_HitTest()
        {
            var model = new ChartModel
            {
                DataSource = new SingleSeriesSource()
            };
            model.Legend.IsVisible = false;
            model.CategoryAxis.IsVisible = false;
            model.ValueAxis.IsVisible = false;
            model.ValueAxis.Minimum = 0;

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                ChartModel = model,
                ChartStyle = new SkiaChartStyle
                {
                    PaddingLeft = 0,
                    PaddingRight = 0,
                    PaddingTop = 0,
                    PaddingBottom = 0
                },
                ToolTipFormatter = hit =>
                    $"Series:{hit.Category}:{hit.Value.ToString(CultureInfo.InvariantCulture)}"
            };

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = chartView
            };

            window.Show();
            chartView.UpdateLayout();
            model.Refresh();
            chartView.UpdateLayout();

            try
            {
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
                var tip = FindToolTip(chartView, chartView, pointer);

                Assert.Equal("Series:A:10", tip);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_Pan_Updates_WindowStart()
        {
            var model = new ChartModel
            {
                DataSource = new MultiCategorySource()
            };
            model.Legend.IsVisible = false;
            model.CategoryAxis.IsVisible = false;
            model.ValueAxis.IsVisible = false;
            model.ValueAxis.Minimum = 0;
            model.Request.WindowStart = 5;
            model.Request.WindowCount = 5;

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                ChartModel = model,
                ChartStyle = new SkiaChartStyle
                {
                    PaddingLeft = 0,
                    PaddingRight = 0,
                    PaddingTop = 0,
                    PaddingBottom = 0
                }
            };

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = chartView
            };

            window.Show();
            chartView.UpdateLayout();
            model.Refresh();
            chartView.UpdateLayout();

            try
            {
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
                var start = new Point(200, 150);
                var move = new Point(320, 150);

                chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, start));
                chartView.RaiseEvent(CreatePointerMovedArgs(chartView, chartView, pointer, move));
                chartView.RaiseEvent(CreatePointerReleasedArgs(chartView, chartView, pointer, move));

                Assert.NotNull(model.Request.WindowStart);
                Assert.True(model.Request.WindowStart < 5);
            }
            finally
            {
                window.Close();
            }
        }

        private static string? FindToolTip(ProChartView view, Visual root, IPointer pointer)
        {
            var width = Math.Max(1, (int)Math.Round(view.Bounds.Width));
            var height = Math.Max(1, (int)Math.Round(view.Bounds.Height));

            for (var y = 1; y < height; y += 10)
            {
                for (var x = 1; x < width; x += 10)
                {
                    view.RaiseEvent(CreatePointerHoverArgs(view, root, pointer, new Point(x, y)));
                    if (ToolTip.GetTip(view) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private static PointerPressedEventArgs CreatePointerPressedArgs(
            Control source,
            Visual root,
            IPointer pointer,
            Point position)
        {
            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None);
        }

        private static PointerEventArgs CreatePointerMovedArgs(
            Control source,
            Visual root,
            IPointer pointer,
            Point position)
        {
            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
            return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, KeyModifiers.None);
        }

        private static PointerEventArgs CreatePointerHoverArgs(
            Control source,
            Visual root,
            IPointer pointer,
            Point position)
        {
            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
            return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, KeyModifiers.None);
        }

        private static PointerReleasedEventArgs CreatePointerReleasedArgs(
            Control source,
            Visual root,
            IPointer pointer,
            Point position)
        {
            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            return new PointerReleasedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, MouseButton.Left);
        }

        private sealed class SingleSeriesSource : IChartDataSource
        {
            private readonly ChartDataSnapshot _snapshot;

            public SingleSeriesSource()
            {
                _snapshot = new ChartDataSnapshot(
                    new[] { "A" },
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Column, new double?[] { 10d }) });
            }

            public event EventHandler? DataInvalidated;

            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => _snapshot;

            public void Invalidate() => DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private sealed class MultiCategorySource : IChartDataSource
        {
            private readonly ChartDataSnapshot _snapshot;

            public MultiCategorySource()
            {
                var categories = new string?[20];
                var values = new double?[20];
                for (var i = 0; i < categories.Length; i++)
                {
                    categories[i] = $"C{i + 1}";
                    values[i] = i + 1;
                }

                _snapshot = new ChartDataSnapshot(
                    categories,
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Line, values) });
            }

            public event EventHandler? DataInvalidated;

            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => _snapshot;

            public void Invalidate() => DataInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
