// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Automation.Peers;
using Avalonia.Automation;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Utilities;
using System.Diagnostics;
using System;

namespace Avalonia.Controls
{
    partial class DataGridRow
    {
        private bool _hasDeferredHeight;
        private double _deferredHeight;

        internal void SetRecycledBounds(Rect bounds)
        {
            Bounds = bounds;
        }

        internal bool HasDeferredHeight => _hasDeferredHeight;

        internal double DeferredHeight => _deferredHeight;

        internal void SetDeferredHeight(double height)
        {
            _deferredHeight = height;
            _hasDeferredHeight = true;
        }

        internal void ClearDeferredHeight()
        {
            _hasDeferredHeight = false;
        }

        /// <summary>
        /// Arranges the content of the <see cref="T:Avalonia.Controls.DataGridRow" />.
        /// </summary>
        /// <returns>
        /// The actual size used by the <see cref="T:Avalonia.Controls.DataGridRow" />.
        /// </returns>
        /// <param name="finalSize">
        /// The final area within the parent that this element should use to arrange itself and its children.
        /// </param>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (OwningGrid == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            using var arrangeScope = DataGridDiagnostics.BeginRowArrange();

            // If the DataGrid was scrolled horizontally after our last Arrange, we need to make sure
            // the Cells and Details are Arranged again
            bool horizontalOffsetChanged = _lastHorizontalOffset != OwningGrid.HorizontalOffset;
            if (horizontalOffsetChanged)
            {
                _lastHorizontalOffset = OwningGrid.HorizontalOffset;
                InvalidateHorizontalArrange();
            }

            bool canReuseArrange = RootElement != null &&
                                   _hasValidArrange &&
                                   !_checkDetailsContentHeight &&
                                   RootElement.IsArrangeValid &&
                                   MathUtilities.AreClose(_lastArrangeSize.Width, finalSize.Width) &&
                                   MathUtilities.AreClose(_lastArrangeSize.Height, finalSize.Height);

            Size size;
            if (canReuseArrange)
            {
                size = _lastArrangeResult;
            }
            else
            {
                size = base.ArrangeOverride(finalSize);
                _lastArrangeSize = finalSize;
                _lastArrangeResult = size;
                _hasValidArrange = RootElement != null;
            }

            if (_checkDetailsContentHeight)
            {
                _checkDetailsContentHeight = false;
                EnsureDetailsContentHeight();
            }

            if (!canReuseArrange && RootElement != null)
            {
                foreach (Control child in RootElement.Children)
                {
                    if (DataGridFrozenGrid.GetIsFrozen(child))
                    {
                        TranslateTransform transform = child.RenderTransform as TranslateTransform;
                        if (transform == null)
                        {
                            transform = new TranslateTransform();
                            child.RenderTransform = transform;
                        }

                        // Automatic layout rounding doesn't apply to transforms so we need to Round this
                        transform.X = Math.Round(OwningGrid.HorizontalOffset);
                        transform.Y = 0;
                    }
                }
            }

            if (!canReuseArrange && _bottomGridLine != null)
            {
                if (_bottomGridLineClipGeometry == null)
                {
                    _bottomGridLineClipGeometry = new RectangleGeometry();
                    _bottomGridLine.Clip = _bottomGridLineClipGeometry;
                }

                // Use the arranged width (which accounts for total columns/header width) so the
                // horizontal grid line spans the full row, not just the measured viewport width.
                double arrangedWidth = Math.Max(finalSize.Width, DesiredSize.Width);
                _bottomGridLineClipGeometry.Rect = new Rect(
                    OwningGrid.HorizontalOffset,
                    0,
                    Math.Max(0, arrangedWidth - OwningGrid.HorizontalOffset),
                    _bottomGridLine.DesiredSize.Height);
            }

            return size;
        }

        /// <summary>
        /// Measures the children of a <see cref="T:Avalonia.Controls.DataGridRow" /> to
        /// prepare for arranging them during the <see cref="M:System.Windows.FrameworkElement.ArrangeOverride(System.Windows.Size)" /> pass.
        /// </summary>
        /// <param name="availableSize">
        /// The available size that this element can give to child elements. Indicates an upper limit that child elements should not exceed.
        /// </param>
        /// <returns>
        /// The size that the <see cref="T:Avalonia.Controls.Primitives.DataGridRow" /> determines it needs during layout, based on its calculations of child object allocated sizes.
        /// </returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (OwningGrid == null)
            {
                return base.MeasureOverride(availableSize);
            }

