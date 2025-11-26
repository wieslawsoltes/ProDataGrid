// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Linq;
using System.Diagnostics;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {

                private void SelectDisplayedElement(int slot)
                {
                    Debug.Assert(IsSlotVisible(slot));
                    Control element = DisplayData.GetDisplayedElement(slot);
                    if (element is DataGridRow row)
                    {
                        row.ApplyState();
                        EnsureRowDetailsVisibility(row, raiseNotification: true, animate: true);
                    }
                    else
                    {
                        // Assume it's a RowGroupHeader
                        DataGridRowGroupHeader groupHeader = element as DataGridRowGroupHeader;
                        groupHeader.UpdatePseudoClasses();
                    }
                }



                private void SelectSlot(int slot, bool isSelected)
                {
                    _selectedItems.SelectSlot(slot, isSelected);
                    if (IsSlotVisible(slot))
                    {
                        SelectDisplayedElement(slot);
                    }
                }



                private void SelectSlots(int startSlot, int endSlot, bool isSelected)
                {
                    _selectedItems.SelectSlots(startSlot, endSlot, isSelected);

                    // Apply the correct row state for display rows and also expand or collapse detail accordingly
                    int firstSlot = Math.Max(DisplayData.FirstScrollingSlot, startSlot);
                    int lastSlot = Math.Min(DisplayData.LastScrollingSlot, endSlot);

                    for (int slot = firstSlot; slot <= lastSlot; slot++)
                    {
                        if (IsSlotVisible(slot))
                        {
                            SelectDisplayedElement(slot);
                        }
                    }
                }



                /// <summary>
                /// Clears the entire selection. Displayed rows are deselected explicitly to visualize
                /// potential transition effects
                /// </summary>
                internal void ClearRowSelection(bool resetAnchorSlot)
                {
                    if (resetAnchorSlot)
                    {
                        AnchorSlot = -1;
                    }
                    if (_selectedItems.Count > 0)
                    {
                        _noSelectionChangeCount++;
                        try
                        {
                            // Individually deselecting displayed rows to view potential transitions
                            for (int slot = DisplayData.FirstScrollingSlot;
                                 slot > -1 && slot <= DisplayData.LastScrollingSlot;
                                 slot++)
                            {
                                if (DisplayData.GetDisplayedElement(slot) is DataGridRow row)
                                {
                                    if (_selectedItems.ContainsSlot(row.Slot))
                                    {
                                        SelectSlot(row.Slot, false);
                                    }
                                }
                            }
                            _selectedItems.ClearRows();
                            SelectionHasChanged = true;
                        }
                        finally
                        {
                            NoSelectionChangeCount--;
                        }
                    }
                }



                internal int GetCollapsedSlotCount(int startSlot, int endSlot)
                {
                    return _collapsedSlotsTable.GetIndexCount(startSlot, endSlot);
                }



                internal bool GetRowSelection(int slot)
                {
                    Debug.Assert(slot != -1);
                    return _selectedItems.ContainsSlot(slot);
                }



                internal void SetRowSelection(int slot, bool isSelected, bool setAnchorSlot)
                {
                    Debug.Assert(!(!isSelected && setAnchorSlot));
                    Debug.Assert(!IsSlotOutOfSelectionBounds(slot));
                    _noSelectionChangeCount++;
                    try
                    {
                        if (SelectionMode == DataGridSelectionMode.Single && isSelected)
                        {
                            Debug.Assert(_selectedItems.Count <= 1);
                            if (_selectedItems.Count > 0)
                            {
                                int currentlySelectedSlot = _selectedItems.GetIndexes().First();
                                if (currentlySelectedSlot != slot)
                                {
                                    SelectSlot(currentlySelectedSlot, false);
                                    SelectionHasChanged = true;
                                }
                            }
                        }
                        if (_selectedItems.ContainsSlot(slot) != isSelected)
                        {
                            SelectSlot(slot, isSelected);
                            SelectionHasChanged = true;
                        }
                        if (setAnchorSlot)
                        {
                            AnchorSlot = slot;
                        }
                    }
                    finally
                    {
                        NoSelectionChangeCount--;
                    }
                }


    }
}
