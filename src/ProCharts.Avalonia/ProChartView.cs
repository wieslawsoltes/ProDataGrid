// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Skia;
using SkiaSharp;

namespace ProCharts.Avalonia
{
    public sealed class ProChartView : Control
    {
        private static readonly SkiaChartStyle DefaultStyle = new();
        private static readonly SkiaChartTheme DarkThemeDefaults = new()
        {
            Background = new SKColor(18, 25, 38),
            Axis = new SKColor(96, 108, 128),
            Text = new SKColor(230, 237, 247),
            Gridline = new SKColor(48, 57, 74),
            DataLabelBackground = new SKColor(27, 36, 52, 216),
            DataLabelText = new SKColor(242, 245, 250)
        };

        public static readonly StyledProperty<ChartModel?> ChartModelProperty =
            AvaloniaProperty.Register<ProChartView, ChartModel?>(nameof(ChartModel));

        public static readonly StyledProperty<SkiaChartStyle?> ChartStyleProperty =
            AvaloniaProperty.Register<ProChartView, SkiaChartStyle?>(nameof(ChartStyle));

        public static readonly StyledProperty<bool> ShowToolTipsProperty =
            AvaloniaProperty.Register<ProChartView, bool>(nameof(ShowToolTips), true);

        public static readonly StyledProperty<Func<SkiaChartHitTestResult, string>?> ToolTipFormatterProperty =
            AvaloniaProperty.Register<ProChartView, Func<SkiaChartHitTestResult, string>?>(nameof(ToolTipFormatter));

        public static readonly StyledProperty<bool> EnablePanZoomProperty =
            AvaloniaProperty.Register<ProChartView, bool>(nameof(EnablePanZoom), true);

        public static readonly StyledProperty<ChartModel?> ViewportChartModelProperty =
            AvaloniaProperty.Register<ProChartView, ChartModel?>(nameof(ViewportChartModel));

        public static readonly StyledProperty<MouseButton> PanButtonProperty =
            AvaloniaProperty.Register<ProChartView, MouseButton>(nameof(PanButton), MouseButton.Left);

        public static readonly StyledProperty<KeyModifiers> PanModifiersProperty =
            AvaloniaProperty.Register<ProChartView, KeyModifiers>(nameof(PanModifiers), KeyModifiers.None);

        public static readonly StyledProperty<KeyModifiers> ZoomModifiersProperty =
            AvaloniaProperty.Register<ProChartView, KeyModifiers>(nameof(ZoomModifiers), KeyModifiers.None);

        public static readonly StyledProperty<double> ZoomStepProperty =
            AvaloniaProperty.Register<ProChartView, double>(nameof(ZoomStep), 0.2d);

        public static readonly StyledProperty<int> MinWindowCountProperty =
            AvaloniaProperty.Register<ProChartView, int>(nameof(MinWindowCount), 10);

        public static readonly StyledProperty<bool> ShowCrosshairProperty =
            AvaloniaProperty.Register<ProChartView, bool>(nameof(ShowCrosshair), true);

        public static readonly StyledProperty<bool> EnableHoverTrackingProperty =
            AvaloniaProperty.Register<ProChartView, bool>(nameof(EnableHoverTracking), true);

        public static readonly StyledProperty<bool> EnableKeyboardNavigationProperty =
            AvaloniaProperty.Register<ProChartView, bool>(nameof(EnableKeyboardNavigation), true);

        private readonly SkiaChartRenderer _renderer = new();
        private readonly SkiaChartRenderCache _renderCache = new();
        private WriteableBitmap? _bitmap;
        private Size _lastSize;
        private double _lastScaling = 1d;
        private bool _isDirty = true;
        private SkiaChartHitTestResult? _lastHit;
        private string? _lastToolTipText;
        private bool _isPanning;
        private Point _panStartPoint;
        private int _panStartWindowStart;
        private int _panStartWindowCount;
        private ChartModel? _panTargetModel;
        private ChartModel? _panRenderModel;
        private SkiaChartViewportInfo _panViewport;
        private bool _hasPanViewport;
        private bool _panCanPanWindow;
        private bool _panCanPanValue;
        private double _panStartValueMinimum;
        private double _panStartValueMaximum;
        private IPointer? _panPointer;
        private ChartPointerTool _lastPointerTool = ChartPointerTool.Crosshair;
        private bool _isSelectionDragActive;
        private ChartPointerTool _selectionTool;
        private Point _selectionStartPoint;
        private Point _selectionEndPoint;
        private int _selectionStartCategoryIndex;
        private int _selectionEndCategoryIndex;
        private string? _selectionStartCategoryLabel;
        private string? _selectionEndCategoryLabel;
        private double _selectionStartValue;
        private double _selectionEndValue;
        private bool _hasMeasurementOverlay;
        private ChartModel? _selectionTargetModel;

        static ProChartView()
        {
            AffectsRender<ProChartView>(ChartModelProperty, ChartStyleProperty, ShowCrosshairProperty);
        }

        public ProChartView()
        {
            Focusable = true;
            ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }

        public ChartModel? ChartModel
        {
            get => GetValue(ChartModelProperty);
            set => SetValue(ChartModelProperty, value);
        }

        public SkiaChartStyle? ChartStyle
        {
            get => GetValue(ChartStyleProperty);
            set => SetValue(ChartStyleProperty, value);
        }

        public bool ShowToolTips
        {
            get => GetValue(ShowToolTipsProperty);
            set => SetValue(ShowToolTipsProperty, value);
        }

        public Func<SkiaChartHitTestResult, string>? ToolTipFormatter
        {
            get => GetValue(ToolTipFormatterProperty);
            set => SetValue(ToolTipFormatterProperty, value);
        }

        public bool EnablePanZoom
        {
            get => GetValue(EnablePanZoomProperty);
            set => SetValue(EnablePanZoomProperty, value);
        }

        /// <summary>
        /// Gets or sets the chart model that receives viewport operations such as pan, zoom, keyboard navigation, and latest-window resets.
        /// </summary>
        /// <remarks>
        /// When not set, viewport operations apply to <see cref="ChartModel"/>. Use this to build stacked panes that share one primary viewport
        /// while rendering different data series in each pane.
        /// </remarks>
        public ChartModel? ViewportChartModel
        {
            get => GetValue(ViewportChartModelProperty);
            set => SetValue(ViewportChartModelProperty, value);
        }

        public MouseButton PanButton
        {
            get => GetValue(PanButtonProperty);
            set => SetValue(PanButtonProperty, value);
        }

        public KeyModifiers PanModifiers
        {
            get => GetValue(PanModifiersProperty);
            set => SetValue(PanModifiersProperty, value);
        }

        public KeyModifiers ZoomModifiers
        {
            get => GetValue(ZoomModifiersProperty);
            set => SetValue(ZoomModifiersProperty, value);
        }

        public double ZoomStep
        {
            get => GetValue(ZoomStepProperty);
            set => SetValue(ZoomStepProperty, value);
        }

        public int MinWindowCount
        {
            get => GetValue(MinWindowCountProperty);
            set => SetValue(MinWindowCountProperty, value);
        }

