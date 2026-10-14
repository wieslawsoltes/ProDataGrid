// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using Avalonia.Controls;

namespace ProCharts.Controls;

/// <summary>Stacks chart chrome at its measured height and assigns remaining height to one plot child.</summary>
/// <remarks>
/// Unlike compressed auto/star rows, this panel never places the plot on top of a wrapped toolbar.
/// When constrained below its intrinsic height, content remains in order and is clipped rather than
/// overlapping. A host can use a vertical ScrollViewer to keep every part reachable in a small window.
/// </remarks>
public class ChartLayoutPanel : Panel
{
    /// <summary>Identifies the child that expands to fill unused vertical space; -1 disables expansion.</summary>
    public static readonly StyledProperty<int> FillIndexProperty =
        AvaloniaProperty.Register<ChartLayoutPanel, int>(nameof(FillIndex), -1, validate: value => value >= -1);

    /// <summary>Gets or sets the zero-based expanding child index.</summary>
    public int FillIndex { get => GetValue(FillIndexProperty); set => SetValue(FillIndexProperty, value); }

    static ChartLayoutPanel()
    {
        AffectsMeasure<ChartLayoutPanel>(FillIndexProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ChartLayoutPanel>(true);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0, width = 0;
        // Measure natural chrome heights without letting a short host compress its row slots.
        for (int i = 0; i < Children.Count; i++)
        {
            if (i == FillIndex) continue;
            Control child = Children[i];
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (!child.IsVisible) continue;
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        if ((uint)FillIndex < (uint)Children.Count)
        {
            Control fill = Children[FillIndex];
            double remaining = double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height - height) : double.PositiveInfinity;
            fill.Measure(new Size(availableSize.Width, remaining));
            if (fill.IsVisible)
            {
                height += fill.DesiredSize.Height;
                width = Math.Max(width, fill.DesiredSize.Width);
            }
        }
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        double chrome = 0;
        for (int i = 0; i < Children.Count; i++)
            if (i != FillIndex && Children[i].IsVisible) chrome += Children[i].DesiredSize.Height;
        double y = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            Control child = Children[i];
            if (!child.IsVisible) { child.Arrange(default); continue; }
            double height = child.DesiredSize.Height;
            if (i == FillIndex) height = Math.Max(height, Math.Max(0, finalSize.Height - chrome));
            child.Arrange(new Rect(0, y, finalSize.Width, height));
            y += height;
        }
        return finalSize;
    }
}
