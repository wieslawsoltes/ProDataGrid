// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridLayouts;

internal sealed class DataGridWrapLayoutAlgorithm : IDataGridLayoutAlgorithm
{
    private readonly DataGridWrapLayoutModel _model;

    public DataGridWrapLayoutAlgorithm(DataGridWrapLayoutModel model)
    {
        _model = model;
    }

    public void Initialize(IDataGridLayoutContext context)
    {
        context.LayoutState ??= new State();
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
        DataGridLayoutOrientation orientation = _model.Orientation;
        double spacingU = Math.Max(0, GetSpacingU(orientation));
        double spacingV = Math.Max(0, GetSpacingV(orientation));
        double availableU = GetU(availableSize, orientation);
        if (!IsFinitePositive(availableU))
        {
            availableU = GetU(context.RealizationRect.Size, orientation);
        }
        if (!IsFinitePositive(availableU))
        {
            availableU = Math.Max(1, GetU(context.GetEstimatedItemSize(0), orientation));
        }

        state.EnsureParameters(orientation, availableU, spacingU, spacingV);
        int estimateIndex = Math.Max(0, Math.Min(itemCount - 1, context.RecommendedAnchorIndex));
        state.EnsureEstimates(context.GetEstimatedItemSize(estimateIndex), orientation);

        double realizationStart = Math.Max(0, GetVStart(context.RealizationRect, orientation));
        double realizationEnd = Math.Max(realizationStart, GetVEnd(context.RealizationRect, orientation));
        Anchor anchor = state.FindAnchor(realizationStart, context.RecommendedAnchorIndex);
        if (!anchor.IsValid)
        {
            anchor = context.RecommendedAnchorIndex >= 0
                ? state.EstimateAnchorForIndex(context.RecommendedAnchorIndex, itemCount, availableU, spacingU, spacingV)
                : state.EstimateAnchor(realizationStart, itemCount, availableU, spacingU, spacingV);
        }
        if (context.RecommendedAnchorIndex >= 0)
        {
            realizationEnd = anchor.V + Math.Max(1, GetV(context.RealizationRect.Size, orientation));
        }

        int firstIndex = Math.Max(0, Math.Min(itemCount - 1, anchor.ItemIndex));
        int index = firstIndex;
        int lastIndex = firstIndex - 1;
        double v = Math.Max(0, anchor.V);
        double measuredEndV = v;
        double maximumUsedU = state.MaximumUsedU;
        Size measureConstraint = ToSize(availableU, double.PositiveInfinity, orientation);

        while (index < itemCount)
        {
            int lineStartIndex = index;
            double u = 0;
            double lineV = 0;
            int lineItemCount = 0;

            while (index < itemCount)
            {
                Control element = context.GetOrCreateElementAt(index);
                element.Measure(measureConstraint);
                Size desired = element.DesiredSize;
                double itemU = Math.Max(1, GetU(desired, orientation));
                double itemV = Math.Max(1, GetV(desired, orientation));

                if (lineItemCount > 0 && u + itemU > availableU)
                {
                    break;
                }

                context.SetLayoutBounds(index, ToRect(u, v, itemU, itemV, orientation));
                state.RecordItem(itemU, itemV);
                lineV = Math.Max(lineV, itemV);
                u += itemU;
                lineItemCount++;
                lastIndex = index;
                index++;

                if (index < itemCount)
                {
                    u += spacingU;
                }
            }

            maximumUsedU = Math.Max(maximumUsedU, Math.Max(0, u - (index < itemCount ? spacingU : 0)));
            state.RecordLine(new LineInfo(lineStartIndex, lastIndex, v, lineV, lineItemCount), Math.Max(1, _model.MaximumCachedLines));
            measuredEndV = v + lineV;
            if (measuredEndV >= realizationEnd || index >= itemCount)
            {
                break;
            }

            v = measuredEndV + spacingV;
        }

        state.MaximumUsedU = maximumUsedU;
        RecycleOutsideRange(context, firstIndex, lastIndex);
        context.LayoutOrigin = default;

        double extentV;
        if (lastIndex >= itemCount - 1)
        {
            extentV = measuredEndV;
        }
        else
        {
            double averageItemsPerLine = Math.Max(1, state.AverageItemsPerLine);
            int estimatedLineCount = Math.Max(1, (int)Math.Ceiling(itemCount / averageItemsPerLine));
            extentV = (estimatedLineCount * Math.Max(1, state.AverageLineV)) +
                (Math.Max(0, estimatedLineCount - 1) * spacingV);
            extentV = Math.Max(extentV, measuredEndV);
        }

        return ToSize(Math.Max(availableU, maximumUsedU), extentV, orientation);
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

    private double GetSpacingU(DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? _model.HorizontalSpacing : _model.VerticalSpacing;

    private double GetSpacingV(DataGridLayoutOrientation orientation) =>
        orientation == DataGridLayoutOrientation.Horizontal ? _model.VerticalSpacing : _model.HorizontalSpacing;

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

    private readonly struct Anchor
    {
        public Anchor(int itemIndex, double v)
        {
            ItemIndex = itemIndex;
            V = v;
            IsValid = itemIndex >= 0 && !double.IsNaN(v);
        }

        public int ItemIndex { get; }
        public double V { get; }
        public bool IsValid { get; }
    }

    private readonly struct LineInfo
    {
        public LineInfo(int firstIndex, int lastIndex, double v, double sizeV, int itemCount)
        {
            FirstIndex = firstIndex;
            LastIndex = lastIndex;
            V = v;
            SizeV = sizeV;
            ItemCount = itemCount;
        }

        public int FirstIndex { get; }
        public int LastIndex { get; }
        public double V { get; }
        public double SizeV { get; }
        public int ItemCount { get; }
    }

    private sealed class State
    {
        private readonly List<LineInfo> _lines = new();
        private DataGridLayoutOrientation _orientation;
        private double _availableU = double.NaN;
        private double _spacingU = double.NaN;
        private double _spacingV = double.NaN;
        private int _itemSamples;
        private int _lineSamples;

        public double AverageItemU { get; private set; }
        public double AverageItemV { get; private set; }
        public double AverageLineV { get; private set; }
        public double AverageItemsPerLine { get; private set; }
        public double MaximumUsedU { get; set; }

        public void EnsureParameters(
            DataGridLayoutOrientation orientation,
            double availableU,
            double spacingU,
            double spacingV)
        {
            if (_orientation != orientation ||
                !AreClose(_availableU, availableU) ||
                !AreClose(_spacingU, spacingU) ||
                !AreClose(_spacingV, spacingV))
            {
                _lines.Clear();
                MaximumUsedU = 0;
                _orientation = orientation;
                _availableU = availableU;
                _spacingU = spacingU;
                _spacingV = spacingV;
            }
        }

        public void EnsureEstimates(Size estimate, DataGridLayoutOrientation orientation)
        {
            if (AverageItemU <= 0)
            {
                AverageItemU = Math.Max(1, GetU(estimate, orientation));
            }
            if (AverageItemV <= 0)
            {
                AverageItemV = Math.Max(1, GetV(estimate, orientation));
            }
            if (AverageLineV <= 0)
            {
                AverageLineV = AverageItemV;
            }
        }

        public Anchor FindAnchor(double realizationStart, int recommendedAnchorIndex)
        {
            LineInfo? best = null;
            for (int index = 0; index < _lines.Count; index++)
            {
                LineInfo line = _lines[index];
                if (recommendedAnchorIndex >= line.FirstIndex && recommendedAnchorIndex <= line.LastIndex)
                {
                    best = line;
                    break;
                }
                if (line.V <= realizationStart && (!best.HasValue || line.V > best.Value.V))
                {
                    best = line;
                }
                else if (!best.HasValue && line.V > realizationStart)
                {
                    best = line;
                }
            }
            return best.HasValue ? new Anchor(best.Value.FirstIndex, best.Value.V) : default;
        }

        public Anchor EstimateAnchor(
            double realizationStart,
            int itemCount,
            double availableU,
            double spacingU,
            double spacingV)
        {
            int itemsPerLine = Math.Max(1, (int)Math.Floor((availableU + spacingU) / (Math.Max(1, AverageItemU) + spacingU)));
            double lineAdvance = Math.Max(1, AverageLineV) + spacingV;
            int line = Math.Max(0, (int)Math.Floor(realizationStart / lineAdvance));
            int itemIndex = Math.Min(itemCount - 1, line * itemsPerLine);
            return new Anchor(itemIndex, line * lineAdvance);
        }

        public Anchor EstimateAnchorForIndex(
            int recommendedIndex,
            int itemCount,
            double availableU,
            double spacingU,
            double spacingV)
        {
            int itemsPerLine = Math.Max(1, (int)Math.Floor((availableU + spacingU) / (Math.Max(1, AverageItemU) + spacingU)));
            int line = Math.Max(0, Math.Min(itemCount - 1, recommendedIndex)) / itemsPerLine;
            return new Anchor(line * itemsPerLine, line * (Math.Max(1, AverageLineV) + spacingV));
        }

        public void RecordItem(double u, double v)
        {
            _itemSamples++;
            double weight = _itemSamples <= 256 ? 1d / _itemSamples : 1d / 256;
            AverageItemU += (u - AverageItemU) * weight;
            AverageItemV += (v - AverageItemV) * weight;
        }

        public void RecordLine(LineInfo line, int maximumCachedLines)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].FirstIndex == line.FirstIndex)
                {
                    _lines[index] = line;
                    UpdateLineAverages(line);
                    return;
                }
            }

            _lines.Add(line);
            while (_lines.Count > maximumCachedLines)
            {
                _lines.RemoveAt(0);
            }
            UpdateLineAverages(line);
        }

        private void UpdateLineAverages(LineInfo line)
        {
            _lineSamples++;
            double weight = _lineSamples <= 256 ? 1d / _lineSamples : 1d / 256;
            AverageLineV += (line.SizeV - AverageLineV) * weight;
            AverageItemsPerLine += (line.ItemCount - AverageItemsPerLine) * weight;
        }

        private static bool AreClose(double first, double second) => Math.Abs(first - second) < 0.01;
    }
}