        public bool ShowCrosshair
        {
            get => GetValue(ShowCrosshairProperty);
            set => SetValue(ShowCrosshairProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether hover movement updates tooltip and crosshair state.
        /// </summary>
        public bool EnableHoverTracking
        {
            get => GetValue(EnableHoverTrackingProperty);
            set => SetValue(EnableHoverTrackingProperty, value);
        }

        public bool EnableKeyboardNavigation
        {
            get => GetValue(EnableKeyboardNavigationProperty);
            set => SetValue(EnableKeyboardNavigationProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ChartModelProperty)
            {
                if (change.OldValue is ChartModel oldModel)
                {
                    WeakEventHandlerManager.Unsubscribe<ChartDataUpdateEventArgs, ProChartView>(
                        oldModel,
                        nameof(ChartModel.SnapshotUpdated),
                        OnSnapshotUpdated);
                    WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, ProChartView>(
                        oldModel,
                        nameof(INotifyPropertyChanged.PropertyChanged),
                        OnChartModelPropertyChanged);
                }

                if (change.NewValue is ChartModel newModel)
                {
                    WeakEventHandlerManager.Subscribe<ChartModel, ChartDataUpdateEventArgs, ProChartView>(
                        newModel,
                        nameof(ChartModel.SnapshotUpdated),
                        OnSnapshotUpdated);
                    WeakEventHandlerManager.Subscribe<ChartModel, PropertyChangedEventArgs, ProChartView>(
                        newModel,
                        nameof(INotifyPropertyChanged.PropertyChanged),
                        OnChartModelPropertyChanged);
                }

                _isDirty = true;
                EndPan();
                ClearSelectionOverlay(clearMeasurement: true);
                ClearToolTip();
                InvalidateVisual();
            }
            else if (change.Property == ShowToolTipsProperty)
            {
                if (!ShowToolTips)
                {
                    ClearToolTip();
                }
            }
            else if (change.Property == ToolTipFormatterProperty)
            {
                UpdateToolTipText();
            }
            else if (change.Property == ViewportChartModelProperty || change.Property == EnableHoverTrackingProperty)
            {
                EndPan();
                ClearSelectionOverlay(clearMeasurement: true);
                ClearToolTip();
            }
            else if (change.Property == ShowCrosshairProperty)
            {
                if (!ShowCrosshair)
                {
                    ClearCrosshair();
                }

                InvalidateVisual();
            }
            else if (change.Property == ChartStyleProperty)
            {
                _isDirty = true;
                EndPan();
                ClearToolTip();
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            EnsureBitmap();
            if (_bitmap == null)
            {
                return;
            }

            if (_isDirty)
            {
                RenderToBitmap();
            }

            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                var sourceRect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
                context.DrawImage(_bitmap, sourceRect, new Rect(Bounds.Size));
            }

            DrawInteractionOverlay(context);
        }

        private void OnSnapshotUpdated(object? sender, ChartDataUpdateEventArgs e)
        {
            if (e.Update.Delta.Kind == ChartDataDeltaKind.None)
            {
                EnsureWindowBounds();
                return;
            }

            _isDirty = true;
            ClearToolTip();
            EnsureWindowBounds();
            InvalidateVisual();
        }

        private void OnChartModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChartModel.Interaction))
            {
                var pointerTool = ChartModel?.Interaction.PointerTool ?? ChartPointerTool.Crosshair;
                if (_lastPointerTool != pointerTool)
                {
                    _lastPointerTool = pointerTool;
                    EndPan();
                    ClearSelectionOverlay(clearMeasurement: pointerTool != ChartPointerTool.Measure);
                    ClearToolTip();
                    if (pointerTool != ChartPointerTool.Crosshair)
                    {
                        ClearCrosshair();
                    }
                }

                InvalidateVisual();
                return;
            }

            _isDirty = true;
            ClearToolTip();
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_isPanning)
            {
                UpdatePan(e.GetPosition(this));
                e.Handled = true;
                return;
            }

            if (_isSelectionDragActive)
            {
                UpdateSelectionDrag(e.GetPosition(this));
                e.Handled = true;
                return;
            }

            var position = e.GetPosition(this);
            UpdateHoverState(position);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            ClearToolTip();
            if (!EnableHoverTracking)
            {
                return;
            }

            if (!_isSelectionDragActive)
            {
                ClearCrosshair();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            Focus();

            if (e.ClickCount >= 2)
            {
                ResetToLatestWindow();
                ClearSelectionOverlay(clearMeasurement: true);
                ClearToolTip();
                ClearCrosshair();
                e.Handled = true;
                return;
            }

            var pointerTool = ChartModel?.Interaction.PointerTool ?? ChartPointerTool.Crosshair;
            if (pointerTool == ChartPointerTool.Pan)
            {
                if (!EnablePanZoom)
                {
                    return;
                }

                var panPoint = e.GetCurrentPoint(this);
                if (!TryBeginPan(panPoint))
                {
                    return;
                }

                _panPointer = e.Pointer;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            if ((pointerTool == ChartPointerTool.Zoom || pointerTool == ChartPointerTool.Measure) &&
                TryBeginSelectionDrag(pointerTool, e))
            {
                e.Handled = true;
                return;
            }

            if (!EnablePanZoom || !IsPanGesture(e))
            {
                return;
            }

            var point = e.GetCurrentPoint(this);
            if (!TryBeginPan(point))
            {
                return;
            }

            _panPointer = e.Pointer;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isPanning && (_panPointer == null || ReferenceEquals(e.Pointer, _panPointer)))
            {
                EndPan();
                UpdateHoverState(e.GetPosition(this));
                e.Handled = true;
            }

            if (_isSelectionDragActive && (_panPointer == null || ReferenceEquals(e.Pointer, _panPointer)))
            {
                EndSelectionDrag(e.GetPosition(this));
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            EndPan();
            CancelSelectionDrag();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (!EnablePanZoom || !IsZoomGesture(e))
            {
                return;
            }

            var renderModel = ChartModel;
            var viewportModel = ResolveViewportChartModel();
            if (renderModel == null || viewportModel == null)
            {
                return;
            }

            var step = Math.Max(0.01d, ZoomStep);
            var scale = Math.Pow(1d + step, e.Delta.Y);
            if (Math.Abs(scale - 1d) < 0.0001d)
            {
                return;
            }

            if (!TryGetViewportInfo(renderModel, out var viewport) || !viewport.HasCartesianSeries)
            {
                return;
            }

            var ratio = GetAxisRatio(viewport, e.GetPosition(this));
            if (viewportModel.ZoomWindow(scale, ratio, MinWindowCount))
            {
                ClearToolTip();
                ClearCrosshair();
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!EnableKeyboardNavigation)
            {
                return;
            }

            var model = ResolveViewportChartModel();
            if (model == null)
            {
                return;
            }

            var handled = e.Key switch
            {
                Key.Left => model.PanWindow(-GetKeyboardPanStep(model)),
                Key.Right => model.PanWindow(GetKeyboardPanStep(model)),
                Key.Add or Key.OemPlus => model.ZoomWindow(1d + Math.Max(0.01d, ZoomStep), 0.5d, MinWindowCount),
                Key.Subtract or Key.OemMinus => model.ZoomWindow(1d / (1d + Math.Max(0.01d, ZoomStep)), 0.5d, MinWindowCount),
                Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) => model.RedoWindow(),
                Key.Y when e.KeyModifiers.HasFlag(KeyModifiers.Control) => model.RedoWindow(),
                Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) => model.UndoWindow(),
                Key.Home => model.ResetWindow(false),
                Key.End => ShowLatestWindow(),
                Key.Escape => ClearInteractionOverlaysWithResult(),
                _ => false
            };

            if (!handled)
            {
                return;
            }

