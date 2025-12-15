// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;

using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// Used within the template of a <see cref="T:Avalonia.Controls.DataGrid" /> to specify the
    /// location in the control's visual tree where the rows are to be added.
    /// </summary>
    /// <remarks>
    /// This is a partial class. The ILogicalScrollable implementation is in 
    /// DataGridRowsPresenter.Scrollable.cs for clean separation of concerns.
    /// </remarks>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed partial class DataGridRowsPresenter : Panel, IChildIndexProvider
    {
        private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;
        private int _virtualizationGuardDepth;
        private DataGrid? _owningGrid;
        private double _lastArrangeHeight;

        public DataGridRowsPresenter()
        {
            AddHandler(Gestures.ScrollGestureEvent, OnScrollGesture);
        }

        public static readonly DirectProperty<DataGridRowsPresenter, DataGrid?> OwningGridProperty =
            AvaloniaProperty.RegisterDirect<DataGridRowsPresenter, DataGrid?>(
                nameof(OwningGrid),
                o => o.OwningGrid,
                (o, v) => o.OwningGrid = v);

        /// <summary>
        /// Gets the grid that owns this rows presenter.
        /// </summary>
        public DataGrid? OwningGrid
        {
            get => _owningGrid;
            internal set => SetAndRaise(OwningGridProperty, ref _owningGrid, value);
        }

        internal bool VirtualizationGuardActive => _virtualizationGuardDepth > 0;

        internal IDisposable BeginVirtualizationGuard()
        {
            _virtualizationGuardDepth++;
            CancelPrefetch();

            return new ActionDisposable(() =>
            {
                _virtualizationGuardDepth = Math.Max(0, _virtualizationGuardDepth - 1);
                if (_virtualizationGuardDepth == 0)
                {
                    InvalidateMeasure();
                }
            });
        }

        #region IChildIndexProvider Implementation

        event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
        {
            add => _childIndexChanged += value;
            remove => _childIndexChanged -= value;
        }

        int IChildIndexProvider.GetChildIndex(ILogical child)
        {
            return child is DataGridRow row
                ? row.Index
                : throw new InvalidOperationException("Invalid DataGrid child");
        }

        bool IChildIndexProvider.TryGetTotalCount(out int count)
        {
            if (OwningGrid is null)
            {
                count = 0;
                return false;
            }

            return OwningGrid.DataConnection.TryGetCount(false, true, out count);
        }

        internal void InvalidateChildIndex(DataGridRow row)
        {
            _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(row, row.Index));
        }

        #endregion

        /// <summary>
        /// Arranges the content of the <see cref="T:Avalonia.Controls.Primitives.DataGridRowsPresenter" />.
        /// </summary>
        /// <returns>
        /// The actual size used by the <see cref="T:Avalonia.Controls.Primitives.DataGridRowsPresenter" />.
        /// </returns>
        /// <param name="finalSize">
        /// The final area within the parent that this element should use to arrange itself and its children.
        /// </param>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize.Height == 0 || OwningGrid == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            if (OwningGrid.RowsPresenterAvailableSize is { } measuredSize)
            {
                var measuredHeight = measuredSize.Height;
                var arrangedHeight = finalSize.Height;
                if (!double.IsInfinity(measuredHeight) && !double.IsNaN(measuredHeight) && !double.IsNaN(arrangedHeight))
                {
                    var threshold = Math.Max(OwningGrid.RowHeightEstimate, 1);
                    if (measuredHeight - arrangedHeight > threshold)
                    {
                        InvalidateMeasure();
                    }
                }
            }

            _lastArrangeHeight = finalSize.Height;

            OwningGrid.OnFillerColumnWidthNeeded(finalSize.Width);

            double rowDesiredWidth = OwningGrid.RowHeadersDesiredWidth + OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth + OwningGrid.ColumnsInternal.FillerColumn.FillerWidth;
            double topEdge = -OwningGrid.NegVerticalOffset;
            foreach (Control element in OwningGrid.DisplayData.GetScrollingElements())
            {
                if (element is DataGridRow row)
                {
                    Debug.Assert(row.Index != -1); // A displayed row should always have its index

                    // Visibility for all filler cells needs to be set in one place.  Setting it individually in
                    // each CellsPresenter causes an NxN layout cycle (see DevDiv Bugs 211557)
                    row.EnsureFillerVisibility();
                    row.Arrange(new Rect(-OwningGrid.HorizontalOffset, topEdge, rowDesiredWidth, element.DesiredSize.Height));
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    double leftEdge = (OwningGrid.AreRowGroupHeadersFrozen) ? 0 : -OwningGrid.HorizontalOffset;
                    groupHeader.Arrange(new Rect(leftEdge, topEdge, rowDesiredWidth - leftEdge, element.DesiredSize.Height));
                }

                topEdge += element.DesiredSize.Height;
            }

            double finalHeight = Math.Max(topEdge + OwningGrid.NegVerticalOffset, finalSize.Height);

            // Clip the RowsPresenter so rows cannot overlap other elements in certain styling scenarios
            var rg = new RectangleGeometry
            {
                Rect = new Rect(0, 0, finalSize.Width, finalHeight)
            };
            Clip = rg;

            // Arrange any hidden/recycled children off-screen to prevent ghost rows
            // This is necessary because Avalonia keeps elements at their last arranged position
            // even when they're hidden, and during fast scrolling the visibility change may not
            // take effect before the next render
            var offScreenRect = new Rect(-10000, -10000, 0, 0);
            foreach (Control child in Children)
            {
                if (!child.IsVisible)
                {
                    child.Arrange(offScreenRect);
                }
            }

            return new Size(finalSize.Width, finalHeight);
        }

        /// <summary>
        /// Measures the children of a <see cref="T:Avalonia.Controls.Primitives.DataGridRowsPresenter" /> to 
        /// prepare for arranging them during the <see cref="M:System.Windows.FrameworkElement.ArrangeOverride(System.Windows.Size)" /> pass.
        /// </summary>
        /// <param name="availableSize">
        /// The available size that this element can give to child elements. Indicates an upper limit that child elements should not exceed.
        /// </param>
        /// <returns>
        /// The size that the <see cref="T:Avalonia.Controls.Primitives.DataGridRowsPresenter" /> determines it needs during layout, based on its calculations of child object allocated sizes.
        /// </returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (double.IsInfinity(availableSize.Height))
            {
                double? constrainedHeight = null;

                if (!double.IsNaN(_lastArrangeHeight) && _lastArrangeHeight > 0 && !double.IsInfinity(_lastArrangeHeight))
                {
                    constrainedHeight = _lastArrangeHeight;
                }

                if (OwningGrid is { } grid)
                {
                    var gridConstraint = LayoutHelper.ApplyLayoutConstraints(grid, availableSize).Height;
                    if (!double.IsInfinity(gridConstraint) && !double.IsNaN(gridConstraint) && gridConstraint > 0)
                    {
                        constrainedHeight = gridConstraint;
                    }
                    else if (grid.Bounds.Height > 0)
                    {
                        constrainedHeight = grid.Bounds.Height;
                    }
                }

                if (constrainedHeight is null && VisualRoot is TopLevel topLevel)
                {
                    double maxHeight = topLevel.IsArrangeValid ?
                                        topLevel.Bounds.Height :
                                        LayoutHelper.ApplyLayoutConstraints(topLevel, availableSize).Height;

                    constrainedHeight = maxHeight;
                }

                if (constrainedHeight is double height)
                {
                    availableSize = availableSize.WithHeight(height);
                }
            }

            if (OwningGrid == null)
            {
                return base.MeasureOverride(availableSize);
            }

            // If the Width of our RowsPresenter changed then we need to invalidate our rows
            bool invalidateRows = (!OwningGrid.RowsPresenterAvailableSize.HasValue || availableSize.Width != OwningGrid.RowsPresenterAvailableSize.Value.Width)
                                  && !double.IsInfinity(availableSize.Width);

            // The DataGrid uses the RowsPresenter available size in order to autogrow
            // and calculate the scrollbars
            OwningGrid.RowsPresenterAvailableSize = availableSize;

            OwningGrid.OnRowsMeasure();

            double totalHeight = -OwningGrid.NegVerticalOffset;
            double totalCellsWidth = OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth;

            double headerWidth = 0;
            foreach (Control element in OwningGrid.DisplayData.GetScrollingElements())
            {
                DataGridRow? row = element as DataGridRow;
                if (row != null)
                {
                    if (invalidateRows)
                    {
                        row.InvalidateMeasure();
                    }
                }

                double measureWidth = availableSize.Width;
                if (double.IsInfinity(measureWidth) || double.IsNaN(measureWidth))
                {
                    // Fall back to the space the grid will actually use (headers + columns + filler)
                    measureWidth = OwningGrid.RowHeadersDesiredWidth
                                   + OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth
                                   + OwningGrid.ColumnsInternal.FillerColumn.FillerWidth;
                }

                element.Measure(new Size(measureWidth, double.PositiveInfinity));

                if (row != null && row.HeaderCell != null)
                {
                    headerWidth = Math.Max(headerWidth, row.HeaderCell.DesiredSize.Width);
                }
                else if (element is DataGridRowGroupHeader groupHeader && groupHeader.HeaderCell != null)
                {
                    headerWidth = Math.Max(headerWidth, groupHeader.HeaderCell.DesiredSize.Width);
                }

                totalHeight += element.DesiredSize.Height;
            }

            OwningGrid.RowHeadersDesiredWidth = headerWidth;
            // Could be positive infinity depending on the DataGrid's bounds
            OwningGrid.AvailableSlotElementRoom = availableSize.Height - totalHeight;

            totalHeight = Math.Max(0, totalHeight);

            // Update ILogicalScrollable extent and viewport
            // For horizontal extent, we use total column width (frozen + scrolling)
            // The horizontal offset only affects scrolling columns, but extent includes all
            var extentHeight = OwningGrid.GetEdgedRowsHeight();
            var frozenColumnsWidth = OwningGrid.GetVisibleFrozenColumnsWidth();
            var scrollingColumnsWidth = OwningGrid.GetVisibleScrollingColumnsWidth();
            
            // Extent includes row headers + all columns
            var extentWidth = headerWidth + frozenColumnsWidth + scrollingColumnsWidth;
            var newExtent = new Size(extentWidth, extentHeight);
            
            // Viewport is the visible area
            var newViewport = new Size(availableSize.Width, availableSize.Height);
            
            UpdateScrollInfo(newExtent, newViewport);
            
            // Sync our offset with the DataGrid's current offset
            SyncOffset(OwningGrid.HorizontalOffset, OwningGrid.GetVerticalOffset());

            return new Size(totalCellsWidth + headerWidth, totalHeight);
        }

        private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
        {
            if (OwningGrid?.UseLogicalScrollable != true)
            {
                return;
            }

            e.Handled = e.Handled || OwningGrid.UpdateScroll(-e.Delta);
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _onDispose;

            public ActionDisposable(Action onDispose)
            {
                _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
                _onDispose = null;
            }
        }

        internal void TrimRecycledContainers()
        {
            if (OwningGrid?.DisplayData == null || OwningGrid.RowsPresenterAvailableSize is null)
            {
                return;
            }

            if (!OwningGrid.TrimRecycledContainers)
            {
                return;
            }

            var viewportHeight = OwningGrid.RowsPresenterAvailableSize.Value.Height;
            if (double.IsInfinity(viewportHeight) || double.IsNaN(viewportHeight))
            {
                return;
            }

            var recycleLimit = Math.Max(PrefetchBufferRows + 1, 4);
            OwningGrid.DisplayData.TrimRecycledPools(this, recycleLimit, recycleLimit);
        }

#if DEBUG
        internal void PrintChildren()
        {
            foreach (Control element in Children)
            {
                if (element is DataGridRow row)
                {
                    Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Slot: {0} Row: {1} Visibility: {2} ", row.Slot, row.Index, row.IsVisible));
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    Debug.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Slot: {0} GroupHeader: {1} Visibility: {2}", groupHeader.RowGroupInfo.Slot, groupHeader.RowGroupInfo.CollectionViewGroup.Key, groupHeader.IsVisible));
                }
            }
        }
#endif
    }
}
