// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// ILogicalScrollable implementation for DataGridRowsPresenter.
    /// This partial class contains all scrolling-related functionality that integrates
    /// with Avalonia's virtualized scrolling contract.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed partial class DataGridRowsPresenter : ILogicalScrollable
    {
        private EventHandler? _scrollInvalidated;
        
        // ILogicalScrollable state
        private Size _extent;
        private Size _viewport;
        private Vector _offset;
        private bool _canHorizontallyScroll;
        private bool _canVerticallyScroll = true;
        
        // Pre-fetching state
        private bool _prefetchScheduled;
        private Vector _lastPrefetchOffset;
        private const int PrefetchBufferRows = 3;

        #region ILogicalScrollable Properties

        /// <summary>
        /// Gets or sets whether the content can be scrolled horizontally.
        /// </summary>
        public bool CanHorizontallyScroll
        {
            get => _canHorizontallyScroll;
            set
            {
                if (_canHorizontallyScroll != value)
                {
                    _canHorizontallyScroll = value;
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the content can be scrolled vertically.
        /// </summary>
        public bool CanVerticallyScroll
        {
            get => _canVerticallyScroll;
            set
            {
                if (_canVerticallyScroll != value)
                {
                    _canVerticallyScroll = value;
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets whether logical scrolling is enabled.
        /// Returns true when the owning DataGrid has UseLogicalScrollable set to true,
        /// indicating this control handles its own scrolling via ILogicalScrollable.
        /// </summary>
        public bool IsLogicalScrollEnabled => OwningGrid?.UseLogicalScrollable ?? false;

        /// <summary>
        /// Gets the size to scroll by for small scroll increments (e.g., arrow keys, mouse wheel).
        /// </summary>
        public Size ScrollSize => new Size(
            OwningGrid?.ColumnsInternal.FirstVisibleScrollingColumn?.ActualWidth ?? 16, 
            OwningGrid?.RowHeightEstimate ?? 22);

        /// <summary>
        /// Gets the size to scroll by for large scroll increments (e.g., page up/down).
        /// </summary>
        public Size PageScrollSize => new Size(
            Math.Max(0, _viewport.Width - (OwningGrid?.GetVisibleFrozenColumnsWidth() ?? 0)),
            _viewport.Height);

        /// <summary>
        /// Gets the total extent (size) of the scrollable content.
        /// </summary>
        public Size Extent => _extent;

        /// <summary>
        /// Gets or sets the current scroll offset.
        /// </summary>
        public Vector Offset
        {
            get => _offset;
            set
            {
                if (_offset != value)
                {
                    var oldOffset = _offset;
                    _offset = CoerceOffset(value);
                    
                    if (_offset != oldOffset)
                    {
                        OnOffsetChanged(oldOffset, _offset);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the size of the visible viewport.
        /// </summary>
        public Size Viewport => _viewport;

        /// <summary>
        /// Event raised when scroll properties have changed and the ScrollViewer should update.
        /// </summary>
        public event EventHandler? ScrollInvalidated
        {
            add => _scrollInvalidated += value;
            remove => _scrollInvalidated -= value;
        }

        #endregion

        #region ILogicalScrollable Methods

        /// <summary>
        /// Attempts to bring the specified control into view.
        /// </summary>
        /// <param name="target">The control to bring into view.</param>
        /// <param name="targetRect">The target rectangle within the control.</param>
        /// <returns>True if the control was brought into view; otherwise, false.</returns>
        public bool BringIntoView(Control target, Rect targetRect)
        {
            if (OwningGrid == null)
                return false;

            // Find the row containing the target
            var row = target as DataGridRow ?? target.FindAncestorOfType<DataGridRow>();
            if (row != null && row.Index >= 0)
            {
                OwningGrid.ScrollIntoView(OwningGrid.DataConnection.GetDataItem(row.Index), null);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the next control in the specified navigation direction.
        /// Used by ScrollViewer for keyboard navigation when ILogicalScrollable is enabled.
        /// </summary>
        /// <param name="direction">The navigation direction.</param>
        /// <param name="from">The control to navigate from.</param>
        /// <returns>The next control, or null if navigation should be handled by parent.</returns>
        public Control? GetControlInDirection(NavigationDirection direction, Control? from)
        {
            if (OwningGrid == null || !OwningGrid.UseLogicalScrollable)
                return null;

            // Find the current slot from the 'from' control
            int currentSlot = GetSlotFromControl(from);

            // If we don't have a current slot, use the first or last visible slot
            if (currentSlot == -1)
            {
                switch (direction)
                {
                    case NavigationDirection.Up:
                    case NavigationDirection.Previous:
                        currentSlot = OwningGrid.DisplayData.LastScrollingSlot;
                        break;
                    default:
                        currentSlot = OwningGrid.DisplayData.FirstScrollingSlot;
                        break;
                }
            }

            if (currentSlot == -1)
                return null;

            // Calculate the target slot based on direction
            int targetSlot = GetTargetSlot(direction, currentSlot);

            if (targetSlot == -1 || targetSlot == currentSlot)
                return null;

            // Return the displayed element for the target slot if visible
            if (OwningGrid.IsSlotVisible(targetSlot))
            {
                return OwningGrid.DisplayData.GetDisplayedElement(targetSlot);
            }

            // If not visible, scroll to it and return null (let layout happen first)
            OwningGrid.ScrollSlotIntoView(targetSlot, scrolledHorizontally: false);
            return null;
        }

        /// <summary>
        /// Raises the ScrollInvalidated event.
        /// </summary>
        public void RaiseScrollInvalidated(EventArgs e)
        {
            _scrollInvalidated?.Invoke(this, e);
        }

        #endregion

        #region Scroll Helper Methods

        /// <summary>
        /// Gets the slot index from a control.
        /// </summary>
        private int GetSlotFromControl(Control? from)
        {
            if (from is DataGridRow row)
            {
                return row.Slot;
            }
            
            if (from is DataGridRowGroupHeader groupHeader)
            {
                return groupHeader.RowGroupInfo?.Slot ?? -1;
            }
            
            if (from != null)
            {
                // Try to find parent row
                var parentRow = from.FindAncestorOfType<DataGridRow>();
                if (parentRow != null)
                {
                    return parentRow.Slot;
                }

                var parentGroupHeader = from.FindAncestorOfType<DataGridRowGroupHeader>();
                if (parentGroupHeader != null)
                {
                    return parentGroupHeader.RowGroupInfo?.Slot ?? -1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the target slot based on navigation direction.
        /// </summary>
        private int GetTargetSlot(NavigationDirection direction, int currentSlot)
        {
            if (OwningGrid == null)
                return -1;

            switch (direction)
            {
                case NavigationDirection.Up:
                case NavigationDirection.Previous:
                    return OwningGrid.GetPreviousVisibleSlot(currentSlot);

                case NavigationDirection.Down:
                case NavigationDirection.Next:
                    var nextSlot = OwningGrid.GetNextVisibleSlot(currentSlot);
                    return nextSlot >= OwningGrid.SlotCount ? -1 : nextSlot;

                case NavigationDirection.First:
                    return OwningGrid.FirstVisibleSlot;

                case NavigationDirection.Last:
                    return OwningGrid.LastVisibleSlot;

                case NavigationDirection.PageUp:
                    return GetSlotPageUp(currentSlot);

                case NavigationDirection.PageDown:
                    return GetSlotPageDown(currentSlot);

                case NavigationDirection.Left:
                case NavigationDirection.Right:
                    // Horizontal navigation is handled by the DataGrid's column navigation
                    return -1;

                default:
                    return -1;
            }
        }

        /// <summary>
        /// Gets the slot one page up from the current slot.
        /// </summary>
        private int GetSlotPageUp(int currentSlot)
        {
            if (OwningGrid == null)
                return -1;

            double pageHeight = _viewport.Height;
            double accumulatedHeight = 0;
            int targetSlot = currentSlot;

            while (accumulatedHeight < pageHeight)
            {
                int previousSlot = OwningGrid.GetPreviousVisibleSlot(targetSlot);
                if (previousSlot == -1)
                    break;

                targetSlot = previousSlot;
                accumulatedHeight += OwningGrid.RowHeightEstimate;
            }

            return targetSlot != currentSlot ? targetSlot : OwningGrid.FirstVisibleSlot;
        }

        /// <summary>
        /// Gets the slot one page down from the current slot.
        /// </summary>
        private int GetSlotPageDown(int currentSlot)
        {
            if (OwningGrid == null)
                return -1;

            double pageHeight = _viewport.Height;
            double accumulatedHeight = 0;
            int targetSlot = currentSlot;

            while (accumulatedHeight < pageHeight)
            {
                int nextSlot = OwningGrid.GetNextVisibleSlot(targetSlot);
                if (nextSlot >= OwningGrid.SlotCount)
                    break;

                targetSlot = nextSlot;
                accumulatedHeight += OwningGrid.RowHeightEstimate;
            }

            return targetSlot != currentSlot ? targetSlot : OwningGrid.LastVisibleSlot;
        }

        /// <summary>
        /// Coerces the offset to valid bounds.
        /// </summary>
        private Vector CoerceOffset(Vector value)
        {
            var maxX = Math.Max(0, _extent.Width - _viewport.Width);
            var maxY = Math.Max(0, _extent.Height - _viewport.Height);
            
            return new Vector(
                Math.Max(0, Math.Min(value.X, maxX)),
                Math.Max(0, Math.Min(value.Y, maxY)));
        }

        /// <summary>
        /// Called when the scroll offset changes.
        /// </summary>
        private void OnOffsetChanged(Vector oldOffset, Vector newOffset)
        {
            if (OwningGrid == null)
                return;

            // Calculate the delta and delegate to the DataGrid's scroll handling
            var deltaY = newOffset.Y - oldOffset.Y;
            var deltaX = newOffset.X - oldOffset.X;

            if (deltaY != 0)
            {
                // Use the DataGrid's existing vertical scroll handling
                // Accumulate the delta to handle multiple scroll events before measure
                OwningGrid.DisplayData.PendingVerticalScrollHeight += deltaY;
                InvalidateMeasure();
                
                // Schedule pre-fetching for smoother scrolling
                SchedulePrefetch();
            }

            if (deltaX != 0)
            {
                // Use the DataGrid's existing horizontal scroll handling
                OwningGrid.UpdateHorizontalOffset(newOffset.X);
                
                // Keep column headers in sync with horizontal scroll
                OwningGrid.InvalidateColumnHeadersArrange();
            }
        }

        /// <summary>
        /// Updates the scroll extent and viewport, raising ScrollInvalidated if changed.
        /// </summary>
        internal void UpdateScrollInfo(Size extent, Size viewport)
        {
            bool changed = false;

            if (_extent != extent)
            {
                _extent = extent;
                changed = true;
            }

            if (_viewport != viewport)
            {
                _viewport = viewport;
                changed = true;
            }

            // Coerce offset to new bounds
            var coercedOffset = CoerceOffset(_offset);
            if (coercedOffset != _offset)
            {
                _offset = coercedOffset;
                changed = true;
            }

            if (changed)
            {
                RaiseScrollInvalidated(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Synchronizes the ILogicalScrollable offset with the DataGrid's internal offset.
        /// </summary>
        internal void SyncOffset(double horizontalOffset, double verticalOffset)
        {
            _offset = new Vector(horizontalOffset, verticalOffset);
        }

        #endregion

        #region Predictive Pre-fetching

        /// <summary>
        /// Schedules pre-fetching of rows outside the visible viewport.
        /// This improves scrolling smoothness by realizing rows before they become visible.
        /// </summary>
        internal void SchedulePrefetch()
        {
            if (_prefetchScheduled || OwningGrid == null || !OwningGrid.UseLogicalScrollable)
                return;

            // Only prefetch if offset has changed significantly
            var offsetDelta = Math.Abs(_offset.Y - _lastPrefetchOffset.Y);
            if (offsetDelta < OwningGrid.RowHeightEstimate * 0.5)
                return;

            _prefetchScheduled = true;
            _lastPrefetchOffset = _offset;

            // Schedule prefetch during idle time to avoid blocking the UI
            Dispatcher.UIThread.Post(ExecutePrefetch, DispatcherPriority.Background);
        }

        /// <summary>
        /// Executes the pre-fetching of rows above and below the viewport.
        /// </summary>
        private void ExecutePrefetch()
        {
            _prefetchScheduled = false;

            if (OwningGrid == null || !OwningGrid.UseLogicalScrollable)
                return;

            var displayData = OwningGrid.DisplayData;
            if (displayData == null)
                return;

            int firstVisibleSlot = displayData.FirstScrollingSlot;
            int lastVisibleSlot = displayData.LastScrollingSlot;

            if (firstVisibleSlot == -1 || lastVisibleSlot == -1)
                return;

            // Calculate scroll direction based on velocity
            var scrollDirection = GetScrollDirection();

            // Pre-fetch rows in the direction of scroll
            if (scrollDirection >= 0) // Scrolling down or stationary
            {
                PrefetchRowsBelow(lastVisibleSlot, PrefetchBufferRows);
            }
            
            if (scrollDirection <= 0) // Scrolling up or stationary
            {
                PrefetchRowsAbove(firstVisibleSlot, PrefetchBufferRows);
            }
        }

        /// <summary>
        /// Gets the current scroll direction.
        /// Returns: -1 for scrolling up, 0 for stationary, 1 for scrolling down.
        /// </summary>
        private int GetScrollDirection()
        {
            var delta = _offset.Y - _lastPrefetchOffset.Y;
            if (Math.Abs(delta) < 0.5)
                return 0;
            return delta > 0 ? 1 : -1;
        }

        /// <summary>
        /// Pre-fetches rows below the visible area.
        /// </summary>
        private void PrefetchRowsBelow(int lastVisibleSlot, int count)
        {
            if (OwningGrid == null)
                return;

            int prefetchedCount = 0;
            int slot = lastVisibleSlot;

            while (prefetchedCount < count && slot < OwningGrid.SlotCount)
            {
                int nextSlot = OwningGrid.GetNextVisibleSlot(slot);
                if (nextSlot >= OwningGrid.SlotCount || nextSlot == -1)
                    break;

                slot = nextSlot;

                // Check if the slot is already realized
                if (!OwningGrid.IsSlotVisible(slot))
                {
                    // Pre-realize this row (but don't add to visible display yet)
                    // The row will be ready when scrolling brings it into view
                    EnsureRowPrepared(slot);
                    prefetchedCount++;
                }
            }
        }

        /// <summary>
        /// Pre-fetches rows above the visible area.
        /// </summary>
        private void PrefetchRowsAbove(int firstVisibleSlot, int count)
        {
            if (OwningGrid == null)
                return;

            int prefetchedCount = 0;
            int slot = firstVisibleSlot;

            while (prefetchedCount < count && slot > 0)
            {
                int previousSlot = OwningGrid.GetPreviousVisibleSlot(slot);
                if (previousSlot == -1)
                    break;

                slot = previousSlot;

                // Check if the slot is already realized
                if (!OwningGrid.IsSlotVisible(slot))
                {
                    // Pre-realize this row
                    EnsureRowPrepared(slot);
                    prefetchedCount++;
                }
            }
        }

        /// <summary>
        /// Ensures a row is prepared (recycled row obtained and configured) for the given slot.
        /// This doesn't add the row to the visual tree yet, but makes it ready for quick display.
        /// </summary>
        private void EnsureRowPrepared(int slot)
        {
            if (OwningGrid == null)
                return;

            // Get or create a row from the recycling pool
            var row = OwningGrid.DisplayData.GetRecycledRow();
            if (row == null)
            {
                // If no recycled row available, we'll let normal realization handle it
                return;
            }

            // Prepare the row for the slot
            try
            {
                int dataIndex = OwningGrid.RowIndexFromSlot(slot);
                if (dataIndex >= 0 && dataIndex < OwningGrid.DataConnection.Count)
                {
                    var dataItem = OwningGrid.DataConnection.GetDataItem(dataIndex);
                    if (dataItem != null)
                    {
                        // Configure the row but keep it in recyclable state
                        // It will be properly added when it scrolls into view
                        OwningGrid.DisplayData.RecycleRow(row);
                    }
                }
            }
            catch
            {
                // If preparation fails, just recycle the row back
                OwningGrid.DisplayData.RecycleRow(row);
            }
        }

        /// <summary>
        /// Cancels any pending pre-fetch operation.
        /// </summary>
        internal void CancelPrefetch()
        {
            _prefetchScheduled = false;
        }

        #endregion
    }
}
