// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Utilities;
using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Legacy scrolling implementation using direct ScrollBar controls.
    /// This partial class contains all legacy scrolling code that will be deprecated
    /// when UseLogicalScrollable becomes the default.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        #region Legacy Scrolling Constants

        private const string DATAGRID_elementHorizontalScrollbarName = "PART_HorizontalScrollbar";
        private const string DATAGRID_elementVerticalScrollbarName = "PART_VerticalScrollbar";

        #endregion

        #region Legacy Scrolling Fields

        private ScrollBar _vScrollBar;
        private ScrollBar _hScrollBar;
        private bool _ignoreNextScrollBarsLayout;
        private byte _horizontalScrollChangesIgnored;
        private byte _verticalScrollChangesIgnored;

        #endregion

        #region Legacy Scrolling Properties

        /// <summary>
        /// Gets the horizontal scroll bar. This property is deprecated.
        /// </summary>
        /// <remarks>
        /// When UseLogicalScrollable is true, scrolling is handled via ILogicalScrollable
        /// on DataGridRowsPresenter and this property may return null or an unused ScrollBar.
        /// Use the Offset property on DataGridRowsPresenter instead.
        /// </remarks>
        [Obsolete("Use DataGridRowsPresenter.Offset for scroll position. This property will be removed in a future version.")]
        internal ScrollBar HorizontalScrollBar => _hScrollBar;

        /// <summary>
        /// Gets the vertical scroll bar. This property is deprecated.
        /// </summary>
        /// <remarks>
        /// When UseLogicalScrollable is true, scrolling is handled via ILogicalScrollable
        /// on DataGridRowsPresenter and this property may return null or an unused ScrollBar.
        /// Use the Offset property on DataGridRowsPresenter instead.
        /// </remarks>
        [Obsolete("Use DataGridRowsPresenter.Offset for scroll position. This property will be removed in a future version.")]
        internal ScrollBar VerticalScrollBar => _vScrollBar;

        #endregion

        #region Legacy Scrolling Setup

        /// <summary>
        /// Sets up the legacy scroll bars from the template.
        /// Called from OnApplyTemplate when using legacy scrolling mode.
        /// </summary>
        /// <param name="nameScope">The name scope from the applied template.</param>
        private void SetupLegacyScrollBars(INameScope nameScope)
        {
            if (_hScrollBar != null)
            {
                _hScrollBar.Scroll -= HorizontalScrollBar_Scroll;
            }

            _hScrollBar = nameScope.Find<ScrollBar>(DATAGRID_elementHorizontalScrollbarName);

            if (_hScrollBar != null)
            {
                _hScrollBar.IsTabStop = false;
                _hScrollBar.Maximum = 0.0;
                _hScrollBar.Orientation = Layout.Orientation.Horizontal;
                _hScrollBar.IsVisible = false;
                _hScrollBar.Scroll += HorizontalScrollBar_Scroll;
                _hScrollBar.AllowAutoHide = this.GetValue(ScrollViewer.AllowAutoHideProperty);
            }

            if (_vScrollBar != null)
            {
                _vScrollBar.Scroll -= VerticalScrollBar_Scroll;
            }

            _vScrollBar = nameScope.Find<ScrollBar>(DATAGRID_elementVerticalScrollbarName);

            if (_vScrollBar != null)
            {
                _vScrollBar.IsTabStop = false;
                _vScrollBar.Maximum = 0.0;
                _vScrollBar.Orientation = Layout.Orientation.Vertical;
                _vScrollBar.IsVisible = false;
                _vScrollBar.Scroll += VerticalScrollBar_Scroll;
                _vScrollBar.AllowAutoHide = this.GetValue(ScrollViewer.AllowAutoHideProperty);
            }
        }

        #endregion

        #region Legacy Scrolling Event Handlers

        private void HorizontalScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            ProcessHorizontalScroll(e.ScrollEventType);
            OnHorizontalScroll(new DataGridScrollEventArgs(e.ScrollEventType, e.NewValue, HorizontalScrollEvent, this));
        }

        private void VerticalScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            ProcessVerticalScroll(e.ScrollEventType);
            OnVerticalScroll(new DataGridScrollEventArgs(e.ScrollEventType, e.NewValue, VerticalScrollEvent, this));
        }

        #endregion

        #region Legacy Scrolling Update Methods

        internal void UpdateVerticalScrollBar()
        {
            if (_vScrollBar != null && _vScrollBar.IsVisible)
            {
                double cellsHeight = CellsEstimatedHeight;
                double edgedRowsHeightCalculated = EdgedRowsHeightCalculated;
                UpdateVerticalScrollBar(
                    needVertScrollbar: edgedRowsHeightCalculated > cellsHeight,
                    forceVertScrollbar: VerticalScrollBarVisibility == ScrollBarVisibility.Visible,
                    totalVisibleHeight: edgedRowsHeightCalculated,
                    cellsHeight: cellsHeight);
            }
        }

        private void UpdateHorizontalScrollBar(bool needHorizScrollbar, bool forceHorizScrollbar, double totalVisibleWidth, double frozenLeftWidth, double frozenRightWidth, double cellsWidth)
        {
            if (_hScrollBar != null)
            {
                if (needHorizScrollbar || forceHorizScrollbar)
                {
                    //          viewportSize
                    //        v---v
                    //|<|_____|###|>|
                    //  ^     ^
                    //  min   max

                    // we want to make the relative size of the thumb reflect the relative size of the viewing area
                    // viewportSize / (max + viewportSize) = cellsWidth / max
                    // -> viewportSize = max * cellsWidth / (max - cellsWidth)

                    // always zero
                    _hScrollBar.Minimum = 0;
                    if (needHorizScrollbar)
                    {
                        // maximum travel distance -- not the total width
                        _hScrollBar.Maximum = totalVisibleWidth - cellsWidth;
                        double totalVisibleFrozenWidth = frozenLeftWidth + frozenRightWidth;
                        Debug.Assert(totalVisibleFrozenWidth >= 0);
                        if (_frozenColumnScrollBarSpacer != null)
                        {
                            _frozenColumnScrollBarSpacer.Width = frozenLeftWidth;
                        }
                        if (_frozenColumnScrollBarSpacerRight != null)
                        {
                            _frozenColumnScrollBarSpacerRight.Width = frozenRightWidth;
                        }
                        Debug.Assert(_hScrollBar.Maximum >= 0);

                        // width of the scrollable viewing area
                        double viewPortSize = Math.Max(0, cellsWidth - totalVisibleFrozenWidth);
                        _hScrollBar.ViewportSize = viewPortSize;
                        _hScrollBar.LargeChange = viewPortSize;
                        // The ScrollBar should be in sync with HorizontalOffset at this point.  There's a resize case
                        // where the ScrollBar will coerce an old value here, but we don't want that
                        if (_hScrollBar.Value != _horizontalOffset)
                        {
                            _hScrollBar.Value = _horizontalOffset;
                        }
                        _hScrollBar.IsEnabled = true;
                    }
                    else
                    {
                        _hScrollBar.Maximum = 0;
                        _hScrollBar.ViewportSize = 0;
                        _hScrollBar.IsEnabled = false;
                    }

                    if (!_hScrollBar.IsVisible)
                    {
                        // This will trigger a call to this method via Cells_SizeChanged for
                        _ignoreNextScrollBarsLayout = true;
                        // which no processing is needed.
                        _hScrollBar.IsVisible = true;
                        if (_hScrollBar.DesiredSize.Height == 0)
                        {
                            // We need to know the height for the rest of layout to work correctly so measure it now
                            _hScrollBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        }
                    }
                }
                else
                {
                    _hScrollBar.Maximum = 0;
                    if (_hScrollBar.IsVisible)
                    {
                        // This will trigger a call to this method via Cells_SizeChanged for
                        // which no processing is needed.
                        _hScrollBar.IsVisible = false;
                        _ignoreNextScrollBarsLayout = true;
                    }
                }
            }
        }

        private void UpdateVerticalScrollBar(bool needVertScrollbar, bool forceVertScrollbar, double totalVisibleHeight, double cellsHeight)
        {
            if (_vScrollBar != null)
            {
                if (needVertScrollbar || forceVertScrollbar)
                {
                    //          viewportSize
                    //        v---v
                    //|<|_____|###|>|
                    //  ^     ^
                    //  min   max

                    // we want to make the relative size of the thumb reflect the relative size of the viewing area
                    // viewportSize / (max + viewportSize) = cellsWidth / max
                    // -> viewportSize = max * cellsHeight / (totalVisibleHeight - cellsHeight)
                    // ->              = max * cellsHeight / (totalVisibleHeight - cellsHeight)
                    // ->              = max * cellsHeight / max
                    // ->              = cellsHeight

                    // always zero
                    _vScrollBar.Minimum = 0;
                    if (needVertScrollbar && !double.IsInfinity(cellsHeight))
                    {
                        // maximum travel distance -- not the total height
                        _vScrollBar.Maximum = totalVisibleHeight - cellsHeight;
                        Debug.Assert(_vScrollBar.Maximum >= 0);

                        // total height of the display area
                        _vScrollBar.ViewportSize = cellsHeight;
                        _vScrollBar.IsEnabled = true;
                    }
                    else
                    {
                        _vScrollBar.Maximum = 0;
                        _vScrollBar.ViewportSize = 0;
                        _vScrollBar.IsEnabled = false;
                    }

                    CoerceLegacyVerticalOffsetToRange();

                    if (!_vScrollBar.IsVisible)
                    {
                        // This will trigger a call to this method via Cells_SizeChanged for
                        // which no processing is needed.
                        _vScrollBar.IsVisible = true;
                        if (_vScrollBar.DesiredSize.Width == 0)
                        {
                            // We need to know the width for the rest of layout to work correctly so measure it now
                            _vScrollBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        }
                        _ignoreNextScrollBarsLayout = true;
                    }
                }
                else
                {
                    _vScrollBar.Maximum = 0;
                    CoerceLegacyVerticalOffsetToRange();
                    if (_vScrollBar.IsVisible)
                    {
                        // This will trigger a call to this method via Cells_SizeChanged for
                        // which no processing is needed.
                        _vScrollBar.IsVisible = false;
                        _ignoreNextScrollBarsLayout = true;
                    }
                }
            }
        }

        private void CoerceLegacyVerticalOffsetToRange()
        {
            if (_vScrollBar == null)
            {
                return;
            }

            var maximum = Math.Max(0, _vScrollBar.Maximum);
            var coerced = Math.Max(0, Math.Min(_verticalOffset, maximum));

            if (!MathUtilities.AreClose(_verticalOffset, coerced))
            {
                if (DisplayData != null)
                {
                    DisplayData.PendingVerticalScrollHeight = 0;
                }

                SetVerticalOffset(coerced);
            }
            else if (!MathUtilities.AreClose(_vScrollBar.Value, coerced))
            {
                _vScrollBar.Value = coerced;
            }
        }

        /// <summary>
        /// Synchronizes the vertical scroll bar value with the internal offset.
        /// </summary>
        private void SyncVerticalScrollBarValue(double newVerticalOffset)
        {
            if (_vScrollBar != null && !Utilities.MathUtilities.AreClose(newVerticalOffset, _vScrollBar.Value))
            {
                _vScrollBar.Value = newVerticalOffset;
            }
        }

        /// <summary>
        /// Synchronizes the horizontal scroll bar value with the internal offset.
        /// </summary>
        private void SyncHorizontalScrollBarValue(double value)
        {
            if (_hScrollBar != null && value != _hScrollBar.Value)
            {
                _hScrollBar.Value = value;
            }
        }

        /// <summary>
        /// Hides both legacy scroll bars when there are no visible columns.
        /// Called from MeasureOverride.
        /// </summary>
        private void HideLegacyScrollBars()
        {
            if (_hScrollBar != null && _hScrollBar.IsVisible)
            {
                _hScrollBar.IsVisible = false;
            }
            if (_vScrollBar != null && _vScrollBar.IsVisible)
            {
                _vScrollBar.IsVisible = false;
            }
        }

        /// <summary>
        /// Gets the maximum vertical scroll value from the legacy scroll bar.
        /// Returns 0 if legacy scroll bars are not available.
        /// </summary>
        private double GetLegacyVerticalScrollMaximum()
        {
            return _vScrollBar?.Maximum ?? 0;
        }

        /// <summary>
        /// Gets the current value from the legacy vertical scroll bar.
        /// Returns the internal offset if legacy scroll bars are not available.
        /// </summary>
        private double GetLegacyVerticalScrollValue()
        {
            return _vScrollBar?.Value ?? _verticalOffset;
        }

        /// <summary>
        /// Gets the current value from the legacy horizontal scroll bar.
        /// Returns the internal offset if legacy scroll bars are not available.
        /// </summary>
        private double GetLegacyHorizontalScrollValue()
        {
            return _hScrollBar?.Value ?? _horizontalOffset;
        }

        /// <summary>
        /// Returns true if legacy scroll bars are available and visible for scrolling calculations.
        /// </summary>
        private bool HasLegacyVerticalScrollBar => !UseLogicalScrollable && _vScrollBar != null;

        /// <summary>
        /// Returns true if the legacy vertical scroll bar is visible.
        /// Used for conditional logic when scroll bar visibility affects behavior.
        /// </summary>
        private bool IsLegacyVerticalScrollBarVisible => _vScrollBar?.IsVisible ?? false;

        /// <summary>
        /// Returns true if legacy horizontal scroll bar is available.
        /// </summary>
        internal bool HasLegacyHorizontalScrollBar => _hScrollBar != null;

        /// <summary>
        /// Returns true if the legacy horizontal scroll bar is visible.
        /// </summary>
        internal bool IsLegacyHorizontalScrollBarVisible => _hScrollBar?.IsVisible ?? false;

        /// <summary>
        /// Gets the maximum horizontal scroll value from the legacy scroll bar.
        /// Returns 0 if legacy scroll bars are not available.
        /// </summary>
        internal double GetLegacyHorizontalScrollMaximum()
        {
            return _hScrollBar?.Maximum ?? 0;
        }

        /// <summary>
        /// Attempts to scroll left during column drag operations.
        /// Returns the actual scroll amount applied, or 0 if scrolling not possible.
        /// </summary>
        internal double TryScrollLeftForColumnDrag(double requestedAmount)
        {
            if (_hScrollBar == null || !_hScrollBar.IsVisible || _hScrollBar.Value <= 0)
            {
                return 0;
            }
            
            double scrollAmount = Math.Min(requestedAmount, _hScrollBar.Value);
            UpdateHorizontalOffset(scrollAmount + _hScrollBar.Value);
            return scrollAmount;
        }

        /// <summary>
        /// Attempts to scroll right during column drag operations.
        /// Returns the actual scroll amount applied, or 0 if scrolling not possible.
        /// </summary>
        internal double TryScrollRightForColumnDrag(double requestedAmount)
        {
            if (_hScrollBar == null || !_hScrollBar.IsVisible || _hScrollBar.Value >= _hScrollBar.Maximum)
            {
                return 0;
            }
            
            double scrollAmount = Math.Min(requestedAmount, _hScrollBar.Maximum - _hScrollBar.Value);
            UpdateHorizontalOffset(scrollAmount + _hScrollBar.Value);
            return scrollAmount;
        }

        #endregion

        #region Legacy Scrolling Layout

        private bool IsHorizontalScrollBarOverCells
        {
            get
            {
                return _columnHeadersPresenter != null && Grid.GetColumnSpan(_columnHeadersPresenter) == 2;
            }
        }

        private bool IsVerticalScrollBarOverCells
        {
            get
            {
                return _rowsPresenter != null && Grid.GetRowSpan(_rowsPresenter) == 2;
            }
        }

        private void ComputeScrollBarsLayout()
        {
            if (_ignoreNextScrollBarsLayout)
            {
                _ignoreNextScrollBarsLayout = false;
                //

            }

            bool isHorizontalScrollBarOverCells = IsHorizontalScrollBarOverCells;
            bool isVerticalScrollBarOverCells = IsVerticalScrollBarOverCells;

            double cellsWidth = CellsWidth;
            double cellsHeight = CellsEstimatedHeight;

            bool allowHorizScrollbar = false;
            bool forceHorizScrollbar = false;
            double horizScrollBarHeight = 0;
            if (_hScrollBar != null)
            {
                forceHorizScrollbar = HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;
                allowHorizScrollbar = forceHorizScrollbar || (ColumnsInternal.VisibleColumnCount > 0 &&
                    HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                    HorizontalScrollBarVisibility != ScrollBarVisibility.Hidden);
                // Compensate if the horizontal scrollbar is already taking up space
                if (!forceHorizScrollbar && _hScrollBar.IsVisible)
                {
                    if (!isHorizontalScrollBarOverCells)
                    {
                        cellsHeight += _hScrollBar.DesiredSize.Height;
                    }
                }
                if (!isHorizontalScrollBarOverCells)
                {
                    horizScrollBarHeight = _hScrollBar.Height + _hScrollBar.Margin.Top + _hScrollBar.Margin.Bottom;
                }
            }

            bool allowVertScrollbar = false;
            bool forceVertScrollbar = false;
            double vertScrollBarWidth = 0;
            if (_vScrollBar != null)
            {
                forceVertScrollbar = VerticalScrollBarVisibility == ScrollBarVisibility.Visible;
                allowVertScrollbar = forceVertScrollbar || (ColumnsItemsInternal.Count > 0 &&
                    VerticalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                    VerticalScrollBarVisibility != ScrollBarVisibility.Hidden);
                // Compensate if the vertical scrollbar is already taking up space
                if (!forceVertScrollbar && _vScrollBar.IsVisible)
                {
                    if (!isVerticalScrollBarOverCells)
                    {
                        cellsWidth += _vScrollBar.DesiredSize.Width;
                    }
                }
                if (!isVerticalScrollBarOverCells)
                {
                    vertScrollBarWidth = _vScrollBar.Width + _vScrollBar.Margin.Left + _vScrollBar.Margin.Right;
                }
            }

            // Now cellsWidth is the width potentially available for displaying data cells.
            // Now cellsHeight is the height potentially available for displaying data cells.

            bool needHorizScrollbar = false;
            bool needVertScrollbar = false;

            double totalVisibleWidth = ColumnsInternal.VisibleEdgedColumnsWidth;
            double frozenLeftWidth = ColumnsInternal.GetVisibleFrozenLeftEdgedColumnsWidth();
            double frozenRightWidth = ColumnsInternal.GetVisibleFrozenRightEdgedColumnsWidth();
            double totalVisibleFrozenWidth = frozenLeftWidth + frozenRightWidth;

            UpdateDisplayedRows(DisplayData.FirstScrollingSlot, CellsEstimatedHeight);
            double totalVisibleHeight = EdgedRowsHeightCalculated;

            if (!forceHorizScrollbar && !forceVertScrollbar)
            {
                bool needHorizScrollbarWithoutVertScrollbar = false;

                if (allowHorizScrollbar &&
                    MathUtilities.GreaterThan(totalVisibleWidth, cellsWidth) &&
                    MathUtilities.LessThan(totalVisibleFrozenWidth, cellsWidth) &&
                    MathUtilities.LessThanOrClose(horizScrollBarHeight, cellsHeight))
                {
                    double oldDataHeight = cellsHeight;
                    cellsHeight -= horizScrollBarHeight;
                    Debug.Assert(cellsHeight >= 0);
                    needHorizScrollbarWithoutVertScrollbar = needHorizScrollbar = true;

                    if (vertScrollBarWidth > 0 &&
                        allowVertScrollbar && (MathUtilities.LessThanOrClose(totalVisibleWidth - cellsWidth, vertScrollBarWidth) ||
                        MathUtilities.LessThanOrClose(cellsWidth - totalVisibleFrozenWidth, vertScrollBarWidth)))
                    {
                        // Would we still need a horizontal scrollbar without the vertical one?
                        UpdateDisplayedRows(DisplayData.FirstScrollingSlot, cellsHeight);
                        if (DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount)
                        {
                            needHorizScrollbar = MathUtilities.LessThan(totalVisibleFrozenWidth, cellsWidth - vertScrollBarWidth);
                        }
                    }

                    if (!needHorizScrollbar)
                    {
                        // Restore old data height because turns out a horizontal scroll bar wouldn't make sense
                        cellsHeight = oldDataHeight;
                    }
                }

                // Store the current FirstScrollingSlot because removing the horizontal scrollbar could scroll
                // the DataGrid up; however, if we realize later that we need to keep the horizontal scrollbar
                // then we should use the first slot stored here which is not scrolled.
                int firstScrollingSlot = DisplayData.FirstScrollingSlot;

                UpdateDisplayedRows(firstScrollingSlot, cellsHeight);
                if (allowVertScrollbar &&
                    MathUtilities.GreaterThan(cellsHeight, 0) &&
                    MathUtilities.LessThanOrClose(vertScrollBarWidth, cellsWidth) &&
                    DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount)
                {
                    cellsWidth -= vertScrollBarWidth;
                    Debug.Assert(cellsWidth >= 0);
                    needVertScrollbar = true;
                }

                DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();

                // we compute the number of visible columns only after we set up the vertical scroll bar.
                ComputeDisplayedColumns();

                if ((vertScrollBarWidth > 0 || horizScrollBarHeight > 0) &&
                    allowHorizScrollbar &&
                    needVertScrollbar && !needHorizScrollbar &&
                    MathUtilities.GreaterThan(totalVisibleWidth, cellsWidth) &&
                    MathUtilities.LessThan(totalVisibleFrozenWidth, cellsWidth) &&
                    MathUtilities.LessThanOrClose(horizScrollBarHeight, cellsHeight))
                {
                    cellsWidth += vertScrollBarWidth;
                    cellsHeight -= horizScrollBarHeight;
                    Debug.Assert(cellsHeight >= 0);
                    needVertScrollbar = false;

                    UpdateDisplayedRows(firstScrollingSlot, cellsHeight);
                    if (cellsHeight > 0 &&
                        vertScrollBarWidth <= cellsWidth &&
                        DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount)
                    {
                        cellsWidth -= vertScrollBarWidth;
                        Debug.Assert(cellsWidth >= 0);
                        needVertScrollbar = true;
                    }
                    if (needVertScrollbar)
                    {
                        needHorizScrollbar = true;
                    }
                    else
                    {
                        needHorizScrollbar = needHorizScrollbarWithoutVertScrollbar;
                    }
                }
            }
            else if (forceHorizScrollbar && !forceVertScrollbar)
            {
                if (allowVertScrollbar)
                {
                    if (cellsHeight > 0 &&
                        MathUtilities.LessThanOrClose(vertScrollBarWidth, cellsWidth) &&
                        DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount)
                    {
                        cellsWidth -= vertScrollBarWidth;
                        Debug.Assert(cellsWidth >= 0);
                        needVertScrollbar = true;
                    }
                    DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();
                    ComputeDisplayedColumns();
                }
                needHorizScrollbar = totalVisibleWidth > cellsWidth && totalVisibleFrozenWidth < cellsWidth;
            }
            else if (!forceHorizScrollbar && forceVertScrollbar)
            {
                if (allowHorizScrollbar)
                {
                    if (cellsWidth > 0 &&
                        MathUtilities.LessThanOrClose(horizScrollBarHeight, cellsHeight) &&
                        MathUtilities.GreaterThan(totalVisibleWidth, cellsWidth) &&
                        MathUtilities.LessThan(totalVisibleFrozenWidth, cellsWidth))
                    {
                        cellsHeight -= horizScrollBarHeight;
                        Debug.Assert(cellsHeight >= 0);
                        needHorizScrollbar = true;
                        UpdateDisplayedRows(DisplayData.FirstScrollingSlot, cellsHeight);
                    }
                    DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();
                    ComputeDisplayedColumns();
                }
                needVertScrollbar = DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount;
            }
            else
            {
                Debug.Assert(forceHorizScrollbar && forceVertScrollbar);
                Debug.Assert(allowHorizScrollbar && allowVertScrollbar);
                DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();
                ComputeDisplayedColumns();
                needVertScrollbar = DisplayData.NumTotallyDisplayedScrollingElements != VisibleSlotCount;
                needHorizScrollbar = totalVisibleWidth > cellsWidth && totalVisibleFrozenWidth < cellsWidth;
            }

            UpdateHorizontalScrollBar(needHorizScrollbar, forceHorizScrollbar, totalVisibleWidth, frozenLeftWidth, frozenRightWidth, cellsWidth);
            UpdateVerticalScrollBar(needVertScrollbar, forceVertScrollbar, totalVisibleHeight, cellsHeight);

            if (_topRightCornerHeader != null)
            {
                // Show the TopRightHeaderCell based on vertical ScrollBar visibility
                if (AreColumnHeadersVisible &&
                    _vScrollBar != null && _vScrollBar.IsVisible)
                {
                    _topRightCornerHeader.IsVisible = true;
                }
                else
                {
                    _topRightCornerHeader.IsVisible = false;
                }
            }

            if (_bottomRightCorner != null)
            {                // Show the BottomRightCorner when both scrollbars are visible.
                _bottomRightCorner.IsVisible =
                    _hScrollBar != null && _hScrollBar.IsVisible &&
                    _vScrollBar != null && _vScrollBar.IsVisible;
            }
        }

        #endregion

        #region Legacy Scroll Event Processing

        /// <summary>
        /// Process horizontal scroll from legacy scroll bar events.
        /// This method is only called when using legacy scroll bars (UseLogicalScrollable = false).
        /// </summary>
        internal void ProcessHorizontalScroll(ScrollEventType scrollEventType)
        {
            if (_horizontalScrollChangesIgnored > 0)
            {
                return;
            }

            // Guard for when legacy scroll bars aren't in the template
            if (_hScrollBar == null)
            {
                return;
            }

            // If the user scrolls with the buttons, we need to update the new value of the scroll bar since we delay
            // this calculation.  If they scroll in another other way, the scroll bar's correct value has already been set
            double scrollBarValueDifference = 0;
            if (scrollEventType == ScrollEventType.SmallIncrement)
            {
                scrollBarValueDifference = GetHorizontalSmallScrollIncrease();
            }
            else if (scrollEventType == ScrollEventType.SmallDecrement)
            {
                scrollBarValueDifference = -GetHorizontalSmallScrollDecrease();
            }
            _horizontalScrollChangesIgnored++;
            try
            {
                if (scrollBarValueDifference != 0)
                {
                    Debug.Assert(_horizontalOffset + scrollBarValueDifference >= 0);
                    SyncHorizontalScrollBarValue(_horizontalOffset + scrollBarValueDifference);
                }
                UpdateHorizontalOffset(GetLegacyHorizontalScrollValue());
            }
            finally
            {
                _horizontalScrollChangesIgnored--;
            }
        }

        /// <summary>
        /// Process vertical scroll from legacy scroll bar events.
        /// This method is only called when using legacy scroll bars (UseLogicalScrollable = false).
        /// </summary>
        internal void ProcessVerticalScroll(ScrollEventType scrollEventType)
        {
            if (_verticalScrollChangesIgnored > 0)
            {
                return;
            }

            // Guard for when legacy scroll bars aren't in the template
            if (_vScrollBar == null)
            {
                return;
            }

            double vScrollValue = GetLegacyVerticalScrollValue();
            double vScrollMax = GetLegacyVerticalScrollMaximum();
            Debug.Assert(MathUtilities.LessThanOrClose(vScrollValue, vScrollMax));

            _verticalScrollChangesIgnored++;
            try
            {
                if (scrollEventType == ScrollEventType.SmallIncrement)
                {
                    DisplayData.PendingVerticalScrollHeight = GetVerticalSmallScrollIncrease();
                    double newVerticalOffset = _verticalOffset + DisplayData.PendingVerticalScrollHeight;
                    if (newVerticalOffset > vScrollMax)
                    {
                        DisplayData.PendingVerticalScrollHeight -= newVerticalOffset - vScrollMax;
                    }
                }
                else if (scrollEventType == ScrollEventType.SmallDecrement)
                {
                    if (MathUtilities.GreaterThan(NegVerticalOffset, 0))
                    {
                        DisplayData.PendingVerticalScrollHeight -= NegVerticalOffset;
                    }
                    else
                    {
                        int previousScrollingSlot = GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot);
                        if (previousScrollingSlot >= 0)
                        {
                            ScrollSlotIntoView(previousScrollingSlot, scrolledHorizontally: false);
                        }
                        return;
                    }
                }
                else
                {
                    DisplayData.PendingVerticalScrollHeight = vScrollValue - _verticalOffset;
                }

                if (!MathUtilities.IsZero(DisplayData.PendingVerticalScrollHeight))
                {
                    // Invalidate so the scroll happens on idle
                    InvalidateRowsMeasure(invalidateIndividualElements: false);
                }
            }
            finally
            {
                _verticalScrollChangesIgnored--;
            }
        }

        #endregion
    }
}