            e.Handled = true;
        }

        private void EnsureBitmap()
        {
            var size = Bounds.Size;
            var scaling = this.GetPresentationSource()?.RenderScaling ?? 1d;
            if (_bitmap != null && size.Equals(_lastSize) && Math.Abs(_lastScaling - scaling) < double.Epsilon)
            {
                return;
            }

            _lastSize = size;
            _lastScaling = scaling;

            var pixelWidth = Math.Max(1, (int)Math.Round(size.Width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Round(size.Height * scaling));
            var pixelSize = new PixelSize(pixelWidth, pixelHeight);
            var dpiX = size.Width > 0 ? (pixelWidth / size.Width) * 96 : 96;
            var dpiY = size.Height > 0 ? (pixelHeight / size.Height) * 96 : 96;
            var dpi = new Vector(dpiX, dpiY);

            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
            _isDirty = true;
        }

        private void RenderToBitmap()
        {
            if (_bitmap == null)
            {
                return;
            }

            using var locked = _bitmap.Lock();
            var info = new SKImageInfo(locked.Size.Width, locked.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, locked.Address, locked.RowBytes);
            if (surface == null)
            {
                return;
            }

            var model = ChartModel;
            var snapshot = model?.Snapshot ?? ChartDataSnapshot.Empty;
            var update = model?.LastUpdate;
            var style = BuildEffectiveStyle();
            var canvas = surface.Canvas;
            canvas.Save();
            var scaleX = _lastSize.Width > 0 ? (float)(locked.Size.Width / _lastSize.Width) : 1f;
            var scaleY = _lastSize.Height > 0 ? (float)(locked.Size.Height / _lastSize.Height) : 1f;
            if (Math.Abs(scaleX - 1f) > float.Epsilon || Math.Abs(scaleY - 1f) > float.Epsilon)
            {
                canvas.Scale(scaleX, scaleY);
            }

            var rect = new SKRect(0, 0, (float)_lastSize.Width, (float)_lastSize.Height);
            if (update != null)
            {
                _renderer.Render(canvas, rect, update, style, _renderCache);
            }
            else
            {
                _renderer.Render(canvas, rect, snapshot, style, _renderCache);
            }
            canvas.Restore();
            canvas.Flush();

            _isDirty = false;
        }

        private SkiaChartStyle BuildEffectiveStyle()
        {
            var style = ChartStyle != null ? new SkiaChartStyle(ChartStyle) : new SkiaChartStyle();
            var model = ChartModel;
            if (model == null)
            {
                return style;
            }

            var legend = model.Legend;
            style.LegendPosition = legend.Position;
            style.ShowLegend = legend.IsVisible && legend.Position != ChartLegendPosition.None;

            var categoryAxis = model.CategoryAxis;
            style.ShowCategoryLabels = categoryAxis.IsVisible;
            style.ShowCategoryAxisLine = categoryAxis.IsVisible;
            style.CategoryAxisTitle = categoryAxis.IsVisible ? categoryAxis.Title : null;
            style.CategoryAxisKind = categoryAxis.Kind;
            style.CategoryAxisMinimum = categoryAxis.Minimum;
            style.CategoryAxisMaximum = categoryAxis.Maximum;
            style.CategoryAxisLabelFormatter = categoryAxis.LabelFormatter;
            style.CategoryAxisValueFormat = categoryAxis.ValueFormat;
            style.CategoryAxisCrossing = categoryAxis.Crossing;
            style.CategoryAxisCrossingValue = categoryAxis.CrossingValue;
            style.CategoryAxisOffset = categoryAxis.Offset;
            style.CategoryAxisMinorTickCount = categoryAxis.MinorTickCount;
            style.ShowCategoryMinorTicks = categoryAxis.ShowMinorTicks;
            style.ShowCategoryMinorGridlines = categoryAxis.ShowMinorGridlines;

            var valueAxis = model.ValueAxis;
            style.ShowAxisLabels = valueAxis.IsVisible;
            style.ShowValueAxisLine = valueAxis.IsVisible;
            style.ValueAxisTitle = valueAxis.IsVisible ? valueAxis.Title : null;
            style.ValueAxisMinimum = valueAxis.Minimum;
            style.ValueAxisMaximum = valueAxis.Maximum;
            style.ValueAxisKind = valueAxis.Kind;
            style.AxisLabelFormatter = valueAxis.LabelFormatter;
            style.AxisValueFormat = valueAxis.ValueFormat;
            style.ValueAxisCrossing = valueAxis.Crossing;
            style.ValueAxisCrossingValue = valueAxis.CrossingValue;
            style.ValueAxisOffset = valueAxis.Offset;
            style.ValueAxisMinorTickCount = valueAxis.MinorTickCount;
            style.ShowValueMinorTicks = valueAxis.ShowMinorTicks;
            style.ShowValueMinorGridlines = valueAxis.ShowMinorGridlines;

            var secondaryAxis = model.SecondaryValueAxis;
            style.ShowSecondaryValueAxis = secondaryAxis.IsVisible;
            style.SecondaryValueAxisTitle = secondaryAxis.IsVisible ? secondaryAxis.Title : null;
            style.SecondaryValueAxisMinimum = secondaryAxis.Minimum;
            style.SecondaryValueAxisMaximum = secondaryAxis.Maximum;
            style.SecondaryValueAxisKind = secondaryAxis.Kind;
            style.SecondaryAxisLabelFormatter = secondaryAxis.LabelFormatter;
            style.SecondaryAxisValueFormat = secondaryAxis.ValueFormat;
            style.SecondaryValueAxisCrossing = secondaryAxis.Crossing;
            style.SecondaryValueAxisCrossingValue = secondaryAxis.CrossingValue;
            style.SecondaryValueAxisOffset = secondaryAxis.Offset;
            style.SecondaryValueAxisMinorTickCount = secondaryAxis.MinorTickCount;
            style.ShowSecondaryValueMinorTicks = secondaryAxis.ShowMinorTicks;
            style.ShowSecondaryValueMinorGridlines = secondaryAxis.ShowMinorGridlines;

            var secondaryCategoryAxis = model.SecondaryCategoryAxis;
            style.ShowSecondaryCategoryAxis = secondaryCategoryAxis.IsVisible;
            style.SecondaryCategoryAxisTitle = secondaryCategoryAxis.IsVisible ? secondaryCategoryAxis.Title : null;
            style.SecondaryCategoryAxisKind = secondaryCategoryAxis.Kind;
            style.SecondaryCategoryAxisMinimum = secondaryCategoryAxis.Minimum;
            style.SecondaryCategoryAxisMaximum = secondaryCategoryAxis.Maximum;
            style.SecondaryCategoryAxisLabelFormatter = secondaryCategoryAxis.LabelFormatter;
            style.SecondaryCategoryAxisValueFormat = secondaryCategoryAxis.ValueFormat;
            style.SecondaryCategoryAxisCrossing = secondaryCategoryAxis.Crossing;
            style.SecondaryCategoryAxisCrossingValue = secondaryCategoryAxis.CrossingValue;
            style.SecondaryCategoryAxisOffset = secondaryCategoryAxis.Offset;
            style.SecondaryCategoryAxisMinorTickCount = secondaryCategoryAxis.MinorTickCount;
            style.ShowSecondaryCategoryMinorTicks = secondaryCategoryAxis.ShowMinorTicks;
            style.ShowSecondaryCategoryMinorGridlines = secondaryCategoryAxis.ShowMinorGridlines;

            style.CoreTheme = model.Theme;
            style.CoreSeriesStyles = model.SeriesStyles;
            ApplyThemeVariantDefaults(style, model);

            return style;
        }

        public byte[] ExportPng()
        {
            if (!TryGetExportSizes(out var pixelWidth, out var pixelHeight, out _, out _))
            {
                return Array.Empty<byte>();
            }

            var snapshot = ChartModel?.Snapshot ?? ChartDataSnapshot.Empty;
            var style = BuildEffectiveStyle();
            return SkiaChartExporter.ExportPng(snapshot, pixelWidth, pixelHeight, style);
        }

        public string ExportSvg()
        {
            if (!TryGetExportSizes(out _, out _, out var width, out var height))
            {
                return string.Empty;
            }

            var snapshot = ChartModel?.Snapshot ?? ChartDataSnapshot.Empty;
            var style = BuildEffectiveStyle();
            return SkiaChartExporter.ExportSvg(snapshot, width, height, style);
        }

        public Task CopyToClipboardAsync(ChartClipboardFormat format)
        {
            return format switch
            {
                ChartClipboardFormat.Png => CopyPngToClipboardAsync(),
                ChartClipboardFormat.Svg => CopySvgToClipboardAsync(),
                _ => Task.CompletedTask
            };
        }

        private async Task CopyPngToClipboardAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            var png = ExportPng();
            if (png.Length == 0)
            {
                return;
            }

            using var stream = new MemoryStream(png);
            using var bitmap = new Bitmap(stream);
            await clipboard.SetBitmapAsync(bitmap);
        }

        private async Task CopySvgToClipboardAsync()
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            var svg = ExportSvg();
            if (string.IsNullOrWhiteSpace(svg))
            {
                return;
            }

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, svg);
            item.Set(ChartClipboardFormats.Svg, svg);

            var dataTransfer = new DataTransfer();
            dataTransfer.Add(item);

            await clipboard.SetDataAsync(dataTransfer);
        }

        private bool TryGetExportSizes(
            out int pixelWidth,
            out int pixelHeight,
            out int width,
            out int height)
        {
            var size = Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                pixelWidth = 0;
                pixelHeight = 0;
                width = 0;
                height = 0;
                return false;
            }

            var scaling = this.GetPresentationSource()?.RenderScaling ?? 1d;
            pixelWidth = Math.Max(1, (int)Math.Round(size.Width * scaling));
            pixelHeight = Math.Max(1, (int)Math.Round(size.Height * scaling));
            width = Math.Max(1, (int)Math.Round(size.Width));
            height = Math.Max(1, (int)Math.Round(size.Height));
            return true;
        }

        private void UpdateToolTip(Point point)
        {
            if (!ShowToolTips)
            {
                ClearToolTip();
                return;
            }

            var model = ChartModel;
            if (model == null)
            {
                ClearToolTip();
                return;
            }

            var snapshot = model.Snapshot;
            if (snapshot.Series.Count == 0 || snapshot.Categories.Count == 0)
            {
                ClearToolTip();
                return;
            }

            var hitPoint = new SKPoint((float)point.X, (float)point.Y);
            var bounds = new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height);
            var style = BuildEffectiveStyle();
            var hit = _renderer.HitTest(hitPoint, bounds, snapshot, style);

            if (hit.HasValue)
            {
                var hitValue = hit.Value;
                var text = FormatToolTip(hitValue);
                if (!_lastHit.HasValue || !_lastHit.Value.Equals(hitValue) || _lastToolTipText != text)
                {
                    _lastHit = hitValue;
                    _lastToolTipText = text;
                    ToolTip.SetTip(this, text);
                }

                return;
            }

            ClearToolTip();
        }

        private void UpdateToolTipText()
        {
            if (!_lastHit.HasValue)
            {
                return;
            }

            var text = FormatToolTip(_lastHit.Value);
            if (_lastToolTipText != text)
            {
                _lastToolTipText = text;
                ToolTip.SetTip(this, text);
            }
        }

        private string FormatToolTip(SkiaChartHitTestResult hit)
        {
            var formatter = ToolTipFormatter;
            if (formatter != null)
            {
                return formatter(hit);
            }

            var model = ChartModel;
            var seriesName = string.IsNullOrWhiteSpace(hit.SeriesName)
                ? $"Series {hit.SeriesIndex + 1}"
                : hit.SeriesName!;
            var series = TryGetHitSeriesSnapshot(model, hit.SeriesIndex);
            var valueAxis = ResolveValueAxis(model, series);
            var categoryAxis = ResolveCategoryAxis(model, series);

            if (IsDefaultFinancialTooltip(hit) &&
                hit.OpenValue.HasValue &&
                hit.HighValue.HasValue &&
                hit.LowValue.HasValue &&
                hit.CloseValue.HasValue)
            {
                var header = !string.IsNullOrWhiteSpace(hit.Category)
                    ? $"{seriesName} - {hit.Category}"
                    : seriesName;
                return $"{header}: O {FormatAxisValue(valueAxis, hit.OpenValue.Value)}, H {FormatAxisValue(valueAxis, hit.HighValue.Value)}, L {FormatAxisValue(valueAxis, hit.LowValue.Value)}, C {FormatAxisValue(valueAxis, hit.CloseValue.Value)}";
            }

            if (hit.SeriesKind == ChartSeriesKind.Hlc &&
                hit.HighValue.HasValue &&
                hit.LowValue.HasValue &&
                hit.CloseValue.HasValue)
            {
                var header = !string.IsNullOrWhiteSpace(hit.Category)
                    ? $"{seriesName} - {hit.Category}"
                    : seriesName;
                return $"{header}: H {FormatAxisValue(valueAxis, hit.HighValue.Value)}, L {FormatAxisValue(valueAxis, hit.LowValue.Value)}, C {FormatAxisValue(valueAxis, hit.CloseValue.Value)}";
            }

            if ((hit.SeriesKind == ChartSeriesKind.Scatter || hit.SeriesKind == ChartSeriesKind.Bubble) && hit.XValue.HasValue)
            {
                return $"{seriesName}: ({FormatAxisValue(categoryAxis, hit.XValue.Value)}, {FormatAxisValue(valueAxis, hit.Value)})";
            }

            if (!string.IsNullOrWhiteSpace(hit.Category))
            {
                return $"{seriesName} - {hit.Category}: {FormatAxisValue(valueAxis, hit.Value)}";
            }

            return $"{seriesName}: {FormatAxisValue(valueAxis, hit.Value)}";
        }

        private void ClearToolTip()
        {
            if (!_lastHit.HasValue && _lastToolTipText == null)
            {
                return;
            }

            _lastHit = null;
            _lastToolTipText = null;
            ToolTip.SetTip(this, null);
        }

        private bool IsPanGesture(PointerPressedEventArgs e)
        {
            var modifiers = e.KeyModifiers;
            if ((modifiers & PanModifiers) != PanModifiers)
            {
                return false;
            }

            var point = e.GetCurrentPoint(this);
            return PanButton switch
            {
                MouseButton.Left => point.Properties.IsLeftButtonPressed,
                MouseButton.Middle => point.Properties.IsMiddleButtonPressed,
                MouseButton.Right => point.Properties.IsRightButtonPressed,
                _ => false
            };
        }

        private bool IsZoomGesture(PointerWheelEventArgs e)
        {
            var modifiers = e.KeyModifiers;
            if ((modifiers & ZoomModifiers) != ZoomModifiers)
            {
                return false;
            }

            return Math.Abs(e.Delta.Y) > double.Epsilon;
        }

        private bool TryBeginPan(PointerPoint point)
        {
            var renderModel = ChartModel;
            var viewportModel = ResolveViewportChartModel();
            if (renderModel == null || viewportModel == null)
            {
                return false;
            }

            var total = GetTotalCategoryCount(viewportModel);
            if (total <= 0)
            {
                return false;
            }

            var start = viewportModel.Request.WindowStart ?? 0;
            var count = viewportModel.Request.WindowCount ?? total;
            if (count <= 0)
            {
                count = total;
            }

            if (!TryGetViewportInfo(renderModel, out var viewport) || !viewport.HasCartesianSeries)
            {
                return false;
            }

            var canPanWindow = count < total;
            var canPanValue =
                !viewport.BarOnly &&
                renderModel.ValueAxis.IsVisible &&
                viewport.MaxValue > viewport.MinValue;
            if (!canPanWindow && !canPanValue)
            {
                return false;
            }

            _isPanning = true;
            _panStartPoint = point.Position;
            _panStartWindowStart = start;
            _panStartWindowCount = count;
            _panTargetModel = viewportModel;
            _panRenderModel = renderModel;
            _panViewport = viewport;
            _hasPanViewport = true;
            _panCanPanWindow = canPanWindow;
            _panCanPanValue = canPanValue;
            _panStartValueMinimum = viewport.MinValue;
            _panStartValueMaximum = viewport.MaxValue;
            ClearToolTip();
            ClearCrosshair();
            return true;
        }

        private void UpdatePan(Point position)
        {
            if (!_isPanning || !_hasPanViewport)
            {
                return;
            }

            var model = _panTargetModel ?? ResolveViewportChartModel();
            if (model == null)
            {
                return;
            }

            if (_panCanPanWindow)
            {
                var axisLength = _panViewport.BarOnly ? _panViewport.Plot.Height : _panViewport.Plot.Width;
                if (axisLength > 0)
                {
                    var deltaPixels = _panViewport.BarOnly
                        ? position.Y - _panStartPoint.Y
                        : position.X - _panStartPoint.X;
                    var deltaRatio = deltaPixels / axisLength;
                    var deltaIndex = (int)Math.Round(-deltaRatio * _panStartWindowCount);
                    model.SetVisibleWindow(_panStartWindowStart + deltaIndex, _panStartWindowCount, false);
                }
            }

            if (_panCanPanValue && _panRenderModel != null)
            {
                var valueAxisLength = _panViewport.Plot.Height;
                if (valueAxisLength > 0)
                {
                    var valueDeltaPixels = position.Y - _panStartPoint.Y;
                    var valueRange = _panStartValueMaximum - _panStartValueMinimum;
                    if (valueRange > 0d)
                    {
                        var valueDelta = (valueDeltaPixels / valueAxisLength) * valueRange;
                        _panRenderModel.SetValueRange(_panStartValueMinimum + valueDelta, _panStartValueMaximum + valueDelta);
                    }
                }
            }
        }

        private void EndPan()
        {
            if (!_isPanning)
            {
                return;
            }

            _isPanning = false;
            _hasPanViewport = false;
            _panStartWindowStart = 0;
            _panStartWindowCount = 0;
            _panTargetModel = null;
            _panRenderModel = null;
            _panCanPanWindow = false;
            _panCanPanValue = false;
            _panStartValueMinimum = 0d;
            _panStartValueMaximum = 0d;
            if (_panPointer != null && ReferenceEquals(_panPointer.Captured, this))
            {
                _panPointer.Capture(null);
            }

            _panPointer = null;
        }

        private bool TryBeginSelectionDrag(ChartPointerTool tool, PointerPressedEventArgs e)
        {
            var renderModel = ChartModel;
            if (renderModel == null)
            {
                return false;
            }

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return false;
            }

            if (!TryResolveSelectionPoint(renderModel, point.Position, out var categoryIndex, out var categoryLabel, out var value))
            {
                return false;
            }

            _selectionTool = tool;
            _isSelectionDragActive = true;
            _selectionStartPoint = point.Position;
            _selectionEndPoint = point.Position;
            _selectionStartCategoryIndex = categoryIndex;
            _selectionEndCategoryIndex = categoryIndex;
            _selectionStartCategoryLabel = categoryLabel;
            _selectionEndCategoryLabel = categoryLabel;
            _selectionStartValue = value;
            _selectionEndValue = value;
            _hasMeasurementOverlay = tool == ChartPointerTool.Measure;
            _selectionTargetModel = ResolveViewportChartModel() ?? renderModel;
            _panPointer = e.Pointer;
            e.Pointer.Capture(this);
            ClearToolTip();
            ClearCrosshair();
            InvalidateVisual();
            return true;
        }

        private void UpdateSelectionDrag(Point position)
        {
            var model = ChartModel;
            if (!_isSelectionDragActive || model == null)
            {
                return;
            }

            if (!TryResolveSelectionPoint(model, position, out var categoryIndex, out var categoryLabel, out var value))
            {
                return;
            }

            _selectionEndPoint = position;
            _selectionEndCategoryIndex = categoryIndex;
            _selectionEndCategoryLabel = categoryLabel;
            _selectionEndValue = value;
            InvalidateVisual();
        }

        private void EndSelectionDrag(Point position)
        {
            if (!_isSelectionDragActive)
            {
                return;
            }

            UpdateSelectionDrag(position);

            var model = _selectionTargetModel ?? ResolveViewportChartModel() ?? ChartModel;
            var selectionTool = _selectionTool;
            _isSelectionDragActive = false;
            ReleasePointerCapture();

            if (model == null)
            {
                return;
            }

            if (selectionTool == ChartPointerTool.Zoom)
            {
                ApplyZoomSelection(model);
                ClearSelectionOverlay(clearMeasurement: true);
            }
            else if (selectionTool == ChartPointerTool.Measure)
            {
                _hasMeasurementOverlay = true;
                InvalidateVisual();
            }

            _selectionTargetModel = null;
        }

        private void CancelSelectionDrag()
        {
            if (!_isSelectionDragActive)
            {
                return;
            }

            _isSelectionDragActive = false;
            ReleasePointerCapture();
            _selectionTargetModel = null;
            if (_selectionTool == ChartPointerTool.Zoom)
            {
                ClearSelectionOverlay(clearMeasurement: false);
            }
            else
            {
                InvalidateVisual();
            }
        }

        private void ApplyZoomSelection(ChartModel model)
        {
            if (!TryGetVisibleWindowState(model, out var total, out var start, out var count))
            {
                return;
            }

            if (count <= 0)
            {
                return;
            }

            var minIndex = Math.Min(_selectionStartCategoryIndex, _selectionEndCategoryIndex);
            var maxIndex = Math.Max(_selectionStartCategoryIndex, _selectionEndCategoryIndex);
            var selectedCount = Math.Max(MinWindowCount, (maxIndex - minIndex) + 1);
            selectedCount = Math.Min(selectedCount, count);
            var absoluteStart = start + minIndex;
            model.SetVisibleWindow(absoluteStart, selectedCount, false);
        }

        private bool TryResolveSelectionPoint(
            ChartModel model,
            Point position,
            out int categoryIndex,
            out string? categoryLabel,
            out double value)
        {
            categoryIndex = 0;
            categoryLabel = null;
            value = 0d;

            if (!TryGetViewportInfo(model, out var viewport) || !viewport.HasCartesianSeries || viewport.BarOnly)
            {
                return false;
            }

            var snapshot = model.Snapshot;
            if (snapshot.Categories.Count == 0)
            {
                return false;
            }

            var horizontalRatio = GetAxisRatio(viewport, position);
            categoryIndex = (int)Math.Round(horizontalRatio * Math.Max(0, snapshot.Categories.Count - 1));
            if (categoryIndex < 0)
            {
                categoryIndex = 0;
            }
            else if (categoryIndex >= snapshot.Categories.Count)
            {
                categoryIndex = snapshot.Categories.Count - 1;
            }

            categoryLabel = snapshot.Categories[categoryIndex];
            value = InterpolateValue(viewport, GetValueRatio(viewport, position));
            return true;
        }

        private bool TryGetVisibleWindowState(ChartModel model, out int total, out int start, out int count)
        {
            total = GetTotalCategoryCount(model);
            start = 0;
            count = 0;

            if (total <= 0)
            {
                return false;
            }

            count = model.Request.WindowCount ?? total;
            if (count <= 0 || count > total)
            {
                count = total;
            }

            start = model.Request.WindowStart ?? 0;
            if (start < 0)
            {
                start = 0;
            }

            if (start + count > total)
            {
                start = Math.Max(0, total - count);
            }

            return true;
        }

        private void ClearSelectionOverlay(bool clearMeasurement)
        {
            _isSelectionDragActive = false;
            if (clearMeasurement)
            {
                _hasMeasurementOverlay = false;
            }

            _selectionStartPoint = default;
            _selectionEndPoint = default;
            _selectionStartCategoryIndex = 0;
            _selectionEndCategoryIndex = 0;
            _selectionStartCategoryLabel = null;
            _selectionEndCategoryLabel = null;
            _selectionStartValue = 0d;
            _selectionEndValue = 0d;
            _selectionTool = ChartPointerTool.Crosshair;
            _selectionTargetModel = null;
            ReleasePointerCapture();
            InvalidateVisual();
        }

        private void ReleasePointerCapture()
        {
            if (_panPointer != null && ReferenceEquals(_panPointer.Captured, this))
            {
                _panPointer.Capture(null);
            }

            _panPointer = null;
        }

        private int GetTotalCategoryCount(ChartModel model)
        {
            if (model.DataSource is IChartWindowInfoProvider provider)
            {
                var count = provider.GetTotalCategoryCount();
                if (count.HasValue)
                {
                    return Math.Max(0, count.Value);
                }
            }

            return model.Snapshot.Categories.Count;
        }

        private void EnsureWindowBounds()
        {
            var model = ChartModel;
            if (model == null)
            {
                return;
            }

            var request = model.Request;
            if (!request.WindowStart.HasValue && !request.WindowCount.HasValue)
            {
                return;
            }

            var total = GetTotalCategoryCount(model);
            if (total > 0 && request.WindowCount.HasValue)
            {
                var count = Math.Max(1, Math.Min(request.WindowCount.Value, total));
                var start = request.WindowStart ?? 0;
                if (start < 0)
                {
                    start = 0;
                }

                if (start + count > total)
                {
                    start = Math.Max(0, total - count);
                }

                model.SetVisibleWindow(start, count, model.Interaction.FollowLatest && start + count >= total);
            }
        }

        private bool TryGetViewportInfo(ChartModel model, out SkiaChartViewportInfo viewport)
        {
            var snapshot = model.Snapshot;
            if (snapshot.Series.Count == 0)
            {
                viewport = default;
                return false;
            }

            var bounds = new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height);
            var style = BuildEffectiveStyle();
            return _renderer.TryGetViewportInfo(bounds, snapshot, style, out viewport);
        }

        private static double GetAxisRatio(SkiaChartViewportInfo viewport, Point position)
        {
            var axisStart = viewport.BarOnly ? viewport.Plot.Top : viewport.Plot.Left;
            var axisLength = viewport.BarOnly ? viewport.Plot.Height : viewport.Plot.Width;
            if (axisLength <= 0)
            {
                return 0.5d;
            }

            var axisPosition = viewport.BarOnly ? position.Y : position.X;
            var ratio = (axisPosition - axisStart) / axisLength;
            if (ratio < 0d)
            {
                return 0d;
            }

            if (ratio > 1d)
            {
                return 1d;
            }

            return ratio;
        }

        private bool ShowLatestWindow()
        {
            var model = ResolveViewportChartModel();
            if (model == null)
            {
                return false;
            }

            var total = GetTotalCategoryCount(model);
            if (total <= 0)
            {
                return false;
            }

            var preferredCount = model.Request.WindowCount ?? total;
            preferredCount = Math.Max(1, Math.Min(preferredCount, total));
            return model.ShowLatest(preferredCount, true);
        }

        private void ResetToLatestWindow()
        {
            ShowLatestWindow();
        }

        private ChartModel? ResolveViewportChartModel()
        {
            return ViewportChartModel ?? ChartModel;
        }

        private int GetKeyboardPanStep(ChartModel model)
        {
            var total = GetTotalCategoryCount(model);
            if (total <= 0)
            {
                return 1;
            }

            var count = model.Request.WindowCount ?? total;
            return Math.Max(1, count / 12);
        }

        private bool ClearCrosshairWithResult()
        {
            var model = ChartModel;
            if (model == null || !model.Interaction.IsCrosshairVisible)
            {
                return false;
            }

            ClearCrosshair();
            return true;
        }

        private bool ClearInteractionOverlaysWithResult()
        {
            if (_isSelectionDragActive || _hasMeasurementOverlay)
            {
                ClearSelectionOverlay(clearMeasurement: true);
                ClearCrosshair();
                return true;
            }

            return ClearCrosshairWithResult();
        }

        private void UpdateHoverState(Point position)
        {
            if (!EnableHoverTracking)
            {
                return;
            }

            UpdateToolTip(position);
            UpdateCrosshair(position);
        }

        private void UpdateCrosshair(Point point)
        {
            var model = ChartModel;
            if (model == null || !ShowCrosshair)
            {
                ClearCrosshair();
                return;
            }

            if (model.Interaction.PointerTool != ChartPointerTool.Crosshair)
            {
                ClearCrosshair();
                return;
            }

            if (!TryGetViewportInfo(model, out var viewport) || !viewport.HasCartesianSeries || viewport.BarOnly)
            {
                ClearCrosshair();
                return;
            }

            var snapshot = model.Snapshot;
            if (snapshot.Categories.Count == 0)
            {
                ClearCrosshair();
                return;
            }

            var horizontalRatio = GetAxisRatio(viewport, point);
            var verticalRatio = GetValueRatio(viewport, point);
            var categoryIndex = (int)Math.Round(horizontalRatio * Math.Max(0, snapshot.Categories.Count - 1));
            if (categoryIndex < 0)
            {
                categoryIndex = 0;
            }
            else if (categoryIndex >= snapshot.Categories.Count)
            {
                categoryIndex = snapshot.Categories.Count - 1;
            }

            var value = InterpolateValue(viewport, verticalRatio);
            model.TrackCrosshair(
                categoryIndex,
                snapshot.Categories[categoryIndex],
                value,
                horizontalRatio,
                verticalRatio);
        }

        private void ClearCrosshair()
        {
            var model = ChartModel;
            if (model == null)
            {
                return;
            }

            model.ClearCrosshair();
        }

        private void DrawInteractionOverlay(DrawingContext context)
        {
            var model = ChartModel;
            if (model == null)
            {
                return;
            }

            if (!TryGetViewportInfo(model, out var viewport) || !viewport.HasCartesianSeries || viewport.BarOnly)
            {
                return;
            }

            var plot = new Rect(viewport.Plot.Left, viewport.Plot.Top, viewport.Plot.Width, viewport.Plot.Height);
            if (plot.Width <= 0 || plot.Height <= 0)
            {
                return;
            }

            var style = BuildEffectiveStyle();
            var lineBrush = CreateBrush(style.Axis.WithAlpha(172));
            var labelBackgroundBrush = CreateBrush(style.DataLabelBackground);
            var labelForegroundBrush = CreateBrush(style.DataLabelText);
            var overlayBounds = new Rect(Bounds.Size);
            if (_isSelectionDragActive && _selectionTool == ChartPointerTool.Zoom)
            {
                DrawZoomSelectionOverlay(context, plot, lineBrush, overlayBounds);
            }

            if (_isSelectionDragActive && _selectionTool == ChartPointerTool.Measure || _hasMeasurementOverlay)
            {
                DrawMeasurementOverlay(context, model, plot, labelBackgroundBrush, labelForegroundBrush, overlayBounds);
            }

            if (!ShowCrosshair || !model.Interaction.IsCrosshairVisible)
            {
                return;
            }

            var x = plot.X + (plot.Width * model.Interaction.CrosshairHorizontalRatio);
            var y = plot.Y + (plot.Height * model.Interaction.CrosshairVerticalRatio);
            var crosshairMode = model.Interaction.CrosshairMode;
            var showVerticalGuide = crosshairMode != ChartCrosshairMode.HorizontalOnly;
            var showHorizontalGuide = crosshairMode != ChartCrosshairMode.VerticalOnly;

            if (showVerticalGuide)
            {
                context.DrawLine(new Pen(lineBrush, 1d), new Point(x, plot.Top), new Point(x, plot.Bottom));
            }

            if (showHorizontalGuide)
            {
                context.DrawLine(new Pen(lineBrush, 1d), new Point(plot.Left, y), new Point(plot.Right, y));
            }

            if (showVerticalGuide && !string.IsNullOrWhiteSpace(model.Interaction.CrosshairCategoryLabel))
            {
                DrawOverlayLabel(
                    context,
                    model.Interaction.CrosshairCategoryLabel!,
                    new Point(x, plot.Bottom + 4),
                    HorizontalAlignment.Center,
                    labelBackgroundBrush,
                    labelForegroundBrush,
                    overlayBounds);
            }

            if (showHorizontalGuide && model.Interaction.CrosshairValue.HasValue)
            {
                DrawOverlayLabel(
                    context,
                    FormatCrosshairValue(model, model.Interaction.CrosshairValue.Value),
                    new Point(plot.Right + 4, y),
                    HorizontalAlignment.Left,
                    labelBackgroundBrush,
                    labelForegroundBrush,
                    overlayBounds);
            }
        }

        private static void DrawOverlayLabel(
            DrawingContext context,
            string text,
            Point anchor,
            HorizontalAlignment alignment,
            IBrush background,
            IBrush foreground,
            Rect availableBounds)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                foreground);
            var paddingX = 6d;
            var paddingY = 3d;
            var width = formattedText.Width + (paddingX * 2d);
            var height = formattedText.Height + (paddingY * 2d);
            var originX = alignment == HorizontalAlignment.Center
                ? anchor.X - (width / 2d)
                : anchor.X;
            var originY = anchor.Y - (height / 2d);
            if (availableBounds.Width > 0d)
            {
                originX = Math.Clamp(originX, availableBounds.Left, Math.Max(availableBounds.Left, availableBounds.Right - width));
            }

            if (availableBounds.Height > 0d)
            {
                originY = Math.Clamp(originY, availableBounds.Top, Math.Max(availableBounds.Top, availableBounds.Bottom - height));
            }

            var rect = new Rect(originX, originY, width, height);

            context.DrawRectangle(background, null, rect, 4d);
            context.DrawText(formattedText, new Point(rect.X + paddingX, rect.Y + paddingY));
        }

        private void DrawZoomSelectionOverlay(
            DrawingContext context,
            Rect plot,
            IBrush strokeBrush,
            Rect availableBounds)
        {
            var start = ClampToPlot(plot, _selectionStartPoint);
            var end = ClampToPlot(plot, _selectionEndPoint);
            var rect = new Rect(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y));
            if (rect.Width <= 0d || rect.Height <= 0d)
            {
                return;
            }

            var fillBrush = new SolidColorBrush(Color.FromArgb(42, 74, 104, 255));
            var labelBackgroundBrush = new SolidColorBrush(Color.FromArgb(216, 27, 36, 52));
            var labelForegroundBrush = new SolidColorBrush(Color.FromArgb(255, 242, 245, 250));
            context.DrawRectangle(fillBrush, new Pen(strokeBrush, 1d), rect, 4d);

            var selectedBars = Math.Abs(_selectionEndCategoryIndex - _selectionStartCategoryIndex) + 1;
            DrawOverlayLabel(
                context,
                $"Zoom {selectedBars} {(selectedBars == 1 ? "bar" : "bars")}",
                new Point(rect.X + (rect.Width * 0.5d), rect.Top - 12d),
                HorizontalAlignment.Center,
                labelBackgroundBrush,
                labelForegroundBrush,
                availableBounds);
        }

        private void DrawMeasurementOverlay(
            DrawingContext context,
            ChartModel model,
            Rect plot,
            IBrush labelBackgroundBrush,
            IBrush labelForegroundBrush,
            Rect availableBounds)
        {
            var start = ClampToPlot(plot, _selectionStartPoint);
            var end = ClampToPlot(plot, _selectionEndPoint);
            if (start == default && end == default)
            {
                return;
            }

            var measureBrush = new SolidColorBrush(Color.FromArgb(255, 94, 196, 255));
            var pen = new Pen(measureBrush, 1.35d);
            context.DrawLine(pen, start, end);
            context.DrawEllipse(measureBrush, null, start, 3.5d, 3.5d);
            context.DrawEllipse(measureBrush, null, end, 3.5d, 3.5d);

            var label = FormatMeasurementLabel(model);
            var anchor = new Point((start.X + end.X) * 0.5d, Math.Min(start.Y, end.Y) - 12d);
            DrawOverlayLabel(context, label, anchor, HorizontalAlignment.Center, labelBackgroundBrush, labelForegroundBrush, availableBounds);
        }

        private static IBrush CreateBrush(SKColor color)
        {
            return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        }

        private static double GetValueRatio(SkiaChartViewportInfo viewport, Point position)
        {
            var axisLength = viewport.Plot.Height;
            if (axisLength <= 0)
            {
                return 0.5d;
            }

            var ratio = (position.Y - viewport.Plot.Top) / axisLength;
            if (ratio < 0d)
            {
                return 0d;
            }

            if (ratio > 1d)
            {
                return 1d;
            }

            return ratio;
        }

        private static double InterpolateValue(SkiaChartViewportInfo viewport, double verticalRatio)
        {
            var clampedRatio = verticalRatio < 0d
                ? 0d
                : verticalRatio > 1d
                    ? 1d
                    : verticalRatio;
            var inverted = 1d - clampedRatio;
            return viewport.MinValue + ((viewport.MaxValue - viewport.MinValue) * inverted);
        }

        private static string FormatCrosshairValue(ChartModel model, double value)
        {
            return FormatAxisValue(model.ValueAxis, value);
        }

        private static bool IsDefaultFinancialTooltip(SkiaChartHitTestResult hit)
        {
            return hit.SeriesKind == ChartSeriesKind.Candlestick ||
                   hit.SeriesKind == ChartSeriesKind.HollowCandlestick ||
                   hit.SeriesKind == ChartSeriesKind.Ohlc ||
                   hit.SeriesKind == ChartSeriesKind.HeikinAshi ||
                   hit.SeriesKind == ChartSeriesKind.Range ||
                   hit.SeriesKind == ChartSeriesKind.Renko ||
                   hit.SeriesKind == ChartSeriesKind.LineBreak ||
                   hit.SeriesKind == ChartSeriesKind.Kagi ||
                   hit.SeriesKind == ChartSeriesKind.PointFigure;
        }

        private static ChartSeriesSnapshot? TryGetHitSeriesSnapshot(ChartModel? model, int seriesIndex)
        {
            if (model == null || seriesIndex < 0 || seriesIndex >= model.Snapshot.Series.Count)
            {
                return null;
            }

            return model.Snapshot.Series[seriesIndex];
        }

        private static ChartAxisDefinition? ResolveValueAxis(ChartModel? model, ChartSeriesSnapshot? series)
        {
            if (model == null)
            {
                return null;
            }

            return series?.ValueAxisAssignment == ChartValueAxisAssignment.Secondary
                ? model.SecondaryValueAxis
                : model.ValueAxis;
        }

        private static ChartAxisDefinition? ResolveCategoryAxis(ChartModel? model, ChartSeriesSnapshot? series)
        {
            if (model == null)
            {
                return null;
            }

            return series?.ValueAxisAssignment == ChartValueAxisAssignment.Secondary
                ? model.SecondaryCategoryAxis
                : model.CategoryAxis;
        }

        private static string FormatAxisValue(ChartAxisDefinition? axis, double value)
        {
            if (axis == null)
            {
                return ChartValueFormatter.Format(value, null);
            }

            if (axis.LabelFormatter != null)
            {
                return axis.LabelFormatter(value);
            }

            if (axis.Kind == ChartAxisKind.DateTime)
            {
                try
                {
                    return DateTime.FromOADate(value).ToString("d", CultureInfo.CurrentCulture);
                }
                catch (ArgumentException)
                {
                    return ChartValueFormatter.Format(value, axis.ValueFormat);
                }
            }

            return ChartValueFormatter.Format(value, axis.ValueFormat);
        }

        private static Point ClampToPlot(Rect plot, Point point)
        {
            var x = Math.Clamp(point.X, plot.Left, plot.Right);
            var y = Math.Clamp(point.Y, plot.Top, plot.Bottom);
            return new Point(x, y);
        }

        private string FormatMeasurementLabel(ChartModel model)
        {
            var deltaValue = _selectionEndValue - _selectionStartValue;
            var deltaPercent = Math.Abs(_selectionStartValue) > double.Epsilon
                ? (deltaValue / _selectionStartValue) * 100d
                : 0d;
            var signedValue = deltaValue >= 0d
                ? $"+{FormatCrosshairValue(model, Math.Abs(deltaValue))}"
                : $"-{FormatCrosshairValue(model, Math.Abs(deltaValue))}";
            var bars = Math.Abs(_selectionEndCategoryIndex - _selectionStartCategoryIndex) + 1;
            return $"{signedValue} • {deltaPercent:+0.##;-0.##;0}% • {bars} {(bars == 1 ? "bar" : "bars")}";
        }

        private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        {
            _isDirty = true;
            InvalidateVisual();
        }

        private void ApplyThemeVariantDefaults(SkiaChartStyle style, ChartModel? model)
        {
            if (ActualThemeVariant != ThemeVariant.Dark)
            {
                return;
            }

            var explicitStyle = ChartStyle;
            var setBackground = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.Background.HasValue,
                theme => theme.Background.HasValue,
                chartStyle => chartStyle.Background,
                DefaultStyle.Background);
            var setAxis = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.Axis.HasValue,
                theme => theme.Axis.HasValue,
                chartStyle => chartStyle.Axis,
                DefaultStyle.Axis);
            var setText = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.Text.HasValue,
                theme => theme.Text.HasValue,
                chartStyle => chartStyle.Text,
                DefaultStyle.Text);
            var setGridline = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.Gridline.HasValue,
                theme => theme.Gridline.HasValue,
                chartStyle => chartStyle.Gridline,
                DefaultStyle.Gridline);
            var setDataLabelBackground = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.DataLabelBackground.HasValue,
                theme => theme.DataLabelBackground.HasValue,
                chartStyle => chartStyle.DataLabelBackground,
                DefaultStyle.DataLabelBackground);
            var setDataLabelText = ShouldApplyDarkSurfaceOverride(
                explicitStyle,
                model,
                theme => theme.DataLabelText.HasValue,
                theme => theme.DataLabelText.HasValue,
                chartStyle => chartStyle.DataLabelText,
                DefaultStyle.DataLabelText);

            if (!setBackground &&
                !setAxis &&
                !setText &&
                !setGridline &&
                !setDataLabelBackground &&
                !setDataLabelText)
            {
                return;
            }

            if (style.Theme == null &&
                setBackground &&
                setAxis &&
                setText &&
                setGridline &&
                setDataLabelBackground &&
                setDataLabelText)
            {
                style.Theme = DarkThemeDefaults;
                return;
            }

            var theme = CloneTheme(style.Theme);
            if (setBackground)
            {
                theme.Background = DarkThemeDefaults.Background;
            }

            if (setAxis)
            {
                theme.Axis = DarkThemeDefaults.Axis;
            }

            if (setText)
            {
                theme.Text = DarkThemeDefaults.Text;
            }

            if (setGridline)
            {
                theme.Gridline = DarkThemeDefaults.Gridline;
            }

            if (setDataLabelBackground)
            {
                theme.DataLabelBackground = DarkThemeDefaults.DataLabelBackground;
            }

            if (setDataLabelText)
            {
                theme.DataLabelText = DarkThemeDefaults.DataLabelText;
            }

            style.Theme = theme;
        }

        private static bool ShouldApplyDarkSurfaceOverride(
            SkiaChartStyle? chartStyle,
            ChartModel? model,
            Func<SkiaChartTheme, bool> hasExplicitSkiaThemeValue,
            Func<ChartTheme, bool> hasExplicitCoreThemeValue,
            Func<SkiaChartStyle, SKColor> getSkiaStyleColor,
            SKColor defaultColor)
        {
            if (chartStyle?.Theme is { } theme && hasExplicitSkiaThemeValue(theme))
            {
                return false;
            }

            if (model?.Theme is { } coreTheme && hasExplicitCoreThemeValue(coreTheme))
            {
                return false;
            }

            return chartStyle == null || getSkiaStyleColor(chartStyle) == defaultColor;
        }

        private static SkiaChartTheme CloneTheme(SkiaChartTheme? source)
        {
            return source == null
                ? new SkiaChartTheme()
                : new SkiaChartTheme
                {
                    Background = source.Background,
                    Axis = source.Axis,
                    Text = source.Text,
                    Gridline = source.Gridline,
                    DataLabelBackground = source.DataLabelBackground,
                    DataLabelText = source.DataLabelText,
                    SeriesColors = source.SeriesColors,
                    SeriesStyles = source.SeriesStyles
                };
        }
    }
}
