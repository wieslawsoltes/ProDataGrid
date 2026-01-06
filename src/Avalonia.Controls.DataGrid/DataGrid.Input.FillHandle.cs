// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia;
using Avalonia.Controls.DataGridInteractions;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
        private bool _isFillHandleDragging;
        private IPointer _fillPointer;
        private int? _fillPointerId;
        private DataGridCellRange _fillSourceRange;
        private DataGridCellRange _fillDragRange;
        private DispatcherTimer _fillAutoScrollTimer;
        private int _fillAutoScrollDirectionX;
        private int _fillAutoScrollDirectionY;
        private Point? _fillLastPoint;

        private void FillHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            BeginFillHandleDrag(e, ignoreHandled: false);
        }

        private void DataGrid_FillHandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_fillHandle == null || SelectionUnit == DataGridSelectionUnit.FullRow || !IsEnabled)
            {
                return;
            }

            if (!IsPointerOverFillHandle(e))
            {
                return;
            }

            BeginFillHandleDrag(e, ignoreHandled: true);
        }

        private void BeginFillHandleDrag(PointerPressedEventArgs e, bool ignoreHandled)
        {
            if ((!ignoreHandled && e.Handled) || SelectionUnit == DataGridSelectionUnit.FullRow || !IsEnabled)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (!TryGetSelectedCellRange(out var range))
            {
                return;
            }

            _isFillHandleDragging = true;
            _fillPointer = e.Pointer;
            _fillPointerId = e.Pointer.Id;
            _fillSourceRange = range;
            _fillDragRange = range;
            _fillLastPoint = e.GetPosition(this);
            StopFillAutoScroll();
            var captureTarget = (IInputElement?)_fillHandle ?? this;
            _fillPointer.Capture(captureTarget);
            e.Handled = true;
        }

        private void DataGrid_FillHandlePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isFillHandleDragging)
            {
                return;
            }

            FillHandle_PointerMoved(_fillHandle, e);
        }

        private void DataGrid_FillHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isFillHandleDragging)
            {
                return;
            }

            FillHandle_PointerReleased(_fillHandle, e);
        }

        private void DataGrid_FillHandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (!_isFillHandleDragging)
            {
                return;
            }

            FillHandle_PointerCaptureLost(_fillHandle, e);
        }

        private void FillHandle_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isFillHandleDragging || _fillPointerId != e.Pointer.Id)
            {
                return;
            }

            var position = e.GetPosition(this);
            _fillLastPoint = position;
            UpdateFillAutoScroll(position);

            if (UpdateFillDrag(position, e))
            {
                e.Handled = true;
            }
        }

        private void FillHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isFillHandleDragging || _fillPointerId != e.Pointer.Id)
            {
                return;
            }

            EndFillHandleDrag(applyFill: true);
            e.Handled = true;
        }

        private void FillHandle_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (!_isFillHandleDragging || _fillPointerId != e.Pointer.Id)
            {
                return;
            }

            EndFillHandleDrag(applyFill: false);
        }

        private void EndFillHandleDrag(bool applyFill)
        {
            var sourceRange = _fillSourceRange;
            var targetRange = _fillDragRange;

            if (_fillPointer != null && (ReferenceEquals(_fillPointer.Captured, this) || ReferenceEquals(_fillPointer.Captured, _fillHandle)))
            {
                _fillPointer.Capture(null);
            }

            _fillPointer = null;
            _fillPointerId = null;
            _isFillHandleDragging = false;
            _fillLastPoint = null;
            StopFillAutoScroll();

            if (applyFill)
            {
                ApplyFillModel(sourceRange, targetRange);
            }
        }

        private bool IsPointerOverFillHandle(PointerEventArgs e)
        {
            if (_fillHandle == null || !_selectionOverlayVisible || !_fillHandle.IsVisible)
            {
                return false;
            }

            var topLeft = _fillHandle.TranslatePoint(default, this);
            if (topLeft == null)
            {
                return false;
            }

            var rect = new Rect(topLeft.Value, _fillHandle.Bounds.Size);
            return rect.Contains(e.GetPosition(this));
        }

        private bool UpdateFillDrag(Point position, RoutedEventArgs triggerEventArgs)
        {
            if (!TryGetDragCellTarget(position, out var slot, out var columnIndex))
            {
                return false;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return false;
            }

            var targetCell = new DataGridCellPosition(rowIndex, columnIndex);
            var model = RangeInteractionModel;
            var targetRange = model != null
                ? model.BuildFillHandleRange(new DataGridFillHandleRangeContext(this, _fillSourceRange, targetCell))
                : GetFillTargetRange(_fillSourceRange, rowIndex, columnIndex);
            if (RangesEqual(_fillDragRange, targetRange))
            {
                return false;
            }

            _fillDragRange = targetRange;
            ApplyCellSelectionRange(targetRange, append: false, DataGridSelectionChangeSource.Pointer, triggerEventArgs);
            return true;
        }

        private void UpdateFillAutoScroll(Point point)
        {
            if (!_isFillHandleDragging)
            {
                return;
            }

            var model = RangeInteractionModel;
            DataGridAutoScrollDirection direction;
            if (model != null)
            {
                var presenterPoint = _rowsPresenter != null ? this.TranslatePoint(point, _rowsPresenter) : null;
                var presenterSize = _rowsPresenter?.Bounds.Size ?? default;
                direction = model.GetAutoScrollDirection(new DataGridAutoScrollContext(
                    this,
                    point,
                    presenterPoint,
                    presenterSize,
                    ActualRowHeaderWidth,
                    CellsWidth,
                    isRowSelection: false));
            }
            else
            {
                var verticalDirection = 0;
                var horizontalDirection = 0;

                if (_rowsPresenter != null)
                {
                    var presenterPoint = this.TranslatePoint(point, _rowsPresenter) ?? point;
                    var presenterHeight = _rowsPresenter.Bounds.Height;
                    if (presenterPoint.Y < 0)
                    {
                        verticalDirection = -1;
                    }
                    else if (presenterPoint.Y > presenterHeight)
                    {
                        verticalDirection = 1;
                    }
                }
                else
                {
                    if (point.Y < 0)
                    {
                        verticalDirection = -1;
                    }
                    else if (point.Y > Bounds.Height)
                    {
                        verticalDirection = 1;
                    }
                }

                var x = point.X - ActualRowHeaderWidth;
                if (x < 0)
                {
                    horizontalDirection = -1;
                }
                else if (x > CellsWidth)
                {
                    horizontalDirection = 1;
                }

                direction = new DataGridAutoScrollDirection(horizontalDirection, verticalDirection);
            }

            if (!direction.HasScroll)
            {
                StopFillAutoScroll();
                return;
            }

            _fillAutoScrollDirectionX = direction.Horizontal;
            _fillAutoScrollDirectionY = direction.Vertical;
            StartFillAutoScroll();
        }

        private void StartFillAutoScroll()
        {
            if (_fillAutoScrollTimer == null)
            {
                _fillAutoScrollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _fillAutoScrollTimer.Tick += OnFillAutoScrollTick;
            }

            if (!_fillAutoScrollTimer.IsEnabled)
            {
                _fillAutoScrollTimer.Start();
            }
        }

        private void StopFillAutoScroll()
        {
            if (_fillAutoScrollTimer != null)
            {
                _fillAutoScrollTimer.Stop();
            }

            _fillAutoScrollDirectionX = 0;
            _fillAutoScrollDirectionY = 0;
        }

        private void OnFillAutoScrollTick(object? sender, EventArgs e)
        {
            if (!_isFillHandleDragging)
            {
                StopFillAutoScroll();
                return;
            }

            if (_fillAutoScrollDirectionY != 0)
            {
                ScrollVerticalForDrag(_fillAutoScrollDirectionY);
            }

            if (_fillAutoScrollDirectionX != 0)
            {
                ScrollHorizontalForDrag(_fillAutoScrollDirectionX);
            }

            if (_fillLastPoint.HasValue)
            {
                UpdateFillDrag(_fillLastPoint.Value, null);
            }
        }

        private static bool RangesEqual(DataGridCellRange left, DataGridCellRange right) => left == right;

        private DataGridCellRange GetFillTargetRange(DataGridCellRange source, int targetRow, int targetColumn)
        {
            var startRow = source.StartRow;
            var endRow = source.EndRow;
            var startColumn = source.StartColumn;
            var endColumn = source.EndColumn;

            if (targetRow >= source.EndRow)
            {
                endRow = Math.Max(source.EndRow, targetRow);
            }
            else
            {
                startRow = targetRow;
            }

            if (targetColumn >= source.EndColumn)
            {
                endColumn = Math.Max(source.EndColumn, targetColumn);
            }
            else
            {
                startColumn = targetColumn;
            }

            return new DataGridCellRange(startRow, endRow, startColumn, endColumn);
        }
    }
}
