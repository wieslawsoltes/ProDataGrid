using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Controls;

partial class ControlHighlightAdorner : Control
{
    private static readonly IPen s_rulerPen =
        new Pen(new SolidColorBrush(Color.FromArgb(232, 255, 122, 0)), 1).ToImmutable();
    private static readonly IPen s_extensionPen =
        new Pen(new SolidColorBrush(Color.FromArgb(218, 0, 191, 255)), 1).ToImmutable();
    private static readonly IBrush s_infoBackgroundBrush =
        new SolidColorBrush(Color.FromArgb(230, 30, 30, 30)).ToImmutable();
    private static readonly IBrush s_infoForegroundBrush = Brushes.White;
    readonly IPen _pen;

    private ControlHighlightAdorner(IPen pen)
    {
        _pen = pen;
        this.Clip = null;
    }

    public static IDisposable? Add(InputElement owner, IBrush highlightBrush)
    {

        if (AdornerLayer.GetAdornerLayer(owner) is { } layer)
        {
            var pen = new Pen(highlightBrush, 2).ToImmutable();
            var adorner = new ControlHighlightAdorner(pen)
            {
                [AdornerLayer.AdornedElementProperty] = owner
            };
            layer.Children.Add(adorner);

            return Disposable.Create((layer, adorner), state =>
            {
                state.layer.Children.Remove(state.adorner);
            });
        }
        return default;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(_pen, Bounds.Deflate(2));
    }

    internal static IDisposable? Add(Visual visual, bool visualizeMarginPadding)
    {
        return Add(
            visual,
            new OverlayDisplayOptions(
                VisualizeMarginPadding: visualizeMarginPadding,
                ShowInfo: false,
                ShowRulers: false,
                ShowExtensionLines: false));
    }

    internal static IDisposable? Add(Visual visual, OverlayDisplayOptions options)
    {
        if (AdornerLayer.GetAdornerLayer(visual) is { } layer)
        {
            var overlayAdorner = new InspectionOverlayAdorner(layer, visual, options)
            {
                [AdornerLayer.AdornedElementProperty] = layer
            };
            AdornerLayer.SetIsClipEnabled(overlayAdorner, false);
            layer.Children.Add(overlayAdorner);

            return Disposable.Create((Layer: layer, Adorner: overlayAdorner), state =>
            {
                state.Adorner.Dispose();
                state.Layer.Children.Remove(state.Adorner);
            });
        }
        return default;
    }

    private static Rect InflateRect(Rect rect, Thickness thickness)
    {
        return new Rect(
            rect.X - thickness.Left,
            rect.Y - thickness.Top,
            Math.Max(1, rect.Width + thickness.Left + thickness.Right),
            Math.Max(1, rect.Height + thickness.Top + thickness.Bottom));
    }

    private static Rect DeflateRect(Rect rect, Thickness thickness)
    {
        var x = rect.X + thickness.Left;
        var y = rect.Y + thickness.Top;
        var width = rect.Width - thickness.Left - thickness.Right;
        var height = rect.Height - thickness.Top - thickness.Bottom;
        if (width <= 1 || height <= 1)
        {
            return new Rect(x, y, Math.Max(1, width), Math.Max(1, height));
        }

        return new Rect(x, y, width, height);
    }

    private static string CreateInfoText(Visual visual, Rect globalBounds)
    {
        var typeName = visual.GetType().Name;
        var name = (visual as StyledElement)?.Name;
        var localBounds = visual.Bounds;
        var sizeText = $"{localBounds.Width:0.#} x {localBounds.Height:0.#}";
        var positionText = $"{globalBounds.X:0.#}, {globalBounds.Y:0.#}";

        if (!string.IsNullOrWhiteSpace(name))
        {
            typeName += "#" + name;
        }

        var zIndex = visual.GetValue(Panel.ZIndexProperty);
        var rootName = TopLevel.GetTopLevel(visual)?.GetType().Name ?? "VisualRoot";
        return $"{typeName}\nGlobal: {positionText}\nSize: {sizeText}\nZIndex: {zIndex}\nRoot: {rootName}";
    }

