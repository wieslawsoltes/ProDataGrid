// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Utilities;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives;

sealed partial class DataGridRowsPresenter
{
    private readonly HashSet<DataGridCell> _flatCells = new();
    private readonly HashSet<DataGridCell> _desiredFlatCells = new();
    private readonly List<DataGridCell> _flatCellsToRemove = new();
    private readonly List<FlatColumnLayout> _flatColumnLayouts = new();
    private readonly DataGridCustomDrawingTextLayoutCache _flatTextLayoutCache =
        new(DataGridCustomDrawingCell.DefaultSharedTextLayoutCacheCapacity);
    private double _flatHeaderWidth;
    private double _flatFrozenLeftWidth;
    private double _flatRightFrozenStart;

    internal int FlatRealizedCellCount => _flatCells.Count;

    internal void ResetFlatVisualLayout()
    {
        if (_flatCells.Count == 0)
        {
            return;
        }

        _flatCellsToRemove.Clear();
        foreach (DataGridCell cell in _flatCells)
        {
            _flatCellsToRemove.Add(cell);
        }

        for (int index = 0; index < _flatCellsToRemove.Count; index++)
        {
            DetachFlatCell(_flatCellsToRemove[index]);
        }

        _flatCellsToRemove.Clear();
        _desiredFlatCells.Clear();
        _flatColumnLayouts.Clear();
        _flatTextLayoutCache.Clear();
    }

    private void SyncFlatCells()
    {
        if (OwningGrid?.UsesVirtualCellSurface == true)
        {
            ResetFlatVisualLayout();
            SyncVirtualCellSurface();
            _desiredFlatCells.Clear();
            foreach (DataGridRow row in OwningGrid.DisplayData.GetScrollingRows())
            {
                if (!ReferenceEquals(OwningGrid.EditingRow, row) ||
                    OwningGrid.CurrentColumnIndex < 0 ||
                    OwningGrid.CurrentColumnIndex >= row.Cells.Count)
                {
                    continue;
                }

                row.DetachCellsFromNestedPresenter();
                AddDesiredFlatCell(row.Cells[OwningGrid.CurrentColumnIndex], row);
            }
            return;
        }

        DetachVirtualCellSurface();
        if (OwningGrid?.UsesFlatVisualLayout != true)
        {
            ResetFlatVisualLayout();
            return;
        }

        _desiredFlatCells.Clear();
        foreach (DataGridRow row in OwningGrid.DisplayData.GetScrollingRows())
        {
            row.DetachCellsFromNestedPresenter();
            for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
            {
                AddDesiredFlatCell(row.Cells[columnIndex], row);
            }

            if (!OwningGrid.UseLightweightFiller && OwningGrid.ColumnsInternal.FillerColumn.IsActive)
            {
                AddDesiredFlatCell(row.FillerCell, row);
            }
        }

        _flatCellsToRemove.Clear();
        foreach (DataGridCell cell in _flatCells)
        {
            if (!_desiredFlatCells.Contains(cell))
            {
                _flatCellsToRemove.Add(cell);
            }
        }

        for (int index = 0; index < _flatCellsToRemove.Count; index++)
        {
            RetainRecycledFlatCell(_flatCellsToRemove[index]);
        }

        _flatCellsToRemove.Clear();
    }

    private void AddDesiredFlatCell(DataGridCell cell, DataGridRow row)
    {
        _desiredFlatCells.Add(cell);
        if (!ReferenceEquals(cell.Parent, row))
        {
            row.AttachFlatCellLogicalChild(cell);
        }
        cell.SetFlatDataContext(row.DataContext);
        if (cell is DataGridCustomDrawingCell drawingCell)
        {
            drawingCell.EnableFlatSharedTextLayoutCache(_flatTextLayoutCache);
        }

        if (_flatCells.Add(cell))
        {
            if (cell.GetVisualParent() is Panel parentPanel && !ReferenceEquals(parentPanel, this))
            {
                parentPanel.Children.Remove(cell);
            }

            if (!ReferenceEquals(cell.GetVisualParent(), this))
            {
                cell.SetValue(Panel.ZIndexProperty, 1);
                VisualChildren.Add(cell);
            }
        }
    }

    private void RemoveFlatCells(DataGridRow row)
    {
        for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
        {
            DataGridCell cell = row.Cells[columnIndex];
            if (_flatCells.Contains(cell))
            {
                DetachFlatCell(cell);
            }
        }

        if (row.ExistingFillerCell is { } fillerCell && _flatCells.Contains(fillerCell))
        {
            DetachFlatCell(fillerCell);
        }
    }

    private void DetachFlatCell(DataGridCell cell)
    {
        _flatCells.Remove(cell);
        _desiredFlatCells.Remove(cell);
        _measureConstraints.Remove(cell);
        if (ReferenceEquals(cell.GetVisualParent(), this))
        {
            VisualChildren.Remove(cell);
        }
        cell.OwningRow?.DetachFlatCellLogicalChild(cell);
        cell.ClearFlatDataContext();
        if (cell is DataGridCustomDrawingCell drawingCell)
        {
            drawingCell.DisableFlatSharedTextLayoutCache();
        }
        cell.ShowFlatVisual();
        cell.ClearValue(Panel.ZIndexProperty);
        cell.Clip = null;
    }

