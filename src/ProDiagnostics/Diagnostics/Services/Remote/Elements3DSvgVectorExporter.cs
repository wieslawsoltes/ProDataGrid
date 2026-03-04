using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Platform;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace Avalonia.Diagnostics.Services;

internal static class Elements3DSvgVectorExporter
{
    private static readonly Vector s_exportDpi = new(96, 96);

    public static (string svgSnapshot, string svgViewBox)? Export(
        Elements3DPageViewModel viewModel,
        int requestedWidth,
        int requestedHeight,
        int maxSvgNodes)
    {
        if (!IsSkiaRenderBackend())
        {
            return null;
        }

        var width = Math.Clamp(requestedWidth, 320, 8192);
        var height = Math.Clamp(requestedHeight, 240, 8192);

        var orderedNodes = viewModel.VisibleNodes
            .Where(node =>
                double.IsFinite(node.BoundsRect.X) &&
                double.IsFinite(node.BoundsRect.Y) &&
                double.IsFinite(node.BoundsRect.Width) &&
                double.IsFinite(node.BoundsRect.Height) &&
                node.BoundsRect.Width >= 0 &&
                node.BoundsRect.Height >= 0)
            .OrderBy(node => node.Depth)
            .ThenByDescending(node => node.ZIndex)
            .ToArray();
        if (orderedNodes.Length == 0)
        {
            return null;
        }

        var nodeLimit = maxSvgNodes <= 0
            ? orderedNodes.Length
            : Math.Min(orderedNodes.Length, maxSvgNodes);
        if (nodeLimit <= 0)
        {
            return null;
        }

        if (nodeLimit < orderedNodes.Length)
        {
            Array.Resize(ref orderedNodes, nodeLimit);
        }

        var selectedNode = viewModel.SelectedNode is not null && orderedNodes.Contains(viewModel.SelectedNode)
            ? viewModel.SelectedNode
            : null;

        var renderView = CreateRenderView(viewModel, orderedNodes, selectedNode, width, height);
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
                .RenderAsync(canvas, renderView, new Rect(0, 0, width, height), s_exportDpi)
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
        if (string.IsNullOrWhiteSpace(svg))
        {
            return null;
        }

        return (svg, "0 0 " + width + " " + height);
    }

    private static Elements3DExplodedView CreateRenderView(
        Elements3DPageViewModel source,
        Elements3DNodeViewModel[] items,
        Elements3DNodeViewModel? selectedNode,
        int width,
        int height)
    {
        var flattenTo2D = source.ShowAllLayersInGrid;
        var normalizedDepthSpacing = Math.Clamp(NormalizeFinite(source.DepthSpacing, 22d), 1d, 256d);
        var normalizedTilt = Math.Clamp(NormalizeFinite(source.Tilt, 0.55d), -2d, 2d);
        var normalizedZoom = Math.Clamp(NormalizeFinite(source.Zoom, 1d), 0.05d, 64d);
        var normalizedYaw = NormalizeFinite(source.OrbitYaw, 0d);
        var normalizedPitch = NormalizeFinite(source.OrbitPitch, 0d);
        var normalizedRoll = NormalizeFinite(source.OrbitRoll, 0d);
        var renderView = new Elements3DExplodedView
        {
            Width = width,
            Height = height,
            Items = items,
            SelectedItem = selectedNode,
            ShowInvisible = source.ShowInvisibleNodes,
            DepthSpacing = normalizedDepthSpacing,
            Tilt = normalizedTilt,
            Zoom = normalizedZoom,
            OrbitYaw = flattenTo2D ? 0d : normalizedYaw,
            OrbitPitch = flattenTo2D ? 0d : normalizedPitch,
            OrbitRoll = flattenTo2D ? 0d : normalizedRoll,
            FlattenTo2D = flattenTo2D,
            Flat2DMaxLayersPerRow = source.Flat2DMaxLayersPerRow,
        };

        renderView.Measure(new Size(width, height));
        renderView.Arrange(new Rect(0, 0, width, height));
        renderView.UpdateLayout();
        return renderView;
    }

    private static bool IsSkiaRenderBackend()
    {
        var renderInterface = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();
        var typeName = renderInterface?.GetType().FullName;
        return typeName?.Contains("Avalonia.Skia", StringComparison.Ordinal) == true;
    }

    private static double NormalizeFinite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;
}
