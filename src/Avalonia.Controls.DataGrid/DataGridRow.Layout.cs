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

            // If the DataGrid was scrolled horizontally after our last Arrange, we need to make sure
            // the Cells and Details are Arranged again
            if (_lastHorizontalOffset != OwningGrid.HorizontalOffset)
            {
                _lastHorizontalOffset = OwningGrid.HorizontalOffset;
                InvalidateHorizontalArrange();
            }

            Size size = base.ArrangeOverride(finalSize);

            if (_checkDetailsContentHeight)
            {
                _checkDetailsContentHeight = false;
                EnsureDetailsContentHeight();
            }

            if (RootElement != null)
            {
                foreach (Control child in RootElement.Children)
                {
                    if (DataGridFrozenGrid.GetIsFrozen(child))
                    {
                        TranslateTransform transform = new TranslateTransform();
                        // Automatic layout rounding doesn't apply to transforms so we need to Round this
                        transform.X = Math.Round(OwningGrid.HorizontalOffset);
                        child.RenderTransform = transform;
                    }
                }
            }

            if (_bottomGridLine != null)
            {
                RectangleGeometry gridlineClipGeometry = new RectangleGeometry();
                // Use the arranged width (which accounts for total columns/header width) so the
                // horizontal grid line spans the full row, not just the measured viewport width.
                double arrangedWidth = Math.Max(finalSize.Width, DesiredSize.Width);
                gridlineClipGeometry.Rect = new Rect(
                    OwningGrid.HorizontalOffset,
                    0,
                    Math.Max(0, arrangedWidth - OwningGrid.HorizontalOffset),
                    _bottomGridLine.DesiredSize.Height);
                _bottomGridLine.Clip = gridlineClipGeometry;
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

            bool constraintsChanged =
                !MathUtilities.AreClose(_lastMeasureConstraintWidth, availableSize.Width) ||
                !MathUtilities.AreClose(_lastMeasureConstraintHeight, availableSize.Height);

            if (constraintsChanged)
            {
                _lastMeasureConstraintWidth = availableSize.Width;
                _lastMeasureConstraintHeight = availableSize.Height;

                // Re-measure child presenters only when row constraints actually change.
                if (_headerElement != null && _headerElement.IsMeasureValid)
                {
                    _headerElement.InvalidateMeasure();
                }
                if (_cellsElement != null && _cellsElement.IsMeasureValid)
                {
                    _cellsElement.InvalidateMeasure();
                }
                if (_detailsElement != null && _detailsElement.IsMeasureValid)
                {
                    _detailsElement.InvalidateMeasure();
                }
            }

            Size desiredSize = base.MeasureOverride(availableSize);
            return desiredSize.WithWidth(Math.Max(desiredSize.Width, OwningGrid.CellsWidth));
        }

        internal void ApplyCellsState()
        {
            foreach (DataGridCell dataGridCell in Cells)
            {
                if (dataGridCell.IsVisible || dataGridCell.IsCurrent)
                {
                    dataGridCell.UpdatePseudoClasses();
                }
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

        internal void ClearPointerOverState()
        {
            PseudoClassesHelper.Set(PseudoClasses, ":pointerover", false);
        }

        internal void ClearDragDropState()
        {
            PseudoClassesHelper.Set(PseudoClasses, ":dragging", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-before", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-after", false);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-inside", false);
        }

        internal void SetDragging(bool dragging)
        {
            PseudoClassesHelper.Set(PseudoClasses, ":dragging", dragging);
        }

        internal void SetDropPosition(DataGridRowDropPosition? position)
        {
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-before", position == DataGridRowDropPosition.Before);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-after", position == DataGridRowDropPosition.After);
            PseudoClassesHelper.Set(PseudoClasses, ":drag-over-inside", position == DataGridRowDropPosition.Inside);
        }

        //TODO Animation
        internal void DetachFromDataGrid(bool recycle)
        {
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
            _cellsElement?.InvalidateDesiredHeight();
        }

        internal void ResetGridLine()
        {
            _bottomGridLine = null;
        }

    }
}