    private sealed class InspectionOverlayAdorner : Control, IDisposable
    {
        private readonly AdornerLayer _layer;
        private readonly Visual _target;
        private readonly OverlayDisplayOptions _options;
        private readonly IDisposable _targetBoundsSubscription;
        private readonly IDisposable _layerBoundsSubscription;
        private readonly IBrush _contentBrush = new SolidColorBrush(Color.FromArgb(128, 160, 197, 232)).ToImmutable();
        private readonly IPen _marginPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 216, 0)), 1).ToImmutable();
        private readonly IPen _paddingPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 64, 200, 64)), 1).ToImmutable();
        private readonly IBrush _rulerBandBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)).ToImmutable();
        private readonly IBrush _rulerTextBrush = new SolidColorBrush(Color.FromArgb(220, 70, 70, 70)).ToImmutable();
        private const double RulerThickness = 18;
        private const double InfoMargin = 8;

        public InspectionOverlayAdorner(AdornerLayer layer, Visual target, OverlayDisplayOptions options)
        {
            _layer = layer;
            _target = target;
            _options = options;
            IsHitTestVisible = false;
            ClipToBounds = false;
            Clip = null;

            _targetBoundsSubscription = _target.GetObservable(Visual.BoundsProperty).Subscribe(_ => InvalidateVisual());
            _layerBoundsSubscription = _layer.GetObservable(BoundsProperty).Subscribe(_ => InvalidateVisual());
        }

        public void Dispose()
        {
            _targetBoundsSubscription.Dispose();
            _layerBoundsSubscription.Dispose();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            if (!TryGetTargetRect(out var targetRect))
            {
                return;
            }

            DrawMarginPadding(context, targetRect);

            if (_options.ShowRulers)
            {
                DrawRulers(context, bounds, targetRect);
            }

            if (_options.ShowExtensionLines)
            {
                var centerX = targetRect.X + (targetRect.Width * 0.5);
                var centerY = targetRect.Y + (targetRect.Height * 0.5);
                context.DrawLine(
                    s_extensionPen,
                    new Point(0, centerY),
                    new Point(bounds.Width, centerY));
                context.DrawLine(
                    s_extensionPen,
                    new Point(centerX, 0),
                    new Point(centerX, bounds.Height));
            }

            if (_options.ShowInfo)
            {
                DrawInfoPanel(context, bounds, targetRect);
            }
        }

        private bool TryGetTargetRect(out Rect rect)
        {
            rect = default;
            if (_target.VisualRoot is null)
            {
                return false;
            }

            var transform = _target.TransformToVisual(_layer);
            if (!transform.HasValue)
            {
                return false;
            }

            var localBounds = _target.Bounds;
            if (localBounds.Width <= 0 || localBounds.Height <= 0)
            {
                return false;
            }

            rect = new Rect(localBounds.Size).TransformToAABB(transform.Value);
            return rect.Width > 0 && rect.Height > 0;
        }

        private void DrawMarginPadding(DrawingContext context, Rect targetRect)
        {
            context.DrawRectangle(_contentBrush, null, targetRect);
            if (!_options.VisualizeMarginPadding)
            {
                return;
            }

            var margin = _target is Layoutable layoutable ? layoutable.Margin : default;
            var padding = _target is TemplatedControl templated ? templated.Padding : default;
            var marginRect = InflateRect(targetRect, margin);
            var contentRect = DeflateRect(targetRect, padding);

            context.DrawRectangle(null, _marginPen, marginRect);
            context.DrawRectangle(null, _paddingPen, targetRect);
            context.DrawRectangle(null, _paddingPen, contentRect);
        }

        private void DrawRulers(DrawingContext context, Rect bounds, Rect targetRect)
        {
            context.DrawRectangle(_rulerBandBrush, null, new Rect(0, 0, bounds.Width, RulerThickness));
            context.DrawRectangle(_rulerBandBrush, null, new Rect(0, 0, RulerThickness, bounds.Height));
            context.DrawLine(s_rulerPen, new Point(0, RulerThickness - 1), new Point(bounds.Width, RulerThickness - 1));
            context.DrawLine(s_rulerPen, new Point(RulerThickness - 1, 0), new Point(RulerThickness - 1, bounds.Height));

            for (var x = 0; x <= bounds.Width; x += 10)
            {
                var isMajor = x % 50 == 0;
                var tickHeight = isMajor ? 8 : 4;
                context.DrawLine(
                    s_rulerPen,
                    new Point(x, RulerThickness - 1),
                    new Point(x, RulerThickness - tickHeight));

                if (x > RulerThickness && x % 100 == 0)
                {
                    var text = new FormattedText(
                        x.ToString(CultureInfo.InvariantCulture),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        10,
                        _rulerTextBrush);
                    context.DrawText(text, new Point(x + 2, 1));
                }
            }

            for (var y = 0; y <= bounds.Height; y += 10)
            {
                var isMajor = y % 50 == 0;
                var tickWidth = isMajor ? 8 : 4;
                context.DrawLine(
                    s_rulerPen,
                    new Point(RulerThickness - 1, y),
                    new Point(RulerThickness - tickWidth, y));

                if (y > RulerThickness && y % 100 == 0)
                {
                    var text = new FormattedText(
                        y.ToString(CultureInfo.InvariantCulture),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        10,
                        _rulerTextBrush);
                    context.DrawText(text, new Point(1, y + 1));
                }
            }

            context.DrawLine(s_rulerPen, new Point(0, targetRect.Y), new Point(bounds.Width, targetRect.Y));
            context.DrawLine(s_rulerPen, new Point(targetRect.X, 0), new Point(targetRect.X, bounds.Height));
        }

        private void DrawInfoPanel(DrawingContext context, Rect bounds, Rect targetRect)
        {
            var infoText = CreateInfoText(_target, targetRect);
            if (string.IsNullOrWhiteSpace(infoText))
            {
                return;
            }

            var text = new FormattedText(
                    infoText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    s_infoForegroundBrush);
            var panelWidth = text.Width + 10;
            var panelHeight = text.Height + 8;

            var minY = _options.ShowRulers ? RulerThickness + 2 : 2;
            var maxX = Math.Max(2, bounds.Width - panelWidth - 2);
            var maxY = Math.Max(minY, bounds.Height - panelHeight - 2);
            var alignedY = Math.Clamp(targetRect.Top, minY, maxY);
            var alignedX = Math.Clamp(targetRect.Left, 2, maxX);

            double x;
            double y;
            var rightX = targetRect.Right + InfoMargin;
            var leftX = targetRect.Left - panelWidth - InfoMargin;
            var aboveY = targetRect.Top - panelHeight - InfoMargin;
            var belowY = targetRect.Bottom + InfoMargin;

            if (rightX <= maxX)
            {
                x = rightX;
                y = alignedY;
            }
            else if (leftX >= 2)
            {
                x = leftX;
                y = alignedY;
            }
            else if (aboveY >= minY)
            {
                x = alignedX;
                y = aboveY;
            }
            else if (belowY <= maxY)
            {
                x = alignedX;
                y = belowY;
            }
            else
            {
                x = alignedX;
                y = alignedY;
            }

            var infoRect = new Rect(x, y, panelWidth, panelHeight);
            context.DrawRectangle(s_infoBackgroundBrush, null, infoRect, 3);
            context.DrawText(text, new Point(infoRect.X + 5, infoRect.Y + 4));

            var targetCenter = new Point(targetRect.X + (targetRect.Width * 0.5), targetRect.Y + (targetRect.Height * 0.5));
            var panelCenter = new Point(infoRect.X + (infoRect.Width * 0.5), infoRect.Y + (infoRect.Height * 0.5));

            Point anchor;
            Point panelAnchor;
            if (infoRect.Bottom <= targetRect.Top)
            {
                anchor = new Point(targetCenter.X, targetRect.Top);
                panelAnchor = new Point(panelCenter.X, infoRect.Bottom);
            }
            else if (infoRect.Top >= targetRect.Bottom)
            {
                anchor = new Point(targetCenter.X, targetRect.Bottom);
                panelAnchor = new Point(panelCenter.X, infoRect.Top);
            }
            else if (infoRect.Right <= targetRect.Left)
            {
                anchor = new Point(targetRect.Left, targetCenter.Y);
                panelAnchor = new Point(infoRect.Right, panelCenter.Y);
            }
            else if (infoRect.Left >= targetRect.Right)
            {
                anchor = new Point(targetRect.Right, targetCenter.Y);
                panelAnchor = new Point(infoRect.Left, panelCenter.Y);
            }
            else
            {
                anchor = targetCenter;
                panelAnchor = panelCenter;
            }

            context.DrawLine(s_rulerPen, anchor, panelAnchor);
        }
    }
}
