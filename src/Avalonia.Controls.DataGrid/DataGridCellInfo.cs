// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    readonly struct DataGridCellInfo : IEquatable<DataGridCellInfo>
    {
        private readonly int _rowIndex;
        private readonly bool _tracksItem;

        public DataGridCellInfo(object? item, DataGridColumn column, int rowIndex, int columnIndex, bool isValid = true)
            : this(item, column, rowIndex, columnIndex, isValid, tracksItem: false)
        {
        }

        public DataGridCellInfo(object? item, DataGridColumn column, int rowIndex, int columnIndex, bool isValid, bool tracksItem)
        {
            Item = item;
            Column = column;
            _rowIndex = rowIndex;
            ColumnIndex = columnIndex;
            IsValid = isValid && column != null && rowIndex >= 0 && columnIndex >= 0;
            _tracksItem = tracksItem && IsValid;
        }

        public object? Item { get; }

        public DataGridColumn Column { get; }

        public int RowIndex => _tracksItem && Column.OwningGrid is { } grid ? grid.GetTrackedRowIndex(Item, _rowIndex) : _rowIndex;

        public int ColumnIndex { get; }

        public bool IsValid { get; }

        public static DataGridCellInfo Unset { get; } = default;

        internal DataGridCellInfo TrackingItem() =>
            _tracksItem || !IsValid ? this : new DataGridCellInfo(Item, Column, _rowIndex, ColumnIndex, true, true);

        internal DataGridCellInfo AtCurrentRow() =>
            _tracksItem ? new DataGridCellInfo(Item, Column, RowIndex, ColumnIndex, true) : this;

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
