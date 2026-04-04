// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Utilities;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Presenters;

namespace Avalonia.Controls.DataGridDragDrop
{
    internal sealed class DataGridRowDragDropController : IDataGridRowDragDropController
    {
        private readonly DataGrid _grid;
        private readonly DataGridRowDragDropOptions _options;
        private readonly IDataGridRowDropHandler _dropHandler;
        private readonly DispatcherTimer _autoScrollTimer;
        private int _autoScrollDirection;

        private int? _pointerId;
        private Point _pointerStart;
        private DataGridRow? _dragStartRow;
        private DataGridRowDragInfo? _dragInfo;
        private readonly List<DataGridRow> _draggingRows = new();
        private DataGridRow? _indicatorRow;
        private DataGridRowDropPosition? _indicatorPosition;
        private bool _disposed;
        private IPointer? _capturedPointer;
        private bool _capturePending;
        private readonly bool _setAllowDrop;
        private Canvas? _dropAdorner;
        private Rectangle? _dropIndicator;
        private ContentPresenter? _feedbackPresenter;

        public DataGridRowDragDropController(
            DataGrid grid,
            IDataGridRowDropHandler dropHandler,
            DataGridRowDragDropOptions options)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _dropHandler = dropHandler ?? throw new ArgumentNullException(nameof(dropHandler));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var allowDrop = DragDrop.GetAllowDrop(_grid);
            if (!allowDrop)
            {
                DragDrop.SetAllowDrop(_grid, true);
                _setAllowDrop = true;
            }

            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _autoScrollTimer.Tick += (_, _) => ScrollBy(_autoScrollDirection);

            AttachHandlers();
        }

        public DataGrid Grid => _grid;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DetachHandlers();
            ClearIndicator();
            ClearDraggingRows();
            ResetPointerState();

            if (_setAllowDrop)
            {
                DragDrop.SetAllowDrop(_grid, false);
            }

