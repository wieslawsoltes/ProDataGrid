// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    readonly struct DataGridCellRange : IEquatable<DataGridCellRange>
    {
        public DataGridCellRange(int startRow, int endRow, int startColumn, int endColumn)
        {
            StartRow = startRow;
            EndRow = endRow;
            StartColumn = startColumn;
            EndColumn = endColumn;
        }

        public int StartRow { get; }

        public int EndRow { get; }

        public int StartColumn { get; }

        public int EndColumn { get; }

        public int RowCount => EndRow - StartRow + 1;

        public int ColumnCount => EndColumn - StartColumn + 1;

        public bool Contains(int rowIndex, int columnIndex)
        {
            return rowIndex >= StartRow && rowIndex <= EndRow
                && columnIndex >= StartColumn && columnIndex <= EndColumn;
        }

        public bool Equals(DataGridCellRange other)
        {
            return StartRow == other.StartRow
                && EndRow == other.EndRow
                && StartColumn == other.StartColumn
                && EndColumn == other.EndColumn;
        }

        public override bool Equals(object obj)
        {
            return obj is DataGridCellRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StartRow;
                hash = (hash * 397) ^ EndRow;
                hash = (hash * 397) ^ StartColumn;
                hash = (hash * 397) ^ EndColumn;
                return hash;
            }
        }

        public static bool operator ==(DataGridCellRange left, DataGridCellRange right) => left.Equals(right);

        public static bool operator !=(DataGridCellRange left, DataGridCellRange right) => !left.Equals(right);
    }
}
