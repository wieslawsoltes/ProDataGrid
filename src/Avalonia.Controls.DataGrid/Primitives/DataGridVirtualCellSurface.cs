// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace Avalonia.Controls.Primitives;

/// <summary>
/// Owns the single retained visual used by the virtualized display-cell pipeline.
/// Cell geometry, drawing, and hit testing remain presenter-owned; this control is
/// deliberately free of row or cell state so it can be reused for the presenter's lifetime.
/// </summary>
internal sealed class DataGridVirtualCellSurface : Control, ICustomHitTest
{
    internal DataGridRowsPresenter? Owner { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Owner?.RenderVirtualCells(context);
    }

    bool ICustomHitTest.HitTest(Point point) => Owner?.IsVirtualCellPoint(point) == true;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Owner?.HandleVirtualCellPointerPressed(e) == true)
        {
            // Preserve the same touch/pen scroll gesture behavior as retained cells.
            if (e.Pointer.Type is not PointerType.Touch and not PointerType.Pen)
            {
                e.Handled = true;
            }
        }
    }
}
