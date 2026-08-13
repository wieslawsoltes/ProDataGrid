// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridLayouts;

internal sealed class DataGridUniformGridLayoutAlgorithm : IDataGridLayoutAlgorithm
{
    private readonly DataGridUniformGridLayoutModel _model;

    public DataGridUniformGridLayoutAlgorithm(DataGridUniformGridLayoutModel model)
    {
        _model = model;
    }

    public void Initialize(IDataGridLayoutContext context)
    {
        context.LayoutState = new State();
    }

    public Size Measure(IDataGridLayoutContext context, Size availableSize)
    {
        int itemCount = context.ItemCount;
        if (itemCount <= 0)
        {
            RecycleOutsideRange(context, 0, -1);
            context.LayoutOrigin = default;
            return default;
        }

        State state = GetState(context);
        DataGridLayoutOrientation flowOrientation = _model.Orientation;
        double availableU = GetU(availableSize, flowOrientation);
        if (!IsFinitePositive(availableU))
        {
            availableU = GetU(context.RealizationRect.Size, flowOrientation);
        }

        Size baseCell = ResolveBaseCellSize(context, availableSize, state);
        double baseU = Math.Max(1, GetU(baseCell, flowOrientation));
        double baseV = Math.Max(1, GetV(baseCell, flowOrientation));
        double spacingU = Math.Max(0, GetSpacingU(flowOrientation));
        double spacingV = Math.Max(0, GetSpacingV(flowOrientation));
        int maximum = _model.MaximumRowsOrColumns <= 0 ? int.MaxValue : _model.MaximumRowsOrColumns;
        int itemsPerLine = IsFinitePositive(availableU)
            ? Math.Max(1, (int)Math.Floor((availableU + spacingU) / (baseU + spacingU)))
            : Math.Min(itemCount, maximum);
        itemsPerLine = Math.Min(itemsPerLine, maximum);

        double cellU = baseU;
        double cellV = baseV;
        if (IsFinitePositive(availableU) && _model.ItemsStretch != DataGridUniformGridItemsStretch.None)
        {
            double stretchedU = Math.Max(1, (availableU - (spacingU * (itemsPerLine - 1))) / itemsPerLine);
            if (_model.ItemsStretch == DataGridUniformGridItemsStretch.Uniform)
            {
                cellV = Math.Max(1, baseV * (stretchedU / baseU));
            }
            cellU = stretchedU;
        }

        double lineAdvance = cellV + spacingV;
        int lineCount = (itemCount + itemsPerLine - 1) / itemsPerLine;
        double realizationStart = Math.Max(0, GetVStart(context.RealizationRect, flowOrientation));
        double realizationEnd = Math.Max(realizationStart, GetVEnd(context.RealizationRect, flowOrientation));
        int firstLine = Math.Min(lineCount - 1, Math.Max(0, (int)Math.Floor(realizationStart / lineAdvance)));
        int lastLine = Math.Min(lineCount - 1, Math.Max(firstLine, (int)Math.Floor(Math.Max(0, realizationEnd - 0.001) / lineAdvance)));
        int firstIndex = firstLine * itemsPerLine;
        int lastIndex = Math.Min(itemCount - 1, ((lastLine + 1) * itemsPerLine) - 1);
        Size measureSize = ToSize(cellU, cellV, flowOrientation);

        for (int line = firstLine; line <= lastLine; line++)
        {
            int lineStartIndex = line * itemsPerLine;
            int lineItemCount = Math.Min(itemsPerLine, itemCount - lineStartIndex);
            GetLineAlignment(availableU, cellU, spacingU, lineItemCount, out double lineStartU, out double effectiveSpacingU);
            double v = line * lineAdvance;

            for (int lineIndex = 0; lineIndex < lineItemCount; lineIndex++)
            {
                int itemIndex = lineStartIndex + lineIndex;
                Control element = context.GetOrCreateElementAt(itemIndex);
                element.Measure(measureSize);
                double u = lineStartU + (lineIndex * (cellU + effectiveSpacingU));
                context.SetLayoutBounds(itemIndex, ToRect(u, v, cellU, cellV, flowOrientation));
            }
        }

        RecycleOutsideRange(context, firstIndex, lastIndex);
        context.LayoutOrigin = default;
        state.CellSize = ToSize(cellU, cellV, flowOrientation);
        state.ItemsPerLine = itemsPerLine;

        double extentU = IsFinitePositive(availableU)
            ? availableU
            : (Math.Min(itemsPerLine, itemCount) * cellU) + (Math.Max(0, Math.Min(itemsPerLine, itemCount) - 1) * spacingU);
        double extentV = (lineCount * cellV) + (Math.Max(0, lineCount - 1) * spacingV);
        return ToSize(extentU, extentV, flowOrientation);
    }