            HideAdorner();
            _grid.SetActiveRowDragSession(null);
            _autoScrollTimer.Stop();
        }

        private void AttachHandlers()
        {
            _grid.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _grid.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            _grid.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            _grid.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
            _grid.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);
            _grid.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void DetachHandlers()
        {
            _grid.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _grid.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            _grid.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            _grid.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _grid.RemoveHandler(DragDrop.DropEvent, OnDrop);
            _grid.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }

        private bool ShouldHandlePointer(PointerEventArgs e)
        {
            if (!_grid.CanUserReorderRows ||
                _grid.IsReadOnly ||
                !_grid.IsEnabled)
            {
                return false;
            }

            var point = e.GetCurrentPoint(_grid);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return false;
            }

            if (IsScrollBarHit(e.Source) ||
                IsScrollBarHit(_grid.GetVisualAt(point.Position)))
            {
                return false;
            }

            // Don't intercept clicks on expand/collapse toggle buttons (hierarchical expanders)
            if (IsExpanderButtonHit(e.Source) ||
                IsExpanderButtonHit(_grid.GetVisualAt(point.Position)))
            {
                return false;
            }

            if (_grid.EditingRow != null ||
                _grid.DataConnection?.EditableCollectionView?.IsAddingNew == true ||
                _grid.DataConnection?.EditableCollectionView?.IsEditingItem == true)
            {
                return false;
            }

            return true;
        }

        private static bool IsScrollBarHit(object? source)
        {
            return source is Visual visual &&
                   visual.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any();
        }

        private static bool IsExpanderButtonHit(object? source)
        {
            if (source is not Visual visual)
            {
                return false;
            }

            // Check if the click is on a ToggleButton that's part of a DataGridHierarchicalPresenter
            // (the expand/collapse button for hierarchical rows)
            var toggleButton = visual.GetSelfAndVisualAncestors().OfType<ToggleButton>().FirstOrDefault();
            if (toggleButton == null)
            {
                return false;
            }

            // Check if this toggle button is inside a hierarchical presenter (PART_Expander)
            return toggleButton.GetVisualAncestors().OfType<DataGridHierarchicalPresenter>().Any();
        }

        private bool TryGetRowFromEvent(Interactive? source, Point gridPoint, out DataGridRow? row)
        {
            row = null;

            if (source is Control control && TryGetRowFromControl(control, out row, out var header))
            {
                return IsDragHandleSurface(row, header, gridPoint);
            }

            if (source is not Visual visual)
            {
                return false;
            }

            var headerFallback = visual.GetSelfAndVisualAncestors()
                .OfType<DataGridRowHeader>()
                .FirstOrDefault();

            row = headerFallback?.Owner as DataGridRow ?? visual.GetSelfAndVisualAncestors().OfType<DataGridRow>().FirstOrDefault();

            if (row == null || row.OwningGrid != _grid)
            {
                row = null;
                return false;
            }

            return IsDragHandleSurface(row, headerFallback, gridPoint);
        }

        private bool TryGetRowFromPoint(Point point, out DataGridRow? row)
        {
            row = null;
            DataGridRowHeader? header = null;

            var visual = _grid.GetVisualAt(point);
            if (visual is Visual hit)
            {
                header = hit.GetSelfAndVisualAncestors()
                    .OfType<DataGridRowHeader>()
                    .FirstOrDefault();

                row = hit.GetSelfAndVisualAncestors()
                    .OfType<DataGridRow>()
                    .FirstOrDefault(r => r.OwningGrid == _grid);
            }

            var presenter = GetRowsPresenter();
            if ((row == null || row.Index < 0) && presenter != null)
            {
                var presenterPoint = _grid.TranslatePoint(point, presenter) ?? point;
                foreach (var candidate in presenter.Children.OfType<DataGridRow>())
                {
                    if (!candidate.Bounds.Contains(presenterPoint))
                    {
                        continue;
                    }

                    row = candidate;
                    header ??= candidate.GetVisualDescendants().OfType<DataGridRowHeader>().FirstOrDefault();
                    break;
                }

                if (row == null)
                {
                    var presenterHit = presenter.GetVisualAt(presenterPoint);
                    header ??= presenterHit?
                        .GetSelfAndVisualAncestors()
                        .OfType<DataGridRowHeader>()
                        .FirstOrDefault();

                    row = presenterHit?
                        .GetSelfAndVisualAncestors()
                        .OfType<DataGridRow>()
                        .FirstOrDefault(r => r.OwningGrid == _grid);
                }
            }

            if (row == null ||
                ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder))
            {
                row = null;
                return false;
            }

            return IsDragHandleSurface(row, header, point);
        }

        private bool TryGetRowFromControl(Control? control, out DataGridRow? row, out DataGridRowHeader? header)
        {
            row = null;
            header = null;

            if (control == null)
            {
                return false;
            }

            header = control
                .GetSelfAndVisualAncestors()
                .OfType<DataGridRowHeader>()
                .FirstOrDefault(h => h.Owner is DataGridRow r && r.OwningGrid == _grid);

            row = header?.Owner as DataGridRow
                ?? control.GetSelfAndVisualAncestors()
                    .OfType<DataGridRow>()
                    .FirstOrDefault(r => r.OwningGrid == _grid);

            if (row == null || ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder))
            {
                row = null;
                header = null;
                return false;
            }

            header ??= row.GetVisualDescendants().OfType<DataGridRowHeader>().FirstOrDefault();
            return true;
        }

        private bool IsHeaderSurface(DataGridRow row, DataGridRowHeader? header, Point gridPoint)
        {
            if (header != null)
            {
                var origin = header.TranslatePoint(new Point(0, 0), _grid);
                if (origin.HasValue)
                {
                    var headerBounds = new Rect(origin.Value, header.Bounds.Size);
                    if (headerBounds.Contains(gridPoint))
                    {
                        return true;
                    }
                }
            }

            if (_grid.RowDragHandle != DataGridRowDragHandle.RowHeader)
            {
                return false;
            }

            var headerWidth = _grid.ActualRowHeaderWidth;
            if (headerWidth <= 0 && row.HasHeaderCell)
            {
                headerWidth = row.HeaderCell.Bounds.Width;
            }

            if (double.IsNaN(headerWidth) || headerWidth <= 0)
            {
                return false;
            }

            var pointInRow = _grid.TranslatePoint(gridPoint, row);
            if (pointInRow == null)
            {
                return false;
            }

            return pointInRow.Value.X <= headerWidth;
        }

        private bool IsDragHandleSurface(DataGridRow row, DataGridRowHeader? header, Point gridPoint)
        {
            var isHeaderSurface = IsHeaderSurface(row, header, gridPoint);

            return _grid.RowDragHandle switch
            {
                DataGridRowDragHandle.RowHeader => isHeaderSurface,
                DataGridRowDragHandle.Row => !isHeaderSurface,
                _ => true
            };
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (!ShouldHandlePointer(e))
            {
                return;
            }

            var gridPoint = e.GetPosition(_grid);

            if (!TryGetRowFromEvent(e.Source as Interactive, gridPoint, out var row) &&
                !TryGetRowFromPoint(gridPoint, out row))
            {
                return;
            }

            if (ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder))
            {
                return;
            }

            _pointerId = e.Pointer.Id;
            _pointerStart = gridPoint;
            _dragStartRow = row;
            _capturePending = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_pointerId == null || e.Pointer.Id != _pointerId)
            {
                return;
            }

            if (_grid.IsHeaderSelectionDragActive)
            {
                ResetPointerState();
                return;
            }

            var point = e.GetCurrentPoint(_grid);
            if (!point.Properties.IsLeftButtonPressed)
            {
                ResetPointerState();
                return;
            }

            if (_dragInfo != null)
            {
                return;
            }

            var delta = e.GetPosition(_grid) - _pointerStart;
            if (Math.Abs(delta.X) < _options.HorizontalDragThreshold &&
                Math.Abs(delta.Y) < _options.VerticalDragThreshold)
            {
                return;
            }

            if (_capturePending)
            {
                _capturePending = false;
                if (e.Pointer.Captured == null)
                {
                    e.Pointer.Capture(_grid);
                }
                _capturedPointer = e.Pointer;
            }

            StartDragAsync(e);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pointerId == null || e.Pointer.Id != _pointerId)
            {
                return;
            }

            ResetPointerState();
        }

        private void ResetPointerState()
        {
            _pointerId = null;
            _pointerStart = default;
            _dragStartRow = null;
            _dragInfo = null;
            _capturePending = false;
            _capturedPointer?.Capture(null);
            _capturedPointer = null;
        }

        private void StartDragAsync(PointerEventArgs triggerEvent)
        {
            var info = TryCreateDragInfo();
            if (info == null)
            {
                ResetPointerState();
                return;
            }

            var session = new DataGridRowDragSession(_grid, info.Items, info.Indices, info.FromSelection);
            session.SetPointerPosition(triggerEvent.GetPosition(_grid));
            session.SetKeyModifiers(triggerEvent.KeyModifiers);
            session.SetTargetGrid(_grid);
            UpdateSessionEffects(session, _options.AllowedEffects, triggerEvent.KeyModifiers);
            session.SetIsActive(true);
            session.SetResultEffect(DragDropEffects.None);
            session.SetIsCanceled(false);

            info = new DataGridRowDragInfo(_grid, info.Items, info.Indices, info.FromSelection, session);
            _dragInfo = info;
            SetDraggingRows(info.Items, true);

            var data = CreateDataTransfer(info);
            session.SetData(data);

            var startingArgs = new DataGridRowDragStartingEventArgs(info.Items, info.Indices, data, _options.AllowedEffects);
            startingArgs.SetSession(session);
            _grid.OnRowDragStarting(startingArgs);

            if (startingArgs.Cancel || startingArgs.AllowedEffects == DragDropEffects.None)
            {
                session.SetIsActive(false);
                ClearDraggingRows();
                (data as IDisposable)?.Dispose();
                ResetPointerState();
                return;
            }

            UpdateSessionEffects(session, startingArgs.AllowedEffects, triggerEvent.KeyModifiers);
            _grid.SetActiveRowDragSession(session);
            _grid.OnRowDragStarted(new DataGridRowDragStartedEventArgs(session));

#pragma warning disable CS4014
            DoDragAsync(triggerEvent, data, startingArgs.AllowedEffects, info, session);
#pragma warning restore CS4014
        }

        private async System.Threading.Tasks.Task DoDragAsync(
            PointerEventArgs triggerEvent,
            IDataTransfer data,
            DragDropEffects allowedEffects,
            DataGridRowDragInfo info,
            DataGridRowDragSession session)
        {
            var result = await DragDrop.DoDragDropAsync(triggerEvent, data, allowedEffects);
            FinishDrag(info, data, session, result);
        }

        private void FinishDrag(
            DataGridRowDragInfo info,
            IDataTransfer data,
            DataGridRowDragSession session,
            DragDropEffects result)
        {
            session.SetResultEffect(result);
            session.SetIsCanceled(result == DragDropEffects.None);
            session.SetIsActive(false);

            ClearIndicator();
            ClearDraggingRows();
            ResetPointerState();
            HideAdorner();

            if (session.TargetGrid != null &&
                !ReferenceEquals(session.TargetGrid, _grid))
            {
                session.TargetGrid.SetActiveRowDragSession(null);
            }

            _grid.SetActiveRowDragSession(null);

            if (session.IsCanceled)
            {
                _grid.OnRowDragCanceled(new DataGridRowDragCanceledEventArgs(session));
            }
            else
            {
                var completedArgs = new DataGridRowDragCompletedEventArgs(info.Items, result);
                completedArgs.SetSession(session);
                _grid.OnRowDragCompleted(completedArgs);
            }

            (data as IDisposable)?.Dispose();
        }

        private DataGridRowDragInfo? TryCreateDragInfo()
        {
            if (_dragStartRow == null)
            {
                return null;
            }

            var items = new List<object>();
            var indices = new List<int>();
            var added = new HashSet<object?>(ReferenceEqualityComparer.Instance);
            var fromSelection = false;
            var usedSelection = false;
            var isHierarchical = _grid.HierarchicalRowsEnabled && _dragStartRow.DataContext is HierarchicalNode;

            if (_options.DragSelectedRows && _dragStartRow.IsSelected)
            {
                if (isHierarchical && TryAddHierarchicalSelection(items, indices, added))
                {
                    fromSelection = true;
                    usedSelection = true;
                }
                else if (_grid.SelectedItems is IList selected && selected.Count > 0)
                {
                    foreach (var item in selected.Cast<object?>())
                    {
                        if (!TryAddDragItem(item, added, items, indices))
                        {
                            continue;
                        }
                    }

                    if (items.Count > 0)
                    {
                        fromSelection = true;
                        usedSelection = true;
                    }
                }
            }

            if (!usedSelection)
            {
                TryAddDragItem(_dragStartRow.DataContext, added, items, indices);
            }

            if (items.Count == 0)
            {
                return null;
            }

            return new DataGridRowDragInfo(_grid, items, indices, fromSelection);
        }

        private bool TryAddHierarchicalSelection(
            List<object> items,
            List<int> indices,
            HashSet<object?> added)
        {
            var selection = _grid.Selection;
            var dataConnection = _grid.DataConnection;
            if (selection?.SelectedIndexes == null || dataConnection == null)
            {
                return false;
            }

            foreach (var index in selection.SelectedIndexes)
            {
                if (index < 0)
                {
                    continue;
                }

                var item = dataConnection.GetDataItem(index);
                if (!TryAddDragItem(item, added, items, indices, index))
                {
                    continue;
                }
            }

            return items.Count > 0;
        }

        private bool TryAddDragItem(
            object? item,
            HashSet<object?> added,
            List<object> items,
            List<int> indices,
            int? knownIndex = null)
        {
            if (item == null || ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
            {
                return false;
            }

            if (!added.Add(item))
            {
                return false;
            }

            var index = knownIndex ?? ResolveIndex(item);
            if (index < 0)
            {
                return false;
            }

            items.Add(item);
            indices.Add(index);
            return true;
        }

        private int ResolveIndex(object item)
        {
            // Preferred: data connection lookup.
            var index = _grid.DataConnection?.IndexOf(item) ?? -1;
            if (index >= 0)
            {
                return index;
            }

            // Row lookup (realized row).
            var row = _grid.GetRowFromItem(item);
            if (row != null)
            {
                if (row.Index >= 0)
                {
                    return row.Index;
                }

                if (row.Slot >= 0)
                {
                    var slotIndex = _grid.RowIndexFromSlot(row.Slot);
                    if (slotIndex >= 0)
                    {
                        return slotIndex;
                    }
                }
            }

            // IList fallback (reference equality).
            var list = _grid.DataConnection?.List ?? _grid.ItemsSource as IList;
            if (list != null)
            {
                var listIndex = list.IndexOf(item);
                if (listIndex >= 0)
                {
                    return listIndex;
                }
            }

            // Linear scan fallback using reference equality to avoid Equals overrides.
            var items = _grid.ItemsSource as IEnumerable;
            if (items != null)
            {
                var i = 0;
                foreach (var candidate in items)
                {
                    if (ReferenceEquals(candidate, item))
                    {
                        return i;
                    }

                    i++;
                }
            }

            return -1;
        }

        private static IDataTransfer CreateDataTransfer(DataGridRowDragInfo info)
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DataGridRowDragInfo.DataTransferFormat, info));
            return data;
        }

        private static DragDropEffects GetRequestedEffect(DataGridRowDragDropOptions options, KeyModifiers modifiers)
        {
            return GetRequestedEffect(options.AllowedEffects, modifiers);
        }

        private static DragDropEffects GetRequestedEffect(DragDropEffects allowed, KeyModifiers modifiers)
        {
            if (allowed.HasFlag(DragDropEffects.Copy) && modifiers.HasFlag(KeyModifiers.Control))
            {
                return DragDropEffects.Copy;
            }

            if (allowed.HasFlag(DragDropEffects.Move))
            {
                return DragDropEffects.Move;
            }

            return allowed;
        }

        private static DragDropEffects CoerceEffect(DragDropEffects allowedEffects, DragDropEffects effect)
        {
            if (effect == DragDropEffects.None || allowedEffects == DragDropEffects.None)
            {
                return DragDropEffects.None;
            }

            var coerced = effect & allowedEffects;
            return coerced == 0 ? DragDropEffects.None : coerced;
        }

        private void UpdateSessionState(
            DataGridRowDragSession session,
            DragEventArgs dragEventArgs,
            DataGridRow? hoveredRow,
            object? hoveredItem,
            DataGridRowDropEventArgs? dropArgs,
            DragDropEffects suggestedEffect)
        {
            session.SetTargetGrid(_grid);
            session.SetPointerPosition(dragEventArgs.GetPosition(_grid));
            session.SetKeyModifiers(dragEventArgs.KeyModifiers);
            session.SetHoveredState(hoveredRow ?? dropArgs?.TargetRow, hoveredItem ?? dropArgs?.TargetItem);
            session.SetRequestedEffect(suggestedEffect);
            session.EffectiveEffect = suggestedEffect;
            session.SetTargetState(
                dropArgs?.TargetRow,
                dropArgs?.TargetItem,
                dropArgs?.TargetIndex ?? GetMaxInsertIndex(),
                dropArgs?.InsertIndex ?? GetMaxInsertIndex(),
                dropArgs?.Position);
        }

        private static void UpdateSessionEffects(
            DataGridRowDragSession session,
            DragDropEffects allowedEffects,
            KeyModifiers modifiers)
        {
            session.SetAllowedEffects(allowedEffects);

            var requestedEffect = GetRequestedEffect(allowedEffects, modifiers);
            session.SetRequestedEffect(requestedEffect);
            session.EffectiveEffect = CoerceEffect(allowedEffects, requestedEffect);
        }

        private bool TryPrepareSessionUpdate(
            DataGridRowDragInfo dragInfo,
            DragEventArgs dragEventArgs,
            out DataGridRowDragSession session,
            out DataGridRowDropEventArgs? dropArgs)
        {
            session = dragInfo.Session ?? new DataGridRowDragSession(dragInfo.Grid, dragInfo.Items, dragInfo.Indices, dragInfo.FromSelection);
            session.SetAllowedEffects(session.AllowedEffects == DragDropEffects.None ? _options.AllowedEffects : session.AllowedEffects);

            _grid.SetActiveRowDragSession(session);

            var hoveredRow = GetHoveredRow(dragEventArgs);
            var hoveredItem = hoveredRow?.DataContext;
            var isPlaceholder = IsPlaceholderRow(hoveredRow);
            var suggestedEffect = GetRequestedEffect(session.AllowedEffects, dragEventArgs.KeyModifiers);

            dropArgs = isPlaceholder ? null : CreateDropArgs(dragInfo, dragEventArgs, suggestedEffect);
            if (dropArgs != null)
            {
                dropArgs.SetSession(session);
            }

            UpdateSessionState(session, dragEventArgs, hoveredRow, hoveredItem, dropArgs, suggestedEffect);

            var valid = !isPlaceholder && dropArgs != null && _dropHandler.Validate(dropArgs);
            if (!valid)
            {
                session.EffectiveEffect = DragDropEffects.None;
            }

            session.EffectiveEffect = CoerceEffect(session.AllowedEffects, session.EffectiveEffect);
            session.SetIsValidTarget(valid && session.EffectiveEffect != DragDropEffects.None);

            if (dropArgs != null)
            {
                dropArgs.EffectiveEffect = session.EffectiveEffect;
            }

            _grid.OnRowDragUpdated(new DataGridRowDragUpdatedEventArgs(session, dragEventArgs));

            return session.IsValidTarget;
        }

        private void ClearSessionTarget(DataGridRowDragSession session, DragEventArgs? dragEventArgs)
        {
            if (dragEventArgs != null)
            {
                session.SetPointerPosition(dragEventArgs.GetPosition(_grid));
                session.SetKeyModifiers(dragEventArgs.KeyModifiers);
            }

            session.SetTargetGrid(null);
            session.SetHoveredState(null, null);
            session.SetTargetState(null, null, GetMaxInsertIndex(), GetMaxInsertIndex(), null);
            session.EffectiveEffect = DragDropEffects.None;
            session.SetIsValidTarget(false);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            var dragInfo = GetDragInfo(e);
            if (dragInfo == null)
            {
                ClearIndicator();
                _grid.SetActiveRowDragSession(null);
                return;
            }

            var valid = TryPrepareSessionUpdate(dragInfo, e, out var session, out var dropArgs);
            e.DragEffects = session.EffectiveEffect;

            if (valid && dropArgs != null)
            {
                UpdateIndicator(dropArgs.TargetRow, dropArgs.Position);
                AutoScrollIfNeeded(e);
            }
            else
            {
                ClearIndicator();
            }

            UpdateFeedbackAdorner(session);
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            var dragInfo = GetDragInfo(e);
            if (dragInfo == null)
            {
                ClearIndicator();
                _grid.SetActiveRowDragSession(null);
                return;
            }

            var valid = TryPrepareSessionUpdate(dragInfo, e, out var session, out var dropArgs);
            var executed = valid && dropArgs != null && _dropHandler.Execute(dropArgs);
            e.DragEffects = executed ? dropArgs.EffectiveEffect : DragDropEffects.None;

            if (executed && dropArgs != null)
            {
                UpdateSelectionAfterDrop(dropArgs);
            }

            ClearIndicator();
            ClearDraggingRows();
            UpdateFeedbackAdorner(null);

            if (!ReferenceEquals(session.SourceGrid, _grid))
            {
                _grid.SetActiveRowDragSession(null);
            }
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            ClearIndicator();

            var dragInfo = GetDragInfo(e);
            if (dragInfo?.Session is { } session)
            {
                ClearSessionTarget(session, e);
                UpdateFeedbackAdorner(ReferenceEquals(session.SourceGrid, _grid) ? session : null);

                if (!ReferenceEquals(session.SourceGrid, _grid))
                {
                    _grid.SetActiveRowDragSession(null);
                }
            }
            else if (_grid.ActiveRowDragSession != null)
            {
                UpdateFeedbackAdorner(null);
            }
        }

        private static DataGridRowDragInfo? GetDragInfo(DragEventArgs e)
        {
            var transfer = e.DataTransfer ?? e.Data as IDataTransfer;
            if (transfer != null)
            {
                foreach (var item in transfer.Items)
                {
                    var raw = item.TryGetRaw(DataGridRowDragInfo.DataTransferFormat);
                    if (raw is DataGridRowDragInfo info)
                    {
                        return info;
                    }
                }
            }

#pragma warning disable CS0618
            if (e.Data is IDataObject dataObject &&
                dataObject.Contains(DataGridRowDragInfo.DataFormat))
            {
                return dataObject.Get(DataGridRowDragInfo.DataFormat) as DataGridRowDragInfo;
            }
#pragma warning restore CS0618

            return null;
        }

        private DataGridRow? GetHoveredRow(DragEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var visual = _grid.GetVisualAt(position);
            return visual?
                .GetSelfAndVisualAncestors()
                .OfType<DataGridRow>()
                .FirstOrDefault(r => r.OwningGrid == _grid);
        }

        private static bool IsPlaceholderRow(DataGridRow? row)
        {
            return row != null && ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder);
        }

        private int GetMaxInsertIndex()
        {
            if (_grid.DataConnection == null)
            {
                return 0;
            }

            var count = _grid.DataConnection.Count;
            if (_grid.DataConnection.EditableCollectionView?.CanAddNew == true && _grid.CanUserAddRows)
            {
                count = Math.Max(0, count - 1);
            }

            return count;
        }

        private DataGridRowDropEventArgs? CreateDropArgs(
            DataGridRowDragInfo dragInfo,
            DragEventArgs dragEventArgs,
            DragDropEffects requestedEffect)
        {
            var row = GetHoveredRow(dragEventArgs);
            if (IsPlaceholderRow(row))
            {
                return null;
            }

            var presenter = GetRowsPresenter();
            var position = DataGridRowDropPosition.Before;
            var targetIndex = GetMaxInsertIndex();
            var insertIndex = targetIndex;
            object? targetItem = null;

            if (row == null && presenter != null)
            {
                row = ResolveRowFromPresenterHit(presenter, dragEventArgs.GetPosition(presenter), out position, out targetIndex) ?? row;
                insertIndex = targetIndex;
            }

            if (row == null && presenter != null)
            {
                row = ResolveRowFromPresenterEdge(presenter, dragEventArgs.GetPosition(presenter), out position, out targetIndex) ?? row;
                insertIndex = targetIndex;
            }

            if (row != null)
            {
                var relative = dragEventArgs.GetPosition(row);
                var isHierarchical = _grid.HierarchicalRowsEnabled && row.DataContext is HierarchicalNode;

                if (isHierarchical && row.Bounds.Height > 0)
                {
                    var ratio = relative.Y / row.Bounds.Height;
                    position = ratio switch
                    {
                        < 0.33 => DataGridRowDropPosition.Before,
                        > 0.66 => DataGridRowDropPosition.After,
                        _ => DataGridRowDropPosition.Inside
                    };
                }
                else
                {
                    position = relative.Y > (row.Bounds.Height / 2) ? DataGridRowDropPosition.After : DataGridRowDropPosition.Before;
                }

                targetIndex = row.Index + (position == DataGridRowDropPosition.After ? 1 : 0);
                insertIndex = targetIndex;

                if (position == DataGridRowDropPosition.Inside && row.DataContext is HierarchicalNode parentNode)
                {
                    insertIndex = parentNode.MutableChildren.Count;
                }

                targetItem = row.DataContext;
            }
            else
            {
                targetIndex = GetMaxInsertIndex();
                insertIndex = targetIndex;
            }

            targetIndex = Math.Min(targetIndex, GetMaxInsertIndex());
            if (position != DataGridRowDropPosition.Inside)
            {
                insertIndex = Math.Min(insertIndex, GetMaxInsertIndex());
            }

            if (targetItem != null && dragInfo.Items.Contains(targetItem))
            {
                targetItem = null;
            }

            var list = _grid.DataConnection?.List ?? _grid.ItemsSource as IList;

            return new DataGridRowDropEventArgs(
                _grid,
                list,
                dragInfo.Items,
                dragInfo.Indices,
                targetItem,
                row?.Index ?? targetIndex,
                insertIndex,
                row,
                position,
                ReferenceEquals(dragInfo.Grid, _grid),
                requestedEffect,
                dragEventArgs);
        }

        private DataGridRowsPresenter? GetRowsPresenter()
        {
            return _grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().FirstOrDefault();
        }

        private DataGridRow? ResolveRowFromPresenterEdge(
            DataGridRowsPresenter presenter,
            Point position,
            out DataGridRowDropPosition positionHint,
            out int targetIndex)
        {
            positionHint = DataGridRowDropPosition.After;
            targetIndex = GetMaxInsertIndex();

            var rows = presenter
                .GetVisualDescendants()
                .OfType<DataGridRow>()
                .Where(r => r.OwningGrid == _grid && !r.IsPlaceholder && r.Index >= 0)
                .OrderBy(r => r.Index)
                .ToList();

            if (rows.Count == 0)
            {
                return null;
            }

            var first = rows[0];
            var last = rows[rows.Count - 1];

            var firstTop = first.TranslatePoint(new Point(0, 0), presenter)?.Y;
            var lastBottom = last.TranslatePoint(new Point(0, last.Bounds.Height), presenter)?.Y;

            if (firstTop.HasValue && position.Y < firstTop.Value)
            {
                positionHint = DataGridRowDropPosition.Before;
                targetIndex = first.Index;
                return first;
            }

            if (lastBottom.HasValue && position.Y > lastBottom.Value)
            {
                positionHint = DataGridRowDropPosition.After;
                targetIndex = last.Index + 1;
                return last;
            }

            return null;
        }

        private DataGridRow? ResolveRowFromPresenterHit(
            DataGridRowsPresenter presenter,
            Point position,
            out DataGridRowDropPosition positionHint,
            out int targetIndex)
        {
            positionHint = DataGridRowDropPosition.Before;
            targetIndex = GetMaxInsertIndex();

            var rows = presenter
                .GetVisualDescendants()
                .OfType<DataGridRow>()
                .Where(r => r.OwningGrid == _grid && !r.IsPlaceholder && r.Index >= 0)
                .OrderBy(r => r.Index)
                .ToList();

            foreach (var row in rows)
            {
                var top = row.TranslatePoint(new Point(0, 0), presenter)?.Y;
                var bottom = row.TranslatePoint(new Point(0, row.Bounds.Height), presenter)?.Y;

                if (!top.HasValue || !bottom.HasValue)
                {
                    continue;
                }

                if (position.Y >= top.Value && position.Y <= bottom.Value)
                {
                    var midpoint = (top.Value + bottom.Value) / 2;
                    positionHint = position.Y > midpoint ? DataGridRowDropPosition.After : DataGridRowDropPosition.Before;
                    targetIndex = row.Index + (positionHint == DataGridRowDropPosition.After ? 1 : 0);
                    return row;
                }
            }

            return null;
        }

        private void AutoScrollIfNeeded(DragEventArgs e)
        {
            if (_grid.DisplayData == null || _grid.DataConnection == null)
            {
                return;
            }

            const double edgeThreshold = 60;
            var reference = (Control?)GetRowsPresenter() ?? _grid;
            var position = e.GetPosition(reference);
            var height = reference.Bounds.Height;
            if (height <= 0)
            {
                return;
            }

            if (position.Y < edgeThreshold)
            {
                StartAutoScroll(-1);
            }
            else if (position.Y > height - edgeThreshold)
            {
                StartAutoScroll(1);
            }
            else
            {
                StopAutoScroll();
            }
        }

        private void ScrollBy(int direction)
        {
            var scroller = _grid.ScrollViewer;
            if (scroller != null)
            {
                if (direction < 0)
                {
                    scroller.LineUp();
                }
                else if (direction > 0)
                {
                    scroller.LineDown();
                }

                return;
            }

            if (_grid.DisplayData == null ||
                _grid.DataConnection == null ||
                _grid.DisplayData.NumDisplayedScrollingElements == 0)
            {
                return;
            }

            var slot = direction < 0
                ? _grid.GetPreviousVisibleSlot(_grid.DisplayData.FirstScrollingSlot)
                : _grid.GetNextVisibleSlot(_grid.DisplayData.LastScrollingSlot);

            if (slot < 0 || slot >= _grid.SlotCount)
            {
                return;
            }

            var rowIndex = _grid.RowIndexFromSlot(slot);
            if (rowIndex < 0 || rowIndex >= _grid.DataConnection.Count)
            {
                return;
            }

            var item = _grid.DataConnection.GetDataItem(rowIndex);
            _grid.ScrollIntoView(item, null);
        }

        private void StartAutoScroll(int direction)
        {
            _autoScrollDirection = direction;
            if (!_autoScrollTimer.IsEnabled)
            {
                _autoScrollTimer.Start();
            }
        }

        private void StopAutoScroll()
        {
            _autoScrollTimer.Stop();
            _autoScrollDirection = 0;
        }

        private Canvas? GetOrCreateAdorner()
        {
            if (_dropAdorner != null)
            {
                return _dropAdorner;
            }

            var adornerLayer = AdornerLayer.GetAdornerLayer(_grid);
            if (adornerLayer == null)
            {
                return null;
            }

            IBrush stroke = Brushes.DodgerBlue;
            if (_grid.TryFindResource("DataGridRowDropIndicatorBrush", out var resource) && resource is IBrush brush)
            {
                stroke = brush;
            }

            _dropIndicator = new Rectangle
            {
                Stroke = stroke,
                StrokeThickness = 3,
                RadiusX = 2,
                RadiusY = 2,
                IsVisible = false
            };

            _feedbackPresenter = new ContentPresenter
            {
                IsHitTestVisible = false,
                IsVisible = false
            };

            _dropAdorner = new Canvas
            {
                Children =
                {
                    _dropIndicator,
                    _feedbackPresenter
                },
                IsHitTestVisible = false
            };

            adornerLayer.Children.Add(_dropAdorner);
            AdornerLayer.SetAdornedElement(_dropAdorner, _grid);
            return _dropAdorner;
        }

        private void UpdateIndicatorAdorner(DataGridRow? row, DataGridRowDropPosition? position)
        {
            var adorner = GetOrCreateAdorner();
            if (adorner == null || _dropIndicator == null)
            {
                return;
            }

            if (row == null || position == null || row.TransformToVisual(_grid) is not { } transform)
            {
                _dropIndicator.IsVisible = false;
                return;
            }

            var rowBounds = new Rect(row.Bounds.Size).TransformToAABB(transform);

            _dropIndicator.IsVisible = true;
            Canvas.SetLeft(_dropIndicator, rowBounds.Left);
            _dropIndicator.Width = rowBounds.Width;

            switch (position)
            {
                case DataGridRowDropPosition.Before:
                    Canvas.SetTop(_dropIndicator, rowBounds.Top);
                    _dropIndicator.Height = 0;
                    break;
                case DataGridRowDropPosition.After:
                    Canvas.SetTop(_dropIndicator, rowBounds.Bottom);
                    _dropIndicator.Height = 0;
                    break;
                case DataGridRowDropPosition.Inside:
                    Canvas.SetTop(_dropIndicator, rowBounds.Top);
                    _dropIndicator.Height = rowBounds.Height;
                    break;
            }
        }

        private void UpdateFeedbackAdorner(DataGridRowDragSession? session)
        {
            if (_feedbackPresenter == null && session == null)
            {
                return;
            }

            if (session == null || _grid.RowDragFeedbackTemplate == null)
            {
                if (_feedbackPresenter != null)
                {
                    _feedbackPresenter.IsVisible = false;
                    _feedbackPresenter.Content = null;
                    _feedbackPresenter.ContentTemplate = null;
                }

                if (_indicatorRow == null)
                {
                    HideAdorner();
                }

                return;
            }

            var adorner = GetOrCreateAdorner();
            if (adorner == null || _feedbackPresenter == null)
            {
                return;
            }

            var offset = _grid.RowDragFeedbackOffset;
            _feedbackPresenter.Content = session;
            _feedbackPresenter.ContentTemplate = _grid.RowDragFeedbackTemplate;
            _feedbackPresenter.IsVisible = true;
            Canvas.SetLeft(_feedbackPresenter, session.PointerPosition.X + offset.X);
            Canvas.SetTop(_feedbackPresenter, session.PointerPosition.Y + offset.Y);
            _feedbackPresenter.InvalidateMeasure();
        }

        private void HideAdorner()
        {
            if (_dropAdorner?.Parent is AdornerLayer layer)
            {
                layer.Children.Remove(_dropAdorner);
            }

            _dropAdorner = null;
            _dropIndicator = null;
            _feedbackPresenter = null;
        }

        private void UpdateIndicator(DataGridRow? row, DataGridRowDropPosition position)
        {
            if (_indicatorRow == row && _indicatorPosition == position)
            {
                return;
            }

            ClearIndicator();

            _indicatorRow = row;
            _indicatorPosition = position;

            if (_indicatorRow != null)
            {
                _indicatorRow.SetDropPosition(position);
            }

            UpdateIndicatorAdorner(_indicatorRow, _indicatorPosition);
        }

        private void ClearIndicator()
        {
            if (_indicatorRow != null)
            {
                _indicatorRow.SetDropPosition(null);
            }

            _indicatorRow = null;
            _indicatorPosition = null;

            if (_dropIndicator != null)
            {
                _dropIndicator.IsVisible = false;
            }

            if (_feedbackPresenter?.IsVisible != true)
            {
                HideAdorner();
            }

            StopAutoScroll();
        }

        private void SetDraggingRows(IReadOnlyList<object> items, bool dragging)
        {
            if (items.Count == 0)
            {
                return;
            }

            foreach (var item in items)
            {
                var row = _grid.GetRowFromItem(item);
                if (row == null)
                {
                    continue;
                }

                row.SetDragging(dragging);

                if (dragging)
                {
                    if (!_draggingRows.Contains(row))
                    {
                        _draggingRows.Add(row);
                    }
                }
                else
                {
                    _draggingRows.Remove(row);
                }
            }
        }

        private void ClearDraggingRows()
        {
            foreach (var row in _draggingRows)
            {
                row.SetDragging(false);
            }

            _draggingRows.Clear();
        }

        private void UpdateSelectionAfterDrop(DataGridRowDropEventArgs args)
        {
            if (args.Items.Count == 0)
            {
                return;
            }

            var grid = args.Grid;
            var dataConnection = grid.DataConnection;
            var targetList = args.TargetList ?? dataConnection?.List;
            var candidates = new List<(int Index, object Item)>();

            foreach (var item in args.Items)
            {
                if (item == null || ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
                {
                    continue;
                }

                var index = dataConnection?.IndexOf(item) ?? -1;
                if (index < 0 && targetList != null)
                {
                    index = targetList.IndexOf(item);
                }

                if (index >= 0)
                {
                    candidates.Add((index, item));
                }
            }

            if (candidates.Count > 0)
            {
                candidates.Sort((x, y) => x.Index.CompareTo(y.Index));
                SelectItems(grid, candidates.Select(x => x.Item));
                return;
            }

            if (targetList == null || targetList.Count == 0)
            {
                return;
            }

            var targetCount = targetList.Count;
            var startIndex = MathUtilities.Clamp(args.InsertIndex, 0, targetCount - 1);
            var itemsToSelect = new List<object>();

            for (var i = 0; i < args.Items.Count; i++)
            {
                var index = startIndex + i;
                if (index >= targetCount)
                {
                    break;
                }

                var item = targetList[index];
                if (item == null || ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
                {
                    continue;
                }

                itemsToSelect.Add(item);
            }

            SelectItems(grid, itemsToSelect);
        }

        private static void SelectItems(DataGrid grid, IEnumerable<object> items)
        {
            if (grid.SelectionMode == DataGridSelectionMode.Single)
            {
                var first = items.FirstOrDefault();
                if (first != null)
                {
                    grid.SelectedItem = first;
                    grid.ScrollIntoView(first, null);
                }
                return;
            }

            var selection = items.ToList();
            if (selection.Count == 0)
            {
                return;
            }

            grid.SelectedItems.Clear();
            foreach (var item in selection)
            {
                grid.SelectedItems.Add(item);
            }

            grid.ScrollIntoView(selection[0], null);
        }
    }
}
