// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// IScrollSnapPointsInfo implementation for DataGridRowsPresenter.
    /// This partial class provides scroll snap point support for row-based snapping,
    /// allowing the scroll to naturally align to row boundaries.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed partial class DataGridRowsPresenter : IScrollSnapPointsInfo
    {
        /// <summary>
        /// Defines the <see cref="HorizontalSnapPointsChanged"/> event.
        /// </summary>
        public static readonly RoutedEvent<RoutedEventArgs> HorizontalSnapPointsChangedEvent =
            RoutedEvent.Register<DataGridRowsPresenter, RoutedEventArgs>(
                nameof(HorizontalSnapPointsChanged),
                RoutingStrategies.Bubble);

        /// <summary>
        /// Defines the <see cref="VerticalSnapPointsChanged"/> event.
        /// </summary>
        public static readonly RoutedEvent<RoutedEventArgs> VerticalSnapPointsChangedEvent =
            RoutedEvent.Register<DataGridRowsPresenter, RoutedEventArgs>(
                nameof(VerticalSnapPointsChanged),
                RoutingStrategies.Bubble);

        private bool _areHorizontalSnapPointsRegular;
        private bool _areVerticalSnapPointsRegular;

        #region IScrollSnapPointsInfo Properties

        /// <summary>
        /// Gets or sets a value indicating whether the horizontal snap points are regular (equidistant).
        /// For DataGrid, horizontal snap points relate to columns, which may have varying widths.
        /// </summary>
        public bool AreHorizontalSnapPointsRegular
        {
            get => _areHorizontalSnapPointsRegular;
            set => _areHorizontalSnapPointsRegular = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the vertical snap points are regular (equidistant).
        /// For DataGrid, rows typically have the same height (RowHeight), so snap points can be regular.
        /// </summary>
        public bool AreVerticalSnapPointsRegular
        {
            get => _areVerticalSnapPointsRegular;
            set => _areVerticalSnapPointsRegular = value;
        }

        #endregion

        #region IScrollSnapPointsInfo Events

        /// <summary>
        /// Occurs when the horizontal snap points change.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged
        {
            add => AddHandler(HorizontalSnapPointsChangedEvent, value);
            remove => RemoveHandler(HorizontalSnapPointsChangedEvent, value);
        }

        /// <summary>
        /// Occurs when the vertical snap points change.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged
        {
            add => AddHandler(VerticalSnapPointsChangedEvent, value);
            remove => RemoveHandler(VerticalSnapPointsChangedEvent, value);
        }

        #endregion

        #region IScrollSnapPointsInfo Methods

        /// <summary>
        /// Gets irregular snap points for the specified orientation.
        /// For vertical orientation, returns the Y positions of each visible row.
        /// </summary>
        /// <param name="orientation">The orientation for the desired snap point set.</param>
        /// <param name="snapPointsAlignment">The alignment to use when applying snap points.</param>
        /// <returns>The read-only collection of snap point distances.</returns>
        public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
        {
            if (OwningGrid == null)
            {
                return Array.Empty<double>();
            }

            if (orientation == Orientation.Vertical)
            {
                return GetVerticalSnapPoints(snapPointsAlignment);
            }
            else
            {
                return GetHorizontalSnapPoints(snapPointsAlignment);
            }
        }

        /// <summary>
        /// Gets the distance between regular snap points.
        /// For vertical scrolling with uniform row heights, returns the row height.
        /// </summary>
        /// <param name="orientation">The orientation for the desired snap point set.</param>
        /// <param name="snapPointsAlignment">The alignment to use when applying snap points.</param>
        /// <param name="offset">The offset of the first snap point.</param>
        /// <returns>The distance between equidistant snap points, or 0 if irregular.</returns>
        public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
        {
            offset = 0;

            if (OwningGrid == null)
            {
                return 0;
            }

            if (orientation == Orientation.Vertical && AreVerticalSnapPointsRegular)
            {
                // Regular vertical snap points at row height intervals
                double rowHeight = OwningGrid.RowHeight > 0 ? OwningGrid.RowHeight : OwningGrid.RowHeightEstimate;
                
                // Adjust offset based on alignment
                switch (snapPointsAlignment)
                {
                    case SnapPointsAlignment.Near:
                        offset = 0;
                        break;
                    case SnapPointsAlignment.Center:
                        offset = rowHeight / 2;
                        break;
                    case SnapPointsAlignment.Far:
                        offset = rowHeight;
                        break;
                }

                return rowHeight;
            }

            if (orientation == Orientation.Horizontal && AreHorizontalSnapPointsRegular)
            {
                // Regular horizontal snap points (typically not applicable for DataGrid)
                var firstColumn = OwningGrid.ColumnsInternal.FirstVisibleScrollingColumn;
                if (firstColumn != null)
                {
                    double width = firstColumn.ActualWidth;
                    switch (snapPointsAlignment)
                    {
                        case SnapPointsAlignment.Near:
                            offset = 0;
                            break;
                        case SnapPointsAlignment.Center:
                            offset = width / 2;
                            break;
                        case SnapPointsAlignment.Far:
                            offset = width;
                            break;
                    }
                    return width;
                }
            }

            // Irregular snap points - caller should use GetIrregularSnapPoints
            return 0;
        }

        #endregion

        #region Snap Points Helper Methods

        /// <summary>
        /// Gets vertical snap points based on row positions.
        /// </summary>
        private IReadOnlyList<double> GetVerticalSnapPoints(SnapPointsAlignment alignment)
        {
            if (OwningGrid == null)
            {
                return Array.Empty<double>();
            }

            var snapPoints = new List<double>();
            double rowHeight = OwningGrid.RowHeight > 0 ? OwningGrid.RowHeight : OwningGrid.RowHeightEstimate;
            double currentY = 0;

            // Calculate snap points for visible slots
            int slotCount = OwningGrid.SlotCount;
            for (int slot = OwningGrid.FirstVisibleSlot; slot <= OwningGrid.LastVisibleSlot && slot < slotCount; slot++)
            {
                double snapPoint;
                switch (alignment)
                {
                    case SnapPointsAlignment.Near:
                        snapPoint = currentY;
                        break;
                    case SnapPointsAlignment.Center:
                        snapPoint = currentY + (rowHeight / 2);
                        break;
                    case SnapPointsAlignment.Far:
                        snapPoint = currentY + rowHeight;
                        break;
                    default:
                        snapPoint = currentY;
                        break;
                }

                snapPoints.Add(snapPoint);
                currentY += rowHeight;
            }

            return snapPoints;
        }

        /// <summary>
        /// Gets horizontal snap points based on column positions.
        /// Respects frozen columns by only snapping to scrollable column boundaries.
        /// </summary>
        private IReadOnlyList<double> GetHorizontalSnapPoints(SnapPointsAlignment alignment)
        {
            if (OwningGrid == null)
            {
                return Array.Empty<double>();
            }

            var snapPoints = new List<double>();
            double frozenWidth = OwningGrid.GetVisibleFrozenColumnsWidth();
            double currentX = 0; // Start from 0 for scrollable offset

            // Get snap points for scrollable columns only
            foreach (var column in OwningGrid.ColumnsInternal.GetVisibleScrollingColumns())
            {
                double width = column.ActualWidth;
                double snapPoint;

                switch (alignment)
                {
                    case SnapPointsAlignment.Near:
                        snapPoint = currentX;
                        break;
                    case SnapPointsAlignment.Center:
                        snapPoint = currentX + (width / 2);
                        break;
                    case SnapPointsAlignment.Far:
                        snapPoint = currentX + width;
                        break;
                    default:
                        snapPoint = currentX;
                        break;
                }

                snapPoints.Add(snapPoint);
                currentX += width;
            }

            return snapPoints;
        }

        /// <summary>
        /// Raises the VerticalSnapPointsChanged event.
        /// Should be called when row heights or the number of rows changes.
        /// </summary>
        internal void RaiseVerticalSnapPointsChanged()
        {
            RaiseEvent(new RoutedEventArgs(VerticalSnapPointsChangedEvent));
        }

        /// <summary>
        /// Raises the HorizontalSnapPointsChanged event.
        /// Should be called when column widths or the number of columns changes.
        /// </summary>
        internal void RaiseHorizontalSnapPointsChanged()
        {
            RaiseEvent(new RoutedEventArgs(HorizontalSnapPointsChangedEvent));
        }

        #endregion
    }
}
