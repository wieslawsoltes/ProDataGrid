// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia.Media;

namespace Avalonia.Controls;

partial class DataGridRow
{
    private const double FlatHorizontalGridLineHeight = 1d;

    internal double GetFlatCellsHeight()
    {
        if (OwningGrid is not { } grid)
        {
            return DesiredSize.Height;
        }

        return double.IsNaN(grid.RowHeight)
            ? Math.Max(0, DesiredSize.Height - GetFlatHorizontalGridLineHeight(grid))
            : grid.RowHeight;
    }

    internal static double GetFlatDesiredHeight(DataGrid grid, double contentHeight)
    {
        double rowHeight = double.IsNaN(grid.RowHeight) ? contentHeight : grid.RowHeight;
        return rowHeight + GetFlatHorizontalGridLineHeight(grid);
    }

    internal void InvalidateFlatGridLine()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (OwningGrid is not { UsesFlatVisualLayout: true } grid ||
            GetFlatHorizontalGridLineHeight(grid) <= 0 ||
            Bounds.Width <= 0 ||
            Bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(
            grid.HorizontalGridLinesBrush,
            pen: null,
            new Rect(0, Math.Max(0, Bounds.Height - FlatHorizontalGridLineHeight), Bounds.Width, FlatHorizontalGridLineHeight));
    }

    internal void DetachCellsFromNestedPresenter()
    {
        if (_cellsElement == null)
        {
            return;
        }

        for (int index = 0; index < Cells.Count; index++)
        {
            DataGridCell cell = Cells[index];
            if (ReferenceEquals(cell.Parent, _cellsElement))
            {
                _cellsElement.Children.Remove(cell);
            }

            AttachFlatCellLogicalChild(cell);
        }

        if (ExistingFillerCell is { } fillerCell)
        {
            if (ReferenceEquals(fillerCell.Parent, _cellsElement))
            {
                _cellsElement.Children.Remove(fillerCell);
            }

            AttachFlatCellLogicalChild(fillerCell);
        }
    }

    internal void AttachFlatCellLogicalChild(DataGridCell cell)
    {
        cell.RestoreFlatLogicalContentParent();
        if (!LogicalChildren.Contains(cell))
        {
            LogicalChildren.Add(cell);
        }
    }

    internal void DetachFlatCellLogicalChild(DataGridCell cell)
    {
        LogicalChildren.Remove(cell);
    }

    internal void UpdateFlatCellDataContexts()
    {
        for (int index = 0; index < Cells.Count; index++)
        {
            Cells[index].SetFlatDataContext(DataContext);
        }

        ExistingFillerCell?.SetFlatDataContext(DataContext);
    }

    private static double GetFlatHorizontalGridLineHeight(DataGrid grid) =>
        grid.GridLinesVisibility is DataGridGridLinesVisibility.Horizontal or DataGridGridLinesVisibility.All
            ? FlatHorizontalGridLineHeight
            : 0d;

}
