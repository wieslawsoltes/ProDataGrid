// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#endif
    readonly struct DataGridCellInfo : IEquatable<DataGridCellInfo>
    {
        public DataGridCellInfo(object? item, DataGridColumn column, int rowIndex, int columnIndex, bool isValid = true)
        {
            Item = item;
            Column = column;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            IsValid = isValid && column != null && rowIndex >= 0 && columnIndex >= 0;
        }

        public object? Item { get; }

        public DataGridColumn Column { get; }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public bool IsValid { get; }

        public static DataGridCellInfo Unset { get; } = default;

        public bool Equals(DataGridCellInfo other)
        {
            return ReferenceEquals(Column, other.Column) &&
                   Equals(Item, other.Item);
        }

        public override bool Equals(object? obj) =>
            obj is DataGridCellInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Item?.GetHashCode() ?? 0) * 397) ^ (Column?.GetHashCode() ?? 0);
            }
        }

        public static bool operator ==(DataGridCellInfo left, DataGridCellInfo right) => left.Equals(right);

        public static bool operator !=(DataGridCellInfo left, DataGridCellInfo right) => !left.Equals(right);
    }
}
