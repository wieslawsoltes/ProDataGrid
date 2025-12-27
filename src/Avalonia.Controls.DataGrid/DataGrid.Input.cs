// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Keyboard and pointer input handling
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="CellPointerPressed"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridCellPointerPressedEventArgs> CellPointerPressedEvent =
            RoutedEvent.Register<DataGrid, DataGridCellPointerPressedEventArgs>(nameof(CellPointerPressed), RoutingStrategies.Bubble);

        private IDisposable _keyDownRouteFinishedSubscription;
        private IDisposable _keyUpRouteFinishedSubscription;
        private DataGridKeyboardGestures _defaultKeyboardGestures;

        //TODO TabStop
        //TODO FlowDirection
        private bool ProcessDataGridKey(KeyEventArgs e)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Keyboard, e);

            var overrides = KeyboardGestureOverrides;
            var defaults = GetDefaultKeyboardGestures();

            var tabGesture = ResolveGesture(overrides?.Tab, defaults.Tab);
            var beginEditGesture = ResolveGesture(overrides?.BeginEdit, defaults.BeginEdit);
            if (MatchesGesture(tabGesture, e, allowAdditionalModifiers: true))
            {
                return ProcessTabKey(e, allowCtrl: AllowsCtrlModifier(tabGesture));
            }

            bool focusDataGrid = false;

            if (MatchesGesture(ResolveGesture(overrides?.MoveUp, defaults.MoveUp), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessUpKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MoveDown, defaults.MoveDown), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessDownKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MovePageDown, defaults.MovePageDown), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessNextKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MovePageUp, defaults.MovePageUp), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessPriorKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MoveLeft, defaults.MoveLeft), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessLeftKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MoveRight, defaults.MoveRight), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessRightKey(e);
            }
            else if (MatchesGesture(beginEditGesture, e, allowAdditionalModifiers: false))
            {
                return ProcessF2Key(e);
            }
            else if (MatchesDefaultBeginEditWithAlt(e, overrides, beginEditGesture))
            {
                return ProcessF2Key(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MoveHome, defaults.MoveHome), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessHomeKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.MoveEnd, defaults.MoveEnd), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessEndKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.Enter, defaults.Enter), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessEnterKey(e);
            }
            else if (MatchesGesture(ResolveGesture(overrides?.CancelEdit, defaults.CancelEdit), e, allowAdditionalModifiers: true))
            {
                return ProcessEscapeKey();
            }
            else if (MatchesGesture(ResolveGesture(overrides?.SelectAll, defaults.SelectAll), e, allowAdditionalModifiers: false))
            {
                return ProcessAKey();
            }
            else if (MatchesGesture(ResolveGesture(overrides?.Copy, defaults.Copy), e, allowAdditionalModifiers: false) ||
                     MatchesGesture(ResolveGesture(overrides?.CopyAlternate, defaults.CopyAlternate), e, allowAdditionalModifiers: false))
            {
                return ProcessCopyKey();
            }
            else if (MatchesGesture(ResolveGesture(overrides?.Delete, defaults.Delete), e, allowAdditionalModifiers: true))
            {
                return ProcessDeleteKey();
            }
            else if (MatchesGesture(ResolveGesture(overrides?.ExpandAll, defaults.ExpandAll), e, allowAdditionalModifiers: true))
            {
                focusDataGrid = ProcessMultiplyKey(e);
            }

            if (focusDataGrid)
            {
                Focus();
            }
            return focusDataGrid;
        }

        private DataGridKeyboardGestures GetDefaultKeyboardGestures()
        {
            if (_defaultKeyboardGestures == null)
            {
                _defaultKeyboardGestures = DataGridKeyboardGestures.CreateDefault(GetCommandModifiers());
            }

            return _defaultKeyboardGestures;
        }

        private KeyModifiers GetCommandModifiers()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            return topLevel?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        }

        private static KeyGesture ResolveGesture(KeyGesture overrideGesture, KeyGesture defaultGesture)
        {
            return overrideGesture ?? defaultGesture;
        }

        private static bool AllowsCtrlModifier(KeyGesture gesture)
        {
            if (gesture == null)
            {
                return false;
            }

            return (gesture.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        }

        private static bool MatchesGesture(KeyGesture gesture, KeyEventArgs e, bool allowAdditionalModifiers)
        {
            if (gesture == null || gesture.Key == Key.None)
            {
                return false;
            }

            if (NormalizeGestureKey(e.Key) != NormalizeGestureKey(gesture.Key))
            {
                return false;
            }

            if (allowAdditionalModifiers)
            {
                return (e.KeyModifiers & gesture.KeyModifiers) == gesture.KeyModifiers;
            }

            return e.KeyModifiers == gesture.KeyModifiers;
        }

        private bool MatchesDefaultBeginEditWithAlt(KeyEventArgs e, DataGridKeyboardGestures overrides, KeyGesture beginEditGesture)
        {
            if (overrides?.BeginEdit != null || beginEditGesture == null)
            {
                return false;
            }

            if (beginEditGesture.Key != Key.F2 || beginEditGesture.KeyModifiers != KeyModifiers.None)
            {
                return false;
            }

            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift, out bool alt);
            return !ctrl && !shift && alt && NormalizeGestureKey(e.Key) == Key.F2;
        }

        private bool ProcessTabKey(KeyEventArgs e, bool allowCtrl)
        {
            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out bool shift);
            return ProcessTabKey(e, shift, ctrl, allowCtrl);
        }

        private static Key NormalizeGestureKey(Key key)
        {
            return key switch
            {
                Key.Add => Key.OemPlus,
                Key.Subtract => Key.OemMinus,
                Key.Decimal => Key.OemPeriod,
                _ => key
            };
        }






        private bool ProcessTabKey(KeyEventArgs e, bool shift, bool ctrl, bool allowCtrl)
        {
            if ((!allowCtrl && ctrl) || _editingColumnIndex == -1 || IsReadOnly)
            {
                //Go to the next/previous control on the page when
                // - Ctrl key is used
                // - Potential current cell is not edited, or the datagrid is read-only.
                return false;
            }

            // Try to locate a writable cell before/after the current cell
            Debug.Assert(CurrentColumnIndex != -1);
            Debug.Assert(CurrentSlot != -1);

            int neighborVisibleWritableColumnIndex, neighborSlot;
            DataGridColumn dataGridColumn;
            if (shift)
            {
                dataGridColumn = ColumnsInternal.GetPreviousVisibleWritableColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                neighborSlot = GetPreviousVisibleSlot(CurrentSlot);
                if (EditingRow != null)
                {
                    while (neighborSlot != -1 && IsGroupSlot(neighborSlot))
                    {
                        neighborSlot = GetPreviousVisibleSlot(neighborSlot);
                    }
                }
            }
            else
            {
                dataGridColumn = ColumnsInternal.GetNextVisibleWritableColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                neighborSlot = GetNextVisibleSlot(CurrentSlot);
                if (EditingRow != null)
                {
                    while (neighborSlot < SlotCount && IsGroupSlot(neighborSlot))
                    {
                        neighborSlot = GetNextVisibleSlot(neighborSlot);
                    }
                }
            }
            neighborVisibleWritableColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;

            if (neighborVisibleWritableColumnIndex == -1 && (neighborSlot == -1 || neighborSlot >= SlotCount))
            {
                // There is no previous/next row and no previous/next writable cell on the current row
                return false;
            }

            if (WaitForLostFocus(() => ProcessTabKey(e, shift, ctrl, allowCtrl)))
            {
                return true;
            }

            int targetSlot = -1, targetColumnIndex = -1;

            _noSelectionChangeCount++;
            try
            {
                if (neighborVisibleWritableColumnIndex == -1)
                {
                    targetSlot = neighborSlot;
                    if (shift)
                    {
                        Debug.Assert(ColumnsInternal.LastVisibleWritableColumn != null);
                        targetColumnIndex = ColumnsInternal.LastVisibleWritableColumn.Index;
                    }
                    else
                    {
                        Debug.Assert(ColumnsInternal.FirstVisibleWritableColumn != null);
                        targetColumnIndex = ColumnsInternal.FirstVisibleWritableColumn.Index;
                    }
                }
                else
                {
                    targetSlot = CurrentSlot;
                    targetColumnIndex = neighborVisibleWritableColumnIndex;
                }

                DataGridSelectionAction action;
                if (targetSlot != CurrentSlot || (SelectionMode == DataGridSelectionMode.Extended))
                {
                    if (IsSlotOutOfBounds(targetSlot))
                    {
                        return true;
                    }
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    action = DataGridSelectionAction.None;
                }

                UpdateSelectionAndCurrency(targetColumnIndex, targetSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            if (_successfullyUpdatedSelection)
            {
                BeginCellEdit(e);
            }

            // Return true to say we handled the key event even if the operation was unsuccessful. If we don't
            // say we handled this event, the framework will continue to process the tab key and change focus.
            return true;
        }








        private bool ProcessUpKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessUpKey(shift, ctrl)))
            {
                return true;
            }

            int previousVisibleSlot = (CurrentSlot != -1) ? GetPreviousVisibleSlot(CurrentSlot) : -1;

            _noSelectionChangeCount++;

            try
            {
                int slot;
                int columnIndex;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    slot = firstVisibleSlot;
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else if (ctrl)
                {
                    if (shift)
                    {
                        // Both Ctrl and Shift
                        slot = firstVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = (SelectionMode == DataGridSelectionMode.Extended)
                            ? DataGridSelectionAction.SelectFromAnchorToCurrent
                            : DataGridSelectionAction.SelectCurrent;
                    }
                    else
                    {
                        // Ctrl without Shift
                        slot = firstVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }
                else
                {
                    if (previousVisibleSlot == -1)
                    {
                        return true;
                    }
                    if (shift)
                    {
                        // Shift without Ctrl
                        slot = previousVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectFromAnchorToCurrent;
                    }
                    else
                    {
                        // Neither Shift nor Ctrl
                        slot = previousVisibleSlot;
                        columnIndex = CurrentColumnIndex;
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                }
                UpdateSelectionAndCurrency(columnIndex, slot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }

        private bool ProcessMultiplyKey(KeyEventArgs e)
        {
            if (!_hierarchicalRowsEnabled || _hierarchicalAdapter == null)
            {
                return false;
            }

            if (TryHandleGroupSlotAsNode(CurrentSlot, GroupSlotAction.Expand, subtree: true))
            {
                return true;
            }

            if (!TryGetHierarchicalIndexFromSlot(CurrentSlot, out var hierarchicalIndex))
            {
                return false;
            }

            var node = _hierarchicalAdapter.NodeAt(hierarchicalIndex);
            RunHierarchicalAction(() => _hierarchicalAdapter.ExpandAll(node));
            return true;
        }




        private bool ProcessLeftKey(bool shift, bool ctrl, bool alt)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessLeftKey(shift, ctrl, alt)))
            {
                return true;
            }

            if (_hierarchicalRowsEnabled && _hierarchicalAdapter != null)
            {
                if (TryHandleGroupSlotAsNode(CurrentSlot, GroupSlotAction.Collapse, subtree: alt))
                {
                    return true;
                }

                if (TryGetHierarchicalIndexFromSlot(CurrentSlot, out var hierarchicalIndex))
                {
                    if (alt)
                    {
                        var node = _hierarchicalAdapter.NodeAt(hierarchicalIndex);
                        RunHierarchicalAction(() => _hierarchicalAdapter.CollapseAll(node));
                        return true;
                    }

                    if (_hierarchicalAdapter.IsExpandable(hierarchicalIndex) &&
                        _hierarchicalAdapter.IsExpanded(hierarchicalIndex))
                    {
                        _hierarchicalAdapter.Collapse(hierarchicalIndex);
                        return true;
                    }
                }
            }

            int previousVisibleColumnIndex = -1;
            if (CurrentColumnIndex != -1)
            {
                dataGridColumn = ColumnsInternal.GetPreviousVisibleNonFillerColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                if (dataGridColumn != null)
                {
                    previousVisibleColumnIndex = dataGridColumn.Index;
                }
            }

            _noSelectionChangeCount++;
            try
            {
                if (ctrl)
                {
                    return ProcessLeftMost(firstVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    if (RowGroupHeadersTable.Contains(CurrentSlot))
                    {
                        CollapseRowGroup(RowGroupHeadersTable.GetValueAt(CurrentSlot).CollectionViewGroup, collapseAllSubgroups: false);
                    }
                    else if (CurrentColumnIndex == -1)
                    {
                        UpdateSelectionAndCurrency(
                            firstVisibleColumnIndex,
                            firstVisibleSlot,
                            DataGridSelectionAction.SelectCurrent,
                            scrollIntoView: true);
                    }
                    else
                    {
                        if (previousVisibleColumnIndex == -1)
                        {
                            return true;
                        }

                        UpdateSelectionAndCurrency(
                            previousVisibleColumnIndex,
                            CurrentSlot,
                            DataGridSelectionAction.None,
                            scrollIntoView: true);
                    }
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessRightKey(bool shift, bool ctrl, bool alt)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.LastVisibleColumn;
            int lastVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (lastVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(delegate { ProcessRightKey(shift, ctrl, alt); }))
            {
                return true;
            }

            if (_hierarchicalRowsEnabled && _hierarchicalAdapter != null)
            {
                if (TryHandleGroupSlotAsNode(CurrentSlot, GroupSlotAction.Expand, subtree: alt))
                {
                    return true;
                }

                if (TryGetHierarchicalIndexFromSlot(CurrentSlot, out var hierarchicalIndex) &&
                    _hierarchicalAdapter.IsExpandable(hierarchicalIndex))
                {
                    if (alt)
                    {
                        var node = _hierarchicalAdapter.NodeAt(hierarchicalIndex);
                        RunHierarchicalAction(() => _hierarchicalAdapter.ExpandAll(node));
                        return true;
                    }

                    if (!_hierarchicalAdapter.IsExpanded(hierarchicalIndex))
                    {
                        _hierarchicalAdapter.Expand(hierarchicalIndex);
                        return true;
                    }
                }
            }

            int nextVisibleColumnIndex = -1;
            if (CurrentColumnIndex != -1)
            {
                dataGridColumn = ColumnsInternal.GetNextVisibleColumn(ColumnsItemsInternal[CurrentColumnIndex]);
                if (dataGridColumn != null)
                {
                    nextVisibleColumnIndex = dataGridColumn.Index;
                }
            }
            _noSelectionChangeCount++;
            try
            {
                if (ctrl)
                {
                    return ProcessRightMost(lastVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    if (RowGroupHeadersTable.Contains(CurrentSlot))
                    {
                        ExpandRowGroup(RowGroupHeadersTable.GetValueAt(CurrentSlot).CollectionViewGroup, expandAllSubgroups: false);
                    }
                    else if (CurrentColumnIndex == -1)
                    {
                        var firstVisibleColumn = ColumnsInternal.FirstVisibleColumn;
                        Debug.Assert(firstVisibleColumn != null);
                        int firstVisibleColumnIndex = firstVisibleColumn.Index;

                        UpdateSelectionAndCurrency(
                            firstVisibleColumnIndex,
                            firstVisibleSlot,
                            DataGridSelectionAction.SelectCurrent,
                            scrollIntoView: true);
                    }
                    else
                    {
                        if (nextVisibleColumnIndex == -1)
                        {
                            return true;
                        }

                        UpdateSelectionAndCurrency(
                            nextVisibleColumnIndex,
                            CurrentSlot,
                            DataGridSelectionAction.None,
                            scrollIntoView: true);
                    }
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessHomeKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            if (firstVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessHomeKey(shift, ctrl)))
            {
                return true;
            }

            _noSelectionChangeCount++;
            try
            {
                if (!ctrl)
                {
                    return ProcessLeftMost(firstVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    DataGridSelectionAction action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;

                    UpdateSelectionAndCurrency(firstVisibleColumnIndex, firstVisibleSlot, action, scrollIntoView: true);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessEndKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.LastVisibleColumn;
            int lastVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            int firstVisibleSlot = FirstVisibleSlot;
            int lastVisibleSlot = LastVisibleSlot;
            if (lastVisibleColumnIndex == -1 || firstVisibleSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessEndKey(shift, ctrl)))
            {
                return true;
            }

            _noSelectionChangeCount++;
            try
            {
                if (!ctrl)
                {
                    return ProcessRightMost(lastVisibleColumnIndex, firstVisibleSlot);
                }
                else
                {
                    DataGridSelectionAction action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;

                    UpdateSelectionAndCurrency(lastVisibleColumnIndex, lastVisibleSlot, action, scrollIntoView: true);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessEnterKey(bool shift, bool ctrl)
        {
            int oldCurrentSlot = CurrentSlot;

            if (!ctrl)
            {
                // If Enter was used by a TextBox, we shouldn't handle the key
                var focusManager = FocusManager.GetFocusManager(this);
                Debug.Assert(focusManager != null);
                if (focusManager!.GetFocusedElement() is TextBox focusedTextBox
                    && focusedTextBox.AcceptsReturn)
                {
                    return false;
                }

                if (WaitForLostFocus(() => ProcessEnterKey(shift, ctrl)))
                {
                    return true;
                }

                // Enter behaves like down arrow - it commits the potential editing and goes down one cell.
                if (!ProcessDownKeyInternal(false, ctrl))
                {
                    return false;
                }
            }
            else if (WaitForLostFocus(() => ProcessEnterKey(shift, ctrl)))
            {
                return true;
            }

            // Try to commit the potential editing
            if (oldCurrentSlot == CurrentSlot &&
                EndCellEdit(DataGridEditAction.Commit, exitEditingMode: true, keepFocus: true, raiseEvents: true) &&
                EditingRow != null)
            {
                EndRowEdit(DataGridEditAction.Commit, exitEditingMode: true, raiseEvents: true);
                ScrollIntoView(CurrentItem, CurrentColumn);
            }

            return true;
        }






        private bool ProcessNextKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            if (firstVisibleColumnIndex == -1 || DisplayData.FirstScrollingSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessNextKey(shift, ctrl)))
            {
                return true;
            }

            int nextPageSlot = CurrentSlot == -1 ? DisplayData.FirstScrollingSlot : CurrentSlot;
            Debug.Assert(nextPageSlot != -1);
            int slot = GetNextVisibleSlot(nextPageSlot);

            int scrollCount = DisplayData.NumTotallyDisplayedScrollingElements;
            while (scrollCount > 0 && slot < SlotCount)
            {
                nextPageSlot = slot;
                scrollCount--;
                slot = GetNextVisibleSlot(slot);
            }

            _noSelectionChangeCount++;
            try
            {
                DataGridSelectionAction action;
                int columnIndex;
                if (CurrentColumnIndex == -1)
                {
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    columnIndex = CurrentColumnIndex;
                    action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? action = DataGridSelectionAction.SelectFromAnchorToCurrent
                        : action = DataGridSelectionAction.SelectCurrent;
                }

                UpdateSelectionAndCurrency(columnIndex, nextPageSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }




        private bool ProcessPriorKey(bool shift, bool ctrl)
        {
            DataGridColumn dataGridColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            int firstVisibleColumnIndex = (dataGridColumn == null) ? -1 : dataGridColumn.Index;
            if (firstVisibleColumnIndex == -1 || DisplayData.FirstScrollingSlot == -1)
            {
                return false;
            }

            if (WaitForLostFocus(() => ProcessPriorKey(shift, ctrl)))
            {
                return true;
            }

            int previousPageSlot = (CurrentSlot == -1) ? DisplayData.FirstScrollingSlot : CurrentSlot;
            Debug.Assert(previousPageSlot != -1);

            int scrollCount = DisplayData.NumTotallyDisplayedScrollingElements;
            int slot = GetPreviousVisibleSlot(previousPageSlot);
            while (scrollCount > 0 && slot != -1)
            {
                previousPageSlot = slot;
                scrollCount--;
                slot = GetPreviousVisibleSlot(slot);
            }
            Debug.Assert(previousPageSlot != -1);

            _noSelectionChangeCount++;
            try
            {
                int columnIndex;
                DataGridSelectionAction action;
                if (CurrentColumnIndex == -1)
                {
                    columnIndex = firstVisibleColumnIndex;
                    action = DataGridSelectionAction.SelectCurrent;
                }
                else
                {
                    columnIndex = CurrentColumnIndex;
                    action = (shift && SelectionMode == DataGridSelectionMode.Extended)
                        ? DataGridSelectionAction.SelectFromAnchorToCurrent
                        : DataGridSelectionAction.SelectCurrent;
                }

                UpdateSelectionAndCurrency(columnIndex, previousPageSlot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return _successfullyUpdatedSelection;
        }








        private void UpdateKeyboardGestureSubscriptions()
        {
            _keyDownRouteFinishedSubscription?.Dispose();
            _keyDownRouteFinishedSubscription = null;
            _keyUpRouteFinishedSubscription?.Dispose();
            _keyUpRouteFinishedSubscription = null;

            if (!IsAttachedToVisualTree)
            {
                return;
            }

            _keyDownRouteFinishedSubscription = InputElement.KeyDownEvent.RouteFinished.Subscribe(OnKeyDownRouteFinished);
            _keyUpRouteFinishedSubscription = InputElement.KeyUpEvent.RouteFinished.Subscribe(OnKeyUpRouteFinished);
        }

        private void OnKeyDownRouteFinished(RoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var route = e.Route;
            var isBubble = route.HasFlag(RoutingStrategies.Bubble);
            var isDirect = route == RoutingStrategies.Direct || route == 0;
            if (!isBubble && !isDirect)
            {
                return;
            }

            if (e is not KeyEventArgs keyEventArgs)
            {
                return;
            }

            if (!IsKeyEventFromThisGrid(keyEventArgs))
            {
                return;
            }

            DataGrid_KeyDown(this, keyEventArgs);
        }

        private void OnKeyUpRouteFinished(RoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var route = e.Route;
            var isBubble = route.HasFlag(RoutingStrategies.Bubble);
            var isDirect = route == RoutingStrategies.Direct || route == 0;
            if (!isBubble && !isDirect)
            {
                return;
            }

            if (e is not KeyEventArgs keyEventArgs)
            {
                return;
            }

            if (!IsKeyEventFromThisGrid(keyEventArgs))
            {
                return;
            }

            DataGrid_KeyUp(this, keyEventArgs);
        }

        private bool IsKeyEventFromThisGrid(KeyEventArgs e)
        {
            if (ReferenceEquals(e.Source, this))
            {
                return true;
            }

            if (e.Source is Visual visual)
            {
                var grid = visual.GetSelfAndVisualAncestors().OfType<DataGrid>().FirstOrDefault();
                return grid == this;
            }

            return false;
        }

        private void DataGrid_KeyDownDirectional(object sender, KeyEventArgs e)
        {
            if (e.Handled || !IsDirectionalKey(e.Key))
            {
                return;
            }

            if (!IsKeyEventFromThisGrid(e))
            {
                return;
            }

            e.Handled = ProcessDataGridKey(e);
        }

        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = ProcessDataGridKey(e);
            }
        }

        private static bool IsDirectionalKey(Key key)
        {
            return key is Key.Up or Key.Down or Key.Left or Key.Right;
        }


        private void DataGrid_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var overrides = KeyboardGestureOverrides;
            var defaults = GetDefaultKeyboardGestures();
            var tabGesture = ResolveGesture(overrides?.Tab, defaults.Tab);

            if (!MatchesGesture(tabGesture, e, allowAdditionalModifiers: true))
            {
                return;
            }

            KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out _);
            if (ctrl && !AllowsCtrlModifier(tabGesture))
            {
                return;
            }

            if (CurrentColumnIndex != -1 && e.Source == this)
            {
                bool success =
                    ScrollSlotIntoView(
                        CurrentColumnIndex, CurrentSlot,
                        forCurrentCellChange: false,
                        forceHorizontalScroll: true);
                Debug.Assert(success);
                if (SelectedItem == null)
                {
                    SetRowSelection(CurrentSlot, isSelected: true, setAnchorSlot: true);
                }
            }
        }







        //TODO: Ensure left button is checked for
        private bool UpdateStateOnMouseLeftButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit, bool shift, bool ctrl)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Pointer, pointerPressedEventArgs);

            if (SelectionUnit != DataGridSelectionUnit.FullRow && columnIndex >= 0)
            {
                return UpdateCellSelectionOnMouseLeftButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl);
            }

            bool beginEdit;

            Debug.Assert(slot >= 0);

            // Before changing selection, check if the current cell needs to be committed, and
            // check if the current row needs to be committed. If any of those two operations are required and fail,
            // do not change selection, and do not change current cell.

            bool wasInEdit = EditingColumnIndex != -1;

            if (IsSlotOutOfBounds(slot))
            {
                return true;
            }

            if (wasInEdit && (columnIndex != EditingColumnIndex || slot != CurrentSlot) &&
                WaitForLostFocus(() => UpdateStateOnMouseLeftButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl)))
            {
                return true;
            }

            try
            {
                _noSelectionChangeCount++;

                beginEdit = allowEdit &&
                            CurrentSlot == slot &&
                            columnIndex != -1 &&
                            (wasInEdit || CurrentColumnIndex == columnIndex) &&
                            !GetColumnEffectiveReadOnlyState(ColumnsItemsInternal[columnIndex]);

                DataGridSelectionAction action;
                if (SelectionMode == DataGridSelectionMode.Extended && shift)
                {
                    // Shift select multiple rows
                    action = DataGridSelectionAction.SelectFromAnchorToCurrent;
                }
                else if (GetRowSelection(slot))  // Unselecting single row or Selecting a previously multi-selected row
                {
                    if (!ctrl && SelectionMode == DataGridSelectionMode.Extended && _selectedItems.Count != 0)
                    {
                        // Unselect everything except the row that was clicked on
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                    else if (ctrl && EditingRow == null)
                    {
                        action = DataGridSelectionAction.RemoveCurrentFromSelection;
                    }
                    else
                    {
                        action = DataGridSelectionAction.None;
                    }
                }
                else // Selecting a single row or multi-selecting with Ctrl
                {
                    if (SelectionMode == DataGridSelectionMode.Single || !ctrl)
                    {
                        // Unselect the currently selected rows except the new selected row
                        action = DataGridSelectionAction.SelectCurrent;
                    }
                    else
                    {
                        action = DataGridSelectionAction.AddCurrentToSelection;
                    }
                }

                UpdateSelectionAndCurrency(columnIndex, slot, action, scrollIntoView: false);
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            if (_successfullyUpdatedSelection && beginEdit && BeginCellEdit(pointerPressedEventArgs))
            {
                FocusEditingCell(setFocus: true);
            }

            return true;
        }

        private bool UpdateCellSelectionOnMouseLeftButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit, bool shift, bool ctrl)
        {
            bool beginEdit;

            Debug.Assert(slot >= 0);

            bool wasInEdit = EditingColumnIndex != -1;

            if (IsSlotOutOfBounds(slot))
            {
                return true;
            }

            if (wasInEdit && (columnIndex != EditingColumnIndex || slot != CurrentSlot) &&
                WaitForLostFocus(() => UpdateCellSelectionOnMouseLeftButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl)))
            {
                return true;
            }

            try
            {
                _noSelectionChangeCount++;

                beginEdit = allowEdit &&
                            CurrentSlot == slot &&
                            columnIndex != -1 &&
                            (wasInEdit || CurrentColumnIndex == columnIndex) &&
                            !GetColumnEffectiveReadOnlyState(ColumnsItemsInternal[columnIndex]);

                var added = new List<DataGridCellInfo>();
                var removed = new List<DataGridCellInfo>();

                if (SelectionMode == DataGridSelectionMode.Single)
                {
                    if (_selectedCellsView.Count > 0)
                    {
                        removed.AddRange(_selectedCellsView);
                    }

                    ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                    AddSingleCellSelection(columnIndex, slot, added);
                }
                else if (SelectionMode == DataGridSelectionMode.Extended && shift && _cellAnchor.Slot != -1)
                {
                    int anchorRowIndex = RowIndexFromSlot(_cellAnchor.Slot);
                    int targetRowIndex = RowIndexFromSlot(slot);
                    if (anchorRowIndex >= 0 && targetRowIndex >= 0)
                    {
                        int startRow = Math.Min(anchorRowIndex, targetRowIndex);
                        int endRow = Math.Max(anchorRowIndex, targetRowIndex);
                        int startCol = Math.Min(_cellAnchor.ColumnIndex, columnIndex);
                        int endCol = Math.Max(_cellAnchor.ColumnIndex, columnIndex);

                        if (!ctrl)
                        {
                            removed.AddRange(_selectedCellsView);
                            ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                        }

                        SelectCellRangeInternal(startRow, endRow, startCol, endCol, added);
                    }
                }
                else
                {
                    bool alreadySelected = GetCellSelectionFromSlot(slot, columnIndex);
                    if (!ctrl)
                    {
                        if (_selectedCellsView.Count > 0)
                        {
                            removed.AddRange(_selectedCellsView);
                        }
                        ClearCellSelectionInternal(clearRows: true, raiseEvent: false);
                    }

                    if (alreadySelected && ctrl)
                    {
                        RemoveCellSelectionFromSlot(slot, columnIndex, removed);
                    }
                    else
                    {
                        AddSingleCellSelection(columnIndex, slot, added);
                    }

                    _cellAnchor = new DataGridCellCoordinates(columnIndex, slot);
                }

                if (added.Count > 0 || removed.Count > 0)
                {
                    RaiseSelectedCellsChanged(added, removed);
                }

                _successfullyUpdatedSelection = true;
                if (CurrentSlot != slot || CurrentColumnIndex != columnIndex)
                {
                    SetCurrentCellCore(columnIndex, slot, commitEdit: true, endRowEdit: false);
                }
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            if (_successfullyUpdatedSelection && beginEdit && BeginCellEdit(pointerPressedEventArgs))
            {
                FocusEditingCell(setFocus: true);
            }

            return true;
        }




        //TODO: Ensure right button is checked for
        private bool UpdateStateOnMouseRightButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit, bool shift, bool ctrl)
        {
            using var _ = BeginSelectionChangeScope(DataGridSelectionChangeSource.Pointer, pointerPressedEventArgs);

            Debug.Assert(slot >= 0);

            if (SelectionUnit != DataGridSelectionUnit.FullRow && columnIndex >= 0)
            {
                if (CurrentSlot != slot || CurrentColumnIndex != columnIndex)
                {
                    SetCurrentCellCore(columnIndex, slot, commitEdit: true, endRowEdit: false);
                }
                return true;
            }

            if (shift || ctrl)
            {
                return true;
            }
            if (IsSlotOutOfBounds(slot))
            {
                return true;
            }
            if (GetRowSelection(slot))
            {
                return true;
            }
            // Unselect everything except the row that was clicked on
            _noSelectionChangeCount++;
            try
            {
                UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
            }
            finally
            {
                NoSelectionChangeCount--;
            }
            return true;
        }





        private Control _clickedElement;


        /// <summary>
        /// Occurs when cell is mouse-pressed.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridCellPointerPressedEventArgs> CellPointerPressed
        {
            add => AddHandler(CellPointerPressedEvent, value);
            remove => RemoveHandler(CellPointerPressedEvent, value);
        }

    }
}
