using System;
using Avalonia.Controls;
using Avalonia.Controls.DataGridInteractions;

namespace DataGridSample.InteractionModels
{
    public sealed class TopLeftRangeInteractionModel : DataGridRangeInteractionModel
    {
        protected override double DragSelectionThreshold => 8;

        public override DataGridCellRange BuildFillHandleRange(DataGridFillHandleRangeContext context)
        {
            var source = context.SourceRange;
            var target = context.TargetCell;

            var startRow = Math.Min(source.StartRow, target.RowIndex);
            var endRow = Math.Max(source.StartRow, target.RowIndex);
            var startColumn = Math.Min(source.StartColumn, target.ColumnIndex);
            var endColumn = Math.Max(source.StartColumn, target.ColumnIndex);

            return new DataGridCellRange(startRow, endRow, startColumn, endColumn);
        }
    }
}