            using var measureScope = DataGridDiagnostics.BeginRowMeasure();

            ClearDeferredHeight();
            _hasValidArrange = false;

            //Allow the DataGrid specific components to adjust themselves based on new values
            bool recycledRootDataContextChanged = ConsumeRecycledRootDataContextChanged();
            if (recycledRootDataContextChanged && RootElement != null)
            {
                RootElement.InvalidateMeasure();
            }
            if (_headerElement != null)
            {
                _headerElement.InvalidateMeasure();
            }
            if (_cellsElement != null)
            {
                _cellsElement.InvalidateMeasure();
            }
            if (_detailsElement != null)
            {
                _detailsElement.InvalidateMeasure();
            }
            if (recycledRootDataContextChanged && RootElement != null)
            {
                RootElement.Measure(availableSize);
            }

            Size desiredSize = base.MeasureOverride(availableSize);
            if (OwningGrid.UsesFlatVisualLayout)
            {
                desiredSize = desiredSize.WithHeight(GetFlatDesiredHeight(OwningGrid, desiredSize.Height));
            }
            return desiredSize.WithWidth(Math.Max(desiredSize.Width, OwningGrid.CellsWidth));
        }

        internal void ApplyCellsState()
        {
            foreach (DataGridCell dataGridCell in Cells)
            {
                dataGridCell.UpdatePseudoClasses();
            }
        }

        internal void ApplyHeaderStatus()
        {
            if (_headerElement != null && OwningGrid.AreRowHeadersVisible)
            {
                _headerElement.UpdatePseudoClasses();
            }
        }

        internal void UpdateCurrentPseudoClass()
        {
            var isCurrent = OwningGrid != null
                            && Slot != -1
                            && OwningGrid.CurrentSlot == Slot;
            PseudoClassesHelper.Set(PseudoClasses, ":current", isCurrent);
        }

        internal void ApplyState(bool? isSelectedOverride = null)
        {
            if (RootElement != null && OwningGrid != null && IsVisible)
            {
                var isSelected = isSelectedOverride ?? (Slot != -1 && OwningGrid.GetRowSelection(Slot));
                IsSelected = isSelected;
                UpdateSelectionPseudoClasses();
                PseudoClassesHelper.Set(PseudoClasses, ":editing", IsEditing);
                PseudoClassesHelper.Set(PseudoClasses, ":invalid", ValidationSeverity == DataGridValidationSeverity.Error);
                PseudoClassesHelper.Set(PseudoClasses, ":warning", ValidationSeverity == DataGridValidationSeverity.Warning);
                PseudoClassesHelper.Set(PseudoClasses, ":info", ValidationSeverity == DataGridValidationSeverity.Info);
                UpdateCurrentPseudoClass();
                PseudoClassesHelper.Set(PseudoClasses, ":pointerover", IsMouseOver);
                ApplyHeaderStatus();
            }
        }

        internal void ApplyRetargetedVirtualSurfaceState(
            bool wasCurrent,
            DataGridValidationSeverity previousValidationSeverity,
            bool isSelected,
            bool isFullySelected)
        {
            if (RootElement == null || OwningGrid == null || !IsVisible)
            {
                return;
            }

            ApplyRetargetedSelectionState(isSelected, isFullySelected);

            if (previousValidationSeverity != ValidationSeverity)
            {
                PseudoClassesHelper.Set(PseudoClasses, ":invalid", ValidationSeverity == DataGridValidationSeverity.Error);
                PseudoClassesHelper.Set(PseudoClasses, ":warning", ValidationSeverity == DataGridValidationSeverity.Warning);
                PseudoClassesHelper.Set(PseudoClasses, ":info", ValidationSeverity == DataGridValidationSeverity.Info);
            }

            bool isCurrent = Slot != -1 && OwningGrid.CurrentSlot == Slot;
            if (wasCurrent != isCurrent)
            {
                PseudoClassesHelper.Set(PseudoClasses, ":current", isCurrent);
            }
        }

        internal void ClearPointerOverState()
        {
            if (!IsMouseOver && MouseOverColumnIndex is null)
            {
                return;
            }

            OwningGrid?.ClearPointerOverRowForRecycle(this);
            MouseOverColumnIndex = null;
            PseudoClassesHelper.Set(PseudoClasses, ":pointerover", false);
        }

        internal void ClearDragDropState()
        {
            if (!_isDragging && _dropPosition is null)
            {
                return;
            }

            _isDragging = false;
            _dropPosition = null;
            PseudoClassesHelper.Set(PseudoClasses, ":dragging", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-before", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-after", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-inside", false);
        }

        internal void SetDragging(bool dragging)
        {
            if (_isDragging == dragging)
            {
                return;
            }

            _isDragging = dragging;
            PseudoClassesHelper.Set(PseudoClasses, ":dragging", dragging);
        }

        internal void SetDropPosition(DataGridRowDropPosition? position)
        {
            if (_dropPosition == position)
            {
                return;
            }

            _dropPosition = position;
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-before", position == DataGridRowDropPosition.Before);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-after", position == DataGridRowDropPosition.After);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-inside", position == DataGridRowDropPosition.Inside);
        }

        //TODO Animation
        internal void DetachFromDataGrid(bool recycle)
        {
            ClearDeferredHeight();
            _hasValidArrange = false;
            UnloadDetailsTemplate(recycle);

            if (recycle)
            {
                IsRecycled = true;

                if (_cellsElement != null)
                {
                    _cellsElement.Recycle();
                }

                _checkDetailsContentHeight = false;

                // Clear out the old Details cache so it won't be reused for other data
                //_detailsDesiredHeight = double.NaN;
                if (_detailsElement != null)
                {
                    _detailsElement.ClearValue(DataGridDetailsPresenter.ContentHeightProperty);
                }
            }

            Slot = -1;
            UpdateCurrentPseudoClass();
            PseudoClassesHelper.Set(PseudoClasses, ":pointerover", false);
            ClearDragDropState();
        }

        internal void InvalidateCellsIndex()
        {
            _cellsElement?.InvalidateChildIndex();
        }

        internal void EnsureFillerVisibility()
        {
            if (_cellsElement != null)
            {
                _cellsElement.EnsureFillerVisibility();
            }
        }

        internal void EnsureGridLines()
        {
            if (OwningGrid != null)
            {
                if (OwningGrid.UsesFlatVisualLayout)
                {
                    InvalidateFlatGridLine();
                }

                if (_bottomGridLine != null)
                {
                    // It looks like setting Visibility sometimes has side effects so make sure the value is actually
                    // different before setting it
                    bool newVisibility = OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.Horizontal || OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.All;

                    if (newVisibility != _bottomGridLine.IsVisible)
                    {
                        _bottomGridLine.IsVisible = newVisibility;
                    }
                    _bottomGridLine.Fill = OwningGrid.HorizontalGridLinesBrush;
                }

                foreach (DataGridCell cell in Cells)
                {
                    cell.EnsureGridLine(OwningGrid.ColumnsInternal.LastVisibleColumn);
                }
            }
        }

        internal void EnsureHeaderStyleAndVisibility(Styling.Style previousStyle)
        {
            if (_headerElement != null && OwningGrid != null)
            {
                _headerElement.IsVisible = OwningGrid.AreRowHeadersVisible;
            }
        }

        internal void EnsureHeaderVisibility()
        {
            if (_headerElement != null && OwningGrid != null)
            {
                _headerElement.IsVisible = OwningGrid.AreRowHeadersVisible;
            }
        }

        internal void InvalidateHorizontalArrange()
        {
            _hasValidArrange = false;
            if (_cellsElement != null)
            {
                _cellsElement.InvalidateArrange();
            }
            if (_detailsElement != null)
            {
                _detailsElement.InvalidateArrange();
            }
        }

        internal void InvalidateDesiredHeight()
        {
            _hasValidArrange = false;
            _cellsElement?.InvalidateDesiredHeight();
        }

        internal void ResetGridLine()
        {
            _bottomGridLine = null;
        }

    }
}