    private void RetainRecycledFlatCell(DataGridCell cell)
    {
        _desiredFlatCells.Remove(cell);
        _measureConstraints.Remove(cell);
        cell.HideFlatVisual();
    }

    private void MeasureFlatCells(DataGridRow row)
    {
        using var measureScope = DataGridDiagnostics.BeginCellsMeasure();
        DataGrid grid = OwningGrid!;
        double rowHeight = row.GetFlatCellsHeight();

        for (int layoutIndex = 0; layoutIndex < _flatColumnLayouts.Count; layoutIndex++)
        {
            FlatColumnLayout layout = _flatColumnLayouts[layoutIndex];
            DataGridColumn column = layout.Column;
            DataGridCell cell = row.Cells[column.Index];
            bool shouldDisplay = layout.ShouldDisplay || row.Index == 0;
            EnsureFlatCellDisplay(cell, shouldDisplay);

            if (shouldDisplay)
            {
                cell.Measure(new Size(column.LayoutRoundedWidth, rowHeight));
            }
        }

        if (!grid.UseLightweightFiller && grid.ColumnsInternal.FillerColumn.IsActive)
        {
            row.FillerCell.Measure(new Size(grid.ColumnsInternal.FillerColumn.FillerWidth, rowHeight));
        }
    }

    private void MeasureVirtualCompatibilityCell(DataGridRow row)
    {
        DataGrid grid = OwningGrid!;
        int columnIndex = grid.CurrentColumnIndex;
        if (!ReferenceEquals(grid.EditingRow, row) || columnIndex < 0 || columnIndex >= row.Cells.Count)
        {
            return;
        }

        DataGridColumn column = grid.ColumnsInternal[columnIndex];
        row.Cells[columnIndex].Measure(new Size(column.LayoutRoundedWidth, row.GetFlatCellsHeight()));
    }

    private void ArrangeFlatCells(DataGridRow row, double top, double height)
    {
        using var arrangeScope = DataGridDiagnostics.BeginCellsArrange();
        DataGrid grid = OwningGrid!;
        height = row.GetFlatCellsHeight();
        double lastScrollingRightEdge = _flatFrozenLeftWidth;

        for (int layoutIndex = 0; layoutIndex < _flatColumnLayouts.Count; layoutIndex++)
        {
            FlatColumnLayout layout = _flatColumnLayouts[layoutIndex];
            DataGridColumn column = layout.Column;
            DataGridCell cell = row.Cells[column.Index];
            double cellLeftEdge = layout.Left;

            if (!cell.IsFlatVisualHidden)
            {
                Rect target = new(_flatHeaderWidth + cellLeftEdge, top, column.LayoutRoundedWidth, height);
                if (!AreClose(cell.Bounds, target) || !cell.IsArrangeValid)
                {
                    cell.Arrange(target);
                }
                EnsureFlatCellClip(
                    cell,
                    column.ActualWidth,
                    height,
                    _flatFrozenLeftWidth,
                    _flatRightFrozenStart,
                    cellLeftEdge);
            }

            if (!column.IsFrozen)
            {
                lastScrollingRightEdge = Math.Max(lastScrollingRightEdge, cellLeftEdge + column.ActualWidth);
            }
            column.IsInitialDesiredWidthDetermined = true;
        }

        if (!grid.UseLightweightFiller && grid.ColumnsInternal.FillerColumn.IsActive)
        {
            DataGridCell fillerCell = row.FillerCell;
            fillerCell.ShowFlatVisual();
            fillerCell.Arrange(new Rect(
                _flatHeaderWidth + lastScrollingRightEdge,
                top,
                grid.ColumnsInternal.FillerColumn.FillerWidth,
                height));
        }
    }

    private void ArrangeVirtualCompatibilityCell(DataGridRow row, double top)
    {
        DataGrid grid = OwningGrid!;
        int columnIndex = grid.CurrentColumnIndex;
        if (!ReferenceEquals(grid.EditingRow, row) || columnIndex < 0 || columnIndex >= row.Cells.Count)
        {
            return;
        }

        FlatColumnLayout layout = FindFlatColumnLayout(grid.ColumnsInternal[columnIndex]);
        DataGridCell cell = row.Cells[columnIndex];
        double height = row.GetFlatCellsHeight();
        cell.Arrange(new Rect(_flatHeaderWidth + layout.Left, top, layout.Column.LayoutRoundedWidth, height));
    }

