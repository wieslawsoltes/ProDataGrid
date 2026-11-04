// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.LogicalTree;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.DataGridBanding;
using Avalonia.Styling;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// Used within the template of a <see cref="T:Avalonia.Controls.DataGrid" /> to specify the 
    /// location in the control's visual tree where the column headers are to be added.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    sealed class DataGridColumnHeadersPresenter : Panel, IChildIndexProvider
    {
        private Control _dragIndicator;
        private Control _dropLocationIndicator;
        private EventHandler<ChildIndexChangedEventArgs> _childIndexChanged;
        private readonly List<DataGridColumn> _visibleBandColumns = new();
        private readonly List<DataGridColumnBandHeaderCell> _bandHeaderCells = new();
        private int _bandHeaderRowCount = 1;

        /// <summary>
        /// Tracks which column is currently being dragged.
        /// </summary>
        internal DataGridColumn DragColumn
        {
            get;
            set;
        }

        /// <summary>
        /// The current drag indicator control.  This value is null if no column is being dragged.
        /// </summary>
        internal Control DragIndicator
        {
            get
            {
                return _dragIndicator;
            }
            set
            {
                if (value != _dragIndicator)
                {
                    if (Children.Contains(_dragIndicator))
                    {
                        Children.Remove(_dragIndicator);
                    }
                    _dragIndicator = value;
                    if (_dragIndicator != null)
                    {
                        Children.Add(_dragIndicator);
                    }
                }
            }
        }

        /// <summary>
        /// The distance, in pixels, that the DragIndicator should be positioned away from the corresponding DragColumn.
        /// </summary>
        internal Double DragIndicatorOffset
        {
            get;
            set;
        }

        /// <summary>
        /// The drop location indicator control.  This value is null if no column is being dragged.
        /// </summary>
        internal Control DropLocationIndicator
        {
            get
            {
                return _dropLocationIndicator;
            }
            set
            {
                if (value != _dropLocationIndicator)
                {
                    if (Children.Contains(_dropLocationIndicator))
                    {
                        Children.Remove(_dropLocationIndicator);
                    }
                    _dropLocationIndicator = value;
                    if (_dropLocationIndicator != null)
                    {
                        Children.Add(_dropLocationIndicator);
                    }
                }
            }
        }

        /// <summary>
        /// The distance, in pixels, that the drop location indicator should be positioned away from the left edge
        /// of the ColumnsHeaderPresenter.
        /// </summary>
        internal double DropLocationIndicatorOffset
        {
            get;
            set;
        }

        internal DataGrid OwningGrid
        {
            get;
            set;
        }

        event EventHandler<ChildIndexChangedEventArgs> IChildIndexProvider.ChildIndexChanged
        {
            add => _childIndexChanged += value;
            remove => _childIndexChanged -= value;
        }

        int IChildIndexProvider.GetChildIndex(ILogical child)
        {
            if (child is DataGridColumnHeader header)
            {
                return header.OwningColumn?.DisplayIndex ?? -1;
            }

            if (child is DataGridColumnBandHeaderCell bandHeader &&
                (uint)bandHeader.StartColumnIndex < (uint)_visibleBandColumns.Count)
            {
                return _visibleBandColumns[bandHeader.StartColumnIndex].DisplayIndex;
            }

            return -1;
        }

        bool IChildIndexProvider.TryGetTotalCount(out int count)
        {
            count = 0;
            foreach (Control child in Children)
            {
                if (child is DataGridColumnHeader { OwningColumn: not DataGridFillerColumn })
                {
                    count++;
                }
            }

            return true;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DataGridColumnHeadersPresenterAutomationPeer(this);
        }

        /// <summary>
        /// Arranges the content of the <see cref="T:Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter" />.
        /// </summary>
        /// <returns>
        /// The actual size used by the <see cref="T:Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter" />.
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

            if (OwningGrid.AutoSizingColumns)
            {
                // When we initially load an auto-column, we have to wait for all the rows to be measured
                // before we know its final desired size.  We need to trigger a new round of measures now
                // that the final sizes have been calculated.
                OwningGrid.AutoSizingColumns = false;
                return base.ArrangeOverride(finalSize);
            }

            double dragIndicatorLeftEdge = 0;
            double frozenLeftEdge = 0;
            double frozenLeftWidth = OwningGrid.GetVisibleFrozenColumnsWidthLeft();
            double frozenRightWidth = OwningGrid.GetVisibleFrozenColumnsWidthRight();
            double rightFrozenStart = frozenRightWidth > 0 ? Math.Max(0, OwningGrid.CellsWidth - frozenRightWidth) : double.PositiveInfinity;
            double rightFrozenEdge = frozenRightWidth > 0 ? rightFrozenStart : 0;
            double scrollingLeftEdge = -OwningGrid.HorizontalOffset;
            double lastScrollingRightEdge = frozenLeftWidth;
            foreach (DataGridColumn dataGridColumn in OwningGrid.ColumnsInternal.GetVisibleColumns())
            {
                DataGridColumnHeader columnHeader = dataGridColumn.HeaderCell;
                Debug.Assert(columnHeader.OwningColumn == dataGridColumn);
                GetColumnHeaderVerticalBounds(dataGridColumn, finalSize.Height, out double headerTop, out double headerHeight);

                if (dataGridColumn.IsFrozenLeft)
                {
                    columnHeader.Arrange(new Rect(frozenLeftEdge, headerTop, dataGridColumn.LayoutRoundedWidth, headerHeight));
                    columnHeader.Clip = null; // The layout system could have clipped this because it's not aware of our render transform
                    if (DragColumn == dataGridColumn && DragIndicator != null)
                    {
                        dragIndicatorLeftEdge = frozenLeftEdge + DragIndicatorOffset;
                    }
                    frozenLeftEdge += dataGridColumn.ActualWidth;
                }
                else if (dataGridColumn.IsFrozenRight)
                {
                    columnHeader.Arrange(new Rect(rightFrozenEdge, headerTop, dataGridColumn.LayoutRoundedWidth, headerHeight));
                    columnHeader.Clip = null; // The layout system could have clipped this because it's not aware of our render transform
                    if (DragColumn == dataGridColumn && DragIndicator != null)
                    {
                        dragIndicatorLeftEdge = rightFrozenEdge + DragIndicatorOffset;
                    }
                    rightFrozenEdge += dataGridColumn.ActualWidth;
                }
                else
                {
                    columnHeader.Arrange(new Rect(scrollingLeftEdge, headerTop, dataGridColumn.LayoutRoundedWidth, headerHeight));
                    EnsureColumnHeaderClip(columnHeader, dataGridColumn.ActualWidth, headerHeight, frozenLeftWidth, rightFrozenStart, scrollingLeftEdge);
                    if (DragColumn == dataGridColumn && DragIndicator != null)
                    {
                        dragIndicatorLeftEdge = scrollingLeftEdge + DragIndicatorOffset;
                    }
                    lastScrollingRightEdge = Math.Max(lastScrollingRightEdge, scrollingLeftEdge + dataGridColumn.ActualWidth);
                }
                scrollingLeftEdge += dataGridColumn.ActualWidth;
            }

            ArrangeBandHeaderCells(finalSize.Height, frozenLeftWidth, rightFrozenStart);

            if (DragColumn != null)
            {
                if (DragIndicator != null)
                {
                    EnsureColumnReorderingClip(DragIndicator, finalSize.Height, frozenLeftWidth, rightFrozenStart, dragIndicatorLeftEdge);

                    var height = DragIndicator.Bounds.Height;
                    if (height <= 0)
                        height = DragIndicator.DesiredSize.Height;

                    DragIndicator.Arrange(new Rect(dragIndicatorLeftEdge, 0, DragIndicator.Bounds.Width, height));
                }
                if (DropLocationIndicator != null)
                {
                    if (DropLocationIndicator is Control element)
                    {
                        EnsureColumnReorderingClip(element, finalSize.Height, frozenLeftWidth, rightFrozenStart, DropLocationIndicatorOffset);
                    }

                    DropLocationIndicator.Arrange(new Rect(DropLocationIndicatorOffset, 0, DropLocationIndicator.Bounds.Width, DropLocationIndicator.Bounds.Height));
                }
            }

            // Arrange filler
            OwningGrid.OnFillerColumnWidthNeeded(finalSize.Width);
            DataGridFillerColumn fillerColumn = OwningGrid.ColumnsInternal.FillerColumn;
            if (fillerColumn.FillerWidth > 0)
            {
                fillerColumn.HeaderCell.IsVisible = true;
                fillerColumn.HeaderCell.Arrange(new Rect(lastScrollingRightEdge, 0, fillerColumn.FillerWidth, finalSize.Height));
            }
            else
            {
                fillerColumn.HeaderCell.IsVisible = false;
            }

            // This needs to be updated after the filler column is configured
            DataGridColumn lastVisibleColumn = OwningGrid.ColumnsInternal.LastVisibleColumn;
            if (lastVisibleColumn != null)
            {
                lastVisibleColumn.HeaderCell.UpdateSeparatorVisibility(lastVisibleColumn);
            }
            return finalSize;
        }

        private static void EnsureColumnHeaderClip(DataGridColumnHeader columnHeader, double width, double height, double frozenLeftWidth, double rightFrozenStart, double columnHeaderLeftEdge)
        {
            // Clip the header only if it's scrolled under frozen columns. Unfortunately, we need to clip in this case
            // because headers could be transparent.
            double leftClip = Math.Max(0, frozenLeftWidth - columnHeaderLeftEdge);
            double rightClip = rightFrozenStart < double.PositiveInfinity
                ? Math.Max(0, (columnHeaderLeftEdge + width) - rightFrozenStart)
                : 0;

            if (leftClip > 0 || rightClip > 0)
            {
                RectangleGeometry rg = new RectangleGeometry();
                double clipWidth = Math.Max(0, width - leftClip - rightClip);
                rg.Rect = new Rect(leftClip, 0, clipWidth, height);
                columnHeader.Clip = rg;
            }
            else
            {
                columnHeader.Clip = null;
            }
        }

        /// <summary>
        /// Clips the DragIndicator and DropLocationIndicator controls according to current ColumnHeaderPresenter constraints.
        /// </summary>
        /// <param name="control">The DragIndicator or DropLocationIndicator</param>
        /// <param name="height">The available height</param>
        /// <param name="frozenColumnsWidth">The width of the frozen column region</param>
        /// <param name="controlLeftEdge">The left edge of the control to clip</param>
        private void EnsureColumnReorderingClip(Control control, double height, double frozenLeftWidth, double rightFrozenStart, double controlLeftEdge)
        {
            double leftEdge = 0;
            double rightEdge = OwningGrid.CellsWidth;
            double width = control.Bounds.Width;
            if (DragColumn.IsFrozenLeft)
            {
                // If we're dragging a frozen column, we want to clip the corresponding DragIndicator control when it goes
                // into the scrolling columns region, but not the DropLocationIndicator.
                if (control == DragIndicator)
                {
                    rightEdge = Math.Min(rightEdge, frozenLeftWidth);
                }
            }
            else if (DragColumn.IsFrozenRight)
            {
                if (control == DragIndicator)
                {
                    leftEdge = Math.Max(leftEdge, rightFrozenStart);
                }
            }
            else
            {
                // If we're dragging a scrolling column, we want to clip both the DragIndicator and the DropLocationIndicator
                // controls when they go into either frozen column range.
                leftEdge = frozenLeftWidth;
                rightEdge = Math.Min(rightEdge, rightFrozenStart);
            }
            RectangleGeometry rg = null;
            if (leftEdge > controlLeftEdge)
            {
                rg = new RectangleGeometry();
                double xClip = Math.Min(width, leftEdge - controlLeftEdge);
                rg.Rect = new Rect(xClip, 0, width - xClip, height);
            }
            if (controlLeftEdge + width >= rightEdge)
            {
                if (rg == null)
                {
                    rg = new RectangleGeometry();
                }
                rg.Rect = new Rect(rg.Rect.X, rg.Rect.Y, Math.Max(0, rightEdge - controlLeftEdge - rg.Rect.X), height);
            }
            control.Clip = rg;
        }

        /// <summary>
        /// Measures the children of a <see cref="T:Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter" /> to 
        /// prepare for arranging them during the <see cref="M:System.Windows.FrameworkElement.ArrangeOverride(System.Windows.Size)" /> pass.
        /// </summary>
        /// <param name="availableSize">
        /// The available size that this element can give to child elements. Indicates an upper limit that child elements should not exceed.
        /// </param>
        /// <returns>
        /// The size that the <see cref="T:Avalonia.Controls.Primitives.DataGridColumnHeadersPresenter" /> determines it needs during layout, based on its calculations of child object allocated sizes.
        /// </returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (OwningGrid == null)
            {
                return base.MeasureOverride(availableSize);
            }
            if (!OwningGrid.AreColumnHeadersVisible)
            {
                return default;
            }
            UpdateBandHeaderCells();

            double height = OwningGrid.ColumnHeaderHeight;
            bool autoSizeHeight;
            if (double.IsNaN(height))
            {
                // No explicit height values were set so we can autosize
                height = 0;
                autoSizeHeight = true;
            }
            else
            {
                autoSizeHeight = false;
            }

            double totalDisplayWidth = 0;
            OwningGrid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
            DataGridColumn lastVisibleColumn = OwningGrid.ColumnsInternal.LastVisibleColumn;
            foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetVisibleColumns())
            {
                // Measure each column header
                bool autoGrowWidth = column.Width.IsAuto || column.Width.IsSizeToHeader;
                DataGridColumnHeader columnHeader = column.HeaderCell;
                if (column != lastVisibleColumn)
                {
                    columnHeader.UpdateSeparatorVisibility(lastVisibleColumn);
                }

                // If we're not using star sizing or the current column can't be resized,
                // then just set the display width according to the column's desired width
                if (!OwningGrid.UsesStarSizing || (!column.ActualCanUserResize && !column.Width.IsStar))
                {
                    // In the edge-case where we're given infinite width and we have star columns, the 
                    // star columns grow to their predefined limit of 10,000 (or their MaxWidth)
                    double newDisplayWidth = column.Width.IsStar ?
                        Math.Min(column.ActualMaxWidth, DataGrid.DATAGRID_maximumStarColumnWidth) :
                        Math.Max(column.ActualMinWidth, Math.Min(column.ActualMaxWidth, column.Width.DesiredValue));
                    column.SetWidthDisplayValue(newDisplayWidth);
                }

                // If we're auto-growing the column based on the header content, we want to measure it at its maximum value
                if (autoGrowWidth)
                {
                    columnHeader.Measure(new Size(column.ActualMaxWidth, double.PositiveInfinity));
                    OwningGrid.AutoSizeColumn(column, columnHeader.DesiredSize.Width);
                    column.ComputeLayoutRoundedWidth(totalDisplayWidth);
                }
                else if (!OwningGrid.UsesStarSizing)
                {
                    column.ComputeLayoutRoundedWidth(totalDisplayWidth);
                    columnHeader.Measure(new Size(column.LayoutRoundedWidth, double.PositiveInfinity));
                }

                // We need to track the largest height in order to auto-size
                if (autoSizeHeight)
                {
                    height = Math.Max(height, columnHeader.DesiredSize.Height);
                }
                totalDisplayWidth += column.ActualWidth;
            }

            // If we're using star sizing (and we're not waiting for an auto-column to finish growing)
            // then we will resize all the columns to fit the available space.
            if (OwningGrid.UsesStarSizing && !OwningGrid.AutoSizingColumns)
            {
                // With the ScrollViewer template, headers and cells must share the same width excluding the scroll bar.
                var availableCellsWidth = OwningGrid.UseLogicalScrollable && OwningGrid.RowsPresenterAvailableSize.HasValue
                    ? OwningGrid.CellsWidth
                    : availableSize.Width;
                double adjustment = Double.IsPositiveInfinity(availableCellsWidth) ? OwningGrid.CellsWidth : availableCellsWidth - totalDisplayWidth;
                totalDisplayWidth += adjustment - OwningGrid.AdjustColumnWidths(0, adjustment, false);

                // Since we didn't know the final widths of the columns until we resized,
                // we waited until now to measure each header
                double leftEdge = 0;
                foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetVisibleColumns())
                {
                    column.ComputeLayoutRoundedWidth(leftEdge);
                    column.HeaderCell.Measure(new Size(column.LayoutRoundedWidth, double.PositiveInfinity));
                    if (autoSizeHeight)
                    {
                        height = Math.Max(height, column.HeaderCell.DesiredSize.Height);
                    }
                    leftEdge += column.ActualWidth;
                }
            }

            foreach (DataGridColumnBandHeaderCell bandHeader in _bandHeaderCells)
            {
                bandHeader.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                AutoSizeColumnsForBandHeader(bandHeader, bandHeader.DesiredSize.Width);
                bandHeader.Measure(new Size(GetBandHeaderWidth(bandHeader), double.PositiveInfinity));
                if (autoSizeHeight)
                {
                    height = Math.Max(height, bandHeader.DesiredSize.Height);
                }
            }

            if (autoSizeHeight && _bandHeaderRowCount > 1)
            {
                height *= _bandHeaderRowCount;
            }

            // Add the filler column if it's not represented.  We won't know whether we need it or not until Arrange
            DataGridFillerColumn fillerColumn = OwningGrid.ColumnsInternal.FillerColumn;
            if (!fillerColumn.IsRepresented)
            {
                Debug.Assert(!Children.Contains(fillerColumn.HeaderCell));
                fillerColumn.HeaderCell.AreSeparatorsVisible = false;
                var insertionIndex = Math.Clamp(OwningGrid.ColumnsInternal.Count, 0, Children.Count);
                Children.Insert(insertionIndex, fillerColumn.HeaderCell);
                fillerColumn.IsRepresented = true;
                // Optimize for the case where we don't need the filler cell 
                fillerColumn.HeaderCell.IsVisible = false;
            }
            fillerColumn.HeaderCell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (DragIndicator != null)
            {
                DragIndicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
            if (DropLocationIndicator != null)
            {
                DropLocationIndicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            OwningGrid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
            return new Size(OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth, height);
        }

        private void UpdateBandHeaderCells()
        {
            _visibleBandColumns.Clear();
            int maximumDepth = 1;
            bool hasGroupedHeaders = false;

            foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetVisibleColumns())
            {
                _visibleBandColumns.Add(column);
                if (TryGetGroupedHeader(column, out ColumnBandHeader header))
                {
                    hasGroupedHeaders = true;
                    maximumDepth = Math.Max(maximumDepth, header.Segments.Count);
                }
            }

            _bandHeaderRowCount = hasGroupedHeaders ? maximumDepth : 1;
            int bandCellIndex = 0;
            if (hasGroupedHeaders)
            {
                for (int level = 0; level < maximumDepth - 1; level++)
                {
                    int columnIndex = 0;
                    while (columnIndex < _visibleBandColumns.Count)
                    {
                        DataGridColumn startColumn = _visibleBandColumns[columnIndex];
                        if (!TryGetGroupedHeader(startColumn, out ColumnBandHeader startHeader) ||
                            startHeader.Segments.Count <= level + 1)
                        {
                            columnIndex++;
                            continue;
                        }

                        int startIndex = columnIndex;
                        columnIndex++;
                        while (columnIndex < _visibleBandColumns.Count &&
                               IsSameBandGroup(startColumn, startHeader, _visibleBandColumns[columnIndex], level))
                        {
                            columnIndex++;
                        }

                        DataGridColumnBandHeaderCell cell = GetOrCreateBandHeaderCell(bandCellIndex++);
                        cell.Content = startHeader.Segments[level];
                        cell.StartColumnIndex = startIndex;
                        cell.EndColumnIndex = columnIndex - 1;
                        cell.Level = level;
                        cell.IsFrozenLeft = startColumn.IsFrozenLeft;
                        cell.IsFrozenRight = startColumn.IsFrozenRight;
                    }
                }
            }

            while (_bandHeaderCells.Count > bandCellIndex)
            {
                int index = _bandHeaderCells.Count - 1;
                DataGridColumnBandHeaderCell cell = _bandHeaderCells[index];
                _bandHeaderCells.RemoveAt(index);
                Children.Remove(cell);
            }
        }

        private DataGridColumnBandHeaderCell GetOrCreateBandHeaderCell(int index)
        {
            if (index < _bandHeaderCells.Count)
            {
                DataGridColumnBandHeaderCell existing = _bandHeaderCells[index];
                EnsureBandHeaderCellTheme(existing);
                if (!Children.Contains(existing))
                {
                    Children.Add(existing);
                }

                return existing;
            }

            DataGridColumnBandHeaderCell cell = new DataGridColumnBandHeaderCell();
            EnsureBandHeaderCellTheme(cell);
            _bandHeaderCells.Add(cell);
            Children.Add(cell);
            return cell;
        }

        private void EnsureBandHeaderCellTheme(DataGridColumnBandHeaderCell cell)
        {
            if (cell.Theme == null &&
                OwningGrid.TryFindResource(typeof(DataGridColumnBandHeaderCell), out object theme) &&
                theme is ControlTheme controlTheme)
            {
                cell.Theme = controlTheme;
            }
        }

        private void GetColumnHeaderVerticalBounds(
            DataGridColumn column,
            double totalHeight,
            out double top,
            out double height)
        {
            if (_bandHeaderRowCount > 1 && TryGetGroupedHeader(column, out ColumnBandHeader header))
            {
                double rowHeight = totalHeight / _bandHeaderRowCount;
                int leafLevel = Math.Max(0, header.Segments.Count - 1);
                top = leafLevel * rowHeight;
                height = totalHeight - top;
                return;
            }

            top = 0;
            height = totalHeight;
        }

        private void ArrangeBandHeaderCells(double totalHeight, double frozenLeftWidth, double rightFrozenStart)
        {
            if (_bandHeaderCells.Count == 0 || _bandHeaderRowCount <= 1)
            {
                return;
            }

            double rowHeight = totalHeight / _bandHeaderRowCount;
            foreach (DataGridColumnBandHeaderCell cell in _bandHeaderCells)
            {
                if ((uint)cell.StartColumnIndex >= (uint)_visibleBandColumns.Count ||
                    (uint)cell.EndColumnIndex >= (uint)_visibleBandColumns.Count)
                {
                    continue;
                }

                DataGridColumnHeader firstHeader = _visibleBandColumns[cell.StartColumnIndex].HeaderCell;
                DataGridColumnHeader lastHeader = _visibleBandColumns[cell.EndColumnIndex].HeaderCell;
                double left = firstHeader.Bounds.X;
                double width = Math.Max(0, lastHeader.Bounds.Right - left);
                cell.Arrange(new Rect(left, cell.Level * rowHeight, width, rowHeight));

                if (cell.IsFrozenLeft || cell.IsFrozenRight)
                {
                    cell.Clip = null;
                }
                else
                {
                    EnsureControlClip(cell, width, rowHeight, frozenLeftWidth, rightFrozenStart, left);
                }
            }
        }

        private double GetBandHeaderWidth(DataGridColumnBandHeaderCell cell)
        {
            if ((uint)cell.StartColumnIndex >= (uint)_visibleBandColumns.Count ||
                (uint)cell.EndColumnIndex >= (uint)_visibleBandColumns.Count)
            {
                return 0;
            }

            double width = 0;
            for (int index = cell.StartColumnIndex; index <= cell.EndColumnIndex; index++)
            {
                width += _visibleBandColumns[index].ActualWidth;
            }

            return width;
        }

        private void AutoSizeColumnsForBandHeader(DataGridColumnBandHeaderCell cell, double desiredWidth)
        {
            double remainingGrowth = desiredWidth - GetBandHeaderWidth(cell);
            if (remainingGrowth <= 0)
            {
                return;
            }

            while (remainingGrowth > 0.001)
            {
                int candidateCount = 0;
                for (int index = cell.StartColumnIndex; index <= cell.EndColumnIndex; index++)
                {
                    DataGridColumn column = _visibleBandColumns[index];
                    if ((column.Width.IsAuto || column.Width.IsSizeToHeader) &&
                        column.ActualWidth + 0.001 < column.ActualMaxWidth)
                    {
                        candidateCount++;
                    }
                }

                if (candidateCount == 0)
                {
                    break;
                }

                double requestedGrowth = remainingGrowth / candidateCount;
                double appliedGrowth = 0;
                for (int index = cell.StartColumnIndex; index <= cell.EndColumnIndex; index++)
                {
                    DataGridColumn column = _visibleBandColumns[index];
                    if ((!column.Width.IsAuto && !column.Width.IsSizeToHeader) ||
                        column.ActualWidth + 0.001 >= column.ActualMaxWidth)
                    {
                        continue;
                    }

                    double currentWidth = column.ActualWidth;
                    OwningGrid.AutoSizeColumn(column, currentWidth + requestedGrowth);
                    appliedGrowth += Math.Max(0, column.ActualWidth - currentWidth);
                }

                if (appliedGrowth <= 0.001)
                {
                    break;
                }

                remainingGrowth = Math.Max(0, remainingGrowth - appliedGrowth);
            }
        }

        private static bool TryGetGroupedHeader(DataGridColumn column, out ColumnBandHeader header)
        {
            header = column?.Header as ColumnBandHeader;
            return header?.Layout == ColumnBandHeaderLayout.Grouped && header.Segments.Count > 0;
        }

        private static bool IsSameBandGroup(
            DataGridColumn startColumn,
            ColumnBandHeader startHeader,
            DataGridColumn candidateColumn,
            int level)
        {
            if (candidateColumn.IsFrozenLeft != startColumn.IsFrozenLeft ||
                candidateColumn.IsFrozenRight != startColumn.IsFrozenRight ||
                !TryGetGroupedHeader(candidateColumn, out ColumnBandHeader candidateHeader) ||
                candidateHeader.Segments.Count <= level + 1)
            {
                return false;
            }

            for (int index = 0; index <= level; index++)
            {
                bool startHasKey = startHeader.SegmentKeys.Count > index;
                bool candidateHasKey = candidateHeader.SegmentKeys.Count > index;
                if (startHasKey || candidateHasKey)
                {
                    if (!startHasKey ||
                        !candidateHasKey ||
                        !ReferenceEquals(startHeader.SegmentKeys[index], candidateHeader.SegmentKeys[index]))
                    {
                        return false;
                    }

                    continue;
                }

                if (!string.Equals(startHeader.Segments[index], candidateHeader.Segments[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureControlClip(
            Control control,
            double width,
            double height,
            double frozenLeftWidth,
            double rightFrozenStart,
            double leftEdge)
        {
            double leftClip = Math.Max(0, frozenLeftWidth - leftEdge);
            double rightClip = rightFrozenStart < double.PositiveInfinity
                ? Math.Max(0, (leftEdge + width) - rightFrozenStart)
                : 0;

            if (leftClip > 0 || rightClip > 0)
            {
                control.Clip = new RectangleGeometry
                {
                    Rect = new Rect(leftClip, 0, Math.Max(0, width - leftClip - rightClip), height)
                };
            }
            else
            {
                control.Clip = null;
            }
        }

        protected override void ChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.ChildrenChanged(sender, e);

            InvalidateChildIndex();
        }

        internal void InvalidateChildIndex()
        {
            _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.ChildIndexesReset);
        }
    }
}
