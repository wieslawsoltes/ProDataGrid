using Avalonia;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class Elements3DNodeViewModel
{
    public Elements3DNodeViewModel(
        int depth,
        string node,
        int zIndex,
        Rect boundsRect,
        bool isVisible,
        double opacity,
        Visual visual)
        : this(depth, node, zIndex, boundsRect, isVisible, opacity, visual, nodeId: null, nodePath: null, isRendered: true)
    {
    }

    public Elements3DNodeViewModel(
        int depth,
        string node,
        int zIndex,
        Rect boundsRect,
        bool isVisible,
        double opacity,
        Visual? visual,
        string? nodeId,
        string? nodePath,
        bool isRendered)
    {
        Depth = depth;
        Node = node;
        ZIndex = zIndex;
        BoundsRect = boundsRect;
        Bounds = boundsRect.ToString();
        IsVisible = isVisible;
        Opacity = opacity;
        Visual = visual;
        NodeId = nodeId;
        NodePath = nodePath;
        IsRendered = isRendered;
    }

    public int Depth { get; }

    public string Node { get; }

    public int ZIndex { get; }

    public Rect BoundsRect { get; }

    public string Bounds { get; }

    public bool IsVisible { get; }

    public double Opacity { get; }

    public bool IsRendered { get; }

    public string? NodeId { get; }

    public string? NodePath { get; }

    internal Visual? Visual { get; }
}
