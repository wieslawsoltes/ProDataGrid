// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Controls.Utils;
using Avalonia.Input;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        private Point _layoutNavigationAnchor;
        private bool _layoutNavigationAnchorValid;
        private int _layoutNavigationAnchorRowIndex = -1;
        private object _layoutNavigationAnchorItem;
        private IDataGridLayoutModel _layoutNavigationAnchorModel;

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
                modifiers,
                out LayoutNavigationPlan layoutPlan);
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
                _ => layoutPlan.IsOwned
                    ? layoutPlan.HasTarget
                    : request.ProposedPosition.HasValue || CanExecuteNavigationWithoutTarget(command)
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
            DataGridNavigationRequest request = CreateNavigationRequest(command, origin, modifiers, out LayoutNavigationPlan layoutPlan);
            DataGridNavigationPosition oldPosition = request.CurrentPosition;
            DataGridNavigationResult result = NavigationModel.Resolve(request);
            bool handled;
            DataGridNavigationFailureReason failureReason = DataGridNavigationFailureReason.None;

            switch (result.Decision)
            {
                case DataGridNavigationDecision.Move:
                    if (TryCreateSpatialBoundaryWrapPlan(
                        command,
                        modifiers,
                        request,
                        result,
                        layoutPlan,
                        out DataGridNavigationCommand wrapCommand,
                        out LayoutNavigationPlan wrapPlan))
                    {
                        handled = ExecuteDefaultNavigation(
                            wrapCommand,
                            keyEventArgs,
                            modifiers,
                            allowCtrlForTab,
                            wrapPlan);
                    }
                    else
                    {
                        handled = TryApplyNavigationTarget(result.Target, modifiers);
                    }
                    if (!handled)
                    {
                        failureReason = DataGridNavigationFailureReason.InvalidTarget;
                    }
                    break;

                case DataGridNavigationDecision.Redirect:
                    if (result.RedirectedCommand != DataGridNavigationCommand.None)
                    {
                        CreateNavigationRequest(result.RedirectedCommand, origin, modifiers, out LayoutNavigationPlan redirectedLayoutPlan);
                        handled = ExecuteDefaultNavigation(
                            result.RedirectedCommand,
                            keyEventArgs,
                            modifiers,
                            allowCtrlForTab,
                            redirectedLayoutPlan);
                    }
                    else
                    {
                        handled = false;
                    }
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
                    handled = ExecuteDefaultNavigation(command, keyEventArgs, modifiers, allowCtrlForTab, layoutPlan);
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
            bool allowCtrlForTab,
            LayoutNavigationPlan layoutPlan)
        {
            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out bool shift, out bool alt);
            if (layoutPlan.IsOwned)
            {
                return ExecuteLayoutNavigation(command, keyEventArgs, modifiers, allowCtrlForTab, alt, layoutPlan);
            }

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
            KeyModifiers modifiers,
            out LayoutNavigationPlan layoutPlan)
        {
            DataGridNavigationPosition current = CreateNavigationPosition();
            layoutPlan = CreateLayoutNavigationPlan(command, modifiers, current);
            DataGridNavigationPosition? proposed = layoutPlan.IsOwned
                ? layoutPlan.HasTarget ? layoutPlan.Target : null
                : TryGetProposedNavigationPosition(command, modifiers, out DataGridNavigationPosition target)
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

        private bool TryCreateSpatialBoundaryWrapPlan(
            DataGridNavigationCommand command,
            KeyModifiers modifiers,
            DataGridNavigationRequest request,
            DataGridNavigationResult result,
            LayoutNavigationPlan layoutPlan,
            out DataGridNavigationCommand wrapCommand,
            out LayoutNavigationPlan wrapPlan)
        {
            wrapCommand = DataGridNavigationCommand.None;
            wrapPlan = default;
            if (!layoutPlan.IsOwned || layoutPlan.HasTarget ||
                NavigationModel is not DataGridNavigationModel policy ||
                !TryGetSpatialBoundaryWrapCommand(command, policy, out wrapCommand) ||
                result.Target != GetLegacyBoundaryWrapTarget(command, request))
            {
                return false;
            }

            wrapPlan = CreateLayoutNavigationPlan(wrapCommand, modifiers, request.CurrentPosition);
            return wrapPlan.IsOwned;
        }

        private static bool TryGetSpatialBoundaryWrapCommand(
            DataGridNavigationCommand command,
            DataGridNavigationModel policy,
            out DataGridNavigationCommand wrapCommand)
        {
            wrapCommand = command switch
            {
                DataGridNavigationCommand.Left when
                    policy.HorizontalBoundaryMode == DataGridNavigationBoundaryMode.Wrap => DataGridNavigationCommand.RowEnd,
                DataGridNavigationCommand.Right when
                    policy.HorizontalBoundaryMode == DataGridNavigationBoundaryMode.Wrap => DataGridNavigationCommand.RowStart,
                DataGridNavigationCommand.Up when
                    policy.VerticalBoundaryMode == DataGridNavigationBoundaryMode.Wrap => DataGridNavigationCommand.ColumnEnd,
                DataGridNavigationCommand.Down when
                    policy.VerticalBoundaryMode == DataGridNavigationBoundaryMode.Wrap => DataGridNavigationCommand.ColumnStart,
                _ => DataGridNavigationCommand.None
            };
            return wrapCommand != DataGridNavigationCommand.None;
        }

        private static DataGridNavigationPosition GetLegacyBoundaryWrapTarget(
            DataGridNavigationCommand command,
            DataGridNavigationRequest request) => command switch
            {
                DataGridNavigationCommand.Left => new DataGridNavigationPosition(
                    request.CurrentPosition.RowIndex,
                    request.LastColumnDisplayIndex),
                DataGridNavigationCommand.Right => new DataGridNavigationPosition(
                    request.CurrentPosition.RowIndex,
                    request.FirstColumnDisplayIndex),
                DataGridNavigationCommand.Up => new DataGridNavigationPosition(
                    request.LastRowIndex,
                    request.CurrentPosition.ColumnDisplayIndex),
                DataGridNavigationCommand.Down => new DataGridNavigationPosition(
                    request.FirstRowIndex,
                    request.CurrentPosition.ColumnDisplayIndex),
                _ => DataGridNavigationPosition.Unset
            };

        private LayoutNavigationPlan CreateLayoutNavigationPlan(
            DataGridNavigationCommand command,
            KeyModifiers modifiers,
            DataGridNavigationPosition current)
        {
            if (LayoutModel == null || !current.IsValid || CurrentColumn == null ||
                !TryMapLayoutNavigationDirection(command, modifiers, out DataGridLayoutNavigationDirection direction) ||
                !SupportsLayoutNavigation(direction))
            {
                return default;
            }

            TryGetLayoutNavigationBounds(current.RowIndex, out Rect sourceBounds);
            Point anchor = GetLayoutNavigationAnchor(current.RowIndex, sourceBounds);
            bool hasTarget = TryResolveLayoutNavigation(
                current.RowIndex,
                direction,
                anchor,
                out int targetRowIndex,
                out Rect estimatedBounds);
            DataGridColumn targetColumn = command switch
            {
                DataGridNavigationCommand.GridStart => ColumnsInternal.FirstVisibleNonFillerColumn,
                DataGridNavigationCommand.GridEnd => GetLastVisibleNonFillerNavigationColumn(),
                _ => CurrentColumn
            };
            DataGridNavigationPosition target = hasTarget && targetColumn != null
                ? new DataGridNavigationPosition(targetRowIndex, targetColumn.DisplayIndex)
                : DataGridNavigationPosition.Unset;
            return new LayoutNavigationPlan(
                direction,
                target,
                sourceBounds,
                estimatedBounds,
                anchor,
                hasTarget && target.IsValid);
        }

        private static bool TryMapLayoutNavigationDirection(
            DataGridNavigationCommand command,
            KeyModifiers modifiers,
            out DataGridLayoutNavigationDirection direction)
        {
            bool edge = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
            direction = command switch
            {
                DataGridNavigationCommand.Up => edge ? DataGridLayoutNavigationDirection.First : DataGridLayoutNavigationDirection.Up,
                DataGridNavigationCommand.Down => edge ? DataGridLayoutNavigationDirection.Last : DataGridLayoutNavigationDirection.Down,
                DataGridNavigationCommand.Left => edge ? DataGridLayoutNavigationDirection.LineStart : DataGridLayoutNavigationDirection.Left,
                DataGridNavigationCommand.Right => edge ? DataGridLayoutNavigationDirection.LineEnd : DataGridLayoutNavigationDirection.Right,
                DataGridNavigationCommand.PageUp => DataGridLayoutNavigationDirection.PageUp,
                DataGridNavigationCommand.PageDown => DataGridLayoutNavigationDirection.PageDown,
                DataGridNavigationCommand.RowStart => DataGridLayoutNavigationDirection.LineStart,
                DataGridNavigationCommand.RowEnd => DataGridLayoutNavigationDirection.LineEnd,
                DataGridNavigationCommand.ColumnStart or DataGridNavigationCommand.GridStart => DataGridLayoutNavigationDirection.First,
                DataGridNavigationCommand.ColumnEnd or DataGridNavigationCommand.GridEnd => DataGridLayoutNavigationDirection.Last,
                _ => default
            };
            return command is DataGridNavigationCommand.Up or DataGridNavigationCommand.Down or
                DataGridNavigationCommand.Left or DataGridNavigationCommand.Right or
                DataGridNavigationCommand.PageUp or DataGridNavigationCommand.PageDown or
                DataGridNavigationCommand.RowStart or DataGridNavigationCommand.RowEnd or
                DataGridNavigationCommand.ColumnStart or DataGridNavigationCommand.ColumnEnd or
                DataGridNavigationCommand.GridStart or DataGridNavigationCommand.GridEnd;
        }

        private bool ExecuteLayoutNavigation(
            DataGridNavigationCommand command,
            KeyEventArgs keyEventArgs,
            KeyModifiers modifiers,
            bool allowCtrlForTab,
            bool alt,
            LayoutNavigationPlan plan)
        {
            if (WaitForLostFocus(() => ExecuteDefaultNavigationWithFreshLayoutPlan(
                command,
                keyEventArgs,
                modifiers,
                allowCtrlForTab)))
            {
                return true;
            }

            if ((command == DataGridNavigationCommand.Left && TryProcessHierarchyLeft(alt)) ||
                (command == DataGridNavigationCommand.Right && TryProcessHierarchyRight(alt)))
            {
                return true;
            }

            if (!plan.HasTarget || !TryApplyNavigationTarget(plan.Target, modifiers))
            {
                return false;
            }

            UpdateLayoutNavigationAnchor(plan);
            return true;
        }

        private void ExecuteDefaultNavigationWithFreshLayoutPlan(
            DataGridNavigationCommand command,
            KeyEventArgs keyEventArgs,
            KeyModifiers modifiers,
            bool allowCtrlForTab)
        {
            DataGridNavigationPosition current = CreateNavigationPosition();
            LayoutNavigationPlan plan = CreateLayoutNavigationPlan(command, modifiers, current);
            ExecuteDefaultNavigation(command, keyEventArgs, modifiers, allowCtrlForTab, plan);
        }

        private Point GetLayoutNavigationAnchor(int currentRowIndex, Rect sourceBounds)
        {
            if (_layoutNavigationAnchorValid &&
                _layoutNavigationAnchorRowIndex == currentRowIndex &&
                ReferenceEquals(_layoutNavigationAnchorItem, CurrentItem) &&
                ReferenceEquals(_layoutNavigationAnchorModel, LayoutModel))
            {
                return _layoutNavigationAnchor;
            }

            return GetBoundsCenter(sourceBounds);
        }

        private void UpdateLayoutNavigationAnchor(LayoutNavigationPlan plan)
        {
            Rect targetBounds = TryGetLayoutNavigationBounds(plan.Target.RowIndex, out Rect actualBounds)
                ? actualBounds
                : plan.EstimatedBounds;
            Point targetCenter = GetBoundsCenter(targetBounds);
            Point sourceCenter = GetBoundsCenter(plan.SourceBounds);
            _layoutNavigationAnchor = plan.Direction switch
            {
                DataGridLayoutNavigationDirection.Up or DataGridLayoutNavigationDirection.Down =>
                    new Point(plan.Anchor.X, targetCenter.Y),
                DataGridLayoutNavigationDirection.Left or DataGridLayoutNavigationDirection.Right =>
                    new Point(targetCenter.X, plan.Anchor.Y),
                DataGridLayoutNavigationDirection.PageUp or DataGridLayoutNavigationDirection.PageDown
                    when Math.Abs(targetCenter.X - sourceCenter.X) > Math.Abs(targetCenter.Y - sourceCenter.Y) =>
                    new Point(targetCenter.X, plan.Anchor.Y),
                DataGridLayoutNavigationDirection.PageUp or DataGridLayoutNavigationDirection.PageDown =>
                    new Point(plan.Anchor.X, targetCenter.Y),
                _ => targetCenter
            };
            _layoutNavigationAnchorValid = true;
            _layoutNavigationAnchorRowIndex = plan.Target.RowIndex;
            _layoutNavigationAnchorItem = CurrentItem;
            _layoutNavigationAnchorModel = LayoutModel;
        }

        private void ResetLayoutNavigationAnchor()
        {
            _layoutNavigationAnchor = default;
            _layoutNavigationAnchorValid = false;
            _layoutNavigationAnchorRowIndex = -1;
            _layoutNavigationAnchorItem = null;
            _layoutNavigationAnchorModel = null;
        }

        private static Point GetBoundsCenter(Rect bounds) =>
            new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));

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

        private readonly struct LayoutNavigationPlan
        {
            public LayoutNavigationPlan(
                DataGridLayoutNavigationDirection direction,
                DataGridNavigationPosition target,
                Rect sourceBounds,
                Rect estimatedBounds,
                Point anchor,
                bool hasTarget)
            {
                Direction = direction;
                Target = target;
                SourceBounds = sourceBounds;
                EstimatedBounds = estimatedBounds;
                Anchor = anchor;
                HasTarget = hasTarget;
                IsOwned = true;
            }

            public bool IsOwned { get; }
            public bool HasTarget { get; }
            public DataGridLayoutNavigationDirection Direction { get; }
            public DataGridNavigationPosition Target { get; }
            public Rect SourceBounds { get; }
            public Rect EstimatedBounds { get; }
            public Point Anchor { get; }
        }
    }
}
