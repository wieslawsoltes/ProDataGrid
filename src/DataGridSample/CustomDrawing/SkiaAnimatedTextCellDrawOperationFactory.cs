using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace DataGridSample.CustomDrawing;

public sealed class SkiaAnimatedTextCellDrawOperationFactory :
    IDataGridCellDrawOperationFactory,
    IDataGridCellDrawOperationInvalidationSource
{
    public Thickness Padding { get; set; } = new(4, 2, 4, 2);

    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;

    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;

    public float WaveAmplitude { get; set; } = 1.8f;

    public float Phase { get; private set; }

    public event EventHandler<DataGridCellDrawOperationInvalidatedEventArgs>? Invalidated;

    public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
    {
        float fontSize = (float)Math.Max(1d, context.FontSize);
        float phase = Phase + GetItemPhaseOffset(context.Item);
        return new SkiaAnimatedTextCellDrawOperation(
            context.Bounds,
            context.Text ?? string.Empty,
            ResolveTextColor(context.Foreground),
            fontSize,
            Padding,
            TextAlignment,
            VerticalAlignment,
            phase,
            WaveAmplitude);
    }

    public void SetPhase(float phase)
    {
        Phase = phase;
    }

    public void SetPhaseAndInvalidate(float phase)
    {
        Phase = phase;
        Invalidate();
    }

    public void Invalidate(bool invalidateMeasure = false, bool clearTextLayoutCache = false)
    {
        Invalidated?.Invoke(
            this,
            new DataGridCellDrawOperationInvalidatedEventArgs(
                invalidateMeasure: invalidateMeasure,
                clearTextLayoutCache: clearTextLayoutCache));
    }

    private static float GetItemPhaseOffset(object? item)
    {
        if (item is null)
        {
            return 0f;
        }

        int hash = RuntimeHelpers.GetHashCode(item);
        return (hash & 0x3FF) * 0.01f;
    }

    private static SKColor ResolveTextColor(IBrush? brush)
    {
        if (brush is ISolidColorBrush solidColorBrush)
        {
            Color color = solidColorBrush.Color;
            byte alpha = (byte)Math.Clamp(
                (int)Math.Round(color.A * solidColorBrush.Opacity),
                0,
                byte.MaxValue);
            return new SKColor(color.R, color.G, color.B, alpha);
        }

        return SKColors.Black;
    }
}

internal sealed class SkiaAnimatedTextCellDrawOperation : ICustomDrawOperation
{
    private readonly string _text;
    private readonly SKColor _baseColor;
    private readonly float _fontSize;
    private readonly TextAlignment _textAlignment;
    private readonly VerticalAlignment _verticalAlignment;
    private readonly float _phase;
    private readonly float _waveAmplitude;
    private readonly SKRect _contentRect;

    public SkiaAnimatedTextCellDrawOperation(
        Rect bounds,
        string text,
        SKColor baseColor,
        float fontSize,
        Thickness padding,
        TextAlignment textAlignment,
        VerticalAlignment verticalAlignment,
        float phase,
        float waveAmplitude)
    {
        Bounds = bounds;
        _text = text;
        _baseColor = baseColor;
        _fontSize = fontSize;
        _textAlignment = textAlignment;
        _verticalAlignment = verticalAlignment;
        _phase = phase;
        _waveAmplitude = Math.Max(0f, waveAmplitude);

        float left = (float)(bounds.X + padding.Left);
        float top = (float)(bounds.Y + padding.Top);
        float right = (float)(bounds.Right - padding.Right);
        float bottom = (float)(bounds.Bottom - padding.Bottom);

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
    }

    public bool HitTest(Point p)
    {
        return Bounds.Contains(p);
    }

    public bool Equals(ICustomDrawOperation? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is SkiaAnimatedTextCellDrawOperation operation &&
               Bounds.Equals(operation.Bounds) &&
               string.Equals(_text, operation._text, StringComparison.Ordinal) &&
               _baseColor.Equals(operation._baseColor) &&
               _fontSize.Equals(operation._fontSize) &&
               _textAlignment == operation._textAlignment &&
               _verticalAlignment == operation._verticalAlignment &&
               _phase.Equals(operation._phase) &&
               _waveAmplitude.Equals(operation._waveAmplitude) &&
               _contentRect.Equals(operation._contentRect);
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

        using var font = new SKFont
        {
            Size = _fontSize,
            Edging = SKFontEdging.Antialias,
            Subpixel = true
        };

        using var paint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = false,
            Color = GetAnimatedColor(_baseColor, _phase)
        };

        font.GetFontMetrics(out SKFontMetrics metrics);
        float lineHeight = metrics.Descent - metrics.Ascent;
        if (lineHeight <= 0f)
        {
            lineHeight = Math.Max(1f, _fontSize * 1.2f);
        }

        float lineWidth = MeasureTextWidth(_text, font, paint);
        float x = GetAlignedX(_contentRect, lineWidth, _textAlignment);
        float y = GetBaselineY(_contentRect, metrics.Ascent, lineHeight, _verticalAlignment);
        y += (float)Math.Sin(_phase * 1.9f) * _waveAmplitude;

        canvas.DrawText(_text, x, y, font, paint);

        using var underlinePaint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = true,
            StrokeWidth = 1.5f,
            Color = GetAnimatedColor(_baseColor, _phase + 0.9f)
        };

        float progress = 0.5f + (0.5f * (float)Math.Sin(_phase * 1.3f));
        float maxWidth = Math.Max(0f, _contentRect.Width);
        float underlineWidth = Math.Max(8f, maxWidth * progress);
        float underlineStart = _contentRect.Left;
        float underlineEnd = Math.Min(_contentRect.Right, underlineStart + underlineWidth);
        float underlineY = Math.Min(_contentRect.Bottom - 1f, y - metrics.Descent + 2f);
        canvas.DrawLine(underlineStart, underlineY, underlineEnd, underlineY, underlinePaint);

        canvas.Restore();
    }

    private static float MeasureTextWidth(string text, SKFont font, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        int glyphCount = font.CountGlyphs(text);
        if (glyphCount <= 0)
        {
            return 0f;
        }

        if (glyphCount <= 256)
        {
            Span<ushort> glyphs = stackalloc ushort[glyphCount];
            font.GetGlyphs(text, glyphs);
            return font.MeasureText(glyphs, paint);
        }

        var glyphsArray = new ushort[glyphCount];
        font.GetGlyphs(text, glyphsArray);
        return font.MeasureText(glyphsArray, paint);
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

    private static float GetBaselineY(SKRect bounds, float ascent, float lineHeight, VerticalAlignment verticalAlignment)
    {
        float topOffset = verticalAlignment switch
        {
            VerticalAlignment.Bottom => Math.Max(0f, bounds.Height - lineHeight),
            VerticalAlignment.Center => Math.Max(0f, (bounds.Height - lineHeight) * 0.5f),
            _ => 0f
        };

        return bounds.Top + topOffset - ascent;
    }

    private static SKColor GetAnimatedColor(SKColor color, float phase)
    {
        float pulse = 0.5f + (0.5f * (float)Math.Sin(phase));
        byte mixR = (byte)Math.Clamp(color.Red + ((255 - color.Red) * pulse * 0.28f), 0, 255);
        byte mixG = (byte)Math.Clamp(color.Green + ((255 - color.Green) * pulse * 0.28f), 0, 255);
        byte mixB = (byte)Math.Clamp(color.Blue + ((255 - color.Blue) * pulse * 0.28f), 0, 255);
        return new SKColor(mixR, mixG, mixB, color.Alpha);
    }
}
