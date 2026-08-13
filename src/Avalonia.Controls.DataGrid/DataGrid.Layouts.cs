// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls.DataGridLayouts;

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
                    oldValue.LayoutInvalidated -= OnLayoutModelInvalidated;
                }

                SetAndRaise(LayoutModelProperty, ref _layoutModel, value);

                if (value != null)
                {
                    value.LayoutInvalidated += OnLayoutModelInvalidated;
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
