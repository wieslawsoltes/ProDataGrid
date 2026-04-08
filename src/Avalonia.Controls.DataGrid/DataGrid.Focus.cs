// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Focus handling
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {

        //TODO: Make override?
        private void DataGrid_GotFocus(object sender, FocusChangedEventArgs e)
        {
            DetachExternalEditingElement();
            if (!ContainsFocus)
            {
                ContainsFocus = true;
                ApplyDisplayedRowsState(DisplayData.FirstScrollingSlot, DisplayData.LastScrollingSlot);
                if (CurrentColumnIndex != -1 && IsSlotVisible(CurrentSlot))
                {
                    if (DisplayData.GetDisplayedElement(CurrentSlot) is DataGridRow row)
                    {
                        row.Cells[CurrentColumnIndex].UpdatePseudoClasses();
                    }
                }
            }

            // Keep track of which row contains the newly focused element
            DataGridRow focusedRow = null;
            Visual focusedElement = e.Source as Visual;
            _focusedObject = focusedElement;
            while (focusedElement != null)
            {
                focusedRow = focusedElement as DataGridRow;
                if (focusedRow != null && focusedRow.OwningGrid == this && _focusedRow != focusedRow)
                {
                    ResetFocusedRow();
                    _focusedRow = focusedRow.IsVisible ? focusedRow : null;
                    break;
                }
                focusedElement = focusedElement.GetVisualParent();
            }
        }


        //TODO: Make override?
        private void DataGrid_LostFocus(object sender, FocusChangedEventArgs e)
        {
            var previousFocusedObject = _focusedObject as Visual;
            _focusedObject = null;
            DetachExternalEditingElement();
            if (ContainsFocus)
            {
                var focusManager = FocusManager.GetFocusManager(this);
                Visual focusedObject = e.NewFocusedElement as Visual ?? focusManager?.GetFocusedElement() as Visual;
                var newFocusedObject = focusedObject;
                var focusLeftDataGrid = !IsFocusWithinDataGrid(focusedObject, out var dataGridWillReceiveRoutedEvent);
                DataGridColumn editingColumn = null;

                if (focusLeftDataGrid
                    && previousFocusedObject != null
                    && (!previousFocusedObject.IsAttachedToVisualTree || !previousFocusedObject.IsEffectivelyVisible)
                    && (newFocusedObject == null
                        || !newFocusedObject.IsAttachedToVisualTree
                        || !newFocusedObject.IsEffectivelyVisible))
                {
                    RequestFocusAfterRowRecycle();
                }

                if (EditingRow != null && EditingColumnIndex != -1)
                {
                    editingColumn = ColumnsItemsInternal[EditingColumnIndex];

                    if (focusLeftDataGrid && editingColumn is DataGridTemplateColumn)
                    {
                        dataGridWillReceiveRoutedEvent = false;
                    }
                }

                if (focusLeftDataGrid && !(editingColumn is DataGridTemplateColumn))
                {
                    ContainsFocus = false;
                    if (EditingRow != null)
                    {
                        CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
                    }
                    ResetFocusedRow();
                    ApplyDisplayedRowsState(DisplayData.FirstScrollingSlot, DisplayData.LastScrollingSlot);
                    if (CurrentColumnIndex != -1 && IsSlotVisible(CurrentSlot))
                    {
                        if (DisplayData.GetDisplayedElement(CurrentSlot) is DataGridRow row)
                        {
                            row.Cells[CurrentColumnIndex].UpdatePseudoClasses();
                        }
                    }
                }
                else if (!dataGridWillReceiveRoutedEvent)
                {
                    if (newFocusedObject is Control focusedElement)
                    {
                        if (!ReferenceEquals(_externalEditingElement, focusedElement))
                        {
                            DetachExternalEditingElement();
                        }
                        _externalEditingElement = focusedElement;
                        focusedElement.LostFocus += ExternalEditingElement_LostFocus;
                    }
                }
            }
        }


        private void ExternalEditingElement_LostFocus(object sender, FocusChangedEventArgs e)
        {
            if (sender is Control element)
            {
                element.LostFocus -= ExternalEditingElement_LostFocus;
                if (ReferenceEquals(_externalEditingElement, element))
                {
                    _externalEditingElement = null;
                }
                DataGrid_LostFocus(sender, e);
            }
        }

        private void DataGrid_DescendantLostFocus(object sender, FocusChangedEventArgs e)
        {
            if (e.Source is not Visual source || !this.ContainsChild(source))
            {
                return;
            }

            var focusManager = FocusManager.GetFocusManager(this);
            var focusedElement = e.NewFocusedElement as Visual ?? focusManager?.GetFocusedElement() as Visual;
            if (focusedElement != null && focusedElement.IsAttachedToVisualTree)
            {
                return;
            }

            RequestFocusAfterRowRecycle();
        }


        private void MakeFirstDisplayedCellCurrentCell()
        {
            if (CurrentColumnIndex != -1)
            {
                _makeFirstDisplayedCellCurrentCellPending = false;
                _desiredCurrentColumnIndex = -1;
                FlushCurrentCellChanged();
                return;
            }
            if (SlotCount != SlotFromRowIndex(DataConnection.Count))
            {
                _makeFirstDisplayedCellCurrentCellPending = true;
                return;
            }

            // No current cell, therefore no selection either - try to set the current cell to the
            // ItemsSource's ICollectionView.CurrentItem if it exists, otherwise use the first displayed cell.
            int slot = 0;
            if (DataConnection.CollectionView != null)
            {
                if (DataConnection.CollectionView.IsCurrentBeforeFirst ||
                    DataConnection.CollectionView.IsCurrentAfterLast)
                {
                    slot = IsGroupSlot(0) ? 0 : -1;
                }
                else
                {
                    slot = SlotFromRowIndex(DataConnection.CollectionView.CurrentPosition);
                }
            }
            else
            {
                if (SelectedIndex == -1)
                {
                    // Try to default to the first row
                    slot = SlotFromRowIndex(0);
                    if (!IsSlotVisible(slot))
                    {
                        slot = -1;
                    }
                }
                else
                {
                    slot = SlotFromRowIndex(SelectedIndex);
                }
            }
            int columnIndex = FirstDisplayedNonFillerColumnIndex;
            if (_desiredCurrentColumnIndex >= 0 && _desiredCurrentColumnIndex < ColumnsItemsInternal.Count)
            {
                columnIndex = _desiredCurrentColumnIndex;
            }

            SetAndSelectCurrentCell(columnIndex, slot, forceCurrentCellSelection: false);
            AnchorSlot = slot;
            _makeFirstDisplayedCellCurrentCellPending = false;
            _desiredCurrentColumnIndex = -1;
            FlushCurrentCellChanged();
        }


        private void ResetFocusedRow()
        {
            if (_focusedRow != null
                && _focusedRow != EditingRow
                && !IsSlotVisible(_focusedRow.Slot))
            {
                // Unload the old focused row if it's off screen
                _focusedRow.Clip = null;
                UnloadRow(_focusedRow);
            }
            _focusedRow = null;
        }

        private void DetachExternalEditingElement()
        {
            if (_externalEditingElement != null)
            {
                _externalEditingElement.LostFocus -= ExternalEditingElement_LostFocus;
                _externalEditingElement = null;
            }
        }

        internal bool IsFocusWithinDataGrid(Visual focusedObject, out bool dataGridWillReceiveRoutedEvent)
        {
            dataGridWillReceiveRoutedEvent = true;

            if (focusedObject == null)
            {
                return false;
            }

            if (ReferenceEquals(focusedObject, this) || this.IsVisualAncestorOf(focusedObject))
            {
                return true;
            }

            if (focusedObject is not StyledElement styledElement)
            {
                return false;
            }

            // Avalonia 12 exposes a richer logical-parent chain; guard against popup/template cycles
            // while still treating logically-parented editors as part of the grid focus scope.
            var visited = new HashSet<StyledElement>();
            var logicalAncestor = styledElement.Parent;

            while (logicalAncestor != null && visited.Add(logicalAncestor))
            {
                if (ReferenceEquals(logicalAncestor, this) ||
                    logicalAncestor is Visual logicalVisual && this.IsVisualAncestorOf(logicalVisual))
                {
                    dataGridWillReceiveRoutedEvent = false;
                    return true;
                }

                logicalAncestor = logicalAncestor.Parent;
            }

            return false;
        }

        private Visual _focusedObject;

        private DataGridRow _focusedRow;

        private Control _externalEditingElement;

        private Queue<Action> _lostFocusActions;

        private bool _executingLostFocusActions;

        private bool _focusRestoreScheduled;


        internal bool ContainsFocus
        {
            get;
            private set;
        }

    }
}
