using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace DataGridSample.CustomDrawing
{
    public sealed class SkiaTextCellDrawOperationFactory :
        IDataGridCellDrawOperationFactory,
        IDataGridCellDrawOperationMeasureProvider,
        IDataGridCellDrawOperationArrangeProvider
    {
        private readonly Dictionary<MetricsCacheKey, LinkedListNode<MetricsCacheEntry>> _metricsCache = new();
        private readonly LinkedList<MetricsCacheEntry> _metricsCacheLru = new();
        private readonly object _metricsCacheGate = new();

        public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;

        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;

        public Thickness Padding { get; set; } = new Thickness(4, 2, 4, 2);

        public int MetricsCacheCapacity { get; set; } = 4096;

        /// <summary>
        /// Enables optional per-item metrics caching via <see cref="IDataGridCellDrawOperationItemCache"/>.
        /// </summary>
        public bool UseItemCacheContract { get; set; }

        /// <summary>
        /// Factory-defined slot used for per-item cache entries.
        /// </summary>
        public int ItemCacheSlot { get; set; }

        public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
        {
            float fontSize = (float)Math.Max(1d, context.FontSize);
            var metrics = GetOrCreateMetrics(context.Item, context.Text ?? string.Empty, fontSize, context.Typeface.ToString() ?? string.Empty);
            return new SkiaTextCellDrawOperation(context, TextAlignment, VerticalAlignment, Padding, metrics);
        }

        public bool TryMeasure(DataGridCellDrawOperationMeasureContext context, out Size desiredSize)
        {
            float fontSize = (float)Math.Max(1d, context.FontSize);
            var metrics = GetOrCreateMetrics(context.Item, context.Text ?? string.Empty, fontSize, context.Typeface.ToString() ?? string.Empty);
            double width = metrics.MaxLineWidth + Padding.Left + Padding.Right;
            double height = metrics.TotalHeight + Padding.Top + Padding.Bottom;

            desiredSize = new Size(
                CoerceDimension(width, context.AvailableSize.Width),
                CoerceDimension(height, context.AvailableSize.Height));

            return true;
        }

        public bool TryArrange(DataGridCellDrawOperationArrangeContext context, out Size arrangedSize)
        {
            arrangedSize = context.FinalSize;
            return true;
        }

        private SkiaTextLayoutMetrics GetOrCreateMetrics(object? item, string text, float fontSize, string typefaceKey)
        {
            var key = new MetricsCacheKey(text, fontSize, typefaceKey);
            int itemCacheSlot = ItemCacheSlot;
            if (UseItemCacheContract &&
                itemCacheSlot >= 0 &&
                item is IDataGridCellDrawOperationItemCache itemCache)
            {
                int itemCacheKey = key.GetHashCode();
                if (itemCache.TryGetCellDrawCacheEntry(itemCacheSlot, itemCacheKey, out object? value) &&
                    value is ItemMetricsCacheEntry entry &&
                    entry.Key.Equals(key))
                {
                    return entry.Metrics;
                }

                var metrics = CreateMetrics(text, fontSize);
                itemCache.SetCellDrawCacheEntry(itemCacheSlot, itemCacheKey, new ItemMetricsCacheEntry(key, metrics));
                return metrics;
            }

            int capacity = Math.Max(1, MetricsCacheCapacity);

            lock (_metricsCacheGate)
            {
                if (_metricsCache.TryGetValue(key, out LinkedListNode<MetricsCacheEntry>? existingNode))
                {
                    _metricsCacheLru.Remove(existingNode);
                    _metricsCacheLru.AddFirst(existingNode);
                    TrimMetricsCache(capacity);
                    return existingNode.Value.Metrics;
                }

                var metrics = CreateMetrics(text, fontSize);
                var entry = new MetricsCacheEntry(key, metrics);
                var node = new LinkedListNode<MetricsCacheEntry>(entry);
                _metricsCacheLru.AddFirst(node);
                _metricsCache[key] = node;
                TrimMetricsCache(capacity);
                return metrics;
            }
        }

        private void TrimMetricsCache(int capacity)
        {
            while (_metricsCache.Count > capacity)
            {
                LinkedListNode<MetricsCacheEntry>? last = _metricsCacheLru.Last;
                if (last is null)
                {
                    return;
                }

                _metricsCache.Remove(last.Value.Key);
                _metricsCacheLru.RemoveLast();
            }
        }

        private static SkiaTextLayoutMetrics CreateMetrics(string text, float fontSize)
        {
            var lines = (text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
            {
                lines = new[] { string.Empty };
            }

            using var paint = new SKPaint
            {
                IsAntialias = true,
                IsStroke = false,
                TextSize = fontSize
            };

            paint.GetFontMetrics(out SKFontMetrics metrics);
            float lineHeight = metrics.Descent - metrics.Ascent;
            if (lineHeight <= 0f)
            {
                lineHeight = paint.TextSize * 1.2f;
            }

            float[] lineWidths = new float[lines.Length];
            float maxLineWidth = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                float width = paint.MeasureText(lines[i]);
                lineWidths[i] = width;
                if (width > maxLineWidth)
                {
                    maxLineWidth = width;
                }
            }

            float totalHeight = lines.Length * lineHeight;
            return new SkiaTextLayoutMetrics(lines, lineWidths, maxLineWidth, lineHeight, totalHeight, metrics.Ascent);
        }

        private static double CoerceDimension(double value, double limit)
        {
            if (double.IsNaN(value) || value < 0)
            {
                value = 0;
            }

            if (!double.IsInfinity(limit))
            {
                value = Math.Min(value, Math.Max(0, limit));
            }

            return value;
        }

        private readonly struct MetricsCacheEntry
        {
            public MetricsCacheEntry(MetricsCacheKey key, SkiaTextLayoutMetrics metrics)
            {
                Key = key;
                Metrics = metrics;
            }

            public MetricsCacheKey Key { get; }

            public SkiaTextLayoutMetrics Metrics { get; }
        }

        private readonly struct ItemMetricsCacheEntry
        {
            public ItemMetricsCacheEntry(MetricsCacheKey key, SkiaTextLayoutMetrics metrics)
            {
                Key = key;
                Metrics = metrics;
            }

            public MetricsCacheKey Key { get; }

            public SkiaTextLayoutMetrics Metrics { get; }
        }

        private readonly struct MetricsCacheKey : IEquatable<MetricsCacheKey>
        {
            private readonly string _text;
            private readonly float _fontSize;
            private readonly string _typefaceKey;

            public MetricsCacheKey(string text, float fontSize, string typefaceKey)
            {
                _text = text;
                _fontSize = fontSize;
                _typefaceKey = typefaceKey;
            }

            public bool Equals(MetricsCacheKey other)
            {
                return string.Equals(_text, other._text, StringComparison.Ordinal) &&
                       _fontSize.Equals(other._fontSize) &&
                       string.Equals(_typefaceKey, other._typefaceKey, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is MetricsCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(_text, StringComparer.Ordinal);
                hash.Add(_fontSize);
                hash.Add(_typefaceKey, StringComparer.Ordinal);
                return hash.ToHashCode();
            }
        }
    }

    internal sealed class SkiaTextCellDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaTextLayoutMetrics _metrics;
        private readonly SKColor _color;
        private readonly float _fontSize;
        private readonly TextAlignment _textAlignment;
        private readonly VerticalAlignment _verticalAlignment;
        private readonly SKRect _contentRect;

        public SkiaTextCellDrawOperation(
            DataGridCellDrawOperationContext context,
            TextAlignment textAlignment,
            VerticalAlignment verticalAlignment,
            Thickness padding,
            SkiaTextLayoutMetrics metrics)
        {
            Bounds = context.Bounds;
            _textAlignment = textAlignment;
            _verticalAlignment = verticalAlignment;
            _fontSize = (float)Math.Max(1d, context.FontSize);
            _color = GetTextColor(context.Foreground);
            _metrics = metrics;

            float left = (float)(context.Bounds.X + padding.Left);
            float top = (float)(context.Bounds.Y + padding.Top);
            float right = (float)(context.Bounds.Right - padding.Right);
            float bottom = (float)(context.Bounds.Bottom - padding.Bottom);

            if (right < left)
            {
                right = left;
            }

            if (bottom < top)
            {
                bottom = top;
            }

            _contentRect = new SKRect(left, top, right, bottom);
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
            // Nothing to dispose.
        }

        public bool HitTest(Point p)
        {
            return Bounds.Contains(p);
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            using ISkiaSharpApiLease? lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null || _contentRect.Width <= 0f || _contentRect.Height <= 0f)
            {
                return;
            }

            SKCanvas canvas = lease.SkCanvas;
            canvas.Save();
            canvas.ClipRect(_contentRect);

            using SKPaint paint = new SKPaint
            {
                IsAntialias = true,
                IsStroke = false,
                Color = _color,
                TextSize = _fontSize
            };

            float y = GetInitialBaselineY(
                _contentRect,
                _metrics.Ascent,
                _metrics.LineHeight,
                _metrics.TotalHeight,
                _verticalAlignment);

            for (int i = 0; i < _metrics.Lines.Length; i++)
            {
                string line = _metrics.Lines[i];
                float lineWidth = _metrics.LineWidths[i];
                float x = GetAlignedX(_contentRect, lineWidth, _textAlignment);
                canvas.DrawText(line, x, y, paint);
                y += _metrics.LineHeight;
            }

            canvas.Restore();
        }

        private static float GetAlignedX(SKRect bounds, float lineWidth, TextAlignment alignment)
        {
            return alignment switch
            {
                TextAlignment.Right => bounds.Right - lineWidth,
                TextAlignment.Center => bounds.Left + ((bounds.Width - lineWidth) * 0.5f),
                _ => bounds.Left
            };
        }

        private static float GetInitialBaselineY(
            SKRect bounds,
            float ascent,
            float lineHeight,
            float totalHeight,
            VerticalAlignment alignment)
        {
            float availableHeight = bounds.Height;
            float topOffset = alignment switch
            {
                VerticalAlignment.Bottom => Math.Max(0f, availableHeight - totalHeight),
                VerticalAlignment.Center => Math.Max(0f, (availableHeight - totalHeight) * 0.5f),
                _ => 0f
            };

            return bounds.Top + topOffset - ascent;
        }

        private static SKColor GetTextColor(IBrush? brush)
        {
            if (brush is ISolidColorBrush solidColorBrush)
            {
                Color color = solidColorBrush.Color;
                byte alpha = (byte)Math.Clamp((int)Math.Round(color.A * solidColorBrush.Opacity), 0, byte.MaxValue);
                return new SKColor(color.R, color.G, color.B, alpha);
            }

            return SKColors.Black;
        }
    }

    internal sealed class SkiaTextLayoutMetrics
    {
        public SkiaTextLayoutMetrics(
            string[] lines,
            float[] lineWidths,
            float maxLineWidth,
            float lineHeight,
            float totalHeight,
            float ascent)
        {
            Lines = lines;
            LineWidths = lineWidths;
            MaxLineWidth = maxLineWidth;
            LineHeight = lineHeight;
            TotalHeight = totalHeight;
            Ascent = ascent;
        }

        public string[] Lines { get; }

        public float[] LineWidths { get; }

        public float MaxLineWidth { get; }

        public float LineHeight { get; }

        public float TotalHeight { get; }

        public float Ascent { get; }
    }
}