    private void PrepareFlatColumnLayouts()
    {
        _flatColumnLayouts.Clear();
        DataGrid grid = OwningGrid!;
        double frozenLeftEdge = 0;
        double frozenRightWidth = grid.GetVisibleFrozenColumnsWidthRight();
        double rightFrozenStart = frozenRightWidth > 0
            ? Math.Max(0, grid.CellsWidth - frozenRightWidth)
            : double.PositiveInfinity;
        double rightFrozenEdge = frozenRightWidth > 0 ? rightFrozenStart : 0;
        double scrollingLeftEdge = -grid.HorizontalOffset;
        double layoutLeftEdge = 0;

        foreach (DataGridColumn column in grid.ColumnsInternal.GetVisibleColumns())
        {
            double cellLeftEdge = column.IsFrozenLeft
                ? frozenLeftEdge
                : column.IsFrozenRight
                    ? rightFrozenEdge
                    : scrollingLeftEdge;
            bool shouldDisplay = ShouldDisplayFlatCell(
                grid,
                column,
                frozenLeftEdge,
                scrollingLeftEdge,
                rightFrozenEdge,
                rightFrozenStart);
            column.ComputeLayoutRoundedWidth(layoutLeftEdge);
            double visibleLeftEdge = Math.Max(0, cellLeftEdge);
            double visibleRightEdge = Math.Min(
                grid.CellsWidth,
                cellLeftEdge + column.LayoutRoundedWidth);
            if (!column.IsFrozen)
            {
                visibleLeftEdge = Math.Max(visibleLeftEdge, frozenLeftEdge);
                visibleRightEdge = Math.Min(visibleRightEdge, rightFrozenStart);
            }
            _flatColumnLayouts.Add(new FlatColumnLayout(
                column,
                cellLeftEdge,
                shouldDisplay,
                visibleLeftEdge,
                Math.Max(0, visibleRightEdge - visibleLeftEdge)));

            if (column.IsFrozenLeft)
            {
                frozenLeftEdge += column.ActualWidth;
            }
            else if (column.IsFrozenRight)
            {
                rightFrozenEdge += column.ActualWidth;
            }

            scrollingLeftEdge += column.ActualWidth;
            layoutLeftEdge += column.ActualWidth;
        }

        _flatHeaderWidth = grid.AreRowHeadersVisible ? grid.RowHeadersDesiredWidth : 0d;
        _flatFrozenLeftWidth = frozenLeftEdge;
        _flatRightFrozenStart = rightFrozenStart;
    }

    private static bool ShouldDisplayFlatCell(
        DataGrid grid,
        DataGridColumn column,
        double frozenLeftEdge,
        double scrollingLeftEdge,
        double rightFrozenEdge,
        double rightFrozenStart)
    {
        scrollingLeftEdge += grid.HorizontalAdjustment;
        double leftEdge = column.IsFrozenLeft
            ? frozenLeftEdge
            : column.IsFrozenRight
                ? rightFrozenEdge
                : scrollingLeftEdge;
        double rightEdge = leftEdge + column.ActualWidth;
        if (column.IsFrozen)
        {
            return MathUtilities.GreaterThan(rightEdge, 0) &&
                   MathUtilities.LessThanOrClose(leftEdge, grid.CellsWidth);
        }

        return MathUtilities.GreaterThan(rightEdge, 0) &&
               MathUtilities.LessThanOrClose(leftEdge, grid.CellsWidth) &&
               MathUtilities.GreaterThan(rightEdge, frozenLeftEdge) &&
               MathUtilities.LessThan(leftEdge, rightFrozenStart);
    }

    private static void EnsureFlatCellDisplay(DataGridCell cell, bool display)
    {
        if (display)
        {
            cell.ShowFlatVisual();
        }
        else
        {
            cell.HideFlatVisual();
        }
    }

    private static void EnsureFlatCellClip(
        DataGridCell cell,
        double width,
        double height,
        double frozenLeftWidth,
        double rightFrozenStart,
        double cellLeftEdge)
    {
        if (cell.OwningColumn.IsFrozen)
        {
            if (cell.Clip != null)
            {
                cell.Clip = null;
            }
            return;
        }

        double leftClip = Math.Max(0, frozenLeftWidth - cellLeftEdge);
        double rightClip = rightFrozenStart < double.PositiveInfinity
            ? Math.Max(0, cellLeftEdge + width - rightFrozenStart)
            : 0;
        if (leftClip <= 0 && rightClip <= 0)
        {
            if (cell.Clip != null)
            {
                cell.Clip = null;
            }
            return;
        }

        var clipRect = new Rect(leftClip, 0, Math.Max(0, width - leftClip - rightClip), height);
        if (cell.Clip is RectangleGeometry clip)
        {
            if (!AreClose(clip.Rect, clipRect))
            {
                clip.Rect = clipRect;
            }
        }
        else
        {
            cell.Clip = new RectangleGeometry { Rect = clipRect };
        }
    }

    private readonly record struct FlatColumnLayout(
        DataGridColumn Column,
        double Left,
        bool ShouldDisplay,
        double VisibleLeft,
        double VisibleWidth);
}
