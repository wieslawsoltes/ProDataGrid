// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Controls.DataGridNavigation;
using Avalonia.Controls.Utils;
using Avalonia.Input;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        /// <summary>
        /// Attempts to execute a semantic navigation command through the current <see cref="NavigationModel"/>.
        /// </summary>
        /// <param name="command">The semantic navigation command.</param>
        /// <param name="modifiers">Modifiers used for selection extension and edge movement.</param>
        /// <returns><see langword="true"/> when the request was handled by the grid; otherwise, <see langword="false"/>.</returns>
        public bool Navigate(DataGridNavigationCommand command, KeyModifiers modifiers = KeyModifiers.None)
        {
            if (command == DataGridNavigationCommand.None)
            {
                return false;
            }

            return ProcessNavigationCommand(
                command,
                null,
                DataGridNavigationOrigin.Programmatic,
                modifiers,
                allowCtrlForTab: false);
        }

        /// <summary>
        /// Determines whether a semantic navigation command currently has a valid route.
        /// </summary>
        /// <param name="command">The semantic navigation command.</param>
        /// <param name="modifiers">Modifiers used to resolve the proposed target.</param>
        /// <returns><see langword="true"/> when the model or default engine can handle the request.</returns>
        public bool CanNavigate(DataGridNavigationCommand command, KeyModifiers modifiers = KeyModifiers.None)
        {
            if (command == DataGridNavigationCommand.None)
            {
                return false;
            }

            DataGridNavigationRequest request = CreateNavigationRequest(
                command,
                DataGridNavigationOrigin.Programmatic,
                modifiers);
            if (NavigationModel is not IDataGridNavigationQueryModel queryModel)
            {
                return request.ProposedPosition.HasValue || CanExecuteNavigationWithoutTarget(command);
            }

            DataGridNavigationResult result = queryModel.Query(request);
            return result.Decision switch
            {
                DataGridNavigationDecision.Move => result.Target.IsValid,
                DataGridNavigationDecision.Redirect => result.RedirectedCommand != DataGridNavigationCommand.None,
                DataGridNavigationDecision.Stay => false,
                DataGridNavigationDecision.LeaveGrid => false,
                _ => request.ProposedPosition.HasValue || CanExecuteNavigationWithoutTarget(command)
            };
        }

        private bool ProcessNavigationCommand(
            DataGridNavigationCommand command,
            KeyEventArgs keyEventArgs,
            DataGridNavigationOrigin origin,
            bool allowCtrlForTab = false)
        {
            KeyModifiers modifiers = keyEventArgs?.KeyModifiers ?? KeyModifiers.None;
            return ProcessNavigationCommand(command, keyEventArgs, origin, modifiers, allowCtrlForTab);
        }

        private bool ProcessNavigationCommand(
            DataGridNavigationCommand command,
            KeyEventArgs keyEventArgs,
            DataGridNavigationOrigin origin,
            KeyModifiers modifiers,
            bool allowCtrlForTab)
        {
            DataGridNavigationRequest request = CreateNavigationRequest(command, origin, modifiers);
            DataGridNavigationPosition oldPosition = request.CurrentPosition;
            DataGridNavigationResult result = NavigationModel.Resolve(request);
            bool handled;
            DataGridNavigationFailureReason failureReason = DataGridNavigationFailureReason.None;

            switch (result.Decision)
            {
                case DataGridNavigationDecision.Move:
                    handled = TryApplyNavigationTarget(result.Target, modifiers);
                    if (!handled)
                    {
                        failureReason = DataGridNavigationFailureReason.InvalidTarget;
                    }
                    break;

                case DataGridNavigationDecision.Redirect:
                    handled = result.RedirectedCommand != DataGridNavigationCommand.None &&
                        ExecuteDefaultNavigation(result.RedirectedCommand, keyEventArgs, modifiers, allowCtrlForTab);
                    if (!handled)
                    {
                        failureReason = DataGridNavigationFailureReason.BoundaryReached;
                    }
                    break;

                case DataGridNavigationDecision.Stay:
                    handled = true;
                    failureReason = result.FailureReason == DataGridNavigationFailureReason.None
                        ? DataGridNavigationFailureReason.BoundaryReached
                        : result.FailureReason;
                    break;

                case DataGridNavigationDecision.LeaveGrid:
                    handled = false;
                    failureReason = DataGridNavigationFailureReason.BoundaryReached;
                    break;

                default:
                    handled = ExecuteDefaultNavigation(command, keyEventArgs, modifiers, allowCtrlForTab);
                    if (!handled && !CreateNavigationPosition().IsValid)
                    {
                        failureReason = ResolveUnavailableNavigationReason();
                    }
                    else if (!handled)
                    {
                        failureReason = DataGridNavigationFailureReason.BoundaryReached;
                    }
                    break;
            }

            DataGridNavigationPosition newPosition = CreateNavigationPosition();
            NavigationModel.NotifyCompleted(new DataGridNavigationCompleted(
                request,
                result,
                oldPosition,
                newPosition,
                handled,
                failureReason));
            return handled;
        }

        private bool ExecuteDefaultNavigation(
            DataGridNavigationCommand command,
            KeyEventArgs keyEventArgs,
            KeyModifiers modifiers,
            bool allowCtrlForTab)
        {
            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out bool shift, out bool alt);
            return command switch
            {
                DataGridNavigationCommand.Up => ProcessUpKey(shift, ctrl),
                DataGridNavigationCommand.Down => ProcessDownKeyInternal(shift, ctrl),
                DataGridNavigationCommand.Left => ProcessLeftKey(shift, ctrl, alt),
                DataGridNavigationCommand.Right => ProcessRightKey(shift, ctrl, alt),
                DataGridNavigationCommand.PageUp => ProcessPriorKey(shift, ctrl),
                DataGridNavigationCommand.PageDown => ProcessNextKey(shift, ctrl),
                DataGridNavigationCommand.RowStart => ProcessHomeKey(shift, ctrl: false),
                DataGridNavigationCommand.RowEnd => ProcessEndKey(shift, ctrl: false),
                DataGridNavigationCommand.ColumnStart => ProcessUpKey(shift, ctrl: true),
                DataGridNavigationCommand.ColumnEnd => ProcessDownKeyInternal(shift, ctrl: true),
                DataGridNavigationCommand.GridStart => ProcessHomeKey(shift, ctrl: true),
                DataGridNavigationCommand.GridEnd => ProcessEndKey(shift, ctrl: true),
                DataGridNavigationCommand.Next => ProcessTabKey(keyEventArgs, shift: false, ctrl, allowCtrlForTab),
                DataGridNavigationCommand.Previous => ProcessTabKey(keyEventArgs, shift: true, ctrl, allowCtrlForTab),
                DataGridNavigationCommand.Enter => ProcessEnterKey(keyEventArgs, shift, ctrl),
                DataGridNavigationCommand.BeginEdit => ProcessF2Key(keyEventArgs),
                DataGridNavigationCommand.CancelEdit => ProcessEscapeKey(),
                DataGridNavigationCommand.Expand => ProcessExpandCommand(subtree: false),
                DataGridNavigationCommand.Collapse => ProcessCollapseCommand(subtree: false),
                DataGridNavigationCommand.ExpandAll => ProcessMultiplyKey(keyEventArgs),
                _ => false
            };
        }

        private DataGridNavigationRequest CreateNavigationRequest(
            DataGridNavigationCommand command,
            DataGridNavigationOrigin origin,
            KeyModifiers modifiers)
        {
            DataGridNavigationPosition current = CreateNavigationPosition();
            DataGridNavigationPosition? proposed = TryGetProposedNavigationPosition(command, modifiers, out DataGridNavigationPosition target)
                ? target
                : null;
            DataGridColumn firstColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
            DataGridColumn lastColumn = GetLastVisibleNonFillerNavigationColumn();
            int lastRowIndex = DataConnection?.Count > 0 ? DataConnection.Count - 1 : -1;

            return new DataGridNavigationRequest(
                command,
                origin,
                current,
                proposed,
                modifiers,
                _editingColumnIndex != -1,
                SelectionMode,
                SelectionUnit,
                FlowDirection,
                lastRowIndex >= 0 ? 0 : -1,
                lastRowIndex,
                firstColumn?.DisplayIndex ?? -1,
                lastColumn?.DisplayIndex ?? -1);
        }

        private DataGridNavigationPosition CreateNavigationPosition()
        {
            if (CurrentColumnIndex < 0 || CurrentSlot < 0 || IsGroupSlot(CurrentSlot))
            {
                return DataGridNavigationPosition.Unset;
            }

            int rowIndex = RowIndexFromSlot(CurrentSlot);
            if (rowIndex < 0 || CurrentColumn == null)
            {
                return DataGridNavigationPosition.Unset;
            }

            return new DataGridNavigationPosition(rowIndex, CurrentColumn.DisplayIndex);
        }

        private bool TryGetProposedNavigationPosition(
            DataGridNavigationCommand command,
            KeyModifiers modifiers,
            out DataGridNavigationPosition target)
        {
            target = DataGridNavigationPosition.Unset;
            DataGridNavigationPosition current = CreateNavigationPosition();
            if (!current.IsValid || CurrentColumn == null)
            {
                return false;
            }

            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out _, out _);
            int targetSlot = CurrentSlot;
            DataGridColumn targetColumn = CurrentColumn;

            switch (command)
            {
                case DataGridNavigationCommand.Up:
                    targetSlot = ctrl ? FirstVisibleSlot : GetPreviousVisibleSlot(CurrentSlot);
                    break;
                case DataGridNavigationCommand.Down:
                    targetSlot = ctrl ? LastVisibleSlot : GetNextVisibleSlot(CurrentSlot);
                    break;
                case DataGridNavigationCommand.Left:
                    targetColumn = ctrl
                        ? ColumnsInternal.FirstVisibleNonFillerColumn
                        : ColumnsInternal.GetPreviousVisibleNonFillerColumn(CurrentColumn);
                    break;
                case DataGridNavigationCommand.Right:
                    targetColumn = ctrl
                        ? GetLastVisibleNonFillerNavigationColumn()
                        : ColumnsInternal.GetNextVisibleColumn(CurrentColumn);
                    if (targetColumn is DataGridFillerColumn)
                    {
                        targetColumn = null;
                    }
                    break;
                case DataGridNavigationCommand.RowStart:
                    targetColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                    break;
                case DataGridNavigationCommand.RowEnd:
                    targetColumn = GetLastVisibleNonFillerNavigationColumn();
                    break;
                case DataGridNavigationCommand.ColumnStart:
                    targetSlot = FirstVisibleSlot;
                    break;
                case DataGridNavigationCommand.ColumnEnd:
                    targetSlot = LastVisibleSlot;
                    break;
                case DataGridNavigationCommand.GridStart:
                    targetSlot = FirstVisibleSlot;
                    targetColumn = ColumnsInternal.FirstVisibleNonFillerColumn;
                    break;
                case DataGridNavigationCommand.GridEnd:
                    targetSlot = LastVisibleSlot;
                    targetColumn = GetLastVisibleNonFillerNavigationColumn();
                    break;
                case DataGridNavigationCommand.PageUp:
                    targetSlot = GetPageNavigationSlot(forward: false);
                    break;
                case DataGridNavigationCommand.PageDown:
                    targetSlot = GetPageNavigationSlot(forward: true);
                    break;
                case DataGridNavigationCommand.Next:
                case DataGridNavigationCommand.Previous:
                    return TryGetTabNavigationPosition(command == DataGridNavigationCommand.Previous, out target);
                case DataGridNavigationCommand.Enter:
                    if (_editingColumnIndex != -1 && EnterKeyNavigationMode == DataGridEnterKeyNavigationMode.NextCell)
                    {
                        return TryGetTabNavigationPosition(previous: false, out target);
                    }
                    targetSlot = GetNextVisibleSlot(CurrentSlot);
                    break;
                default:
                    return false;
            }

            return TryCreateNavigationPosition(targetSlot, targetColumn, out target) && target != current;
        }

        private bool TryGetTabNavigationPosition(bool previous, out DataGridNavigationPosition target)
        {
            target = DataGridNavigationPosition.Unset;
            DataGridColumn targetColumn = _editingColumnIndex != -1
                ? (previous
                    ? ColumnsInternal.GetPreviousVisibleWritableColumn(CurrentColumn)
                    : ColumnsInternal.GetNextVisibleWritableColumn(CurrentColumn))
                : (previous
                    ? ColumnsInternal.GetPreviousVisibleNonFillerColumn(CurrentColumn)
                    : ColumnsInternal.GetNextVisibleColumn(CurrentColumn));
            int targetSlot = CurrentSlot;

            if (targetColumn is DataGridFillerColumn)
            {
                targetColumn = null;
            }

            if (targetColumn == null)
            {
                targetSlot = previous ? GetPreviousVisibleSlot(CurrentSlot) : GetNextVisibleSlot(CurrentSlot);
                while (targetSlot >= 0 && targetSlot < SlotCount && IsGroupSlot(targetSlot))
                {
                    targetSlot = previous ? GetPreviousVisibleSlot(targetSlot) : GetNextVisibleSlot(targetSlot);
                }

                targetColumn = _editingColumnIndex != -1
                    ? (previous ? ColumnsInternal.LastVisibleWritableColumn : ColumnsInternal.FirstVisibleWritableColumn)
                    : (previous ? GetLastVisibleNonFillerNavigationColumn() : ColumnsInternal.FirstVisibleNonFillerColumn);
            }

            return TryCreateNavigationPosition(targetSlot, targetColumn, out target);
        }

        private int GetPageNavigationSlot(bool forward)
        {
            int pageSlot = CurrentSlot == -1 ? DisplayData.FirstScrollingSlot : CurrentSlot;
            int remaining = DisplayData.NumTotallyDisplayedScrollingElements;
            int slot = forward ? GetNextVisibleSlot(pageSlot) : GetPreviousVisibleSlot(pageSlot);
            while (remaining > 0 && slot >= 0 && slot < SlotCount)
            {
                pageSlot = slot;
                remaining--;
                slot = forward ? GetNextVisibleSlot(slot) : GetPreviousVisibleSlot(slot);
            }

            return pageSlot;
        }

        private bool TryCreateNavigationPosition(
            int slot,
            DataGridColumn column,
            out DataGridNavigationPosition target)
        {
            target = DataGridNavigationPosition.Unset;
            if (column == null || !column.IsVisible || column is DataGridFillerColumn ||
                slot < 0 || slot >= SlotCount || IsGroupSlot(slot) || IsSlotOutOfBounds(slot))
            {
                return false;
            }

            int rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= DataConnection.Count)
            {
                return false;
            }

            target = new DataGridNavigationPosition(rowIndex, column.DisplayIndex);
            return true;
        }

        private bool TryApplyNavigationTarget(DataGridNavigationPosition target, KeyModifiers modifiers)
        {
            if (!target.IsValid || target.RowIndex >= DataConnection.Count ||
                target.ColumnDisplayIndex >= ColumnsInternal.DisplayIndexMap.Count)
            {
                return false;
            }

            DataGridColumn column = ColumnsInternal.GetColumnAtDisplayIndex(target.ColumnDisplayIndex);
            if (column == null || !column.IsVisible || column is DataGridFillerColumn)
            {
                return false;
            }

            int slot = SlotFromRowIndex(target.RowIndex);
            if (slot < 0 || IsSlotOutOfBounds(slot))
            {
                return false;
            }

            KeyboardHelper.GetMetaKeyState(this, modifiers, out _, out bool shift);
            DataGridSelectionAction action = shift && SelectionMode == DataGridSelectionMode.Extended
                ? DataGridSelectionAction.SelectFromAnchorToCurrent
                : target.RowIndex != RowIndexFromSlot(CurrentSlot) || CurrentColumnIndex == -1
                    ? DataGridSelectionAction.SelectCurrent
                    : DataGridSelectionAction.None;

            _noSelectionChangeCount++;
            try
            {
                UpdateSelectionAndCurrency(column.Index, slot, action, scrollIntoView: true);
            }
            finally
            {
                NoSelectionChangeCount--;
            }

            return _successfullyUpdatedSelection || CreateNavigationPosition() == target;
        }

        private DataGridColumn GetLastVisibleNonFillerNavigationColumn()
        {
            DataGridColumn column = ColumnsInternal.LastVisibleColumn;
            while (column is DataGridFillerColumn)
            {
                column = ColumnsInternal.GetPreviousVisibleNonFillerColumn(column);
            }

            return column;
        }

        private bool CanExecuteNavigationWithoutTarget(DataGridNavigationCommand command) =>
            command is DataGridNavigationCommand.BeginEdit or
                DataGridNavigationCommand.CancelEdit or
                DataGridNavigationCommand.Expand or
                DataGridNavigationCommand.Collapse or
                DataGridNavigationCommand.ExpandAll;

        private DataGridNavigationFailureReason ResolveUnavailableNavigationReason()
        {
            if (DataConnection == null || DataConnection.Count == 0)
            {
                return DataGridNavigationFailureReason.NoRows;
            }

            if (ColumnsInternal.FirstVisibleNonFillerColumn == null)
            {
                return DataGridNavigationFailureReason.NoColumns;
            }

            return DataGridNavigationFailureReason.NoCurrentCell;
        }
    }
}
