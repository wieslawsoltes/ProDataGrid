// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Utilities;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {
        private IDataGridLayoutModel? _layoutModel;

        /// <summary>
        /// Identifies the <see cref="LayoutModel"/> direct property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, IDataGridLayoutModel?> LayoutModelProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, IDataGridLayoutModel?>(
                nameof(LayoutModel),
                grid => grid.LayoutModel,
                (grid, value) => grid.LayoutModel = value);

        /// <summary>
        /// Gets or sets the model that controls row realization and spatial arrangement.
        /// </summary>
        /// <remarks>
        /// A <c>null</c> value uses the classic vertical list implementation. Assign any built-in
        /// model or a user-defined <see cref="IDataGridLayoutModel"/> to enable the extensible layout
        /// engine. Layout algorithm state is retained per model instance for fast runtime switching.
        /// </remarks>
        public IDataGridLayoutModel? LayoutModel
        {
            get => _layoutModel;
            set
            {
                if (ReferenceEquals(_layoutModel, value))
                {
                    return;
                }

                IDataGridLayoutModel? oldValue = _layoutModel;
                if (oldValue != null)
                {
                    WeakEventHandlerManager.Unsubscribe<DataGridLayoutInvalidatedEventArgs, DataGrid>(
                        oldValue,
                        nameof(IDataGridLayoutModel.LayoutInvalidated),
                        OnLayoutModelInvalidated);
                }

                SetAndRaise(LayoutModelProperty, ref _layoutModel, value);

                if (value != null)
                {
                    WeakEventHandlerManager.Subscribe<IDataGridLayoutModel, DataGridLayoutInvalidatedEventArgs, DataGrid>(
                        value,
                        nameof(IDataGridLayoutModel.LayoutInvalidated),
                        OnLayoutModelInvalidated);
                }

                _rowsPresenter?.OnLayoutModelChanged(oldValue, value);
                InvalidateMeasure();
            }
        }

        internal int LayoutItemCount => VisibleSlotCount;

        internal int GetLayoutSlot(int layoutIndex)
        {
            if (layoutIndex < 0 || layoutIndex >= VisibleSlotCount)
            {
                return -1;
            }

            if (_collapsedSlotsTable.IsEmpty)
            {
                return layoutIndex;
            }

            int low = layoutIndex;
            int high = SlotCount - 1;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                int visibleThroughMiddle = middle + 1 - _collapsedSlotsTable.GetIndexCount(0, middle);
                if (visibleThroughMiddle <= layoutIndex)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return _collapsedSlotsTable.Contains(low) ? GetNextVisibleSlot(low) : low;
        }

        internal int GetLayoutIndexFromSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount || _collapsedSlotsTable.Contains(slot))
            {
                return -1;
            }

            return slot - _collapsedSlotsTable.GetIndexCount(0, slot);
        }

        internal Control GetOrCreateLayoutElement(int layoutIndex)
        {
            int slot = GetLayoutSlot(layoutIndex);
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(layoutIndex));
            }

            if (IsSlotVisible(slot))
            {
                return DisplayData.GetDisplayedElement(slot);
            }

            bool isAdjacent = DisplayData.FirstScrollingSlot == -1 ||
                slot == GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot) ||
                slot == GetNextVisibleSlot(DisplayData.LastScrollingSlot);
            if (!isAdjacent)
            {
                ResetDisplayedRows();
            }

            GetDisplayedSlotElementHeight(slot);
            return DisplayData.GetDisplayedElement(slot);
        }

        internal void CompleteLayoutRealization(int firstLayoutIndex, int lastLayoutIndex)
        {
            if (firstLayoutIndex < 0 || lastLayoutIndex < firstLayoutIndex)
            {
                ResetDisplayedRows();
                return;
            }

            int firstSlot = GetLayoutSlot(firstLayoutIndex);
            int lastSlot = GetLayoutSlot(lastLayoutIndex);
            if (firstSlot < 0 || lastSlot < firstSlot)
            {
                ResetDisplayedRows();
                return;
            }

            RemoveNonDisplayedRows(firstSlot, lastSlot);
            DisplayData.NumTotallyDisplayedScrollingElements = DisplayData.NumDisplayedScrollingElements;
        }

        internal Size GetEstimatedLayoutItemSize(int layoutIndex)
        {
            int slot = GetLayoutSlot(layoutIndex);
            if (slot < 0)
            {
                return default;
            }

            double width = RowHeadersDesiredWidth + ColumnsInternal.VisibleEdgedColumnsWidth +
                ColumnsInternal.FillerColumn.FillerWidth;
            return new Size(Math.Max(0, width), Math.Max(0, GetSlotElementHeight(slot)));
        }

        internal double GetEstimatedLayoutItemOffset(int layoutIndex, DataGridLayoutOrientation orientation)
        {
            if (layoutIndex <= 0)
            {
                return 0;
            }

            if (orientation == DataGridLayoutOrientation.Horizontal)
            {
                return layoutIndex * Math.Max(0, GetEstimatedLayoutItemSize(Math.Min(layoutIndex - 1, VisibleSlotCount - 1)).Width);
            }

            int slot = layoutIndex >= VisibleSlotCount ? SlotCount : GetLayoutSlot(layoutIndex);
            EnsureScrollHeightIndex(refreshEstimatorChanges: false);
            return Math.Max(0, _scrollHeightIndex.GetOffsetToSlot(Math.Max(0, slot)));
        }

        internal int GetLayoutIndex(Control element)
        {
            int slot = element switch
            {
                DataGridRow row => row.Slot,
                DataGridRowGroupHeader header => header.RowGroupInfo?.Slot ?? -1,
                DataGridRowGroupFooter footer => footer.RowGroupInfo?.Slot ?? -1,
                _ => -1
            };
            return GetLayoutIndexFromSlot(slot);
        }

        internal bool TryResolveLayoutNavigation(
            int currentRowIndex,
            DataGridLayoutNavigationDirection direction,
            Point navigationAnchor,
            out int targetRowIndex,
            out Rect estimatedBounds)
        {
            targetRowIndex = -1;
            estimatedBounds = default;
            if (_rowsPresenter == null || LayoutModel == null || currentRowIndex < 0)
            {
                return false;
            }

            int currentSlot = SlotFromRowIndex(currentRowIndex);
            int currentLayoutIndex = GetLayoutIndexFromSlot(currentSlot);
            if (currentLayoutIndex < 0)
            {
                return false;
            }

            Rect viewport = new(
                _rowsPresenter.Offset.X,
                _rowsPresenter.Offset.Y,
                _rowsPresenter.Viewport.Width,
                _rowsPresenter.Viewport.Height);
            DataGridLayoutNavigationRequest request = new(
                currentLayoutIndex,
                direction,
                viewport,
                navigationAnchor);
            if (!_rowsPresenter.TryResolveLayoutNavigation(request, out DataGridLayoutNavigationResult result))
            {
                return false;
            }

            int targetLayoutIndex = result.ItemIndex;
            int scanStep = GetNavigationScanStep(direction, currentLayoutIndex, targetLayoutIndex);
            while (targetLayoutIndex >= 0 && targetLayoutIndex < LayoutItemCount)
            {
                int targetSlot = GetLayoutSlot(targetLayoutIndex);
                int rowIndex = targetSlot >= 0 && !IsGroupSlot(targetSlot)
                    ? RowIndexFromSlot(targetSlot)
                    : -1;
                if (rowIndex >= 0)
                {
                    targetRowIndex = rowIndex;
                    estimatedBounds = _rowsPresenter.TryGetLayoutBounds(targetLayoutIndex, out Rect exactBounds)
                        ? exactBounds
                        : result.EstimatedBounds;
                    return targetRowIndex != currentRowIndex;
                }

                targetLayoutIndex += scanStep;
            }

            return false;
        }

        private static int GetNavigationScanStep(
            DataGridLayoutNavigationDirection direction,
            int currentLayoutIndex,
            int targetLayoutIndex)
        {
            return direction switch
            {
                DataGridLayoutNavigationDirection.First or DataGridLayoutNavigationDirection.LineStart => 1,
                DataGridLayoutNavigationDirection.Last or DataGridLayoutNavigationDirection.LineEnd => -1,
                _ => targetLayoutIndex >= currentLayoutIndex ? 1 : -1
            };
        }

        private void OnLayoutModelInvalidated(object? sender, DataGridLayoutInvalidatedEventArgs e)
        {
            if (sender is IDataGridLayoutModel model)
            {
                _rowsPresenter?.OnLayoutModelInvalidated(model, e.Kind);
            }

            if (e.Kind == DataGridLayoutInvalidationKind.Arrange)
            {
                _rowsPresenter?.InvalidateArrange();
            }
            else
            {
                _rowsPresenter?.InvalidateMeasure();
                InvalidateMeasure();
            }
        }
    }
}
