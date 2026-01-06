// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System.Diagnostics;
using System;
using System.Linq;
using Avalonia.Collections;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        private bool ProcessAKey()
        {
            if (SelectionMode != DataGridSelectionMode.Extended)
            {
                return false;
            }

            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                SelectAll();
            }
            else
            {
                SelectAllCells();
            }
            return true;
        }

        private bool ProcessTabKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessTabKey(e, shift, ctrl, allowCtrl: false);
        }

        internal bool ProcessDownKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessDownKeyInternal(shift, ctrl);
        }

        private bool ProcessDownKeyInternal(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int lastSlot = LastVisibleSlot;
            if (firstVisibleColumnIndex == -1 || lastSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessDownKeyInternal(shift, ctrl)))
            {
                return true;
            }

            int nextSlot = -1;
            if (CurrentSlot != -1)
            {
                nextSlot = GetNextVisibleSlot(CurrentSlot);
                if (nextSlot >= SlotCount)
                {
                    nextSlot = -1;
                }
            }

            _noSelectionChangeCount++;
            try
            {
                int desiredSlot;
                int columnIndex;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    desiredSlot = FirstVisibleSlot;
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else if (ctrl)
                {
                    if (shift)
                    {
                        // Both Ctrl and Shift
                        desiredSlot = lastSlot;
                        columnIndex = CurrentColumnIndex;
                        action = (SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;
                    }
                    else
                    {
                        // Ctrl without Shift
                        desiredSlot = lastSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }
                else
                {
                    if (nextSlot == -1)
                    {
                        return true;
                    }
                    if (shift)
                    {
                        // Shift without Ctrl
                        desiredSlot = nextSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectFromAnchorToCurrent;
                    }
                    else
                    {
                        // Neither Ctrl nor Shift
                        desiredSlot = nextSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }

                UpdateSelectionAndCurrency(columnIndex, desiredSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }

        internal bool ProcessUpKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessUpKey(shift, ctrl);
        }

        internal bool ProcessLeftKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift, out bool alt);
            return ProcessLeftKey(shift, ctrl, alt);
        }

        internal bool ProcessRightKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift, out bool alt);
            return ProcessRightKey(shift, ctrl, alt);
        }

        internal bool ProcessHomeKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessHomeKey(shift, ctrl);
        }

        internal bool ProcessEndKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessEndKey(shift, ctrl);
        }

        internal bool ProcessEnterKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessEnterKey(shift, ctrl);
        }

        private bool ProcessEscapeKey()
        {
            if (WaitForLostFocus(() => ProcessEscapeKey()))
            {
                return true;
            }

            if (_editingColumnIndex != -1)
            {
                // Revert the potential cell editing and exit cell editing.
                EndCellEdit(DataGridEditAction.Cancel, exitEditingMode: true, keepFocus: true, raiseEvents: true);
                return true;
            }
            else if (EditingRow != null)
            {
                // Revert the potential row editing and exit row editing.
                EndRowEdit(DataGridEditAction.Cancel, exitEditingMode: true, raiseEvents: true);
                return true;
            }
            return false;
        }

        internal bool ProcessNextKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessNextKey(shift, ctrl);
        }

        internal bool ProcessPriorKey(KeyEventArgs e)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessPriorKey(shift, ctrl);
        }

        // Ctrl Left <==> Home
        private bool ProcessLeftMost(int firstVisibleColumnIndex, int firstVisibleSlot)
        {
            _noSelectionChangeCount++;
            try
            {
                int desiredSlot;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    desiredSlot = firstVisibleSlot;
                    action = DataGridSelectionAction.SelectCurrent;
                    Debug.Assert(_selectedItems.Count == 0);
                }
                else
                {
                    desiredSlot = CurrentSlot;
                    action = DataGridSelectionAction.None;
                }

                UpdateSelectionAndCurrency(firstVisibleColumnIndex, desiredSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }

        // Ctrl Right <==> End
        private bool ProcessRightMost(int lastVisibleColumnIndex, int firstVisibleSlot)
        {
            _noSelectionChangeCount++;
            try
            {
                int desiredSlot;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    desiredSlot = firstVisibleSlot;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    desiredSlot = CurrentSlot;
                    action = DataGridSelectionAction.None;
                }

                UpdateSelectionAndCurrency(lastVisibleColumnIndex, desiredSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }

        private bool ProcessDeleteKey()
        {
            if (!DataConnection.CanRemove)
            {
                return false;
            }

            object[] toRemove = _selectedItems
                .OfType<object>()
                .Where(item => item != DataGridCollectionView.NewItemPlaceholder)
                .ToArray();

            if (toRemove.Length == 0)
            {
                return false;
            }

            if (!CommitEdit())
            {
                return false;
            }

            foreach (object item in toRemove)
            {
                DataConnection.Remove(item);
            }

            return true;
        }

        private bool ProcessF2Key(KeyEventArgs e)
        {
            if (!EditTriggers.HasFlag(DataGridEditTriggers.F2))
            {
                return false;
            }

            if (_editingColumnIndex == -1 && CurrentColumnIndex != -1 && GetRowSelection(CurrentSlot) &&
                !GetColumnEffectiveReadOnlyState(CurrentColumn))
            {
                if (ScrollSlotIntoView(CurrentColumnIndex, CurrentSlot, forCurrentCellChange: false, forceHorizontalScroll: true))
                {
                    BeginCellEdit(e);
                }
                return true;
            }

            return false;
        }

    }
}
