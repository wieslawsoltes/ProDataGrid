// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using Avalonia.Controls;

namespace ProCharts.Controls;

/// <summary>Stacks chart chrome at its natural height and assigns remaining height to one plot child.</summary>
/// <remarks>
/// Children are measured without a vertical constraint so their minimum heights are not lost to
/// DesiredSize clipping. A short host clips ordered content instead of overlapping toolbar hit targets.
/// Use a vertical ScrollViewer when every part must remain reachable below the intrinsic height.
/// This panel is intended for a small number of chart parts, not a virtualizing items layout.
/// </remarks>
public class ChartLayoutPanel : Panel
{
    private double _measuredWidth = double.NaN;

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
    protected override Size MeasureOverride(Size availableSize) => MeasureChildren(availableSize.Width);

    private Size MeasureChildren(double availableWidth)
    {
        _measuredWidth = availableWidth;
        double height = 0, width = 0;
        foreach (Control child in Children)
        {
            // A finite remaining height below MinHeight would clip DesiredSize. Arranging that
            // clipped slot makes Avalonia center the larger child over the preceding toolbar.
            child.Measure(new Size(availableWidth, double.PositiveInfinity));
            if (!child.IsVisible) continue;
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Scrollbars and constrained hosts can allocate a narrower width than the measure offer.
        // Reflow wrapped chrome against that actual width before assigning vertical slots.
        if (_measuredWidth != finalSize.Width) MeasureChildren(finalSize.Width);

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
