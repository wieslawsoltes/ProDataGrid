// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridLayouts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layouts;

public class DataGridStackLayoutModelTests
{
    [Fact]
    public void Defaults_match_virtualized_vertical_list_behavior()
    {
        var model = new DataGridStackLayoutModel();

        Assert.Equal(DataGridLayoutOrientation.Vertical, model.Orientation);
        Assert.Equal(0, model.Spacing);
        Assert.False(model.DisableVirtualization);
    }

    [Fact]
    public void Orientation_change_requests_state_reset()
    {
        var model = new DataGridStackLayoutModel();
        DataGridLayoutInvalidationKind? kind = null;
        model.LayoutInvalidated += (_, e) => kind = e.Kind;

        model.Orientation = DataGridLayoutOrientation.Horizontal;

        Assert.Equal(DataGridLayoutInvalidationKind.Reset, kind);
    }

    [Fact]
    public void Vertical_stack_realizes_only_the_window_and_uses_estimated_extent()
    {
        var model = new DataGridStackLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestLayoutContext(
            itemCount: 100,
            itemSize: new Size(120, 20),
            realizationRect: new Rect(0, 200, 120, 100),
            scrollOffset: new Vector(0, 200));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(120, 100));
        algorithm.Arrange(context, new Size(120, 100));

        Assert.Equal(120, extent.Width);
        Assert.Equal(2000, extent.Height);
        Assert.Equal(10, context.RealizedIndices.Min());
        Assert.Equal(14, context.RealizedIndices.Max());
        Assert.Equal(5, context.RealizedIndices.Count);
        Assert.Equal(0, context.GetBounds(10).Y);
    }

    [Fact]
    public void Spacing_participates_in_anchor_and_extent_math()
    {
        var model = new DataGridStackLayoutModel { Spacing = 5 };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestLayoutContext(
            itemCount: 10,
            itemSize: new Size(100, 20),
            realizationRect: new Rect(0, 50, 100, 25),
            scrollOffset: new Vector(0, 50));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(100, 25));

        Assert.Equal(245, extent.Height);
        Assert.Equal(2, context.RealizedIndices.Min());
    }

    [Fact]
    public void Horizontal_stack_uses_width_as_the_major_axis()
    {
        var model = new DataGridStackLayoutModel
        {
            Orientation = DataGridLayoutOrientation.Horizontal
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestLayoutContext(
            itemCount: 20,
            itemSize: new Size(30, 40),
            realizationRect: new Rect(90, 0, 90, 40),
            scrollOffset: new Vector(90, 0));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(90, 40));

        Assert.Equal(600, extent.Width);
        Assert.Equal(40, extent.Height);
        Assert.Equal(3, context.RealizedIndices.Min());
        Assert.Equal(5, context.RealizedIndices.Max());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Non_virtualizing_modes_realize_every_item(bool useDisableVirtualization)
    {
        IDataGridLayoutModel model = useDisableVirtualization
            ? new DataGridStackLayoutModel { DisableVirtualization = true }
            : new DataGridNonVirtualizingStackLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestLayoutContext(
            itemCount: 12,
            itemSize: new Size(80, 10),
            realizationRect: new Rect(0, 40, 80, 10),
            scrollOffset: new Vector(0, 40));
        algorithm.Initialize(context);

        algorithm.Measure(context, new Size(80, 10));

        Assert.Equal(Enumerable.Range(0, 12), context.RealizedIndices);
    }

    private sealed class TestLayoutContext : IDataGridLayoutContext
    {
        private readonly Size _itemSize;
        private readonly List<Control> _realized = new();
        private readonly Dictionary<int, FixedSizeControl> _elements = new();
        private readonly Dictionary<int, Rect> _bounds = new();

        public TestLayoutContext(int itemCount, Size itemSize, Rect realizationRect, Vector scrollOffset)
        {
            ItemCount = itemCount;
            _itemSize = itemSize;
            RealizationRect = realizationRect;
            ScrollOffset = scrollOffset;
        }

        public int ItemCount { get; }

        public Rect RealizationRect { get; }

        public Vector ScrollOffset { get; }

        public int RecommendedAnchorIndex => -1;

        public Point LayoutOrigin { get; set; }

        public object? LayoutState { get; set; }

        public IReadOnlyList<Control> RealizedElements => _realized;

        public IReadOnlyList<int> RealizedIndices => _elements.Keys.OrderBy(index => index).ToArray();

        public Control GetOrCreateElementAt(int index)
        {
            if (!_elements.TryGetValue(index, out FixedSizeControl? element))
            {
                element = new FixedSizeControl(_itemSize, index);
                _elements.Add(index, element);
                _realized.Add(element);
            }

            return element;
        }

        public void RecycleElement(Control element)
        {
            int index = GetElementIndex(element);
            if (index >= 0)
            {
                _elements.Remove(index);
                _bounds.Remove(index);
                _realized.Remove(element);
            }
        }

        public int GetElementIndex(Control element)
        {
            return element is FixedSizeControl fixedSize ? fixedSize.Index : -1;
        }

        public Size GetEstimatedItemSize(int index)
        {
            return _itemSize;
        }

        public double GetEstimatedItemOffset(int index, DataGridLayoutOrientation orientation)
        {
            return index * (orientation == DataGridLayoutOrientation.Vertical ? _itemSize.Height : _itemSize.Width);
        }

        public void SetLayoutBounds(int index, Rect bounds)
        {
            _bounds[index] = bounds;
        }

        public bool TryGetLayoutBounds(int index, out Rect bounds)
        {
            return _bounds.TryGetValue(index, out bounds);
        }

        public Rect GetBounds(int index)
        {
            return _elements[index].Bounds;
        }
    }

    private sealed class FixedSizeControl : Control
    {
        private readonly Size _size;

        public FixedSizeControl(Size size, int index)
        {
            _size = size;
            Index = index;
        }

        public int Index { get; }

        protected override Size MeasureOverride(Size availableSize)
        {
            return _size;
        }
    }
}
