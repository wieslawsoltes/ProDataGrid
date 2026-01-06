// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.DataGridFilling;
using System.Globalization;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private void ApplyFillModel(DataGridCellRange source, DataGridCellRange target)
        {
            if (DataConnection == null || IsReadOnly || source == target)
            {
                return;
            }

            FillModel?.ApplyFill(new DataGridFillContext(this, source, target));
        }

        internal bool TryGetFillCellText(int rowIndex, int columnIndex, out string text)
        {
            text = string.Empty;

            if (!TryGetFillCellValue(rowIndex, columnIndex, out var value))
            {
                return false;
            }

            var converted = DataGridValueConverter.Instance.Convert(value, typeof(string), null, CultureInfo.CurrentCulture);
            text = converted?.ToString() ?? string.Empty;
            return true;
        }

        internal bool TryGetFillCellValue(int rowIndex, int columnIndex, out object value)
        {
            value = null;

            if (DataConnection == null || rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return false;
            }

            if (columnIndex < 0 || columnIndex >= ColumnsItemsInternal.Count)
            {
                return false;
            }

            var column = ColumnsItemsInternal[columnIndex];
            if (column == null)
            {
                return false;
            }

            var item = DataConnection.GetDataItem(rowIndex);
            if (item == null || ReferenceEquals(item, global::Avalonia.Collections.DataGridCollectionView.NewItemPlaceholder))
            {
                return false;
            }

            var binding = column.ClipboardContentBinding;
            if (binding == null && column is DataGridBoundColumn boundColumn)
            {
                binding = boundColumn.Binding;
            }

            if (binding == null)
            {
                return false;
            }

            value = column.GetCellValue(item, binding);
            return true;
        }

        internal bool TrySetFillCellText(int rowIndex, int columnIndex, string text)
        {
            if (DataConnection == null || rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return false;
            }

            var item = DataConnection.GetDataItem(rowIndex);
            if (item == null || ReferenceEquals(item, global::Avalonia.Collections.DataGridCollectionView.NewItemPlaceholder))
            {
                return false;
            }

            return TrySetFillCellText(item, columnIndex, text);
        }

        internal bool TrySetFillCellText(object item, int columnIndex, string text)
        {
            if (item == null)
            {
                return false;
            }

            return TrySetCellValue(item, columnIndex, text);
        }
    }
}
