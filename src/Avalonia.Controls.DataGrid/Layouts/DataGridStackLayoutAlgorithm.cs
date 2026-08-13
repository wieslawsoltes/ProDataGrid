// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridLayouts;

internal sealed class DataGridStackLayoutAlgorithm : IDataGridLayoutAlgorithm
{
    private readonly IDataGridLayoutModel _model;
    private readonly bool _forceNonVirtualizing;

    public DataGridStackLayoutAlgorithm(
        DataGridStackLayoutModel model,
        bool forceNonVirtualizing)
    {
        _model = model;
        _forceNonVirtualizing = forceNonVirtualizing;
    }

    public DataGridStackLayoutAlgorithm(DataGridNonVirtualizingStackLayoutModel model)
    {
        _model = model;
        _forceNonVirtualizing = true;
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
        DataGridLayoutOrientation orientation = GetOrientation();
        double spacing = Math.Max(0, GetSpacing());
        bool virtualizing = !_forceNonVirtualizing && !GetDisableVirtualization();
        double realizationStart = virtualizing ? GetMajorStart(context.RealizationRect, orientation) : 0;
        double realizationEnd = virtualizing ? GetMajorEnd(context.RealizationRect, orientation) : double.PositiveInfinity;
        realizationStart = Math.Max(0, realizationStart);

        int recommendedAnchor = context.RecommendedAnchorIndex;
        int firstIndex = virtualizing && recommendedAnchor >= 0
            ? Math.Min(itemCount - 1, recommendedAnchor)
            : virtualizing
                ? FindFirstRealizedIndex(context, orientation, spacing, realizationStart, itemCount)
                : 0;
        double majorPosition = GetEstimatedOffset(context, firstIndex, orientation, spacing);
        if (virtualizing && recommendedAnchor >= 0)
        {
            realizationEnd = majorPosition + Math.Max(1, GetMajor(context.RealizationRect.Size, orientation));
        }
        int lastIndex = firstIndex - 1;
        double maxMinor = state.MaxMinor;
        Size measureConstraint = GetMeasureConstraint(availableSize, orientation);

        for (int index = firstIndex; index < itemCount; index++)
        {
            Control element = context.GetOrCreateElementAt(index);
            element.Measure(measureConstraint);

            Size desiredSize = element.DesiredSize;
            double major = Math.Max(0, GetMajor(desiredSize, orientation));
            double minor = Math.Max(0, GetMinor(desiredSize, orientation));
            double availableMinor = GetMinor(availableSize, orientation);
            if (IsFinite(availableMinor))
            {
                minor = Math.Max(minor, availableMinor);
            }

            context.SetLayoutBounds(index, CreateRect(majorPosition, 0, major, minor, orientation));
            maxMinor = Math.Max(maxMinor, minor);
            lastIndex = index;
            majorPosition += major;

            if (virtualizing && majorPosition >= realizationEnd)
            {
                break;
            }

            majorPosition += spacing;
        }

        state.MaxMinor = maxMinor;
        RecycleOutsideRange(context, firstIndex, lastIndex);
        context.LayoutOrigin = default;

        double estimatedMajor = GetEstimatedOffset(context, itemCount, orientation, spacing);
        if (itemCount > 0)
        {
            estimatedMajor -= spacing;
        }
        estimatedMajor = Math.Max(majorPosition, estimatedMajor);

        return CreateSize(estimatedMajor, maxMinor, orientation);
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
                element.Arrange(new Rect(
                    bounds.X - offset.X,
                    bounds.Y - offset.Y,
                    bounds.Width,
                    bounds.Height));
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

    private DataGridLayoutOrientation GetOrientation()
    {
        return _model switch
        {
            DataGridStackLayoutModel model => model.Orientation,
            DataGridNonVirtualizingStackLayoutModel model => model.Orientation,
            _ => DataGridLayoutOrientation.Vertical
        };
    }

    private double GetSpacing()
    {
        return _model switch
        {
            DataGridStackLayoutModel model => model.Spacing,
            DataGridNonVirtualizingStackLayoutModel model => model.Spacing,
            _ => 0
        };
    }

    private bool GetDisableVirtualization()
    {
        return _model is DataGridStackLayoutModel { DisableVirtualization: true };
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

    private static int FindFirstRealizedIndex(
        IDataGridLayoutContext context,
        DataGridLayoutOrientation orientation,
        double spacing,
        double realizationStart,
        int itemCount)
    {
        int low = 0;
        int high = itemCount;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            double offset = GetEstimatedOffset(context, middle, orientation, spacing);
            Size estimatedSize = context.GetEstimatedItemSize(middle);
            double end = offset + Math.Max(1, GetMajor(estimatedSize, orientation));
            if (end <= realizationStart)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return Math.Max(0, Math.Min(itemCount - 1, low));
    }

    private static double GetEstimatedOffset(
        IDataGridLayoutContext context,
        int index,
        DataGridLayoutOrientation orientation,
        double spacing)
    {
        return Math.Max(0, context.GetEstimatedItemOffset(index, orientation)) + (spacing * index);
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

    private static Size GetMeasureConstraint(Size availableSize, DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical
            ? new Size(availableSize.Width, double.PositiveInfinity)
            : new Size(double.PositiveInfinity, availableSize.Height);
    }

    private static double GetMajor(Size size, DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical ? size.Height : size.Width;
    }

    private static double GetMinor(Size size, DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical ? size.Width : size.Height;
    }

    private static double GetMajorStart(Rect rect, DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical ? rect.Y : rect.X;
    }

    private static double GetMajorEnd(Rect rect, DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical ? rect.Bottom : rect.Right;
    }

    private static Rect CreateRect(
        double majorStart,
        double minorStart,
        double majorSize,
        double minorSize,
        DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical
            ? new Rect(minorStart, majorStart, minorSize, majorSize)
            : new Rect(majorStart, minorStart, majorSize, minorSize);
    }

    private static Size CreateSize(
        double majorSize,
        double minorSize,
        DataGridLayoutOrientation orientation)
    {
        return orientation == DataGridLayoutOrientation.Vertical
            ? new Size(minorSize, majorSize)
            : new Size(majorSize, minorSize);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private sealed class State
    {
        public double MaxMinor { get; set; }
    }
}
