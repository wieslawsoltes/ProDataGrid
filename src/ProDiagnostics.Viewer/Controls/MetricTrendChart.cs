using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.Controls;

public sealed class MetricTrendChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<MetricSample>?> SamplesProperty =
        AvaloniaProperty.Register<MetricTrendChart, IReadOnlyList<MetricSample>?>(nameof(Samples));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<MetricTrendChart, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<MetricTrendChart, IBrush?>(nameof(Background), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> MinLineBrushProperty =
        AvaloniaProperty.Register<MetricTrendChart, IBrush?>(nameof(MinLineBrush));

    public static readonly StyledProperty<IBrush?> MaxLineBrushProperty =
        AvaloniaProperty.Register<MetricTrendChart, IBrush?>(nameof(MaxLineBrush));

    public static readonly StyledProperty<IBrush?> AverageLineBrushProperty =
        AvaloniaProperty.Register<MetricTrendChart, IBrush?>(nameof(AverageLineBrush));

    public static readonly StyledProperty<double> MinValueProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(MinValue));

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(MaxValue));

    public static readonly StyledProperty<double> AverageValueProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(AverageValue));

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(LineThickness), 1.8);

    public static readonly StyledProperty<double> ThresholdThicknessProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(ThresholdThickness), 1.0);

    public static readonly StyledProperty<TimeSpan> TimeRangeProperty =
        AvaloniaProperty.Register<MetricTrendChart, TimeSpan>(nameof(TimeRange));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(MinZoom), 1.0);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(MaxZoom), 12.0);

    public static readonly StyledProperty<double> ViewportStartProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(ViewportStart), 0.0);

    public static readonly StyledProperty<double> WheelZoomStepProperty =
        AvaloniaProperty.Register<MetricTrendChart, double>(nameof(WheelZoomStep), 0.2);

    public static readonly StyledProperty<bool> EnableWheelZoomProperty =
        AvaloniaProperty.Register<MetricTrendChart, bool>(nameof(EnableWheelZoom), true);

    public static readonly StyledProperty<bool> EnablePanProperty =
        AvaloniaProperty.Register<MetricTrendChart, bool>(nameof(EnablePan), true);

    private INotifyCollectionChanged? _collectionSubscription;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartIndex;

    public MetricTrendChart()
    {
        Focusable = true;
        IsHitTestVisible = true;
        AddHandler(PointerPressedEvent, OnPointerPressedCore, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMovedCore, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedCore, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChangedCore, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    static MetricTrendChart()
    {
        AffectsRender<MetricTrendChart>(
            SamplesProperty,
            StrokeProperty,
            BackgroundProperty,
            MinLineBrushProperty,
            MaxLineBrushProperty,
            AverageLineBrushProperty,
            MinValueProperty,
            MaxValueProperty,
            AverageValueProperty,
            LineThicknessProperty,
            ThresholdThicknessProperty,
            TimeRangeProperty,
            ZoomProperty,
            MinZoomProperty,
            MaxZoomProperty,
            ViewportStartProperty,
            WheelZoomStepProperty,
            EnableWheelZoomProperty,
            EnablePanProperty);
    }

    public IReadOnlyList<MetricSample>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? MinLineBrush
    {
        get => GetValue(MinLineBrushProperty);
        set => SetValue(MinLineBrushProperty, value);
    }

    public IBrush? MaxLineBrush
    {
        get => GetValue(MaxLineBrushProperty);
        set => SetValue(MaxLineBrushProperty, value);
    }

    public IBrush? AverageLineBrush
    {
        get => GetValue(AverageLineBrushProperty);
        set => SetValue(AverageLineBrushProperty, value);
    }

    public double MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public double AverageValue
    {
        get => GetValue(AverageValueProperty);
        set => SetValue(AverageValueProperty, value);
    }

    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    public double ThresholdThickness
    {
        get => GetValue(ThresholdThicknessProperty);
        set => SetValue(ThresholdThicknessProperty, value);
    }

    public TimeSpan TimeRange
    {
        get => GetValue(TimeRangeProperty);
        set => SetValue(TimeRangeProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public double ViewportStart
    {
        get => GetValue(ViewportStartProperty);
        set => SetValue(ViewportStartProperty, value);
    }

    public double WheelZoomStep
    {
        get => GetValue(WheelZoomStepProperty);
        set => SetValue(WheelZoomStepProperty, value);
    }

    public bool EnableWheelZoom
    {
        get => GetValue(EnableWheelZoomProperty);
        set => SetValue(EnableWheelZoomProperty, value);
    }

    public bool EnablePan
    {
        get => GetValue(EnablePanProperty);
        set => SetValue(EnablePanProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SamplesProperty)
        {
            if (_collectionSubscription != null)
            {
                _collectionSubscription.CollectionChanged -= OnSamplesChanged;
                _collectionSubscription = null;
            }

            _collectionSubscription = change.NewValue as INotifyCollectionChanged;
            if (_collectionSubscription != null)
            {
                _collectionSubscription.CollectionChanged += OnSamplesChanged;
            }
        }
        else if (change.Property == ZoomProperty || change.Property == MinZoomProperty || change.Property == MaxZoomProperty)
        {
            var clamped = ClampZoom(Zoom);
            if (Math.Abs(clamped - Zoom) > 0.0001)
            {
                SetCurrentValue(ZoomProperty, clamped);
            }
        }
        else if (change.Property == ViewportStartProperty)
        {
            var clamped = Math.Clamp(ViewportStart, 0, 1);
            if (Math.Abs(clamped - ViewportStart) > 0.0001)
            {
                SetCurrentValue(ViewportStartProperty, clamped);
            }
        }
    }


    private void OnPointerWheelChangedCore(object? sender, PointerWheelEventArgs e)
    {
        if (!EnableWheelZoom)
        {
            return;
        }

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        var direction = Math.Sign(e.Delta.Y);
        if (direction == 0)
        {
            return;
        }

        if (!TryGetVisibleRange(samples, out _, out _, out var visibleCount, out var maxStartIndex, out var zoom, out var exactStartIndex))
        {
            return;
        }

        var step = Math.Clamp(Math.Abs(WheelZoomStep), 0.05, 0.9);
        var zoomFactor = direction > 0 ? 1 + step : 1 - step;
        var newZoom = ClampZoom(zoom * zoomFactor);

        if (Math.Abs(newZoom - zoom) < 0.0001)
        {
            return;
        }

        var width = Bounds.Width;
        var position = e.GetPosition(this);
        var anchorRatio = width <= 0 ? 0 : Math.Clamp(position.X / width, 0, 1);
        var anchorIndex = exactStartIndex + anchorRatio * Math.Max(1, visibleCount - 1);

        var rangeCount = maxStartIndex + visibleCount;
        var newVisibleCount = Math.Max(2, (int)Math.Ceiling(rangeCount / newZoom));
        newVisibleCount = Math.Min(newVisibleCount, rangeCount);
        var newMaxStartIndex = Math.Max(0, rangeCount - newVisibleCount);
        var newStartIndex = anchorIndex - anchorRatio * Math.Max(1, newVisibleCount - 1);
        newStartIndex = Math.Clamp(newStartIndex, 0, newMaxStartIndex);

        SetCurrentValue(ZoomProperty, newZoom);
        SetCurrentValue(ViewportStartProperty, newMaxStartIndex == 0 ? 0 : newStartIndex / newMaxStartIndex);
        e.Handled = true;
    }

    private void OnPointerPressedCore(object? sender, PointerPressedEventArgs e)
    {
        if (!EnablePan || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        if (!TryGetVisibleRange(samples, out _, out _, out _, out var maxStartIndex, out _, out var exactStartIndex))
        {
            return;
        }

        if (maxStartIndex == 0)
        {
            return;
        }

        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panStartIndex = exactStartIndex;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMovedCore(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        if (!TryGetVisibleRange(samples, out _, out _, out var visibleCount, out var maxStartIndex, out _, out _))
        {
            return;
        }

        if (maxStartIndex == 0 || Bounds.Width <= 1)
        {
            return;
        }

        var position = e.GetPosition(this);
        var deltaX = position.X - _panStart.X;
        var deltaSamples = deltaX / Bounds.Width * Math.Max(1, visibleCount - 1);
        var newStartIndex = _panStartIndex - deltaSamples;
        newStartIndex = Math.Clamp(newStartIndex, 0, maxStartIndex);

        SetCurrentValue(ViewportStartProperty, newStartIndex / maxStartIndex);
        e.Handled = true;
    }

    private void OnPointerReleasedCore(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        EndPan(e.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPan(e.Pointer);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        if (!TryGetVisibleRange(samples, out var startIndex, out var endIndex, out var visibleCount, out _, out _, out _))
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2 || height <= 2)
        {
            return;
        }

        var background = Background;
        if (background != null)
        {
            context.FillRectangle(background, new Rect(0, 0, width, height));
        }

        if (!TryGetWindowStats(samples, startIndex, endIndex, out var min, out var max, out var average))
        {
            return;
        }

        var rangeMin = min;
        var rangeMax = max;
        if (Math.Abs(rangeMax - rangeMin) < 0.0001)
        {
            rangeMax = rangeMin + 1;
        }

        DrawThreshold(context, min, rangeMin, rangeMax, MinLineBrush);
        DrawThreshold(context, average, rangeMin, rangeMax, AverageLineBrush);
        DrawThreshold(context, max, rangeMin, rangeMax, MaxLineBrush);

        var pen = new Pen(Stroke ?? Brushes.Black, LineThickness);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var visibleIndex = 0;
            for (var i = startIndex; i <= endIndex; i++)
            {
                var x = visibleIndex / (double)(visibleCount - 1) * width;
                var normalized = (samples[i].Value - rangeMin) / (rangeMax - rangeMin);
                var y = height - (normalized * height);
                var point = new Point(x, y);

                if (visibleIndex == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }

                visibleIndex++;
            }
        }

        context.DrawGeometry(null, pen, geometry);

        DrawThresholdLabel(context, "Min", min, rangeMin, rangeMax, MinLineBrush, labelAbove: false);
        DrawThresholdLabel(context, "Avg", average, rangeMin, rangeMax, AverageLineBrush, labelAbove: true);
        DrawThresholdLabel(context, "Max", max, rangeMin, rangeMax, MaxLineBrush, labelAbove: false);
    }

    private void DrawThreshold(DrawingContext context, double value, double min, double max, IBrush? brush)
    {
        if (brush == null)
        {
            return;
        }

        var height = Bounds.Height;
        var width = Bounds.Width;
        var normalized = (value - min) / (max - min);
        var y = height - (normalized * height);
        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            return;
        }

        y = Math.Max(0, Math.Min(height, y));

        var pen = new Pen(brush, ThresholdThickness, dashStyle: new DashStyle(new[] { 4.0, 4.0 }, 0));
        context.DrawLine(pen, new Point(0, y), new Point(width, y));
    }

    private void DrawThresholdLabel(
        DrawingContext context,
        string label,
        double value,
        double min,
        double max,
        IBrush? brush,
        bool labelAbove)
    {
        if (brush == null || double.IsNaN(value) || double.IsInfinity(value))
        {
            return;
        }

        var height = Bounds.Height;
        var width = Bounds.Width;
        if (height <= 1 || width <= 1)
        {
            return;
        }

        var normalized = (value - min) / (max - min);
        var y = height - (normalized * height);
        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            return;
        }

        var text = string.Concat(label, " ", value.ToString("0.###", CultureInfo.CurrentCulture));
        var fontSize = 11.0;
        var typeface = Typeface.Default;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection, typeface, fontSize, brush);

        var padding = 6;
        var x = width - formatted.Width - padding;
        if (x < padding)
        {
            x = padding;
        }

        var yText = labelAbove ? y - formatted.Height - 2 : y - formatted.Height / 2;
        if (yText < 0)
        {
            yText = 0;
        }
        else if (yText + formatted.Height > height)
        {
            yText = height - formatted.Height;
        }

        context.DrawText(formatted, new Point(x, yText));
    }

    private bool TryGetWindowStats(
        IReadOnlyList<MetricSample> samples,
        int startIndex,
        int endIndex,
        out double min,
        out double max,
        out double average)
    {
        min = double.MaxValue;
        max = double.MinValue;
        average = 0;
        var sum = 0.0;
        var count = 0;

        for (var i = startIndex; i <= endIndex; i++)
        {
            var value = samples[i].Value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
            count++;
        }

        if (count == 0 || min == double.MaxValue || max == double.MinValue)
        {
            return false;
        }

        average = sum / count;

        return true;
    }

    private void OnSamplesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    private double ClampZoom(double zoom)
    {
        var min = Math.Max(1, MinZoom);
        var max = Math.Max(min, MaxZoom);
        return Math.Clamp(zoom, min, max);
    }

    private bool TryGetVisibleRange(
        IReadOnlyList<MetricSample> samples,
        out int startIndex,
        out int endIndex,
        out int visibleCount,
        out int maxStartIndex,
        out double zoom,
        out double exactStartIndex)
    {
        startIndex = 0;
        endIndex = 0;
        visibleCount = 0;
        maxStartIndex = 0;
        zoom = 1;
        exactStartIndex = 0;

        if (samples.Count < 2)
        {
            return false;
        }

        var rangeStartIndex = 0;
        var rangeEndIndex = samples.Count - 1;
        var range = TimeRange;
        if (range > TimeSpan.Zero)
        {
            var latestTimestamp = samples[^1].Timestamp;
            var minTimestamp = latestTimestamp - range;
            while (rangeStartIndex < samples.Count && samples[rangeStartIndex].Timestamp < minTimestamp)
            {
                rangeStartIndex++;
            }

            if (rangeStartIndex >= samples.Count - 1)
            {
                return false;
            }
        }

        var rangeCount = rangeEndIndex - rangeStartIndex + 1;
        if (rangeCount < 2)
        {
            return false;
        }

        zoom = ClampZoom(Zoom);
        visibleCount = Math.Max(2, (int)Math.Ceiling(rangeCount / zoom));
        visibleCount = Math.Min(visibleCount, rangeCount);
        maxStartIndex = Math.Max(0, rangeCount - visibleCount);

        exactStartIndex = maxStartIndex == 0 ? 0 : ViewportStart * maxStartIndex;
        var startOffset = (int)Math.Round(exactStartIndex);
        startOffset = Math.Clamp(startOffset, 0, maxStartIndex);
        startIndex = rangeStartIndex + startOffset;
        endIndex = startIndex + visibleCount - 1;
        if (endIndex > rangeEndIndex)
        {
            endIndex = rangeEndIndex;
            startIndex = Math.Max(rangeStartIndex, endIndex - visibleCount + 1);
        }
        return true;
    }

    private void EndPan(IPointer pointer)
    {
        if (!_isPanning)
        {
            return;
        }

        if (pointer.Captured == this)
        {
            pointer.Capture(null);
        }

        _isPanning = false;
    }
}
