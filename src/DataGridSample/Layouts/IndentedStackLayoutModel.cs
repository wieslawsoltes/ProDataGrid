// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridLayouts;

namespace DataGridSample.Layouts;

/// <summary>
/// Sample custom layout that virtualizes a vertical stack and alternates its horizontal indent.
/// </summary>
public sealed class IndentedStackLayoutModel : DataGridLayoutModelBase
{
    private double _indent = 36;
    private double _spacing = 6;

    public double Indent
    {
        get => _indent;
        set => SetProperty(ref _indent, value);
    }

    public double Spacing
    {
        get => _spacing;
        set => SetProperty(ref _spacing, value);
    }

    public override IDataGridLayoutAlgorithm CreateAlgorithm() => new Algorithm(this);

    private sealed class Algorithm : IDataGridLayoutAlgorithm, IDataGridLayoutNavigation
    {
        private readonly IndentedStackLayoutModel _model;

        public Algorithm(IndentedStackLayoutModel model)
        {
            _model = model;
        }

        public void Initialize(IDataGridLayoutContext context)
        {
        }

        public Size Measure(IDataGridLayoutContext context, Size availableSize)
        {
            int itemCount = context.ItemCount;
            if (itemCount == 0)
            {
                RecycleOutsideRange(context, 0, -1);
                return default;
            }

            double spacing = Math.Max(0, _model.Spacing);
            double start = Math.Max(0, context.RealizationRect.Y);
            double end = Math.Max(start, context.RealizationRect.Bottom);
            int firstIndex = FindFirstIndex(context, itemCount, start, spacing);
            double y = GetOffset(context, firstIndex, spacing);
            int lastIndex = firstIndex - 1;
            double width = IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : 0;
            double indent = Math.Max(0, _model.Indent);

            for (int index = firstIndex; index < itemCount && y <= end; index++)
            {
                double x = (index & 1) == 0 ? 0 : indent;
                double itemWidth = Math.Max(1, width - x);
                Control element = context.GetOrCreateElementAt(index);
                element.Measure(new Size(itemWidth, double.PositiveInfinity));
                double height = Math.Max(1, element.DesiredSize.Height);
                context.SetLayoutBounds(index, new Rect(x, y, itemWidth, height));
                y += height + spacing;
                lastIndex = index;
            }

            RecycleOutsideRange(context, firstIndex, lastIndex);
            double extentHeight = Math.Max(y - spacing, GetOffset(context, itemCount, spacing) - spacing);
            return new Size(width, Math.Max(0, extentHeight));
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
        }

        public void Uninitialize(IDataGridLayoutContext context)
        {
        }

        public bool SupportsNavigation(DataGridLayoutNavigationDirection direction) =>
            direction is DataGridLayoutNavigationDirection.Up or
                DataGridLayoutNavigationDirection.Down or
                DataGridLayoutNavigationDirection.PageUp or
                DataGridLayoutNavigationDirection.PageDown or
                DataGridLayoutNavigationDirection.First or
                DataGridLayoutNavigationDirection.Last;

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

            double spacing = Math.Max(0, _model.Spacing);
            Size estimate = context.GetEstimatedItemSize(itemIndex);
            double x = (itemIndex & 1) == 0 ? 0 : Math.Max(0, _model.Indent);
            bounds = new Rect(
                x,
                GetOffset(context, itemIndex, spacing),
                Math.Max(1, estimate.Width - x),
                Math.Max(1, estimate.Height));
            return true;
        }

        public bool TryResolveNavigation(
            IDataGridLayoutContext context,
            in DataGridLayoutNavigationRequest request,
            out DataGridLayoutNavigationResult result)
        {
            if (!SupportsNavigation(request.Direction))
            {
                result = default;
                return false;
            }

            int target = request.Direction switch
            {
                DataGridLayoutNavigationDirection.Up => request.CurrentItemIndex - 1,
                DataGridLayoutNavigationDirection.Down => request.CurrentItemIndex + 1,
                DataGridLayoutNavigationDirection.PageUp => FindPageTarget(context, request, forward: false),
                DataGridLayoutNavigationDirection.PageDown => FindPageTarget(context, request, forward: true),
                DataGridLayoutNavigationDirection.First => 0,
                DataGridLayoutNavigationDirection.Last => context.ItemCount - 1,
                _ => -1
            };
            if (target < 0 || target >= context.ItemCount || target == request.CurrentItemIndex)
            {
                result = default;
                return false;
            }

            TryGetNavigationBounds(context, target, request.Viewport, out Rect bounds);
            result = new DataGridLayoutNavigationResult(target, bounds);
            return true;
        }

        private static int FindPageTarget(
            IDataGridLayoutContext context,
            in DataGridLayoutNavigationRequest request,
            bool forward)
        {
            double rowHeight = Math.Max(1, context.GetEstimatedItemSize(request.CurrentItemIndex).Height);
            int rows = Math.Max(1, (int)Math.Floor(request.Viewport.Height / rowHeight));
            return request.CurrentItemIndex + (forward ? rows : -rows);
        }

        private static int FindFirstIndex(
            IDataGridLayoutContext context,
            int itemCount,
            double realizationStart,
            double spacing)
        {
            int low = 0;
            int high = itemCount;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                double end = GetOffset(context, middle, spacing) +
                    Math.Max(1, context.GetEstimatedItemSize(middle).Height);
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

        private static double GetOffset(IDataGridLayoutContext context, int index, double spacing) =>
            Math.Max(0, context.GetEstimatedItemOffset(index, DataGridLayoutOrientation.Vertical)) + (spacing * index);

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

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