    public Size Arrange(IDataGridLayoutContext context, Size finalSize)
    {
        Vector offset = context.ScrollOffset;
        IReadOnlyList<Control> realized = context.RealizedElements;
        for (int index = 0; index < realized.Count; index++)
        {
            Control element = realized[index];
            int itemIndex = context.GetElementIndex(element);
            if (itemIndex >= 0 && context.TryGetLayoutBounds(itemIndex, out Rect bounds))
            {
                element.Arrange(new Rect(bounds.X - offset.X, bounds.Y - offset.Y, bounds.Width, bounds.Height));
            }
        }
        return finalSize;
    }

    public void OnItemsChanged(IDataGridLayoutContext context, NotifyCollectionChangedEventArgs change)
    {
        context.LayoutState = new State();
    }

    public void Uninitialize(IDataGridLayoutContext context)
    {
    }

    private Size ResolveBaseCellSize(IDataGridLayoutContext context, Size availableSize, State state)
    {
        bool needsWidth = !IsFinitePositive(_model.MinItemWidth);
        bool needsHeight = !IsFinitePositive(_model.MinItemHeight);
        Size measured = state.NaturalCellSize;

        if ((needsWidth || needsHeight) && (measured.Width <= 0 || measured.Height <= 0))
        {
            Control first = context.GetOrCreateElementAt(0);
            first.Measure(availableSize);
            measured = first.DesiredSize;
            state.NaturalCellSize = measured;
        }

        double width = needsWidth ? Math.Max(1, measured.Width) : _model.MinItemWidth;
        double height = needsHeight ? Math.Max(1, measured.Height) : _model.MinItemHeight;
        return new Size(width, height);
    }

    private void GetLineAlignment(
        double availableU,
        double cellU,
        double minimumSpacing,
        int itemCount,
        out double start,
        out double spacing)
    {
        spacing = minimumSpacing;
        start = 0;
        if (!IsFinitePositive(availableU) || itemCount <= 0)
        {
            return;
        }

        double used = (itemCount * cellU) + (Math.Max(0, itemCount - 1) * minimumSpacing);
        double remaining = Math.Max(0, availableU - used);
        switch (_model.ItemsJustification)
        {
            case DataGridUniformGridItemsJustification.Center:
                start = remaining / 2;
                break;
            case DataGridUniformGridItemsJustification.End:
                start = remaining;
                break;
            case DataGridUniformGridItemsJustification.SpaceAround:
                spacing += remaining / itemCount;
                start = (spacing - minimumSpacing) / 2;
                break;
            case DataGridUniformGridItemsJustification.SpaceBetween when itemCount > 1:
                spacing += remaining / (itemCount - 1);
                break;
            case DataGridUniformGridItemsJustification.SpaceEvenly:
                double addition = remaining / (itemCount + 1);
                spacing += addition;
                start = addition;
                break;
        }
    }

    private double GetSpacingU(DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Horizontal ? _model.MinColumnSpacing : _model.MinRowSpacing;
    }

    private double GetSpacingV(DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Horizontal ? _model.MinRowSpacing : _model.MinColumnSpacing;
    }

    private static State GetState(IDataGridLayoutContext context)
    {
        if (context.LayoutState is State state)
        {
            return state;
        }
        state = new State();
        context.LayoutState = state;
        return state;
    }

    private static void RecycleOutsideRange(IDataGridLayoutContext context, int firstIndex, int lastIndex)
    {
        IReadOnlyList<Control> realized = context.RealizedElements;
        for (int index = realized.Count - 1; index >= 0; index--)
        {
            Control element = realized[index];
            int itemIndex = context.GetElementIndex(element);
            if (itemIndex < firstIndex || itemIndex > lastIndex)
            {
                context.RecycleElement(element);
            }
        }
    }

    private static double GetU(Size size, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? size.Width : size.Height;

    private static double GetV(Size size, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? size.Height : size.Width;

    private static double GetVStart(Rect rect, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? rect.Y : rect.X;

    private static double GetVEnd(Rect rect, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? rect.Bottom : rect.Right;

    private static Size ToSize(double u, double v, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? new Size(u, v) : new Size(v, u);

    private static Rect ToRect(double u, double v, double sizeU, double sizeV, DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal
            ? new Rect(u, v, sizeU, sizeV)
            : new Rect(v, u, sizeV, sizeU);

    private static bool IsFinitePositive(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private sealed class State
    {
        public Size NaturalCellSize { get; set; }
        public Size CellSize { get; set; }
        public int ItemsPerLine { get; set; }
    }
}
