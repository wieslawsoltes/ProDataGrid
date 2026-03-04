using System;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace Avalonia.Diagnostics.Services;

internal static class PreviewSvgVectorExporter
{
    private static readonly Vector s_exportDpi = new(96, 96);

    public static string? Export(
        TopLevel topLevel,
        int requestedWidth,
        int requestedHeight)
    {
        if (!IsSkiaRenderBackend())
        {
            return null;
        }

        var width = Math.Clamp(requestedWidth, 1, 8192);
        var height = Math.Clamp(requestedHeight, 1, 8192);

        using var stream = new MemoryStream();
        var viewport = SKRect.Create(width, height);
        try
        {
            using var canvas = SKSvgCanvas.Create(viewport, stream);
            if (canvas is null)
            {
                return null;
            }

            DrawingContextHelper
                .RenderAsync(canvas, topLevel, new Rect(0, 0, width, height), s_exportDpi)
                .GetAwaiter()
                .GetResult();

            canvas.Flush();
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        var svg = reader.ReadToEnd();
        return string.IsNullOrWhiteSpace(svg) ? null : svg;
    }

    private static bool IsSkiaRenderBackend()
    {
        var renderInterface = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();
        var typeName = renderInterface?.GetType().FullName;
        return typeName?.Contains("Avalonia.Skia", StringComparison.Ordinal) == true;
    }
}
