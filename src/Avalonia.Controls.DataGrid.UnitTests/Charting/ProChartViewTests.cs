// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;
using SkiaSharp;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ProChartViewTests
    {
        private static readonly SKColor DarkChartBackground = new(18, 25, 38);

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
                MinWindowCount = 2,
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
        public void ProChartView_DefaultToolTip_Uses_Axis_ValueFormat()
        {
            var model = new ChartModel
            {
                DataSource = new SingleSeriesSource(79.14999999999999d)
            };
            model.Legend.IsVisible = false;
            model.CategoryAxis.IsVisible = false;
            model.ValueAxis.IsVisible = false;
            model.ValueAxis.Minimum = 0;
            model.ValueAxis.ValueFormat = new ChartValueFormat
            {
                MinimumFractionDigits = 0,
                MaximumFractionDigits = 2,
                RoundingMode = MidpointRounding.AwayFromZero,
                Culture = CultureInfo.InvariantCulture
            };

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                MinWindowCount = 2,
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
                var tip = FindToolTip(chartView, chartView, pointer);

                Assert.NotNull(tip);
                Assert.Contains("79.15", tip!, StringComparison.Ordinal);
                Assert.DoesNotContain("79.149999", tip!, StringComparison.Ordinal);
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
                MinWindowCount = 2,
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

        [AvaloniaFact]
        public void ProChartView_VerticalPan_Updates_ValueAxisRange()
        {
            var model = new ChartModel
            {
                DataSource = new MultiCategorySource()
            };
            model.Legend.IsVisible = false;
            model.CategoryAxis.IsVisible = false;
            model.ValueAxis.IsVisible = true;

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                MinWindowCount = 2,
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
                var start = new Point(200, 220);
                var move = new Point(200, 110);

                chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, start));
                chartView.RaiseEvent(CreatePointerMovedArgs(chartView, chartView, pointer, move));
                chartView.RaiseEvent(CreatePointerReleasedArgs(chartView, chartView, pointer, move));

                Assert.True(model.ValueAxis.Minimum.HasValue);
                Assert.True(model.ValueAxis.Maximum.HasValue);
                Assert.True(model.ValueAxis.Maximum.Value > model.ValueAxis.Minimum.Value);
                Assert.True(model.ValueAxis.Maximum.Value < 20d);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_ViewportChartModel_Pans_Target_Window()
        {
            var renderModel = new ChartModel
            {
                DataSource = new MultiCategorySource()
            };
            renderModel.Legend.IsVisible = false;
            renderModel.CategoryAxis.IsVisible = false;
            renderModel.ValueAxis.IsVisible = false;
            renderModel.ValueAxis.Minimum = 0;
            renderModel.Request.WindowStart = 5;
            renderModel.Request.WindowCount = 5;

            var viewportModel = new ChartModel
            {
                DataSource = new MultiCategorySource()
            };
            viewportModel.Legend.IsVisible = false;
            viewportModel.CategoryAxis.IsVisible = false;
            viewportModel.ValueAxis.IsVisible = false;
            viewportModel.ValueAxis.Minimum = 0;
            viewportModel.Request.WindowStart = 5;
            viewportModel.Request.WindowCount = 5;

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                MinWindowCount = 2,
                ChartModel = renderModel,
                ViewportChartModel = viewportModel,
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
            renderModel.Refresh();
            viewportModel.Refresh();
            chartView.UpdateLayout();

            try
            {
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
                var start = new Point(200, 150);
                var move = new Point(320, 150);

                chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, start));
                chartView.RaiseEvent(CreatePointerMovedArgs(chartView, chartView, pointer, move));
                chartView.RaiseEvent(CreatePointerReleasedArgs(chartView, chartView, pointer, move));

                Assert.Equal(5, renderModel.Request.WindowStart);
                Assert.NotNull(viewportModel.Request.WindowStart);
                Assert.True(viewportModel.Request.WindowStart < 5);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_Disabled_HoverTracking_Does_Not_Update_Crosshair_State()
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
            model.Request.WindowCount = 10;

            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300,
                ChartModel = model,
                EnableHoverTracking = false,
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

                chartView.RaiseEvent(CreatePointerHoverArgs(chartView, chartView, pointer, new Point(220, 140)));

                Assert.False(model.Interaction.IsCrosshairVisible);
                Assert.Null(model.Interaction.CrosshairCategoryIndex);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_Defaults_To_Wheel_Zoom_Without_Modifier()
        {
            var chartView = new ProChartView
            {
                Width = 400,
                Height = 300
            };

            Assert.Equal(KeyModifiers.None, chartView.ZoomModifiers);
        }

        [AvaloniaFact]
        public void ProChartView_Hover_Updates_Crosshair_State()
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
            model.Request.WindowCount = 10;

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

                chartView.RaiseEvent(CreatePointerHoverArgs(chartView, chartView, pointer, new Point(220, 140)));

                Assert.True(model.Interaction.IsCrosshairVisible);
                Assert.NotNull(model.Interaction.CrosshairCategoryIndex);
                Assert.NotNull(model.Interaction.CrosshairCategoryLabel);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_DoubleClick_Shows_Latest_Window()
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
                var position = new Point(200, 150);

                chartView.RaiseEvent(CreatePointerPressedArgs(chartView, chartView, pointer, position, clickCount: 2));

                Assert.Equal(15, model.Request.WindowStart);
                Assert.Equal(5, model.Request.WindowCount);
                Assert.True(model.Interaction.FollowLatest);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_PointerTool_Gates_Crosshair_Hover_State()
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
            model.Request.WindowCount = 10;
            model.Interaction.PointerTool = ChartPointerTool.Pan;

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

                chartView.RaiseEvent(CreatePointerHoverArgs(chartView, chartView, pointer, new Point(220, 140)));

                Assert.False(model.Interaction.IsCrosshairVisible);

                model.Interaction.PointerTool = ChartPointerTool.Crosshair;

                chartView.RaiseEvent(CreatePointerHoverArgs(chartView, chartView, pointer, new Point(220, 140)));

                Assert.True(model.Interaction.IsCrosshairVisible);
                Assert.NotNull(model.Interaction.CrosshairCategoryIndex);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_Applies_Dark_Surface_When_WindowTheme_Is_Dark()
        {
            var chartView = CreateRenderTestChartView();
            var window = CreateHostWindow(chartView, ThemeVariant.Dark);

            try
            {
                window.Show();
                PumpLayout(window);

                var pixel = CapturePixel(chartView, 12, 12);
                Assert.Equal(DarkChartBackground, pixel);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_Preserves_Explicit_Surface_Color_In_Dark_Mode()
        {
            var explicitBackground = new SKColor(34, 48, 72);
            var chartView = CreateRenderTestChartView();
            chartView.ChartStyle = new SkiaChartStyle
            {
                Background = explicitBackground
            };

            var window = CreateHostWindow(chartView, ThemeVariant.Dark);

            try
            {
                window.Show();
                PumpLayout(window);

                var pixel = CapturePixel(chartView, 12, 12);
                Assert.Equal(explicitBackground, pixel);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ProChartView_ReRenders_When_WindowThemeVariant_Changes()
        {
            var chartView = CreateRenderTestChartView();
            var window = CreateHostWindow(chartView, ThemeVariant.Light);

            try
            {
                window.Show();
                PumpLayout(window);

                var lightPixel = CapturePixel(chartView, 12, 12);
                Assert.Equal(SKColors.White, lightPixel);

                window.RequestedThemeVariant = ThemeVariant.Dark;
                PumpLayout(window);

                var darkPixel = CapturePixel(chartView, 12, 12);
                Assert.Equal(DarkChartBackground, darkPixel);
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

        private static ProChartView CreateRenderTestChartView()
        {
            var model = new ChartModel
            {
                DataSource = new SingleSeriesSource()
            };
            model.Legend.IsVisible = false;
            model.ValueAxis.Minimum = 0;

            return new ProChartView
            {
                Width = 320,
                Height = 240,
                ChartModel = model
            };
        }

        private static Window CreateHostWindow(Control content, ThemeVariant themeVariant)
        {
            return new Window
            {
                Width = 320,
                Height = 240,
                RequestedThemeVariant = themeVariant,
                Content = content
            };
        }

        private static SKColor CapturePixel(ProChartView chartView, int x, int y)
        {
            var png = chartView.ExportPng();
            Assert.NotEmpty(png);

            using var stream = new MemoryStream(png);
            using var bitmap = SKBitmap.Decode(stream);
            Assert.NotNull(bitmap);

            return bitmap!.GetPixel(x, y);
        }

        private static void PumpLayout(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        private static PointerPressedEventArgs CreatePointerPressedArgs(
            Control source,
            Visual root,
            IPointer pointer,
            Point position,
            int clickCount = 1)
        {
            var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
            return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, clickCount);
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

            public SingleSeriesSource(double value = 10d)
            {
                _snapshot = new ChartDataSnapshot(
                    new[] { "A" },
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Column, new double?[] { value }) });
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
