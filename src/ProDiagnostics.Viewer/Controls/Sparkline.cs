using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ProDiagnostics.Viewer.Models;

namespace ProDiagnostics.Viewer.Controls;

public sealed class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<MetricSample>?> SamplesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<MetricSample>?>(nameof(Samples));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<TimeSpan> TimeRangeProperty =
        AvaloniaProperty.Register<Sparkline, TimeSpan>(nameof(TimeRange));

    private INotifyCollectionChanged? _collectionSubscription;

    static Sparkline()
    {
        AffectsRender<Sparkline>(SamplesProperty, StrokeProperty, TimeRangeProperty);
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

    public TimeSpan TimeRange
    {
        get => GetValue(TimeRangeProperty);
        set => SetValue(TimeRangeProperty, value);
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
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2 || height <= 2)
        {
            return;
        }

        if (!TryGetVisibleRange(samples, out var startIndex, out var endIndex))
        {
            return;
        }

        var visibleCount = endIndex - startIndex + 1;
        if (visibleCount < 2)
        {
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        for (var i = startIndex; i <= endIndex; i++)
        {
            var value = samples[i].Value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (Math.Abs(max - min) < 0.0001)
        {
            max = min + 1;
        }

        var pen = new Pen(Stroke ?? Brushes.Black, 1.6);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var visibleIndex = 0;
            for (var i = startIndex; i <= endIndex; i++)
            {
                var x = visibleIndex / (double)(visibleCount - 1) * width;
                var normalized = (samples[i].Value - min) / (max - min);
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
    }

    private void OnSamplesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    private bool TryGetVisibleRange(IReadOnlyList<MetricSample> samples, out int startIndex, out int endIndex)
    {
        startIndex = 0;
        endIndex = samples.Count - 1;

        if (samples.Count < 2)
        {
            return false;
        }

        var range = TimeRange;
        if (range <= TimeSpan.Zero)
        {
            return true;
        }

        var latestTimestamp = samples[^1].Timestamp;
        var minTimestamp = latestTimestamp - range;

        while (startIndex < samples.Count && samples[startIndex].Timestamp < minTimestamp)
        {
            startIndex++;
        }

        return endIndex - startIndex >= 1;
    }
}
