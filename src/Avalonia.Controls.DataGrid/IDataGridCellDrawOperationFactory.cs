// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Creates per-cell custom draw operations for <see cref="DataGridCustomDrawingCell"/>.
    /// </summary>
    interface IDataGridCellDrawOperationFactory
    {
        /// <summary>
        /// Creates a custom draw operation for a cell render pass.
        /// </summary>
        /// <param name="context">Rendering context for the current cell.</param>
        /// <returns>An operation to execute via <see cref="DrawingContext.Custom(ICustomDrawOperation)"/>, or null.</returns>
        ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Optional interface for draw-operation factories that can provide text/layout metrics during measure.
    /// </summary>
    interface IDataGridCellDrawOperationMeasureProvider
    {
        /// <summary>
        /// Attempts to provide desired size for the current cell.
        /// </summary>
        /// <param name="context">Measure context for the current cell.</param>
        /// <param name="desiredSize">Desired size returned by the provider.</param>
        /// <returns><c>true</c> when metrics were provided and should be used; otherwise <c>false</c>.</returns>
        bool TryMeasure(DataGridCellDrawOperationMeasureContext context, out Size desiredSize);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Optional interface for draw-operation factories that can participate in arrange.
    /// </summary>
    interface IDataGridCellDrawOperationArrangeProvider
    {
        /// <summary>
        /// Attempts to provide arranged size for the current cell.
        /// </summary>
        /// <param name="context">Arrange context for the current cell.</param>
        /// <param name="arrangedSize">Arranged size returned by the provider.</param>
        /// <returns><c>true</c> when arranged size was provided and should be used; otherwise <c>false</c>.</returns>
        bool TryArrange(DataGridCellDrawOperationArrangeContext context, out Size arrangedSize);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Describes an explicit invalidation request emitted by a draw-operation factory.
    /// </summary>
    sealed class DataGridCellDrawOperationInvalidatedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellDrawOperationInvalidatedEventArgs"/> class.
        /// </summary>
        /// <param name="invalidateMeasure">
        /// <c>true</c> to invalidate measure/arrange in addition to render; otherwise <c>false</c>.
        /// </param>
        /// <param name="clearTextLayoutCache">
        /// <c>true</c> to clear shared text-layout cache before refreshing realized cells; otherwise <c>false</c>.
        /// </param>
        public DataGridCellDrawOperationInvalidatedEventArgs(
            bool invalidateMeasure = false,
            bool clearTextLayoutCache = false)
        {
            InvalidateMeasure = invalidateMeasure;
            ClearTextLayoutCache = clearTextLayoutCache;
        }

        /// <summary>
        /// Gets a value indicating whether layout should be invalidated along with render.
        /// </summary>
        public bool InvalidateMeasure { get; }

        /// <summary>
        /// Gets a value indicating whether shared text-layout cache should be cleared.
        /// </summary>
        public bool ClearTextLayoutCache { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Optional interface for factories that can request redraw/re-measure of realized custom-drawing cells.
    /// </summary>
    interface IDataGridCellDrawOperationInvalidationSource
    {
        /// <summary>
        /// Raised when realized cells using this factory should be invalidated.
        /// </summary>
        event EventHandler<DataGridCellDrawOperationInvalidatedEventArgs> Invalidated;
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Contains the contextual state used to create a custom draw operation for a data grid cell.
    /// </summary>
    sealed class DataGridCellDrawOperationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellDrawOperationContext"/> class.
        /// </summary>
        public DataGridCellDrawOperationContext(
            DataGridCell cell,
            DataGridColumn column,
            object item,
            object value,
            string text,
            Rect bounds,
            IBrush foreground,
            Typeface typeface,
            double fontSize,
            bool isCurrent,
            bool isSelected)
        {
            Cell = cell;
            Column = column;
            Item = item;
            Value = value;
            Text = text;
            Bounds = bounds;
            Foreground = foreground;
            Typeface = typeface;
            FontSize = fontSize;
            IsCurrent = isCurrent;
            IsSelected = isSelected;
        }

        /// <summary>
        /// Gets the current visual cell.
        /// </summary>
        public DataGridCell Cell { get; }

        /// <summary>
        /// Gets the owning column.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Gets the current row item.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the bound value assigned to the cell.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the display text resolved from <see cref="Value"/>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the cell drawing bounds.
        /// </summary>
        public Rect Bounds { get; }

        /// <summary>
        /// Gets the resolved foreground brush.
        /// </summary>
        public IBrush Foreground { get; }

        /// <summary>
        /// Gets the resolved typeface.
        /// </summary>
        public Typeface Typeface { get; }

        /// <summary>
        /// Gets the resolved font size.
        /// </summary>
        public double FontSize { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is the current cell.
        /// </summary>
        public bool IsCurrent { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is selected.
        /// </summary>
        public bool IsSelected { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Contains contextual state used to measure custom draw-operation cells.
    /// </summary>
    sealed class DataGridCellDrawOperationMeasureContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellDrawOperationMeasureContext"/> class.
        /// </summary>
        public DataGridCellDrawOperationMeasureContext(
            DataGridCell cell,
            DataGridColumn column,
            object item,
            object value,
            string text,
            Size availableSize,
            IBrush foreground,
            Typeface typeface,
            double fontSize,
            TextAlignment textAlignment,
            TextTrimming textTrimming,
            FlowDirection flowDirection,
            bool isCurrent,
            bool isSelected)
        {
            Cell = cell;
            Column = column;
            Item = item;
            Value = value;
            Text = text;
            AvailableSize = availableSize;
            Foreground = foreground;
            Typeface = typeface;
            FontSize = fontSize;
            TextAlignment = textAlignment;
            TextTrimming = textTrimming;
            FlowDirection = flowDirection;
            IsCurrent = isCurrent;
            IsSelected = isSelected;
        }

        /// <summary>
        /// Gets the current visual cell.
        /// </summary>
        public DataGridCell Cell { get; }

        /// <summary>
        /// Gets the owning column.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Gets the current row item.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the bound value assigned to the cell.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the display text resolved from <see cref="Value"/>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the available layout size.
        /// </summary>
        public Size AvailableSize { get; }

        /// <summary>
        /// Gets the resolved foreground brush.
        /// </summary>
        public IBrush Foreground { get; }

        /// <summary>
        /// Gets the resolved typeface.
        /// </summary>
        public Typeface Typeface { get; }

        /// <summary>
        /// Gets the resolved font size.
        /// </summary>
        public double FontSize { get; }

        /// <summary>
        /// Gets the resolved text alignment.
        /// </summary>
        public TextAlignment TextAlignment { get; }

        /// <summary>
        /// Gets the resolved text trimming mode.
        /// </summary>
        public TextTrimming TextTrimming { get; }

        /// <summary>
        /// Gets the resolved flow direction.
        /// </summary>
        public FlowDirection FlowDirection { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is the current cell.
        /// </summary>
        public bool IsCurrent { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is selected.
        /// </summary>
        public bool IsSelected { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Contains contextual state used to arrange custom draw-operation cells.
    /// </summary>
    sealed class DataGridCellDrawOperationArrangeContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCellDrawOperationArrangeContext"/> class.
        /// </summary>
        public DataGridCellDrawOperationArrangeContext(
            DataGridCell cell,
            DataGridColumn column,
            object item,
            object value,
            string text,
            Size finalSize,
            IBrush foreground,
            Typeface typeface,
            double fontSize,
            TextAlignment textAlignment,
            TextTrimming textTrimming,
            FlowDirection flowDirection,
            bool isCurrent,
            bool isSelected)
        {
            Cell = cell;
            Column = column;
            Item = item;
            Value = value;
            Text = text;
            FinalSize = finalSize;
            Foreground = foreground;
            Typeface = typeface;
            FontSize = fontSize;
            TextAlignment = textAlignment;
            TextTrimming = textTrimming;
            FlowDirection = flowDirection;
            IsCurrent = isCurrent;
            IsSelected = isSelected;
        }

        /// <summary>
        /// Gets the current visual cell.
        /// </summary>
        public DataGridCell Cell { get; }

        /// <summary>
        /// Gets the owning column.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Gets the current row item.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the bound value assigned to the cell.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the display text resolved from <see cref="Value"/>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the final arranged size.
        /// </summary>
        public Size FinalSize { get; }

        /// <summary>
        /// Gets the resolved foreground brush.
        /// </summary>
        public IBrush Foreground { get; }

        /// <summary>
        /// Gets the resolved typeface.
        /// </summary>
        public Typeface Typeface { get; }

        /// <summary>
        /// Gets the resolved font size.
        /// </summary>
        public double FontSize { get; }

        /// <summary>
        /// Gets the resolved text alignment.
        /// </summary>
        public TextAlignment TextAlignment { get; }

        /// <summary>
        /// Gets the resolved text trimming mode.
        /// </summary>
        public TextTrimming TextTrimming { get; }

        /// <summary>
        /// Gets the resolved flow direction.
        /// </summary>
        public FlowDirection FlowDirection { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is the current cell.
        /// </summary>
        public bool IsCurrent { get; }

        /// <summary>
        /// Gets a value indicating whether the cell is selected.
        /// </summary>
        public bool IsSelected { get; }
    }
}
