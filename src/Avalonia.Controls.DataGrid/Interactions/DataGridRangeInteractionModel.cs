// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia;
using Avalonia.Input;

namespace Avalonia.Controls.DataGridInteractions
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridCellPosition : IEquatable<DataGridCellPosition>
    {
        public DataGridCellPosition(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public bool Equals(DataGridCellPosition other)
        {
            return RowIndex == other.RowIndex && ColumnIndex == other.ColumnIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is DataGridCellPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RowIndex * 397) ^ ColumnIndex;
            }
        }

        public static bool operator ==(DataGridCellPosition left, DataGridCellPosition right) => left.Equals(right);

        public static bool operator !=(DataGridCellPosition left, DataGridCellPosition right) => !left.Equals(right);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridAutoScrollDirection
    {
        public DataGridAutoScrollDirection(int horizontal, int vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        public int Horizontal { get; }

        public int Vertical { get; }

        public bool HasScroll => Horizontal != 0 || Vertical != 0;
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridSelectionAnchorContext
    {
        public DataGridSelectionAnchorContext(
            DataGrid grid,
            DataGridCellPosition? existingAnchor,
            DataGridCellPosition currentCell,
            KeyModifiers modifiers)
        {
            Grid = grid;
            ExistingAnchor = existingAnchor;
            CurrentCell = currentCell;
            Modifiers = modifiers;
        }

        public DataGrid Grid { get; }

        public DataGridCellPosition? ExistingAnchor { get; }

        public DataGridCellPosition CurrentCell { get; }

        public KeyModifiers Modifiers { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridSelectionRangeContext
    {
        public DataGridSelectionRangeContext(
            DataGrid grid,
            DataGridCellPosition anchor,
            DataGridCellPosition target,
            KeyModifiers modifiers)
        {
            Grid = grid;
            Anchor = anchor;
            Target = target;
            Modifiers = modifiers;
        }

        public DataGrid Grid { get; }

        public DataGridCellPosition Anchor { get; }

        public DataGridCellPosition Target { get; }

        public KeyModifiers Modifiers { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridFillHandleRangeContext
    {
        public DataGridFillHandleRangeContext(
            DataGrid grid,
            DataGridCellRange sourceRange,
            DataGridCellPosition targetCell)
        {
            Grid = grid;
            SourceRange = sourceRange;
            TargetCell = targetCell;
        }

        public DataGrid Grid { get; }

        public DataGridCellRange SourceRange { get; }

        public DataGridCellPosition TargetCell { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridAutoScrollContext
    {
        public DataGridAutoScrollContext(
            DataGrid grid,
            Point pointerPosition,
            Point? rowsPresenterPoint,
            Size rowsPresenterSize,
            double rowHeaderWidth,
            double cellsWidth,
            bool isRowSelection)
        {
            Grid = grid;
            PointerPosition = pointerPosition;
            RowsPresenterPoint = rowsPresenterPoint;
            RowsPresenterSize = rowsPresenterSize;
            RowHeaderWidth = rowHeaderWidth;
            CellsWidth = cellsWidth;
            IsRowSelection = isRowSelection;
        }

        public DataGrid Grid { get; }

        public Point PointerPosition { get; }

        public Point? RowsPresenterPoint { get; }

        public Size RowsPresenterSize { get; }

        public double RowHeaderWidth { get; }

        public double CellsWidth { get; }

        public bool IsRowSelection { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRangeInteractionModel
    {
        bool IsSelectionDragThresholdMet(Point start, Point current);

        DataGridCellPosition ResolveSelectionAnchor(DataGridSelectionAnchorContext context);

        DataGridCellRange BuildSelectionRange(DataGridSelectionRangeContext context);

        DataGridCellRange BuildFillHandleRange(DataGridFillHandleRangeContext context);

        DataGridAutoScrollDirection GetAutoScrollDirection(DataGridAutoScrollContext context);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRangeInteractionModelFactory
    {
        IDataGridRangeInteractionModel Create();
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridRangeInteractionModel : IDataGridRangeInteractionModel
    {
        protected virtual double DragSelectionThreshold => 4;

        public virtual bool IsSelectionDragThresholdMet(Point start, Point current)
        {
            var delta = current - start;
            var threshold = DragSelectionThreshold;
            return Math.Abs(delta.X) >= threshold || Math.Abs(delta.Y) >= threshold;
        }

        public virtual DataGridCellPosition ResolveSelectionAnchor(DataGridSelectionAnchorContext context)
        {
            if (context.ExistingAnchor.HasValue)
            {
                var anchor = context.ExistingAnchor.Value;
                if (anchor.RowIndex >= 0 && anchor.ColumnIndex >= 0)
                {
                    return anchor;
                }
            }

            return context.CurrentCell;
        }

        public virtual DataGridCellRange BuildSelectionRange(DataGridSelectionRangeContext context)
        {
            return BuildRange(context.Anchor, context.Target);
        }

        public virtual DataGridCellRange BuildFillHandleRange(DataGridFillHandleRangeContext context)
        {
            var source = context.SourceRange;
            var target = context.TargetCell;

            var startRow = source.StartRow;
            var endRow = source.EndRow;
            if (target.RowIndex >= source.EndRow)
            {
                endRow = Math.Max(source.EndRow, target.RowIndex);
            }
            else
            {
                startRow = target.RowIndex;
            }

            var startColumn = source.StartColumn;
            var endColumn = source.EndColumn;
            if (target.ColumnIndex >= source.EndColumn)
            {
                endColumn = Math.Max(source.EndColumn, target.ColumnIndex);
            }
            else
            {
                startColumn = target.ColumnIndex;
            }

            return new DataGridCellRange(startRow, endRow, startColumn, endColumn);
        }

        public virtual DataGridAutoScrollDirection GetAutoScrollDirection(DataGridAutoScrollContext context)
        {
            var verticalDirection = 0;
            var horizontalDirection = 0;

            if (context.RowsPresenterPoint.HasValue)
            {
                var presenterPoint = context.RowsPresenterPoint.Value;
                var presenterHeight = context.RowsPresenterSize.Height;
                if (presenterPoint.Y < 0)
                {
                    verticalDirection = -1;
                }
                else if (presenterPoint.Y > presenterHeight)
                {
                    verticalDirection = 1;
                }
            }
            else
            {
                var gridBounds = context.Grid.Bounds;
                if (context.PointerPosition.Y < 0)
                {
                    verticalDirection = -1;
                }
                else if (context.PointerPosition.Y > gridBounds.Height)
                {
                    verticalDirection = 1;
                }
            }

            if (!context.IsRowSelection)
            {
                var x = context.PointerPosition.X - context.RowHeaderWidth;
                if (x < 0)
                {
                    horizontalDirection = -1;
                }
                else if (x > context.CellsWidth)
                {
                    horizontalDirection = 1;
                }
            }

            return new DataGridAutoScrollDirection(horizontalDirection, verticalDirection);
        }

        protected static DataGridCellRange BuildRange(DataGridCellPosition anchor, DataGridCellPosition target)
        {
            var startRow = Math.Min(anchor.RowIndex, target.RowIndex);
            var endRow = Math.Max(anchor.RowIndex, target.RowIndex);
            var startColumn = Math.Min(anchor.ColumnIndex, target.ColumnIndex);
            var endColumn = Math.Max(anchor.ColumnIndex, target.ColumnIndex);
            return new DataGridCellRange(startRow, endRow, startColumn, endColumn);
        }
    }
}
