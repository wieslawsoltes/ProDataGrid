// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridLayouts;

internal sealed class DataGridStackLayoutAlgorithm : IDataGridLayoutAlgorithm, IDataGridLayoutNavigation
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

    public bool SupportsNavigation(DataGridLayoutNavigationDirection direction)
    {
        DataGridLayoutOrientation orientation = GetOrientation();
        return direction is DataGridLayoutNavigationDirection.PageUp or
            DataGridLayoutNavigationDirection.PageDown or
            DataGridLayoutNavigationDirection.First or
            DataGridLayoutNavigationDirection.Last ||
            orientation == DataGridLayoutOrientation.Vertical &&
                direction is DataGridLayoutNavigationDirection.Up or DataGridLayoutNavigationDirection.Down ||
            orientation == DataGridLayoutOrientation.Horizontal &&
                direction is DataGridLayoutNavigationDirection.Left or DataGridLayoutNavigationDirection.Right;
    }

    public bool TryGetNavigationBounds(
        IDataGridLayoutContext context,
        int itemIndex,
        Rect viewport,
        out Rect bounds)
    {
        if (itemIndex < 0 || itemIndex >= context.ItemCount)
        {
            bounds = default;
            return false;
        }

        bounds = GetEstimatedBounds(
            context,
            itemIndex,
            GetOrientation(),
            Math.Max(0, GetSpacing()));
        return true;
    }

    public bool TryResolveNavigation(
        IDataGridLayoutContext context,
        in DataGridLayoutNavigationRequest request,
        out DataGridLayoutNavigationResult result)
    {
        result = default;
        if (!SupportsNavigation(request.Direction))
        {
            return false;
        }
        int itemCount = context.ItemCount;
        int current = request.CurrentItemIndex;
        if (current < 0 || current >= itemCount)
        {
            return false;
        }

        DataGridLayoutOrientation orientation = GetOrientation();
        int target;
        switch (request.Direction)
        {
            case DataGridLayoutNavigationDirection.Up when orientation == DataGridLayoutOrientation.Vertical:
            case DataGridLayoutNavigationDirection.Left when orientation == DataGridLayoutOrientation.Horizontal:
                target = current - 1;
                break;
            case DataGridLayoutNavigationDirection.Down when orientation == DataGridLayoutOrientation.Vertical:
            case DataGridLayoutNavigationDirection.Right when orientation == DataGridLayoutOrientation.Horizontal:
                target = current + 1;
                break;
            case DataGridLayoutNavigationDirection.PageUp:
            case DataGridLayoutNavigationDirection.PageDown:
                target = FindPageTarget(context, request, orientation);
                break;
            case DataGridLayoutNavigationDirection.First:
                target = 0;
                break;
            case DataGridLayoutNavigationDirection.Last:
                target = itemCount - 1;
                break;
            default:
                return false;
        }

        if (target < 0 || target >= itemCount || target == current)
        {
            return false;
        }

        result = new DataGridLayoutNavigationResult(
            target,
            GetEstimatedBounds(context, target, orientation, Math.Max(0, GetSpacing())));
        return true;
    }

    private int FindPageTarget(
        IDataGridLayoutContext context,
        in DataGridLayoutNavigationRequest request,
        DataGridLayoutOrientation orientation)
    {
        double spacing = Math.Max(0, GetSpacing());
        Rect currentBounds = context.TryGetLayoutBounds(request.CurrentItemIndex, out Rect exactBounds)
            ? exactBounds
            : GetEstimatedBounds(context, request.CurrentItemIndex, orientation, spacing);
        double viewportMajor = GetMajor(request.Viewport.Size, orientation);
        if (!IsFinite(viewportMajor) || viewportMajor <= 0)
        {
            viewportMajor = Math.Max(1, GetMajor(currentBounds.Size, orientation));
        }

        bool forward = request.Direction == DataGridLayoutNavigationDirection.PageDown;
        double coordinate = Math.Max(0, GetMajorStart(currentBounds, orientation) + (forward ? viewportMajor : -viewportMajor));
        int low = 0;
        int high = context.ItemCount - 1;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            Rect bounds = GetEstimatedBounds(context, middle, orientation, spacing);
            double end = GetMajorStart(bounds, orientation) + Math.Max(1, GetMajor(bounds.Size, orientation));
            if (end < coordinate)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        if (low == request.CurrentItemIndex)
        {
            low += forward ? 1 : -1;
        }
        return Math.Max(0, Math.Min(context.ItemCount - 1, low));
    }

    private static Rect GetEstimatedBounds(
        IDataGridLayoutContext context,
        int index,
        DataGridLayoutOrientation orientation,
        double spacing)
    {
        if (context.TryGetLayoutBounds(index, out Rect bounds))
        {
            return bounds;
        }

        Size estimated = context.GetEstimatedItemSize(index);
        double major = Math.Max(1, GetMajor(estimated, orientation));
        double minor = Math.Max(1, GetMinor(estimated, orientation));
        double offset = GetEstimatedOffset(context, index, orientation, spacing);
        return CreateRect(offset, 0, major, minor, orientation);
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
