using System;
using Avalonia.Controls.DataGridFilling;

namespace DataGridSample.FillModels
{
    public sealed class CopyOnlyFillModel : IDataGridFillModel
    {
        public void ApplyFill(DataGridFillContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var source = context.SourceRange;
            var target = context.TargetRange;
            var rowCount = source.RowCount;
            var colCount = source.ColumnCount;

            if (rowCount <= 0 || colCount <= 0)
            {
                return;
            }

            for (var rowIndex = target.StartRow; rowIndex <= target.EndRow; rowIndex++)
            {
                using var scope = context.BeginRowEdit(rowIndex, out var item);
                if (item == null)
                {
                    continue;
                }

                for (var columnIndex = target.StartColumn; columnIndex <= target.EndColumn; columnIndex++)
                {
                    if (source.Contains(rowIndex, columnIndex))
                    {
                        continue;
                    }

                    var sourceRow = source.StartRow + Mod(rowIndex - source.StartRow, rowCount);
                    var sourceColumn = source.StartColumn + Mod(columnIndex - source.StartColumn, colCount);

                    if (context.TryGetCellText(sourceRow, sourceColumn, out var text))
                    {
                        context.TrySetCellText(item, columnIndex, text);
                    }
                }
            }
        }

        private static int Mod(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
