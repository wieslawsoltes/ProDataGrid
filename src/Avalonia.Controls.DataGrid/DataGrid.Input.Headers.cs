// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Input;
using System;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private void DataGrid_ColumnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Handled || SelectionUnit == DataGridSelectionUnit.FullRow || !CanUserSelectColumns)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.Source is not DataGridColumnHeader header || header.OwningGrid != this)
            {
                return;
            }

            if (header.IsResizing)
            {
                return;
            }

            var column = header.OwningColumn;
            if (column == null || !column.IsVisible || DataConnection == null || DataConnection.Count == 0)
            {
                return;
            }

            var anchorSlot = SlotFromRowIndex(0);
            if (EditingRow != null && anchorSlot != EditingRow.Slot && !CommitEdit(DataGridEditingUnit.Row, true))
            {
                return;
            }

            if (SuppressSortOnColumnHeaderSelection)
            {
                header.SuppressSortOnClick();
            }

            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out var ctrl, out _);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (SelectionMode == DataGridSelectionMode.Single)
            {
                ctrl = false;
                shift = false;
            }

            var startCol = column.Index;
            var endCol = column.Index;

            if (SelectionMode == DataGridSelectionMode.Extended && shift && _columnHeaderAnchorIndex >= 0)
            {
                startCol = Math.Min(_columnHeaderAnchorIndex, column.Index);
                endCol = Math.Max(_columnHeaderAnchorIndex, column.Index);
            }
            else if (!shift)
            {
                _columnHeaderAnchorIndex = column.Index;
            }

            var range = new DataGridCellRange(0, DataConnection.Count - 1, startCol, endCol);
            var append = SelectionMode == DataGridSelectionMode.Extended && ctrl;

            if (ApplyCellSelectionRange(range, append, DataGridSelectionChangeSource.Pointer, e))
            {
                if (anchorSlot >= 0 && !IsGroupSlot(anchorSlot))
                {
                    _cellAnchor = new DataGridCellCoordinates(startCol, anchorSlot);
                    SetCurrentCellCore(startCol, anchorSlot, commitEdit: true, endRowEdit: false);
                }
            }

            e.Handled = true;
        }

        private void TopLeftCornerHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                SelectAll();
            }
            else
            {
                SelectAllCells();
            }

            e.Handled = true;
        }
    }
}
