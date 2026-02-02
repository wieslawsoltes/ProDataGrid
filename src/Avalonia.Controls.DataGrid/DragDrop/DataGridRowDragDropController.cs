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
        private bool _hideDropAdorner;

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

            HideDropAdorner();
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
                return _grid.RowDragHandle switch
                {
                    DataGridRowDragHandle.RowHeader => IsHeaderSurface(row, header, gridPoint),
                    DataGridRowDragHandle.Row => true,
                    _ => true
                };
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

            return _grid.RowDragHandle switch
            {
                DataGridRowDragHandle.RowHeader => IsHeaderSurface(row, headerFallback, gridPoint),
                DataGridRowDragHandle.Row => true,
                _ => true
            };
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

            return _grid.RowDragHandle switch
            {
                DataGridRowDragHandle.RowHeader => IsHeaderSurface(row, header, point),
                DataGridRowDragHandle.Row => true,
                _ => true
            };
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

            _dragInfo = info;
            SetDraggingRows(info.Items, true);

            var data = CreateDataTransfer(info);
            var startingArgs = new DataGridRowDragStartingEventArgs(info.Items, info.Indices, data, _options.AllowedEffects);
            _grid.OnRowDragStarting(startingArgs);

            if (startingArgs.Cancel)
            {
                ClearDraggingRows();
                (data as IDisposable)?.Dispose();
                ResetPointerState();
                return;
            }

#pragma warning disable CS4014
            DoDragAsync(triggerEvent, data, startingArgs.AllowedEffects, info);
#pragma warning restore CS4014
        }

        private async System.Threading.Tasks.Task DoDragAsync(
            PointerEventArgs triggerEvent,
            IDataTransfer data,
            DragDropEffects allowedEffects,
            DataGridRowDragInfo info)
        {
            var result = await DragDrop.DoDragDropAsync(triggerEvent, data, allowedEffects);
            _grid.OnRowDragCompleted(new DataGridRowDragCompletedEventArgs(info.Items, result));

            ClearIndicator();
            ClearDraggingRows();
            ResetPointerState();

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
            var allowed = options.AllowedEffects;
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

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            var dragInfo = GetDragInfo(e);
            if (dragInfo == null)
            {
                ClearIndicator();
                return;
            }

            if (IsOverPlaceholderRow(e))
            {
                ClearIndicator();
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var requestedEffect = GetRequestedEffect(_options, e.KeyModifiers);
            var dropArgs = CreateDropArgs(dragInfo, e, requestedEffect);
            if (dropArgs == null || !_dropHandler.Validate(dropArgs))
            {
                ClearIndicator();
                e.DragEffects = DragDropEffects.None;
                return;
            }

            e.DragEffects = dropArgs.RequestedEffect;
            UpdateIndicator(dropArgs.TargetRow, dropArgs.Position);
            AutoScrollIfNeeded(e);
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            var dragInfo = GetDragInfo(e);
            if (dragInfo == null)
            {
                ClearIndicator();
                return;
            }

            if (IsOverPlaceholderRow(e))
            {
                ClearIndicator();
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var requestedEffect = GetRequestedEffect(_options, e.KeyModifiers);
            var dropArgs = CreateDropArgs(dragInfo, e, requestedEffect);
            if (dropArgs == null)
            {
                ClearIndicator();
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var valid = _dropHandler.Validate(dropArgs);
            var executed = valid && _dropHandler.Execute(dropArgs);
            e.DragEffects = executed ? dropArgs.RequestedEffect : DragDropEffects.None;

            if (executed)
            {
                UpdateSelectionAfterDrop(dropArgs);
            }

            ClearIndicator();
            ClearDraggingRows();
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            ClearIndicator();
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

        private DataGridRow? GetRowFromEvent(DragEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var visual = _grid.GetVisualAt(position);
            var row = visual?
                .GetSelfAndVisualAncestors()
                .OfType<DataGridRow>()
                .FirstOrDefault(r => r.OwningGrid == _grid);

            return row != null && ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder)
                ? null
                : row;
        }

        private bool IsOverPlaceholderRow(DragEventArgs e)
        {
            var position = e.GetPosition(_grid);
            var visual = _grid.GetVisualAt(position);
            var row = visual?
                .GetSelfAndVisualAncestors()
                .OfType<DataGridRow>()
                .FirstOrDefault(r => r.OwningGrid == _grid);

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
            var row = GetRowFromEvent(dragEventArgs);
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

        private Canvas? GetOrCreateDropAdorner()
        {
            _hideDropAdorner = false;

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

            _dropAdorner = new Canvas
            {
                Children =
                {
                    new Rectangle
                    {
                        Stroke = stroke,
                        StrokeThickness = 3,
                        RadiusX = 2,
                        RadiusY = 2
                    }
                },
                IsHitTestVisible = false
            };

            adornerLayer.Children.Add(_dropAdorner);
            AdornerLayer.SetAdornedElement(_dropAdorner, _grid);
            return _dropAdorner;
        }

        private void ShowDropAdorner(DataGridRow? row, DataGridRowDropPosition? position)
        {
            if (row == null || position == null || row.TransformToVisual(_grid) is not { } transform)
            {
                HideDropAdorner();
                return;
            }

            var adorner = GetOrCreateDropAdorner();
            if (adorner == null)
            {
                return;
            }

            var rectangle = (Rectangle)adorner.Children[0];
            var rowBounds = new Rect(row.Bounds.Size).TransformToAABB(transform);

            Canvas.SetLeft(rectangle, rowBounds.Left);
            rectangle.Width = rowBounds.Width;

            switch (position)
            {
                case DataGridRowDropPosition.Before:
                    Canvas.SetTop(rectangle, rowBounds.Top);
                    rectangle.Height = 0;
                    break;
                case DataGridRowDropPosition.After:
                    Canvas.SetTop(rectangle, rowBounds.Bottom);
                    rectangle.Height = 0;
                    break;
                case DataGridRowDropPosition.Inside:
                    Canvas.SetTop(rectangle, rowBounds.Top);
                    rectangle.Height = rowBounds.Height;
                    break;
            }
        }

        private void HideDropAdorner()
        {
            _hideDropAdorner = true;

            DispatcherTimer.RunOnce(() =>
            {
                if (_hideDropAdorner && _dropAdorner?.Parent is AdornerLayer layer)
                {
                    layer.Children.Remove(_dropAdorner);
                    _dropAdorner = null;
                }
            }, TimeSpan.FromMilliseconds(50));
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

            ShowDropAdorner(_indicatorRow, _indicatorPosition);
        }

        private void ClearIndicator()
        {
            if (_indicatorRow != null)
            {
                _indicatorRow.SetDropPosition(null);
            }

            _indicatorRow = null;
            _indicatorPosition = null;

            HideDropAdorner();
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
