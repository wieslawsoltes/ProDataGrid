// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Utilities;
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
#else
internal
#endif
    sealed partial class DataGridRowsPresenter : Panel, IChildIndexProvider
    {
        private const double ScrollInfoSizeEpsilon = 0.5;

        private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;
        private TopLevel? _observedTopLevel;
        private int _virtualizationGuardDepth;
        private DataGrid? _owningGrid;
        private double _lastArrangeHeight;
        private bool _lastArrangeMatchesDesired = true;
        private bool _processingRowsMeasure;
        private bool _canUseRetargetedRowsMeasureFastPath;
        private bool _canUseRetargetedRowsArrangeFastPath;
        private bool _retargetedRowsRemainArrangeValid;
        private int _retargetedMeasureRowCount;
        private double _retargetedMeasureRowHeight;
        private double _lastArrangedNegVerticalOffset;
        private Size _lastArrangedSize;
        private Size? _lastMeasureConstraint;
        private Size? _arrangedMeasureSize;
        private bool _cleanupRetainedRowsForLightweightLayout;
        private readonly HashSet<Control> _displayedElementsScratch = new();
        private readonly Dictionary<Control, Size> _measureConstraints = new();
        private readonly RectangleGeometry _clipRectGeometry = new();

        internal double LastArrangeHeight => _lastArrangeHeight;

        internal int MeasureConstraintCacheCount => _measureConstraints.Count;

        internal long RetargetedRowsMeasureFastPathCount { get; private set; }

        internal long RetargetedRowsArrangeFastPathCount { get; private set; }

        public DataGridRowsPresenter()
        {
            ScrollGesture += OnScrollGesture;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            HookTopLevel(e.Root as TopLevel ?? TopLevel.GetTopLevel(this));
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            UnhookTopLevel();
            _lastMeasureConstraint = null;
            _arrangedMeasureSize = null;
            ResetFlatVisualLayout();
            DetachVirtualCellSurface();
            _measureConstraints.Clear();
            base.OnDetachedFromVisualTree(e);
        }

        internal void ClearTrackedChildren()
        {
            ResetFlatVisualLayout();
            DetachVirtualCellSurface();
            _measureConstraints.Clear();
            Children.Clear();
        }

        internal void RemoveTrackedChild(Control child)
        {
            if (child is DataGridRow row)
            {
                RemoveFlatCells(row);
            }
            _measureConstraints.Remove(child);
            Children.Remove(child);
        }

        internal void RequestRetainedRowsCleanupForLightweightLayout()
        {
            _cleanupRetainedRowsForLightweightLayout = true;
        }

        private void CleanupRetainedRowsForLightweightLayout()
        {
            bool hasLightweightRangeWithRetainedChildren =
                OwningGrid?.DisplayData.HasVirtualScrollingElements == true &&
                Children.Count != 0;
            if (!_cleanupRetainedRowsForLightweightLayout &&
                !hasLightweightRangeWithRetainedChildren)
            {
                return;
            }

            if (OwningGrid?.DisplayData.HasVirtualScrollingElements != true)
            {
                return;
            }

            _cleanupRetainedRowsForLightweightLayout = false;
            for (int index = Children.Count - 1; index >= 0; index--)
            {
                if (Children[index] is DataGridRow
                    or DataGridRowGroupHeader
                    or DataGridRowGroupFooter)
                {
                    RemoveTrackedChild(Children[index]);
                }
            }
        }

        private void RemoveTrackedChildAt(int index)
        {
            _measureConstraints.Remove(Children[index]);
            Children.RemoveAt(index);
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

        internal void InvalidateChildIndexes()
        {
            _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.ChildIndexesReset);
        }

        internal void MarkDefaultVirtualRowsRetargeted(
            int rowCount,
            double rowHeight,
            bool rowsRemainMeasureValid,
            bool rowsRemainArrangeValid)
        {
            _canUseRetargetedRowsMeasureFastPath =
                _processingRowsMeasure &&
                rowsRemainMeasureValid;
            _retargetedRowsRemainArrangeValid = rowsRemainArrangeValid;
            _retargetedMeasureRowCount = rowCount;
            _retargetedMeasureRowHeight = rowHeight;
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
            if (OwningGrid == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            if (finalSize.Height <= 0)
            {
                var viewportHeight = Math.Max(0, finalSize.Height);
                if (OwningGrid.RowsPresenterAvailableSize is { } availableSize &&
                    !double.IsNaN(availableSize.Height) &&
                    !double.IsInfinity(availableSize.Height) &&
                    !MathUtilities.AreClose(availableSize.Height, viewportHeight))
                {
                    OwningGrid.RowsPresenterAvailableSize = availableSize.WithHeight(viewportHeight);
                }

                var collapsedViewport = new Size(finalSize.Width, viewportHeight);
                if (!AreClose(_viewport, collapsedViewport))
                {
                    UpdateScrollInfo(_extent, collapsedViewport);
                }

                _lastArrangeHeight = viewportHeight;
                _lastArrangeMatchesDesired = false;
                _lastArrangedSize = new Size(finalSize.Width, viewportHeight);
                _lastArrangedNegVerticalOffset = OwningGrid.NegVerticalOffset;
                return base.ArrangeOverride(finalSize);
            }

            var threshold = Math.Max(OwningGrid.RowHeightEstimate, 1);
            // During a resize, the window and grid bounds can still describe the previous layout pass.
            var viewport = _lastMeasureConstraint is { } constraint
                ? new Size(Math.Min(finalSize.Width, constraint.Width), Math.Min(finalSize.Height, constraint.Height))
                : finalSize;
            if (this.GetVisualParent() is not ScrollContentPresenter &&
                OwningGrid.IsArrangeValid && OwningGrid.Bounds.Height > 0)
            {
                var gridRowsHeight = Math.Max(0, OwningGrid.Bounds.Height - GetColumnHeadersHeight());
                viewport = viewport.WithHeight(Math.Min(viewport.Height, gridRowsHeight));
            }
            var effectiveRowsHeight = viewport.Height;

            // Only the ScrollViewer template needs to reconcile an oversized measure
            // constraint with its viewport. Legacy templates supply the rows area directly.
            if (OwningGrid.UseLogicalScrollable && _lastMeasureConstraint is { } originalConstraint)
            {
                var arrangedConstraint = new Size(
                    double.IsInfinity(originalConstraint.Width) ? originalConstraint.Width : viewport.Width,
                    double.IsInfinity(originalConstraint.Height) && MatchesDesiredHeight(viewport.Height)
                        ? originalConstraint.Height
                        : viewport.Height);
                _arrangedMeasureSize = AreClose(originalConstraint, arrangedConstraint) ? null : arrangedConstraint;
            }

            double measuredHeight = double.NaN;
            if (OwningGrid.RowsPresenterAvailableSize is { } measuredSize)
            {
                measuredHeight = measuredSize.Height;
                // Only star-sized cells need another measure when arranging reveals a
                // narrower ScrollViewer viewport. Fixed-width cells keep their widths.
                var widthChanged = OwningGrid.UseLogicalScrollable && OwningGrid.UsesStarSizing &&
                    !double.IsInfinity(measuredSize.Width) &&
                    !AreScrollInfoSizeClose(measuredSize.Width, viewport.Width);
                var heightChanged = !double.IsInfinity(measuredHeight) &&
                    !AreScrollInfoSizeClose(measuredHeight, effectiveRowsHeight);
                if (widthChanged || heightChanged)
                {
                    OwningGrid.RowsPresenterAvailableSize = viewport;
                    DataGridDiagnostics.RecordRowsArrangeMeasureInvalidated();
                    InvalidateMeasure();
                }
            }

            if (!AreClose(_viewport, viewport))
            {
                UpdateScrollInfo(_extent, viewport);
            }

            if (OwningGrid.KeepRecycledContainersInVisualTree &&
                !OwningGrid.TrimRecycledContainers &&
                !double.IsNaN(OwningGrid.Height) &&
                !double.IsInfinity(OwningGrid.Height) &&
                OwningGrid.Height > 0)
            {
                var recycleLimit = Math.Max(PrefetchBufferRows + 1, 4);
                var shouldTrim = !double.IsInfinity(measuredHeight) &&
                                 !double.IsNaN(measuredHeight) &&
                                 measuredHeight - effectiveRowsHeight > threshold;
                if (!shouldTrim)
                {
                    var maxDesiredHeight = OwningGrid.RowHeightEstimate * recycleLimit;
                    shouldTrim = effectiveRowsHeight <= maxDesiredHeight;
                }

                if (shouldTrim)
                {
                    OwningGrid.DisplayData.TrimRecycledPools(this, recycleLimit, recycleLimit, recycleLimit);
                }
            }

            bool useRetargetedRowsArrangeFastPath =
                _canUseRetargetedRowsArrangeFastPath &&
                OwningGrid.UsesVirtualCellSurface &&
                AreClose(_lastArrangedSize, new Size(finalSize.Width, effectiveRowsHeight)) &&
                MathUtilities.AreClose(_lastArrangedNegVerticalOffset, OwningGrid.NegVerticalOffset);
            _canUseRetargetedRowsArrangeFastPath = false;
            if (useRetargetedRowsArrangeFastPath)
            {
                _lastArrangeHeight = effectiveRowsHeight;
                _lastArrangeMatchesDesired = MatchesDesiredHeight(finalSize.Height);
                _lastArrangedSize = new Size(finalSize.Width, effectiveRowsHeight);
                _lastArrangedNegVerticalOffset = OwningGrid.NegVerticalOffset;
                RetargetedRowsArrangeFastPathCount++;
                DataGridDiagnostics.RecordRowsArrangeSkipped(_retargetedMeasureRowCount);
                DataGridDiagnostics.RecordRowsRetargetArrangeReused(_retargetedMeasureRowCount);
                return new Size(
                    finalSize.Width,
                    Math.Max(_retargetedMeasureRowCount * _retargetedMeasureRowHeight, finalSize.Height));
            }

            _lastArrangeHeight = effectiveRowsHeight;
            _lastArrangeMatchesDesired = MatchesDesiredHeight(finalSize.Height);

            OwningGrid.OnFillerColumnWidthNeeded(finalSize.Width);

            double rowDesiredWidth = OwningGrid.RowHeadersDesiredWidth + OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth + OwningGrid.ColumnsInternal.FillerColumn.FillerWidth;
            double topEdge = -OwningGrid.NegVerticalOffset;
            if (OwningGrid.UsesFlatVisualLayout)
            {
                PrepareFlatColumnLayouts();
            }
            var displayedElements = _displayedElementsScratch;
            displayedElements.Clear();
            int arrangedElements = 0;
            int skippedArrangeElements = 0;
            using var arrangeScope = DataGridDiagnostics.BeginRowsArrange();
            if (OwningGrid.DisplayData.HasVirtualScrollingElements)
            {
                topEdge += RefreshLightweightVirtualRowGeometry();
            }
            foreach (Control element in OwningGrid.DisplayData.GetScrollingElements())
            {
                displayedElements.Add(element);

                if (element is DataGridRow row)
                {
                    Debug.Assert(row.Index != -1); // A displayed row should always have its index

                    // Visibility for all filler cells needs to be set in one place.  Setting it individually in
                    // each CellsPresenter causes an NxN layout cycle (see DevDiv Bugs 211557)
                    row.EnsureFillerVisibility();
                    var targetRect = OwningGrid.UsesFlatVisualLayout
                        ? new Rect(0, topEdge, Math.Max(finalSize.Width, rowDesiredWidth), element.DesiredSize.Height)
                        : new Rect(-OwningGrid.HorizontalOffset, topEdge, rowDesiredWidth, element.DesiredSize.Height);
                    if (ShouldArrangeElement(row, targetRect))
                    {
                        row.Arrange(targetRect);
                        arrangedElements++;
                    }
                    else
                    {
                        skippedArrangeElements++;
                    }

                    if (OwningGrid.UsesFlatVisualLayout && !OwningGrid.UsesVirtualCellSurface)
                    {
                        ArrangeFlatCells(row, topEdge, element.DesiredSize.Height);
                    }
                    else if (OwningGrid.UsesVirtualCellSurface)
                    {
                        ArrangeVirtualCompatibilityCell(row, topEdge);
                    }
                }
                else if (element is DataGridRowGroupHeader groupHeader)
                {
                    double leftEdge = (OwningGrid.AreRowGroupHeadersFrozen) ? 0 : -OwningGrid.HorizontalOffset;
                    var targetRect = new Rect(leftEdge, topEdge, rowDesiredWidth - leftEdge, element.DesiredSize.Height);
                    if (ShouldArrangeElement(groupHeader, targetRect))
                    {
                        groupHeader.Arrange(targetRect);
                        arrangedElements++;
                    }
                    else
                    {
                        skippedArrangeElements++;
                    }
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    double leftEdge = (OwningGrid.AreRowGroupHeadersFrozen) ? 0 : -OwningGrid.HorizontalOffset;
                    var targetRect = new Rect(leftEdge, topEdge, rowDesiredWidth - leftEdge, element.DesiredSize.Height);
                    if (ShouldArrangeElement(groupFooter, targetRect))
                    {
                        groupFooter.Arrange(targetRect);
                        arrangedElements++;
                    }
                    else
                    {
                        skippedArrangeElements++;
                    }
                }

                topEdge += element.DesiredSize.Height;
            }

            double finalHeight = Math.Max(topEdge + OwningGrid.NegVerticalOffset, finalSize.Height);
            ArrangeVirtualCellSurface(finalSize.Width, finalHeight);

            // Clip the RowsPresenter so rows cannot overlap other elements in certain styling scenarios
            var clipRect = new Rect(0, 0, finalSize.Width, finalHeight);
            if (!AreClose(_clipRectGeometry.Rect, clipRect))
            {
                _clipRectGeometry.Rect = clipRect;
            }
            if (!ReferenceEquals(Clip, _clipRectGeometry))
            {
                Clip = _clipRectGeometry;
            }

            for (var childIndex = Children.Count - 1; childIndex >= 0; childIndex--)
            {
                Control child = Children[childIndex];
                if (!displayedElements.Contains(child))
                {
                    _measureConstraints.Remove(child);
                }

                if (!child.IsVisible)
                {
                    if (!displayedElements.Contains(child) &&
                        child is DataGridRow row &&
                        !row.IsRecycled &&
                        row.IsRecyclable &&
                        OwningGrid.RecycleOrphanedElement(child) &&
                        !OwningGrid.KeepRecycledContainersInVisualTree &&
                        ReferenceEquals(child.Parent, this))
                    {
                        RemoveTrackedChildAt(childIndex);
                    }

                    skippedArrangeElements++;
                    continue;
                }

                if (!displayedElements.Contains(child))
                {
                    if (OwningGrid.RecycleOrphanedElement(child))
                    {
                        if (!OwningGrid.KeepRecycledContainersInVisualTree &&
                            ReferenceEquals(child.Parent, this))
                        {
                            RemoveTrackedChildAt(childIndex);
                        }
                    }
                }
            }

            DataGridDiagnostics.RecordRowsArranged(arrangedElements);
            DataGridDiagnostics.RecordRowsArrangeSkipped(skippedArrangeElements);

            _lastArrangedSize = new Size(finalSize.Width, effectiveRowsHeight);
            _lastArrangedNegVerticalOffset = OwningGrid.NegVerticalOffset;

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
            var originalAvailableSize = availableSize;
            if (OwningGrid?.UseLogicalScrollable == true &&
                _lastMeasureConstraint == originalAvailableSize && _arrangedMeasureSize is Size arrangedSize)
            {
                // An unchanged constraint must not oscillate between the measured and the arranged size.
                availableSize = arrangedSize;
            }
            else
            {
                _arrangedMeasureSize = null;
            }
            _lastMeasureConstraint = originalAvailableSize;

            if (double.IsInfinity(availableSize.Height))
            {
                var grid = OwningGrid;
                var allowInfiniteHeight = grid != null &&
                                          double.IsNaN(grid.Height) &&
                                          double.IsInfinity(grid.MaxHeight);
                ScrollViewer? externalScrollViewer = null;

                if (allowInfiniteHeight && grid != null)
                {
                    var hasItemsPresenter = false;
                    for (var ancestor = grid.GetVisualParent(); ancestor != null; ancestor = ancestor.GetVisualParent())
                    {
                        if (ancestor is ItemsPresenter)
                        {
                            hasItemsPresenter = true;
                        }

                        if (ancestor is ScrollViewer)
                        {
                            if (!hasItemsPresenter)
                            {
                                allowInfiniteHeight = false;
                                externalScrollViewer = (ScrollViewer)ancestor;
                            }

                            break;
                        }
                    }
                }

                if (!allowInfiniteHeight)
                {
                    double? constrainedHeight = null;

                    if (externalScrollViewer != null)
                    {
                        if (!double.IsNaN(externalScrollViewer.Height) &&
                            !double.IsInfinity(externalScrollViewer.Height) &&
                            externalScrollViewer.Height > 0)
                        {
                            constrainedHeight = externalScrollViewer.Height;
                        }
                        else if (externalScrollViewer.Bounds.Height > 0)
                        {
                            constrainedHeight = externalScrollViewer.Bounds.Height;
                        }
                    }

                    if (!_lastArrangeMatchesDesired &&
                        !double.IsNaN(_lastArrangeHeight) &&
                        _lastArrangeHeight > 0 &&
                        !double.IsInfinity(_lastArrangeHeight))
                    {
                        constrainedHeight = _lastArrangeHeight;
                    }

                    if (grid != null)
                    {
                        if (constrainedHeight is null &&
                            !double.IsNaN(grid.Height) &&
                            !double.IsInfinity(grid.Height) &&
                            grid.Height > 0)
                        {
                            constrainedHeight = grid.Height;
                        }
                        else
                        {
                            var gridConstraint = LayoutHelper.ApplyLayoutConstraints(grid, availableSize).Height;
                            if (!double.IsInfinity(gridConstraint) && !double.IsNaN(gridConstraint) && gridConstraint > 0)
                            {
                                constrainedHeight = gridConstraint;
                            }
                        }
                        if (constrainedHeight is null && !_lastArrangeMatchesDesired && grid.Bounds.Height > 0)
                        {
                            constrainedHeight = grid.Bounds.Height;
                        }
                    }

                    if (constrainedHeight is null && VisualRoot is TopLevel topLevel)
                    {
                        double maxHeight = topLevel.IsArrangeValid
                            ? topLevel.Bounds.Height
                            : LayoutHelper.ApplyLayoutConstraints(topLevel, availableSize).Height;

                        constrainedHeight = maxHeight;
                    }

                    if (constrainedHeight is double height)
                    {
                        availableSize = availableSize.WithHeight(height);
                    }
                }
            }

            if (OwningGrid is { } constrainedGrid &&
                !double.IsInfinity(availableSize.Height) &&
                !double.IsNaN(availableSize.Height))
            {
                if (!double.IsNaN(constrainedGrid.Height) &&
                    !double.IsInfinity(constrainedGrid.Height) &&
                    constrainedGrid.Height > 0 &&
                    constrainedGrid.Height < availableSize.Height)
                {
                    availableSize = availableSize.WithHeight(constrainedGrid.Height);
                }

                var gridConstraint = LayoutHelper.ApplyLayoutConstraints(constrainedGrid, availableSize).Height;
                if (!double.IsInfinity(gridConstraint) && !double.IsNaN(gridConstraint) &&
                    gridConstraint > 0 && gridConstraint < availableSize.Height)
                {
                    availableSize = availableSize.WithHeight(gridConstraint);
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
            if (invalidateRows && OwningGrid.UseLogicalScrollable && OwningGrid.UsesStarSizing)
            {
                OwningGrid.ColumnHeaders?.InvalidateMeasure();
            }

            _canUseRetargetedRowsMeasureFastPath = false;
            _canUseRetargetedRowsArrangeFastPath = false;
            _processingRowsMeasure = true;
            try
            {
                OwningGrid.OnRowsMeasure();
            }
            finally
            {
                _processingRowsMeasure = false;
            }
            CleanupRetainedRowsForLightweightLayout();
            SyncFlatCells();
            if (OwningGrid.UsesFlatVisualLayout)
            {
                PrepareFlatColumnLayouts();
            }

            double totalHeight = -OwningGrid.NegVerticalOffset;
            double totalCellsWidth = OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth;
            double measureWidth = availableSize.Width;
            if (double.IsInfinity(measureWidth) || double.IsNaN(measureWidth))
            {
                // Fall back to the space the grid will actually use (headers + columns + filler)
                measureWidth = OwningGrid.RowHeadersDesiredWidth
                               + OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth
                               + OwningGrid.ColumnsInternal.FillerColumn.FillerWidth;
            }

            var measureConstraint = new Size(measureWidth, double.PositiveInfinity);

            double headerWidth = 0;
            int measuredElements = 0;
            int skippedMeasureElements = 0;
            int measuredSlot = OwningGrid.DisplayData.FirstScrollingSlot;
            using var measureScope = DataGridDiagnostics.BeginRowsMeasure();
            bool useRetargetedRowsMeasureFastPath =
                _canUseRetargetedRowsMeasureFastPath &&
                !invalidateRows &&
                OwningGrid.UsesVirtualCellSurface;
            _canUseRetargetedRowsMeasureFastPath = false;
            if (OwningGrid.DisplayData.HasVirtualScrollingElements &&
                OwningGrid.TryGetLightweightVirtualRowHeight(out double lightweightRowHeight))
            {
                totalHeight += OwningGrid.DisplayData.NumDisplayedScrollingElements * lightweightRowHeight;
            }
            else if (useRetargetedRowsMeasureFastPath)
            {
                totalHeight += _retargetedMeasureRowCount * _retargetedMeasureRowHeight;
                skippedMeasureElements = _retargetedMeasureRowCount;
                _canUseRetargetedRowsArrangeFastPath = _retargetedRowsRemainArrangeValid;
                RetargetedRowsMeasureFastPathCount++;
                DataGridDiagnostics.RecordRowsRetargetMeasureReused(_retargetedMeasureRowCount);
            }
            else
            {
                foreach (Control element in OwningGrid.DisplayData.GetScrollingElements())
                {
                    DataGridRow? row = element as DataGridRow;
                    bool hasMatchingConstraint = _measureConstraints.TryGetValue(element, out var previousConstraint) &&
                        AreClose(previousConstraint, measureConstraint);
                    double previousDesiredHeight = element.DesiredSize.Height;
                    if (row != null)
                    {
                        if (invalidateRows)
                        {
                            row.InvalidateMeasure();
                        }
                    }

                    if (invalidateRows || !element.IsMeasureValid || !hasMatchingConstraint)
                    {
                        element.Measure(measureConstraint);
                        _measureConstraints[element] = measureConstraint;
                        measuredElements++;
                    }
                    else
                    {
                        skippedMeasureElements++;
                    }

                    if (row != null && row.HeaderCell != null)
                    {
                        headerWidth = Math.Max(headerWidth, row.HeaderCell.DesiredSize.Width);
                    }

                    if (row != null && OwningGrid.UsesFlatVisualLayout && !OwningGrid.UsesVirtualCellSurface)
                    {
                        MeasureFlatCells(row);
                    }
                    else if (row != null && OwningGrid.UsesVirtualCellSurface)
                    {
                        MeasureVirtualCompatibilityCell(row);
                    }
                    else if (element is DataGridRowGroupHeader groupHeader && groupHeader.HeaderCell != null)
                    {
                        headerWidth = Math.Max(headerWidth, groupHeader.HeaderCell.DesiredSize.Width);
                    }

                    if (measuredSlot >= 0 &&
                        (!MathUtilities.AreClose(previousDesiredHeight, element.DesiredSize.Height) ||
                         !hasMatchingConstraint))
                    {
                        OwningGrid.UpdateScrollHeightEstimate(measuredSlot, element.DesiredSize.Height);
                    }

                    totalHeight += element.DesiredSize.Height;
                    measuredSlot = OwningGrid.GetNextVisibleSlot(measuredSlot);
                }
            }

            DataGridDiagnostics.RecordRowsMeasured(measuredElements);
            DataGridDiagnostics.RecordRowsMeasureSkipped(skippedMeasureElements);

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

            MeasureVirtualCellSurface(availableSize);

            return new Size(totalCellsWidth + headerWidth, totalHeight);
        }

        private bool MatchesDesiredHeight(double arrangedHeight)
        {
            var desiredHeight = DesiredSize.Height;
            if (double.IsNaN(desiredHeight) || double.IsInfinity(desiredHeight) || desiredHeight <= 0)
            {
                return false;
            }

            var threshold = Math.Max(OwningGrid?.RowHeightEstimate ?? 1, 1);
            return Math.Abs(arrangedHeight - desiredHeight) <= threshold;
        }

        private static bool ShouldArrangeElement(Control element, Rect targetRect)
        {
            if (!element.IsArrangeValid)
            {
                return true;
            }

            var bounds = element.Bounds;
            return !MathUtilities.AreClose(bounds.X, targetRect.X) ||
                   !MathUtilities.AreClose(bounds.Y, targetRect.Y) ||
                   !MathUtilities.AreClose(bounds.Width, targetRect.Width) ||
                   !MathUtilities.AreClose(bounds.Height, targetRect.Height);
        }

        private static bool AreClose(Rect first, Rect second)
        {
            return MathUtilities.AreClose(first.X, second.X) &&
                   MathUtilities.AreClose(first.Y, second.Y) &&
                   MathUtilities.AreClose(first.Width, second.Width) &&
                   MathUtilities.AreClose(first.Height, second.Height);
        }

        private static bool AreClose(Size first, Size second)
        {
             return AreScrollInfoSizeClose(first.Width, second.Width) &&
                 AreScrollInfoSizeClose(first.Height, second.Height);
         }

         private static bool AreScrollInfoSizeClose(double first, double second)
         {
             return MathUtilities.AreClose(first, second) ||
                 Math.Abs(first - second) <= ScrollInfoSizeEpsilon;
        }

        private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
        {
            if (OwningGrid?.UseLogicalScrollable != true)
            {
                return;
            }

            e.Handled = e.Handled || OwningGrid.UpdateScroll(-e.Delta);
        }

        private void HookTopLevel(TopLevel? topLevel)
        {
            if (ReferenceEquals(_observedTopLevel, topLevel))
            {
                return;
            }

            UnhookTopLevel();
            _observedTopLevel = topLevel;

            if (_observedTopLevel != null)
            {
                _observedTopLevel.PropertyChanged += OnTopLevelPropertyChanged;
            }
        }

        private void UnhookTopLevel()
        {
            if (_observedTopLevel != null)
            {
                _observedTopLevel.PropertyChanged -= OnTopLevelPropertyChanged;
                _observedTopLevel = null;
            }
        }

        private void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Layoutable.HeightProperty &&
                e.Property != Layoutable.WidthProperty &&
                e.Property != Visual.BoundsProperty)
            {
                return;
            }

            if (sender is TopLevel topLevel && OwningGrid?.UseLogicalScrollable == true)
            {
                RefreshViewportFromTopLevel(topLevel);
            }

            InvalidateMeasure();
            InvalidateArrange();
        }

        private double GetColumnHeadersHeight()
        {
            if (OwningGrid?.AreColumnHeadersVisible != true)
            {
                return 0;
            }

            if (OwningGrid.ColumnHeaders != null)
            {
                if (OwningGrid.ColumnHeaders.Bounds.Height > 0)
                {
                    return OwningGrid.ColumnHeaders.Bounds.Height;
                }

                if (OwningGrid.ColumnHeaders.DesiredSize.Height > 0)
                {
                    return OwningGrid.ColumnHeaders.DesiredSize.Height;
                }
            }

            if (!double.IsNaN(OwningGrid.ColumnHeaderHeight))
            {
                return OwningGrid.ColumnHeaderHeight;
            }

            return 0;
        }

        private void RefreshViewportFromTopLevel(TopLevel topLevel)
        {
            if (OwningGrid == null)
            {
                return;
            }

            // A presenter that has been arranged already knows its rows area; the top-level size is only
            // a fallback for a presenter without any arrange pass. Taking the window height for an
            // embedded grid realizes rows for the whole window on every window resize (a 447 px grid in
            // a 1043 px window realized 42 instead of 16 rows with 47 columns, 150-300 ms per step)
            // before the pending layout pass corrects the viewport. The invalidation queued by the caller
            // is enough to follow a real change of the rows area.
            if (!double.IsNaN(_lastArrangeHeight) && !double.IsInfinity(_lastArrangeHeight) && _lastArrangeHeight > 0)
            {
                return;
            }

            var headerHeight = GetColumnHeadersHeight();
            var viewportHeight = topLevel.Height;
            if (double.IsNaN(viewportHeight) || double.IsInfinity(viewportHeight) || viewportHeight <= 0)
            {
                viewportHeight = topLevel.Bounds.Height;
            }

            viewportHeight = Math.Max(0, viewportHeight - headerHeight);

            if (OwningGrid.RowsPresenterAvailableSize is { } availableSize)
            {
                OwningGrid.RowsPresenterAvailableSize = availableSize.WithHeight(viewportHeight);
            }

            var viewportWidth = _viewport.Width > 0 ? _viewport.Width : Bounds.Width;
            if (!double.IsNaN(viewportWidth) && !double.IsInfinity(viewportWidth) &&
                !double.IsNaN(viewportHeight) && !double.IsInfinity(viewportHeight))
            {
                UpdateScrollInfo(_extent, new Size(Math.Max(0, viewportWidth), viewportHeight));
            }
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
            OwningGrid.DisplayData.TrimRecycledPools(this, recycleLimit, recycleLimit, recycleLimit);
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
